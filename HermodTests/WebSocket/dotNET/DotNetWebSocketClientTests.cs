/*
 * Copyright (c) 2010-2023, Achim Friedland <achim.friedland@graphdefined.com>
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
using System.Net;
using System.Net.WebSockets;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.UnitTests.HTTP.WebSockets
{

    /// <summary>
    /// Tests between .NET WebSocket clients and Hermod WebSocket servers.
    /// </summary>
    [TestFixture]
    public class DotNetWebSocketClientTests : AWebSocketServerTests
    {

        #region Constructor(s)

        public DotNetWebSocketClientTests()
            : base(IPPort.Parse(111))
        { }

        #endregion


        #region Test_AnonymousAccess()

        [Test]
        public async Task Test_AnonymousAccess()
        {

            #region Setup

            if (webSocketServer is null) {
                Assert.Fail("WebSocketServer is null!");
                return;
            }

            var validatedTCP            = new List<String>();
            var newTCPConnection        = new List<String>();
            var validatedWebSocket      = new List<String>();
            var newWebSocketConnection  = new List<String>();
            var httpRequests            = new List<HTTPRequest>();
            var httpResponses           = new List<HTTPResponse>();
            var messageRequests         = new List<WebSocketFrame>();
            var messageResponses        = new List<WebSocketFrame>();
            var textMessageRequests     = new List<String>();
            var textMessageResponses    = new List<String>();
            var binaryMessageRequests   = new List<Byte[]>();
            var binaryMessageResponses  = new List<Byte[]>();

            webSocketServer.OnValidateTCPConnection       += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedTCP.Add($"{validatedTCP.Count}: {connection.Client.RemoteEndPoint?.ToString() ?? "-"}");
                return true;
            };

            webSocketServer.OnNewTCPConnection            += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newTCPConnection.Add($"{newTCPConnection.Count}: {connection.RemoteSocket}");
            };

            webSocketServer.OnHTTPRequest                 += async (timestamp, server, httpRequest) => {
                httpRequests.Add(httpRequest);
            };

            webSocketServer.OnValidateWebSocketConnection += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedWebSocket.Add($"{validatedWebSocket.Count}: {connection.RemoteSocket}");
                return null;
            };

            webSocketServer.OnHTTPResponse                += async (timestamp, server, httpRequest, httpResponse) => {
                httpResponses.Add(httpResponse);
            };

            webSocketServer.OnNewWebSocketConnection      += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newWebSocketConnection.Add($"{newWebSocketConnection.Count}: {connection.RemoteSocket}");
            };

            webSocketServer.OnWebSocketFrameReceived      += async (timestamp, server, connection, eventTrackingId, requestFrame) => {
                messageRequests.       Add(requestFrame);
            };

            webSocketServer.OnWebSocketFrameSent          += async (timestamp, server, connection, eventTrackingId, responseFrame) => {
                messageResponses.      Add(responseFrame);
            };

            webSocketServer.OnTextMessageReceived         += async (timestamp, server, connection, eventTrackingId, textMessage) => {
                textMessageRequests.   Add(textMessage);
            };

            webSocketServer.OnTextMessageSent             += async (timestamp, server, connection, eventTrackingId, textMessage) => {
                textMessageResponses.  Add(textMessage ?? "-");
            };

            webSocketServer.OnBinaryMessageReceived       += async (timestamp, server, connection, eventTrackingId, binaryMessage) => {
                binaryMessageRequests. Add(binaryMessage);
            };

            webSocketServer.OnBinaryMessageSent           += async (timestamp, server, connection, eventTrackingId, binaryMessage) => {
                binaryMessageResponses.Add(binaryMessage);
            };

            #endregion


            var clientWebSocket = new ClientWebSocket();

            try
            {
                await clientWebSocket.ConnectAsync(new Uri($"ws://127.0.0.1:{HTTPPort}"),
                                                   CancellationToken.None);
            }
            catch (Exception e)
            {
                Assert.Fail(e.Message);
            }

            var state                = clientWebSocket.State;
            var closeStatus          = clientWebSocket.CloseStatus;
            var options              = clientWebSocket.Options;
            var subProtocol          = clientWebSocket.SubProtocol;
            var httpStatusCode       = clientWebSocket.HttpStatusCode;
            var httpResponseHeaders  = clientWebSocket.HttpResponseHeaders;


            #region Check HTTP request

            // Wait a bit, because running multiple tests at once has timing issues!
            while (newWebSocketConnection.Count == 0)
                Thread.Sleep(10);

            Assert.AreEqual(1, validatedTCP.          Count, validatedTCP.          AggregateWith(", "));
            Assert.AreEqual(1, newTCPConnection.      Count, newTCPConnection.      AggregateWith(", "));
            Assert.AreEqual(1, validatedWebSocket.    Count, validatedWebSocket.    AggregateWith(", "));
            Assert.AreEqual(1, newWebSocketConnection.Count, newWebSocketConnection.AggregateWith(", "));

            Assert.AreEqual(1, httpRequests.          Count);
            Assert.AreEqual(1, httpResponses.         Count);
            Assert.AreEqual(1, webSocketServer.WebSocketConnections.Count());


            var httpRequest          = httpRequests.First();
            var request              = httpRequest.EntirePDU;

            // GET / HTTP/1.1
            // Host:                    127.0.0.1:111
            // Connection:              Upgrade
            // Upgrade:                 websocket
            // Sec-WebSocket-Key:       +LYHhVOGskWz/0bFFcK8dQ==
            // Sec-WebSocket-Version:   13

            Assert.AreEqual("Upgrade",                                         httpRequest.Connection);
            Assert.AreEqual("websocket",                                       httpRequest.Upgrade);

            Assert.IsTrue  (request.Contains("GET / HTTP/1.1"),                request);
            Assert.IsTrue  (request.Contains($"Host: 127.0.0.1:{HTTPPort}"),   request);
            Assert.IsTrue  (request.Contains($"Connection: Upgrade"),          request);
            Assert.IsTrue  (request.Contains($"Upgrade: websocket"),           request);
            Assert.IsTrue  (request.Contains($"Sec-WebSocket-Key:"),           request);
            Assert.IsTrue  (request.Contains($"Sec-WebSocket-Version:"),       request);

            #endregion

            #region Check HTTP response

            var httpResponse  = httpResponses.First();
            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 101 Switching Protocols
            // Date:                     Wed, 02 Aug 2023 19:33:53 GMT
            // Server:                   GraphDefined HTTP Web Socket Service v2.0
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Accept:     s9FvxhRowHKxS38G/sBt7gC5qec=
            // Sec-WebSocket-Version:    13

            Assert.IsTrue  (response.Contains("HTTP/1.1 101 Switching Protocols"),   response);

            Assert.AreEqual("GraphDefined HTTP Web Socket Service v2.0",             httpResponse.Server);
            Assert.AreEqual("Upgrade",                                               httpResponse.Connection);
            Assert.AreEqual("websocket",                                             httpResponse.Upgrade);

            #endregion


            #region Send messages

            await clientWebSocket.SendAsync(buffer:             new ArraySegment<Byte>(Encoding.UTF8.GetBytes("1234")),
                                            messageType:        WebSocketMessageType.Text,
                                            endOfMessage:       true,
                                            cancellationToken:  CancellationToken.None);

            while (textMessageResponses.  Count == 0)
                Thread.Sleep(10);

            await clientWebSocket.SendAsync(buffer:             new ArraySegment<Byte>(Encoding.UTF8.GetBytes("ABCD")),
                                            messageType:        WebSocketMessageType.Binary,
                                            endOfMessage:       true,
                                            cancellationToken:  CancellationToken.None);

            while (binaryMessageResponses.Count == 0)
                Thread.Sleep(10);

            #endregion

            #region Validate message delivery

            Assert.AreEqual(2,       messageRequests. Count);
            Assert.AreEqual("1234",  messageRequests. ElementAt(0).Payload.ToUTF8String());
            Assert.AreEqual("ABCD",  messageRequests. ElementAt(1).Payload.ToUTF8String());

            Assert.AreEqual(2,       messageResponses.Count);
            Assert.AreEqual("4321",  messageResponses.ElementAt(0).Payload.ToUTF8String());
            Assert.AreEqual("DCBA",  messageResponses.ElementAt(1).Payload.ToUTF8String());


            Assert.AreEqual(1,       textMessageRequests.   Count);
            Assert.AreEqual("1234",  textMessageRequests.   ElementAt(0));
            Assert.AreEqual(1,       binaryMessageRequests. Count);
            Assert.AreEqual("ABCD",  binaryMessageRequests. ElementAt(0).ToUTF8String());

            Assert.AreEqual(1,       textMessageResponses.  Count);
            Assert.AreEqual("4321",  textMessageResponses.  ElementAt(0));
            Assert.AreEqual(1,       binaryMessageResponses.Count);
            Assert.AreEqual("DCBA",  binaryMessageResponses.ElementAt(0).ToUTF8String());

            #endregion


            try
            {

                await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                                                 "Done",
                                                 CancellationToken.None);

            }
            catch (Exception e)
            {

                // System.Net.WebSockets.WebSocketException
                // The remote party closed the WebSocket connection without completing the close handshake.

                Assert.Fail(e.Message);

            }


        }

        #endregion


        #region Test_UnknownSubprotocol()

        [Test]
        public async Task Test_UnknownSubprotocol()
        {

            #region Setup

            if (webSocketServer is null) {
                Assert.Fail("WebSocketServer is null!");
                return;
            }

            var validatedTCP            = new List<String>();
            var newTCPConnection        = new List<String>();
            var validatedWebSocket      = new List<String>();
            var newWebSocketConnection  = new List<String>();
            var httpRequests            = new List<HTTPRequest>();
            var httpResponses           = new List<HTTPResponse>();
            var messageRequests         = new List<WebSocketFrame>();
            var messageResponses        = new List<WebSocketFrame>();
            var textMessageRequests     = new List<String>();
            var textMessageResponses    = new List<String>();
            var binaryMessageRequests   = new List<Byte[]>();
            var binaryMessageResponses  = new List<Byte[]>();

            webSocketServer.OnValidateTCPConnection       += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedTCP.Add($"{validatedTCP.Count}: {connection.Client.RemoteEndPoint?.ToString() ?? "-"}");
                return true;
            };

            webSocketServer.OnNewTCPConnection            += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newTCPConnection.Add($"{newTCPConnection.Count}: {connection.RemoteSocket}");
            };

            webSocketServer.OnHTTPRequest                 += async (timestamp, server, httpRequest) => {
                httpRequests.Add(httpRequest);
            };

            webSocketServer.OnValidateWebSocketConnection += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedWebSocket.Add($"{validatedWebSocket.Count}: {connection.RemoteSocket}");
                return null;
            };

            webSocketServer.OnHTTPResponse                += async (timestamp, server, httpRequest, httpResponse) => {
                httpResponses.Add(httpResponse);
            };

            webSocketServer.OnNewWebSocketConnection      += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newWebSocketConnection.Add($"{newWebSocketConnection.Count}: {connection.RemoteSocket}");
            };

            webSocketServer.OnWebSocketFrameReceived      += async (timestamp, server, connection, eventTrackingId, requestFrame) => {
                messageRequests.       Add(requestFrame);
            };

            webSocketServer.OnWebSocketFrameSent          += async (timestamp, server, connection, eventTrackingId, responseFrame) => {
                messageResponses.      Add(responseFrame);
            };

            webSocketServer.OnTextMessageReceived         += async (timestamp, server, connection, eventTrackingId, textMessage) => {
                textMessageRequests.   Add(textMessage);
            };

            webSocketServer.OnTextMessageSent             += async (timestamp, server, connection, eventTrackingId, textMessage) => {
                textMessageResponses.  Add(textMessage ?? "-");
            };

            webSocketServer.OnBinaryMessageReceived       += async (timestamp, server, connection, eventTrackingId, binaryMessage) => {
                binaryMessageRequests. Add(binaryMessage);
            };

            webSocketServer.OnBinaryMessageSent           += async (timestamp, server, connection, eventTrackingId, binaryMessage) => {
                binaryMessageResponses.Add(binaryMessage);
            };

            #endregion


            var clientWebSocket = new ClientWebSocket();

            clientWebSocket.Options.AddSubProtocol("ocpp1.6");

            try
            {
                await clientWebSocket.ConnectAsync(new Uri($"ws://127.0.0.1:{HTTPPort}"), CancellationToken.None);
            }
            catch (Exception e)
            {
                Assert.Fail(e.Message);
            }

            var state                = clientWebSocket.State;
            var closeStatus          = clientWebSocket.CloseStatus;
            var options              = clientWebSocket.Options;
            var subProtocol          = clientWebSocket.SubProtocol;
            var httpStatusCode       = clientWebSocket.HttpStatusCode;
            var httpResponseHeaders  = clientWebSocket.HttpResponseHeaders;


            #region Check HTTP request

            // Wait a bit, because running multiple tests at once has timing issues!
            while (newWebSocketConnection.Count == 0)
                Thread.Sleep(10);

            Assert.AreEqual(1, validatedTCP.          Count, validatedTCP.          AggregateWith(", "));
            Assert.AreEqual(1, newTCPConnection.      Count, newTCPConnection.      AggregateWith(", "));
            Assert.AreEqual(1, validatedWebSocket.    Count, validatedWebSocket.    AggregateWith(", "));
            Assert.AreEqual(1, newWebSocketConnection.Count, newWebSocketConnection.AggregateWith(", "));

            Assert.AreEqual(1, httpRequests.          Count);
            Assert.AreEqual(1, httpResponses.         Count);
            Assert.AreEqual(1, webSocketServer.WebSocketConnections.Count());


            var httpRequest          = httpRequests.First();
            var request              = httpRequest.EntirePDU;

            // GET / HTTP/1.1
            // Host:                     127.0.0.1:111
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Key:        +LYHhVOGskWz/0bFFcK8dQ==
            // Sec-WebSocket-Version:    13
            // Sec-WebSocket-Protocol:   ocpp1.6

            Assert.AreEqual("Upgrade",                                         httpRequest.Connection);
            Assert.AreEqual("websocket",                                       httpRequest.Upgrade);

            Assert.IsTrue  (request.Contains("GET / HTTP/1.1"),                request);
            Assert.IsTrue  (request.Contains($"Host: 127.0.0.1:{HTTPPort}"),   request);
            Assert.IsTrue  (request.Contains($"Connection: Upgrade"),          request);
            Assert.IsTrue  (request.Contains($"Upgrade: websocket"),           request);
            Assert.IsTrue  (request.Contains($"Sec-WebSocket-Key:"),           request);
            Assert.IsTrue  (request.Contains($"Sec-WebSocket-Version:"),       request);

            #endregion

            #region Check HTTP response

            var httpResponse  = httpResponses.First();
            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 101 Switching Protocols
            // Date:                     Wed, 02 Aug 2023 19:33:53 GMT
            // Server:                   GraphDefined HTTP Web Socket Service v2.0
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Accept:     s9FvxhRowHKxS38G/sBt7gC5qec=
            // Sec-WebSocket-Version:    13

            Assert.IsTrue  (response.Contains("HTTP/1.1 101 Switching Protocols"),   response);

            Assert.AreEqual("GraphDefined HTTP Web Socket Service v2.0",             httpResponse.Server);
            Assert.AreEqual("Upgrade",                                               httpResponse.Connection);
            Assert.AreEqual("websocket",                                             httpResponse.Upgrade);

            #endregion


            #region Send messages

            await clientWebSocket.SendAsync(buffer:             new ArraySegment<Byte>(Encoding.UTF8.GetBytes("1234")),
                                            messageType:        WebSocketMessageType.Text,
                                            endOfMessage:       true,
                                            cancellationToken:  CancellationToken.None);

            while (textMessageResponses.  Count == 0)
                Thread.Sleep(10);

            await clientWebSocket.SendAsync(buffer:             new ArraySegment<Byte>(Encoding.UTF8.GetBytes("ABCD")),
                                            messageType:        WebSocketMessageType.Binary,
                                            endOfMessage:       true,
                                            cancellationToken:  CancellationToken.None);

            while (binaryMessageResponses.Count == 0)
                Thread.Sleep(10);

            #endregion

            #region Validate message delivery

            Assert.AreEqual(2,       messageRequests. Count);
            Assert.AreEqual("1234",  messageRequests. ElementAt(0).Payload.ToUTF8String());
            Assert.AreEqual("ABCD",  messageRequests. ElementAt(1).Payload.ToUTF8String());

            Assert.AreEqual(2,       messageResponses.Count);
            Assert.AreEqual("4321",  messageResponses.ElementAt(0).Payload.ToUTF8String());
            Assert.AreEqual("DCBA",  messageResponses.ElementAt(1).Payload.ToUTF8String());


            Assert.AreEqual(1,       textMessageRequests.   Count);
            Assert.AreEqual("1234",  textMessageRequests.   ElementAt(0));
            Assert.AreEqual(1,       binaryMessageRequests. Count);
            Assert.AreEqual("ABCD",  binaryMessageRequests. ElementAt(0).ToUTF8String());

            Assert.AreEqual(1,       textMessageResponses.  Count);
            Assert.AreEqual("4321",  textMessageResponses.  ElementAt(0));
            Assert.AreEqual(1,       binaryMessageResponses.Count);
            Assert.AreEqual("DCBA",  binaryMessageResponses.ElementAt(0).ToUTF8String());

            #endregion


            try
            {

                await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                                                 "Done",
                                                 CancellationToken.None);

            }
            catch (Exception e)
            {

                // System.Net.WebSockets.WebSocketException
                // The remote party closed the WebSocket connection without completing the close handshake.

                Assert.Fail(e.Message);

            }


        }

        #endregion

        #region Test_KnownSubprotocol()

        [Test]
        public async Task Test_KnownSubprotocol()
        {

            #region Setup

            if (webSocketServer is null) {
                Assert.Fail("WebSocketServer is null!");
                return;
            }

            webSocketServer.SecWebSocketProtocols.Add("ocpp1.6");

            var validatedTCP            = new List<String>();
            var newTCPConnection        = new List<String>();
            var validatedWebSocket      = new List<String>();
            var newWebSocketConnection  = new List<String>();
            var httpRequests            = new List<HTTPRequest>();
            var httpResponses           = new List<HTTPResponse>();
            var messageRequests         = new List<WebSocketFrame>();
            var messageResponses        = new List<WebSocketFrame>();
            var textMessageRequests     = new List<String>();
            var textMessageResponses    = new List<String>();
            var binaryMessageRequests   = new List<Byte[]>();
            var binaryMessageResponses  = new List<Byte[]>();

            webSocketServer.OnValidateTCPConnection       += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedTCP.Add($"{validatedTCP.Count}: {connection.Client.RemoteEndPoint?.ToString() ?? "-"}");
                return true;
            };

            webSocketServer.OnNewTCPConnection            += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newTCPConnection.Add($"{newTCPConnection.Count}: {connection.RemoteSocket}");
            };

            webSocketServer.OnHTTPRequest                 += async (timestamp, server, httpRequest) => {
                httpRequests.Add(httpRequest);
            };

            webSocketServer.OnValidateWebSocketConnection += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedWebSocket.Add($"{validatedWebSocket.Count}: {connection.RemoteSocket}");
                return null;
            };

            webSocketServer.OnHTTPResponse                += async (timestamp, server, httpRequest, httpResponse) => {
                httpResponses.Add(httpResponse);
            };

            webSocketServer.OnNewWebSocketConnection      += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newWebSocketConnection.Add($"{newWebSocketConnection.Count}: {connection.RemoteSocket}");
            };

            webSocketServer.OnWebSocketFrameReceived      += async (timestamp, server, connection, eventTrackingId, requestFrame) => {
                messageRequests.       Add(requestFrame);
            };

            webSocketServer.OnWebSocketFrameSent          += async (timestamp, server, connection, eventTrackingId, responseFrame) => {
                messageResponses.      Add(responseFrame);
            };

            webSocketServer.OnTextMessageReceived         += async (timestamp, server, connection, eventTrackingId, textMessage) => {
                textMessageRequests.   Add(textMessage);
            };

            webSocketServer.OnTextMessageSent             += async (timestamp, server, connection, eventTrackingId, textMessage) => {
                textMessageResponses.  Add(textMessage ?? "-");
            };

            webSocketServer.OnBinaryMessageReceived       += async (timestamp, server, connection, eventTrackingId, binaryMessage) => {
                binaryMessageRequests. Add(binaryMessage);
            };

            webSocketServer.OnBinaryMessageSent           += async (timestamp, server, connection, eventTrackingId, binaryMessage) => {
                binaryMessageResponses.Add(binaryMessage);
            };

            #endregion


            var clientWebSocket = new ClientWebSocket();

            clientWebSocket.Options.AddSubProtocol("ocpp1.6");

            try
            {
                await clientWebSocket.ConnectAsync(new Uri($"ws://127.0.0.1:{HTTPPort}"), CancellationToken.None);
            }
            catch (Exception e)
            {
                Assert.Fail(e.Message);
            }

            var state                = clientWebSocket.State;
            var closeStatus          = clientWebSocket.CloseStatus;
            var options              = clientWebSocket.Options;
            var subProtocol          = clientWebSocket.SubProtocol;
            var httpStatusCode       = clientWebSocket.HttpStatusCode;
            var httpResponseHeaders  = clientWebSocket.HttpResponseHeaders;


            #region Check HTTP request

            // Wait a bit, because running multiple tests at once has timing issues!
            while (newWebSocketConnection.Count == 0)
                Thread.Sleep(10);

            Assert.AreEqual(1, validatedTCP.          Count, validatedTCP.          AggregateWith(", "));
            Assert.AreEqual(1, newTCPConnection.      Count, newTCPConnection.      AggregateWith(", "));
            Assert.AreEqual(1, validatedWebSocket.    Count, validatedWebSocket.    AggregateWith(", "));
            Assert.AreEqual(1, newWebSocketConnection.Count, newWebSocketConnection.AggregateWith(", "));

            Assert.AreEqual(1, httpRequests.          Count);
            Assert.AreEqual(1, httpResponses.         Count);
            Assert.AreEqual(1, webSocketServer.WebSocketConnections.Count());


            var httpRequest          = httpRequests.First();
            var request              = httpRequest.EntirePDU;

            // GET / HTTP/1.1
            // Host:                     127.0.0.1:111
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Key:        +LYHhVOGskWz/0bFFcK8dQ==
            // Sec-WebSocket-Version:    13
            // Sec-WebSocket-Protocol:   ocpp1.6

            Assert.AreEqual("Upgrade",                                         httpRequest.Connection);
            Assert.AreEqual("websocket",                                       httpRequest.Upgrade);

            Assert.IsTrue  (request.Contains("GET / HTTP/1.1"),                request);
            Assert.IsTrue  (request.Contains($"Host: 127.0.0.1:{HTTPPort}"),   request);
            Assert.IsTrue  (request.Contains($"Connection: Upgrade"),          request);
            Assert.IsTrue  (request.Contains($"Upgrade: websocket"),           request);
            Assert.IsTrue  (request.Contains($"Sec-WebSocket-Key:"),           request);
            Assert.IsTrue  (request.Contains($"Sec-WebSocket-Version:"),       request);

            #endregion

            #region Check HTTP response

            var httpResponse  = httpResponses.First();
            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 101 Switching Protocols
            // Date:                     Wed, 02 Aug 2023 19:33:53 GMT
            // Server:                   GraphDefined HTTP Web Socket Service v2.0
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Accept:     s9FvxhRowHKxS38G/sBt7gC5qec=
            // Sec-WebSocket-Protocol:   ocpp1.6
            // Sec-WebSocket-Version:    13

            Assert.IsTrue  (response.Contains("HTTP/1.1 101 Switching Protocols"),   response);

            Assert.AreEqual("GraphDefined HTTP Web Socket Service v2.0",             httpResponse.Server);
            Assert.AreEqual("Upgrade",                                               httpResponse.Connection);
            Assert.AreEqual("websocket",                                             httpResponse.Upgrade);

            #endregion


            #region Send messages

            await clientWebSocket.SendAsync(buffer:             new ArraySegment<Byte>(Encoding.UTF8.GetBytes("1234")),
                                            messageType:        WebSocketMessageType.Text,
                                            endOfMessage:       true,
                                            cancellationToken:  CancellationToken.None);

            while (textMessageResponses.  Count == 0)
                Thread.Sleep(10);

            await clientWebSocket.SendAsync(buffer:             new ArraySegment<Byte>(Encoding.UTF8.GetBytes("ABCD")),
                                            messageType:        WebSocketMessageType.Binary,
                                            endOfMessage:       true,
                                            cancellationToken:  CancellationToken.None);

            while (binaryMessageResponses.Count == 0)
                Thread.Sleep(10);

            #endregion

            #region Validate message delivery

            Assert.AreEqual(2,       messageRequests. Count);
            Assert.AreEqual("1234",  messageRequests. ElementAt(0).Payload.ToUTF8String());
            Assert.AreEqual("ABCD",  messageRequests. ElementAt(1).Payload.ToUTF8String());

            Assert.AreEqual(2,       messageResponses.Count);
            Assert.AreEqual("4321",  messageResponses.ElementAt(0).Payload.ToUTF8String());
            Assert.AreEqual("DCBA",  messageResponses.ElementAt(1).Payload.ToUTF8String());


            Assert.AreEqual(1,       textMessageRequests.   Count);
            Assert.AreEqual("1234",  textMessageRequests.   ElementAt(0));
            Assert.AreEqual(1,       binaryMessageRequests. Count);
            Assert.AreEqual("ABCD",  binaryMessageRequests. ElementAt(0).ToUTF8String());

            Assert.AreEqual(1,       textMessageResponses.  Count);
            Assert.AreEqual("4321",  textMessageResponses.  ElementAt(0));
            Assert.AreEqual(1,       binaryMessageResponses.Count);
            Assert.AreEqual("DCBA",  binaryMessageResponses.ElementAt(0).ToUTF8String());

            #endregion


            try
            {

                await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                                                 "Done",
                                                 CancellationToken.None);

            }
            catch (Exception e)
            {

                // System.Net.WebSockets.WebSocketException
                // The remote party closed the WebSocket connection without completing the close handshake.

                Assert.Fail(e.Message);

            }


        }

        #endregion

        #region Test_KnownSubprotocols()

        [Test]
        public async Task Test_KnownSubprotocols()
        {

            #region Setup

            if (webSocketServer is null) {
                Assert.Fail("WebSocketServer is null!");
                return;
            }

            webSocketServer.SecWebSocketProtocols.Add("ocpp1.6");
            webSocketServer.SecWebSocketProtocols.Add("ocpp2.0");

            var validatedTCP            = new List<String>();
            var newTCPConnection        = new List<String>();
            var validatedWebSocket      = new List<String>();
            var newWebSocketConnection  = new List<String>();
            var httpRequests            = new List<HTTPRequest>();
            var httpResponses           = new List<HTTPResponse>();
            var messageRequests         = new List<WebSocketFrame>();
            var messageResponses        = new List<WebSocketFrame>();
            var textMessageRequests     = new List<String>();
            var textMessageResponses    = new List<String>();
            var binaryMessageRequests   = new List<Byte[]>();
            var binaryMessageResponses  = new List<Byte[]>();

            webSocketServer.OnValidateTCPConnection       += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedTCP.Add($"{validatedTCP.Count}: {connection.Client.RemoteEndPoint?.ToString() ?? "-"}");
                return true;
            };

            webSocketServer.OnNewTCPConnection            += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newTCPConnection.Add($"{newTCPConnection.Count}: {connection.RemoteSocket}");
            };

            webSocketServer.OnHTTPRequest                 += async (timestamp, server, httpRequest) => {
                httpRequests.Add(httpRequest);
            };

            webSocketServer.OnValidateWebSocketConnection += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedWebSocket.Add($"{validatedWebSocket.Count}: {connection.RemoteSocket}");
                return null;
            };

            webSocketServer.OnHTTPResponse                += async (timestamp, server, httpRequest, httpResponse) => {
                httpResponses.Add(httpResponse);
            };

            webSocketServer.OnNewWebSocketConnection      += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newWebSocketConnection.Add($"{newWebSocketConnection.Count}: {connection.RemoteSocket}");
            };

            webSocketServer.OnWebSocketFrameReceived      += async (timestamp, server, connection, eventTrackingId, requestFrame) => {
                messageRequests.       Add(requestFrame);
            };

            webSocketServer.OnWebSocketFrameSent          += async (timestamp, server, connection, eventTrackingId, responseFrame) => {
                messageResponses.      Add(responseFrame);
            };

            webSocketServer.OnTextMessageReceived         += async (timestamp, server, connection, eventTrackingId, textMessage) => {
                textMessageRequests.   Add(textMessage);
            };

            webSocketServer.OnTextMessageSent             += async (timestamp, server, connection, eventTrackingId, textMessage) => {
                textMessageResponses.  Add(textMessage ?? "-");
            };

            webSocketServer.OnBinaryMessageReceived       += async (timestamp, server, connection, eventTrackingId, binaryMessage) => {
                binaryMessageRequests. Add(binaryMessage);
            };

            webSocketServer.OnBinaryMessageSent           += async (timestamp, server, connection, eventTrackingId, binaryMessage) => {
                binaryMessageResponses.Add(binaryMessage);
            };

            #endregion


            var clientWebSocket = new ClientWebSocket();

            //clientWebSocket.Options.Credentials = CredentialCache.DefaultCredentials;
            clientWebSocket.Options.AddSubProtocol("ocpp2.0");

            try
            {
                await clientWebSocket.ConnectAsync(new Uri($"ws://127.0.0.1:{HTTPPort}"), CancellationToken.None);
            }
            catch (Exception e)
            {
                Assert.Fail(e.Message);
            }

            var state                = clientWebSocket.State;
            var closeStatus          = clientWebSocket.CloseStatus;
            var options              = clientWebSocket.Options;
            var subProtocol          = clientWebSocket.SubProtocol;
            var httpStatusCode       = clientWebSocket.HttpStatusCode;
            var httpResponseHeaders  = clientWebSocket.HttpResponseHeaders;


            #region Check HTTP request

            // Wait a bit, because running multiple tests at once has timing issues!
            while (newWebSocketConnection.Count == 0)
                Thread.Sleep(10);

            Assert.AreEqual(1, validatedTCP.          Count, validatedTCP.          AggregateWith(", "));
            Assert.AreEqual(1, newTCPConnection.      Count, newTCPConnection.      AggregateWith(", "));
            Assert.AreEqual(1, validatedWebSocket.    Count, validatedWebSocket.    AggregateWith(", "));
            Assert.AreEqual(1, newWebSocketConnection.Count, newWebSocketConnection.AggregateWith(", "));

            Assert.AreEqual(1, httpRequests.          Count);
            Assert.AreEqual(1, httpResponses.         Count);
            Assert.AreEqual(1, webSocketServer.WebSocketConnections.Count());


            var httpRequest          = httpRequests.First();
            var request              = httpRequest.EntirePDU;

            // GET / HTTP/1.1
            // Host:                     127.0.0.1:111
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Key:        +LYHhVOGskWz/0bFFcK8dQ==
            // Sec-WebSocket-Version:    13
            // Sec-WebSocket-Protocol:   ocpp2.0

            Assert.AreEqual("Upgrade",                                         httpRequest.Connection);
            Assert.AreEqual("websocket",                                       httpRequest.Upgrade);

            Assert.IsTrue  (request.Contains("GET / HTTP/1.1"),                request);
            Assert.IsTrue  (request.Contains($"Host: 127.0.0.1:{HTTPPort}"),   request);
            Assert.IsTrue  (request.Contains($"Connection: Upgrade"),          request);
            Assert.IsTrue  (request.Contains($"Upgrade: websocket"),           request);
            Assert.IsTrue  (request.Contains($"Sec-WebSocket-Key:"),           request);
            Assert.IsTrue  (request.Contains($"Sec-WebSocket-Version:"),       request);

            #endregion

            #region Check HTTP response

            var httpResponse  = httpResponses.First();
            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 101 Switching Protocols
            // Date:                     Wed, 02 Aug 2023 19:33:53 GMT
            // Server:                   GraphDefined HTTP Web Socket Service v2.0
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Accept:     s9FvxhRowHKxS38G/sBt7gC5qec=
            // Sec-WebSocket-Protocol:   ocpp2.0
            // Sec-WebSocket-Version:    13

            Assert.IsTrue  (response.Contains("HTTP/1.1 101 Switching Protocols"),   response);

            Assert.AreEqual("GraphDefined HTTP Web Socket Service v2.0",             httpResponse.Server);
            Assert.AreEqual("Upgrade",                                               httpResponse.Connection);
            Assert.AreEqual("websocket",                                             httpResponse.Upgrade);

            #endregion


            #region Send messages

            await clientWebSocket.SendAsync(buffer:             new ArraySegment<Byte>(Encoding.UTF8.GetBytes("1234")),
                                            messageType:        WebSocketMessageType.Text,
                                            endOfMessage:       true,
                                            cancellationToken:  CancellationToken.None);

            while (textMessageResponses.  Count == 0)
                Thread.Sleep(10);

            await clientWebSocket.SendAsync(buffer:             new ArraySegment<Byte>(Encoding.UTF8.GetBytes("ABCD")),
                                            messageType:        WebSocketMessageType.Binary,
                                            endOfMessage:       true,
                                            cancellationToken:  CancellationToken.None);

            while (binaryMessageResponses.Count == 0)
                Thread.Sleep(10);

            #endregion

            #region Validate message delivery

            Assert.AreEqual(2,       messageRequests. Count);
            Assert.AreEqual("1234",  messageRequests. ElementAt(0).Payload.ToUTF8String());
            Assert.AreEqual("ABCD",  messageRequests. ElementAt(1).Payload.ToUTF8String());

            Assert.AreEqual(2,       messageResponses.Count);
            Assert.AreEqual("4321",  messageResponses.ElementAt(0).Payload.ToUTF8String());
            Assert.AreEqual("DCBA",  messageResponses.ElementAt(1).Payload.ToUTF8String());


            Assert.AreEqual(1,       textMessageRequests.   Count);
            Assert.AreEqual("1234",  textMessageRequests.   ElementAt(0));
            Assert.AreEqual(1,       binaryMessageRequests. Count);
            Assert.AreEqual("ABCD",  binaryMessageRequests. ElementAt(0).ToUTF8String());

            Assert.AreEqual(1,       textMessageResponses.  Count);
            Assert.AreEqual("4321",  textMessageResponses.  ElementAt(0));
            Assert.AreEqual(1,       binaryMessageResponses.Count);
            Assert.AreEqual("DCBA",  binaryMessageResponses.ElementAt(0).ToUTF8String());

            #endregion


            try
            {

                await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                                                 "Done",
                                                 CancellationToken.None);

            }
            catch (Exception e)
            {

                // System.Net.WebSockets.WebSocketException
                // The remote party closed the WebSocket connection without completing the close handshake.

                Assert.Fail(e.Message);

            }


        }

        #endregion


        #region Test_BasicAuth_Optional()

        [Test]
        public async Task Test_BasicAuth_Optional()
        {

            #region Setup

            if (webSocketServer is null) {
                Assert.Fail("WebSocketServer is null!");
                return;
            }

            var validatedTCP            = new List<String>();
            var newTCPConnection        = new List<String>();
            var validatedWebSocket      = new List<String>();
            var newWebSocketConnection  = new List<String>();
            var httpRequests            = new List<HTTPRequest>();
            var httpResponses           = new List<HTTPResponse>();
            var messageRequests         = new List<WebSocketFrame>();
            var messageResponses        = new List<WebSocketFrame>();
            var textMessageRequests     = new List<String>();
            var textMessageResponses    = new List<String>();
            var binaryMessageRequests   = new List<Byte[]>();
            var binaryMessageResponses  = new List<Byte[]>();

            webSocketServer.OnValidateTCPConnection       += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedTCP.Add($"{validatedTCP.Count}: {connection.Client.RemoteEndPoint?.ToString() ?? "-"}");
                return true;
            };

            webSocketServer.OnNewTCPConnection            += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newTCPConnection.Add($"{newTCPConnection.Count}: {connection.RemoteSocket}");
            };

            webSocketServer.OnHTTPRequest                 += async (timestamp, server, httpRequest) => {
                httpRequests.Add(httpRequest);
            };

            webSocketServer.OnValidateWebSocketConnection += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedWebSocket.Add($"{validatedWebSocket.Count}: {connection.RemoteSocket}");
                return null;
            };

            webSocketServer.OnHTTPResponse                += async (timestamp, server, httpRequest, httpResponse) => {
                httpResponses.Add(httpResponse);
            };

            webSocketServer.OnNewWebSocketConnection      += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newWebSocketConnection.Add($"{newWebSocketConnection.Count}: {connection.RemoteSocket}");
            };

            webSocketServer.OnWebSocketFrameReceived      += async (timestamp, server, connection, eventTrackingId, requestFrame) => {
                messageRequests.       Add(requestFrame);
            };

            webSocketServer.OnWebSocketFrameSent          += async (timestamp, server, connection, eventTrackingId, responseFrame) => {
                messageResponses.      Add(responseFrame);
            };

            webSocketServer.OnTextMessageReceived         += async (timestamp, server, connection, eventTrackingId, textMessage) => {
                textMessageRequests.   Add(textMessage);
            };

            webSocketServer.OnTextMessageSent             += async (timestamp, server, connection, eventTrackingId, textMessage) => {
                textMessageResponses.  Add(textMessage ?? "-");
            };

            webSocketServer.OnBinaryMessageReceived       += async (timestamp, server, connection, eventTrackingId, binaryMessage) => {
                binaryMessageRequests. Add(binaryMessage);
            };

            webSocketServer.OnBinaryMessageSent           += async (timestamp, server, connection, eventTrackingId, binaryMessage) => {
                binaryMessageResponses.Add(binaryMessage);
            };

            #endregion


            var clientWebSocket = new ClientWebSocket();

            // Will only work, when the server sends back a "401 Unauthorized" and a "WWW-Authenticate" header for the first request!
            //clientWebSocket.Options.Credentials = new NetworkCredential("username", "password");

            clientWebSocket.Options.SetRequestHeader("Authorization", $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes("username:password"))}");

            try
            {
                await clientWebSocket.ConnectAsync(new Uri($"ws://127.0.0.1:{HTTPPort}"), CancellationToken.None);
            }
            catch (Exception e)
            {
                Assert.Fail(e.Message);
            }

            var state                = clientWebSocket.State;
            var closeStatus          = clientWebSocket.CloseStatus;
            var options              = clientWebSocket.Options;
            var subProtocol          = clientWebSocket.SubProtocol;
            var httpStatusCode       = clientWebSocket.HttpStatusCode;
            var httpResponseHeaders  = clientWebSocket.HttpResponseHeaders;


            #region Check HTTP request

            // Wait a bit, because running multiple tests at once has timing issues!
            while (newWebSocketConnection.Count == 0)
                Thread.Sleep(10);

            Assert.AreEqual(1, validatedTCP.          Count, validatedTCP.          AggregateWith(", "));
            Assert.AreEqual(1, newTCPConnection.      Count, newTCPConnection.      AggregateWith(", "));
            Assert.AreEqual(1, validatedWebSocket.    Count, validatedWebSocket.    AggregateWith(", "));
            Assert.AreEqual(1, newWebSocketConnection.Count, newWebSocketConnection.AggregateWith(", "));

            Assert.AreEqual(1, httpRequests.          Count);
            Assert.AreEqual(1, httpResponses.         Count);
            Assert.AreEqual(1, webSocketServer.WebSocketConnections.Count());


            var httpRequest          = httpRequests.First();
            var request              = httpRequest.EntirePDU;

            // GET / HTTP/1.1
            // Host:                     127.0.0.1:111
            // Authorization:            Basic dXNlcm5hbWU6cGFzc3dvcmQ=
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Key:        +LYHhVOGskWz/0bFFcK8dQ==
            // Sec-WebSocket-Version:    13
            // Sec-WebSocket-Protocol:   ocpp2.0

            Assert.AreEqual("Upgrade",                                         httpRequest.Connection);
            Assert.AreEqual("websocket",                                       httpRequest.Upgrade);
            Assert.IsTrue  (httpRequest.Authorization is HTTPBasicAuthentication, "Is not HTTP Basic Authentication!");
            Assert.AreEqual("username",                                       (httpRequest.Authorization as HTTPBasicAuthentication)?.Username);
            Assert.AreEqual("password",                                       (httpRequest.Authorization as HTTPBasicAuthentication)?.Password);

            Assert.IsTrue  (request.Contains("GET / HTTP/1.1"),                request);
            Assert.IsTrue  (request.Contains($"Host: 127.0.0.1:{HTTPPort}"),   request);
            Assert.IsTrue  (request.Contains($"Connection: Upgrade"),          request);
            Assert.IsTrue  (request.Contains($"Upgrade: websocket"),           request);
            Assert.IsTrue  (request.Contains($"Sec-WebSocket-Key:"),           request);
            Assert.IsTrue  (request.Contains($"Sec-WebSocket-Version:"),       request);

            #endregion

            #region Check HTTP response

            var httpResponse  = httpResponses.First();
            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 101 Switching Protocols
            // Date:                     Wed, 02 Aug 2023 19:33:53 GMT
            // Server:                   GraphDefined HTTP Web Socket Service v2.0
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Accept:     s9FvxhRowHKxS38G/sBt7gC5qec=
            // Sec-WebSocket-Protocol:   ocpp2.0
            // Sec-WebSocket-Version:    13

            Assert.IsTrue  (response.Contains("HTTP/1.1 101 Switching Protocols"),   response);

            Assert.AreEqual("GraphDefined HTTP Web Socket Service v2.0",             httpResponse.Server);
            Assert.AreEqual("Upgrade",                                               httpResponse.Connection);
            Assert.AreEqual("websocket",                                             httpResponse.Upgrade);

            #endregion


            #region Send messages

            await clientWebSocket.SendAsync(buffer:             new ArraySegment<Byte>(Encoding.UTF8.GetBytes("1234")),
                                            messageType:        WebSocketMessageType.Text,
                                            endOfMessage:       true,
                                            cancellationToken:  CancellationToken.None);

            while (textMessageResponses.  Count == 0)
                Thread.Sleep(10);

            await clientWebSocket.SendAsync(buffer:             new ArraySegment<Byte>(Encoding.UTF8.GetBytes("ABCD")),
                                            messageType:        WebSocketMessageType.Binary,
                                            endOfMessage:       true,
                                            cancellationToken:  CancellationToken.None);

            while (binaryMessageResponses.Count == 0)
                Thread.Sleep(10);

            #endregion

            #region Validate message delivery

            Assert.AreEqual(2,       messageRequests. Count);
            Assert.AreEqual("1234",  messageRequests. ElementAt(0).Payload.ToUTF8String());
            Assert.AreEqual("ABCD",  messageRequests. ElementAt(1).Payload.ToUTF8String());

            Assert.AreEqual(2,       messageResponses.Count);
            Assert.AreEqual("4321",  messageResponses.ElementAt(0).Payload.ToUTF8String());
            Assert.AreEqual("DCBA",  messageResponses.ElementAt(1).Payload.ToUTF8String());


            Assert.AreEqual(1,       textMessageRequests.   Count);
            Assert.AreEqual("1234",  textMessageRequests.   ElementAt(0));
            Assert.AreEqual(1,       binaryMessageRequests. Count);
            Assert.AreEqual("ABCD",  binaryMessageRequests. ElementAt(0).ToUTF8String());

            Assert.AreEqual(1,       textMessageResponses.  Count);
            Assert.AreEqual("4321",  textMessageResponses.  ElementAt(0));
            Assert.AreEqual(1,       binaryMessageResponses.Count);
            Assert.AreEqual("DCBA",  binaryMessageResponses.ElementAt(0).ToUTF8String());

            #endregion


            try
            {

                await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                                                 "Done",
                                                 CancellationToken.None);

            }
            catch (Exception e)
            {

                // System.Net.WebSockets.WebSocketException
                // The remote party closed the WebSocket connection without completing the close handshake.

                Assert.Fail(e.Message);

            }


        }

        #endregion

        #region Test_BasicAuth_Mandatory()

        [Test]
        public async Task Test_BasicAuth_Mandatory()
        {

            #region Setup

            if (webSocketServer is null) {
                Assert.Fail("WebSocketServer is null!");
                return;
            }

            var validatedTCP            = new List<String>();
            var newTCPConnection        = new List<String>();
            var validatedWebSocket      = new List<String>();
            var newWebSocketConnection  = new List<String>();
            var httpRequests            = new List<HTTPRequest>();
            var httpResponses           = new List<HTTPResponse>();
            var messageRequests         = new List<WebSocketFrame>();
            var messageResponses        = new List<WebSocketFrame>();
            var textMessageRequests     = new List<String>();
            var textMessageResponses    = new List<String>();
            var binaryMessageRequests   = new List<Byte[]>();
            var binaryMessageResponses  = new List<Byte[]>();

            webSocketServer.OnValidateTCPConnection       += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedTCP.Add($"{validatedTCP.Count}: {connection.Client.RemoteEndPoint?.ToString() ?? "-"}");
                return true;
            };

            webSocketServer.OnNewTCPConnection            += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newTCPConnection.Add($"{newTCPConnection.Count}: {connection.RemoteSocket}");
            };

            webSocketServer.OnHTTPRequest                 += async (timestamp, server, httpRequest) => {
                httpRequests.Add(httpRequest);
            };

            webSocketServer.OnValidateWebSocketConnection += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {

                if (connection.HTTPRequest is not null &&
                    connection.HTTPRequest.Authorization is HTTPBasicAuthentication httpBasicAuth &&
                    httpBasicAuth.Username == "username" &&
                    httpBasicAuth.Password == "password")
                {
                    validatedWebSocket.Add($"{validatedWebSocket.Count}: {connection.RemoteSocket}");
                    return null;
                }
                else
                {
                    return new HTTPResponse.Builder(connection.HTTPRequest) {
                               HTTPStatusCode   = HTTPStatusCode.Unauthorized,
                               WWWAuthenticate  = @"Basic realm=""Access to the web sockets server"", charset =""UTF-8""",
                               Connection       = "Close"
                           }.AsImmutable;
                }

            };

            webSocketServer.OnHTTPResponse                += async (timestamp, server, httpRequest, httpResponse) => {
                httpResponses.Add(httpResponse);
            };

            webSocketServer.OnNewWebSocketConnection      += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newWebSocketConnection.Add($"{newWebSocketConnection.Count}: {connection.RemoteSocket}");
            };

            webSocketServer.OnWebSocketFrameReceived      += async (timestamp, server, connection, eventTrackingId, requestFrame) => {
                messageRequests.       Add(requestFrame);
            };

            webSocketServer.OnWebSocketFrameSent          += async (timestamp, server, connection, eventTrackingId, responseFrame) => {
                messageResponses.      Add(responseFrame);
            };

            webSocketServer.OnTextMessageReceived         += async (timestamp, server, connection, eventTrackingId, textMessage) => {
                textMessageRequests.   Add(textMessage);
            };

            webSocketServer.OnTextMessageSent             += async (timestamp, server, connection, eventTrackingId, textMessage) => {
                textMessageResponses.  Add(textMessage ?? "-");
            };

            webSocketServer.OnBinaryMessageReceived       += async (timestamp, server, connection, eventTrackingId, binaryMessage) => {
                binaryMessageRequests. Add(binaryMessage);
            };

            webSocketServer.OnBinaryMessageSent           += async (timestamp, server, connection, eventTrackingId, binaryMessage) => {
                binaryMessageResponses.Add(binaryMessage);
            };

            #endregion


            var clientWebSocket = new ClientWebSocket();

            // Will only work, when the server sends back a "401 Unauthorized" and a "WWW-Authenticate" header for the first request!
            clientWebSocket.Options.Credentials = new NetworkCredential("username", "password");

            try
            {
                await clientWebSocket.ConnectAsync(new Uri($"ws://127.0.0.1:{HTTPPort}"), CancellationToken.None);
            }
            catch (Exception e)
            {
                Assert.AreEqual("The server returned status code '401' when status code '101' was expected.", e.Message);
            }

            var state                = clientWebSocket.State;
            var closeStatus          = clientWebSocket.CloseStatus;
            var options              = clientWebSocket.Options;
            var subProtocol          = clientWebSocket.SubProtocol;
            var httpStatusCode       = clientWebSocket.HttpStatusCode;
            var httpResponseHeaders  = clientWebSocket.HttpResponseHeaders;


            #region Check HTTP request

            // Wait a bit, because running multiple tests at once has timing issues!
            while (newWebSocketConnection.Count == 0)
                Thread.Sleep(10);

            // 2 because of the way .NET handles HTTP authentication!
            Assert.AreEqual(2, validatedTCP.          Count, validatedTCP.          AggregateWith(", "));
            Assert.AreEqual(2, newTCPConnection.      Count, newTCPConnection.      AggregateWith(", "));
            Assert.AreEqual(1, validatedWebSocket.    Count, validatedWebSocket.    AggregateWith(", "));
            Assert.AreEqual(2, newWebSocketConnection.Count, newWebSocketConnection.AggregateWith(", "));

            Assert.AreEqual(2, httpRequests.          Count);
            Assert.AreEqual(2, httpResponses.         Count);
            Assert.AreEqual(2, webSocketServer.WebSocketConnections.Count());


            var httpRequest1          = httpRequests.First();
            var request1              = httpRequest1.EntirePDU;

            // GET / HTTP/1.1
            // Host:                     127.0.0.1:111
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Key:        fPK+DduM8UCSqm1WUQ876w==
            // Sec-WebSocket-Version:    13
            // Sec-WebSocket-Protocol:   ocpp2.0



            var httpRequest2          = httpRequests.ElementAt(1);
            var request2              = httpRequest2.EntirePDU;

            // GET / HTTP/1.1
            // Host:                     127.0.0.1:111
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Key:        +LYHhVOGskWz/0bFFcK8dQ==
            // Sec-WebSocket-Version:    13
            // Sec-WebSocket-Protocol:   ocpp2.0
            // Authorization:            Basic dXNlcm5hbWU6cGFzc3dvcmQ=

            Assert.AreEqual("Upgrade",                                         httpRequest2.Connection);
            Assert.AreEqual("websocket",                                       httpRequest2.Upgrade);
            Assert.IsTrue  (httpRequest2.Authorization is HTTPBasicAuthentication, "Is not HTTP Basic Authentication!");
            Assert.AreEqual("username",                                       (httpRequest2.Authorization as HTTPBasicAuthentication)?.Username);
            Assert.AreEqual("password",                                       (httpRequest2.Authorization as HTTPBasicAuthentication)?.Password);

            Assert.IsTrue  (request2.Contains("GET / HTTP/1.1"),                request2);
            Assert.IsTrue  (request2.Contains($"Host: 127.0.0.1:{HTTPPort}"),   request2);
            Assert.IsTrue  (request2.Contains($"Connection: Upgrade"),          request2);
            Assert.IsTrue  (request2.Contains($"Upgrade: websocket"),           request2);
            Assert.IsTrue  (request2.Contains($"Sec-WebSocket-Key:"),           request2);
            Assert.IsTrue  (request2.Contains($"Sec-WebSocket-Version:"),       request2);

            #endregion

            #region Check HTTP response

            var httpResponse1  = httpResponses.First();
            var response1      = httpResponse1.EntirePDU;
            var httpBody1      = httpResponse1.HTTPBodyAsUTF8String;

            // HTTP/1.1 401 Unauthorized
            // Date:                     Thu, 03 Aug 2023 17:09:17 GMT
            // WWW-Authenticate:         Basic realm="Access to the web sockets server", charset ="UTF-8"
            // Connection:               Close



            var httpResponse2  = httpResponses.ElementAt(1);
            var response2      = httpResponse2.EntirePDU;
            var httpBody2      = httpResponse2.HTTPBodyAsUTF8String;

            // HTTP/1.1 101 Switching Protocols
            // Date:                     Thu, 03 Aug 2023 17:09:17 GMT
            // Server:                   GraphDefined HTTP Web Socket Service v2.0
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Accept:     s9FvxhRowHKxS38G/sBt7gC5qec=
            // Sec-WebSocket-Protocol:   ocpp2.0
            // Sec-WebSocket-Version:    13

            Assert.IsTrue  (response2.Contains("HTTP/1.1 101 Switching Protocols"),   response2);

            Assert.AreEqual("GraphDefined HTTP Web Socket Service v2.0",             httpResponse2.Server);
            Assert.AreEqual("Upgrade",                                               httpResponse2.Connection);
            Assert.AreEqual("websocket",                                             httpResponse2.Upgrade);

            #endregion


            #region Send messages

            await clientWebSocket.SendAsync(buffer:             new ArraySegment<Byte>(Encoding.UTF8.GetBytes("1234")),
                                            messageType:        WebSocketMessageType.Text,
                                            endOfMessage:       true,
                                            cancellationToken:  CancellationToken.None);

            while (textMessageResponses.  Count == 0)
                Thread.Sleep(10);

            await clientWebSocket.SendAsync(buffer:             new ArraySegment<Byte>(Encoding.UTF8.GetBytes("ABCD")),
                                            messageType:        WebSocketMessageType.Binary,
                                            endOfMessage:       true,
                                            cancellationToken:  CancellationToken.None);

            while (binaryMessageResponses.Count == 0)
                Thread.Sleep(10);

            #endregion

            #region Validate message delivery

            Assert.AreEqual(2,       messageRequests. Count);
            Assert.AreEqual("1234",  messageRequests. ElementAt(0).Payload.ToUTF8String());
            Assert.AreEqual("ABCD",  messageRequests. ElementAt(1).Payload.ToUTF8String());

            Assert.AreEqual(2,       messageResponses.Count);
            Assert.AreEqual("4321",  messageResponses.ElementAt(0).Payload.ToUTF8String());
            Assert.AreEqual("DCBA",  messageResponses.ElementAt(1).Payload.ToUTF8String());


            Assert.AreEqual(1,       textMessageRequests.   Count);
            Assert.AreEqual("1234",  textMessageRequests.   ElementAt(0));
            Assert.AreEqual(1,       binaryMessageRequests. Count);
            Assert.AreEqual("ABCD",  binaryMessageRequests. ElementAt(0).ToUTF8String());

            Assert.AreEqual(1,       textMessageResponses.  Count);
            Assert.AreEqual("4321",  textMessageResponses.  ElementAt(0));
            Assert.AreEqual(1,       binaryMessageResponses.Count);
            Assert.AreEqual("DCBA",  binaryMessageResponses.ElementAt(0).ToUTF8String());

            #endregion


            try
            {

                await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                                                 "Done",
                                                 CancellationToken.None);

            }
            catch (Exception e)
            {

                // System.Net.WebSockets.WebSocketException
                // The remote party closed the WebSocket connection without completing the close handshake.

                Assert.Fail(e.Message);

            }


        }

        #endregion

        #region Test_BasicAuth_Mandatory_Failed()

        [Test]
        public async Task Test_BasicAuth_Mandatory_Failed()
        {

            #region Setup

            if (webSocketServer is null) {
                Assert.Fail("WebSocketServer is null!");
                return;
            }

            var validatedTCP            = new List<String>();
            var newTCPConnection        = new List<String>();
            var validatedWebSocket      = new List<String>();
            var newWebSocketConnection  = new List<String>();
            var httpRequests            = new List<HTTPRequest>();
            var httpResponses           = new List<HTTPResponse>();
            var messageRequests         = new List<WebSocketFrame>();
            var messageResponses        = new List<WebSocketFrame>();
            var textMessageRequests     = new List<String>();
            var textMessageResponses    = new List<String>();
            var binaryMessageRequests   = new List<Byte[]>();
            var binaryMessageResponses  = new List<Byte[]>();

            webSocketServer.OnValidateTCPConnection       += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedTCP.Add($"{validatedTCP.Count}: {connection.Client.RemoteEndPoint?.ToString() ?? "-"}");
                return true;
            };

            webSocketServer.OnNewTCPConnection            += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newTCPConnection.Add($"{newTCPConnection.Count}: {connection.RemoteSocket}");
            };

            webSocketServer.OnHTTPRequest                 += async (timestamp, server, httpRequest) => {
                httpRequests.Add(httpRequest);
            };

            webSocketServer.OnValidateWebSocketConnection += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {

                if (connection.HTTPRequest is not null &&
                    connection.HTTPRequest.Authorization is HTTPBasicAuthentication httpBasicAuth &&
                    httpBasicAuth.Username == "username" &&
                    httpBasicAuth.Password == "password")
                {
                    validatedWebSocket.Add($"{validatedWebSocket.Count}: {connection.RemoteSocket}");
                    return null;
                }
                else
                {
                    return new HTTPResponse.Builder(connection.HTTPRequest) {
                               HTTPStatusCode   = HTTPStatusCode.Unauthorized,
                               Server           = "GraphDefined HTTP Web Socket Service v2.0",
                               WWWAuthenticate  = @"Basic realm=""Access to the web sockets server"", charset =""UTF-8""",
                               Connection       = "Close"
                           }.AsImmutable;
                }

            };

            webSocketServer.OnHTTPResponse                += async (timestamp, server, httpRequest, httpResponse) => {
                httpResponses.Add(httpResponse);
            };

            webSocketServer.OnNewWebSocketConnection      += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newWebSocketConnection.Add($"{newWebSocketConnection.Count}: {connection.RemoteSocket}");
            };

            webSocketServer.OnWebSocketFrameReceived      += async (timestamp, server, connection, eventTrackingId, requestFrame) => {
                messageRequests.       Add(requestFrame);
            };

            webSocketServer.OnWebSocketFrameSent          += async (timestamp, server, connection, eventTrackingId, responseFrame) => {
                messageResponses.      Add(responseFrame);
            };

            webSocketServer.OnTextMessageReceived         += async (timestamp, server, connection, eventTrackingId, textMessage) => {
                textMessageRequests.   Add(textMessage);
            };

            webSocketServer.OnTextMessageSent             += async (timestamp, server, connection, eventTrackingId, textMessage) => {
                textMessageResponses.  Add(textMessage ?? "-");
            };

            webSocketServer.OnBinaryMessageReceived       += async (timestamp, server, connection, eventTrackingId, binaryMessage) => {
                binaryMessageRequests. Add(binaryMessage);
            };

            webSocketServer.OnBinaryMessageSent           += async (timestamp, server, connection, eventTrackingId, binaryMessage) => {
                binaryMessageResponses.Add(binaryMessage);
            };

            #endregion


            var clientWebSocket = new ClientWebSocket();

            // Will only work, when the server sends back a "401 Unauthorized" and a "WWW-Authenticate" header for the first request!
            clientWebSocket.Options.Credentials = new NetworkCredential("nameOfUser", "passphrase");

            try
            {
                await clientWebSocket.ConnectAsync(new Uri($"ws://127.0.0.1:{HTTPPort}"), CancellationToken.None);
            }
            catch (Exception e)
            {
                Assert.AreEqual("The server returned status code '401' when status code '101' was expected.", e.Message);
            }

            var state                = clientWebSocket.State;
            var closeStatus          = clientWebSocket.CloseStatus;
            var options              = clientWebSocket.Options;
            var subProtocol          = clientWebSocket.SubProtocol;
            var httpStatusCode       = clientWebSocket.HttpStatusCode;
            var httpResponseHeaders  = clientWebSocket.HttpResponseHeaders;


            #region Check HTTP request

            // Wait a bit, because running multiple tests at once has timing issues!
            while (newWebSocketConnection.Count == 0)
                Thread.Sleep(10);

            // 2 because of the way .NET handles HTTP authentication!
            Assert.AreEqual(2, validatedTCP.          Count, validatedTCP.          AggregateWith(", "));
            Assert.AreEqual(2, newTCPConnection.      Count, newTCPConnection.      AggregateWith(", "));
            Assert.AreEqual(0, validatedWebSocket.    Count, validatedWebSocket.    AggregateWith(", "));
            Assert.AreEqual(2, newWebSocketConnection.Count, newWebSocketConnection.AggregateWith(", "));

            Assert.AreEqual(2, httpRequests.          Count);
            Assert.AreEqual(2, httpResponses.         Count);
            Assert.AreEqual(2, webSocketServer.WebSocketConnections.Count());


            var httpRequest1          = httpRequests.First();
            var request1              = httpRequest1.EntirePDU;

            // GET / HTTP/1.1
            // Host:                     127.0.0.1:111
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Key:        fPK+DduM8UCSqm1WUQ876w==
            // Sec-WebSocket-Version:    13



            var httpRequest2          = httpRequests.ElementAt(1);
            var request2              = httpRequest2.EntirePDU;

            // GET / HTTP/1.1
            // Host:                    127.0.0.1:111
            // Connection:              Upgrade
            // Upgrade:                 websocket
            // Sec-WebSocket-Key:       kfHFF9xcPEeUwywOtd2+VA==
            // Sec-WebSocket-Version:   13
            // Authorization:           Basic bmFtZU9mVXNlcjpwYXNzcGhyYXNl

            Assert.AreEqual("Upgrade",                                         httpRequest2.Connection);
            Assert.AreEqual("websocket",                                       httpRequest2.Upgrade);
            Assert.IsTrue  (httpRequest2.Authorization is HTTPBasicAuthentication, "Is not HTTP Basic Authentication!");
            Assert.AreEqual("nameOfUser",                                     (httpRequest2.Authorization as HTTPBasicAuthentication)?.Username);
            Assert.AreEqual("passphrase",                                     (httpRequest2.Authorization as HTTPBasicAuthentication)?.Password);

            Assert.IsTrue  (request2.Contains("GET / HTTP/1.1"),                                       request2);
            Assert.IsTrue  (request2.Contains($"Host: 127.0.0.1:{HTTPPort}"),                          request2);
            Assert.IsTrue  (request2.Contains($"Connection: Upgrade"),                                 request2);
            Assert.IsTrue  (request2.Contains($"Upgrade: websocket"),                                  request2);
            Assert.IsTrue  (request2.Contains($"Sec-WebSocket-Key:"),                                  request2);
            Assert.IsTrue  (request2.Contains($"Sec-WebSocket-Version:"),                              request2);
            Assert.IsTrue  (request2.Contains($"Authorization: Basic bmFtZU9mVXNlcjpwYXNzcGhyYXNl"),   request2);

            #endregion

            #region Check HTTP response

            var httpResponse1  = httpResponses.First();
            var response1      = httpResponse1.EntirePDU;
            var httpBody1      = httpResponse1.HTTPBodyAsUTF8String;

            // HTTP/1.1 401 Unauthorized
            // Date:               Thu, 03 Aug 2023 22:38:42 GMT
            // WWW-Authenticate:   Basic realm="Access to the web sockets server", charset ="UTF-8"
            // Connection:         Close



            var httpResponse2  = httpResponses.ElementAt(1);
            var response2      = httpResponse2.EntirePDU;
            var httpBody2      = httpResponse2.HTTPBodyAsUTF8String;

            // HTTP/1.1 401 Unauthorized
            // Date:               Thu, 03 Aug 2023 22:38:43 GMT
            // WWW-Authenticate:   Basic realm="Access to the web sockets server", charset ="UTF-8"
            // Connection:         Close

            Assert.IsTrue  (response2.Contains("HTTP/1.1 401 Unauthorized"),   response2);

            Assert.AreEqual("GraphDefined HTTP Web Socket Service v2.0",                              httpResponse2.Server);
            Assert.AreEqual("Basic realm=\"Access to the web sockets server\", charset =\"UTF-8\"",   httpResponse2.WWWAuthenticate);
            Assert.AreEqual("Close",                                                                  httpResponse2.Connection);

            #endregion

        }

        #endregion








        #region Test_ManyClients()

        [Test]
        public async Task Test_ManyClients()
        {

            // Note: Your operating system or firewall might not allow you to open
            //       or accept 100+ TCP connections within a short time span!
            //       Also the task scheduling might slow down the test!
            var numberOfClients         = 100;

            #region Setup

            if (webSocketServer is null) {
                Assert.Fail("WebSocketServer is null!");
                return;
            }

            var validatedTCP            = new List<String>();
            var newTCPConnection        = new List<String>();
            var validatedWebSocket      = new List<String>();
            var newWebSocketConnection  = new List<String>();
            var httpRequests            = new List<HTTPRequest>();
            var httpResponses           = new List<HTTPResponse>();
            var messageRequests         = new List<WebSocketFrame>();
            var messageResponses        = new List<WebSocketFrame>();
            var textMessageRequests     = new List<String>();
            var textMessageResponses    = new List<String>();
            var binaryMessageRequests   = new List<Byte[]>();
            var binaryMessageResponses  = new List<Byte[]>();

            webSocketServer.OnValidateTCPConnection       += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedTCP.Add($"{validatedTCP.Count}: {connection.Client.RemoteEndPoint?.ToString() ?? "-"}");
                return true;
            };

            webSocketServer.OnNewTCPConnection            += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newTCPConnection.Add($"{newTCPConnection.Count}: {connection.RemoteSocket}");
            };

            webSocketServer.OnHTTPRequest                 += async (timestamp, server, httpRequest) => {
                httpRequests.Add(httpRequest);
            };

            webSocketServer.OnValidateWebSocketConnection += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedWebSocket.Add($"{validatedWebSocket.Count}: {connection.RemoteSocket}");
                return null;
            };

            webSocketServer.OnHTTPResponse                += async (timestamp, server, httpRequest, httpResponse) => {
                httpResponses.Add(httpResponse);
            };

            webSocketServer.OnNewWebSocketConnection      += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newWebSocketConnection.Add($"{newWebSocketConnection.Count}: {connection.RemoteSocket}");
            };

            webSocketServer.OnWebSocketFrameReceived      += async (timestamp, server, connection, eventTrackingId, requestFrame) => {
                messageRequests.       Add(requestFrame);
            };

            webSocketServer.OnWebSocketFrameSent          += async (timestamp, server, connection, eventTrackingId, responseFrame) => {
                messageResponses.      Add(responseFrame);
            };

            webSocketServer.OnTextMessageReceived         += async (timestamp, server, connection, eventTrackingId, textMessage) => {
                textMessageRequests.   Add(textMessage);
            };

            webSocketServer.OnTextMessageSent             += async (timestamp, server, connection, eventTrackingId, textMessage) => {
                textMessageResponses.  Add(textMessage ?? "-");
            };

            webSocketServer.OnBinaryMessageReceived       += async (timestamp, server, connection, eventTrackingId, binaryMessage) => {
                binaryMessageRequests. Add(binaryMessage);
            };

            webSocketServer.OnBinaryMessageSent           += async (timestamp, server, connection, eventTrackingId, binaryMessage) => {
                binaryMessageResponses.Add(binaryMessage);
            };

            #endregion


            var startTimestamp          = Timestamp.Now;
            var clientWebSockets        = new List<ClientWebSocket>();
            var exceptions              = new List<Exception>();

            for (var i = 1; i <= numberOfClients; i++)
            {

                var clientWebSocket = new ClientWebSocket();

                try
                {

                    await clientWebSocket.ConnectAsync(new Uri($"ws://127.0.0.1:{HTTPPort}"),
                                                       CancellationToken.None);

                    clientWebSockets.Add(clientWebSocket);

                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }

            }

            var runTime1 = Timestamp.Now - startTimestamp;


            #region Check HTTP request

            // Wait a bit, because running multiple tests at once has timing issues!
            while (newWebSocketConnection.Count < numberOfClients)
                Thread.Sleep(10);

            Assert.AreEqual(numberOfClients, validatedTCP.          Count, validatedTCP.          AggregateWith(", "));
            Assert.AreEqual(numberOfClients, newTCPConnection.      Count, newTCPConnection.      AggregateWith(", "));
            Assert.AreEqual(numberOfClients, validatedWebSocket.    Count, validatedWebSocket.    AggregateWith(", "));
            Assert.AreEqual(numberOfClients, newWebSocketConnection.Count, newWebSocketConnection.AggregateWith(", "));

            Assert.AreEqual(numberOfClients, httpRequests.          Count);
            Assert.AreEqual(numberOfClients, httpResponses.         Count);
            Assert.AreEqual(numberOfClients, webSocketServer.WebSocketConnections.Count());


            //var httpRequest          = httpRequests.First();
            //var request              = httpRequest.EntirePDU;

            //// GET / HTTP/1.1
            //// Host:                    127.0.0.1:111
            //// Connection:              Upgrade
            //// Upgrade:                 websocket
            //// Sec-WebSocket-Key:       +LYHhVOGskWz/0bFFcK8dQ==
            //// Sec-WebSocket-Version:   13

            //Assert.AreEqual("Upgrade",                                         httpRequest.Connection);
            //Assert.AreEqual("websocket",                                       httpRequest.Upgrade);

            //Assert.IsTrue  (request.Contains("GET / HTTP/1.1"),                request);
            //Assert.IsTrue  (request.Contains($"Host: 127.0.0.1:{HTTPPort}"),   request);
            //Assert.IsTrue  (request.Contains($"Connection: Upgrade"),          request);
            //Assert.IsTrue  (request.Contains($"Upgrade: websocket"),           request);
            //Assert.IsTrue  (request.Contains($"Sec-WebSocket-Key:"),           request);
            //Assert.IsTrue  (request.Contains($"Sec-WebSocket-Version:"),       request);

            #endregion

            #region Check HTTP response

            //var httpResponse  = httpResponses.First();
            //var response      = httpResponse.EntirePDU;
            //var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            //// HTTP/1.1 101 Switching Protocols
            //// Date:                     Wed, 02 Aug 2023 19:33:53 GMT
            //// Server:                   GraphDefined HTTP Web Socket Service v2.0
            //// Connection:               Upgrade
            //// Upgrade:                  websocket
            //// Sec-WebSocket-Accept:     s9FvxhRowHKxS38G/sBt7gC5qec=
            //// Sec-WebSocket-Version:    13

            //Assert.IsTrue  (response.Contains("HTTP/1.1 101 Switching Protocols"),   response);

            //Assert.AreEqual("GraphDefined HTTP Web Socket Service v2.0",             httpResponse.Server);
            //Assert.AreEqual("Upgrade",                                               httpResponse.Connection);
            //Assert.AreEqual("websocket",                                             httpResponse.Upgrade);

            #endregion


            #region Send messages

            //await clientWebSocket.SendAsync(buffer:             new ArraySegment<Byte>(Encoding.UTF8.GetBytes("1234")),
            //                                messageType:        WebSocketMessageType.Text,
            //                                endOfMessage:       true,
            //                                cancellationToken:  CancellationToken.None);

            //while (textMessageResponses.  Count == 0)
            //    Thread.Sleep(10);

            //await clientWebSocket.SendAsync(buffer:             new ArraySegment<Byte>(Encoding.UTF8.GetBytes("ABCD")),
            //                                messageType:        WebSocketMessageType.Binary,
            //                                endOfMessage:       true,
            //                                cancellationToken:  CancellationToken.None);

            //while (binaryMessageResponses.Count == 0)
            //    Thread.Sleep(10);

            #endregion

            #region Validate message delivery

            //Assert.AreEqual(2,       messageRequests. Count);
            //Assert.AreEqual("1234",  messageRequests. ElementAt(0).Payload.ToUTF8String());
            //Assert.AreEqual("ABCD",  messageRequests. ElementAt(1).Payload.ToUTF8String());

            //Assert.AreEqual(2,       messageResponses.Count);
            //Assert.AreEqual("4321",  messageResponses.ElementAt(0).Payload.ToUTF8String());
            //Assert.AreEqual("DCBA",  messageResponses.ElementAt(1).Payload.ToUTF8String());


            //Assert.AreEqual(1,       textMessageRequests.   Count);
            //Assert.AreEqual("1234",  textMessageRequests.   ElementAt(0));
            //Assert.AreEqual(1,       binaryMessageRequests. Count);
            //Assert.AreEqual("ABCD",  binaryMessageRequests. ElementAt(0).ToUTF8String());

            //Assert.AreEqual(1,       textMessageResponses.  Count);
            //Assert.AreEqual("4321",  textMessageResponses.  ElementAt(0));
            //Assert.AreEqual(1,       binaryMessageResponses.Count);
            //Assert.AreEqual("DCBA",  binaryMessageResponses.ElementAt(0).ToUTF8String());

            #endregion


            startTimestamp  = Timestamp.Now;
            var exceptions2 = new List<Exception>();

            foreach (var clientWebSocket in clientWebSockets)
            {

                try
                {

                    await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                                                     "Done",
                                                     CancellationToken.None);

                }
                catch (Exception e)
                {
                    exceptions2.Add(e);
                }

            }

            var runTime2 = Timestamp.Now - startTimestamp;

            Assert.AreEqual(0, exceptions2.Count, $"{exceptions2.Count} HTTP web socket closing exceptions!");


        }

        #endregion


    }

}
