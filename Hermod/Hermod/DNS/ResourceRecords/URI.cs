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

#region Usings

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

            this.Priority  = (UInt16) ((Stream.ReadByte() & byte.MaxValue) << 8 | Stream.ReadByte() & byte.MaxValue);
            this.Weight    = (UInt16) ((Stream.ReadByte() & byte.MaxValue) << 8 | Stream.ReadByte() & byte.MaxValue);
            this.Target    = URL.Parse(DNSTools.ExtractName(Stream));

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

            this.Priority  = (UInt16) ((Stream.ReadByte() & byte.MaxValue) << 8 | Stream.ReadByte() & byte.MaxValue);
            this.Weight    = (UInt16) ((Stream.ReadByte() & byte.MaxValue) << 8 | Stream.ReadByte() & byte.MaxValue);
            this.Target    = URL.Parse(DNSTools.ExtractName(Stream));

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
            tempStream.WriteUInt16BE(Weight);

            // TARGET domain-name (variable, with compression)
            var targetOffset = (Int32) Stream.Position + 2 + (Int32) tempStream.Position;  // +2 for RDLength
            Target.ToString().Serialize(
                tempStream,
                targetOffset,
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

            => $"Priority={Priority}, Weight={Weight}, Target={Target}, {base.ToString()}";

        #endregion

    }

}
