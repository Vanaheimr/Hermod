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
    /// A shared secret for <c>hmac-sha256</c> (RFC 9421, Section 3.3.3).
    /// </summary>
    /// <remarks>
    /// Whoever can verify can sign, which is what Section 7.3.3 warns about. The
    /// comparison takes the same time however many octets match.
    /// </remarks>
    public sealed class HTTPSignatureHMACKey : HTTPSignatureKey
    {

        private readonly Byte[] secret;

        /// <inheritdoc/>
        public override Boolean CanSign
            => true;

        internal HTTPSignatureHMACKey(Byte[]   Secret,
                                      String?  KeyId)

            : base(HTTPSignatureAlgorithms.HMACSHA256, KeyId)

        {

            if (Secret is null || Secret.Length == 0)
                throw new ArgumentException("An HMAC secret must not be empty!", nameof(Secret));

            this.secret = [.. Secret];

        }

        /// <inheritdoc/>
        public override Byte[] Sign(Byte[] SignatureBase)

            => System.Security.Cryptography.HMACSHA256.HashData(secret, SignatureBase);

        /// <inheritdoc/>
        public override Boolean Verify(Byte[] SignatureBase, Byte[] Signature)

            => CryptographicOperations.FixedTimeEquals(Sign(SignatureBase), Signature);

    }

}
