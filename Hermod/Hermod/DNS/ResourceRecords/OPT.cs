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
    /// The DNS OPT (pseudo) resource record for DNS Extension Mechanisms (EDNS)
    /// https://www.rfc-editor.org/rfc/rfc6891
    /// </summary>
    public class OPT : IDNSPseudoResourceRecord, IDNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS OPT resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.OPT;

        public DNSResourceRecordTypes Type
            => TypeId;

        #endregion

        #region Properties

        public UInt16                   UDPPayloadSize    { get; }  // Stored in Class field
        public Byte                     ExtendedRCODE     { get; }
        public Byte                     Version           { get; }
        public UInt16                   Flags             { get; }
        public IEnumerable<EDNSOption>  Options           { get; }



        public DNSQueryClasses          Class             { get; }

        public DNSServiceName           DomainName  => throw new NotImplementedException();

        public DateTimeOffset           EndOfLife         { get; }

        public String?                  RText       => throw new NotImplementedException();

        public IIPAddress?              Source      => throw new NotImplementedException();

        public TimeSpan                 TimeToLive        { get; }

        #endregion

        #region Constructor

        #region OPT(Stream)

        /// <summary>
        /// Create a new OPT resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the OPT resource record data.</param>
        public OPT(Stream Stream)

            //: base(Stream,
            //       TypeId)

        {

            //this.IPv6Address = new IPv6Address(Stream);

        }

        #endregion

        #region OPT(DomainName Stream)

        public OPT(DomainName       DomainName,
                   DNSQueryClasses  Class,
                   UInt32           TTL,
                   Stream           Stream)

            //: this(DNSService.Parse(DomainName.FullName),
            //       Stream)

        { }


        /// <summary>
        /// Create a new OPT resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this OPT resource record.</param>
        /// <param name="Stream">A stream containing the OPT resource record data.</param>
        public OPT(DNSServiceName   DomainName,
                   //DNSQueryClasses  Class,
                   //UInt32           TTL,
                   Stream           Stream)

            //: base(DomainName,
            //       TypeId,
            //       Stream)

        {

            this.Class       = (DNSQueryClasses) Stream.ReadUInt16BE();
            var timeToLife   = Stream.ReadUInt32BE();
            this.TimeToLive  = TimeSpan.FromSeconds(timeToLife);
            //this.TimeToLive  = TimeSpan.FromSeconds((DNSStream.ReadByte() & Byte.MaxValue) << 24 | (DNSStream.ReadByte() & Byte.MaxValue) << 16 | (DNSStream.ReadByte() & Byte.MaxValue) << 8 | DNSStream.ReadByte() & Byte.MaxValue);
            this.EndOfLife   = Timestamp.Now + TimeToLive;

            //this.IPv6Address = new IPv6Address(Stream);

        }

        #endregion

        #region OPT(UDPPayloadSize, ExtendedRCODE, Version, Flags, Options = null)

        /// <summary>
        /// Create a new DNS OPT resource record.
        /// </summary>
        /// <param name="UDPPayloadSize">The UDP payload size.</param>
        /// <param name="ExtendedRCODE">The extended RCODE of this OPT resource record.</param>
        /// <param name="Version">The version of this OPT resource record.</param>
        /// <param name="Flags">The flags of this OPT resource record.</param>
        /// <param name="Options">An optional enumeration of EDNS options.</param>
        public OPT(UInt16                    UDPPayloadSize,
                   Byte                      ExtendedRCODE,
                   Byte                      Version,
                   UInt16                    Flags,
                   IEnumerable<EDNSOption>?  Options   = null)
        {

            this.UDPPayloadSize  = UDPPayloadSize;
            this.ExtendedRCODE   = ExtendedRCODE;
            this.Version         = Version;
            this.Flags           = Flags;
            this.Options         = Options ?? [];

        }

        #endregion

        #endregion



        /// <summary>
        /// Serialize the OPT RR to a byte array for inclusion in a DNS packet.
        /// </summary>
        /// <returns>The serialized byte array representing the OPT RR.</returns>
        public Byte[] Serialize()
        {

            var rrBytes = new List<byte>();

            // Name: Root domain (empty label followed by 0)
            rrBytes.Add(0x00);  // End of domain name

            // Type: OPT (41)
            rrBytes.Add((byte)(41 >> 8));  // 0x00
            rrBytes.Add((byte)(41 & 0xFF));  // 0x29

            // Class: UDP payload size
            rrBytes.Add((byte) (UDPPayloadSize >> 8));
            rrBytes.Add((byte) (UDPPayloadSize & 0xFF));

            // TTL: Encoded extended RCODE, version, flags
            rrBytes.Add(ExtendedRCODE);
            rrBytes.Add(Version);

            rrBytes.Add((byte)(Flags >> 8));
            rrBytes.Add((byte)(Flags & 0xFF));

            // RDLength: Total length of options
            UInt16 rdLength = (UInt16)Options.Sum(opt => 4 + opt.Length);
            rrBytes.Add((byte)(rdLength >> 8));
            rrBytes.Add((byte)(rdLength & 0xFF));

            foreach (var opt in Options)
            {

                rrBytes.Add((byte) (opt.Code   >>    8));
                rrBytes.Add((byte) (opt.Code   &  0xFF));

                rrBytes.Add((byte) (opt.Length >>    8));
                rrBytes.Add((byte) (opt.Length &  0xFF));

                rrBytes.AddRange(opt.Data);

            }

            return rrBytes.ToArray();

        }

        public void Serialize(Stream                      stream,
                              Boolean                     UseCompression       = true,
                              Dictionary<String, Int32>?  CompressionOffsets   = null)
        {

            // Name: Root domain (empty label followed by 0)
            stream.WriteByte(0x00);  // End of domain name

            // Type: OPT (41)
            stream.WriteByte((Byte) (41 >>    8));  // 0x00
            stream.WriteByte((Byte) (41 &  0xFF));  // 0x29

            // Class: UDP payload size
            stream.WriteByte((Byte) (UDPPayloadSize >> 8));
            stream.WriteByte((Byte) (UDPPayloadSize & 0xFF));

            // TTL: Encoded extended RCODE, version, flags
            stream.WriteByte(ExtendedRCODE);
            stream.WriteByte(Version);

            stream.WriteByte((Byte) (Flags >> 8));
            stream.WriteByte((Byte) (Flags & 0xFF));

            // RDLength: Total length of options
            UInt16 rdLength = (UInt16) Options.Sum(opt => 4 + opt.Length);
            stream.WriteByte((Byte)  (rdLength >> 8));
            stream.WriteByte((Byte)  (rdLength & 0xFF));

            foreach (var opt in Options)
            {

                stream.WriteByte((Byte) (opt.Code >> 8));
                stream.WriteByte((Byte) (opt.Code & 0xFF));

                stream.WriteByte((Byte) (opt.Length >> 8));
                stream.WriteByte((Byte) (opt.Length & 0xFF));

                stream.Write(opt.Data, 0, opt.Data.Length);

            }

        }



        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"{ExtendedRCODE} {Version} {Flags} {Options.Count()} options, {base.ToString()}";

        #endregion

    }

}
