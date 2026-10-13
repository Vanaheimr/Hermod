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

using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Collections.Concurrent;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.TCP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// A WebSocket endpoint: the protocol on an HTTP path, without a TCP server of its own -
    /// and a WebSocket server, an endpoint with a listener it owns.
    /// </summary>
    [TestFixture]
    public class WebSocketEndpointTests
    {

        #region Data

        private static readonly TimeSpan  Patience  = TimeSpan.FromSeconds(10);

        private HTTPServer?               httpServer;
        private RecordingEndpoint?        endpoint;

        #endregion

        #region (class) RecordingEndpoint

        /// <summary>
        /// An endpoint that echoes text, and remembers which TCP server asked it about each connection.
        /// </summary>
        private sealed class RecordingEndpoint : WebSocketEndpoint
        {

            public ConcurrentQueue<ITCPServer> AskedBy { get; } = new();

            public RecordingEndpoint()

                : base(RequireAuthentication: false)

            {

                OnTextMessageReceived += async (timestamp, server, connection, frame, eventTrackingId, text, cancellationToken) =>
                    await SendTextMessage(connection, $"Echo: {text}", CancellationToken: cancellationToken);

            }

            public override Task<ConnectionFilterResponse> ValidateConnection(DateTimeOffset     Timestamp,
                                                                              ITCPServer         Server,
                                                                              TCPConnection      Connection,
                                                                              EventTracking_Id   EventTrackingId,
                                                                              CancellationToken  CancellationToken)
            {

                AskedBy.Enqueue(Server);

                return base.ValidateConnection(Timestamp, Server, Connection, EventTrackingId, CancellationToken);

            }

        }

        #endregion

        #region Setting up / tearing down

        [SetUp]
        public async Task Init()
        {

            httpServer  = await HTTPServer.StartNew();
            endpoint    = new RecordingEndpoint();

            httpServer.AddHTTPAPI().AddHandler(
                HTTPMethod.GET,
                HTTPPath.Parse("/ws"),
                HTTPDelegate: WebSocketUpgrade.For(endpoint)
            );

        }

        [TearDown]
        public async Task Shutdown()
        {

            if (httpServer is not null)
                await httpServer.DisposeAsync();

            if (endpoint is not null)
                await endpoint.DisposeAsync();

            httpServer  = null;
            endpoint    = null;

        }

        #endregion

        #region (private) ConnectAsync()

        private async Task<ClientWebSocket> ConnectAsync()
        {

            var client = new ClientWebSocket();

            await client.ConnectAsync(new Uri($"ws://127.0.0.1:{httpServer!.TCPPort}/ws"), CancellationToken.None).WaitAsync(Patience);

            return client;

        }

        #endregion

        #region (private static) ReceiveTextAsync(Client)

        private static async Task<String> ReceiveTextAsync(ClientWebSocket Client)
        {

            var buffer  = new Byte[4096];
            var result  = await Client.ReceiveAsync(buffer, CancellationToken.None).WaitAsync(Patience);

            return Encoding.UTF8.GetString(buffer, 0, result.Count);

        }

        #endregion


        #region AnEndpointOnAnHTTPPathSpeaksWebSocket()

        /// <summary>
        /// An endpoint speaks WebSocket on the path it is put on, and is no TCP server.
        /// </summary>
        [Test]
        public async Task AnEndpointOnAnHTTPPathSpeaksWebSocket()
        {

            using var client = await ConnectAsync();

            await client.SendAsync(Encoding.UTF8.GetBytes("hello"), WebSocketMessageType.Text, true, CancellationToken.None);

            var answer = await ReceiveTextAsync(client);

            Assert.Multiple(() => {
                Assert.That(answer,                            Is.EqualTo("Echo: hello"));
                Assert.That(endpoint,                          Is.Not.InstanceOf<ITCPServer>());
                Assert.That(endpoint!.WebSocketConnections,    Has.Exactly(1).Items);
            });

        }

        #endregion

        #region TheHTTPServerThatAcceptedAConnectionIsTheOneAsking()

        /// <summary>
        /// A connection handed over is validated with the HTTP server that accepted it as the TCP server.
        /// </summary>
        [Test]
        public async Task TheHTTPServerThatAcceptedAConnectionIsTheOneAsking()
        {

            using var client = await ConnectAsync();

            Assert.That(endpoint!.AskedBy.ToArray(), Is.EqualTo(new ITCPServer[] { httpServer! }));

        }

        #endregion

        #region DisposingAnEndpointTellsItsClientsItGoesAway()

        /// <summary>
        /// Disposing of an endpoint closes its connections with 1001, going away.
        /// </summary>
        [Test]
        public async Task DisposingAnEndpointTellsItsClientsItGoesAway()
        {

            using var client  = await ConnectAsync();

            var disposing     = endpoint!.DisposeAsync().AsTask();

            var buffer        = new Byte[4096];
            var result        = await client.ReceiveAsync(buffer, CancellationToken.None).WaitAsync(Patience);

            await disposing.WaitAsync(Patience);

            Assert.Multiple(() => {
                Assert.That(result.MessageType,                Is.EqualTo(WebSocketMessageType.Close));
                Assert.That(result.CloseStatus,                Is.EqualTo(WebSocketCloseStatus.EndpointUnavailable));
            });

        }

        #endregion

        #region AServerIsTheSenderOfWhatItsListenerReports()

        /// <summary>
        /// A server on a port of its own owns its listener: what the listener reports, e.g. a connection
        /// kept out by OnValidateTCPConnection, is reported by the server, with the server as its sender.
        /// </summary>
        [Test]
        public async Task AServerIsTheSenderOfWhatItsListenerReports()
        {

            await using var server  = new WebSocketServer(HTTPPort:                 IPPort.Zero,
                                                          RequireAuthentication:    false,
                                                          DisableMaintenanceTasks:  true,
                                                          DisableWardenTasks:       true);

            var started   = new TaskCompletionSource<AWebSocketServer>(TaskCreationOptions.RunContinuationsAsynchronously);
            var rejected  = new TaskCompletionSource<ITCPServer>      (TaskCreationOptions.RunContinuationsAsynchronously);

            server.OnServerStarted             += (timestamp, webSocketServer, eventTrackingId, cancellationToken) => {
                started.TrySetResult(webSocketServer);
                return Task.CompletedTask;
            };

            server.OnValidateTCPConnection     += (timestamp, webSocketServer, tcpClient, eventTrackingId, cancellationToken) =>
                Task.FromResult(ConnectionFilterResponse.Rejected("Not today."));

            server.OnNewTCPConnectionRejected  += (tcpServer, timestamp, eventTrackingId, remoteSocket, connectionId, reason) => {
                rejected.TrySetResult(tcpServer);
                return Task.CompletedTask;
            };

            await server.Start();

            using var client = new TcpClient();
            await client.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToUInt16()).WaitAsync(Patience);

            var startedBy   = await started. Task.WaitAsync(Patience);
            var rejectedBy  = await rejected.Task.WaitAsync(Patience);

            Assert.Multiple(() => {
                Assert.That(startedBy,                                Is.SameAs(server));
                Assert.That(rejectedBy,                               Is.SameAs(server));
                Assert.That(server.TCPListener.IsRunning,             Is.True);
            });

        }

        #endregion

    }

}
