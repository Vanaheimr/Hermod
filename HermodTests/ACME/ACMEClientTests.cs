/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#region Usings

using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Hermod.ACME;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.ACME
{

    /// <summary>
    /// The ACME client against <see cref="TestACMEServer"/>: an account, a
    /// certificate through http-01 from a Hermod HTTP server, a wildcard
    /// through dns-01, bad nonces, failed challenges, an external account
    /// binding, and the ARI identifier of RFC 9773.
    /// </summary>
    [TestFixture]
    public class ACMEClientTests
    {

        #region Data

        private TestACMEServer?            ca;
        private HTTPServer?                port80;
        private HTTP01ChallengeResponder?  http01;

        #endregion

        #region Setup / Teardown

        [SetUp]
        public async Task SetUp()
        {

            ca      = await TestACMEServer.StartAsync();

            // The tests' "port 80": where the certificate authority asks for
            // http-01 answers, with the name to be proven as the Host.
            port80  = new HTTPServer(
                          IPAddress:  IPv4Address.Localhost,
                          TCPPort:    IPPort.Zero,
                          AutoStart:  true
                      );

            http01  = new HTTP01ChallengeResponder();
            http01.RegisterAt(port80);

            ca.HTTP01Port = port80.TCPPort.ToUInt16();

        }

        [TearDown]
        public async Task TearDown()
        {
            if (port80 is not null)
                await port80.DisposeAsync();
            if (ca is not null)
                await ca.DisposeAsync();
        }

        #endregion

        #region (static) DNS01(Zone)

        /// <summary>
        /// A dns-01 responder that writes into the given zone.
        /// </summary>
        internal static DNS01ChallengeResponder DNS01(ConcurrentDictionary<String, ConcurrentDictionary<String, Byte>> Zone)

            => new (
                   Publish:   (name, value, ct) => {
                                  Zone.GetOrAdd(name, _ => new ())[value] = 0;
                                  return Task.CompletedTask;
                              },
                   Withdraw:  (name, value, ct) => {
                                  if (Zone.TryGetValue(name, out var values))
                                  {
                                      values.TryRemove(value, out _);
                                      if (values.IsEmpty)
                                          Zone.TryRemove(name, out _);
                                  }
                                  return Task.CompletedTask;
                              }
               );

        #endregion

        #region (private) NewClient(Key = null)

        private ACMEClient NewClient(ACMEAccountKey? Key = null)

            => new (ca!.DirectoryURL,
                    Key ?? ACMEAccountKey.Generate(),
                    PollInterval: TimeSpan.FromMilliseconds(100),
                    PollTimeout:  TimeSpan.FromSeconds(20));

        #endregion


        #region AnAccountIsOpenedOnce_AndFoundAgainByItsKey()

        [Test]
        public async Task AnAccountIsOpenedOnce_AndFoundAgainByItsKey()
        {

            using var key     = ACMEAccountKey.Generate();
            using var client  = NewClient(key);

            var opened  = await client.CreateAccountAsync(["admin@example.org"], TermsOfServiceAgreed: true);
            var again   = await client.CreateAccountAsync(["admin@example.org"], TermsOfServiceAgreed: true);

            Assert.That(opened.Created,   Is.True);
            Assert.That(opened.Contacts,  Is.EqualTo(new[] { "mailto:admin@example.org" }), "an address without a scheme gets 'mailto:'");
            Assert.That(again.Created,    Is.False);
            Assert.That(again.URL,        Is.EqualTo(opened.URL));

            // The key alone finds it: a new client of the same key, as after a restart.
            using var restarted = NewClient(ACMEAccountKey.FromPem(key.ToPem()));
            Assert.That((await restarted.FindAccountAsync())?.URL, Is.EqualTo(opened.URL));

            // And a key that has none finds none - without opening one.
            using var stranger = NewClient();
            Assert.That(await stranger.FindAccountAsync(), Is.Null);
            Assert.That(await stranger.FindAccountAsync(), Is.Null);

        }

        #endregion

        #region WithoutTheTermsOfServiceThereIsNoAccount()

        [Test]
        public async Task WithoutTheTermsOfServiceThereIsNoAccount()
        {

            using var client = NewClient();

            var e = await Assert.ThrowsAsync<ACMEException>(() => client.CreateAccountAsync(["admin@example.org"]));

            Assert.That(e!.Problem?.ShortType, Is.EqualTo("malformed"));
            Assert.That(e.Message,             Does.Contain("terms of service"));

        }

        #endregion

        #region ACertificateIsIssued_ThroughHTTP01_FromAHermodHTTPServer()

        [Test]
        public async Task ACertificateIsIssued_ThroughHTTP01_FromAHermodHTTPServer()
        {

            using var client = NewClient();
            await client.CreateAccountAsync(TermsOfServiceAgreed: true);

            var certificate = await client.ObtainCertificateAsync(["Time.Example.org", "ntp.example.org"], [ http01! ]);

            Assert.That(certificate.Identifiers.Select(identifier => identifier.Value),
                        Is.EquivalentTo(new[] { "time.example.org", "ntp.example.org" }));

            Assert.That(certificate.Certificate.HasPrivateKey,                       Is.True,  "the chain carries the new key");
            Assert.That(certificate.Certificate.GetECDsaPublicKey()?.KeySize,        Is.EqualTo(256));
            Assert.That(certificate.Certificate.Issuer,                              Is.EqualTo(ca!.Intermediate.Subject));
            Assert.That(certificate.Certificate.GetNameInfo(X509NameType.SimpleName, false), Is.EqualTo("time.example.org"));
            Assert.That(certificate.Chain.Intermediates.Count,                       Is.EqualTo(1), "the intermediate comes along, the root does not");
            Assert.That(certificate.Chain.Intermediates[0].Thumbprint,               Is.EqualTo(ca.Intermediate.Thumbprint));
            Assert.That(certificate.PrivateKeyPem,                                   Does.StartWith("-----BEGIN PRIVATE KEY-----"));
            Assert.That(certificate.FullChainPem.Split("BEGIN CERTIFICATE").Length - 1, Is.EqualTo(2));

            Assert.That(ca.Validations,  Is.EquivalentTo(new[] { "http-01 time.example.org", "http-01 ntp.example.org" }));
            Assert.That(http01!.Open,    Is.EqualTo(0), "the answers are taken away again");

            // And the chain is one a TLS server can present.
            Assert.That(certificate.Chain.CacheKey, Does.StartWith(certificate.Certificate.Thumbprint));

        }

        #endregion

        #region AWildcardIsProvenThroughDNS01_AndTheRecordsAreWithdrawnAgain()

        [Test]
        public async Task AWildcardIsProvenThroughDNS01_AndTheRecordsAreWithdrawnAgain()
        {

            using var client = NewClient();
            await client.CreateAccountAsync(TermsOfServiceAgreed: true);

            var published  = new ConcurrentQueue<(String Name, String Value)>();
            var zone       = ca!.TXTRecords;
            var dns01      = new DNS01ChallengeResponder(
                                 Publish:   (name, value, ct) => {
                                                published.Enqueue((name, value));
                                                return DNS01(zone).Publish(name, value, ct);
                                            },
                                 Withdraw:  DNS01(zone).Withdraw
                             );

            // http-01 first: it answers for the name, and cannot for the wildcard.
            var certificate = await client.ObtainCertificateAsync(["*.example.org", "example.org"], [ http01!, dns01 ], KeyType: ACMEKeyType.ECDSA_P384);

            Assert.That(certificate.Identifiers.Select(identifier => identifier.Value),
                        Is.EquivalentTo(new[] { "*.example.org", "example.org" }));

            Assert.That(certificate.Certificate.GetECDsaPublicKey()?.KeySize,  Is.EqualTo(384));
            Assert.That(ca.Validations,                                        Is.EquivalentTo(new[] { "dns-01 *.example.org", "http-01 example.org" }));
            Assert.That(published.Select(record => record.Name),               Is.EqualTo(new[] { "_acme-challenge.example.org" }));
            Assert.That(published.Single().Value,                              Has.Length.EqualTo(43), "base64url of a SHA-256, without padding");
            Assert.That(zone,                                                  Is.Empty, "and the record is gone again");

        }

        #endregion

        #region AnIPAddressIsAnIdentifierOfItsOwn()

        [Test]
        public async Task AnIPAddressIsAnIdentifierOfItsOwn()
        {

            using var client = NewClient();
            await client.CreateAccountAsync(TermsOfServiceAgreed: true);

            var certificate = await client.ObtainCertificateAsync(["192.0.2.7", "time.example.org"], [ http01! ]);

            Assert.That(certificate.Identifiers, Is.EquivalentTo(new[] { new ACMEIdentifier("ip", "192.0.2.7"), new ACMEIdentifier("dns", "time.example.org") }));

        }

        #endregion

        #region ABadNonceIsAnsweredWithTheNextOne()

        [Test]
        public async Task ABadNonceIsAnsweredWithTheNextOne()
        {

            using var client = NewClient();

            ca!.RefuseNextNonces = 2;

            await client.CreateAccountAsync(TermsOfServiceAgreed: true);
            var certificate = await client.ObtainCertificateAsync(["time.example.org"], [ http01! ]);

            Assert.That(ca.NoncesRefused,                      Is.EqualTo(2));
            Assert.That(certificate.Identifiers.Single().Value, Is.EqualTo("time.example.org"));

        }

        #endregion

        #region AWrongAnswerFails_SaysWhy_AndIsTakenAwayAllTheSame()

        [Test]
        public async Task AWrongAnswerFails_SaysWhy_AndIsTakenAwayAllTheSame()
        {

            using var client = NewClient();
            await client.CreateAccountAsync(TermsOfServiceAgreed: true);

            var liar = new LyingResponder(http01!);

            var e = await Assert.ThrowsAsync<ACMEException>(() => client.ObtainCertificateAsync(["time.example.org"], [ liar ]));

            Assert.That(e!.Problem?.ShortType,  Is.EqualTo("unauthorized"));
            Assert.That(e.Message,              Does.Contain("time.example.org"));
            Assert.That(e.Message,              Does.Contain("is not the key authorization"));
            Assert.That(liar.CleanedUp,         Is.EqualTo(1));
            Assert.That(http01!.Open,           Is.EqualTo(0));

        }

        private sealed class LyingResponder(HTTP01ChallengeResponder Inner) : IACMEChallengeResponder
        {

            public Int32   CleanedUp;

            public String  ChallengeType
                => Inner.ChallengeType;

            public Boolean CanAnswer(ACMEIdentifier Identifier, Boolean Wildcard)
                => Inner.CanAnswer(Identifier, Wildcard);

            public Task PrepareAsync(ACMEChallengeTask Challenge, CancellationToken CancellationToken)
                => Inner.PrepareAsync(Challenge with { KeyAuthorization = $"{Challenge.Token}.somebody-else" }, CancellationToken);

            public Task CleanUpAsync(ACMEChallengeTask Challenge, CancellationToken CancellationToken)
            {
                Interlocked.Increment(ref CleanedUp);
                return Inner.CleanUpAsync(Challenge, CancellationToken);
            }

        }

        #endregion

        #region AWildcardWithoutDNS01IsRefusedBeforeAnythingIsAnswered()

        [Test]
        public async Task AWildcardWithoutDNS01IsRefusedBeforeAnythingIsAnswered()
        {

            using var client = NewClient();
            await client.CreateAccountAsync(TermsOfServiceAgreed: true);

            var e = await Assert.ThrowsAsync<ACMEException>(() => client.ObtainCertificateAsync(["*.example.org"], [ http01! ]));

            Assert.That(e!.Message,      Does.Contain("*.example.org"));
            Assert.That(e.Message,       Does.Contain("dns-01 only"));
            Assert.That(ca!.Validations, Is.Empty);

        }

        #endregion

        #region AnExternalAccountBindingIsSignedWithItsMACKey()

        [Test]
        public async Task AnExternalAccountBindingIsSignedWithItsMACKey()
        {

            var macKey = RandomNumberGenerator.GetBytes(32);
            ca!.ExternalAccountBinding = ("kid-7", macKey);

            using var client = NewClient();

            var missing = await Assert.ThrowsAsync<ACMEException>(() => client.CreateAccountAsync(TermsOfServiceAgreed: true));
            Assert.That(missing!.Message, Does.Contain("external account binding"));

            var wrong   = await Assert.ThrowsAsync<ACMEException>(() => client.CreateAccountAsync(TermsOfServiceAgreed: true,
                                                                                            EABKeyId:    "kid-7",
                                                                                            EABHMACKey:  Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32))));
            Assert.That(wrong!.Problem?.ShortType, Is.EqualTo("unauthorized"));

            var account = await client.CreateAccountAsync(TermsOfServiceAgreed: true,
                                                          EABKeyId:    "kid-7",
                                                          EABHMACKey:  Base64Url.EncodeToString(macKey));

            Assert.That(account.Created, Is.True);

        }

        #endregion

        #region NothingIsOrderedWithoutAnAccount()

        [Test]
        public async Task NothingIsOrderedWithoutAnAccount()
        {

            using var client = NewClient();

            var e = await Assert.ThrowsAsync<ACMEException>(() => client.ObtainCertificateAsync(["time.example.org"], [ http01! ]));

            Assert.That(e!.Message,  Does.Contain("CreateAccountAsync"));
            Assert.That(ca!.Orders,  Is.EqualTo(0));

        }

        #endregion

        #region TheCertificateIdentifierIsTheOneOfRFC9773()

        /// <summary>
        /// The example of RFC 9773, section 4.1: an authority key identifier of
        /// 69:88:5B:6B:87:46:40:41:E1:B3:7B:84:7B:A0:AE:2C:DE:01:C8:D4 and the
        /// serial number 00:87:65:43:21 - with the leading zero its encoding has.
        /// </summary>
        [Test]
        public void TheCertificateIdentifierIsTheOneOfRFC9773()
        {

            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

            var request = new CertificateRequest("CN=example.org", key, HashAlgorithmName.SHA256);
            request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromSubjectKeyIdentifier(
                                                  Convert.FromHexString("69885B6B87464041E1B37B847BA0AE2CDE01C8D4")));

            using var issuer      = new CertificateRequest("CN=issuer", key, HashAlgorithmName.SHA256).CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            using var certificate = request.Create(issuer.SubjectName, X509SignatureGenerator.CreateForECDsa(key),
                                                   DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1),
                                                   [ 0x00, 0x87, 0x65, 0x43, 0x21 ]);

            Assert.That(ACMEClient.CertificateIdentifier(certificate), Is.EqualTo("aYhba4dGQEHhs3uEe6CuLN4ByNQ.AIdlQyE"));

        }

        #endregion

        #region TheIdentifierAgreesWithTheCertificateAuthoritys()

        [Test]
        public async Task TheIdentifierAgreesWithTheCertificateAuthoritys()
        {

            using var client = NewClient();
            await client.CreateAccountAsync(TermsOfServiceAgreed: true);

            var certificate  = await client.ObtainCertificateAsync(["time.example.org"], [ http01! ]);
            var info         = await client.GetRenewalInfoAsync(certificate.Certificate);

            Assert.That(ca!.CertificateIdentifiers.Values, Does.Contain(ACMEClient.CertificateIdentifier(certificate.Certificate)));
            Assert.That(info,                              Is.Not.Null, "the authority knows the certificate by that identifier");
            Assert.That(info!.Start,                       Is.LessThan(info.End));
            Assert.That(info.End,                          Is.LessThan(certificate.NotAfter));

        }

        #endregion

        #region TheKeyAuthorizationIsTheTokenAndTheThumbprint()

        [Test]
        public void TheKeyAuthorizationIsTheTokenAndTheThumbprint()
        {

            using var key = ACMEAccountKey.Generate();

            Assert.That(key.Thumbprint,                    Has.Length.EqualTo(43));
            Assert.That(key.KeyAuthorization("abc"),       Is.EqualTo($"abc.{key.Thumbprint}"));
            Assert.That(ACMEAccountKey.FromPem(key.ToPem()).Thumbprint, Is.EqualTo(key.Thumbprint));

            using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
            Assert.Throws<ArgumentException>(() => ACMEAccountKey.FromPem(p384.ExportPkcs8PrivateKeyPem()));

        }

        #endregion

        #region AnAccountKeyIsKeptInItsFile()

        [Test]
        public void AnAccountKeyIsKeptInItsFile()
        {

            var directory = Directory.CreateTempSubdirectory("hermod-acme-");

            try
            {

                var path    = Path.Combine(directory.FullName, "account.pem");

                using var first   = ACMEAccountKey.LoadOrCreate(path);
                using var second  = ACMEAccountKey.LoadOrCreate(path);

                Assert.That(second.Thumbprint, Is.EqualTo(first.Thumbprint));

                File.WriteAllText(path, "not a key");
                Assert.That(() => ACMEAccountKey.LoadOrCreate(path), Throws.Exception, "a key that cannot be read is not replaced");
                Assert.That(File.ReadAllText(path), Is.EqualTo("not a key"));

            }
            finally
            {
                directory.Delete(true);
            }

        }

        #endregion


        #region LetsEncryptStagingHandsOutItsDirectory()

        /// <summary>
        /// Let's Encrypt's staging environment, read only: its directory names
        /// everything this client asks for.
        /// </summary>
        [Test, Category("External")]
        public async Task LetsEncryptStagingHandsOutItsDirectory()
        {

            using var key     = ACMEAccountKey.Generate();
            using var client  = new ACMEClient(ACMEClient.LetsEncryptStaging, key);

            var directory = await client.GetDirectoryAsync();

            Assert.That(directory.NewOrder.Host,     Is.EqualTo("acme-staging-v02.api.letsencrypt.org"));
            Assert.That(directory.RenewalInfo,       Is.Not.Null);
            Assert.That(directory.TermsOfService,    Is.Not.Null);
            Assert.That(await client.FindAccountAsync(), Is.Null, "a new key has no account");

        }

        #endregion

    }

}
