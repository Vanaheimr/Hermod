/*
 * Copyright (c) 2011-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
 * 
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or
 * (at your option) any later version.
 * 
 * You may obtain a copy of the License at
 *   http://www.gnu.org/licenses/gpl.html
 * 
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * General Public License for more details.
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
