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

using System.Buffers;
using System.IO.Pipelines;
using System.Security.Cryptography;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// M2 cipher/MAC breadth: the full transport handshake across every supported cipher family
    /// (AES-GCM and AES-CTR + HMAC-SHA2-ETM), plus a direct CTR+ETM framing round-trip and MAC
    /// tamper detection.
    /// </summary>
    [TestFixture]
    public class CipherMatrixTests
    {

        #region Handshake across the cipher matrix

        public static System.Collections.Generic.IEnumerable<TestCaseData> CipherMatrix()
        {
            yield return new TestCaseData(SshAlgorithmNames.Cipher.ChaCha20Poly1305, SshAlgorithmNames.Mac.HmacSha2_256Etm).SetName("chacha20-poly1305");
            yield return new TestCaseData(SshAlgorithmNames.Cipher.Aes256Gcm, SshAlgorithmNames.Mac.HmacSha2_256Etm).SetName("aes256-gcm");
            yield return new TestCaseData(SshAlgorithmNames.Cipher.Aes128Gcm, SshAlgorithmNames.Mac.HmacSha2_256Etm).SetName("aes128-gcm");
            yield return new TestCaseData(SshAlgorithmNames.Cipher.Aes256Ctr, SshAlgorithmNames.Mac.HmacSha2_256Etm).SetName("aes256-ctr+hmac-sha2-256-etm");
            yield return new TestCaseData(SshAlgorithmNames.Cipher.Aes128Ctr, SshAlgorithmNames.Mac.HmacSha2_256Etm).SetName("aes128-ctr+hmac-sha2-256-etm");
            yield return new TestCaseData(SshAlgorithmNames.Cipher.Aes256Ctr, SshAlgorithmNames.Mac.HmacSha2_512Etm).SetName("aes256-ctr+hmac-sha2-512-etm");
        }

        [Test]
        [CancelAfter(10000)]
        [TestCaseSource(nameof(CipherMatrix))]
        public async Task Handshake_And_EncryptedRoundTrip(String Cipher, String Mac, CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();

            String[] ciphers = [ Cipher ];
            String[] macs    = [ Mac ];

            var clientTask = SshHandshake.ClientHandshakeAsync(clientPipe, Ciphers: ciphers, Macs: macs, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            var serverTask = SshHandshake.ServerHandshakeAsync(serverPipe, hostKey, Ciphers: ciphers, Macs: macs, CancellationToken: CancellationToken);

            using var client = await clientTask;
            using var server = await serverTask;

            Assert.That(client.Algorithms.CipherClientToServer, Is.EqualTo(Cipher));

            // First post-NEWKEYS packet each way is sequence number 0 (strict-KEX resets counters).
            var clientToServer = RandomNumberGenerator.GetBytes(120);
            SshPacketFraming.WritePacket(clientPipe.Output, client.SendCipher, clientToServer, 0, client.SendMac);
            await clientPipe.Output.FlushAsync(CancellationToken);
            var gotByServer = await SshPacketFraming.ReadPacketAsync(serverPipe.Input, server.ReceiveCipher, 0, server.ReceiveMac, CancellationToken);

            var serverToClient = RandomNumberGenerator.GetBytes(64);
            SshPacketFraming.WritePacket(serverPipe.Output, server.SendCipher, serverToClient, 0, server.SendMac);
            await serverPipe.Output.FlushAsync(CancellationToken);
            var gotByClient = await SshPacketFraming.ReadPacketAsync(clientPipe.Input, client.ReceiveCipher, 0, client.ReceiveMac, CancellationToken);

            Assert.Multiple(() => {
                Assert.That(gotByServer, Is.EqualTo(clientToServer));
                Assert.That(gotByClient, Is.EqualTo(serverToClient));
            });

        }

        #endregion

        #region Direct CTR + HMAC-ETM framing

        [Test]
        [CancelAfter(5000)]
        public async Task CtrEtm_MultiPacket_SequenceNumbers()
        {

            var key   = RandomNumberGenerator.GetBytes(32);
            var iv    = RandomNumberGenerator.GetBytes(AesCtrTransportCipher.CounterLength);
            var macKey = RandomNumberGenerator.GetBytes(32);

            using var sendCipher = new AesCtrTransportCipher(key, iv);
            using var recvCipher = new AesCtrTransportCipher(key, iv);
            using var sendMac    = HmacSha2Mac.Sha256(macKey);
            using var recvMac    = HmacSha2Mac.Sha256(macKey);

            var pipe = new Pipe();

            var expected = new List<Byte[]>();
            for (var seq = 0u; seq < 8; seq++)
            {
                var payload = RandomNumberGenerator.GetBytes(30 + (Int32) seq);
                expected.Add(payload);
                SshPacketFraming.WritePacket(pipe.Writer, sendCipher, payload, seq, sendMac);
            }
            await pipe.Writer.FlushAsync();

            for (var seq = 0u; seq < 8; seq++)
            {
                var got = await SshPacketFraming.ReadPacketAsync(pipe.Reader, recvCipher, seq, recvMac);
                Assert.That(got, Is.EqualTo(expected[(Int32) seq]), $"packet {seq}");
            }

        }

        [Test]
        [CancelAfter(5000)]
        public async Task CtrEtm_TamperedCiphertext_FailsMac()
        {

            var key    = RandomNumberGenerator.GetBytes(32);
            var iv     = RandomNumberGenerator.GetBytes(AesCtrTransportCipher.CounterLength);
            var macKey = RandomNumberGenerator.GetBytes(32);

            using var sendCipher = new AesCtrTransportCipher(key, iv);
            using var recvCipher = new AesCtrTransportCipher(key, iv);
            using var sendMac    = HmacSha2Mac.Sha256(macKey);
            using var recvMac    = HmacSha2Mac.Sha256(macKey);

            var abw = new ArrayBufferWriter<Byte>();
            SshPacketFraming.WritePacket(abw, sendCipher, "secret payload"u8.ToArray(), 0, sendMac);

            var wire = abw.WrittenSpan.ToArray();
            wire[10] ^= 0x01;   // flip a bit in the ciphertext

            var pipe = new Pipe();
            await pipe.Writer.WriteAsync(wire);
            await pipe.Writer.FlushAsync();

            Assert.That(async () => await SshPacketFraming.ReadPacketAsync(pipe.Reader, recvCipher, 0, recvMac),
                        Throws.TypeOf<SshWireException>());

        }

        [Test]
        [CancelAfter(5000)]
        public async Task CtrEtm_WrongSequenceNumber_FailsMac()
        {

            var key    = RandomNumberGenerator.GetBytes(32);
            var iv     = RandomNumberGenerator.GetBytes(AesCtrTransportCipher.CounterLength);
            var macKey = RandomNumberGenerator.GetBytes(32);

            using var sendCipher = new AesCtrTransportCipher(key, iv);
            using var recvCipher = new AesCtrTransportCipher(key, iv);
            using var sendMac    = HmacSha2Mac.Sha256(macKey);
            using var recvMac    = HmacSha2Mac.Sha256(macKey);

            var pipe = new Pipe();
            SshPacketFraming.WritePacket(pipe.Writer, sendCipher, "data"u8.ToArray(), SequenceNumber: 5, Mac: sendMac);
            await pipe.Writer.FlushAsync();

            // Reading with the wrong sequence number must fail the MAC (it is part of the MAC input).
            Assert.That(async () => await SshPacketFraming.ReadPacketAsync(pipe.Reader, recvCipher, SequenceNumber: 6, Mac: recvMac),
                        Throws.TypeOf<SshWireException>());

        }

        #endregion

    }

}
