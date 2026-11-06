/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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
using org.GraphDefined.Vanaheimr.Hermod.Messaging;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.Messaging
{

    /// <summary>
    /// An HTTP WebSocket connection, seen as an <see cref="IMessageConnection"/>: it says it is a WebSocket,
    /// which end it is and which subprotocol was agreed - and a message sent through it goes as the server
    /// or client it belongs to sends it, with that one's events.
    /// </summary>
    [TestFixture]
    public class WebSocketMessageConnectionTests
    {

        #region Data

        private WebSocketMirrorServer? server;

        #endregion

        #region (helper) StartServer()

        private WebSocketMirrorServer StartServer()

            => server = new WebSocketMirrorServer(
                            HTTPPort:               IPPort.Zero,
                            RequireAuthentication:  false,
                            SecWebSocketProtocols:  [ "ocpp2.1", "ocpp2.0.1" ],
                            AutoStart:              true
                        );

        private static async Task<T> Within<T>(Task<T> Happening, String What)
        {
            if (await Task.WhenAny(Happening, Task.Delay(TimeSpan.FromSeconds(10))) != Happening)
                Assert.Fail($"{What} did not happen within 10 s.");
            return await Happening;
        }

        #endregion

        #region Shutdown()

        [TearDown]
        public async Task Shutdown()
        {
            if (server is not null)
            {
                await server.Shutdown(Wait: true);
                await server.DisposeAsync();
            }
            server = null;
        }

        #endregion


        #region ClientConnection_IsAWebSocketClient_AndSendsAsTheClient

        [Test]
        public async Task ClientConnection_IsAWebSocketClient_AndSendsAsTheClient()
        {

            var port    = StartServer().IPPort;

            await using var client = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{port}"), SecWebSocketProtocols: [ "ocpp2.1" ]);

            var mirrored  = new TaskCompletionSource<String>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sentEvent = new TaskCompletionSource<String>(TaskCreationOptions.RunContinuationsAsynchronously);

            client.OnTextMessageReceived += (timestamp, webSocketClient, connection, frame, eventTrackingId, text, ct) => { mirrored. TrySetResult(text); return Task.CompletedTask; };
            client.OnTextMessageSent     += (timestamp, webSocketClient, connection, frame, eventTrackingId, text, status, ct) => { sentEvent.TrySetResult(text); return Task.CompletedTask; };

            var (webSocketConnection, _) = await client.Connect();

            IMessageConnection connection = webSocketConnection;

            var status = await connection.SendTextMessage("through the common interface");

            var sent     = await Within(sentEvent.Task, "The client's OnTextMessageSent");
            var mirrorText = await Within(mirrored. Task, "The mirrored message");

            Assert.Multiple(() => {

                Assert.That(connection.Transport,    Is.EqualTo(MessageTransport.WebSocket));
                Assert.That(connection.Role,         Is.EqualTo(ConnectionRole.Client));
                Assert.That(connection.Subprotocol,  Is.EqualTo("ocpp2.1"));
                Assert.That(connection.RemoteSocket.Port, Is.EqualTo(port));

                Assert.That(connection.TryGetWebSocket(out var webSocket), Is.True);
                Assert.That(webSocket,                                     Is.SameAs(webSocketConnection));
                Assert.That(connection.TryGetSSH(out _),                   Is.False);

                Assert.That(status, Is.EqualTo(SentStatus.Success));

            });

            Assert.Multiple(() => {
                Assert.That(sent,       Is.EqualTo("through the common interface"), "sent past the client's events");
                Assert.That(mirrorText, Is.EqualTo("ecafretni nommoc eht hguorht"), "the message was not sent as text");   // the mirror answers reversed
            });

        }

        #endregion

        #region ServerConnection_IsAWebSocketServer_AndSendsAsTheServer

        [Test]
        public async Task ServerConnection_IsAWebSocketServer_AndSendsAsTheServer()
        {

            var mirror  = StartServer();

            await using var client = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{mirror.IPPort}"), SecWebSocketProtocols: [ "ocpp2.0.1" ]);

            var received  = new TaskCompletionSource<String>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sentEvent = new TaskCompletionSource<String>(TaskCreationOptions.RunContinuationsAsynchronously);

            client.OnTextMessageReceived += (timestamp, webSocketClient, connection, frame, eventTrackingId, text, ct) => { received. TrySetResult(text); return Task.CompletedTask; };
            mirror.OnTextMessageSent     += (timestamp, endpoint, connection, frame, eventTrackingId, text, status, ct)  => { sentEvent.TrySetResult(text); return Task.CompletedTask; };

            await client.Connect();

            var accepted = await Within(Task.Run(async () => {
                               while (!mirror.WebSocketConnections.Any())
                                   await Task.Delay(10);
                               return mirror.WebSocketConnections.First();
                           }), "The server's connection");

            IMessageConnection connection = accepted;

            var status = await connection.SendTextMessage("from the server");
            var sent   = await Within(sentEvent.Task, "The server's OnTextMessageSent");
            var got    = await Within(received. Task, "The client's receipt");

            Assert.Multiple(() => {

                Assert.That(connection.Transport,    Is.EqualTo(MessageTransport.WebSocket));
                Assert.That(connection.Role,         Is.EqualTo(ConnectionRole.Server));
                Assert.That(connection.Subprotocol,  Is.EqualTo("ocpp2.0.1"));
                Assert.That(status,                  Is.EqualTo(SentStatus.Success));

                Assert.That(sent, Is.EqualTo("from the server"), "sent past the server's events");
                Assert.That(got,  Is.EqualTo("from the server"));

            });

        }

        #endregion

        #region ClosedClientConnection_SendsNothing

        /// <summary>
        /// A client connection that is closed sends nothing - not even over the client's next connection.
        /// </summary>
        [Test]
        public async Task ClosedClientConnection_SendsNothing()
        {

            var port    = StartServer().IPPort;

            await using var client = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{port}"), SecWebSocketProtocols: [ "ocpp2.1" ]);

            var (webSocketConnection, _) = await client.Connect();

            IMessageConnection connection = webSocketConnection;

            await connection.Close(MessageCloseCode.NormalClosure, "done");

            Assert.That(await connection.SendTextMessage("too late"), Is.EqualTo(SentStatus.FatalError));

        }

        #endregion

    }

}
