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
    /// The DNS KEY resource record (RFC 2535 §3, restricted by RFC 3445).
    /// </summary>
    /// <remarks>
    /// <para>
    /// KEY and DNSKEY have almost the same shape and entirely different jobs.
    /// KEY was the original DNSSEC key record; DNSSEC-bis replaced it with
    /// DNSKEY, and RFC 3445 then took the zone-signing role away from KEY
    /// altogether — it may no longer carry a zone key, only an application one.
    /// </para>
    /// <para>
    /// Two applications still use it: the Diffie-Hellman mode of TKEY
    /// (RFC 2930 §4.2), whose key material is encoded per RFC 2539, and SIG(0)
    /// transaction signatures (RFC 2931). Anything to do with signing a zone
    /// belongs in <see cref="DNSKEY"/>.
    /// </para>
    /// </remarks>
    public class KEY : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS KEY resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.KEY;

        /// <summary>
        /// The only protocol value RFC 3445 leaves: 3, meaning DNSSEC. The field
        /// itself is vestigial — §4 keeps it purely so the record's layout does
        /// not change — but a conforming KEY still carries this value.
        /// </summary>
        public const Byte ProtocolDNSSEC = 3;

        /// <summary>
        /// Diffie-Hellman, the algorithm TKEY's key exchange uses (RFC 2539 §2).
        /// </summary>
        public const Byte AlgorithmDiffieHellman = 2;

        #endregion

        #region Properties

        /// <summary>
        /// The flags field.
        /// </summary>
        public UInt16  Flags        { get; }

        /// <summary>
        /// The protocol field, which RFC 3445 §4 fixes at 3.
        /// </summary>
        public Byte    Protocol     { get; }

        /// <summary>
        /// The public key algorithm.
        /// </summary>
        public Byte    Algorithm    { get; }

        /// <summary>
        /// The public key material, in whatever form the algorithm defines.
        /// </summary>
        public Byte[]  PublicKey    { get; }


        /// <summary>
        /// Whether the flags forbid this key's use for authentication
        /// (RFC 2535 §3.1.2, bit 0 set).
        /// </summary>
        public Boolean AuthenticationProhibited
            => (Flags & 0x8000) != 0;

        /// <summary>
        /// Whether the flags forbid this key's use for confidentiality
        /// (RFC 2535 §3.1.2, bit 1 set).
        /// </summary>
        public Boolean ConfidentialityProhibited
            => (Flags & 0x4000) != 0;

        /// <summary>
        /// Whether this record carries no key at all — both use bits set, which
        /// RFC 2535 §3.1.2 defines as "no key information". The record then
        /// exists only to assert that the name has no key, and the public key
        /// field is empty.
        /// </summary>
        public Boolean IsNoKey
            => (Flags & 0xC000) == 0xC000;

        /// <summary>
        /// The key tag of this record (RFC 4034 Appendix B over the same RDATA
        /// layout). A SIG(0) names its key by this number plus the signer's name
        /// (RFC 2931 §3), so a verifier holding several keys at one name can pick
        /// the right one without trying each in turn.
        /// </summary>
        /// <remarks>
        /// A tag is not an identifier. It is a 16-bit checksum, collisions happen,
        /// and RFC 4034 §5.3 is explicit that it may only narrow the candidates —
        /// never decide. Two keys at one name with the same tag both have to be
        /// tried.
        /// </remarks>
        public UInt16 KeyTag
            => DNSSECValidator.ComputeKeyTag(Flags, Protocol, Algorithm, PublicKey);

        #endregion

        #region Constructor

        #region KEY(DomainName,     Stream)

        /// <summary>
        /// Create a new KEY resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this KEY resource record.</param>
        /// <param name="Stream">A stream containing the KEY resource record data.</param>
        public KEY(DomainName  DomainName,
                   Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.Flags      = Stream.ReadUInt16BE();
            this.Protocol   = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Algorithm  = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.PublicKey  = DNSTools.ExtractByteArray(Stream, (UInt32) (rdLength - 4));

        }

        #endregion

        #region KEY(DNSServiceName, Stream)

        /// <summary>
        /// Create a new KEY resource record from the given name and stream.
        /// </summary>
        /// <param name="DNSServiceName">The DNS service name of this KEY resource record.</param>
        /// <param name="Stream">A stream containing the KEY resource record data.</param>
        public KEY(DNSServiceName  DNSServiceName,
                   Stream          Stream)

            : base(DNSServiceName,
                   TypeId,
                   Stream)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.Flags      = Stream.ReadUInt16BE();
            this.Protocol   = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Algorithm  = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.PublicKey  = DNSTools.ExtractByteArray(Stream, (UInt32) (rdLength - 4));

        }

        #endregion

        #region KEY(DomainName,     Class, TimeToLive, Flags, Protocol, Algorithm, PublicKey)

        /// <summary>
        /// Create a new DNS KEY resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this KEY resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="Flags">The flags field.</param>
        /// <param name="Protocol">The protocol field; RFC 3445 §4 fixes this at 3.</param>
        /// <param name="Algorithm">The public key algorithm.</param>
        /// <param name="PublicKey">The public key material.</param>
        public KEY(DomainName       DomainName,
                   DNSQueryClasses  Class,
                   TimeSpan         TimeToLive,
                   UInt16           Flags,
                   Byte             Protocol,
                   Byte             Algorithm,
                   Byte[]           PublicKey)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.Flags      = Flags;
            this.Protocol   = Protocol;
            this.Algorithm  = Algorithm;
            this.PublicKey  = PublicKey;

        }

        #endregion

        #endregion


        #region (static) FromPublicKey(DomainName, Algorithm, PublicKey, Class = IN, TimeToLive = 1h, Flags = 0)

        /// <summary>
        /// Publish a public key as a KEY record.
        /// </summary>
        /// <param name="DomainName">The name to publish it at. A SIG(0) signer names this in its signer field, so it has to be the name the verifier will look up.</param>
        /// <param name="Algorithm">The DNSSEC algorithm number of the key.</param>
        /// <param name="PublicKey">An <see cref="System.Security.Cryptography.RSA"/> or <see cref="System.Security.Cryptography.ECDsa"/>; only the public half is read.</param>
        /// <param name="Class">The DNS query class.</param>
        /// <param name="TimeToLive">The time to live.</param>
        /// <param name="Flags">The flags field. Zero — no use restrictions — is what a SIG(0) key carries.</param>
        public static KEY FromPublicKey(DomainName                                    DomainName,
                                        Byte                                          Algorithm,
                                        System.Security.Cryptography.AsymmetricAlgorithm  PublicKey,
                                        DNSQueryClasses                               Class        = DNSQueryClasses.IN,
                                        TimeSpan?                                     TimeToLive   = null,
                                        UInt16                                        Flags        = 0)

            => new (DomainName,
                    Class,
                    TimeToLive ?? TimeSpan.FromHours(1),
                    Flags,
                    ProtocolDNSSEC,
                    Algorithm,
                    DNSSECSigning.EncodePublicKey(Algorithm, PublicKey));


        /// <summary>
        /// Publish a public key that is already in its wire form as a KEY record.
        /// </summary>
        /// <param name="DomainName">The name to publish it at.</param>
        /// <param name="Algorithm">The DNSSEC algorithm number of the key.</param>
        /// <param name="PublicKey">The public key, encoded as its algorithm defines.</param>
        /// <param name="Class">The DNS query class.</param>
        /// <param name="TimeToLive">The time to live.</param>
        /// <param name="Flags">The flags field. Zero — no use restrictions — is what a SIG(0) key carries.</param>
        /// <remarks>
        /// The Edwards curves need this: RFC 8080 §3 gives their public keys no
        /// encoding beyond the raw point, so there is no structured key object to
        /// hand to the overload above and nothing for it to encode.
        /// </remarks>
        public static KEY FromPublicKeyBytes(DomainName       DomainName,
                                             Byte             Algorithm,
                                             Byte[]           PublicKey,
                                             DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                             TimeSpan?        TimeToLive   = null,
                                             UInt16           Flags        = 0)

            => new (DomainName,
                    Class,
                    TimeToLive ?? TimeSpan.FromHours(1),
                    Flags,
                    ProtocolDNSSEC,
                    Algorithm,
                    PublicKey);

        #endregion

        #region (static) TryParseFromJSON(Name, TimeToLive, Data)

        /// <summary>
        /// Try to parse this resource record from a DNS JSON API "data" field.
        /// </summary>
        /// <param name="Name">The owner name of this resource record.</param>
        /// <param name="TimeToLive">The TTL of this resource record.</param>
        /// <param name="Data">The "data" field value from the JSON response.</param>
        public static KEY? TryParseFromJSON(DomainName Name, TimeSpan TimeToLive, String Data)
        {
            try
            {

                var parts = Data.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 4)
                    return null;

                return new KEY(
                           Name,
                           DNSQueryClasses.IN,
                           TimeToLive,
                           UInt16.Parse(parts[0]),
                           Byte.  Parse(parts[1]),
                           Byte.  Parse(parts[2]),
                           Convert.FromBase64String(String.Concat(parts[3..]))
                       );

            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region (protected override) ZoneFileRData()

        /// <inheritdoc/>
        protected override String ZoneFileRData()

            => $"{Flags} {Protocol} {Algorithm} {Convert.ToBase64String(PublicKey)}";

        #endregion

        #region (protected override) SerializeRRData(Stream, UseCompression = true, CompressionOffsets = null)

        /// <summary>
        /// Serialize the KEY resource record to the given stream.
        /// </summary>
        /// <param name="Stream">The stream to write to.</param>
        /// <param name="UseCompression">Whether to use name compression.</param>
        /// <param name="CompressionOffsets">An optional dictionary for name compression offsets.</param>
        protected override void SerializeRRData(Stream                      Stream,
                                                Boolean                     UseCompression       = true,
                                                Dictionary<String, Int32>?  CompressionOffsets   = null)
        {

            Stream.WriteUInt16BE((UInt16) (4 + PublicKey.Length));

            Stream.WriteUInt16BE(Flags);
            Stream.WriteByte    (Protocol);
            Stream.WriteByte    (Algorithm);
            Stream.Write        (PublicKey, 0, PublicKey.Length);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"{Flags} {Protocol} {Algorithm}, {PublicKey.Length} octets, {base.ToString()}";

        #endregion

    }

}
