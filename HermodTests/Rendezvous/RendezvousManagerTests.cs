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

using System.Net;
using System.Net.Sockets;

using org.GraphDefined.Vanaheimr.Hermod.Rendezvous;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.Rendezvous
{

    /// <summary>
    /// Tests for opening and closing rendezvous.
    /// </summary>
    [TestFixture]
    public class RendezvousManagerTests
    {

        #region ConnectPorts

        [Test]
        public async Task ConnectPorts_WithTwoRandomPorts_OpensTwoListeners()
        {

            await using var host = RendezvousTestHost.Create();

            var response  = host.ExecuteOk("ConnectPorts([?,?])");
            var ports     = TestNet.ParsePorts(response);

            Assert.Multiple(() => {
                Assert.That(ports,          Has.Length.EqualTo(2));
                Assert.That(ports[0],       Is.Not.EqualTo(ports[1]), "Both endpoints need their own TCP port!");
                Assert.That(response.Text,  Does.EndWith(", Balanced)"), "The response echoes the effective transfer profile.");
                Assert.That(host.Manager.Count, Is.EqualTo(1));
            });

            // Both TCP ports are accepting connections.
            using var alice = await TestNet.ConnectAsync(ports[0]);
            using var bob   = await TestNet.ConnectAsync(ports[1]);

            await TestNet.WaitUntilAsync(() => host.Session.State == SessionState.Established,
                                         "The rendezvous was not established!");

        }

        [Test]
        public async Task ConnectPorts_WithTwoFixedPorts_UsesExactlyThosePorts()
        {

            await using var host = RendezvousTestHost.Create();

            var freePorts  = TestNet.GetFreePorts(2);
            var response   = host.ExecuteOk($"ConnectPorts([{freePorts[0]}, {freePorts[1]}])");

            Assert.That(response.Text, Is.EqualTo($"ConnectPorts([{freePorts[0]}, {freePorts[1]}], Balanced)"));

            using var alice = await TestNet.ConnectAsync(freePorts[0]);
            using var bob   = await TestNet.ConnectAsync(freePorts[1]);

        }

        [Test]
        public async Task ConnectPorts_WithOneFixedAndOneRandomPort()
        {

            await using var host = RendezvousTestHost.Create();

            var fixedPort  = TestNet.GetFreePorts(1)[0];
            var response   = host.ExecuteOk($"ConnectPorts([{fixedPort}, ?])");
            var ports      = TestNet.ParsePorts(response);

            Assert.Multiple(() => {
                Assert.That(ports[0], Is.EqualTo(fixedPort), "The fixed port must keep its position within the list!");
                Assert.That(ports[1], Is.Not.EqualTo(fixedPort));
            });

        }

        [Test]
        public async Task ConnectPorts_WithThreePorts_OpensThreeListeners()
        {

            await using var host = RendezvousTestHost.Create();

            var ports = TestNet.ParsePorts(host.ExecuteOk("ConnectPorts([?,?,?])"));

            Assert.Multiple(() => {
                Assert.That(ports,                     Has.Length.EqualTo(3));
                Assert.That(ports.Distinct().Count(),  Is.EqualTo(3));
                Assert.That(host.Session.Endpoints,    Has.Count.EqualTo(3));
            });

        }

        [Test]
        public async Task ConnectPorts_WithARestrictedPortRange_PicksPortsWithinThatRange()
        {

            await using var host = RendezvousTestHost.Create(options => {
                                       options.MinDataPort  = IPPort.Parse(45000);
                                       options.MaxDataPort  = IPPort.Parse(45999);
                                   });

            var ports = TestNet.ParsePorts(host.ExecuteOk("ConnectPorts([?,?])"));

            Assert.Multiple(() => {
                Assert.That(ports[0], Is.InRange(IPPort.Parse(45000), IPPort.Parse(45999)));
                Assert.That(ports[1], Is.InRange(IPPort.Parse(45000), IPPort.Parse(45999)));
            });

        }

        #endregion

        #region ConnectPorts errors

        [Test]
        public async Task ConnectPorts_WithAnAlreadyUsedPort_Fails()
        {

            await using var host = RendezvousTestHost.Create();

            var occupied = new TcpListener(System.Net.IPAddress.Loopback, 0);
            occupied.Start();

            try
            {

                var port      = (UInt16) ((IPEndPoint) occupied.LocalEndpoint).Port;
                var response  = host.Execute($"ConnectPorts([{port}, ?])");

                Assert.Multiple(() => {
                    Assert.That(response.Code,      Is.EqualTo(ResponseCode.PortInUse));
                    Assert.That(host.Manager.Count, Is.Zero, "A failed ConnectPorts must not leak a rendezvous!");
                });

            }
            finally
            {
                occupied.Stop();
                occupied.Dispose();
            }

        }

        [Test]
        public async Task ConnectPorts_WhenOnePortFails_ReleasesTheOtherPortsAgain()
        {

            await using var host = RendezvousTestHost.Create();

            var freePort  = TestNet.GetFreePorts(1)[0];
            var occupied  = new TcpListener(System.Net.IPAddress.Loopback, 0);
            occupied.Start();

            try
            {

                var occupiedPort  = (UInt16) ((IPEndPoint) occupied.LocalEndpoint).Port;
                var response      = host.Execute($"ConnectPorts([{freePort}, {occupiedPort}])");

                Assert.Multiple(() => {
                    Assert.That(response.Code,                 Is.EqualTo(ResponseCode.PortInUse));
                    Assert.That(host.Manager.Count,            Is.Zero);
                    Assert.That(TestNet.IsPortFree(freePort),  Is.True, "The already bound TCP port must be released again!");
                });

            }
            finally
            {
                occupied.Stop();
                occupied.Dispose();
            }

        }

        [Test]
        public async Task ConnectPorts_WithAPortOfAnotherRendezvous_Fails()
        {

            await using var host = RendezvousTestHost.Create();

            var ports     = TestNet.ParsePorts(host.ExecuteOk("ConnectPorts([?,?])"));
            var response  = host.Execute($"ConnectPorts([{ports[0]}, ?])");

            Assert.Multiple(() => {
                Assert.That(response.Code,      Is.EqualTo(ResponseCode.PortInUse));
                Assert.That(host.Manager.Count, Is.EqualTo(1));
            });

        }

        [Test]
        public async Task ConnectPorts_WithAPortOutsideOfThePortRange_Fails()
        {

            await using var host = RendezvousTestHost.Create(options => {
                                       options.MinDataPort  = IPPort.Parse(40000);
                                       options.MaxDataPort  = IPPort.Parse(41000);
                                   });

            var response = host.Execute("ConnectPorts([20000, ?])");

            Assert.Multiple(() => {
                Assert.That(response.Code,      Is.EqualTo(ResponseCode.PortNotAllowed));
                Assert.That(response.Text,      Does.Contain("40000"));
                Assert.That(host.Manager.Count, Is.Zero);
            });

        }

        [Test]
        public async Task ConnectPorts_WithTooManyPorts_Fails()
        {

            await using var host = RendezvousTestHost.Create(options => options.MaxPortsPerSession = 2);

            var response = host.Execute("ConnectPorts([?,?,?])");

            Assert.That(response.Code, Is.EqualTo(ResponseCode.TooManyPorts));

        }

        [Test]
        public async Task ConnectPorts_WithTooManySessions_Fails()
        {

            await using var host = RendezvousTestHost.Create(options => options.MaxSessions = 1);

            host.ExecuteOk("ConnectPorts([?,?])");

            var response = host.Execute("ConnectPorts([?,?])");

            Assert.Multiple(() => {
                Assert.That(response.Code,      Is.EqualTo(ResponseCode.TooManySessions));
                Assert.That(host.Manager.Count, Is.EqualTo(1));
            });

        }

        #endregion

        #region DisconnectPorts

        [Test]
        public async Task DisconnectPorts_ClosesTheRendezvous()
        {

            await using var host = RendezvousTestHost.Create();

            var ports    = TestNet.ParsePorts(host.ExecuteOk("ConnectPorts([?,?])"));
            var session  = host.Session;

            var response = host.ExecuteOk($"DisconnectPorts({ports[0]}, {ports[1]})");

            await session.Completion.WaitAsync(TestNet.Timeout);

            Assert.Multiple(() => {
                Assert.That(response.Text,               Is.EqualTo($"DisconnectPorts([{ports[0]}, {ports[1]}])"));
                Assert.That(session.State,               Is.EqualTo(SessionState.Closed));
                Assert.That(session.CloseReason,         Is.EqualTo(SessionCloseReason.DisconnectRequested));
                Assert.That(host.Manager.Count,          Is.Zero);
                Assert.That(TestNet.IsPortFree(ports[0]), Is.True, "The TCP ports must be free again!");
                Assert.That(TestNet.IsPortFree(ports[1]), Is.True, "The TCP ports must be free again!");
            });

        }

        [Test]
        public async Task DisconnectPorts_WithASinglePort_ClosesTheWholeRendezvous()
        {

            await using var host = RendezvousTestHost.Create();

            var ports     = TestNet.ParsePorts(host.ExecuteOk("ConnectPorts([?,?,?])"));
            var session   = host.Session;

            var response  = host.ExecuteOk($"DisconnectPorts({ports[1]})");

            await session.Completion.WaitAsync(TestNet.Timeout);

            Assert.Multiple(() => {
                Assert.That(response.Text,      Is.EqualTo($"DisconnectPorts([{ports[0]}, {ports[1]}, {ports[2]}])"));
                Assert.That(session.State,      Is.EqualTo(SessionState.Closed));
                Assert.That(host.Manager.Count, Is.Zero);
            });

        }

        [Test]
        public async Task DisconnectPorts_DisconnectsTheConnectedClients()
        {

            await using var host = RendezvousTestHost.Create();

            var ports = TestNet.ParsePorts(host.ExecuteOk("ConnectPorts([?,?])"));

            using var alice = await TestNet.ConnectAsync(ports[0]);
            using var bob   = await TestNet.ConnectAsync(ports[1]);

            await TestNet.WaitUntilAsync(() => host.Session.State == SessionState.Established,
                                         "The rendezvous was not established!");

            host.ExecuteOk($"DisconnectPorts({ports[0]}, {ports[1]})");

            await TestNet.ExpectEndOfStreamAsync(alice);
            await TestNet.ExpectEndOfStreamAsync(bob);

        }

        [Test]
        public async Task DisconnectPorts_ByAnotherKey_Fails()
        {

            await using var host = RendezvousTestHost.Create();

            using var alice  = ControlSigner.GenerateEd25519("alice");
            using var mallory = ControlSigner.GenerateEd25519("mallory");

            var ports     = TestNet.ParsePorts(host.ExecuteOk("ConnectPorts([?,?])",
                                                              new ControlAuthorization(alice.ToControlKey())));

            var response  = host.Execute($"DisconnectPorts({ports[0]}, {ports[1]})",
                                         new ControlAuthorization(mallory.ToControlKey()));

            Assert.Multiple(() => {
                Assert.That(response.Code,        Is.EqualTo(ResponseCode.Unauthorized));
                Assert.That(host.Manager.Count,   Is.EqualTo(1), "An unauthorized command must not close the rendezvous!");
                Assert.That(host.Session.State,   Is.EqualTo(SessionState.Pending));
            });

        }

        [Test]
        public async Task DisconnectPorts_ByTheOwningKey_ClosesTheRendezvous()
        {

            await using var host = RendezvousTestHost.Create();

            using var alice = ControlSigner.GenerateEd25519("alice");

            var owner    = new ControlAuthorization(alice.ToControlKey());
            var ports    = TestNet.ParsePorts(host.ExecuteOk("ConnectPorts([?,?])", owner));
            var session  = host.Session;

            host.ExecuteOk($"DisconnectPorts({ports[0]}, {ports[1]})", owner);

            await session.Completion.WaitAsync(TestNet.Timeout);

            Assert.Multiple(() => {
                Assert.That(session.CreatedBy,   Is.EqualTo(new[] { "alice" }));
                Assert.That(session.State,       Is.EqualTo(SessionState.Closed));
                Assert.That(host.Manager.Count,  Is.Zero);
            });

        }

        [Test]
        public async Task DisconnectPorts_ByAnAdministratorKey_ClosesTheRendezvousOfSomebodyElse()
        {

            await using var host = RendezvousTestHost.Create();

            using var alice = ControlSigner.GenerateEd25519("alice");
            using var root  = ControlSigner.GenerateEd25519("root");

            var ports    = TestNet.ParsePorts(host.ExecuteOk("ConnectPorts([?,?])",
                                                             new ControlAuthorization(alice.ToControlKey())));
            var session  = host.Session;

            host.ExecuteOk($"DisconnectPorts({ports[0]}, {ports[1]})",
                           new ControlAuthorization(root.ToControlKey(IsAdministrator: true)));

            await session.Completion.WaitAsync(TestNet.Timeout);

            Assert.Multiple(() => {
                Assert.That(session.State,       Is.EqualTo(SessionState.Closed));
                Assert.That(host.Manager.Count,  Is.Zero);
            });

        }

        [Test]
        public async Task DisconnectPorts_ByATrustedCaller_ClosesEveryRendezvous()
        {

            await using var host = RendezvousTestHost.Create();

            using var alice = ControlSigner.GenerateEd25519("alice");

            var ports    = TestNet.ParsePorts(host.ExecuteOk("ConnectPorts([?,?])",
                                                             new ControlAuthorization(alice.ToControlKey())));
            var session  = host.Session;

            // Whoever holds the manager could close everything by hand anyway.
            host.ExecuteOk($"DisconnectPorts({ports[0]}, {ports[1]})");

            await session.Completion.WaitAsync(TestNet.Timeout);

            Assert.That(session.State, Is.EqualTo(SessionState.Closed));

        }

        [Test]
        public async Task ConnectPorts_RecordsWhoOpenedItAndWhatItIsFor()
        {

            await using var host = RendezvousTestHost.Create();

            using var alice = ControlSigner.GenerateEd25519("alice");

            host.ExecuteOk("ConnectPorts([?,?], \"SSH rendezvous for maintenance work\")",
                           new ControlAuthorization(alice.ToControlKey()));

            var session = host.Session;

            Assert.Multiple(() => {
                Assert.That(session.CreatedBy,    Is.EqualTo(new[] { "alice" }));
                Assert.That(session.Description,  Is.EqualTo("SSH rendezvous for maintenance work"));
                Assert.That(session.CreatedUtc,   Is.EqualTo(host.Time.GetUtcNow()));
                Assert.That(session.IsOwnedBy("alice"),   Is.True);
                Assert.That(session.IsOwnedBy("mallory"), Is.False);
            });

        }

        [Test]
        public async Task DisconnectPorts_WithAnUnknownPort_Fails()
        {

            await using var host = RendezvousTestHost.Create();

            var response = host.Execute("DisconnectPorts(20000)");

            Assert.Multiple(() => {
                Assert.That(response.Code, Is.EqualTo(ResponseCode.UnknownSession));
                Assert.That(response.Text, Does.Contain("20000"));
            });

        }

        [Test]
        public async Task DisconnectPorts_WithPortsOfTwoRendezvous_Fails()
        {

            await using var host = RendezvousTestHost.Create();

            var ports1 = TestNet.ParsePorts(host.ExecuteOk("ConnectPorts([?,?], \"The first rendezvous\")"));
            var ports2 = TestNet.ParsePorts(host.ExecuteOk("ConnectPorts([?,?], \"The second rendezvous\")"));

            var response = host.Execute($"DisconnectPorts({ports1[0]}, {ports2[0]})");

            Assert.Multiple(() => {
                Assert.That(response.Code,      Is.EqualTo(ResponseCode.UnknownSession));
                Assert.That(host.Manager.Count, Is.EqualTo(2), "Both rendezvous must stay open!");
            });

        }

        #endregion

        #region Unknown commands

        [Test]
        public async Task UnknownCommand_IsReportedAsAnError()
        {

            await using var host = RendezvousTestHost.Create();

            var response = host.Execute("DoSomethingWeird(42)");

            Assert.Multiple(() => {
                Assert.That(response.IsSuccess,        Is.False);
                Assert.That(response.Code,             Is.EqualTo(ResponseCode.UnknownCommand));
                Assert.That(response.ToProtocolLine(), Does.StartWith("ERROR UnknownCommand "));
            });

        }

        #endregion

    }

}
