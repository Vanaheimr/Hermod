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

using org.GraphDefined.Vanaheimr.Hermod.Rendezvous;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.Rendezvous
{

    /// <summary>
    /// Tests for the data relay between three or more clients: a chat.
    /// </summary>
    [TestFixture]
    public class ChatRelayTests
    {

        #region (private) ConnectThreeClientsAsync(Host, Command)

        private static async Task<(IPPort[] Ports, TcpClient Alice, TcpClient Bob, TcpClient Carol)>

            ConnectThreeClientsAsync(RendezvousTestHost  Host,
                                     String              Command = "ConnectPorts([?,?,?], chat)")

        {

            var ports  = TestNet.ParsePorts(Host.ExecuteOk(Command));

            var alice  = await TestNet.ConnectAsync(ports[0]);
            var bob    = await TestNet.ConnectAsync(ports[1]);
            var carol  = await TestNet.ConnectAsync(ports[2]);

            await TestNet.WaitUntilAsync(() => Host.Session.State == SessionState.Established,
                                         "The chat was not established!");

            return (ports, alice, bob, carol);

        }

        #endregion


        [Test]
        public async Task Chat_BroadcastsToAllOtherClients()
        {

            await using var host = RendezvousTestHost.Create();

            var (_, alice, bob, carol) = await ConnectThreeClientsAsync(host);

            using (alice)
            using (bob)
            using (carol)
            {

                await TestNet.SendAsync(alice, "Hello!");

                Assert.That(await TestNet.ReceiveAsync(bob,   6), Is.EqualTo("Hello!"));
                Assert.That(await TestNet.ReceiveAsync(carol, 6), Is.EqualTo("Hello!"));

                await Task.Delay(250);

                Assert.That(alice.Available, Is.Zero, "A chat message must never be echoed back to its sender!");

                // ...and the other way around.
                await TestNet.SendAsync(carol, "Hi!");

                Assert.That(await TestNet.ReceiveAsync(alice, 3), Is.EqualTo("Hi!"));
                Assert.That(await TestNet.ReceiveAsync(bob,   3), Is.EqualTo("Hi!"));

            }

        }

        [Test]
        public async Task Chat_UsesTheInteractiveProfile()
        {

            await using var host = RendezvousTestHost.Create();

            var (_, alice, bob, carol) = await ConnectThreeClientsAsync(host);

            using (alice)
            using (bob)
            using (carol)
            {

                Assert.Multiple(() => {
                    Assert.That(host.Session.Profile,                              Is.EqualTo(TransferProfile.Interactive));
                    Assert.That(host.Session.ProfileSettings.BroadcastQueueLength, Is.EqualTo(512));
                    Assert.That(host.Session.Endpoints,                            Has.Count.EqualTo(3));
                });

            }

        }

        [Test]
        public async Task Chat_ContinuesWhenOneClientLeaves()
        {

            await using var host = RendezvousTestHost.Create();

            var (_, alice, bob, carol) = await ConnectThreeClientsAsync(host);

            using (bob)
            using (carol)
            {

                alice.Close();

                await Task.Delay(250);

                // Bob and Carol can still talk to each other.
                await TestNet.SendAsync(bob, "Alice left.");

                Assert.That(await TestNet.ReceiveAsync(carol, 11), Is.EqualTo("Alice left."));
                Assert.That(host.Session.State, Is.EqualTo(SessionState.Established));

            }

        }

        [Test]
        public async Task Chat_ClosesWhenOnlyOneClientIsLeft()
        {

            await using var host = RendezvousTestHost.Create();

            var (ports, alice, bob, carol) = await ConnectThreeClientsAsync(host);

            using (carol)
            {

                var session = host.Session;

                alice.Close();
                bob.  Close();

                await session.Completion.WaitAsync(TestNet.Timeout);

                await TestNet.ExpectEndOfStreamAsync(carol);

                Assert.Multiple(() => {
                    Assert.That(session.State,                Is.EqualTo(SessionState.Closed));
                    Assert.That(host.Manager.Count,           Is.Zero);
                    Assert.That(TestNet.IsPortFree(ports[0]), Is.True, "All listeners must be removed!");
                    Assert.That(TestNet.IsPortFree(ports[1]), Is.True, "All listeners must be removed!");
                    Assert.That(TestNet.IsPortFree(ports[2]), Is.True, "All listeners must be removed!");
                });

            }

        }

    }

}
