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

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// IP address helpers.
    /// </summary>
    public static class IPAddressHelper
    {

        #region (static) Build(ByteArray)

        /// <summary>
        /// Create a new IIPAddress based on the given byte array representation.
        /// </summary>
        /// <param name="ByteArray">A byte representation of an IPAddress.</param>
        public static IIPAddress Build(Byte[] ByteArray)
        {

            switch (ByteArray.Length)
            {

                case  4: return new IPv4Address(ByteArray);
                
                case 16: return new IPv6Address(ByteArray);

                default: throw new FormatException("Not a valid IIPAdress!");

            }

        }

        #endregion

        #region (static) Build(IPAddress)

        /// <summary>
        /// Create a new IIPAddress based on the given System.Net.IPAddress.
        /// </summary>
        /// <param name="IPAddress">A System.Net.IPAddress.</param>
        public static IIPAddress Build(System.Net.IPAddress IPAddress)
        {
            return Build(IPAddress.GetAddressBytes());
        }

        #endregion

        #region (static) Parse(IPAddressString)

        /// <summary>
        /// Parsed the given string representation into a new IIPAddress.
        /// </summary>
        /// <param name="IPAddressString">An IPAddress string representation.</param>
        public static IIPAddress Parse(String IPAddressString)
        {

            if (IPAddressString.IndexOf('.') > 0)
                return IPv4Address.Parse(IPAddressString);

            if (IPAddressString.IndexOf(':') > 0)
                return IPv6Address.Parse(IPAddressString);

            throw new FormatException("The given string '" + IPAddressString + "' is not a valid IP address!");

        }

        #endregion

        #region (static) TryParse(IPAddressString, out IPAddress)

        /// <summary>
        /// Parsed the given string representation into a new IIPAddress.
        /// </summary>
        /// <param name="IPAddressString">A string representation of an IP address.</param>
        /// <param name="IPAddress">The parsed IP address.</param>
        public static Boolean TryParse(String IPAddressString, out IIPAddress IPAddress)
        {

            IPv4Address _IPv4Address;

            if (IPAddressString.IndexOf('.') > 0)
                if (IPv4Address.TryParse(IPAddressString, out _IPv4Address))
                {
                    IPAddress = _IPv4Address;
                    return true;
                }

            IPv6Address _IPv6Address;

            if (IPAddressString.IndexOf(':') > 0)
                if (IPv6Address.TryParse(IPAddressString, out _IPv6Address))
                {
                    IPAddress = _IPv6Address;
                    return true;
                }

            throw new FormatException("The given string '" + IPAddressString + "' is not a valid IP address!");

        }

        #endregion

    }

}
