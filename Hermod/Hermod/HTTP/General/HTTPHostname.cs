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

#region Usings

using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;

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

        #region Data

        private static readonly Regex regEx = new ("^([a-z0-9-]+(\\.[a-z0-9-]+)*)$");

        #endregion

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
            => new ("*", null);


        /// <summary>
        /// Return an new HTTP hostname having a hostname wildcard, e.g. "*:443".
        /// </summary>
        public HTTPHostname AnyHost
            => new ("*", Port);


        /// <summary>
        /// Return an new HTTP hostname having a port wildcard, e.g. "localhost:*".
        /// </summary>
        public HTTPHostname AnyPort
            => new (Name, null);


        /// <summary>
        /// The HTTP 'localhost' host.
        /// </summary>
        public static HTTPHostname Localhost
            => new ("localhost", null);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Generate a new HTTP hostname based on the given name and port.
        /// </summary>
        private HTTPHostname(String   Name,
                             UInt16?  Port = null)
        {

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

            if (TryParse(Text, out var httpHostname))
                return httpHostname;

            throw new ArgumentException($"Invalid text representation of a HTTP hostname: '{Text}'!",
                                        nameof(Text));

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

            if (TryParse(Text, Port, out var httpHostname))
                return httpHostname;

            throw new ArgumentException($"Invalid text representation of a HTTP hostname: '{Text}:{Port}'!",
                                        nameof(Text));

        }

        #endregion

        #region TryParse(Text)

        /// <summary>
        /// Try to parse the given text as HTTP hostname.
        /// </summary>
        /// <param name="Text">The text representation of a HTTP hostname.</param>
        public static HTTPHostname? TryParse(String Text)
        {

            if (TryParse(Text, out var httpHostname))
                return httpHostname;

            return null;

        }

        #endregion

        #region TryParse(Text, Port)

        /// <summary>
        /// Parse the given string as a HTTP hostname.
        /// </summary>
        /// <param name="Text">The text representation of a HTTP hostname.</param>
        /// <param name="Port">The TCP/IP port.</param>
        public static HTTPHostname? TryParse(String  Text,
                                             UInt16  Port)
        {

            if (TryParse(Text, Port, out var httpHostname))
                return httpHostname;

            return null;

        }

        #endregion

        #region TryParse(Text,       out Hostname)

        /// <summary>
        /// Parse the given string as a HTTP hostname.
        /// </summary>
        /// <param name="Text">The text representation of a HTTP hostname.</param>
        /// <param name="Hostname">The parsed HTTP hostname.</param>
        public static Boolean TryParse(String                                Text,
                                       [NotNullWhen(true)] out HTTPHostname  Hostname)
        {

            Text = Text.Trim().ToLower();

            if (Text.IsNullOrEmpty())
            {
                Hostname = default;
                return false;
            }

            var parts     = Text.Split(':');
            var hostname  = parts.Length > 0 ? parts[0]?.Trim() : null;
            var portText  = parts.Length > 1 ? parts[1]?.Trim() : null;

            if (hostname is not null &&
               (hostname == "*" || regEx.IsMatch(hostname)))
            {

                if (portText is null)
                {
                    Hostname = new HTTPHostname(hostname);
                    return true;
                }

                if (portText is not null && UInt16.TryParse(portText, out var port))
                {
                    Hostname = new HTTPHostname(hostname, port);
                    return true;
                }

            }

            Hostname = default;
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
        public static Boolean TryParse(String                                Text,
                                       UInt16                                Port,
                                       [NotNullWhen(true)] out HTTPHostname  Hostname)
        {

            Text = Text.Trim().ToLower();

            if (Text == "*" || regEx.IsMatch(Text))
            {
                Hostname = new HTTPHostname(Text, Port);
                return true;
            }

            Hostname = default;
            return false;

        }

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this HTTP hostname.
        /// </summary>
        public HTTPHostname Clone()

            => new (
                   Name.CloneString(),
                   Port
               );

        #endregion


        #region Operator overloading

        #region Operator == (Hostname1, Hostname2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Hostname1">A HTTPHostname.</param>
        /// <param name="Hostname2">Another HTTPHostname.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (HTTPHostname Hostname1,
                                           HTTPHostname Hostname2)

            => Hostname1.Equals(Hostname2);

        #endregion

        #region Operator == (Hostname1, Hostname2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Hostname1">A HTTPHostname.</param>
        /// <param name="Hostname2">Another HTTPHostname.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (HTTPHostname Hostname1,
                                           String       Hostname2)

            => Hostname1.Name.Equals(Hostname2);

        #endregion

        #region Operator != (Hostname1, Hostname2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Hostname1">A HTTPHostname.</param>
        /// <param name="Hostname2">Another HTTPHostname.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPHostname Hostname1,
                                           HTTPHostname Hostname2)

            => !Hostname1.Equals(Hostname2);

        #endregion

        #region Operator != (Hostname1, Hostname2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Hostname1">A HTTPHostname.</param>
        /// <param name="Hostname2">Another HTTPHostname.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPHostname Hostname1,
                                           String       Hostname2)

            => !Hostname1.Name.Equals(Hostname2);

        #endregion

        #region Operator <  (Hostname1, Hostname2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Hostname1">A HTTPHostname.</param>
        /// <param name="Hostname2">Another HTTPHostname.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (HTTPHostname Hostname1,
                                          HTTPHostname Hostname2)

            => Hostname1.CompareTo(Hostname2) < 0;

        #endregion

        #region Operator <= (Hostname1, Hostname2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Hostname1">A HTTPHostname.</param>
        /// <param name="Hostname2">Another HTTPHostname.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (HTTPHostname Hostname1,
                                           HTTPHostname Hostname2)

            => Hostname1.CompareTo(Hostname2) <= 0;

        #endregion

        #region Operator >  (Hostname1, Hostname2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Hostname1">A HTTPHostname.</param>
        /// <param name="Hostname2">Another HTTPHostname.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (HTTPHostname Hostname1,
                                          HTTPHostname Hostname2)

            => Hostname1.CompareTo(Hostname2) > 0;

        #endregion

        #region Operator >= (Hostname1, Hostname2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Hostname1">A HTTPHostname.</param>
        /// <param name="Hostname2">Another HTTPHostname.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (HTTPHostname Hostname1,
                                           HTTPHostname Hostname2)

            => Hostname1.CompareTo(Hostname2) >= 0;

        #endregion

        #endregion

        #region IComparable<HTTPHostname> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two HTTP hostnames.
        /// </summary>
        /// <param name="Object">A HTTP hostname to compare with.</param>
        public Int32 CompareTo(Object Object)

            => Object is HTTPHostname httpHostname
                   ? CompareTo(httpHostname)
                   : throw new ArgumentException("The given object is not a HTTP hostname!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(Hostname)

        /// <summary>
        /// Compares two HTTP hostnames.
        /// </summary>
        /// <param name="Hostname">A HTTP hostname to compare with.</param>
        public Int32 CompareTo(HTTPHostname Hostname)

            => ToString().CompareTo(Hostname.ToString());

        #endregion

        #endregion

        #region IEquatable<HTTPHostname> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two HTTP hostnames for equality.
        /// </summary>
        /// <param name="Object">A HTTP hostname to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is HTTPHostname httpHostname &&
                   Equals(httpHostname);

        #endregion

        #region Equals(Hostname)

        /// <summary>
        /// Compares two HTTP hostnames for equality.
        /// </summary>
        /// <param name="Hostname">A HTTP hostname to compare with.</param>
        public Boolean Equals(HTTPHostname Hostname)

            => String.Equals(ToString(),
                             Hostname.ToString(),
                             StringComparison.Ordinal);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
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

            => String.Concat(

                   Name,

                   Port.HasValue
                       ? $":{Port.Value}"
                       : ""

               );

        #endregion

    }

}
