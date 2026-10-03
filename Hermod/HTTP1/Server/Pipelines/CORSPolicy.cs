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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// What an application is willing to permit a foreign origin to do.
    ///
    /// Deliberately a value the application states rather than something this
    /// library infers: which origins may call, and with what, is the one part of
    /// CORS that no server can work out on its own. The preflight *mechanics* —
    /// recognising the request, shaping the answer — are mechanics, and those
    /// are in <see cref="HTTPCORSPipeline"/>.
    /// </summary>
    /// <param name="AllowedOrigins">The origins permitted to call, or "*". An empty set permits nothing, which is the safe default for a value nobody filled in.</param>
    /// <param name="AllowedMethods">The methods a foreign origin may use.</param>
    /// <param name="AllowedHeaders">The request header names a foreign origin may send.</param>
    /// <param name="AllowCredentials">Whether the browser may attach credentials. Note that "*" and credentials are mutually exclusive per WHATWG Fetch — see <see cref="AllowOrigin"/>.</param>
    /// <param name="MaxAge">How long the browser may cache this preflight answer.</param>
    public sealed class CORSPolicy(IEnumerable<String>?      AllowedOrigins     = null,
                                   IEnumerable<HTTPMethod>?  AllowedMethods     = null,
                                   IEnumerable<String>?      AllowedHeaders     = null,
                                   Boolean                   AllowCredentials   = false,
                                   TimeSpan?                 MaxAge             = null)
    {

        #region Properties

        public IReadOnlyList<String>      AllowedOrigins      { get; } = [.. AllowedOrigins ?? []];
        public IReadOnlyList<HTTPMethod>  AllowedMethods      { get; } = [.. AllowedMethods ?? []];
        public IReadOnlyList<String>      AllowedHeaders      { get; } = [.. AllowedHeaders ?? []];
        public Boolean                    AllowCredentials    { get; } = AllowCredentials;
        public TimeSpan?                  MaxAge              { get; } = MaxAge;

        #endregion


        #region AllowOrigin(Origin)

        /// <summary>
        /// The value to echo back for the given origin, or null when it is not
        /// permitted and the preflight must be refused.
        ///
        /// A wildcard policy echoes the concrete origin rather than "*" when
        /// credentials are allowed: WHATWG Fetch rejects "*" together with
        /// Access-Control-Allow-Credentials, and a browser would discard the
        /// whole answer. Echoing is also what makes Vary: Origin necessary,
        /// which the pipeline sets.
        /// </summary>
        public String? AllowOrigin(String Origin)
        {

            if (AllowedOrigins.Contains(Origin, StringComparer.OrdinalIgnoreCase))
                return Origin;

            if (AllowedOrigins.Contains("*"))
                return AllowCredentials ? Origin : "*";

            return null;

        }

        #endregion

        #region Allows(Method)

        public Boolean Allows(HTTPMethod Method)

            => AllowedMethods.Contains(Method);

        #endregion

        #region AllowsHeaders(RequestedHeaders)

        /// <summary>
        /// Whether every header the preflight asked about is permitted. The
        /// browser lower-cases the names it sends, so the comparison is
        /// case-insensitive — as HTTP field names are anyway.
        /// </summary>
        public Boolean AllowsHeaders(String? RequestedHeaders)
        {

            if (RequestedHeaders is null || RequestedHeaders.Trim().Length == 0)
                return true;

            if (AllowedHeaders.Contains("*"))
                return true;

            foreach (var requested in RequestedHeaders.Split(','))
            {

                var name = requested.Trim();

                if (name.Length == 0)
                    continue;

                if (!AllowedHeaders.Contains(name, StringComparer.OrdinalIgnoreCase))
                    return false;

            }

            return true;

        }

        #endregion

    }

}
