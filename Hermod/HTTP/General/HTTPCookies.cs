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

        /// <summary>
        /// Create a new collection of HTTP cookies.
        /// </summary>
        /// <param name="Cookies">An enumeration of HTTP cookies.</param>
        public HTTPCookies(params IEnumerable<HTTPCookie> Cookies)
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


        public static HTTPCookies Create(params HTTPCookie[] Cookies)
            => new (Cookies);


        #region Parse   (Texts)

        /// <summary>
        /// Parse the given texts as one HTTP cookie each: the values of
        /// "Set-Cookie" headers, or the cookies of a "Cookie" header already
        /// taken apart. A cookie keeps its attributes ("Path=/", "HttpOnly").
        /// </summary>
        /// <param name="Texts">One text per cookie.</param>
        public static HTTPCookies Parse(IEnumerable<String> Texts)
        {

            if (TryParse(Texts, out var httpCookies))
                return httpCookies;

            throw new ArgumentException("The given text representation of HTTP cookies is invalid!",
                                        nameof(Texts));

        }

        /// <summary>
        /// Parse the given texts as one HTTP cookie each: the values of
        /// "Set-Cookie" headers, or the cookies of a "Cookie" header already
        /// taken apart. A cookie keeps its attributes ("Path=/", "HttpOnly").
        /// </summary>
        /// <remarks>
        /// A single text is a single cookie as well. This used to hand one
        /// text to the parser of the "Cookie" header, which splits at the
        /// semicolon because that is what separates cookies there - and so
        /// "session=abc; Path=/; HttpOnly" became three cookies named
        /// "session", "Path" and "HttpOnly", each written as a Set-Cookie
        /// line of its own. Whoever holds the value of a "Cookie" header
        /// says so: <see cref="ParseCookieHeader"/>.
        /// </remarks>
        /// <param name="Texts">One text per cookie.</param>
        public static HTTPCookies Parse(params String[] Texts)

            => Parse((IEnumerable<String>) Texts);

        #endregion

        #region ParseSetCookie / TryParseSetCookie(Text, out HTTPCookies)

        /// <summary>
        /// Parse the value of one "Set-Cookie" response header: a single
        /// cookie with its attributes, e.g. "session=abc; Path=/; HttpOnly".
        /// </summary>
        /// <param name="Text">The value of a "Set-Cookie" header.</param>
        public static HTTPCookies ParseSetCookie(String Text)
        {

            if (TryParseSetCookie(Text, out var httpCookies))
                return httpCookies;

            throw new ArgumentException("The given text representation of a Set-Cookie header is invalid!",
                                        nameof(Text));

        }

        /// <summary>
        /// Try to parse the value of one "Set-Cookie" response header: a single
        /// cookie with its attributes, e.g. "session=abc; Path=/; HttpOnly".
        /// </summary>
        /// <param name="Text">The value of a "Set-Cookie" header.</param>
        /// <param name="HTTPCookies">The parsed cookie, as a collection of one.</param>
        public static Boolean TryParseSetCookie(String                                Text,
                                                [NotNullWhen(true)] out HTTPCookies?  HTTPCookies)
        {

            if (HTTPCookie.TryParse(Text, out var httpCookie))
            {
                HTTPCookies = new HTTPCookies(httpCookie);
                return true;
            }

            HTTPCookies = null;
            return false;

        }

        #endregion

        #region ParseCookieHeader / TryParseCookieHeader(Text, out HTTPCookies)

        /// <summary>
        /// Parse the value of a "Cookie" request header: any number of
        /// cookies separated by semicolons, e.g. "session=abc; theme=dark".
        /// </summary>
        /// <param name="Text">The value of a "Cookie" header.</param>
        public static HTTPCookies ParseCookieHeader(String Text)
        {

            if (TryParseCookieHeader(Text, out var httpCookies))
                return httpCookies;

            throw new ArgumentException("The given text representation of a Cookie header is invalid!",
                                        nameof(Text));

        }

        /// <summary>
        /// Try to parse the value of a "Cookie" request header: any number of
        /// cookies separated by semicolons, e.g. "session=abc; theme=dark".
        /// </summary>
        /// <param name="Text">The value of a "Cookie" header.</param>
        /// <param name="HTTPCookies">The parsed cookies.</param>
        public static Boolean TryParseCookieHeader(String                                Text,
                                                   [NotNullWhen(true)] out HTTPCookies?  HTTPCookies)
        {

            Text = Text?.Trim() ?? "";

            if (Text.IsNullOrEmpty())
            {
                HTTPCookies = null;
                return false;
            }

            return TryParse(
                       Text.Split(
                           multipleCookiesSplitter,
                           StringSplitOptions.RemoveEmptyEntries
                       ).Select(cookieText => cookieText.Trim()),
                       out HTTPCookies
                   );

        }

        #endregion

        #region TryParse(Text,  out HTTPCookies)

        /// <summary>
        /// Try to parse the value of a "Cookie" request header - the same as
        /// <see cref="TryParseCookieHeader"/>, kept under its old name.
        /// For the value of a "Set-Cookie" header take
        /// <see cref="TryParseSetCookie"/>: there the semicolon separates the
        /// cookie from its attributes, not one cookie from the next.
        /// </summary>
        /// <param name="Text">The value of a "Cookie" header.</param>
        /// <param name="HTTPCookies">The parsed cookies.</param>
        public static Boolean TryParse(String                                Text,
                                       [NotNullWhen(true)] out HTTPCookies?  HTTPCookies)

            => TryParseCookieHeader(Text, out HTTPCookies);

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
