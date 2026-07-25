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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Extensions methods for DNS URI resource records.
    /// </summary>
    public static class DNS_URI_Extensions
    {

        #region CacheURI(this DNSClient, DNSServiceName, Priority, Weight, Target, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS URI record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DNSServiceName">The DNS Service Name of this URI resource record.</param>
        /// <param name="Priority">The priority of this target host.</param>
        /// <param name="Weight">The relative weight for entries with the same priority.</param>
        /// <param name="Target">The domain name of the target host.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheURI(this DNSClient   DNSClient,
                                    DNSServiceName   DNSServiceName,
                                    UInt16           Priority,
                                    UInt16           Weight,
                                    URL              Target,
                                    DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                    TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new URI(
                                DNSServiceName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                Priority,
                                Weight,
                                Target
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS Uniform Resource Identifier (URI) resource record.
    /// https://www.rfc-editor.org/rfc/rfc7553
    /// </summary>
    public class URI : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Uniform Resource Identifier (URI) resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.URI;

        #endregion

        #region Properties

        /// <summary>
        /// A 16-bit unsigned integer specifying the priority of this target URI.
        /// Lower values indicate higher priority.
        /// </summary>
        public UInt16  Priority    { get; }

        /// <summary>
        /// A 16-bit unsigned integer specifying a relative weight for entries with the same priority.
        /// Higher weights should be given a proportionately higher probability of being selected.
        /// </summary>
        public UInt16  Weight      { get; }

        /// <summary>
        /// The URI of the target, enclosed in double-quote characters in presentation format.
        /// </summary>
        public URL     Target      { get; }

        #endregion

        #region Constructors

        #region URI(Stream)

        /// <summary>
        /// Create a new URI resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the URI resource record data.</param>
        public URI(Stream Stream)

            : base(Stream,
                   TypeId)

        {

            var rdLength   = Stream.ReadUInt16BE();

            this.Priority  = Stream.ReadUInt16BE();
            this.Weight    = Stream.ReadUInt16BE();

            // RFC 7553 §4.5: the Target is the remaining octets of the RDATA.
            this.Target    = URL.Parse(ReadTarget(Stream, rdLength));

        }

        #endregion

        #region URI(DNSServiceName, Stream)

        /// <summary>
        /// Create a new URI resource record from the given name and stream.
        /// </summary>
        /// <param name="DNSServiceName">The DNS Service Name of this URI resource record.</param>
        /// <param name="Stream">A stream containing the URI resource record data.</param>
        public URI(DNSServiceName  DNSServiceName,
                   Stream          Stream)

            : base(DNSServiceName,
                   TypeId,
                   Stream)

        {

            var rdLength   = Stream.ReadUInt16BE();

            this.Priority  = Stream.ReadUInt16BE();
            this.Weight    = Stream.ReadUInt16BE();

            // RFC 7553 §4.5: the Target is the remaining octets of the RDATA.
            this.Target    = URL.Parse(ReadTarget(Stream, rdLength));

        }

        #endregion

        #region URI(DNSServiceName, Class, TimeToLive, Priority, Weight, Port, Target)

        /// <summary>
        ///  Create a new DNS URI record.
        /// </summary>
        /// <param name="DNSServiceName">The DNS Service Name of this URI record.</param>
        /// <param name="Class">The DNS query class of this URI record.</param>
        /// <param name="TimeToLive">The time to live of this URI record.</param>
        /// <param name="Priority">The priority of this target host.</param>
        /// <param name="Weight">The relative weight for entries with the same priority.</param>
        /// <param name="Target">The domain name of the target host.</param>
        public URI(DNSServiceName   DNSServiceName,
                   DNSQueryClasses  Class,
                   TimeSpan         TimeToLive,
                   UInt16           Priority,
                   UInt16           Weight,
                   URL              Target)

            : base(DNSServiceName,
                   TypeId,
                   Class,
                   TimeToLive,
                   $"{Priority} {Weight} {Target}")

        {

            this.Priority  = Priority;
            this.Weight    = Weight;
            this.Target    = Target;

        }

        #endregion

        #endregion



        #region (private static) ReadTarget(Stream, RDLength)

        /// <summary>
        /// Read the URI Target: the RDATA octets remaining after the 2-byte
        /// Priority and 2-byte Weight fields (RFC 7553 §4.5).
        /// </summary>
        private static String ReadTarget(Stream  Stream,
                                         UInt16  RDLength)
        {

            if (RDLength < 4)
                throw new InvalidDataException($"URI RDATA of {RDLength} bytes is too short for Priority and Weight!");

            var buffer = new Byte[RDLength - 4];
            Stream.ReadExactly(buffer, 0, buffer.Length);

            return Encoding.ASCII.GetString(buffer);

        }

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
        public static URI? TryParseFromJSON(DNSServiceName Name, TimeSpan TimeToLive, String Data)
        {
            try
            {
                var parts = Data.Split(' ', 3);
                if (parts.Length < 3) return null;
                return new URI(Name, DNSQueryClasses.IN, TimeToLive,
                               UInt16.Parse(parts[0]), UInt16.Parse(parts[1]),
                               HTTP.URL.Parse(parts[2].Trim('"')));
            }
            catch { return null; }
        }

        #endregion

        #region (protected override) ZoneFileRData()

        /// <inheritdoc/>
        protected override String ZoneFileRData()
            => $"{Priority} {Weight} \"{Target}\"";

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

            // RFC 7553 §4.5: the Target is "the remaining octets of the RDATA"
            // — plain octets, NOT a domain name and NOT a character-string, so
            // it carries neither a length prefix nor label encoding, and name
            // compression MUST NOT be applied to it.
            var target   = Encoding.ASCII.GetBytes(Target.ToString());

            var dataLen  = 2 + 2 + target.Length;

            if (dataLen > UInt16.MaxValue)
                throw new InvalidOperationException("RDATA exceeds maximum UInt16 length (65535 bytes)!");

            Stream.WriteUInt16BE(dataLen);

            Stream.WriteUInt16BE(Priority);
            Stream.WriteUInt16BE(Weight);
            Stream.Write(target, 0, target.Length);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"Priority={Priority}, Weight={Weight}, Target={Target}, {base.ToString()}";

        #endregion

    }

}
