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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// Tests between Hermod WebSocket clients and Hermod WebSocket servers.
    /// </summary>
    [TestFixture]
    public class WebSocketClientTests : AWebSocketServerTests
    {

        #region Constructor(s)

        public WebSocketClientTests()
            : base(IPPort.Parse(101))
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


            #region Client setup and connect

            var webSocketClient  = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{HTTPPort}"));

            #region OnTextMessageReceived

            var textMessageLog   = new List<String>();

            webSocketClient.OnTextMessageReceived += (timestamp,
                                                      webSocketClient,
                                                      webSocketClientConnection,
                                                      webSocketFrame,
                                                      eventTrackingId,
                                                      textMessage) => {

                textMessageLog.Add(textMessage);

                return Task.CompletedTask;

            };

            #endregion

            #region OnBinaryMessageReceived

            var binaryMessageLog   = new List<Byte[]>();

            webSocketClient.OnBinaryMessageReceived += (timestamp,
                                                        webSocketClient,
                                                        webSocketClientConnection,
                                                        webSocketFrame,
                                                        eventTrackingId,
                                                        binaryMessage) => {

                binaryMessageLog.Add(binaryMessage);

                return Task.CompletedTask;

            };

            #endregion

            var httpResponse     = await webSocketClient.Connect();

            #endregion

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


            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host:                     127.0.0.1:101
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Key:        /vkroMJ5bwBVW200riZKRg==
            // Sec-WebSocket-Version:    13

            // HTTP requests should not have a "Date"-header!
            Assert.IsFalse (request.Contains("Date:"),                         request);
            Assert.IsTrue  (request.Contains("GET / HTTP/1.1"),                request);
            Assert.IsTrue  (request.Contains($"Host: 127.0.0.1:{HTTPPort}"),   request);
            Assert.IsTrue  (request.Contains($"Upgrade: websocket"),           request);

            #endregion

            #region Check HTTP response

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

            await webSocketClient.SendText("1234");

            while (textMessageLog.Count == 0)
                Thread.Sleep(10);

            await webSocketClient.SendBinary("ABCD".ToUTF8Bytes());

            while (binaryMessageLog.Count == 0)
                Thread.Sleep(10);

            #endregion

            #region Validate message delivery

            //Note: If you are debugging too slowly HTTP Web Socket PING/PONG messages will arrive!

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


            Assert.AreEqual(1,       textMessageLog.        Count);
            Assert.AreEqual("4321",  textMessageLog.        ElementAt(0));
            Assert.AreEqual(1,       binaryMessageLog.      Count);
            Assert.AreEqual("DCBA",  binaryMessageLog.      ElementAt(0));

            #endregion


            await webSocketClient.Close();

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


            var webSocketClient  = new WebSocketClient(
                                       URL.Parse($"ws://127.0.0.1:{HTTPPort}"),
                                       SecWebSocketProtocols: new[] { "ocpp1.6" }
                                   );

            var httpResponse     = await webSocketClient.Connect();


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


            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host:                     127.0.0.1:101
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Key:        /vkroMJ5bwBVW200riZKRg==
            // Sec-WebSocket-Protocol:   ocpp1.6
            // Sec-WebSocket-Version:    13

            // HTTP requests should not have a "Date"-header!
            Assert.IsFalse (request.Contains("Date:"),                              request);
            Assert.IsTrue  (request.Contains("GET / HTTP/1.1"),                     request);
            Assert.IsTrue  (request.Contains($"Host: 127.0.0.1:{HTTPPort}"),        request);
            Assert.IsTrue  (request.Contains($"Upgrade: websocket"),                request);
            Assert.IsTrue  (request.Contains($"Sec-WebSocket-Protocol: ocpp1.6"),   request);

            #endregion

            #region Check HTTP response

            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 101 Switching Protocols
            // Date:                     Thu, 03 Aug 2023 21:53:54 GMT
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

            await webSocketClient.SendText("1234");

            while (textMessageResponses.Count == 0)
                Thread.Sleep(10);

            await webSocketClient.SendBinary("ABCD".ToUTF8Bytes());

            while (binaryMessageResponses.Count == 0)
                Thread.Sleep(10);

            #endregion

            #region Validate message delivery

            //Note: If you are debugging too slowly HTTP Web Socket PING/PONG messages will arrive!

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


            await webSocketClient.Close();

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


            var webSocketClient  = new WebSocketClient(
                                       URL.Parse($"ws://127.0.0.1:{HTTPPort}"),
                                       SecWebSocketProtocols: new[] { "ocpp1.6" }
                                   );

            var httpResponse     = await webSocketClient.Connect();


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


            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host:                     127.0.0.1:101
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Key:        /vkroMJ5bwBVW200riZKRg==
            // Sec-WebSocket-Protocol:   ocpp1.6
            // Sec-WebSocket-Version:    13

            // HTTP requests should not have a "Date"-header!
            Assert.IsFalse (request.Contains("Date:"),                              request);
            Assert.IsTrue  (request.Contains("GET / HTTP/1.1"),                     request);
            Assert.IsTrue  (request.Contains($"Host: 127.0.0.1:{HTTPPort}"),        request);
            Assert.IsTrue  (request.Contains($"Upgrade: websocket"),                request);
            Assert.IsTrue  (request.Contains($"Sec-WebSocket-Protocol: ocpp1.6"),   request);

            #endregion

            #region Check HTTP response

            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 101 Switching Protocols
            // Date:                     Thu, 03 Aug 2023 22:08:09 GMT
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
            Assert.AreEqual("ocpp1.6",                                               httpResponse.SecWebSocketProtocol.First());

            #endregion


            #region Send messages

            await webSocketClient.SendText("1234");

            while (textMessageResponses.Count == 0)
                Thread.Sleep(10);

            await webSocketClient.SendBinary("ABCD".ToUTF8Bytes());

            while (binaryMessageResponses.Count == 0)
                Thread.Sleep(10);

            #endregion

            #region Validate message delivery

            //Note: If you are debugging too slowly HTTP Web Socket PING/PONG messages will arrive!

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


            await webSocketClient.Close();

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


            var webSocketClient  = new WebSocketClient(
                                       URL.Parse($"ws://127.0.0.1:{HTTPPort}"),
                                       SecWebSocketProtocols: new[] { "ocpp1.6" }
                                   );

            var httpResponse     = await webSocketClient.Connect();


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


            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host:                     127.0.0.1:101
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Key:        /vkroMJ5bwBVW200riZKRg==
            // Sec-WebSocket-Protocol:   ocpp1.6
            // Sec-WebSocket-Version:    13

            // HTTP requests should not have a "Date"-header!
            Assert.IsFalse (request.Contains("Date:"),                              request);
            Assert.IsTrue  (request.Contains("GET / HTTP/1.1"),                     request);
            Assert.IsTrue  (request.Contains($"Host: 127.0.0.1:{HTTPPort}"),        request);
            Assert.IsTrue  (request.Contains($"Upgrade: websocket"),                request);
            Assert.IsTrue  (request.Contains($"Sec-WebSocket-Protocol: ocpp1.6"),   request);

            #endregion

            #region Check HTTP response

            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 101 Switching Protocols
            // Date:                     Thu, 03 Aug 2023 22:08:09 GMT
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
            Assert.AreEqual("ocpp1.6",                                               httpResponse.SecWebSocketProtocol.First());

            #endregion


            #region Send messages

            await webSocketClient.SendText("1234");

            while (textMessageResponses.Count == 0)
                Thread.Sleep(10);

            await webSocketClient.SendBinary("ABCD".ToUTF8Bytes());

            while (binaryMessageResponses.Count == 0)
                Thread.Sleep(10);

            #endregion

            #region Validate message delivery

            //Note: If you are debugging too slowly HTTP Web Socket PING/PONG messages will arrive!

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


            await webSocketClient.Close();

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


            var webSocketClient  = new WebSocketClient(
                                       URL.Parse($"ws://127.0.0.1:{HTTPPort}"),
                                       HTTPAuthentication:  HTTPBasicAuthentication.Create("username", "password")
                                   );

            var httpResponse     = await webSocketClient.Connect();


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


            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host:                     127.0.0.1:101
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Key:        /vkroMJ5bwBVW200riZKRg==
            // Sec-WebSocket-Protocol:   ocpp1.6
            // Sec-WebSocket-Version:    13
            // Authorization:            Basic dXNlcm5hbWU6cGFzc3dvcmQ=

            // HTTP requests should not have a "Date"-header!
            Assert.IsFalse (request.Contains("Date:"),                                            request);
            Assert.IsTrue  (request.Contains("GET / HTTP/1.1"),                                   request);
            Assert.IsTrue  (request.Contains($"Host: 127.0.0.1:{HTTPPort}"),                      request);
            Assert.IsTrue  (request.Contains($"Upgrade: websocket"),                              request);
            Assert.IsTrue  (request.Contains($"Authorization: Basic dXNlcm5hbWU6cGFzc3dvcmQ="),   request);

            #endregion

            #region Check HTTP response

            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 101 Switching Protocols
            // Date:                     Thu, 03 Aug 2023 22:29:09 GMT
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

            await webSocketClient.SendText("1234");

            while (textMessageResponses.Count == 0)
                Thread.Sleep(10);

            await webSocketClient.SendBinary("ABCD".ToUTF8Bytes());

            while (binaryMessageResponses.Count == 0)
                Thread.Sleep(10);

            #endregion

            #region Validate message delivery

            //Note: If you are debugging too slowly HTTP Web Socket PING/PONG messages will arrive!

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


            await webSocketClient.Close();

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


            var webSocketClient  = new WebSocketClient(
                                       URL.Parse($"ws://127.0.0.1:{HTTPPort}"),
                                       HTTPAuthentication:  HTTPBasicAuthentication.Create("username", "password")
                                   );

            var httpResponse     = await webSocketClient.Connect();


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


            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host:                     127.0.0.1:101
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Key:        /vkroMJ5bwBVW200riZKRg==
            // Sec-WebSocket-Protocol:   ocpp1.6
            // Sec-WebSocket-Version:    13
            // Authorization:            Basic dXNlcm5hbWU6cGFzc3dvcmQ=

            // HTTP requests should not have a "Date"-header!
            Assert.IsFalse (request.Contains("Date:"),                                            request);
            Assert.IsTrue  (request.Contains("GET / HTTP/1.1"),                                   request);
            Assert.IsTrue  (request.Contains($"Host: 127.0.0.1:{HTTPPort}"),                      request);
            Assert.IsTrue  (request.Contains($"Upgrade: websocket"),                              request);
            Assert.IsTrue  (request.Contains($"Authorization: Basic dXNlcm5hbWU6cGFzc3dvcmQ="),   request);

            #endregion

            #region Check HTTP response

            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 101 Switching Protocols
            // Date:                     Thu, 03 Aug 2023 22:29:09 GMT
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

            await webSocketClient.SendText("1234");

            while (textMessageResponses.Count == 0)
                Thread.Sleep(10);

            await webSocketClient.SendBinary("ABCD".ToUTF8Bytes());

            while (binaryMessageResponses.Count == 0)
                Thread.Sleep(10);

            #endregion

            #region Validate message delivery

            //Note: If you are debugging too slowly HTTP Web Socket PING/PONG messages will arrive!

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


            await webSocketClient.Close();

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


            var webSocketClient  = new WebSocketClient(
                                       URL.Parse($"ws://127.0.0.1:{HTTPPort}"),
                                       HTTPAuthentication:  HTTPBasicAuthentication.Create("nameOfUser", "passphrase")
                                   );

            var httpResponse     = await webSocketClient.Connect();


            #region Check HTTP request

            // Wait a bit, because running multiple tests at once has timing issues!
            while (newWebSocketConnection.Count == 0)
                Thread.Sleep(10);

            Assert.AreEqual(1, validatedTCP.          Count, validatedTCP.          AggregateWith(", "));
            Assert.AreEqual(1, newTCPConnection.      Count, newTCPConnection.      AggregateWith(", "));
            Assert.AreEqual(0, validatedWebSocket.    Count, validatedWebSocket.    AggregateWith(", "));
            Assert.AreEqual(1, newWebSocketConnection.Count, newWebSocketConnection.AggregateWith(", "));

            Assert.AreEqual(1, httpRequests.          Count);
            Assert.AreEqual(1, httpResponses.         Count);
            Assert.AreEqual(1, webSocketServer.WebSocketConnections.Count());


            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host:                    127.0.0.1:101
            // Connection:              Upgrade
            // Upgrade:                 websocket
            // Sec-WebSocket-Key:       /vkroMJ5bwBVW200riZKRg==
            // Sec-WebSocket-Version:   13
            // Authorization:           Basic bmFtZU9mVXNlcjpwYXNzcGhyYXNl

            // HTTP requests should not have a "Date"-header!
            Assert.IsFalse (request.Contains("Date:"),                                                request);
            Assert.IsTrue  (request.Contains("GET / HTTP/1.1"),                                       request);
            Assert.IsTrue  (request.Contains($"Host: 127.0.0.1:{HTTPPort}"),                          request);
            Assert.IsTrue  (request.Contains($"Upgrade: websocket"),                                  request);
            Assert.IsTrue  (request.Contains($"Authorization: Basic bmFtZU9mVXNlcjpwYXNzcGhyYXNl"),   request);

            #endregion

            #region Check HTTP response

            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 401 Unauthorized
            // Date:               Thu, 03 Aug 2023 23:01:56 GMT
            // Server:             GraphDefined HTTP Web Socket Service v2.0
            // WWW-Authenticate:   Basic realm="Access to the web sockets server", charset ="UTF-8"
            // Connection:         Close

            Assert.IsTrue  (response.Contains("HTTP/1.1 401 Unauthorized"),                           response);

            Assert.AreEqual("GraphDefined HTTP Web Socket Service v2.0",                              httpResponse.Server);
            Assert.AreEqual("Basic realm=\"Access to the web sockets server\", charset =\"UTF-8\"",   httpResponse.WWWAuthenticate);
            Assert.AreEqual("Close",                                                                  httpResponse.Connection);

            #endregion


            await webSocketClient.Close();

        }

        #endregion


    }

}
