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

using System.Net;
using System.Net.Sockets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// EDNS Client Subnet option (RFC 7871).
    /// Allows a recursive resolver to forward a truncated client IP address
    /// to the authoritative server for geo-aware / CDN-optimized responses.
    ///
    /// Wire format:
    ///   +0 (2 bytes)  FAMILY        — 1 = IPv4, 2 = IPv6
    ///   +2 (1 byte)   SOURCE PREFIX-LENGTH
    ///   +3 (1 byte)   SCOPE PREFIX-LENGTH
    ///   +4 (variable) ADDRESS       — ceiling(SOURCE PREFIX-LENGTH / 8) bytes,
    ///                                  right-padded with zeroes to the byte boundary
    /// </summary>
    /// <remarks>See RFC 7871 for the EDNS Client Subnet specification.</remarks>
    public class EDNSClientSubnetOption : EDNSOption
    {

        #region Properties

        /// <summary>
        /// The address family: 1 = IPv4, 2 = IPv6.
        /// </summary>
        public UInt16           Family              { get; }

        /// <summary>
        /// The number of most-significant bits of the client address
        /// that the resolver is willing to reveal to the authority.
        /// </summary>
        public Byte             SourcePrefixLength  { get; }

        /// <summary>
        /// In a response, the authoritative server sets this to the
        /// prefix length it used to select the answer.  In a query
        /// this MUST be 0.
        /// </summary>
        public Byte             ScopePrefixLength   { get; }

        /// <summary>
        /// The client IP address, truncated to SourcePrefixLength bits.
        /// </summary>
        public System.Net.IPAddress  Address        { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new EDNS Client Subnet option.
        /// </summary>
        /// <param name="Family">The address family (1=IPv4, 2=IPv6).</param>
        /// <param name="SourcePrefixLength">The source prefix length.</param>
        /// <param name="ScopePrefixLength">The scope prefix length (0 for queries).</param>
        /// <param name="Address">The client address.</param>
        public EDNSClientSubnetOption(UInt16               Family,
                                      Byte                 SourcePrefixLength,
                                      Byte                 ScopePrefixLength,
                                      System.Net.IPAddress Address)

            : base(EDNSOptionCode.ClientSubnet,
                   Serialize(Family, SourcePrefixLength, ScopePrefixLength, Address))

        {
            this.Family              = Family;
            this.SourcePrefixLength  = SourcePrefixLength;
            this.ScopePrefixLength   = ScopePrefixLength;
            this.Address             = Address;
        }

        /// <summary>
        /// Create a new EDNS Client Subnet option from the given IP address and prefix length.
        /// The address family is auto-detected from the address type.
        /// </summary>
        /// <param name="Address">The client IP address.</param>
        /// <param name="SourcePrefixLength">The source prefix length (number of significant bits).</param>
        public EDNSClientSubnetOption(System.Net.IPAddress  Address,
                                      Byte                  SourcePrefixLength)

            : this(Address.AddressFamily == AddressFamily.InterNetwork
                       ? (UInt16) 1
                       : (UInt16) 2,
                   SourcePrefixLength,
                   0,
                   Address)

        { }

        #endregion


        #region (private static) Serialize(...)

        private static Byte[] Serialize(UInt16               Family,
                                        Byte                 SourcePrefixLength,
                                        Byte                 ScopePrefixLength,
                                        System.Net.IPAddress Address)
        {

            var addressBytes  = Address.GetAddressBytes();
            var addrLength    = (Int32) Math.Ceiling(SourcePrefixLength / 8.0);

            // Truncate address to SourcePrefixLength bits
            var truncated = new Byte[addrLength];
            Array.Copy(addressBytes, truncated, Math.Min(addressBytes.Length, addrLength));

            // Zero out trailing bits beyond SourcePrefixLength
            if (addrLength > 0)
            {
                var trailingBits = SourcePrefixLength % 8;
                if (trailingBits != 0)
                    truncated[addrLength - 1] &= (Byte) (0xFF << (8 - trailingBits));
            }

            var data = new Byte[4 + addrLength];
            data[0] = (Byte) (Family >> 8);
            data[1] = (Byte) (Family & 0xFF);
            data[2] = SourcePrefixLength;
            data[3] = ScopePrefixLength;
            Array.Copy(truncated, 0, data, 4, addrLength);

            return data;

        }

        #endregion

        #region (static) Parse(Data)

        /// <summary>
        /// Parse an EDNS Client Subnet option from raw data bytes.
        /// </summary>
        /// <param name="Data">The raw option data.</param>
        public new static EDNSClientSubnetOption Parse(Byte[] Data)
        {

            if (Data.Length < 4)
                throw new ArgumentException("EDNS Client Subnet option data must be at least 4 bytes!", nameof(Data));

            var family              = (UInt16) ((Data[0] << 8) | Data[1]);
            var sourcePrefixLength  = Data[2];
            var scopePrefixLength   = Data[3];

            var addrLength          = (Int32) Math.Ceiling(sourcePrefixLength / 8.0);

            // Reconstruct full-length address (padded with zeroes)
            var fullLength          = family == 1 ? 4 : 16;
            var addressBytes        = new Byte[fullLength];
            Array.Copy(Data, 4, addressBytes, 0, Math.Min(addrLength, Data.Length - 4));

            return new EDNSClientSubnetOption(
                       family,
                       sourcePrefixLength,
                       scopePrefixLength,
                       new System.Net.IPAddress(addressBytes)
                   );

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this EDNS Client Subnet option.
        /// </summary>
        public override String ToString()

            => $"ECS {Address}/{SourcePrefixLength} (scope {ScopePrefixLength}, family {Family})";

        #endregion

    }

}
