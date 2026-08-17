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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// The SSH key derivation function (RFC 4253, section 7.2): keys are derived from the shared
    /// secret K, the exchange hash H, a single letter A..F and the session id, extended by repeated
    /// hashing until enough bytes are produced.
    /// </summary>
    public static class Kdf
    {

        #region KeyLetter

        /// <summary>
        /// The six key-derivation letters of RFC 4253, section 7.2.
        /// </summary>
        public static class KeyLetter
        {
            /// <summary>
            /// Initial IV, client to server.
            /// </summary>
            public const Byte InitialIVClientToServer         = (Byte) 'A';
            /// <summary>
            /// Initial IV, server to client.
            /// </summary>
            public const Byte InitialIVServerToClient         = (Byte) 'B';
            /// <summary>
            /// Encryption key, client to server.
            /// </summary>
            public const Byte EncryptionKeyClientToServer     = (Byte) 'C';
            /// <summary>
            /// Encryption key, server to client.
            /// </summary>
            public const Byte EncryptionKeyServerToClient     = (Byte) 'D';
            /// <summary>
            /// Integrity (MAC) key, client to server.
            /// </summary>
            public const Byte IntegrityKeyClientToServer      = (Byte) 'E';
            /// <summary>
            /// Integrity (MAC) key, server to client.
            /// </summary>
            public const Byte IntegrityKeyServerToClient      = (Byte) 'F';
        }

        #endregion


        #region Derive(SharedSecretMPInt, ExchangeHash, Letter, SessionId, Length)

        /// <summary>
        /// Derive <paramref name="Length"/> bytes of key material for the given letter, using the key
        /// exchange's hash algorithm.
        /// </summary>
        /// <param name="HashAlgorithm">The key exchange's hash algorithm (SHA-256/384/512).</param>
        /// <param name="SharedSecretMPInt">The shared secret K, mpint-encoded (as in the exchange hash).</param>
        /// <param name="ExchangeHash">The exchange hash H.</param>
        /// <param name="Letter">One of the A..F key-derivation letters.</param>
        /// <param name="SessionId">The session id (H of the first key exchange).</param>
        /// <param name="Length">The number of key bytes required.</param>
        public static Byte[] Derive(HashAlgorithmName   HashAlgorithm,
                                    ReadOnlySpan<Byte>  SharedSecretMPInt,
                                    ReadOnlySpan<Byte>  ExchangeHash,
                                    Byte                Letter,
                                    ReadOnlySpan<Byte>  SessionId,
                                    Int32               Length)
        {

            using var hash = IncrementalHash.CreateHash(HashAlgorithm);

            // K1 = HASH(K || H || letter || session_id)
            hash.AppendData(SharedSecretMPInt);
            hash.AppendData(ExchangeHash);
            hash.AppendData([ Letter ]);
            hash.AppendData(SessionId);

            var blocks    = new List<Byte[]> { hash.GetHashAndReset() };
            var produced  = blocks[0].Length;

            // Kn = HASH(K || H || K1 || ... || K(n-1)) — the FULL previous blocks, not the truncated output.
            while (produced < Length)
            {

                hash.AppendData(SharedSecretMPInt);
                hash.AppendData(ExchangeHash);
                foreach (var previous in blocks)
                    hash.AppendData(previous);

                var next = hash.GetHashAndReset();
                blocks.Add(next);
                produced += next.Length;

            }

            var result  = new Byte[Length];
            var filled   = 0;
            foreach (var block in blocks)
            {
                var take = Math.Min(block.Length, Length - filled);
                block.AsSpan(0, take).CopyTo(result.AsSpan(filled));
                filled += take;
                if (filled == Length)
                    break;
            }

            return result;

        }

        #endregion

    }

}
