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

using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using NUnit.Framework;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TLS.KeyPins
{

    /// <summary>
    /// The second TLS stack beside SslStream: TLS 1.3 over TCP by
    /// BouncyCastle, with raw public keys (RFC 7250) in both directions,
    /// a fallback to certificates, and a stream that reads and writes at
    /// the same time.
    /// </summary>
    /// <remarks>
    /// Over real sockets on the loopback interface. No certificate store is
    /// touched: the certificates live in memory.
    /// </remarks>
    [TestFixture]
    public class BouncyCastleTLSTests
    {

        #region (helpers)

        private static readonly TimeSpan Guard = TimeSpan.FromSeconds(30);

        private sealed class Told
        {
            public readonly ConcurrentQueue<KeyPinValidation> Results = new();
            public void Add(KeyPinValidation Validation) => Results.Enqueue(Validation);
            public KeyPinOutcome[] Outcomes => Results.Select(result => result.Outcome).ToArray();
        }

        /// <summary>
        /// A key in a self-signed carrier certificate that nobody vouches
        /// for, with a name nobody dials.
        /// </summary>
        internal static X509Certificate2 Carrier(String Subject = "CN=carrier.invalid", ECCurve? Curve = null)
        {

            using var key = ECDsa.Create(Curve ?? ECCurve.NamedCurves.nistP256);

            var request = new CertificateRequest(Subject, key, HashAlgorithmName.SHA256);

            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));

            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

        }

        internal static KeyPinPolicy PinnedTo(X509Certificate2 Certificate, RawPublicKeyMode RawPublicKeys)

            => new ([ KeyPin.FromCertificate(Certificate) ], RawPublicKeys: RawPublicKeys);

        /// <summary>
        /// A client's raw public key is believed where its pin is the pin of the given certificate's key.
        /// </summary>
        private static Func<TLSPeerKey, TLSValidationResult> KeyOf(X509Certificate2 Certificate, ConcurrentQueue<TLSPeerKey>? Asked = null)

            => key => {
                   Asked?.Enqueue(key);
                   return key.KeyPin == KeyPin.FromCertificate(Certificate)
                              ? TLSValidationResult.Success()
                              : TLSValidationResult.Failed($"The key pin {key.KeyPin} is not the station's.");
               };

        /// <summary>
        /// A client's certificate is believed where its pin is the pin of the given certificate's key;
        /// a client without one is not.
        /// </summary>
        private static RemoteCertificateValidationCallback CertificateOf(X509Certificate2 Certificate)

            => (sender, certificate, chain, policyErrors)
                   => certificate is X509Certificate2 shown &&
                      KeyPin.FromCertificate(shown) == KeyPin.FromCertificate(Certificate);

        /// <summary>
        /// One connection over the loopback interface: both handshakes, each
        /// to its session or to the exception that ended it.
        /// </summary>
        private static async Task<(TLSSession? Client, Exception? ClientFailed, TLSSession? Server, Exception? ServerFailed)>

            Connect(RawPublicKeyTLSClientParameters  ClientParameters,
                    RawPublicKeyTLSServerParameters  ServerParameters)

        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var port = ((IPEndPoint) listener.LocalEndpoint).Port;

            async Task<(TLSSession?, Exception?)> serve()
            {
                var accepted = await listener.AcceptTcpClientAsync().WaitAsync(Guard);
                try
                {
                    return (await BouncyCastleTLS.AuthenticateAsServerAsync(accepted.GetStream(), ServerParameters).WaitAsync(Guard), null);
                }
                catch (Exception e)
                {
                    accepted.Dispose();
                    return (null, e);
                }
            }

            async Task<(TLSSession?, Exception?)> dial()
            {
                var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(System.Net.IPAddress.Loopback, port).WaitAsync(Guard);
                try
                {
                    return (await BouncyCastleTLS.AuthenticateAsClientAsync(tcpClient.GetStream(), ClientParameters).WaitAsync(Guard), null);
                }
                catch (Exception e)
                {
                    tcpClient.Dispose();
                    return (null, e);
                }
            }

            var serving = serve();
            var dialled = dial();

            var (client, clientFailed) = await dialled;
            var (server, serverFailed) = await serving;

            return (client, clientFailed, server, serverFailed);

        }

        private static async Task<Byte[]> ReadExactly(Stream Stream, Int32 Count)
        {
            var buffer = new Byte[Count];
            await Stream.ReadExactlyAsync(buffer).AsTask().WaitAsync(Guard);
            return buffer;
        }

        #endregion


        #region Raw public keys in both directions

        [Test]
        public async Task BothKeys_ComeRaw_AndTheStreamCarriesData([Values(RawPublicKeyMode.Prefer, RawPublicKeyMode.Require)] RawPublicKeyMode ClientMode,
                                                                   [Values(RawPublicKeyMode.Prefer, RawPublicKeyMode.Require)] RawPublicKeyMode ServerMode)
        {

            using var serverKey  = Carrier();
            using var stationKey = Carrier("CN=station.invalid");

            var told  = new Told();
            var asked = new ConcurrentQueue<TLSPeerKey>();

            var (client, clientFailed, server, serverFailed) = await Connect(

                new RawPublicKeyTLSClientParameters {
                    ServerKeyPins             = PinnedTo(serverKey, ClientMode),
                    OnServerKeyPinValidated   = told.Add,
                    ServerName                = "somewhere-else.invalid",
                    ApplicationProtocols      = [ "h2", "http/1.1" ],
                    ClientCertificate         = stationKey
                },

                new RawPublicKeyTLSServerParameters {
                    ServerCertificate         = serverKey,
                    ServerRawPublicKeys       = ServerMode,
                    ApplicationProtocols      = [ "http/1.1" ],
                    RequestClientCertificate  = true,
                    ClientRawPublicKeys       = ServerMode,
                    ClientKeyValidator        = KeyOf(stationKey, asked)
                }

            );

            Assert.That(clientFailed, Is.Null, clientFailed?.ToString());
            Assert.That(serverFailed, Is.Null, serverFailed?.ToString());

            await using var clientStream = client!.Stream;
            await using var serverStream = server!.Stream;

            Assert.Multiple(() => {

                Assert.That(client.Provider,                    Is.EqualTo(TLSProvider.BouncyCastle));
                Assert.That(client.RemoteCertificateType,       Is.EqualTo(TLSCertificateType.RawPublicKey));
                Assert.That(client.LocalCertificateType,        Is.EqualTo(TLSCertificateType.RawPublicKey));
                Assert.That(client.RemoteKey?.IsRawPublicKey,   Is.True);
                Assert.That(client.RemoteKey?.KeyPin,           Is.EqualTo(KeyPin.FromCertificate(serverKey)));
                Assert.That(client.RemoteCertificate,           Is.Null);
                Assert.That(client.ApplicationProtocol,         Is.EqualTo("http/1.1"));

                Assert.That(server.LocalCertificateType,        Is.EqualTo(TLSCertificateType.RawPublicKey));
                Assert.That(server.RemoteCertificateType,       Is.EqualTo(TLSCertificateType.RawPublicKey));
                Assert.That(server.RemoteKey?.IsRawPublicKey,   Is.True);
                Assert.That(server.RemoteKey?.KeyPin,           Is.EqualTo(KeyPin.FromCertificate(stationKey)));
                Assert.That(server.RemoteCertificate,           Is.Null);
                Assert.That(server.ApplicationProtocol,         Is.EqualTo("http/1.1"));

                Assert.That(told.Outcomes,                      Is.EqualTo(new[] { KeyPinOutcome.Accepted }));
                Assert.That(told.Results.Single().CertificateType, Is.EqualTo(TLSCertificateType.RawPublicKey));
                Assert.That(asked.Single().KeyPin,              Is.EqualTo(KeyPin.FromCertificate(stationKey)));

                // The same keying material on both ends, and it is material.
                Assert.That(client.ExportKeyingMaterial("EXPORTER-hermod-test", [], 32),
                            Is.EqualTo(server.ExportKeyingMaterial("EXPORTER-hermod-test", [], 32)));
                Assert.That(client.ExportKeyingMaterial("EXPORTER-hermod-test", [], 32),
                            Is.Not.EqualTo(new Byte[32]));

            });

            // A read that waits does not hold a write up - as on an SslStream, and unlike a blocking stream.
            var serverReads = ReadExactly(serverStream, 5);
            var clientReads = ReadExactly(clientStream, 5);

            await Task.Delay(100);

            await clientStream.WriteAsync(Encoding.ASCII.GetBytes("hello")).AsTask().WaitAsync(Guard);
            await serverStream.WriteAsync(Encoding.ASCII.GetBytes("world")).AsTask().WaitAsync(Guard);

            Assert.That(Encoding.ASCII.GetString(await serverReads), Is.EqualTo("hello"));
            Assert.That(Encoding.ASCII.GetString(await clientReads), Is.EqualTo("world"));

            // More than one record, both ways at once.
            var big        = RandomNumberGenerator.GetBytes(300_000);
            var bigReads   = ReadExactly(serverStream, big.Length);
            var bigReads2  = ReadExactly(clientStream, big.Length);

            await Task.WhenAll(
                      clientStream.WriteAsync(big).AsTask(),
                      serverStream.WriteAsync(big).AsTask()
                  ).WaitAsync(Guard);

            Assert.That(await bigReads,  Is.EqualTo(big));
            Assert.That(await bigReads2, Is.EqualTo(big));

            // Closed on one side is the end of the stream on the other.
            await clientStream.DisposeAsync();

            Assert.That(await serverStream.ReadAsync(new Byte[16]).AsTask().WaitAsync(Guard), Is.Zero);

        }

        #endregion

        #region Fallback to certificates

        [Test]
        public async Task TheKeys_ComeInCertificates_WhereOneSideDoesNotSpeakRawKeys()
        {

            using var serverKey  = Carrier();
            using var stationKey = Carrier("CN=station.invalid");

            // A server that never shows its key raw and takes none raw; a client that prefers raw public keys.
            {

                var told = new Told();

                var (client, clientFailed, server, serverFailed) = await Connect(

                    new RawPublicKeyTLSClientParameters {
                        ServerKeyPins             = PinnedTo(serverKey, RawPublicKeyMode.Prefer),
                        OnServerKeyPinValidated   = told.Add,
                        ClientCertificate         = stationKey
                    },

                    new RawPublicKeyTLSServerParameters {
                        ServerCertificate           = serverKey,
                        RequestClientCertificate    = true,
                        ClientCertificateValidator  = CertificateOf(stationKey)
                    }

                );

                Assert.That(clientFailed, Is.Null, clientFailed?.ToString());
                Assert.That(serverFailed, Is.Null, serverFailed?.ToString());

                await using var clientStream = client!.Stream;
                await using var serverStream = server!.Stream;

                Assert.Multiple(() => {
                    Assert.That(client.RemoteCertificateType,         Is.EqualTo(TLSCertificateType.X509));
                    Assert.That(client.RemoteCertificate?.Subject,    Is.EqualTo("CN=carrier.invalid"));
                    Assert.That(client.RemoteKey?.KeyPin,             Is.EqualTo(KeyPin.FromCertificate(serverKey)));
                    Assert.That(server.RemoteCertificateType,         Is.EqualTo(TLSCertificateType.X509));
                    Assert.That(server.RemoteCertificate?.Subject,    Is.EqualTo("CN=station.invalid"));
                    Assert.That(server.RemoteKey?.KeyPin,             Is.EqualTo(KeyPin.FromCertificate(stationKey)));
                    Assert.That(told.Outcomes,                        Is.EqualTo(new[] { KeyPinOutcome.Accepted }));
                    Assert.That(told.Results.Single().CertificateType, Is.EqualTo(TLSCertificateType.X509));
                });

            }

            // The mixed case: the server's key raw, the client's in its certificate.
            {

                var (client, clientFailed, server, serverFailed) = await Connect(

                    new RawPublicKeyTLSClientParameters {
                        ServerKeyPins             = PinnedTo(serverKey, RawPublicKeyMode.Require),
                        ClientCertificate         = stationKey
                    },

                    new RawPublicKeyTLSServerParameters {
                        ServerCertificate           = serverKey,
                        ServerRawPublicKeys         = RawPublicKeyMode.Prefer,
                        RequestClientCertificate    = true,
                        ClientCertificateValidator  = CertificateOf(stationKey)
                    }

                );

                Assert.That(clientFailed, Is.Null, clientFailed?.ToString());
                Assert.That(serverFailed, Is.Null, serverFailed?.ToString());

                await using var clientStream = client!.Stream;
                await using var serverStream = server!.Stream;

                Assert.Multiple(() => {
                    Assert.That(client.RemoteCertificateType,         Is.EqualTo(TLSCertificateType.RawPublicKey));
                    Assert.That(client.LocalCertificateType,          Is.EqualTo(TLSCertificateType.X509));
                    Assert.That(server.RemoteCertificateType,         Is.EqualTo(TLSCertificateType.X509));
                    Assert.That(server.RemoteCertificate?.Subject,    Is.EqualTo("CN=station.invalid"));
                });

            }

        }

        #endregion

        #region A key that is not proven, and a key that is none

        /// <summary>
        /// A server that shows a key it does not hold: the public key is
        /// somebody else's - everybody may know it - and CertificateVerify
        /// is signed with a key of its own.
        /// </summary>
        private sealed class Impostor(Org.BouncyCastle.Tls.Crypto.Impl.BC.BcTlsCrypto                BcCrypto,
                                      Byte[]                                                         TheirSubjectPublicKeyInfo,
                                      Org.BouncyCastle.Crypto.AsymmetricKeyParameter                 OwnPrivateKey)

            : Org.BouncyCastle.Tls.DefaultTlsServer(BcCrypto)

        {

            protected override Org.BouncyCastle.Tls.ProtocolVersion[] GetSupportedVersions()
                => Org.BouncyCastle.Tls.ProtocolVersion.TLSv13.Only();

            protected override Int32[] GetSupportedCipherSuites()
                => [ Org.BouncyCastle.Tls.CipherSuite.TLS_AES_128_GCM_SHA256 ];

            public override Org.BouncyCastle.Tls.TlsCredentials GetCredentials()

                => new Org.BouncyCastle.Tls.Crypto.Impl.BC.BcDefaultTlsCredentialedSigner(
                       new Org.BouncyCastle.Tls.Crypto.TlsCryptoParameters(m_context),
                       BcCrypto,
                       OwnPrivateKey,
                       new Org.BouncyCastle.Tls.Certificate(
                           Org.BouncyCastle.Tls.CertificateType.RawPublicKey,
                           Org.BouncyCastle.Tls.TlsUtilities.EmptyBytes,
                           [ new Org.BouncyCastle.Tls.CertificateEntry(
                                 new Org.BouncyCastle.Tls.Crypto.Impl.BC.BcTlsRawKeyCertificate(BcCrypto, TheirSubjectPublicKeyInfo),
                                 null
                             ) ]
                       ),
                       Org.BouncyCastle.Tls.SignatureAndHashAlgorithm.GetInstance(
                           Org.BouncyCastle.Tls.HashAlgorithm.sha256,
                           Org.BouncyCastle.Tls.SignatureAlgorithm.ecdsa
                       )
                   );

        }

        [Test]
        public async Task AServerKey_ThatIsThePinnedOne_AndNotProven_IsRefused_AndNothingIsLearned()
        {

            using var pinned = Carrier();

            // The impostor's own key, on the same curve.
            var generator = new Org.BouncyCastle.Crypto.Generators.ECKeyPairGenerator();
            generator.Init(new Org.BouncyCastle.Crypto.Parameters.ECKeyGenerationParameters(
                               Org.BouncyCastle.Asn1.Sec.SecObjectIdentifiers.SecP256r1,
                               new Org.BouncyCastle.Security.SecureRandom()
                           ));
            var own = generator.GenerateKeyPair();

            foreach (var policy in new[] {
                                       PinnedTo(pinned, RawPublicKeyMode.Require),
                                       KeyPinPolicy.LearnOnFirstUse(RawPublicKeyMode.Require)
                                   })
            {

                using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
                listener.Start();

                var port = ((IPEndPoint) listener.LocalEndpoint).Port;

                var serving = Task.Run(async () => {

                    using var accepted = await listener.AcceptTcpClientAsync().WaitAsync(Guard);

                    try
                    {
                        var protocol = new Org.BouncyCastle.Tls.TlsServerProtocol(accepted.GetStream());
                        protocol.Accept(new Impostor(new Org.BouncyCastle.Tls.Crypto.Impl.BC.BcTlsCrypto(new Org.BouncyCastle.Security.SecureRandom()),
                                                     pinned.PublicKey.ExportSubjectPublicKeyInfo(),
                                                     own.Private));
                        return "the impostor's handshake completed";
                    }
                    catch (Exception e)
                    {
                        return e.Message;
                    }

                });

                var told = new Told();

                using var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(System.Net.IPAddress.Loopback, port).WaitAsync(Guard);

                var failed = await Assert.ThrowsAsync<TLSHandshakeFailedException>(
                                 async () => await BouncyCastleTLS.AuthenticateAsClientAsync(
                                                       tcpClient.GetStream(),
                                                       new RawPublicKeyTLSClientParameters {
                                                           ServerKeyPins            = policy,
                                                           OnServerKeyPinValidated  = told.Add
                                                       }
                                                   ).WaitAsync(Guard)
                             );

                TestContext.Out.WriteLine(failed!.Message);
                TestContext.Out.WriteLine(await serving.WaitAsync(Guard));

                Assert.Multiple(() => {
                    Assert.That(failed.AlertReceived,  Is.False, "The client itself has to refuse.");
                    Assert.That(told.Outcomes,         Is.Empty, "A key that was not proven was accepted or learned.");
                });

            }

        }

        [Test]
        public async Task ARawRSAKey_IsNoneAnybodyIsAskedAbout()
        {

            using var serverKey  = Carrier();

            using var rsa        = RSA.Create(2048);
            using var rsaCarrier = new CertificateRequest("CN=station.invalid", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).
                                       CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

            // A client's.
            {

                var asked = new ConcurrentQueue<TLSPeerKey>();

                var (client, clientFailed, server, serverFailed) = await Connect(

                    new RawPublicKeyTLSClientParameters {
                        ServerKeyPins             = PinnedTo(serverKey, RawPublicKeyMode.Require),
                        ClientCertificate         = rsaCarrier
                    },

                    new RawPublicKeyTLSServerParameters {
                        ServerCertificate         = serverKey,
                        ServerRawPublicKeys       = RawPublicKeyMode.Prefer,
                        RequestClientCertificate  = true,
                        ClientRawPublicKeys       = RawPublicKeyMode.Require,
                        ClientKeyValidator        = key => { asked.Enqueue(key); return TLSValidationResult.Success(); }
                    }

                );

                Assert.Multiple(() => {
                    Assert.That(server, Is.Null);
                    Assert.That((serverFailed as TLSHandshakeFailedException)?.AlertDescription, Is.EqualTo(42), "bad_certificate");
                    Assert.That(serverFailed?.Message, Does.Contain("raw RSA key"));
                    Assert.That(asked, Is.Empty, "Somebody was asked about a key that is none.");
                });

                if (client is not null)
                    await client.Stream.DisposeAsync();

            }

            // A server's.
            {

                var told = new Told();

                var (client, clientFailed, server, serverFailed) = await Connect(

                    new RawPublicKeyTLSClientParameters {
                        ServerKeyPins             = KeyPinPolicy.LearnOnFirstUse(RawPublicKeyMode.Require),
                        OnServerKeyPinValidated   = told.Add
                    },

                    new RawPublicKeyTLSServerParameters {
                        ServerCertificate         = rsaCarrier,
                        ServerRawPublicKeys       = RawPublicKeyMode.Prefer
                    }

                );

                Assert.Multiple(() => {
                    Assert.That(client, Is.Null);
                    Assert.That((clientFailed as TLSHandshakeFailedException)?.AlertDescription, Is.EqualTo(42), "bad_certificate");
                    Assert.That(clientFailed?.Message, Does.Contain("raw RSA key"));
                    Assert.That(told.Outcomes, Is.EqualTo(new[] { KeyPinOutcome.Refused }));
                });

            }

        }

        #endregion

        #region Refusals

        [Test]
        public async Task AServerKey_ThatIsNotPinned_IsRefused()
        {

            using var serverKey = Carrier();
            using var otherKey  = Carrier();

            var told = new Told();

            var (client, clientFailed, server, serverFailed) = await Connect(

                new RawPublicKeyTLSClientParameters {
                    ServerKeyPins             = PinnedTo(otherKey, RawPublicKeyMode.Require),
                    OnServerKeyPinValidated   = told.Add
                },

                new RawPublicKeyTLSServerParameters {
                    ServerCertificate         = serverKey,
                    ServerRawPublicKeys       = RawPublicKeyMode.Prefer
                }

            );

            Assert.Multiple(() => {

                Assert.That(client, Is.Null);
                Assert.That(server, Is.Null);

                Assert.That(clientFailed, Is.InstanceOf<TLSHandshakeFailedException>());
                Assert.That((clientFailed as TLSHandshakeFailedException)?.AlertDescription,  Is.EqualTo(42), "bad_certificate");
                Assert.That((clientFailed as TLSHandshakeFailedException)?.AlertReceived,     Is.False);
                Assert.That(clientFailed?.Message, Does.Contain(KeyPin.FromCertificate(serverKey).ToHex()));

                // The server is told why.
                Assert.That((serverFailed as TLSHandshakeFailedException)?.AlertDescription,  Is.EqualTo(42));
                Assert.That((serverFailed as TLSHandshakeFailedException)?.AlertReceived,     Is.True);

                Assert.That(told.Outcomes, Is.EqualTo(new[] { KeyPinOutcome.Refused }));

            });

        }

        [Test]
        public async Task AClientKey_ThatIsNotTheStations_IsRefused()
        {

            using var serverKey   = Carrier();
            using var stationKey  = Carrier("CN=station.invalid");
            using var strangerKey = Carrier("CN=station.invalid");

            var (client, clientFailed, server, serverFailed) = await Connect(

                new RawPublicKeyTLSClientParameters {
                    ServerKeyPins             = PinnedTo(serverKey, RawPublicKeyMode.Require),
                    ClientCertificate         = strangerKey
                },

                new RawPublicKeyTLSServerParameters {
                    ServerCertificate         = serverKey,
                    ServerRawPublicKeys       = RawPublicKeyMode.Prefer,
                    RequestClientCertificate  = true,
                    ClientRawPublicKeys       = RawPublicKeyMode.Require,
                    ClientKeyValidator        = KeyOf(stationKey)
                }

            );

            Assert.Multiple(() => {
                Assert.That(server, Is.Null);
                Assert.That(serverFailed, Is.InstanceOf<TLSHandshakeFailedException>());
                Assert.That((serverFailed as TLSHandshakeFailedException)?.AlertDescription, Is.EqualTo(49), "access_denied");
                Assert.That(serverFailed?.Message, Does.Contain(KeyPin.FromCertificate(strangerKey).ToString()));
            });

            // In TLS 1.3 a client is done before the server has looked at its key:
            // the refusal reaches it with its first read.
            if (client is not null)
            {
                await using var clientStream = client.Stream;
                Assert.That(async () => await clientStream.ReadAsync(new Byte[16]).AsTask().WaitAsync(Guard),
                            Throws.Exception);
            }

        }

        [Test]
        public async Task AClientCertificate_TheValidatorRefuses_IsRefused()
        {

            using var serverKey   = Carrier();
            using var stationKey  = Carrier("CN=station.invalid");
            using var strangerKey = Carrier("CN=station.invalid");

            // The server's key comes raw, the client's in its certificate - and is another one's.
            var (client, clientFailed, server, serverFailed) = await Connect(

                new RawPublicKeyTLSClientParameters {
                    ServerKeyPins               = PinnedTo(serverKey, RawPublicKeyMode.Require),
                    ClientCertificate           = strangerKey
                },

                new RawPublicKeyTLSServerParameters {
                    ServerCertificate           = serverKey,
                    ServerRawPublicKeys         = RawPublicKeyMode.Prefer,
                    RequestClientCertificate    = true,
                    ClientCertificateValidator  = CertificateOf(stationKey)
                }

            );

            Assert.Multiple(() => {
                Assert.That(server, Is.Null);
                Assert.That(serverFailed, Is.InstanceOf<TLSHandshakeFailedException>());
                Assert.That((serverFailed as TLSHandshakeFailedException)?.AlertDescription, Is.EqualTo(42), "bad_certificate");
                Assert.That(serverFailed?.Message, Does.Contain("station.invalid"));
            });

            if (client is not null)
                await client.Stream.DisposeAsync();

        }

        [Test]
        public async Task WhereOneSideDemandsRawKeys_AndTheOtherHasNone_TheHandshakeEnds()
        {

            using var serverKey  = Carrier();
            using var stationKey = Carrier("CN=station.invalid");

            // The client takes a raw public key alone; the server never shows its key raw.
            {

                var (client, clientFailed, server, serverFailed) = await Connect(
                    new RawPublicKeyTLSClientParameters { ServerKeyPins     = PinnedTo(serverKey, RawPublicKeyMode.Require) },
                    new RawPublicKeyTLSServerParameters { ServerCertificate = serverKey }
                );

                Assert.Multiple(() => {
                    Assert.That(client, Is.Null);
                    Assert.That(server, Is.Null);
                    Assert.That((serverFailed as TLSHandshakeFailedException)?.AlertDescription, Is.EqualTo(43), "unsupported_certificate");
                    Assert.That((clientFailed as TLSHandshakeFailedException)?.AlertReceived,    Is.True);
                });

            }

            // The server shows its key raw alone; the client's key pins take certificates alone.
            {

                var (client, clientFailed, server, serverFailed) = await Connect(
                    new RawPublicKeyTLSClientParameters { ServerKeyPins     = PinnedTo(serverKey, RawPublicKeyMode.Off) },
                    new RawPublicKeyTLSServerParameters { ServerCertificate = serverKey, ServerRawPublicKeys = RawPublicKeyMode.Require }
                );

                Assert.Multiple(() => {
                    Assert.That(client, Is.Null);
                    Assert.That(server, Is.Null);
                    Assert.That((serverFailed as TLSHandshakeFailedException)?.AlertDescription, Is.EqualTo(43), "unsupported_certificate");
                });

            }

            // The server takes a client's key raw alone; the client has a key and no raw public keys to offer.
            {

                var (client, clientFailed, server, serverFailed) = await Connect(

                    new RawPublicKeyTLSClientParameters {
                        ServerKeyPins             = PinnedTo(serverKey, RawPublicKeyMode.Off),
                        ClientCertificate         = stationKey
                    },

                    new RawPublicKeyTLSServerParameters {
                        ServerCertificate           = serverKey,
                        RequestClientCertificate    = true,
                        ClientRawPublicKeys         = RawPublicKeyMode.Require,
                        ClientKeyValidator          = KeyOf(stationKey),
                        ClientCertificateValidator  = CertificateOf(stationKey)
                    }

                );

                Assert.Multiple(() => {
                    Assert.That(server, Is.Null);
                    Assert.That((serverFailed as TLSHandshakeFailedException)?.AlertDescription, Is.EqualTo(43), "unsupported_certificate");
                });

                if (client is not null)
                    await client.Stream.DisposeAsync();

            }

        }

        [Test]
        public async Task AClientWithoutAKey_IsTheValidatorsToJudge()
        {

            using var serverKey  = Carrier();
            using var stationKey = Carrier("CN=station.invalid");

            foreach (var letIn in new[] { true, false })
            {

                var (client, clientFailed, server, serverFailed) = await Connect(

                    new RawPublicKeyTLSClientParameters {
                        ServerKeyPins             = PinnedTo(serverKey, RawPublicKeyMode.Require)
                    },

                    new RawPublicKeyTLSServerParameters {
                        ServerCertificate           = serverKey,
                        ServerRawPublicKeys         = RawPublicKeyMode.Prefer,
                        RequestClientCertificate    = true,
                        ClientRawPublicKeys         = RawPublicKeyMode.Prefer,
                        ClientKeyValidator          = KeyOf(stationKey),
                        ClientCertificateValidator  = (sender, certificate, chain, policyErrors) => certificate is null && letIn
                    }

                );

                if (letIn)
                {

                    Assert.That(serverFailed, Is.Null, serverFailed?.ToString());
                    Assert.That(server!.RemoteKey, Is.Null);

                    await server.Stream.DisposeAsync();

                }
                else
                {
                    Assert.That(server, Is.Null);
                    Assert.That((serverFailed as TLSHandshakeFailedException)?.AlertDescription, Is.EqualTo(116), "certificate_required");
                }

                if (client is not null)
                    await client.Stream.DisposeAsync();

            }

        }

        #endregion

    }

}
