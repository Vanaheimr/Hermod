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
    /// Extended DNS Error info codes (RFC 8914, Section 4).
    /// </summary>
    public enum ExtendedDNSErrorCode : UInt16
    {

        /// <summary>Other (0) — unclassified error.</summary>
        Other                           =  0,

        /// <summary>Unsupported DNSKEY Algorithm (1).</summary>
        UnsupportedDNSKEYAlgorithm      =  1,

        /// <summary>Unsupported DS Digest Type (2).</summary>
        UnsupportedDSDigestType         =  2,

        /// <summary>Stale Answer (3) — answer from expired cache.</summary>
        StaleAnswer                     =  3,

        /// <summary>Forged Answer (4) — modified by middlebox.</summary>
        ForgedAnswer                    =  4,

        /// <summary>DNSSEC Indeterminate (5).</summary>
        DNSSECIndeterminate             =  5,

        /// <summary>DNSSEC Bogus (6) — validation failure.</summary>
        DNSSECBogus                     =  6,

        /// <summary>Signature Expired (7).</summary>
        SignatureExpired                 =  7,

        /// <summary>Signature Not Yet Valid (8).</summary>
        SignatureNotYetValid            =  8,

        /// <summary>DNSKEY Missing (9).</summary>
        DNSKEYMissing                   =  9,

        /// <summary>RRSIGs Missing (10).</summary>
        RRSIGsMissing                   = 10,

        /// <summary>No Zone Key Bit Set (11).</summary>
        NoZoneKeyBitSet                 = 11,

        /// <summary>NSEC Missing (12).</summary>
        NSECMissing                     = 12,

        /// <summary>Cached Error (13).</summary>
        CachedError                     = 13,

        /// <summary>Not Ready (14) — server not yet ready to serve.</summary>
        NotReady                        = 14,

        /// <summary>Blocked (15) — query blocked by policy.</summary>
        Blocked                         = 15,

        /// <summary>Censored (16) — answer censored by policy.</summary>
        Censored                        = 16,

        /// <summary>Filtered (17) — answer filtered by policy.</summary>
        Filtered                        = 17,

        /// <summary>Prohibited (18) — query prohibited.</summary>
        Prohibited                      = 18,

        /// <summary>Stale NXDOMAIN Answer (19).</summary>
        StaleNXDOMAINAnswer             = 19,

        /// <summary>Not Authoritative (20).</summary>
        NotAuthoritative                = 20,

        /// <summary>Not Supported (21).</summary>
        NotSupported                    = 21,

        /// <summary>No Reachable Authority (22).</summary>
        NoReachableAuthority            = 22,

        /// <summary>Network Error (23).</summary>
        NetworkError                    = 23,

        /// <summary>Invalid Data (24).</summary>
        InvalidData                     = 24,

        /// <summary>Signature Expired before Valid (25).</summary>
        SignatureExpiredBeforeValid      = 25,

        /// <summary>Too Early (26) — RFC 9461.</summary>
        TooEarly                        = 26,

        /// <summary>Unsupported NSEC3 Iterations Value (27).</summary>
        UnsupportedNSEC3IterationsValue = 27,

        /// <summary>Unable to conform to policy (28).</summary>
        UnableToConformToPolicy         = 28,

        /// <summary>Synthesized (29).</summary>
        Synthesized                     = 29

    }


    /// <summary>
    /// EDNS Extended DNS Error option (RFC 8914).
    /// Provides additional error information beyond the basic RCODE,
    /// e.g. DNSSEC validation failure details, stale data, policy blocks.
    ///
    /// Wire format:
    ///   +0 (2 bytes)   INFO-CODE
    ///   +2 (variable)  EXTRA-TEXT (UTF-8, optional)
    /// </summary>
    public class EDNSExtendedDNSError : EDNSOption
    {

        #region Properties

        /// <summary>
        /// The extended DNS error info code.
        /// </summary>
        public ExtendedDNSErrorCode  InfoCode     { get; }

        /// <summary>
        /// Optional human-readable UTF-8 error text.
        /// </summary>
        public String?               ExtraText    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new EDNS Extended DNS Error option.
        /// </summary>
        /// <param name="InfoCode">The extended DNS error info code.</param>
        /// <param name="ExtraText">Optional human-readable error text.</param>
        public EDNSExtendedDNSError(ExtendedDNSErrorCode  InfoCode,
                                    String?               ExtraText = null)

            : base(EDNSOptionCode.ExtendedDNSError,
                   Serialize(InfoCode, ExtraText))

        {
            this.InfoCode   = InfoCode;
            this.ExtraText  = ExtraText;
        }

        #endregion


        #region (private static) Serialize(...)

        private static Byte[] Serialize(ExtendedDNSErrorCode  InfoCode,
                                        String?               ExtraText)
        {

            var textBytes  = ExtraText is not null
                                 ? Encoding.UTF8.GetBytes(ExtraText)
                                 : [];

            var data    = new Byte[2 + textBytes.Length];
            data[0]     = (Byte) ((UInt16) InfoCode >> 8);
            data[1]     = (Byte) ((UInt16) InfoCode & 0xFF);

            if (textBytes.Length > 0)
                Array.Copy(textBytes, 0, data, 2, textBytes.Length);

            return data;

        }

        #endregion

        #region (static) Parse(Data)

        /// <summary>
        /// Parse an EDNS Extended DNS Error option from raw data bytes.
        /// </summary>
        /// <param name="Data">The raw option data.</param>
        public static EDNSExtendedDNSError Parse(Byte[] Data)
        {

            if (Data.Length < 2)
                throw new ArgumentException("EDNS Extended DNS Error option must be at least 2 bytes!", nameof(Data));

            var infoCode = (ExtendedDNSErrorCode) ((Data[0] << 8) | Data[1]);

            var extraText = Data.Length > 2
                                ? Encoding.UTF8.GetString(Data, 2, Data.Length - 2)
                                : null;

            return new EDNSExtendedDNSError(infoCode, extraText);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this EDNS Extended DNS Error option.
        /// </summary>
        public override String ToString()

            => ExtraText is not null
                   ? $"EDE {InfoCode} ({(UInt16) InfoCode}): {ExtraText}"
                   : $"EDE {InfoCode} ({(UInt16) InfoCode})";

        #endregion

    }

}
