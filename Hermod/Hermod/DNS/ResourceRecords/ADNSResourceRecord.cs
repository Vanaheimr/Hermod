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

using System.Text;
using System.Globalization;
using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// The abstract DNS Resource Record class for objects returned in
    /// answers, authorities and additional record DNS responses.
    /// </summary>
    public abstract class ADNSResourceRecord : IDNSResourceRecord
    {

        #region Properties

        /// <summary>
        /// The domain name of this resource record.
        /// </summary>
        public DNSServiceName          DomainName    { get; }

        /// <summary>
        /// The type of this resource record.
        /// </summary>
        public DNSResourceRecordTypes  Type          { get; }

        /// <summary>
        /// The class of this resource record.
        /// </summary>
        public DNSQueryClasses         Class         { get; }

        /// <summary>
        /// The time to live of this resource record.
        /// </summary>
        public TimeSpan                TimeToLive    { get; }



        /// <summary>
        /// The end of life of this resource record.
        /// </summary>
        [NoDNSPaketInformation]
        public DateTimeOffset          EndOfLife     { get; }

        /// <summary>
        /// The source IP address of this resource record, if available.
        /// </summary>
        [NoDNSPaketInformation]
        public IIPAddress?             Source        { get; }

        /// <summary>
        /// The text representation of this resource record, if available.
        /// </summary>
        [NoDNSPaketInformation]
        public String?                 RText         { get; }

        #endregion

        #region Constructor(s)

        #region (protected) ADNSResourceRecord(DNSStream,      Type)

        /// <summary>
        /// Create a new DNS resource record from the given DNS stream and type.
        /// </summary>
        /// <param name="DNSStream">A stream containing the DNS resource record data.</param>
        /// <param name="Type">A valid DNS resource record type.</param>
        protected ADNSResourceRecord(Stream                  DNSStream,
                                     DNSResourceRecordTypes  Type)
        {

            this.DomainName  = DNSTools.ExtractDNSServiceName(DNSStream);

            this.Type        = Type;
            var type         = (DNSResourceRecordTypes) DNSStream.ReadUInt16BE();  //((DNSStream.ReadByte() & Byte.MaxValue) <<  8 |  DNSStream.ReadByte() & Byte.MaxValue);
            if (type != Type)
                throw new ArgumentException($"Invalid DNS resource record type! Expected '{Type}', but got '{type}'!");

            this.Class       = (DNSQueryClasses)        DNSStream.ReadUInt16BE();  //(DNSStream.ReadByte() & Byte.MaxValue) <<  8 |  DNSStream.ReadByte() & Byte.MaxValue);
            this.TimeToLive  = TimeSpan.FromSeconds    (DNSStream.ReadUInt32BE()); //(DNSStream.ReadByte() & Byte.MaxValue) << 24 | (DNSStream.ReadByte() & Byte.MaxValue) << 16 | (DNSStream.ReadByte() & Byte.MaxValue) << 8 | DNSStream.ReadByte() & Byte.MaxValue);
            this.EndOfLife   = Timestamp.Now + TimeToLive;

            //var RDLength     = (DNSStream.ReadByte() & Byte.MaxValue) << 8 | DNSStream.ReadByte() & Byte.MaxValue;

        }

        #endregion

        #region (protected) ADNSResourceRecord(DomainName,     Type, DNSStream)

        protected ADNSResourceRecord(DomainName              DomainName,
                                     DNSResourceRecordTypes  Type,
                                     Stream                  DNSStream)

            : this(DNSServiceName.Parse(
                       DomainName.FullName
                   ),
                   Type,
                   DNSStream)

        { }

        #endregion

        #region (protected) ADNSResourceRecord(DNSServiceName, Type, DNSStream)

        /// <summary>
        /// Create a new DNS resource record from the given name, type and DNS stream.
        /// </summary>
        /// <param name="DNSServiceName">A DNS service name.</param>
        /// <param name="Type">A valid DNS resource record type.</param>
        /// <param name="DNSStream">A stream containing the DNS resource record data.</param>
        protected ADNSResourceRecord(DNSServiceName          DNSServiceName,
                                     DNSResourceRecordTypes  Type,
                                     Stream                  DNSStream)
        {

            this.DomainName  = DNSServiceName;
            this.Type        = Type;
            this.Class       = (DNSQueryClasses)    DNSStream.ReadUInt16BE();  //((DNSStream.ReadByte() & Byte.MaxValue) <<  8 |  DNSStream.ReadByte() & Byte.MaxValue);
            this.TimeToLive  = TimeSpan.FromSeconds(DNSStream.ReadUInt32BE()); // (DNSStream.ReadByte() & Byte.MaxValue) << 24 | (DNSStream.ReadByte() & Byte.MaxValue) << 16 | (DNSStream.ReadByte() & Byte.MaxValue) << 8 | DNSStream.ReadByte() & Byte.MaxValue);
            this.EndOfLife   = Timestamp.Now + TimeToLive;

            //var RDLength     = (DNSStream.ReadByte() & Byte.MaxValue) << 8 | DNSStream.ReadByte() & Byte.MaxValue;

        }

        #endregion

        #region (protected) ADNSResourceRecord(DomainName,     Type, Class, TimeToLive, RText = null)

        protected ADNSResourceRecord(DomainName              DomainName,
                                     DNSResourceRecordTypes  Type,
                                     DNSQueryClasses         Class,
                                     TimeSpan                TimeToLive,
                                     String?                 RText   = null)

            : this(DNSServiceName.Parse(
                       DomainName.FullName
                   ),
                   Type,
                   Class,
                   TimeToLive,
                   RText)

        { }

        #endregion

        #region (protected) ADNSResourceRecord(DNSServiceName, Type, Class, TimeToLive, RText = null)

        protected ADNSResourceRecord(DNSServiceName          DNSServiceName,
                                     DNSResourceRecordTypes  Type,
                                     DNSQueryClasses         Class,
                                     TimeSpan                TimeToLive,
                                     String?                 RText   = null)
        {

            this.DomainName  = DNSServiceName;
            this.Type        = Type;
            this.Class       = Class;
            this.TimeToLive  = TimeToLive;
            this.EndOfLife   = Timestamp.Now + TimeToLive;
            this.RText       = RText;

        }

        #endregion

        #endregion


        #region (private static) TryParseZoneFileRData(DNSServiceName, DomainName, Type, Class, TimeToLive, RData)

        private static Boolean TryParseZoneFileRData(DNSServiceName                               DNSServiceName,
                                                     DomainName?                                  DomainName,
                                                     DNSResourceRecordTypes                       Type,
                                                     DNSQueryClasses                              Class,
                                                     TimeSpan                                     TimeToLive,
                                                     String                                       RData,
                                                     [NotNullWhen(true)] out IDNSResourceRecord?  ResourceRecord)
        {

            if (DomainName is not null)
            {

                ResourceRecord = Type switch {

                    DNSResourceRecordTypes.A           => A.         TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.AAAA        => AAAA.      TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.NS          => NS.        TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.CNAME       => CNAME.     TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.SOA         => SOA.       TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.PTR         => PTR.       TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.HINFO       => HINFO.     TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.MX          => MX.        TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.TXT         => TXT.       TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.RP          => RP.        TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.AFSDB       => AFSDB.     TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.LOC         => LOC.       TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.NAPTR       => NAPTR.     TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.CERT        => CERT.      TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.DNAME       => DNAME.     TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.DS          => DS.        TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.SSHFP       => SSHFP.     TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.RRSIG       => RRSIG.     TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.NSEC        => NSEC.      TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.DNSKEY      => DNSKEY.    TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.NSEC3       => NSEC3.     TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.NSEC3PARAM  => NSEC3PARAM.TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.TLSA        => TLSA.      TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.SMIMEA      => SMIMEA.    TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.CDS         => CDS.       TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.CDNSKEY     => CDNSKEY.   TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.OPENPGPKEY  => OPENPGPKEY.TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.CSYNC       => CSYNC.     TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.ZONEMD      => ZONEMD.    TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.SVCB        => SVCB.      TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.HTTPS       => HTTPS.     TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.SPF         => SPF.       TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.EUI48       => EUI48.     TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.EUI64       => EUI64.     TryParseFromJSON(DomainName, TimeToLive, RData),
                    DNSResourceRecordTypes.CAA         => CAA.       TryParseFromJSON(DomainName, TimeToLive, RData),

                    DNSResourceRecordTypes.TKEY        => TryParseTKEYFromZoneFile   (DomainName, Class, TimeToLive, RData),
                    DNSResourceRecordTypes.TSIG        => TryParseTSIGFromZoneFile   (DomainName, Class, TimeToLive, RData),

                    _                                  => null

                };

                if (ResourceRecord is not null)
                    return true;

            }

            if (DomainName is not null)
            {

                ResourceRecord = Type switch {

                    DNSResourceRecordTypes.SRV         => SRV.TryParseFromJSON(DNSServiceName, TimeToLive, RData),
                    DNSResourceRecordTypes.URI         => URI.TryParseFromJSON(DNSServiceName, TimeToLive, RData),

                    _                                  => null

                };

                if (ResourceRecord is not null)
                    return true;

            }

            ResourceRecord = null;
            return false;

        }

        #endregion

        #region (private static) TryParseTKEYFromZoneFile(Name, Class, TimeToLive, RData)

        private static TKEY? TryParseTKEYFromZoneFile(DomainName       Name,
                                                      DNSQueryClasses  Class,
                                                      TimeSpan         TimeToLive,
                                                      String           RData)
        {

            try
            {

                var parts = RData.Split(' ', 6, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 6)
                    return null;

                if (!TryParseZoneFileTimestamp(parts[1], out var inception) ||
                    !TryParseZoneFileTimestamp(parts[2], out var expiration))
                {
                    return null;
                }

                return new TKEY(
                           Name,
                           Class,
                           TimeToLive,
                           DNS.DomainName.Parse(parts[0]),
                           (UInt32) inception,
                           (UInt32) expiration,
                           UInt16.Parse(parts[3]),
                           UInt16.Parse(parts[4]),
                           Convert.FromBase64String(parts[5]),
                           []
                       );

            }
            catch
            {
                return null;
            }

        }

        #endregion

        #region (private static) TryParseTSIGFromZoneFile(Name, Class, TimeToLive, RData)

        private static TSIG? TryParseTSIGFromZoneFile(DomainName       Name,
                                                      DNSQueryClasses  Class,
                                                      TimeSpan         TimeToLive,
                                                      String           RData)
        {

            try
            {

                var parts = RData.Split(' ', 6, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 6)
                    return null;

                if (!TryParseZoneFileTimestamp(parts[1], out var timeSigned))
                    return null;

                return new TSIG(
                           Name,
                           Class,
                           TimeToLive,
                           DNS.DomainName.Parse(parts[0]),
                           (UInt64) timeSigned,
                           UInt16.Parse(parts[2]),
                           Convert.FromBase64String(parts[3]),
                           UInt16.Parse(parts[4]),
                           UInt16.Parse(parts[5]),
                           []
                       );

            }
            catch
            {
                return null;
            }

        }

        #endregion

        #region (private static) TokenizeZoneFileString(ZoneFileString)

        private static List<String> TokenizeZoneFileString(String ZoneFileString)
        {

            var tokens      = new List<String>();
            var token       = new System.Text.StringBuilder();
            var inQuote     = false;
            var isEscaped   = false;

            foreach (var c in ZoneFileString ?? "")
            {

                if (isEscaped)
                {
                    token.Append(c);
                    isEscaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    token.Append(c);
                    isEscaped = true;
                    continue;
                }

                if (c == '"')
                {
                    token.Append(c);
                    inQuote = !inQuote;
                    continue;
                }

                if (c == ';' && !inQuote)
                    break;

                if ((c == '(' || c == ')') && !inQuote)
                {
                    AddToken(tokens, token);
                    continue;
                }

                if (Char.IsWhiteSpace(c) && !inQuote)
                {
                    AddToken(tokens, token);
                    continue;
                }

                token.Append(c);

            }

            AddToken(tokens, token);

            return tokens;

        }

        #endregion

        #region (private static) AddToken(Tokens, Token)

        private static void AddToken(List<String>   Tokens,
                                     StringBuilder  Token)
        {

            if (Token.Length > 0)
            {
                Tokens.Add(Token.ToString());
                Token.Clear();
            }

        }

        #endregion

        #region (private static) TryParseDNSQueryClass(Text, out Class)

        private static Boolean TryParseDNSQueryClass(String               Text,
                                                     out DNSQueryClasses  Class)
        {

            if (Enum.TryParse(Text, true, out Class) &&
                Enum.IsDefined(Class))
            {
                return true;
            }

            if (UInt16.TryParse(Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var classId))
            {
                Class = (DNSQueryClasses) classId;
                return true;
            }

            return false;

        }

        #endregion

        #region (private static) TryParseDNSResourceRecordType(Text, out Type)

        private static Boolean TryParseDNSResourceRecordType(String                      Text,
                                                             out DNSResourceRecordTypes  Type)
        {

            if (Enum.TryParse (Text, true, out Type) &&
                Enum.IsDefined(Type))
            {
                return true;
            }

            if (Text.StartsWith("TYPE", StringComparison.OrdinalIgnoreCase) &&
                UInt16.TryParse(Text[4..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var typeId))
            {
                Type = (DNSResourceRecordTypes) typeId;
                return true;
            }

            return false;

        }

        #endregion

        #region (private static) TryParseZoneFileTimeToLive(Text, out TimeToLive)

        private static Boolean TryParseZoneFileTimeToLive(String        Text,
                                                          out TimeSpan  TimeToLive)
        {

            TimeToLive = TimeSpan.Zero;

            if (UInt32.TryParse(Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
            {
                TimeToLive = TimeSpan.FromSeconds(seconds);
                return true;
            }

            return false;

        }

        #endregion

        #region (private static) TryParseZoneFileTimestamp(Text, out UnixTime)

        private static Boolean TryParseZoneFileTimestamp(String     Text,
                                                         out Int64  UnixTime)
        {

            UnixTime = 0;

            if (!DateTimeOffset.TryParseExact(
                    Text,
                    "yyyyMMddHHmmss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var timestamp
                ))
            {
                return false;
            }

            UnixTime = timestamp.ToUnixTimeSeconds();
            return true;

        }

        #endregion

        #region (private static) TryParseTypeBitmapType(Text, out TypeId)

        private static Boolean TryParseTypeBitmapType(String      Text,
                                                      out UInt16  TypeId)
        {

            if (Enum.TryParse<DNSResourceRecordTypes>(Text, true, out var resourceRecordType) &&
                Enum.IsDefined(resourceRecordType))
            {
                TypeId = (UInt16) resourceRecordType;
                return true;
            }

            if (Text.StartsWith("TYPE", StringComparison.OrdinalIgnoreCase) &&
                UInt16.TryParse(Text[4..], NumberStyles.Integer, CultureInfo.InvariantCulture, out TypeId))
            {
                return true;
            }

            return UInt16.TryParse(Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out TypeId);

        }

        #endregion


        #region (static) ParseZoneFileString(ZoneFileString, DefaultTimeToLive = null)

        /// <summary>
        /// Parse a single BIND-style zone-file resource record line.
        /// </summary>
        /// <param name="ZoneFileString">A BIND-style zone-file resource record line.</param>
        /// <param name="DefaultTimeToLive">An optional TTL used when the zone-file line omits one.</param>
        public static IDNSResourceRecord ParseZoneFileString(String     ZoneFileString,
                                                             TimeSpan?  DefaultTimeToLive   = null)
        {

            if (TryParseZoneFileString(
                    ZoneFileString,
                    out var resourceRecord,
                    out var errorResponse,
                    DefaultTimeToLive
                ))
            {
                return resourceRecord;
            }

            throw new ArgumentException(errorResponse, nameof(ZoneFileString));

        }

        #endregion

        #region (static) TryParseZoneFileString(ZoneFileString, out ResourceRecord, out ErrorResponse, DefaultTimeToLive = null)

        /// <summary>
        /// Try to parse a single BIND-style zone-file resource record line.
        /// </summary>
        /// <param name="ZoneFileString">A BIND-style zone-file resource record line.</param>
        /// <param name="ResourceRecord">The parsed DNS resource record.</param>
        /// <param name="ErrorResponse">An optional error response in case the parsing fails.</param>
        /// <param name="DefaultTimeToLive">An optional TTL used when the zone-file line omits one.</param>
        public static Boolean TryParseZoneFileString(String                                        ZoneFileString,
                                                     [NotNullWhen(true)]  out IDNSResourceRecord?  ResourceRecord,
                                                     [NotNullWhen(false)] out String?              ErrorResponse,
                                                     TimeSpan?                                     DefaultTimeToLive   = null)
        {

            ResourceRecord  = null;
            ErrorResponse   = null;

            var tokens = TokenizeZoneFileString(ZoneFileString);
            if (tokens.Count < 4)
            {
                ErrorResponse = "The given zone-file resource record must contain at least name, class, type and RDATA!";
                return false;
            }

            var ownerNameText = tokens[0];

            if (!DNSServiceName.TryParse(ownerNameText, out var dnsServiceName, out ErrorResponse))
                return false;

            if (!DNS.DomainName.TryParse(ownerNameText, out var domainName,     out ErrorResponse))
                return false;

            var resourceRecordClass  = DNSQueryClasses.IN;
            var timeToLive           = DefaultTimeToLive ?? TimeSpan.Zero;
            var index                = 1;
            var typeFound            = false;
            var resourceRecordType   = default(DNSResourceRecordTypes);

            while (index < tokens.Count)
            {

                if (TryParseDNSQueryClass(tokens[index], out var parsedClass))
                {
                    resourceRecordClass = parsedClass;
                    index++;
                    continue;
                }

                if (TryParseZoneFileTimeToLive(tokens[index], out var parsedTimeToLive))
                {
                    timeToLive = parsedTimeToLive;
                    index++;
                    continue;
                }

                if (TryParseDNSResourceRecordType(tokens[index], out resourceRecordType))
                {
                    typeFound = true;
                    index++;
                    break;
                }

                ErrorResponse = $"Invalid zone-file resource record header token '{tokens[index]}'!";
                return false;

            }

            if (!typeFound)
            {
                ErrorResponse = "Missing DNS resource record type!";
                return false;
            }

            if (index >= tokens.Count)
            {
                ErrorResponse = $"Missing RDATA for DNS resource record type '{resourceRecordType}'!";
                return false;
            }

            if (!TryParseZoneFileRData(
                   dnsServiceName,
                   domainName,
                   resourceRecordType,
                   resourceRecordClass,
                   timeToLive,
                   String.Join(" ", tokens.Skip(index)),
                   out ResourceRecord
               ))
            {
                ErrorResponse = $"Could not parse RDATA for DNS resource record type '{resourceRecordType}'!";
                return false;
            }

            if (ResourceRecord.Class != resourceRecordClass)
            {
                ErrorResponse = $"DNS resource record class '{resourceRecordClass}' is not supported by the '{resourceRecordType}' zone-file parser!";
                ResourceRecord = null;
                return false;
            }

            return true;

        }

        #endregion

        #region ToZoneFileString()

        /// <summary>
        /// Return the standard BIND zone-file representation of this resource record.
        /// Format: &lt;name&gt; &lt;TTL&gt; &lt;class&gt; &lt;type&gt; &lt;rdata&gt;
        /// </summary>
        public virtual String ToZoneFileString()
            => $"{DomainName,-24} {(Int32) TimeToLive.TotalSeconds,-7} {Class,-4} {Type,-10} {ZoneFileRData()}";

        /// <summary>
        /// Return the RDATA portion of this resource record in zone-file presentation format.
        /// </summary>
        protected abstract String ZoneFileRData();

        /// <summary>
        /// Decode a DNS type bit map (RFC 4034 Section 4.1.2) into a space-separated string of type names.
        /// Used by NSEC, NSEC3, and CSYNC zone-file representations.
        /// </summary>
        /// <param name="TypeBitMaps">The raw type bit map bytes.</param>
        protected static String DecodeTypeBitMaps(Byte[] TypeBitMaps)
        {

            var types  = new List<String>();
            var offset = 0;

            while (offset < TypeBitMaps.Length)
            {

                if (offset + 2 > TypeBitMaps.Length)
                    break;

                var windowBlock  = TypeBitMaps[offset];
                var bitmapLength = TypeBitMaps[offset + 1];
                offset += 2;

                if (offset + bitmapLength > TypeBitMaps.Length)
                    break;

                for (var i = 0; i < bitmapLength; i++)
                {
                    var octet = TypeBitMaps[offset + i];
                    for (var bit = 0; bit < 8; bit++)
                    {
                        if ((octet & (0x80 >> bit)) != 0)
                        {
                            var typeNumber = windowBlock * 256 + i * 8 + bit;
                            var rrType     = (DNSResourceRecordTypes) typeNumber;
                            types.Add(Enum.IsDefined(rrType) ? rrType.ToString() : $"TYPE{typeNumber}");
                        }
                    }
                }

                offset += bitmapLength;

            }

            return String.Join(" ", types);

        }

        #endregion


        #region (protected static)   EncodeTypeBitMaps(Types)

        /// <summary>
        /// Encode a sequence of DNS resource record type names into a DNS type bit map
        /// (RFC 4034 Section 4.1.2). Used by NSEC, NSEC3, and CSYNC parsers.
        /// </summary>
        /// <param name="Types">DNS resource record type names or TYPE#### tokens.</param>
        protected static Byte[] EncodeTypeBitMaps(IEnumerable<String> Types)
        {

            var typeIds = new SortedSet<UInt16>();

            foreach (var type in Types)
            {
                if (TryParseTypeBitmapType(type, out var typeId))
                    typeIds.Add(typeId);
            }

            if (typeIds.Count == 0)
                return [];

            var bytes = new List<Byte>();

            foreach (var window in typeIds.GroupBy(typeId => typeId / 256))
            {

                var offsets       = window.Select(typeId => typeId % 256).ToArray();
                var bitmapLength  = offsets.Max() / 8 + 1;
                var bitmap        = new Byte[bitmapLength];

                foreach (var offset in offsets)
                    bitmap[offset / 8] |= (Byte) (0x80 >> (offset % 8));

                bytes.Add((Byte) window.Key);
                bytes.Add((Byte) bitmapLength);
                bytes.AddRange(bitmap);

            }

            return [.. bytes];

        }

        #endregion

        #region (protected abstract) SerializeRRData(Stream)

        /// <summary>
        /// Serialize the concrete DNS resource record to the given stream.
        /// </summary>
        /// <param name="Stream">The stream to write to.</param>
        /// <param name="UseCompression">Whether to use name compression (true by default).</param>
        /// <param name="CompressionOffsets">An optional dictionary for name compression offsets.</param>
        protected abstract void SerializeRRData(Stream                      Stream,
                                                Boolean                     UseCompression       = true,
                                                Dictionary<String, Int32>?  CompressionOffsets   = null);

        #endregion

        #region Serialize(Stream, UseCompression = true, CompressionOffsets = null)

        /// <summary>
        /// Serialize the abstract DNS resource record to the given stream.
        /// </summary>
        /// <param name="Stream">The stream to write to.</param>
        /// <param name="UseCompression">Whether to use name compression (true by default).</param>
        /// <param name="CompressionOffsets">An optional dictionary for name compression offsets.</param>
        public void Serialize(Stream                      Stream,
                              Boolean                     UseCompression       = true,
                              Dictionary<String, Int32>?  CompressionOffsets   = null)
        {

            DomainName.Serialize(
                Stream,
                (Int32) Stream.Position,
                UseCompression,
                CompressionOffsets
            );

            Stream.WriteUInt16BE((UInt16) Type);
            Stream.WriteUInt16BE((UInt16) Class);
            Stream.WriteUInt32BE((UInt32) Math.Min(TimeToLive.TotalSeconds, UInt32.MaxValue));

            SerializeRRData(
                Stream,
                UseCompression,
                CompressionOffsets
            );

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(

                   $"DomainName={DomainName}, Type={Type}, Class={Class}, TTL={TimeToLive.TotalSeconds} seconds, EndOfLife='{EndOfLife}'",

                   Source is not null
                       ? $"Source = {Source}"
                       : String.Empty

               );

        #endregion

    }

}
