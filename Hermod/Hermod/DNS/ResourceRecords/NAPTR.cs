/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Text.RegularExpressions;

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Extensions methods for DNS NAPTR resource records.
    /// </summary>
    public static class DNS_NAPTR_Extensions
    {

        #region AddToCache(this DNSClient, DomainName, Order, Preference, Flags, Services, RegExpr, Replacement, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS NAPTR record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">The domain name of this AAAA resource record.</param>
        /// <param name="Order">The order in which the NAPTR records MUST be processed.</param>
        /// <param name="Preference">The order in which NAPTR records with the same Order value should be processed.</param>
        /// <param name="Flags">The flags to control aspects of the rewriting and interpretation of the fields.</param>
        /// <param name="Services">The service parameters applicable to this delegation path.</param>
        /// <param name="RegExpr">The substitution expression (regular expression) applied to the original string to construct the next domain name.</param>
        /// <param name="Replacement">The new value in the case where the regular expression is a simple replacement.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheNAPTR(this DNSClient   DNSClient,
                                      DomainName       DomainName,
                                      UInt16           Order,
                                      UInt16           Preference,
                                      String           Flags,
                                      String           Services,
                                      String           RegExpr,
                                      DomainName       Replacement,
                                      DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                      TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new NAPTR(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                Order,
                                Preference,
                                Flags,
                                Services,
                                RegExpr,
                                Replacement
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS Naming Authority Pointer (NAPTR) resource record.
    /// https://www.rfc-editor.org/rfc/rfc3403
    /// </summary>
    public class NAPTR : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Naming Authority Pointer (NAPTR) resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.NAPTR;

        #endregion

        #region Properties

        /// <summary>
        /// A 16-bit unsigned integer specifying the order in which the NAPTR records MUST be processed.
        /// Low numbers are processed before high numbers.
        /// </summary>
        public UInt16      Order          { get; }

        /// <summary>
        /// A 16-bit unsigned integer that specifies the order in which NAPTR records with the same Order value should be processed.
        /// Low numbers are processed before high numbers.
        /// </summary>
        public UInt16      Preference     { get; }

        /// <summary>
        /// A character-string containing flags to control aspects of the rewriting and interpretation of the fields.
        /// Flags are single characters from the set [A-Z0-9], case preserved.
        /// </summary>
        public String      Flags          { get; }

        /// <summary>
        /// A character-string that specifies the Service Parameters applicable to this delegation path.
        /// </summary>
        public String      Services       { get; }

        /// <summary>
        /// A character-string containing a substitution expression (regular expression) applied to the original string to construct the next domain name.
        /// </summary>
        public String      RegExpr        { get; }

        /// <summary>
        /// A domain-name which specifies the new value in the case where the regular expression is a simple replacement.
        /// Can be "." if terminal.
        /// </summary>
        public DomainName  Replacement    { get; }

        #endregion

        #region Constructors

        #region NAPTR(Stream)

        /// <summary>
        /// Create a new NAPTR resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the NAPTR resource record data.</param>
        public NAPTR(Stream Stream)

            : base(Stream,
                   TypeId)

        {

            this.Order        = (UInt16) ((Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue);
            this.Preference   = (UInt16) ((Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue);
            this.Flags        = DNSTools.ExtractCharacterString(Stream);
            this.Services     = DNSTools.ExtractCharacterString(Stream);
            this.RegExpr      = DNSTools.ExtractCharacterString(Stream);
            this.Replacement  = DNSTools.ExtractDomainName(Stream);

        }

        #endregion

        #region NAPTR(DomainName, Stream)

        /// <summary>
        /// Create a new NAPTR resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this NAPTR resource record.</param>
        /// <param name="Stream">A stream containing the NAPTR resource record data.</param>
        public NAPTR(DomainName  DomainName,
                     Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            this.Order        = (UInt16) ((Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue);
            this.Preference   = (UInt16) ((Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue);
            this.Flags        = DNSTools.ExtractCharacterString(Stream);
            this.Services     = DNSTools.ExtractCharacterString(Stream);
            this.RegExpr      = DNSTools.ExtractCharacterString(Stream);
            this.Replacement  = DNSTools.ExtractDomainName(Stream);

        }

        #endregion

        #region NAPTR(DomainName, Class, TimeToLive, Order, Preference, Flags, Services, RegExpr, Replacement)

        /// <summary>
        /// Create a new DNS NAPTR record.
        /// </summary>
        /// <param name="DomainName">The domain name of this NAPTR record.</param>
        /// <param name="Class">The DNS query class of this NAPTR record.</param>
        /// <param name="TimeToLive">The time to live of this NAPTR record.</param>
        /// <param name="Order">The order in which the NAPTR records MUST be processed.</param>
        /// <param name="Preference">The order in which NAPTR records with the same Order value should be processed.</param>
        /// <param name="Flags">The flags to control aspects of the rewriting and interpretation of the fields.</param>
        /// <param name="Services">The service parameters applicable to this delegation path.</param>
        /// <param name="RegExpr">The substitution expression (regular expression) applied to the original string to construct the next domain name.</param>
        /// <param name="Replacement">The new value in the case where the regular expression is a simple replacement.</param>
        public NAPTR(DomainName       DomainName,
                     DNSQueryClasses  Class,
                     TimeSpan         TimeToLive,
                     UInt16           Order,
                     UInt16           Preference,
                     String           Flags,
                     String           Services,
                     String           RegExpr,
                     DomainName       Replacement)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive,
                   $"{Order} {Preference} '{Flags}' '{Services}' '{RegExpr}' {Replacement}")

        {

            this.Order        = Order;
            this.Preference   = Preference;
            this.Flags        = Flags;
            this.Services     = Services;
            this.RegExpr      = RegExpr;
            this.Replacement  = Replacement;

        }

        #endregion

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

            var tempStream = new MemoryStream();

            tempStream.WriteUInt16BE (Order);
            tempStream.WriteUInt16BE (Preference);
            tempStream.WriteASCIIMax255    (Flags);
            tempStream.WriteASCIIMax255    (Services);
            tempStream.WriteASCIIMax255    (RegExpr);

            // REPLACEMENT (domain-name, variable with compression)
            int replacementOffset = (Int32) Stream.Position + 2 + (Int32) tempStream.Position;  // +2 for RDLength
            Replacement.Serialize(
                tempStream,
                replacementOffset,
                UseCompression,
                CompressionOffsets
            );

            if (tempStream.Length > UInt16.MaxValue)
                throw new InvalidOperationException("RDATA exceeds maximum UInt16 length (65535 bytes)!");

            // RDLENGTH: Variable, when compression is used!
            Stream.WriteUInt16BE(tempStream.Length);

            // Copy RDATA to main stream
            tempStream.Position = 0;
            tempStream.CopyTo(Stream);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"Order={Order}, Preference={Preference}, Flags='{Flags}', Services='{Services}', RegExpr='{RegExpr}', Replacement={Replacement}, {base.ToString()}";

        #endregion

    }

}
