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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;
using org.GraphDefined.Vanaheimr.Hermod.HTTP3;
using org.GraphDefined.Vanaheimr.Hermod.Quic;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;
using org.GraphDefined.Vanaheimr.Hermod.TCP;
using org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TLS.KeyPins
{

    /// <summary>
    /// Key pins over real handshakes: a peer with a self-signed certificate
    /// that no CA vouches for, with a name that is not the one dialled, is
    /// accepted by its key pin alone, and the same peer with another key is
    /// refused. For WebSockets over HTTP/1.1, for HTTP/2 and for HTTP/3, as
    /// server and as client certificates.
    /// </summary>
    /// <remarks>
    /// No certificate store is touched: the certificates live in memory and
    /// are disposed with the test.
    /// </remarks>
    [TestFixture]
    public class KeyPinHandshakeTests
    {

        #region (helpers)

        /// <summary>
        /// A self-signed carrier certificate for the given key: CA:FALSE, no
        /// keyCertSign, a name nobody dials, and usable by SslStream on
        /// Windows (which needs a key it can find again).
        /// </summary>
        private static X509Certificate2 Carrier(ECDsa    Key,
                                                String   Subject     = "CN=carrier.invalid",
                                                Boolean  ClientAuth  = false)
        {

            var request = new CertificateRequest(Subject, Key, HashAlgorithmName.SHA256);

            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([ new Oid(ClientAuth ? "1.3.6.1.5.5.7.3.2" : "1.3.6.1.5.5.7.3.1") ], false));

            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

            return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx, "x"), "x", X509KeyStorageFlags.UserKeySet);

        }

        private static readonly TimeSpan Guard = TimeSpan.FromSeconds(30);

        private sealed class Told
        {
            public readonly ConcurrentQueue<KeyPinValidation> Results = new();
            public void Add(KeyPinValidation Validation) => Results.Enqueue(Validation);
            public KeyPinOutcome[] Outcomes => Results.Select(result => result.Outcome).ToArray();
        }

        #endregion


        #region WebSocket over HTTP/1.1: the server's key

        private static WebSocketMirrorServer StartWebSocketServer(X509Certificate2                                          Certificate,
                                                                  RemoteTLSClientCertificateValidationHandler<ITCPServer>?  ClientCertificateValidator   = null)

            => new (HTTPPort:                    IPPort.Zero,
                    RequireAuthentication:       false,
                    ServerCertificateSelector:   (tcpServer, tcpClient) => Certificate,
                    ClientCertificateValidator:  ClientCertificateValidator,
                    AutoStart:                   true);

        [Test]
        public async Task WebSocket_ConnectsByKeyPinAlone()
        {

            using var key         = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var certificate = Carrier(key);

            await using var server = StartWebSocketServer(certificate);

            var told = new Told();

            // 127.0.0.1 is not the certificate's name, and no CA vouches for it.
            await using var client = new WebSocketClient(
                                         URL.Parse($"wss://127.0.0.1:{server.IPPort}"),
                                         RemoteCertificateValidator:  KeyPinValidator.ForServerCertificates<IWebSocketClient>(
                                                                          KeyPinPolicy.PinnedTo(KeyPin.FromKey(key)),
                                                                          told.Add
                                                                      ),
                                         ConnectTimeout:              TimeSpan.FromSeconds(5)
                                     );

            var connect = client.Connect();
            if (await Task.WhenAny(connect, Task.Delay(Guard)) != connect)
                Assert.Fail("Connect() did not return");

            var (_, response) = await connect;

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,  Is.EqualTo(HTTPStatusCode.SwitchingProtocols), response.HTTPBodyAsUTF8String);
                Assert.That(told.Outcomes,            Is.EqualTo(new[] { KeyPinOutcome.Accepted }));
                Assert.That(told.Results.Single().Pin, Is.EqualTo(KeyPin.FromKey(key)));
            });

        }

        [Test]
        public async Task WebSocket_RefusesTheSameServer_WithAnotherKey()
        {

            using var key         = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var otherKey    = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var certificate = Carrier(otherKey);

            await using var server = StartWebSocketServer(certificate);

            var connections = 0;
            server.OnNewWebSocketConnection += (timestamp, webSocketServer, connection, eventTrackingId, sharedSubprotocols, selectedSubprotocol, cancellationToken) => {
                Interlocked.Increment(ref connections);
                return Task.CompletedTask;
            };

            var told = new Told();

            await using var client = new WebSocketClient(
                                         URL.Parse($"wss://127.0.0.1:{server.IPPort}"),
                                         RemoteCertificateValidator:  KeyPinValidator.ForServerCertificates<IWebSocketClient>(
                                                                          KeyPinPolicy.PinnedTo(KeyPin.FromKey(key)),
                                                                          told.Add
                                                                      ),
                                         ConnectTimeout:              TimeSpan.FromSeconds(5)
                                     );

            var connect = client.Connect();
            if (await Task.WhenAny(connect, Task.Delay(Guard)) != connect)
                Assert.Fail("Connect() did not return");

            var (_, response) = await connect;

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,         Is.Not.EqualTo(HTTPStatusCode.SwitchingProtocols));
                Assert.That(told.Outcomes,                   Does.Contain(KeyPinOutcome.Refused));
                Assert.That(told.Outcomes,                   Has.No.Member(KeyPinOutcome.Accepted));
                Assert.That(told.Results.First().Pin,        Is.EqualTo(KeyPin.FromKey(otherKey)));
                Assert.That(Volatile.Read(ref connections),  Is.Zero, "no WebSocket connection on the server");
            });

        }

        [Test]
        public async Task WebSocket_ARenewedCarrierCertificate_KeepsItsPin()
        {

            using var key     = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var renewed = Carrier(key, "CN=renewed-with-another-name.invalid");

            await using var server = StartWebSocketServer(renewed);

            await using var client = new WebSocketClient(
                                         URL.Parse($"wss://127.0.0.1:{server.IPPort}"),
                                         RemoteCertificateValidator:  KeyPinValidator.ForServerCertificates<IWebSocketClient>(
                                                                          KeyPinPolicy.PinnedTo(KeyPin.FromKey(key))
                                                                      ),
                                         ConnectTimeout:              TimeSpan.FromSeconds(5)
                                     );

            var (_, response) = await client.Connect();

            Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.SwitchingProtocols), response.HTTPBodyAsUTF8String);

        }

        #endregion

        #region WebSocket over HTTP/1.1: the client's key

        [Test]
        public async Task WebSocket_Server_AdmitsAClientByItsKeyPin_AndRefusesAnother()
        {

            using var serverKey   = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var stationKey  = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var strangerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

            using var serverCertificate   = Carrier(serverKey);
            using var stationCertificate  = Carrier(stationKey,  "CN=station.invalid",  ClientAuth: true);
            using var strangerCertificate = Carrier(strangerKey, "CN=station.invalid",  ClientAuth: true);

            var told = new Told();

            await using var server = StartWebSocketServer(serverCertificate,
                                                          KeyPinValidator.ForClientCertificates<ITCPServer>(
                                                              KeyPinPolicy.PinnedTo(KeyPin.FromKey(stationKey)),
                                                              told.Add
                                                          ));

            var connections = 0;
            server.OnNewWebSocketConnection += (timestamp, webSocketServer, connection, eventTrackingId, sharedSubprotocols, selectedSubprotocol, cancellationToken) => {
                Interlocked.Increment(ref connections);
                return Task.CompletedTask;
            };

            WebSocketClient Client(X509Certificate2 ClientCertificate)

                => new (URL.Parse($"wss://127.0.0.1:{server.IPPort}"),
                        RemoteCertificateValidator:  KeyPinValidator.ForServerCertificates<IWebSocketClient>(KeyPinPolicy.PinnedTo(KeyPin.FromKey(serverKey))),
                        LocalCertificateSelector:    (sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers) => ClientCertificate,
                        ClientCertificates:          [ ClientCertificate ],
                        ConnectTimeout:              TimeSpan.FromSeconds(5));

            await using (var station = Client(stationCertificate))
            {
                var (_, response) = await station.Connect();
                Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.SwitchingProtocols), response.HTTPBodyAsUTF8String);
            }

            await using (var stranger = Client(strangerCertificate))
            {
                var connect = stranger.Connect();
                if (await Task.WhenAny(connect, Task.Delay(Guard)) != connect)
                    Assert.Fail("Connect() did not return");
                var (_, response) = await connect;
                Assert.That(response.HTTPStatusCode, Is.Not.EqualTo(HTTPStatusCode.SwitchingProtocols), "the stranger has the same name, but another key");
            }

            Assert.Multiple(() => {
                Assert.That(told.Results.First().Outcome,    Is.EqualTo(KeyPinOutcome.Accepted));
                Assert.That(told.Results.First().Pin,        Is.EqualTo(KeyPin.FromKey(stationKey)));
                Assert.That(told.Outcomes,                   Does.Contain(KeyPinOutcome.Refused));
                Assert.That(told.Results.Where(result => result.Outcome == KeyPinOutcome.Refused).Select(result => result.Pin),
                            Is.All.EqualTo(KeyPin.FromKey(strangerKey)));
                Assert.That(Volatile.Read(ref connections),  Is.EqualTo(1), "only the station's");
            });

        }

        #endregion


        #region HTTP/2

        private static Boolean ServesLocalhost(String Authority)
            => Authority.Split(':')[0] == "localhost";

        [Test]
        public async Task HTTP2_ACarrierCertificate_ServesNoOrigin_UnlessTheServerIsTold()
        {

            // Why the server of a key-pinned endpoint needs IsAuthorityServed or an OriginSet: by
            // default it serves the origins of its certificate, and a carrier names none.
            using var key         = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var certificate = Carrier(key);

            await using var server = await TestH2Server.StartAsync(Hello, Certificate: certificate);

            var connection = await HTTP2Client.ConnectAsync("localhost",
                                                            server.Port,
                                                            KeyPinValidator.ForSslStream(KeyPinPolicy.PinnedTo(KeyPin.FromKey(key))));

            var response = await connection.SendRequestAsync(HTTPMethod.GET, URIScheme.https, $"localhost:{server.Port}", "/");
            await connection.CloseAsync();

            Assert.That(response.Status, Is.EqualTo(421));

        }

        private static Task<(List<(String, String)>, Byte[]?)> Hello(UInt32 StreamId, List<(String Name, String Value)> Headers, Byte[]? Body, CancellationToken CancellationToken)
        {
            var body = Encoding.UTF8.GetBytes("hello");
            return Task.FromResult<(List<(String, String)>, Byte[]?)>(
                       ([(":status", "200"), ("content-length", body.Length.ToString())], body));
        }

        [Test]
        public async Task HTTP2_ConnectsByKeyPinAlone_AndRefusesAnotherKey()
        {

            using var key         = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var otherKey    = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var certificate = Carrier(key);

            // A carrier certificate names no origin, so the server is told which ones it serves:
            // by default it takes them from its certificate and answers every request with 421.
            await using var server = await TestH2Server.StartAsync(Hello, Certificate: certificate, IsAuthorityServed: ServesLocalhost);

            var connection = await HTTP2Client.ConnectAsync("localhost",
                                                            server.Port,
                                                            KeyPinValidator.ForSslStream(KeyPinPolicy.PinnedTo(KeyPin.FromKey(key))));

            var response = await connection.SendRequestAsync(HTTPMethod.GET, URIScheme.https, $"localhost:{server.Port}", "/");
            await connection.CloseAsync();

            Assert.That(response.Status, Is.EqualTo(200));

            var told = new Told();

            Assert.That(async () => await HTTP2Client.ConnectAsync("localhost",
                                                                   server.Port,
                                                                   KeyPinValidator.ForSslStream(KeyPinPolicy.PinnedTo(KeyPin.FromKey(otherKey)), told.Add)),
                        Throws.InstanceOf<System.Security.Authentication.AuthenticationException>());

            Assert.That(told.Outcomes, Is.EqualTo(new[] { KeyPinOutcome.Refused }));

        }

        [Test]
        public async Task HTTP2_Server_AdmitsAClientByItsKeyPin_AndRefusesAnother()
        {

            using var serverKey   = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var stationKey  = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var strangerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

            using var serverCertificate   = Carrier(serverKey);
            using var stationCertificate  = Carrier(stationKey,  "CN=station.invalid", ClientAuth: true);
            using var strangerCertificate = Carrier(strangerKey, "CN=station.invalid", ClientAuth: true);

            await using var server = await TestH2Server.StartAsync(Hello,
                                                                   Certificate:                serverCertificate,
                                                                   RequireClientCertificate:   true,
                                                                   ValidateClientCertificate:  KeyPinValidator.ForSslStream(KeyPinPolicy.PinnedTo(KeyPin.FromKey(stationKey))),
                                                                   IsAuthorityServed:          ServesLocalhost);

            var pinnedServer = KeyPinValidator.ForSslStream(KeyPinPolicy.PinnedTo(KeyPin.FromKey(serverKey)));

            var connection = await HTTP2Client.ConnectAsync("localhost", server.Port, pinnedServer, ClientCertificate: stationCertificate);
            var response   = await connection.SendRequestAsync(HTTPMethod.GET, URIScheme.https, $"localhost:{server.Port}", "/");
            await connection.CloseAsync();

            Assert.That(response.Status, Is.EqualTo(200));

            // TLS 1.3 sends the client's Certificate after the server's Finished: the server's
            // refusal may arrive at the handshake or at the first read, but no answer comes.
            var refused = false;
            try
            {
                var stranger = await HTTP2Client.ConnectAsync("localhost", server.Port, pinnedServer, ClientCertificate: strangerCertificate);
                await stranger.SendRequestAsync(HTTPMethod.GET, URIScheme.https, $"localhost:{server.Port}", "/");
            }
            catch
            {
                refused = true;
            }

            Assert.That(refused, Is.True, "a client with another key gets no answer");

        }

        #endregion


        #region QUIC TLS 1.3 and HTTP/3

        private static readonly Byte[] TransportParameters = [ 0x0f, 0x00 ];

        private static void RunHandshake(TlsClientHandshake Client, TlsServerHandshake Server)
        {

            Client.Start();

            for (var round = 0; round < 10 && !(Client.IsComplete && Server.IsComplete); round++)
            {
                Pump(Client, Server);
                Pump(Server, Client);
            }

            Assert.That(Client.IsComplete, Is.True, "Client handshake incomplete.");
            Assert.That(Server.IsComplete, Is.True, "Server handshake incomplete.");

        }

        private static void Pump(ITlsHandshake From, ITlsHandshake To)
        {
            while (From.TryGetOutgoingCrypto(out EncryptionLevel level, out Byte[] data))
                To.ProvideCrypto(level, data);
        }

        [Test]
        public void QUIC_ConnectsByKeyPinAlone_AndRefusesAnotherKey()
        {

            using var certificate = ServerCertificate.CreateSelfSigned("localhost");
            using var other       = ServerCertificate.CreateSelfSigned("localhost");

            var told   = new Told();

            // A name that is not the certificate's: only the key counts.
            var client = new TlsClientHandshake("somewhere-else.invalid", TransportParameters,
                                                certificateValidation: CertificateValidationOptions.PinnedTo(KeyPinPolicy.PinnedTo(certificate.KeyPin), told.Add));
            using (var server = new TlsServerHandshake(certificate, TransportParameters))
                RunHandshake(client, server);

            Assert.Multiple(() => {
                Assert.That(client.ServerCertificateValid, Is.True);
                Assert.That(told.Outcomes,                 Is.EqualTo(new[] { KeyPinOutcome.Accepted }));
            });

            client.Dispose();

            var refused = new TlsClientHandshake("localhost", TransportParameters,
                                                 certificateValidation: CertificateValidationOptions.PinnedTo(KeyPinPolicy.PinnedTo(other.KeyPin)));
            using (var server = new TlsServerHandshake(certificate, TransportParameters))
            {
                var e = Assert.Throws<CertificateValidationException>(() => RunHandshake(refused, server));
                Assert.That(e!.Message, Does.Contain(certificate.KeyPin.ToHex()));
            }

            refused.Dispose();

        }

        [Test]
        public void QUIC_AMismatch_DoesNotFallBackToTheCustomRoots()
        {

            using var certificate = ServerCertificate.CreateSelfSigned("localhost");
            using var other       = ServerCertificate.CreateSelfSigned("localhost");

            // The CA path would accept this server; the key pins do not.
            var client = new TlsClientHandshake("localhost", TransportParameters,
                                                certificateValidation: new CertificateValidationOptions {
                                                                           CustomTrustRoots  = [ certificate.Certificate ],
                                                                           KeyPins           = KeyPinPolicy.PinnedTo(other.KeyPin)
                                                                       });

            using (var server = new TlsServerHandshake(certificate, TransportParameters))
                Assert.Throws<CertificateValidationException>(() => RunHandshake(client, server));

            client.Dispose();

        }

        [Test]
        public void QUIC_Server_AdmitsAClientByItsKeyPin_AndRefusesAnother()
        {

            using var certificate = ServerCertificate.CreateSelfSigned("localhost");
            using var station     = ServerCertificate.CreateSelfSigned("station.invalid");
            using var stranger    = ServerCertificate.CreateSelfSigned("station.invalid");

            var told    = new Told();
            var options = ClientCertificateOptions.RequirePinned(KeyPinPolicy.PinnedTo(station.KeyPin), told.Add);

            var client  = new TlsClientHandshake("localhost", TransportParameters,
                                                 certificateValidation: CertificateValidationOptions.PinnedTo(KeyPinPolicy.PinnedTo(certificate.KeyPin)),
                                                 clientCertificate:     station);

            using (var server = new TlsServerHandshake(certificate, TransportParameters, clientCertificate: options))
            {
                RunHandshake(client, server);
                Assert.That(server.ClientCertificate, Is.Not.Null);
                Assert.That(KeyPin.FromCertificate(server.ClientCertificate!), Is.EqualTo(station.KeyPin));
            }

            client.Dispose();

            var strangerClient = new TlsClientHandshake("localhost", TransportParameters,
                                                        certificateValidation: CertificateValidationOptions.PinnedTo(KeyPinPolicy.PinnedTo(certificate.KeyPin)),
                                                        clientCertificate:     stranger);

            using (var server = new TlsServerHandshake(certificate, TransportParameters, clientCertificate: options))
            {
                var e = Assert.Throws<TlsHandshakeException>(() => RunHandshake(strangerClient, server));
                Assert.That(e!.Message, Does.Contain(stranger.KeyPin.ToHex()));
            }

            strangerClient.Dispose();

            Assert.That(told.Outcomes, Is.EqualTo(new[] { KeyPinOutcome.Accepted, KeyPinOutcome.Refused }));

        }

        [Test]
        public void HTTP3_GetsAnAnswer_FromAServerPinnedByKey()
        {

            using var certificate = ServerCertificate.CreateSelfSigned("carrier.invalid");

            Http3Response Handler(Http3Request request) => new() {
                Status  = 200,
                Body    = Encoding.UTF8.GetBytes("pinned")
            };

            using var server = new Http3ServerConnection(certificate, Handler);
            using var client = new Http3ClientConnection("localhost",
                                                         certificateValidation: CertificateValidationOptions.PinnedTo(KeyPinPolicy.PinnedTo(certificate.KeyPin)));
            client.Start();

            UInt64         requestStream = 0;
            var            requestSent   = false;
            Http3Response? response      = null;

            for (var round = 0; round < 20 && response is null; round++)
            {

                client.CheckTimeouts();

                foreach (var datagram in client.GetDatagramsToSend())
                    server.ProcessDatagram(datagram);

                foreach (var datagram in server.GetDatagramsToSend())
                    client.ProcessDatagram(datagram);

                if (client.HandshakeConfirmed && !requestSent)
                {
                    client.InitializeHttp3();
                    requestStream = client.SendRequest(Http3Request.Get("localhost", "/"));
                    requestSent   = true;
                }

                if (requestSent)
                    client.TryGetResponse(requestStream, out response);

            }

            Assert.That(response,           Is.Not.Null);
            Assert.That(response!.BodyText, Is.EqualTo("pinned"));

        }

        #endregion

    }

}
