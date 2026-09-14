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
    /// A DNSSEC key with the private half still attached: a DNSKEY to publish and
    /// the ability to sign with it.
    /// </summary>
    /// <remarks>
    /// The two halves of a key live in different shapes depending on the
    /// algorithm — RSA and ECDSA are platform <see cref="AsymmetricAlgorithm"/>
    /// objects, the Edwards curves of RFC 8080 are plain octet strings — and
    /// everything that wants to sign something would otherwise have to know
    /// which. This is the one place that does.
    /// </remarks>
    public sealed class DNSSECSigningKey : IDisposable
    {

        #region Data

        private readonly AsymmetricAlgorithm?  asymmetric;
        private readonly Byte[]?               raw;

        #endregion

        #region Properties

        /// <summary>The DNSKEY record to publish at the zone apex.</summary>
        public DNSKEY  DNSKEY    { get; }

        /// <summary>The DNSSEC algorithm number (RFC 8624 §3.1).</summary>
        public Byte    Algorithm  => DNSKEY.Algorithm;

        /// <summary>The key tag this key is referred to by (RFC 4034 Appendix B).</summary>
        public UInt16  KeyTag     { get; }

        /// <summary>
        /// Whether this is a key signing key, which is what the Secure Entry Point
        /// flag of RFC 4034 §2.1.1 marks.
        /// </summary>
        public Boolean IsKeySigningKey
            => (DNSKEY.Flags & 0x0001) != 0;

        #endregion

        #region Constructor(s)

        private DNSSECSigningKey(DNSKEY                DNSKEY,
                                 AsymmetricAlgorithm?  Asymmetric,
                                 Byte[]?               Raw)
        {

            this.DNSKEY      = DNSKEY;
            this.asymmetric  = Asymmetric;
            this.raw         = Raw;
            this.KeyTag      = DNSSECValidator.ComputeKeyTag(DNSKEY);

        }

        #endregion

        #region (static) Generate(Zone, Algorithm, KeySigningKey = false, RSAKeySize = 2048, TimeToLive = null)

        /// <summary>
        /// Generate a fresh key for a zone.
        /// </summary>
        /// <param name="Zone">The zone apex the DNSKEY will be published at.</param>
        /// <param name="Algorithm">A DNSSEC algorithm number this implementation will sign with.</param>
        /// <param name="KeySigningKey">Whether to set the Secure Entry Point flag (RFC 4034 §2.1.1).</param>
        /// <param name="RSAKeySize">The modulus size for the RSA algorithms.</param>
        /// <param name="TimeToLive">The TTL of the DNSKEY record.</param>
        public static DNSSECSigningKey Generate(DomainName  Zone,
                                                Byte        Algorithm,
                                                Boolean     KeySigningKey   = false,
                                                Int32       RSAKeySize      = 2048,
                                                TimeSpan?   TimeToLive      = null)
        {

            if (!DNSSECSigning.IsSupportedForSigning(Algorithm))
                throw new NotSupportedException(
                          $"DNSSEC algorithm {Algorithm} is not one this implementation signs with. " +
                           "RFC 8624 §3.1 forbids RSA/SHA-1 (5 and 7) for new signatures, which is why " +
                           "they can be validated here and not produced.");

            // RFC 4034 §2.1.1: bit 7 of the flags is Zone Key and must be set for
            // any key that signs a zone; bit 15 is the Secure Entry Point.
            var flags  = (UInt16) (0x0100 | (KeySigningKey ? 0x0001 : 0x0000));
            var ttl    = TimeToLive ?? TimeSpan.FromHours(1);

            if (DNSSECSigning.UsesRawPrivateKey(Algorithm))
            {

                var privateKey = DNSSECSigning.GeneratePrivateKey(Algorithm);

                return new DNSSECSigningKey(
                           new DNSKEY(Zone, DNSQueryClasses.IN, ttl,
                                      flags, 3, Algorithm,
                                      DNSSECSigning.PublicKeyFromPrivateKey(Algorithm, privateKey)),
                           null,
                           privateKey
                       );

            }

            AsymmetricAlgorithm key = Algorithm switch {
                                          8 or 10  => RSA.  Create(RSAKeySize),
                                          13       => ECDsa.Create(ECCurve.NamedCurves.nistP256),
                                          14       => ECDsa.Create(ECCurve.NamedCurves.nistP384),
                                          _        => throw new NotSupportedException($"No key generation for DNSSEC algorithm {Algorithm}.")
                                      };

            return new DNSSECSigningKey(
                       new DNSKEY(Zone, DNSQueryClasses.IN, ttl,
                                  flags, 3, Algorithm,
                                  DNSSECSigning.EncodePublicKey(Algorithm, key)),
                       key,
                       null
                   );

        }

        #endregion

        #region Sign(Data)

        /// <summary>
        /// Sign data with this key, in the signature encoding DNS uses for its
        /// algorithm.
        /// </summary>
        /// <param name="Data">The octets to sign.</param>
        public Byte[] Sign(Byte[] Data)

            => raw is not null
                   ? DNSSECSigning.Sign(Algorithm, raw,        Data)
                   : DNSSECSigning.Sign(Algorithm, asymmetric!, Data);

        #endregion

        #region DelegationSigner(DigestType = 2)

        /// <summary>
        /// The DS record a parent zone would publish for this key
        /// (RFC 4034 §5.1.4).
        /// </summary>
        /// <param name="DigestType">1 for SHA-1, 2 for SHA-256, 4 for SHA-384.</param>
        public DS DelegationSigner(Byte DigestType = 2)
        {

            // §5.1.4: the digest is over the canonical owner name followed by the
            // DNSKEY RDATA — the owner name lowercased and uncompressed, exactly
            // as a signature would take it.
            var owner  = DNSTools.SerializeCanonicalName(DNSKEY.DomainName.FullName);
            var rdata  = ADNSResourceRecord.RDataOf(DNSKEY);

            var input  = new Byte[owner.Length + rdata.Length];
            Buffer.BlockCopy(owner, 0, input, 0,             owner.Length);
            Buffer.BlockCopy(rdata, 0, input, owner.Length,  rdata.Length);

            var digest = DigestType switch {
                             1  => SHA1.  HashData(input),
                             2  => SHA256.HashData(input),
                             4  => SHA384.HashData(input),
                             _  => throw new NotSupportedException($"DS digest type {DigestType} is not one of RFC 4034 §5.1.3's.")
                         };

            return new DS(
                       DomainName.ParseLenient(DNSKEY.DomainName.FullName),
                       DNSQueryClasses.IN,
                       DNSKEY.TimeToLive,
                       KeyTag,
                       Algorithm,
                       DigestType,
                       digest
                   );

        }

        #endregion

        #region Dispose()

        public void Dispose()
            => asymmetric?.Dispose();

        #endregion

        public override String ToString()
            => $"{(IsKeySigningKey ? "KSK" : "ZSK")} {DNSKEY.DomainName} algorithm {Algorithm}, key tag {KeyTag}";

    }

}
