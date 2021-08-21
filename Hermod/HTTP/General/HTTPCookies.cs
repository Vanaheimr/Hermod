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
using System.Linq;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;
using System.Collections;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Multiple HTTP cookies.
    /// </summary>
    public class HTTPCookies : IEnumerable<HTTPCookie>
    {

        #region Data

        private static readonly Char[] MultipleCookiesSplitter = new Char[] { ';' };

        private readonly Dictionary<HTTPCookieName, HTTPCookie> Cookies;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create new HTTP cookies.
        /// </summary>
        /// <param name="Cookies">An enumeration of HTTP cookies.</param>
        private HTTPCookies(IEnumerable<HTTPCookie> Cookies = null)
        {

            this.Cookies = new Dictionary<HTTPCookieName, HTTPCookie>();

            if (Cookies != null)
            {

                foreach (var cookie in Cookies)
                {

                    // There is no gurantee, that cookie.Name is unquie within a HTTP request!
                    // Therefore use the latest cookie having this id/name!
                    if (!this.Cookies.ContainsKey(cookie.Name))
                        this.Cookies.Add(cookie.Name, cookie);

                    else
                        this.Cookies[cookie.Name] = cookie;

                }

            }

        }

        #endregion


        #region Parse   (Text)

        /// <summary>
        /// Parse the given text as multiple HTTP cookies.
        /// </summary>
        /// <param name="Text">A text representation of multiple HTTP cookies.</param>
        public static HTTPCookies Parse(String Text)
        {

            if (TryParse(Text, out HTTPCookies _HTTPCookies))
                return _HTTPCookies;

            return null;

        }

        #endregion

        #region TryParse(Text, out HTTPCookies)

        /// <summary>
        /// Parse the given string as multiple HTTP cookies.
        /// </summary>
        /// <param name="Text">A text representation of multiple HTTP cookies.</param>
        /// <param name="HTTPCookies">The parsed enumeration of HTTP cookies.</param>
        public static Boolean TryParse(String Text, out HTTPCookies HTTPCookies)
        {

            if (Text.IsNotNullOrEmpty())
                Text = Text.Trim();

            if (Text.IsNullOrEmpty())
            {
                HTTPCookies = null;
                return false;
            }

            HTTPCookie Cookie = null;
            var Cookies = new List<HTTPCookie>();

            foreach (var TextCookie in Text.Split(MultipleCookiesSplitter, StringSplitOptions.RemoveEmptyEntries))
            {

                try
                {

                    if (HTTPCookie.TryParse(TextCookie, out Cookie))
                        Cookies.Add(Cookie);

                }
#pragma warning disable RCS1075 // Avoid empty catch clause that catches System.Exception.
                catch (Exception)
#pragma warning restore RCS1075 // Avoid empty catch clause that catches System.Exception.
                { }

            }

            HTTPCookies = new HTTPCookies(Cookies);
            return true;

        }

        #endregion



        public Boolean TryGet(HTTPCookieName CookieName, out HTTPCookie Cookie)
            => Cookies.TryGetValue(CookieName, out Cookie);

        public Boolean Contains(HTTPCookieName CookieName)
            => Cookies.ContainsKey(CookieName);


        public IEnumerator<HTTPCookie> GetEnumerator()
            => Cookies.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => Cookies.Values.GetEnumerator();


    }

}
