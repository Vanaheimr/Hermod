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
    /// Extensions methods for DNS NSEC3 resource records.
    /// </summary>
    public static class DNS_NSEC3_Extensions
    {

        #region CacheNSEC3(this DNSClient, DomainName, HashAlgorithm, Flags, Iterations, Salt, NextHashedOwnerName, TypeBitMaps, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS NSEC3 record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">The domain name of this NSEC3 resource record.</param>
        /// <param name="HashAlgorithm">The hash algorithm used.</param>
        /// <param name="Flags">The NSEC3 flags.</param>
        /// <param name="Iterations">The number of additional hash iterations.</param>
        /// <param name="Salt">The salt value.</param>
        /// <param name="NextHashedOwnerName">The next hashed owner name.</param>
        /// <param name="TypeBitMaps">The type bit maps.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheNSEC3(this DNSClient   DNSClient,
                                      DomainName       DomainName,
                                      Byte             HashAlgorithm,
                                      Byte             Flags,
                                      UInt16           Iterations,
                                      Byte[]           Salt,
                                      Byte[]           NextHashedOwnerName,
                                      Byte[]           TypeBitMaps,
                                      DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                      TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new NSEC3(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                HashAlgorithm,
                                Flags,
                                Iterations,
                                Salt,
                                NextHashedOwnerName,
                                TypeBitMaps
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS NSEC3 resource record (RFC 5155).
    /// </summary>
    public class NSEC3 : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS NSEC3 resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.NSEC3;

        /// <summary>
        /// The only hash algorithm RFC 5155 defines: SHA-1 (IANA "DNSSEC NSEC3
        /// Hash Algorithms", value 1).
        /// </summary>
        public const Byte HashAlgorithmSHA1 = 1;

        /// <summary>
        /// The Base32hex alphabet of RFC 4648 §7, which RFC 5155 §1.3 mandates
        /// for hashed owner names. Note it is *not* the ordinary Base32 alphabet:
        /// this one preserves the sort order of the underlying bytes, which is
        /// what makes the NSEC3 chain orderable.
        /// </summary>
        private const String Base32HexAlphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUV";

        #endregion

        #region Properties

        /// <summary>
        /// The hash algorithm used.
        /// </summary>
        public Byte    HashAlgorithm           { get; }

        /// <summary>
        /// The NSEC3 flags.
        /// </summary>
        public Byte    Flags                   { get; }

        /// <summary>
        /// The number of additional hash iterations.
        /// </summary>
        public UInt16  Iterations              { get; }

        /// <summary>
        /// The salt value appended to the original owner name before hashing.
        /// </summary>
        public Byte[]  Salt                    { get; }

        /// <summary>
        /// The next hashed owner name in hash order.
        /// </summary>
        public Byte[]  NextHashedOwnerName     { get; }

        /// <summary>
        /// The type bit maps indicating which RR types exist at the original owner name.
        /// </summary>
        public Byte[]  TypeBitMaps             { get; }

        #endregion

        #region Constructor

        #region NSEC3(DomainName, Stream)

        /// <summary>
        /// Create a new NSEC3 resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this NSEC3 resource record.</param>
        /// <param name="Stream">A stream containing the NSEC3 resource record data.</param>
        public NSEC3(DomainName  DomainName,
                     Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength  = Stream.ReadUInt16BE();
            var startPos  = Stream.Position;

            this.HashAlgorithm        = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Flags                = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Iterations           = Stream.ReadUInt16BE();

            var saltLength            = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Salt                 = DNSTools.ExtractByteArray(Stream, saltLength);

            var hashLength            = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.NextHashedOwnerName  = DNSTools.ExtractByteArray(Stream, hashLength);

            var bytesRead             = (Int32) (Stream.Position - startPos);
            this.TypeBitMaps          = DNSTools.ExtractByteArray(Stream, (UInt32)(rdLength - bytesRead));

        }

        #endregion

        #region NSEC3(DomainName, Class, TimeToLive, HashAlgorithm, Flags, Iterations, Salt, NextHashedOwnerName, TypeBitMaps)

        /// <summary>
        /// Create a new DNS NSEC3 resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this NSEC3 resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="HashAlgorithm">The hash algorithm used.</param>
        /// <param name="Flags">The NSEC3 flags.</param>
        /// <param name="Iterations">The number of additional hash iterations.</param>
        /// <param name="Salt">The salt value.</param>
        /// <param name="NextHashedOwnerName">The next hashed owner name.</param>
        /// <param name="TypeBitMaps">The type bit maps.</param>
        public NSEC3(DomainName       DomainName,
                     DNSQueryClasses  Class,
                     TimeSpan         TimeToLive,
                     Byte             HashAlgorithm,
                     Byte             Flags,
                     UInt16           Iterations,
                     Byte[]           Salt,
                     Byte[]           NextHashedOwnerName,
                     Byte[]           TypeBitMaps)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.HashAlgorithm        = HashAlgorithm;
            this.Flags                = Flags;
            this.Iterations           = Iterations;
            this.Salt                 = Salt;
            this.NextHashedOwnerName  = NextHashedOwnerName;
            this.TypeBitMaps          = TypeBitMaps;

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
        public static NSEC3? TryParseFromJSON(DomainName Name, TimeSpan TimeToLive, String Data)
        {
            try
            {
                var parts = Data.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5) return null;
                var salt = parts[3] == "-" ? Array.Empty<Byte>() : Convert.FromHexString(parts[3]);
                var nextHash = Convert.FromHexString(parts[4]);
                return new NSEC3(Name, DNSQueryClasses.IN, TimeToLive,
                                 Byte.Parse(parts[0]), Byte.Parse(parts[1]), UInt16.Parse(parts[2]),
                                 salt, nextHash, EncodeTypeBitMaps(parts.Skip(5)));
            }
            catch { return null; }
        }

        #endregion

        #region (static) ComputeHash          (Name, Iterations, Salt, HashAlgorithm = SHA1)

        /// <summary>
        /// Hash an owner name the way RFC 5155 §5 defines it.
        /// </summary>
        /// <param name="Name">The owner name to hash.</param>
        /// <param name="Iterations">How many *additional* rounds to apply; 0 means a single hash.</param>
        /// <param name="Salt">The zone's salt, empty for none.</param>
        /// <param name="HashAlgorithm">The hash algorithm; only SHA-1 (1) is defined.</param>
        /// <returns>The raw hash, 20 octets for SHA-1.</returns>
        /// <remarks>
        /// RFC 5155 §5:
        /// <code>
        ///   IH(salt, x, 0) = H(x || salt)
        ///   IH(salt, x, k) = H(IH(salt, x, k-1) || salt), k > 0
        ///   H(name)        = IH(salt, name, iterations)
        /// </code>
        /// Two details are easy to get wrong and are worth stating. The salt is
        /// appended on *every* round, not only the first. And the input to round
        /// zero is the canonical wire form of the name — lowercased, length-
        /// prefixed, root-terminated — never its presentation text.
        /// </remarks>
        public static Byte[] ComputeHash(DomainName  Name,
                                         UInt16      Iterations,
                                         Byte[]      Salt,
                                         Byte        HashAlgorithm   = HashAlgorithmSHA1)
        {

            if (HashAlgorithm != HashAlgorithmSHA1)
                throw new NotSupportedException($"RFC 5155 defines only hash algorithm 1 (SHA-1); got {HashAlgorithm}.");

            var salt    = Salt ?? [];
            var buffer  = DNSTools.SerializeCanonicalName(Name.FullName);

            // Round zero, plus `Iterations` further rounds — so Iterations = 0
            // still hashes once. The count is of *extra* rounds, not of rounds.
            for (var round = 0; round <= Iterations; round++)
            {

                var input = new Byte[buffer.Length + salt.Length];
                Buffer.BlockCopy(buffer, 0, input, 0,             buffer.Length);
                Buffer.BlockCopy(salt,   0, input, buffer.Length, salt.Length);

                buffer = System.Security.Cryptography.SHA1.HashData(input);

            }

            return buffer;

        }

        #endregion

        #region (static) ComputeHashedOwnerName(Name, Zone, Iterations, Salt, HashAlgorithm = SHA1)

        /// <summary>
        /// Hash an owner name and place it under its zone, which is the form an
        /// NSEC3 record actually owns: the Base32hex of the hash as a single
        /// leftmost label, followed by the zone.
        /// </summary>
        /// <param name="Name">The owner name to hash.</param>
        /// <param name="Zone">The zone the NSEC3 record lives in.</param>
        /// <param name="Iterations">How many additional rounds to apply.</param>
        /// <param name="Salt">The zone's salt, empty for none.</param>
        /// <param name="HashAlgorithm">The hash algorithm; only SHA-1 (1) is defined.</param>
        public static DomainName ComputeHashedOwnerName(DomainName  Name,
                                                        DomainName  Zone,
                                                        UInt16      Iterations,
                                                        Byte[]      Salt,
                                                        Byte        HashAlgorithm   = HashAlgorithmSHA1)

            => DNS.DomainName.Parse(
                   $"{Base32HexEncode(ComputeHash(Name, Iterations, Salt, HashAlgorithm))}.{Zone.FullName.TrimStart('.')}"
               );

        #endregion

        #region (static) Base32HexEncode      (Data)

        /// <summary>
        /// Encode with the Base32hex alphabet of RFC 4648 §7, unpadded — the
        /// representation RFC 5155 §1.3 gives hashed owner names.
        /// </summary>
        /// <param name="Data">The octets to encode.</param>
        /// <remarks>
        /// Unpadded is not a stylistic choice: a SHA-1 hash is 160 bits, which is
        /// exactly 32 base-32 characters, so a conforming NSEC3 owner name never
        /// has padding to carry.
        /// </remarks>
        public static String Base32HexEncode(Byte[] Data)
        {

            if (Data.Length == 0)
                return "";

            var result     = new StringBuilder((Data.Length * 8 + 4) / 5);
            var buffer     = 0;
            var bitsInBuf  = 0;

            foreach (var octet in Data)
            {

                buffer     = (buffer << 8) | octet;
                bitsInBuf += 8;

                while (bitsInBuf >= 5)
                {
                    bitsInBuf -= 5;
                    result.Append(Base32HexAlphabet[(buffer >> bitsInBuf) & 0x1F]);
                }

            }

            // A trailing partial group is left-aligned, i.e. padded with zero bits.
            if (bitsInBuf > 0)
                result.Append(Base32HexAlphabet[(buffer << (5 - bitsInBuf)) & 0x1F]);

            return result.ToString();

        }

        #endregion

        #region (static) Base32HexDecode      (Text)

        /// <summary>
        /// Decode a Base32hex string produced by <see cref="Base32HexEncode"/>.
        /// Case-insensitive, since a hashed owner name is a domain name and
        /// RFC 4343 makes those case-insensitive on the wire.
        /// </summary>
        /// <param name="Text">The Base32hex text to decode.</param>
        public static Byte[] Base32HexDecode(String Text)
        {

            if (Text.Length == 0)
                return [];

            var result     = new List<Byte>(Text.Length * 5 / 8);
            var buffer     = 0;
            var bitsInBuf  = 0;

            foreach (var character in Text.ToUpperInvariant())
            {

                var value = Base32HexAlphabet.IndexOf(character);

                if (value < 0)
                    throw new FormatException($"'{character}' is not a Base32hex character.");

                buffer     = (buffer << 5) | value;
                bitsInBuf += 5;

                if (bitsInBuf >= 8)
                {
                    bitsInBuf -= 8;
                    result.Add((Byte) ((buffer >> bitsInBuf) & 0xFF));
                }

            }

            return [.. result];

        }

        #endregion

        #region (protected override) ZoneFileRData()

        /// <inheritdoc/>
        protected override String ZoneFileRData()
        {
            var saltHex     = Salt.Length > 0 ? Convert.ToHexString(Salt).ToLowerInvariant() : "-";
            var nextHashB32 = Convert.ToHexString(NextHashedOwnerName).ToLowerInvariant();
            return $"{HashAlgorithm} {Flags} {Iterations} {saltHex} {nextHashB32} {DecodeTypeBitMaps(TypeBitMaps)}";
        }

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

            // RDLENGTH (2 bytes): 4 (HashAlg + Flags + Iterations) + 1 (SaltLength) + Salt.Length + 1 (HashLength) + NextHashedOwnerName.Length + TypeBitMaps.Length
            Stream.WriteUInt16BE(4 + 1 + Salt.Length + 1 + NextHashedOwnerName.Length + TypeBitMaps.Length);

            Stream.WriteByte    (HashAlgorithm);
            Stream.WriteByte    (Flags);
            Stream.WriteUInt16BE(Iterations);

            Stream.WriteByte    ((Byte) Salt.Length);
            Stream.Write        (Salt, 0, Salt.Length);

            Stream.WriteByte    ((Byte) NextHashedOwnerName.Length);
            Stream.Write        (NextHashedOwnerName, 0, NextHashedOwnerName.Length);

            Stream.Write        (TypeBitMaps, 0, TypeBitMaps.Length);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"HashAlgorithm={HashAlgorithm}, Flags={Flags}, Iterations={Iterations}, Salt=[{Salt.Length} bytes], NextHashedOwnerName=[{NextHashedOwnerName.Length} bytes], TypeBitMaps=[{TypeBitMaps.Length} bytes], {base.ToString()}";

        #endregion

    }

}
