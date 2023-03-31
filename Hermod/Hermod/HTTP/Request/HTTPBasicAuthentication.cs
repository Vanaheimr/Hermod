/*
 * Copyright (c) 2010-2023 GraphDefined GmbH
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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A HTTP basic authentication.
    /// </summary>
    public class HTTPBasicAuthentication : IHTTPAuthentication,
                                           IEquatable<HTTPBasicAuthentication>,
                                           IComparable<HTTPBasicAuthentication>,
                                           IComparable
    {

        #region Properties

        /// <summary>
        /// The username.
        /// </summary>
        public String                   Username              { get; }

        /// <summary>
        /// The password.
        /// </summary>
        public String                   Password              { get; }

        /// <summary>
        /// The type of the HTTP authentication.
        /// </summary>
        public HTTPAuthenticationTypes  HTTPCredentialType    { get; }


        public String HTTPText

            => $"Basic " + $"{Username}:{Password}".ToBase64();

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create the credentials based on a base64 encoded string which comes from a HTTP header Authentication:
        /// </summary>
        /// <param name="Username">The username.</param>
        /// <param name="Password">The password.</param>
        public HTTPBasicAuthentication(String  Username,
                                       String  Password)
        {

            #region Initial checks

            if (Username.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Username),  "The given username must not be null or empty!");

            if (Password.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Password),  "The given password must not be null or empty!");

            #endregion

            this.HTTPCredentialType  = HTTPAuthenticationTypes.Basic;
            this.Username            = Username;
            this.Password            = Password;

        }

        #endregion


        #region (static) TryParse(Text, out BasicAuthentication)

        /// <summary>
        /// Try to parse the given text.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP basic authentication header.</param>
        /// <param name="BasicAuthentication">The parsed HTTP basic authentication header.</param>
        /// <returns>true, when the parsing was successful, else false.</returns>
        public static Boolean TryParse(String Text, out HTTPBasicAuthentication? BasicAuthentication)
        {

            BasicAuthentication = null;

            if (Text.IsNullOrEmpty())
                return false;

            Text = Text.Trim();

            if (Text.IsNullOrEmpty())
                return false;

            var splitted = Text.Split(new Char[] { ' ' });

            if (splitted.IsNullOrEmpty())
                return false;

            if (splitted.Length == 2 &&
                String.Equals(splitted[0], "basic", StringComparison.OrdinalIgnoreCase))
            {

                var usernamePassword = splitted[1].FromBase64_UTF8().Split(new Char[] { ':' });

                if (usernamePassword.IsNullOrEmpty())
                    return false;

                BasicAuthentication = new HTTPBasicAuthentication(
                                          usernamePassword[0],
                                          usernamePassword[1]
                                      );

                return true;

            }

            return false;

        }

        #endregion


        #region Operator overloading

        #region Operator == (HTTPBasicAuthentication1, HTTPBasicAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPBasicAuthentication1">A HTTP basic authentication.</param>
        /// <param name="HTTPBasicAuthentication2">Another HTTP basic authentication.</param>
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
        /// <param name="HTTPBasicAuthentication1">A HTTP basic authentication.</param>
        /// <param name="HTTPBasicAuthentication2">Another HTTP basic authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPBasicAuthentication HTTPBasicAuthentication1,
                                           HTTPBasicAuthentication HTTPBasicAuthentication2)

            => !(HTTPBasicAuthentication1 == HTTPBasicAuthentication2);

        #endregion

        #region Operator <  (HTTPBasicAuthentication1, HTTPBasicAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPBasicAuthentication1">A HTTP basic authentication.</param>
        /// <param name="HTTPBasicAuthentication2">Another HTTP basic authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (HTTPBasicAuthentication HTTPBasicAuthentication1,
                                          HTTPBasicAuthentication HTTPBasicAuthentication2)

            => HTTPBasicAuthentication1 is null
                   ? throw new ArgumentNullException(nameof(HTTPBasicAuthentication1), "The given HTTP basic authentication must not be null!")
                   : HTTPBasicAuthentication1.CompareTo(HTTPBasicAuthentication2) < 0;

        #endregion

        #region Operator <= (HTTPBasicAuthentication1, HTTPBasicAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPBasicAuthentication1">A HTTP basic authentication.</param>
        /// <param name="HTTPBasicAuthentication2">Another HTTP basic authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (HTTPBasicAuthentication HTTPBasicAuthentication1,
                                           HTTPBasicAuthentication HTTPBasicAuthentication2)

            => !(HTTPBasicAuthentication1 > HTTPBasicAuthentication2);

        #endregion

        #region Operator >  (HTTPBasicAuthentication1, HTTPBasicAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPBasicAuthentication1">A HTTP basic authentication.</param>
        /// <param name="HTTPBasicAuthentication2">Another HTTP basic authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (HTTPBasicAuthentication HTTPBasicAuthentication1,
                                          HTTPBasicAuthentication HTTPBasicAuthentication2)

            => HTTPBasicAuthentication1 is null
                   ? throw new ArgumentNullException(nameof(HTTPBasicAuthentication1), "The given HTTP basic authentication must not be null!")
                   : HTTPBasicAuthentication1.CompareTo(HTTPBasicAuthentication2) > 0;

        #endregion

        #region Operator >= (HTTPBasicAuthentication1, HTTPBasicAuthentication2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPBasicAuthentication1">A HTTP basic authentication.</param>
        /// <param name="HTTPBasicAuthentication2">Another HTTP basic authentication.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (HTTPBasicAuthentication HTTPBasicAuthentication1,
                                           HTTPBasicAuthentication HTTPBasicAuthentication2)

            => !(HTTPBasicAuthentication1 < HTTPBasicAuthentication2);

        #endregion

        #endregion

        #region IComparable<HTTPBasicAuthentication> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two HTTP basic authentications.
        /// </summary>
        /// <param name="Object">A HTTP basic authentication to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is HTTPBasicAuthentication httpBasicAuthentication
                   ? CompareTo(httpBasicAuthentication)
                   : throw new ArgumentException("The given object is not a HTTP Basic Authentication!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(HTTPBasicAuthentication)

        /// <summary>
        /// Compares two HTTP basic authentications.
        /// </summary>
        /// <param name="HTTPBasicAuthentication">A HTTP basic authentication to compare with.</param>
        public Int32 CompareTo(HTTPBasicAuthentication? HTTPBasicAuthentication)
        {

            if (HTTPBasicAuthentication is null)
                throw new ArgumentNullException(nameof(Object),
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
        /// Compares two HTTP basic authentications for equality.
        /// </summary>
        /// <param name="Object">A HTTP basic authentication to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is HTTPBasicAuthentication httpBasicAuthentication &&
                   Equals(httpBasicAuthentication);

        #endregion

        #region Equals(HTTPBasicAuthentication)

        /// <summary>
        /// Compares two HTTP basic authentications for equality.
        /// </summary>
        /// <param name="HTTPBasicAuthentication">A HTTP basic authentication to compare with.</param>
        public Boolean Equals(HTTPBasicAuthentication? HTTPBasicAuthentication)

            => HTTPBasicAuthentication is not null &&
               Username.Equals(HTTPBasicAuthentication.Username) &&
               Password.Equals(HTTPBasicAuthentication.Password);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
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

            => String.Concat($"Basic {Username}:{Password} (", $"{Username}:{Password}".ToBase64(), ")");

        #endregion

    }

}
