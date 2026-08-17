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
using System.Buffers.Binary;
using System.Security.Cryptography;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// Frames and de-frames SSH binary packets (RFC 4253, section 6) over
    /// <see cref="System.IO.Pipelines"/>, delegating encryption/decryption to an
    /// <see cref="SshTransportCipher"/>. Handles the padding computation and defensive length checks.
    /// </summary>
    public static class SshPacketFraming
    {

        #region Data

        /// <summary>
        /// The largest accepted packet_length, bounding a peer's allocation demand.
        /// </summary>
        public const Int32 MaxPacketLength = 256 * 1024;

        /// <summary>
        /// The smallest sensible packet_length (1 padding_length byte + minimum 4 padding + margin).
        /// </summary>
        public const Int32 MinPacketLength = 8;

        #endregion


        #region ComputePaddingLength(PayloadLength, BlockSize, LengthIncludedInAlignment)

        /// <summary>
        /// Compute the padding length so the aligned region is a whole number of blocks, with at least
        /// 4 bytes of padding (RFC 4253, section 6).
        /// </summary>
        public static Byte ComputePaddingLength(Int32    PayloadLength,
                                                Int32    BlockSize,
                                                Boolean  LengthIncludedInAlignment)
        {

            var block     = Math.Max(BlockSize, 8);
            var unpadded  = (LengthIncludedInAlignment ? 4 : 0) + 1 + PayloadLength;
            var padding   = block - (unpadded % block);

            if (padding < 4)
                padding += block;

            return (Byte) padding;

        }

        #endregion

        #region WritePacket(Output, Cipher, Payload, SequenceNumber = 0, Mac = null)

        /// <summary>
        /// Frame and encrypt one packet with the given payload, writing it to <paramref name="Output"/>
        /// (the caller flushes).
        /// </summary>
        /// <param name="Output">The destination buffer writer (e.g. a <see cref="PipeWriter"/>).</param>
        /// <param name="Cipher">The send cipher for the current direction.</param>
        /// <param name="Payload">The packet payload (the message).</param>
        /// <param name="SequenceNumber">The outbound packet sequence number (used by the MAC).</param>
        /// <param name="Mac">An optional encrypt-then-MAC (for CTR ciphers); null for AEAD/none.</param>
        public static void WritePacket(IBufferWriter<Byte>  Output,
                                       SshTransportCipher   Cipher,
                                       ReadOnlySpan<Byte>   Payload,
                                       UInt32               SequenceNumber  = 0,
                                       ISshMac?             Mac             = null)
        {

            // chacha20-poly1305@openssh.com encrypts the length field itself; it owns the whole packet.
            if (Cipher is ChaCha20Poly1305Cipher chacha)
            {
                chacha.EncryptPacketInto(SequenceNumber, Payload, Output);
                return;
            }

            var paddingLength  = ComputePaddingLength(Payload.Length, Cipher.BlockSize, Cipher.LengthIncludedInPaddingAlignment);
            var packetLength   = 1 + Payload.Length + paddingLength;
            var macLength      = Mac?.Length ?? 0;

            // Assemble the plaintext block: padding_length || payload || random padding.
            var plaintext = ArrayPool<Byte>.Shared.Rent(packetLength);
            try
            {

                plaintext[0] = paddingLength;
                Payload.CopyTo(plaintext.AsSpan(1));
                RandomNumberGenerator.Fill(plaintext.AsSpan(1 + Payload.Length, paddingLength));

                var output = Output.GetSpan(4 + packetLength + Cipher.TagLength + macLength);

                BinaryPrimitives.WriteUInt32BigEndian(output, (UInt32) packetLength);

                var cipherText = output.Slice(4, packetLength + Cipher.TagLength);
                Cipher.Encrypt(output[..4], plaintext.AsSpan(0, packetLength), cipherText);

                // Encrypt-then-MAC: MAC over sequence_number || packet_length || ciphertext.
                Mac?.ComputeInto(SequenceNumber, output[..4], cipherText, output.Slice(4 + packetLength + Cipher.TagLength, macLength));

                Output.Advance(4 + packetLength + Cipher.TagLength + macLength);

            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext.AsSpan(0, packetLength));
                ArrayPool<Byte>.Shared.Return(plaintext);
            }

        }

        #endregion

        #region ReadPacketAsync(Input, Cipher, SequenceNumber = 0, Mac = null, CancellationToken = default)

        /// <summary>
        /// Read, decrypt and authenticate one packet, returning its payload.
        /// </summary>
        /// <param name="Input">The source pipe reader.</param>
        /// <param name="Cipher">The receive cipher for the current direction.</param>
        /// <param name="SequenceNumber">The inbound packet sequence number (used by the MAC).</param>
        /// <param name="Mac">An optional encrypt-then-MAC (for CTR ciphers); null for AEAD/none.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static async ValueTask<Byte[]> ReadPacketAsync(PipeReader          Input,
                                                              SshTransportCipher  Cipher,
                                                              UInt32              SequenceNumber     = 0,
                                                              ISshMac?            Mac                = null,
                                                              CancellationToken   CancellationToken  = default)
        {

            // chacha20-poly1305@openssh.com: the length field is encrypted and must be decrypted first.
            if (Cipher is ChaCha20Poly1305Cipher chacha)
            {

                var encryptedLength  = await ReadExactAsync(Input, 4, CancellationToken).ConfigureAwait(false);
                var chachaLength     = chacha.DecryptLength(SequenceNumber, encryptedLength);

                if (chachaLength < MinPacketLength || chachaLength > MaxPacketLength)
                    throw new SshWireException($"Illegal SSH packet_length {chachaLength} (must be {MinPacketLength}..{MaxPacketLength})!");

                var chachaBody   = await ReadExactAsync(Input, (Int32) chachaLength + chacha.TagLength, CancellationToken).ConfigureAwait(false);
                var chachaPlain  = chacha.DecryptAndVerify(SequenceNumber, encryptedLength, chachaBody);

                var chachaPad    = chachaPlain[0];
                var chachaPayLen = (Int32) chachaLength - chachaPad - 1;
                if (chachaPad < 4 || chachaPayLen < 0)
                    throw new SshWireException($"Illegal SSH padding_length {chachaPad} for packet_length {chachaLength}!");

                return chachaPlain[1..(1 + chachaPayLen)];

            }

            var macLength = Mac?.Length ?? 0;

            // 1. The 4-byte packet_length is plaintext for all our ciphers (none, aes-gcm, aes-ctr+etm).
            var lengthBytes   = await ReadExactAsync(Input, 4, CancellationToken).ConfigureAwait(false);
            var packetLength  = BinaryPrimitives.ReadUInt32BigEndian(lengthBytes);

            if (packetLength < MinPacketLength || packetLength > MaxPacketLength)
                throw new SshWireException($"Illegal SSH packet_length {packetLength} (must be {MinPacketLength}..{MaxPacketLength})!");

            if (!Cipher.LengthIncludedInPaddingAlignment && packetLength % (UInt32) Cipher.BlockSize != 0)
                throw new SshWireException($"SSH packet_length {packetLength} is not a multiple of the {Cipher.BlockSize}-byte block size!");

            // 2. The encrypted region (packet_length bytes) plus the AEAD tag plus the MAC.
            var body        = await ReadExactAsync(Input, (Int32) packetLength + Cipher.TagLength + macLength, CancellationToken).ConfigureAwait(false);
            var cipherText  = body.AsSpan(0, (Int32) packetLength + Cipher.TagLength);

            // 3. Encrypt-then-MAC: verify the MAC (constant-time) before decrypting.
            if (Mac is not null)
            {
                Span<Byte> expected = stackalloc Byte[macLength];
                Mac.ComputeInto(SequenceNumber, lengthBytes, cipherText, expected);
                if (!CryptographicOperations.FixedTimeEquals(expected, body.AsSpan((Int32) packetLength + Cipher.TagLength)))
                    throw new SshWireException("SSH packet MAC verification failed!");
            }

            var plaintext = new Byte[packetLength];

            if (!Cipher.Decrypt(lengthBytes, cipherText, plaintext))
                throw new SshWireException("SSH packet authentication failed (bad AEAD tag)!");

            var paddingLength = plaintext[0];
            var payloadLength = (Int32) packetLength - paddingLength - 1;

            if (paddingLength < 4 || payloadLength < 0)
                throw new SshWireException($"Illegal SSH padding_length {paddingLength} for packet_length {packetLength}!");

            return plaintext[1..(1 + payloadLength)];

        }

        #endregion


        #region (private) ReadExactAsync(Input, Count, CancellationToken)

        private static async ValueTask<Byte[]> ReadExactAsync(PipeReader         Input,
                                                              Int32              Count,
                                                              CancellationToken  CancellationToken)
        {

            while (true)
            {

                var result  = await Input.ReadAsync(CancellationToken).ConfigureAwait(false);
                var buffer  = result.Buffer;

                if (buffer.Length >= Count)
                {
                    var slice  = buffer.Slice(0, Count);
                    var bytes  = slice.ToArray();
                    Input.AdvanceTo(buffer.GetPosition(Count));
                    return bytes;
                }

                Input.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                    throw new SshWireException("The connection was closed in the middle of an SSH packet!");

            }

        }

        #endregion

    }

}
