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
    /// An RSA key for <c>rsa-pss-sha512</c> (RFC 9421, Section 3.3.1) or for
    /// <c>rsa-v1_5-sha256</c> (Section 3.3.2).
    /// </summary>
    /// <remarks>
    /// The BCL's PSS uses MGF1 with the message hash and a salt as long as the
    /// hash, which for SHA-512 is the 64 octets Section 3.3.1 requires.
    /// </remarks>
    public sealed class HTTPSignatureRSAKey : HTTPSignatureKey
    {

        #region Data

        private readonly RSA                   key;
        private readonly HashAlgorithmName     hash;
        private readonly RSASignaturePadding   padding;

        #endregion

        #region Properties

        /// <inheritdoc/>
        public override Boolean CanSign
        {
            get
            {
                try
                {
                    return key.ExportParameters(includePrivateParameters: true).D is not null;
                }
                catch (CryptographicException)
                {
                    return false;
                }
            }
        }

        #endregion

        #region Constructor(s)

        internal HTTPSignatureRSAKey(String   Algorithm,
                                     RSA      Key,
                                     String?  KeyId)

            : base(Algorithm, KeyId)

        {

            this.key = Key;

            (hash, padding) = Algorithm switch {
                HTTPSignatureAlgorithms.RSAPSSSHA512  => (HashAlgorithmName.SHA512, RSASignaturePadding.Pss),
                HTTPSignatureAlgorithms.RSAV15SHA256  => (HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
                _                                     => throw new ArgumentException($"'{Algorithm}' is not an RSA algorithm!", nameof(Algorithm))
            };

        }

        #endregion


        /// <inheritdoc/>
        public override Byte[] Sign(Byte[] SignatureBase)

            => key.SignData(SignatureBase, hash, padding);

        /// <inheritdoc/>
        public override Boolean Verify(Byte[] SignatureBase, Byte[] Signature)
        {
            try
            {
                return key.VerifyData(SignatureBase, Signature, hash, padding);
            }
            catch (CryptographicException)
            {
                return false;
            }
        }

    }

}
