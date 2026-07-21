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
using System.Net;
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
    public class DotNetWebSocketClientTests : AWebSocketServerTests
    {

        #region Constructor(s)

        public DotNetWebSocketClientTests()
            : base(IPPort.Parse(10111))
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
            var framesReceived          = new List<WebSocketFrame>();
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
                framesReceived.       Add(requestFrame);
                return Task.CompletedTask;
            };

            webSocketServer.OnWebSocketFrameSent          += (timestamp, server, connection, eventTrackingId, responseFrame, cancellationToken) => {
                messageResponses.      Add(responseFrame);
                return Task.CompletedTask;
            };

            //webSocketServer.OnTextMessage       += (timestamp, server, connection, frame, eventTrackingId, textMessage, cancellationToken) => {
            //    textMessageRequests.   Add(textMessage);
            //    return Task.CompletedTask;
            //};

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

            Assert.That(validatedTCP.          Count, Is.EqualTo(1), validatedTCP.          AggregateCSV());
            Assert.That(newTCPConnection.      Count, Is.EqualTo(1), newTCPConnection.      AggregateCSV());
            Assert.That(validatedWebSocket.    Count, Is.EqualTo(1), validatedWebSocket.    AggregateCSV());
            Assert.That(newWebSocketConnection.Count, Is.EqualTo(1), newWebSocketConnection.AggregateCSV());

            Assert.That(httpRequests.          Count, Is.EqualTo(1));
            Assert.That(httpResponses.         Count, Is.EqualTo(1));
            Assert.That(webSocketServer.WebSocketConnections.Count(), Is.EqualTo(1));


            var httpRequest          = httpRequests.First();
            var request              = httpRequest.EntirePDU;

            // GET / HTTP/1.1
            // Host:                    127.0.0.1:111
            // Connection:              Upgrade
            // Upgrade:                 websocket
            // Sec-WebSocket-Key:       +LYHhVOGskWz/0bFFcK8dQ==
            // Sec-WebSocket-Version:   13

            Assert.That(httpRequest.Connection,                            Is.EqualTo(ConnectionType.Upgrade));
            Assert.That(httpRequest.Upgrade,                               Is.EqualTo("websocket"));

            Assert.That(request.Contains("GET / HTTP/1.1"),                Is.True, request);
            Assert.That(request.Contains($"Host: 127.0.0.1:{HTTPPort}"),   Is.True, request);
            Assert.That(request.Contains($"Connection: Upgrade"),          Is.True, request);
            Assert.That(request.Contains($"Upgrade: websocket"),           Is.True, request);
            Assert.That(request.Contains($"Sec-WebSocket-Key:"),           Is.True, request);
            Assert.That(request.Contains($"Sec-WebSocket-Version:"),       Is.True, request);

            #endregion

            #region Check HTTP response

            var httpResponse  = httpResponses.First();
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

            Assert.That(httpResponse.Server,       Is.EqualTo("GraphDefined HTTP WebSocket Service v2.0"));
            Assert.That(httpResponse.Connection,   Is.EqualTo(ConnectionType.Upgrade));
            Assert.That(httpResponse.Upgrade,      Is.EqualTo("websocket"));

            #endregion


            #region Send text message

            await clientWebSocket.SendAsync(buffer:             new ArraySegment<Byte>(Encoding.UTF8.GetBytes("1234")),
                                            messageType:        WebSocketMessageType.Text,
                                            endOfMessage:       true,
                                            cancellationToken:  CancellationToken.None);


            // The WebSocket frame was received by the WebSocket server
            var sleepCounter = 0;
            while (framesReceived.Count == 0 && sleepCounter < 50)
            {
                Thread.Sleep(10);
                sleepCounter++;
            }

            Assert.That(framesReceived.Count,   Is.GreaterThan(0), $"No WebSocket frame was received by the WebSocket server after {sleepCounter * 10} ms!");


            // The text message was received by the WebSocket server
            sleepCounter = 0;
            while (textMessageRequests.Count == 0 && sleepCounter < 50)
            {
                Thread.Sleep(10);
                sleepCounter++;
            }

            Assert.That(textMessageRequests.Count,   Is.GreaterThan(0), $"No text message was received by the WebSocket server after {sleepCounter * 10} ms!");


            // The response is received from the WebSocket server
            sleepCounter = 0;
            while (textMessageResponses.Count == 0 && sleepCounter < 50)
            {
                Thread.Sleep(10);
                sleepCounter++;
            }

            Assert.That(textMessageResponses.Count,   Is.GreaterThan(0), $"No text message response received after {sleepCounter * 10} ms!");

            #endregion

            #region Send binary message

            await clientWebSocket.SendAsync(buffer:             new ArraySegment<Byte>(Encoding.UTF8.GetBytes("ABCD")),
                                            messageType:        WebSocketMessageType.Binary,
                                            endOfMessage:       true,
                                            cancellationToken:  CancellationToken.None);

            sleepCounter = 0;
            while (binaryMessageResponses.Count == 0 && sleepCounter < 50)
            {
                Thread.Sleep(10);
                sleepCounter++;
            }

            Assert.That(binaryMessageResponses.Count,   Is.GreaterThan(0), $"No binary message response received after {sleepCounter * 10} ms!");

            #endregion

            #region Validate message delivery

            Assert.That(framesReceived. Count, Is.EqualTo(2));
            Assert.That(framesReceived. ElementAt(0).Payload.ToUTF8String(), Is.EqualTo("1234"));
            Assert.That(framesReceived. ElementAt(1).Payload.ToUTF8String(), Is.EqualTo("ABCD"));

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


            try
            {

                await clientWebSocket.CloseAsync(
                          WebSocketCloseStatus.NormalClosure,
                          "Done",
                          CancellationToken.None
                      );

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

            webSocketServer.OnValidateWebSocketConnection += (timestamp, server, connection, eventTrackingId,                cancellationToken) => {
                validatedWebSocket.Add($"{validatedWebSocket.Count}: {connection.RemoteSocket}");
                return Task.FromResult<HTTPResponse?>(null);
            };

            webSocketServer.OnHTTPResponse                += (timestamp, server, httpRequest, httpResponse,                  cancellationToken) => {
                httpResponses.Add(httpResponse);
                return Task.CompletedTask;
            };

            webSocketServer.OnNewWebSocketConnection      += (timestamp, server, connection, eventTrackingId, sharedSubprotocols, selectedSubprotocol, cancellationToken) => {
                newWebSocketConnection.Add($"{newWebSocketConnection.Count}: {connection.RemoteSocket}");
                return Task.CompletedTask;
            };

            webSocketServer.OnWebSocketFrameReceived      += (timestamp, server, connection, eventTrackingId, requestFrame,  cancellationToken) => {
                messageRequests.       Add(requestFrame);
                return Task.CompletedTask;
            };

            webSocketServer.OnWebSocketFrameSent          += (timestamp, server, connection, eventTrackingId, responseFrame, cancellationToken) => {
                messageResponses.      Add(responseFrame);
                return Task.CompletedTask;
            };

            webSocketServer.OnTextMessageReceived         += (timestamp, server, connection, frame, eventTrackingId, textMessage,   cancellationToken) => {
                textMessageRequests.   Add(textMessage);
                return Task.CompletedTask;
            };

            webSocketServer.OnTextMessageSent             += (timestamp, server, connection, frame, eventTrackingId, textMessage,   sentStatus, cancellationToken) => {
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

            Assert.That(validatedTCP.          Count, Is.EqualTo(1), validatedTCP.          AggregateCSV());
            Assert.That(newTCPConnection.      Count, Is.EqualTo(1), newTCPConnection.      AggregateCSV());
            Assert.That(validatedWebSocket.    Count, Is.EqualTo(1), validatedWebSocket.    AggregateCSV());
            Assert.That(newWebSocketConnection.Count, Is.EqualTo(1), newWebSocketConnection.AggregateCSV());

            Assert.That(httpRequests.          Count, Is.EqualTo(1));
            Assert.That(httpResponses.         Count, Is.EqualTo(1));
            Assert.That(webSocketServer.WebSocketConnections.Count(), Is.EqualTo(1));


            var httpRequest          = httpRequests.First();
            var request              = httpRequest.EntirePDU;

            // GET / HTTP/1.1
            // Host:                     127.0.0.1:111
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Key:        +LYHhVOGskWz/0bFFcK8dQ==
            // Sec-WebSocket-Version:    13
            // Sec-WebSocket-Protocol:   ocpp1.6

            Assert.That(httpRequest.Connection,                            Is.EqualTo(ConnectionType.Upgrade));
            Assert.That(httpRequest.Upgrade,                               Is.EqualTo("websocket"));

            Assert.That(request.Contains("GET / HTTP/1.1"),                Is.True, request);
            Assert.That(request.Contains($"Host: 127.0.0.1:{HTTPPort}"),   Is.True, request);
            Assert.That(request.Contains($"Connection: Upgrade"),          Is.True, request);
            Assert.That(request.Contains($"Upgrade: websocket"),           Is.True, request);
            Assert.That(request.Contains($"Sec-WebSocket-Key:"),           Is.True, request);
            Assert.That(request.Contains($"Sec-WebSocket-Version:"),       Is.True, request);

            #endregion

            #region Check HTTP response

            var httpResponse  = httpResponses.First();
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

            Assert.That(validatedTCP.          Count, Is.EqualTo(1), validatedTCP.          AggregateCSV());
            Assert.That(newTCPConnection.      Count, Is.EqualTo(1), newTCPConnection.      AggregateCSV());
            Assert.That(validatedWebSocket.    Count, Is.EqualTo(1), validatedWebSocket.    AggregateCSV());
            Assert.That(newWebSocketConnection.Count, Is.EqualTo(1), newWebSocketConnection.AggregateCSV());

            Assert.That(httpRequests.          Count, Is.EqualTo(1));
            Assert.That(httpResponses.         Count, Is.EqualTo(1));
            Assert.That(webSocketServer.WebSocketConnections.Count(), Is.EqualTo(1));


            var httpRequest          = httpRequests.First();
            var request              = httpRequest.EntirePDU;

            // GET / HTTP/1.1
            // Host:                     127.0.0.1:111
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Key:        +LYHhVOGskWz/0bFFcK8dQ==
            // Sec-WebSocket-Version:    13
            // Sec-WebSocket-Protocol:   ocpp1.6

            Assert.That(httpRequest.Connection,   Is.EqualTo(ConnectionType.Upgrade));
            Assert.That(httpRequest.Upgrade,      Is.EqualTo("websocket"));

            Assert.That(request.Contains("GET / HTTP/1.1"), Is.True, request);
            Assert.That(request.Contains($"Host: 127.0.0.1:{HTTPPort}"), Is.True, request);
            Assert.That(request.Contains($"Connection: Upgrade"), Is.True, request);
            Assert.That(request.Contains($"Upgrade: websocket"), Is.True, request);
            Assert.That(request.Contains($"Sec-WebSocket-Key:"), Is.True, request);
            Assert.That(request.Contains($"Sec-WebSocket-Version:"), Is.True, request);

            #endregion

            #region Check HTTP response

            var httpResponse  = httpResponses.First();
            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 101 Switching Protocols
            // Date:                     Wed, 02 Aug 2023 19:33:53 GMT
            // Server:                   GraphDefined HTTP WebSocket Service v2.0
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Accept:     s9FvxhRowHKxS38G/sBt7gC5qec=
            // Sec-WebSocket-Protocol:   ocpp1.6
            // Sec-WebSocket-Version:    13

            Assert.That(response.Contains("HTTP/1.1 101 Switching Protocols"), Is.True, response);

            Assert.That(httpResponse.Server,       Is.EqualTo("GraphDefined HTTP WebSocket Service v2.0"));
            Assert.That(httpResponse.Connection,   Is.EqualTo(ConnectionType.Upgrade));
            Assert.That(httpResponse.Upgrade,      Is.EqualTo("websocket"));

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

            webSocketServer.AddSecWebSocketProtocol("ocpp1.6");
            webSocketServer.AddSecWebSocketProtocol("ocpp2.0");

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

            Assert.That(validatedTCP.          Count, Is.EqualTo(1), validatedTCP.          AggregateCSV());
            Assert.That(newTCPConnection.      Count, Is.EqualTo(1), newTCPConnection.      AggregateCSV());
            Assert.That(validatedWebSocket.    Count, Is.EqualTo(1), validatedWebSocket.    AggregateCSV());
            Assert.That(newWebSocketConnection.Count, Is.EqualTo(1), newWebSocketConnection.AggregateCSV());

            Assert.That(httpRequests.          Count, Is.EqualTo(1));
            Assert.That(httpResponses.         Count, Is.EqualTo(1));
            Assert.That(webSocketServer.WebSocketConnections.Count(), Is.EqualTo(1));


            var httpRequest          = httpRequests.First();
            var request              = httpRequest.EntirePDU;

            // GET / HTTP/1.1
            // Host:                     127.0.0.1:111
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Key:        +LYHhVOGskWz/0bFFcK8dQ==
            // Sec-WebSocket-Version:    13
            // Sec-WebSocket-Protocol:   ocpp2.0

            Assert.That(httpRequest.Connection,                            Is.EqualTo(ConnectionType.Upgrade));
            Assert.That(httpRequest.Upgrade,                               Is.EqualTo("websocket"));

            Assert.That(request.Contains("GET / HTTP/1.1"),                Is.True, request);
            Assert.That(request.Contains($"Host: 127.0.0.1:{HTTPPort}"),   Is.True, request);
            Assert.That(request.Contains($"Connection: Upgrade"),          Is.True, request);
            Assert.That(request.Contains($"Upgrade: websocket"),           Is.True, request);
            Assert.That(request.Contains($"Sec-WebSocket-Key:"),           Is.True, request);
            Assert.That(request.Contains($"Sec-WebSocket-Version:"),       Is.True, request);

            #endregion

            #region Check HTTP response

            var httpResponse  = httpResponses.First();
            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 101 Switching Protocols
            // Date:                     Wed, 02 Aug 2023 19:33:53 GMT
            // Server:                   GraphDefined HTTP WebSocket Service v2.0
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Accept:     s9FvxhRowHKxS38G/sBt7gC5qec=
            // Sec-WebSocket-Protocol:   ocpp2.0
            // Sec-WebSocket-Version:    13

            Assert.That(response.Contains("HTTP/1.1 101 Switching Protocols"), Is.True, response);

            Assert.That(httpResponse.Server,       Is.EqualTo("GraphDefined HTTP WebSocket Service v2.0"));
            Assert.That(httpResponse.Connection,   Is.EqualTo(ConnectionType.Upgrade));
            Assert.That(httpResponse.Upgrade,      Is.EqualTo("websocket"));

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

            Assert.That(validatedTCP.          Count, Is.EqualTo(1), validatedTCP.          AggregateCSV());
            Assert.That(newTCPConnection.      Count, Is.EqualTo(1), newTCPConnection.      AggregateCSV());
            Assert.That(validatedWebSocket.    Count, Is.EqualTo(1), validatedWebSocket.    AggregateCSV());
            Assert.That(newWebSocketConnection.Count, Is.EqualTo(1), newWebSocketConnection.AggregateCSV());

            Assert.That(httpRequests.          Count, Is.EqualTo(1));
            Assert.That(httpResponses.         Count, Is.EqualTo(1));
            Assert.That(webSocketServer.WebSocketConnections.Count(), Is.EqualTo(1));


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

            Assert.That(httpRequest.Connection,   Is.EqualTo(ConnectionType.Upgrade));
            Assert.That(httpRequest.Upgrade,      Is.EqualTo("websocket"));
            Assert.That(httpRequest.Authorization is HTTPBasicAuthentication, Is.True, "Is not HTTP Basic Authentication!");
            Assert.That((httpRequest.Authorization as HTTPBasicAuthentication)?.Username, Is.EqualTo("username"));
            Assert.That((httpRequest.Authorization as HTTPBasicAuthentication)?.Password, Is.EqualTo("password"));

            Assert.That(request.Contains("GET / HTTP/1.1"), Is.True, request);
            Assert.That(request.Contains($"Host: 127.0.0.1:{HTTPPort}"), Is.True, request);
            Assert.That(request.Contains($"Connection: Upgrade"), Is.True, request);
            Assert.That(request.Contains($"Upgrade: websocket"), Is.True, request);
            Assert.That(request.Contains($"Sec-WebSocket-Key:"), Is.True, request);
            Assert.That(request.Contains($"Sec-WebSocket-Version:"), Is.True, request);

            #endregion

            #region Check HTTP response

            var httpResponse  = httpResponses.First();
            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 101 Switching Protocols
            // Date:                     Wed, 02 Aug 2023 19:33:53 GMT
            // Server:                   GraphDefined HTTP WebSocket Service v2.0
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Accept:     s9FvxhRowHKxS38G/sBt7gC5qec=
            // Sec-WebSocket-Protocol:   ocpp2.0
            // Sec-WebSocket-Version:    13

            Assert.That(response.Contains("HTTP/1.1 101 Switching Protocols"), Is.True, response);

            Assert.That(httpResponse.Server,       Is.EqualTo("GraphDefined HTTP WebSocket Service v2.0"));
            Assert.That(httpResponse.Connection,   Is.EqualTo(ConnectionType.Upgrade));
            Assert.That(httpResponse.Upgrade,      Is.EqualTo("websocket"));

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
                               new HTTPResponse.Builder(connection.HTTPRequest!) {
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


            var clientWebSocket = new ClientWebSocket();

            // Will only work, when the server sends back a "401 Unauthorized" and a "WWW-Authenticate" header for the first request!
            clientWebSocket.Options.Credentials = new NetworkCredential("username", "password");

            try
            {
                await clientWebSocket.ConnectAsync(new Uri($"ws://127.0.0.1:{HTTPPort}"), CancellationToken.None);
            }
            catch (Exception e)
            {
                Assert.That(e.Message, Is.EqualTo("The server returned status code '401' when status code '101' was expected."));
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
            Assert.That(validatedTCP.          Count, Is.EqualTo(2), validatedTCP.          AggregateCSV());
            Assert.That(newTCPConnection.      Count, Is.EqualTo(2), newTCPConnection.      AggregateCSV());
            Assert.That(validatedWebSocket.    Count, Is.EqualTo(1), validatedWebSocket.    AggregateCSV());
            Assert.That(newWebSocketConnection.Count, Is.EqualTo(1), newWebSocketConnection.AggregateCSV());

            Assert.That(httpRequests.          Count, Is.EqualTo(2));
            Assert.That(httpResponses.         Count, Is.EqualTo(2));
            Assert.That(webSocketServer.WebSocketConnections.Count(), Is.EqualTo(1));


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

            Assert.That(httpRequest2.Connection, Is.EqualTo(ConnectionType.Upgrade));
            Assert.That(httpRequest2.Upgrade, Is.EqualTo("websocket"));
            Assert.That(httpRequest2.Authorization is HTTPBasicAuthentication, Is.True, "Is not HTTP Basic Authentication!");
            Assert.That((httpRequest2.Authorization as HTTPBasicAuthentication)?.Username, Is.EqualTo("username"));
            Assert.That((httpRequest2.Authorization as HTTPBasicAuthentication)?.Password, Is.EqualTo("password"));

            Assert.That(request2.Contains("GET / HTTP/1.1"), Is.True, request2);
            Assert.That(request2.Contains($"Host: 127.0.0.1:{HTTPPort}"), Is.True, request2);
            Assert.That(request2.Contains($"Connection: Upgrade"), Is.True, request2);
            Assert.That(request2.Contains($"Upgrade: websocket"), Is.True, request2);
            Assert.That(request2.Contains($"Sec-WebSocket-Key:"), Is.True, request2);
            Assert.That(request2.Contains($"Sec-WebSocket-Version:"), Is.True, request2);

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
            // Server:                   GraphDefined HTTP WebSocket Service v2.0
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Accept:     s9FvxhRowHKxS38G/sBt7gC5qec=
            // Sec-WebSocket-Protocol:   ocpp2.0
            // Sec-WebSocket-Version:    13

            Assert.That(response2.Contains("HTTP/1.1 101 Switching Protocols"), Is.True, response2);

            Assert.That(httpResponse2.Server, Is.EqualTo("GraphDefined HTTP WebSocket Service v2.0"));
            Assert.That(httpResponse2.Connection, Is.EqualTo(ConnectionType.Upgrade));
            Assert.That(httpResponse2.Upgrade, Is.EqualTo("websocket"));

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
                               new HTTPResponse.Builder(connection.HTTPRequest!) {
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


            var clientWebSocket = new ClientWebSocket();

            // Will only work, when the server sends back a "401 Unauthorized" and a "WWW-Authenticate" header for the first request!
            clientWebSocket.Options.Credentials = new NetworkCredential("nameOfUser", "passphrase");

            try
            {
                await clientWebSocket.ConnectAsync(new Uri($"ws://127.0.0.1:{HTTPPort}"), CancellationToken.None);
            }
            catch (Exception e)
            {
                Assert.That(e.Message, Is.EqualTo("The server returned status code '401' when status code '101' was expected."));
            }

            var state                = clientWebSocket.State;
            var closeStatus          = clientWebSocket.CloseStatus;
            var options              = clientWebSocket.Options;
            var subProtocol          = clientWebSocket.SubProtocol;
            var httpStatusCode       = clientWebSocket.HttpStatusCode;
            var httpResponseHeaders  = clientWebSocket.HttpResponseHeaders;


            #region Check HTTP request

            Assert.That(
                SpinWait.SpinUntil(() => httpResponses.Count == 2, TimeSpan.FromSeconds(5)),
                Is.True,
                "Timed out waiting for the HTTP 401 responses."
            );

            // 2 because of the way .NET handles HTTP authentication!
            Assert.That(validatedTCP.          Count, Is.EqualTo(2), validatedTCP.          AggregateCSV());
            Assert.That(newTCPConnection.      Count, Is.EqualTo(2), newTCPConnection.      AggregateCSV());
            Assert.That(validatedWebSocket.    Count, Is.EqualTo(0), validatedWebSocket.    AggregateCSV());
            Assert.That(newWebSocketConnection.Count, Is.EqualTo(0), newWebSocketConnection.AggregateCSV());

            Assert.That(httpRequests.          Count, Is.EqualTo(2));
            Assert.That(httpResponses.         Count, Is.EqualTo(2));
            Assert.That(
                SpinWait.SpinUntil(() => !webSocketServer.WebSocketConnections.Any(), TimeSpan.FromSeconds(5)),
                Is.True,
                "Timed out waiting for rejected WebSocket connections to close."
            );


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

            Assert.That(httpRequest2.Connection, Is.EqualTo(ConnectionType.Upgrade));
            Assert.That(httpRequest2.Upgrade, Is.EqualTo("websocket"));
            Assert.That(httpRequest2.Authorization is HTTPBasicAuthentication, Is.True, "Is not HTTP Basic Authentication!");
            Assert.That((httpRequest2.Authorization as HTTPBasicAuthentication)?.Username, Is.EqualTo("nameOfUser"));
            Assert.That((httpRequest2.Authorization as HTTPBasicAuthentication)?.Password, Is.EqualTo("passphrase"));

            Assert.That(request2.Contains("GET / HTTP/1.1"), Is.True, request2);
            Assert.That(request2.Contains($"Host: 127.0.0.1:{HTTPPort}"), Is.True, request2);
            Assert.That(request2.Contains($"Connection: Upgrade"), Is.True, request2);
            Assert.That(request2.Contains($"Upgrade: websocket"), Is.True, request2);
            Assert.That(request2.Contains($"Sec-WebSocket-Key:"), Is.True, request2);
            Assert.That(request2.Contains($"Sec-WebSocket-Version:"), Is.True, request2);
            Assert.That(request2.Contains($"Authorization: Basic bmFtZU9mVXNlcjpwYXNzcGhyYXNl"), Is.True, request2);

            #endregion

            #region Check HTTP response

            var httpResponse1  = httpResponses.First();
            var response1      = httpResponse1.EntirePDU;
            var httpBody1      = httpResponse1.HTTPBodyAsUTF8String;

            // HTTP/1.1 401 Unauthorized
            // Date:               Thu, 03 Aug 2023 22:38:42 GMT
            // WWW-Authenticate:   Basic realm="Access to the WebSocket server", charset="UTF-8"
            // Connection:         Close



            var httpResponse2  = httpResponses.ElementAt(1);
            var response2      = httpResponse2.EntirePDU;
            var httpBody2      = httpResponse2.HTTPBodyAsUTF8String;

            // HTTP/1.1 401 Unauthorized
            // Date:               Thu, 03 Aug 2023 22:38:43 GMT
            // WWW-Authenticate:   Basic realm="Access to the WebSocket server", charset="UTF-8"
            // Connection:         Close

            Assert.That(response2.Contains("HTTP/1.1 401 Unauthorized"), Is.True, response2);

            Assert.That(httpResponse2.Server, Is.EqualTo("GraphDefined HTTP WebSocket Service v2.0"));
            Assert.That(httpResponse2.WWWAuthenticate?.ToString(), Is.EqualTo("Basic realm=\"Access to the WebSocket server\", charset=\"UTF-8\""));
            Assert.That(httpResponse2.Connection, Is.EqualTo(ConnectionType.Close));

            #endregion

        }

        #endregion


    }

}
