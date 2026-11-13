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
    /// WWW-Authenticate
    /// </summary>
    public class WWWAuthenticate
    {

        #region Data

        private static readonly Char[]                      splitter1   = [ ',' ];
        private static readonly Char[]                      splitter2   = [ '=' ];

        private        readonly Dictionary<String, String>  parameters;

        private        readonly List<WWWAuthenticate>       further     = [];

        #endregion

        #region Properties

        /// <summary>
        /// The WWW-Authenticate method.
        /// </summary>
        public String  Method    { get; }

        /// <summary>
        /// This challenge and the ones that follow it in the same header
        /// field: a server that takes more than one scheme offers them all
        /// (RFC 9110, section 11.6.1), and a client answers the one it can.
        /// </summary>
        public IEnumerable<WWWAuthenticate> Challenges
            => new[] { this }.Concat(further);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP WWW-Authenticate based on the given method and parameters.
        /// </summary>
        /// <param name="Method">An HTTP WWW-Authenticate method.</param>
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

                    WWWAuthenticate = new(
                                          method,
                                          parameters
                                      );

                    foreach (var keyValuePair in keyValuePairs)
                    {

                        var keyValue = keyValuePair.Split(splitter2,
                                                          2,
                                                          StringSplitOptions.RemoveEmptyEntries);

                        var key      = keyValue[0].Trim();

                        // Another challenge begins where a scheme stands before
                        // a parameter - "TOTP realm=..." - or by itself.
                        var space    = key.IndexOf(' ');

                        if (space > 0 || (keyValue.Length == 1 && key.Length > 0))
                        {

                            parameters = [];

                            WWWAuthenticate.further.Add(new (space > 0 ? key[..space] : key, parameters));

                            key        = space > 0 ? key[(space + 1)..].Trim() : "";

                        }

                        if (keyValue.Length == 2 && key.Length > 0)
                            parameters[key] = keyValue[1].Trim().Trim('"');

                    }

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



        #region (static) TOTP(Realm = null)

        /// <summary>
        /// TOTP realm="OCPP": the challenge of the HTTP TOTP authentication,
        /// a time-based one-time password in "Authorization: TOTP".
        /// </summary>
        /// <param name="Realm">The optional realm.</param>
        public static WWWAuthenticate TOTP(String? Realm = null)

            => new ("TOTP",
                    Realm is not null
                        ? new Dictionary<String, String> { { "realm", Realm } }
                        : []);

        #endregion

        #region And(Another)

        /// <summary>
        /// This challenge, followed by another one in the same header field.
        /// </summary>
        /// <param name="Another">A challenge of another scheme.</param>
        public WWWAuthenticate And(WWWAuthenticate Another)
        {

            var both = new WWWAuthenticate(Method, parameters);

            both.further.AddRange(further);
            both.further.AddRange(Another.Challenges.Select(challenge => new WWWAuthenticate(challenge.Method, challenge.parameters)));

            return both;

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => Challenges.Select(challenge => challenge.parameters.Count > 0
                                                  ? $"{challenge.Method} {challenge.parameters.Select(parameter => $"{parameter.Key}=\"{parameter.Value}\"").AggregateCSV()}"
                                                  : challenge.Method).
                          AggregateCSV();

        #endregion


    }

}
