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

using org.GraphDefined.Vanaheimr.Hermod.Rendezvous;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.Rendezvous
{

    /// <summary>
    /// Tests for the rendezvous and idle timeouts, using a controllable clock.
    /// </summary>
    [TestFixture]
    public class TimeoutTests
    {

        #region The rendezvous timeout

        [Test]
        public async Task RendezvousTimeout_ClosesARendezvousWhereNobodyArrived()
        {

            await using var host = RendezvousTestHost.Create();

            var ports    = TestNet.ParsePorts(host.ExecuteOk("ConnectPorts([?,?])"));
            var session  = host.Session;

            host.Time.Advance(TimeSpan.FromMinutes(4));
            host.Manager.Sweep();

            Assert.That(session.State, Is.EqualTo(SessionState.Pending),
                        "The default rendezvous timeout is 5 minutes!");

            host.Time.Advance(TimeSpan.FromMinutes(1));
            host.Manager.Sweep();

            await session.Completion.WaitAsync(TestNet.Timeout);

            Assert.Multiple(() => {
                Assert.That(session.State,                Is.EqualTo(SessionState.Closed));
                Assert.That(session.CloseReason,          Is.EqualTo(SessionCloseReason.RendezvousTimeout));
                Assert.That(host.Manager.Count,           Is.Zero);
                Assert.That(TestNet.IsPortFree(ports[0]), Is.True, "The listeners must be removed!");
                Assert.That(TestNet.IsPortFree(ports[1]), Is.True, "The listeners must be removed!");
            });

        }

        [Test]
        public async Task RendezvousTimeout_ClosesARendezvousWhereOnlyOneClientArrived()
        {

            await using var host = RendezvousTestHost.Create();

            var ports    = TestNet.ParsePorts(host.ExecuteOk("ConnectPorts([?,?])"));
            var session  = host.Session;

            using var alice = await TestNet.ConnectAsync(ports[0]);

            await TestNet.WaitUntilAsync(() => session.ConnectedClients == 1,
                                         "The first client did not arrive!");

            host.Time.Advance(TimeSpan.FromMinutes(5));
            host.Manager.Sweep();

            await session.Completion.WaitAsync(TestNet.Timeout);

            await TestNet.ExpectEndOfStreamAsync(alice);

            Assert.Multiple(() => {
                Assert.That(session.State,       Is.EqualTo(SessionState.Closed));
                Assert.That(session.CloseReason, Is.EqualTo(SessionCloseReason.RendezvousTimeout));
            });

        }

        [Test]
        public async Task RendezvousTimeout_IsConfigurable()
        {

            await using var host = RendezvousTestHost.Create(options => options.RendezvousTimeout = TimeSpan.FromSeconds(30));

            host.ExecuteOk("ConnectPorts([?,?])");

            var session = host.Session;

            host.Time.Advance(TimeSpan.FromSeconds(29));
            host.Manager.Sweep();

            Assert.That(session.State, Is.EqualTo(SessionState.Pending));

            host.Time.Advance(TimeSpan.FromSeconds(1));
            host.Manager.Sweep();

            await session.Completion.WaitAsync(TestNet.Timeout);

            Assert.That(session.CloseReason, Is.EqualTo(SessionCloseReason.RendezvousTimeout));

        }

        [Test]
        public async Task RendezvousTimeout_DoesNotCloseAnEstablishedRendezvous()
        {

            await using var host = RendezvousTestHost.Create();

            var ports = TestNet.ParsePorts(host.ExecuteOk("ConnectPorts([?,?])"));

            using var alice = await TestNet.ConnectAsync(ports[0]);
            using var bob   = await TestNet.ConnectAsync(ports[1]);

            await TestNet.WaitUntilAsync(() => host.Session.State == SessionState.Established,
                                         "The rendezvous was not established!");

            host.Time.Advance(TimeSpan.FromMinutes(9));
            host.Manager.Sweep();

            Assert.That(host.Session.State, Is.EqualTo(SessionState.Established),
                        "An established rendezvous is only limited by the idle timeout!");

        }

        #endregion

        #region The idle timeout

        [Test]
        public async Task IdleTimeout_ClosesAnEstablishedRendezvousWithoutPayload()
        {

            await using var host = RendezvousTestHost.Create();

            var ports = TestNet.ParsePorts(host.ExecuteOk("ConnectPorts([?,?])"));

            using var alice = await TestNet.ConnectAsync(ports[0]);
            using var bob   = await TestNet.ConnectAsync(ports[1]);

            await TestNet.WaitUntilAsync(() => host.Session.State == SessionState.Established,
                                         "The rendezvous was not established!");

            var session = host.Session;

            host.Time.Advance(TimeSpan.FromMinutes(10));
            host.Manager.Sweep();

            await session.Completion.WaitAsync(TestNet.Timeout);

            await TestNet.ExpectEndOfStreamAsync(alice);
            await TestNet.ExpectEndOfStreamAsync(bob);

            Assert.Multiple(() => {
                Assert.That(session.State,                Is.EqualTo(SessionState.Closed));
                Assert.That(session.CloseReason,          Is.EqualTo(SessionCloseReason.IdleTimeout));
                Assert.That(TestNet.IsPortFree(ports[0]), Is.True, "The listeners must be removed!");
            });

        }

        [Test]
        public async Task IdleTimeout_IsResetByPayload()
        {

            await using var host = RendezvousTestHost.Create();

            var ports = TestNet.ParsePorts(host.ExecuteOk("ConnectPorts([?,?])"));

            using var alice = await TestNet.ConnectAsync(ports[0]);
            using var bob   = await TestNet.ConnectAsync(ports[1]);

            await TestNet.WaitUntilAsync(() => host.Session.State == SessionState.Established,
                                         "The rendezvous was not established!");

            var session = host.Session;

            #region 9 minutes without payload are fine

            host.Time.Advance(TimeSpan.FromMinutes(9));
            host.Manager.Sweep();

            Assert.That(session.State, Is.EqualTo(SessionState.Established));

            #endregion

            #region A single byte resets the idle timeout

            await TestNet.SendAsync(alice, "?");
            Assert.That(await TestNet.ReceiveAsync(bob, 1), Is.EqualTo("?"));

            host.Time.Advance(TimeSpan.FromMinutes(9));
            host.Manager.Sweep();

            Assert.That(session.State, Is.EqualTo(SessionState.Established),
                        "The idle timeout must be measured from the last payload!");

            #endregion

            #region ...and 10 minutes later it is over

            host.Time.Advance(TimeSpan.FromMinutes(1));
            host.Manager.Sweep();

            await session.Completion.WaitAsync(TestNet.Timeout);

            Assert.That(session.CloseReason, Is.EqualTo(SessionCloseReason.IdleTimeout));

            #endregion

        }

        [Test]
        public async Task IdleTimeout_IsConfigurable()
        {

            await using var host = RendezvousTestHost.Create(options => options.IdleTimeout = TimeSpan.FromMinutes(1));

            var ports = TestNet.ParsePorts(host.ExecuteOk("ConnectPorts([?,?])"));

            using var alice = await TestNet.ConnectAsync(ports[0]);
            using var bob   = await TestNet.ConnectAsync(ports[1]);

            await TestNet.WaitUntilAsync(() => host.Session.State == SessionState.Established,
                                         "The rendezvous was not established!");

            var session = host.Session;

            host.Time.Advance(TimeSpan.FromMinutes(1));
            host.Manager.Sweep();

            await session.Completion.WaitAsync(TestNet.Timeout);

            Assert.That(session.CloseReason, Is.EqualTo(SessionCloseReason.IdleTimeout));

        }

        #endregion

        #region The session janitor

        [Test]
        public async Task SessionJanitor_SweepsPeriodically()
        {

            // The fake clock of these tests does not fake timers, and faking them
            // would say nothing about whether the loop really runs. Therefore this
            // one uses the real clock with timeouts in milliseconds.
            await using var manager = new RendezvousManager(
                                          new RendezvousOptions {
                                              DataAddress        = "127.0.0.1",
                                              RendezvousTimeout  = TimeSpan.FromMilliseconds(200),
                                              AutoMaintenance    = false
                                          }
                                      );

            manager.TryConnectPorts(
                new ConnectPortsCommand([PortSpecification.Random, PortSpecification.Random]),
                out var session,
                out _
            );

            // A plain object with a timer - no hosting framework involved.
            await using var janitor = new SessionJanitor(manager, TimeSpan.FromMilliseconds(25));

            Assert.That(janitor.IsRunning, Is.False, "A janitor does nothing before it was started.");

            janitor.Start();

            await session!.Completion.WaitAsync(TestNet.Timeout);

            Assert.Multiple(() => {
                Assert.That(session.CloseReason, Is.EqualTo(SessionCloseReason.RendezvousTimeout));
                Assert.That(janitor.IsRunning,   Is.True);
            });

        }

        #endregion

    }

}
