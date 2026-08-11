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
using System.Buffers.Binary;
using System.Security.Cryptography;

using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// The <c>chacha20-poly1305@openssh.com</c> AEAD cipher (OpenSSH <c>PROTOCOL.chacha20poly1305</c>).
    /// Unlike RFC 8439, this is OpenSSH's own construction: 64 bytes of key material split into two
    /// 32-byte ChaCha20 keys — <c>K_main</c> (payload + Poly1305 key) and <c>K_header</c> (the 4-byte
    /// packet length). The nonce is the packet sequence number; the length field is encrypted separately
    /// (so it can be decrypted before the rest) and both the encrypted length and payload are
    /// Poly1305-authenticated. The BouncyCastle primitives arrive via the Hermod/Styx submodules.
    /// </summary>
    /// <remarks>
    /// The binary packet framing special-cases this cipher (via <see cref="EncryptPacketInto"/>,
    /// <see cref="DecryptLength"/> and <see cref="DecryptAndVerify"/>) because the length field is
    /// encrypted, unlike the AEAD/CTR ciphers where it is plaintext.
    /// </remarks>
    public sealed class ChaCha20Poly1305Cipher : SshTransportCipher
    {

        #region Data

        /// <summary>The required key length in bytes (two 32-byte ChaCha20 keys).</summary>
        public const Int32  KeyLength   = 64;

        /// <summary>The Poly1305 tag length in bytes.</summary>
        public const Int32  TagLen      = 16;

        private readonly Byte[]  mainKey;     // key[0..32]  — payload + Poly1305 key
        private readonly Byte[]  headerKey;   // key[32..64] — the 4-byte packet length

        #endregion

        #region Properties

        public override Int32    BlockSize                          => 8;
        public override Int32    TagLength                          => TagLen;
        public override Boolean  LengthIncludedInPaddingAlignment   => false;

        #endregion

        #region Constructor(s)

        /// <summary>Create a ChaCha20-Poly1305 cipher for one direction from 64 bytes of key material.</summary>
        public ChaCha20Poly1305Cipher(ReadOnlySpan<Byte> Key)
        {

            if (Key.Length != KeyLength)
                throw new ArgumentException($"A chacha20-poly1305@openssh.com key must be {KeyLength} bytes!", nameof(Key));

            this.mainKey    = Key[..32].ToArray();
            this.headerKey  = Key[32..].ToArray();

        }

        #endregion


        #region (private) Nonce(SequenceNumber) / ChaCha(...)

        // OpenSSH's construction: the nonce is the packet sequence number, the Poly1305 one-time key is
        // the first 32 bytes of keystream block 0, and the payload is encrypted from block 1 onwards.
        private static void DerivePoly1305Key(ReadOnlySpan<Byte> Key, ReadOnlySpan<Byte> Nonce, Span<Byte> PolyKey)
        {
            Span<Byte> block = stackalloc Byte[ChaCha20.BlockSize];
            ChaCha20.Keystream(Key, Nonce, 0, block);
            block[..32].CopyTo(PolyKey);
            CryptographicOperations.ZeroMemory(block);
        }

        private static void Poly1305Tag(ReadOnlySpan<Byte>  PolyKey,
                                        ReadOnlySpan<Byte>  EncryptedLength,
                                        ReadOnlySpan<Byte>  Ciphertext,
                                        Span<Byte>          Tag)
        {
            var poly = new Poly1305();
            poly.Init(new KeyParameter(PolyKey.ToArray()));
            poly.BlockUpdate(EncryptedLength);
            poly.BlockUpdate(Ciphertext);
            poly.DoFinal(Tag);
        }

        #endregion


        #region EncryptPacketInto(SequenceNumber, Payload, Output)

        /// <summary>
        /// Frame and encrypt one packet: writes the encrypted length (4), the encrypted
        /// <c>padding_length || payload || padding</c>, and the 16-byte Poly1305 tag.
        /// </summary>
        public void EncryptPacketInto(UInt32 SequenceNumber, ReadOnlySpan<Byte> Payload, IBufferWriter<Byte> Output)
        {

            // Padding: align (padding_length || payload || padding) to the 8-byte block, at least 4 bytes.
            var paddingLength  = 8 - ((1 + Payload.Length) % 8);
            if (paddingLength < 4)
                paddingLength += 8;
            var packetLength   = 1 + Payload.Length + paddingLength;

            Span<Byte> nonce = stackalloc Byte[ChaCha20.NonceSize];
            BinaryPrimitives.WriteUInt64BigEndian(nonce, SequenceNumber);

            var plaintext = ArrayPool<Byte>.Shared.Rent(packetLength);
            try
            {

                plaintext[0] = (Byte) paddingLength;
                Payload.CopyTo(plaintext.AsSpan(1));
                RandomNumberGenerator.Fill(plaintext.AsSpan(1 + Payload.Length, paddingLength));

                var output = Output.GetSpan(4 + packetLength + TagLen);

                // Encrypted length (header key), written straight into the output.
                Span<Byte> lengthBytes = stackalloc Byte[4];
                BinaryPrimitives.WriteUInt32BigEndian(lengthBytes, (UInt32) packetLength);
                ChaCha20.Xor(headerKey, nonce, 0, lengthBytes, output[..4]);

                // Poly1305 key + encrypted payload (main key, counter 0 then 1). The ciphertext goes
                // directly into the output buffer — a separate array here would be a full copy of every
                // packet, which is what made this cipher allocate ~5x the payload per record.
                Span<Byte> polyKey = stackalloc Byte[32];
                DerivePoly1305Key(mainKey, nonce, polyKey);
                ChaCha20.Xor(mainKey, nonce, 1, plaintext.AsSpan(0, packetLength), output.Slice(4, packetLength));

                // Poly1305 tag over the encrypted length and the ciphertext, also written in place.
                Poly1305Tag(polyKey, output[..4], output.Slice(4, packetLength), output.Slice(4 + packetLength, TagLen));
                CryptographicOperations.ZeroMemory(polyKey);

                Output.Advance(4 + packetLength + TagLen);

            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext.AsSpan(0, packetLength));
                ArrayPool<Byte>.Shared.Return(plaintext);
            }

        }

        #endregion

        #region DecryptLength(SequenceNumber, EncryptedLength)

        /// <summary>Decrypt the 4-byte packet length field (header key) so the caller can read the rest.</summary>
        public UInt32 DecryptLength(UInt32 SequenceNumber, ReadOnlySpan<Byte> EncryptedLength)
        {
            Span<Byte> nonce = stackalloc Byte[ChaCha20.NonceSize];
            BinaryPrimitives.WriteUInt64BigEndian(nonce, SequenceNumber);

            Span<Byte> lengthBytes = stackalloc Byte[4];
            ChaCha20.Xor(headerKey, nonce, 0, EncryptedLength[..4], lengthBytes);
            return BinaryPrimitives.ReadUInt32BigEndian(lengthBytes);
        }

        #endregion

        #region DecryptAndVerify(SequenceNumber, EncryptedLength, CiphertextAndTag)

        /// <summary>
        /// Verify the Poly1305 tag and decrypt the payload block, returning
        /// <c>padding_length || payload || padding</c> (the caller strips the padding).
        /// </summary>
        public Byte[] DecryptAndVerify(UInt32 SequenceNumber, ReadOnlySpan<Byte> EncryptedLength, ReadOnlySpan<Byte> CiphertextAndTag)
        {

            var packetLength  = CiphertextAndTag.Length - TagLen;
            var ciphertext    = CiphertextAndTag[..packetLength];
            var receivedTag   = CiphertextAndTag[packetLength..];

            Span<Byte> nonce = stackalloc Byte[ChaCha20.NonceSize];
            BinaryPrimitives.WriteUInt64BigEndian(nonce, SequenceNumber);

            Span<Byte> polyKey = stackalloc Byte[32];
            DerivePoly1305Key(mainKey, nonce, polyKey);

            // Authenticate before decrypting — and before allocating anything for the plaintext.
            Span<Byte> expectedTag = stackalloc Byte[TagLen];
            Poly1305Tag(polyKey, EncryptedLength, ciphertext, expectedTag);
            CryptographicOperations.ZeroMemory(polyKey);

            if (!CryptographicOperations.FixedTimeEquals(expectedTag, receivedTag))
                throw new SshWireException("SSH chacha20-poly1305 authentication failed (bad Poly1305 tag)!");

            // Decrypt straight into the result; the old ToArray() copied every packet first.
            var plaintext = new Byte[packetLength];
            ChaCha20.Xor(mainKey, nonce, 1, ciphertext, plaintext);
            return plaintext;

        }

        #endregion


        #region (unused) Encrypt / Decrypt

        // This cipher is handled directly by the packet framing (the length field is encrypted, so the
        // generic length-plaintext path does not apply); these members are never called.
        public override void Encrypt(ReadOnlySpan<Byte> LengthBytes, ReadOnlySpan<Byte> Plaintext, Span<Byte> Output)
            => throw new NotSupportedException("chacha20-poly1305 is framed directly by SshPacketFraming.");

        public override Boolean Decrypt(ReadOnlySpan<Byte> LengthBytes, ReadOnlySpan<Byte> Input, Span<Byte> Plaintext)
            => throw new NotSupportedException("chacha20-poly1305 is framed directly by SshPacketFraming.");

        #endregion

    }

}
