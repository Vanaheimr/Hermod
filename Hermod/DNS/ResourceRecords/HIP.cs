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
    /// The DNS Host Identity Protocol (HIP) resource record (RFC 8005): a host's
    /// Host Identity Tag, its Host Identity — a public key — and the rendezvous
    /// servers it can be reached through.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two fields carry the lengths of two others, and the rendezvous servers
    /// carry none: §5.6 says the names are "concatenated", and since "the zero
    /// length label termination serves as a separator" and "the overall length of
    /// the RR's RDATA is also known (RDLENGTH), all the length information
    /// necessary to parse the HIP RR is available". So the HIT ends where its
    /// length says, the key where its length says, and the names run to the last
    /// octet of the RDATA — not one short of it and not past it.
    /// </para>
    /// <para>
    /// "The HIT length, PK algorithm, PK length, HIT, and Public Key fields are
    /// REQUIRED" (§5), so a HIT or a key of length zero is not a HIP, and BIND
    /// answers FORMERR for either. Such a record, and one whose lengths add up to
    /// more than its RDATA, is kept as the octets that arrived.
    /// </para>
    /// <para>
    /// The names "MUST NOT be compressed" (§5.6), and keep their case in the
    /// DNSSEC canonical form: HIP came after RFC 3597, whose §7 closed the list of
    /// types whose names are lowercased there.
    /// </para>
    /// </remarks>
    public class HIP : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS HIP resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.HIP;

        #endregion

        #region Properties

        /// <summary>
        /// The public key algorithm, in the numbering of IPSECKEY's algorithm type
        /// (RFC 8005 §5.2): 1 DSA, 2 RSA, 3 ECDSA.
        /// </summary>
        public Byte                       PKAlgorithm          { get; }

        /// <summary>
        /// The Host Identity Tag, as stored: "a binary value in network byte order"
        /// (RFC 8005 §5.4).
        /// </summary>
        public Byte[]                     HIT                  { get; }

        /// <summary>
        /// The Host Identity, a public key in the format the algorithm implies
        /// (RFC 8005 §5.5).
        /// </summary>
        public Byte[]                     PublicKey            { get; }

        /// <summary>
        /// The rendezvous servers, "listed in order of preference (i.e., the first
        /// RVS or servers are preferred)" (RFC 8005 §5.6). Possibly none.
        /// </summary>
        public IReadOnlyList<DomainName>  RendezvousServers    { get; }

        /// <summary>
        /// The RDATA exactly as it arrived, when it could not be read as a HIP.
        /// Null for every readable record. While it is set, the other properties
        /// carry nothing.
        /// </summary>
        public Byte[]?                    UnparsedRData        { get; }

        #endregion

        #region Constructor(s)

        #region HIP(DomainName, Stream)

        /// <summary>
        /// Create a new HIP resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this HIP resource record.</param>
        /// <param name="Stream">A stream containing the HIP resource record data.</param>
        public HIP(DomainName  DomainName,
                   Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength  = Stream.ReadUInt16BE();
            var rdata     = DNSTools.ExtractByteArray(Stream, rdLength);

            if (rdata.Length != rdLength)
                throw new InvalidDataException($"A HIP record claims {rdLength} RDATA octets but only {rdata.Length} were left in the message!");

            if (TryRead(rdata, out var pkAlgorithm, out var hit, out var publicKey, out var rendezvousServers))
            {
                this.PKAlgorithm        = pkAlgorithm;
                this.HIT                = hit;
                this.PublicKey          = publicKey;
                this.RendezvousServers  = rendezvousServers;
            }

            else
            {
                this.HIT                = [];
                this.PublicKey          = [];
                this.RendezvousServers  = [];
                this.UnparsedRData      = rdata;
            }

        }

        #endregion

        #region HIP(DomainName, Class, TimeToLive, PKAlgorithm, HIT, PublicKey, RendezvousServers = null)

        /// <summary>
        /// Create a new DNS HIP resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this HIP resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="PKAlgorithm">The public key algorithm.</param>
        /// <param name="HIT">The Host Identity Tag, one to 255 octets.</param>
        /// <param name="PublicKey">The Host Identity, one to 65535 octets.</param>
        /// <param name="RendezvousServers">The rendezvous servers, in order of preference.</param>
        public HIP(DomainName                 DomainName,
                   DNSQueryClasses            Class,
                   TimeSpan                   TimeToLive,
                   Byte                       PKAlgorithm,
                   Byte[]                     HIT,
                   Byte[]                     PublicKey,
                   IEnumerable<DomainName>?   RendezvousServers   = null)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            // The HIT length is one octet and the PK length two (§5.1, §5.3), and
            // both fields are REQUIRED (§5): a value outside those bounds has no
            // encoding at all.
            if (HIT.Length is < 1 or > Byte.MaxValue)
                throw new ArgumentException($"A HIT is one to {Byte.MaxValue} octets long, not {HIT.Length}!", nameof(HIT));

            if (PublicKey.Length is < 1 or > UInt16.MaxValue)
                throw new ArgumentException($"A HIP public key is one to {UInt16.MaxValue} octets long, not {PublicKey.Length}!", nameof(PublicKey));

            this.PKAlgorithm        = PKAlgorithm;
            this.HIT                = HIT;
            this.PublicKey          = PublicKey;
            this.RendezvousServers  = RendezvousServers?.ToArray() ?? [];

        }

        #endregion

        #endregion


        #region (private static) TryRead(RData, out PKAlgorithm, out HIT, out PublicKey, out RendezvousServers)

        /// <summary>
        /// Read the RDATA of RFC 8005 §5, or fail without guessing.
        /// </summary>
        private static Boolean TryRead(Byte[]                     RData,
                                       out Byte                   PKAlgorithm,
                                       out Byte[]                 HIT,
                                       out Byte[]                 PublicKey,
                                       out DomainName[]           RendezvousServers)
        {

            PKAlgorithm        = 0;
            HIT                = [];
            PublicKey          = [];
            RendezvousServers  = [];

            if (RData.Length < 4)
                return false;

            var hitLength  = RData[0];
            var pkLength   = (RData[2] << 8) | RData[3];

            if (hitLength == 0 || pkLength == 0)
                return false;

            var offset     = 4 + hitLength + pkLength;

            if (offset > RData.Length)
                return false;

            var servers    = new List<DomainName>();

            // §5.6: the names run to the end of the RDATA, each ended by its own
            // root label. One that runs past the end, or a pointer standing in for
            // its tail, leaves no boundary to find.
            while (offset < RData.Length)
            {

                if (!DNSTools.TryReadUncompressedName(RData, ref offset, out var server))
                    return false;

                servers.Add(server);

            }

            PKAlgorithm        = RData[1];
            HIT                = RData[4..(4 + hitLength)];
            PublicKey          = RData[(4 + hitLength)..(4 + hitLength + pkLength)];
            RendezvousServers  = [.. servers];

            return true;

        }

        #endregion

        #region (static) TryParseFromJSON(Name, TimeToLive, Data, Origin = null)

        /// <summary>
        /// Try to parse this resource record from its presentation form (RFC 8005
        /// §6): the algorithm, the HIT in hexadecimal, the public key in base-64,
        /// and the rendezvous servers, if any.
        /// </summary>
        /// <remarks>
        /// §6 makes whitespace the separator twice over: the HIT "MUST NOT contain
        /// whitespaces to distinguish it from the Public Key field", and the key
        /// "MUST NOT contain whitespace(s) to distinguish it from the Rendezvous
        /// Server field". So a key split across two tokens is not a long key but a
        /// short key and a server name — which is what BIND reads it as, too.
        /// </remarks>
        /// <param name="Name">The owner name of this resource record.</param>
        /// <param name="TimeToLive">The TTL of this resource record.</param>
        /// <param name="Data">The presentation form of the RDATA.</param>
        /// <param name="Origin">An optional origin for relative server names.</param>
        public static HIP? TryParseFromJSON(DomainName   Name,
                                            TimeSpan     TimeToLive,
                                            String       Data,
                                            DomainName?  Origin   = null)
        {

            var parts = Data.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 3)
                return null;

            // §6: "The PK algorithm field is represented as unsigned integers."
            if (!Byte.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var pkAlgorithm))
                return null;

            Byte[] hit;
            Byte[] publicKey;

            try
            {
                hit        = Convert.FromHexString   (parts[1]);
                publicKey  = Convert.FromBase64String(parts[2]);
            }
            catch
            {
                return null;
            }

            if (hit.Length       is < 1 or > Byte.MaxValue ||
                publicKey.Length is < 1 or > UInt16.MaxValue)
            {
                return null;
            }

            var servers = new List<DomainName>();

            foreach (var part in parts[3..])
            {
                try
                {
                    servers.Add(DNS.DomainName.ParseLenient(part, Origin));
                }
                catch
                {
                    return null;
                }
            }

            var record = new HIP(
                             Name,
                             DNSQueryClasses.IN,
                             TimeToLive,
                             pkAlgorithm,
                             hit,
                             publicKey,
                             servers
                         );

            // RDLENGTH is sixteen bits, and a key near its own limit leaves less
            // than that for the rest.
            return record.RDataLength() <= UInt16.MaxValue
                       ? record
                       : null;

        }

        #endregion

        #region (private) RDataLength()

        private Int32 RDataLength()

            => 4 + HIT.Length + PublicKey.Length + RendezvousServers.Sum(WireLength);

        private static Int32 WireLength(DomainName Name)
        {
            using var stream = new MemoryStream();
            Name.Serialize(stream, 0, false, null);
            return (Int32) stream.Length;
        }

        #endregion

        #region (protected override) ZoneFileRData()

        /// <inheritdoc/>
        protected override String ZoneFileRData()
        {

            if (UnparsedRData is not null)
                return UnknownRecord.GenericRData(UnparsedRData);

            // §6: the HIT in Base16, which BIND writes in uppercase, and the key
            // in Base64 — and neither length, since both are implied.
            var text = $"{PKAlgorithm} {Convert.ToHexString(HIT)} {Convert.ToBase64String(PublicKey)}";

            return RendezvousServers.Count > 0
                       ? $"{text} {String.Join(' ', RendezvousServers.Select(server => server.FullName))}"
                       : text;

        }

        #endregion

        #region (protected override) SerializeRRData(Stream, UseCompression = true, CompressionOffsets = null)

        /// <summary>
        /// Serialize the concrete DNS resource record to the given stream.
        /// </summary>
        /// <param name="Stream">The stream to write to.</param>
        /// <param name="UseCompression">Ignored: RFC 8005 §5.6 forbids compressing the server names.</param>
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

            tempStream.WriteByte    ((Byte) HIT.Length);
            tempStream.WriteByte    (PKAlgorithm);
            tempStream.WriteUInt16BE(PublicKey.Length);
            tempStream.Write        (HIT,       0, HIT.Length);
            tempStream.Write        (PublicKey, 0, PublicKey.Length);

            // §5.6: "The domain names MUST NOT be compressed."
            foreach (var server in RendezvousServers)
                server.Serialize(tempStream, 0, false, null);

            if (tempStream.Length > UInt16.MaxValue)
                throw new InvalidOperationException("RDATA exceeds maximum UInt16 length (65535 bytes)!");

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

            => $"{ZoneFileRData()}, {base.ToString()}";

        #endregion

    }

}
