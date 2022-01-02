/*
 * Copyright (c) 2010-2022, Achim Friedland <achim.friedland@graphdefined.com>
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

using System;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Extension methods for HTTP hostnames.
    /// </summary>
    public static class HTTPHostnameExtensions
    {

        /// <summary>
        /// Indicates whether this HTTP hostname is null or empty.
        /// </summary>
        /// <param name="HTTPHostname">A HTTP hostname.</param>
        public static Boolean IsNullOrEmpty(this HTTPHostname? HTTPHostname)
            => !HTTPHostname.HasValue || HTTPHostname.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this HTTP hostname is null or empty.
        /// </summary>
        /// <param name="HTTPHostname">A HTTP hostname.</param>
        public static Boolean IsNotNullOrEmpty(this HTTPHostname? HTTPHostname)
            => HTTPHostname.HasValue && HTTPHostname.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// The unique identification of a HTTP hostname.
    /// </summary>
    public readonly struct HTTPHostname : IEquatable<HTTPHostname>,
                                          IComparable<HTTPHostname>

    {

        #region Properties

        /// <summary>
        /// The hostname.
        /// </summary>
        public String   Name    { get; }

        /// <summary>
        /// The TCP/IP port.
        /// </summary>
        public UInt16?  Port    { get; }

        /// <summary>
        /// Indicates whether this user identification is null or empty.
        /// </summary>
        public Boolean IsNullOrEmpty
            => Name.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this user identification is NOT null or empty.
        /// </summary>
        public Boolean IsNotNullOrEmpty
            => Name.IsNotNullOrEmpty();

        /// <summary>
        /// Returns the length of the identification.
        /// </summary>
        public UInt64   Length
            => (UInt64) ToString().Length;

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public String   SimpleString
            => Name + (Port.HasValue ? ":" + Port.Value.ToString() : "");

        #endregion

        #region Statics

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
                             UInt16?  Port = null)
        {

            Name = Name?.Trim();

            this.Name  = Name.IsNullOrEmpty() ? "*" : Name;
            this.Port  = Port;

        }

        #endregion


        #region Parse   (Text)

        /// <summary>
        /// Parse the given text as HTTP hostname.
        /// </summary>
        /// <param name="Text">The text representation of a HTTP hostname.</param>
        public static HTTPHostname Parse(String Text)
        {

            if (TryParse(Text, out HTTPHostname Hostname))
                return Hostname;

            throw new ArgumentException("The given text '" + Text + "' is not a valid HTTP hostname!", nameof(Text));

        }

        #endregion

        #region Parse   (Text, Port)

        /// <summary>
        /// Parse the given name and port as HTTP hostname.
        /// </summary>
        /// <param name="Text">The text representation of a HTTP hostname.</param>
        /// <param name="Port">The TCP/IP port.</param>
        public static HTTPHostname Parse(String  Text,
                                         UInt16  Port)
        {

            if (Text != null)
                Text = Text.Trim().ToLower();

            if (TryParse(Text, Port, out HTTPHostname httpHostname))
                return httpHostname;

            throw new ArgumentException("The given text '" + Text + "' is not a valid HTTP hostname!", nameof(Text));

        }

        #endregion

        #region TryParse(Text)

        /// <summary>
        /// Try to parse the given text as HTTP hostname.
        /// </summary>
        /// <param name="Text">The text representation of a HTTP hostname.</param>
        public static HTTPHostname? TryParse(String Text)
        {

            if (TryParse(Text, out HTTPHostname Hostname))
                return Hostname;

            return new HTTPHostname?();

        }

        #endregion

        #region TryParse(Text, Port)

        /// <summary>
        /// Parse the given string as a HTTP hostname.
        /// </summary>
        /// <param name="Text">The text representation of a HTTP hostname.</param>
        /// <param name="Port">The TCP/IP port.</param>
        public static HTTPHostname? TryParse(String Text, UInt16 Port)
        {

            if (TryParse(Text, Port, out HTTPHostname Hostname))
                return Hostname;

            return new HTTPHostname?();

        }

        #endregion

        #region TryParse(Text,       out Hostname)

        /// <summary>
        /// Parse the given string as a HTTP hostname.
        /// </summary>
        /// <param name="Text">The text representation of a HTTP hostname.</param>
        /// <param name="Hostname">The parsed HTTP hostname.</param>
        public static Boolean TryParse(String Text, out HTTPHostname Hostname)
        {

            if (Text != null)
                Text = Text.Trim().ToLower();

            if (Text.IsNullOrEmpty())
            {
                Hostname = default(HTTPHostname);
                return false;
            }

            var Parts = Text.Split(':');

            if (Parts[0] != null)
                Parts[0] = Parts[0].Trim().ToLower();

            if (Parts[0].IsNullOrEmpty())
            {
                Hostname = default(HTTPHostname);
                return false;
            }

            if (Parts.Length == 1)
            {
                Hostname = new HTTPHostname(Parts[0]);
                return true;
            }

            if (Parts.Length == 2)// || Parts[0].IsNullOrEmpty())
            {

                if (Parts[1] != null)
                    Parts[1] = Parts[1].Trim();

                if (Parts[1].IsNullOrEmpty())
                {
                    Hostname = default(HTTPHostname);
                    return false;
                }

                if (Parts[1] == "*")
                {
                    Hostname = new HTTPHostname(Parts[0].Trim());
                    return true;
                }

                if (UInt16.TryParse(Parts[1].Trim(), out ushort Port))
                {
                    Hostname = new HTTPHostname(Parts[0].Trim(), Port);
                    return true;
                }

            }

            Hostname = default(HTTPHostname);
            return false;

        }

        #endregion

        #region TryParse(Text, Port, out Hostname)

        /// <summary>
        /// Parse the given string as a HTTP hostname.
        /// </summary>
        /// <param name="Text">The text representation of a HTTP hostname.</param>
        /// <param name="Port">The TCP/IP port.</param>
        /// <param name="Hostname">The parsed HTTP hostname.</param>
        public static Boolean TryParse(String Text, UInt16 Port, out HTTPHostname Hostname)
        {

            if (Text != null)
                Text = Text.Trim().ToLower();

            if (Text.IsNullOrEmpty() || Text.Contains(":"))
            {
                Hostname = default(HTTPHostname);
                return false;
            }

            Hostname = new HTTPHostname(Text, Port);
            return true;

        }

        #endregion

        #region Clone

        /// <summary>
        /// Clone this object.
        /// </summary>
        public HTTPHostname Clone

            => new HTTPHostname(new String(Name.ToCharArray()),
                                Port);

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

        #region Operator == (Hostname1, Hostname2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Hostname1">A HTTPHostname.</param>
        /// <param name="Hostname2">Another HTTPHostname.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (HTTPHostname Hostname1, String Hostname2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(Hostname1, Hostname2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) Hostname1 == null) || ((Object) Hostname2 == null))
                return false;

            return Hostname1.Name.Equals(Hostname2);

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

        #region Operator != (Hostname1, Hostname2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Hostname1">A HTTPHostname.</param>
        /// <param name="Hostname2">Another HTTPHostname.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPHostname Hostname1, String Hostname2)
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

            if (!(Object is HTTPHostname httpHostname))
                throw new ArgumentException("The given object is not a HTTP hostname!");

            return CompareTo(httpHostname);

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
                throw new ArgumentNullException("The given HTTP hostname must not be null!");

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

            if (!(Object is HTTPHostname httpHostname))
                return false;

            return Equals(httpHostname);

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
                return Name.GetHashCode() * 17 ^ (Port ?? 0);
            }
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
            => Name + (Port.HasValue ? ":" + Port.Value.ToString() : "");

        #endregion

    }

}
