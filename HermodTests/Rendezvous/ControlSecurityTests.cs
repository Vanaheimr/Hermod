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

using org.GraphDefined.Vanaheimr.Hermod.Rendezvous;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.Rendezvous
{

    /// <summary>
    /// Tests for the authorization of control commands: the TCP port of a
    /// control endpoint is open to everybody, therefore a valid signature is
    /// the only thing that separates an operator from an attacker.
    /// </summary>
    [TestFixture]
    public class ControlSecurityTests
    {

        #region (private) SendRawAsync(Host, Message)

        /// <summary>
        /// Send an already built signed message and return the response.
        /// </summary>
        private static async Task<ControlResponse?> SendRawAsync(ControlServerTests.ControlTestHost  Host,
                                                                 SignedMessage                       Message)
        {

            using var client = new TcpClient();
            await client.ConnectAsync(System.Net.IPAddress.Loopback,
                                      Host.RemoteSocket.Port.ToUInt16()).
                         WaitAsync(TestNet.Timeout);

            var payload = Message.ToByteArray();
            var frame   = new Byte[4 + payload.Length];

            BinaryPrimitives.WriteInt32BigEndian(frame, payload.Length);
            payload.CopyTo(frame, 4);

            await client.GetStream().WriteAsync(frame).AsTask().WaitAsync(TestNet.Timeout);

            var header = new Byte[4];
            await client.GetStream().ReadExactlyAsync(header).AsTask().WaitAsync(TestNet.Timeout);

            var responseFrame = new Byte[BinaryPrimitives.ReadInt32BigEndian(header)];
            await client.GetStream().ReadExactlyAsync(responseFrame).AsTask().WaitAsync(TestNet.Timeout);

            if (!SignedMessage.TryParse(responseFrame, out var signedResponse, out _))
                return null;

            return ControlResponse.TryParse(signedResponse.Payload, out var response, out _)
                       ? response
                       : null;

        }

        #endregion

        #region (private) ConnectPortsRequest()

        private static ControlRequest ConnectPortsRequest(DateTimeOffset? Timestamp = null)

            => new (new ConnectPortsCommand(
                        [PortSpecification.Random, PortSpecification.Random]
                    ),
                    Timestamp: Timestamp);

        #endregion


        #region An unknown key

        [Test]
        public async Task AnUnknownKeyIsRejected()
        {

            await using var host = ControlServerTests.ControlTestHost.Start();

            using var stranger = ControlSigner.GenerateEd25519("stranger");

            var response = await SendRawAsync(
                                     host,
                                     SignedMessage.Create(ConnectPortsRequest().ToByteArray(), stranger)
                                 );

            Assert.Multiple(() => {
                Assert.That(response?.Code,     Is.EqualTo(ResponseCode.Unauthorized));
                Assert.That(host.Manager.Count, Is.Zero, "Nothing may be opened for an unknown key!");
            });

        }

        #endregion

        #region A tampered payload

        [Test]
        public async Task ATamperedPayloadIsRejected()
        {

            await using var host = ControlServerTests.ControlTestHost.Start();

            var request  = ConnectPortsRequest();
            var signed   = SignedMessage.Create(request.ToByteArray(), host.ClientSigner);

            // Same signature, different payload: add a description afterwards.
            var tampered = new SignedMessage(
                               new ControlRequest(
                                   new ConnectPortsCommand(
                                       [PortSpecification.Random, PortSpecification.Random],
                                       Description: "somethingElse"
                                   ),
                                   request.Nonce,
                                   request.Timestamp
                               ).ToByteArray(),
                               signed.Signatures
                           );

            var response = await SendRawAsync(host, tampered);

            Assert.Multiple(() => {
                Assert.That(response?.Code,     Is.EqualTo(ResponseCode.Unauthorized));
                Assert.That(host.Manager.Count, Is.Zero);
            });

        }

        #endregion

        #region Key validity

        [Test]
        public async Task AnExpiredKeyIsRejected()
        {

            using var signer = ControlSigner.GenerateEd25519("expired-key");

            await using var host = ControlServerTests.ControlTestHost.Start(ClientSigner: signer, TrustTheKey: false);

            host.Server.Keys.Add(
                signer.ToControlKey(
                    NotAfter: DateTimeOffset.UtcNow.AddMinutes(-1)
                )
            );

            var response = await SendRawAsync(
                                     host,
                                     SignedMessage.Create(ConnectPortsRequest().ToByteArray(), signer)
                                 );

            Assert.That(response?.Code, Is.EqualTo(ResponseCode.Unauthorized));

        }

        [Test]
        public async Task AKeyThatIsNotValidYetIsRejected()
        {

            using var signer = ControlSigner.GenerateEd25519("future-key");

            await using var host = ControlServerTests.ControlTestHost.Start(ClientSigner: signer, TrustTheKey: false);

            host.Server.Keys.Add(
                signer.ToControlKey(
                    NotBefore: DateTimeOffset.UtcNow.AddHours(1)
                )
            );

            var response = await SendRawAsync(
                                     host,
                                     SignedMessage.Create(ConnectPortsRequest().ToByteArray(), signer)
                                 );

            Assert.That(response?.Code, Is.EqualTo(ResponseCode.Unauthorized));

        }

        [Test]
        public async Task AKeyWithinItsValidityIsAccepted()
        {

            using var signer = ControlSigner.GenerateEd25519("valid-key");

            await using var host = ControlServerTests.ControlTestHost.Start(ClientSigner: signer, TrustTheKey: false);

            host.Server.Keys.Add(
                signer.ToControlKey(
                    NotBefore:    DateTimeOffset.UtcNow.AddMinutes(-1),
                    NotAfter:     DateTimeOffset.UtcNow.AddMinutes(10),
                    Description:  "The operator on duty"
                )
            );

            var response = await SendRawAsync(
                                     host,
                                     SignedMessage.Create(ConnectPortsRequest().ToByteArray(), signer)
                                 );

            Assert.That(response?.Code, Is.EqualTo(ResponseCode.OK));

        }

        [Test]
        public async Task ARemovedKeyIsRejectedRightAway()
        {

            await using var host    = ControlServerTests.ControlTestHost.Start();
            using var       client  = host.CreateClient();

            var first = await client.ConnectPortsAsync([PortSpecification.Random, PortSpecification.Random]);
            Assert.That(first.IsSuccess, Is.True, first.Message);

            // A compromised key has to be revocable without a restart.
            Assert.That(host.Server.Keys.Remove(host.ClientSigner.KeyId), Is.True);

            var second = await client.ConnectPortsAsync([PortSpecification.Random, PortSpecification.Random]);

            Assert.Multiple(() => {
                Assert.That(second.Code,        Is.EqualTo(ResponseCode.Unauthorized));
                Assert.That(host.Manager.Count, Is.EqualTo(1), "Only the first rendezvous may exist.");
            });

        }

        #endregion

        #region Freshness

        [Test]
        public async Task AReplayedRequestIsRejected()
        {

            await using var host = ControlServerTests.ControlTestHost.Start();

            var signed = SignedMessage.Create(ConnectPortsRequest().ToByteArray(), host.ClientSigner);

            var first  = await SendRawAsync(host, signed);
            var second = await SendRawAsync(host, signed);   // the very same bytes again

            Assert.Multiple(() => {
                Assert.That(first?.Code,        Is.EqualTo(ResponseCode.OK));
                Assert.That(second?.Code,       Is.EqualTo(ResponseCode.Unauthorized));
                Assert.That(host.Manager.Count, Is.EqualTo(1), "A replay must not open a second rendezvous!");
            });

        }

        [Test]
        public async Task AStaleRequestIsRejected()
        {

            await using var host = ControlServerTests.ControlTestHost.Start(options => options.MaxClockSkew = TimeSpan.FromMinutes(5));

            var response = await SendRawAsync(
                                     host,
                                     SignedMessage.Create(
                                         ConnectPortsRequest(DateTimeOffset.UtcNow.AddMinutes(-10)).ToByteArray(),
                                         host.ClientSigner
                                     )
                                 );

            Assert.Multiple(() => {
                Assert.That(response?.Code,     Is.EqualTo(ResponseCode.Unauthorized));
                Assert.That(response?.Message,  Does.Contain("timestamp"));
            });

        }

        [Test]
        public async Task ARequestFromTheFutureIsRejected()
        {

            await using var host = ControlServerTests.ControlTestHost.Start(options => options.MaxClockSkew = TimeSpan.FromMinutes(5));

            var response = await SendRawAsync(
                                     host,
                                     SignedMessage.Create(
                                         ConnectPortsRequest(DateTimeOffset.UtcNow.AddMinutes(10)).ToByteArray(),
                                         host.ClientSigner
                                     )
                                 );

            Assert.That(response?.Code, Is.EqualTo(ResponseCode.Unauthorized));

        }

        #endregion

        #region Several mandatory signatures

        [Test]
        public async Task TwoRequiredSignatures_OneIsNotEnough()
        {

            using var ed25519 = ControlSigner.GenerateEd25519("operator-ed25519");

            await using var host = ControlServerTests.ControlTestHost.Start(
                                       options => options.RequiredSignatures = 2,
                                       ClientSigner: ed25519
                                   );

            var response = await SendRawAsync(
                                     host,
                                     SignedMessage.Create(ConnectPortsRequest().ToByteArray(), ed25519)
                                 );

            Assert.Multiple(() => {
                Assert.That(response?.Code,    Is.EqualTo(ResponseCode.Unauthorized));
                Assert.That(response?.Message, Does.Contain("2"));
            });

        }

        [Test]
        public async Task TwoRequiredSignatures_ClassicalAndPostQuantumTogether()
        {

            if (!ControlKey.IsMLDsaSupported)
                Assert.Ignore("This platform does not offer ML-DSA.");

            using var ed25519 = ControlSigner.GenerateEd25519("operator-ed25519");
            using var mlDsa   = ControlSigner.GenerateMLDsa  ("operator-mldsa", SignatureKeyType.MLDsa65);

            await using var host = ControlServerTests.ControlTestHost.Start(
                                       options => options.RequiredSignatures = 2,
                                       ClientSigner: ed25519
                                   );

            host.Server.Keys.Add(mlDsa.ToControlKey());

            var response = await SendRawAsync(
                                     host,
                                     SignedMessage.Create(ConnectPortsRequest().ToByteArray(), ed25519, mlDsa)
                                 );

            Assert.Multiple(() => {
                Assert.That(response?.Code,     Is.EqualTo(ResponseCode.OK));
                Assert.That(host.Manager.Count, Is.EqualTo(1));
            });

        }

        [Test]
        public async Task TheSameKeyTwiceIsStillOneSignature()
        {

            await using var host = ControlServerTests.ControlTestHost.Start(options => options.RequiredSignatures = 2);

            var payload = ConnectPortsRequest().ToByteArray();

            // Signing twice with the same key does not make a request twice as trusted.
            var response = await SendRawAsync(
                                     host,
                                     new SignedMessage(
                                         payload,
                                         [host.ClientSigner.SignatureFor(payload),
                                          host.ClientSigner.SignatureFor(payload)]
                                     )
                                 );

            Assert.That(response?.Code, Is.EqualTo(ResponseCode.Unauthorized));

        }

        #endregion

        #region Ed448 and ML-DSA

        [Test]
        public async Task AnEd448KeyIsAccepted()
        {

            using var signer = ControlSigner.GenerateEd448("operator-ed448");

            await using var host = ControlServerTests.ControlTestHost.Start(ClientSigner: signer);

            var response = await SendRawAsync(
                                     host,
                                     SignedMessage.Create(ConnectPortsRequest().ToByteArray(), signer)
                                 );

            Assert.Multiple(() => {
                Assert.That(response?.Code,        Is.EqualTo(ResponseCode.OK));
                Assert.That(signer.PublicKey,      Has.Length.EqualTo(57));
                Assert.That(signer.KeyType,        Is.EqualTo(SignatureKeyType.Ed448));
            });

        }

        [Test]
        [TestCase(SignatureKeyType.MLDsa44)]
        [TestCase(SignatureKeyType.MLDsa65)]
        [TestCase(SignatureKeyType.MLDsa87)]
        public async Task AnMLDsaKeyIsAccepted(SignatureKeyType KeyType)
        {

            if (!ControlKey.IsMLDsaSupported)
                Assert.Ignore("This platform does not offer ML-DSA.");

            using var signer = ControlSigner.GenerateMLDsa("operator-mldsa", KeyType);

            await using var host = ControlServerTests.ControlTestHost.Start(ClientSigner: signer);

            var response = await SendRawAsync(
                                     host,
                                     SignedMessage.Create(ConnectPortsRequest().ToByteArray(), signer)
                                 );

            Assert.Multiple(() => {
                Assert.That(response?.Code,   Is.EqualTo(ResponseCode.OK));
                Assert.That(signer.PublicKey, Has.Length.EqualTo(KeyType.PublicKeySize()));
            });

        }

        #endregion

        #region Ownership

        [Test]
        public async Task AKeyCanNotCloseTheRendezvousOfAnotherKey()
        {

            await using var host = ControlServerTests.ControlTestHost.Start();

            using var mallorySigner = ControlSigner.GenerateEd25519("mallory");
            host.Server.Keys.Add(mallorySigner.ToControlKey());

            using var alice    = host.CreateClient();
            using var mallory  = host.CreateClient(mallorySigner);

            var opened = await alice.ConnectPortsAsync([PortSpecification.Random, PortSpecification.Random]);
            Assert.That(opened.IsSuccess, Is.True, opened.Message);

            // Mallory is a perfectly valid operator - and still not the owner.
            var stolen = await mallory.DisconnectPortsAsync([opened.Ports[0]]);

            Assert.Multiple(() => {
                Assert.That(stolen.Code,        Is.EqualTo(ResponseCode.Unauthorized));
                Assert.That(stolen.Message,     Does.Not.Contain("mallory"), "An error message must not become an oracle!");
                Assert.That(host.Manager.Count, Is.EqualTo(1));
            });

        }

        [Test]
        public async Task ARendezvousOfTheProcessItselfIsNotClosableFromOutside()
        {

            await using var host = ControlServerTests.ControlTestHost.Start();

            // Opened by the application, not through the control endpoint:
            // it has no owning key, so no ordinary operator may close it.
            host.Manager.TryConnectPorts(
                new ConnectPortsCommand([PortSpecification.Random, PortSpecification.Random]),
                out var session,
                out _
            );

            using var client = host.CreateClient();

            var closed = await client.DisconnectPortsAsync([session!.Ports[0]]);

            Assert.Multiple(() => {
                Assert.That(closed.Code,        Is.EqualTo(ResponseCode.Unauthorized));
                Assert.That(host.Manager.Count, Is.EqualTo(1));
            });

        }

        [Test]
        public async Task AnAdministratorKeyMayCloseEveryRendezvous()
        {

            await using var host = ControlServerTests.ControlTestHost.Start();

            using var rootSigner = ControlSigner.GenerateEd25519("root");
            host.Server.Keys.Add(rootSigner.ToControlKey(IsAdministrator: true));

            host.Manager.TryConnectPorts(
                new ConnectPortsCommand([PortSpecification.Random, PortSpecification.Random]),
                out var session,
                out _
            );

            using var root = host.CreateClient(rootSigner);

            var closed = await root.DisconnectPortsAsync([session!.Ports[0]], "Cleaning up");

            Assert.That(closed.IsSuccess, Is.True, closed.Message);

            await session.Completion.WaitAsync(TestNet.Timeout);

            Assert.That(host.Manager.Count, Is.Zero);

        }

        [Test]
        public async Task ARevokedKeyLosesItsRendezvous()
        {

            await using var host = ControlServerTests.ControlTestHost.Start();

            using var client = host.CreateClient();

            var opened = await client.ConnectPortsAsync([PortSpecification.Random, PortSpecification.Random]);
            Assert.That(opened.IsSuccess, Is.True, opened.Message);

            // Revoking a key takes effect immediately, also for what it opened.
            host.Server.Keys.Remove(host.ClientSigner.KeyId);

            var closed = await client.DisconnectPortsAsync([opened.Ports[0]]);

            Assert.Multiple(() => {
                Assert.That(closed.Code,        Is.EqualTo(ResponseCode.Unauthorized));
                Assert.That(host.Manager.Count, Is.EqualTo(1));
            });

        }

        #endregion

        #region The response signature

        [Test]
        public async Task TheResponseIsSigned()
        {

            await using var host    = ControlServerTests.ControlTestHost.Start();
            using var       client  = host.CreateClient();

            var response = await client.ConnectPortsAsync(
                                     [PortSpecification.Random, PortSpecification.Random]
                                 );

            Assert.Multiple(() => {
                Assert.That(response.IsSuccess,   Is.True, response.Message);
                Assert.That(response.SignedBy,    Has.Count.EqualTo(1));
                Assert.That(response.SignedBy[0].Id, Is.EqualTo(host.Server.ResponseKey.Id));
            });

        }

        [Test]
        public async Task AResponseOfAnUnknownKeyIsRejectedByTheClient()
        {

            await using var host = ControlServerTests.ControlTestHost.Start();

            // A client pinning the wrong server key must not believe the answer.
            var wrongKeys = new ControlKeyRing();
            using var somebodyElse = ControlSigner.GenerateEd25519("not-the-server");
            wrongKeys.Add(somebodyElse.ToControlKey());

            using var client = new RendezvousControlClient(
                                   host.RemoteSocket,
                                   host.ClientSigner,
                                   wrongKeys,
                                   TimeSpan.FromSeconds(15)
                               );

            var response = await client.ConnectPortsAsync(
                                     [PortSpecification.Random, PortSpecification.Random]
                                 );

            Assert.Multiple(() => {
                Assert.That(response.HasResponse, Is.False);
                Assert.That(response.Error,       Does.Contain("signed"));
            });

        }

        #endregion

    }

}
