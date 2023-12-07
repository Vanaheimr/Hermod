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

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.WebSocket
{

    /// <summary>
    /// HTTP web socket tests.
    /// </summary>
    [TestFixture]
    public class WebSocketTests
    {

        #region Data

        private          AWebSocketServer?  webSocketServer;
        private readonly UInt16            port  = 18402;

        #endregion


        #region SetupEachTest()

        [SetUp]
        public virtual void SetupEachTest()
        {

            webSocketServer = new WebSocketServer(
                                  HTTPPort:   IPPort.Parse(port),
                                  AutoStart:  true
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

            webSocketServer.OnValidateTCPConnection       += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedTCP            = true;
                return Task.FromResult(ConnectionFilterResponse.Accepted());
            };

            webSocketServer.OnNewTCPConnection            += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newTCPConnection        = true;
                return Task.CompletedTask;
            };

            webSocketServer.OnHTTPRequest                 += (timestamp, server, httpRequest, cancellationToken) => {
                httpRequests.Add(httpRequest);
                return Task.FromResult<HTTPResponse?>(null);
            };

            webSocketServer.OnValidateWebSocketConnection += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedWebSocket      = true;
                return Task.FromResult<HTTPResponse?>(null);
            };

            webSocketServer.OnHTTPResponse                += (timestamp, server, httpRequest, httpResponse, cancellationToken) => {
                httpResponses.Add(httpResponse);
                return Task.CompletedTask;
            };

            webSocketServer.OnNewWebSocketConnection      += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newWebSocketConnection  = true;
                return Task.CompletedTask;
            };

            webSocketServer.OnWebSocketFrameReceived      += (timestamp, server, connection, eventTrackingId, requestFrame, cancellationToken) => {
                messageRequests.     Add(requestFrame);
                return Task.CompletedTask;
            };

            //webSocketServer.OnWebSocketFrameResponseSent      += async (timestamp, server, connection, requestFrame, responseFrame, eventTrackingId) => {
            //    messageResponses.    Add(responseFrame);
            //};

            webSocketServer.OnTextMessageReceived          += (timestamp, server, connection, eventTrackingId, requestMessage, cancellationToken) => {
                textMessageRequests. Add(requestMessage);
                return Task.CompletedTask;
            };

            //webSocketServer.OnTextMessageResponseSent         += async (timestamp, server, connection, eventTrackingId, requestTimestamp, requestMessage, responseTimestamp, responseMessage) => {
            //    textMessageResponses.Add(responseMessage ?? "-");
            //};

            var webSocketClient = new WebSocketClient(URL.Parse("ws://127.0.0.1:" + port));
            await webSocketClient.Connect();

            Assert.IsTrue  (validatedTCP);
            Assert.IsTrue  (newTCPConnection);
            Assert.IsTrue  (validatedWebSocket);
            Assert.IsTrue  (newWebSocketConnection);

            Assert.AreEqual(1, httpRequests. Count);
            Assert.AreEqual(1, httpResponses.Count);
            Assert.AreEqual(1, webSocketServer.WebSocketConnections.Count());


            // Send messages
            await webSocketClient.SendText("1234");
            await webSocketClient.SendText("ABCD");

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

            await webSocketClient.Close();

        }

        #endregion

        #region SendTwoTextFrames_Slow_Test()

        [Test]
        public async Task SendTwoTextFrames_Slow_Test()
        {

            Assert.NotNull(webSocketServer);

            if (webSocketServer is null)
                return;

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

            webSocketServer.OnValidateTCPConnection       += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedTCP            = true;
                return Task.FromResult(ConnectionFilterResponse.Accepted());
            };

            webSocketServer.OnNewTCPConnection            += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newTCPConnection        = true;
                return Task.CompletedTask;
            };

            webSocketServer.OnHTTPRequest                 += (timestamp, server, httpRequest, cancellationToken) => {
                httpRequests.Add(httpRequest);
                return Task.CompletedTask;
            };

            webSocketServer.OnValidateWebSocketConnection += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                validatedWebSocket      = true;
                return Task.FromResult<HTTPResponse?>(null);
            };

            webSocketServer.OnHTTPResponse                += (timestamp, server, httpRequest, httpResponse, cancellationToken) => {
                httpResponses.Add(httpResponse);
                return Task.CompletedTask;
            };

            webSocketServer.OnNewWebSocketConnection      += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                newWebSocketConnection  = true;
                return Task.CompletedTask;
            };

            webSocketServer.OnWebSocketFrameReceived      += (timestamp, server, connection, eventTrackingId, requestFrame, cancellationToken) => {
                messageRequests.     Add(requestFrame);
                return Task.CompletedTask;
            };

            //webSocketServer.OnWebSocketFrameResponseSent      += async (timestamp, server, connection, requestFrame, responseFrame, eventTrackingId) => {
            //    messageResponses.    Add(responseFrame);
            //};

            webSocketServer.OnTextMessageReceived          += (timestamp, server, connection, eventTrackingId, requestMessage, cancellationToken) => {
                textMessageRequests. Add(requestMessage);
                return Task.CompletedTask;
            };

            //webSocketServer.OnTextMessageResponseSent         += async (timestamp, server, connection, eventTrackingId, requestTimestamp, requestMessage, responseTimestamp, responseMessage) => {
            //    textMessageResponses.Add(responseMessage ?? "-");
            //};

            var webSocketClient = new WebSocketClient(URL.Parse("ws://127.0.0.1:" + port));
            await webSocketClient.Connect();

            Assert.IsTrue  (validatedTCP);
            Assert.IsTrue  (newTCPConnection);
            Assert.IsTrue  (validatedWebSocket);
            Assert.IsTrue  (newWebSocketConnection);

            Assert.AreEqual(1, httpRequests. Count);
            Assert.AreEqual(1, httpResponses.Count);
            Assert.AreEqual(1, webSocketServer.WebSocketConnections.Count());


            // Send messages
            await webSocketClient.SendText("1234");

            while (textMessageRequests.Count == 0)
                Thread.Sleep(10);

            await webSocketClient.SendText("ABCD");

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

            await webSocketClient.Close();

        }

        #endregion


    }

}
