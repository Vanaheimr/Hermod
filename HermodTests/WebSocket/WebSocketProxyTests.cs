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

            webSocketServer.OnValidateTCPConnection       += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                server_validatedTCP.Add($"{server_validatedTCP.Count}: {connection.Client.RemoteEndPoint?.ToString() ?? "-"}");
                return true;
            };

            webSocketServer.OnNewTCPConnection            += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                server_newTCPConnection.Add($"{server_newTCPConnection.Count}: {connection.RemoteSocket}");
            };

            webSocketServer.OnHTTPRequest                 += async (timestamp, server, httpRequest) => {
                server_httpRequests.Add(httpRequest);
            };

            webSocketServer.OnValidateWebSocketConnection += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                server_validatedWebSocket.Add($"{server_validatedWebSocket.Count}: {connection.RemoteSocket}");
                return null;
            };

            webSocketServer.OnHTTPResponse                += async (timestamp, server, httpRequest, httpResponse) => {
                server_httpResponses.Add(httpResponse);
            };

            webSocketServer.OnNewWebSocketConnection      += async (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                server_newWebSocketConnection.Add($"{server_newWebSocketConnection.Count}: {connection.RemoteSocket}");
            };

            webSocketServer.OnWebSocketFrameReceived      += async (timestamp, server, connection, eventTrackingId, requestFrame) => {
                server_messageRequests.       Add(requestFrame);
            };

            webSocketServer.OnWebSocketFrameSent          += async (timestamp, server, connection, eventTrackingId, responseFrame) => {
                server_messageResponses.      Add(responseFrame);
            };

            webSocketServer.OnTextMessageReceived         += async (timestamp, server, connection, eventTrackingId, textMessage) => {
                server_textMessageRequests.   Add(textMessage);
            };

            webSocketServer.OnTextMessageSent             += async (timestamp, server, connection, eventTrackingId, textMessage) => {
                server_textMessageResponses.  Add(textMessage ?? "-");
            };

            webSocketServer.OnBinaryMessageReceived       += async (timestamp, server, connection, eventTrackingId, binaryMessage) => {
                server_binaryMessageRequests. Add(binaryMessage);
            };

            webSocketServer.OnBinaryMessageSent           += async (timestamp, server, connection, eventTrackingId, binaryMessage) => {
                server_binaryMessageResponses.Add(binaryMessage);
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

            webSocketProxy.OnValidateTCPConnection       += async (timestamp, server, connection, eventTrackingId, cancellationToken)  => {
                proxy_validatedTCP.Add($"{proxy_validatedTCP.Count}: {connection.Client.RemoteEndPoint?.ToString() ?? "-"}");
                return true;
            };

            webSocketProxy.OnNewTCPConnection            += async (timestamp, server, connection, eventTrackingId, cancellationToken)  => {
                proxy_newTCPConnection.Add($"{proxy_newTCPConnection.Count}: {connection.RemoteSocket}");
            };

            webSocketProxy.OnHTTPRequest                 += async (timestamp, server, httpRequest)                                     => {
                proxy_httpRequests.Add(httpRequest);
            };

            webSocketProxy.OnValidateWebSocketConnection += async (timestamp, server, connection, eventTrackingId, cancellationToken)  => {
                proxy_validatedWebSocket.Add($"{proxy_validatedWebSocket.Count}: {connection.RemoteSocket}");
                return null;
            };

            webSocketProxy.OnHTTPResponse                += async (timestamp, server, httpRequest, httpResponse)                       => {
                proxy_httpResponses.Add(httpResponse);
            };

            webSocketProxy.OnNewWebSocketConnection      += async (timestamp, server, connection, eventTrackingId, cancellationToken)  => {
                proxy_newWebSocketConnection.Add($"{proxy_newWebSocketConnection.Count}: {connection.RemoteSocket}");
            };

            webSocketProxy.OnWebSocketFrameReceived      += async (timestamp, server, connection, eventTrackingId, requestFrame)       => {
                proxy_messageRequests.       Add(requestFrame);
            };

            webSocketProxy.OnWebSocketFrameSent          += async (timestamp, server, connection, eventTrackingId, responseFrame)      => {
                proxy_messageResponses.      Add(responseFrame);
            };

            webSocketProxy.OnTextMessageReceived         += async (timestamp, server, connection, eventTrackingId, textMessage)        => {
                proxy_textMessageRequests.   Add(textMessage);
            };

            webSocketProxy.OnTextMessageSent             += async (timestamp, server, connection, eventTrackingId, textMessage)        => {
                proxy_textMessageResponses.  Add(textMessage ?? "-");
            };

            webSocketProxy.OnBinaryMessageReceived       += async (timestamp, server, connection, eventTrackingId, binaryMessage)      => {
                proxy_binaryMessageRequests. Add(binaryMessage);
            };

            webSocketProxy.OnBinaryMessageSent           += async (timestamp, server, connection, eventTrackingId, binaryMessage)      => {
                proxy_binaryMessageResponses.Add(binaryMessage);
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
            while (proxy_newWebSocketConnection.Count == 0)
                Thread.Sleep(10);

            Assert.AreEqual(1, proxy_validatedTCP.          Count, proxy_validatedTCP.          AggregateWith(", "));
            Assert.AreEqual(1, proxy_newTCPConnection.      Count, proxy_newTCPConnection.      AggregateWith(", "));
            Assert.AreEqual(1, proxy_validatedWebSocket.    Count, proxy_validatedWebSocket.    AggregateWith(", "));
            Assert.AreEqual(1, proxy_newWebSocketConnection.Count, proxy_newWebSocketConnection.AggregateWith(", "));

            Assert.AreEqual(1, proxy_httpRequests.          Count);
            Assert.AreEqual(1, proxy_httpResponses.         Count);
            Assert.AreEqual(1, webSocketServer.WebSocketConnections.Count());


            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host:                     127.0.0.1:101
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Key:        /vkroMJ5bwBVW200riZKRg==
            // Sec-WebSocket-Version:    13

            // HTTP requests should not have a "Date"-header!
            Assert.IsFalse (request.Contains("Date:"),                              request);
            Assert.IsTrue  (request.Contains("GET / HTTP/1.1"),                     request);
            Assert.IsTrue  (request.Contains($"Host: 127.0.0.1:{HTTPPortProxy}"),   request);
            Assert.IsTrue  (request.Contains($"Upgrade: websocket"),                request);

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

            Assert.AreEqual(2,       proxy_messageRequests.        Count);
            Assert.AreEqual("1234",  proxy_messageRequests.        ElementAt(0).Payload.ToUTF8String());
            Assert.AreEqual("ABCD",  proxy_messageRequests.        ElementAt(1).Payload.ToUTF8String());

            Assert.AreEqual(2,       server_messageRequests.       Count);
            Assert.AreEqual("1234",  server_messageRequests.       ElementAt(0).Payload.ToUTF8String());
            Assert.AreEqual("ABCD",  server_messageRequests.       ElementAt(1).Payload.ToUTF8String());


            Assert.AreEqual(2,       proxy_messageResponses.       Count);
            Assert.AreEqual("4321",  proxy_messageResponses.       ElementAt(0).Payload.ToUTF8String());
            Assert.AreEqual("DCBA",  proxy_messageResponses.       ElementAt(1).Payload.ToUTF8String());

            Assert.AreEqual(2,       server_messageResponses.      Count);
            Assert.AreEqual("4321",  server_messageResponses.      ElementAt(0).Payload.ToUTF8String());
            Assert.AreEqual("DCBA",  server_messageResponses.      ElementAt(1).Payload.ToUTF8String());


            Assert.AreEqual(1,       proxy_textMessageRequests.    Count);
            Assert.AreEqual("1234",  proxy_textMessageRequests.    ElementAt(0));
            Assert.AreEqual(1,       proxy_binaryMessageRequests.  Count);
            Assert.AreEqual("ABCD",  proxy_binaryMessageRequests.  ElementAt(0).ToUTF8String());

            Assert.AreEqual(1,       server_textMessageRequests.   Count);
            Assert.AreEqual("1234",  server_textMessageRequests.   ElementAt(0));
            Assert.AreEqual(1,       server_binaryMessageRequests. Count);
            Assert.AreEqual("ABCD",  server_binaryMessageRequests. ElementAt(0).ToUTF8String());


            Assert.AreEqual(1,       proxy_textMessageResponses.   Count);
            Assert.AreEqual("4321",  proxy_textMessageResponses.   ElementAt(0));
            Assert.AreEqual(1,       proxy_binaryMessageResponses. Count);
            Assert.AreEqual("DCBA",  proxy_binaryMessageResponses. ElementAt(0).ToUTF8String());

            Assert.AreEqual(1,       server_textMessageResponses.  Count);
            Assert.AreEqual("4321",  server_textMessageResponses.  ElementAt(0));
            Assert.AreEqual(1,       server_binaryMessageResponses.Count);
            Assert.AreEqual("DCBA",  server_binaryMessageResponses.ElementAt(0).ToUTF8String());


            Assert.AreEqual(1,       textMessageLog.               Count);
            Assert.AreEqual("4321",  textMessageLog.               ElementAt(0));
            Assert.AreEqual(1,       binaryMessageLog.             Count);
            Assert.AreEqual("DCBA",  binaryMessageLog.             ElementAt(0));

            #endregion

            await webSocketClient.Close();

        }

        #endregion


    }

}
