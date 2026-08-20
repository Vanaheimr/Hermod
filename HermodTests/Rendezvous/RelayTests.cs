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
    /// Tests for the data relay between two clients.
    /// </summary>
    [TestFixture]
    public class RelayTests
    {

        #region The echo to the sender

        [Test]
        public async Task TwoClients_WithEcho_BothSeeTheVerySameStream()
        {

            await using var host = RendezvousTestHost.Create();

            // Two clients asking for an echo are relayed by the broadcast as
            // well, so that both of them see the same conversation.
            var (_, alice, bob) = await ConnectTwoClientsAsync(host, "ConnectPorts([?,?], \"A conversation\", Echo)");

            using (alice)
            using (bob)
            {

                Assert.That(host.Session.EchoToSender, Is.True);

                await TestNet.SendAsync(alice, "Hello!");

                Assert.That(await TestNet.ReceiveAsync(alice, 6), Is.EqualTo("Hello!"), "The sender must be echoed!");
                Assert.That(await TestNet.ReceiveAsync(bob,   6), Is.EqualTo("Hello!"));

                await TestNet.SendAsync(bob, "Hi!");

                Assert.That(await TestNet.ReceiveAsync(alice, 3), Is.EqualTo("Hi!"));
                Assert.That(await TestNet.ReceiveAsync(bob,   3), Is.EqualTo("Hi!"));

            }

        }

        [Test]
        public async Task TwoClients_WithoutEcho_KeepTheirOwnBytes()
        {

            await using var host = RendezvousTestHost.Create();

            var (_, alice, bob) = await ConnectTwoClientsAsync(host);

            using (alice)
            using (bob)
            {

                Assert.That(host.Session.EchoToSender, Is.False, "The echo must be off unless it was asked for!");

                await TestNet.SendAsync(alice, "Hello!");

                Assert.That(await TestNet.ReceiveAsync(bob, 6), Is.EqualTo("Hello!"));

                await Task.Delay(250);

                Assert.That(alice.Available, Is.Zero, "Without an echo a client must never receive its own bytes!");

            }

        }

        #endregion

        #region (private) ConnectTwoClientsAsync(Host, Command)

        private static async Task<(IPPort[] Ports, TcpClient Alice, TcpClient Bob)>

            ConnectTwoClientsAsync(RendezvousTestHost  Host,
                                   String              Command = "ConnectPorts([?,?])")

        {

            var ports  = TestNet.ParsePorts(Host.ExecuteOk(Command));

            var alice  = await TestNet.ConnectAsync(ports[0]);
            var bob    = await TestNet.ConnectAsync(ports[1]);

            await TestNet.WaitUntilAsync(() => Host.Session.State == SessionState.Established,
                                         "The rendezvous was not established!");

            return (ports, alice, bob);

        }

        #endregion


        #region Relaying

        [Test]
        public async Task Relay_ForwardsDataInBothDirections()
        {

            await using var host = RendezvousTestHost.Create();

            var (_, alice, bob) = await ConnectTwoClientsAsync(host);

            using (alice)
            using (bob)
            {

                await TestNet.SendAsync(alice, "Hello Bob!");
                Assert.That(await TestNet.ReceiveAsync(bob,   10), Is.EqualTo("Hello Bob!"));

                await TestNet.SendAsync(bob,   "Hi Alice!");
                Assert.That(await TestNet.ReceiveAsync(alice,  9), Is.EqualTo("Hi Alice!"));

                Assert.That(host.Session.BytesRelayed, Is.EqualTo(19));

            }

        }

        [Test]
        public async Task Relay_ForwardsLargeAmountsOfData()
        {

            await using var host = RendezvousTestHost.Create();

            var (_, alice, bob) = await ConnectTwoClientsAsync(host, "ConnectPorts([?,?], Bulk)");

            using (alice)
            using (bob)
            {

                var payload = new Byte[4 * 1024 * 1024];
                Random.Shared.NextBytes(payload);

                // Send and receive at the same time, as neither the operating
                // system nor the service will buffer 4 MByte for us.
                var receiving  = TestNet.ReceiveExactAsync(bob, payload.Length);
                var sending    = alice.GetStream().WriteAsync(payload).AsTask();

                await sending.WaitAsync(TestNet.Timeout);

                var (received, receivedBytes) = await receiving;

                Assert.Multiple(() => {
                    Assert.That(receivedBytes,             Is.EqualTo(payload.Length));
                    Assert.That(received,                  Is.EqualTo(payload).AsCollection);
                    Assert.That(host.Session.BytesRelayed, Is.EqualTo(payload.Length));
                });

            }

        }

        [Test]
        public async Task Relay_DoesNotStartBeforeAllClientsArrived()
        {

            await using var host = RendezvousTestHost.Create();

            var ports = TestNet.ParsePorts(host.ExecuteOk("ConnectPorts([?,?])"));

            using var alice = await TestNet.ConnectAsync(ports[0]);

            // Alice is early and starts talking - nothing may get lost.
            await TestNet.SendAsync(alice, "Are you there?");
            await Task.Delay(250);

            Assert.Multiple(() => {
                Assert.That(host.Session.State,        Is.EqualTo(SessionState.Pending));
                Assert.That(host.Session.BytesRelayed, Is.Zero);
            });

            using var bob = await TestNet.ConnectAsync(ports[1]);

            Assert.That(await TestNet.ReceiveAsync(bob, 14), Is.EqualTo("Are you there?"));

        }

        #endregion

        #region Closing

        [Test]
        public async Task Relay_ForwardsAHalfClose()
        {

            await using var host = RendezvousTestHost.Create();

            var (_, alice, bob) = await ConnectTwoClientsAsync(host);

            using (alice)
            using (bob)
            {

                await TestNet.SendAsync(alice, "Bye!");

                // Alice closes her sending side only.
                alice.Client.Shutdown(SocketShutdown.Send);

                Assert.That(await TestNet.ReceiveAsync(bob, 4), Is.EqualTo("Bye!"));

                // Bob sees the end of the stream...
                await TestNet.ExpectEndOfStreamAsync(bob);

                // ...but may still answer.
                await TestNet.SendAsync(bob, "Bye Alice!");
                Assert.That(await TestNet.ReceiveAsync(alice, 10), Is.EqualTo("Bye Alice!"));

                Assert.That(host.Session.State, Is.EqualTo(SessionState.Established),
                            "A half-close must not close the whole rendezvous!");

            }

        }

        [Test]
        public async Task Relay_ClosesTheRendezvousWhenBothClientsDisconnected()
        {

            await using var host = RendezvousTestHost.Create();

            var (ports, alice, bob) = await ConnectTwoClientsAsync(host);

            var session = host.Session;

            alice.Close();
            bob.  Close();

            await session.Completion.WaitAsync(TestNet.Timeout);

            Assert.Multiple(() => {
                Assert.That(session.State,                Is.EqualTo(SessionState.Closed));
                Assert.That(session.CloseReason,          Is.EqualTo(SessionCloseReason.ClientDisconnected));
                Assert.That(host.Manager.Count,           Is.Zero);
                Assert.That(TestNet.IsPortFree(ports[0]), Is.True,  "The listeners must be removed!");
                Assert.That(TestNet.IsPortFree(ports[1]), Is.True,  "The listeners must be removed!");
            });

        }

        [Test]
        public async Task Relay_ReleasesThePortsForANewRendezvous()
        {

            await using var host = RendezvousTestHost.Create();

            var freePorts = TestNet.GetFreePorts(2);

            host.ExecuteOk($"ConnectPorts([{freePorts[0]}, {freePorts[1]}])");

            var session = host.Session;

            host.ExecuteOk($"DisconnectPorts({freePorts[0]}, {freePorts[1]})");
            await session.Completion.WaitAsync(TestNet.Timeout);

            // The very same ports can be used again.
            var response = host.Execute($"ConnectPorts([{freePorts[0]}, {freePorts[1]}])");

            Assert.That(response.IsSuccess, Is.True, response.ToProtocolLine());

        }

        #endregion

        #region Additional clients

        [Test]
        public async Task Relay_RejectsAdditionalClientsOnTheSamePort()
        {

            await using var host = RendezvousTestHost.Create();

            var (ports, alice, bob) = await ConnectTwoClientsAsync(host);

            using (alice)
            using (bob)
            {

                // The listener stays open, so that nobody else can take over
                // the port, but a second client on the same port is rejected.
                using var eve = await TestNet.ConnectAsync(ports[0]);

                await TestNet.ExpectEndOfStreamAsync(eve);

                // Alice and Bob are not affected at all.
                await TestNet.SendAsync(alice, "Still there?");
                Assert.That(await TestNet.ReceiveAsync(bob, 12), Is.EqualTo("Still there?"));

            }

        }

        #endregion

        #region Transfer profiles

        [Test]
        public async Task Relay_AppliesTheInteractiveProfile()
        {

            await using var host = RendezvousTestHost.Create();

            var (_, alice, bob) = await ConnectTwoClientsAsync(host, "ConnectPorts([?,?], SSH)");

            using (alice)
            using (bob)
            {

                var session = host.Session;

                Assert.Multiple(() => {

                    Assert.That(session.Profile,                          Is.EqualTo(TransferProfile.Interactive));
                    Assert.That(session.ProfileSettings.RelayBufferSize,  Is.EqualTo(8 * 1024));

                    // The Nagle algorithm is disabled for low latency.
                    Assert.That(session.Endpoints[0].Client!.NoDelay,     Is.True);
                    Assert.That(session.Endpoints[1].Client!.NoDelay,     Is.True);

                    // Some operating systems double the requested buffer sizes.
                    Assert.That(session.Endpoints[0].Client!.ReceiveBufferSize, Is.GreaterThanOrEqualTo(32 * 1024));

                });

            }

        }

        [Test]
        public async Task Relay_AppliesTheBulkProfile()
        {

            await using var host = RendezvousTestHost.Create();

            var (_, alice, bob) = await ConnectTwoClientsAsync(host, "ConnectPorts([?,?], bulk)");

            using (alice)
            using (bob)
            {

                var session = host.Session;

                Assert.Multiple(() => {
                    Assert.That(session.Profile,                          Is.EqualTo(TransferProfile.Bulk));
                    Assert.That(session.ProfileSettings.RelayBufferSize,  Is.EqualTo(256 * 1024));

                    // Bulk transfers keep the Nagle algorithm enabled.
                    Assert.That(session.Endpoints[0].Client!.NoDelay,     Is.False);
                });

            }

        }

        [Test]
        public async Task Relay_UsesTheConfiguredDefaultProfile()
        {

            await using var host = RendezvousTestHost.Create(options => options.DefaultProfile = TransferProfile.Interactive);

            var response = host.ExecuteOk("ConnectPorts([?,?])");

            Assert.Multiple(() => {
                Assert.That(response.Text,       Does.EndWith(", Interactive)"));
                Assert.That(host.Session.Profile, Is.EqualTo(TransferProfile.Interactive));
            });

        }

        #endregion

    }

}
