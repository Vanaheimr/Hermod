/*
 * Copyright (c) 2010-2021, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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
using System.Diagnostics;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A HTTP basic authentication.
    /// </summary>
    [DebuggerDisplay("{DebugView}")]
    public class HTTPBasicAuthentication : IHTTPAuthentication
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
            => "Basic " + (Username + ":" + Password).ToBase64();


        /// <summary>
        /// Return a debug representation of this object.
        /// </summary>
        private String DebugView
            => String.Concat("Basic '", Username, "', '", Password, "'");

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
                throw new ArgumentNullException(nameof(Username), "The given username must not be null or empty!");

            if (Password.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Password), "The given password must not be null or empty!");

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
        public static Boolean TryParse(String Text, out HTTPBasicAuthentication BasicAuthentication)
        {

            BasicAuthentication = null;

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

                BasicAuthentication = new HTTPBasicAuthentication(usernamePassword[0], usernamePassword[1]);
                return true;

            }

            return false;

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
            => "Basic " + (Username + ":" + Password).ToBase64();

        #endregion

    }

}
