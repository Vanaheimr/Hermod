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
    /// Tests for the typed API: a caller within the same process builds the
    /// commands itself and gets the rendezvous object back, without ever
    /// touching the text protocol.
    /// </summary>
    [TestFixture]
    public class ProgrammaticApiTests
    {

        #region TryConnectPorts

        [Test]
        public async Task TryConnectPorts_WithFixedPorts_ReturnsTheRendezvous()
        {

            await using var host = RendezvousTestHost.Create();

            var freePorts  = TestNet.GetFreePorts(2);

            var success    = host.Manager.TryConnectPorts(
                                 new ConnectPortsCommand(
                                     [
                                         PortSpecification.Fixed(freePorts[0]),
                                         PortSpecification.Fixed(freePorts[1])
                                     ],
                                     TransferProfile.Interactive
                                 ),
                                 out var session,
                                 out var response
                             );

            Assert.That(success, Is.True, response.ToProtocolLine());

            Assert.Multiple(() => {
                Assert.That(session!.Ports,     Is.EqualTo(freePorts));
                Assert.That(session.Profile,   Is.EqualTo(TransferProfile.Interactive));
                Assert.That(session.State,     Is.EqualTo(SessionState.Pending));
                Assert.That(session.Id,        Is.Not.EqualTo(Guid.Empty));
                Assert.That(response.Text,     Is.EqualTo($"ConnectPorts([{freePorts[0]}, {freePorts[1]}], Interactive)"));
                Assert.That(host.Session,      Is.SameAs(session), "The manager must return the very same rendezvous!");
            });

        }

        [Test]
        public async Task TryConnectPorts_WithRandomPorts_TellsTheChosenPorts()
        {

            await using var host = RendezvousTestHost.Create();

            var success = host.Manager.TryConnectPorts(
                              new ConnectPortsCommand(
                                  [PortSpecification.Random, PortSpecification.Random]
                              ),
                              out var session,
                              out var response
                          );

            Assert.That(success, Is.True, response.ToProtocolLine());

            // This is the whole point: no parsing of the response text.
            Assert.Multiple(() => {
                Assert.That(session!.Ports,        Has.Count.EqualTo(2));
                Assert.That(session.Ports[0].ToUInt16(), Is.GreaterThan(0));
                Assert.That(session.Ports[1],     Is.Not.EqualTo(session.Ports[0]));
                Assert.That(session.Profile,      Is.EqualTo(TransferProfile.Balanced), "An unspecified profile uses the configured default.");
            });

            // ...and the ports are really listening.
            using var alice = await TestNet.ConnectAsync(session!.Ports[0]);
            using var bob   = await TestNet.ConnectAsync(session.Ports[1]);

            await TestNet.WaitUntilAsync(() => session.State == SessionState.Established,
                                         "The rendezvous was not established!");

        }

        [Test]
        public async Task TryConnectPorts_UsesTheConfiguredDefaultProfile()
        {

            await using var host = RendezvousTestHost.Create(options => options.DefaultProfile = TransferProfile.Bulk);

            host.Manager.TryConnectPorts(
                new ConnectPortsCommand([PortSpecification.Random, PortSpecification.Random]),
                out var session,
                out _
            );

            Assert.Multiple(() => {
                Assert.That(session!.Profile,                          Is.EqualTo(TransferProfile.Bulk));
                Assert.That(session!.ProfileSettings.RelayBufferSize,  Is.EqualTo(256 * 1024));
            });

        }

        [Test]
        public async Task TryConnectPorts_RelaysDataWithoutAnyTextProtocol()
        {

            await using var host = RendezvousTestHost.Create();

            // Written the way a real caller would write it: within the 'true'
            // branch the compiler knows that the rendezvous is not null, so this
            // test also proves that the [NotNullWhen(true)] annotations are right.
            if (!host.Manager.TryConnectPorts(
                     new ConnectPortsCommand(
                         [PortSpecification.Random, PortSpecification.Random],
                         TransferProfile.Bulk
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

            // One port is enough to close the whole rendezvous.
            if (!host.Manager.TryDisconnectPorts(
                     new DisconnectPortsCommand([session.Ports[0]]),
                     out var closedSession,
                     out var closeResponse))
            {
                Assert.Fail(closeResponse.ToProtocolLine());
                return;
            }

            Assert.That(closedSession, Is.SameAs(session));

            await closedSession.Completion.WaitAsync(TestNet.Timeout);

            Assert.Multiple(() => {
                Assert.That(session.State,                        Is.EqualTo(SessionState.Closed));
                Assert.That(session.CloseReason,                  Is.EqualTo(SessionCloseReason.DisconnectRequested));
                Assert.That(TestNet.IsPortFree(session.Ports[0]), Is.True);
                Assert.That(TestNet.IsPortFree(session.Ports[1]), Is.True);
            });

        }

        #endregion

        #region TryConnectPorts errors

        [Test]
        public async Task TryConnectPorts_WithAnUnusablePort_ReturnsFalseAndNoRendezvous()
        {

            await using var host = RendezvousTestHost.Create(options => {
                                       options.MinDataPort  = IPPort.Parse(40000);
                                       options.MaxDataPort  = IPPort.Parse(41000);
                                   });

            var success = host.Manager.TryConnectPorts(
                              new ConnectPortsCommand(
                                  [PortSpecification.Fixed(IPPort.Parse(20000)), PortSpecification.Random]
                              ),
                              out var session,
                              out var response
                          );

            Assert.Multiple(() => {
                Assert.That(success,            Is.False);
                Assert.That(session,            Is.Null);
                Assert.That(response.Code,      Is.EqualTo(ResponseCode.PortNotAllowed));
                Assert.That(host.Manager.Count, Is.Zero);
            });

        }

        [Test]
        public async Task TryConnectPorts_WithASinglePort_IsRejectedWithoutTheParser()
        {

            await using var host = RendezvousTestHost.Create();

            // The parser would have caught this, but the typed API bypasses it.
            var success = host.Manager.TryConnectPorts(
                              new ConnectPortsCommand([PortSpecification.Random]),
                              out var session,
                              out var response
                          );

            Assert.Multiple(() => {
                Assert.That(success,            Is.False);
                Assert.That(session,            Is.Null);
                Assert.That(response.Code,      Is.EqualTo(ResponseCode.InvalidSyntax));
                Assert.That(host.Manager.Count, Is.Zero);
            });

        }

        [Test]
        public async Task TryConnectPorts_WithDuplicatePorts_IsRejectedWithoutTheParser()
        {

            await using var host = RendezvousTestHost.Create();

            var freePort  = TestNet.GetFreePorts(1)[0];

            var success   = host.Manager.TryConnectPorts(
                                new ConnectPortsCommand(
                                    [
                                        PortSpecification.Fixed(freePort),
                                        PortSpecification.Fixed(freePort)
                                    ]
                                ),
                                out var session,
                                out var response
                            );

            Assert.Multiple(() => {
                Assert.That(success,                      Is.False);
                Assert.That(session,                      Is.Null);
                Assert.That(response.Code,                Is.EqualTo(ResponseCode.InvalidSyntax));
                Assert.That(response.Text,                Does.Contain("Duplicate"));
                Assert.That(TestNet.IsPortFree(freePort), Is.True, "A rejected request must not leave a listener behind!");
            });

        }

        [Test]
        public async Task TryConnectPorts_WithATooLongDescription_IsRejectedWithoutTheParser()
        {

            await using var host = RendezvousTestHost.Create();

            var success = host.Manager.TryConnectPorts(
                              new ConnectPortsCommand(
                                  [PortSpecification.Random, PortSpecification.Random],
                                  Description: new String('x', RendezvousCommand.MaxDescriptionLength + 1)
                              ),
                              out var session,
                              out var response
                          );

            Assert.Multiple(() => {
                Assert.That(success,            Is.False);
                Assert.That(session,            Is.Null);
                Assert.That(response.Code,      Is.EqualTo(ResponseCode.InvalidSyntax));
                Assert.That(host.Manager.Count, Is.Zero);
            });

        }

        [Test]
        public void PortSpecification_RejectsTcpPortZero()
        {

            Assert.Throws<ArgumentOutOfRangeException>(() => PortSpecification.Fixed(IPPort.Parse(0)));

        }

        #endregion

        #region TryDisconnectPorts

        [Test]
        public async Task TryDisconnectPorts_ByAnotherKey_ReturnsNeitherRendezvousNorSuccess()
        {

            await using var host = RendezvousTestHost.Create();

            using var alice   = ControlSigner.GenerateEd25519("alice");
            using var mallory = ControlSigner.GenerateEd25519("mallory");

            host.Manager.TryConnectPorts(
                new ConnectPortsCommand([PortSpecification.Random, PortSpecification.Random]),
                new ControlAuthorization(alice.ToControlKey()),
                out var session,
                out _
            );

            var success = host.Manager.TryDisconnectPorts(
                              new DisconnectPortsCommand([session!.Ports[0]]),
                              new ControlAuthorization(mallory.ToControlKey()),
                              out var closedSession,
                              out var response
                          );

            Assert.Multiple(() => {
                Assert.That(success,            Is.False);
                Assert.That(closedSession,      Is.Null, "An unauthorized caller must not get hold of the rendezvous!");
                Assert.That(response.Code,      Is.EqualTo(ResponseCode.Unauthorized));
                Assert.That(session.State,      Is.EqualTo(SessionState.Pending));
                Assert.That(host.Manager.Count, Is.EqualTo(1));
            });

        }

        [Test]
        public async Task TryDisconnectPorts_ByANamedTrustedCaller_Works()
        {

            await using var host = RendezvousTestHost.Create();

            host.Manager.TryConnectPorts(
                new ConnectPortsCommand([PortSpecification.Random, PortSpecification.Random],
                                        Description: "The nightly backup"),
                ControlAuthorization.TrustedAs("backup-job"),
                out var session,
                out _
            );

            Assert.Multiple(() => {
                Assert.That(session!.CreatedBy,   Is.EqualTo(new[] { "backup-job" }), "A trusted caller may name itself!");
                Assert.That(session!.Description, Is.EqualTo("The nightly backup"));
            });

            var success = host.Manager.TryDisconnectPorts(
                              new DisconnectPortsCommand([session!.Ports[0]], "Backup is done"),
                              ControlAuthorization.TrustedAs("backup-job"),
                              out var closedSession,
                              out var response
                          );

            await session.Completion.WaitAsync(TestNet.Timeout);

            Assert.Multiple(() => {
                Assert.That(success,        Is.True, response.Text);
                Assert.That(closedSession,  Is.SameAs(session));
                Assert.That(session.State,  Is.EqualTo(SessionState.Closed));
            });

        }

        [Test]
        public async Task TryDisconnectPorts_WithAnUnknownPort_ReturnsFalse()
        {

            await using var host = RendezvousTestHost.Create();

            var success = host.Manager.TryDisconnectPorts(
                              new DisconnectPortsCommand([IPPort.Parse(20000)]),
                              out var session,
                              out var response
                          );

            Assert.Multiple(() => {
                Assert.That(success,       Is.False);
                Assert.That(session,       Is.Null);
                Assert.That(response.Code, Is.EqualTo(ResponseCode.UnknownSession));
            });

        }

        [Test]
        public async Task TryDisconnectPorts_WithoutAnyPort_IsRejectedWithoutTheParser()
        {

            await using var host = RendezvousTestHost.Create();

            var success = host.Manager.TryDisconnectPorts(
                              new DisconnectPortsCommand([]),
                              out var session,
                              out var response
                          );

            Assert.Multiple(() => {
                Assert.That(success,       Is.False);
                Assert.That(session,       Is.Null);
                Assert.That(response.Code, Is.EqualTo(ResponseCode.InvalidSyntax));
            });

        }

        #endregion

        #region Both APIs stay in sync

        [Test]
        public async Task TheTextProtocolAndTheTypedApiAgree()
        {

            await using var host = RendezvousTestHost.Create();

            var freePorts = TestNet.GetFreePorts(2);

            // Once through the parser...
            var viaText  = host.Manager.Execute($"ConnectPorts([{freePorts[0]}, {freePorts[1]}], Bulk)");
            var textPorts = TestNet.ParsePorts(viaText);

            host.Manager.Execute($"DisconnectPorts({freePorts[0]}, {freePorts[1]})");

            await TestNet.WaitUntilAsync(() => host.Manager.Count == 0,
                                         "The rendezvous was not closed!");

            // ...and once through the typed API.
            var success = host.Manager.TryConnectPorts(
                              new ConnectPortsCommand(
                                  [
                                      PortSpecification.Fixed(freePorts[0]),
                                      PortSpecification.Fixed(freePorts[1])
                                  ],
                                  TransferProfile.Bulk
                              ),
                              out var session,
                              out var viaApi
                          );

            Assert.That(success, Is.True, viaApi.ToProtocolLine());

            Assert.Multiple(() => {
                Assert.That(viaApi.ToProtocolLine(), Is.EqualTo(viaText.ToProtocolLine()));
                Assert.That(session!.Ports,           Is.EqualTo(textPorts));
            });

        }

        #endregion

    }

}
