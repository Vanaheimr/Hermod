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

using System.Diagnostics;
using System.Collections.Concurrent;

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

        #region Data

        /// <summary>
        /// How long a test waits for the server to have taken on its clients, or for a message to have gone round.
        /// </summary>
        private static readonly TimeSpan  Patience  = TimeSpan.FromSeconds(30);

        #endregion


        #region (private static) Until(Condition, Expected)

        /// <summary>
        /// Wait until the condition holds, for as long as the patience allows, and fail with what was expected otherwise.
        /// </summary>
        /// <param name="Condition">What is waited for.</param>
        /// <param name="Expected">What was expected, as it is said when it did not come.</param>
        private static async Task Until(Func<Boolean>  Condition,
                                        Func<String>   Expected)
        {

            var waited = Stopwatch.StartNew();

            while (!Condition() && waited.Elapsed < Patience)
                await Task.Delay(10);

            Assert.That(Condition(), Is.True, () => $"Waited {Patience.TotalSeconds} seconds for {Expected()}.");

        }

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

            var validatedTCP            = new ConcurrentQueue<String>();
            var newTCPConnection        = new ConcurrentQueue<String>();
            var validatedWebSocket      = new ConcurrentQueue<String>();
            var newWebSocketConnection  = new ConcurrentQueue<String>();
            var httpRequests            = new ConcurrentQueue<HTTPRequest>();
            var httpResponses           = new ConcurrentQueue<HTTPResponse>();
            var messageRequests         = new ConcurrentQueue<WebSocketFrame>();
            var messageResponses        = new ConcurrentQueue<WebSocketFrame>();
            var textMessageRequests     = new ConcurrentQueue<String>();
            var textMessageResponses    = new ConcurrentQueue<String>();
            var binaryMessageRequests   = new ConcurrentQueue<Byte[]>();
            var binaryMessageResponses  = new ConcurrentQueue<Byte[]>();

            webSocketChatServer.OnValidateTCPConnection       += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedTCP.Enqueue($"{validatedTCP.Count}: {connection.Client.RemoteEndPoint?.ToString() ?? "-"}");
                return Task.FromResult(ConnectionFilterResponse.Accepted());
            };

            webSocketChatServer.OnNewTCPConnection            += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newTCPConnection.Enqueue($"{newTCPConnection.Count}: {connection.RemoteSocket}");
                return Task.CompletedTask;
            };

            webSocketChatServer.OnHTTPRequest                 += (timestamp, server, httpRequest, cancellationToken) => {
                httpRequests.Enqueue(httpRequest);
                return Task.CompletedTask;
            };

            webSocketChatServer.OnValidateWebSocketConnection += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedWebSocket.Enqueue($"{validatedWebSocket.Count}: {connection.RemoteSocket}");
                return Task.FromResult<HTTPResponse?>(null);
            };

            webSocketChatServer.OnHTTPResponse                += (timestamp, server, httpRequest, httpResponse, cancellationToken) => {
                httpResponses.Enqueue(httpResponse);
                return Task.CompletedTask;
            };

            webSocketChatServer.OnNewWebSocketConnection      += (timestamp, server, connection, eventTrackingId, sharedSubprotocols, selectedSubprotocol, cancellationToken) => {
                newWebSocketConnection.Enqueue($"{newWebSocketConnection.Count}: {connection.RemoteSocket}");
                return Task.CompletedTask;
            };

            webSocketChatServer.OnWebSocketFrameReceived      += (timestamp, server, connection, eventTrackingId, requestFrame, cancellationToken) => {
                messageRequests.       Enqueue(requestFrame);
                return Task.CompletedTask;
            };

            webSocketChatServer.OnWebSocketFrameSent          += (timestamp, server, connection, eventTrackingId, responseFrame, cancellationToken) => {
                messageResponses.      Enqueue(responseFrame);
                return Task.CompletedTask;
            };

            //webSocketChatServer.OnTextMessageReceived         += (timestamp, server, connection, frame, eventTrackingId, textMessage, cancellationToken) => {
            //    textMessageRequests.   Enqueue(textMessage);
            //    return Task.CompletedTask;
            //};

            webSocketChatServer.OnTextMessageSent             += (timestamp, server, connection, frame, eventTrackingId, textMessage, sentStatus, cancellationToken) => {
                textMessageResponses.  Enqueue(textMessage ?? "-");
                return Task.CompletedTask;
            };

            //webSocketChatServer.OnBinaryMessageReceived       += (timestamp, server, connection, frame, eventTrackingId, binaryMessage, cancellationToken) => {
            //    binaryMessageRequests. Enqueue(binaryMessage);
            //    return Task.CompletedTask;
            //};

            webSocketChatServer.OnBinaryMessageSent           += (timestamp, server, connection, frame, eventTrackingId, binaryMessage, sentStatus, cancellationToken) => {
                binaryMessageResponses.Enqueue(binaryMessage);
                return Task.CompletedTask;
            };

            #endregion

            #region Clients setup

            var startTimestamp          = Timestamp.Now;
            var webSocketClients        = new List<WebSocketClient>();
            var httpClientResponses     = new List<HTTPResponse>();
            var exceptions              = new List<Exception>();

            var textMessageLogs         = new List<ConcurrentQueue<String>>();

            for (var i = 1; i <= numberOfClients; i++)
            {
                try
                {

                    var webSocketClient  = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{HTTPPort}"));
                    webSocketClients.Add(webSocketClient);

                    var textMessageLog = new ConcurrentQueue<String>();

                    webSocketClient.OnTextMessageReceived += (timestamp,
                                                              webSocketClient,
                                                              webSocketClientConnection,
                                                              webSocketFrame,
                                                              eventTrackingId,
                                                              textMessage,
                                                              cancellationToken) => {

                        textMessageLog.Enqueue(textMessage);

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

            // A client that could not connect is never taken on: that is said here, not waited for.
            Assert.That(exceptions, Is.Empty, $"{exceptions.Count} of {numberOfClients} clients could not connect: {exceptions.Select(exception => exception.Message).AggregateCSV()}");

            // Wait a bit, because running multiple tests at once has timing issues - but not without
            // end: a connection the server does not report is a failure the asserts below name.
            var waited = Stopwatch.StartNew();

            while (newWebSocketConnection.Count < numberOfClients && waited.Elapsed < Patience)
                await Task.Delay(10);

            Assert.That(validatedTCP.          Count, Is.EqualTo(numberOfClients), validatedTCP.          AggregateCSV());
            Assert.That(newTCPConnection.      Count, Is.EqualTo(numberOfClients), newTCPConnection.      AggregateCSV());
            Assert.That(validatedWebSocket.    Count, Is.EqualTo(numberOfClients), validatedWebSocket.    AggregateCSV());
            Assert.That(newWebSocketConnection.Count, Is.EqualTo(numberOfClients), newWebSocketConnection.AggregateCSV());

            Assert.That(httpRequests.          Count, Is.EqualTo(numberOfClients));
            Assert.That(httpResponses.         Count, Is.EqualTo(numberOfClients));
            Assert.That(webSocketChatServer.WebSocketConnections.Count(), Is.EqualTo(numberOfClients));

            //var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            //// GET / HTTP/1.1
            //// Host:                     127.0.0.1:101
            //// Connection:               Upgrade
            //// Upgrade:                  websocket
            //// Sec-WebSocket-Key:        /vkroMJ5bwBVW200riZKRg==
            //// Sec-WebSocket-Version:    13

            //// HTTP requests should not have a "Date"-header!
            //Assert.That(request.Contains("Date:"), Is.False, request);
            //Assert.That(request.Contains("GET / HTTP/1.1"), Is.True, request);
            //Assert.That(request.Contains($"Host: 127.0.0.1:{HTTPPort}"), Is.True, request);
            //Assert.That(request.Contains($"Upgrade: websocket"), Is.True, request);

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

            //Assert.That(response.Contains("HTTP/1.1 101 Switching Protocols"), Is.True, response);

            //Assert.That(httpResponse.Server, Is.EqualTo("GraphDefined HTTP WebSocket Service v2.0"));
            //Assert.That(httpResponse.Connection, Is.EqualTo(ConnectionType.Upgrade));
            //Assert.That(httpResponse.Upgrade, Is.EqualTo("websocket"));

            #endregion

            #region Send messages

            await webSocketClients.ElementAt(0).SendTextMessage("chat::Hello world!");

            await Until(() => textMessageLogs.All(list => list.Count == 2),
                        () => $"every client to have heard 2 messages, but they heard {textMessageLogs.Select(list => list.Count).AggregateCSV()}");

            await webSocketClients.ElementAt(1).SendTextMessage("chat::What has happend?");

            await Until(() => textMessageLogs.All(list => list.Count == 3),
                        () => $"every client to have heard 3 messages, but they heard {textMessageLogs.Select(list => list.Count).AggregateCSV()}");

            await webSocketClients.ElementAt(2).SendTextMessage("chat::Have a nice day!");

            await Until(() => textMessageLogs.All(list => list.Count == 4),
                        () => $"every client to have heard 4 messages, but they heard {textMessageLogs.Select(list => list.Count).AggregateCSV()}");

            #endregion

            startTimestamp = Timestamp.Now;
            var exceptions2 = new List<Exception>();

            foreach (var webSocketClient in webSocketClients)
            {

                try
                {

                    await webSocketClient.DisposeAsync();

                }
                catch (Exception e)
                {
                    exceptions2.Add(e);
                }

            }

            var runTime2 = Timestamp.Now - startTimestamp;

            Assert.That(exceptions2.Count, Is.EqualTo(0), $"{exceptions2.Count} HTTP WebSocket closing exceptions!");

        }

        #endregion

    }

}
