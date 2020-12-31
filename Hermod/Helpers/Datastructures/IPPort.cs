/*
 * Copyright (c) 2010-2020, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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

using System;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// An IP port.
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
        /// Create a new IP port.
        /// </summary>
        /// <param name="Port">An IP port number.</param>
        private IPPort(UInt16 Port)
        {
            this.InternalId = Port;
        }

        #endregion


        #region /etc/services

        /// <summary>
        /// SSH.
        /// </summary>
        public static readonly IPPort SSH       = new IPPort(22);

        /// <summary>
        /// TELNET.
        /// </summary>
        public static readonly IPPort TELNET    = new IPPort(23);

        /// <summary>
        /// SMTP.
        /// </summary>
        public static readonly IPPort SMTP      = new IPPort(25);

        /// <summary>
        /// DNS.
        /// </summary>
        public static readonly IPPort DNS       = new IPPort(53);

        /// <summary>
        /// HTTP.
        /// </summary>
        public static readonly IPPort HTTP      = new IPPort(80);

        /// <summary>
        /// HTTPS.
        /// </summary>
        public static readonly IPPort HTTPS     = new IPPort(443);

        #endregion


        #region (static) Parse   (Number)

        /// <summary>
        /// Parse the given numeric representation of an IP port.
        /// </summary>
        /// <param name="Number">A numeric representation of an IP port to parse.</param>
        public static IPPort Parse(UInt16 Number)
            => new IPPort(Number);


        /// <summary>
        /// Parse the given numeric representation of an IP port.
        /// </summary>
        /// <param name="Number">A numeric representation of an IP port to parse.</param>
        public static IPPort Parse(Int32 Number)
            => new IPPort((UInt16) Number);

        #endregion

        #region (static) TryParse(Number)

        /// <summary>
        /// Try to parse the given numeric representation of an IP port.
        /// </summary>
        /// <param name="Number">A numeric representation of an IP port to parse.</param>
        public static IPPort? TryParse(UInt16 Number)
        {

            if (TryParse(Number, out IPPort Port))
                return Port;

            return new IPPort?();

        }

        /// <summary>
        /// Try to parse the given numeric representation of an IP port.
        /// </summary>
        /// <param name="Number">A numeric representation of an IP port to parse.</param>
        public static IPPort? TryParse(Int32 Number)
        {

            if (TryParse(Number, out IPPort Port))
                return Port;

            return new IPPort?();

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
            }
            catch (Exception)
            {
                IPPort = default(IPPort);
                return false;
            }

            return true;

        }

        #endregion


        #region (static) Parse   (String)

        /// <summary>
        /// Parse the given text representation of an IP port.
        /// </summary>
        /// <param name="String">A text representation of an IP port to parse.</param>
        public static IPPort Parse(String String)

            => new IPPort(UInt16.Parse(String));

        #endregion

        #region (static) TryParse(String)

        /// <summary>
        /// Try to parse the given text representation of an IP port.
        /// </summary>
        /// <param name="String">A text representation of an IP port to parse.</param>
        public static IPPort? TryParse(String String)
        {

            if (TryParse(String, out IPPort Port))
                return Port;

            return new IPPort?();

        }

        #endregion

        #region (static) TryParse(String, out IPPort)

        /// <summary>
        /// Try to parse the given text representation of an IP port.
        /// </summary>
        /// <param name="String">A text representation of an IP port to parse.</param>
        /// <param name="IPPort">The parsed IP port.</param>
        public static Boolean TryParse(String String, out IPPort IPPort)
        {

            if (UInt16.TryParse(String, out UInt16 Port))
            {
                IPPort = new IPPort(Port);
                return true;
            }

            IPPort = default(IPPort);
            return false;

        }

        #endregion

        #region Clone

        /// <summary>
        /// Clone this object.
        /// </summary>
        public IPPort Clone

            => new IPPort(InternalId);

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
        public static Boolean operator == (IPPort IPPort1, IPPort IPPort2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(IPPort1, IPPort2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) IPPort1 == null) || ((Object) IPPort2 == null))
                return false;

            return IPPort1.Equals(IPPort2);

        }

        #endregion

        #region Operator != (IPPort1, IPPort2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPPort1">An IP port.</param>
        /// <param name="IPPort2">Another IP port.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (IPPort IPPort1, IPPort IPPort2)
            => !(IPPort1 == IPPort2);

        #endregion

        #region Operator <  (IPPort1, IPPort2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPPort1">An IP port.</param>
        /// <param name="IPPort2">Another IP port.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (IPPort IPPort1, IPPort IPPort2)
        {

            if ((Object) IPPort1 == null)
                throw new ArgumentNullException("Parameter IPPort1 must not be null!");

            if ((Object) IPPort2 == null)
                throw new ArgumentNullException("Parameter IPPort2 must not be null!");

            return IPPort1.CompareTo(IPPort2) < 0;

        }

        #endregion

        #region Operator >  (IPPort1, IPPort2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPPort1">An IP port.</param>
        /// <param name="IPPort2">Another IP port.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (IPPort IPPort1, IPPort IPPort2)
        {

            if ((Object) IPPort1 == null)
                throw new ArgumentNullException("Parameter IPPort1 must not be null!");

            if ((Object) IPPort2 == null)
                throw new ArgumentNullException("Parameter IPPort2 must not be null!");

            return IPPort1.CompareTo(IPPort2) > 0;

        }

        #endregion

        #region Operator <= (IPPort1, IPPort2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPPort1">An IP port.</param>
        /// <param name="IPPort2">Another IP port.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (IPPort IPPort1, IPPort IPPort2)
            => !(IPPort1 > IPPort2);

        #endregion

        #region Operator >= (IPPort1, IPPort2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPPort1">An IP port.</param>
        /// <param name="IPPort2">Another IP port.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (IPPort IPPort1, IPPort IPPort2)
            => !(IPPort1 < IPPort2);

        #endregion

        #endregion

        #region IComparable<IPPort> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public Int32 CompareTo(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException("The given Object must not be null!");

            if (!(Object is IPPort))
                throw new ArgumentException("The given Object is an IP port!");

            return CompareTo((IPPort) Object);

        }

        #endregion

        #region CompareTo(IPPort)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPPort">An object to compare with.</param>
        /// <returns>true|false</returns>
        public Int32 CompareTo(IPPort IPPort)
        {

            if ((Object) IPPort == null)
                throw new ArgumentNullException("The given IP port must not be null!");

            return IPPort.InternalId.CompareTo(IPPort.InternalId);

        }

        #endregion

        #endregion

        #region IEquatable<IPPort> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException("The given Object must not be null!");

            if (!(Object is IPPort))
                throw new ArgumentException("The given Object is not an IP port!");

            return Equals((IPPort) Object);

        }

        #endregion

        #region Equals(IPPort)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPPort">An object to compare with.</param>
        /// <returns>true|false</returns>
        public Boolean Equals(IPPort IPPort)
        {

            if ((Object) IPPort == null)
                throw new ArgumentNullException("The given IPPort must not be null!");

            return InternalId.Equals(IPPort.InternalId);

        }

        #endregion

        #endregion

        #region GetHashCode()

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
