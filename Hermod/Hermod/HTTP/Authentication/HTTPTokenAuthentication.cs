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

using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// An HTTP Token Authentication.
    /// </summary>
    public sealed class HTTPTokenAuthentication : IHTTPAuthentication,
                                                  IEquatable<HTTPTokenAuthentication>,
                                                  IComparable<HTTPTokenAuthentication>,
                                                  IComparable
    {

        #region Data

        // All Unicode code points from U+0021 to U+007E
        private static readonly Regex   tokenRegExpr  = new (@"^[A-Za-z0-9!""#$%&'()\*\+,\-\./:;<=>\?@\[\]\\^_`{}\|~]*$");

        private static readonly Char[]  splitter      = [ ' ' ];

        #endregion

        #region Properties

        /// <summary>
        /// The username.
        /// </summary>
        public String  Token    { get; }

        /// <summary>
        /// The HTTP request header representation.
        /// </summary>
        public String  HTTPText
            => $"Token {Token}";

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP Token Authentication based on the given text.
        /// </summary>
        /// <param name="Token">An authentication token.</param>
        private HTTPTokenAuthentication(String Token)
        {
            this.Token = Token;
        }

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given text as a HTTP Token Authentication.
        /// </summary>
        /// <param name="Text">A token.</param>
        public static HTTPTokenAuthentication Parse(String Text)
        {

            if (TryParse(Text, out var httpTokenAuthentication))
                return httpTokenAuthentication!;

            throw new ArgumentException("The given text is not valid a valid HTTP Token authentication token!", nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given text a HTTP Token Authentication.
        /// </summary>
        /// <param name="Text">A token.</param>
        public static HTTPTokenAuthentication? TryParse(String Text)
        {

            if (TryParse(Text, out var httpTokenAuthentication))
                return httpTokenAuthentication;

            return null;

        }

        #endregion

        #region (static) TryParse(Text, out TokenAuthentication)

        /// <summary>
        /// Try to parse the given text a HTTP Token Authentication.
        /// </summary>
        /// <param name="Text">A token.</param>
        /// <param name="TokenAuthentication">The new HTTP Token Authentication.</param>
        public static Boolean TryParse(String Text, out HTTPTokenAuthentication? TokenAuthentication)
        {

            TokenAuthentication = null;

            Text = Text.Trim();

            if (Text.IsNullOrEmpty())
                return false;

            if (tokenRegExpr.IsMatch(Text))
            {
                TokenAuthentication = new HTTPTokenAuthentication(Text);
                return true;
            }

            return false;

        }

        #endregion


        #region (static) ParseHTTPHeader   (Text)

        /// <summary>
        /// Parse the given text as a HTTP Token Authentication header.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP Token Authentication header.</param>
        public static HTTPTokenAuthentication ParseHTTPHeader(String Text)
        {

            if (TryParseHTTPHeader(Text, out var httpTokenAuthentication))
                return httpTokenAuthentication!;

            throw new ArgumentException("The given text representation of a HTTP Token authentication header is invalid!", nameof(Text));

        }

        #endregion

        #region (static) TryParseHTTPHeader(Text)

        /// <summary>
        /// Try to parse the given text as a HTTP Token Authentication header.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP Token Authentication header.</param>
        public static HTTPTokenAuthentication? TryParseHTTPHeader(String Text)
        {

            if (TryParseHTTPHeader(Text, out var httpTokenAuthentication))
                return httpTokenAuthentication;

            return null;

        }

        #endregion

        #region (static) TryParseHTTPHeader(Text, out TokenAuthentication)

        /// <summary>
        /// Try to parse the given text as a HTTP Token Authentication header.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP Token Authentication header.</param>
        /// <param name="TokenAuthentication">The parsed HTTP Token Authentication header.</param>
        public static Boolean TryParseHTTPHeader(String                                            Text,
                                                 [NotNullWhen(true)] out HTTPTokenAuthentication?  TokenAuthentication)
        {

            TokenAuthentication = null;

            Text = Text.Trim();

            if (Text.IsNullOrEmpty())
                return false;

            var splitted = Text.Split(splitter, StringSplitOptions.RemoveEmptyEntries);

            if (splitted.IsNullOrEmpty())
                return false;

            if (splitted.Length == 2 &&
                String.Equals(splitted[0], "token", StringComparison.OrdinalIgnoreCase))
            {
                if (tokenRegExpr.IsMatch(splitted[1]))
                {
                    TokenAuthentication = new HTTPTokenAuthentication(splitted[1]);
                    return true;
                }
            }

            return false;

        }

        #endregion


        #region Operator overloading

        #region Operator == (HTTPTokenAuthentication1, HTTPTokenAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPTokenAuthentication1">An HTTP Token Authentication.</param>
        /// <param name="HTTPTokenAuthentication2">Another HTTP Token Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (HTTPTokenAuthentication HTTPTokenAuthentication1,
                                           HTTPTokenAuthentication HTTPTokenAuthentication2)
        {

            if (Object.ReferenceEquals(HTTPTokenAuthentication1, HTTPTokenAuthentication2))
                return true;

            if (HTTPTokenAuthentication1 is null || HTTPTokenAuthentication2 is null)
                return false;

            return HTTPTokenAuthentication1.Equals(HTTPTokenAuthentication2);

        }

        #endregion

        #region Operator != (HTTPTokenAuthentication1, HTTPTokenAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPTokenAuthentication1">An HTTP Token Authentication.</param>
        /// <param name="HTTPTokenAuthentication2">Another HTTP Token Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPTokenAuthentication HTTPTokenAuthentication1,
                                           HTTPTokenAuthentication HTTPTokenAuthentication2)

            => !(HTTPTokenAuthentication1 == HTTPTokenAuthentication2);

        #endregion

        #region Operator <  (HTTPTokenAuthentication1, HTTPTokenAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPTokenAuthentication1">An HTTP Token Authentication.</param>
        /// <param name="HTTPTokenAuthentication2">Another HTTP Token Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (HTTPTokenAuthentication HTTPTokenAuthentication1,
                                          HTTPTokenAuthentication HTTPTokenAuthentication2)

            => HTTPTokenAuthentication1 is null
                   ? throw new ArgumentNullException(nameof(HTTPTokenAuthentication1), "The given HTTP Token Authentication must not be null!")
                   : HTTPTokenAuthentication1.CompareTo(HTTPTokenAuthentication2) < 0;

        #endregion

        #region Operator <= (HTTPTokenAuthentication1, HTTPTokenAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPTokenAuthentication1">An HTTP Token Authentication.</param>
        /// <param name="HTTPTokenAuthentication2">Another HTTP Token Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (HTTPTokenAuthentication HTTPTokenAuthentication1,
                                           HTTPTokenAuthentication HTTPTokenAuthentication2)

            => !(HTTPTokenAuthentication1 > HTTPTokenAuthentication2);

        #endregion

        #region Operator >  (HTTPTokenAuthentication1, HTTPTokenAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPTokenAuthentication1">An HTTP Token Authentication.</param>
        /// <param name="HTTPTokenAuthentication2">Another HTTP Token Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (HTTPTokenAuthentication HTTPTokenAuthentication1,
                                          HTTPTokenAuthentication HTTPTokenAuthentication2)

            => HTTPTokenAuthentication1 is null
                   ? throw new ArgumentNullException(nameof(HTTPTokenAuthentication1), "The given HTTP Token Authentication must not be null!")
                   : HTTPTokenAuthentication1.CompareTo(HTTPTokenAuthentication2) > 0;

        #endregion

        #region Operator >= (HTTPTokenAuthentication1, HTTPTokenAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPTokenAuthentication1">An HTTP Token Authentication.</param>
        /// <param name="HTTPTokenAuthentication2">Another HTTP Token Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (HTTPTokenAuthentication HTTPTokenAuthentication1,
                                           HTTPTokenAuthentication HTTPTokenAuthentication2)

            => !(HTTPTokenAuthentication1 < HTTPTokenAuthentication2);

        #endregion

        #endregion

        #region IComparable<HTTPTokenAuthentication> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two HTTP Token Authentications.
        /// </summary>
        /// <param name="Object">An HTTP Token Authentication to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is HTTPTokenAuthentication httpTokenAuthentication
                   ? CompareTo(httpTokenAuthentication)
                   : throw new ArgumentException("The given object is not a HTTP Token Authentication!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(HTTPTokenAuthentication)

        /// <summary>
        /// Compares two HTTP Token Authentications.
        /// </summary>
        /// <param name="HTTPTokenAuthentication">An HTTP Token Authentication to compare with.</param>
        public Int32 CompareTo(HTTPTokenAuthentication? HTTPTokenAuthentication)
        {

            if (HTTPTokenAuthentication is null)
                throw new ArgumentNullException(nameof(HTTPTokenAuthentication),
                                                "The given object HTTP Token Authentication must not be null!");

            return String.Compare(Token,
                                  HTTPTokenAuthentication.Token,
                                  StringComparison.Ordinal);

        }

        #endregion

        #endregion

        #region IEquatable<HTTPTokenAuthentication> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two HTTP Token Authentications for equality.
        /// </summary>
        /// <param name="Object">An HTTP Token Authentication to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is HTTPTokenAuthentication httpTokenAuthentication &&
                   Equals(httpTokenAuthentication);

        #endregion

        #region Equals(HTTPTokenAuthentication)

        /// <summary>
        /// Compares two HTTP Token Authentications for equality.
        /// </summary>
        /// <param name="HTTPTokenAuthentication">An HTTP Token Authentication to compare with.</param>
        public Boolean Equals(HTTPTokenAuthentication? HTTPTokenAuthentication)

            => HTTPTokenAuthentication is not null &&
               Token.Equals(HTTPTokenAuthentication.Token);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()
            => Token.GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
            => $"Token '{Token}'";

        #endregion

    }

}
