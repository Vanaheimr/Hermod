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

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// M2 extension negotiation (RFC 8308): the SSH_MSG_EXT_INFO wire format and the server's
    /// "server-sig-algs" delivery right after the initial NEWKEYS.
    /// </summary>
    [TestFixture]
    public class ExtInfoTests
    {

        #region ExtInfo_EncodeDecode_RoundTrip

        [Test]
        public void ExtInfo_EncodeDecode_RoundTrip()
        {

            var original = new ExtInfoMessage(
                               ("server-sig-algs",       "ssh-ed25519,rsa-sha2-512,rsa-sha2-256"),
                               ("ping@openssh.com",      "0"),
                               ("delay-compression",     "none,none")
                           );

            var decoded = ExtInfoMessage.Decode(original.Encode());

            Assert.Multiple(() => {
                Assert.That(decoded.Extensions,          Has.Count.EqualTo(3));
                Assert.That(decoded["server-sig-algs"],  Is.EqualTo("ssh-ed25519,rsa-sha2-512,rsa-sha2-256"));
                Assert.That(decoded["ping@openssh.com"], Is.EqualTo("0"));
                Assert.That(decoded["delay-compression"],Is.EqualTo("none,none"));
                Assert.That(decoded["absent"],           Is.Null);
            });

        }

        #endregion

        #region ExtInfo_Encode_StartsWithMessageNumber7

        [Test]
        public void ExtInfo_Encode_StartsWithMessageNumber7()
        {
            Assert.That(ExtInfoMessage.ForServerSigAlgs([ "ssh-ed25519" ]).Encode()[0],
                        Is.EqualTo((Byte) SshMessageNumber.ExtInfo));
        }

        #endregion

        #region ExtInfo_Decode_RejectsWrongMessageNumber

        [Test]
        public void ExtInfo_Decode_RejectsWrongMessageNumber()
        {
            var notExtInfo = new Byte[] { (Byte) SshMessageNumber.ServiceRequest, 1, 2, 3 };
            Assert.That(() => ExtInfoMessage.Decode(notExtInfo), Throws.TypeOf<SshWireException>());
        }

        #endregion

        #region Server_SendsServerSigAlgs_AfterHandshake_ClientParsesThem

        [Test]
        [CancelAfter(10000)]
        public async Task Server_SendsServerSigAlgs_AfterHandshake_ClientParsesThem(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();

            var clientTask = SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            var serverTask = SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);

            using var client = await clientTask;
            using var server = await serverTask;

            Assert.That(client.Algorithms.ExtensionInfo, Is.True, "ext-info must be negotiated by default.");

            // The very next packet the client receives is the server's EXT_INFO.
            var payload = await client.ReceivePacketAsync(CancellationToken);

            Assert.Multiple(() => {
                Assert.That(payload[0],                     Is.EqualTo((Byte) SshMessageNumber.ExtInfo));
                Assert.That(client.TryHandleExtInfo(payload), Is.True);
            });

            var sigAlgs = client.PeerServerSignatureAlgorithms;

            Assert.Multiple(() => {
                Assert.That(sigAlgs,                                    Is.Not.Null);
                Assert.That(sigAlgs,                                    Does.Contain(SshAlgorithmNames.HostKey.Ed25519));
                Assert.That(sigAlgs,                                    Does.Contain(SshAlgorithmNames.HostKey.RsaSha2_256));
                Assert.That(sigAlgs,                                    Does.Contain(SshAlgorithmNames.HostKey.RsaSha2_512));
                Assert.That(client.PeerExtensions[SshExtensionNames.ServerSigAlgs], Is.Not.Empty);
                // A non-EXT_INFO packet must be left for the caller to dispatch.
                Assert.That(server.TryHandleExtInfo(new Byte[] { (Byte) SshMessageNumber.ServiceRequest }), Is.False);
            });

        }

        #endregion

        #region Server_CustomServerSigAlgs_AreDelivered

        [Test]
        [CancelAfter(10000)]
        public async Task Server_CustomServerSigAlgs_AreDelivered(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();

            String[] onlyEd25519 = [ SshAlgorithmNames.HostKey.Ed25519 ];

            var clientTask = SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            var serverTask = SshTransport.ServerHandshakeAsync(serverPipe, hostKey, ServerSignatureAlgorithms: onlyEd25519, CancellationToken: CancellationToken);

            using var client = await clientTask;
            using var server = await serverTask;

            var payload = await client.ReceivePacketAsync(CancellationToken);
            client.TryHandleExtInfo(payload);

            Assert.That(client.PeerServerSignatureAlgorithms, Is.EqualTo(onlyEd25519));

        }

        #endregion

    }

}
