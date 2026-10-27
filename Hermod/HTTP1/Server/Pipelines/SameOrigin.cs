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
    /// Whether a request a browser sent comes from a page of the server it was
    /// sent to: its 'Origin' header, compared with what the request was sent to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What a page of another site can make a browser send without asking - a
    /// form's POST, a WebSocket handshake - carries the browser's cookies, and
    /// its 'Origin' says whose page it was. A server that authenticates by
    /// cookie, Basic auth, client certificate or address refuses what another
    /// page sent, and this is the comparison it makes. Programs that are no
    /// browsers send no 'Origin', and nothing here is asked of them.
    /// </para>
    /// <para>
    /// The host of the origin has to be the host the request was sent to, and
    /// its port the port: the request's Host header says both, leaving out the
    /// port where it is the default of the scheme, as the origin does. Behind a
    /// reverse proxy the Host header may name the node and not what the browser
    /// asked for; what a proxy the server believes says instead is compared as
    /// well, by its host alone where it names no port - nginx's $host names
    /// none. Only that: the scheme is not compared, because the server behind a
    /// proxy of HTTPS sees plain HTTP, and one port does not speak both.
    /// </para>
    /// </remarks>
    public static class SameOrigin
    {

        #region IsSameOrigin(this Request, Origin, HostAskedFor = null)

        /// <summary>
        /// Whether the given origin - "https://hub.example.org:8443" - is the one
        /// the given request was sent to.
        /// </summary>
        /// <param name="Request">An HTTP request.</param>
        /// <param name="Origin">The value of an 'Origin' header. "null", which a sandboxed page sends, is no origin of anybody's.</param>
        /// <param name="HostAskedFor">What a reverse proxy the server believes says the browser asked for, "hub.example.org" or "hub.example.org:8443"; null where none says it.</param>
        public static Boolean IsSameOrigin(this HTTPRequest  Request,
                                           String            Origin,
                                           String?           HostAskedFor   = null)
        {

            if (!Uri.TryCreate(Origin.Trim(), UriKind.Absolute, out var origin) ||
                origin.Host.Length == 0 ||
                origin.PathAndQuery != "/" ||
                origin.UserInfo.Length > 0)
            {
                return false;
            }

            if (Request.GetHeaderField("Host") is String host &&
                HostMatches(origin, host, AnyPortWhereNoneIsNamed: false))
            {
                return true;
            }

            return HostAskedFor is not null &&
                   HostMatches(origin, HostAskedFor, AnyPortWhereNoneIsNamed: true);

        }

        #endregion

        #region AllowsOrigin(this Request, AllowedOrigins = null, HostAskedFor = null)

        /// <summary>
        /// Whether the given request may be served by the 'Origin' it carries: none
        /// at all, as from a program that is no browser; the request's own; or one
        /// of the given origins, compared case-insensitively.
        /// </summary>
        /// <param name="Request">An HTTP request.</param>
        /// <param name="AllowedOrigins">Origins besides the request's own whose pages may send it, "https://map.example.org".</param>
        /// <param name="HostAskedFor">What a reverse proxy the server believes says the browser asked for; null where none says it.</param>
        public static Boolean AllowsOrigin(this HTTPRequest      Request,
                                           IEnumerable<String>?  AllowedOrigins   = null,
                                           String?               HostAskedFor     = null)
        {

            if (Request.GetHeaderField("Origin") is not String origin)
                return true;

            if (AllowedOrigins is not null &&
                AllowedOrigins.Any(allowed => String.Equals(allowed.TrimEnd('/'), origin.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return Request.IsSameOrigin(origin, HostAskedFor);

        }

        #endregion


        #region (private static) HostMatches(Origin, Host, AnyPortWhereNoneIsNamed)

        /// <summary>
        /// Whether the given origin names the given host, "hub.example.org",
        /// "hub.example.org:8443" or "[::1]:8080": the same host, and the same
        /// port - where the host names none, the default of the origin's scheme,
        /// or any.
        /// </summary>
        private static Boolean HostMatches(Uri      Origin,
                                           String   Host,
                                           Boolean  AnyPortWhereNoneIsNamed)
        {

            var text  = Host.Trim();
            var name  = text;
            Int32? port = null;

            if (text.StartsWith('['))
            {

                var end = text.IndexOf(']');

                if (end < 0)
                    return false;

                name = text[..(end + 1)];

                if (end + 1 < text.Length)
                {

                    if (text[end + 1] != ':' || !Int32.TryParse(text[(end + 2)..], out var number))
                        return false;

                    port = number;

                }

            }
            else
            {

                var colon = text.LastIndexOf(':');

                if (colon >= 0)
                {

                    if (text.IndexOf(':') != colon || !Int32.TryParse(text[(colon + 1)..], out var number))
                        return false;

                    name = text[..colon];
                    port = number;

                }

            }

            if (!String.Equals(name, Origin.Host, StringComparison.OrdinalIgnoreCase))
                return false;

            return port.HasValue
                       ? port.Value == Origin.Port
                       : AnyPortWhereNoneIsNamed || Origin.IsDefaultPort;

        }

        #endregion

    }

}
