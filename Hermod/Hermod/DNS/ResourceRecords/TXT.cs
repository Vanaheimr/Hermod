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

using System.Text;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Extensions methods for DNS TXT resource records.
    /// </summary>
    public static class DNS_TXT_Extensions
    {

        #region CacheTXT(this DNSClient, DomainName, RText, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS TXT record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">The domain name of this TXT resource record.</param>
        /// <param name="RText">The text of this DNS TXT resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheTXT(this DNSClient   DNSClient,
                                    DomainName       DomainName,
                                    String           RText,
                                    DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                    TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new TXT(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                RText
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS Text (TXT) resource record type identifier.
    /// </summary>
    public class TXT : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Text (TXT) resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.TXT;

        #endregion

        #region Properties

        /// <summary>
        /// The text of this DNS Text (TXT) resource record.
        /// </summary>
        public String  Text    { get; }

        #endregion

        #region Constructor

        #region TXT(Stream)

        /// <summary>
        /// Create a new TXT resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the TXT resource record data.</param>
        public TXT(Stream Stream)

            : base(Stream,
                   TypeId)

        {
            this.Text = DNSTools.ExtractName(Stream);
        }

        #endregion

        #region TXT(DomainName, Stream)

        /// <summary>
        /// Create a new TXT resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this TXT resource record.</param>
        /// <param name="Stream">A stream containing the TXT resource record data.</param>
        public TXT(DomainName  DomainName,
                   Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.Text = DNSTools.ExtractName(Stream);

        }

        #endregion

        #region TXT(DomainName, Class, TimeToLive, RText)

        /// <summary>
        /// Create a new DNS TXT resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this TXT resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="RText">The text of this DNS TXT resource record.</param>
        public TXT(DomainName       DomainName,
                   DNSQueryClasses  Class,
                   TimeSpan         TimeToLive,
                   String           RText)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive,
                   RText)

        {

            this.Text = RText;

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

            var tokens = Text.SubTokens(255).ToList();

            var dataLen = tokens.Sum(t => 1 + Encoding.ASCII.GetByteCount(t));
            if (dataLen > UInt16.MaxValue)
                throw new InvalidOperationException("RDATA exceeds maximum UInt16 length (65535 bytes)!");

            // RDLENGTH
            Stream.WriteUInt16BE(dataLen);

            foreach (var token in tokens)
                Stream.WriteASCIIMax255(token);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"{Text}, {base.ToString()}";

        #endregion

    }

}
