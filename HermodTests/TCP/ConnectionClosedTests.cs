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
using System.Net.Sockets;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.TCP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TCP
{

    /// <summary>
    /// A TCP server reports each connection it took on closed once, through
    /// OnTCPConnectionClosed, and says who closed it.
    /// </summary>
    /// <remarks>
    /// Most connections used to be reported twice, and the second report
    /// contradicted the first. TCPConnection.Dispose() reported a connection
    /// closed by the server, and the server, once Dispose() had returned,
    /// reported it closed by the client - whether the client had hung up or
    /// the server's handler had ended. A connection that its handler closed
    /// itself, as Modbus/TCP's does, was reported as the handler said and then
    /// as closed by the client. And an HTTP connection was not reported at all,
    /// unless Stop() closed it: AHTTPServer lists a connection of its own in
    /// the server's place, and the server reported only the connections it
    /// still found listed.
    ///
    /// A second report comes after the first, so counted too early, two look
    /// like one. Each test therefore waits until its server is done with every
    /// connection - until the task in which the server handles a connection,
    /// its report included, has finished - and counts only then. The servers
    /// here keep those tasks: ValidateConnection is asked about every
    /// connection that gets that far, while it is still listed with its task.
    /// </remarks>
    [TestFixture]
    public class ConnectionClosedTests
    {

        #region Data

        /// <summary>
        /// How long a test waits for a server, or for a client, to be done.
        /// </summary>
        private static readonly TimeSpan             Patience             = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Not the two sockets, as by default: a client's port may come round
        /// again, and the reports are counted by connection.
        /// </summary>
        private static readonly ConnectionIdBuilder  UniqueConnectionIds  = (sender, timestamp, localSocket, remoteSocket) => Guid.NewGuid().ToString();

        /// <summary>
        /// An HTTP request that asks for a WebSocket.
        /// </summary>
        private static readonly Byte[]               UpgradeRequest       = Encoding.ASCII.GetBytes(
                                                                                "GET / HTTP/1.1\r\n"                              +
                                                                                "Host: 127.0.0.1\r\n"                             +
                                                                                "Upgrade: websocket\r\n"                          +
                                                                                "Connection: Upgrade\r\n"                         +
                                                                                "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n" +
                                                                                "Sec-WebSocket-Version: 13\r\n"                   +
                                                                                "\r\n"
                                                                            );

        #endregion

        #region (class) HandledConnections

        /// <summary>
        /// The connections a server took on, each with the task in which the
        /// server handles it, from its start to its report that it closed.
        /// </summary>
        private sealed class HandledConnections
        {

            private readonly ConcurrentQueue<(String Id, Task Handling)> connections = new();

            /// <summary>
            /// The identifications of the connections, in the order they came.
            /// </summary>
            public IReadOnlyList<String> Ids
                => connections.Select(connection => connection.Id).ToArray();

            /// <summary>
            /// Note a connection - from ValidateConnection, where it is still
            /// listed with the task that handles it.
            /// </summary>
            public void Add(ConcurrentDictionary<TCPConnection, Task>  ActiveClients,
                            TCPConnection                              Connection)

                => connections.Enqueue((
                       Connection.ConnectionId,
                       ActiveClients.TryGetValue(Connection, out var handling)
                           ? handling
                           : Task.FromException(new InvalidOperationException($"Connection {Connection.ConnectionId} was not listed."))
                   ));

            /// <summary>
            /// Wait until the server has taken on the given number of connections.
            /// </summary>
            public async Task TakenOn(Int32 Number)
            {

                var waited = Stopwatch.StartNew();

                while (connections.Count < Number && waited.Elapsed < Patience)
                    await Task.Delay(10);

                Assert.That(connections.Count, Is.EqualTo(Number), "connections the server took on");

            }

            /// <summary>
            /// Wait until the server has taken on the given number of
            /// connections, and is done with every one of them.
            /// </summary>
            public async Task Done(Int32 Number)
            {

                await TakenOn(Number);

                await Task.WhenAll(connections.Select(connection => connection.Handling)).WaitAsync(Patience);

            }

        }

        #endregion

        #region (class) Reports

        /// <summary>
        /// What a server reported about its connections, through the events of
        /// ATCPServer - which an AWebSocketServer hides behind events of its own
        /// of the same names.
        /// </summary>
        private sealed class Reports
        {

            private readonly ConcurrentQueue<(String ConnectionId, ConnectionClosedBy ClosedBy, String EventTrackingId)>  closed    = new();
            private readonly ConcurrentDictionary<String, String>                                                    opened    = new();
            private readonly ConcurrentQueue<String>                                                                  sequence  = new();

            public Reports(ATCPServer Server)
            {

                Server.OnNewTCPConnection          += (server, timestamp, eventTrackingId, remoteSocket, connectionId, connection) => {
                                                          opened[connectionId] = eventTrackingId.ToString();
                                                          return Task.CompletedTask;
                                                      };

                Server.OnNewTCPConnectionRejected  += (server, timestamp, eventTrackingId, remoteSocket, connectionId, reason) => {
                                                          opened[connectionId] = eventTrackingId.ToString();
                                                          return Task.CompletedTask;
                                                      };

                Server.OnTCPConnectionClosed       += (server, timestamp, eventTrackingId, remoteSocket, connectionId, closedBy) => {
                                                          closed.  Enqueue((connectionId, closedBy, eventTrackingId.ToString()));
                                                          sequence.Enqueue($"closed {connectionId}");
                                                          return Task.CompletedTask;
                                                      };

                Server.OnTCPServerStopped          += (server, timestamp, eventTrackingId, message) => {
                                                          sequence.Enqueue("stopped");
                                                          return Task.CompletedTask;
                                                      };

            }

            /// <summary>
            /// Who closed the given connection, as each report about it says.
            /// </summary>
            public ConnectionClosedBy[] ClosedBy(String ConnectionId)

                => closed.Where (report => report.ConnectionId == ConnectionId).
                          Select(report => report.ClosedBy).
                          ToArray();

            /// <summary>
            /// The event tracking identification of each report about the given
            /// connection.
            /// </summary>
            public String[] TrackedAs(String ConnectionId)

                => closed.Where (report => report.ConnectionId == ConnectionId).
                          Select(report => report.EventTrackingId).
                          ToArray();

            /// <summary>
            /// The event tracking identification of the report that the given
            /// connection was taken on - or refused.
            /// </summary>
            public String? OpenedAs(String ConnectionId)

                => opened.TryGetValue(ConnectionId, out var eventTrackingId)
                       ? eventTrackingId
                       : null;

            /// <summary>
            /// The reports about closed connections and about the server
            /// stopping, in the order they came.
            /// </summary>
            public String[] Sequence
                => sequence.ToArray();

        }

        #endregion

        #region (class) TestTCPServer

        /// <summary>
        /// A TCP server that handles its connections as a test says - or
        /// refuses them, where it says so.
        /// </summary>
        private sealed class TestTCPServer(Func<TCPConnection, CancellationToken, Task>  Handler,
                                           Boolean                                     Refuse   = false)

            : ATCPServer(TCPPort:                  IPPort.Zero,
                         ConnectionIdBuilder:      UniqueConnectionIds,
                         DisableMaintenanceTasks:  true,
                         DisableWardenTasks:       true)

        {

            public HandledConnections Connections { get; } = new();

            public override Task<ConnectionFilterResponse> ValidateConnection(DateTimeOffset     Timestamp,
                                                                              ITCPServer         Server,
                                                                              TCPConnection      Connection,
                                                                              EventTracking_Id   EventTrackingId,
                                                                              CancellationToken  CancellationToken)
            {

                Connections.Add(activeClients, Connection);

                return Task.FromResult(
                           Refuse
                               ? ConnectionFilterResponse.Rejected("Not this one.")
                               : ConnectionFilterResponse.Accepted()
                       );

            }

            protected override Task HandleConnection(TCPConnection      Connection,
                                                     CancellationToken  Token)

                => Handler(Connection, Token);

        }

        #endregion

        #region (class) TestHTTPServer

        /// <summary>
        /// An HTTP server that answers GET / with "ok" - or as a test says.
        /// </summary>
        /// <remarks>
        /// An HTTPServer, and not an HTTPTestServer, which cannot hand a
        /// connection over to a WebSocket server.
        /// </remarks>
        private sealed class TestHTTPServer : HTTPServer
        {

            public HandledConnections Connections { get; } = new();

            public TestHTTPServer(HTTPDelegate? Respond = null)

                : base(TCPPort:                  IPPort.Zero,
                       ConnectionIdBuilder:      UniqueConnectionIds,
                       DisableMaintenanceTasks:  true,
                       DisableWardenTasks:       true)

            {

                AddHTTPAPI().AddHandler(
                    HTTPMethod.GET,
                    HTTPPath.Root,
                    HTTPDelegate: Respond ?? (request => Task.FromResult(
                                                             new HTTPResponse.Builder(request) {
                                                                 HTTPStatusCode  = HTTPStatusCode.OK,
                                                                 ContentType     = HTTPContentType.Text.PLAIN,
                                                                 Content         = "ok".ToUTF8Bytes()
                                                             }.AsImmutable
                                                         ))
                );

            }

            public override Task<ConnectionFilterResponse> ValidateConnection(DateTimeOffset     Timestamp,
                                                                              ITCPServer         Server,
                                                                              TCPConnection      Connection,
                                                                              EventTracking_Id   EventTrackingId,
                                                                              CancellationToken  CancellationToken)
            {

                Connections.Add(activeClients, Connection);

                return base.ValidateConnection(Timestamp, Server, Connection, EventTrackingId, CancellationToken);

            }

        }

        #endregion

        #region (class) TestWebSocketServer

        /// <summary>
        /// A WebSocket server on a port of its own.
        /// </summary>
        private sealed class TestWebSocketServer()

            : WebSocketMirrorServer(HTTPPort:                 IPPort.Zero,
                                    RequireAuthentication:    false,
                                    ConnectionIdBuilder:      UniqueConnectionIds,
                                    DisableMaintenanceTasks:  true,
                                    DisableWardenTasks:       true)

        {

            public HandledConnections Connections { get; } = new();

            public override Task<ConnectionFilterResponse> ValidateConnection(DateTimeOffset     Timestamp,
                                                                              ITCPServer         Server,
                                                                              TCPConnection      Connection,
                                                                              EventTracking_Id   EventTrackingId,
                                                                              CancellationToken  CancellationToken)
            {

                Connections.Add(activeClients, Connection);

                return base.ValidateConnection(Timestamp, Server, Connection, EventTrackingId, CancellationToken);

            }

        }

        #endregion


        #region (private static) ConnectTo(Server)

        /// <summary>
        /// A client, connected to the given server.
        /// </summary>
        private static async Task<TcpClient> ConnectTo(ATCPServer Server)
        {

            var client = new TcpClient();

            await client.ConnectAsync(System.Net.IPAddress.Loopback, Server.TCPPort.ToUInt16()).WaitAsync(Patience);

            return client;

        }

        #endregion

        #region (private static) ReadUntilTheServerHangsUp(Client)

        /// <summary>
        /// Read until the server hangs up, and return what it sent.
        /// </summary>
        private static async Task<String> ReadUntilTheServerHangsUp(TcpClient Client)
        {

            using var timeout   = new CancellationTokenSource(Patience);

            var       received  = new StringBuilder();
            var       buffer    = new Byte[1024];

            try
            {

                Int32 read;

                while ((read = await Client.GetStream().ReadAsync(buffer, timeout.Token)) > 0)
                    received.Append(Encoding.ASCII.GetString(buffer, 0, read));

            }
            catch (IOException)
            {
                // Reset rather than closed: a refused connection is not lingered on.
            }

            return received.ToString();

        }

        #endregion

        #region (private static) Exchange(Client, Request)

        /// <summary>
        /// Send an HTTP request, and read its response: up to the end of its
        /// header, and as much body as the header announces.
        /// </summary>
        private static async Task<String> Exchange(TcpClient  Client,
                                                   String     Request)
        {

            var stream = Client.GetStream();

            await stream.WriteAsync(Encoding.ASCII.GetBytes(Request));

            using var timeout   = new CancellationTokenSource(Patience);

            var       received  = "";
            var       buffer    = new Byte[1024];

            while (true)
            {

                var endOfHeader = received.IndexOf("\r\n\r\n", StringComparison.Ordinal);

                if (endOfHeader >= 0)
                {

                    var contentLength = Regex.Match(received[..endOfHeader], @"(?im)^Content-Length:\s*(\d+)");

                    if (received.Length - endOfHeader - 4 >= (contentLength.Success ? Int32.Parse(contentLength.Groups[1].Value) : 0))
                        return received;

                }

                var read = await stream.ReadAsync(buffer, timeout.Token);

                if (read == 0)
                    return received;

                received += Encoding.ASCII.GetString(buffer, 0, read);

            }

        }

        #endregion

        #region (private static) ReadUntilTheClientHangsUp(Connection, Token)

        /// <summary>
        /// A handler that reads until its client hangs up - and leaves the
        /// connection to the server to close, as handlers do.
        /// </summary>
        private static async Task ReadUntilTheClientHangsUp(TCPConnection      Connection,
                                                            CancellationToken  Token)
        {

            var stream = Connection.TCPClient.GetStream();
            var buffer = new Byte[1024];

            while (await stream.ReadAsync(buffer, Token) > 0)
            { }

        }

        #endregion


        #region AServerThatHangsUpReportsEachConnectionOnce()

        /// <summary>
        /// The handler ends at once, and the server hangs up on clients that
        /// wait for it, one after another: each connection is reported closed
        /// once, by the server, with the event tracking identification it was
        /// reported taken on with.
        /// </summary>
        /// <remarks>
        /// What TCPServer does, whose handler returns at once - and every one of
        /// its connections used to be reported twice: by the server, from
        /// TCPConnection.Dispose(), and then by the client.
        /// </remarks>
        [Test]
        public async Task AServerThatHangsUpReportsEachConnectionOnce()
        {

            await using var server   = new TestTCPServer(Handler: (connection, token) => Task.CompletedTask);
            var             reports  = new Reports(server);

            await server.Start();

            for (var i = 0; i < 3; i++)
            {
                using var client = await ConnectTo(server);
                await ReadUntilTheServerHangsUp(client);
            }

            await server.Connections.Done(3);

            Assert.Multiple(() => {

                foreach (var connectionId in server.Connections.Ids)
                {

                    Assert.That(reports.ClosedBy (connectionId),
                                Is.EqualTo(new[] { ConnectionClosedBy.Server }),
                                $"reports that connection {connectionId} closed, and by whom");

                    Assert.That(reports.TrackedAs(connectionId),
                                Is.EqualTo(new[] { reports.OpenedAs(connectionId) }),
                                $"their event tracking identifications, against that of connection {connectionId}'s acceptance");

                }

            });

        }

        #endregion

        #region AClientThatHangsUpIsReportedOnceAsTheOneWhoClosed()

        /// <summary>
        /// The client hangs up, and the handler, which reads until it does,
        /// ends: the connection is reported closed once, by the client.
        /// </summary>
        [Test]
        public async Task AClientThatHangsUpIsReportedOnceAsTheOneWhoClosed()
        {

            await using var server   = new TestTCPServer(Handler: ReadUntilTheClientHangsUp);
            var             reports  = new Reports(server);

            await server.Start();

            using (var client = await ConnectTo(server))
            {
                await client.GetStream().WriteAsync("Hello"u8.ToArray());
            }

            await server.Connections.Done(1);

            Assert.That(reports.ClosedBy(server.Connections.Ids.Single()),
                        Is.EqualTo(new[] { ConnectionClosedBy.Client }),
                        "reports that the connection closed, and by whom");

        }

        #endregion

        #region AHandlerThatClosesItsConnectionSaysWhoClosedIt()

        /// <summary>
        /// A handler that closes its connection itself says who closed it, as
        /// Modbus/TCP's does, and the report says the same - here that an admin
        /// threw the client off, which only the handler can know.
        /// </summary>
        /// <remarks>
        /// Once the handler has closed the connection, the server cannot look
        /// at the socket any more. Close() has to keep what it was told for the
        /// report, or the answer would be the server's, which it gives when it
        /// cannot tell.
        /// </remarks>
        [Test]
        public async Task AHandlerThatClosesItsConnectionSaysWhoClosedIt()
        {

            await using var server   = new TestTCPServer(Handler: async (connection, token) => {

                                                             // One byte, and the client is thrown off.
                                                             await connection.TCPClient.GetStream().ReadExactlyAsync(new Byte[1], token);

                                                             connection.Close(ConnectionClosedBy.Admin);

                                                         });
            var             reports  = new Reports(server);

            await server.Start();

            using (var client = await ConnectTo(server))
            {
                await client.GetStream().WriteAsync(new Byte[] { 0x2A });
                await ReadUntilTheServerHangsUp(client);
            }

            await server.Connections.Done(1);

            Assert.That(reports.ClosedBy(server.Connections.Ids.Single()),
                        Is.EqualTo(new[] { ConnectionClosedBy.Admin }),
                        "reports that the connection closed, and by whom");

        }

        #endregion

        #region ARefusedConnectionIsReportedOnceAsClosedByAFilterRule()

        /// <summary>
        /// ValidateConnection refuses the connection: it is reported closed
        /// once, by a filter rule, with the event tracking identification of
        /// the refusal.
        /// </summary>
        [Test]
        public async Task ARefusedConnectionIsReportedOnceAsClosedByAFilterRule()
        {

            await using var server   = new TestTCPServer(Handler: (connection, token) => Task.CompletedTask,
                                                         Refuse:  true);
            var             reports  = new Reports(server);

            await server.Start();

            using (var client = await ConnectTo(server))
            {
                await ReadUntilTheServerHangsUp(client);
            }

            await server.Connections.Done(1);

            var connectionId = server.Connections.Ids.Single();

            Assert.Multiple(() => {

                Assert.That(reports.ClosedBy (connectionId),
                            Is.EqualTo(new[] { ConnectionClosedBy.FilterRule }),
                            "reports that the connection closed, and by whom");

                Assert.That(reports.TrackedAs(connectionId),
                            Is.EqualTo(new[] { reports.OpenedAs(connectionId) }),
                            "their event tracking identifications, against that of the refusal");

            });

        }

        #endregion

        #region AConnectionThatStopClosesIsReportedOnceBeforeTheServerHasStopped()

        /// <summary>
        /// Stop() closes a connection whose client is still there: it is
        /// reported closed once, by the server, and before the server reports
        /// that it has stopped.
        /// </summary>
        /// <remarks>
        /// Stop() takes a connection off the server's list before it closes it,
        /// and the server used to report only the connections it still found
        /// listed. While Close() reported as well, that came to once. Now that
        /// only the server reports, the list must not decide whether it does.
        /// </remarks>
        [Test]
        public async Task AConnectionThatStopClosesIsReportedOnceBeforeTheServerHasStopped()
        {

            await using var server   = new TestTCPServer(Handler: ReadUntilTheClientHangsUp);
            var             reports  = new Reports(server);

            await server.Start();

            using var client = await ConnectTo(server);

            await server.Connections.TakenOn(1);

            await server.Stop();

            await server.Connections.Done(1);

            var connectionId = server.Connections.Ids.Single();

            Assert.Multiple(() => {

                Assert.That(reports.ClosedBy(connectionId),
                            Is.EqualTo(new[] { ConnectionClosedBy.Server }),
                            "reports that the connection closed, and by whom");

                Assert.That(reports.Sequence,
                            Is.EqualTo(new[] { $"closed {connectionId}", "stopped" }),
                            "what the server reported, in order");

            });

        }

        #endregion

        #region AnHTTPClientThatHangsUpIsReportedOnceAsTheOneWhoClosed()

        /// <summary>
        /// An HTTP client asks once, keeps its connection open as HTTP/1.1 does,
        /// and then hangs up: the connection is reported closed once, by the
        /// client.
        /// </summary>
        /// <remarks>
        /// HTTP connections were not reported at all, unless Stop() closed
        /// them: AHTTPServer lists a connection of its own in the server's
        /// place, and takes it off when it is done. The server then no longer
        /// found its own connection listed, and reported nothing.
        /// </remarks>
        [Test]
        public async Task AnHTTPClientThatHangsUpIsReportedOnceAsTheOneWhoClosed()
        {

            await using var server   = new TestHTTPServer();
            var             reports  = new Reports(server);

            await server.Start();

            using (var client = await ConnectTo(server))
            {

                var response = await Exchange(client, "GET / HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n");

                Assert.That(response, Does.StartWith("HTTP/1.1 200"), response);

            }

            await server.Connections.Done(1);

            Assert.That(reports.ClosedBy(server.Connections.Ids.Single()),
                        Is.EqualTo(new[] { ConnectionClosedBy.Client }),
                        "reports that the connection closed, and by whom");

        }

        #endregion

        #region AnHTTPConnectionTheServerClosesIsReportedOnceAsClosedByTheServer()

        /// <summary>
        /// An HTTP client asks for its connection to be closed after the
        /// response, and waits for the server to close it: the connection is
        /// reported closed once, by the server.
        /// </summary>
        [Test]
        public async Task AnHTTPConnectionTheServerClosesIsReportedOnceAsClosedByTheServer()
        {

            await using var server   = new TestHTTPServer();
            var             reports  = new Reports(server);

            await server.Start();

            using (var client = await ConnectTo(server))
            {

                await client.GetStream().WriteAsync(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n"));

                var response = await ReadUntilTheServerHangsUp(client);

                Assert.That(response, Does.StartWith("HTTP/1.1 200"), response);

            }

            await server.Connections.Done(1);

            Assert.That(reports.ClosedBy(server.Connections.Ids.Single()),
                        Is.EqualTo(new[] { ConnectionClosedBy.Server }),
                        "reports that the connection closed, and by whom");

        }

        #endregion

        #region AnHTTPConnectionThatStopClosesIsReportedOnceBeforeTheServerHasStopped()

        /// <summary>
        /// Stop() closes an HTTP connection that is kept open between requests:
        /// it is reported closed once, by the server, and before the server
        /// reports that it has stopped.
        /// </summary>
        /// <remarks>
        /// Reported under the identification of the connection, the one it was
        /// reported taken on with - and not under that of the HTTPConnection
        /// that AHTTPServer lists in its place, which is the one Stop() closes.
        /// The two are the same only where the ConnectionIdBuilder makes the
        /// identification out of the two sockets, as it does by default; this
        /// fixture's makes up a new one every time it is asked.
        /// </remarks>
        [Test]
        public async Task AnHTTPConnectionThatStopClosesIsReportedOnceBeforeTheServerHasStopped()
        {

            await using var server    = new TestHTTPServer();
            var             reports   = new Reports(server);

            await server.Start();

            using var       client    = await ConnectTo(server);

            var             response  = await Exchange(client, "GET / HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n");

            await server.Stop();

            await server.Connections.Done(1);

            var connectionId = server.Connections.Ids.Single();

            Assert.Multiple(() => {

                Assert.That(response, Does.StartWith("HTTP/1.1 200"), response);

                Assert.That(reports.ClosedBy(connectionId),
                            Is.EqualTo(new[] { ConnectionClosedBy.Server }),
                            "reports that the connection closed, and by whom");

                Assert.That(reports.Sequence,
                            Is.EqualTo(new[] { $"closed {connectionId}", "stopped" }),
                            "what the server reported, in order");

            });

        }

        #endregion

        #region AConnectionRefusedOnAWebSocketPathIsReportedOnceAsClosedByAFilterRule()

        /// <summary>
        /// A WebSocket server lent to an HTTP server refuses a client that asks
        /// for a WebSocket: the HTTP server reports the connection closed once,
        /// by a filter rule - as a server on a port of its own reports one that
        /// it refuses.
        /// </summary>
        [Test]
        public async Task AConnectionRefusedOnAWebSocketPathIsReportedOnceAsClosedByAFilterRule()
        {

            await using var lentServer  = new WebSocketMirrorServer(RequireAuthentication:  false,
                                                                    AutoStart:              false);

            lentServer.OnValidateTCPConnection += (timestamp, webSocketServer, connection, eventTrackingId, cancellationToken) =>
                                                      Task.FromResult(ConnectionFilterResponse.Rejected("Not this one."));

            await using var server      = new TestHTTPServer(Respond: WebSocketUpgrade.For(lentServer));
            var             reports     = new Reports(server);

            await server.Start();

            using (var client = await ConnectTo(server))
            {

                await client.GetStream().WriteAsync(UpgradeRequest);

                var response = await ReadUntilTheServerHangsUp(client);

                Assert.That(response, Does.StartWith("HTTP/1.1 403"), response);

            }

            await server.Connections.Done(1);

            Assert.That(reports.ClosedBy(server.Connections.Ids.Single()),
                        Is.EqualTo(new[] { ConnectionClosedBy.FilterRule }),
                        "reports that the connection closed, and by whom");

        }

        #endregion

        #region (private static) Upgrade(Client)

        /// <summary>
        /// Ask for a WebSocket, and read the answer: a 101.
        /// </summary>
        private static async Task Upgrade(TcpClient Client)
        {

            var response = await Exchange(Client, Encoding.ASCII.GetString(UpgradeRequest));

            Assert.That(response, Does.StartWith("HTTP/1.1 101"), response);

        }

        #endregion

        #region AWebSocketServerReportsEachConnectionOnce()

        /// <summary>
        /// A WebSocket server on a port of its own reports each connection
        /// closed once through OnTCPConnectionClosed of ATCPServer - beside the
        /// event of that name of its own - and by the client, which hung up
        /// before it asked for anything.
        /// </summary>
        /// <remarks>
        /// The WebSocket server closes the socket in its connection loop, before
        /// the server can look at it. Until the loop said who closed it, all the
        /// server could say was that it had closed the connection itself.
        /// </remarks>
        [Test]
        public async Task AWebSocketServerReportsEachConnectionOnce()
        {

            await using var server   = new TestWebSocketServer();
            var             reports  = new Reports(server);

            await server.Start();

            // Each client hangs up without a word.
            for (var i = 0; i < 3; i++)
            {
                using var client = await ConnectTo(server);
            }

            await server.Connections.Done(3);

            Assert.Multiple(() => {

                foreach (var connectionId in server.Connections.Ids)
                    Assert.That(reports.ClosedBy(connectionId),
                                Is.EqualTo(new[] { ConnectionClosedBy.Client }),
                                $"reports that connection {connectionId} closed, and by whom");

            });

        }

        #endregion

        #region AWebSocketClientThatSendsACloseFrameIsReportedOnceAsTheOneWhoClosed()

        /// <summary>
        /// A WebSocket client sends a close frame, and waits for the server to
        /// answer it and hang up: the connection is reported closed once, by
        /// the client.
        /// </summary>
        /// <remarks>
        /// The client is still there when the server hangs up, so the socket
        /// cannot tell who closed: only the loop that read the close frame can.
        /// </remarks>
        [Test]
        public async Task AWebSocketClientThatSendsACloseFrameIsReportedOnceAsTheOneWhoClosed()
        {

            await using var server   = new TestWebSocketServer();
            var             reports  = new Reports(server);

            await server.Start();

            using (var client = await ConnectTo(server))
            {

                await Upgrade(client);

                await client.GetStream().WriteAsync(WebSocketFrame.Close(WebSocketFrame.ClosingStatusCode.NormalClosure,
                                                                         Mask:        WebSocketFrame.MaskStatus.On,
                                                                         MaskingKey:  [ 0x12, 0x34, 0x56, 0x78 ]).ToByteArray());

                await ReadUntilTheServerHangsUp(client);

            }

            await server.Connections.Done(1);

            Assert.That(reports.ClosedBy(server.Connections.Ids.Single()),
                        Is.EqualTo(new[] { ConnectionClosedBy.Client }),
                        "reports that the connection closed, and by whom");

        }

        #endregion

        #region AWebSocketClientThatDropsItsConnectionIsReportedOnceAsTheOneWhoClosed(Reset)

        /// <summary>
        /// A WebSocket client drops its connection without a close frame -
        /// closes it, or resets it: the connection is reported closed once, by
        /// the client.
        /// </summary>
        [TestCase(false, TestName = "AWebSocketClientThatDropsItsConnectionIsReportedOnceAsTheOneWhoClosed(closed)")]
        [TestCase(true,  TestName = "AWebSocketClientThatDropsItsConnectionIsReportedOnceAsTheOneWhoClosed(reset)")]
        public async Task AWebSocketClientThatDropsItsConnectionIsReportedOnceAsTheOneWhoClosed(Boolean Reset)
        {

            await using var server   = new TestWebSocketServer();
            var             reports  = new Reports(server);

            await server.Start();

            using (var client = await ConnectTo(server))
            {

                await Upgrade(client);

                // An RST instead of a FIN: the socket closed without lingering,
                // and closed itself. Disposing the client shuts its stream
                // down first, and that sends a FIN before the RST.
                if (Reset)
                {
                    client.Client.LingerState = new LingerOption(true, 0);
                    client.Client.Close();
                }

            }

            await server.Connections.Done(1);

            Assert.That(reports.ClosedBy(server.Connections.Ids.Single()),
                        Is.EqualTo(new[] { ConnectionClosedBy.Client }),
                        "reports that the connection closed, and by whom");

        }

        #endregion

        #region AWebSocketConnectionTheServerClosesIsReportedOnceAsClosedByTheServer()

        /// <summary>
        /// The WebSocket server closes a connection whose client is still
        /// there, and goes on running: the connection is reported closed once,
        /// by the server.
        /// </summary>
        /// <remarks>
        /// Close() closes the socket and then cancels the loop's read. On
        /// Windows the read mostly ends cancelled; on Linux it fails, as though
        /// the client had reset the connection. So the close has to say that it
        /// was the server's before it closes anything: without that, this test
        /// and the next failed 5 of 5 on Linux, and passed on Windows.
        /// </remarks>
        [Test]
        public async Task AWebSocketConnectionTheServerClosesIsReportedOnceAsClosedByTheServer()
        {

            await using var server   = new TestWebSocketServer();
            var             reports  = new Reports(server);

            await server.Start();

            using (var client = await ConnectTo(server))
            {

                await Upgrade(client);

                await server.WebSocketConnections.Single().Close(WebSocketFrame.ClosingStatusCode.NormalClosure);

                await ReadUntilTheServerHangsUp(client);

            }

            await server.Connections.Done(1);

            Assert.That(reports.ClosedBy(server.Connections.Ids.Single()),
                        Is.EqualTo(new[] { ConnectionClosedBy.Server }),
                        "reports that the connection closed, and by whom");

        }

        #endregion

        #region AWebSocketConnectionTheServerShutsDownIsReportedOnceAsClosedByTheServer()

        /// <summary>
        /// The WebSocket server shuts down, and closes a connection whose
        /// client is still there: the connection is reported closed once, by
        /// the server.
        /// </summary>
        /// <remarks>
        /// Shutdown() closes each connection with a close frame of its own
        /// first, and only then stops the server. In between, the loop's read
        /// can fail as though the client had reset the connection - measured:
        /// an IOException, OperationAborted.
        /// </remarks>
        [Test]
        public async Task AWebSocketConnectionTheServerShutsDownIsReportedOnceAsClosedByTheServer()
        {

            await using var server   = new TestWebSocketServer();
            var             reports  = new Reports(server);

            await server.Start();

            using var client = await ConnectTo(server);

            await Upgrade(client);

            await server.Shutdown("Bye.");

            await ReadUntilTheServerHangsUp(client);

            await server.Connections.Done(1);

            Assert.That(reports.ClosedBy(server.Connections.Ids.Single()),
                        Is.EqualTo(new[] { ConnectionClosedBy.Server }),
                        "reports that the connection closed, and by whom");

        }

        #endregion

        #region AWebSocketConnectionWhoseClientMissesItsPingsIsReportedOnceAsClosedByTheServer()

        /// <summary>
        /// A WebSocket client says nothing, not even a pong, for longer than
        /// the server waits, while its connection stays open: the server gives
        /// it up, and the connection is reported closed once, by the server.
        /// </summary>
        /// <remarks>
        /// The loop ends without a close of its own here, and closes the
        /// connection on its way out.
        /// </remarks>
        [Test]
        public async Task AWebSocketConnectionWhoseClientMissesItsPingsIsReportedOnceAsClosedByTheServer()
        {

            await using var server   = new TestWebSocketServer() {
                                           WebSocketPingEvery   = TimeSpan.FromMilliseconds(200),
                                           MaxOutstandingPings  = 1
                                       };
            var             reports  = new Reports(server);

            await server.Start();

            using (var client = await ConnectTo(server))
            {

                await Upgrade(client);

                await ReadUntilTheServerHangsUp(client);

            }

            await server.Connections.Done(1);

            Assert.That(reports.ClosedBy(server.Connections.Ids.Single()),
                        Is.EqualTo(new[] { ConnectionClosedBy.Server }),
                        "reports that the connection closed, and by whom");

        }

        #endregion

        #region AWebSocketConnectionFailedForAProtocolErrorIsReportedOnceAsClosedByTheServer()

        /// <summary>
        /// A WebSocket client sends a frame without a mask, which a client must
        /// not, and the server fails the connection: it is reported closed
        /// once, by the server - although the client sent the frame that ended
        /// it.
        /// </summary>
        [Test]
        public async Task AWebSocketConnectionFailedForAProtocolErrorIsReportedOnceAsClosedByTheServer()
        {

            await using var server   = new TestWebSocketServer();
            var             reports  = new Reports(server);

            await server.Start();

            using (var client = await ConnectTo(server))
            {

                await Upgrade(client);

                await client.GetStream().WriteAsync(WebSocketFrame.Text("Hello").ToByteArray());

                await ReadUntilTheServerHangsUp(client);

            }

            await server.Connections.Done(1);

            Assert.That(reports.ClosedBy(server.Connections.Ids.Single()),
                        Is.EqualTo(new[] { ConnectionClosedBy.Server }),
                        "reports that the connection closed, and by whom");

        }

        #endregion

    }

}
