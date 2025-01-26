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
    public class WebSocketProxyTests : AWebSocketProxyTests
    {

        #region Constructor(s)

        public WebSocketProxyTests()

            : base(IPPort.Parse(2002),
                   IPPort.Parse(2001))

        { }

        #endregion


        #region Test_Proxy()

        [Test]
        public async Task Test_Proxy()
        {

            #region Server setup

            if (webSocketServer is null) {
                Assert.Fail("WebSocketServer is null!");
                return;
            }

            var server_validatedTCP            = new List<String>();
            var server_newTCPConnection        = new List<String>();
            var server_validatedWebSocket      = new List<String>();
            var server_newWebSocketConnection  = new List<String>();
            var server_httpRequests            = new List<HTTPRequest>();
            var server_httpResponses           = new List<HTTPResponse>();
            var server_messageRequests         = new List<WebSocketFrame>();
            var server_messageResponses        = new List<WebSocketFrame>();
            var server_textMessageRequests     = new List<String>();
            var server_textMessageResponses    = new List<String>();
            var server_binaryMessageRequests   = new List<Byte[]>();
            var server_binaryMessageResponses  = new List<Byte[]>();

            webSocketServer.OnValidateTCPConnection       += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                server_validatedTCP.Add($"{server_validatedTCP.Count}: {connection.Client.RemoteEndPoint?.ToString() ?? "-"}");
                return Task.FromResult(ConnectionFilterResponse.Accepted());
            };

            webSocketServer.OnNewTCPConnection            += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                server_newTCPConnection.Add($"{server_newTCPConnection.Count}: {connection.RemoteSocket}");
                return Task.CompletedTask;
            };

            webSocketServer.OnHTTPRequest                 += (timestamp, server, httpRequest, cancellationToken) => {
                server_httpRequests.Add(httpRequest);
                return Task.CompletedTask;
            };

            webSocketServer.OnValidateWebSocketConnection += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                server_validatedWebSocket.Add($"{server_validatedWebSocket.Count}: {connection.RemoteSocket}");
                return Task.FromResult<HTTPResponse?>(null);
            };

            webSocketServer.OnHTTPResponse                += (timestamp, server, httpRequest, httpResponse, cancellationToken) => {
                server_httpResponses.Add(httpResponse);
                return Task.CompletedTask;
            };

            webSocketServer.OnNewWebSocketConnection      += (timestamp, server, connection, eventTrackingId, sharedSubprotocols, selectedSubprotocol, cancellationToken) => {
                server_newWebSocketConnection.Add($"{server_newWebSocketConnection.Count}: {connection.RemoteSocket}");
                return Task.CompletedTask;
            };

            webSocketServer.OnWebSocketFrameReceived      += (timestamp, server, connection, eventTrackingId, requestFrame, cancellationToken) => {
                server_messageRequests.       Add(requestFrame);
                return Task.CompletedTask;
            };

            webSocketServer.OnWebSocketFrameSent          += (timestamp, server, connection, eventTrackingId, responseFrame, cancellationToken) => {
                server_messageResponses.      Add(responseFrame);
                return Task.CompletedTask;
            };

            webSocketServer.OnTextMessageReceived         += (timestamp, server, connection, frame, eventTrackingId, textMessage, cancellationToken) => {
                server_textMessageRequests.   Add(textMessage);
                return Task.CompletedTask;
            };

            webSocketServer.OnTextMessageSent             += (timestamp, server, connection, frame, eventTrackingId, textMessage, sentStatus, cancellationToken) => {
                server_textMessageResponses.  Add(textMessage ?? "-");
                return Task.CompletedTask;
            };

            webSocketServer.OnBinaryMessageReceived       += (timestamp, server, connection, frame, eventTrackingId, binaryMessage, cancellationToken) => {
                server_binaryMessageRequests. Add(binaryMessage);
                return Task.CompletedTask;
            };

            webSocketServer.OnBinaryMessageSent           += (timestamp, server, connection, frame, eventTrackingId, binaryMessage, sentStatus, cancellationToken) => {
                server_binaryMessageResponses.Add(binaryMessage);
                return Task.CompletedTask;
            };

            #endregion

            #region Proxy setup

            if (webSocketProxy is null) {
                Assert.Fail("WebSocketProxy is null!");
                return;
            }

            var proxy_validatedTCP            = new List<String>();
            var proxy_newTCPConnection        = new List<String>();
            var proxy_validatedWebSocket      = new List<String>();
            var proxy_newWebSocketConnection  = new List<String>();
            var proxy_httpRequests            = new List<HTTPRequest>();
            var proxy_httpResponses           = new List<HTTPResponse>();
            var proxy_messageRequests         = new List<WebSocketFrame>();
            var proxy_messageResponses        = new List<WebSocketFrame>();
            var proxy_textMessageRequests     = new List<String>();
            var proxy_textMessageResponses    = new List<String>();
            var proxy_binaryMessageRequests   = new List<Byte[]>();
            var proxy_binaryMessageResponses  = new List<Byte[]>();

            webSocketProxy.OnValidateTCPConnection       += (timestamp, server, connection, eventTrackingId,                cancellationToken) => {
                proxy_validatedTCP.Add($"{proxy_validatedTCP.Count}: {connection.Client.RemoteEndPoint?.ToString() ?? "-"}");
                return Task.FromResult(ConnectionFilterResponse.Accepted());
            };

            webSocketProxy.OnNewTCPConnection            += (timestamp, server, connection, eventTrackingId,                cancellationToken) => {
                proxy_newTCPConnection.Add($"{proxy_newTCPConnection.Count}: {connection.RemoteSocket}");
                return Task.CompletedTask;
            };

            webSocketProxy.OnHTTPRequest                 += (timestamp, server, httpRequest,                                cancellationToken) => {
                proxy_httpRequests.Add(httpRequest);
                return Task.CompletedTask;
            };

            webSocketProxy.OnValidateWebSocketConnection += (timestamp, server, connection, eventTrackingId,                cancellationToken) => {
                proxy_validatedWebSocket.Add($"{proxy_validatedWebSocket.Count}: {connection.RemoteSocket}");
                return Task.FromResult<HTTPResponse?>(null);
            };

            webSocketProxy.OnHTTPResponse                += (timestamp, server, httpRequest, httpResponse,                  cancellationToken) => {
                proxy_httpResponses.Add(httpResponse);
                return Task.CompletedTask;
            };

            webSocketProxy.OnNewWebSocketConnection      += (timestamp, server, connection, eventTrackingId, sharedSubprotocols, selectedSubprotocol, cancellationToken) => {
                proxy_newWebSocketConnection.Add($"{proxy_newWebSocketConnection.Count}: {connection.RemoteSocket}");
                return Task.CompletedTask;
            };

            webSocketProxy.OnWebSocketFrameReceived      += (timestamp, server, connection, eventTrackingId, requestFrame,  cancellationToken) => {
                proxy_messageRequests.       Add(requestFrame);
                return Task.CompletedTask;
            };

            webSocketProxy.OnWebSocketFrameSent          += (timestamp, server, connection, eventTrackingId, responseFrame, cancellationToken) => {
                proxy_messageResponses.      Add(responseFrame);
                return Task.CompletedTask;
            };

            webSocketProxy.OnTextMessageReceived         += (timestamp, server, connection, frame, eventTrackingId, textMessage,   cancellationToken) => {
                proxy_textMessageRequests.   Add(textMessage);
                return Task.CompletedTask;
            };

            webSocketProxy.OnTextMessageSent             += (timestamp, server, connection, frame, eventTrackingId, textMessage, sentStatus, cancellationToken) => {
                proxy_textMessageResponses.  Add(textMessage ?? "-");
                return Task.CompletedTask;
            };

            webSocketProxy.OnBinaryMessageReceived       += (timestamp, server, connection, frame, eventTrackingId, binaryMessage, cancellationToken) => {
                proxy_binaryMessageRequests. Add(binaryMessage);
                return Task.CompletedTask;
            };

            webSocketProxy.OnBinaryMessageSent           += (timestamp, server, connection, frame, eventTrackingId, binaryMessage, sentStatus, cancellationToken) => {
                proxy_binaryMessageResponses.Add(binaryMessage);
                return Task.CompletedTask;
            };

            #endregion


            #region Client setup and connect

            var webSocketClient  = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{HTTPPortProxy}"));

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
            while (proxy_newWebSocketConnection.Count == 0)
                Thread.Sleep(10);

            ClassicAssert.AreEqual(1, proxy_validatedTCP.          Count, proxy_validatedTCP.          AggregateWith(", "));
            ClassicAssert.AreEqual(1, proxy_newTCPConnection.      Count, proxy_newTCPConnection.      AggregateWith(", "));
            ClassicAssert.AreEqual(1, proxy_validatedWebSocket.    Count, proxy_validatedWebSocket.    AggregateWith(", "));
            ClassicAssert.AreEqual(1, proxy_newWebSocketConnection.Count, proxy_newWebSocketConnection.AggregateWith(", "));

            ClassicAssert.AreEqual(1, proxy_httpRequests.          Count);
            ClassicAssert.AreEqual(1, proxy_httpResponses.         Count);
            ClassicAssert.AreEqual(1, webSocketServer.WebSocketConnections.Count());


            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host:                     127.0.0.1:101
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Key:        /vkroMJ5bwBVW200riZKRg==
            // Sec-WebSocket-Version:    13

            // HTTP requests should not have a "Date"-header!
            ClassicAssert.IsFalse (request.Contains("Date:"),                              request);
            ClassicAssert.IsTrue  (request.Contains("GET / HTTP/1.1"),                     request);
            ClassicAssert.IsTrue  (request.Contains($"Host: 127.0.0.1:{HTTPPortProxy}"),   request);
            ClassicAssert.IsTrue  (request.Contains($"Upgrade: websocket"),                request);

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

            ClassicAssert.AreEqual(2,       proxy_messageRequests.        Count);
            ClassicAssert.AreEqual("1234",  proxy_messageRequests.        ElementAt(0).Payload.ToUTF8String());
            ClassicAssert.AreEqual("ABCD",  proxy_messageRequests.        ElementAt(1).Payload.ToUTF8String());

            ClassicAssert.AreEqual(2,       server_messageRequests.       Count);
            ClassicAssert.AreEqual("1234",  server_messageRequests.       ElementAt(0).Payload.ToUTF8String());
            ClassicAssert.AreEqual("ABCD",  server_messageRequests.       ElementAt(1).Payload.ToUTF8String());


            ClassicAssert.AreEqual(2,       proxy_messageResponses.       Count);
            ClassicAssert.AreEqual("4321",  proxy_messageResponses.       ElementAt(0).Payload.ToUTF8String());
            ClassicAssert.AreEqual("DCBA",  proxy_messageResponses.       ElementAt(1).Payload.ToUTF8String());

            ClassicAssert.AreEqual(2,       server_messageResponses.      Count);
            ClassicAssert.AreEqual("4321",  server_messageResponses.      ElementAt(0).Payload.ToUTF8String());
            ClassicAssert.AreEqual("DCBA",  server_messageResponses.      ElementAt(1).Payload.ToUTF8String());


            ClassicAssert.AreEqual(1,       proxy_textMessageRequests.    Count);
            ClassicAssert.AreEqual("1234",  proxy_textMessageRequests.    ElementAt(0));
            ClassicAssert.AreEqual(1,       proxy_binaryMessageRequests.  Count);
            ClassicAssert.AreEqual("ABCD",  proxy_binaryMessageRequests.  ElementAt(0).ToUTF8String());

            ClassicAssert.AreEqual(1,       server_textMessageRequests.   Count);
            ClassicAssert.AreEqual("1234",  server_textMessageRequests.   ElementAt(0));
            ClassicAssert.AreEqual(1,       server_binaryMessageRequests. Count);
            ClassicAssert.AreEqual("ABCD",  server_binaryMessageRequests. ElementAt(0).ToUTF8String());


            ClassicAssert.AreEqual(1,       proxy_textMessageResponses.   Count);
            ClassicAssert.AreEqual("4321",  proxy_textMessageResponses.   ElementAt(0));
            ClassicAssert.AreEqual(1,       proxy_binaryMessageResponses. Count);
            ClassicAssert.AreEqual("DCBA",  proxy_binaryMessageResponses. ElementAt(0).ToUTF8String());

            ClassicAssert.AreEqual(1,       server_textMessageResponses.  Count);
            ClassicAssert.AreEqual("4321",  server_textMessageResponses.  ElementAt(0));
            ClassicAssert.AreEqual(1,       server_binaryMessageResponses.Count);
            ClassicAssert.AreEqual("DCBA",  server_binaryMessageResponses.ElementAt(0).ToUTF8String());


            ClassicAssert.AreEqual(1,       textMessageLog.               Count);
            ClassicAssert.AreEqual("4321",  textMessageLog.               ElementAt(0));
            ClassicAssert.AreEqual(1,       binaryMessageLog.             Count);
            ClassicAssert.AreEqual("DCBA",  binaryMessageLog.             ElementAt(0));

            #endregion


            await webSocketClient.Close();


        }

        #endregion


    }

}
