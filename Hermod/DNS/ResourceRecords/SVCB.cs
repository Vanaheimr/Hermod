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
    /// Extensions methods for DNS SVCB resource records.
    /// </summary>
    public static class DNS_SVCB_Extensions
    {

        #region CacheSVCB(this DNSClient, DomainName, Priority, TargetName, SVCParameters, Class = DNSQueryClasses.IN, TimeToLive = null)

        /// <summary>
        /// Add a DNS SVCB record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">The domain name of this SVCB resource record.</param>
        /// <param name="Priority">The priority of this target host.</param>
        /// <param name="TargetName">The domain name of the target host.</param>
        /// <param name="SVCParameters">The SVCB parameters.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheSVCB(this DNSClient             DNSClient,
                                     DomainName                 DomainName,
                                     UInt16                     Priority,
                                     DomainName                 TargetName,
                                     IEnumerable<SVCParameter>  SVCParameters,
                                     DNSQueryClasses            Class        = DNSQueryClasses.IN,
                                     TimeSpan?                  TimeToLive   = null)
        {

            var dnsRecord = new SVCB(
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
    /// A DNS SVCB parameter.
    /// </summary>
    public struct SVCParameter
    {

        #region Properties

        /// <summary>
        /// The parameter key.
        /// </summary>
        public UInt16  Key    { get; }

        /// <summary>
        /// The parameter value.
        /// </summary>
        public Byte[]  Value  { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new SVCB parameter.
        /// </summary>
        /// <param name="Key">The parameter key.</param>
        /// <param name="Value">The parameter value.</param>
        public SVCParameter(UInt16  Key,
                            Byte[]  Value)
        {

            this.Key    = Key;
            this.Value  = Value ?? throw new ArgumentNullException(nameof(Value), "The given value must not be null!");

        }

        #endregion

    }


    /// <summary>
    /// The DNS Service Binding (SVCB) resource record.
    /// https://www.rfc-editor.org/rfc/rfc9460
    /// </summary>
    public class SVCB : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Service Binding (SVCB) resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.SVCB;

        #endregion

        #region Properties

        /// <summary>
        /// The priority of this target host.
        /// </summary>
        public UInt16                     Priority         { get; }

        /// <summary>
        /// The domain name of the target host.
        /// </summary>
        public DomainName                 TargetName       { get; }

        /// <summary>
        /// The SVCB parameters.
        /// </summary>
        public IEnumerable<SVCParameter>  SVCParameters    { get; }

        #endregion

        #region (internal static) ParseSVCParameters(Stream, RemainingRDataLength)

        /// <summary>
        /// Parse the SvcParams that occupy the remaining RDATA of an SVCB-form
        /// record (RFC 9460 §2.2). Shared by SVCB and HTTPS.
        /// </summary>
        /// <remarks>
        /// The loop is bounded by RDLENGTH, not by the end of the stream:
        /// RDLENGTH delimits the RDATA (RFC 1035 §4.1.3), and reading beyond it
        /// would consume the resource record that follows — which is exactly
        /// what happens in a real response, where an OPT record usually trails.
        /// </remarks>
        /// <param name="Stream">A stream positioned at the first SvcParamKey.</param>
        /// <param name="RemainingRDataLength">The RDATA octets left after SvcPriority and TargetName.</param>
        internal static List<SVCParameter> ParseSVCParameters(Stream  Stream,
                                                              Int32   RemainingRDataLength)
        {

            var svcParameters  = new List<SVCParameter>();
            var remaining      = RemainingRDataLength;

            while (remaining >= 4)
            {

                var key  = Stream.ReadUInt16BE();
                var len  = Stream.ReadUInt16BE();
                remaining -= 4;

                if (len > remaining)
                    throw new InvalidDataException($"SvcParam {key} claims {len} bytes, but only {remaining} bytes of RDATA remain!");

                var value = new Byte[len];
                Stream.ReadExactly(value, 0, len);
                remaining -= len;

                svcParameters.Add(new SVCParameter(key, value));

            }

            if (remaining != 0)
                throw new InvalidDataException($"{remaining} trailing byte(s) after the last SvcParam!");

            return svcParameters;

        }

        #endregion


        #region Constructors

        #region SVCB(DomainName, Stream)

        /// <summary>
        /// Create a new SVCB resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this SVCB resource record.</param>
        /// <param name="Stream">A stream containing the SVCB resource record data.</param>
        public SVCB(DomainName  DomainName,
                    Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength       = Stream.ReadUInt16BE();
            var rdataStart     = Stream.Position;

            this.Priority      = Stream.ReadUInt16BE();
            this.TargetName    = DNSTools.ExtractDomainName(Stream);

            this.SVCParameters = ParseSVCParameters(
                                     Stream,
                                     rdLength - (Int32) (Stream.Position - rdataStart)
                                 ).AsReadOnly();

        }

        #endregion

        #region SVCB(DomainName, Class, TimeToLive, Priority, TargetName, SVCParameters)

        /// <summary>
        /// Create a new DNS SVCB record.
        /// </summary>
        /// <param name="DomainName">The domain name of this SVCB record.</param>
        /// <param name="Class">The DNS query class of this SVCB record.</param>
        /// <param name="TimeToLive">The time to live of this SVCB record.</param>
        /// <param name="Priority">The priority of this target host.</param>
        /// <param name="TargetName">The domain name of the target host.</param>
        /// <param name="SVCParameters">The SVCB parameters.</param>
        public SVCB(DomainName                 DomainName,
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
            this.SVCParameters  = SVCParameters ?? [];

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
        public static SVCB? TryParseFromJSON(DomainName Name, TimeSpan TimeToLive, String Data, DomainName? Origin = null)
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

                return new SVCB(Name, DNSQueryClasses.IN, TimeToLive,
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
            var targetOffset = (Int32) Stream.Position + 2 + (Int32) tempStream.Position;  // +2 for RDLength
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

            => $"Priority={Priority}, Target={TargetName}, SVCParams={string.Join(", ", SVCParameters.Select(p => $"{SVCParameterPresentation.KeyName(p.Key)}={(p.Value.Length > 0 ? SVCParameterPresentation.EscapeValue(p.Value) : "")}"))}, {base.ToString()}";

        #endregion

    }

}
