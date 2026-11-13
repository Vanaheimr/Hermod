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
using System.Net.Sockets;
using System.Text;

using NUnit.Framework;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TLS.KeyPins
{

    /// <summary>
    /// Raw public keys (RFC 7250) over TCP between Hermod's second TLS
    /// stack and OpenSSL, in both roles: against "openssl s_server" and
    /// "openssl s_client" with -enable_server_rpk and -enable_client_rpk.
    /// </summary>
    /// <remarks>
    /// Ignored, with a message that says so, where no OpenSSL 3.2 or newer
    /// is found: see <see cref="OpenSSLForTests"/>.
    /// </remarks>
    [TestFixture]
    public class RawPublicKeyOpenSSLOverTCPTests
    {

        #region (helpers)

        private static readonly TimeSpan Guard = TimeSpan.FromSeconds(30);

        private static Int32 FreePort()
        {
            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            return ((System.Net.IPEndPoint) listener.LocalEndpoint).Port;
        }

        private static async Task<TcpClient> Dial(Int32 Port)
        {
            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(System.Net.IPAddress.Loopback, Port).WaitAsync(Guard);
            return tcpClient;
        }

        private static async Task<String> ReadToEnd(Stream Stream)
        {

            var text   = new StringBuilder();
            var buffer = new Byte[4096];

            while (true)
            {

                var read = await Stream.ReadAsync(buffer).AsTask().WaitAsync(Guard);

                if (read == 0)
                    return text.ToString();

                text.Append(Encoding.ASCII.GetString(buffer, 0, read));

            }

        }

        #endregion


        #region Hermod as the client, OpenSSL as the server

        [Test]
        public async Task Hermod_TakesTheRawPublicKey_OfAnOpenSSLServer_AndShowsItsOwn()
        {

            var openssl = OpenSSLForTests.Required();

            using var serverFiles = new RawPublicKeyOpenSSLTests.KeyFiles();
            using var stationKey  = BouncyCastleTLSTests.Carrier("CN=station.invalid");

            var port = FreePort();

            using var server = new OpenSSLForTests.Running(openssl,
                                                           [ "s_server", "-accept", $"127.0.0.1:{port}",
                                                             "-key", serverFiles.KeyFile, "-cert", serverFiles.CertificateFile,
                                                             "-tls1_3", "-www", "-verify", "1",
                                                             "-enable_server_rpk", "-enable_client_rpk" ]);

            Assert.That(await server.Says("ACCEPT", Guard), Is.True, server.Output);

            var told = new ConcurrentQueue<KeyPinValidation>();

            using var tcpClient = await Dial(port);

            var session = await BouncyCastleTLS.AuthenticateAsClientAsync(
                                    tcpClient.GetStream(),
                                    new RawPublicKeyTLSClientParameters {
                                        ServerKeyPins             = new KeyPinPolicy([ serverFiles.KeyPin ], RawPublicKeys: RawPublicKeyMode.Require),
                                        OnServerKeyPinValidated   = told.Enqueue,
                                        ClientCertificate         = stationKey
                                    }
                                ).WaitAsync(Guard);

            await using var stream = session.Stream;

            await stream.WriteAsync(Encoding.ASCII.GetBytes("GET / HTTP/1.0\r\n\r\n")).AsTask().WaitAsync(Guard);

            var answer = await ReadToEnd(stream);

            TestContext.Out.WriteLine(answer);
            TestContext.Out.WriteLine(server.Output);

            Assert.Multiple(() => {

                Assert.That(session.RemoteCertificateType,      Is.EqualTo(TLSCertificateType.RawPublicKey));
                Assert.That(session.LocalCertificateType,       Is.EqualTo(TLSCertificateType.RawPublicKey));
                Assert.That(session.RemoteKey?.IsRawPublicKey,  Is.True);
                Assert.That(session.RemoteKey?.KeyPin,          Is.EqualTo(serverFiles.KeyPin));
                Assert.That(told.Single().Outcome,              Is.EqualTo(KeyPinOutcome.Accepted));
                Assert.That(told.Single().CertificateType,      Is.EqualTo(TLSCertificateType.RawPublicKey));

                // What OpenSSL made of it: it answered, and what it was shown by the
                // client was a raw public key - no certificate, and a key nobody told it to trust.
                Assert.That(answer,                             Does.StartWith("HTTP/1.0 200"));
                Assert.That(answer,                             Does.Contain("TLSv1.3"));
                Assert.That(answer,                             Does.Contain("Verify return code: 95 (Raw public key untrusted"));
                Assert.That(server.Output,                      Does.Contain("depth=0 <no cert>"));

            });

        }

        [Test]
        public async Task Hermod_RefusesAnOpenSSLServer_WhoseKeyIsNotPinned()
        {

            var openssl = OpenSSLForTests.Required();

            using var serverFiles = new RawPublicKeyOpenSSLTests.KeyFiles();
            using var otherKey    = BouncyCastleTLSTests.Carrier();

            var port = FreePort();

            using var server = new OpenSSLForTests.Running(openssl,
                                                           [ "s_server", "-accept", $"127.0.0.1:{port}",
                                                             "-key", serverFiles.KeyFile, "-cert", serverFiles.CertificateFile,
                                                             "-tls1_3", "-www", "-enable_server_rpk" ]);

            Assert.That(await server.Says("ACCEPT", Guard), Is.True, server.Output);

            var told = new ConcurrentQueue<KeyPinValidation>();

            using var tcpClient = await Dial(port);

            var e = await Assert.ThrowsAsync<TLSHandshakeFailedException>(
                        async () => await BouncyCastleTLS.AuthenticateAsClientAsync(
                                              tcpClient.GetStream(),
                                              new RawPublicKeyTLSClientParameters {
                                                  ServerKeyPins            = BouncyCastleTLSTests.PinnedTo(otherKey, RawPublicKeyMode.Require),
                                                  OnServerKeyPinValidated  = told.Enqueue
                                              }
                                          ).WaitAsync(Guard)
                    );

            Assert.Multiple(() => {
                Assert.That(e!.AlertDescription,     Is.EqualTo(42), "bad_certificate");
                Assert.That(e.Message,               Does.Contain(serverFiles.KeyPin.ToHex()));
                Assert.That(told.Single().Outcome,   Is.EqualTo(KeyPinOutcome.Refused));
                Assert.That(told.Single().Pin,       Is.EqualTo(serverFiles.KeyPin));
            });

        }

        [Test]
        public async Task Hermod_WithAnOpenSSLServerWithoutRawKeys_TakesItsCertificateByItsPin_OrRefusesIt()
        {

            var openssl = OpenSSLForTests.Required();

            using var serverFiles = new RawPublicKeyOpenSSLTests.KeyFiles();

            var port = FreePort();

            // No -enable_server_rpk: this server shows its certificate, whatever it is offered.
            using var server = new OpenSSLForTests.Running(openssl,
                                                           [ "s_server", "-accept", $"127.0.0.1:{port}",
                                                             "-key", serverFiles.KeyFile, "-cert", serverFiles.CertificateFile,
                                                             "-tls1_3", "-www" ]);

            Assert.That(await server.Says("ACCEPT", Guard), Is.True, server.Output);

            // Prefer: the certificate, by the pin of its key.
            using (var tcpClient = await Dial(port))
            {

                var session = await BouncyCastleTLS.AuthenticateAsClientAsync(
                                        tcpClient.GetStream(),
                                        new RawPublicKeyTLSClientParameters {
                                            ServerKeyPins = new KeyPinPolicy([ serverFiles.KeyPin ], RawPublicKeys: RawPublicKeyMode.Prefer)
                                        }
                                    ).WaitAsync(Guard);

                await using var stream = session.Stream;

                Assert.Multiple(() => {
                    Assert.That(session.RemoteCertificateType,        Is.EqualTo(TLSCertificateType.X509));
                    Assert.That(session.RemoteCertificate?.Subject,   Is.EqualTo("CN=station.invalid"));
                    Assert.That(session.RemoteKey?.KeyPin,            Is.EqualTo(serverFiles.KeyPin));
                });

            }

            // Require: no connection.
            using (var tcpClient = await Dial(port))
            {

                var e = await Assert.ThrowsAsync<TLSHandshakeFailedException>(
                            async () => await BouncyCastleTLS.AuthenticateAsClientAsync(
                                                  tcpClient.GetStream(),
                                                  new RawPublicKeyTLSClientParameters {
                                                      ServerKeyPins = new KeyPinPolicy([ serverFiles.KeyPin ], RawPublicKeys: RawPublicKeyMode.Require)
                                                  }
                                              ).WaitAsync(Guard)
                        );

                TestContext.Out.WriteLine(e!.Message);

            }

        }

        #endregion

        #region A server of Hermod's own that reads the ClientHello ahead

        [Test]
        public async Task OpenSSL_AndAWebSocketServer_RawKeysAndCertificatesOnOnePort()
        {

            var openssl = OpenSSLForTests.Required();

            using var serverKey = BouncyCastleTLSTests.Carrier();

            // SslStream on Windows needs a key it can find again.
            using var forSslStream = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12(
                                         serverKey.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx, "x"),
                                         "x",
                                         System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.UserKeySet
                                     );

            await using var server = new Hermod.WebSocket.WebSocketMirrorServer(
                                         HTTPPort:                   Hermod.IPPort.Zero,
                                         RequireAuthentication:      false,
                                         ServerCertificateSelector:  (tcpServer, tcpClient) => forSslStream,
                                         AutoStart:                  false
                                     ) {
                                         RawPublicKeys = RawPublicKeyMode.Prefer
                                     };

            server.Start();

            // A client that takes raw public keys: the ClientHello of OpenSSL is read ahead,
            // and the stack that speaks raw public keys answers it.
            var (_, raw) = await Task.Run(() => OpenSSLForTests.Run(openssl,
                                                                    [ "s_client", "-connect", $"127.0.0.1:{server.IPPort}", "-enable_server_rpk" ],
                                                                    Guard));

            TestContext.Out.WriteLine(raw);

            // One that does not: SslStream, given what was read, as if nothing had been.
            var (_, x509) = await Task.Run(() => OpenSSLForTests.Run(openssl,
                                                                     [ "s_client", "-connect", $"127.0.0.1:{server.IPPort}" ],
                                                                     Guard));

            TestContext.Out.WriteLine(x509);

            Assert.Multiple(() => {

                Assert.That(raw,   Does.Contain("Server-to-client raw public key negotiated"));
                Assert.That(raw,   Does.Contain("Server raw public key"));
                Assert.That(raw,   Does.Not.Contain("carrier.invalid"));

                Assert.That(x509,  Does.Contain("carrier.invalid"));
                Assert.That(x509,  Does.Not.Contain("raw public key negotiated"));
                Assert.That(x509,  Does.Contain("Server certificate"));

            });

        }

        #endregion

        #region Hermod as the server, OpenSSL as the client

        private static async Task<(TLSSession? Session, Exception? Failed, String Output)>

            ServeOpenSSL(String                           OpenSSL,
                         RawPublicKeyTLSServerParameters  Parameters,
                         params String[]                  ClientArguments)

        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var port = ((System.Net.IPEndPoint) listener.LocalEndpoint).Port;

            async Task<(TLSSession?, Exception?)> serve()
            {

                var accepted = await listener.AcceptTcpClientAsync().WaitAsync(Guard);

                try
                {
                    return (await BouncyCastleTLS.AuthenticateAsServerAsync(accepted.GetStream(), Parameters).WaitAsync(Guard), null);
                }
                catch (Exception e)
                {
                    accepted.Dispose();
                    return (null, e);
                }

            }

            var serving = serve();

            // With nothing on its standard input s_client leaves once the handshake is done.
            var (_, output) = await Task.Run(() => OpenSSLForTests.Run(OpenSSL,
                                                                       [ "s_client", "-connect", $"127.0.0.1:{port}", "-tls1_3", .. ClientArguments ],
                                                                       Guard));

            var (session, failed) = await serving;

            TestContext.Out.WriteLine(output);

            return (session, failed, output);

        }

        [Test]
        public async Task OpenSSL_TakesTheRawPublicKey_OfHermodsServer_AndShowsItsOwn()
        {

            var openssl = OpenSSLForTests.Required();

            using var serverKey    = BouncyCastleTLSTests.Carrier();
            using var stationFiles = new RawPublicKeyOpenSSLTests.KeyFiles();

            var asked = new ConcurrentQueue<TLSPeerKey>();

            var (session, failed, output) = await ServeOpenSSL(

                openssl,

                new RawPublicKeyTLSServerParameters {
                    ServerCertificate         = serverKey,
                    ServerRawPublicKeys       = RawPublicKeyMode.Prefer,
                    RequestClientCertificate  = true,
                    ClientRawPublicKeys       = RawPublicKeyMode.Require,
                    ClientKeyValidator        = key => {
                                                    asked.Enqueue(key);
                                                    return key.KeyPin == stationFiles.KeyPin
                                                               ? TLSValidationResult.Success()
                                                               : TLSValidationResult.Failed("Not the station's key.");
                                                }
                },

                "-enable_server_rpk", "-enable_client_rpk",
                "-key", stationFiles.KeyFile, "-cert", stationFiles.CertificateFile

            );

            Assert.That(failed, Is.Null, failed?.ToString());

            await using var stream = session!.Stream;

            Assert.Multiple(() => {
                Assert.That(session.LocalCertificateType,       Is.EqualTo(TLSCertificateType.RawPublicKey));
                Assert.That(session.RemoteCertificateType,      Is.EqualTo(TLSCertificateType.RawPublicKey));
                Assert.That(session.RemoteKey?.IsRawPublicKey,  Is.True);
                Assert.That(session.RemoteKey?.KeyPin,          Is.EqualTo(stationFiles.KeyPin));
                Assert.That(asked.Single().KeyPin,              Is.EqualTo(stationFiles.KeyPin));
                Assert.That(output,                             Does.Contain("Server-to-client raw public key negotiated"));
                Assert.That(output,                             Does.Contain("Client-to-server raw public key negotiated"));
                Assert.That(output,                             Does.Contain("Server raw public key"));
            });

        }

        [Test]
        public async Task OpenSSL_WithAKeyThatIsNotTheStations_IsRefusedByHermodsServer()
        {

            var openssl = OpenSSLForTests.Required();

            using var serverKey    = BouncyCastleTLSTests.Carrier();
            using var stationFiles = new RawPublicKeyOpenSSLTests.KeyFiles();

            var (session, failed, output) = await ServeOpenSSL(

                openssl,

                new RawPublicKeyTLSServerParameters {
                    ServerCertificate         = serverKey,
                    ServerRawPublicKeys       = RawPublicKeyMode.Prefer,
                    RequestClientCertificate  = true,
                    ClientRawPublicKeys       = RawPublicKeyMode.Require,
                    ClientKeyValidator        = key => TLSValidationResult.Failed("Not the station's key.")
                },

                "-enable_server_rpk", "-enable_client_rpk",
                "-key", stationFiles.KeyFile, "-cert", stationFiles.CertificateFile

            );

            Assert.Multiple(() => {
                Assert.That(session, Is.Null);
                Assert.That((failed as TLSHandshakeFailedException)?.AlertDescription, Is.EqualTo(49), "access_denied");
            });

        }

        [Test]
        public async Task OpenSSL_WithoutRawKeys_IsShownTheCertificateOfHermodsServer_OrRefused()
        {

            var openssl = OpenSSLForTests.Required();

            using var serverKey = BouncyCastleTLSTests.Carrier();

            // Prefer: the certificate.
            {

                var (session, failed, output) = await ServeOpenSSL(
                    openssl,
                    new RawPublicKeyTLSServerParameters {
                        ServerCertificate    = serverKey,
                        ServerRawPublicKeys  = RawPublicKeyMode.Prefer
                    }
                );

                Assert.That(failed, Is.Null, failed?.ToString());

                await using var stream = session!.Stream;

                Assert.That(session.LocalCertificateType, Is.EqualTo(TLSCertificateType.X509));
                Assert.That(output,                       Does.Contain("carrier.invalid"));
                Assert.That(output,                       Does.Not.Contain("raw public key negotiated"));

            }

            // Require: no handshake, and the certificate is not shown.
            {

                var (session, failed, output) = await ServeOpenSSL(
                    openssl,
                    new RawPublicKeyTLSServerParameters {
                        ServerCertificate    = serverKey,
                        ServerRawPublicKeys  = RawPublicKeyMode.Require
                    }
                );

                Assert.Multiple(() => {
                    Assert.That(session, Is.Null);
                    Assert.That((failed as TLSHandshakeFailedException)?.AlertDescription, Is.EqualTo(43), "unsupported_certificate");
                    Assert.That(output, Does.Not.Contain("carrier.invalid"));
                });

            }

        }

        #endregion

    }

}
