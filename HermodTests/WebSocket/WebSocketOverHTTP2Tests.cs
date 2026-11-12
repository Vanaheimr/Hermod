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

using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Collections.Concurrent;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// A WebSocket client and a WebSocket server of this library open a
    /// WebSocket over HTTP/2 (RFC 8441): by an extended CONNECT where the
    /// Upgrade of HTTP/1.1 was, with the handshake - the subprotocol, a
    /// refusal and what it says - they have over HTTP/1.1, on the port they
    /// have it on.
    /// </summary>
    [TestFixture]
    public class WebSocketOverHTTP2Tests : AWebSocketTLSServerTests
    {

        #region Data

        private readonly ConcurrentQueue<WebSocketServerConnection>  accepted  = new();

        private readonly List<IAsyncDisposable>                       toDispose = [];

        public WebSocketOverHTTP2Tests()
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

        }

        #endregion

        #region (private) ClientOver(Version, URL = null)

        private WebSocketClient ClientOver(WebSocketHTTPVersion Version, String? URL = null)
        {

            var client = new WebSocketClient(
                             org.GraphDefined.Vanaheimr.Hermod.HTTP.URL.Parse(URL ?? $"wss://127.0.0.1:{HTTPPort}/somewhere?with=this"),
                             SecWebSocketProtocols:       [ "ocpp2.1" ],
                             RemoteCertificateValidator:  (sender, certificate, certificateChain, tlsClient, policyErrors) =>
                                                              certificate is not null && certificate.Thumbprint == serverCertificate!.Thumbprint
                                                                  ? TLSValidationResult.Success()
                                                                  : TLSValidationResult.Failed("Unexpected server certificate!")
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

            // Larger than a frame of HTTP/2 (16384 octets) carries at once.
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


        #region AWebSocketIsOpenedOverHTTP2WhereNothingElseWillDo()

        [Test]
        public async Task AWebSocketIsOpenedOverHTTP2WhereNothingElseWillDo()
        {

            webSocketServer!.AcceptHTTP2 = true;

            var requests = new ConcurrentQueue<HTTPRequest>();
            webSocketServer.OnHTTPRequest += (timestamp, server, httpRequest, cancellationToken) => { requests.Enqueue(httpRequest); return Task.CompletedTask; };

            var client         = ClientOver(WebSocketHTTPVersion.HTTP2);
            var (_, response)  = await client.Connect();

            Assert.That(SpinWait.SpinUntil(() => !accepted.IsEmpty, TimeSpan.FromSeconds(10)), Is.True,
                        "The server took no WebSocket: " + response?.EntirePDU);

            Assert.Multiple(() => {

                Assert.That(client.HTTPVersionInUse,                  Is.EqualTo(HTTPVersion.HTTP_2_0),               "what the client asked over");
                Assert.That(accepted.First().HTTPVersion,             Is.EqualTo(HTTPVersion.HTTP_2_0),               "what the server says it was opened over");

                // The handshake reads as it does over HTTP/1.1, on either side.
                Assert.That(response.HTTPStatusCode,                  Is.EqualTo(HTTPStatusCode.SwitchingProtocols));
                Assert.That(response.SecWebSocketProtocol,            Is.EqualTo("ocpp2.1"),                          "the subprotocol the server chose");
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

        #region WhereTheServerChoosesItChoosesHTTP2()

        [Test]
        public async Task WhereTheServerChoosesItChoosesHTTP2()
        {

            webSocketServer!.AcceptHTTP2 = true;

            var client         = ClientOver(WebSocketHTTPVersion.Auto);
            var (_, response)  = await client.Connect();

            Assert.That(SpinWait.SpinUntil(() => !accepted.IsEmpty, TimeSpan.FromSeconds(10)), Is.True,
                        "The server took no WebSocket: " + response?.EntirePDU);

            Assert.Multiple(() => {
                Assert.That(client.HTTPVersionInUse,       Is.EqualTo(HTTPVersion.HTTP_2_0));
                Assert.That(accepted.First().HTTPVersion,  Is.EqualTo(HTTPVersion.HTTP_2_0));
            });

            await TheMirrorAnswers(client);

        }

        #endregion

        #region WhereTheServerChoosesAndKnowsHTTP11AloneItIsHTTP11()

        [Test]
        public async Task WhereTheServerChoosesAndKnowsHTTP11AloneItIsHTTP11()
        {

            // AcceptHTTP2 stays off: the server offers nothing for ALPN, as before.

            var client         = ClientOver(WebSocketHTTPVersion.Auto);
            var (_, response)  = await client.Connect();

            Assert.That(SpinWait.SpinUntil(() => !accepted.IsEmpty, TimeSpan.FromSeconds(10)), Is.True,
                        "The server took no WebSocket: " + response?.EntirePDU);

            Assert.Multiple(() => {
                Assert.That(client.HTTPVersionInUse,       Is.EqualTo(HTTPVersion.HTTP_1_1));
                Assert.That(accepted.First().HTTPVersion,  Is.EqualTo(HTTPVersion.HTTP_1_1));
            });

            await TheMirrorAnswers(client);

        }

        #endregion

        #region AServerOfHTTP2StillTakesAClientOfHTTP11()

        [Test]
        public async Task AServerOfHTTP2StillTakesAClientOfHTTP11()
        {

            webSocketServer!.AcceptHTTP2 = true;

            var client         = ClientOver(WebSocketHTTPVersion.HTTP1_1);
            var (_, response)  = await client.Connect();

            Assert.That(SpinWait.SpinUntil(() => !accepted.IsEmpty, TimeSpan.FromSeconds(10)), Is.True,
                        "The server took no WebSocket: " + response?.EntirePDU);

            Assert.Multiple(() => {
                Assert.That(client.HTTPVersionInUse,       Is.EqualTo(HTTPVersion.HTTP_1_1));
                Assert.That(accepted.First().HTTPVersion,  Is.EqualTo(HTTPVersion.HTTP_1_1));
            });

            await TheMirrorAnswers(client);

        }

        #endregion

        #region NothingElseWillDoAndTheServerKnowsHTTP11Alone()

        [Test]
        public async Task NothingElseWillDoAndTheServerKnowsHTTP11Alone()
        {

            var client         = ClientOver(WebSocketHTTPVersion.HTTP2);
            var (_, response)  = await client.Connect();

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,  Is.Not.EqualTo(HTTPStatusCode.SwitchingProtocols));
                Assert.That(accepted,                 Is.Empty,  "A WebSocket was opened over something else.");
                Assert.That(client.HTTPVersionInUse,  Is.Null,   "An attempt that asked nothing says it asked over something.");
            });

        }

        #endregion

        #region ARefusalSaysOverHTTP2WhatItSaysOverHTTP11()

        [Test]
        public async Task ARefusalSaysOverHTTP2WhatItSaysOverHTTP11()
        {

            webSocketServer!.AcceptHTTP2 = true;

            webSocketServer.OnValidateWebSocketConnection += (timestamp, server, connection, eventTrackingId, cancellationToken) =>
                Task.FromResult<HTTPResponse?>(
                    new HTTPResponse.Builder(connection.HTTPRequest!) {
                        HTTPStatusCode  = HTTPStatusCode.ServiceUnavailable,
                        RetryAfter      = "7",
                        Connection      = ConnectionType.Close
                    }
                );

            var client         = ClientOver(WebSocketHTTPVersion.HTTP2);
            var (_, response)  = await client.Connect();

            Assert.Multiple(() => {
                Assert.That(client.HTTPVersionInUse,                 Is.EqualTo(HTTPVersion.HTTP_2_0));
                Assert.That(response.HTTPStatusCode,                 Is.EqualTo(HTTPStatusCode.ServiceUnavailable),  "the status of the refusal");
                Assert.That(response.GetHeaderField("Retry-After"),  Is.EqualTo("7"),                                "what the refusal said beside it");
                Assert.That(accepted,                                Is.Empty);
            });

        }

        #endregion

        #region WithoutTLSItIsHTTP2FromTheFirstByte()

        [Test]
        public async Task WithoutTLSItIsHTTP2FromTheFirstByte()
        {

            await using var server = new WebSocketMirrorServer(
                                         HTTPPort:               IPPort.Zero,
                                         RequireAuthentication:  false,
                                         SecWebSocketProtocols:  [ "ocpp2.1" ],
                                         AutoStart:              true
                                     ) {
                                         AcceptHTTP2 = true
                                     };

            var versions = new ConcurrentQueue<HTTPVersion>();
            server.OnNewWebSocketConnection += (timestamp, sender, connection, eventTrackingId, sharedSubprotocols, selectedSubprotocol, cancellationToken) => {
                versions.Enqueue(connection.HTTPVersion);
                return Task.CompletedTask;
            };

            try
            {

                // Prior knowledge: nothing but HTTP/2 will do, and there is no ALPN to agree with.
                var overHTTP2 = ClientOver(WebSocketHTTPVersion.HTTP2, $"ws://127.0.0.1:{server.IPPort}/");
                await overHTTP2.Connect();

                Assert.That(SpinWait.SpinUntil(() => versions.Count == 1, TimeSpan.FromSeconds(10)), Is.True, "over HTTP/2");
                await TheMirrorAnswers(overHTTP2);

                // The server chooses, and without TLS it cannot: HTTP/1.1.
                var whatever  = ClientOver(WebSocketHTTPVersion.Auto,  $"ws://127.0.0.1:{server.IPPort}/");
                await whatever.Connect();

                Assert.That(SpinWait.SpinUntil(() => versions.Count == 2, TimeSpan.FromSeconds(10)), Is.True, "over HTTP/1.1");
                await TheMirrorAnswers(whatever);

                Assert.Multiple(() => {
                    Assert.That(versions,                    Is.EqualTo(new[] { HTTPVersion.HTTP_2_0, HTTPVersion.HTTP_1_1 }));
                    Assert.That(overHTTP2.HTTPVersionInUse,  Is.EqualTo(HTTPVersion.HTTP_2_0));
                    Assert.That(whatever. HTTPVersionInUse,  Is.EqualTo(HTTPVersion.HTTP_1_1));
                });

            }
            finally
            {
                await server.Shutdown(Wait: true);
            }

        }

        #endregion

        #region AServerOfHTTP2WithoutWebSocketsIsDialledAgainWithHTTP11Alone()

        /// <summary>
        /// A server that chooses "h2" and does not announce the extended
        /// CONNECT cannot open a WebSocket over it. Where the server chooses,
        /// the client dials it again and offers "http/1.1" alone; where
        /// nothing but HTTP/2 will do, it does not.
        /// </summary>
        [Test]
        public async Task AServerOfHTTP2WithoutWebSocketsIsDialledAgainWithHTTP11Alone()
        {

            var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var port     = ((IPEndPoint) listener.LocalEndpoint).Port;
            var offered  = new ConcurrentQueue<String>();
            using var stop = new CancellationTokenSource();

            var serving  = Task.Run(async () => {

                while (!stop.IsCancellationRequested)
                {

                    TcpClient tcpClient;

                    try
                    {
                        tcpClient = await listener.AcceptTcpClientAsync(stop.Token);
                    }
                    catch
                    {
                        return;
                    }

                    _ = Task.Run(async () => {

                        using (tcpClient)
                        {
                            try
                            {

                                await using var tls = new SslStream(tcpClient.GetStream());

                                await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions {
                                          ServerCertificate     = serverCertificate,
                                          ApplicationProtocols  = [ SslApplicationProtocol.Http2, SslApplicationProtocol.Http11 ]
                                      }, stop.Token);

                                offered.Enqueue(tls.NegotiatedApplicationProtocol.ToString());

                                // HTTP/2 without a CONNECT handler: SETTINGS_ENABLE_CONNECT_PROTOCOL is not announced.
                                if (tls.NegotiatedApplicationProtocol == SslApplicationProtocol.Http2)
                                    await new HTTP2Connection(
                                              tls,
                                              (streamId, headers, body, cancellationToken) =>
                                                  Task.FromResult<(List<(String Name, String Value)>, Byte[]?)>(([ (":status", "404") ], null)),
                                              CancellationToken: stop.Token
                                          ).RunAsync();

                            }
                            catch
                            { }
                        }

                    });

                }

            });

            try
            {

                var whatever       = ClientOver(WebSocketHTTPVersion.Auto, $"wss://127.0.0.1:{port}/");
                var (_, response)  = await whatever.Connect();

                Assert.That(SpinWait.SpinUntil(() => offered.Count >= 2, TimeSpan.FromSeconds(10)), Is.True,
                            "The server was not dialled again: " + offered.AggregateCSV());

                Assert.Multiple(() => {
                    Assert.That(offered.Take(2),             Is.EqualTo(new[] { "h2", "http/1.1" }),  "what the two attempts agreed on");
                    Assert.That(whatever.HTTPVersionInUse,   Is.EqualTo(HTTPVersion.HTTP_1_1));
                });

                var onlyHTTP2      = ClientOver(WebSocketHTTPVersion.HTTP2, $"wss://127.0.0.1:{port}/");
                var before         = offered.Count;
                var (_, refusal)   = await onlyHTTP2.Connect();

                Assert.Multiple(() => {
                    Assert.That(refusal.HTTPStatusCode,          Is.Not.EqualTo(HTTPStatusCode.SwitchingProtocols));
                    Assert.That(offered.Skip(before),            Has.None.EqualTo("http/1.1"),  "It was dialled over HTTP/1.1 although nothing but HTTP/2 would do.");
                });

            }
            finally
            {
                stop.Cancel();
                listener.Stop();
                await serving;
            }

        }

        #endregion


        #region AnExtendedCONNECTIsTheUpgradeRequestItStandsFor()

        [Test]
        public void AnExtendedCONNECTIsTheUpgradeRequestItStandsFor()
        {

            Assert.That(WebSocketOverHTTP2.TryUpgradeRequestOf(
                            [ (":method",                 "CONNECT"),
                              (":protocol",               "websocket"),
                              (":scheme",                 "https"),
                              (":authority",              "csms.example.org:8443"),
                              (":path",                   "/ocpp/CS01?x=1"),
                              ("sec-websocket-protocol",  "ocpp2.1, ocpp1.6"),
                              ("sec-websocket-version",   "13"),
                              ("authorization",           "Basic Q1MwMTpzZWNyZXQ=") ],
                            out var requestBytes,
                            out var error),
                        Is.True, error);

            var request = requestBytes.ToUTF8String();

            Assert.Multiple(() => {
                Assert.That(request,  Does.StartWith("GET /ocpp/CS01?x=1 HTTP/1.1\r\nHost: csms.example.org:8443\r\n"));
                Assert.That(request,  Does.Contain("\r\nUpgrade: websocket\r\n"));
                Assert.That(request,  Does.Contain("\r\nConnection: Upgrade\r\n"));
                Assert.That(request,  Does.Match("\r\nSec-WebSocket-Key: [A-Za-z0-9+/]{22}==\r\n"));
                Assert.That(request,  Does.Contain("\r\nsec-websocket-protocol: ocpp2.1, ocpp1.6\r\n"));
                Assert.That(request,  Does.Contain("\r\nauthorization: Basic Q1MwMTpzZWNyZXQ=\r\n"));
                Assert.That(request,  Does.EndWith("\r\n\r\n"));
                Assert.That(request,  Does.Not.Contain(":scheme"));
            });

        }

        #endregion

        #region AnExtendedCONNECTThatWouldSayMoreThanItSaysIsRefused()

        /// <summary>
        /// A header field with a line break in it would be two lines of the
        /// request the CONNECT stands for - a second Authorization, say - and a
        /// path with a blank a request line of its sender's making.
        /// </summary>
        [TestCase("x-anything",     "one\r\nAuthorization: Basic bm90OnlvdXJz")]
        [TestCase("x-anything",     "one\ntwo")]
        [TestCase("x any thing",    "value")]
        [TestCase("connection",     "keep-alive")]
        [TestCase("upgrade",        "h2c")]
        [TestCase(":path",          "/ocpp HTTP/1.1\r\nX: y")]
        [TestCase(":path",          "ocpp")]
        [TestCase(":authority",     "csms.example.org\r\nX: y")]
        public void AnExtendedCONNECTThatWouldSayMoreThanItSaysIsRefused(String Name, String Value)
        {

            var headers = new List<(String Name, String Value)> {
                              (":method",    "CONNECT"),
                              (":protocol",  "websocket"),
                              (":scheme",    "https")
                          };

            if (Name != ":authority") headers.Add((":authority", "csms.example.org"));
            if (Name != ":path")      headers.Add((":path",      "/ocpp/CS01"));

            headers.Add((Name, Value));

            Assert.Multiple(() => {
                Assert.That(WebSocketOverHTTP2.TryUpgradeRequestOf(headers, out var requestBytes, out var error),  Is.False);
                Assert.That(requestBytes,                                                                          Is.Empty);
                Assert.That(error,                                                                                 Is.Not.Null.And.Not.Empty);
            });

        }

        #endregion

    }

}
