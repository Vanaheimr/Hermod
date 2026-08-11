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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// Unit tests for the post-quantum hybrid key exchanges (mlkem768x25519-sha256,
    /// sntrup761x25519-sha512) and their underlying KEMs (ML-KEM-768 via the BCL, sntrup761 via BC).
    /// </summary>
    [TestFixture]
    public class HybridKeyExchangeTests
    {

        #region Kem_RoundTrip

        [Test]
        public void MlKem768_Encapsulate_Decapsulate_RoundTrip()
            => AssertKemRoundTrip(SshKem.MlKem768(), 1184, 1088);

        [Test]
        public void SNtruP761_Encapsulate_Decapsulate_RoundTrip()
            => AssertKemRoundTrip(SshKem.SNtruP761(), 1158, 1039);

        private static void AssertKemRoundTrip(SshKem Kem, Int32 ExpectedPublicKeyLength, Int32 ExpectedCiphertextLength)
        {

            using var keyPair = Kem.GenerateKeyPair();

            Assert.That(keyPair.PublicKey.Length, Is.EqualTo(ExpectedPublicKeyLength));
            Assert.That(Kem.PublicKeyLength,      Is.EqualTo(ExpectedPublicKeyLength));

            var (ciphertext, serverSecret) = Kem.Encapsulate(keyPair.PublicKey);
            var clientSecret               = keyPair.Decapsulate(ciphertext);

            Assert.Multiple(() => {
                Assert.That(ciphertext.Length, Is.EqualTo(ExpectedCiphertextLength));
                Assert.That(Kem.CiphertextLength, Is.EqualTo(ExpectedCiphertextLength));
                Assert.That(clientSecret, Is.EqualTo(serverSecret));   // both sides agree
                Assert.That(serverSecret, Is.Not.Empty);
            });

        }

        #endregion

        #region Hybrid_BothSides_DeriveSameSecret

        [TestCase(SshAlgorithmNames.Kex.MlKem768X25519Sha256,  32, 1184 + 32, 1088 + 32)]
        [TestCase(SshAlgorithmNames.Kex.SntruP761X25519Sha512, 64, 1158 + 32, 1039 + 32)]
        public void Hybrid_BothSides_DeriveSameSecret(String Kex, Int32 SecretLength, Int32 ClientPublicLength, Int32 ServerPublicLength)
        {

            using var client = SshKeyExchange.Create(Kex);
            using var server = SshKeyExchange.Create(Kex);

            var clientPublic          = client.StartClient();
            var (serverPublic, kServer) = server.ServerRespond(clientPublic);
            var kClient               = client.ClientFinish(serverPublic);

            Assert.Multiple(() => {
                Assert.That(clientPublic.Length, Is.EqualTo(ClientPublicLength),  "client public = KEM public || X25519 public");
                Assert.That(serverPublic.Length, Is.EqualTo(ServerPublicLength),  "server public = KEM ciphertext || X25519 public");
                Assert.That(kClient,             Is.EqualTo(kServer),             "both sides derive the same secret");
                Assert.That(kClient.Length,      Is.EqualTo(SecretLength),        "K is the KEX hash output (SHA-256/512)");
            });

        }

        #endregion

        #region Hybrid_EncodesSharedSecretAsString_NotMpint

        [Test]
        public void Hybrid_EncodesSharedSecretAsString_NotMpint()
        {

            using var client = SshKeyExchange.Create(SshAlgorithmNames.Kex.MlKem768X25519Sha256);
            using var server = SshKeyExchange.Create(SshAlgorithmNames.Kex.MlKem768X25519Sha256);

            var clientPublic       = client.StartClient();
            var (serverPublic, _)  = server.ServerRespond(clientPublic);
            var raw                = client.ClientFinish(serverPublic);

            var encoded = client.EncodeSharedSecret(raw);

            // An SSH string is a 4-byte big-endian length followed by exactly that many raw bytes — no
            // mpint sign-byte adjustment (which is the classic PQ interop bug).
            Assert.Multiple(() => {
                Assert.That(encoded.Length,                                Is.EqualTo(4 + raw.Length));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(encoded), Is.EqualTo((UInt32) raw.Length));
                Assert.That(encoded.AsSpan(4).ToArray(),                   Is.EqualTo(raw));
            });

        }

        #endregion

    }

}
