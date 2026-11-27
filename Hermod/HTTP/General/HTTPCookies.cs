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

        /// <summary>
        /// The cookies, in their order: a Cookie header field lists them as
        /// the user agent sorted them (RFC 6265, Section 5.4), and a
        /// collection is not changed once it is made.
        /// </summary>
        private readonly OrderedDictionary<HTTPCookieName, HTTPCookie> cookies = [];

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

            HTTPCookies = null;

            if (Text is null)
                return false;

            // RFC 6265, Section 5.4 and draft-ietf-httpbis-rfc6265bis-22,
            // Section 5.8.3: the user agent sorts the cookies of a name with
            // the longest path first and, among equal paths, the oldest first.
            // So the first of a name is the one set for this resource and not
            // for a parent path or by a sibling host later: it is the one kept.
            // Section 4.2.2 warns against relying on that order, and it is no
            // more than the best guess there is.
            var parsedCookies = new Dictionary<HTTPCookieName, HTTPCookie>();

            foreach (var cookiePair in Text.Split(multipleCookiesSplitter))
            {

                // A cookie-pair without a name, a nameless cookie or a control
                // character is skipped, not the whole header field.
                if (HTTPCookie.TryParseCookiePair(cookiePair, out var cookie))
                    parsedCookies.TryAdd(cookie.Name, cookie);

            }

            if (parsedCookies.Count == 0)
                return false;

            HTTPCookies = new HTTPCookies(parsedCookies.Values);
            return true;

        }

        #endregion

        #region TryParseCookieHeaderLines / TryParseSetCookieLines(Lines, out HTTPCookies)

        /// <summary>
        /// Try to parse the lines of a "Cookie" header field: one cookie-string,
        /// which a user agent may split across lines (draft-ietf-httpbis-rfc6265bis-22,
        /// Section 5.8.1; RFC 9113, Section 8.2.3), so they are read joined by "; ".
        /// </summary>
        /// <param name="Lines">The values of all "Cookie" header lines.</param>
        /// <param name="HTTPCookies">The parsed cookies.</param>
        public static Boolean TryParseCookieHeaderLines(IEnumerable<String>                   Lines,
                                                        [NotNullWhen(true)] out HTTPCookies?  HTTPCookies)

            => TryParseCookieHeader(String.Join("; ", Lines), out HTTPCookies);


        /// <summary>
        /// Try to parse the lines of the "Set-Cookie" header field: one cookie
        /// each. Set-Cookie is the one field whose lines may not be joined by
        /// commas (RFC 9110, Section 5.3), because a date has one. A line that
        /// is no cookie is skipped; a second cookie of a name replaces the first,
        /// as it does in a user agent's store.
        /// </summary>
        /// <param name="Lines">The values of all "Set-Cookie" header lines.</param>
        /// <param name="HTTPCookies">The parsed cookies.</param>
        public static Boolean TryParseSetCookieLines(IEnumerable<String>                   Lines,
                                                     [NotNullWhen(true)] out HTTPCookies?  HTTPCookies)
        {

            var parsedCookies = new Dictionary<HTTPCookieName, HTTPCookie>();

            foreach (var line in Lines)
                if (HTTPCookie.TryParse(line, out var cookie))
                    parsedCookies[cookie.Name] = cookie;

            HTTPCookies = parsedCookies.Count > 0
                              ? new HTTPCookies(parsedCookies.Values)
                              : null;

            return HTTPCookies is not null;

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

            // A second cookie of a name replaces the first, as it does in a
            // user agent's store; a text that is no cookie is skipped.
            foreach (var singleCookie in Texts)
                if (HTTPCookie.TryParse(singleCookie, out var parsedCookie))
                    parsedCookies[parsedCookie.Name] = parsedCookie;

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
        /// The cookies as the value of a "Cookie" header field: their names
        /// and values joined by "; ", and none of their attributes. A
        /// "Set-Cookie" header field carries one cookie per line, each as
        /// <see cref="HTTPCookie.ToString"/> writes it.
        /// </summary>
        public override String ToString()

            => cookies.Values.Select(cookie => cookie.ToCookiePair()).AggregateWith("; ");

        #endregion


    }

}
