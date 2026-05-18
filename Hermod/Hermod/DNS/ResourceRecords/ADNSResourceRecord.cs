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

        #region (protected abstract) serializeRRData(Stream)

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
