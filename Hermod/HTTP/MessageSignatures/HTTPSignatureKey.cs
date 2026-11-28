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

    using System.Security.Cryptography;


    /// <summary>
    /// Key material for HTTP message signatures, bound to the one algorithm it
    /// is used with: HTTP_SIGN and HTTP_VERIFY of RFC 9421, Section 3.3.
    /// </summary>
    /// <remarks>
    /// The algorithm belongs to the key, not to the message. Section 3.2, step 6
    /// has a verifier take it from what it knows, and a message's <c>alg</c>
    /// parameter, where there is one, must agree with that or the verification
    /// fails; Section 7.3.6 is why. So a key here cannot be asked to verify in an
    /// algorithm other than its own.
    /// </remarks>
    public abstract class HTTPSignatureKey
    {

        #region Properties

        /// <summary>
        /// The algorithm, from <see cref="HTTPSignatureAlgorithms"/>.
        /// </summary>
        public String   Algorithm    { get; }

        /// <summary>
        /// The key identifier, for the <c>keyid</c> parameter, if any.
        /// </summary>
        public String?  KeyId        { get; }

        /// <summary>
        /// Whether this key can sign, rather than only verify.
        /// </summary>
        public abstract Boolean CanSign { get; }

        #endregion

        #region Constructor(s)

        protected HTTPSignatureKey(String   Algorithm,
                                   String?  KeyId)
        {
            this.Algorithm  = Algorithm;
            this.KeyId      = KeyId;
        }

        #endregion


        /// <summary>
        /// HTTP_SIGN (M, Ks) -> S
        /// </summary>
        /// <param name="SignatureBase">The signature base, as its ASCII octets.</param>
        public abstract Byte[]  Sign  (Byte[] SignatureBase);

        /// <summary>
        /// HTTP_VERIFY (M, Kv, S) -> V. Never throws for a signature that is
        /// merely wrong, of the wrong length included.
        /// </summary>
        /// <param name="SignatureBase">The signature base, as its ASCII octets.</param>
        /// <param name="Signature">The signature to check.</param>
        public abstract Boolean Verify(Byte[] SignatureBase, Byte[] Signature);


        #region Factories

        /// <summary>An RSA key for <c>rsa-pss-sha512</c> (Section 3.3.1).</summary>
        public static HTTPSignatureKey RSAPSS     (RSA     Key, String? KeyId = null) => new HTTPSignatureRSAKey  (HTTPSignatureAlgorithms.RSAPSSSHA512, Key, KeyId);

        /// <summary>An RSA key for <c>rsa-v1_5-sha256</c> (Section 3.3.2).</summary>
        public static HTTPSignatureKey RSAPKCS1v15(RSA     Key, String? KeyId = null) => new HTTPSignatureRSAKey  (HTTPSignatureAlgorithms.RSAV15SHA256, Key, KeyId);

        /// <summary>A shared secret for <c>hmac-sha256</c> (Section 3.3.3).</summary>
        public static HTTPSignatureKey HMACSHA256 (Byte[]  Secret, String? KeyId = null) => new HTTPSignatureHMACKey (Secret, KeyId);

        /// <summary>A P-256 or P-384 key for <c>ecdsa-p256-sha256</c> or <c>ecdsa-p384-sha384</c>, by its curve (Sections 3.3.4 and 3.3.5).</summary>
        public static HTTPSignatureKey ECDSA      (ECDsa   Key, String? KeyId = null) => new HTTPSignatureECDSAKey(Key, KeyId);

        /// <summary>An Ed25519 private key, its 32-octet seed, for <c>ed25519</c> (Section 3.3.6).</summary>
        public static HTTPSignatureKey Ed25519    (Byte[]  PrivateKey, String? KeyId = null) => HTTPSignatureEd25519Key.FromPrivateKey(PrivateKey, KeyId);

        /// <summary>An Ed25519 public key, 32 octets, which verifies only.</summary>
        public static HTTPSignatureKey Ed25519PublicKey(Byte[] PublicKey, String? KeyId = null) => HTTPSignatureEd25519Key.FromPublicKey(PublicKey, KeyId);

        #endregion

    }

}
