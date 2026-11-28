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
using org.GraphDefined.Vanaheimr.Hermod.SSH.Client;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Server;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// SSH over WebSocket on a WebSocket server: on a port of its own, and on the port
    /// of another server that hands it the WebSockets that ask for SSH - a charging
    /// station reaches SSH on the port it reaches OCPP on.
    /// </summary>
    [TestFixture]
    public class SshWebSocketServerTests
    {

        #region Data

        private static readonly TimeSpan  Patience  = TimeSpan.FromSeconds(10);

        private readonly ISshHostKey      hostKey   = SshHostKey.GenerateEd25519();
        private readonly ISshHostKey      userKey   = SshHostKey.GenerateEd25519();

        #endregion

        #region (private) Helpers

        private SshServer AnSshServer()

            => new (new SshServerOptions {
                   HostKeys       = [ hostKey ],
                   Authenticator  = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob),
                   ExecHandler    = async (ctx, ct) => {
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

        /// <summary>
        /// A server that knows one station, as one routed from an OCPP server would
        /// know the stations of that server.
        /// </summary>
        private sealed class KnowsOneStation(SshWebSocketConnectionHandler OnConnection) : SshWebSocketServer(OnConnection, RequireAuthentication: true)
        {

            protected override Task<String?> AuthenticateAsync(WebSocketServerConnection  Connection,
                                                               HTTPRequest                Request,
                                                               CancellationToken          CancellationToken)

                => Task.FromResult(
                       Request.Authorization is HTTPBasicAuthentication { Username: "CP001", Password: "station-password" }
                           ? "CP001"
                           : null
                   );

        }

        #endregion


        #region OnTheOCPPPort_RoutedByTheSubprotocol

        /// <summary>
        /// One port: a client that asks for OCPP is the OCPP server's, a client that asks
        /// for SSH over WebSocket is the SSH server's, and a command runs over it.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task OnTheOCPPPort_RoutedByTheSubprotocol(CancellationToken CancellationToken)
        {

            await using var sshServer  = AnSshServer();

            await using var ocpp       = new WebSocketMirrorServer(HTTPPort:               IPPort.Zero,
                                                                   RequireAuthentication:  false,
                                                                   SecWebSocketProtocols:  [ "ocpp2.1" ],
                                                                   AutoStart:              true);

            await using var ssh        = new SshWebSocketServer((connection, ct) => sshServer.ServeAsync(connection.Pipe, CancellationToken: ct),
                                                                RequireAuthentication: false);

            ocpp.RouteBySubprotocol(ssh);

            var             url        = URL.Parse($"ws://127.0.0.1:{ocpp.IPPort}");

            await using var station    = new WebSocketClient(url, SecWebSocketProtocols: [ "ocpp2.1" ]);
            var (_, ocppAnswer)        = await station.Connect(CancellationToken: CancellationToken);

            using var       webSocket  = new SshWebSocketClient(url);
            await using var client     = await SSHClient.Connect(webSocket, AnSshClient(), CancellationToken);

            var result                 = await client.Execute("whoami", CancellationToken);

            Assert.Multiple(() => {
                Assert.That(ocppAnswer.SecWebSocketProtocol,  Is.EqualTo("ocpp2.1"));
                Assert.That(result.StandardOutput,            Is.EqualTo("ran: whoami\n"));
                Assert.That(ssh.SshConnections,               Is.EqualTo(1));
                Assert.That(ocpp.WebSocketConnections.Select(connection => connection.Subprotocol), Is.EqualTo(new[] { "ocpp2.1" }));
            });

        }

        #endregion

        #region OnAPortOfItsOwn_TheStationConnectsOut

        /// <summary>
        /// On a port of its own, and the station's way round: the SSH server connects
        /// out, and the server runs the SSH client on what it accepted.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task OnAPortOfItsOwn_TheStationConnectsOut(CancellationToken CancellationToken)
        {

            var ran = new TaskCompletionSource<SshCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            await using var ssh = new SshWebSocketServer(async (connection, ct) => {

                                                              try
                                                              {
                                                                  await using var client = await SSHClient.ConnectOver(connection.Pipe, AnSshClient(), ct);
                                                                  ran.TrySetResult(await client.Execute("hostname", ct));
                                                              }
                                                              catch (Exception exception)
                                                              {
                                                                  ran.TrySetException(exception);
                                                              }

                                                          },
                                                          TCPPort:                IPPort.Zero,
                                                          RequireAuthentication:  false,
                                                          AutoStart:              true);

            await using var station    = AnSshServer();
            using var       webSocket  = new SshWebSocketClient(URL.Parse($"ws://127.0.0.1:{ssh.IPPort}"));

            var serving = station.ConnectOut(webSocket, CancellationToken);
            var result  = await ran.Task.WaitAsync(Patience, CancellationToken);

            Assert.That(result.StandardOutput, Is.EqualTo("ran: hostname\n"));

            await serving.WaitAsync(Patience, CancellationToken);

        }

        #endregion

        #region TheLogins_OfAnOverride

        /// <summary>
        /// Who may reach SSH is what an override of AuthenticateAsync knows: without the
        /// station's credentials 401, from the server and not from a proxy, and with
        /// them the connection carries the station's login.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task TheLogins_OfAnOverride(CancellationToken CancellationToken)
        {

            var logins = new TaskCompletionSource<String?>(TaskCreationOptions.RunContinuationsAsynchronously);

            await using var sshServer  = AnSshServer();

            await using var ocpp       = new WebSocketMirrorServer(HTTPPort:               IPPort.Zero,
                                                                   RequireAuthentication:  false,
                                                                   SecWebSocketProtocols:  [ "ocpp2.1" ],
                                                                   AutoStart:              true);

            await using var ssh        = new KnowsOneStation(async (connection, ct) => {
                                             logins.TrySetResult(connection.Login);
                                             await sshServer.ServeAsync(connection.Pipe, CancellationToken: ct);
                                         });

            ocpp.RouteBySubprotocol(ssh);

            var url = URL.Parse($"ws://127.0.0.1:{ocpp.IPPort}");

            using (var stranger = new SshWebSocketClient(url))
            {

                var refused = await Assert.ThrowsAsync<SshWebSocketException>(async () => await stranger.OpenAsync(CancellationToken));

                Assert.Multiple(() => {
                    Assert.That(refused.HTTPStatusCode,  Is.EqualTo(HTTPStatusCode.Unauthorized));
                    Assert.That(refused.FromProxy,       Is.False);
                });

            }

            using var       webSocket  = new SshWebSocketClient(url, HTTPAuthentication: HTTPBasicAuthentication.Create("CP001", "station-password"));
            await using var client     = await SSHClient.Connect(webSocket, AnSshClient(), CancellationToken);

            var result = await client.Execute("whoami", CancellationToken);
            var login  = await logins.Task.WaitAsync(Patience, CancellationToken);

            Assert.Multiple(() => {
                Assert.That(result.StandardOutput,  Is.EqualTo("ran: whoami\n"));
                Assert.That(login,                  Is.EqualTo("CP001"));
            });

        }

        #endregion

    }

}
