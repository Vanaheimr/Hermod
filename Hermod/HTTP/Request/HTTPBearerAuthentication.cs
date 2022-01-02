/*
 * Copyright (c) 2010-2022, Achim Friedland <achim.friedland@graphdefined.com>
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
    /// A HTTP Bearer authentication.
    /// </summary>
    [DebuggerDisplay("{DebugView}")]
    public class HTTPBearerAuthentication : IHTTPAuthentication
    {

        #region Properties

        /// <summary>
        /// The authentication token.
        /// </summary>
        public String                   Token                 { get; }

        /// <summary>
        /// The type of the HTTP authentication.
        /// </summary>
        public HTTPAuthenticationTypes  HTTPCredentialType    { get; }


        public String HTTPText
            => "Bearer " + Token;


        /// <summary>
        /// Return a debug representation of this object.
        /// </summary>
        private String DebugView
            => String.Concat("Bearer '", Token, "'");

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create the credentials based on a string.
        /// </summary>
        /// <param name="Token">The authentication token.</param>
        public HTTPBearerAuthentication(String  Token)
        {
            this.HTTPCredentialType  = HTTPAuthenticationTypes.Bearer;
            this.Token               = Token?.Trim() ?? throw new ArgumentNullException(nameof(Token), "The given token must not be null or empty!");
        }

        #endregion


        #region (static) TryParse(Text, out BearerAuthentication)

        /// <summary>
        /// Try to parse the given text.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP Bearer authentication header.</param>
        /// <param name="BearerAuthentication">The parsed HTTP Bearer authentication header.</param>
        /// <returns>true, when the parsing was successful, else false.</returns>
        public static Boolean TryParse(String Text, out HTTPBearerAuthentication BearerAuthentication)
        {

            BearerAuthentication = null;

            if (Text.IsNullOrEmpty())
                return false;

            var splitted = Text.Split(new Char[] { ' ' });

            if (splitted.IsNullOrEmpty())
                return false;

            if (splitted.Length == 2 &&
                String.Equals(splitted[0], "Bearer", StringComparison.OrdinalIgnoreCase))
            {

                if (splitted[1].IsNullOrEmpty())
                    return false;

                BearerAuthentication = new HTTPBearerAuthentication(splitted[1]);
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
            => "Bearer " + Token;

        #endregion

    }

}
