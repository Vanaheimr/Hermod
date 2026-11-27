/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using System.Text;
using System.Net.Sockets;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// A handshake that offers its extensions or its subprotocols on more than
    /// one line is the same handshake as one that offers them on one: "The
    /// |Sec-WebSocket-Extensions| header field MAY appear multiple times in an
    /// HTTP request (which is logically the same as a single
    /// |Sec-WebSocket-Extensions| header field that contains all values)"
    /// (RFC 6455, Section 11.3.2), and Section 11.3.4 says the same of
    /// |Sec-WebSocket-Protocol|.
    /// </summary>
    /// <remarks>
    /// Measured before the fix, with the offer that matters on the second line:
    /// the WebSocket server on a port of its own answered 101 without
    /// permessage-deflate and without a subprotocol, because it read the two
    /// lines as "System.String[]" and as none; a WebSocket on a path of the HTTP
    /// server answered 400, because the HTTP server took both fields for ones
    /// that may not come twice.
    /// </remarks>
    [TestFixture]
    public class WebSocketHandshakeFieldLinesTests
    {

        #region Data

        private const String Handshake = "GET {0} HTTP/1.1\r\n"                              +
                                         "Host: 127.0.0.1\r\n"                               +
                                         "Upgrade: websocket\r\n"                            +
                                         "Connection: Upgrade\r\n"                           +
                                         "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n"   +
                                         "Sec-WebSocket-Version: 13\r\n"                     +
                                         "{1}"                                               +
                                         "\r\n";

        private WebSocketMirrorServer?  webSocketServer;
        private HTTPServer?             httpServer;

        #endregion

        #region TearDown()

        [TearDown]
        public async Task TearDown()
        {

            if (httpServer is not null)
                await httpServer.DisposeAsync();

            if (webSocketServer is not null)
            {
                await webSocketServer.Shutdown(Wait: true);
                await webSocketServer.DisposeAsync();
            }

            httpServer       = null;
            webSocketServer  = null;

        }

        #endregion


        #region ExtensionsOnTwoLines_AreOneOffer(OnAnHTTPPath)

        /// <summary>
        /// permessage-deflate offered on the second of two extension lines is
        /// accepted.
        /// </summary>
        [TestCase(false, TestName = "ExtensionsOnTwoLines_AreOneOffer(on a port of its own)")]
        [TestCase(true,  TestName = "ExtensionsOnTwoLines_AreOneOffer(on a path of the HTTP server)")]
        public async Task ExtensionsOnTwoLines_AreOneOffer(Boolean OnAnHTTPPath)
        {

            var answer = await Upgrade(OnAnHTTPPath,
                                       null,
                                       "Sec-WebSocket-Extensions: x-hermod-unknown\r\n" +
                                       "Sec-WebSocket-Extensions: permessage-deflate\r\n");

            Assert.Multiple(() => {
                Assert.That(answer, Does.StartWith("HTTP/1.1 101"),                          answer);
                Assert.That(answer, Does.Contain("Sec-WebSocket-Extensions: permessage-deflate"), answer);
                Assert.That(answer, Does.Not.Contain("x-hermod-unknown"),                        answer);
            });

        }

        #endregion

        #region SubprotocolsOnTwoLines_AreOneOffer(OnAnHTTPPath)

        /// <summary>
        /// The one subprotocol the server speaks, offered on the second of two
        /// subprotocol lines, is selected.
        /// </summary>
        [TestCase(false, TestName = "SubprotocolsOnTwoLines_AreOneOffer(on a port of its own)")]
        [TestCase(true,  TestName = "SubprotocolsOnTwoLines_AreOneOffer(on a path of the HTTP server)")]
        public async Task SubprotocolsOnTwoLines_AreOneOffer(Boolean OnAnHTTPPath)
        {

            var answer = await Upgrade(OnAnHTTPPath,
                                       [ "chat.hermod.test" ],
                                       "Sec-WebSocket-Protocol: superchat.hermod.test\r\n" +
                                       "Sec-WebSocket-Protocol: chat.hermod.test\r\n");

            Assert.Multiple(() => {
                Assert.That(answer, Does.StartWith("HTTP/1.1 101"),                     answer);
                Assert.That(answer, Does.Contain("Sec-WebSocket-Protocol: chat.hermod.test"), answer);
            });

        }

        #endregion

        #region TheKeyOnTwoLines_IsStillRefused()

        /// <summary>
        /// Not every field that can come twice may: a second Sec-WebSocket-Key
        /// is still refused by the HTTP server, which knows no meaning for it.
        /// </summary>
        [Test]
        public async Task TheKeyOnTwoLines_IsStillRefused()
        {

            var answer = await Upgrade(true,
                                       null,
                                       "Sec-WebSocket-Key: x3JJHMbDL1EzLkh9GBhXDw==\r\n");

            Assert.That(answer, Does.StartWith("HTTP/1.1 400"), answer);

        }

        #endregion


        #region (private) Upgrade(OnAnHTTPPath, SecWebSocketProtocols, FieldLines)

        /// <summary>
        /// Ask for an upgrade with the given additional field lines, and return
        /// the response header as it came.
        /// </summary>
        private async Task<String> Upgrade(Boolean               OnAnHTTPPath,
                                           IEnumerable<String>?  SecWebSocketProtocols,
                                           String                FieldLines)
        {

            webSocketServer = new WebSocketMirrorServer(
                                  IPAddress:              IPv4Address.Localhost,
                                  HTTPPort:               IPPort.Zero,
                                  RequireAuthentication:  false,
                                  SecWebSocketProtocols:  SecWebSocketProtocols,
                                  AutoStart:              !OnAnHTTPPath
                              ) {
                                  EnablePerMessageDeflate = true
                              };

            var port = webSocketServer.IPPort;

            if (OnAnHTTPPath)
            {

                httpServer = await HTTPServer.StartNew();

                httpServer.AddHTTPAPI().AddHandler(
                    HTTPMethod.GET,
                    HTTPPath.Parse("/ws"),
                    HTTPDelegate: WebSocketUpgrade.For(webSocketServer)
                );

                port = httpServer.TCPPort;

            }

            using var tcpClient = new TcpClient();

            await tcpClient.ConnectAsync("127.0.0.1", port.ToInt32());

            var stream = tcpClient.GetStream();

            await stream.WriteAsync(Encoding.ASCII.GetBytes(String.Format(Handshake,
                                                                          OnAnHTTPPath ? "/ws" : "/",
                                                                          FieldLines)));

            var received = new MemoryStream();
            var buffer   = new Byte[4096];

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            while (!Encoding.ASCII.GetString(received.ToArray()).Contains("\r\n\r\n"))
            {

                var count = await stream.ReadAsync(buffer, timeout.Token);

                if (count == 0)
                    break;

                received.Write(buffer, 0, count);

            }

            var text = Encoding.ASCII.GetString(received.ToArray());
            var end  = text.IndexOf("\r\n\r\n");

            return end >= 0 ? text[..end] : text;

        }

        #endregion

    }

}
