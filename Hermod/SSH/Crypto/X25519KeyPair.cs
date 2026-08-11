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

using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Parameters;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// An ephemeral X25519 (Curve25519) key pair for the <c>curve25519-sha256</c> key exchange
    /// (RFC 7748, RFC 8731). The BouncyCastle primitives arrive via the Hermod/Styx submodules.
    /// </summary>
    public sealed class X25519KeyPair
    {

        #region Data

        private readonly X25519PrivateKeyParameters  privateKey;

        /// <summary>The length in bytes of an X25519 public key or shared secret (32).</summary>
        public const     Int32                       KeySize = 32;

        #endregion

        #region Properties

        /// <summary>The 32-byte public key (Q) to send to the peer.</summary>
        public Byte[] PublicKey { get; }

        #endregion

        #region Constructor(s)

        private X25519KeyPair(X25519PrivateKeyParameters PrivateKey)
        {
            this.privateKey  = PrivateKey;
            this.PublicKey   = PrivateKey.GeneratePublicKey().GetEncoded();
        }

        #endregion


        #region (static) Generate(Random = null)

        /// <summary>
        /// Generate a fresh, random ephemeral X25519 key pair.
        /// </summary>
        /// <param name="Random">An optional secure random source; a fresh one is used by default.</param>
        public static X25519KeyPair Generate(SecureRandom? Random = null)
            => new (new X25519PrivateKeyParameters(Random ?? new SecureRandom()));

        #endregion

        #region (static) FromPrivateKey(PrivateKey)

        /// <summary>
        /// Reconstruct an X25519 key pair from a 32-byte private scalar (mainly for test vectors).
        /// </summary>
        /// <param name="PrivateKey">The 32-byte private scalar.</param>
        public static X25519KeyPair FromPrivateKey(ReadOnlySpan<Byte> PrivateKey)
        {

            if (PrivateKey.Length != KeySize)
                throw new ArgumentException($"An X25519 private key must be {KeySize} bytes!", nameof(PrivateKey));

            return new (new X25519PrivateKeyParameters(PrivateKey.ToArray(), 0));

        }

        #endregion


        #region Agree(PeerPublicKey)

        /// <summary>
        /// Compute the 32-byte shared secret from this private key and the peer's public key.
        /// </summary>
        /// <param name="PeerPublicKey">The peer's 32-byte X25519 public key.</param>
        public Byte[] Agree(ReadOnlySpan<Byte> PeerPublicKey)
        {

            if (PeerPublicKey.Length != KeySize)
                throw new ArgumentException($"An X25519 public key must be {KeySize} bytes!", nameof(PeerPublicKey));

            var agreement = new X25519Agreement();
            agreement.Init(privateKey);

            var secret = new Byte[agreement.AgreementSize];
            agreement.CalculateAgreement(new X25519PublicKeyParameters(PeerPublicKey.ToArray(), 0), secret, 0);

            return secret;

        }

        #endregion

    }

}
