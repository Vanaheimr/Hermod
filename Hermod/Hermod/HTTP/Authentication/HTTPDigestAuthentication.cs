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

using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A HTTP Digest Authentication.
    /// </summary>
    public sealed class HTTPDigestAuthentication : IHTTPAuthentication,
                                                   IEquatable<HTTPDigestAuthentication>,
                                                   IComparable<HTTPDigestAuthentication>,
                                                   IComparable
    {

        #region Data

        private static readonly Char[] splitter1 = [ ' ' ];
        private static readonly Char[] splitter2 = [ ':' ];

        #endregion

        #region Properties
        //  realm="example", nonce="xyz", uri="/", response="abc"
        /// <summary>
        /// The username.
        /// </summary>
        public String  Username    { get; }

        /// <summary>
        /// The time-based one-time password.
        /// </summary>
        public String  Digest        { get; }

        /// <summary>
        /// The HTTP request header representation.
        /// </summary>
        public String  HTTPText
            => $"Digest {Username.ToBase64()}:{Digest.ToBase64()}";

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP Digest Authentication based on the given username and time-based one-time password.
        /// </summary>
        /// <param name="Username">A username.</param>
        /// <param name="Digest">A time-based one-time password.</param>
        private HTTPDigestAuthentication(String  Username,
                                       String  Digest)
        {

            this.Username  = Username;
            this.Digest      = Digest;

        }

        #endregion


        #region (static) Create   (Username, Digest)

        /// <summary>
        /// Create a HTTP Digest Authentication based on the given username and time-based one-time password.
        /// </summary>
        /// <param name="Username">A username.</param>
        /// <param name="Digest">A time-based one-time password.</param>
        public static HTTPDigestAuthentication Create(String  Username,
                                                    String  Digest)
        {

            if (TryCreate(Username,
                          Digest,
                          out var httpDigestAuthentication))
            {
                return httpDigestAuthentication;
            }

            throw new ArgumentException($"The given username '{Username}' or time-based one-time password '{Digest}' is invalid!");

        }

        #endregion

        #region (static) TryCreate(Username, Digest)

        /// <summary>
        /// Try to create a HTTP Digest Authentication based on the given username and time-based one-time password.
        /// </summary>
        /// <param name="Username">A username.</param>
        /// <param name="Digest">A time-based one-time password.</param>
        public static HTTPDigestAuthentication? TryCreate(String  Username,
                                                        String  Digest)
        {

            if (TryCreate(Username,
                          Digest,
                          out var httpDigestAuthentication))
            {
                return httpDigestAuthentication;
            }

            return null;

        }

        #endregion

        #region (static) TryCreate(Username, Digest, out DigestAuthentication)

        /// <summary>
        /// Try to create a HTTP Digest Authentication based on the given username and time-based one-time password.
        /// </summary>
        /// <param name="Username">A username.</param>
        /// <param name="Digest">A time-based one-time password.</param>
        /// <param name="DigestAuthentication">The created HTTP Digest Authentication.</param>
        public static Boolean TryCreate(String                                           Username,
                                        String                                           Digest,
                                        [NotNullWhen(true)] out HTTPDigestAuthentication?  DigestAuthentication)
        {

            DigestAuthentication = null;

            Username = Username.Trim();

            if (Username.IsNullOrEmpty())
                return false;

            DigestAuthentication = new HTTPDigestAuthentication(
                                     Username,
                                     Digest
                                 );

            return true;

        }

        #endregion


        #region (static) ParseHeader   (Text)

        /// <summary>
        /// Parse the given text as a HTTP Digest Authentication header.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP Digest Authentication header.</param>
        public static HTTPDigestAuthentication ParseHeader(String Text)
        {

            if (TryParseHeader(Text, out var httpDigestAuthentication))
                return httpDigestAuthentication!;

            throw new ArgumentException("The given text representation of a HTTP Digest Authentication header is invalid!", nameof(Text));

        }

        #endregion

        #region (static) TryParseHeader(Text)

        /// <summary>
        /// Try to parse the given text as a HTTP Digest Authentication header.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP Digest Authentication header.</param>
        public static HTTPDigestAuthentication? TryParseHeader(String Text)
        {

            if (TryParseHeader(Text, out var httpDigestAuthentication))
                return httpDigestAuthentication;

            return null;

        }

        #endregion

        #region (static) TryParseHeader(Text, out DigestAuthentication)

        /// <summary>
        /// Try to parse the given text as a HTTP Digest Authentication header.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP Digest Authentication header.</param>
        /// <param name="DigestAuthentication">The parsed HTTP Digest Authentication header.</param>
        public static Boolean TryParseHeader(String                                           Text,
                                             [NotNullWhen(true)] out HTTPDigestAuthentication?  DigestAuthentication)
        {

            DigestAuthentication = null;

            Text = Text.Trim();

            if (Text.IsNullOrEmpty())
                return false;

            var splitted = Text.Split(splitter1, StringSplitOptions.RemoveEmptyEntries);

            if (splitted.Length == 2 &&
                String.Equals(splitted[0], "Digest", StringComparison.OrdinalIgnoreCase))
            {

                var loginWithPassword = splitted[1].Trim().Split(splitter2, StringSplitOptions.RemoveEmptyEntries);
                if (loginWithPassword.Length == 2)
                {

                    if (!loginWithPassword[0].TryParseBASE64_UTF8(out var username, out _))
                        return false;

                    if (!loginWithPassword[1].TryParseBASE64_UTF8(out var totp, out _))
                        return false;

                    DigestAuthentication = new HTTPDigestAuthentication(
                                             username,
                                             totp
                                         );

                    return true;

                }

            }

            return false;

        }

        #endregion


        #region Operator overloading

        #region Operator == (HTTPDigestAuthentication1, HTTPDigestAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPDigestAuthentication1">A HTTP Digest Authentication.</param>
        /// <param name="HTTPDigestAuthentication2">Another HTTP Digest Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (HTTPDigestAuthentication HTTPDigestAuthentication1,
                                           HTTPDigestAuthentication HTTPDigestAuthentication2)
        {

            if (Object.ReferenceEquals(HTTPDigestAuthentication1, HTTPDigestAuthentication2))
                return true;

            if (HTTPDigestAuthentication1 is null || HTTPDigestAuthentication2 is null)
                return false;

            return HTTPDigestAuthentication1.Equals(HTTPDigestAuthentication2);

        }

        #endregion

        #region Operator != (HTTPDigestAuthentication1, HTTPDigestAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPDigestAuthentication1">A HTTP Digest Authentication.</param>
        /// <param name="HTTPDigestAuthentication2">Another HTTP Digest Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPDigestAuthentication HTTPDigestAuthentication1,
                                           HTTPDigestAuthentication HTTPDigestAuthentication2)

            => !(HTTPDigestAuthentication1 == HTTPDigestAuthentication2);

        #endregion

        #region Operator <  (HTTPDigestAuthentication1, HTTPDigestAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPDigestAuthentication1">A HTTP Digest Authentication.</param>
        /// <param name="HTTPDigestAuthentication2">Another HTTP Digest Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (HTTPDigestAuthentication HTTPDigestAuthentication1,
                                          HTTPDigestAuthentication HTTPDigestAuthentication2)

            => HTTPDigestAuthentication1 is null
                   ? throw new ArgumentNullException(nameof(HTTPDigestAuthentication1), "The given HTTP Digest Authentication must not be null!")
                   : HTTPDigestAuthentication1.CompareTo(HTTPDigestAuthentication2) < 0;

        #endregion

        #region Operator <= (HTTPDigestAuthentication1, HTTPDigestAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPDigestAuthentication1">A HTTP Digest Authentication.</param>
        /// <param name="HTTPDigestAuthentication2">Another HTTP Digest Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (HTTPDigestAuthentication HTTPDigestAuthentication1,
                                           HTTPDigestAuthentication HTTPDigestAuthentication2)

            => !(HTTPDigestAuthentication1 > HTTPDigestAuthentication2);

        #endregion

        #region Operator >  (HTTPDigestAuthentication1, HTTPDigestAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPDigestAuthentication1">A HTTP Digest Authentication.</param>
        /// <param name="HTTPDigestAuthentication2">Another HTTP Digest Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (HTTPDigestAuthentication HTTPDigestAuthentication1,
                                          HTTPDigestAuthentication HTTPDigestAuthentication2)

            => HTTPDigestAuthentication1 is null
                   ? throw new ArgumentNullException(nameof(HTTPDigestAuthentication1), "The given HTTP Digest Authentication must not be null!")
                   : HTTPDigestAuthentication1.CompareTo(HTTPDigestAuthentication2) > 0;

        #endregion

        #region Operator >= (HTTPDigestAuthentication1, HTTPDigestAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPDigestAuthentication1">A HTTP Digest Authentication.</param>
        /// <param name="HTTPDigestAuthentication2">Another HTTP Digest Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (HTTPDigestAuthentication HTTPDigestAuthentication1,
                                           HTTPDigestAuthentication HTTPDigestAuthentication2)

            => !(HTTPDigestAuthentication1 < HTTPDigestAuthentication2);

        #endregion

        #endregion

        #region IComparable<HTTPDigestAuthentication> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two HTTP Digest Authentications.
        /// </summary>
        /// <param name="Object">A HTTP Digest Authentication to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is HTTPDigestAuthentication httpDigestAuthentication
                   ? CompareTo(httpDigestAuthentication)
                   : throw new ArgumentException("The given object is not a HTTP Digest Authentication!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(HTTPDigestAuthentication)

        /// <summary>
        /// Compares two HTTP Digest Authentications.
        /// </summary>
        /// <param name="HTTPDigestAuthentication">A HTTP Digest Authentication to compare with.</param>
        public Int32 CompareTo(HTTPDigestAuthentication? HTTPDigestAuthentication)
        {

            if (HTTPDigestAuthentication is null)
                throw new ArgumentNullException(nameof(HTTPDigestAuthentication),
                                                "The given object HTTP Digest Authentication must not be null!");

            var c = String.Compare(Username,
                                   HTTPDigestAuthentication.Username,
                                   StringComparison.Ordinal);

            if (c == 0)
                String.Compare(Digest,
                               HTTPDigestAuthentication.Digest,
                               StringComparison.Ordinal);

            return c;

        }

        #endregion

        #endregion

        #region IEquatable<HTTPDigestAuthentication> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two HTTP Digest Authentications for equality.
        /// </summary>
        /// <param name="Object">A HTTP Digest Authentication to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is HTTPDigestAuthentication httpDigestAuthentication &&
                   Equals(httpDigestAuthentication);

        #endregion

        #region Equals(HTTPDigestAuthentication)

        /// <summary>
        /// Compares two HTTP Digest Authentications for equality.
        /// </summary>
        /// <param name="HTTPDigestAuthentication">A HTTP Digest Authentication to compare with.</param>
        public Boolean Equals(HTTPDigestAuthentication? HTTPDigestAuthentication)

            => HTTPDigestAuthentication is not null &&
               Username.Equals(HTTPDigestAuthentication.Username) &&
               Digest.    Equals(HTTPDigestAuthentication.Digest);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()
        {
            unchecked
            {

                return Username.GetHashCode() * 3 ^
                       Digest.    GetHashCode();

            }
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
            => $"Digest '{Username}':'{Digest}'";

        #endregion

    }

}
