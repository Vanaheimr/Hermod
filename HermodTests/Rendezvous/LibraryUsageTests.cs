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

using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.Rendezvous;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.Rendezvous
{

    /// <summary>
    /// Tests for using the library on its own: no hosting, no dependency
    /// injection and no configuration binder - just "new RendezvousManager(...)"
    /// and a few method calls. Even the control endpoint is an ordinary object
    /// that can be instantiated wherever it is needed.
    /// </summary>
    [TestFixture]
    public class LibraryUsageTests
    {

        #region A manager without anything around it

        [Test]
        public async Task AManagerNeedsNoArgumentsAtAll()
        {

            // No options, no time provider, no logger factory, no host.
            await using var manager = new RendezvousManager();

            Assert.Multiple(() => {
                Assert.That(manager.Count,      Is.Zero);
                Assert.That(manager.Sessions,   Is.Empty);
                Assert.That(manager.Janitor,    Is.Not.Null, "The manager looks after its timeouts by default.");
            });

        }

        [Test]
        public async Task AManagerRelaysDataWithoutAnyFramework()
        {

            await using var manager = new RendezvousManager(
                                          new RendezvousOptions {
                                              DataAddress = "127.0.0.1"
                                          }
                                      );

            if (!manager.TryConnectPorts(
                     new ConnectPortsCommand(
                         [PortSpecification.Random, PortSpecification.Random],
                         TransferProfile.Interactive
                     ),
                     out var session,
                     out var response))
            {
                Assert.Fail(response.ToProtocolLine());
                return;
            }

            using var alice = await TestNet.ConnectAsync(session.Ports[0]);
            using var bob   = await TestNet.ConnectAsync(session.Ports[1]);

            await TestNet.WaitUntilAsync(() => session.State == SessionState.Established,
                                         "The rendezvous was not established!");

            await TestNet.SendAsync(alice, "Hello Bob!");
            Assert.That(await TestNet.ReceiveAsync(bob, 10), Is.EqualTo("Hello Bob!"));

            manager.DisconnectPorts(new DisconnectPortsCommand([session.Ports[0]]));

            await session.Completion.WaitAsync(TestNet.Timeout);

            Assert.That(session.State, Is.EqualTo(SessionState.Closed));

        }

        [Test]
        public void AnInvalidConfigurationIsRejectedRightAway()
        {

            var exception = Assert.Throws<ArgumentException>(
                                () => new RendezvousManager(
                                          new RendezvousOptions { DataAddress = "not an IP address" }
                                      )
                            );

            Assert.That(exception!.Message, Does.Contain("DataAddress"));

        }

        [Test]
        public async Task TheControlEndpointIsAnOrdinaryObjectToo()
        {

            var configuration = new RendezvousOptions {
                                    ControlAddress  = "127.0.0.1",
                                    ControlPort     = IPPort.Parse(0),          // pick a free port
                                    DataAddress     = "127.0.0.1"
                                };

            // The whole service, without a host and without dependency injection.
            await using var manager  = new RendezvousManager(configuration);
            await using var control  = new ControlServer(manager, configuration);

            // The control endpoint is open to the world, therefore it only
            // obeys commands signed by a key it knows.
            using var signer = ControlSigner.GenerateEd25519("test-operator");
            control.Keys.Add(signer.ToControlKey());

            control.Start();

            Assert.Multiple(() => {
                Assert.That(control.IsRunning,            Is.True);
                Assert.That(control.LocalSocket,        Is.Not.Null);
                Assert.That(control.LocalSocket!.Value.Port.ToUInt16(), Is.GreaterThan(0), "The bound port is known right after Start().");
            });

            #region Speak the control protocol

            var serverKeys = new ControlKeyRing();
            serverKeys.Add(control.ResponseKey);

            using var client = new RendezvousControlClient(
                                   control.LocalSocket!.Value,
                                   signer,
                                   serverKeys
                               );

            var response = await client.ConnectPortsAsync(
                                     [PortSpecification.Random, PortSpecification.Random],
                                     TransferProfile.Interactive
                                 );

            Assert.Multiple(() => {
                Assert.That(response.IsSuccess,  Is.True, response.Message);
                Assert.That(response.Ports,      Has.Count.EqualTo(2));
                Assert.That(response.Profile,    Is.EqualTo(TransferProfile.Interactive));
                Assert.That(response.SignedBy,   Has.Count.EqualTo(1), "The response is signed as well.");
                Assert.That(manager.Count,       Is.EqualTo(1));
            });

            #endregion

            await control.StopAsync();

            Assert.That(control.IsRunning, Is.False);

        }

        #endregion

        #region The automatic maintenance

        [Test]
        public async Task AManagerClosesTimedOutRendezvousOnItsOwn()
        {

            // The real clock, with timeouts in milliseconds: this test is about
            // the manager running its maintenance without anybody asking it to.
            await using var manager = new RendezvousManager(
                                          new RendezvousOptions {
                                              DataAddress          = "127.0.0.1",
                                              RendezvousTimeout    = TimeSpan.FromMilliseconds(200),
                                              MaintenanceInterval  = TimeSpan.FromMilliseconds(25)
                                              // AutoMaintenance is enabled by default
                                          }
                                      );

            manager.TryConnectPorts(
                new ConnectPortsCommand([PortSpecification.Random, PortSpecification.Random]),
                out var session,
                out _
            );

            // Nobody started a janitor here - the manager brought its own.
            Assert.That(manager.Janitor,            Is.Not.Null);
            Assert.That(manager.Janitor!.IsRunning, Is.True);

            await session!.Completion.WaitAsync(TestNet.Timeout);

            Assert.Multiple(() => {
                Assert.That(session.CloseReason,                  Is.EqualTo(SessionCloseReason.RendezvousTimeout));
                Assert.That(manager.Count,                        Is.Zero);
                Assert.That(TestNet.IsPortFree(session.Ports[0]), Is.True);
            });

        }

        [Test]
        public async Task TheAutomaticMaintenanceCanBeTurnedOff()
        {

            var time = new FakeTimeProvider();

            await using var manager = new RendezvousManager(
                                          new RendezvousOptions {
                                              DataAddress      = "127.0.0.1",
                                              AutoMaintenance  = false
                                          },
                                          time
                                      );

            manager.TryConnectPorts(
                new ConnectPortsCommand([PortSpecification.Random, PortSpecification.Random]),
                out var session,
                out _
            );

            Assert.That(manager.Janitor, Is.Null, "Nobody asked for a janitor!");

            time.Advance(TimeSpan.FromHours(1));
            await Task.Delay(250);

            Assert.That(session!.State, Is.EqualTo(SessionState.Pending),
                        "Without maintenance nothing closes by itself.");

            // The caller decides when to look for timeouts.
            manager.Sweep();

            await session.Completion.WaitAsync(TestNet.Timeout);

            Assert.That(session.CloseReason, Is.EqualTo(SessionCloseReason.RendezvousTimeout));

        }

        [Test]
        public async Task ADisposedManagerStopsItsJanitor()
        {

            var manager = new RendezvousManager(new RendezvousOptions { DataAddress = "127.0.0.1" });
            var janitor = manager.Janitor;

            Assert.That(janitor,           Is.Not.Null);
            Assert.That(janitor!.IsRunning, Is.True);

            await manager.DisposeAsync();

            Assert.That(janitor.IsRunning, Is.False);

        }

        #endregion

        #region A janitor on its own

        [Test]
        public async Task AJanitorCanBeDrivenByTheCaller()
        {

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

            await using var janitor = new SessionJanitor(manager, TimeSpan.FromMilliseconds(25));

            Assert.That(janitor.IsRunning, Is.False, "A janitor does nothing before it was started.");

            janitor.Start();
            janitor.Start();   // starting twice is harmless

            await session!.Completion.WaitAsync(TestNet.Timeout);

            Assert.That(session.CloseReason, Is.EqualTo(SessionCloseReason.RendezvousTimeout));

            await janitor.StopAsync();

            Assert.That(janitor.IsRunning, Is.False);

        }

        [Test]
        public async Task AJanitorRejectsANonPositiveInterval()
        {

            await using var manager = new RendezvousManager(new RendezvousOptions { AutoMaintenance = false });

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new SessionJanitor(manager, TimeSpan.Zero)
            );

        }

        #endregion

    }

}
