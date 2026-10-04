/*
 * Copyright (c) 2011-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Web;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// HTTP tools.
    /// </summary>
    public static class HTTPTools
    {

        #region MovedPermanently(HTTPRequest, Location)

        /// <summary>
        /// Return a HTTP response redirecting to the given location permanently.
        /// </summary>
        /// <param name="HTTPRequest">The HTTP request.</param>
        /// <param name="Location">The location of the redirect.</param>
        public static HTTPResponse MovedPermanently(HTTPRequest  HTTPRequest,
                                                    Location     Location)

            => new HTTPResponse.Builder(HTTPRequest) {
                       HTTPStatusCode  = HTTPStatusCode.MovedPermanently,
                       CacheControl    = "no-cache",
                       Location        = Location,
                       Connection      = ConnectionType.Close
            };

        #endregion

        #region MovedTemporarily(HTTPRequest, Location)

        /// <summary>
        /// Return a HTTP response redirecting to the given location temporarily.
        /// </summary>
        /// <param name="HTTPRequest">The HTTP request.</param>
        /// <param name="Location">The location of the redirect.</param>
        public static HTTPResponse MovedTemporarily(HTTPRequest  HTTPRequest,
                                                    Location     Location)

            => new HTTPResponse.Builder(HTTPRequest) {
                       HTTPStatusCode  = HTTPStatusCode.TemporaryRedirect,
                       CacheControl    = "no-cache",
                       Location        = Location,
                       Connection      = ConnectionType.Close
            };

        #endregion

        #region URLDecode(Input)

        /// <summary>
        /// Converts a string that has been encoded for transmission in a URL into a decoded string.
        /// </summary>
        /// <param name="Input">An URL encoded string.</param>
        public static String URLDecode(String Input)

            => HttpUtility.UrlDecode(Input);

        #endregion

    }

}
