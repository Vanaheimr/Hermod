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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Extensions methods for DNS HTTPS resource records.
    /// </summary>
    public static class DNS_HTTPS_Extensions
    {

        #region CacheHTTPS(this DNSClient, DomainName, Class = IN, TimeToLive = 365days, Priority, TargetName, SVCParameters, ...)

        /// <summary>
        /// Add a DNS HTTPS record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">A domain name.</param>
        /// <param name="Class">The DNS query class of this HTTPS record.</param>
        /// <param name="TimeToLive">The time to live of this HTTPS record.</param>
        /// <param name="Priority">The priority of this target host.</param>
        /// <param name="TargetName">The domain name of the target host.</param>
        /// <param name="SVCParameters">The HTTPS parameters.</param>
        public static void CacheHTTPS(this DNSClient             DNSClient,
                                      DomainName                 DomainName,
                                      UInt16                     Priority,
                                      DomainName                 TargetName,
                                      IEnumerable<SVCParameter>  SVCParameters,
                                      DNSQueryClasses            Class        = DNSQueryClasses.IN,
                                      TimeSpan?                  TimeToLive   = null)
        {

            var dnsRecord = new HTTPS(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                Priority,
                                TargetName,
                                SVCParameters
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS HTTPS resource record.
    /// https://www.rfc-editor.org/rfc/rfc9460
    /// </summary>
    public class HTTPS : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS HTTPS resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.HTTPS;

        #endregion

        #region Properties

        /// <summary>
        /// The priority of this target host.
        /// </summary>
        public UInt16                     Priority        { get; }

        /// <summary>
        /// The domain name of the target host.
        /// </summary>
        public DomainName                 TargetName      { get; }

        /// <summary>
        /// The HTTPS parameters.
        /// </summary>
        public IEnumerable<SVCParameter>  SVCParameters    { get; }

        #endregion

        #region Constructors

        #region HTTPS(DomainName, Stream)

        /// <summary>
        /// Create a new HTTPS resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this HTTPS resource record.</param>
        /// <param name="Stream">A stream containing the HTTPS resource record data.</param>
        public HTTPS(DomainName  DomainName,
                     Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength       = Stream.ReadUInt16BE();
            var rdataStart     = Stream.Position;

            this.Priority      = Stream.ReadUInt16BE();
            this.TargetName    = DNSTools.ExtractDomainName(Stream);

            this.SVCParameters = SVCB.ParseSVCParameters(
                                     Stream,
                                     rdLength - (Int32) (Stream.Position - rdataStart)
                                 ).AsReadOnly();

        }

        #endregion

        #region HTTPS(DomainName, Class, TimeToLive, Priority, TargetName, SVCParameters)

        /// <summary>
        /// Create a new DNS HTTPS record.
        /// </summary>
        /// <param name="DomainName">The domain name of this HTTPS record.</param>
        /// <param name="Class">The DNS query class of this HTTPS record.</param>
        /// <param name="TimeToLive">The time to live of this HTTPS record.</param>
        /// <param name="Priority">The priority of this target host.</param>
        /// <param name="TargetName">The domain name of the target host.</param>
        /// <param name="SVCParameters">The HTTPS parameters.</param>
        public HTTPS(DomainName                 DomainName,
                     DNSQueryClasses            Class,
                     TimeSpan                   TimeToLive,
                     UInt16                     Priority,
                     DomainName                 TargetName,
                     IEnumerable<SVCParameter>  SVCParameters)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.Priority       = Priority;
            this.TargetName     = TargetName;
            this.SVCParameters  = SVCParameters;

        }

        #endregion

        #endregion


        #region (static) TryParseFromJSON(Name, TimeToLive, Data)

        /// <summary>
        /// Try to parse this resource record from a DNS JSON API "data" field
        /// (e.g. Google dns.google/resolve or Cloudflare cloudflare-dns.com/dns-query).
        /// </summary>
        /// <param name="Name">The owner name of this resource record.</param>
        /// <param name="TimeToLive">The TTL of this resource record.</param>
        /// <param name="Data">The "data" field value from the JSON response.</param>
        /// <returns>The parsed resource record, or null if parsing fails.</returns>
        public static HTTPS? TryParseFromJSON(DomainName Name, TimeSpan TimeToLive, String Data, DomainName? Origin = null)
        {
            try
            {

                // Presentation format: "1 . alpn="h2,h3" ipv4hint=1.2.3.4"
                var parts = Data.Split(' ', 3);
                if (parts.Length < 2) return null;

                var priority   = UInt16.Parse(parts[0]);
                var targetStr  = parts[1].TrimEnd('.').Length == 0 ? "." : parts[1];

                var targetName = DNS.DomainName.ParseLenient(targetStr, Origin);

                // RFC 9460 §2.1: each value is decoded as a character-string and
                // then converted into its key's wire format. A value that is not
                // valid for its key throws, and the record is refused below.
                var svcParams  = parts.Length > 2
                                     ? SVCParameterPresentation.Read(parts[2])
                                     : new List<SVCParameter>();

                return new HTTPS(Name, DNSQueryClasses.IN, TimeToLive,
                                 priority, targetName, svcParams);

            }
            catch { return null; }
        }

        #endregion


        #region (protected override) ZoneFileRData()

        /// <inheritdoc/>
        protected override String ZoneFileRData()
            => SVCParameterPresentation.Write(Priority, TargetName.ToString(), SVCParameters);

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

            tempStream.WriteUInt16BE(Priority);

            // TargetName (variable, with compression)
            int targetOffset = (Int32) Stream.Position + 2 + (Int32) tempStream.Position;  // +2 for RDLength
            TargetName.Serialize(
                tempStream,
                targetOffset,
                false,   // RFC 9460 §2.2: the TargetName goes on the wire uncompressed.
                CompressionOffsets
            );

            // SVC parameters sorted by their keys
            foreach (var svcParameter in SVCParameters.OrderBy(kvp => kvp.Key))
            {
                tempStream.WriteUInt16BE(svcParameter.Key);
                tempStream.WriteUInt16BE(svcParameter.Value.Length);
                tempStream.Write        (svcParameter.Value, 0, svcParameter.Value.Length);
            }

            if (tempStream.Length > UInt16.MaxValue)
                throw new InvalidOperationException("RDATA exceeds maximum UInt16 length (65535 bytes)!");

            // RDLENGTH (2 bytes)
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

            => $"Priority={Priority}, Target={TargetName}, SvcParams={string.Join(", ", SVCParameters.Select(p => $"{SVCParameterPresentation.KeyName(p.Key)}={(p.Value.Length > 0 ? SVCParameterPresentation.EscapeValue(p.Value) : "")}"))}, {base.ToString()}";

        #endregion

    }

}