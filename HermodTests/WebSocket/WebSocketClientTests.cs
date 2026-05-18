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
            : base(IPPort.Parse(1101))
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

            webSocketServer.OnValidateTCPConnection       += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedTCP.Add($"{validatedTCP.Count}: {connection.Client.RemoteEndPoint?.ToString() ?? "-"}");
                return Task.FromResult(ConnectionFilterResponse.Accepted());
            };

            webSocketServer.OnNewTCPConnection            += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newTCPConnection.Add($"{newTCPConnection.Count}: {connection.RemoteSocket}");
                return Task.CompletedTask;
            };

            webSocketServer.OnHTTPRequest                 += (timestamp, server, httpRequest, cancellationToken) => {
                httpRequests.Add(httpRequest);
                return Task.CompletedTask;
            };

            webSocketServer.OnValidateWebSocketConnection += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedWebSocket.Add($"{validatedWebSocket.Count}: {connection.RemoteSocket}");
                return Task.FromResult<HTTPResponse?>(null);
            };

            webSocketServer.OnHTTPResponse                += (timestamp, server, httpRequest, httpResponse, cancellationToken) => {
                httpResponses.Add(httpResponse);
                return Task.CompletedTask;
            };

            webSocketServer.OnNewWebSocketConnection      += (timestamp, server, connection, eventTrackingId, sharedSubprotocols, selectedSubprotocol, cancellationToken) => {
                newWebSocketConnection.Add($"{newWebSocketConnection.Count}: {connection.RemoteSocket}");
                return Task.CompletedTask;
            };

            webSocketServer.OnWebSocketFrameReceived      += (timestamp, server, connection, eventTrackingId, requestFrame, cancellationToken) => {
                messageRequests.       Add(requestFrame);
                return Task.CompletedTask;
            };

            webSocketServer.OnWebSocketFrameSent          += (timestamp, server, connection, eventTrackingId, responseFrame, cancellationToken) => {
                messageResponses.      Add(responseFrame);
                return Task.CompletedTask;
            };

            webSocketServer.OnTextMessageReceived         += (timestamp, server, connection, frame, eventTrackingId, textMessage, cancellationToken) => {
                textMessageRequests.   Add(textMessage);
                return Task.CompletedTask;
            };

            webSocketServer.OnTextMessageSent             += (timestamp, server, connection, frame, eventTrackingId, textMessage, sentStatus, cancellationToken) => {
                textMessageResponses.  Add(textMessage ?? "-");
                return Task.CompletedTask;
            };

            webSocketServer.OnBinaryMessageReceived       += (timestamp, server, connection, frame, eventTrackingId, binaryMessage, cancellationToken) => {
                binaryMessageRequests. Add(binaryMessage);
                return Task.CompletedTask;
            };

            webSocketServer.OnBinaryMessageSent           += (timestamp, server, connection, frame, eventTrackingId, binaryMessage, sentStatus, cancellationToken) => {
                binaryMessageResponses.Add(binaryMessage);
                return Task.CompletedTask;
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
                                                      textMessage,
                                                      cancellationToken) => {

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
                                                        binaryMessage,
                                                        cancellationToken) => {

                binaryMessageLog.Add(binaryMessage);

                return Task.CompletedTask;

            };

            #endregion

            var response1     = await webSocketClient.Connect();
            var httpResponse  = response1.Item2;

            #endregion

            #region Check HTTP request

            // Wait a bit, because running multiple tests at once has timing issues!
            while (newWebSocketConnection.Count == 0)
                Thread.Sleep(10);

            Assert.That(validatedTCP.          Count, Is.EqualTo(1), validatedTCP.          AggregateCSV());
            Assert.That(newTCPConnection.      Count, Is.EqualTo(1), newTCPConnection.      AggregateCSV());
            Assert.That(validatedWebSocket.    Count, Is.EqualTo(1), validatedWebSocket.    AggregateCSV());
            Assert.That(newWebSocketConnection.Count, Is.EqualTo(1), newWebSocketConnection.AggregateCSV());

            Assert.That(httpRequests.          Count, Is.EqualTo(1));
            Assert.That(httpResponses.         Count, Is.EqualTo(1));
            Assert.That(webSocketServer.WebSocketConnections.Count(), Is.EqualTo(1));


            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host:                     127.0.0.1:101
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Key:        /vkroMJ5bwBVW200riZKRg==
            // Sec-WebSocket-Version:    13

            // HTTP requests should not have a "Date"-header!
            Assert.That(request.Contains("Date:"), Is.False, request);
            Assert.That(request.Contains("GET / HTTP/1.1"), Is.True, request);
            Assert.That(request.Contains($"Host: 127.0.0.1:{HTTPPort}"), Is.True, request);
            Assert.That(request.Contains($"Upgrade: websocket"), Is.True, request);

            #endregion

            #region Check HTTP response

            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 101 Switching Protocols
            // Date:                     Wed, 02 Aug 2023 19:33:53 GMT
            // Server:                   GraphDefined HTTP WebSocket Service v2.0
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Accept:     s9FvxhRowHKxS38G/sBt7gC5qec=
            // Sec-WebSocket-Version:    13

            Assert.That(response.Contains("HTTP/1.1 101 Switching Protocols"), Is.True, response);

            Assert.That(httpResponse.Server, Is.EqualTo("GraphDefined HTTP WebSocket Service v2.0"));
            Assert.That(httpResponse.Connection, Is.EqualTo(ConnectionType.Upgrade));
            Assert.That(httpResponse.Upgrade, Is.EqualTo("websocket"));

            #endregion


            #region Send messages

            await webSocketClient.SendTextMessage("1234");

            while (textMessageLog.Count == 0)
                Thread.Sleep(10);

            await webSocketClient.SendBinaryMessage("ABCD".ToUTF8Bytes());

            while (binaryMessageLog.Count == 0)
                Thread.Sleep(10);

            #endregion

            #region Validate message delivery

            //Note: If you are debugging too slowly HTTP WebSocket PING/PONG messages will arrive!

            Assert.That(messageRequests. Count, Is.EqualTo(2));
            Assert.That(messageRequests. ElementAt(0).Payload.ToUTF8String(), Is.EqualTo("1234"));
            Assert.That(messageRequests. ElementAt(1).Payload.ToUTF8String(), Is.EqualTo("ABCD"));

            Assert.That(messageResponses.Count, Is.EqualTo(2));
            Assert.That(messageResponses.ElementAt(0).Payload.ToUTF8String(), Is.EqualTo("4321"));
            Assert.That(messageResponses.ElementAt(1).Payload.ToUTF8String(), Is.EqualTo("DCBA"));


            Assert.That(textMessageRequests.   Count, Is.EqualTo(1));
            Assert.That(textMessageRequests.   ElementAt(0), Is.EqualTo("1234"));
            Assert.That(binaryMessageRequests. Count, Is.EqualTo(1));
            Assert.That(binaryMessageRequests. ElementAt(0).ToUTF8String(), Is.EqualTo("ABCD"));

            Assert.That(textMessageResponses.  Count, Is.EqualTo(1));
            Assert.That(textMessageResponses.  ElementAt(0), Is.EqualTo("4321"));
            Assert.That(binaryMessageResponses.Count, Is.EqualTo(1));
            Assert.That(binaryMessageResponses.ElementAt(0).ToUTF8String(), Is.EqualTo("DCBA"));


            Assert.That(textMessageLog.        Count, Is.EqualTo(1));
            Assert.That(textMessageLog.        ElementAt(0), Is.EqualTo("4321"));
            Assert.That(binaryMessageLog.      Count, Is.EqualTo(1));
            Assert.That(binaryMessageLog.      ElementAt(0), Is.EqualTo("DCBA"));

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

            webSocketServer.OnValidateTCPConnection       += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedTCP.Add($"{validatedTCP.Count}: {connection.Client.RemoteEndPoint?.ToString() ?? "-"}");
                return Task.FromResult(ConnectionFilterResponse.Accepted());
            };

            webSocketServer.OnNewTCPConnection            += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newTCPConnection.Add($"{newTCPConnection.Count}: {connection.RemoteSocket}");
                return Task.CompletedTask;
            };

            webSocketServer.OnHTTPRequest                 += (timestamp, server, httpRequest, cancellationToken) => {
                httpRequests.Add(httpRequest);
                return Task.CompletedTask;
            };

            webSocketServer.OnValidateWebSocketConnection += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedWebSocket.Add($"{validatedWebSocket.Count}: {connection.RemoteSocket}");
                return Task.FromResult<HTTPResponse?>(null);
            };

            webSocketServer.OnHTTPResponse                += (timestamp, server, httpRequest, httpResponse, cancellationToken) => {
                httpResponses.Add(httpResponse);
                return Task.CompletedTask;
            };

            webSocketServer.OnNewWebSocketConnection      += (timestamp, server, connection, eventTrackingId, sharedSubprotocols, selectedSubprotocol, cancellationToken) => {
                newWebSocketConnection.Add($"{newWebSocketConnection.Count}: {connection.RemoteSocket}");
                return Task.CompletedTask;
            };

            webSocketServer.OnWebSocketFrameReceived      += (timestamp, server, connection, eventTrackingId, requestFrame, cancellationToken) => {
                messageRequests.       Add(requestFrame);
                return Task.CompletedTask;
            };

            webSocketServer.OnWebSocketFrameSent          += (timestamp, server, connection, eventTrackingId, responseFrame, cancellationToken) => {
                messageResponses.      Add(responseFrame);
                return Task.CompletedTask;
            };

            webSocketServer.OnTextMessageReceived         += (timestamp, server, connection, frame, eventTrackingId, textMessage, cancellationToken) => {
                textMessageRequests.   Add(textMessage);
                return Task.CompletedTask;
            };

            webSocketServer.OnTextMessageSent             += (timestamp, server, connection, frame, eventTrackingId, textMessage, sentStatus, cancellationToken) => {
                textMessageResponses.  Add(textMessage ?? "-");
                return Task.CompletedTask;
            };

            webSocketServer.OnBinaryMessageReceived       += (timestamp, server, connection, frame, eventTrackingId, binaryMessage, cancellationToken) => {
                binaryMessageRequests. Add(binaryMessage);
                return Task.CompletedTask;
            };

            webSocketServer.OnBinaryMessageSent           += (timestamp, server, connection, frame, eventTrackingId, binaryMessage, sentStatus, cancellationToken) => {
                binaryMessageResponses.Add(binaryMessage);
                return Task.CompletedTask;
            };

            #endregion


            var webSocketClient  = new WebSocketClient(
                                       URL.Parse($"ws://127.0.0.1:{HTTPPort}"),
                                       SecWebSocketProtocols: ["ocpp1.6"]
                                   );

            var response1        = await webSocketClient.Connect();
            var httpResponse     = response1.Item2;


            #region Check HTTP request

            // Wait a bit, because running multiple tests at once has timing issues!
            while (newWebSocketConnection.Count == 0)
                Thread.Sleep(10);

            Assert.That(validatedTCP.          Count, Is.EqualTo(1), validatedTCP.          AggregateCSV());
            Assert.That(newTCPConnection.      Count, Is.EqualTo(1), newTCPConnection.      AggregateCSV());
            Assert.That(validatedWebSocket.    Count, Is.EqualTo(1), validatedWebSocket.    AggregateCSV());
            Assert.That(newWebSocketConnection.Count, Is.EqualTo(1), newWebSocketConnection.AggregateCSV());

            Assert.That(httpRequests.          Count, Is.EqualTo(1));
            Assert.That(httpResponses.         Count, Is.EqualTo(1));
            Assert.That(webSocketServer.WebSocketConnections.Count(), Is.EqualTo(1));


            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host:                     127.0.0.1:101
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Key:        /vkroMJ5bwBVW200riZKRg==
            // Sec-WebSocket-Protocol:   ocpp1.6
            // Sec-WebSocket-Version:    13

            // HTTP requests should not have a "Date"-header!
            Assert.That(request.Contains("Date:"), Is.False, request);
            Assert.That(request.Contains("GET / HTTP/1.1"), Is.True, request);
            Assert.That(request.Contains($"Host: 127.0.0.1:{HTTPPort}"), Is.True, request);
            Assert.That(request.Contains($"Upgrade: websocket"), Is.True, request);
            Assert.That(request.Contains($"Sec-WebSocket-Protocol: ocpp1.6"), Is.True, request);

            #endregion

            #region Check HTTP response

            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 101 Switching Protocols
            // Date:                     Thu, 03 Aug 2023 21:53:54 GMT
            // Server:                   GraphDefined HTTP WebSocket Service v2.0
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Accept:     s9FvxhRowHKxS38G/sBt7gC5qec=
            // Sec-WebSocket-Version:    13

            Assert.That(response.Contains("HTTP/1.1 101 Switching Protocols"), Is.True, response);

            Assert.That(httpResponse.Server, Is.EqualTo("GraphDefined HTTP WebSocket Service v2.0"));
            Assert.That(httpResponse.Connection, Is.EqualTo(ConnectionType.Upgrade));
            Assert.That(httpResponse.Upgrade, Is.EqualTo("websocket"));

            #endregion


            #region Send messages

            await webSocketClient.SendTextMessage("1234");

            while (textMessageResponses.Count == 0)
                Thread.Sleep(10);

            await webSocketClient.SendBinaryMessage("ABCD".ToUTF8Bytes());

            while (binaryMessageResponses.Count == 0)
                Thread.Sleep(10);

            #endregion

            #region Validate message delivery

            //Note: If you are debugging too slowly HTTP WebSocket PING/PONG messages will arrive!

            Assert.That(messageRequests. Count, Is.EqualTo(2));
            Assert.That(messageRequests. ElementAt(0).Payload.ToUTF8String(), Is.EqualTo("1234"));
            Assert.That(messageRequests. ElementAt(1).Payload.ToUTF8String(), Is.EqualTo("ABCD"));

            Assert.That(messageResponses.Count, Is.EqualTo(2));
            Assert.That(messageResponses.ElementAt(0).Payload.ToUTF8String(), Is.EqualTo("4321"));
            Assert.That(messageResponses.ElementAt(1).Payload.ToUTF8String(), Is.EqualTo("DCBA"));


            Assert.That(textMessageRequests.   Count, Is.EqualTo(1));
            Assert.That(textMessageRequests.   ElementAt(0), Is.EqualTo("1234"));
            Assert.That(binaryMessageRequests. Count, Is.EqualTo(1));
            Assert.That(binaryMessageRequests. ElementAt(0).ToUTF8String(), Is.EqualTo("ABCD"));

            Assert.That(textMessageResponses.  Count, Is.EqualTo(1));
            Assert.That(textMessageResponses.  ElementAt(0), Is.EqualTo("4321"));
            Assert.That(binaryMessageResponses.Count, Is.EqualTo(1));
            Assert.That(binaryMessageResponses.ElementAt(0).ToUTF8String(), Is.EqualTo("DCBA"));

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

            webSocketServer.AddSecWebSocketProtocol("ocpp1.6");

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

            webSocketServer.OnValidateTCPConnection       += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedTCP.Add($"{validatedTCP.Count}: {connection.Client.RemoteEndPoint?.ToString() ?? "-"}");
                return Task.FromResult(ConnectionFilterResponse.Accepted());
            };

            webSocketServer.OnNewTCPConnection            += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newTCPConnection.Add($"{newTCPConnection.Count}: {connection.RemoteSocket}");
                return Task.CompletedTask;
            };

            webSocketServer.OnHTTPRequest                 += (timestamp, server, httpRequest, cancellationToken) => {
                httpRequests.Add(httpRequest);
                return Task.CompletedTask;
            };

            webSocketServer.OnValidateWebSocketConnection += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedWebSocket.Add($"{validatedWebSocket.Count}: {connection.RemoteSocket}");
                return Task.FromResult<HTTPResponse?>(null);
            };

            webSocketServer.OnHTTPResponse                += (timestamp, server, httpRequest, httpResponse, cancellationToken) => {
                httpResponses.Add(httpResponse);
                return Task.CompletedTask;
            };

            webSocketServer.OnNewWebSocketConnection      += (timestamp, server, connection, eventTrackingId, sharedSubprotocols, selectedSubprotocol, cancellationToken) => {
                newWebSocketConnection.Add($"{newWebSocketConnection.Count}: {connection.RemoteSocket}");
                return Task.CompletedTask;
            };

            webSocketServer.OnWebSocketFrameReceived      += (timestamp, server, connection, eventTrackingId, requestFrame, cancellationToken) => {
                messageRequests.       Add(requestFrame);
                return Task.CompletedTask;
            };

            webSocketServer.OnWebSocketFrameSent          += (timestamp, server, connection, eventTrackingId, responseFrame, cancellationToken) => {
                messageResponses.      Add(responseFrame);
                return Task.CompletedTask;
            };

            webSocketServer.OnTextMessageReceived         += (timestamp, server, connection, frame, eventTrackingId, textMessage, cancellationToken) => {
                textMessageRequests.   Add(textMessage);
                return Task.CompletedTask;
            };

            webSocketServer.OnTextMessageSent             += (timestamp, server, connection, frame, eventTrackingId, textMessage, sentStatus, cancellationToken) => {
                textMessageResponses.  Add(textMessage ?? "-");
                return Task.CompletedTask;
            };

            webSocketServer.OnBinaryMessageReceived       += (timestamp, server, connection, frame, eventTrackingId, binaryMessage, cancellationToken) => {
                binaryMessageRequests. Add(binaryMessage);
                return Task.CompletedTask;
            };

            webSocketServer.OnBinaryMessageSent           += (timestamp, server, connection, frame, eventTrackingId, binaryMessage, sentStatus, cancellationToken) => {
                binaryMessageResponses.Add(binaryMessage);
                return Task.CompletedTask;
            };

            #endregion


            var webSocketClient  = new WebSocketClient(
                                       URL.Parse($"ws://127.0.0.1:{HTTPPort}"),
                                       SecWebSocketProtocols: ["ocpp1.6"]
                                   );

            var response1        = await webSocketClient.Connect();
            var httpResponse     = response1.Item2;


            #region Check HTTP request

            // Wait a bit, because running multiple tests at once has timing issues!
            while (newWebSocketConnection.Count == 0)
                Thread.Sleep(10);

            Assert.That(validatedTCP.          Count, Is.EqualTo(1), validatedTCP.          AggregateCSV());
            Assert.That(newTCPConnection.      Count, Is.EqualTo(1), newTCPConnection.      AggregateCSV());
            Assert.That(validatedWebSocket.    Count, Is.EqualTo(1), validatedWebSocket.    AggregateCSV());
            Assert.That(newWebSocketConnection.Count, Is.EqualTo(1), newWebSocketConnection.AggregateCSV());

            Assert.That(httpRequests.          Count, Is.EqualTo(1));
            Assert.That(httpResponses.         Count, Is.EqualTo(1));
            Assert.That(webSocketServer.WebSocketConnections.Count(), Is.EqualTo(1));


            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host:                     127.0.0.1:101
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Key:        /vkroMJ5bwBVW200riZKRg==
            // Sec-WebSocket-Protocol:   ocpp1.6
            // Sec-WebSocket-Version:    13

            // HTTP requests should not have a "Date"-header!
            Assert.That(request.Contains("Date:"), Is.False, request);
            Assert.That(request.Contains("GET / HTTP/1.1"), Is.True, request);
            Assert.That(request.Contains($"Host: 127.0.0.1:{HTTPPort}"), Is.True, request);
            Assert.That(request.Contains($"Upgrade: websocket"), Is.True, request);
            Assert.That(request.Contains($"Sec-WebSocket-Protocol: ocpp1.6"), Is.True, request);

            #endregion

            #region Check HTTP response

            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 101 Switching Protocols
            // Date:                     Thu, 03 Aug 2023 22:08:09 GMT
            // Server:                   GraphDefined HTTP WebSocket Service v2.0
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Accept:     s9FvxhRowHKxS38G/sBt7gC5qec=
            // Sec-WebSocket-Protocol:   ocpp1.6
            // Sec-WebSocket-Version:    13

            Assert.That(response.Contains("HTTP/1.1 101 Switching Protocols"), Is.True, response);

            Assert.That(httpResponse.Server, Is.EqualTo("GraphDefined HTTP WebSocket Service v2.0"));
            Assert.That(httpResponse.Connection, Is.EqualTo(ConnectionType.Upgrade));
            Assert.That(httpResponse.Upgrade, Is.EqualTo("websocket"));
            Assert.That(httpResponse.SecWebSocketProtocol, Is.EqualTo("ocpp1.6"));

            #endregion


            #region Send messages

            await webSocketClient.SendTextMessage("1234");

            while (textMessageResponses.Count == 0)
                Thread.Sleep(10);

            await webSocketClient.SendBinaryMessage("ABCD".ToUTF8Bytes());

            while (binaryMessageResponses.Count == 0)
                Thread.Sleep(10);

            #endregion

            #region Validate message delivery

            //Note: If you are debugging too slowly HTTP WebSocket PING/PONG messages will arrive!

            Assert.That(messageRequests. Count, Is.EqualTo(2));
            Assert.That(messageRequests. ElementAt(0).Payload.ToUTF8String(), Is.EqualTo("1234"));
            Assert.That(messageRequests. ElementAt(1).Payload.ToUTF8String(), Is.EqualTo("ABCD"));

            Assert.That(messageResponses.Count, Is.EqualTo(2));
            Assert.That(messageResponses.ElementAt(0).Payload.ToUTF8String(), Is.EqualTo("4321"));
            Assert.That(messageResponses.ElementAt(1).Payload.ToUTF8String(), Is.EqualTo("DCBA"));


            Assert.That(textMessageRequests.   Count, Is.EqualTo(1));
            Assert.That(textMessageRequests.   ElementAt(0), Is.EqualTo("1234"));
            Assert.That(binaryMessageRequests. Count, Is.EqualTo(1));
            Assert.That(binaryMessageRequests. ElementAt(0).ToUTF8String(), Is.EqualTo("ABCD"));

            Assert.That(textMessageResponses.  Count, Is.EqualTo(1));
            Assert.That(textMessageResponses.  ElementAt(0), Is.EqualTo("4321"));
            Assert.That(binaryMessageResponses.Count, Is.EqualTo(1));
            Assert.That(binaryMessageResponses.ElementAt(0).ToUTF8String(), Is.EqualTo("DCBA"));

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

            webSocketServer.AddSecWebSocketProtocol("ocpp1.6");

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

            webSocketServer.OnValidateTCPConnection       += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedTCP.Add($"{validatedTCP.Count}: {connection.Client.RemoteEndPoint?.ToString() ?? "-"}");
                return Task.FromResult(ConnectionFilterResponse.Accepted());
            };

            webSocketServer.OnNewTCPConnection            += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newTCPConnection.Add($"{newTCPConnection.Count}: {connection.RemoteSocket}");
                return Task.CompletedTask;
            };

            webSocketServer.OnHTTPRequest                 += (timestamp, server, httpRequest, cancellationToken) => {
                httpRequests.Add(httpRequest);
                return Task.CompletedTask;
            };

            webSocketServer.OnValidateWebSocketConnection += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedWebSocket.Add($"{validatedWebSocket.Count}: {connection.RemoteSocket}");
                return Task.FromResult<HTTPResponse?>(null);
            };

            webSocketServer.OnHTTPResponse                += (timestamp, server, httpRequest, httpResponse, cancellationToken) => {
                httpResponses.Add(httpResponse);
                return Task.FromResult(ConnectionFilterResponse.Accepted());
            };

            webSocketServer.OnNewWebSocketConnection      += (timestamp, server, connection, eventTrackingId, sharedSubprotocols, selectedSubprotocol, cancellationToken) => {
                newWebSocketConnection.Add($"{newWebSocketConnection.Count}: {connection.RemoteSocket}");
                return Task.FromResult(ConnectionFilterResponse.Accepted());
            };

            webSocketServer.OnWebSocketFrameReceived      += (timestamp, server, connection, eventTrackingId, requestFrame, cancellationToken) => {
                messageRequests.       Add(requestFrame);
                return Task.FromResult(ConnectionFilterResponse.Accepted());
            };

            webSocketServer.OnWebSocketFrameSent          += (timestamp, server, connection, eventTrackingId, responseFrame, cancellationToken) => {
                messageResponses.      Add(responseFrame);
                return Task.FromResult(ConnectionFilterResponse.Accepted());
            };

            webSocketServer.OnTextMessageReceived         += (timestamp, server, connection, frame, eventTrackingId, textMessage, cancellationToken) => {
                textMessageRequests.   Add(textMessage);
                return Task.FromResult(ConnectionFilterResponse.Accepted());
            };

            webSocketServer.OnTextMessageSent             += (timestamp, server, connection, frame, eventTrackingId, textMessage, sentStatus, cancellationToken) => {
                textMessageResponses.  Add(textMessage ?? "-");
                return Task.FromResult(ConnectionFilterResponse.Accepted());
            };

            webSocketServer.OnBinaryMessageReceived       += (timestamp, server, connection, frame, eventTrackingId, binaryMessage, cancellationToken) => {
                binaryMessageRequests. Add(binaryMessage);
                return Task.FromResult(ConnectionFilterResponse.Accepted());
            };

            webSocketServer.OnBinaryMessageSent           += (timestamp, server, connection, frame, eventTrackingId, binaryMessage, sentStatus, cancellationToken) => {
                binaryMessageResponses.Add(binaryMessage);
                return Task.FromResult(ConnectionFilterResponse.Accepted());
            };

            #endregion


            var webSocketClient  = new WebSocketClient(
                                       URL.Parse($"ws://127.0.0.1:{HTTPPort}"),
                                       SecWebSocketProtocols: ["ocpp1.6"]
                                   );

            var response1        = await webSocketClient.Connect();
            var httpResponse     = response1.Item2;


            #region Check HTTP request

            // Wait a bit, because running multiple tests at once has timing issues!
            while (newWebSocketConnection.Count == 0)
                Thread.Sleep(10);

            Assert.That(validatedTCP.          Count, Is.EqualTo(1), validatedTCP.          AggregateCSV());
            Assert.That(newTCPConnection.      Count, Is.EqualTo(1), newTCPConnection.      AggregateCSV());
            Assert.That(validatedWebSocket.    Count, Is.EqualTo(1), validatedWebSocket.    AggregateCSV());
            Assert.That(newWebSocketConnection.Count, Is.EqualTo(1), newWebSocketConnection.AggregateCSV());

            Assert.That(httpRequests.          Count, Is.EqualTo(1));
            Assert.That(httpResponses.         Count, Is.EqualTo(1));
            Assert.That(webSocketServer.WebSocketConnections.Count(), Is.EqualTo(1));


            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host:                     127.0.0.1:101
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Key:        /vkroMJ5bwBVW200riZKRg==
            // Sec-WebSocket-Protocol:   ocpp1.6
            // Sec-WebSocket-Version:    13

            // HTTP requests should not have a "Date"-header!
            Assert.That(request.Contains("Date:"), Is.False, request);
            Assert.That(request.Contains("GET / HTTP/1.1"), Is.True, request);
            Assert.That(request.Contains($"Host: 127.0.0.1:{HTTPPort}"), Is.True, request);
            Assert.That(request.Contains($"Upgrade: websocket"), Is.True, request);
            Assert.That(request.Contains($"Sec-WebSocket-Protocol: ocpp1.6"), Is.True, request);

            #endregion

            #region Check HTTP response

            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 101 Switching Protocols
            // Date:                     Thu, 03 Aug 2023 22:08:09 GMT
            // Server:                   GraphDefined HTTP WebSocket Service v2.0
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Accept:     s9FvxhRowHKxS38G/sBt7gC5qec=
            // Sec-WebSocket-Protocol:   ocpp1.6
            // Sec-WebSocket-Version:    13

            Assert.That(response.Contains("HTTP/1.1 101 Switching Protocols"), Is.True, response);

            Assert.That(httpResponse.Server, Is.EqualTo("GraphDefined HTTP WebSocket Service v2.0"));
            Assert.That(httpResponse.Connection, Is.EqualTo(ConnectionType.Upgrade));
            Assert.That(httpResponse.Upgrade, Is.EqualTo("websocket"));
            Assert.That(httpResponse.SecWebSocketProtocol, Is.EqualTo("ocpp1.6"));

            #endregion


            #region Send messages

            await webSocketClient.SendTextMessage("1234");

            while (textMessageResponses.Count == 0)
                Thread.Sleep(10);

            await webSocketClient.SendBinaryMessage("ABCD".ToUTF8Bytes());

            while (binaryMessageResponses.Count == 0)
                Thread.Sleep(10);

            #endregion

            #region Validate message delivery

            //Note: If you are debugging too slowly HTTP WebSocket PING/PONG messages will arrive!

            Assert.That(messageRequests. Count, Is.EqualTo(2));
            Assert.That(messageRequests. ElementAt(0).Payload.ToUTF8String(), Is.EqualTo("1234"));
            Assert.That(messageRequests. ElementAt(1).Payload.ToUTF8String(), Is.EqualTo("ABCD"));

            Assert.That(messageResponses.Count, Is.EqualTo(2));
            Assert.That(messageResponses.ElementAt(0).Payload.ToUTF8String(), Is.EqualTo("4321"));
            Assert.That(messageResponses.ElementAt(1).Payload.ToUTF8String(), Is.EqualTo("DCBA"));


            Assert.That(textMessageRequests.   Count, Is.EqualTo(1));
            Assert.That(textMessageRequests.   ElementAt(0), Is.EqualTo("1234"));
            Assert.That(binaryMessageRequests. Count, Is.EqualTo(1));
            Assert.That(binaryMessageRequests. ElementAt(0).ToUTF8String(), Is.EqualTo("ABCD"));

            Assert.That(textMessageResponses.  Count, Is.EqualTo(1));
            Assert.That(textMessageResponses.  ElementAt(0), Is.EqualTo("4321"));
            Assert.That(binaryMessageResponses.Count, Is.EqualTo(1));
            Assert.That(binaryMessageResponses.ElementAt(0).ToUTF8String(), Is.EqualTo("DCBA"));

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

            webSocketServer.OnValidateTCPConnection       += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedTCP.Add($"{validatedTCP.Count}: {connection.Client.RemoteEndPoint?.ToString() ?? "-"}");
                return Task.FromResult(ConnectionFilterResponse.Accepted());
            };

            webSocketServer.OnNewTCPConnection            += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newTCPConnection.Add($"{newTCPConnection.Count}: {connection.RemoteSocket}");
                return Task.CompletedTask;
            };

            webSocketServer.OnHTTPRequest                 += (timestamp, server, httpRequest, cancellationToken) => {
                httpRequests.Add(httpRequest);
                return Task.CompletedTask;
            };

            webSocketServer.OnValidateWebSocketConnection += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedWebSocket.Add($"{validatedWebSocket.Count}: {connection.RemoteSocket}");
                return Task.FromResult<HTTPResponse?>(null);
            };

            webSocketServer.OnHTTPResponse                += (timestamp, server, httpRequest, httpResponse, cancellationToken) => {
                httpResponses.Add(httpResponse);
                return Task.CompletedTask;
            };

            webSocketServer.OnNewWebSocketConnection      += (timestamp, server, connection, eventTrackingId, sharedSubprotocols, selectedSubprotocol, cancellationToken) => {
                newWebSocketConnection.Add($"{newWebSocketConnection.Count}: {connection.RemoteSocket}");
                return Task.CompletedTask;
            };

            webSocketServer.OnWebSocketFrameReceived      += (timestamp, server, connection, eventTrackingId, requestFrame, cancellationToken) => {
                messageRequests.       Add(requestFrame);
                return Task.CompletedTask;
            };

            webSocketServer.OnWebSocketFrameSent          += (timestamp, server, connection, eventTrackingId, responseFrame, cancellationToken) => {
                messageResponses.      Add(responseFrame);
                return Task.CompletedTask;
            };

            webSocketServer.OnTextMessageReceived         += (timestamp, server, connection, frame, eventTrackingId, textMessage, cancellationToken) => {
                textMessageRequests.   Add(textMessage);
                return Task.CompletedTask;
            };

            webSocketServer.OnTextMessageSent             += (timestamp, server, connection, frame, eventTrackingId, textMessage, sentStatus, cancellationToken) => {
                textMessageResponses.  Add(textMessage ?? "-");
                return Task.CompletedTask;
            };

            webSocketServer.OnBinaryMessageReceived       += (timestamp, server, connection, frame, eventTrackingId, binaryMessage, cancellationToken) => {
                binaryMessageRequests. Add(binaryMessage);
                return Task.CompletedTask;
            };

            webSocketServer.OnBinaryMessageSent           += (timestamp, server, connection, frame, eventTrackingId, binaryMessage, sentStatus, cancellationToken) => {
                binaryMessageResponses.Add(binaryMessage);
                return Task.CompletedTask;
            };

            #endregion


            var webSocketClient  = new WebSocketClient(
                                       URL.Parse($"ws://127.0.0.1:{HTTPPort}"),
                                       HTTPAuthentication:  HTTPBasicAuthentication.Create("username", "password")
                                   );

            var response1        = await webSocketClient.Connect();
            var httpResponse     = response1.Item2;


            #region Check HTTP request

            // Wait a bit, because running multiple tests at once has timing issues!
            while (newWebSocketConnection.Count == 0)
                Thread.Sleep(10);

            Assert.That(validatedTCP.          Count, Is.EqualTo(1), validatedTCP.          AggregateCSV());
            Assert.That(newTCPConnection.      Count, Is.EqualTo(1), newTCPConnection.      AggregateCSV());
            Assert.That(validatedWebSocket.    Count, Is.EqualTo(1), validatedWebSocket.    AggregateCSV());
            Assert.That(newWebSocketConnection.Count, Is.EqualTo(1), newWebSocketConnection.AggregateCSV());

            Assert.That(httpRequests.          Count, Is.EqualTo(1));
            Assert.That(httpResponses.         Count, Is.EqualTo(1));
            Assert.That(webSocketServer.WebSocketConnections.Count(), Is.EqualTo(1));


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
            Assert.That(request.Contains("Date:"), Is.False, request);
            Assert.That(request.Contains("GET / HTTP/1.1"), Is.True, request);
            Assert.That(request.Contains($"Host: 127.0.0.1:{HTTPPort}"), Is.True, request);
            Assert.That(request.Contains($"Upgrade: websocket"), Is.True, request);
            Assert.That(request.Contains($"Authorization: Basic dXNlcm5hbWU6cGFzc3dvcmQ="), Is.True, request);

            #endregion

            #region Check HTTP response

            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 101 Switching Protocols
            // Date:                     Thu, 03 Aug 2023 22:29:09 GMT
            // Server:                   GraphDefined HTTP WebSocket Service v2.0
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Accept:     s9FvxhRowHKxS38G/sBt7gC5qec=
            // Sec-WebSocket-Version:    13

            Assert.That(response.Contains("HTTP/1.1 101 Switching Protocols"), Is.True, response);

            Assert.That(httpResponse.Server, Is.EqualTo("GraphDefined HTTP WebSocket Service v2.0"));
            Assert.That(httpResponse.Connection, Is.EqualTo(ConnectionType.Upgrade));
            Assert.That(httpResponse.Upgrade, Is.EqualTo("websocket"));

            #endregion


            #region Send messages

            await webSocketClient.SendTextMessage("1234");

            while (textMessageResponses.Count == 0)
                Thread.Sleep(10);

            await webSocketClient.SendBinaryMessage("ABCD".ToUTF8Bytes());

            while (binaryMessageResponses.Count == 0)
                Thread.Sleep(10);

            #endregion

            #region Validate message delivery

            //Note: If you are debugging too slowly HTTP WebSocket PING/PONG messages will arrive!

            Assert.That(messageRequests. Count, Is.EqualTo(2));
            Assert.That(messageRequests. ElementAt(0).Payload.ToUTF8String(), Is.EqualTo("1234"));
            Assert.That(messageRequests. ElementAt(1).Payload.ToUTF8String(), Is.EqualTo("ABCD"));

            Assert.That(messageResponses.Count, Is.EqualTo(2));
            Assert.That(messageResponses.ElementAt(0).Payload.ToUTF8String(), Is.EqualTo("4321"));
            Assert.That(messageResponses.ElementAt(1).Payload.ToUTF8String(), Is.EqualTo("DCBA"));


            Assert.That(textMessageRequests.   Count, Is.EqualTo(1));
            Assert.That(textMessageRequests.   ElementAt(0), Is.EqualTo("1234"));
            Assert.That(binaryMessageRequests. Count, Is.EqualTo(1));
            Assert.That(binaryMessageRequests. ElementAt(0).ToUTF8String(), Is.EqualTo("ABCD"));

            Assert.That(textMessageResponses.  Count, Is.EqualTo(1));
            Assert.That(textMessageResponses.  ElementAt(0), Is.EqualTo("4321"));
            Assert.That(binaryMessageResponses.Count, Is.EqualTo(1));
            Assert.That(binaryMessageResponses.ElementAt(0).ToUTF8String(), Is.EqualTo("DCBA"));

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

            webSocketServer.OnValidateTCPConnection       += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedTCP.Add($"{validatedTCP.Count}: {connection.Client.RemoteEndPoint?.ToString() ?? "-"}");
                return Task.FromResult(ConnectionFilterResponse.Accepted());
            };

            webSocketServer.OnNewTCPConnection            += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newTCPConnection.Add($"{newTCPConnection.Count}: {connection.RemoteSocket}");
                return Task.CompletedTask;
            };

            webSocketServer.OnHTTPRequest                 += (timestamp, server, httpRequest, cancellationToken) => {
                httpRequests.Add(httpRequest);
                return Task.CompletedTask;
            };

            webSocketServer.OnValidateWebSocketConnection += (timestamp, server, connection, eventTrackingId, cancellationToken) => {

                if (connection.HTTPRequest is not null &&
                    connection.HTTPRequest.Authorization is HTTPBasicAuthentication httpBasicAuth &&
                    httpBasicAuth.Username == "username" &&
                    httpBasicAuth.Password == "password")
                {
                    validatedWebSocket.Add($"{validatedWebSocket.Count}: {connection.RemoteSocket}");
                    return Task.FromResult<HTTPResponse?>(null);
                }
                else
                {
                    return Task.FromResult<HTTPResponse?>(
                               new HTTPResponse.Builder(connection.HTTPRequest) {
                                   HTTPStatusCode   = HTTPStatusCode.Unauthorized,
                                   WWWAuthenticate  = WWWAuthenticate.Basic("Access to the WebSocket server"),
                                   Connection       = ConnectionType.Close
                               }.AsImmutable
                           );
                }

            };

            webSocketServer.OnHTTPResponse                += (timestamp, server, httpRequest, httpResponse, cancellationToken) => {
                httpResponses.Add(httpResponse);
                return Task.CompletedTask;
            };

            webSocketServer.OnNewWebSocketConnection      += (timestamp, server, connection, eventTrackingId, sharedSubprotocols, selectedSubprotocol, cancellationToken) => {
                newWebSocketConnection.Add($"{newWebSocketConnection.Count}: {connection.RemoteSocket}");
                return Task.CompletedTask;
            };

            webSocketServer.OnWebSocketFrameReceived      += (timestamp, server, connection, eventTrackingId, requestFrame, cancellationToken) => {
                messageRequests.       Add(requestFrame);
                return Task.CompletedTask;
            };

            webSocketServer.OnWebSocketFrameSent          += (timestamp, server, connection, eventTrackingId, responseFrame, cancellationToken) => {
                messageResponses.      Add(responseFrame);
                return Task.CompletedTask;
            };

            webSocketServer.OnTextMessageReceived         += (timestamp, server, connection, frame, eventTrackingId, textMessage, cancellationToken) => {
                textMessageRequests.   Add(textMessage);
                return Task.CompletedTask;
            };

            webSocketServer.OnTextMessageSent             += (timestamp, server, connection, frame, eventTrackingId, textMessage, sentStatus, cancellationToken) => {
                textMessageResponses.  Add(textMessage ?? "-");
                return Task.CompletedTask;
            };

            webSocketServer.OnBinaryMessageReceived       += (timestamp, server, connection, frame, eventTrackingId, binaryMessage, cancellationToken) => {
                binaryMessageRequests. Add(binaryMessage);
                return Task.CompletedTask;
            };

            webSocketServer.OnBinaryMessageSent           += (timestamp, server, connection, frame, eventTrackingId, binaryMessage, sentStatus, cancellationToken) => {
                binaryMessageResponses.Add(binaryMessage);
                return Task.CompletedTask;
            };

            #endregion


            var webSocketClient  = new WebSocketClient(
                                       URL.Parse($"ws://127.0.0.1:{HTTPPort}"),
                                       HTTPAuthentication:  HTTPBasicAuthentication.Create("username", "password")
                                   );

            var response1        = await webSocketClient.Connect();
            var httpResponse     = response1.Item2;


            #region Check HTTP request

            // Wait a bit, because running multiple tests at once has timing issues!
            while (newWebSocketConnection.Count == 0)
                Thread.Sleep(10);

            Assert.That(validatedTCP.          Count,                   Is.EqualTo(1), validatedTCP.          AggregateCSV());
            Assert.That(newTCPConnection.      Count,                   Is.EqualTo(1), newTCPConnection.      AggregateCSV());
            Assert.That(validatedWebSocket.    Count,                   Is.EqualTo(1), validatedWebSocket.    AggregateCSV());
            Assert.That(newWebSocketConnection.Count,                   Is.EqualTo(1), newWebSocketConnection.AggregateCSV());

            Assert.That(httpRequests.          Count,                   Is.EqualTo(1));
            Assert.That(httpResponses.         Count,                   Is.EqualTo(1));
            Assert.That(webSocketServer.WebSocketConnections.Count(),   Is.EqualTo(1));


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
            Assert.That(request.Contains("Date:"),                                            Is.False, request);
            Assert.That(request.Contains("GET / HTTP/1.1"),                                   Is.True,  request);
            Assert.That(request.Contains($"Host: 127.0.0.1:{HTTPPort}"),                      Is.True,  request);
            Assert.That(request.Contains($"Upgrade: websocket"),                              Is.True,  request);
            Assert.That(request.Contains($"Authorization: Basic dXNlcm5hbWU6cGFzc3dvcmQ="),   Is.True,  request);

            #endregion

            #region Check HTTP response

            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 101 Switching Protocols
            // Date:                     Thu, 03 Aug 2023 22:29:09 GMT
            // Server:                   GraphDefined HTTP WebSocket Service v2.0
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Accept:     s9FvxhRowHKxS38G/sBt7gC5qec=
            // Sec-WebSocket-Version:    13

            Assert.That(response.Contains("HTTP/1.1 101 Switching Protocols"), Is.True, response);

            Assert.That(httpResponse.Server,        Is.EqualTo("GraphDefined HTTP WebSocket Service v2.0"));
            Assert.That(httpResponse.Connection,    Is.EqualTo(ConnectionType.Upgrade));
            Assert.That(httpResponse.Upgrade,       Is.EqualTo("websocket"));

            #endregion


            #region Send messages

            await webSocketClient.SendTextMessage("1234");

            while (textMessageResponses.Count == 0)
                Thread.Sleep(10);

            await webSocketClient.SendBinaryMessage("ABCD".ToUTF8Bytes());

            while (binaryMessageResponses.Count == 0)
                Thread.Sleep(10);

            #endregion

            #region Validate message delivery

            //Note: If you are debugging too slowly HTTP WebSocket PING/PONG messages will arrive!

            Assert.That(messageRequests. Count, Is.EqualTo(2));
            Assert.That(messageRequests. ElementAt(0).Payload.ToUTF8String(), Is.EqualTo("1234"));
            Assert.That(messageRequests. ElementAt(1).Payload.ToUTF8String(), Is.EqualTo("ABCD"));

            Assert.That(messageResponses.Count, Is.EqualTo(2));
            Assert.That(messageResponses.ElementAt(0).Payload.ToUTF8String(), Is.EqualTo("4321"));
            Assert.That(messageResponses.ElementAt(1).Payload.ToUTF8String(), Is.EqualTo("DCBA"));


            Assert.That(textMessageRequests.   Count, Is.EqualTo(1));
            Assert.That(textMessageRequests.   ElementAt(0), Is.EqualTo("1234"));
            Assert.That(binaryMessageRequests. Count, Is.EqualTo(1));
            Assert.That(binaryMessageRequests. ElementAt(0).ToUTF8String(), Is.EqualTo("ABCD"));

            Assert.That(textMessageResponses.  Count, Is.EqualTo(1));
            Assert.That(textMessageResponses.  ElementAt(0), Is.EqualTo("4321"));
            Assert.That(binaryMessageResponses.Count, Is.EqualTo(1));
            Assert.That(binaryMessageResponses.ElementAt(0).ToUTF8String(), Is.EqualTo("DCBA"));

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

            webSocketServer.OnValidateTCPConnection       += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedTCP.Add($"{validatedTCP.Count}: {connection.Client.RemoteEndPoint?.ToString() ?? "-"}");
                return Task.FromResult(ConnectionFilterResponse.Accepted());
            };

            webSocketServer.OnNewTCPConnection            += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newTCPConnection.Add($"{newTCPConnection.Count}: {connection.RemoteSocket}");
                return Task.CompletedTask;
            };

            webSocketServer.OnHTTPRequest                 += (timestamp, server, httpRequest, cancellationToken) => {
                httpRequests.Add(httpRequest);
                return Task.CompletedTask;
            };

            webSocketServer.OnValidateWebSocketConnection += (timestamp, server, connection, eventTrackingId, cancellationToken) => {

                if (connection.HTTPRequest is not null &&
                    connection.HTTPRequest.Authorization is HTTPBasicAuthentication httpBasicAuth &&
                    httpBasicAuth.Username == "username" &&
                    httpBasicAuth.Password == "password")
                {
                    validatedWebSocket.Add($"{validatedWebSocket.Count}: {connection.RemoteSocket}");
                    return Task.FromResult<HTTPResponse?>(null);
                }
                else
                {
                    return Task.FromResult<HTTPResponse?>(
                               new HTTPResponse.Builder(connection.HTTPRequest) {
                                   HTTPStatusCode   = HTTPStatusCode.Unauthorized,
                                   Server           = "GraphDefined HTTP WebSocket Service v2.0",
                                   WWWAuthenticate  = WWWAuthenticate.Basic("Access to the WebSocket server"),
                                   Connection       = ConnectionType.Close
                               }.AsImmutable
                           );
                }

            };

            webSocketServer.OnHTTPResponse                += (timestamp, server, httpRequest, httpResponse, cancellationToken) => {
                httpResponses.Add(httpResponse);
                return Task.CompletedTask;
            };

            webSocketServer.OnNewWebSocketConnection      += (timestamp, server, connection, eventTrackingId, sharedSubprotocols, selectedSubprotocol, cancellationToken) => {
                newWebSocketConnection.Add($"{newWebSocketConnection.Count}: {connection.RemoteSocket}");
                return Task.CompletedTask;
            };

            webSocketServer.OnWebSocketFrameReceived      += (timestamp, server, connection, eventTrackingId, requestFrame, cancellationToken) => {
                messageRequests.       Add(requestFrame);
                return Task.CompletedTask;
            };

            webSocketServer.OnWebSocketFrameSent          += (timestamp, server, connection, eventTrackingId, responseFrame, cancellationToken) => {
                messageResponses.      Add(responseFrame);
                return Task.CompletedTask;
            };

            webSocketServer.OnTextMessageReceived         += (timestamp, server, connection, frame, eventTrackingId, textMessage, cancellationToken) => {
                textMessageRequests.   Add(textMessage);
                return Task.CompletedTask;
            };

            webSocketServer.OnTextMessageSent             += (timestamp, server, connection, frame, eventTrackingId, textMessage, sentStatus, cancellationToken) => {
                textMessageResponses.  Add(textMessage ?? "-");
                return Task.CompletedTask;
            };

            webSocketServer.OnBinaryMessageReceived       += (timestamp, server, connection, frame, eventTrackingId, binaryMessage, cancellationToken) => {
                binaryMessageRequests. Add(binaryMessage);
                return Task.CompletedTask;
            };

            webSocketServer.OnBinaryMessageSent           += (timestamp, server, connection, frame, eventTrackingId, binaryMessage, sentStatus, cancellationToken) => {
                binaryMessageResponses.Add(binaryMessage);
                return Task.CompletedTask;
            };

            #endregion


            var webSocketClient  = new WebSocketClient(
                                       URL.Parse($"ws://127.0.0.1:{HTTPPort}"),
                                       HTTPAuthentication:  HTTPBasicAuthentication.Create("nameOfUser", "passphrase")
                                   );

            var response1        = await webSocketClient.Connect();
            var httpResponse     = response1.Item2;


            #region Check HTTP request

            Assert.That(
                SpinWait.SpinUntil(() => httpResponses.Count == 1, TimeSpan.FromSeconds(5)),
                Is.True,
                "Timed out waiting for the HTTP 401 response."
            );

            Assert.That(validatedTCP.          Count, Is.EqualTo(1), validatedTCP.          AggregateCSV());
            Assert.That(newTCPConnection.      Count, Is.EqualTo(1), newTCPConnection.      AggregateCSV());
            Assert.That(validatedWebSocket.    Count, Is.EqualTo(0), validatedWebSocket.    AggregateCSV());
            Assert.That(newWebSocketConnection.Count, Is.EqualTo(0), newWebSocketConnection.AggregateCSV());

            Assert.That(httpRequests.          Count, Is.EqualTo(1));
            Assert.That(httpResponses.         Count, Is.EqualTo(1));
            Assert.That(
                SpinWait.SpinUntil(() => !webSocketServer.WebSocketConnections.Any(), TimeSpan.FromSeconds(5)),
                Is.True,
                "Timed out waiting for the rejected WebSocket connection to close."
            );


            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host:                    127.0.0.1:101
            // Connection:              Upgrade
            // Upgrade:                 websocket
            // Sec-WebSocket-Key:       /vkroMJ5bwBVW200riZKRg==
            // Sec-WebSocket-Version:   13
            // Authorization:           Basic bmFtZU9mVXNlcjpwYXNzcGhyYXNl

            // HTTP requests should not have a "Date"-header!
            Assert.That(request.Contains("Date:"), Is.False, request);
            Assert.That(request.Contains("GET / HTTP/1.1"), Is.True, request);
            Assert.That(request.Contains($"Host: 127.0.0.1:{HTTPPort}"), Is.True, request);
            Assert.That(request.Contains($"Upgrade: websocket"), Is.True, request);
            Assert.That(request.Contains($"Authorization: Basic bmFtZU9mVXNlcjpwYXNzcGhyYXNl"), Is.True, request);

            #endregion

            #region Check HTTP response

            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 401 Unauthorized
            // Date:               Thu, 03 Aug 2023 23:01:56 GMT
            // Server:             GraphDefined HTTP WebSocket Service v2.0
            // WWW-Authenticate:   Basic realm="Access to the WebSocket server", charset="UTF-8"
            // Connection:         Close

            Assert.That(response.Contains("HTTP/1.1 401 Unauthorized"), Is.True, response);

            Assert.That(httpResponse.Server, Is.EqualTo("GraphDefined HTTP WebSocket Service v2.0"));
            Assert.That(httpResponse.WWWAuthenticate?.ToString(), Is.EqualTo("Basic realm=\"Access to the WebSocket server\", charset=\"UTF-8\""));
            Assert.That(httpResponse.Connection, Is.EqualTo(ConnectionType.Close));

            #endregion


            await webSocketClient.Close();

        }

        #endregion


    }

}
