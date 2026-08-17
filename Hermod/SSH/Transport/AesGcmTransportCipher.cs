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
using System.Security.Cryptography;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// The <c>aes256-gcm@openssh.com</c> / <c>aes128-gcm@openssh.com</c> AEAD cipher (RFC 5647 with
    /// OpenSSH semantics): the 4-byte packet_length is sent unencrypted and used as associated data;
    /// <c>padding_length || payload || padding</c> is encrypted with AES-GCM and followed by a 16-byte tag.
    /// </summary>
    /// <remarks>
    /// The 12-byte nonce is <c>fixed(4) || invocation-counter(8)</c>. It is seeded from the KDF-derived
    /// IV and the 8-byte counter is incremented (big-endian) after every packet, exactly as OpenSSH does.
    /// </remarks>
    public sealed class AesGcmTransportCipher : SshTransportCipher
    {

        #region Data

        /// <summary>
        /// The AES-GCM authentication tag length in bytes.
        /// </summary>
        public  const Int32  GcmTagLength   = 16;

        /// <summary>
        /// The AES-GCM nonce length in bytes.
        /// </summary>
        public  const Int32  NonceLength    = 12;

        private readonly AesGcm       aesGcm;
        private readonly Byte[]       nonce;

        #endregion

        #region Properties

        public override Int32    BlockSize                          => 16;
        public override Int32    TagLength                          => GcmTagLength;
        public override Boolean  LengthIncludedInPaddingAlignment   => false;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create an AES-GCM cipher for one direction.
        /// </summary>
        /// <param name="Key">The 16- or 32-byte AES key (from the KDF).</param>
        /// <param name="InitialNonce">The 12-byte initial IV (from the KDF).</param>
        public AesGcmTransportCipher(ReadOnlySpan<Byte>  Key,
                                     ReadOnlySpan<Byte>  InitialNonce)
        {

            if (Key.Length is not 16 and not 32)
                throw new ArgumentException("An AES-GCM key must be 16 or 32 bytes!", nameof(Key));

            if (InitialNonce.Length != NonceLength)
                throw new ArgumentException($"An AES-GCM nonce must be {NonceLength} bytes!", nameof(InitialNonce));

            this.aesGcm  = new AesGcm(Key, GcmTagLength);
            this.nonce   = InitialNonce.ToArray();

        }

        #endregion


        #region Encrypt(LengthBytes, Plaintext, Output)

        public override void Encrypt(ReadOnlySpan<Byte>  LengthBytes,
                                     ReadOnlySpan<Byte>  Plaintext,
                                     Span<Byte>          Output)
        {

            var ciphertext  = Output[..Plaintext.Length];
            var tag         = Output.Slice(Plaintext.Length, GcmTagLength);

            aesGcm.Encrypt(nonce, Plaintext, ciphertext, tag, LengthBytes);

            IncrementCounter();

        }

        #endregion

        #region Decrypt(LengthBytes, Input, Plaintext)

        public override Boolean Decrypt(ReadOnlySpan<Byte>  LengthBytes,
                                        ReadOnlySpan<Byte>  Input,
                                        Span<Byte>          Plaintext)
        {

            var ciphertext  = Input[..Plaintext.Length];
            var tag         = Input.Slice(Plaintext.Length, GcmTagLength);

            try
            {
                aesGcm.Decrypt(nonce, ciphertext, tag, Plaintext, LengthBytes);
            }
            catch (AuthenticationTagMismatchException)
            {
                return false;
            }

            IncrementCounter();
            return true;

        }

        #endregion

        #region (private) IncrementCounter()

        // Increment the 8-byte invocation counter (nonce[4..12]) as a big-endian integer.
        private void IncrementCounter()
        {
            var counter = BinaryPrimitives.ReadUInt64BigEndian(nonce.AsSpan(4, 8));
            BinaryPrimitives.WriteUInt64BigEndian(nonce.AsSpan(4, 8), counter + 1);
        }

        #endregion

        #region Dispose()

        public override void Dispose()
        {
            aesGcm.Dispose();
            CryptographicOperations.ZeroMemory(nonce);
            base.Dispose();
        }

        #endregion

    }

}
