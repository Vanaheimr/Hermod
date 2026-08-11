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

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// The cipher for one direction of the SSH binary packet protocol (RFC 4253, section 6). It turns
    /// the plaintext block <c>padding_length || payload || padding</c> into the on-the-wire ciphertext
    /// (plus an authentication tag for AEAD ciphers), and back.
    /// </summary>
    /// <remarks>
    /// Directional: a connection uses one instance for sending and one for receiving. Instances hold
    /// mutable state (e.g. the AES-GCM invocation counter), so they are not thread-safe and are used
    /// by a single reader or writer loop.
    /// </remarks>
    public abstract class SshTransportCipher : IDisposable
    {

        /// <summary>The block size to which the padded region must be aligned (at least 8).</summary>
        public abstract Int32    BlockSize                          { get; }

        /// <summary>The length of the authentication tag / MAC appended to each packet (0 if none).</summary>
        public abstract Int32    TagLength                          { get; }

        /// <summary>
        /// Whether the 4-byte packet_length field counts towards the padding alignment. True for
        /// unencrypted / CTR modes; false for AEAD modes (RFC 5647) where the length is not encrypted.
        /// </summary>
        public abstract Boolean  LengthIncludedInPaddingAlignment   { get; }


        /// <summary>
        /// Encrypt one packet's plaintext (padding_length || payload || padding) into <paramref name="Output"/>,
        /// whose length must be <c>Plaintext.Length + TagLength</c>.
        /// </summary>
        /// <param name="LengthBytes">The 4 big-endian packet_length bytes (associated data for AEAD).</param>
        /// <param name="Plaintext">The block to encrypt.</param>
        /// <param name="Output">The destination for the ciphertext followed by the tag.</param>
        public abstract void Encrypt(ReadOnlySpan<Byte>  LengthBytes,
                                     ReadOnlySpan<Byte>  Plaintext,
                                     Span<Byte>          Output);

        /// <summary>
        /// Decrypt and authenticate one packet. <paramref name="Input"/> is the ciphertext followed by
        /// the tag (length <c>Plaintext.Length + TagLength</c>); the plaintext is written to
        /// <paramref name="Plaintext"/>.
        /// </summary>
        /// <param name="LengthBytes">The 4 big-endian packet_length bytes (associated data for AEAD).</param>
        /// <param name="Input">The ciphertext followed by the tag.</param>
        /// <param name="Plaintext">The destination for the decrypted block.</param>
        /// <returns>True on success; false if authentication failed.</returns>
        public abstract Boolean Decrypt(ReadOnlySpan<Byte>  LengthBytes,
                                        ReadOnlySpan<Byte>  Input,
                                        Span<Byte>          Plaintext);


        /// <summary>Release any key material held by this cipher.</summary>
        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
        }

    }


    /// <summary>
    /// The identity cipher used before the first key exchange completes: no encryption, no MAC,
    /// 8-byte padding alignment (cipher name "none", RFC 4253).
    /// </summary>
    public sealed class NullTransportCipher : SshTransportCipher
    {

        /// <summary>A shared, stateless instance.</summary>
        public static NullTransportCipher Instance { get; } = new ();

        public override Int32    BlockSize                          => 8;
        public override Int32    TagLength                          => 0;
        public override Boolean  LengthIncludedInPaddingAlignment   => true;

        public override void Encrypt(ReadOnlySpan<Byte>  LengthBytes,
                                     ReadOnlySpan<Byte>  Plaintext,
                                     Span<Byte>          Output)

            => Plaintext.CopyTo(Output);

        public override Boolean Decrypt(ReadOnlySpan<Byte>  LengthBytes,
                                        ReadOnlySpan<Byte>  Input,
                                        Span<Byte>          Plaintext)
        {
            Input.CopyTo(Plaintext);
            return true;
        }

    }

}
