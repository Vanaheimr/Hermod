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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Crypto;


    /// <summary>
    /// An Ed25519 key for <c>ed25519</c> (RFC 9421, Section 3.3.6): PureEdDSA
    /// over the signature base itself, with no prehash.
    /// </summary>
    /// <remarks>
    /// The BCL has no Ed25519. The primitive is BouncyCastle's, through the same
    /// <see cref="Ed25519Signature"/> the TLS 1.3 stack uses for its
    /// CertificateVerify.
    /// </remarks>
    public sealed class HTTPSignatureEd25519Key : HTTPSignatureKey
    {

        #region Data

        private readonly Ed25519Signature?  signer;

        #endregion

        #region Properties

        /// <summary>
        /// The public key, 32 octets.
        /// </summary>
        public Byte[] PublicKey { get; }

        /// <inheritdoc/>
        public override Boolean CanSign
            => signer is not null;

        #endregion

        #region Constructor(s)

        private HTTPSignatureEd25519Key(Ed25519Signature?  Signer,
                                        Byte[]             PublicKey,
                                        String?            KeyId)

            : base(HTTPSignatureAlgorithms.Ed25519, KeyId)

        {
            this.signer     = Signer;
            this.PublicKey  = PublicKey;
        }

        /// <summary>
        /// A key that signs, from its 32-octet private key (RFC 8032, Section 5.1.5).
        /// </summary>
        public static HTTPSignatureEd25519Key FromPrivateKey(Byte[]   PrivateKey,
                                                             String?  KeyId = null)
        {

            if (PrivateKey is null || PrivateKey.Length != 32)
                throw new ArgumentException("An Ed25519 private key is 32 octets!", nameof(PrivateKey));

            var signer = new Ed25519Signature(PrivateKey);

            return new (signer, signer.PublicKey, KeyId);

        }

        /// <summary>
        /// A key that verifies, from its 32-octet public key.
        /// </summary>
        public static HTTPSignatureEd25519Key FromPublicKey(Byte[]   PublicKey,
                                                            String?  KeyId = null)
        {

            if (PublicKey is null || PublicKey.Length != 32)
                throw new ArgumentException("An Ed25519 public key is 32 octets!", nameof(PublicKey));

            return new (null, [.. PublicKey], KeyId);

        }

        #endregion


        /// <inheritdoc/>
        public override Byte[] Sign(Byte[] SignatureBase)

            => signer is not null
                   ? signer.Sign(SignatureBase)
                   : throw new InvalidOperationException("This Ed25519 key is a public key and cannot sign!");

        /// <inheritdoc/>
        public override Boolean Verify(Byte[] SignatureBase, Byte[] Signature)
        {

            if (Signature.Length != 64)
                return false;

            try
            {
                return Ed25519Signature.Verify(PublicKey, SignatureBase, Signature);
            }
            catch (Exception)
            {
                return false;
            }

        }

    }

}
