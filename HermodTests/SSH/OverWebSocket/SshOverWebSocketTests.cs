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

using System.Net.WebSockets;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Client;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Server;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// SSH over WebSocket: an SSH connection in the binary messages of a WebSocket of its
    /// own, either way round - the SSH client connecting out to an SSH server behind a
    /// WebSocket endpoint, and an SSH server connecting out, as a charging station behind a
    /// firewall does, to a back end that runs the SSH client on what it accepted.
    /// </summary>
    [TestFixture]
    public class SshOverWebSocketTests
    {

        #region Data

        private static readonly TimeSpan  Patience  = TimeSpan.FromSeconds(10);

        private HTTPServer?            httpServer;
        private SshWebSocketEndpoint?  endpoint;

        private readonly ISshHostKey   hostKey   = SshHostKey.GenerateEd25519();
        private readonly ISshHostKey   userKey   = SshHostKey.GenerateEd25519();

        #endregion

        #region Setting up / tearing down

        [TearDown]
        public async Task Shutdown()
        {

            if (endpoint is not null)
                await endpoint.DisposeAsync();

            if (httpServer is not null)
                await httpServer.DisposeAsync();

            endpoint    = null;
            httpServer  = null;

        }

        #endregion

        #region (private) Helpers

        // An HTTP server with the endpoint on /ssh.
        private async Task<URL> AnEndpoint(SshWebSocketConnectionHandler  OnConnection,
                                           Boolean                        RequireAuthentication  = false,
                                           Int32                          MessageSize            = SshOverWebSocket.DefaultMessageSize)
        {

            httpServer  = await HTTPServer.StartNew();
            endpoint    = new SshWebSocketEndpoint(OnConnection, RequireAuthentication: RequireAuthentication) {
                              MessageSize = MessageSize
                          };

            httpServer.AddHTTPAPI().AddHandler(
                HTTPMethod.GET,
                HTTPPath.Parse("/ssh"),
                HTTPDelegate: WebSocketUpgrade.For(endpoint)
            );

            return URL.Parse($"ws://127.0.0.1:{httpServer.TCPPort}/ssh");

        }

        // An SSH server that runs every command by saying it back, and its output
        // as long as the command asks for, in "repeat <n>".
        private SshServer AnSshServer()

            => new (new SshServerOptions {
                   HostKeys       = [ hostKey ],
                   Authenticator  = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob),
                   ExecHandler    = async (ctx, ct) => {

                                        if (ctx.Command.StartsWith("repeat ", StringComparison.Ordinal))
                                        {
                                            var count = Int32.Parse(ctx.Command["repeat ".Length..]);
                                            await ctx.WriteAsync(Repeated(count), ct);
                                        }
                                        else
                                            await ctx.WriteAsync($"ran: {ctx.Command}\n", ct);

                                        return 0;

                                    }
               });

        private SSHClientOptions AnSshClient()

            => new () {
                   Username       = User_Id.Parse("operator"),
                   VerifyHostKey  = blob => blob.AsSpan().SequenceEqual(hostKey.PublicKeyBlob),
                   Credentials    = [ userKey ]
               };

        // Bytes that say where they are, so that a byte out of place is seen.
        private static Byte[] Repeated(Int32 Count)
        {

            var bytes = new Byte[Count];

            for (var i = 0; i < Count; i++)
                bytes[i] = (Byte) ('a' + i % 26);

            return bytes;

        }

        #endregion


        #region TheClientConnectsOut_ToAServerBehindTheEndpoint

        /// <summary>
        /// The SSH client opens the WebSocket, the SSH server is behind the endpoint: a
        /// command runs, end to end, with SSH's own host key and user key.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task TheClientConnectsOut_ToAServerBehindTheEndpoint(CancellationToken CancellationToken)
        {

            await using var server     = AnSshServer();
            var             url        = await AnEndpoint((connection, ct) => server.ServeAsync(connection.Pipe, CancellationToken: ct));

            using var       webSocket  = new SshWebSocketClient(url);
            await using var client     = await SSHClient.Connect(webSocket, AnSshClient(), CancellationToken);

            var result = await client.Execute("whoami", CancellationToken);

            Assert.Multiple(() => {
                Assert.That(result.ExitCode,        Is.EqualTo(0));
                Assert.That(result.StandardOutput,  Is.EqualTo("ran: whoami\n"));
                Assert.That(client.ServerHostKey,   Is.EqualTo(hostKey.PublicKeyBlob));
            });

        }

        #endregion

        #region TheServerConnectsOut_ToAClientBehindTheEndpoint

        /// <summary>
        /// The roles the other way round: the SSH server opens the WebSocket - a charging
        /// station that may only connect out -, and the back end runs the SSH client on the
        /// connection it accepted. Once the client is done, the station's server has ended too.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task TheServerConnectsOut_ToAClientBehindTheEndpoint(CancellationToken CancellationToken)
        {

            var ran = new TaskCompletionSource<SshCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            var url = await AnEndpoint(async (connection, ct) => {

                          try
                          {
                              await using var client = await SSHClient.ConnectOver(connection.Pipe, AnSshClient(), ct);
                              ran.TrySetResult(await client.Execute("hostname", ct));
                          }
                          catch (Exception exception)
                          {
                              ran.TrySetException(exception);
                          }

                      });

            await using var station    = AnSshServer();
            using var       webSocket  = new SshWebSocketClient(url);

            var serving = station.ConnectOut(webSocket, CancellationToken);

            var result  = await ran.Task.WaitAsync(Patience, CancellationToken);

            Assert.Multiple(() => {
                Assert.That(result.ExitCode,        Is.EqualTo(0));
                Assert.That(result.StandardOutput,  Is.EqualTo("ran: hostname\n"));
            });

            // The back end's client is gone, and with it the WebSocket: the station serves no more.
            await serving.WaitAsync(Patience, CancellationToken);

        }

        #endregion

        #region PacketsAcrossMessages_AndMessagesOfOneByte

        /// <summary>
        /// Where the messages are cut has no meaning: the endpoint sends one byte a message,
        /// the client seven, so that every SSH packet spans many messages - and still the
        /// handshake, the authentication and a command go through.
        /// </summary>
        [Test]
        [CancelAfter(60000)]
        public async Task PacketsAcrossMessages_AndMessagesOfOneByte(CancellationToken CancellationToken)
        {

            await using var server     = AnSshServer();
            var             url        = await AnEndpoint((connection, ct) => server.ServeAsync(connection.Pipe, CancellationToken: ct),
                                                          MessageSize: 1);

            using var       webSocket  = new SshWebSocketClient(url) { MessageSize = 7 };
            await using var client     = await SSHClient.Connect(webSocket, AnSshClient(), CancellationToken);

            var result = await client.Execute("uname", CancellationToken);

            Assert.That(result.StandardOutput, Is.EqualTo("ran: uname\n"));

        }

        #endregion

        #region MuchOutput_ComesThroughWhole

        /// <summary>
        /// Far more than the receive buffer and many messages of their full size: 8 MiB of
        /// output come through whole and in order, the WebSocket read no further while SSH
        /// is behind.
        /// </summary>
        [Test]
        [CancelAfter(60000)]
        public async Task MuchOutput_ComesThroughWhole(CancellationToken CancellationToken)
        {

            const Int32 size = 8 * 1024 * 1024;

            await using var server     = AnSshServer();
            var             url        = await AnEndpoint((connection, ct) => server.ServeAsync(connection.Pipe, CancellationToken: ct));

            using var       webSocket  = new SshWebSocketClient(url);
            await using var client     = await SSHClient.Connect(webSocket, AnSshClient(), CancellationToken);

            var result = await client.Execute($"repeat {size}", CancellationToken);

            Assert.That(Encoding.Latin1.GetBytes(result.StandardOutput), Is.EqualTo(Repeated(size)));

        }

        #endregion

        #region ATextMessage_IsClosedWith1003

        /// <summary>
        /// SSH goes in binary messages alone: a text message is answered with a close of 1003.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task ATextMessage_IsClosedWith1003(CancellationToken CancellationToken)
        {

            await using var server     = AnSshServer();
            var             url        = await AnEndpoint((connection, ct) => server.ServeAsync(connection.Pipe, CancellationToken: ct));

            using var       webSocket  = new ClientWebSocket();
            webSocket.Options.AddSubProtocol(SshOverWebSocket.Subprotocol);

            await webSocket.ConnectAsync(new Uri(url.ToString()), CancellationToken).WaitAsync(Patience, CancellationToken);

            await webSocket.SendAsync(Encoding.UTF8.GetBytes("SSH-2.0-but-as-text\r\n"), WebSocketMessageType.Text, true, CancellationToken);

            var buffer = new Byte[64 * 1024];

            // The server's identification may come first, in a binary message.
            while (true)
            {

                var received = await webSocket.ReceiveAsync(buffer, CancellationToken).WaitAsync(Patience, CancellationToken);

                if (received.MessageType == WebSocketMessageType.Close)
                    break;

            }

            Assert.That((Int32?) webSocket.CloseStatus, Is.EqualTo(1003));

        }

        #endregion

        #region AnotherSubprotocol_IsRefused

        /// <summary>
        /// A client that does not offer SSH over WebSocket - another subprotocol, or none -
        /// is refused before anything is read as SSH, and the handler is never called.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task AnotherSubprotocol_IsRefused(CancellationToken CancellationToken)
        {

            var called  = 0;
            var url     = await AnEndpoint((connection, ct) => { Interlocked.Increment(ref called); return Task.CompletedTask; });

            using var ocpp = new ClientWebSocket();
            ocpp.Options.AddSubProtocol("ocpp2.1");

            using var none = new ClientWebSocket();

            await Assert.ThrowsAsync<WebSocketException>(() => ocpp.ConnectAsync(new Uri(url.ToString()), CancellationToken).WaitAsync(Patience, CancellationToken));
            await Assert.ThrowsAsync<WebSocketException>(() => none.ConnectAsync(new Uri(url.ToString()), CancellationToken).WaitAsync(Patience, CancellationToken));

            Assert.That(called, Is.Zero);

        }

        #endregion

        #region TheHTTPAuthentication_SaysWhoMayReachSSH

        /// <summary>
        /// Who may reach the SSH handshake at all is said by the HTTP authentication of the
        /// upgrade: without credentials the client is refused with 401 and the handler never
        /// called; with them the handler learns who connected.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task TheHTTPAuthentication_SaysWhoMayReachSSH(CancellationToken CancellationToken)
        {

            var logins  = new List<String?>();

            await using var server  = AnSshServer();
            var             url     = await AnEndpoint((connection, ct) => {
                                                           lock (logins)
                                                               logins.Add(connection.Login);
                                                           return server.ServeAsync(connection.Pipe, CancellationToken: ct);
                                                       },
                                                       RequireAuthentication: true);

            endpoint!.AddOrUpdateHTTPBasicAuth("CP001", "the station's password");

            using var anonymous = new SshWebSocketClient(url);

            var refused = await Assert.ThrowsAsync<SshWebSocketException>(async () => await SSHClient.Connect(anonymous, AnSshClient(), CancellationToken));

            Assert.Multiple(() => {
                Assert.That(refused.HTTPStatusCode,  Is.EqualTo(HTTPStatusCode.Unauthorized));
                Assert.That(refused.FromProxy,       Is.False);
            });

            using var       station  = new SshWebSocketClient(url, HTTPAuthentication: HTTPBasicAuthentication.Create("CP001", "the station's password"));
            await using var client   = await SSHClient.Connect(station, AnSshClient(), CancellationToken);

            var result = await client.Execute("whoami", CancellationToken);

            Assert.Multiple(() => {
                Assert.That(result.StandardOutput,  Is.EqualTo("ran: whoami\n"));
                Assert.That(logins,                 Is.EqualTo(new[] { "CP001" }));
            });

        }

        #endregion

        #region TheEndpointEnds_AndTheClientSeesIt

        /// <summary>
        /// The other way of ending: the endpoint goes away, closes its WebSockets, and the SSH
        /// client over one of them sees its connection end.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task TheEndpointEnds_AndTheClientSeesIt(CancellationToken CancellationToken)
        {

            await using var server     = AnSshServer();
            var             url        = await AnEndpoint((connection, ct) => server.ServeAsync(connection.Pipe, CancellationToken: ct));

            using var       webSocket  = new SshWebSocketClient(url);
            await using var client     = await SSHClient.Connect(webSocket, AnSshClient(), CancellationToken);

            Assert.That(endpoint!.SshConnections, Is.EqualTo(1));

            await endpoint.DisposeAsync();
            endpoint = null;

            await client.Closed.WaitAsync(Patience, CancellationToken);

        }

        #endregion

        #region TheClientEnds_AndTheEndpointForgetsIt

        /// <summary>
        /// The SSH client says goodbye: the server behind the endpoint ends, the WebSocket is
        /// closed, and the endpoint runs no SSH connection any more.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task TheClientEnds_AndTheEndpointForgetsIt(CancellationToken CancellationToken)
        {

            await using var server     = AnSshServer();
            var             url        = await AnEndpoint((connection, ct) => server.ServeAsync(connection.Pipe, CancellationToken: ct));

            using var       webSocket  = new SshWebSocketClient(url);
            var             client     = await SSHClient.Connect(webSocket, AnSshClient(), CancellationToken);

            Assert.That(endpoint!.SshConnections, Is.EqualTo(1));

            await client.DisposeAsync();

            var deadline = DateTimeOffset.UtcNow + Patience;

            while (endpoint.SshConnections > 0 && DateTimeOffset.UtcNow < deadline)
                await Task.Delay(20, CancellationToken);

            Assert.That(endpoint.SshConnections, Is.Zero);

        }

        #endregion

    }

}
