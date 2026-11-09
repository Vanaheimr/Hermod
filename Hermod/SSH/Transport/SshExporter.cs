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
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// The keying material exporter of an SSH connection: keys for a protocol that runs inside the
    /// connection, bound to its key exchange as the TLS exporter of RFC 8446, section 7.5 binds them
    /// to a TLS connection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SSH defines none. This is the one of section 4.3 of the white paper "NTS Key Establishment
    /// over SSH" of Open Charging Technology, built the way RFC 4253, section 7.2 derives the keys of
    /// the connection itself, with a label in place of the letters "A" to "F":
    /// </para>
    /// <code>
    /// exporter_secret          = HASH(K || H || string("ssh-exporter@open.charging.cloud") || session_id)
    /// Export(label, context, L) = HKDF-Expand(exporter_secret, string(label) || string(context), L)
    /// </code>
    /// <para>
    /// K and H as the key exchange method encodes them for its own key derivation - an mpint for the
    /// classical methods, a string for the hybrid post-quantum ones - and HASH, like the HMAC of
    /// HKDF-Expand (RFC 5869), is the hash function of the key exchange method.
    /// </para>
    /// <para>
    /// It rests on K, which only the two ends hold. The session id alone would not do: it is signed in
    /// public key authentication and handed to agents, so it is no secret.
    /// </para>
    /// <para>
    /// The first key exchange fixes it for the life of the connection. A rekey changes the keys of
    /// the connection, not this, as KeyUpdate leaves the exporter of TLS 1.3 alone. So one label and
    /// one context give the same keys every time on one connection, and a protocol that needs fresh
    /// keys for every run uses the exporter once per connection.
    /// </para>
    /// </remarks>
    public sealed class SshExporter : IDisposable
    {

        #region Data

        /// <summary>
        /// What separates the exporter from the keys of the connection in the hash: a name in the
        /// namespace of the domain that defines it, where RFC 4253 has a single letter.
        /// </summary>
        public const String SecretLabel = "ssh-exporter@open.charging.cloud";

        private readonly Byte[]  secret;
        private          Boolean disposed;

        #endregion

        #region Properties

        /// <summary>
        /// The hash function of the key exchange method, which HKDF-Expand takes its HMAC from.
        /// </summary>
        public HashAlgorithmName  HashAlgorithm    { get; }

        #endregion

        #region Constructor(s)

        private SshExporter(HashAlgorithmName  HashAlgorithm,
                            Byte[]             ExporterSecret)
        {
            this.HashAlgorithm  = HashAlgorithm;
            this.secret         = ExporterSecret;
        }

        #endregion


        #region (static) Create        (HashAlgorithm, SharedSecret, ExchangeHash, SessionId)

        /// <summary>
        /// The exporter of a connection, from the values of its first key exchange.
        /// </summary>
        /// <param name="HashAlgorithm">The hash function of the key exchange method.</param>
        /// <param name="SharedSecret">The shared secret K, encoded as the key exchange method encodes it.</param>
        /// <param name="ExchangeHash">The exchange hash H.</param>
        /// <param name="SessionId">The session id, which is H of the first key exchange.</param>
        public static SshExporter Create(HashAlgorithmName   HashAlgorithm,
                                         ReadOnlySpan<Byte>  SharedSecret,
                                         ReadOnlySpan<Byte>  ExchangeHash,
                                         ReadOnlySpan<Byte>  SessionId)

            => new (HashAlgorithm,
                    ComputeSecret(HashAlgorithm, SharedSecret, ExchangeHash, SessionId));

        #endregion

        #region (static) ComputeSecret (HashAlgorithm, SharedSecret, ExchangeHash, SessionId)

        /// <summary>
        /// exporter_secret = HASH(K || H || string("ssh-exporter@open.charging.cloud") || session_id).
        /// </summary>
        /// <remarks>
        /// Public for test vectors, and for a server that hands the exchange to another process along
        /// with this secret of the connection. A connection never gives its own away.
        /// </remarks>
        /// <param name="HashAlgorithm">The hash function of the key exchange method.</param>
        /// <param name="SharedSecret">The shared secret K, encoded as the key exchange method encodes it.</param>
        /// <param name="ExchangeHash">The exchange hash H.</param>
        /// <param name="SessionId">The session id, which is H of the first key exchange.</param>
        public static Byte[] ComputeSecret(HashAlgorithmName   HashAlgorithm,
                                           ReadOnlySpan<Byte>  SharedSecret,
                                           ReadOnlySpan<Byte>  ExchangeHash,
                                           ReadOnlySpan<Byte>  SessionId)
        {

            using var hash = IncrementalHash.CreateHash(HashAlgorithm);

            hash.AppendData(SharedSecret);
            hash.AppendData(ExchangeHash);
            hash.AppendData(SshString(Encoding.ASCII.GetBytes(SecretLabel)));
            hash.AppendData(SessionId);

            return hash.GetHashAndReset();

        }

        #endregion

        #region (static) Expand        (HashAlgorithm, ExporterSecret, Label, Context, Length)

        /// <summary>
        /// HKDF-Expand(PRK = exporter_secret, info = string(label) || string(context), L).
        /// </summary>
        /// <param name="HashAlgorithm">The hash function of the key exchange method.</param>
        /// <param name="ExporterSecret">The exporter_secret, as long as a hash of that function.</param>
        /// <param name="Label">What the keys are for, e.g. "EXPORTER-network-time-security".</param>
        /// <param name="Context">What sets these keys apart from others of the same label.</param>
        /// <param name="Length">How many octets to derive, at most 255 hashes.</param>
        public static Byte[] Expand(HashAlgorithmName   HashAlgorithm,
                                    ReadOnlySpan<Byte>  ExporterSecret,
                                    String              Label,
                                    ReadOnlySpan<Byte>  Context,
                                    Int32               Length)
        {

            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Length);

            var output = new Byte[Length];

            HKDF.Expand(HashAlgorithm,
                        ExporterSecret,
                        output,
                        Info(Label, Context));

            return output;

        }

        #endregion

        #region (static) Info          (Label, Context)

        /// <summary>
        /// The info of HKDF-Expand: string(label) || string(context), each with its length before it,
        /// so that no other label and context run together into the same octets.
        /// </summary>
        /// <param name="Label">What the keys are for.</param>
        /// <param name="Context">What sets these keys apart from others of the same label.</param>
        public static Byte[] Info(String              Label,
                                  ReadOnlySpan<Byte>  Context)

            => [ .. SshString(Encoding.UTF8.GetBytes(Label)),
                 .. SshString(Context) ];

        #endregion


        #region Export(Label, Context, Length)

        /// <summary>
        /// Keys for a protocol in this connection: the same on both ends, and on no other connection.
        /// </summary>
        /// <param name="Label">What the keys are for, e.g. "EXPORTER-network-time-security".</param>
        /// <param name="Context">What sets these keys apart from others of the same label.</param>
        /// <param name="Length">How many octets to derive, at most 255 hashes.</param>
        /// <exception cref="ObjectDisposedException">The connection has ended, and its exporter with it.</exception>
        public Byte[] Export(String              Label,
                             ReadOnlySpan<Byte>  Context,
                             Int32               Length)
        {

            // Not one key from a secret half wiped: Dispose waits for an export that has begun.
            lock (secret)
            {

                ObjectDisposedException.ThrowIf(disposed, this);

                return Expand(HashAlgorithm, secret, Label, Context, Length);

            }

        }

        #endregion


        #region (private static) SshString(Value)

        private static Byte[] SshString(ReadOnlySpan<Byte> Value)
        {

            var encoded = new Byte[4 + Value.Length];

            BinaryPrimitives.WriteUInt32BigEndian(encoded, (UInt32) Value.Length);
            Value.CopyTo(encoded.AsSpan(4));

            return encoded;

        }

        #endregion


        #region Dispose()

        /// <summary>
        /// Wipe the secret: the connection is over, and so is every export from it.
        /// </summary>
        public void Dispose()
        {
            lock (secret)
            {
                disposed = true;
                CryptographicOperations.ZeroMemory(secret);
            }
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// The hash function, and never the secret.
        /// </summary>
        public override String ToString()

            => $"SSH exporter ({HashAlgorithm.Name})";

        #endregion

    }

}
