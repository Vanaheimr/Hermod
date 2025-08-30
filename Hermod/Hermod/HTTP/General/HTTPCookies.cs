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

using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A collection of HTTP cookies.
    /// 
    /// Multiple HTTP cookies are send from the server to the client in multiple "Set-Cookie" headers,
    /// but from the client to the server multiple HTTP cookies are send in one "Cookie" header concatenated via "; ".
    /// </summary>
    public class HTTPCookies : IEnumerable<HTTPCookie>
    {

        #region Data

        private readonly ConcurrentDictionary<HTTPCookieName, HTTPCookie> cookies = [];

        private static readonly Char[] multipleCookiesSplitter = [ ';' ];

        #endregion

        #region Constructor(s)

        #region HTTPCookies(Cookies)

        /// <summary>
        /// Create a new collection of HTTP cookies.
        /// </summary>
        /// <param name="Cookies">An enumeration of HTTP cookies.</param>
        public HTTPCookies(IEnumerable<HTTPCookie> Cookies)

            : this([.. Cookies])

        { }

        #endregion

        #region HTTPCookies(params Cookies)

        /// <summary>
        /// Create a new collection of HTTP cookies.
        /// </summary>
        /// <param name="Cookies">An array of HTTP cookies.</param>
        public HTTPCookies(params HTTPCookie[] Cookies)
        {

            // There is no guarantee, that cookie.Name is unique within a HTTP request!
            // Therefore use the latest cookie having this id/name!
            foreach (var cookie in Cookies)
            {

                if (!cookies.TryAdd(cookie.Name, cookie))
                    cookies[cookie.Name] = cookie;

            }

        }

        #endregion

        #endregion


        public static HTTPCookies Create(params HTTPCookie[] Cookies)
            => new (Cookies);


        #region Parse   (Texts)

        /// <summary>
        /// Parse the given enumeration of texts.
        /// </summary>
        /// <param name="Texts">An enumeration of text representations of HTTP cookies.</param>
        public static HTTPCookies Parse(IEnumerable<String> Texts)
        {

            if (TryParse(Texts, out var httpCookies))
                return httpCookies;

            throw new ArgumentException("The given JSON representation of HTTP cookies is invalid!",
                                        nameof(Texts));

        }

        /// <summary>
        /// Parse the given enumeration of texts.
        /// </summary>
        /// <param name="Texts">An enumeration of text representations of HTTP cookies.</param>
        public static HTTPCookies Parse(params String[] Texts)
        {

            // Might be multiple cookies in one string!
            if (Texts.Length == 1 &&
                TryParse(Texts[0], out var httpCookies))
            {
                return httpCookies;
            }

            if (TryParse(Texts, out httpCookies))
                return httpCookies;

            throw new ArgumentException("The given JSON representation of HTTP cookies is invalid!",
                                        nameof(Texts));

        }

        #endregion

        #region TryParse(Text,  out HTTPCookies)

        /// <summary>
        /// Parse the given string as one or multiple HTTP cookies.
        /// </summary>
        /// <param name="Text">A text representation of one or multiple HTTP cookies.</param>
        /// <param name="HTTPCookies">The parsed enumeration of HTTP cookies.</param>
        public static Boolean TryParse(String                                Text,
                                       [NotNullWhen(true)] out HTTPCookies?  HTTPCookies)
        {

            Text = Text.Trim();

            if (Text.IsNullOrEmpty())
            {
                HTTPCookies = null;
                return false;
            }

            if (TryParse(
                    Text.Split(
                        multipleCookiesSplitter,
                        StringSplitOptions.RemoveEmptyEntries
                    ).Select(cookieText => cookieText.Trim()),
                    out HTTPCookies
                ))
            {
                return true;
            }

            return false;

        }

        #endregion

        #region TryParse(Texts, out HTTPCookies)

        /// <summary>
        /// Parse the given enumeration of texts.
        /// </summary>
        /// <param name="Texts">An enumeration of text representations of HTTP cookies.</param>
        /// <param name="HTTPCookies">The parsed enumeration of HTTP cookies.</param>
        public static Boolean TryParse(IEnumerable<String>                   Texts,
                                       [NotNullWhen(true)] out HTTPCookies?  HTTPCookies)
        {

            if (!Texts.Any())
            {
                HTTPCookies = null;
                return false;
            }

            var parsedCookies = new Dictionary<HTTPCookieName, HTTPCookie>();

            foreach (var singleCookie in Texts)
            {

                try
                {

                    if (HTTPCookie.TryParse(singleCookie, out var parsedCookie))
                    {

                        // There is no guarantee, that cookie.Name is unique within a HTTP request!
                        // Therefore use the latest cookie having this id/name!
                        if (!parsedCookies.TryAdd(parsedCookie.Name, parsedCookie))
                            parsedCookies[parsedCookie.Name] = parsedCookie;

                    }

                }
                catch
                { }

            }

            HTTPCookies = new HTTPCookies(parsedCookies.Values);
            return true;

        }

        #endregion


        #region Contains (CookieName)

        public Boolean Contains(HTTPCookieName CookieName)

            => cookies.ContainsKey(CookieName);

        #endregion

        #region Get      (CookieName)

        public HTTPCookie? Get(HTTPCookieName CookieName)
        {

            if (cookies.TryGetValue(CookieName, out var cookie))
                return cookie;

            return null;

        }

        #endregion

        #region TryGet   (CookieName, Cookie)

        public Boolean TryGet(HTTPCookieName                       CookieName,
                              [NotNullWhen(true)] out HTTPCookie?  Cookie)

            => cookies.TryGetValue(
                   CookieName,
                   out Cookie
               );

        #endregion


        #region GetEnumerator()

        public IEnumerator<HTTPCookie> GetEnumerator()
            => cookies.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => cookies.Values.GetEnumerator();

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => cookies.Values.AggregateWith("; ");

        #endregion


    }

}
