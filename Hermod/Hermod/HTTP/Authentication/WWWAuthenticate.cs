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
    /// WWW-Authenticate
    /// </summary>
    public class WWWAuthenticate
    {

        #region Data

        private static readonly Char[]                      splitter1   = [ ',' ];
        private static readonly Char[]                      splitter2   = [ '=' ];

        private        readonly Dictionary<String, String>  parameters;

        #endregion

        #region Properties

        /// <summary>
        /// The WWW-Authenticate method.
        /// </summary>
        public String  Method    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP WWW-Authenticate based on the given method and parameters.
        /// </summary>
        /// <param name="Method">A HTTP WWW-Authenticate method.</param>
        /// <param name="Parameters">Optional WWW-Authenticate parameters.</param>
        private WWWAuthenticate(String                      Method,
                                Dictionary<String, String>  Parameters)
        {

            this.Method      = Method;
            this.parameters  = Parameters;

        }

        #endregion


        #region (static) Parse    (Text)

        /// <summary>
        /// Parse the given text as a HTTP WWW-Authenticate header.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP WWW-Authenticate header.</param>
        public static WWWAuthenticate Parse(String Text)
        {

            if (TryParse(Text, out var wwwAuthenticate))
                return wwwAuthenticate!;

            throw new ArgumentException("The given text representation of a HTTP WWW-Authenticate header is invalid!", nameof(Text));

        }

        #endregion

        #region (static) TryParse (Text)

        /// <summary>
        /// Try to parse the given text as a HTTP WWW-Authenticate header.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP WWW-Authenticate header.</param>
        public static WWWAuthenticate? TrynParse(String Text)
        {

            if (TryParse(Text, out var wwwAuthenticate))
                return wwwAuthenticate;

            return null;

        }

        #endregion

        #region (static) TryParse (Text, out WWWAuthenticate)

        /// <summary>
        /// Try to parse the given text as a HTTP WWW-Authenticate header.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP WWW-Authenticate header.</param>
        /// <param name="WWWAuthenticate">The parsed HTTP WWW-Authenticate header.</param>
        public static Boolean TryParse(String                                    Text,
                                       [NotNullWhen(true)] out WWWAuthenticate?  WWWAuthenticate)
        {

            try
            {

                var parameters = new Dictionary<String, String>();

                var firstSpaceIndex = Text.IndexOf(' ');
                if (firstSpaceIndex != -1)
                {

                    var method = Text[..firstSpaceIndex];
                    var keyValuePairs = Text[(firstSpaceIndex + 1)..].Split(splitter1,
                                                                               StringSplitOptions.RemoveEmptyEntries);

                    foreach (var keyValuePair in keyValuePairs)
                    {

                        var keyValue = keyValuePair.Split(splitter2,
                                                          StringSplitOptions.RemoveEmptyEntries);

                        if (keyValue.Length == 2)
                            parameters[keyValue[0].Trim()] = keyValue[1].Trim().Trim('"');

                    }

                    WWWAuthenticate = new(
                                          method,
                                          parameters
                                      );

                    return true;

                }

            } catch
            { }

            WWWAuthenticate = null;
            return false;

        }

        #endregion


        #region GetParameter(Key)

        /// <summary>
        /// Get the parameter with the given key.
        /// </summary>
        /// <param name="Key">The key of the parameter.</param>
        public String? GetParameter(String Key)
        {

            if (parameters.TryGetValue(Key, out var value))
                return value;

            return null;

        }

        #endregion


        #region (static) Basic(Realm, Charset = "UTF-8")

        /// <summary>
        /// Basic realm="Access to the staging site", charset="UTF-8"
        /// </summary>
        /// <param name="Realm">The realm.</param>
        /// <param name="Charset">The optional charset.</param>
        public static WWWAuthenticate Basic(String  Realm,
                                            String  Charset = "UTF-8")

            => new ("Basic",
                    new Dictionary<String, String> {
                        { "realm",    Realm },
                        { "charset",  Charset }
                    });

        #endregion



        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
            => $"{Method} {parameters.Select(parameter => $"{parameter.Key}=\"{parameter.Value}\"").AggregateWith(", ")}";

        #endregion


    }

}
