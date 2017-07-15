/*
 * Copyright (c) 2010-2016, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
    public class IPPort : IEquatable<IPPort>,
                          IComparable<IPPort>,
                          IComparable
    {

        #region Data

        private readonly UInt16 _IPPort;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Creates a new IPPort.
        /// </summary>
        public IPPort(UInt16 Port)
        {
            this._IPPort = Port;
        }

        #endregion


        #region /etc/services

        public static readonly IPPort SSH    = new IPPort(22);
        public static readonly IPPort TELNET = new IPPort(23);
        public static readonly IPPort HTTP   = new IPPort(80);
        public static readonly IPPort HTTPS  = new IPPort(443);

        #endregion


        #region (static) Parse(UInt16)

        /// <summary>
        /// Return the IPPort for the given UInt16.
        /// </summary>
        public static IPPort Parse(UInt16 UInt16)
        {
            return new IPPort(UInt16);
        }

        #endregion

        #region (static) Parse(Int32)

        /// <summary>
        /// Return the IPPort for the given UInt16.
        /// </summary>
        public static IPPort Parse(Int32 Int32)
        {
            return new IPPort((UInt16) Int32);
        }

        #endregion

        #region (static) Parse(String)

        /// <summary>
        /// Return the IPPort for the given String.
        /// </summary>
        public static IPPort Parse(String String)
        {
            return new IPPort(UInt16.Parse(String));
        }

        #endregion


        #region (static) TryParse(UInt16, out IPPort)

        /// <summary>
        /// Return the IPPort for the given UInt16.
        /// </summary>
        /// <param name="UInt16">The UInt16 to parse.</param>
        public static Boolean TryParse(UInt16 UInt16, out IPPort IPPort)
        {
            IPPort = new IPPort(UInt16);
            return true;
        }

        #endregion

        #region (static) TryParse(Int32, out IPPort)

        /// <summary>
        /// Return the IPPort for the given UInt16.
        /// </summary>
        /// <param name="Int32">The Int32 to parse.</param>
        public static Boolean TryParse(Int32 Int32, out IPPort IPPort)
        {
            IPPort = new IPPort((UInt16) Int32);
            return true;
        }

        #endregion

        #region (static) TryParse(String, out IPPort)

        /// <summary>
        /// Return the IPPort for the given String.
        /// </summary>
        /// <param name="String">The string to parse.</param>
        /// <param name="IPPort">The result.</param>
        public static Boolean TryParse(String String, out IPPort IPPort)
        {

            UInt16 Port;

            if (UInt16.TryParse(String, out Port))
            {
                IPPort = new IPPort(Port);
                return true;
            }

            IPPort = null;
            return false;

        }

        #endregion


        #region ToUInt16()

        /// <summary>
        /// Returns the IPPort as UInt16.
        /// </summary>
        public UInt16 ToUInt16()
        {
            return _IPPort;
        }

        #endregion

        #region ToInt32()

        /// <summary>
        /// Returns the IPPort as Int32.
        /// </summary>
        public Int32 ToInt32()
        {
            return _IPPort;
        }

        #endregion


        #region Operator overloading

        #region Operator == (myIPPort1, myIPPort2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="myIPPort1">A IPPort.</param>
        /// <param name="myIPPort2">Another IPPort.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (IPPort myIPPort1, IPPort myIPPort2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(myIPPort1, myIPPort2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) myIPPort1 == null) || ((Object) myIPPort2 == null))
                return false;

            return myIPPort1.Equals(myIPPort2);

        }

        #endregion

        #region Operator != (myIPPort1, myIPPort2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="myIPPort1">A IPPort.</param>
        /// <param name="myIPPort2">Another IPPort.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (IPPort myIPPort1, IPPort myIPPort2)
        {
            return !(myIPPort1 == myIPPort2);
        }

        #endregion

        #region Operator <  (myIPPort1, myIPPort2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="myIPPort1">A IPPort.</param>
        /// <param name="myIPPort2">Another IPPort.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (IPPort myIPPort1, IPPort myIPPort2)
        {

            // Check if myIPPort1 is null
            if ((Object) myIPPort1 == null)
                throw new ArgumentNullException("Parameter myIPPort1 must not be null!");

            // Check if myIPPort2 is null
            if ((Object) myIPPort2 == null)
                throw new ArgumentNullException("Parameter myIPPort2 must not be null!");

            return myIPPort1.CompareTo(myIPPort2) < 0;

        }

        #endregion

        #region Operator >  (myIPPort1, myIPPort2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="myIPPort1">A IPPort.</param>
        /// <param name="myIPPort2">Another IPPort.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (IPPort myIPPort1, IPPort myIPPort2)
        {

            // Check if myIPPort1 is null
            if ((Object) myIPPort1 == null)
                throw new ArgumentNullException("Parameter myIPPort1 must not be null!");

            // Check if myIPPort2 is null
            if ((Object) myIPPort2 == null)
                throw new ArgumentNullException("Parameter myIPPort2 must not be null!");

            return myIPPort1.CompareTo(myIPPort2) > 0;

        }

        #endregion

        #region Operator <= (myIPPort1, myIPPort2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="myIPPort1">A IPPort.</param>
        /// <param name="myIPPort2">Another IPPort.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (IPPort myIPPort1, IPPort myIPPort2)
        {
            return !(myIPPort1 > myIPPort2);
        }

        #endregion

        #region Operator >= (myIPPort1, myIPPort2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="myIPPort1">A IPPort.</param>
        /// <param name="myIPPort2">Another IPPort.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (IPPort myIPPort1, IPPort myIPPort2)
        {
            return !(myIPPort1 < myIPPort2);
        }

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

            // Check if myObject can be casted to an IPPort object
            var _IPPort = Object as IPPort;
            if ((Object) _IPPort == null)
                throw new ArgumentException("The given Object is an IPPort!");

            return CompareTo(_IPPort);

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
                throw new ArgumentNullException("The given IPPort must not be null!");

            return IPPort.CompareTo(IPPort._IPPort);

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

            // Check if myObject can be cast to IPPort
            var _IPPort = Object as IPPort;
            if ((Object) _IPPort == null)
                throw new ArgumentException("The given Object is not an IPPort!");

            return this.Equals(_IPPort);

        }

        #endregion

        #region Equals(myIPPort)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPPort">An object to compare with.</param>
        /// <returns>true|false</returns>
        public Boolean Equals(IPPort IPPort)
        {

            if ((Object) IPPort == null)
                throw new ArgumentNullException("The given IPPort must not be null!");

            return _IPPort.Equals(IPPort._IPPort);

        }

        #endregion

        #endregion

        #region GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()
        {
            return _IPPort;
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override String ToString()
        {
            return _IPPort.ToString();
        }

        #endregion

    }

}
