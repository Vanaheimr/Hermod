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

using System.Collections.Concurrent;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Client;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// Destinations of the application's: <c>ssh -J operator@csms operator@CP001</c>, where the jump host
    /// takes "CP001" to a pipe of its own - a tunnel to a station - rather than to a TCP connection, and
    /// SSH runs end to end through it. What the factory does not answer for is a port forwarding as before.
    /// </summary>
    [TestFixture]
    public class SshDestinationTests
    {

        #region Data

        private readonly ISshHostKey  jumpHostKey     = SshHostKey.GenerateEd25519();
        private readonly ISshHostKey  stationHostKey  = SshHostKey.GenerateEd25519();
        private readonly ISshHostKey  operatorKey     = SshHostKey.GenerateEd25519();

        #endregion

        #region (private) Helpers

        // The station: an SSH server that is reached through a pipe alone.
        private SshServer AStation()

            => new (new SshServerOptions {
                   HostKeys       = [ stationHostKey ],
                   Authenticator  = SshUserAuthenticator.ForAuthorizedKeys(operatorKey.PublicKeyBlob),
                   ExecHandler    = async (ctx, ct) => {
                                        await ctx.WriteAsync($"station ran: {ctx.Command}\n", ct);
                                        return 0;
                                    }
               });

        // The jump host: "CP001" is the station, for the operator alone; nothing is forwarded otherwise.
        private async Task<SshServer> AJumpHost(SshServer                     Station,
                                                ConcurrentQueue<String>       Asked,
                                                CancellationToken             CancellationToken,
                                                ForwardingPolicy?             ForwardingPolicy  = null)
        {

            var jumpHost = new SshServer(new SshServerOptions {
                               HostKeys          = [ jumpHostKey ],
                               Authenticator     = SshUserAuthenticator.ForAuthorizedKeys(operatorKey.PublicKeyBlob),
                               ForwardingPolicy  = ForwardingPolicy ?? ForwardingPolicy.None,
                               Destinations      = (session, host, port, ct) => {

                                                       Asked.Enqueue($"{session.Username}@{host}:{port}");

                                                       return ValueTask.FromResult<SshDestinationHandler?>(
                                                                  host == "CP001"
                                                                      ? async (context, token) => {

                                                                            var (here, there) = DuplexPipe.CreateConnectedPair();
                                                                            var serving       = Station.ServeAsync(there, CancellationToken: token);

                                                                            await context.RelayAsync(here, token);

                                                                            await serving.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None).ContinueWith(_ => { });

                                                                        }
                                                                      : null
                                                              );

                                                   }
                           });

            await jumpHost.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);

            return jumpHost;

        }

        private SSHClientOptions TheOperator(SshServer JumpHost)

            => new () {
                   Username         = User_Id.Parse("operator"),
                   VerifyHostKey    = blob => blob.AsSpan().SequenceEqual(stationHostKey.PublicKeyBlob),
                   Credentials      = [ operatorKey ],
                   ProxyJump        = [ new SSHJumpHost("127.0.0.1", JumpHost.LocalEndPoint.Port, User_Id.Parse("operator")) ],
                   JumpHostOptions  = _ => new SSHClientOptions {
                                               Username       = User_Id.Parse("operator"),
                                               VerifyHostKey  = blob => blob.AsSpan().SequenceEqual(jumpHostKey.PublicKeyBlob),
                                               Credentials    = [ operatorKey ]
                                           }
               };

        #endregion


        #region ProxyJump_ToADestinationOfTheApplication

        /// <summary>
        /// <c>ssh -J operator@jumphost operator@CP001</c>: the jump host is asked for "CP001", hands the
        /// channel to the station's pipe, and the command runs on the station - its host key checked end to
        /// end, with no port forwarding allowed on the jump host at all.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task ProxyJump_ToADestinationOfTheApplication(CancellationToken CancellationToken)
        {

            var asked = new ConcurrentQueue<String>();

            await using var station   = AStation();
            await using var jumpHost  = await AJumpHost(station, asked, CancellationToken);

            await using var client    = await SSHClient.Connect("CP001", IPPort.SSH, TheOperator(jumpHost), CancellationToken);

            var result = await client.Execute("hostname", CancellationToken);

            Assert.Multiple(() => {
                Assert.That(result.StandardOutput,  Is.EqualTo("station ran: hostname\n"));
                Assert.That(client.ServerHostKey,   Is.EqualTo(stationHostKey.PublicKeyBlob));
                Assert.That(asked.ToArray(),        Is.EqualTo(new[] { "operator@CP001:22" }));
            });

        }

        #endregion

        #region ANameTheFactoryDoesNotKnow_IsAPortForwarding_AndRefused

        /// <summary>
        /// A destination the factory answers null for is a port forwarding, which this jump host does not
        /// allow: the hop fails, and nothing was connected to.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task ANameTheFactoryDoesNotKnow_IsAPortForwarding_AndRefused(CancellationToken CancellationToken)
        {

            var asked = new ConcurrentQueue<String>();

            await using var station   = AStation();
            await using var jumpHost  = await AJumpHost(station, asked, CancellationToken);

            await Assert.CatchAsync(async () => await SSHClient.Connect("CP002", IPPort.SSH, TheOperator(jumpHost), CancellationToken));

            Assert.That(asked.ToArray(), Is.EqualTo(new[] { "operator@CP002:22" }));

        }

        #endregion

        #region ANameTheFactoryDoesNotKnow_IsForwardedWhereThePolicyAllows

        /// <summary>
        /// Where the forwarding policy allows it, a destination the factory answers null for is forwarded
        /// as before: to an SSH server on a loopback port.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task ANameTheFactoryDoesNotKnow_IsForwardedWhereThePolicyAllows(CancellationToken CancellationToken)
        {

            var asked = new ConcurrentQueue<String>();

            await using var station   = AStation();
            await station.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);

            await using var jumpHost  = await AJumpHost(station, asked, CancellationToken, ForwardingPolicy.LoopbackOnly);

            await using var client    = await SSHClient.Connect("127.0.0.1", station.LocalEndPoint.Port, TheOperator(jumpHost), CancellationToken);

            var result = await client.Execute("whoami", CancellationToken);

            Assert.Multiple(() => {
                Assert.That(result.StandardOutput,  Is.EqualTo("station ran: whoami\n"));
                Assert.That(asked.ToArray(),        Is.EqualTo(new[] { $"operator@127.0.0.1:{station.LocalEndPoint.Port}" }));
            });

        }

        #endregion

    }

}
