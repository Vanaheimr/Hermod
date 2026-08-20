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

using System.Buffers.Binary;
using System.Net.Sockets;

using Microsoft.Extensions.Logging.Abstractions;

using org.GraphDefined.Vanaheimr.Hermod.Rendezvous;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.Rendezvous
{

    /// <summary>
    /// End-to-end tests of the control endpoint: real TCP, real signed CBOR.
    /// </summary>
    [TestFixture]
    public class ControlServerTests
    {

        #region (class) ControlTestHost

        /// <summary>
        /// A running control endpoint on a free TCP port of the loopback interface,
        /// together with a client key it trusts.
        /// </summary>
        internal sealed class ControlTestHost : IAsyncDisposable
        {

            public RendezvousManager  Manager        { get; }
            public ControlServer      Server         { get; }
            public ControlSigner      ClientSigner   { get; }
            public IPSocket           RemoteSocket   { get; }

            private ControlTestHost(RendezvousManager  Manager,
                                    ControlServer      Server,
                                    ControlSigner      ClientSigner)
            {

                this.Manager       = Manager;
                this.Server        = Server;
                this.ClientSigner  = ClientSigner;
                this.RemoteSocket  = Server.LocalSocket!.Value;

            }

            public static ControlTestHost Start(Action<RendezvousOptions>?  Configure      = null,
                                                ControlSigner?              ClientSigner   = null,
                                                Boolean                     TrustTheKey    = true)
            {

                var configuration = new RendezvousOptions {
                                        ControlAddress   = "127.0.0.1",
                                        ControlPort      = IPPort.Parse(0),
                                        DataAddress      = "127.0.0.1",
                                        AutoMaintenance  = false
                                    };

                Configure?.Invoke(configuration);

                var manager  = new RendezvousManager(configuration, TimeProvider.System, NullLoggerFactory.Instance);
                var server   = new ControlServer   (manager, configuration, NullLoggerFactory.Instance);
                var signer   = ClientSigner ?? ControlSigner.GenerateEd25519("test-client");

                if (TrustTheKey)
                    server.Keys.Add(signer.ToControlKey());

                server.Start();

                return new ControlTestHost(manager, server, signer);

            }

            /// <summary>
            /// A client that trusts the response key of this endpoint.
            /// </summary>
            public RendezvousControlClient CreateClient(params ControlSigner[] Signers)
            {

                var serverKeys = new ControlKeyRing();
                serverKeys.Add(Server.ResponseKey);

                return new RendezvousControlClient(
                           RemoteSocket,
                           Signers.Length > 0 ? Signers : [ClientSigner],
                           serverKeys,
                           TimeSpan.FromSeconds(15)
                       );

            }

            public async ValueTask DisposeAsync()
            {
                await Server.DisposeAsync().AsTask().WaitAsync(TestNet.Timeout);
                await Manager.DisposeAsync();
                ClientSigner.Dispose();
            }

        }

        #endregion


        #region The control endpoint

        [Test]
        public async Task ControlEndpoint_IsListeningAfterStart()
        {

            await using var host = ControlTestHost.Start();

            Assert.Multiple(() => {
                Assert.That(host.Server.LocalSocket,                     Is.Not.Null);
                Assert.That(host.Server.LocalSocket!.Value.Port.ToUInt16(), Is.GreaterThan(0), "A control port of zero must be replaced by a free port!");
                Assert.That(host.Server.LocalSocket!.Value.IPAddress.IsLocalhost, Is.True);
                Assert.That(host.Server.ResponseKey.KeyType,             Is.EqualTo(SignatureKeyType.Ed25519), "Without a configured key the endpoint generates an ephemeral one.");
            });

        }

        #endregion

        #region The full round trip

        [Test]
        public async Task ControlEndpoint_OpensAndClosesARendezvous()
        {

            await using var host    = ControlTestHost.Start();
            using var       client  = host.CreateClient();

            #region ConnectPorts

            var response = await client.ConnectPortsAsync(
                                     [PortSpecification.Random, PortSpecification.Random],
                                     TransferProfile.Interactive,
                                     "SSH rendezvous for maintenance work"
                                 );

            Assert.That(response.IsSuccess, Is.True, response.Message);

            Assert.Multiple(() => {
                Assert.That(response.Ports,        Has.Count.EqualTo(2));
                Assert.That(response.Profile,      Is.EqualTo(TransferProfile.Interactive));
                Assert.That(response.SignedBy,     Has.Count.EqualTo(1), "The response must be signed by the endpoint!");
                Assert.That(response.Description,  Is.EqualTo("SSH rendezvous for maintenance work"), "The service reports back what it recorded!");
                Assert.That(response.CreatedBy,    Is.EqualTo(new[] { "test-client" }), "The signing key owns the new rendezvous!");
                Assert.That(response.Created,      Is.Not.Null);
            });

            #endregion

            #region The clients meet each other

            using var alice = await TestNet.ConnectAsync(response.Ports[0]);
            using var bob   = await TestNet.ConnectAsync(response.Ports[1]);

            await TestNet.WaitUntilAsync(() => host.Manager.Sessions.Single().State == SessionState.Established,
                                         "The rendezvous was not established!");

            await TestNet.SendAsync(alice, "Hello Bob!");
            Assert.That(await TestNet.ReceiveAsync(bob, 10), Is.EqualTo("Hello Bob!"));

            #endregion

            #region DisconnectPorts

            var session = host.Manager.Sessions.Single();

            var closed = await client.DisconnectPortsAsync([response.Ports[0]], "Maintenance is done");

            Assert.That(closed.IsSuccess, Is.True, closed.Message);

            await session.Completion.WaitAsync(TestNet.Timeout);

            await TestNet.ExpectEndOfStreamAsync(alice);
            await TestNet.ExpectEndOfStreamAsync(bob);

            Assert.That(host.Manager.Count, Is.Zero);

            #endregion

        }

        [Test]
        public async Task ControlEndpoint_AnswersSeveralRequests()
        {

            await using var host    = ControlTestHost.Start();
            using var       client  = host.CreateClient();

            for (var i = 0; i < 3; i++)
            {

                var response = await client.ConnectPortsAsync(
                                         [PortSpecification.Random, PortSpecification.Random],
                                         Description: $"Rendezvous {i}"
                                     );

                Assert.That(response.IsSuccess, Is.True, response.Message);

            }

            Assert.That(host.Manager.Count, Is.EqualTo(3), "Every request needs its own nonce, which the client generates.");

        }

        #endregion

        #region Errors

        [Test]
        public async Task ControlEndpoint_RejectsADisconnectOfAnotherOperator()
        {

            await using var host = ControlTestHost.Start();

            // A second operator, trusted by the endpoint, but not the owner.
            using var mallorySigner = ControlSigner.GenerateEd25519("mallory");
            host.Server.Keys.Add(mallorySigner.ToControlKey());

            using var alice    = host.CreateClient();
            using var mallory  = host.CreateClient(mallorySigner);

            var opened = await alice.ConnectPortsAsync([PortSpecification.Random, PortSpecification.Random]);

            Assert.That(opened.IsSuccess, Is.True, opened.Message);

            var closed = await mallory.DisconnectPortsAsync([opened.Ports[0]]);

            Assert.Multiple(() => {
                Assert.That(closed.HasResponse,   Is.True, "A rejected command is still a response!");
                Assert.That(closed.IsSuccess,     Is.False);
                Assert.That(closed.Code,          Is.EqualTo(ResponseCode.Unauthorized));
                Assert.That(host.Manager.Count,   Is.EqualTo(1), "A rendezvous belongs to the key that opened it!");
            });

        }

        [Test]
        public async Task ControlEndpoint_LetsAnAdministratorCloseAForeignRendezvous()
        {

            await using var host = ControlTestHost.Start();

            using var rootSigner = ControlSigner.GenerateEd25519("root");
            host.Server.Keys.Add(rootSigner.ToControlKey(IsAdministrator: true));

            using var alice  = host.CreateClient();
            using var root   = host.CreateClient(rootSigner);

            var opened = await alice.ConnectPortsAsync([PortSpecification.Random, PortSpecification.Random]);

            Assert.That(opened.IsSuccess, Is.True, opened.Message);

            var session  = host.Manager.Sessions.Single();
            var closed   = await root.DisconnectPortsAsync([opened.Ports[0]], "Alice is on holiday");

            Assert.That(closed.IsSuccess, Is.True, closed.Message);

            await session.Completion.WaitAsync(TestNet.Timeout);

            Assert.That(host.Manager.Count, Is.Zero);

        }

        [Test]
        public async Task ControlEndpoint_RejectsAnOversizedFrame()
        {

            await using var host = ControlTestHost.Start(options => options.MaxFrameLength = 16384);

            using var client = new TcpClient();
            await client.ConnectAsync(System.Net.IPAddress.Loopback,
                                      host.RemoteSocket.Port.ToUInt16()).
                         WaitAsync(TestNet.Timeout);

            // Announce far more than the endpoint is willing to read.
            var header = new Byte[4];
            BinaryPrimitives.WriteInt32BigEndian(header, 1024 * 1024);

            await client.GetStream().WriteAsync(header).AsTask().WaitAsync(TestNet.Timeout);

            var response = await ReadResponseAsync(client);

            Assert.That(response?.Code, Is.EqualTo(ResponseCode.CommandTooLong));

        }

        [Test]
        public async Task ControlEndpoint_RejectsGarbage()
        {

            await using var host = ControlTestHost.Start();

            using var client = new TcpClient();
            await client.ConnectAsync(System.Net.IPAddress.Loopback,
                                      host.RemoteSocket.Port.ToUInt16()).
                         WaitAsync(TestNet.Timeout);

            var garbage = new Byte[] { 0xFF, 0x00, 0x17, 0x42 };
            var frame   = new Byte[4 + garbage.Length];
            BinaryPrimitives.WriteInt32BigEndian(frame, garbage.Length);
            garbage.CopyTo(frame, 4);

            await client.GetStream().WriteAsync(frame).AsTask().WaitAsync(TestNet.Timeout);

            var response = await ReadResponseAsync(client);

            Assert.That(response?.Code, Is.EqualTo(ResponseCode.InvalidSyntax));

        }

        [Test]
        public async Task ControlEndpoint_LimitsTheNumberOfControlConnections()
        {

            await using var host = ControlTestHost.Start(options => options.MaxControlConnections = 1);

            using var first = new TcpClient();
            await first.ConnectAsync(System.Net.IPAddress.Loopback,
                                     host.RemoteSocket.Port.ToUInt16()).
                        WaitAsync(TestNet.Timeout);

            // Keep the first connection open and busy.
            await TestNet.WaitUntilAsync(() => host.Server.OpenConnections == 1,
                                         "The first control connection was not accepted!");

            using var second = new TcpClient();
            await second.ConnectAsync(System.Net.IPAddress.Loopback,
                                      host.RemoteSocket.Port.ToUInt16()).
                         WaitAsync(TestNet.Timeout);

            var buffer   = new Byte[1];
            var received = await second.GetStream().ReadAsync(buffer.AsMemory()).AsTask().WaitAsync(TestNet.Timeout);

            Assert.That(received, Is.Zero, "The second control connection must be rejected!");

        }

        #endregion

        #region (private, static) ReadResponseAsync(Client)

        private static async Task<ControlResponse?> ReadResponseAsync(TcpClient Client)
        {

            var header = new Byte[4];
            var read   = await Client.GetStream().ReadExactlyAsync(header).AsTask().
                                      ContinueWith(_ => 4).WaitAsync(TestNet.Timeout);

            var length = BinaryPrimitives.ReadInt32BigEndian(header);
            var frame  = new Byte[length];

            await Client.GetStream().ReadExactlyAsync(frame).AsTask().WaitAsync(TestNet.Timeout);

            if (!SignedMessage.TryParse(frame, out var message, out _))
                return null;

            return ControlResponse.TryParse(message.Payload, out var response, out _)
                       ? response
                       : null;

        }

        #endregion

    }

}
