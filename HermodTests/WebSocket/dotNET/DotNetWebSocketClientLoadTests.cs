﻿/*
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
                return Task.FromResult<Boolean?>(true);
            };

            webSocketServer.OnNewTCPConnection            += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newTCPConnection.Add($"{newTCPConnection.Count}: {connection.RemoteSocket}");
                return Task.CompletedTask;
            };

            webSocketServer.OnHTTPRequest                 += (timestamp, server, httpRequest) => {
                httpRequests.Add(httpRequest);
                return Task.CompletedTask;
            };

            webSocketServer.OnValidateWebSocketConnection += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedWebSocket.Add($"{validatedWebSocket.Count}: {connection.RemoteSocket}");
                return Task.FromResult<HTTPResponse?>(null);
            };

            webSocketServer.OnHTTPResponse                += (timestamp, server, httpRequest, httpResponse) => {
                httpResponses.Add(httpResponse);
                return Task.CompletedTask;
            };

            webSocketServer.OnNewWebSocketConnection      += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newWebSocketConnection.Add($"{newWebSocketConnection.Count}: {connection.RemoteSocket}");
                return Task.CompletedTask;
            };

            webSocketServer.OnWebSocketFrameReceived      += (timestamp, server, connection, eventTrackingId, requestFrame) => {
                messageRequests.       Add(requestFrame);
                return Task.CompletedTask;
            };

            webSocketServer.OnWebSocketFrameSent          += (timestamp, server, connection, eventTrackingId, responseFrame) => {
                messageResponses.      Add(responseFrame);
                return Task.CompletedTask;
            };

            webSocketServer.OnTextMessageReceived         += (timestamp, server, connection, eventTrackingId, textMessage) => {
                textMessageRequests.   Add(textMessage);
                return Task.CompletedTask;
            };

            webSocketServer.OnTextMessageSent             += (timestamp, server, connection, eventTrackingId, textMessage) => {
                textMessageResponses.  Add(textMessage ?? "-");
                return Task.CompletedTask;
            };

            webSocketServer.OnBinaryMessageReceived       += (timestamp, server, connection, eventTrackingId, binaryMessage) => {
                binaryMessageRequests. Add(binaryMessage);
                return Task.CompletedTask;
            };

            webSocketServer.OnBinaryMessageSent           += (timestamp, server, connection, eventTrackingId, binaryMessage) => {
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