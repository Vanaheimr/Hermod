﻿/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
using NUnit.Framework.Legacy;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTPS.WebSockets
{

    /// <summary>
    /// Tests between Hermod WebSocket TLS clients and Hermod WebSocket TLS servers.
    /// </summary>
    [TestFixture]
    public class WebSocketTLSClientTests : AWebSocketTLSServerTests
    {

        #region Constructor(s)

        public WebSocketTLSClientTests()
            : base(IPPort.Parse(1131))
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

            var response1        = await webSocketClient.Connect();
            var httpResponse     = response1.Item2;

            #endregion

            #region Check HTTP request

            // Wait a bit, because running multiple tests at once has timing issues!
            while (newWebSocketConnection.Count == 0)
                Thread.Sleep(10);

            ClassicAssert.AreEqual(1, validatedTCP.          Count, validatedTCP.          AggregateCSV());
            ClassicAssert.AreEqual(1, newTCPConnection.      Count, newTCPConnection.      AggregateCSV());
            ClassicAssert.AreEqual(1, validatedWebSocket.    Count, validatedWebSocket.    AggregateCSV());
            ClassicAssert.AreEqual(1, newWebSocketConnection.Count, newWebSocketConnection.AggregateCSV());

            ClassicAssert.AreEqual(1, httpRequests.          Count);
            ClassicAssert.AreEqual(1, httpResponses.         Count);
            ClassicAssert.AreEqual(1, webSocketServer.WebSocketConnections.Count());


            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host:                     127.0.0.1:101
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Key:        /vkroMJ5bwBVW200riZKRg==
            // Sec-WebSocket-Version:    13

            // HTTP requests should not have a "Date"-header!
            ClassicAssert.IsFalse (request.Contains("Date:"),                         request);
            ClassicAssert.IsTrue  (request.Contains("GET / HTTP/1.1"),                request);
            ClassicAssert.IsTrue  (request.Contains($"Host: 127.0.0.1:{HTTPPort}"),   request);
            ClassicAssert.IsTrue  (request.Contains($"Upgrade: websocket"),           request);

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

            ClassicAssert.IsTrue  (response.Contains("HTTP/1.1 101 Switching Protocols"),   response);

            ClassicAssert.AreEqual("GraphDefined HTTP WebSocket Service v2.0",             httpResponse.Server);
            ClassicAssert.AreEqual("Upgrade",                                               httpResponse.Connection);
            ClassicAssert.AreEqual("websocket",                                             httpResponse.Upgrade);

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

            ClassicAssert.AreEqual(2,       messageRequests. Count);
            ClassicAssert.AreEqual("1234",  messageRequests. ElementAt(0).Payload.ToUTF8String());
            ClassicAssert.AreEqual("ABCD",  messageRequests. ElementAt(1).Payload.ToUTF8String());

            ClassicAssert.AreEqual(2,       messageResponses.Count);
            ClassicAssert.AreEqual("4321",  messageResponses.ElementAt(0).Payload.ToUTF8String());
            ClassicAssert.AreEqual("DCBA",  messageResponses.ElementAt(1).Payload.ToUTF8String());


            ClassicAssert.AreEqual(1,       textMessageRequests.   Count);
            ClassicAssert.AreEqual("1234",  textMessageRequests.   ElementAt(0));
            ClassicAssert.AreEqual(1,       binaryMessageRequests. Count);
            ClassicAssert.AreEqual("ABCD",  binaryMessageRequests. ElementAt(0).ToUTF8String());

            ClassicAssert.AreEqual(1,       textMessageResponses.  Count);
            ClassicAssert.AreEqual("4321",  textMessageResponses.  ElementAt(0));
            ClassicAssert.AreEqual(1,       binaryMessageResponses.Count);
            ClassicAssert.AreEqual("DCBA",  binaryMessageResponses.ElementAt(0).ToUTF8String());


            ClassicAssert.AreEqual(1,       textMessageLog.        Count);
            ClassicAssert.AreEqual("4321",  textMessageLog.        ElementAt(0));
            ClassicAssert.AreEqual(1,       binaryMessageLog.      Count);
            ClassicAssert.AreEqual("DCBA",  binaryMessageLog.      ElementAt(0));

            #endregion


            await webSocketClient.Close();

        }

        #endregion


    }

}
