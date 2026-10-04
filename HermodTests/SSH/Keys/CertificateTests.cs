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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// M5 OpenSSH certificates: the mini-CA builder, parser/validator and certificate-based auth.
    /// </summary>
    [TestFixture]
    public class CertificateTests
    {

        private sealed class FixedTimeProvider(DateTimeOffset Now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => Now;
        }

        private static readonly DateTimeOffset Now = new (2026, 7, 24, 12, 0, 0, TimeSpan.Zero);

        private static SshCertificate IssueUserCert(ISshHostKey Subject, ISshHostKey Ca, String[] Principals,
                                                    DateTimeOffset? ValidBefore = null, UInt64 Serial = 1, String KeyId = "id")
            => new OpenSshCertificateBuilder
               {
                   Serial       = Serial,
                   Type         = SshCertType.User,
                   KeyId        = KeyId,
                   Principals   = Principals,
                   ValidAfter   = Now.AddHours(-1),
                   ValidBefore  = ValidBefore ?? Now.AddHours(1)
               }.Sign(Subject.PublicKeyBlob, Ca);


        #region Certificate_BuildParseRoundTrip

        [TestCase("ssh-ed25519")]
        [TestCase("ecdsa-sha2-nistp256")]
        [TestCase("ssh-rsa")]
        public void Certificate_BuildParseRoundTrip(String SubjectType)
        {

            var subject = HostKeyMatrixTests.MakeHostKey(SubjectType == "ssh-rsa" ? "rsa-sha2-512" : SubjectType);
            var ca      = SshHostKey.GenerateEd25519();

            var cert = IssueUserCert(subject, ca, [ "achim", "ops" ], KeyId: "achims-cert", Serial: 42);

            Assert.Multiple(() => {
                Assert.That(cert.Type,             Is.EqualTo(SshCertType.User));
                Assert.That(cert.KeyId,            Is.EqualTo("achims-cert"));
                Assert.That(cert.Serial,           Is.EqualTo(42));
                Assert.That(cert.Principals,       Is.EqualTo(new[] { "achim", "ops" }));
                Assert.That(cert.SubjectPublicKey, Is.EqualTo(subject.PublicKeyBlob));
                Assert.That(cert.SignatureKey,     Is.EqualTo(ca.PublicKeyBlob));
                Assert.That(cert.VerifyCaSignature(), Is.True, "the CA signature must verify");
                Assert.That(cert.CertAlgorithm,    Does.EndWith("-cert-v01@openssh.com"));
            });

        }

        #endregion

        #region Certificate_Validator_ChecksEverything

        [Test]
        public void Certificate_Validator_ChecksEverything()
        {

            var subject = SshHostKey.GenerateEd25519();
            var ca      = SshHostKey.GenerateEd25519();
            var otherCa = SshHostKey.GenerateEd25519();

            var trust   = new SshCertificateAuthorityTrust().TrustCA(ca);
            var cert    = IssueUserCert(subject, ca, [ "achim" ]);

            Assert.Multiple(() => {

                Assert.That(SshCertificateValidator.Validate(cert, SshCertType.User, "achim", trust, Now).IsValid, Is.True, "a valid cert");
                Assert.That(SshCertificateValidator.Validate(cert, SshCertType.Host, "achim", trust, Now).IsValid, Is.False, "wrong type");
                Assert.That(SshCertificateValidator.Validate(cert, SshCertType.User, "bob",   trust, Now).IsValid, Is.False, "wrong principal");
                Assert.That(SshCertificateValidator.Validate(cert, SshCertType.User, "achim", trust, Now.AddDays(2)).IsValid, Is.False, "expired");
                Assert.That(SshCertificateValidator.Validate(cert, SshCertType.User, "achim", new SshCertificateAuthorityTrust().TrustCA(otherCa), Now).IsValid, Is.False, "untrusted CA");

                var revoked = new SshCertificateAuthorityTrust().TrustCA(ca).RevokeSerial(1);
                Assert.That(SshCertificateValidator.Validate(cert, SshCertType.User, "achim", revoked, Now).IsValid, Is.False, "revoked by serial");

            });

        }

        #endregion

        #region Certificate_EmptyPrincipals_MatchNothing

        /// <summary>
        /// An empty principals list matches NOTHING — OpenSSH 10.3's semantics, not the wildcard
        /// reading that preceded it.
        /// </summary>
        /// <remarks>
        /// This pins a deliberate behaviour change. OpenSSH 10.3 lists it under "Potentially
        /// incompatible changes": an empty principals section used to be "treated as matching any
        /// principal (i.e. as a wildcard)" and now "never match[es] any principal". We implemented the
        /// old reading, which left us strictly more permissive than the reference — a certificate
        /// naming nobody authenticated everybody. The assertion that matters is therefore not that
        /// some name is refused, but that <i>no</i> name is accepted, the one a wildcard would have
        /// waved through included.
        /// </remarks>
        [Test]
        public void Certificate_EmptyPrincipals_MatchNothing()
        {

            var subject       = SshHostKey.GenerateEd25519();
            var ca            = SshHostKey.GenerateEd25519();
            var trust         = new SshCertificateAuthorityTrust().TrustCA(ca);

            var noPrincipals  = IssueUserCert(subject, ca, [ ]);
            var withPrincipal = IssueUserCert(subject, ca, [ "achim" ]);

            Assert.Multiple(() => {

                // The certificate is otherwise beyond reproach — trusted CA, sound signature, inside
                // its window — so the empty principals field is the only thing left that can fail it.
                Assert.That(noPrincipals.VerifyCaSignature(), Is.True,  "the certificate itself must be sound");
                Assert.That(noPrincipals.Principals,          Is.Empty, "this test is about an empty list");

                foreach (var name in new[] { "achim", "root", "", "*", "anybody" })
                    Assert.That(SshCertificateValidator.Validate(noPrincipals, SshCertType.User, name, trust, Now).IsValid,
                                Is.False,
                                $"an empty principals list must not match '{name}'");

                // The reason has to name the cause: an operator reading an audit log must be able to
                // tell this apart from a misspelled user name, which is the other way step 6 fails.
                Assert.That(SshCertificateValidator.Validate(noPrincipals, SshCertType.User, "achim", trust, Now).Reason,
                            Does.Contain("no valid principals"));

                // Host certificates run through the same check with the hostname as the principal,
                // which is what HostKeyPolicy passes — so the rule must hold there too.
                var hostCert = new OpenSshCertificateBuilder {
                                   Serial      = 7,
                                   Type        = SshCertType.Host,
                                   KeyId       = "host",
                                   Principals  = [ ],
                                   ValidAfter  = Now.AddHours(-1),
                                   ValidBefore = Now.AddHours(1)
                               }.Sign(subject.PublicKeyBlob, ca);

                Assert.That(SshCertificateValidator.Validate(hostCert, SshCertType.Host, "host.example.org", trust, Now).IsValid,
                            Is.False,
                            "a host certificate naming no principals must not match any hostname either");

                // The control. Without it this test would also pass if validation had simply started
                // refusing everything, which is a different bug wearing the same green.
                Assert.That(SshCertificateValidator.Validate(withPrincipal, SshCertType.User, "achim", trust, Now).IsValid,
                            Is.True,
                            "an explicitly named principal must still be accepted");

            });

        }

        #endregion

        #region Certificate_UnknownCriticalOption_IsRejected

        [Test]
        public void Certificate_UnknownCriticalOption_IsRejected()
        {

            var subject = SshHostKey.GenerateEd25519();
            var ca      = SshHostKey.GenerateEd25519();

            var builder = new OpenSshCertificateBuilder { Type = SshCertType.User, Principals = [ "achim" ], ValidAfter = Now.AddHours(-1), ValidBefore = Now.AddHours(1) };
            builder.CriticalOptions.Add(new ("totally-unknown-option", [ 1, 2, 3 ]));
            var cert = builder.Sign(subject.PublicKeyBlob, ca);

            var trust  = new SshCertificateAuthorityTrust().TrustCA(ca);
            var result = SshCertificateValidator.Validate(cert, SshCertType.User, "achim", trust, Now);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Reason,  Does.Contain("unknown critical option"));

        }

        #endregion

        #region Certificate_Auth_Loopback

        [Test]
        [CancelAfter(15000)]
        public async Task Certificate_Auth_Loopback(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey   = Ed25519KeyPair.Generate();
            var caKey     = SshHostKey.GenerateEd25519();
            var userKey   = SshHostKey.GenerateEd25519();

            var cert          = IssueUserCert(userKey, caKey, [ "achim" ]);
            var certifiedKey  = new CertifiedKey(userKey, cert);

            var authenticator = new SshAuthenticationPolicy()
                                    .WithCertificateAuthority(new SshCertificateAuthorityTrust().TrustCA(caKey),
                                                              new FixedTimeProvider(Now));

            var serverRun = Task.Run(async () =>
            {
                using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                return await UserAuthentication.ServerAuthenticateAsync(t, authenticator, CancellationToken: CancellationToken);
            }, CancellationToken);

            var clientRun = Task.Run(async () =>
            {
                using var t = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
                return await UserAuthentication.ClientPublicKeyAuthenticateAsync(t, "achim", certifiedKey, CancellationToken: CancellationToken);
            }, CancellationToken);

            await Assert.MultipleAsync(async () => {
                Assert.That(await clientRun,              Is.True, "certificate auth must succeed");
                Assert.That((await serverRun).Username,   Is.EqualTo("achim"));
            });

        }

        #endregion

        #region HostCertificate_ClientValidatesViaHostCA

        [Test]
        [CancelAfter(15000)]
        public async Task HostCertificate_ClientValidatesViaHostCA(CancellationToken CancellationToken)
        {

            var caKey       = SshHostKey.GenerateEd25519();
            var hostBaseKey = SshHostKey.GenerateEd25519();

            // Issue a HOST certificate for the server, valid for the hostname "server.example.org".
            var hostCert = new OpenSshCertificateBuilder
            {
                Type        = SshCertType.Host,
                KeyId       = "server-host-cert",
                Principals  = [ "server.example.org" ],
                ValidAfter  = Now.AddHours(-1),
                ValidBefore = Now.AddHours(1)
            }.Sign(hostBaseKey.PublicKeyBlob, caKey);

            var certifiedHostKey = new CertifiedKey(hostBaseKey, hostCert);
            var trust            = new SshCertificateAuthorityTrust().TrustCA(caKey);
            var policy           = HostKeyPolicy.HostCertificate(trust, new FixedTimeProvider(Now));

            // Correct host: the certificate validates, the handshake completes.
            {
                var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
                var serverTask = SshTransport.ServerHandshakeAsync(serverPipe, certifiedHostKey, CancellationToken: CancellationToken);
                using var client = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: policy.ForHost("server.example.org", IPPort.Parse(22)), CancellationToken: CancellationToken);
                using var server = await serverTask;
                Assert.That(client.SessionId, Is.EqualTo(server.SessionId));
            }

            // Wrong host name: the certificate does not list it as a principal ⇒ rejected.
            {
                var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
                _ = SshTransport.ServerHandshakeAsync(serverPipe, certifiedHostKey, CancellationToken: CancellationToken);
                Assert.That(async () => await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: policy.ForHost("evil.example.org", IPPort.Parse(22)), CancellationToken: CancellationToken),
                            Throws.InstanceOf<SshWireException>());
            }

        }

        #endregion

        #region Certificate_Auth_WrongPrincipal_Loopback

        [Test]
        [CancelAfter(15000)]
        public async Task Certificate_Auth_WrongPrincipal_Loopback(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey  = Ed25519KeyPair.Generate();
            var caKey    = SshHostKey.GenerateEd25519();
            var userKey  = SshHostKey.GenerateEd25519();

            // The certificate is only valid for "achim", but the client logs in as "bob".
            var certifiedKey  = new CertifiedKey(userKey, IssueUserCert(userKey, caKey, [ "achim" ]));

            var authenticator = new SshAuthenticationPolicy()
                                    .WithCertificateAuthority(new SshCertificateAuthorityTrust().TrustCA(caKey), new FixedTimeProvider(Now));

            var serverRun = Task.Run(async () =>
            {
                using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                try   { await UserAuthentication.ServerAuthenticateAsync(t, authenticator, MaxAuthTries: 1, CancellationToken: CancellationToken); }
                catch { }
            }, CancellationToken);

            using var client = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            var ok = await UserAuthentication.ClientPublicKeyAuthenticateAsync(client, "bob", certifiedKey, CancellationToken: CancellationToken);

            Assert.That(ok, Is.False, "a certificate must be rejected for a principal it does not list");

            clientPipe.Output.Complete();
            try { await serverRun; } catch { }

        }

        #endregion

    }

}
