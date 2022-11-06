/*
 * Copyright (c) 2010-2022, Achim Friedland <achim.friedland@graphdefined.com>
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

namespace org.GraphDefined.Vanaheimr.Hermod.UnitTests
{

    /// <summary>
    /// HTTP web socket tests.
    /// </summary>
    [TestFixture]
    public class WebSocketTests
    {

        #region Data

        private          WebSocketServer?  webSocketServer;
        private readonly UInt16            port  = 18402;

        #endregion


        #region SetupEachTest()

        [SetUp]
        public virtual void SetupEachTest()
        {

            webSocketServer = new WebSocketServer(
                                  TCPPort:   IPPort.Parse(port),
                                  Autostart: true
                              );

        }

        #endregion

        #region ShutdownEachTest()

        [TearDown]
        public virtual void ShutdownEachTest()
        {
            webSocketServer?.Shutdown();
            webSocketServer = null;
        }

        #endregion


        #region SendTwoTextFrames_Fast_Test()

        [Test]
        public async Task SendTwoTextFrames_Fast_Test()
        {

            Assert.NotNull(webSocketServer);

            if (webSocketServer is null)
                return;

            var newTCPConnection        = false;
            var validated               = false;
            var newWebSocketConnection  = false;
            var httpRequests            = new List<HTTPRequest>();
            var httpResponses           = new List<HTTPResponse>();
            var messageRequests         = new List<WebSocketFrame>();
            var messageResponses        = new List<WebSocketFrame>();
            var textMessageRequests     = new List<String>();
            var textMessageResponses    = new List<String>();

            webSocketServer.OnNewTCPConnection            += async (Timestamp, WebSocketServer, WebSocketConnection, eventTrackingId, cancellationToken) => {
                newTCPConnection        = true;
            };

            webSocketServer.OnHTTPRequest                 += async (Timestamp, WebSocketServer, httpRequest) => {
                httpRequests.Add(httpRequest);
            };

            webSocketServer.OnValidateWebSocketConnection += async (Timestamp, WebSocketServer, WebSocketConnection, eventTrackingId, cancellationToken) => {
                validated               = true;
                return null;
            };

            webSocketServer.OnHTTPResponse                += async (Timestamp, WebSocketServer, httpRequest, httpResponse) => {
                httpResponses.Add(httpResponse);
            };

            webSocketServer.OnNewWebSocketConnection      += async (Timestamp, WebSocketServer, WebSocketConnection, eventTrackingId, cancellationToken) => {
                newWebSocketConnection  = true;
            };

            webSocketServer.OnWebSocketFrame              += async (Timestamp, WebSocketServer, WebSocketConnection, requestFrame, eventTrackingId, cancellationToken) => {
                messageRequests.     Add(requestFrame);
            };

            webSocketServer.OnResponseFrame               += async (Timestamp, WebSocketServer, WebSocketConnection, requestFrame, responseFrame, eventTrackingId, cancellationToken) => {
                messageResponses.    Add(responseFrame);
            };

            webSocketServer.OnTextMessageRequest          += async (Timestamp, WebSocketServer, WebSocketConnection, requestFrame, cancellationToken) => {
                textMessageRequests. Add(requestFrame. Request);
            };

            webSocketServer.OnTextMessageResponse         += async (Timestamp, WebSocketServer, WebSocketConnection, responseFrame) => {
                textMessageResponses.Add(responseFrame.ResponseMessage);
            };

            var webSocketClient = new WebSocketClient(URL.Parse("ws://127.0.0.1:" + port));
            await webSocketClient.Connect();

            Assert.IsTrue  (newTCPConnection);
            Assert.IsTrue  (validated);
            Assert.IsTrue  (newWebSocketConnection);

            Assert.AreEqual(1, httpRequests. Count);
            Assert.AreEqual(1, httpResponses.Count);
            Assert.AreEqual(1, webSocketServer.WebSocketConnections.Count());


            // Send messages
            webSocketClient.SendText("1234");
            webSocketClient.SendText("ABCD");

            while (textMessageRequests.Count < 2)
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

        #region SendTwoTextFrames_Slow_Test()

        [Test]
        public async Task SendTwoTextFrames_Slow_Test()
        {

            Assert.NotNull(webSocketServer);

            if (webSocketServer is null)
                return;

            var newTCPConnection        = false;
            var validated               = false;
            var newWebSocketConnection  = false;
            var httpRequests            = new List<HTTPRequest>();
            var httpResponses           = new List<HTTPResponse>();
            var messageRequests         = new List<WebSocketFrame>();
            var messageResponses        = new List<WebSocketFrame>();
            var textMessageRequests     = new List<String>();
            var textMessageResponses    = new List<String>();

            webSocketServer.OnNewTCPConnection            += async (Timestamp, WebSocketServer, WebSocketConnection, eventTrackingId, cancellationToken) => {
                newTCPConnection        = true;
            };

            webSocketServer.OnHTTPRequest                 += async (Timestamp, WebSocketServer, httpRequest) => {
                httpRequests.Add(httpRequest);
            };

            webSocketServer.OnValidateWebSocketConnection += async (Timestamp, WebSocketServer, WebSocketConnection, eventTrackingId, cancellationToken) => {
                validated               = true;
                return null;
            };

            webSocketServer.OnHTTPResponse                += async (Timestamp, WebSocketServer, httpRequest, httpResponse) => {
                httpResponses.Add(httpResponse);
            };

            webSocketServer.OnNewWebSocketConnection      += async (Timestamp, WebSocketServer, WebSocketConnection, eventTrackingId, cancellationToken) => {
                newWebSocketConnection  = true;
            };

            webSocketServer.OnWebSocketFrame              += async (Timestamp, WebSocketServer, WebSocketConnection, requestFrame, eventTrackingId, cancellationToken) => {
                messageRequests.     Add(requestFrame);
            };

            webSocketServer.OnResponseFrame               += async (Timestamp, WebSocketServer, WebSocketConnection, requestFrame, responseFrame, eventTrackingId, cancellationToken) => {
                messageResponses.    Add(responseFrame);
            };

            webSocketServer.OnTextMessageRequest          += async (Timestamp, WebSocketServer, WebSocketConnection, requestFrame, cancellationToken) => {
                textMessageRequests. Add(requestFrame. Request);
            };

            webSocketServer.OnTextMessageResponse         += async (Timestamp, WebSocketServer, WebSocketConnection, responseFrame) => {
                textMessageResponses.Add(responseFrame.ResponseMessage);
            };

            var webSocketClient = new WebSocketClient(URL.Parse("ws://127.0.0.1:" + port));
            await webSocketClient.Connect();

            Assert.IsTrue  (newTCPConnection);
            Assert.IsTrue  (validated);
            Assert.IsTrue  (newWebSocketConnection);

            Assert.AreEqual(1, httpRequests. Count);
            Assert.AreEqual(1, httpResponses.Count);
            Assert.AreEqual(1, webSocketServer.WebSocketConnections.Count());


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
