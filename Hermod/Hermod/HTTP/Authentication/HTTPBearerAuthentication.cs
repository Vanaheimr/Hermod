/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
    /// A HTTP Bearer Authentication used in token-based authentication systems such as OAuth 2.0.
    /// </summary>
    public sealed class HTTPBearerAuthentication : IHTTPAuthentication,
                                                   IEquatable<HTTPBearerAuthentication>,
                                                   IComparable<HTTPBearerAuthentication>,
                                                   IComparable
    {

        #region Data

        private readonly static Char[] splitter = [ ' ' ];

        #endregion

        #region Properties

        /// <summary>
        /// The authentication token.
        /// </summary>
        public String  Token    { get; }

        /// <summary>
        /// The HTTP request header representation.
        /// </summary>
        public String  HTTPText
            => $"Bearer {Token}";

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP Bearer Authentication based on the given text.
        /// </summary>
        /// <param name="Token">An authentication token.</param>
        private HTTPBearerAuthentication(String Token)
        {
            this.Token = Token;
        }

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given text as a HTTP Bearer Authentication.
        /// </summary>
        /// <param name="Text">A token.</param>
        public static HTTPBearerAuthentication Parse(String Text)
        {

            if (TryParse(Text, out var httpBearerAuthentication))
                return httpBearerAuthentication!;

            throw new ArgumentException("The given text is not valid a valid HTTP Token authentication token!", nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given text a HTTP Bearer Authentication.
        /// </summary>
        /// <param name="Text">A token.</param>
        public static HTTPBearerAuthentication? TryParse(String Text)
        {

            if (TryParse(Text, out var httpBearerAuthentication))
                return httpBearerAuthentication;

            return null;

        }

        #endregion

        #region (static) TryParse(Text, out BearerAuthentication)

        /// <summary>
        /// Try to parse the given text a HTTP Bearer Authentication.
        /// </summary>
        /// <param name="Text">A token.</param>
        /// <param name="BearerAuthentication">The new HTTP Bearer Authentication.</param>
        public static Boolean TryParse(String                                             Text,
                                       [NotNullWhen(true)] out HTTPBearerAuthentication?  BearerAuthentication)
        {

            BearerAuthentication = null;

            Text = Text.Trim();

            if (Text.IsNullOrEmpty())
                return false;

            BearerAuthentication = new HTTPBearerAuthentication(Text);
            return true;

        }

        #endregion


        #region (static) ParseHTTPHeader   (Text)

        /// <summary>
        /// Parse the given text as a HTTP Bearer Authentication header.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP Bearer Authentication header.</param>
        public static HTTPBearerAuthentication ParseHTTPHeader(String Text)
        {

            if (TryParseHTTPHeader(Text, out var httpBearerAuthentication))
                return httpBearerAuthentication!;

            throw new ArgumentException("The given text representation of a HTTP Token authentication header is invalid!", nameof(Text));

        }

        #endregion

        #region (static) TryParseHTTPHeader(Text)

        /// <summary>
        /// Try to parse the given text as a HTTP Bearer Authentication header.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP Bearer Authentication header.</param>
        public static HTTPBearerAuthentication? TryParseHTTPHeader(String Text)
        {

            if (TryParseHTTPHeader(Text, out var httpBearerAuthentication))
                return httpBearerAuthentication;

            return null;

        }

        #endregion

        #region (static) TryParseHTTPHeader(Text, out BearerAuthentication)

        /// <summary>
        /// Try to parse the given text as a HTTP Bearer Authentication header.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP Bearer Authentication header.</param>
        /// <param name="BearerAuthentication">The parsed HTTP Bearer Authentication header.</param>
        public static Boolean TryParseHTTPHeader(String                                             Text,
                                                 [NotNullWhen(true)] out HTTPBearerAuthentication?  BearerAuthentication)
        {

            BearerAuthentication = null;

            if (Text.IsNullOrEmpty())
                return false;

            var splitted = Text.Split(splitter);

            if (splitted.IsNullOrEmpty())
                return false;

            if (splitted.Length == 2 &&
                String.Equals(splitted[0], "Bearer", StringComparison.OrdinalIgnoreCase))
            {

                splitted[1] = splitted[1]?.Trim() ?? "";

                if (splitted[1].IsNullOrEmpty())
                    return false;

                BearerAuthentication = new HTTPBearerAuthentication(splitted[1]);
                return true;

            }

            return false;

        }

        #endregion


        #region Operator overloading

        #region Operator == (HTTPBearerAuthentication1, HTTPBearerAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPBearerAuthentication1">A HTTP Bearer Authentication.</param>
        /// <param name="HTTPBearerAuthentication2">Another HTTP Bearer Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (HTTPBearerAuthentication HTTPBearerAuthentication1,
                                           HTTPBearerAuthentication HTTPBearerAuthentication2)
        {

            if (Object.ReferenceEquals(HTTPBearerAuthentication1, HTTPBearerAuthentication2))
                return true;

            if (HTTPBearerAuthentication1 is null || HTTPBearerAuthentication2 is null)
                return false;

            return HTTPBearerAuthentication1.Equals(HTTPBearerAuthentication2);

        }

        #endregion

        #region Operator != (HTTPBearerAuthentication1, HTTPBearerAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPBearerAuthentication1">A HTTP Bearer Authentication.</param>
        /// <param name="HTTPBearerAuthentication2">Another HTTP Bearer Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPBearerAuthentication HTTPBearerAuthentication1,
                                           HTTPBearerAuthentication HTTPBearerAuthentication2)

            => !(HTTPBearerAuthentication1 == HTTPBearerAuthentication2);

        #endregion

        #region Operator <  (HTTPBearerAuthentication1, HTTPBearerAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPBearerAuthentication1">A HTTP Bearer Authentication.</param>
        /// <param name="HTTPBearerAuthentication2">Another HTTP Bearer Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (HTTPBearerAuthentication HTTPBearerAuthentication1,
                                          HTTPBearerAuthentication HTTPBearerAuthentication2)

            => HTTPBearerAuthentication1 is null
                   ? throw new ArgumentNullException(nameof(HTTPBearerAuthentication1), "The given HTTP Bearer Authentication must not be null!")
                   : HTTPBearerAuthentication1.CompareTo(HTTPBearerAuthentication2) < 0;

        #endregion

        #region Operator <= (HTTPBearerAuthentication1, HTTPBearerAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPBearerAuthentication1">A HTTP Bearer Authentication.</param>
        /// <param name="HTTPBearerAuthentication2">Another HTTP Bearer Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (HTTPBearerAuthentication HTTPBearerAuthentication1,
                                           HTTPBearerAuthentication HTTPBearerAuthentication2)

            => !(HTTPBearerAuthentication1 > HTTPBearerAuthentication2);

        #endregion

        #region Operator >  (HTTPBearerAuthentication1, HTTPBearerAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPBearerAuthentication1">A HTTP Bearer Authentication.</param>
        /// <param name="HTTPBearerAuthentication2">Another HTTP Bearer Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (HTTPBearerAuthentication HTTPBearerAuthentication1,
                                          HTTPBearerAuthentication HTTPBearerAuthentication2)

            => HTTPBearerAuthentication1 is null
                   ? throw new ArgumentNullException(nameof(HTTPBearerAuthentication1), "The given HTTP Bearer Authentication must not be null!")
                   : HTTPBearerAuthentication1.CompareTo(HTTPBearerAuthentication2) > 0;

        #endregion

        #region Operator >= (HTTPBearerAuthentication1, HTTPBearerAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPBearerAuthentication1">A HTTP Bearer Authentication.</param>
        /// <param name="HTTPBearerAuthentication2">Another HTTP Bearer Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (HTTPBearerAuthentication HTTPBearerAuthentication1,
                                           HTTPBearerAuthentication HTTPBearerAuthentication2)

            => !(HTTPBearerAuthentication1 < HTTPBearerAuthentication2);

        #endregion

        #endregion

        #region IComparable<HTTPBearerAuthentication> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two HTTP Bearer Authentications.
        /// </summary>
        /// <param name="Object">A HTTP Bearer Authentication to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is HTTPBearerAuthentication httpBearerAuthentication
                   ? CompareTo(httpBearerAuthentication)
                   : throw new ArgumentException("The given object is not a HTTP Bearer Authentication!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(HTTPBearerAuthentication)

        /// <summary>
        /// Compares two HTTP Bearer Authentications.
        /// </summary>
        /// <param name="HTTPBearerAuthentication">A HTTP Bearer Authentication to compare with.</param>
        public Int32 CompareTo(HTTPBearerAuthentication? HTTPBearerAuthentication)
        {

            if (HTTPBearerAuthentication is null)
                throw new ArgumentNullException(nameof(HTTPBearerAuthentication),
                                                "The given object HTTP Bearer Authentication must not be null!");

            return String.Compare(Token,
                                  HTTPBearerAuthentication.Token,
                                  StringComparison.Ordinal);

        }

        #endregion

        #endregion

        #region IEquatable<HTTPBearerAuthentication> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two HTTP Bearer Authentications for equality.
        /// </summary>
        /// <param name="Object">A HTTP Bearer Authentication to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is HTTPBearerAuthentication httpBearerAuthentication &&
                   Equals(httpBearerAuthentication);

        #endregion

        #region Equals(HTTPBearerAuthentication)

        /// <summary>
        /// Compares two HTTP Bearer Authentications for equality.
        /// </summary>
        /// <param name="HTTPBearerAuthentication">A HTTP Bearer Authentication to compare with.</param>
        public Boolean Equals(HTTPBearerAuthentication? HTTPBearerAuthentication)

            => HTTPBearerAuthentication is not null &&
               Token.Equals(HTTPBearerAuthentication.Token);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        /// <returns>The hash code of this object.</returns>
        public override Int32 GetHashCode()
            => Token.GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
            => $"Bearer '{Token}'";

        #endregion

    }

}
