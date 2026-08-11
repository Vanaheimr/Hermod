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

using System.Security.Cryptography;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// M2 key-exchange breadth: the full handshake across every supported KEX method
    /// (curve25519-sha256, ecdh-sha2-nistp256/384/521, diffie-hellman-group14-sha256 /
    /// group16-sha512), exercising variable hash sizes.
    /// </summary>
    [TestFixture]
    public class KexMatrixTests
    {

        [TestCase(SshAlgorithmNames.Kex.Curve25519Sha256,      32)]
        [TestCase(SshAlgorithmNames.Kex.EcdhNistP256,          32)]
        [TestCase(SshAlgorithmNames.Kex.EcdhNistP384,          48)]
        [TestCase(SshAlgorithmNames.Kex.EcdhNistP521,          64)]
        [TestCase(SshAlgorithmNames.Kex.DhGroup14Sha256,       32)]
        [TestCase(SshAlgorithmNames.Kex.DhGroup16Sha512,       64)]
        [TestCase(SshAlgorithmNames.Kex.MlKem768X25519Sha256,  32)]
        [TestCase(SshAlgorithmNames.Kex.SntruP761X25519Sha512, 64)]
        [CancelAfter(20000)]
        public async Task Handshake_WithKex(String Kex, Int32 ExpectedSessionIdLength, CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();

            String[] kexes = [ Kex ];

            var clientTask = SshHandshake.ClientHandshakeAsync(clientPipe, KeyExchanges: kexes, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            var serverTask = SshHandshake.ServerHandshakeAsync(serverPipe, hostKey, KeyExchanges: kexes, CancellationToken: CancellationToken);

            using var client = await clientTask;
            using var server = await serverTask;

            Assert.Multiple(() => {
                Assert.That(client.Algorithms.KeyExchange, Is.EqualTo(Kex));
                Assert.That(client.SessionId,              Is.EqualTo(server.SessionId));
                // The session id is the exchange hash, whose length follows the KEX hash (SHA-256/384/512).
                Assert.That(client.SessionId.Length,       Is.EqualTo(ExpectedSessionIdLength));
            });

            // The derived keys work end-to-end.
            var payload = RandomNumberGenerator.GetBytes(200);
            SshPacketFraming.WritePacket(clientPipe.Output, client.SendCipher, payload, 0, client.SendMac);
            await clientPipe.Output.FlushAsync(CancellationToken);
            var got = await SshPacketFraming.ReadPacketAsync(serverPipe.Input, server.ReceiveCipher, 0, server.ReceiveMac, CancellationToken);

            Assert.That(got, Is.EqualTo(payload));

        }

    }

}
