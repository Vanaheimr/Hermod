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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;
using org.GraphDefined.Vanaheimr.Hermod.TCP;
using org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TLS.KeyPins
{

    /// <summary>
    /// Raw public keys (RFC 7250) over TCP, where Hermod's servers and
    /// clients speak TLS by SslStream, which knows certificates alone: a
    /// WebSocket over HTTP/1.1 and over HTTP/2 whose server shows its key
    /// and no certificate, and whose client does the same. An endpoint that
    /// does not ask for raw public keys is SslStream's, as before, on the
    /// same port.
    /// </summary>
    /// <remarks>
    /// Over real sockets. No certificate store is touched.
    /// </remarks>
    [TestFixture]
    public class RawPublicKeyOverTCPTests
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
        /// A self-signed carrier certificate for the given key, usable by
        /// SslStream on Windows as well (which needs a key it can find again).
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

        /// <summary>
        /// A WebSocket server and what came to it.
        /// </summary>
        private sealed class Served : IAsyncDisposable
        {

            public WebSocketMirrorServer                           Server       { get; }
            public ConcurrentQueue<WebSocketServerConnection>      Connections  { get; } = new();

            public Served(X509Certificate2                                          Certificate,
                          RawPublicKeyMode                                          RawPublicKeys,
                          RemoteTLSClientCertificateValidationHandler<ITCPServer>?  ClientCertificateValidator   = null,
                          RawPublicKeyMode                                          ClientRawPublicKeys          = RawPublicKeyMode.Off,
                          RemoteTLSClientKeyValidationHandler<ITCPServer>?          ClientKeyValidator           = null,
                          Boolean                                                   AcceptHTTP2                  = false,
                          UInt16?                                                   Port                         = null,
                          Boolean                                                   AcceptHTTP3                  = false)
            {

                Server = new WebSocketMirrorServer(
                             HTTPPort:                    Port.HasValue ? IPPort.Parse(Port.Value) : IPPort.Zero,
                             RequireAuthentication:       false,
                             ServerCertificateSelector:   (tcpServer, tcpClient) => Certificate,
                             ClientCertificateValidator:  ClientCertificateValidator,
                             AutoStart:                   false
                         ) {
                             RawPublicKeys        = RawPublicKeys,
                             ClientRawPublicKeys  = ClientRawPublicKeys,
                             ClientKeyValidator   = ClientKeyValidator,
                             AcceptHTTP2          = AcceptHTTP2
                         };

                Server.OnNewWebSocketConnection += (timestamp, webSocketServer, connection, eventTrackingId, sharedSubprotocols, selectedSubprotocol, cancellationToken) => {
                    Connections.Enqueue(connection);
                    return Task.CompletedTask;
                };

                Server.Start();

                // The key is shown over QUIC as it is over TCP: said before the listener of HTTP/3 starts.
                if (AcceptHTTP3)
                    Server.AcceptHTTP3 = true;

            }

            public String URL
                => $"wss://127.0.0.1:{Server.IPPort}";

            public ValueTask DisposeAsync()
                => Server.DisposeAsync();

        }

        /// <summary>
        /// A WebSocket client whose server is key-pinned, and whose key may come raw.
        /// </summary>
        private static WebSocketClient ClientOf(Served                 Served,
                                                KeyPin                 ServerPin,
                                                RawPublicKeyMode       RawPublicKeys,
                                                Told?                  Told                 = null,
                                                X509Certificate2?      ClientCertificate    = null,
                                                WebSocketHTTPVersion?  Over                 = null)
        {

            var policy = new KeyPinPolicy([ ServerPin ], RawPublicKeys: RawPublicKeys);

            var client = new WebSocketClient(

                             Hermod.HTTP.URL.Parse(Served.URL),

                             // Where the key pins offer no raw public keys the handshake is SslStream's, and this is asked.
                             RemoteCertificateValidator:  KeyPinValidator.ForServerCertificates<IWebSocketClient>(policy, Told is null ? null : Told.Add),

                             LocalCertificateSelector:    ClientCertificate is not null
                                                              ? (sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers) => ClientCertificate
                                                              : null,
                             ClientCertificates:          ClientCertificate is not null
                                                              ? [ ClientCertificate ]
                                                              : null,

                             ConnectTimeout:              TimeSpan.FromSeconds(5)

                         ) {

                             // Where they do, it is the handshake of the stack that speaks them, and these are asked.
                             ServerKeyPins            = () => policy,
                             OnServerKeyPinValidated  = Told is null ? null : Told.Add

                         };

            if (Over.HasValue)
                client.WebSocketOver = Over.Value;

            return client;

        }

        private static async Task<HTTPResponse> Connect(WebSocketClient Client)
        {

            var connect = Client.Connect();

            if (await Task.WhenAny(connect, Task.Delay(Guard)) != connect)
                Assert.Fail("Connect() did not return");

            var (_, response) = await connect;

            return response;

        }

        /// <summary>
        /// A text and a binary message of some size go there and come back mirrored.
        /// </summary>
        private static async Task TheMirrorAnswers(WebSocketClient Client)
        {

            var texts     = new ConcurrentQueue<String>();
            var binaries  = new ConcurrentQueue<Byte[]>();

            Client.OnTextMessageReceived   += (timestamp, client, connection, frame, eventTrackingId, textMessage,   cancellationToken) => { texts.   Enqueue(textMessage);   return Task.CompletedTask; };
            Client.OnBinaryMessageReceived += (timestamp, client, connection, frame, eventTrackingId, binaryMessage, cancellationToken) => { binaries.Enqueue(binaryMessage); return Task.CompletedTask; };

            // More than one TLS record.
            var text = String.Concat(Enumerable.Range(0, 40000).Select(i => (i % 10).ToString())) + "1234";

            await Client.SendTextMessage  (text);
            await Client.SendBinaryMessage("ABCD".ToUTF8Bytes());

            Assert.That(SpinWait.SpinUntil(() => texts.Count > 0 && binaries.Count > 0, TimeSpan.FromSeconds(10)), Is.True,
                        "The server did not mirror what it was sent.");

            Assert.Multiple(() => {
                Assert.That(texts.   First(),                 Is.EqualTo(text.Reverse()));
                Assert.That(binaries.First().ToUTF8String(),  Is.EqualTo("DCBA"));
            });

        }

        private static RemoteTLSClientKeyValidationHandler<ITCPServer> KeyOf(KeyPin Pin, ConcurrentQueue<TLSPeerKey>? Asked = null)

            => (sender, clientKey, tlsServer) => {
                   Asked?.Enqueue(clientKey);
                   return clientKey.KeyPin == Pin
                              ? TLSValidationResult.Success()
                              : TLSValidationResult.Failed($"The key pin {clientKey.KeyPin} is not the station's.");
               };

        #endregion


        #region WebSocket: the server's key

        [Test]
        public async Task WebSocket_TheServersKey_ComesRaw([Values(RawPublicKeyMode.Prefer, RawPublicKeyMode.Require)] RawPublicKeyMode ClientMode,
                                                          [Values(RawPublicKeyMode.Prefer, RawPublicKeyMode.Require)] RawPublicKeyMode ServerMode,
                                                          [Values(WebSocketHTTPVersion.HTTP1_1, WebSocketHTTPVersion.HTTP2)] WebSocketHTTPVersion Version)
        {

            using var key         = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var certificate = Carrier(key);

            await using var served = new Served(certificate, ServerMode, AcceptHTTP2: true);

            var told = new Told();

            // 127.0.0.1 is not the certificate's name, no CA vouches for it - and no certificate is shown.
            await using var client = ClientOf(served, KeyPin.FromKey(key), ClientMode, told, Over: Version);

            var response = await Connect(client);

            Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.SwitchingProtocols), response.EntirePDU);

            Assert.That(SpinWait.SpinUntil(() => !served.Connections.IsEmpty, TimeSpan.FromSeconds(10)), Is.True, "The server took no WebSocket.");

            // Over HTTP/2 the WebSocket is a stream of a connection, and does not say which TLS it runs over.
            var session = served.Connections.First().TCPConnection?.TLSSession;

            Assert.Multiple(() => {

                Assert.That(client.TLSSession?.Provider,                Is.EqualTo(TLSProvider.BouncyCastle));
                Assert.That(client.TLSSession?.RemoteCertificateType,   Is.EqualTo(TLSCertificateType.RawPublicKey));
                Assert.That(client.TLSSession?.RemoteKey?.IsRawPublicKey, Is.True);
                Assert.That(client.TLSSession?.RemoteKey?.KeyPin,       Is.EqualTo(KeyPin.FromKey(key)));
                Assert.That(client.TLSSession?.RemoteCertificate,       Is.Null);
                Assert.That(client.HTTPVersionInUse,                    Is.EqualTo(Version == WebSocketHTTPVersion.HTTP2 ? HTTPVersion.HTTP_2_0 : HTTPVersion.HTTP_1_1));

                if (Version == WebSocketHTTPVersion.HTTP1_1)
                {
                    Assert.That(session?.Provider,                      Is.EqualTo(TLSProvider.BouncyCastle));
                    Assert.That(session?.LocalCertificateType,          Is.EqualTo(TLSCertificateType.RawPublicKey));
                }

                Assert.That(served.Connections.First().ClientKey,       Is.Null);

                Assert.That(told.Outcomes,                              Is.EqualTo(new[] { KeyPinOutcome.Accepted }));
                Assert.That(told.Results.Single().CertificateType,      Is.EqualTo(TLSCertificateType.RawPublicKey));
                Assert.That(told.Results.Single().Pin,                  Is.EqualTo(KeyPin.FromKey(key)));

            });

            await TheMirrorAnswers(client);

        }

        [Test]
        public async Task WebSocket_AServerKey_ThatIsNotPinned_IsRefused()
        {

            using var key         = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var otherKey    = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var certificate = Carrier(otherKey);

            await using var served = new Served(certificate, RawPublicKeyMode.Prefer);

            var told = new Told();

            await using var client = ClientOf(served, KeyPin.FromKey(key), RawPublicKeyMode.Require, told);

            var response = await Connect(client);

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,         Is.Not.EqualTo(HTTPStatusCode.SwitchingProtocols));
                Assert.That(response.EntirePDU,              Does.Contain(KeyPin.FromKey(otherKey).ToHex()), "The client does not say why.");
                Assert.That(client.TLSSession,               Is.Null);
                Assert.That(told.Outcomes,                   Is.EqualTo(new[] { KeyPinOutcome.Refused }));
                Assert.That(told.Results.Single().Pin,       Is.EqualTo(KeyPin.FromKey(otherKey)));
                Assert.That(told.Results.Single().CertificateType, Is.EqualTo(TLSCertificateType.RawPublicKey));
                Assert.That(served.Connections,              Is.Empty, "no WebSocket connection on the server");
            });

        }

        #endregion

        #region WebSocket: the client's key

        [Test]
        public async Task WebSocket_TheClientsKey_ComesRaw_AndIsAdmittedByItsPin_AndAnotherIsRefused([Values(WebSocketHTTPVersion.HTTP1_1, WebSocketHTTPVersion.HTTP2)] WebSocketHTTPVersion Version)
        {

            using var serverKey   = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var stationKey  = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var strangerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

            using var serverCertificate   = Carrier(serverKey);
            using var stationCertificate  = Carrier(stationKey,  "CN=station.invalid",  ClientAuth: true);
            using var strangerCertificate = Carrier(strangerKey, "CN=station.invalid",  ClientAuth: true);

            var asked = new ConcurrentQueue<TLSPeerKey>();

            await using var served = new Served(serverCertificate,
                                                RawPublicKeyMode.Prefer,
                                                ClientRawPublicKeys:  RawPublicKeyMode.Require,
                                                ClientKeyValidator:   KeyOf(KeyPin.FromKey(stationKey), asked),
                                                AcceptHTTP2:          true);

            await using (var station = ClientOf(served, KeyPin.FromKey(serverKey), RawPublicKeyMode.Require, ClientCertificate: stationCertificate, Over: Version))
            {

                var response = await Connect(station);

                Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.SwitchingProtocols), response.EntirePDU);
                Assert.That(SpinWait.SpinUntil(() => !served.Connections.IsEmpty, TimeSpan.FromSeconds(10)), Is.True, "The server took no WebSocket.");

                var connection = served.Connections.First();

                Assert.Multiple(() => {

                    Assert.That(station.TLSSession?.LocalCertificateType,   Is.EqualTo(TLSCertificateType.RawPublicKey));
                    Assert.That(station.TLSSession?.RemoteCertificateType,  Is.EqualTo(TLSCertificateType.RawPublicKey));

                    // What the server holds of the station: its key, and no certificate - so no subject to take for an identity.
                    Assert.That(connection.ClientCertificate,               Is.Null);
                    Assert.That(connection.ClientKey?.IsRawPublicKey,       Is.True);
                    Assert.That(connection.ClientKey?.KeyPin,               Is.EqualTo(KeyPin.FromKey(stationKey)));
                    if (Version == WebSocketHTTPVersion.HTTP1_1)
                        Assert.That(connection.TCPConnection?.TLSSession?.RemoteCertificateType, Is.EqualTo(TLSCertificateType.RawPublicKey));

                    Assert.That(asked.Single().KeyPin,                      Is.EqualTo(KeyPin.FromKey(stationKey)));

                });

                await TheMirrorAnswers(station);

            }

            await using (var stranger = ClientOf(served, KeyPin.FromKey(serverKey), RawPublicKeyMode.Require, ClientCertificate: strangerCertificate, Over: Version))
            {

                var response = await Connect(stranger);

                Assert.That(response.HTTPStatusCode, Is.Not.EqualTo(HTTPStatusCode.SwitchingProtocols), "the stranger has the same name, but another key");

            }

            Assert.Multiple(() => {
                Assert.That(asked.Select(key => key.KeyPin),  Is.EqualTo(new[] { KeyPin.FromKey(stationKey), KeyPin.FromKey(strangerKey) }));
                Assert.That(served.Connections,               Has.Count.EqualTo(1), "only the station's");
            });

        }

        #endregion

        #region WebSocket: the client's key alone

        [Test]
        public async Task WebSocket_TheClientsKey_ComesRaw_AtAServerThatShowsItsOwnInACertificate()
        {

            using var serverKey   = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var stationKey  = ECDsa.Create(ECCurve.NamedCurves.nistP256);

            using var serverCertificate   = Carrier(serverKey);
            using var stationCertificate  = Carrier(stationKey, "CN=station.invalid", ClientAuth: true);

            // The server never shows its key raw, and takes its clients' keys raw:
            // what its client offers for its own key says which TLS stack answers.
            await using var served = new Served(serverCertificate,
                                                RawPublicKeyMode.Off,
                                                ClientRawPublicKeys:  RawPublicKeyMode.Prefer,
                                                ClientKeyValidator:   KeyOf(KeyPin.FromKey(stationKey)));

            var told = new Told();

            await using var station = ClientOf(served, KeyPin.FromKey(serverKey), RawPublicKeyMode.Prefer, told, stationCertificate);

            var response = await Connect(station);

            Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.SwitchingProtocols), response.EntirePDU);
            Assert.That(SpinWait.SpinUntil(() => !served.Connections.IsEmpty, TimeSpan.FromSeconds(10)), Is.True, "The server took no WebSocket.");

            var connection = served.Connections.First();

            Assert.Multiple(() => {

                // The mixed case: the server's key in its certificate, by its pin ...
                Assert.That(station.TLSSession?.RemoteCertificateType,      Is.EqualTo(TLSCertificateType.X509));
                Assert.That(told.Results.Single().CertificateType,          Is.EqualTo(TLSCertificateType.X509));
                Assert.That(told.Outcomes,                                  Is.EqualTo(new[] { KeyPinOutcome.Accepted }));

                // ... and the client's raw.
                Assert.That(station.TLSSession?.LocalCertificateType,       Is.EqualTo(TLSCertificateType.RawPublicKey));
                Assert.That(connection.ClientCertificate,                   Is.Null);
                Assert.That(connection.ClientKey?.IsRawPublicKey,           Is.True);
                Assert.That(connection.ClientKey?.KeyPin,                   Is.EqualTo(KeyPin.FromKey(stationKey)));
                Assert.That(connection.TCPConnection?.TLSSession?.Provider, Is.EqualTo(TLSProvider.BouncyCastle));

            });

            await TheMirrorAnswers(station);

        }

        #endregion

        #region Fallback: SslStream on the same port, and certificates

        [Test]
        public async Task WebSocket_AClientThatAsksForNoRawKeys_IsSslStreams_OnTheSamePort([Values(WebSocketHTTPVersion.HTTP1_1, WebSocketHTTPVersion.HTTP2)] WebSocketHTTPVersion Version)
        {

            using var serverKey   = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var stationKey  = ECDsa.Create(ECCurve.NamedCurves.nistP256);

            using var serverCertificate   = Carrier(serverKey);
            using var stationCertificate  = Carrier(stationKey, "CN=station.invalid", ClientAuth: true);

            var toldServer = new Told();

            // A server that prefers raw public keys in both directions ...
            await using var served = new Served(serverCertificate,
                                                RawPublicKeyMode.Prefer,
                                                ClientCertificateValidator:  KeyPinValidator.ForClientCertificates<ITCPServer>(
                                                                                 KeyPinPolicy.PinnedTo(KeyPin.FromKey(stationKey)),
                                                                                 toldServer.Add
                                                                             ),
                                                ClientRawPublicKeys:         RawPublicKeyMode.Prefer,
                                                ClientKeyValidator:          KeyOf(KeyPin.FromKey(stationKey)),
                                                AcceptHTTP2:                 true);

            // ... and a client whose key pins know certificates alone: SslStream on both ends, as before.
            var told = new Told();

            await using (var client = ClientOf(served, KeyPin.FromKey(serverKey), RawPublicKeyMode.Off, told, stationCertificate, Version))
            {

                var response = await Connect(client);

                Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.SwitchingProtocols), response.EntirePDU);
                Assert.That(SpinWait.SpinUntil(() => !served.Connections.IsEmpty, TimeSpan.FromSeconds(10)), Is.True, "The server took no WebSocket.");

                var connection = served.Connections.First();

                Assert.Multiple(() => {

                    Assert.That(client.TLSSession?.Provider,                        Is.EqualTo(TLSProvider.SslStream));
                    Assert.That(client.TLSSession?.RemoteCertificateType,           Is.EqualTo(TLSCertificateType.X509));
                    Assert.That(client.TLSSession?.RemoteCertificate?.Subject,      Is.EqualTo("CN=carrier.invalid"));

                    if (Version == WebSocketHTTPVersion.HTTP1_1)
                    {
                        Assert.That(connection.TCPConnection?.TLSSession?.Provider, Is.EqualTo(TLSProvider.SslStream));
                        Assert.That(connection.TCPConnection?.SSLStream,            Is.Not.Null);
                    }

                    Assert.That(connection.ClientCertificate?.Subject,              Is.EqualTo("CN=station.invalid"));
                    Assert.That(connection.ClientKey?.IsRawPublicKey,               Is.False);
                    Assert.That(connection.ClientKey?.KeyPin,                       Is.EqualTo(KeyPin.FromKey(stationKey)));

                    Assert.That(told.Outcomes,                                      Is.EqualTo(new[] { KeyPinOutcome.Accepted }));
                    Assert.That(told.Results.Single().CertificateType,              Is.EqualTo(TLSCertificateType.X509));
                    Assert.That(toldServer.Outcomes,                                Does.Contain(KeyPinOutcome.Accepted));

                });

                await TheMirrorAnswers(client);

            }

        }

        [Test]
        public async Task WebSocket_AClientThatPrefersRawKeys_TakesTheCertificateOfAServerThatHasNone_ByItsPin()
        {

            using var key         = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var certificate = Carrier(key);

            // A server that knows nothing of raw public keys: SslStream, as every server was.
            await using var served = new Served(certificate, RawPublicKeyMode.Off);

            var told = new Told();

            await using var client = ClientOf(served, KeyPin.FromKey(key), RawPublicKeyMode.Prefer, told);

            var response = await Connect(client);

            Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.SwitchingProtocols), response.EntirePDU);
            Assert.That(SpinWait.SpinUntil(() => !served.Connections.IsEmpty, TimeSpan.FromSeconds(10)), Is.True, "The server took no WebSocket.");

            Assert.Multiple(() => {

                Assert.That(client.TLSSession?.Provider,                        Is.EqualTo(TLSProvider.BouncyCastle));
                Assert.That(client.TLSSession?.RemoteCertificateType,           Is.EqualTo(TLSCertificateType.X509));
                Assert.That(client.TLSSession?.RemoteCertificate?.Subject,      Is.EqualTo("CN=carrier.invalid"));
                Assert.That(client.TLSSession?.RemoteKey?.KeyPin,               Is.EqualTo(KeyPin.FromKey(key)));

                Assert.That(served.Connections.First().TCPConnection?.TLSSession?.Provider, Is.EqualTo(TLSProvider.SslStream));

                Assert.That(told.Outcomes,                                      Is.EqualTo(new[] { KeyPinOutcome.Accepted }));
                Assert.That(told.Results.Single().CertificateType,              Is.EqualTo(TLSCertificateType.X509));

            });

            await TheMirrorAnswers(client);

        }

        #endregion

        #region WebSocket over HTTP/3

        /// <summary>
        /// A port that is free for TCP and for UDP: a WebSocket server takes
        /// HTTP/3 on the UDP port of the number of its TCP port.
        /// </summary>
        private static UInt16 APortFreeForBoth()
        {

            for (var attempt = 0; attempt < 50; attempt++)
            {

                using var tcp = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
                tcp.Start();

                var port = ((System.Net.IPEndPoint) tcp.LocalEndpoint).Port;

                try
                {
                    using var udp = new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Any, port));
                    return (UInt16) port;
                }
                catch (System.Net.Sockets.SocketException)
                { }

            }

            throw new InvalidOperationException("No port is free for both TCP and UDP.");

        }

        [Test]
        public async Task WebSocket_OverHTTP3_TheServersKey_ComesRaw_AsOverTCP()
        {

            using var key         = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var certificate = Carrier(key);

            Served? served = null;

            // The UDP port of the number of the TCP port may be another socket's by the time it is asked for.
            for (var attempt = 0; attempt < 20 && served?.Server.HTTP3Port is null; attempt++)
            {

                if (served is not null)
                    await served.DisposeAsync();

                try
                {
                    served = new Served(certificate, RawPublicKeyMode.Prefer, Port: APortFreeForBoth(), AcceptHTTP3: true);
                }
                catch
                {
                    served = null;
                }

            }

            Assert.That(served?.Server.HTTP3Port, Is.Not.Null, "The server takes no WebSockets over HTTP/3: " + served?.Server.WhyNoHTTP3);

            await using var _ = served!;

            var told = new Told();

            await using var client = ClientOf(served, KeyPin.FromKey(key), RawPublicKeyMode.Require, told, Over: WebSocketHTTPVersion.HTTP3);

            var response = await Connect(client);

            Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.SwitchingProtocols), response.EntirePDU);
            Assert.That(SpinWait.SpinUntil(() => !served.Connections.IsEmpty, TimeSpan.FromSeconds(10)), Is.True, "The server took no WebSocket.");

            Assert.Multiple(() => {
                Assert.That(client.HTTPVersionInUse,                    Is.EqualTo(HTTPVersion.HTTP_3_0));
                Assert.That(told.Outcomes,                              Is.EqualTo(new[] { KeyPinOutcome.Accepted }));
                Assert.That(told.Results.Single().CertificateType,      Is.EqualTo(TLSCertificateType.RawPublicKey));
                Assert.That(told.Results.Single().Pin,                  Is.EqualTo(KeyPin.FromKey(key)));
            });

            await TheMirrorAnswers(client);

            // And with another key pinned: no WebSocket.
            using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

            var toldRefused = new Told();

            await using var refused = ClientOf(served, KeyPin.FromKey(otherKey), RawPublicKeyMode.Require, toldRefused, Over: WebSocketHTTPVersion.HTTP3);

            var noResponse = await Connect(refused);

            Assert.Multiple(() => {
                Assert.That(noResponse.HTTPStatusCode,   Is.Not.EqualTo(HTTPStatusCode.SwitchingProtocols));
                Assert.That(toldRefused.Outcomes,        Is.EqualTo(new[] { KeyPinOutcome.Refused }));
                Assert.That(served.Connections,          Has.Count.EqualTo(1));
            });

        }

        #endregion

        #region HTTP/2: the server and the client of their own

        private static Boolean ServesLocalhost(String Authority)
            => Authority.StartsWith("localhost", StringComparison.OrdinalIgnoreCase);

        private static Task<(List<(String, String)>, Byte[]?)> Hello(UInt32 StreamId, List<(String Name, String Value)> Headers, Byte[]? Body, CancellationToken CancellationToken)
        {
            var body = "hello".ToUTF8Bytes();
            return Task.FromResult<(List<(String, String)>, Byte[]?)>(
                       ([(":status", "200"), ("content-length", body.Length.ToString())], body));
        }

        [Test]
        public async Task HTTP2_BothKeys_ComeRaw_AndAKeyThatIsNotPinnedIsRefused()
        {

            using var serverKey   = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var stationKey  = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var strangerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

            using var serverCertificate   = Carrier(serverKey);
            using var stationCertificate  = Carrier(stationKey,  "CN=station.invalid", ClientAuth: true);
            using var strangerCertificate = Carrier(strangerKey, "CN=station.invalid", ClientAuth: true);

            // A carrier certificate names no origin, so the server is told which ones it serves.
            await using var server = await TestH2Server.StartAsync(Hello, Certificate: serverCertificate, IsAuthorityServed: ServesLocalhost);

            var asked = new ConcurrentQueue<TLSPeerKey>();

            server.Server.RawPublicKeys        = RawPublicKeyMode.Prefer;
            server.Server.ClientRawPublicKeys  = RawPublicKeyMode.Require;
            server.Server.ClientKeyValidator   = clientKey => {
                                                     asked.Enqueue(clientKey);
                                                     return clientKey.KeyPin == KeyPin.FromKey(stationKey)
                                                                ? TLSValidationResult.Success()
                                                                : TLSValidationResult.Failed("Not the station's key.");
                                                 };

            var told    = new Told();
            var pinned  = new KeyPinPolicy([ KeyPin.FromKey(serverKey) ], RawPublicKeys: RawPublicKeyMode.Require);

            var connection = await HTTP2Client.ConnectAsync("localhost",
                                                            server.Port,
                                                            ClientCertificate:        stationCertificate,
                                                            ServerKeyPins:            pinned,
                                                            OnServerKeyPinValidated:  told.Add);

            var response = await connection.SendRequestAsync(HTTPMethod.GET, URIScheme.https, $"localhost:{server.Port}", "/");
            await connection.CloseAsync();

            Assert.Multiple(() => {
                Assert.That(response.Status,                        Is.EqualTo(200));
                Assert.That(told.Outcomes,                          Is.EqualTo(new[] { KeyPinOutcome.Accepted }));
                Assert.That(told.Results.Single().CertificateType,  Is.EqualTo(TLSCertificateType.RawPublicKey));
                Assert.That(asked.Single().IsRawPublicKey,          Is.True);
                Assert.That(asked.Single().KeyPin,                  Is.EqualTo(KeyPin.FromKey(stationKey)));
            });

            // A client with another key gets no answer: in TLS 1.3 the refusal may reach it
            // at the handshake or at its first read.
            var refused = false;

            try
            {
                var stranger = await HTTP2Client.ConnectAsync("localhost", server.Port, ClientCertificate: strangerCertificate, ServerKeyPins: pinned);
                await stranger.SendRequestAsync(HTTPMethod.GET, URIScheme.https, $"localhost:{server.Port}", "/");
            }
            catch
            {
                refused = true;
            }

            Assert.That(refused,                              Is.True, "a client with another key gets no answer");
            Assert.That(asked.Last().KeyPin,                  Is.EqualTo(KeyPin.FromKey(strangerKey)));

            // A server key that is not the pinned one.
            var toldRefused = new Told();

            Assert.That(async () => await HTTP2Client.ConnectAsync("localhost",
                                                                   server.Port,
                                                                   ServerKeyPins:            new KeyPinPolicy([ KeyPin.FromKey(strangerKey) ], RawPublicKeys: RawPublicKeyMode.Require),
                                                                   OnServerKeyPinValidated:  toldRefused.Add),
                        Throws.InstanceOf<TLSHandshakeFailedException>());

            Assert.That(toldRefused.Outcomes, Is.EqualTo(new[] { KeyPinOutcome.Refused }));

        }

        [Test]
        public async Task HTTP2_AClientThatAsksForNoRawKeys_IsSslStreams_OnTheSamePort()
        {

            using var key         = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var certificate = Carrier(key);

            await using var server = await TestH2Server.StartAsync(Hello, Certificate: certificate, IsAuthorityServed: ServesLocalhost);

            server.Server.RawPublicKeys = RawPublicKeyMode.Prefer;

            // As a client was before: SslStream, and the key in its certificate.
            var told       = new Told();
            var connection = await HTTP2Client.ConnectAsync("localhost",
                                                            server.Port,
                                                            KeyPinValidator.ForSslStream(KeyPinPolicy.PinnedTo(KeyPin.FromKey(key)), told.Add));

            var response   = await connection.SendRequestAsync(HTTPMethod.GET, URIScheme.https, $"localhost:{server.Port}", "/");
            await connection.CloseAsync();

            Assert.Multiple(() => {
                Assert.That(response.Status,                        Is.EqualTo(200));
                Assert.That(told.Outcomes,                          Is.EqualTo(new[] { KeyPinOutcome.Accepted }));
                Assert.That(told.Results.Single().CertificateType,  Is.EqualTo(TLSCertificateType.X509));
            });

            // And a server that shows its key raw alone takes no such client.
            server.Server.RawPublicKeys = RawPublicKeyMode.Require;

            Assert.That(async () => await HTTP2Client.ConnectAsync("localhost",
                                                                   server.Port,
                                                                   KeyPinValidator.ForSslStream(KeyPinPolicy.PinnedTo(KeyPin.FromKey(key)))),
                        Throws.Exception);

        }

        #endregion

        #region Where raw public keys are demanded and not offered

        [Test]
        public async Task WebSocket_WhereOneSideDemandsRawKeys_AndTheOtherHasNone_ThereIsNoConnection()
        {

            using var key         = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var certificate = Carrier(key);

            // The client takes a raw public key alone; the server knows none.
            await using (var served = new Served(certificate, RawPublicKeyMode.Off))
            {

                var told = new Told();

                await using var client = ClientOf(served, KeyPin.FromKey(key), RawPublicKeyMode.Require, told);

                var response = await Connect(client);

                Assert.Multiple(() => {
                    Assert.That(response.HTTPStatusCode,   Is.Not.EqualTo(HTTPStatusCode.SwitchingProtocols));
                    Assert.That(client.TLSSession,         Is.Null);
                    Assert.That(told.Outcomes,             Is.EqualTo(new[] { KeyPinOutcome.Refused }), "The right key, in a certificate.");
                    Assert.That(told.Results.Single().CertificateType, Is.EqualTo(TLSCertificateType.X509));
                    Assert.That(served.Connections,        Is.Empty);
                });

            }

            // The server shows its key raw alone; the client's key pins know certificates alone.
            await using (var served = new Served(certificate, RawPublicKeyMode.Require))
            {

                var told = new Told();

                await using var client = ClientOf(served, KeyPin.FromKey(key), RawPublicKeyMode.Off, told);

                var response = await Connect(client);

                Assert.Multiple(() => {
                    Assert.That(response.HTTPStatusCode,   Is.Not.EqualTo(HTTPStatusCode.SwitchingProtocols));
                    Assert.That(told.Outcomes,             Is.Empty, "The certificate of a key that is shown raw alone was shown.");
                    Assert.That(served.Connections,        Is.Empty);
                });

            }

        }

        #endregion

    }

}
