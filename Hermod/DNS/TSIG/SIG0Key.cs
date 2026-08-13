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

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// The signing half of a SIG(0) identity: a private key, the name it signs
    /// under, and the KEY record a verifier needs (RFC 2931 §3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The asymmetric counterpart to <see cref="TSIGKey"/>, and the asymmetry is
    /// the whole point. A TSIG key is one secret both parties hold; this is a
    /// pair, and only <see cref="PublicKey"/> ever leaves the signer. A verifier
    /// is configured with — or looks up — that KEY record and nothing else.
    /// </para>
    /// <para>
    /// Unlike a TSIG key name, the name here is meant to resolve: RFC 2931 §3
    /// requires a KEY RR to exist at the signer's name holding the matching
    /// public key. Handing the record over out of band, as
    /// <see cref="DNSServerOptions.SIG0Keys"/> expects, is the same trust
    /// decision made in advance — and a good deal safer than fetching it over
    /// unauthenticated DNS, which would move the problem rather than solve it.
    /// </para>
    /// </remarks>
    public sealed class SIG0Key
    {

        #region Properties

        /// <summary>
        /// The name this key signs under. A SIG(0) puts it in its signer field,
        /// and the verifier's KEY record must be owned by it.
        /// </summary>
        public DomainName           Name         { get; }

        /// <summary>The DNSSEC algorithm number (RFC 8624 §3.1).</summary>
        public Byte                 Algorithm    { get; }

        /// <summary>The private key. Never serialized, never leaves this object.</summary>
        public AsymmetricAlgorithm  PrivateKey   { get; }

        /// <summary>The KEY record a verifier needs, ready to be published or handed over.</summary>
        public KEY                  PublicKey    { get; }

        /// <summary>The key tag of <see cref="PublicKey"/>, which every signature names.</summary>
        public UInt16               KeyTag
            => PublicKey.KeyTag;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a SIG(0) key from an existing key pair.
        /// </summary>
        /// <param name="Name">The name to sign under.</param>
        /// <param name="Algorithm">The DNSSEC algorithm number of the key.</param>
        /// <param name="PrivateKey">An <see cref="RSA"/> or <see cref="ECDsa"/> holding the private key.</param>
        /// <param name="TimeToLive">The TTL of the published KEY record.</param>
        public SIG0Key(DomainName           Name,
                       Byte                 Algorithm,
                       AsymmetricAlgorithm  PrivateKey,
                       TimeSpan?            TimeToLive   = null)
        {

            if (!DNSSECSigning.IsSupportedForSigning(Algorithm))
                throw new NotSupportedException($"DNSSEC algorithm {Algorithm} cannot be used for signing here — see DNSSECSigning.IsSupportedForSigning.");

            this.Name        = Name;
            this.Algorithm   = Algorithm;
            this.PrivateKey  = PrivateKey;
            this.PublicKey   = KEY.FromPublicKey(Name, Algorithm, PrivateKey, TimeToLive: TimeToLive);

        }

        #endregion

        #region (static) Generate(Name, Algorithm = RSASHA256, TimeToLive = null)

        /// <summary>
        /// Generate a fresh SIG(0) key pair.
        /// </summary>
        /// <param name="Name">The name to sign under.</param>
        /// <param name="Algorithm">The DNSSEC algorithm number; RSA/SHA-256 when omitted.</param>
        /// <param name="TimeToLive">The TTL of the published KEY record.</param>
        public static SIG0Key Generate(DomainName  Name,
                                       Byte        Algorithm    = 8,
                                       TimeSpan?   TimeToLive   = null)

            => new (Name,
                    Algorithm,
                    Algorithm switch {
                        8 or 10  => RSA.  Create(2048),
                        13       => ECDsa.Create(ECCurve.NamedCurves.nistP256),
                        14       => ECDsa.Create(ECCurve.NamedCurves.nistP384),
                        _        => throw new NotSupportedException($"DNSSEC algorithm {Algorithm} cannot be generated here.")
                    },
                    TimeToLive);

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this key — never including the private half.
        /// </summary>
        public override String ToString()

            => $"{Name} (algorithm {Algorithm}, key tag {KeyTag})";

        #endregion

    }

}
