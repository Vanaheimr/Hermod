/*
 * Copyright (c) 2010-2021, Achim Friedland <achim.friedland@graphdefined.com>
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

using System;
using System.Diagnostics;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A HTTP token authentication.
    /// </summary>
    [DebuggerDisplay("{DebugView}")]
    public class HTTPTokenAuthentication : IHTTPAuthentication
    {

        #region Properties

        /// <summary>
        /// The username.
        /// </summary>
        public String                   Token              { get; }

        /// <summary>
        /// The type of the HTTP authentication.
        /// </summary>
        public HTTPAuthenticationTypes  HTTPCredentialType    { get; }


        public String HTTPText
            => "Token " + Token;


        /// <summary>
        /// Return a debug representation of this object.
        /// </summary>
        private String DebugView
            => String.Concat("Token '", Token, "'");

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create the credentials based on a base64 encoded string which comes from a HTTP header Authentication:
        /// </summary>
        /// <param name="Token">The token.</param>
        public HTTPTokenAuthentication(String Token)
        {

            if (Token.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Token), "The given token must not be null or empty!");

            this.HTTPCredentialType  = HTTPAuthenticationTypes.Basic;
            this.Token               = Token;

        }

        #endregion


        #region (static) TryParse(Text, out BasicAuthentication)

        /// <summary>
        /// Try to parse the given text.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP basic authentication header.</param>
        /// <param name="TokenAuthentication">The parsed HTTP basic authentication header.</param>
        /// <returns>true, when the parsing was successful, else false.</returns>
        public static Boolean TryParse(String Text, out HTTPTokenAuthentication TokenAuthentication)
        {

            TokenAuthentication = null;

            if (Text.IsNullOrEmpty())
                return false;

            var splitted = Text.Split(new Char[] { ' ' });

            if (splitted.IsNullOrEmpty())
                return false;

            if (splitted.Length == 2 &&
                String.Equals(splitted[0], "token", StringComparison.OrdinalIgnoreCase))
            {

                if (splitted[1].IsNullOrEmpty())
                    return false;

                TokenAuthentication = new HTTPTokenAuthentication(splitted[1]);
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
            => "Token " + Token;

        #endregion

    }

}
