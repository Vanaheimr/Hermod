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

#region Usings

using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// An HTTP Time-based One-Time Password Authentication.
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
        public String              Username    { get; }

        /// <summary>
        /// The time-based one-time password.
        /// </summary>
        public String              TOTP        { get; }

        /// <summary>
        /// The TOTP type: a raw TOTP, or one bound to the TLS session
        /// via TLS v1.3 exporter material (TLS channel binding).
        /// </summary>
        public TOTPHTTPHeaderType  Type        { get; }

        /// <summary>
        /// The HTTP request header representation.
        /// A raw TOTP keeps the legacy two-segment form; a TLS-channel-bound
        /// TOTP is prefixed with its type digit as a third segment.
        /// </summary>
        public String  HTTPText
            => Type == TOTPHTTPHeaderType.RAW
                   ? $"TOTP {Username.ToBase64()}:{TOTP.ToBase64()}"
                   : $"TOTP {(Byte) Type}:{Username.ToBase64()}:{TOTP.ToBase64()}";

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP TOTP Authentication based on the given username and time-based one-time password.
        /// </summary>
        /// <param name="Username">A username.</param>
        /// <param name="TOTP">A time-based one-time password.</param>
        /// <param name="Type">The optional TOTP type (raw by default, or bound to the TLS session).</param>
        private HTTPTOTPAuthentication(String              Username,
                                       String              TOTP,
                                       TOTPHTTPHeaderType  Type   = TOTPHTTPHeaderType.RAW)
        {

            this.Username  = Username;
            this.TOTP      = TOTP;
            this.Type      = Type;

        }

        #endregion


        #region (static) Create    (Username, TOTP, Type = RAW)

        /// <summary>
        /// Create a HTTP TOTP Authentication based on the given username and time-based one-time password.
        /// </summary>
        /// <param name="Username">A username.</param>
        /// <param name="TOTP">A time-based one-time password.</param>
        /// <param name="Type">The optional TOTP type (raw by default, or bound to the TLS session).</param>
        public static HTTPTOTPAuthentication Create(String              Username,
                                                    String              TOTP,
                                                    TOTPHTTPHeaderType  Type   = TOTPHTTPHeaderType.RAW)
        {

            if (TryCreate(Username,
                          TOTP,
                          out var httpTOTPAuthentication,
                          Type))
            {
                return httpTOTPAuthentication;
            }

            throw new ArgumentException($"The given username '{Username}' or time-based one-time password '{TOTP}' is invalid!");

        }

        #endregion

        #region (static) TryCreate (Username, TOTP, Type = RAW)

        /// <summary>
        /// Try to create a HTTP TOTP Authentication based on the given username and time-based one-time password.
        /// </summary>
        /// <param name="Username">A username.</param>
        /// <param name="TOTP">A time-based one-time password.</param>
        /// <param name="Type">The optional TOTP type (raw by default, or bound to the TLS session).</param>
        public static HTTPTOTPAuthentication? TryCreate(String              Username,
                                                        String              TOTP,
                                                        TOTPHTTPHeaderType  Type   = TOTPHTTPHeaderType.RAW)
        {

            if (TryCreate(Username,
                          TOTP,
                          out var httpTOTPAuthentication,
                          Type))
            {
                return httpTOTPAuthentication;
            }

            return null;

        }

        #endregion

        #region (static) TryCreate (Username, TOTP, out TOTPAuthentication, Type = RAW)

        /// <summary>
        /// Try to create a HTTP TOTP Authentication based on the given username and time-based one-time password.
        /// </summary>
        /// <param name="Username">A username.</param>
        /// <param name="TOTP">A time-based one-time password.</param>
        /// <param name="TOTPAuthentication">The created HTTP TOTP Authentication.</param>
        /// <param name="Type">The optional TOTP type (raw by default, or bound to the TLS session).</param>
        public static Boolean TryCreate(String                                           Username,
                                        String                                           TOTP,
                                        [NotNullWhen(true)] out HTTPTOTPAuthentication?  TOTPAuthentication,
                                        TOTPHTTPHeaderType                               Type   = TOTPHTTPHeaderType.RAW)
        {

            TOTPAuthentication = null;

            Username = Username.Trim();

            if (Username.IsNullOrEmpty())
                return false;

            TOTPAuthentication = new HTTPTOTPAuthentication(
                                     Username,
                                     TOTP,
                                     Type
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

                // Two segments:   base64(username):base64(totp)             (a raw TOTP, the legacy form)
                // Three segments: type:base64(username):base64(totp)        (type 0 = raw, 1 = TLS channel binding)
                var segments = splitted[1].Trim().Split(splitter2, StringSplitOptions.RemoveEmptyEntries);

                var type    = TOTPHTTPHeaderType.RAW;
                var offset  = 0;

                if (segments.Length == 3)
                {

                    switch (segments[0])
                    {

                        case "0": type = TOTPHTTPHeaderType.RAW;                break;
                        case "1": type = TOTPHTTPHeaderType.TLSChannelBinding;  break;

                        default:
                            return false;

                    }

                    offset = 1;

                }

                if (segments.Length - offset == 2)
                {

                    if (!segments[offset].    TryParseBASE64_UTF8(out var username, out _))
                        return false;

                    if (!segments[offset + 1].TryParseBASE64_UTF8(out var totp,     out _))
                        return false;

                    TOTPAuthentication = new HTTPTOTPAuthentication(
                                             username,
                                             totp,
                                             type
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
        /// <param name="HTTPTOTPAuthentication1">An HTTP TOTP Authentication.</param>
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
        /// <param name="HTTPTOTPAuthentication1">An HTTP TOTP Authentication.</param>
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
        /// <param name="HTTPTOTPAuthentication1">An HTTP TOTP Authentication.</param>
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
        /// <param name="HTTPTOTPAuthentication1">An HTTP TOTP Authentication.</param>
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
        /// <param name="HTTPTOTPAuthentication1">An HTTP TOTP Authentication.</param>
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
        /// <param name="HTTPTOTPAuthentication1">An HTTP TOTP Authentication.</param>
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
        /// <param name="Object">An HTTP TOTP Authentication to compare with.</param>
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
        /// <param name="HTTPTOTPAuthentication">An HTTP TOTP Authentication to compare with.</param>
        public Int32 CompareTo(HTTPTOTPAuthentication? HTTPTOTPAuthentication)
        {

            if (HTTPTOTPAuthentication is null)
                throw new ArgumentNullException(nameof(HTTPTOTPAuthentication),
                                                "The given object HTTP TOTP Authentication must not be null!");

            var c = String.Compare(Username,
                                   HTTPTOTPAuthentication.Username,
                                   StringComparison.Ordinal);

            if (c == 0)
                c = String.Compare(TOTP,
                                   HTTPTOTPAuthentication.TOTP,
                                   StringComparison.Ordinal);

            if (c == 0)
                c = Type.CompareTo(HTTPTOTPAuthentication.Type);

            return c;

        }

        #endregion

        #endregion

        #region IEquatable<HTTPTOTPAuthentication> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two HTTP TOTP Authentications for equality.
        /// </summary>
        /// <param name="Object">An HTTP TOTP Authentication to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is HTTPTOTPAuthentication httpTOTPAuthentication &&
                   Equals(httpTOTPAuthentication);

        #endregion

        #region Equals(HTTPTOTPAuthentication)

        /// <summary>
        /// Compares two HTTP TOTP Authentications for equality.
        /// </summary>
        /// <param name="HTTPTOTPAuthentication">An HTTP TOTP Authentication to compare with.</param>
        public Boolean Equals(HTTPTOTPAuthentication? HTTPTOTPAuthentication)

            => HTTPTOTPAuthentication is not null &&
               Username.Equals(HTTPTOTPAuthentication.Username) &&
               TOTP.    Equals(HTTPTOTPAuthentication.TOTP)     &&
               Type.    Equals(HTTPTOTPAuthentication.Type);

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

                return Username.GetHashCode() * 5 ^
                       TOTP.    GetHashCode() * 3 ^
                       Type.    GetHashCode();

            }
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => Type == TOTPHTTPHeaderType.RAW
                   ? $"TOTP '{Username}':'{TOTP}'"
                   : $"TOTP '{Username}':'{TOTP}' (TLS channel binding)";

        #endregion

    }

}
