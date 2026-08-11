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
    /// Unit tests for the SSH binary packet protocol framing (RFC 4253, section 6) and the
    /// AES-GCM AEAD cipher (RFC 5647 / OpenSSH semantics).
    /// </summary>
    [TestFixture]
    public class PacketFramingTests
    {

        #region Padding

        [Test]
        public void Padding_NullCipher_AlignsWholePacketTo8()
        {
            // none: (4 length + 1 padlen + payload + padding) is a multiple of 8, padding >= 4.
            for (var payload = 0; payload < 64; payload++)
            {
                var pad   = SshPacketFraming.ComputePaddingLength(payload, BlockSize: 8, LengthIncludedInAlignment: true);
                var total = 4 + 1 + payload + pad;
                Assert.Multiple(() => {
                    Assert.That(pad,          Is.GreaterThanOrEqualTo(4), $"payload={payload}");
                    Assert.That(total % 8,    Is.EqualTo(0),             $"payload={payload}");
                });
            }
        }

        [Test]
        public void Padding_AesGcm_AlignsEncryptedRegionTo16()
        {
            // aes-gcm: length field excluded; (1 padlen + payload + padding) is a multiple of 16.
            for (var payload = 0; payload < 64; payload++)
            {
                var pad          = SshPacketFraming.ComputePaddingLength(payload, BlockSize: 16, LengthIncludedInAlignment: false);
                var packetLength = 1 + payload + pad;
                Assert.Multiple(() => {
                    Assert.That(pad,               Is.GreaterThanOrEqualTo(4), $"payload={payload}");
                    Assert.That(packetLength % 16, Is.EqualTo(0),             $"payload={payload}");
                });
            }
        }

        #endregion

        #region (static) Roundtrip helper

        private static async Task<Byte[]> RoundTripAsync(SshTransportCipher WriteCipher,
                                                         SshTransportCipher ReadCipher,
                                                         Byte[]             Payload)
        {

            var pipe = new Pipe();

            SshPacketFraming.WritePacket(pipe.Writer, WriteCipher, Payload);
            await pipe.Writer.FlushAsync();

            return await SshPacketFraming.ReadPacketAsync(pipe.Reader, ReadCipher);

        }

        #endregion

        #region Null cipher round-trips

        [Test]
        [CancelAfter(5000)]
        public async Task NullCipher_RoundTrip_VariousSizes()
        {
            foreach (var size in new[] { 0, 1, 5, 16, 255, 4096 })
            {
                var payload = RandomNumberGenerator.GetBytes(size);
                var result  = await RoundTripAsync(NullTransportCipher.Instance, NullTransportCipher.Instance, payload);
                Assert.That(result, Is.EqualTo(payload), $"size={size}");
            }
        }

        #endregion

        #region AES-GCM round-trips

        [Test]
        [CancelAfter(5000)]
        public async Task AesGcm_RoundTrip_SinglePacket()
        {

            var key    = RandomNumberGenerator.GetBytes(32);
            var iv     = RandomNumberGenerator.GetBytes(AesGcmTransportCipher.NonceLength);

            using var send = new AesGcmTransportCipher(key, iv);
            using var recv = new AesGcmTransportCipher(key, iv);

            var payload = "SSH_MSG_KEXINIT and friends"u8.ToArray();
            var result  = await RoundTripAsync(send, recv, payload);

            Assert.That(result, Is.EqualTo(payload));

        }

        [Test]
        [CancelAfter(5000)]
        public async Task AesGcm_NonceCounter_StaysInSyncAcrossManyPackets()
        {

            var key    = RandomNumberGenerator.GetBytes(32);
            var iv     = RandomNumberGenerator.GetBytes(AesGcmTransportCipher.NonceLength);

            using var send = new AesGcmTransportCipher(key, iv);
            using var recv = new AesGcmTransportCipher(key, iv);

            var pipe = new Pipe();

            // Ten packets in a row must each decrypt correctly (the invocation counter advances in lockstep).
            var expected = new List<Byte[]>();
            for (var i = 0; i < 10; i++)
            {
                var payload = RandomNumberGenerator.GetBytes(20 + i);
                expected.Add(payload);
                SshPacketFraming.WritePacket(pipe.Writer, send, payload);
            }
            await pipe.Writer.FlushAsync();

            for (var i = 0; i < 10; i++)
            {
                var got = await SshPacketFraming.ReadPacketAsync(pipe.Reader, recv);
                Assert.That(got, Is.EqualTo(expected[i]), $"packet {i}");
            }

        }

        [Test]
        [CancelAfter(5000)]
        public async Task AesGcm_TamperedCiphertext_FailsAuthentication()
        {

            var key = RandomNumberGenerator.GetBytes(32);
            var iv  = RandomNumberGenerator.GetBytes(AesGcmTransportCipher.NonceLength);

            using var send = new AesGcmTransportCipher(key, iv);
            using var recv = new AesGcmTransportCipher(key, iv);

            var abw = new ArrayBufferWriter<Byte>();
            SshPacketFraming.WritePacket(abw, send, "authentic data"u8.ToArray());

            // Flip a bit inside the ciphertext (skip the 4-byte length prefix).
            var wire = abw.WrittenSpan.ToArray();
            wire[8] ^= 0x01;

            var pipe = new Pipe();
            await pipe.Writer.WriteAsync(wire);
            await pipe.Writer.FlushAsync();

            Assert.That(async () => await SshPacketFraming.ReadPacketAsync(pipe.Reader, recv),
                        Throws.TypeOf<SshWireException>());

        }

        #endregion

        #region Malformed

        [Test]
        [CancelAfter(5000)]
        public async Task Read_OversizedLength_Throws()
        {

            var pipe = new Pipe();
            // packet_length = 0x00FFFFFF (> MaxPacketLength) — must be rejected before allocation.
            await pipe.Writer.WriteAsync(new Byte[] { 0x00, 0xFF, 0xFF, 0xFF });
            await pipe.Writer.FlushAsync();

            Assert.That(async () => await SshPacketFraming.ReadPacketAsync(pipe.Reader, NullTransportCipher.Instance),
                        Throws.TypeOf<SshWireException>());

        }

        #endregion

    }

}
