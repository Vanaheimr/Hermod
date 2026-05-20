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
    /// An EDNS option (RFC 6891, Section 6.1.2).
    /// Serves as both a generic container and a base class for typed EDNS option subclasses.
    /// </summary>
    public class EDNSOption
    {

        #region Properties

        /// <summary>
        /// The EDNS option code.
        /// </summary>
        public UInt16  Code    { get; }

        /// <summary>
        /// The raw option data bytes.
        /// </summary>
        public Byte[]  Data    { get; }

        /// <summary>
        /// The length of the option data.
        /// </summary>
        public UInt16  Length
            => (UInt16) Data.Length;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new EDNS option with the given code and data.
        /// </summary>
        /// <param name="Code">The EDNS option code.</param>
        /// <param name="Data">The raw option data.</param>
        public EDNSOption(UInt16  Code,
                          Byte[]  Data)
        {
            this.Code = Code;
            this.Data = Data ?? throw new ArgumentNullException(nameof(Data), "Data cannot be null");
        }

        /// <summary>
        /// Create a new EDNS option with the given typed option code and data.
        /// </summary>
        /// <param name="Code">The EDNS option code.</param>
        /// <param name="Data">The raw option data.</param>
        public EDNSOption(EDNSOptionCode  Code,
                          Byte[]          Data)

            : this((UInt16) Code, Data)

        { }

        #endregion


        #region (static) Parse(Code, Data)

        /// <summary>
        /// Parse the given EDNS option code and data into a typed EDNS option subclass
        /// where possible, or return a generic EDNSOption for unknown codes.
        /// </summary>
        /// <param name="Code">The EDNS option code.</param>
        /// <param name="Data">The raw option data.</param>
        public static EDNSOption Parse(UInt16  Code,
                                       Byte[]  Data)

            => Code switch {

                   (UInt16) EDNSOptionCode.ClientSubnet      => EDNSClientSubnetOption.   Parse(Data),
                   (UInt16) EDNSOptionCode.Cookie             => EDNSCookieOption.         Parse(Data),
                   (UInt16) EDNSOptionCode.Keepalive          => EDNSKeepaliveOption.      Parse(Data),
                   (UInt16) EDNSOptionCode.Padding            => EDNSPaddingOption.        Parse(Data),
                   (UInt16) EDNSOptionCode.ExtendedDNSError   => EDNSExtendedDNSError.     Parse(Data),

                   _                                          => new EDNSOption(Code, Data)

               };

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this EDNS option.
        /// </summary>
        public override String ToString()

            => $"EDNS Option {Code} ({Length} bytes)";

        #endregion

    }

}
