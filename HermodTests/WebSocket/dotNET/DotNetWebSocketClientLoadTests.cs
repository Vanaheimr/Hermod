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

using System.Net.WebSockets;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// Tests between .NET WebSocket clients and Hermod WebSocket servers.
    /// </summary>
    [TestFixture]
    public class DotNetWebSocketClientLoadTests : AWebSocketServerTests
    {

        #region Constructor(s)

        public DotNetWebSocketClientLoadTests()
            : base(IPPort.Parse(10112))
        { }

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

            Assert.That(validatedTCP.          Count, Is.EqualTo(numberOfClients), validatedTCP.          AggregateCSV());
            Assert.That(newTCPConnection.      Count, Is.EqualTo(numberOfClients), newTCPConnection.      AggregateCSV());
            Assert.That(validatedWebSocket.    Count, Is.EqualTo(numberOfClients), validatedWebSocket.    AggregateCSV());
            Assert.That(newWebSocketConnection.Count, Is.EqualTo(numberOfClients), newWebSocketConnection.AggregateCSV());

            Assert.That(httpRequests.          Count, Is.EqualTo(numberOfClients));
            Assert.That(httpResponses.         Count, Is.EqualTo(numberOfClients));
            Assert.That(webSocketServer.WebSocketConnections.Count(), Is.EqualTo(numberOfClients));


            //var httpRequest          = httpRequests.First();
            //var request              = httpRequest.EntirePDU;

            //// GET / HTTP/1.1
            //// Host:                    127.0.0.1:111
            //// Connection:              Upgrade
            //// Upgrade:                 websocket
            //// Sec-WebSocket-Key:       +LYHhVOGskWz/0bFFcK8dQ==
            //// Sec-WebSocket-Version:   13

            //Assert.That(httpRequest.Connection, Is.EqualTo("Upgrade"));
            //Assert.That(httpRequest.Upgrade, Is.EqualTo("websocket"));

            //Assert.That(request.Contains("GET / HTTP/1.1"), Is.True, request);
            //Assert.That(request.Contains($"Host: 127.0.0.1:{HTTPPort}"), Is.True, request);
            //Assert.That(request.Contains($"Connection: Upgrade"), Is.True, request);
            //Assert.That(request.Contains($"Upgrade: websocket"), Is.True, request);
            //Assert.That(request.Contains($"Sec-WebSocket-Key:"), Is.True, request);
            //Assert.That(request.Contains($"Sec-WebSocket-Version:"), Is.True, request);

            #endregion

            #region Check HTTP response

            //var httpResponse  = httpResponses.First();
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
            //Assert.That(httpResponse.Connection, Is.EqualTo("Upgrade"));
            //Assert.That(httpResponse.Upgrade, Is.EqualTo("websocket"));

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

            //Assert.That(messageRequests. Count, Is.EqualTo(2));
            //Assert.That(messageRequests. ElementAt(0).Payload.ToUTF8String(), Is.EqualTo("1234"));
            //Assert.That(messageRequests. ElementAt(1).Payload.ToUTF8String(), Is.EqualTo("ABCD"));

            //Assert.That(messageResponses.Count, Is.EqualTo(2));
            //Assert.That(messageResponses.ElementAt(0).Payload.ToUTF8String(), Is.EqualTo("4321"));
            //Assert.That(messageResponses.ElementAt(1).Payload.ToUTF8String(), Is.EqualTo("DCBA"));


            //Assert.That(textMessageRequests.   Count, Is.EqualTo(1));
            //Assert.That(textMessageRequests.   ElementAt(0), Is.EqualTo("1234"));
            //Assert.That(binaryMessageRequests. Count, Is.EqualTo(1));
            //Assert.That(binaryMessageRequests. ElementAt(0).ToUTF8String(), Is.EqualTo("ABCD"));

            //Assert.That(textMessageResponses.  Count, Is.EqualTo(1));
            //Assert.That(textMessageResponses.  ElementAt(0), Is.EqualTo("4321"));
            //Assert.That(binaryMessageResponses.Count, Is.EqualTo(1));
            //Assert.That(binaryMessageResponses.ElementAt(0).ToUTF8String(), Is.EqualTo("DCBA"));

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

            Assert.That(exceptions2.Count, Is.EqualTo(0), $"{exceptions2.Count} HTTP WebSocket closing exceptions!");


        }

        #endregion


    }

}
