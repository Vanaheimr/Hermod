/*
 * Copyright (c) 2011-2013 Achim 'ahzf' Friedland <achim@ahzf.de>
 * This file is part of Hermod <http://www.github.com/Vanaheimr/Hermod>
 * 
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or
 * (at your option) any later version.
 * 
 * You may obtain a copy of the License at
 *     http://www.gnu.org/licenses/gpl.html
 *     
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * General Public License for more details.
 */

#region Usings

using System;

#endregion

namespace eu.Vanaheimr.Hermod.HTTP
{

    public static class HTTPTools
    {

        #region MovedPermanently(Location)

        /// <summary>
        /// Return a HTTP response redirecting to the given location permanently.
        /// </summary>
        /// <param name="Location">The location of the redirect.</param>
        public static HTTPResponse MovedPermanently(String Location)
        {

            #region Initial checks

            if (Location == null || Location == "")
                throw new ArgumentNullException("Location", "The parameter 'Location' must not be null or empty!");

            #endregion

            return new HTTPResponseBuilder()
            {
                HTTPStatusCode = HTTPStatusCode.MovedPermanently,
                CacheControl   = "no-cache",
                Location       = Location
            };

        }

        #endregion

        #region MovedTemporarily(Location)

        /// <summary>
        /// Return a HTTP response redirecting to the given location temporarily.
        /// </summary>
        /// <param name="Location">The location of the redirect.</param>
        public static HTTPResponse MovedTemporarily(String Location)
        {

            #region Initial checks

            if (Location == null || Location == "")
                throw new ArgumentNullException("Location", "The parameter 'Location' must not be null or empty!");

            #endregion

            return new HTTPResponseBuilder()
            {
                HTTPStatusCode = HTTPStatusCode.TemporaryRedirect,
                CacheControl   = "no-cache",
                Location       = Location
            };

        }

        #endregion

    }

}
