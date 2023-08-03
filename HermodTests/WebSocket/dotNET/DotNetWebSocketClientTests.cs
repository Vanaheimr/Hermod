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


        #region Test_001()

        [Test]
        public async Task Test_001()
        {

            if (webSocketServer is null) {
                Assert.Fail("WebSocketServer is null!");
                return;
            }

            var validatedTCP            = false;
            var newTCPConnection        = false;
            var validatedWebSocket      = false;
            var newWebSocketConnection  = false;
            var httpRequests            = new List<HTTPRequest>();
            var httpResponses           = new List<HTTPResponse>();
            var messageRequests         = new List<WebSocketFrame>();
            var messageResponses        = new List<WebSocketFrame>();
            var textMessageRequests     = new List<String>();
            var textMessageResponses    = new List<String>();

            webSocketServer.OnValidateTCPConnection       += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedTCP            = true;
                return true;
            };

            webSocketServer.OnNewTCPConnection            += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newTCPConnection        = true;
            };

            webSocketServer.OnHTTPRequest                 += async (timestamp, server, httpRequest) => {
                httpRequests.Add(httpRequest);
            };

            webSocketServer.OnValidateWebSocketConnection += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedWebSocket      = true;
                return null;
            };

            webSocketServer.OnHTTPResponse                += async (timestamp, server, httpRequest, httpResponse) => {
                httpResponses.Add(httpResponse);
            };

            webSocketServer.OnNewWebSocketConnection      += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newWebSocketConnection  = true;
            };

            webSocketServer.OnWebSocketFrameReceived      += async (timestamp, server, connection, requestFrame, eventTrackingId) => {
                messageRequests.     Add(requestFrame);
            };

            //webSocketServer.OnWebSocketFrameResponseSent      += async (timestamp, server, connection, requestFrame, responseFrame, eventTrackingId) => {
            //    messageResponses.    Add(responseFrame);
            //};

            webSocketServer.OnTextMessageReceived          += async (timestamp, server, connection, eventTrackingId, requestTimestamp, requestMessage) => {
                textMessageRequests. Add(requestMessage);
            };

            //webSocketServer.OnTextMessageResponseSent         += async (timestamp, server, connection, eventTrackingId, requestTimestamp, requestMessage, responseTimestamp, responseMessage) => {
            //    textMessageResponses.Add(responseMessage ?? "-");
            //};


            var clientWebSocket = new ClientWebSocket();

            //clientWebSocket.Options.Credentials = CredentialCache.DefaultCredentials;

            try
            {
                await clientWebSocket.ConnectAsync(new Uri($"ws://127.0.0.1:{HTTPPort}"), CancellationToken.None);
            }
            catch (Exception e)
            {
                DebugX.LogException(e);
            }


            Assert.IsTrue  (validatedTCP);
            Assert.IsTrue  (newTCPConnection);
            Assert.IsTrue  (validatedWebSocket);
            Assert.IsTrue  (newWebSocketConnection);

            Assert.AreEqual(1, httpRequests. Count);
            Assert.AreEqual(1, httpResponses.Count);
            Assert.AreEqual(1, webSocketServer.WebSocketConnections.Count());


            var state                = clientWebSocket.State;
            var closeStatus          = clientWebSocket.CloseStatus;
            var options              = clientWebSocket.Options;
            var subProtocol          = clientWebSocket.SubProtocol;
            var httpStatusCode       = clientWebSocket.HttpStatusCode;
            var httpResponseHeaders  = clientWebSocket.HttpResponseHeaders;


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



            // Send messages
            await clientWebSocket.SendAsync(buffer:             new ArraySegment<Byte>(Encoding.UTF8.GetBytes("1234")),
                                            messageType:        WebSocketMessageType.Text,
                                            endOfMessage:       true,
                                            cancellationToken:  CancellationToken.None);

            while (textMessageRequests.Count == 0)
                Thread.Sleep(10);

            await clientWebSocket.SendAsync(buffer:             new ArraySegment<Byte>(Encoding.UTF8.GetBytes("ABCD")),
                                            messageType:        WebSocketMessageType.Text,
                                            endOfMessage:       true,
                                            cancellationToken:  CancellationToken.None);

            while (textMessageRequests.Count == 1)
                Thread.Sleep(10);


            // Validate message delivery
            Assert.AreEqual(2,      messageRequests. Count);
            Assert.AreEqual("1234", messageRequests.ElementAt(0).Payload.ToUTF8String());
            Assert.AreEqual("ABCD", messageRequests.ElementAt(1).Payload.ToUTF8String());

            Assert.AreEqual(0,      messageResponses.Count);

            Assert.AreEqual(2,      textMessageRequests.Count);
            Assert.AreEqual("1234", textMessageRequests.ElementAt(0));
            Assert.AreEqual("ABCD", textMessageRequests.ElementAt(1));

            Assert.AreEqual(0,      textMessageResponses.Count);


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

                DebugX.LogException(e);

            }


        }

        #endregion

        #region Test_002_UnknownSubprotocol()

        [Test]
        public async Task Test_002_UnknownSubprotocol()
        {

            if (webSocketServer is null) {
                Assert.Fail("WebSocketServer is null!");
                return;
            }

            var validatedTCP            = false;
            var newTCPConnection        = false;
            var validatedWebSocket      = false;
            var newWebSocketConnection  = false;
            var httpRequests            = new List<HTTPRequest>();
            var httpResponses           = new List<HTTPResponse>();
            var messageRequests         = new List<WebSocketFrame>();
            var messageResponses        = new List<WebSocketFrame>();
            var textMessageRequests     = new List<String>();
            var textMessageResponses    = new List<String>();

            webSocketServer.OnValidateTCPConnection       += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedTCP            = true;
                return true;
            };

            webSocketServer.OnNewTCPConnection            += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newTCPConnection        = true;
            };

            webSocketServer.OnHTTPRequest                 += async (timestamp, server, httpRequest) => {
                httpRequests.Add(httpRequest);
            };

            webSocketServer.OnValidateWebSocketConnection += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedWebSocket      = true;
                return null;
            };

            webSocketServer.OnHTTPResponse                += async (timestamp, server, httpRequest, httpResponse) => {
                httpResponses.Add(httpResponse);
            };

            webSocketServer.OnNewWebSocketConnection      += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newWebSocketConnection  = true;
            };

            webSocketServer.OnWebSocketFrameReceived      += async (timestamp, server, connection, requestFrame, eventTrackingId) => {
                messageRequests.     Add(requestFrame);
            };

            //webSocketServer.OnWebSocketFrameResponseSent      += async (timestamp, server, connection, requestFrame, responseFrame, eventTrackingId) => {
            //    messageResponses.    Add(responseFrame);
            //};

            webSocketServer.OnTextMessageReceived          += async (timestamp, server, connection, eventTrackingId, requestTimestamp, requestMessage) => {
                textMessageRequests. Add(requestMessage);
            };

            //webSocketServer.OnTextMessageResponseSent         += async (timestamp, server, connection, eventTrackingId, requestTimestamp, requestMessage, responseTimestamp, responseMessage) => {
            //    textMessageResponses.Add(responseMessage ?? "-");
            //};


            var clientWebSocket = new ClientWebSocket();

            //clientWebSocket.Options.Credentials = CredentialCache.DefaultCredentials;
            clientWebSocket.Options.AddSubProtocol("ocpp1.6");

            try
            {
                await clientWebSocket.ConnectAsync(new Uri($"ws://127.0.0.1:{HTTPPort}"), CancellationToken.None);
            }
            catch (Exception e)
            {
                DebugX.LogException(e);
            }


            Assert.IsTrue  (validatedTCP);
            Assert.IsTrue  (newTCPConnection);
            Assert.IsTrue  (validatedWebSocket);
            Assert.IsTrue  (newWebSocketConnection);

            Assert.AreEqual(1, httpRequests. Count);
            Assert.AreEqual(1, httpResponses.Count);
            Assert.AreEqual(1, webSocketServer.WebSocketConnections.Count());


            var state                = clientWebSocket.State;
            var closeStatus          = clientWebSocket.CloseStatus;
            var options              = clientWebSocket.Options;
            var subProtocol          = clientWebSocket.SubProtocol;
            var httpStatusCode       = clientWebSocket.HttpStatusCode;
            var httpResponseHeaders  = clientWebSocket.HttpResponseHeaders;


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



            // Send messages
            await clientWebSocket.SendAsync(buffer:             new ArraySegment<Byte>(Encoding.UTF8.GetBytes("1234")),
                                            messageType:        WebSocketMessageType.Text,
                                            endOfMessage:       true,
                                            cancellationToken:  CancellationToken.None);

            while (textMessageRequests.Count == 0)
                Thread.Sleep(10);

            await clientWebSocket.SendAsync(buffer:             new ArraySegment<Byte>(Encoding.UTF8.GetBytes("ABCD")),
                                            messageType:        WebSocketMessageType.Text,
                                            endOfMessage:       true,
                                            cancellationToken:  CancellationToken.None);

            while (textMessageRequests.Count == 1)
                Thread.Sleep(10);


            // Validate message delivery
            Assert.AreEqual(2,      messageRequests. Count);
            Assert.AreEqual("1234", messageRequests.ElementAt(0).Payload.ToUTF8String());
            Assert.AreEqual("ABCD", messageRequests.ElementAt(1).Payload.ToUTF8String());

            Assert.AreEqual(0,      messageResponses.Count);

            Assert.AreEqual(2,      textMessageRequests.Count);
            Assert.AreEqual("1234", textMessageRequests.ElementAt(0));
            Assert.AreEqual("ABCD", textMessageRequests.ElementAt(1));

            Assert.AreEqual(0,      textMessageResponses.Count);


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

                DebugX.LogException(e);

            }


        }

        #endregion

        #region Test_003_KnownSubprotocol()

        [Test]
        public async Task Test_003_KnownSubprotocol()
        {

            if (webSocketServer is null) {
                Assert.Fail("WebSocketServer is null!");
                return;
            }

            var validatedTCP            = false;
            var newTCPConnection        = false;
            var validatedWebSocket      = false;
            var newWebSocketConnection  = false;
            var httpRequests            = new List<HTTPRequest>();
            var httpResponses           = new List<HTTPResponse>();
            var messageRequests         = new List<WebSocketFrame>();
            var messageResponses        = new List<WebSocketFrame>();
            var textMessageRequests     = new List<String>();
            var textMessageResponses    = new List<String>();

            webSocketServer.SecWebSocketProtocols.Add("ocpp1.6");

            webSocketServer.OnValidateTCPConnection       += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedTCP            = true;
                return true;
            };

            webSocketServer.OnNewTCPConnection            += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newTCPConnection        = true;
            };

            webSocketServer.OnHTTPRequest                 += async (timestamp, server, httpRequest) => {
                httpRequests.Add(httpRequest);
            };

            webSocketServer.OnValidateWebSocketConnection += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedWebSocket      = true;
                return null;
            };

            webSocketServer.OnHTTPResponse                += async (timestamp, server, httpRequest, httpResponse) => {
                httpResponses.Add(httpResponse);
            };

            webSocketServer.OnNewWebSocketConnection      += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newWebSocketConnection  = true;
            };

            webSocketServer.OnWebSocketFrameReceived      += async (timestamp, server, connection, requestFrame, eventTrackingId) => {
                messageRequests.     Add(requestFrame);
            };

            //webSocketServer.OnWebSocketFrameResponseSent      += async (timestamp, server, connection, requestFrame, responseFrame, eventTrackingId) => {
            //    messageResponses.    Add(responseFrame);
            //};

            webSocketServer.OnTextMessageReceived          += async (timestamp, server, connection, eventTrackingId, requestTimestamp, requestMessage) => {
                textMessageRequests. Add(requestMessage);
            };

            //webSocketServer.OnTextMessageResponseSent         += async (timestamp, server, connection, eventTrackingId, requestTimestamp, requestMessage, responseTimestamp, responseMessage) => {
            //    textMessageResponses.Add(responseMessage ?? "-");
            //};


            var clientWebSocket = new ClientWebSocket();

            //clientWebSocket.Options.Credentials = CredentialCache.DefaultCredentials;
            clientWebSocket.Options.AddSubProtocol("ocpp1.6");

            try
            {
                await clientWebSocket.ConnectAsync(new Uri($"ws://127.0.0.1:{HTTPPort}"), CancellationToken.None);
            }
            catch (Exception e)
            {
                DebugX.LogException(e);
            }


            Assert.IsTrue  (validatedTCP);
            Assert.IsTrue  (newTCPConnection);
            Assert.IsTrue  (validatedWebSocket);
            Assert.IsTrue  (newWebSocketConnection);

            Assert.AreEqual(1, httpRequests. Count);
            Assert.AreEqual(1, httpResponses.Count);
            Assert.AreEqual(1, webSocketServer.WebSocketConnections.Count());


            var state                = clientWebSocket.State;
            var closeStatus          = clientWebSocket.CloseStatus;
            var options              = clientWebSocket.Options;
            var subProtocol          = clientWebSocket.SubProtocol;
            var httpStatusCode       = clientWebSocket.HttpStatusCode;
            var httpResponseHeaders  = clientWebSocket.HttpResponseHeaders;


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



            // Send messages
            await clientWebSocket.SendAsync(buffer:             new ArraySegment<Byte>(Encoding.UTF8.GetBytes("1234")),
                                            messageType:        WebSocketMessageType.Text,
                                            endOfMessage:       true,
                                            cancellationToken:  CancellationToken.None);

            while (textMessageRequests.Count == 0)
                Thread.Sleep(10);

            await clientWebSocket.SendAsync(buffer:             new ArraySegment<Byte>(Encoding.UTF8.GetBytes("ABCD")),
                                            messageType:        WebSocketMessageType.Text,
                                            endOfMessage:       true,
                                            cancellationToken:  CancellationToken.None);

            while (textMessageRequests.Count == 1)
                Thread.Sleep(10);


            // Validate message delivery
            Assert.AreEqual(2,      messageRequests. Count);
            Assert.AreEqual("1234", messageRequests.ElementAt(0).Payload.ToUTF8String());
            Assert.AreEqual("ABCD", messageRequests.ElementAt(1).Payload.ToUTF8String());

            Assert.AreEqual(0,      messageResponses.Count);

            Assert.AreEqual(2,      textMessageRequests.Count);
            Assert.AreEqual("1234", textMessageRequests.ElementAt(0));
            Assert.AreEqual("ABCD", textMessageRequests.ElementAt(1));

            Assert.AreEqual(0,      textMessageResponses.Count);


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

                DebugX.LogException(e);

            }


        }

        #endregion

        #region Test_004_KnownSubprotocols()

        [Test]
        public async Task Test_004_KnownSubprotocols()
        {

            if (webSocketServer is null) {
                Assert.Fail("WebSocketServer is null!");
                return;
            }

            var validatedTCP            = false;
            var newTCPConnection        = false;
            var validatedWebSocket      = false;
            var newWebSocketConnection  = false;
            var httpRequests            = new List<HTTPRequest>();
            var httpResponses           = new List<HTTPResponse>();
            var messageRequests         = new List<WebSocketFrame>();
            var messageResponses        = new List<WebSocketFrame>();
            var textMessageRequests     = new List<String>();
            var textMessageResponses    = new List<String>();

            webSocketServer.SecWebSocketProtocols.Add("ocpp1.6");
            webSocketServer.SecWebSocketProtocols.Add("ocpp2.0");

            webSocketServer.OnValidateTCPConnection       += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedTCP            = true;
                return true;
            };

            webSocketServer.OnNewTCPConnection            += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newTCPConnection        = true;
            };

            webSocketServer.OnHTTPRequest                 += async (timestamp, server, httpRequest) => {
                httpRequests.Add(httpRequest);
            };

            webSocketServer.OnValidateWebSocketConnection += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedWebSocket      = true;
                return null;
            };

            webSocketServer.OnHTTPResponse                += async (timestamp, server, httpRequest, httpResponse) => {
                httpResponses.Add(httpResponse);
            };

            webSocketServer.OnNewWebSocketConnection      += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newWebSocketConnection  = true;
            };

            webSocketServer.OnWebSocketFrameReceived      += async (timestamp, server, connection, requestFrame, eventTrackingId) => {
                messageRequests.     Add(requestFrame);
            };

            //webSocketServer.OnWebSocketFrameResponseSent      += async (timestamp, server, connection, requestFrame, responseFrame, eventTrackingId) => {
            //    messageResponses.    Add(responseFrame);
            //};

            webSocketServer.OnTextMessageReceived          += async (timestamp, server, connection, eventTrackingId, requestTimestamp, requestMessage) => {
                textMessageRequests. Add(requestMessage);
            };

            //webSocketServer.OnTextMessageResponseSent         += async (timestamp, server, connection, eventTrackingId, requestTimestamp, requestMessage, responseTimestamp, responseMessage) => {
            //    textMessageResponses.Add(responseMessage ?? "-");
            //};


            var clientWebSocket = new ClientWebSocket();

            //clientWebSocket.Options.Credentials = CredentialCache.DefaultCredentials;
            clientWebSocket.Options.AddSubProtocol("ocpp2.0");

            try
            {
                await clientWebSocket.ConnectAsync(new Uri($"ws://127.0.0.1:{HTTPPort}"), CancellationToken.None);
            }
            catch (Exception e)
            {
                DebugX.LogException(e);
            }


            Assert.IsTrue  (validatedTCP);
            Assert.IsTrue  (newTCPConnection);
            Assert.IsTrue  (validatedWebSocket);
            Assert.IsTrue  (newWebSocketConnection);

            Assert.AreEqual(1, httpRequests. Count);
            Assert.AreEqual(1, httpResponses.Count);
            Assert.AreEqual(1, webSocketServer.WebSocketConnections.Count());


            var state                = clientWebSocket.State;
            var closeStatus          = clientWebSocket.CloseStatus;
            var options              = clientWebSocket.Options;
            var subProtocol          = clientWebSocket.SubProtocol;
            var httpStatusCode       = clientWebSocket.HttpStatusCode;
            var httpResponseHeaders  = clientWebSocket.HttpResponseHeaders;


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



            // Send messages
            await clientWebSocket.SendAsync(buffer:             new ArraySegment<Byte>(Encoding.UTF8.GetBytes("1234")),
                                            messageType:        WebSocketMessageType.Text,
                                            endOfMessage:       true,
                                            cancellationToken:  CancellationToken.None);

            while (textMessageRequests.Count == 0)
                Thread.Sleep(10);

            await clientWebSocket.SendAsync(buffer:             new ArraySegment<Byte>(Encoding.UTF8.GetBytes("ABCD")),
                                            messageType:        WebSocketMessageType.Text,
                                            endOfMessage:       true,
                                            cancellationToken:  CancellationToken.None);

            while (textMessageRequests.Count == 1)
                Thread.Sleep(10);


            // Validate message delivery
            Assert.AreEqual(2,      messageRequests. Count);
            Assert.AreEqual("1234", messageRequests.ElementAt(0).Payload.ToUTF8String());
            Assert.AreEqual("ABCD", messageRequests.ElementAt(1).Payload.ToUTF8String());

            Assert.AreEqual(0,      messageResponses.Count);

            Assert.AreEqual(2,      textMessageRequests.Count);
            Assert.AreEqual("1234", textMessageRequests.ElementAt(0));
            Assert.AreEqual("ABCD", textMessageRequests.ElementAt(1));

            Assert.AreEqual(0,      textMessageResponses.Count);


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

                DebugX.LogException(e);

            }


        }

        #endregion

    }

}
