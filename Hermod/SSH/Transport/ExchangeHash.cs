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
using System.Numerics;
using System.Security.Cryptography;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// Computes the key-exchange hash H and the shared-secret mpint encoding for the
    /// ECDH-style key exchanges (RFC 5656 / RFC 8731, curve25519-sha256).
    /// </summary>
    public static class ExchangeHash
    {

        #region EncodeSharedSecretMPInt(RawSharedSecret)

        /// <summary>
        /// Encode the raw X25519 shared secret as an SSH mpint (unsigned big-endian integer). The same
        /// bytes are used both inside H and as the "K" prefix of the key derivation (RFC 4253 §7.2).
        /// </summary>
        /// <param name="RawSharedSecret">The 32-byte X25519 output.</param>
        public static Byte[] EncodeSharedSecretMPInt(ReadOnlySpan<Byte> RawSharedSecret)
        {

            var value   = new BigInteger(RawSharedSecret, isUnsigned: true, isBigEndian: true);

            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);
            writer.WriteMPInt(value);

            return abw.WrittenSpan.ToArray();

        }

        #endregion

        #region Compute(HashAlgorithm, V_C, V_S, I_C, I_S, K_S, Q_C, Q_S, SharedSecretMPInt)

        /// <summary>
        /// Compute H = HASH(V_C || V_S || I_C || I_S || K_S || Q_C || Q_S || K), where every field
        /// except K is written as an SSH string and K is the pre-encoded shared-secret mpint. HASH is
        /// the key exchange's hash (SHA-256 for curve25519/nistp256, SHA-384 for nistp384, SHA-512 for
        /// nistp521) — RFC 8731, RFC 5656 §4.
        /// </summary>
        /// <param name="HashAlgorithm">The key exchange's hash algorithm.</param>
        /// <param name="V_C">The client identification string (no CR LF).</param>
        /// <param name="V_S">The server identification string (no CR LF).</param>
        /// <param name="I_C">The client's KEXINIT payload.</param>
        /// <param name="I_S">The server's KEXINIT payload.</param>
        /// <param name="K_S">The server's host-key blob.</param>
        /// <param name="Q_C">The client's ephemeral public key.</param>
        /// <param name="Q_S">The server's ephemeral public key.</param>
        /// <param name="SharedSecretMPInt">The shared secret K, already mpint-encoded.</param>
        public static Byte[] Compute(HashAlgorithmName   HashAlgorithm,
                                     ReadOnlySpan<Byte>  V_C,
                                     ReadOnlySpan<Byte>  V_S,
                                     ReadOnlySpan<Byte>  I_C,
                                     ReadOnlySpan<Byte>  I_S,
                                     ReadOnlySpan<Byte>  K_S,
                                     ReadOnlySpan<Byte>  Q_C,
                                     ReadOnlySpan<Byte>  Q_S,
                                     ReadOnlySpan<Byte>  SharedSecretMPInt)
        {

            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);

            writer.WriteBinaryString(V_C);
            writer.WriteBinaryString(V_S);
            writer.WriteBinaryString(I_C);
            writer.WriteBinaryString(I_S);
            writer.WriteBinaryString(K_S);
            writer.WriteBinaryString(Q_C);
            writer.WriteBinaryString(Q_S);

            // K is already the mpint encoding (length prefix included) — append it verbatim.
            var tail = abw.GetSpan(SharedSecretMPInt.Length);
            SharedSecretMPInt.CopyTo(tail);
            abw.Advance(SharedSecretMPInt.Length);

            using var hash = IncrementalHash.CreateHash(HashAlgorithm);
            hash.AppendData(abw.WrittenSpan);
            return hash.GetHashAndReset();

        }

        #endregion

    }

}
