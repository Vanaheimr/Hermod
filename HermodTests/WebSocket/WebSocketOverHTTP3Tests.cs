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

using System.Diagnostics;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// A WebSocket client and a WebSocket server of this library open a
    /// WebSocket over HTTP/3 (RFC 9220): by an extended CONNECT over QUIC where
    /// the Upgrade of HTTP/1.1 was, with the handshake - the subprotocol, a
    /// refusal and what it says, who the server is and who the client - they
    /// have over HTTP/1.1, on the UDP port of the number of their TCP port.
    /// The certificate of the server here has an RSA key, and so has the
    /// client's.
    /// </summary>
    [TestFixture]
    public class WebSocketOverHTTP3Tests : AWebSocketTLSServerTests
    {

        #region Data

        private readonly ConcurrentQueue<WebSocketServerConnection>  accepted  = new();

        private readonly List<IAsyncDisposable>                       toDispose = [];

        public WebSocketOverHTTP3Tests()
            : base([ "ocpp2.1", "ocpp1.6" ])
        { }

        [SetUp]
        public void TheServerSaysWhoCame()
        {

            accepted.Clear();

            webSocketServer!.OnNewWebSocketConnection += (timestamp, server, connection, eventTrackingId, sharedSubprotocols, selectedSubprotocol, cancellationToken) => {
                accepted.Enqueue(connection);
                return Task.CompletedTask;
            };

        }

        [TearDown]
        public async Task DisposeWhatATestMade()
        {

            foreach (var disposable in toDispose)
                try { await disposable.DisposeAsync(); } catch { }

            toDispose.Clear();

            WebSocketAlternativeServices.Forget(URL.Parse($"wss://127.0.0.1:{HTTPPort}/"));

        }

        #endregion

        #region (private) TheServerTakesHTTP3()

        /// <summary>
        /// The server takes WebSockets over HTTP/3 - and says why not, where it
        /// does not: the UDP port of the number of a TCP port the operating
        /// system handed out may be another socket's.
        /// </summary>
        private async Task TheServerTakesHTTP3()
        {

            webSocketServer!.AcceptHTTP3 = true;

            // The UDP port of the number of the TCP port the operating system
            // handed out may not be the server's to have: see APortFreeForBoth.
            // Then it is another server, on a port that was free for both a
            // moment ago.
            for (var attempt = 0; attempt < 20 && webSocketServer.HTTP3Port is null; attempt++)
            {

                await Shutdown_WebSocketTLSServer();

                var port = APortFreeForBoth();

                try
                {

                    webSocketServer = new WebSocketMirrorServer(
                                          HTTPPort:                    IPPort.Parse(port),
                                          RequireAuthentication:       false,
                                          ServerCertificateSelector:   (tcpServer, tcpClient) => serverCertificate!,
                                          SecWebSocketProtocols:       SecWebSocketProtocols,
                                          AutoStart:                   true
                                      );

                }
                catch
                {
                    // Not free for TCP.
                    Init_WebSocketTLSServer();
                    continue;
                }

                HTTPPort = webSocketServer.IPPort;

                TheServerSaysWhoCame();

                webSocketServer.AcceptHTTP3 = true;

            }

            Assert.That(webSocketServer!.HTTP3Port, Is.EqualTo(HTTPPort),
                        "The server takes no WebSockets over HTTP/3: " + webSocketServer.WhyNoHTTP3);

        }

        #endregion

        #region (private static) APortFreeForBoth()

        /// <summary>
        /// A port that is free for UDP as for TCP, on both loopback addresses, and
        /// outside the range clients are handed their ports from: Windows keeps
        /// ranges of ports for itself, for UDP and TCP apart, and the UDP port of
        /// the number of a TCP port the system handed out may be a client's.
        /// </summary>
        private static UInt16 APortFreeForBoth()
        {

            for (var attempt = 0; attempt < 200; attempt++)
            {

                var port = Random.Shared.Next(20000, 32768);

                try
                {

                    using var udp4 = new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback,     port));
                    using var udp6 = new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.IPv6Loopback, port));

                    var tcp4 = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,     port);
                    tcp4.Start();
                    tcp4.Stop();

                    var tcp6 = new System.Net.Sockets.TcpListener(System.Net.IPAddress.IPv6Loopback, port);
                    tcp6.Start();
                    tcp6.Stop();

                    return (UInt16) port;

                }
                catch (System.Net.Sockets.SocketException)
                { }

            }

            throw new InvalidOperationException("No port is free for UDP and TCP.");

        }

        #endregion

        #region (private) ClientOver(Version, ClientCertificates = null, Trusting = true)

        private WebSocketClient ClientOver(WebSocketHTTPVersion            Version,
                                           IEnumerable<X509Certificate2>?  ClientCertificates   = null,
                                           Boolean                         Trusting             = true)
        {

            var client = new WebSocketClient(
                             URL.Parse($"wss://127.0.0.1:{HTTPPort}/somewhere?with=this"),
                             SecWebSocketProtocols:       [ "ocpp2.1" ],
                             ClientCertificates:          ClientCertificates,
                             RemoteCertificateValidator:  (sender, certificate, certificateChain, tlsClient, policyErrors) =>
                                                              Trusting && certificate is not null && certificate.Thumbprint == serverCertificate!.Thumbprint
                                                                  ? TLSValidationResult.Success()
                                                                  : TLSValidationResult.Failed("Unexpected server certificate!"),
                             ConnectTimeout:              TimeSpan.FromSeconds(5)
                         ) {
                             WebSocketOver = Version
                         };

            toDispose.Add(client);

            return client;

        }

        #endregion

        #region (private) TheMirrorAnswers(Client)

        /// <summary>
        /// A text and a binary message of some size go there and come back mirrored.
        /// </summary>
        private static async Task TheMirrorAnswers(WebSocketClient Client)
        {

            var texts     = new ConcurrentQueue<String>();
            var binaries  = new ConcurrentQueue<Byte[]>();

            Client.OnTextMessageReceived   += (timestamp, client, connection, frame, eventTrackingId, textMessage,   cancellationToken) => { texts.   Enqueue(textMessage);   return Task.CompletedTask; };
            Client.OnBinaryMessageReceived += (timestamp, client, connection, frame, eventTrackingId, binaryMessage, cancellationToken) => { binaries.Enqueue(binaryMessage); return Task.CompletedTask; };

            // Larger than a packet of QUIC carries at once, by far.
            var text = String.Concat(Enumerable.Range(0, 5000).Select(i => (i % 10).ToString())) + "1234";

            await Client.SendTextMessage  (text);
            await Client.SendBinaryMessage("ABCD".ToUTF8Bytes());

            Assert.That(SpinWait.SpinUntil(() => texts.Count > 0 && binaries.Count > 0, TimeSpan.FromSeconds(10)), Is.True,
                        "The server did not mirror what it was sent.");

            Assert.Multiple(() => {
                Assert.That(texts.   First(),                 Is.EqualTo(text.Reverse()));
                Assert.That(binaries.First().ToUTF8String(),  Is.EqualTo("DCBA"));
            });

        }

        #endregion


        #region AWebSocketIsOpenedOverHTTP3WhereNothingElseWillDo()

        [Test]
        public async Task AWebSocketIsOpenedOverHTTP3WhereNothingElseWillDo()
        {

            await TheServerTakesHTTP3();

            var requests = new ConcurrentQueue<HTTPRequest>();
            webSocketServer!.OnHTTPRequest += (timestamp, server, httpRequest, cancellationToken) => { requests.Enqueue(httpRequest); return Task.CompletedTask; };

            var client         = ClientOver(WebSocketHTTPVersion.HTTP3);
            var (_, response)  = await client.Connect();

            Assert.That(SpinWait.SpinUntil(() => !accepted.IsEmpty, TimeSpan.FromSeconds(10)), Is.True,
                        "The server took no WebSocket: " + response?.EntirePDU);

            Assert.Multiple(() => {

                Assert.That(client.HTTPVersionInUse,                  Is.EqualTo(HTTPVersion.HTTP_3_0),               "what the client asked over");
                Assert.That(accepted.First().HTTPVersion,             Is.EqualTo(HTTPVersion.HTTP_3_0),               "what the server says it was opened over");
                Assert.That(accepted.First().RemoteSocket.Port,       Is.Not.EqualTo(IPPort.Zero),                    "where the server says its client is");

                // The handshake reads as it does over HTTP/1.1, on either side.
                Assert.That(response.HTTPStatusCode,                  Is.EqualTo(HTTPStatusCode.SwitchingProtocols));
                Assert.That(response.SecWebSocketProtocol,            Is.EqualTo("ocpp2.1"),                          "the subprotocol the server chose");
                Assert.That(response.AltSvc,                          Is.Null,                                        "a client that is there is not told to come");
                Assert.That(requests.First().Path.ToString(),         Is.EqualTo("/somewhere"),                       "the path the server was asked for");
                Assert.That(requests.First().QueryString.ToString(),  Does.Contain("with=this"),                      "the query string the server was asked with");
                Assert.That(requests.First().Host.ToString(),         Is.EqualTo($"127.0.0.1:{HTTPPort}"),            "the authority, as the Host");
                Assert.That(requests.First().SecWebSocketProtocol,    Is.EquivalentTo(new[] { "ocpp2.1" }),           "the subprotocols offered");

            });

            await TheMirrorAnswers(client);

            await client.Close();

            Assert.That(SpinWait.SpinUntil(() => !webSocketServer.WebSocketConnections.Any(), TimeSpan.FromSeconds(10)), Is.True,
                        "The server kept the WebSocket its client closed.");

        }

        #endregion

        #region AServerOfHTTP3StillTakesClientsOfHTTP11AndHTTP2AndTellsThemOfIt()

        [Test]
        public async Task AServerOfHTTP3StillTakesClientsOfHTTP11AndHTTP2AndTellsThemOfIt()
        {

            await TheServerTakesHTTP3();

            webSocketServer!.AcceptHTTP2 = true;

            foreach (var (version, inUse) in new[] {
                                                 (WebSocketHTTPVersion.HTTP1_1, HTTPVersion.HTTP_1_1),
                                                 (WebSocketHTTPVersion.HTTP2,   HTTPVersion.HTTP_2_0)
                                             })
            {

                var client         = ClientOver(version);
                var (_, response)  = await client.Connect();

                Assert.Multiple(() => {
                    Assert.That(response.HTTPStatusCode,              Is.EqualTo(HTTPStatusCode.SwitchingProtocols),  version.ToString());
                    Assert.That(client.HTTPVersionInUse,              Is.EqualTo(inUse),                              version.ToString());
                    Assert.That(response.AltSvc?.Services.
                                    Select(service => (service.ProtocolId, service.Host, service.Port)),
                                Is.EqualTo(new[] { ("h3", "", HTTPPort.ToUInt16()) }),
                                $"what a client over {version} is told of HTTP/3");
                });

                await TheMirrorAnswers(client);
                await client.Close();

            }

        }

        #endregion

        #region AServerWithoutHTTP3TellsNobodyOfIt()

        [Test]
        public async Task AServerWithoutHTTP3TellsNobodyOfIt()
        {

            var client         = ClientOver(WebSocketHTTPVersion.HTTP1_1);
            var (_, response)  = await client.Connect();

            Assert.Multiple(() => {
                Assert.That(webSocketServer!.HTTP3Port,  Is.Null);
                Assert.That(response.HTTPStatusCode,     Is.EqualTo(HTTPStatusCode.SwitchingProtocols));
                Assert.That(response.AltSvc,             Is.Null);
            });

        }

        #endregion

        #region WhereTheServerChoosesItIsHTTP3OnceItSaidSo()

        [Test]
        public async Task WhereTheServerChoosesItIsHTTP3OnceItSaidSo()
        {

            await TheServerTakesHTTP3();

            // The first time nothing is known of the server, and it is asked over TCP.
            var first          = ClientOver(WebSocketHTTPVersion.Auto);
            var (_, response)  = await first.Connect();

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,  Is.EqualTo(HTTPStatusCode.SwitchingProtocols));
                Assert.That(first.HTTPVersionInUse,   Is.EqualTo(HTTPVersion.HTTP_1_1));
            });

            await first.Close();

            // It said where it is reached over HTTP/3: the next one goes there.
            var second             = ClientOver(WebSocketHTTPVersion.Auto);
            var (_, secondAnswer)  = await second.Connect();

            Assert.Multiple(() => {
                Assert.That(secondAnswer.HTTPStatusCode,  Is.EqualTo(HTTPStatusCode.SwitchingProtocols));
                Assert.That(second.HTTPVersionInUse,      Is.EqualTo(HTTPVersion.HTTP_3_0));
            });

            await TheMirrorAnswers(second);
            await second.Close();

            // A client that names its version is not moved by what the server said.
            var third              = ClientOver(WebSocketHTTPVersion.HTTP1_1);
            await third.Connect();

            Assert.That(third.HTTPVersionInUse, Is.EqualTo(HTTPVersion.HTTP_1_1));

        }

        #endregion

        #region WhereTheServerIsNoLongerReachedOverHTTP3ItIsAskedAsBefore()

        [Test]
        public async Task WhereTheServerIsNoLongerReachedOverHTTP3ItIsAskedAsBefore()
        {

            await TheServerTakesHTTP3();

            var first = ClientOver(WebSocketHTTPVersion.Auto);
            await first.Connect();
            await first.Close();

            Assert.That(WebSocketAlternativeServices.TryGetHTTP3(URL.Parse($"wss://127.0.0.1:{HTTPPort}/"), out var port), Is.True);
            Assert.That(port, Is.EqualTo(HTTPPort.ToUInt16()));

            // The server stops taking HTTP/3, and tells nobody.
            webSocketServer.AcceptHTTP3 = false;

            Assert.That(SpinWait.SpinUntil(() => webSocketServer.HTTP3Port is null, TimeSpan.FromSeconds(10)), Is.True);

            var second         = ClientOver(WebSocketHTTPVersion.Auto);
            var (_, response)  = await second.Connect();

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,  Is.EqualTo(HTTPStatusCode.SwitchingProtocols),  "asked the way it was when it said so");
                Assert.That(second.HTTPVersionInUse,  Is.EqualTo(HTTPVersion.HTTP_1_1));
                Assert.That(WebSocketAlternativeServices.TryGetHTTP3(URL.Parse($"wss://127.0.0.1:{HTTPPort}/"), out _), Is.False,
                            "what the server said, and did not keep, is forgotten");
            });

            await second.Close();

            // It takes HTTP/3 again and says so - and is not believed again so soon.
            await TheServerTakesHTTP3();

            var third          = ClientOver(WebSocketHTTPVersion.Auto);
            var (_, again)     = await third.Connect();

            Assert.Multiple(() => {
                Assert.That(again.AltSvc,            Is.Not.Null,                       "the server says it again");
                Assert.That(third.HTTPVersionInUse,  Is.EqualTo(HTTPVersion.HTTP_1_1));
                Assert.That(WebSocketAlternativeServices.TryGetHTTP3(URL.Parse($"wss://127.0.0.1:{HTTPPort}/"), out _), Is.False,
                            "a server that was not found where it said is believed again at once");
            });

        }

        #endregion

        #region NothingElseWillDoAndTheServerTakesNoHTTP3()

        [Test]
        public async Task NothingElseWillDoAndTheServerTakesNoHTTP3()
        {

            var client         = ClientOver(WebSocketHTTPVersion.HTTP3);

            var stopwatch      = Stopwatch.StartNew();
            var (_, response)  = await client.Connect();
            stopwatch.Stop();

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,  Is.Not.EqualTo(HTTPStatusCode.SwitchingProtocols));
                Assert.That(accepted,                 Is.Empty,  "A WebSocket was opened over something else.");
                Assert.That(client.HTTPVersionInUse,  Is.Null,   "An attempt that asked nothing says it asked over something.");
                Assert.That(stopwatch.Elapsed,        Is.LessThan(TimeSpan.FromSeconds(30)),
                            "The attempt was not given up after the time a connection is given.");
            });

        }

        #endregion

        #region ARefusalSaysOverHTTP3WhatItSaysOverHTTP11()

        [Test]
        public async Task ARefusalSaysOverHTTP3WhatItSaysOverHTTP11()
        {

            await TheServerTakesHTTP3();

            webSocketServer.OnValidateWebSocketConnection += (timestamp, server, connection, eventTrackingId, cancellationToken) =>
                Task.FromResult<HTTPResponse?>(
                    new HTTPResponse.Builder(connection.HTTPRequest!) {
                        HTTPStatusCode  = HTTPStatusCode.ServiceUnavailable,
                        RetryAfter      = "7",
                        Connection      = ConnectionType.Close
                    }
                );

            var client         = ClientOver(WebSocketHTTPVersion.HTTP3);
            var (_, response)  = await client.Connect();

            Assert.Multiple(() => {
                Assert.That(client.HTTPVersionInUse,                 Is.EqualTo(HTTPVersion.HTTP_3_0));
                Assert.That(response.HTTPStatusCode,                 Is.EqualTo(HTTPStatusCode.ServiceUnavailable),  "the status of the refusal");
                Assert.That(response.GetHeaderField("Retry-After"),  Is.EqualTo("7"),                                "what the refusal said beside it");
                Assert.That(accepted,                                Is.Empty);
            });

        }

        #endregion

        #region AServerTheClientDoesNotBelieveIsNotAskedAndThatIsSaidAtOnce()

        [Test]
        public async Task AServerTheClientDoesNotBelieveIsNotAskedAndThatIsSaidAtOnce()
        {

            await TheServerTakesHTTP3();

            var requests = 0;
            webSocketServer.OnHTTPRequest += (timestamp, server, httpRequest, cancellationToken) => { Interlocked.Increment(ref requests); return Task.CompletedTask; };

            var client         = ClientOver(WebSocketHTTPVersion.HTTP3, Trusting: false);

            var stopwatch      = Stopwatch.StartNew();
            var (_, response)  = await client.Connect();
            stopwatch.Stop();

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,  Is.Not.EqualTo(HTTPStatusCode.SwitchingProtocols));
                Assert.That(requests,                 Is.Zero,   "A server that was not believed was asked.");
                Assert.That(accepted,                 Is.Empty);
                Assert.That(client.HTTPVersionInUse,  Is.Null);
                Assert.That(stopwatch.Elapsed,        Is.LessThan(TimeSpan.FromSeconds(4)),
                            "A refused certificate was waited out like a server that does not answer.");
            });

        }

        #endregion

        #region TheServerKnowsTheClientByTheCertificateItShowsOverHTTP3()

        [Test]
        public async Task TheServerKnowsTheClientByTheCertificateItShowsOverHTTP3()
        {

            await TheServerTakesHTTP3();

            var shown = new ConcurrentQueue<String?>();

            webSocketServer!.ClientCertificateValidator = (sender, certificate, certificateChain, tlsServer, policyErrors) => {
                shown.Enqueue(certificate?.Thumbprint);
                return certificate is not null && certificate.Thumbprint == clientCertificate!.Thumbprint
                           ? TLSValidationResult.Success()
                           : TLSValidationResult.Failed("Unexpected client certificate!");
            };

            var client         = ClientOver(WebSocketHTTPVersion.HTTP3, [ clientCertificate! ]);
            var (_, response)  = await client.Connect();

            Assert.That(SpinWait.SpinUntil(() => !accepted.IsEmpty, TimeSpan.FromSeconds(10)), Is.True,
                        "The server took no WebSocket: " + response?.EntirePDU);

            Assert.Multiple(() => {
                Assert.That(shown,                                         Does.Contain(clientCertificate!.Thumbprint),    "what the validator of the server was asked about");
                Assert.That(accepted.First().ClientCertificate?.Thumbprint, Is.EqualTo(clientCertificate.Thumbprint),      "who the server says its client is");
                Assert.That(accepted.First().HTTPVersion,                  Is.EqualTo(HTTPVersion.HTTP_3_0));
            });

            await TheMirrorAnswers(client);

        }

        #endregion

        #region AClientWithoutACertificateIsOneWithoutOverHTTP3()

        [Test]
        public async Task AClientWithoutACertificateIsOneWithoutOverHTTP3()
        {

            await TheServerTakesHTTP3();

            webSocketServer!.ClientCertificateValidator = (sender, certificate, certificateChain, tlsServer, policyErrors) =>
                TLSValidationResult.Failed("Nobody is known here.");

            var client         = ClientOver(WebSocketHTTPVersion.HTTP3);
            var (_, response)  = await client.Connect();

            Assert.That(SpinWait.SpinUntil(() => !accepted.IsEmpty, TimeSpan.FromSeconds(10)), Is.True,
                        "The server took no WebSocket: " + response?.EntirePDU);

            Assert.That(accepted.First().ClientCertificate, Is.Null);

        }

        #endregion

        #region AServerToldNotToTakeHTTP3RightAfterItsStartTakesNone()

        /// <summary>
        /// The listener tells of its start in its own time, and HTTP/3 is
        /// started when it does: a server that was told meanwhile not to take
        /// HTTP/3 must not take it then.
        /// </summary>
        [Test]
        public async Task AServerToldNotToTakeHTTP3RightAfterItsStartTakesNone()
        {

            for (var round = 0; round < 10; round++)
            {

                await using var server = new WebSocketMirrorServer(
                                             HTTPPort:                    IPPort.Zero,
                                             RequireAuthentication:       false,
                                             ServerCertificateSelector:   (tcpServer, tcpClient) => serverCertificate!,
                                             SecWebSocketProtocols:       [ "ocpp2.1" ],
                                             AutoStart:                   true
                                         ) {
                                             AcceptHTTP3 = true
                                         };

                server.AcceptHTTP3 = false;

                await Task.Delay(100);

                Assert.That(server.HTTP3Port, Is.Null, $"round {round}");

            }

        }

        #endregion

        #region AServerWithoutTLSTakesNoHTTP3AndRunsAsBefore()

        [Test]
        public async Task AServerWithoutTLSTakesNoHTTP3AndRunsAsBefore()
        {

            await using var server = new WebSocketMirrorServer(
                                         HTTPPort:               IPPort.Zero,
                                         RequireAuthentication:  false,
                                         SecWebSocketProtocols:  [ "ocpp2.1" ],
                                         AutoStart:              true
                                     ) {
                                         AcceptHTTP3 = true
                                     };

            var client = new WebSocketClient(
                             URL.Parse($"ws://127.0.0.1:{server.IPPort}/"),
                             SecWebSocketProtocols: [ "ocpp2.1" ]
                         );

            toDispose.Add(client);

            var (_, response) = await client.Connect();

            Assert.Multiple(() => {
                Assert.That(server.HTTP3Port,         Is.Null);
                Assert.That(response.HTTPStatusCode,  Is.EqualTo(HTTPStatusCode.SwitchingProtocols));
                Assert.That(response.AltSvc,          Is.Null);
            });

            // And a client that wants nothing but HTTP/3 without TLS is told why not.
            var overHTTP3 = new WebSocketClient(
                                URL.Parse($"ws://127.0.0.1:{server.IPPort}/"),
                                SecWebSocketProtocols: [ "ocpp2.1" ]
                            ) {
                                WebSocketOver = WebSocketHTTPVersion.HTTP3
                            };

            toDispose.Add(overHTTP3);

            var (_, refusal) = await overHTTP3.Connect();

            Assert.That(refusal.HTTPStatusCode, Is.Not.EqualTo(HTTPStatusCode.SwitchingProtocols));

        }

        #endregion


        #region ACertificateThatIsThereSignsWithWhatItsKeyIs()

        [Test]
        public void ACertificateThatIsThereSignsWithWhatItsKeyIs()
        {

            static X509Certificate2 SelfSigned(Func<CertificateRequest> Request)
                => Request().CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

            var content = "what a CertificateVerify signs".ToUTF8Bytes();

            using var p256Key  = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var p256     = SelfSigned(() => new CertificateRequest("CN=p256", p256Key, HashAlgorithmName.SHA256));
            using var overP256 = ServerCertificate.From(p256);

            using var p384Key  = ECDsa.Create(ECCurve.NamedCurves.nistP384);
            using var p384     = SelfSigned(() => new CertificateRequest("CN=p384", p384Key, HashAlgorithmName.SHA384));
            using var overP384 = ServerCertificate.From(p384);

            using var rsaKey   = RSA.Create(2048);
            using var rsa      = SelfSigned(() => new CertificateRequest("CN=rsa", rsaKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
            using var overRSA  = ServerCertificate.From(rsa, [ p256 ]);

            Assert.Multiple(() => {

                Assert.That(overP256.SignatureScheme,  Is.EqualTo(SignatureScheme.EcdsaSecp256r1Sha256));
                Assert.That(p256Key.VerifyData(content, overP256.SignCertificateVerify(content), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence), Is.True);

                Assert.That(overP384.SignatureScheme,  Is.EqualTo(SignatureScheme.EcdsaSecp384r1Sha384));
                Assert.That(p384Key.VerifyData(content, overP384.SignCertificateVerify(content), HashAlgorithmName.SHA384, DSASignatureFormat.Rfc3279DerSequence), Is.True);

                Assert.That(overRSA.SignatureScheme,   Is.EqualTo(SignatureScheme.RsaPssRsaeSha256));
                Assert.That(rsaKey.VerifyData(content, overRSA.SignCertificateVerify(content), HashAlgorithmName.SHA256, RSASignaturePadding.Pss), Is.True);

                Assert.That(overRSA.IntermediatesDer,  Has.Count.EqualTo(1),  "what leads towards a root goes with it");
                Assert.That(overP256.IntermediatesDer, Is.Empty);

            });

            // The certificate stays its owner's.
            overP256.Dispose();
            Assert.That(p256.Subject, Is.EqualTo("CN=p256"));

        }

        #endregion

        #region ACertificateThatCannotBeShownOverQUICIsRefusedInWords()

        [Test]
        public void ACertificateThatCannotBeShownOverQUICIsRefusedInWords()
        {

            static X509Certificate2 SelfSigned(CertificateRequest Request)
                => Request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

            using var smallKey    = RSA.Create(1024);
            using var small       = SelfSigned(new CertificateRequest("CN=small", smallKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

            using var p521Key     = ECDsa.Create(ECCurve.NamedCurves.nistP521);
            using var p521        = SelfSigned(new CertificateRequest("CN=p521", p521Key, HashAlgorithmName.SHA512));

            using var p256Key     = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var withKey     = SelfSigned(new CertificateRequest("CN=public", p256Key, HashAlgorithmName.SHA256));
            using var withoutKey  = X509CertificateLoader.LoadCertificate(withKey.RawData);

            Assert.Multiple(() => {
                Assert.That(() => ServerCertificate.From(small),       Throws.TypeOf<NotSupportedException>().With.Message.Contains("1024 bits"));
                Assert.That(() => ServerCertificate.From(p521),        Throws.TypeOf<NotSupportedException>().With.Message.Contains("P-256 or P-384"));
                Assert.That(() => ServerCertificate.From(withoutKey),  Throws.TypeOf<NotSupportedException>().With.Message.Contains("without its private key"));
            });

        }

        #endregion


        #region A one-time password bound to the connection

        private const String TOTPSecret = "a-shared-secret-of-some-length";

        private static TOTPConfig Bound() => new (TOTPSecret, UseTLSExporterMaterial: true);
        private static TOTPConfig Raw()   => new (TOTPSecret, UseTLSExporterMaterial: false);

        /// <summary>
        /// The server takes WebSockets over HTTP/3 from who it knows by a
        /// one-time password, and tells of every request it was sent.
        /// </summary>
        private async Task<ConcurrentQueue<HTTPRequest>> TheServerKnowsItsStationBy(TOTPConfig Config)
        {

            await TheServerTakesHTTP3();

            webSocketServer!.RequireAuthentication        = true;
            webSocketServer. ClientTOTPConfig["station"]  = Config;

            var requests = new ConcurrentQueue<HTTPRequest>();

            webSocketServer.OnHTTPRequest += (timestamp, server, httpRequest, cancellationToken) => { requests.Enqueue(httpRequest); return Task.CompletedTask; };

            return requests;

        }


        #region AOneTimePasswordBoundToTheConnectionLetsItsClientInOverHTTP3()

        /// <summary>
        /// Over HTTP/3 the TLS of the connection exports keying material, on
        /// both ends the same: a client told to bind its one-time password
        /// does, says so, and is let in by a server that asks for the binding.
        /// What it sent is not the password it would have sent unbound.
        /// </summary>
        [Test]
        public async Task AOneTimePasswordBoundToTheConnectionLetsItsClientInOverHTTP3()
        {

            var requests       = await TheServerKnowsItsStationBy(Bound());

            var client         = ClientOver(WebSocketHTTPVersion.HTTP3);
            client.TOTPConfig  = Bound();
            client.TOTPLogin   = "station";

            var (_, response)  = await client.Connect();

            Assert.That(SpinWait.SpinUntil(() => !accepted.IsEmpty, TimeSpan.FromSeconds(10)), Is.True,
                        "The server took no WebSocket: " + response?.EntirePDU);

            var sent = requests.First().Authorization as HTTPTOTPAuthentication;

            var (before, now, next, _, _) = TOTPGenerator.GenerateTOTPs(TOTPSecret);

            Assert.Multiple(() => {

                Assert.That(response.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.SwitchingProtocols));
                Assert.That(client.HTTPVersionInUse,       Is.EqualTo(HTTPVersion.HTTP_3_0));
                Assert.That(accepted.First().Login,        Is.EqualTo("station"),                             "who the server says came");

                Assert.That(sent,                          Is.Not.Null,                                       "no Authorization: TOTP was sent");
                Assert.That(sent?.Login,                   Is.EqualTo("station"));
                Assert.That(sent?.Type,                    Is.EqualTo(TOTPHTTPHeaderType.TLSChannelBinding),  "the password does not say it is bound");
                Assert.That(sent?.ToString(),              Does.Not.Contain("tlscb"),                         "bound is what a password is unless it says otherwise");
                Assert.That(new[] { before, now, next },   Has.None.EqualTo(sent?.TOTP),                      "the password sent as bound is the unbound one");

                Assert.That(accepted.First().TOTPTLSExporterMaterial?.Length,
                                                           Is.EqualTo(TOTPGenerator.TLSExporterLength),       "the server has no keying material of the connection");

            });

            await TheMirrorAnswers(client);

        }

        #endregion

        #region AOneTimePasswordBoundToOneConnectionIsNoneOnAnother()

        /// <summary>
        /// What the binding is for: the password a client was let in with,
        /// read off its connection and shown on another within its validity,
        /// is refused there - and the same client, making its own for its new
        /// connection, is let in again.
        /// </summary>
        [Test]
        public async Task AOneTimePasswordBoundToOneConnectionIsNoneOnAnother()
        {

            var requests       = await TheServerKnowsItsStationBy(Bound());

            var first          = ClientOver(WebSocketHTTPVersion.HTTP3);
            first.TOTPConfig   = Bound();
            first.TOTPLogin    = "station";

            Assert.That((await first.Connect()).Item2.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.SwitchingProtocols));

            var readOff        = (HTTPTOTPAuthentication) requests.First().Authorization!;

            // Shown again, at once, by somebody else on a connection of their own.
            var thief                 = ClientOver(WebSocketHTTPVersion.HTTP3);
            thief.HTTPAuthentication  = HTTPTOTPAuthentication.Create("station", readOff.TOTP, TOTPHTTPHeaderType.TLSChannelBinding);

            var (_, refused)   = await thief.Connect();

            // And shown as an unbound one, it is none either.
            var liar                  = ClientOver(WebSocketHTTPVersion.HTTP3);
            liar.HTTPAuthentication   = HTTPTOTPAuthentication.Create("station", readOff.TOTP, TOTPHTTPHeaderType.RAW);

            var (_, refusedRaw) = await liar.Connect();

            await first.Close();

            // The client itself, again: another connection, another password.
            var again          = ClientOver(WebSocketHTTPVersion.HTTP3);
            again.TOTPConfig   = Bound();
            again.TOTPLogin    = "station";

            var (_, letIn)     = await again.Connect();

            var secondTime     = requests.Select(request => request.Authorization).OfType<HTTPTOTPAuthentication>().Last();

            Assert.Multiple(() => {
                Assert.That(refused.   HTTPStatusCode,  Is.EqualTo(HTTPStatusCode.Unauthorized),        "a password read off another connection was taken");
                Assert.That(refusedRaw.HTTPStatusCode,  Is.EqualTo(HTTPStatusCode.Unauthorized),        "a bound password was taken for a raw one");
                Assert.That(letIn.     HTTPStatusCode,  Is.EqualTo(HTTPStatusCode.SwitchingProtocols),  "the client was not let in on its next connection");
                Assert.That(secondTime.TOTP,            Is.Not.EqualTo(readOff.TOTP),                   "two connections were shown the same password");
            });

        }

        #endregion

        #region AServerAskingForTheBindingTakesNoRawPasswordOverHTTP3()

        [Test]
        public async Task AServerAskingForTheBindingTakesNoRawPasswordOverHTTP3()
        {

            await TheServerKnowsItsStationBy(Bound());

            var client         = ClientOver(WebSocketHTTPVersion.HTTP3);
            client.TOTPConfig  = Raw();
            client.TOTPLogin   = "station";

            var (_, response)  = await client.Connect();

            // Nor the raw password with "bound" written on it.
            var labelled                 = ClientOver(WebSocketHTTPVersion.HTTP3);
            labelled.HTTPAuthentication  = HTTPTOTPAuthentication.Create("station", TOTPGenerator.GenerateTOTP(TOTPSecret).Current, TOTPHTTPHeaderType.TLSChannelBinding);

            var (_, alsoRefused) = await labelled.Connect();

            Assert.Multiple(() => {
                Assert.That(response.   HTTPStatusCode,  Is.EqualTo(HTTPStatusCode.Unauthorized),  "a raw password was taken where the binding is asked for");
                Assert.That(alsoRefused.HTTPStatusCode,  Is.EqualTo(HTTPStatusCode.Unauthorized),  "a raw password sent as bound was taken");
                Assert.That(accepted,                    Is.Empty);
            });

        }

        #endregion

        #region AServerNotAskingForTheBindingTakesABoundPasswordWhereItCanVerifyIt()

        /// <summary>
        /// More than it asked for: a server whose configuration of a login
        /// does not ask for the binding verifies a bound password with the
        /// keying material of the connection all the same.
        /// </summary>
        [Test]
        public async Task AServerNotAskingForTheBindingTakesABoundPasswordWhereItCanVerifyIt()
        {

            await TheServerKnowsItsStationBy(Raw());

            var client         = ClientOver(WebSocketHTTPVersion.HTTP3);
            client.TOTPConfig  = Bound();
            client.TOTPLogin   = "station";

            var (_, response)  = await client.Connect();

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,  Is.EqualTo(HTTPStatusCode.SwitchingProtocols));
                Assert.That(SpinWait.SpinUntil(() => !accepted.IsEmpty, TimeSpan.FromSeconds(10)), Is.True);
            });

        }

        #endregion

        #region ABoundPasswordWithoutALoginGoesInTheTOTPHeaderAsBound()

        [Test]
        public async Task ABoundPasswordWithoutALoginGoesInTheTOTPHeaderAsBound()
        {

            await TheServerTakesHTTP3();

            var requests = new ConcurrentQueue<HTTPRequest>();
            webSocketServer!.OnHTTPRequest += (timestamp, server, httpRequest, cancellationToken) => { requests.Enqueue(httpRequest); return Task.CompletedTask; };

            var client         = ClientOver(WebSocketHTTPVersion.HTTP3);
            client.TOTPConfig  = Bound();

            var (_, response)  = await client.Connect();

            var header         = requests.First().GetHeaderField("TOTP")?.ToString() ?? "";
            var now            = TOTPGenerator.GenerateTOTPs(TOTPSecret);

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,  Is.EqualTo(HTTPStatusCode.SwitchingProtocols));
                Assert.That(header,                   Does.StartWith("1 "),                              "the TOTP header does not say its password is bound");
                Assert.That(new[] { "1 " + now.Previous, "1 " + now.Current, "1 " + now.Next },
                                                      Has.None.EqualTo(header),                          "the password sent as bound is the unbound one");
            });

        }

        #endregion

        #region ABoundPasswordIsSentOverNothingButHTTP3(Version)

        /// <summary>
        /// Left the choice of the version, the client may come to an
        /// SslStream, which exports nothing: it refuses before it has chosen,
        /// as it does over HTTP/1.1 and HTTP/2, and nothing reaches the server.
        /// </summary>
        [Test]
        public async Task ABoundPasswordIsSentOverNothingButHTTP3([Values(WebSocketHTTPVersion.Auto,
                                                                          WebSocketHTTPVersion.HTTP1_1,
                                                                          WebSocketHTTPVersion.HTTP2)] WebSocketHTTPVersion Version)
        {

            var requests       = await TheServerKnowsItsStationBy(Bound());

            var client         = ClientOver(Version);
            client.TOTPConfig  = Bound();
            client.TOTPLogin   = "station";

            var refused = await Assert.ThrowsAsync<InvalidOperationException>(async () => await client.Connect(),
                                                                         "A client that may come to an SslStream connected with a password to be bound.");

            await Task.Delay(200);

            Assert.Multiple(() => {
                Assert.That(refused?.Message,  Does.Contain("over HTTP/3 and nothing else"));
                Assert.That(requests,          Is.Empty,   "the server was asked something");
            });

        }

        #endregion

        #endregion

    }

}
