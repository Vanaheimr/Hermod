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

namespace org.GraphDefined.Vanaheimr.Hermod.UnitTests.HTTP.WebSockets
{

    /// <summary>
    /// Tests between Hermod WebSocket clients and Hermod WebSocket servers.
    /// </summary>
    [TestFixture]
    public class WebSocketClientTests : AWebSocketServerTests
    {

        #region Constructor(s)

        public WebSocketClientTests()
            : base(IPPort.Parse(101))
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


            var webSocketClient  = new WebSocketClient(URL.Parse($"http://127.0.0.1:{HTTPPort}"));
            var httpResponse     = await webSocketClient.Connect();


            Assert.IsTrue  (validatedTCP);
            Assert.IsTrue  (newTCPConnection);
            Assert.IsTrue  (validatedWebSocket);
            Assert.IsTrue  (newWebSocketConnection);

            Assert.AreEqual(1, httpRequests. Count);
            Assert.AreEqual(1, httpResponses.Count);
            Assert.AreEqual(1, webSocketServer.WebSocketConnections.Count());


            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host:                     127.0.0.1:101
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Key:        /vkroMJ5bwBVW200riZKRg==
            // Sec-WebSocket-Protocol:   
            // Sec-WebSocket-Version:    13

            // HTTP requests should not have a "Date"-header!
            Assert.IsFalse (request.Contains("Date:"),                         request);
            Assert.IsTrue  (request.Contains("GET / HTTP/1.1"),                request);
            Assert.IsTrue  (request.Contains($"Host: 127.0.0.1:{HTTPPort}"),   request);
            Assert.IsTrue  (request.Contains($"Upgrade: websocket"),           request);



            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 101 Switching Protocols
            // Date:                     Wed, 02 Aug 2023 19:33:53 GMT
            // Server:                   GraphDefined HTTP Web Socket Service v2.0
            // Connection:               Upgrade
            // Upgrade:                  websocket
            // Sec-WebSocket-Accept:     s9FvxhRowHKxS38G/sBt7gC5qec=
            // Sec-WebSocket-Protocol:   
            // Sec-WebSocket-Version:    13

            Assert.IsTrue  (response.Contains("HTTP/1.1 101 Switching Protocols"),   response);

            Assert.AreEqual("GraphDefined HTTP Web Socket Service v2.0",             httpResponse.Server);
            Assert.AreEqual("Upgrade",                                               httpResponse.Connection);
            Assert.AreEqual("websocket",                                             httpResponse.Upgrade);



            // Send messages
            webSocketClient.SendText("1234");

            while (textMessageRequests.Count == 0)
                Thread.Sleep(10);

            webSocketClient.SendText("ABCD");

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



            webSocketClient.Close();

        }

        #endregion


    }

}
