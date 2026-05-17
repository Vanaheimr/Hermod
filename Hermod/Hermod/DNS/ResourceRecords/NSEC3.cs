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

        #region NSEC3(Stream)

        /// <summary>
        /// Create a new NSEC3 resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the NSEC3 resource record data.</param>
        public NSEC3(Stream  Stream)

            : base(Stream,
                   TypeId)

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
