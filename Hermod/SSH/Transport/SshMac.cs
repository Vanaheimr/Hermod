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
    /// A message authentication code for the SSH binary packet protocol, in the encrypt-then-MAC
    /// arrangement of the OpenSSH <c>*-etm@openssh.com</c> MACs: the MAC is computed over
    /// <c>sequence_number || packet_length || encrypted_payload</c>.
    /// </summary>
    public interface ISshMac : IDisposable
    {

        /// <summary>The MAC length in bytes appended to each packet.</summary>
        Int32 Length { get; }

        /// <summary>
        /// Compute the MAC over the sequence number, the (cleartext) packet_length bytes and the
        /// encrypted payload, writing <see cref="Length"/> bytes into <paramref name="Output"/>.
        /// </summary>
        void ComputeInto(UInt32              SequenceNumber,
                         ReadOnlySpan<Byte>  LengthBytes,
                         ReadOnlySpan<Byte>  Ciphertext,
                         Span<Byte>          Output);

    }


    /// <summary>
    /// An HMAC-SHA2-256 / HMAC-SHA2-512 MAC (RFC 6668) in encrypt-then-MAC mode
    /// (<c>hmac-sha2-256-etm@openssh.com</c> / <c>hmac-sha2-512-etm@openssh.com</c>).
    /// </summary>
    public sealed class HmacSha2Mac : ISshMac
    {

        #region Data

        private readonly IncrementalHash hmac;

        #endregion

        #region Properties

        /// <summary>The MAC length in bytes (32 for SHA-256, 64 for SHA-512).</summary>
        public Int32 Length { get; }

        #endregion

        #region Constructor(s)

        private HmacSha2Mac(IncrementalHash Hmac, Int32 Length)
        {
            this.hmac    = Hmac;
            this.Length  = Length;
        }

        #endregion


        /// <summary>Create an <c>hmac-sha2-256</c> MAC from a 32-byte key.</summary>
        public static HmacSha2Mac Sha256(ReadOnlySpan<Byte> Key)
            => new (IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, Key), 32);

        /// <summary>Create an <c>hmac-sha2-512</c> MAC from a 64-byte key.</summary>
        public static HmacSha2Mac Sha512(ReadOnlySpan<Byte> Key)
            => new (IncrementalHash.CreateHMAC(HashAlgorithmName.SHA512, Key), 64);


        #region ComputeInto(SequenceNumber, LengthBytes, Ciphertext, Output)

        public void ComputeInto(UInt32              SequenceNumber,
                                ReadOnlySpan<Byte>  LengthBytes,
                                ReadOnlySpan<Byte>  Ciphertext,
                                Span<Byte>          Output)
        {

            Span<Byte> sequenceBytes = stackalloc Byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(sequenceBytes, SequenceNumber);

            hmac.AppendData(sequenceBytes);
            hmac.AppendData(LengthBytes);
            hmac.AppendData(Ciphertext);

            Span<Byte> full = stackalloc Byte[64];
            hmac.GetHashAndReset(full);
            full[..Length].CopyTo(Output);

        }

        #endregion

        #region Dispose()

        public void Dispose()
            => hmac.Dispose();

        #endregion

    }

}
