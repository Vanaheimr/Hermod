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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The unique identification of a HTTP hostname.
    /// </summary>
    public class HTTPHostname : IEquatable<HTTPHostname>,
                                IComparable<HTTPHostname>

    {

        #region Properties

        /// <summary>
        /// The hostname.
        /// </summary>
        public String Name { get; }

        /// <summary>
        /// The TCP/IP port.
        /// </summary>
        public UInt16? Port { get; }

        /// <summary>
        /// Returns the length of the identificator.
        /// </summary>
        public UInt64 Length
            => (UInt64) ToString().Length;



        /// <summary>
        /// The HTTP 'ANY' host or "*".
        /// </summary>
        public static HTTPHostname Any
            => new HTTPHostname("*", null);


        /// <summary>
        /// Return an new HTTP hostname having a hostname wildcard, e.g. "*:443".
        /// </summary>
        public HTTPHostname AnyHost
            => new HTTPHostname("*", Port);


        /// <summary>
        /// Return an new HTTP hostname having a port wildcard, e.g. "localhost:*".
        /// </summary>
        public HTTPHostname AnyPort
            => new HTTPHostname(Name, null);


        /// <summary>
        /// The HTTP 'localhost' host.
        /// </summary>
        public static HTTPHostname Localhost
            => new HTTPHostname("localhost", null);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Generate a new HTTP hostname based on the given name and port.
        /// </summary>
        private HTTPHostname(String   Name,
                             UInt16?  Port)
        {

            if (Name == null)
                Name = "*";

            this.Name  = (Name.Trim().IsNullOrEmpty() ? "*" : Name.Trim());
            this.Port  = Port;

        }

        #endregion


        #region Parse(Text)

        /// <summary>
        /// Parse the given text as HTTP hostname.
        /// </summary>
        /// <param name="Text"></param>
        public static HTTPHostname Parse(String Text)
        {

            UInt16 Port;
            var Parts = Text.Trim().Split(':');

            if (Parts.Length == 1)
                return new HTTPHostname(Parts[0].Trim(), null);

            else if (Parts.Length == 2)
            {

                if (Parts[1].Trim() == "*")
                    return new HTTPHostname(Parts[0].Trim(), null);

                if (UInt16.TryParse(Parts[1].Trim(), out Port))
                    return new HTTPHostname(Parts[0].Trim(), Port);

            }

            throw new ArgumentException("The given text is not a valid HTTP hostname!", "Text");

        }

        #endregion

        #region Parse(Name, Port = null)

        /// <summary>
        /// Parse the given name and port as HTTP hostname.
        /// </summary>
        /// <param name="Name">The name of the HTTP hostname.</param>
        /// <param name="Port">The TCP/IP port.</param>
        public static HTTPHostname Parse(String   Name,
                                         UInt16?  Port = null)
        {
            return new HTTPHostname(Name, Port);
        }

        #endregion

        #region TryParse(Text, out Hostname)

        /// <summary>
        /// Parse the given string as a HTTP hostname.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP hostname.</param>
        /// <param name="Hostname">The parsed HTTP hostname.</param>
        public static Boolean TryParse(String Text, out HTTPHostname Hostname)
        {

            UInt16 Port;
            var Parts = Text.Trim().Split(':');

            if (Parts.Length == 2)
            {

                if (Parts[1].Trim() == "*")
                {
                    Hostname = new HTTPHostname(Parts[0].Trim(), null);
                    return true;
                }

                if (UInt16.TryParse(Parts[1].Trim(), out Port))
                {
                    Hostname = new HTTPHostname(Parts[0].Trim(), Port);
                    return true;
                }

            }

            Hostname = null;
            return false;

        }

        #endregion


        #region Operator overloading

        #region Operator == (Hostname1, Hostname2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Hostname1">A HTTPHostname.</param>
        /// <param name="Hostname2">Another HTTPHostname.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (HTTPHostname Hostname1, HTTPHostname Hostname2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(Hostname1, Hostname2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) Hostname1 == null) || ((Object) Hostname2 == null))
                return false;

            return Hostname1.Equals(Hostname2);

        }

        #endregion

        #region Operator != (Hostname1, Hostname2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Hostname1">A HTTPHostname.</param>
        /// <param name="Hostname2">Another HTTPHostname.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPHostname Hostname1, HTTPHostname Hostname2)
        {
            return !(Hostname1 == Hostname2);
        }

        #endregion

        #region Operator <  (Hostname1, Hostname2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Hostname1">A HTTPHostname.</param>
        /// <param name="Hostname2">Another HTTPHostname.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (HTTPHostname Hostname1, HTTPHostname Hostname2)
        {

            if ((Object) Hostname1 == null)
                throw new ArgumentNullException("The given Hostname1 must not be null!");

            return Hostname1.CompareTo(Hostname2) < 0;

        }

        #endregion

        #region Operator <= (Hostname1, Hostname2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Hostname1">A HTTPHostname.</param>
        /// <param name="Hostname2">Another HTTPHostname.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (HTTPHostname Hostname1, HTTPHostname Hostname2)
        {
            return !(Hostname1 > Hostname2);
        }

        #endregion

        #region Operator >  (Hostname1, Hostname2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Hostname1">A HTTPHostname.</param>
        /// <param name="Hostname2">Another HTTPHostname.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (HTTPHostname Hostname1, HTTPHostname Hostname2)
        {

            if ((Object) Hostname1 == null)
                throw new ArgumentNullException("The given Hostname1 must not be null!");

            return Hostname1.CompareTo(Hostname2) > 0;

        }

        #endregion

        #region Operator >= (Hostname1, Hostname2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Hostname1">A HTTPHostname.</param>
        /// <param name="Hostname2">Another HTTPHostname.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (HTTPHostname Hostname1, HTTPHostname Hostname2)
        {
            return !(Hostname1 < Hostname2);
        }

        #endregion

        #endregion

        #region IComparable<HTTPHostname> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException("The given object must not be null!");

            // Check if the given object is an Hostname.
            var Hostname = Object as HTTPHostname;
            if ((Object) Hostname == null)
                throw new ArgumentException("The given object is not a Hostname!");

            return CompareTo(Hostname);

        }

        #endregion

        #region CompareTo(Hostname)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Hostname">An object to compare with.</param>
        public Int32 CompareTo(HTTPHostname Hostname)
        {

            if ((Object) Hostname == null)
                throw new ArgumentNullException("The given Hostname must not be null!");

            return ToString().CompareTo(Hostname.ToString());

        }

        #endregion

        #endregion

        #region IEquatable<HTTPHostname> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)
        {

            if (Object == null)
                return false;

            // Check if the given object is an Hostname.
            var Hostname = Object as HTTPHostname;
            if ((Object) Hostname == null)
                return false;

            return this.Equals(Hostname);

        }

        #endregion

        #region Equals(Hostname)

        /// <summary>
        /// Compares two Hostnames for equality.
        /// </summary>
        /// <param name="Hostname">A Hostname to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(HTTPHostname Hostname)
        {

            if ((Object) Hostname == null)
                return false;

            return ToString().Equals(Hostname.ToString());

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
            unchecked
            {
                return Name.GetHashCode() * 17 ^ (Port != null ? Port.Value : 0);
            }
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a string representation of this object.
        /// </summary>
        public override String ToString()
        {
            return Name + ":" + (Port.HasValue ? Port.Value.ToString() : "*");
        }

        #endregion

    }

}
