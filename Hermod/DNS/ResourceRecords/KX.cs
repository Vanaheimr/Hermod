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
    /// Extensions methods for DNS KX resource records.
    /// </summary>
    public static class DNS_KX_Extensions
    {

        #region CacheKX(this DNSClient, DomainName, Preference, Exchanger, ...)

        /// <summary>
        /// Add a DNS KX record cache entry.
        /// </summary>
        public static void CacheKX(this DNSClient   DNSClient,
                                   DomainName       DomainName,
                                   UInt16           Preference,
                                   DomainName       Exchanger,
                                   DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                   TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new KX(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromHours(2),
                                Preference,
                                Exchanger
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS Key Exchanger (KX) resource record (RFC 2230), which names a host
    /// that will do the key exchange for the owner name — "modeled on the MX
    /// record", a preference and a name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The shape is MX's; the rules around it are not. §3.1: "The KX RDATA field
    /// MUST NOT be compressed", where RFC 1035 lets an MX be compressed — so the
    /// exchanger is written uncompressed whatever the message around it holds,
    /// and a compression pointer in a KX that arrives is not followed but makes
    /// the record unreadable, which is what BIND makes of it too.
    /// </para>
    /// <para>
    /// The other half of what sets it apart from MX lives in DNSSEC. RFC 2230
    /// predates RFC 3597, so KX is on the list RFC 3597 §7 keeps unchanged: the
    /// canonical form of a KX lowercases its exchanger (RFC 4034 §6.2), as it does
    /// an MX's — and unlike the names of HIP and LP, which came after it.
    /// </para>
    /// </remarks>
    public class KX : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS KX resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.KX;

        #endregion

        #region Properties

        /// <summary>
        /// The preference of this key exchanger among the KX records of the same
        /// owner; "Lower values are preferred" (RFC 2230 §3.1).
        /// </summary>
        public UInt16      Preference       { get; }

        /// <summary>
        /// The host willing to act as key exchanger for the owner name.
        /// </summary>
        public DomainName  Exchanger        { get; }

        /// <summary>
        /// The RDATA exactly as it arrived, when it could not be read as a KX —
        /// shorter than a preference and a name, a name that does not end where
        /// the RDATA does, or a compressed one. Null for every readable record.
        /// </summary>
        /// <remarks>
        /// RFC 3597 §2: an unreadable record is still a record, and keeping its
        /// octets is what lets it be written back, and shown in the generic form
        /// of RFC 3597 §5, without a preference or a name being made up for it.
        /// While it is set, <see cref="Preference"/> and <see cref="Exchanger"/>
        /// carry nothing.
        /// </remarks>
        public Byte[]?     UnparsedRData    { get; }

        #endregion

        #region Constructor(s)

        #region KX(DomainName, Stream)

        /// <summary>
        /// Create a new KX resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this KX resource record.</param>
        /// <param name="Stream">A stream containing the KX resource record data.</param>
        public KX(DomainName  DomainName,
                  Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength  = Stream.ReadUInt16BE();
            var rdata     = DNSTools.ExtractByteArray(Stream, rdLength);

            if (rdata.Length != rdLength)
                throw new InvalidDataException($"A KX record claims {rdLength} RDATA octets but only {rdata.Length} were left in the message!");

            var offset    = 2;

            if (rdata.Length >= 3 &&
                DNSTools.TryReadUncompressedName(rdata, ref offset, out var exchanger) &&
                offset == rdata.Length)
            {
                this.Preference     = (UInt16) ((rdata[0] << 8) | rdata[1]);
                this.Exchanger      = exchanger;
            }

            else
            {
                this.Exchanger      = DNS.DomainName.Parse(".");
                this.UnparsedRData  = rdata;
            }

        }

        #endregion

        #region KX(DomainName, Class, TimeToLive, Preference, Exchanger)

        /// <summary>
        /// Create a new DNS KX resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this KX resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="Preference">The preference of this key exchanger.</param>
        /// <param name="Exchanger">The host willing to act as key exchanger.</param>
        public KX(DomainName       DomainName,
                  DNSQueryClasses  Class,
                  TimeSpan         TimeToLive,
                  UInt16           Preference,
                  DomainName       Exchanger)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.Preference  = Preference;
            this.Exchanger   = Exchanger;

        }

        #endregion

        #endregion


        #region (static) TryParseFromJSON(Name, TimeToLive, Data, Origin = null)

        /// <summary>
        /// Try to parse this resource record from its presentation form,
        /// "&lt;preference&gt; &lt;domain-name&gt;" (RFC 2230 §3).
        /// </summary>
        /// <param name="Name">The owner name of this resource record.</param>
        /// <param name="TimeToLive">The TTL of this resource record.</param>
        /// <param name="Data">The presentation form of the RDATA.</param>
        /// <param name="Origin">An optional origin for a relative exchanger name.</param>
        public static KX? TryParseFromJSON(DomainName   Name,
                                           TimeSpan     TimeToLive,
                                           String       Data,
                                           DomainName?  Origin   = null)
        {

            var parts = Data.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
                return null;

            // §3: "a non-negative integer", and §3.1 gives it sixteen bits — so
            // decimal digits only, and nothing past 65535.
            if (!UInt16.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var preference))
                return null;

            try
            {
                return new KX(
                           Name,
                           DNSQueryClasses.IN,
                           TimeToLive,
                           preference,
                           DNS.DomainName.ParseLenient(parts[1], Origin)
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

            => UnparsedRData is not null
                   ? UnknownRecord.GenericRData(UnparsedRData)
                   : $"{Preference} {Exchanger.FullName}";

        #endregion

        #region (protected override) SerializeRRData(Stream, UseCompression = true, CompressionOffsets = null)

        /// <summary>
        /// Serialize the concrete DNS resource record to the given stream.
        /// </summary>
        /// <param name="Stream">The stream to write to.</param>
        /// <param name="UseCompression">Ignored: RFC 2230 §3.1 forbids compressing the exchanger.</param>
        /// <param name="CompressionOffsets">Ignored, for the same reason.</param>
        protected override void SerializeRRData(Stream                      Stream,
                                                Boolean                     UseCompression       = true,
                                                Dictionary<String, Int32>?  CompressionOffsets   = null)
        {

            if (UnparsedRData is not null)
            {
                Stream.WriteUInt16BE(UnparsedRData.Length);
                Stream.Write(UnparsedRData, 0, UnparsedRData.Length);
                return;
            }

            var tempStream = new MemoryStream();

            tempStream.WriteUInt16BE(Preference);

            // §3.1: "The KX RDATA field MUST NOT be compressed." Not merely left
            // uncompressed when the caller asks for none — a message that already
            // holds the exchanger's name is exactly where a compressing writer
            // would point at it.
            Exchanger.Serialize(
                tempStream,
                0,
                false,
                null
            );

            // Two octets and a name of at most 255: RDLENGTH cannot overflow here.
            Stream.WriteUInt16BE(tempStream.Length);

            tempStream.Position = 0;
            tempStream.CopyTo(Stream);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"Preference: {Preference}, Exchanger: {Exchanger}, {base.ToString()}";

        #endregion

    }

}
