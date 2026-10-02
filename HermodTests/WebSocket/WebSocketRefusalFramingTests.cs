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
    /// A refused WebSocket upgrade is answered with one HTTP message - the
    /// header, the empty line and the body as the octets it is - and then the
    /// connection is closed, with nothing after the message.
    /// </summary>
    /// <remarks>
    /// The WebSocket server writes the answer to an upgrade itself, and it
    /// wrote EntirePDU with an empty line of its own after it. With a body,
    /// EntirePDU already ends in the empty line and the body, so four octets
    /// followed that nothing had announced. And EntirePDU is a string: a body
    /// that is not UTF-8 came out with its invalid sequences replaced, and no
    /// longer as long as its Content-Length said.
    ///
    /// A refusal that says Connection: close was then closed the way an
    /// upgraded connection is: with a close frame, 88 02 03 EA, on a
    /// connection that was never upgraded. The client got those four octets
    /// after the answer. And an answer that does not say how long it is ends
    /// its body with the close - neither the 401 an OCPP server turned a
    /// charging station away with nor the server's own 400 said - so there
    /// the close frame was the body of the refusal.
    ///
    /// Read here over a plain TCP connection, octet for octet: a WebSocket or
    /// an HTTP client stops where it believes the message ends, and what comes
    /// after that is exactly what is looked for.
    /// </remarks>
    [TestFixture]
    public class WebSocketRefusalFramingTests
    {

        #region Data

        private WebSocketMirrorServer? server;

        #endregion

        #region TearDown()

        [TearDown]
        public async Task TearDown()
        {

            if (server is not null)
            {
                await server.Shutdown(Wait: true);
                await server.DisposeAsync();
            }

            server = null;

        }

        #endregion


        #region A401WithoutABodyIsItsHeaderAndNothingElse()

        /// <summary>
        /// A 401 without a body - the way an OCPP server turns away a charging
        /// station - is its header and the empty line after it, and then the
        /// close.
        /// </summary>
        [Test]
        public async Task A401WithoutABodyIsItsHeaderAndNothingElse()
        {

            StartServer(RefuseWith401());

            AssertOneMessageAndNothingElse(await AskForAnUpgrade(), 401, []);

        }

        #endregion

        #region A401WithABodyEndsWithItsBody()

        /// <summary>
        /// A 401 with a body ends with the last octet of its body: no empty line
        /// after it, and no close frame.
        /// </summary>
        [Test]
        public async Task A401WithABodyEndsWithItsBody()
        {

            var body = "This charging station is not known here.".ToUTF8Bytes();

            StartServer(RefuseWith401(HTTPContentType.Text.PLAIN, body));

            AssertOneMessageAndNothingElse(await AskForAnUpgrade(), 401, body);

        }

        #endregion

        #region ABodyThatIsNotUTF8GoesOutOctetForOctet()

        /// <summary>
        /// A body that is not UTF-8 goes out as the octets it is, and exactly
        /// as long as its Content-Length says.
        /// </summary>
        [Test]
        public async Task ABodyThatIsNotUTF8GoesOutOctetForOctet()
        {

            // Two octets that are never UTF-8, a NUL that is, a continuation
            // without a start, and a start without its continuation.
            Byte[] body = [ 0xFF, 0xFE, 0x00, 0x80, 0xC3, 0x28 ];

            StartServer(RefuseWith401(HTTPContentType.Application.OCTETSTREAM, body));

            AssertOneMessageAndNothingElse(await AskForAnUpgrade(), 401, body);

        }

        #endregion

        #region AnUnparsableHandshakeIsAnswered400AndNothingElse()

        /// <summary>
        /// The server's own 400 for a handshake it cannot parse - here one
        /// without a Host - is its header and the empty line after it, and then
        /// the close.
        /// </summary>
        [Test]
        public async Task AnUnparsableHandshakeIsAnswered400AndNothingElse()
        {

            StartServer();

            AssertOneMessageAndNothingElse(await AskForAnUpgrade(WithHost: false), 400, []);

        }

        #endregion


        #region (private) StartServer(params Validators)

        /// <summary>
        /// Start a WebSocket server that asks nobody for credentials, so that
        /// what refuses an upgrade is the given validators, or the handshake.
        /// </summary>
        /// <param name="Validators">The handlers of OnValidateWebSocketConnection, in the order they are added.</param>
        private void StartServer(params OnValidateWebSocketConnectionDelegate[] Validators)
        {

            // On IPv4's loopback alone. The default is IPv4's and IPv6's, and a
            // container without IPv6 has no [::1] to listen on - which says
            // nothing about what goes over a connection.
            server = new WebSocketMirrorServer(
                         IPAddress:              IPv4Address.Localhost,
                         HTTPPort:               IPPort.Zero,
                         RequireAuthentication:  false,
                         AutoStart:              true
                     );

            foreach (var validator in Validators)
                server.OnValidateWebSocketConnection += validator;

        }

        #endregion

        #region (private) AskForAnUpgrade(WithHost = true)

        /// <summary>
        /// Ask for an upgrade over a plain TCP connection, and read whatever
        /// comes back until the server closes the connection, or for five
        /// seconds.
        /// </summary>
        /// <param name="WithHost">Whether the request has a Host; without one it cannot be parsed.</param>
        /// <returns>The octets that came back, and whether the server closed the connection.</returns>
        private async Task<(Byte[] Received, Boolean Closed)> AskForAnUpgrade(Boolean WithHost = true)
        {

            using var tcpClient  = new TcpClient(AddressFamily.InterNetwork);

            await tcpClient.ConnectAsync(System.Net.IPAddress.Loopback, server!.IPPort.ToInt32());

            var stream           = tcpClient.GetStream();

            await stream.WriteAsync(
                      Encoding.ASCII.GetBytes(
                          "GET / HTTP/1.1\r\n" +
                          (WithHost
                               ? $"Host: 127.0.0.1:{server.IPPort}\r\n"
                               : "") +
                          "Upgrade: websocket\r\n" +
                          "Connection: Upgrade\r\n" +
                          "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n" +
                          "Sec-WebSocket-Version: 13\r\n" +
                          "\r\n"
                      )
                  );

            var received         = new List<Byte>();
            var buffer           = new Byte[4096];
            var closed           = false;

            using var timeout    = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            try
            {
                while (!closed)
                {

                    var read = await stream.ReadAsync(buffer, timeout.Token);

                    if (read == 0)
                        closed = true;

                    received.AddRange(buffer.Take(read));

                }
            }
            catch (OperationCanceledException)
            { }

            // A reset is a close as well.
            catch (IOException)
            {
                closed = true;
            }

            return ([.. received], closed);

        }

        #endregion

        #region (private static) AssertOneMessageAndNothingElse((Received, Closed), StatusCode, Body)

        /// <summary>
        /// What came back is one HTTP message with the given status and body,
        /// and nothing after it, and then the server closed the connection.
        /// </summary>
        /// <remarks>
        /// Nothing after it means: after the empty line that ends the header,
        /// the body and not one octet more. Without a Content-Length the close
        /// is what ends the body, so there, whatever came before the close
        /// would have been the body - and the body that is due is none.
        /// </remarks>
        /// <param name="Answer">The octets that came back, and whether the server closed the connection.</param>
        /// <param name="StatusCode">The status the upgrade is refused with.</param>
        /// <param name="Body">The body the refusal has.</param>
        private static void AssertOneMessageAndNothingElse((Byte[] Received, Boolean Closed)  Answer,
                                                           UInt16                              StatusCode,
                                                           Byte[]                              Body)
        {

            var (received, closed)  = Answer;

            var endOfHeader         = received.AsSpan().IndexOf("\r\n\r\n"u8);

            Assert.That(endOfHeader, Is.GreaterThan(0),
                        $"What came back is not an HTTP message: {Hex(received)}");

            var header              = Encoding.ASCII.GetString(received, 0, endOfHeader);
            var afterHeader         = received[(endOfHeader + 4)..];

            var contentLength       = header.Split("\r\n").
                                             Select(line  => line.Split(':', 2)).
                                             Where (field => field.Length == 2 &&
                                                             field[0].Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase)).
                                             Select(field => field[1].Trim()).
                                             FirstOrDefault();

            Assert.Multiple(() => {

                Assert.That(header, Does.StartWith($"HTTP/1.1 {StatusCode} "),
                            $"The upgrade was answered with:{Environment.NewLine}{header}");

                Assert.That(afterHeader, Is.EqualTo(Body),
                            $"After the header came {Hex(afterHeader)} - where the body and nothing else was due: {Hex(Body)}");

                if (Body.Length > 0)
                    Assert.That(contentLength, Is.EqualTo(Body.Length.ToString()),
                                "The Content-Length does not say how long the body is.");

                Assert.That(closed, Is.True,
                            "The server did not close the connection after refusing the upgrade.");

            });

        }

        #endregion

        #region (private static) RefuseWith401(ContentType = null, Body = null)

        /// <summary>
        /// A validator that refuses every upgrade the way an OCPP server turns
        /// away a charging station: 401, a challenge, and Connection: close -
        /// and the given body, where there is one.
        /// </summary>
        /// <param name="ContentType">The content type of the body.</param>
        /// <param name="Body">The body, if the refusal is to have one.</param>
        private static OnValidateWebSocketConnectionDelegate RefuseWith401(HTTPContentType?  ContentType   = null,
                                                                           Byte[]?           Body          = null)

            => (timestamp, webSocketServer, connection, eventTrackingId, cancellationToken) => {

                   var refusal = new HTTPResponse.Builder(connection.HTTPRequest!) {
                                     HTTPStatusCode   = HTTPStatusCode.Unauthorized,
                                     WWWAuthenticate  = WWWAuthenticate.Basic("OCPP"),
                                     Connection       = ConnectionType.Close
                                 };

                   if (Body is not null)
                   {
                       refusal.ContentType  = ContentType;
                       refusal.Content      = Body;
                   }

                   return Task.FromResult<HTTPResponse?>(refusal.AsImmutable);

               };

        #endregion

        #region (private static) Hex(Octets)

        /// <summary>
        /// The given octets in hex, or "nothing".
        /// </summary>
        private static String Hex(Byte[] Octets)

            => Octets.Length > 0
                   ? BitConverter.ToString(Octets).Replace('-', ' ')
                   : "nothing";

        #endregion

    }

}
