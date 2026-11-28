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
    /// An ECDSA key for <c>ecdsa-p256-sha256</c> (RFC 9421, Section 3.3.4) or
    /// <c>ecdsa-p384-sha384</c> (Section 3.3.5), by the curve it is on.
    /// </summary>
    /// <remarks>
    /// The signature is r and s, big-endian and zero-padded to the size of the
    /// curve, concatenated: the IEEE P1363 format, not the DER the BCL can also
    /// write. A DER signature is refused, not tried.
    /// </remarks>
    public sealed class HTTPSignatureECDSAKey : HTTPSignatureKey
    {

        #region Data

        private readonly ECDsa              key;
        private readonly HashAlgorithmName  hash;
        private readonly Int32              signatureLength;

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

        internal HTTPSignatureECDSAKey(ECDsa    Key,
                                       String?  KeyId)

            : base(AlgorithmOf(Key), KeyId)

        {

            this.key = Key;

            (hash, signatureLength) = Algorithm == HTTPSignatureAlgorithms.ECDSAP256SHA256
                                          ? (HashAlgorithmName.SHA256, 64)
                                          : (HashAlgorithmName.SHA384, 96);

        }

        private static String AlgorithmOf(ECDsa Key)

            => Key.KeySize switch {
                   256  => HTTPSignatureAlgorithms.ECDSAP256SHA256,
                   384  => HTTPSignatureAlgorithms.ECDSAP384SHA384,
                   _    => throw new ArgumentException($"An ECDSA key for HTTP message signatures must be on P-256 or P-384, not of {Key.KeySize} bits!", nameof(Key))
               };

        #endregion


        /// <inheritdoc/>
        public override Byte[] Sign(Byte[] SignatureBase)

            => key.SignData(SignatureBase, hash, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        /// <inheritdoc/>
        public override Boolean Verify(Byte[] SignatureBase, Byte[] Signature)
        {

            if (Signature.Length != signatureLength)
                return false;

            try
            {
                return key.VerifyData(SignatureBase, Signature, hash, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            }
            catch (CryptographicException)
            {
                return false;
            }

        }

    }

}
