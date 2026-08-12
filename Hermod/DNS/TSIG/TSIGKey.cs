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

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// A shared secret between two parties, named and bound to an HMAC
    /// algorithm — the "key" of RFC 8945 §3.
    /// </summary>
    /// <remarks>
    /// The key name is a domain name, but it names a secret rather than a node
    /// in the DNS. Nothing resolves it, and it never has to exist in any zone.
    /// </remarks>
    public class TSIGKey
    {

        #region Properties

        /// <summary>
        /// The name of this key, as it appears in the TSIG record's owner name.
        /// </summary>
        public DomainName  Name         { get; }

        /// <summary>
        /// The HMAC algorithm, as one of the names in RFC 8945 §6.
        /// </summary>
        public DomainName  Algorithm    { get; }

        /// <summary>
        /// The shared secret.
        /// </summary>
        public Byte[]      Secret       { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new TSIG key.
        /// </summary>
        /// <param name="Name">The name of this key.</param>
        /// <param name="Secret">The shared secret.</param>
        /// <param name="Algorithm">The HMAC algorithm; HMAC-SHA256 when omitted, which RFC 8945 §6 makes mandatory to implement.</param>
        public TSIGKey(DomainName   Name,
                       Byte[]       Secret,
                       DomainName?  Algorithm   = null)
        {

            this.Name       = Name;
            this.Secret     = Secret;
            this.Algorithm  = Algorithm ?? TSIGAlgorithms.HMACSHA256;

        }

        /// <summary>
        /// Create a new TSIG key from a Base64-encoded secret, which is how
        /// every generator and configuration file hands one over.
        /// </summary>
        /// <param name="Name">The name of this key.</param>
        /// <param name="Base64Secret">The shared secret, Base64-encoded.</param>
        /// <param name="Algorithm">The HMAC algorithm; HMAC-SHA256 when omitted.</param>
        public static TSIGKey ParseBase64(DomainName   Name,
                                          String       Base64Secret,
                                          DomainName?  Algorithm   = null)

            => new (Name,
                    Convert.FromBase64String(Base64Secret),
                    Algorithm);

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this key — never including the secret.
        /// </summary>
        public override String ToString()

            => $"{Name} ({Algorithm})";

        #endregion

    }

}
