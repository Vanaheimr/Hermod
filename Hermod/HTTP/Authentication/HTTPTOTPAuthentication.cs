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

using System.Text;
using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// An HTTP Time-based One-Time Password Authentication: the
    /// "Authorization: TOTP" scheme of the TOTP HTTP authentication
    /// specification, using RFC 9110 auth-params:
    ///
    ///     TOTP login="&lt;b64&gt;", totp="&lt;b64&gt;"[, tlscb=true|false]
    ///
    /// login and totp are MANDATORY (Base64 of the UTF-8 text); tlscb is
    /// OPTIONAL and DEFAULTS TO TRUE - secure by default: a credential that
    /// says nothing about channel binding claims the TLS-bound mode, and an
    /// unbound deployment must say tlscb=false explicitly. Unknown
    /// parameters are ignored (the standard auth-param extension point),
    /// duplicates are rejected. The login names a role - a charging station,
    /// a backend service - deliberately not a "user": nothing about it
    /// implies a natural person.
    /// </summary>
    public sealed class HTTPTOTPAuthentication : IHTTPAuthentication,
                                                 IEquatable<HTTPTOTPAuthentication>,
                                                 IComparable<HTTPTOTPAuthentication>,
                                                 IComparable
    {

        #region Properties

        /// <summary>
        /// The login: the name under which the verifier looks up the TOTP
        /// configuration - typically a role, not a natural person.
        /// </summary>
        public String              Login    { get; }

        /// <summary>
        /// The time-based one-time password.
        /// </summary>
        public String              TOTP     { get; }

        /// <summary>
        /// The TOTP type: bound to the TLS session via TLS v1.3 exporter
        /// material (the default, tlscb=true), or a raw TOTP (tlscb=false).
        /// </summary>
        public TOTPHTTPHeaderType  Type     { get; }

        /// <summary>
        /// The HTTP request header representation. The canonical form sends
        /// login and totp as quoted strings (Base64 padding '=' is not a
        /// token character) and omits tlscb at its default (true).
        /// </summary>
        public String  HTTPText

            => Type == TOTPHTTPHeaderType.RAW
                   ? $"TOTP login=\"{Login.ToBase64()}\", totp=\"{TOTP.ToBase64()}\", tlscb=false"
                   : $"TOTP login=\"{Login.ToBase64()}\", totp=\"{TOTP.ToBase64()}\"";

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP TOTP Authentication based on the given login and time-based one-time password.
        /// </summary>
        /// <param name="Login">A login (a role, not necessarily a natural person).</param>
        /// <param name="TOTP">A time-based one-time password.</param>
        /// <param name="Type">The optional TOTP type (bound to the TLS session by default).</param>
        private HTTPTOTPAuthentication(String              Login,
                                       String              TOTP,
                                       TOTPHTTPHeaderType  Type   = TOTPHTTPHeaderType.TLSChannelBinding)
        {

            this.Login  = Login;
            this.TOTP   = TOTP;
            this.Type   = Type;

        }

        #endregion


        #region (static) Create    (Login, TOTP, Type = TLSChannelBinding)

        /// <summary>
        /// Create a HTTP TOTP Authentication based on the given login and time-based one-time password.
        /// </summary>
        /// <param name="Login">A login (a role, not necessarily a natural person).</param>
        /// <param name="TOTP">A time-based one-time password.</param>
        /// <param name="Type">The optional TOTP type (bound to the TLS session by default).</param>
        public static HTTPTOTPAuthentication Create(String              Login,
                                                    String              TOTP,
                                                    TOTPHTTPHeaderType  Type   = TOTPHTTPHeaderType.TLSChannelBinding)
        {

            if (TryCreate(Login,
                          TOTP,
                          out var httpTOTPAuthentication,
                          Type))
            {
                return httpTOTPAuthentication;
            }

            throw new ArgumentException($"The given login '{Login}' or time-based one-time password '{TOTP}' is invalid!");

        }

        #endregion

        #region (static) TryCreate (Login, TOTP, Type = TLSChannelBinding)

        /// <summary>
        /// Try to create a HTTP TOTP Authentication based on the given login and time-based one-time password.
        /// </summary>
        /// <param name="Login">A login (a role, not necessarily a natural person).</param>
        /// <param name="TOTP">A time-based one-time password.</param>
        /// <param name="Type">The optional TOTP type (bound to the TLS session by default).</param>
        public static HTTPTOTPAuthentication? TryCreate(String              Login,
                                                        String              TOTP,
                                                        TOTPHTTPHeaderType  Type   = TOTPHTTPHeaderType.TLSChannelBinding)
        {

            if (TryCreate(Login,
                          TOTP,
                          out var httpTOTPAuthentication,
                          Type))
            {
                return httpTOTPAuthentication;
            }

            return null;

        }

        #endregion

        #region (static) TryCreate (Login, TOTP, out TOTPAuthentication, Type = TLSChannelBinding)

        /// <summary>
        /// Try to create a HTTP TOTP Authentication based on the given login and time-based one-time password.
        /// </summary>
        /// <param name="Login">A login (a role, not necessarily a natural person).</param>
        /// <param name="TOTP">A time-based one-time password.</param>
        /// <param name="TOTPAuthentication">The created HTTP TOTP Authentication.</param>
        /// <param name="Type">The optional TOTP type (bound to the TLS session by default).</param>
        public static Boolean TryCreate(String                                           Login,
                                        String                                           TOTP,
                                        [NotNullWhen(true)] out HTTPTOTPAuthentication?  TOTPAuthentication,
                                        TOTPHTTPHeaderType                               Type   = TOTPHTTPHeaderType.TLSChannelBinding)
        {

            TOTPAuthentication = null;

            Login = Login.Trim();

            if (Login.IsNullOrEmpty())
                return false;

            TOTPAuthentication = new HTTPTOTPAuthentication(
                                     Login,
                                     TOTP,
                                     Type
                                 );

            return true;

        }

        #endregion


        #region (private static) TryParseAuthParams(Text, out Parameters)

        /// <summary>
        /// Parse a comma-separated list of RFC 9110 auth-params. Parameter
        /// names are case-insensitive; values come as tokens or quoted
        /// strings (with quoted-pair unescaping); duplicates fail.
        /// </summary>
        private static Boolean TryParseAuthParams(String                          Text,
                                                  out Dictionary<String, String>  Parameters)
        {

            Parameters = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);

            var i = 0;

            while (i < Text.Length)
            {

                while (i < Text.Length && (Text[i] == ' ' || Text[i] == '\t'))
                    i++;

                if (i >= Text.Length)
                    break;

                #region Parameter name

                var nameStart = i;

                while (i < Text.Length && Text[i] != '=' && Text[i] != ',' &&
                       Text[i] != ' '  && Text[i] != '\t')
                    i++;

                var name = Text[nameStart..i];

                if (name.Length == 0)
                    return false;

                while (i < Text.Length && (Text[i] == ' ' || Text[i] == '\t'))
                    i++;

                if (i >= Text.Length || Text[i] != '=')
                    return false;

                i++;

                while (i < Text.Length && (Text[i] == ' ' || Text[i] == '\t'))
                    i++;

                #endregion

                #region Parameter value (token or quoted-string)

                String value;

                if (i < Text.Length && Text[i] == '"')
                {

                    i++;
                    var stringBuilder = new StringBuilder();
                    var closed        = false;

                    while (i < Text.Length)
                    {

                        var character = Text[i++];

                        if (character == '\\' && i < Text.Length)
                            stringBuilder.Append(Text[i++]);

                        else if (character == '"')
                        {
                            closed = true;
                            break;
                        }

                        else
                            stringBuilder.Append(character);

                    }

                    if (!closed)
                        return false;

                    value = stringBuilder.ToString();

                }
                else
                {

                    var valueStart = i;

                    while (i < Text.Length && Text[i] != ',' &&
                           Text[i] != ' '  && Text[i] != '\t')
                        i++;

                    value = Text[valueStart..i];

                    if (value.Length == 0)
                        return false;

                }

                #endregion

                // Duplicate parameters are ambiguous - fail closed.
                if (!Parameters.TryAdd(name, value))
                    return false;

                while (i < Text.Length && (Text[i] == ' ' || Text[i] == '\t'))
                    i++;

                if (i < Text.Length)
                {

                    if (Text[i] != ',')
                        return false;

                    i++;

                }

            }

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

            #region Scheme name

            var schemeEnd = Text.IndexOfAny([ ' ', '\t' ]);

            if (schemeEnd < 0)
                return false;

            if (!String.Equals(Text[..schemeEnd], "TOTP", StringComparison.OrdinalIgnoreCase))
                return false;

            #endregion

            if (!TryParseAuthParams(Text[schemeEnd..], out var parameters))
                return false;

            #region login       [mandatory]

            if (!parameters.TryGetValue("login", out var loginBase64) ||
                !loginBase64.TryParseBASE64_UTF8(out var login, out _))
            {
                return false;
            }

            #endregion

            #region totp        [mandatory]

            if (!parameters.TryGetValue("totp", out var totpBase64) ||
                !totpBase64.TryParseBASE64_UTF8(out var totp, out _))
            {
                return false;
            }

            #endregion

            #region tlscb       [optional, default: true]

            var type = TOTPHTTPHeaderType.TLSChannelBinding;

            if (parameters.TryGetValue("tlscb", out var tlscb))
            {

                if      (String.Equals(tlscb, "true",  StringComparison.OrdinalIgnoreCase))
                    type = TOTPHTTPHeaderType.TLSChannelBinding;

                else if (String.Equals(tlscb, "false", StringComparison.OrdinalIgnoreCase))
                    type = TOTPHTTPHeaderType.RAW;

                else
                    return false;

            }

            #endregion

            // Unknown parameters are ignored by design:
            // the standard auth-param extension point.

            return TryCreate(
                       login,
                       totp,
                       out TOTPAuthentication,
                       type
                   );

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

            var c = String.Compare(Login,
                                   HTTPTOTPAuthentication.Login,
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
               Login.Equals(HTTPTOTPAuthentication.Login) &&
               TOTP. Equals(HTTPTOTPAuthentication.TOTP)  &&
               Type. Equals(HTTPTOTPAuthentication.Type);

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

                return Login.GetHashCode() * 5 ^
                       TOTP. GetHashCode() * 3 ^
                       Type. GetHashCode();

            }
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => Type == TOTPHTTPHeaderType.RAW
                   ? $"TOTP '{Login}':'{TOTP}'"
                   : $"TOTP '{Login}':'{TOTP}' (TLS channel binding)";

        #endregion

    }

}
