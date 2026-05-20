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

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Extensions methods for DNS NSEC3PARAM resource records.
    /// </summary>
    public static class DNS_NSEC3PARAM_Extensions
    {

        #region CacheNSEC3PARAM(this DNSClient, DomainName, HashAlgorithm, Flags, Iterations, Salt, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS NSEC3PARAM record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">The domain name of this NSEC3PARAM resource record.</param>
        /// <param name="HashAlgorithm">The hash algorithm used.</param>
        /// <param name="Flags">The NSEC3PARAM flags.</param>
        /// <param name="Iterations">The number of additional hash iterations.</param>
        /// <param name="Salt">The salt value.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheNSEC3PARAM(this DNSClient   DNSClient,
                                           DomainName       DomainName,
                                           Byte             HashAlgorithm,
                                           Byte             Flags,
                                           UInt16           Iterations,
                                           Byte[]           Salt,
                                           DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                           TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new NSEC3PARAM(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                HashAlgorithm,
                                Flags,
                                Iterations,
                                Salt
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS NSEC3 Parameters (NSEC3PARAM) resource record (RFC 5155).
    /// </summary>
    public class NSEC3PARAM : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS NSEC3 Parameters (NSEC3PARAM) resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.NSEC3PARAM;

        #endregion

        #region Properties

        /// <summary>
        /// The hash algorithm used.
        /// </summary>
        public Byte    HashAlgorithm    { get; }

        /// <summary>
        /// The NSEC3PARAM flags.
        /// </summary>
        public Byte    Flags            { get; }

        /// <summary>
        /// The number of additional hash iterations.
        /// </summary>
        public UInt16  Iterations       { get; }

        /// <summary>
        /// The salt value appended to the original owner name before hashing.
        /// </summary>
        public Byte[]  Salt             { get; }

        #endregion

        #region Constructor

        #region NSEC3PARAM(Stream)

        /// <summary>
        /// Create a new NSEC3PARAM resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the NSEC3PARAM resource record data.</param>
        public NSEC3PARAM(Stream  Stream)

            : base(Stream,
                   TypeId)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.HashAlgorithm  = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Flags          = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Iterations     = Stream.ReadUInt16BE();

            var saltLength      = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Salt           = DNSTools.ExtractByteArray(Stream, saltLength);

        }

        #endregion

        #region NSEC3PARAM(DomainName, Stream)

        /// <summary>
        /// Create a new NSEC3PARAM resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this NSEC3PARAM resource record.</param>
        /// <param name="Stream">A stream containing the NSEC3PARAM resource record data.</param>
        public NSEC3PARAM(DomainName  DomainName,
                          Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.HashAlgorithm  = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Flags          = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Iterations     = Stream.ReadUInt16BE();

            var saltLength      = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Salt           = DNSTools.ExtractByteArray(Stream, saltLength);

        }

        #endregion

        #region NSEC3PARAM(DomainName, Class, TimeToLive, HashAlgorithm, Flags, Iterations, Salt)

        /// <summary>
        /// Create a new DNS NSEC3PARAM resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this NSEC3PARAM resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="HashAlgorithm">The hash algorithm used.</param>
        /// <param name="Flags">The NSEC3PARAM flags.</param>
        /// <param name="Iterations">The number of additional hash iterations.</param>
        /// <param name="Salt">The salt value.</param>
        public NSEC3PARAM(DomainName       DomainName,
                          DNSQueryClasses  Class,
                          TimeSpan         TimeToLive,
                          Byte             HashAlgorithm,
                          Byte             Flags,
                          UInt16           Iterations,
                          Byte[]           Salt)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.HashAlgorithm  = HashAlgorithm;
            this.Flags          = Flags;
            this.Iterations     = Iterations;
            this.Salt           = Salt;

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
        public static NSEC3PARAM? TryParseFromJSON(DomainName Name, TimeSpan TimeToLive, String Data)
        {
            try
            {
                var parts = Data.Split(' ', 4);
                if (parts.Length < 4) return null;
                var salt = parts[3] == "-" ? Array.Empty<Byte>() : Convert.FromHexString(parts[3]);
                return new NSEC3PARAM(Name, DNSQueryClasses.IN, TimeToLive,
                                      Byte.Parse(parts[0]), Byte.Parse(parts[1]), UInt16.Parse(parts[2]), salt);
            }
            catch { return null; }
        }

        #endregion

        #region (protected override) ZoneFileRData()

        /// <inheritdoc/>
        protected override String ZoneFileRData()
        {
            var saltHex = Salt.Length > 0 ? Convert.ToHexString(Salt).ToLowerInvariant() : "-";
            return $"{HashAlgorithm} {Flags} {Iterations} {saltHex}";
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

            // RDLENGTH (2 bytes): 4 (HashAlg + Flags + Iterations) + 1 (SaltLength) + Salt.Length
            Stream.WriteUInt16BE(4 + 1 + Salt.Length);

            Stream.WriteByte    (HashAlgorithm);
            Stream.WriteByte    (Flags);
            Stream.WriteUInt16BE(Iterations);

            Stream.WriteByte    ((Byte) Salt.Length);
            Stream.Write        (Salt, 0, Salt.Length);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"HashAlgorithm={HashAlgorithm}, Flags={Flags}, Iterations={Iterations}, Salt=[{Salt.Length} bytes], {base.ToString()}";

        #endregion

    }

}
