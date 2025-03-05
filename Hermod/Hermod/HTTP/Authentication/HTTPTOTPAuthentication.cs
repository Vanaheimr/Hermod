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
    /// A HTTP Time-based One-Time Password Authentication.
    /// </summary>
    public sealed class HTTPTOTPAuthentication : IHTTPAuthentication,
                                                 IEquatable<HTTPTOTPAuthentication>,
                                                 IComparable<HTTPTOTPAuthentication>,
                                                 IComparable
    {

        #region Data

        private static readonly Char[] splitter1 = [ ' ' ];
        private static readonly Char[] splitter2 = [ ':' ];

        #endregion

        #region Properties

        /// <summary>
        /// The username.
        /// </summary>
        public String  Username    { get; }

        /// <summary>
        /// The time-based one-time password.
        /// </summary>
        public String  TOTP        { get; }

        /// <summary>
        /// The HTTP request header representation.
        /// </summary>
        public String  HTTPText
            => $"TOTP {Username.ToBase64()}:{TOTP.ToBase64()}";

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP TOTP Authentication based on the given username and time-based one-time password.
        /// </summary>
        /// <param name="Username">A username.</param>
        /// <param name="TOTP">A time-based one-time password.</param>
        private HTTPTOTPAuthentication(String  Username,
                                       String  TOTP)
        {

            this.Username  = Username;
            this.TOTP      = TOTP;

        }

        #endregion


        #region (static) Create    (Username, TOTP)

        /// <summary>
        /// Create a HTTP TOTP Authentication based on the given username and time-based one-time password.
        /// </summary>
        /// <param name="Username">A username.</param>
        /// <param name="TOTP">A time-based one-time password.</param>
        public static HTTPTOTPAuthentication Create(String  Username,
                                                    String  TOTP)
        {

            if (TryCreate(Username,
                          TOTP,
                          out var httpTOTPAuthentication))
            {
                return httpTOTPAuthentication;
            }

            throw new ArgumentException($"The given username '{Username}' or time-based one-time password '{TOTP}' is invalid!");

        }

        #endregion

        #region (static) TryCreate (Username, TOTP)

        /// <summary>
        /// Try to create a HTTP TOTP Authentication based on the given username and time-based one-time password.
        /// </summary>
        /// <param name="Username">A username.</param>
        /// <param name="TOTP">A time-based one-time password.</param>
        public static HTTPTOTPAuthentication? TryCreate(String  Username,
                                                        String  TOTP)
        {

            if (TryCreate(Username,
                          TOTP,
                          out var httpTOTPAuthentication))
            {
                return httpTOTPAuthentication;
            }

            return null;

        }

        #endregion

        #region (static) TryCreate (Username, TOTP, out TOTPAuthentication)

        /// <summary>
        /// Try to create a HTTP TOTP Authentication based on the given username and time-based one-time password.
        /// </summary>
        /// <param name="Username">A username.</param>
        /// <param name="TOTP">A time-based one-time password.</param>
        /// <param name="TOTPAuthentication">The created HTTP TOTP Authentication.</param>
        public static Boolean TryCreate(String                                           Username,
                                        String                                           TOTP,
                                        [NotNullWhen(true)] out HTTPTOTPAuthentication?  TOTPAuthentication)
        {

            TOTPAuthentication = null;

            Username = Username.Trim();

            if (Username.IsNullOrEmpty())
                return false;

            TOTPAuthentication = new HTTPTOTPAuthentication(
                                     Username,
                                     TOTP
                                 );

            return true;

        }

        #endregion


        #region (static) ParseHTTPHeader    (Text)

        /// <summary>
        /// Parse the given text as a HTTP TOTP Authentication header.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP TOTP Authentication header.</param>
        public static HTTPTOTPAuthentication ParseHTTPHeader(String Text)
        {

            if (TryParseHTTPHeader(Text, out var httpTOTPAuthentication))
                return httpTOTPAuthentication!;

            throw new ArgumentException("The given text representation of a HTTP TOTP Authentication header is invalid!", nameof(Text));

        }

        #endregion

        #region (static) TryParseHTTPHeader (Text)

        /// <summary>
        /// Try to parse the given text as a HTTP TOTP Authentication header.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP TOTP Authentication header.</param>
        public static HTTPTOTPAuthentication? TryParseHTTPHeader(String Text)
        {

            if (TryParseHTTPHeader(Text, out var httpTOTPAuthentication))
                return httpTOTPAuthentication;

            return null;

        }

        #endregion

        #region (static) TryParseHTTPHeader (Text, out TOTPAuthentication)

        /// <summary>
        /// Try to parse the given text as a HTTP TOTP Authentication header.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP TOTP Authentication header.</param>
        /// <param name="TOTPAuthentication">The parsed HTTP TOTP Authentication header.</param>
        public static Boolean TryParseHTTPHeader(String                                           Text,
                                                 [NotNullWhen(true)] out HTTPTOTPAuthentication?  TOTPAuthentication)
        {

            TOTPAuthentication = null;

            Text = Text.Trim();

            if (Text.IsNullOrEmpty())
                return false;

            var splitted = Text.Split(splitter1, StringSplitOptions.RemoveEmptyEntries);

            if (splitted.Length == 2 &&
                String.Equals(splitted[0], "TOTP", StringComparison.OrdinalIgnoreCase))
            {

                var loginWithPassword = splitted[1].Trim().Split(splitter2, StringSplitOptions.RemoveEmptyEntries);
                if (loginWithPassword.Length == 2)
                {

                    if (!loginWithPassword[0].TryParseBASE64_UTF8(out var username, out _))
                        return false;

                    if (!loginWithPassword[1].TryParseBASE64_UTF8(out var totp, out _))
                        return false;

                    TOTPAuthentication = new HTTPTOTPAuthentication(
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

        #region Operator == (HTTPTOTPAuthentication1, HTTPTOTPAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPTOTPAuthentication1">A HTTP TOTP Authentication.</param>
        /// <param name="HTTPTOTPAuthentication2">Another HTTP TOTP Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (HTTPTOTPAuthentication HTTPTOTPAuthentication1,
                                           HTTPTOTPAuthentication HTTPTOTPAuthentication2)
        {

            if (Object.ReferenceEquals(HTTPTOTPAuthentication1, HTTPTOTPAuthentication2))
                return true;

            if (HTTPTOTPAuthentication1 is null || HTTPTOTPAuthentication2 is null)
                return false;

            return HTTPTOTPAuthentication1.Equals(HTTPTOTPAuthentication2);

        }

        #endregion

        #region Operator != (HTTPTOTPAuthentication1, HTTPTOTPAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPTOTPAuthentication1">A HTTP TOTP Authentication.</param>
        /// <param name="HTTPTOTPAuthentication2">Another HTTP TOTP Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPTOTPAuthentication HTTPTOTPAuthentication1,
                                           HTTPTOTPAuthentication HTTPTOTPAuthentication2)

            => !(HTTPTOTPAuthentication1 == HTTPTOTPAuthentication2);

        #endregion

        #region Operator <  (HTTPTOTPAuthentication1, HTTPTOTPAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPTOTPAuthentication1">A HTTP TOTP Authentication.</param>
        /// <param name="HTTPTOTPAuthentication2">Another HTTP TOTP Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (HTTPTOTPAuthentication HTTPTOTPAuthentication1,
                                          HTTPTOTPAuthentication HTTPTOTPAuthentication2)

            => HTTPTOTPAuthentication1 is null
                   ? throw new ArgumentNullException(nameof(HTTPTOTPAuthentication1), "The given HTTP TOTP Authentication must not be null!")
                   : HTTPTOTPAuthentication1.CompareTo(HTTPTOTPAuthentication2) < 0;

        #endregion

        #region Operator <= (HTTPTOTPAuthentication1, HTTPTOTPAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPTOTPAuthentication1">A HTTP TOTP Authentication.</param>
        /// <param name="HTTPTOTPAuthentication2">Another HTTP TOTP Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (HTTPTOTPAuthentication HTTPTOTPAuthentication1,
                                           HTTPTOTPAuthentication HTTPTOTPAuthentication2)

            => !(HTTPTOTPAuthentication1 > HTTPTOTPAuthentication2);

        #endregion

        #region Operator >  (HTTPTOTPAuthentication1, HTTPTOTPAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPTOTPAuthentication1">A HTTP TOTP Authentication.</param>
        /// <param name="HTTPTOTPAuthentication2">Another HTTP TOTP Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (HTTPTOTPAuthentication HTTPTOTPAuthentication1,
                                          HTTPTOTPAuthentication HTTPTOTPAuthentication2)

            => HTTPTOTPAuthentication1 is null
                   ? throw new ArgumentNullException(nameof(HTTPTOTPAuthentication1), "The given HTTP TOTP Authentication must not be null!")
                   : HTTPTOTPAuthentication1.CompareTo(HTTPTOTPAuthentication2) > 0;

        #endregion

        #region Operator >= (HTTPTOTPAuthentication1, HTTPTOTPAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPTOTPAuthentication1">A HTTP TOTP Authentication.</param>
        /// <param name="HTTPTOTPAuthentication2">Another HTTP TOTP Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (HTTPTOTPAuthentication HTTPTOTPAuthentication1,
                                           HTTPTOTPAuthentication HTTPTOTPAuthentication2)

            => !(HTTPTOTPAuthentication1 < HTTPTOTPAuthentication2);

        #endregion

        #endregion

        #region IComparable<HTTPTOTPAuthentication> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two HTTP TOTP Authentications.
        /// </summary>
        /// <param name="Object">A HTTP TOTP Authentication to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is HTTPTOTPAuthentication httpTOTPAuthentication
                   ? CompareTo(httpTOTPAuthentication)
                   : throw new ArgumentException("The given object is not a HTTP TOTP Authentication!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(HTTPTOTPAuthentication)

        /// <summary>
        /// Compares two HTTP TOTP Authentications.
        /// </summary>
        /// <param name="HTTPTOTPAuthentication">A HTTP TOTP Authentication to compare with.</param>
        public Int32 CompareTo(HTTPTOTPAuthentication? HTTPTOTPAuthentication)
        {

            if (HTTPTOTPAuthentication is null)
                throw new ArgumentNullException(nameof(HTTPTOTPAuthentication),
                                                "The given object HTTP TOTP Authentication must not be null!");

            var c = String.Compare(Username,
                                   HTTPTOTPAuthentication.Username,
                                   StringComparison.Ordinal);

            if (c == 0)
                String.Compare(TOTP,
                               HTTPTOTPAuthentication.TOTP,
                               StringComparison.Ordinal);

            return c;

        }

        #endregion

        #endregion

        #region IEquatable<HTTPTOTPAuthentication> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two HTTP TOTP Authentications for equality.
        /// </summary>
        /// <param name="Object">A HTTP TOTP Authentication to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is HTTPTOTPAuthentication httpTOTPAuthentication &&
                   Equals(httpTOTPAuthentication);

        #endregion

        #region Equals(HTTPTOTPAuthentication)

        /// <summary>
        /// Compares two HTTP TOTP Authentications for equality.
        /// </summary>
        /// <param name="HTTPTOTPAuthentication">A HTTP TOTP Authentication to compare with.</param>
        public Boolean Equals(HTTPTOTPAuthentication? HTTPTOTPAuthentication)

            => HTTPTOTPAuthentication is not null &&
               Username.Equals(HTTPTOTPAuthentication.Username) &&
               TOTP.    Equals(HTTPTOTPAuthentication.TOTP);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        /// <returns>The hash code of this object.</returns>
        public override Int32 GetHashCode()
        {
            unchecked
            {

                return Username.GetHashCode() * 3 ^
                       TOTP.    GetHashCode();

            }
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
            => $"TOTP '{Username}':'{TOTP}'";

        #endregion

    }

}
