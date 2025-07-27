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

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    #region (enum) SSHFP_Algorithm

    /// <summary>
    /// The algorithm used for the SSH public key.
    /// https://www.rfc-editor.org/rfc/rfc4255
    /// </summary>
    public enum SSHFP_Algorithm : Byte
    {
        reserved  = 0,
        RSA       = 1,
        DSS       = 2,
        ECDSA     = 3
    }

    #endregion

    #region (enum) SSHFP_FingerprintType

    /// <summary>
    /// The fingerprint type used for the SSH public key.
    /// https://www.rfc-editor.org/rfc/rfc4255
    /// </summary>
    public enum SSHFP_FingerprintType : Byte
    {
        reserved  = 0,
        SHA1      = 1,
        SHA256    = 2
    }

    #endregion


    /// <summary>
    /// Extensions methods for DNS SSHFP resource records.
    /// </summary>
    public static class DNS_SSHFP_Extensions
    {

        #region CacheSSHFP(this DNSClient, DomainName, Algorithm, Type, Fingerprint, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS SSHFP record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">The domain name of this SSHFP resource record.</param>
        /// <param name="Algorithm">The SSH Public Key Fingerprint algorithm.</param>
        /// <param name="Type">The SSH Public Key Fingerprint type.</param>
        /// <param name="Fingerprint">The SSH Public Key Fingerprint.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheSSHFP(this DNSClient         DNSClient,
                                      DomainName             DomainName,
                                      SSHFP_Algorithm        Algorithm,
                                      SSHFP_FingerprintType  Type,
                                      Byte[]                 Fingerprint,
                                      DNSQueryClasses        Class        = DNSQueryClasses.IN,
                                      TimeSpan?              TimeToLive   = null)
        {

            var dnsRecord = new SSHFP(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                Algorithm,
                                Type,
                                Fingerprint
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS SSH Public Key Fingerprint (SSHFP) resource record.
    /// https://www.rfc-editor.org/rfc/rfc4255
    /// </summary>
    public class SSHFP : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS SSH Public Key Fingerprint (SSHFP) resource record type identifier.
        /// </summary>
        public const DNSResourceRecordType TypeId = DNSResourceRecordType.SSHFP;

        #endregion

        #region Properties

        /// <summary>
        /// The SSH Public Key Fingerprint algorithm.
        /// </summary>
        public SSHFP_Algorithm        FingerprintAlgorithm    { get; }

        /// <summary>
        /// The SSH Public Key Fingerprint type.
        /// </summary>
        public SSHFP_FingerprintType  FingerprintType         { get; }

        /// <summary>
        /// The SSH Public Key Fingerprint.
        /// </summary>
        public Byte[]                 Fingerprint             { get; }

        #endregion

        #region Constructor

        #region SSHFP(Stream)

        /// <summary>
        /// Create a new SSHFP resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the SSHFP resource record data.</param>
        public SSHFP(Stream  Stream)

            : base(Stream,
                   TypeId)

        {

            this.FingerprintAlgorithm  = ParseAlgorithm      (Stream);

            this.FingerprintType       = ParseFingerprintType(Stream);

            this.Fingerprint           = FingerprintType switch {
                                             SSHFP_FingerprintType.SHA1    => DNSTools.ExtractByteArray(Stream, 20),
                                             SSHFP_FingerprintType.SHA256  => DNSTools.ExtractByteArray(Stream, 32),
                                             _                             => throw new Exception($"Unknown SSHFP fingerprint type '{Type}'!")
                                         };

            if (FingerprintType == SSHFP_FingerprintType.SHA1   && Fingerprint.Length != 40)
                throw new ArgumentException($"Invalid SHA1 fingerprint length: {Fingerprint.Length} (40)!");

            if (FingerprintType == SSHFP_FingerprintType.SHA256 && Fingerprint.Length != 64)
                throw new ArgumentException($"Invalid SHA256 fingerprint length: {Fingerprint.Length} (64)!");

        }

        #endregion

        #region SSHFP(DomainName, Stream)

        /// <summary>
        /// Create a new SSHFP resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this SSHFP resource record.</param>
        /// <param name="Stream">A stream containing the SSHFP resource record data.</param>
        public SSHFP(DomainName  DomainName,
                     Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            this.FingerprintAlgorithm  = ParseAlgorithm      (Stream);

            this.FingerprintType       = ParseFingerprintType(Stream);

            this.Fingerprint           = FingerprintType switch {
                                             SSHFP_FingerprintType.SHA1    => DNSTools.ExtractByteArray(Stream, 20),
                                             SSHFP_FingerprintType.SHA256  => DNSTools.ExtractByteArray(Stream, 32),
                                             _                             => throw new Exception($"Unknown SSHFP fingerprint type '{Type}'!")
                                         };

            if (FingerprintType == SSHFP_FingerprintType.SHA1   && Fingerprint.Length != 40)
                throw new ArgumentException($"Invalid SHA1 fingerprint length: {Fingerprint.Length} (40)!");

            if (FingerprintType == SSHFP_FingerprintType.SHA256 && Fingerprint.Length != 64)
                throw new ArgumentException($"Invalid SHA256 fingerprint length: {Fingerprint.Length} (64)!");

        }

        #endregion

        #region SSHFP(DomainName, Class, TimeToLive, Algorithm, Type, Fingerprint)

        /// <summary>
        /// Create a new SSHFP resource record with the given parameters.
        /// </summary>
        /// <param name="DomainName">The domain name of this SSHFP resource record.</param>
        /// <param name="Class">The DNS query class of this SSHFP resource record.</param>
        /// <param name="TimeToLive">The time to live of this SSHFP resource record.</param>
        /// <param name="Algorithm">The SSH Public Key Fingerprint algorithm.</param>
        /// <param name="Type">The SSH Public Key Fingerprint type.</param>
        /// <param name="Fingerprint">The SSH Public Key Fingerprint.</param>
        public SSHFP(DomainName             DomainName,
                     DNSQueryClasses        Class,
                     TimeSpan               TimeToLive,
                     SSHFP_Algorithm        Algorithm,
                     SSHFP_FingerprintType  Type,
                     Byte[]                 Fingerprint)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive,
                   $"{Fingerprint} ({Algorithm}, {Type})")

        {



            this.FingerprintAlgorithm  = Algorithm;
            this.FingerprintType       = Type;
            this.Fingerprint           = Fingerprint;

            if (FingerprintType == SSHFP_FingerprintType.SHA1   && Fingerprint.Length != 20)
                throw new ArgumentException($"Invalid SHA1 fingerprint length: {Fingerprint.Length} (40)!");

            if (FingerprintType == SSHFP_FingerprintType.SHA256 && Fingerprint.Length != 32)
                throw new ArgumentException($"Invalid SHA256 fingerprint length: {Fingerprint.Length} (64)!");

        }

        #endregion

        #endregion


        #region (private) ParseAlgorithm       (Stream)

        private static SSHFP_Algorithm ParseAlgorithm(Stream Stream)
        {

            var algorithm = (SSHFP_Algorithm) (Stream.ReadByte() & Byte.MaxValue);

            if (!Enum.IsDefined(algorithm))
                throw new InvalidDataException($"Invalid SSHFP algorithm: {algorithm}");

            return algorithm;

        }

        #endregion

        #region (private) ParseFingerprintType (Stream)

        private static SSHFP_FingerprintType ParseFingerprintType(Stream Stream)
        {

            var type = (SSHFP_FingerprintType) (Stream.ReadByte() & Byte.MaxValue);

            if (!Enum.IsDefined(type))
                throw new InvalidDataException($"Invalid SSHFP fingerprint type: {type}");

            return type;

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

            // RDLENGTH (2 bytes): 2 (algorithm + type) + Fingerprint.Length
            Stream.WriteUInt16BE(2 + Fingerprint.Length);

            Stream.WriteByte((Byte) FingerprintAlgorithm);
            Stream.WriteByte((Byte) FingerprintType);

            Stream.Write    (Fingerprint, 0, Fingerprint.Length);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"{Fingerprint} (Algorithm={FingerprintAlgorithm}, Type={FingerprintType}), {base.ToString()}";

        #endregion

    }

}
