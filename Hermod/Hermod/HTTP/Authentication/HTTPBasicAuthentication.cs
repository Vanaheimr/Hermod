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
    /// An HTTP Basic Authentication.
    /// </summary>
    public sealed class HTTPBasicAuthentication : IHTTPAuthentication,
                                                  IEquatable<HTTPBasicAuthentication>,
                                                  IComparable<HTTPBasicAuthentication>,
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
        /// The password.
        /// </summary>
        public String  Password    { get; }

        /// <summary>
        /// The HTTP request header representation.
        /// </summary>
        public String  HTTPText
            => $"Basic {$"{Username}:{Password}".ToBase64()}";

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP Basic Authentication based on the given username and password.
        /// </summary>
        /// <param name="Username">A username.</param>
        /// <param name="Password">A password.</param>
        private HTTPBasicAuthentication(String  Username,
                                        String  Password)
        {

            this.Username  = Username;
            this.Password  = Password;

        }

        #endregion


        #region (static) Create    (Username, Password)

        /// <summary>
        /// Create a HTTP Basic Authentication based on the given username and password.
        /// </summary>
        /// <param name="Username">A username.</param>
        /// <param name="Password">A password.</param>
        public static HTTPBasicAuthentication Create(String  Username,
                                                     String  Password)
        {

            if (TryCreate(Username,
                          Password,
                          out var httpBasicAuthentication))
            {
                return httpBasicAuthentication;
            }

            throw new ArgumentException($"The given username '{Username}' or password '{Password}' is invalid!");

        }

        #endregion

        #region (static) TryCreate (Username, Password)

        /// <summary>
        /// Try to create a HTTP Basic Authentication based on the given username and password.
        /// </summary>
        /// <param name="Username">A username.</param>
        /// <param name="Password">A password.</param>
        public static HTTPBasicAuthentication? TryCreate(String  Username,
                                                         String  Password)
        {

            if (TryCreate(Username,
                          Password,
                          out var httpBasicAuthentication))
            {
                return httpBasicAuthentication;
            }

            return null;

        }

        #endregion

        #region (static) TryCreate (Username, Password, out BasicAuthentication)

        /// <summary>
        /// Try to create a HTTP Basic Authentication based on the given username and password.
        /// </summary>
        /// <param name="Username">A username.</param>
        /// <param name="Password">A password.</param>
        /// <param name="BasicAuthentication">The created HTTP Basic Authentication.</param>
        public static Boolean TryCreate(String                                            Username,
                                        String                                            Password,
                                        [NotNullWhen(true)] out HTTPBasicAuthentication?  BasicAuthentication)
        {

            BasicAuthentication = null;

            Username = Username.Trim();

            if (Username.IsNullOrEmpty())
                return false;

            BasicAuthentication = new HTTPBasicAuthentication(
                                      Username,
                                      Password
                                  );

            return true;

        }

        #endregion


        #region (static) ParseHTTPHeader    (Text)

        /// <summary>
        /// Parse the given text as a HTTP Basic Authentication header.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP Basic Authentication header.</param>
        public static HTTPBasicAuthentication ParseHTTPHeader(String Text)
        {

            if (TryParseHTTPHeader(Text, out var httpBasicAuthentication))
                return httpBasicAuthentication!;

            throw new ArgumentException("The given text representation of a HTTP Basic Authentication header is invalid!", nameof(Text));

        }

        #endregion

        #region (static) TryParseHTTPHeader (Text)

        /// <summary>
        /// Try to parse the given text as a HTTP Basic Authentication header.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP Basic Authentication header.</param>
        public static HTTPBasicAuthentication? TryParseHTTPHeader(String Text)
        {

            if (TryParseHTTPHeader(Text, out var httpBasicAuthentication))
                return httpBasicAuthentication;

            return null;

        }

        #endregion

        #region (static) TryParseHTTPHeader (Text, out BasicAuthentication)

        /// <summary>
        /// Try to parse the given text as a HTTP Basic Authentication header.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP Basic Authentication header.</param>
        /// <param name="BasicAuthentication">The parsed HTTP Basic Authentication header.</param>
        public static Boolean TryParseHTTPHeader(String                                            Text,
                                                 [NotNullWhen(true)] out HTTPBasicAuthentication?  BasicAuthentication)
        {

            BasicAuthentication = null;

            Text = Text.Trim();

            if (Text.IsNullOrEmpty())
                return false;

            var splitted = Text.Split(splitter1, StringSplitOptions.RemoveEmptyEntries);

            if (splitted.Length == 2 &&
                String.Equals(splitted[0], "Basic", StringComparison.OrdinalIgnoreCase))
            {

                var credentials  = Encoding.UTF8.GetString(Convert.FromBase64String(splitted[1])).
                                                 Split    (splitter2, 2);

                if (credentials.Length == 2)
                {

                    BasicAuthentication = new HTTPBasicAuthentication(
                                              credentials[0],
                                              credentials[1]
                                          );

                    return true;

                }

            }

            return false;

        }

        #endregion


        #region Operator overloading

        #region Operator == (HTTPBasicAuthentication1, HTTPBasicAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPBasicAuthentication1">An HTTP Basic Authentication.</param>
        /// <param name="HTTPBasicAuthentication2">Another HTTP Basic Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (HTTPBasicAuthentication HTTPBasicAuthentication1,
                                           HTTPBasicAuthentication HTTPBasicAuthentication2)
        {

            if (Object.ReferenceEquals(HTTPBasicAuthentication1, HTTPBasicAuthentication2))
                return true;

            if (HTTPBasicAuthentication1 is null || HTTPBasicAuthentication2 is null)
                return false;

            return HTTPBasicAuthentication1.Equals(HTTPBasicAuthentication2);

        }

        #endregion

        #region Operator != (HTTPBasicAuthentication1, HTTPBasicAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPBasicAuthentication1">An HTTP Basic Authentication.</param>
        /// <param name="HTTPBasicAuthentication2">Another HTTP Basic Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPBasicAuthentication HTTPBasicAuthentication1,
                                           HTTPBasicAuthentication HTTPBasicAuthentication2)

            => !(HTTPBasicAuthentication1 == HTTPBasicAuthentication2);

        #endregion

        #region Operator <  (HTTPBasicAuthentication1, HTTPBasicAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPBasicAuthentication1">An HTTP Basic Authentication.</param>
        /// <param name="HTTPBasicAuthentication2">Another HTTP Basic Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (HTTPBasicAuthentication HTTPBasicAuthentication1,
                                          HTTPBasicAuthentication HTTPBasicAuthentication2)

            => HTTPBasicAuthentication1 is null
                   ? throw new ArgumentNullException(nameof(HTTPBasicAuthentication1), "The given HTTP Basic Authentication must not be null!")
                   : HTTPBasicAuthentication1.CompareTo(HTTPBasicAuthentication2) < 0;

        #endregion

        #region Operator <= (HTTPBasicAuthentication1, HTTPBasicAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPBasicAuthentication1">An HTTP Basic Authentication.</param>
        /// <param name="HTTPBasicAuthentication2">Another HTTP Basic Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (HTTPBasicAuthentication HTTPBasicAuthentication1,
                                           HTTPBasicAuthentication HTTPBasicAuthentication2)

            => !(HTTPBasicAuthentication1 > HTTPBasicAuthentication2);

        #endregion

        #region Operator >  (HTTPBasicAuthentication1, HTTPBasicAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPBasicAuthentication1">An HTTP Basic Authentication.</param>
        /// <param name="HTTPBasicAuthentication2">Another HTTP Basic Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (HTTPBasicAuthentication HTTPBasicAuthentication1,
                                          HTTPBasicAuthentication HTTPBasicAuthentication2)

            => HTTPBasicAuthentication1 is null
                   ? throw new ArgumentNullException(nameof(HTTPBasicAuthentication1), "The given HTTP Basic Authentication must not be null!")
                   : HTTPBasicAuthentication1.CompareTo(HTTPBasicAuthentication2) > 0;

        #endregion

        #region Operator >= (HTTPBasicAuthentication1, HTTPBasicAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPBasicAuthentication1">An HTTP Basic Authentication.</param>
        /// <param name="HTTPBasicAuthentication2">Another HTTP Basic Authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (HTTPBasicAuthentication HTTPBasicAuthentication1,
                                           HTTPBasicAuthentication HTTPBasicAuthentication2)

            => !(HTTPBasicAuthentication1 < HTTPBasicAuthentication2);

        #endregion

        #endregion

        #region IComparable<HTTPBasicAuthentication> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two HTTP Basic Authentications.
        /// </summary>
        /// <param name="Object">An HTTP Basic Authentication to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is HTTPBasicAuthentication httpBasicAuthentication
                   ? CompareTo(httpBasicAuthentication)
                   : throw new ArgumentException("The given object is not a HTTP Basic Authentication!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(HTTPBasicAuthentication)

        /// <summary>
        /// Compares two HTTP Basic Authentications.
        /// </summary>
        /// <param name="HTTPBasicAuthentication">An HTTP Basic Authentication to compare with.</param>
        public Int32 CompareTo(HTTPBasicAuthentication? HTTPBasicAuthentication)
        {

            if (HTTPBasicAuthentication is null)
                throw new ArgumentNullException(nameof(HTTPBasicAuthentication),
                                                "The given object HTTP Basic Authentication must not be null!");

            var c = String.Compare(Username,
                                   HTTPBasicAuthentication.Username,
                                   StringComparison.Ordinal);

            if (c == 0)
                String.Compare(Password,
                               HTTPBasicAuthentication.Password,
                               StringComparison.Ordinal);

            return c;

        }

        #endregion

        #endregion

        #region IEquatable<HTTPBasicAuthentication> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two HTTP Basic Authentications for equality.
        /// </summary>
        /// <param name="Object">An HTTP Basic Authentication to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is HTTPBasicAuthentication httpBasicAuthentication &&
                   Equals(httpBasicAuthentication);

        #endregion

        #region Equals(HTTPBasicAuthentication)

        /// <summary>
        /// Compares two HTTP Basic Authentications for equality.
        /// </summary>
        /// <param name="HTTPBasicAuthentication">An HTTP Basic Authentication to compare with.</param>
        public Boolean Equals(HTTPBasicAuthentication? HTTPBasicAuthentication)

            => HTTPBasicAuthentication is not null &&
               Username.Equals(HTTPBasicAuthentication.Username) &&
               Password.Equals(HTTPBasicAuthentication.Password);

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
                       Password.GetHashCode();

            }
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
            => $"Basic '{Username}':'{Password}'";

        #endregion

    }

}
