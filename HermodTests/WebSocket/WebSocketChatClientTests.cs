/*
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

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// Tests between Hermod WebSocket clients and Hermod WebSocket servers.
    /// </summary>
    [TestFixture]
    public class WebSocketChatClientTests : AWebSocketChatServerTests
    {

        #region Constructor(s)

        public WebSocketChatClientTests()
            : base(IPPort.Parse(2001))
        { }

        #endregion


        #region Test_ChatClients()

        [Test]
        public async Task Test_ChatClients()
        {

            // Note: Your operating system or firewall might not allow you to open
            //       or accept 100+ TCP connections within a short time span!
            //       Also the task scheduling might slow down the test!
            var numberOfClients         = 3;

            #region Server setup

            if (webSocketChatServer is null) {
                Assert.Fail("WebSocketChatServer is null!");
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

            webSocketChatServer.OnValidateTCPConnection       += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedTCP.Add($"{validatedTCP.Count}: {connection.Client.RemoteEndPoint?.ToString() ?? "-"}");
                return Task.FromResult(ConnectionFilterResponse.Accepted());
            };

            webSocketChatServer.OnNewTCPConnection            += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newTCPConnection.Add($"{newTCPConnection.Count}: {connection.RemoteSocket}");
                return Task.CompletedTask;
            };

            webSocketChatServer.OnHTTPRequest                 += (timestamp, server, httpRequest, cancellationToken) => {
                httpRequests.Add(httpRequest);
                return Task.CompletedTask;
            };

            webSocketChatServer.OnValidateWebSocketConnection += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedWebSocket.Add($"{validatedWebSocket.Count}: {connection.RemoteSocket}");
                return Task.FromResult<HTTPResponse?>(null);
            };

            webSocketChatServer.OnHTTPResponse                += (timestamp, server, httpRequest, httpResponse, cancellationToken) => {
                httpResponses.Add(httpResponse);
                return Task.CompletedTask;
            };

            webSocketChatServer.OnNewWebSocketConnection      += (timestamp, server, connection, eventTrackingId, sharedSubprotocols, cancellationToken) => {
                newWebSocketConnection.Add($"{newWebSocketConnection.Count}: {connection.RemoteSocket}");
                return Task.CompletedTask;
            };

            webSocketChatServer.OnWebSocketFrameReceived      += (timestamp, server, connection, eventTrackingId, requestFrame, cancellationToken) => {
                messageRequests.       Add(requestFrame);
                return Task.CompletedTask;
            };

            webSocketChatServer.OnWebSocketFrameSent          += (timestamp, server, connection, eventTrackingId, responseFrame, cancellationToken) => {
                messageResponses.      Add(responseFrame);
                return Task.CompletedTask;
            };

            webSocketChatServer.OnTextMessageReceived         += (timestamp, server, connection, frame, eventTrackingId, textMessage, cancellationToken) => {
                textMessageRequests.   Add(textMessage);
                return Task.CompletedTask;
            };

            webSocketChatServer.OnTextMessageSent             += (timestamp, server, connection, frame, eventTrackingId, textMessage, sentStatus, cancellationToken) => {
                textMessageResponses.  Add(textMessage ?? "-");
                return Task.CompletedTask;
            };

            webSocketChatServer.OnBinaryMessageReceived       += (timestamp, server, connection, frame, eventTrackingId, binaryMessage, cancellationToken) => {
                binaryMessageRequests. Add(binaryMessage);
                return Task.CompletedTask;
            };

            webSocketChatServer.OnBinaryMessageSent           += (timestamp, server, connection, frame, eventTrackingId, binaryMessage, sentStatus, cancellationToken) => {
                binaryMessageResponses.Add(binaryMessage);
                return Task.CompletedTask;
            };

            #endregion

            #region Clients setup

            var startTimestamp          = Timestamp.Now;
            var webSocketClients        = new List<WebSocketClient>();
            var httpClientResponses     = new List<HTTPResponse>();
            var exceptions              = new List<Exception>();

            var textMessageLogs         = new List<List<String>>();

            for (var i = 1; i <= numberOfClients; i++)
            {
                try
                {

                    var webSocketClient  = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{HTTPPort}"));
                    webSocketClients.Add(webSocketClient);

                    var textMessageLog = new List<String>();

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

                    textMessageLogs.Add(textMessageLog);

                    httpClientResponses.Add((await webSocketClient.Connect()).Item2);

                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }

            }

            var runTime1          = Timestamp.Now - startTimestamp;

            #endregion


            #region Check HTTP request

            // Wait a bit, because running multiple tests at once has timing issues!
            while (newWebSocketConnection.Count < numberOfClients)
                Thread.Sleep(10);

            ClassicAssert.AreEqual(numberOfClients, validatedTCP.          Count, validatedTCP.          AggregateWith(", "));
            ClassicAssert.AreEqual(numberOfClients, newTCPConnection.      Count, newTCPConnection.      AggregateWith(", "));
            ClassicAssert.AreEqual(numberOfClients, validatedWebSocket.    Count, validatedWebSocket.    AggregateWith(", "));
            ClassicAssert.AreEqual(numberOfClients, newWebSocketConnection.Count, newWebSocketConnection.AggregateWith(", "));

            ClassicAssert.AreEqual(numberOfClients, httpRequests.          Count);
            ClassicAssert.AreEqual(numberOfClients, httpResponses.         Count);
            ClassicAssert.AreEqual(numberOfClients, webSocketChatServer.WebSocketConnections.Count());


            //var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            //// GET / HTTP/1.1
            //// Host:                     127.0.0.1:101
            //// Connection:               Upgrade
            //// Upgrade:                  websocket
            //// Sec-WebSocket-Key:        /vkroMJ5bwBVW200riZKRg==
            //// Sec-WebSocket-Version:    13

            //// HTTP requests should not have a "Date"-header!
            //ClassicAssert.IsFalse (request.Contains("Date:"),                         request);
            //ClassicAssert.IsTrue  (request.Contains("GET / HTTP/1.1"),                request);
            //ClassicAssert.IsTrue  (request.Contains($"Host: 127.0.0.1:{HTTPPort}"),   request);
            //ClassicAssert.IsTrue  (request.Contains($"Upgrade: websocket"),           request);

            #endregion

            #region Check HTTP response

            //var response      = httpResponse.EntirePDU;
            //var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            //// HTTP/1.1 101 Switching Protocols
            //// Date:                     Wed, 02 Aug 2023 19:33:53 GMT
            //// Server:                   GraphDefined HTTP WebSocket Service v2.0
            //// Connection:               Upgrade
            //// Upgrade:                  websocket
            //// Sec-WebSocket-Accept:     s9FvxhRowHKxS38G/sBt7gC5qec=
            //// Sec-WebSocket-Version:    13

            //ClassicAssert.IsTrue  (response.Contains("HTTP/1.1 101 Switching Protocols"),   response);

            //ClassicAssert.AreEqual("GraphDefined HTTP WebSocket Service v2.0",             httpResponse.Server);
            //ClassicAssert.AreEqual("Upgrade",                                               httpResponse.Connection);
            //ClassicAssert.AreEqual("websocket",                                             httpResponse.Upgrade);

            #endregion


            #region Send messages

            await webSocketClients.ElementAt(0).SendTextMessage("chat::Hello world!");

            do
            {
                await Task.Delay(10);
            }
            while (textMessageLogs.Any(list => list.Count != 2));


            await webSocketClients.ElementAt(1).SendTextMessage("chat::What has happend?");

            do
            {
                await Task.Delay(10);
            }
            while (textMessageLogs.Any(list => list.Count != 3));


            await webSocketClients.ElementAt(2).SendTextMessage("chat::Have a nice day!");

            do
            {
                await Task.Delay(10);
            }
            while (textMessageLogs.Any(list => list.Count != 4));

            #endregion


            startTimestamp = Timestamp.Now;
            var exceptions2 = new List<Exception>();

            foreach (var webSocketClient in webSocketClients)
            {

                try
                {

                    await webSocketClient.Close();

                }
                catch (Exception e)
                {
                    exceptions2.Add(e);
                }

            }

            var runTime2 = Timestamp.Now - startTimestamp;

            ClassicAssert.AreEqual(0, exceptions2.Count, $"{exceptions2.Count} HTTP WebSocket closing exceptions!");

        }

        #endregion


    }

}
