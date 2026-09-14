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

using System.Globalization;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Extensions methods for DNS IPSECKEY resource records.
    /// </summary>
    public static class DNS_IPSECKEY_Extensions
    {

        #region CacheIPSECKEY(this DNSClient, DomainName, Precedence, GatewayType, Algorithm, Gateway, PublicKey, ...)

        /// <summary>
        /// Add a DNS IPSECKEY record cache entry.
        /// </summary>
        public static void CacheIPSECKEY(this DNSClient   DNSClient,
                                         DomainName       DomainName,
                                         Byte             Precedence,
                                         Byte             GatewayType,
                                         Byte             Algorithm,
                                         Byte[]           Gateway,
                                         Byte[]           PublicKey,
                                         DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                         TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new IPSECKEY(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromHours(2),
                                Precedence,
                                GatewayType,
                                Algorithm,
                                Gateway,
                                PublicKey
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS IPSECKEY resource record (RFC 4025), which publishes the IPsec
    /// keying material for a host and, optionally, the gateway that speaks for it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The gateway field has four shapes and a preceding octet says which
    /// (RFC 4025 §2.3): 0 no gateway, 1 four octets of IPv4, 2 sixteen of IPv6,
    /// 3 a wire-encoded domain name whose "format is self-describing, so the
    /// length is implicit". A field whose meaning is decided by another field is
    /// where a record parser usually goes wrong, and the same shape has been
    /// wrong twice in this stack already — LOC's version octet and APL's address
    /// family.
    /// </para>
    /// <para>
    /// §2.4: the name "MUST NOT be compressed", which is RFC 3597 §4 for a
    /// post-1035 type and is asserted rather than assumed.
    /// </para>
    /// <para>
    /// §2.5 allows a record with no key at all: algorithm 0 means "no key is
    /// present" and the public key field "MUST be construed to be zero octets in
    /// length". BIND cannot represent that record — `named-checkzone` refuses it
    /// in the presentation form <i>and</i> in the RFC 3597 §5 generic form, with
    /// "unexpected end of input" — so there is nothing to copy and no
    /// interoperable spelling to pick. This writes the line with the key simply
    /// absent, which is what §2.5 describes, and reads the same back.
    /// </para>
    /// </remarks>
    public class IPSECKEY : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS IPSECKEY resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.IPSECKEY;

        /// <summary>No gateway is present (RFC 4025 §2.3).</summary>
        public const Byte NoGateway    = 0;

        /// <summary>A 4-byte IPv4 address is present.</summary>
        public const Byte IPv4Gateway  = 1;

        /// <summary>A 16-byte IPv6 address is present.</summary>
        public const Byte IPv6Gateway  = 2;

        /// <summary>A wire-encoded domain name is present.</summary>
        public const Byte NameGateway  = 3;

        #endregion

        #region Properties

        /// <summary>
        /// Read the same way as an MX PREFERENCE (RFC 4025 §2.2): lower is better.
        /// </summary>
        public Byte     Precedence     { get; }

        /// <summary>
        /// Which of the four shapes the gateway field has (RFC 4025 §2.3).
        /// </summary>
        public Byte     GatewayType    { get; }

        /// <summary>
        /// 0 no key, 1 DSA, 2 RSA (RFC 4025 §2.4).
        /// </summary>
        public Byte     Algorithm      { get; }

        /// <summary>
        /// The gateway exactly as it appears in the RDATA — four octets, sixteen,
        /// a wire-encoded name, or nothing.
        /// </summary>
        /// <remarks>
        /// Kept as octets rather than as a parsed address or name, because the
        /// gateway type is what decides which of those it is, and a record whose
        /// type octet and gateway disagree has to survive being read in order to
        /// be reported at all.
        /// </remarks>
        public Byte[]   Gateway        { get; }

        /// <summary>
        /// The public key, empty when the algorithm is 0.
        /// </summary>
        public Byte[]   PublicKey      { get; }


        /// <summary>
        /// The gateway as an IPv4 address, or null when the gateway is not one.
        /// </summary>
        public IPv4Address? GatewayIPv4

            => GatewayType == IPv4Gateway && Gateway.Length == 4
                   ? new IPv4Address(Gateway)
                   : null;

        /// <summary>
        /// The gateway as an IPv6 address, or null when the gateway is not one.
        /// </summary>
        public IPv6Address? GatewayIPv6

            => GatewayType == IPv6Gateway && Gateway.Length == 16
                   ? new IPv6Address(Gateway)
                   : null;

        /// <summary>
        /// The gateway as a domain name, or null when the gateway is not one.
        /// </summary>
        public DNS.DomainName? GatewayName
        {
            get
            {

                if (GatewayType != NameGateway || Gateway.Length == 0)
                    return null;

                try
                {
                    using var stream = new MemoryStream(Gateway);
                    return DNS.DomainName.ParseLenient(DNSTools.ExtractName(stream));
                }
                catch
                {
                    return null;
                }

            }
        }

        #endregion

        #region Constructor(s)

        #region IPSECKEY(DomainName, Stream)

        /// <summary>
        /// Create a new IPSECKEY resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this IPSECKEY resource record.</param>
        /// <param name="Stream">A stream containing the IPSECKEY resource record data.</param>
        public IPSECKEY(DomainName  DomainName,
                        Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength  = Stream.ReadUInt16BE();
            var rdata     = DNSTools.ExtractByteArray(Stream, rdLength);

            // Three octets before anything else. Less than that is a truncated
            // record, and reading it as zeros keeps the stream where the next
            // record starts rather than throwing and losing every record behind
            // this one (finding 21).
            this.Precedence   = rdata.Length > 0 ? rdata[0] : (Byte) 0;
            this.GatewayType  = rdata.Length > 1 ? rdata[1] : (Byte) 0;
            this.Algorithm    = rdata.Length > 2 ? rdata[2] : (Byte) 0;

            var offset        = Math.Min(3, rdata.Length);
            var gatewayLength = GatewayLengthOf(GatewayType, rdata, offset);

            this.Gateway      = rdata[offset..(offset + gatewayLength)];
            this.PublicKey    = rdata[(offset + gatewayLength)..];

        }

        #endregion

        #region IPSECKEY(DomainName, Class, TimeToLive, Precedence, GatewayType, Algorithm, Gateway, PublicKey)

        /// <summary>
        /// Create a new DNS IPSECKEY resource record.
        /// </summary>
        public IPSECKEY(DomainName       DomainName,
                        DNSQueryClasses  Class,
                        TimeSpan         TimeToLive,
                        Byte             Precedence,
                        Byte             GatewayType,
                        Byte             Algorithm,
                        Byte[]           Gateway,
                        Byte[]           PublicKey)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.Precedence   = Precedence;
            this.GatewayType  = GatewayType;
            this.Algorithm    = Algorithm;
            this.Gateway      = Gateway;
            this.PublicKey    = PublicKey;

        }

        #endregion

        #endregion


        #region (private static) GatewayLengthOf(GatewayType, RData, Offset)

        /// <summary>
        /// How many octets of the RDATA the gateway occupies.
        /// </summary>
        /// <remarks>
        /// Type 3's length is not written anywhere: RFC 4025 §2.3 says the
        /// wire-encoded name "is self-describing, so the length is implicit",
        /// which means walking its labels is the only way to find where the
        /// public key begins. Guessing wrong here does not fail — it silently
        /// moves the boundary between gateway and key.
        /// </remarks>
        private static Int32 GatewayLengthOf(Byte    GatewayType,
                                             Byte[]  RData,
                                             Int32   Offset)
        {

            var available = RData.Length - Offset;

            switch (GatewayType)
            {

                case NoGateway:
                    return 0;

                case IPv4Gateway:
                    return Math.Min(4, available);

                case IPv6Gateway:
                    return Math.Min(16, available);

                case NameGateway:
                    {

                        var index = Offset;

                        while (index < RData.Length)
                        {

                            var label = RData[index];

                            // The name is uncompressed by §2.4, so a pointer here
                            // is not a pointer; stopping is the only safe reading.
                            if ((label & 0xC0) != 0)
                                return index - Offset + 1;

                            index++;

                            if (label == 0)
                                return index - Offset;

                            index += label;

                        }

                        return available;

                    }

                // An unassigned gateway type says nothing about how long its
                // gateway is, so there is no boundary to find and everything
                // after the three fixed octets is taken as the key.
                default:
                    return 0;

            }

        }

        #endregion

        #region (static) TryParseFromJSON(Name, TimeToLive, Data)

        /// <summary>
        /// Try to parse this resource record from the RFC 4025 §3.1 presentation
        /// format: precedence, gateway type, algorithm, gateway, and the base-64
        /// public key.
        /// </summary>
        /// <param name="Name">The owner name of this resource record.</param>
        /// <param name="TimeToLive">The TTL of this resource record.</param>
        /// <param name="Data">The presentation form of the RDATA.</param>
        /// <param name="Origin">An optional origin for a relative gateway name.</param>
        public static IPSECKEY? TryParseFromJSON(DomainName   Name,
                                                 TimeSpan     TimeToLive,
                                                 String       Data,
                                                 DomainName?  Origin   = null)
        {

            var parts = Data.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

            // §2.5 permits a record with no key, so the key is the one field that
            // may be missing; everything before it is mandatory.
            if (parts.Length < 4)
                return null;

            if (!Byte.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var precedence) ||
                !Byte.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var gatewayType) ||
                !Byte.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var algorithm))
            {
                return null;
            }

            var gatewayText = parts[3];
            var gateway     = Array.Empty<Byte>();

            switch (gatewayType)
            {

                case NoGateway:
                    // §3.1: "the gateway field MUST be '.'"
                    if (gatewayText != ".")
                        return null;
                    break;

                case IPv4Gateway:
                    if (!IPv4Address.TryParse(gatewayText, out var ipv4))
                        return null;
                    gateway = ipv4.GetBytes();
                    break;

                case IPv6Gateway:
                    if (!IPv6Address.TryParse(gatewayText, out var ipv6))
                        return null;
                    gateway = ipv6.GetBytes();
                    break;

                case NameGateway:
                    {

                        DNS.DomainName gatewayName;

                        try
                        {
                            gatewayName = DNS.DomainName.ParseLenient(gatewayText, Origin);
                        }
                        catch
                        {
                            return null;
                        }

                        using var stream = new MemoryStream();
                        gatewayName.Serialize(stream, 0, false, null);
                        gateway = stream.ToArray();

                    }
                    break;

                default:
                    return null;

            }

            var publicKey = Array.Empty<Byte>();

            if (parts.Length > 4)
            {
                try
                {
                    publicKey = Convert.FromBase64String(String.Concat(parts[4..]));
                }
                catch
                {
                    return null;
                }
            }

            return new IPSECKEY(
                       Name,
                       DNSQueryClasses.IN,
                       TimeToLive,
                       precedence,
                       gatewayType,
                       algorithm,
                       gateway,
                       publicKey
                   );

        }

        #endregion

        #region (protected override) ZoneFileRData()

        /// <inheritdoc/>
        protected override String ZoneFileRData()
        {

            var gateway = GatewayType switch {

                              NoGateway    => ".",

                              IPv4Gateway  => GatewayIPv4?.ToString()                  ?? ".",

                              // RFC 5952 §4, the same canonical form a zone file
                              // wants everywhere else — and the same reason it is
                              // not IPv6Address.ToString(), which brackets (finding 50).
                              IPv6Gateway  => GatewayIPv6 is not null
                                                  ? DNSTools.ToZoneFileText(GatewayIPv6.Value)
                                                  : ".",

                              NameGateway  => GatewayName?.FullName                    ?? ".",

                              _            => "."

                          };

            var key = PublicKey.Length > 0
                          ? " " + Convert.ToBase64String(PublicKey)
                          : "";

            return $"{Precedence} {GatewayType} {Algorithm} {gateway}{key}";

        }

        #endregion

        #region (protected override) SerializeRRData(Stream, UseCompression = true, CompressionOffsets = null)

        /// <summary>
        /// Serialize the concrete DNS resource record to the given stream.
        /// </summary>
        /// <param name="Stream">The stream to write to.</param>
        /// <param name="UseCompression">Whether to use name compression (true by default).</param>
        /// <param name="CompressionOffsets">An optional dictionary for name compression offsets.</param>
        protected override void SerializeRRData(Stream                      Stream,
                                                Boolean                     UseCompression       = true,
                                                Dictionary<String, Int32>?  CompressionOffsets   = null)
        {

            Stream.WriteUInt16BE(3 + Gateway.Length + PublicKey.Length);

            Stream.WriteByte(Precedence);
            Stream.WriteByte(GatewayType);
            Stream.WriteByte(Algorithm);

            // The gateway is written back exactly as it was read, which for a
            // type 3 name is already the uncompressed wire form RFC 4025 §2.4
            // requires. Re-encoding it through the name serializer would be the
            // opportunity to compress it by accident.
            Stream.Write(Gateway,   0, Gateway.Length);
            Stream.Write(PublicKey, 0, PublicKey.Length);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"{ZoneFileRData()}, {base.ToString()}";

        #endregion

    }

}
