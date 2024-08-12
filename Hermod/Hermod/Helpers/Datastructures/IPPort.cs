/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
    /// An Internet Protocol Layer 4 Port.
    /// </summary>
    public readonly struct IPPort : IEquatable<IPPort>,
                                    IComparable<IPPort>,
                                    IComparable
    {

        #region Data

        private readonly UInt16 InternalId;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new Internet Protocol Layer 4 Port.
        /// </summary>
        /// <param name="Port">An Internet Protocol layer 4 port number.</param>
        private IPPort(UInt16 Port)
        {
            this.InternalId = Port;
        }

        #endregion


        #region /etc/services

        /// <summary>
        /// Zero
        /// </summary>
        public static readonly IPPort Zero = new(0);


        /// <summary>
        /// SSH
        /// </summary>
        public static readonly IPPort SSH = new(22);

        /// <summary>
        /// TELNET
        /// </summary>
        public static readonly IPPort TELNET = new(23);

        /// <summary>
        /// SMTP
        /// </summary>
        public static readonly IPPort SMTP = new(25);

        /// <summary>
        /// DNS
        /// </summary>
        public static readonly IPPort DNS = new(53);

        /// <summary>
        /// HTTP
        /// </summary>
        public static readonly IPPort HTTP = new(80);

        /// <summary>
        /// HTTPS
        /// </summary>
        public static readonly IPPort HTTPS = new(443);

        /// <summary>
        /// MQTT
        /// </summary>
        public static readonly IPPort MQTT = new(1883);

        #endregion


        #region (static) NewRandom

        /// <summary>
        /// Create a new random Internet Protocol Layer 4 Port.
        /// </summary>
        public static IPPort NewRandom

#pragma warning disable SCS0005 // Weak random number generator.

                   // 0 means in this context, that .NET will choose a random port number!
                => new ((UInt16) (Random.Shared.Next(UInt16.MaxValue - 1) + 1));

#pragma warning restore SCS0005 // Weak random number generator.

        #endregion

        #region (static) Auto

        /// <summary>
        /// Create an automatic Internet Protocol Layer 4 Port,
        /// which will be choosen by .NET during socket creation!
        /// </summary>
        public static IPPort Auto
            => new (0);

        #endregion


        #region (static) Parse   (Number)

        /// <summary>
        /// Parse the given numeric representation of an IP port.
        /// </summary>
        /// <param name="Number">A numeric representation of an IP port to parse.</param>
        public static IPPort Parse(UInt16 Number)

            => new (Number);


        /// <summary>
        /// Parse the given numeric representation of an IP port.
        /// </summary>
        /// <param name="Number">A numeric representation of an IP port to parse.</param>
        public static IPPort Parse(Int32 Number)

            => new ((UInt16) Number);

        #endregion

        #region (static) TryParse(Number)

        /// <summary>
        /// Try to parse the given numeric representation of an IP port.
        /// </summary>
        /// <param name="Number">A numeric representation of an IP port to parse.</param>
        public static IPPort? TryParse(UInt16 Number)
        {

            if (TryParse(Number, out var port))
                return port;

            return default;

        }

        /// <summary>
        /// Try to parse the given numeric representation of an IP port.
        /// </summary>
        /// <param name="Number">A numeric representation of an IP port to parse.</param>
        public static IPPort? TryParse(Int32 Number)
        {

            if (TryParse(Number, out var port))
                return port;

            return default;

        }

        #endregion

        #region (static) TryParse(Number, out IPPort)

        /// <summary>
        /// Try to parse the given numeric representation of an IP port.
        /// </summary>
        /// <param name="Number">A numeric representation of an IP port to parse.</param>
        /// <param name="IPPort">The parsed IP port.</param>
        public static Boolean TryParse(UInt16 Number, out IPPort IPPort)
        {
            IPPort = new IPPort(Number);
            return true;
        }

        /// <summary>
        /// Try to parse the given numeric representation of an IP port.
        /// </summary>
        /// <param name="Number">A numeric representation of an IP port to parse.</param>
        /// <param name="IPPort">The parsed IP port.</param>
        public static Boolean TryParse(Int32 Number, out IPPort IPPort)
        {

            try
            {
                IPPort = new IPPort((UInt16) Number);
                return true;
            }
            catch
            {
                IPPort = default;
                return false;
            }

        }

        #endregion


        #region (static) Parse   (String)

        /// <summary>
        /// Parse the given text representation of an IP port.
        /// </summary>
        /// <param name="String">A text representation of an IP port.</param>
        public static IPPort Parse(String String)

            => new (UInt16.Parse(String));

        #endregion

        #region (static) TryParse(String)

        /// <summary>
        /// Try to parse the given text representation of an IP port.
        /// </summary>
        /// <param name="String">A text representation of an IP port.</param>
        public static IPPort? TryParse(String String)
        {

            if (TryParse(String, out var port))
                return port;

            return default;

        }

        #endregion

        #region (static) TryParse(String, out IPPort)

        /// <summary>
        /// Try to parse the given text representation of an IP port.
        /// </summary>
        /// <param name="String">A text representation of an IP port.</param>
        /// <param name="IPPort">The parsed IP port.</param>
        public static Boolean TryParse(String String, out IPPort IPPort)
        {

            if (UInt16.TryParse(String, out var port))
            {
                IPPort = new IPPort(port);
                return true;
            }

            IPPort = default;
            return false;

        }

        #endregion

        #region Clone

        /// <summary>
        /// Clone this object.
        /// </summary>
        public IPPort Clone

            => new (InternalId);

        #endregion


        #region ToUInt16()

        /// <summary>
        /// Returns a numeric representation of an IP port.
        /// </summary>
        public UInt16 ToUInt16()
            =>InternalId;

        #endregion

        #region ToInt32()

        /// <summary>
        /// Returns a numeric representation of an IP port.
        /// </summary>
        public Int32 ToInt32()
            => InternalId;

        #endregion


        #region Operator overloading

        #region Operator == (IPPort1, IPPort2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPPort1">An IP port.</param>
        /// <param name="IPPort2">Another IP port.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (IPPort IPPort1,
                                           IPPort IPPort2)

            => IPPort1.Equals(IPPort2);

        #endregion

        #region Operator != (IPPort1, IPPort2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPPort1">An IP port.</param>
        /// <param name="IPPort2">Another IP port.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (IPPort IPPort1,
                                           IPPort IPPort2)

            => !IPPort1.Equals(IPPort2);

        #endregion

        #region Operator <  (IPPort1, IPPort2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPPort1">An IP port.</param>
        /// <param name="IPPort2">Another IP port.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (IPPort IPPort1,
                                          IPPort IPPort2)

            => IPPort1.CompareTo(IPPort2) < 0;

        #endregion

        #region Operator <= (IPPort1, IPPort2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPPort1">An IP port.</param>
        /// <param name="IPPort2">Another IP port.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (IPPort IPPort1,
                                           IPPort IPPort2)

            => IPPort1.CompareTo(IPPort2) <= 0;

        #endregion

        #region Operator >  (IPPort1, IPPort2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPPort1">An IP port.</param>
        /// <param name="IPPort2">Another IP port.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (IPPort IPPort1,
                                          IPPort IPPort2)

            => IPPort1.CompareTo(IPPort2) > 0;

        #endregion

        #region Operator >= (IPPort1, IPPort2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPPort1">An IP port.</param>
        /// <param name="IPPort2">Another IP port.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (IPPort IPPort1,
                                           IPPort IPPort2)

            => IPPort1.CompareTo(IPPort2) >= 0;

        #endregion

        #endregion

        #region IComparable<IPPort> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two IP ports.
        /// </summary>
        /// <param name="Object">An IP port to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is IPPort ipPort
                   ? CompareTo(ipPort)
                   : throw new ArgumentException("The given object is not an ip port!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(IPPort)

        /// <summary>
        /// Compares two IP ports.
        /// </summary>
        /// <param name="IPPort">An IP port to compare with.</param>
        public Int32 CompareTo(IPPort IPPort)

            => InternalId.CompareTo(IPPort.InternalId);

        #endregion

        #endregion

        #region IEquatable<IPPort> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two IP ports for equality.
        /// </summary>
        /// <param name="Object">An IP port to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is IPPort ipPort &&
                   Equals(ipPort);

        #endregion

        #region Equals(IPPort)

        /// <summary>
        /// Compares two IP ports for equality.
        /// </summary>
        /// <param name="IPPort">An IP port to compare with.</param>
        public Boolean Equals(IPPort IPPort)

            => InternalId.Equals(IPPort.InternalId);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()

            => InternalId;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override String ToString()

            => InternalId.ToString();

        #endregion

    }

}
