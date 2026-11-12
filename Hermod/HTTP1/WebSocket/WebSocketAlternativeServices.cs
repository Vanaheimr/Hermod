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

using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    /// <summary>
    /// What the WebSocket servers this process connected to said of where else
    /// they are reached over HTTP/3 (Alt-Svc, RFC 7838): for a WebSocket client
    /// that leaves the version of HTTP to its server
    /// (<see cref="WebSocketHTTPVersion.Auto"/>).
    /// </summary>
    /// <remarks>
    /// An origin is a host and a port over TLS; what it said is kept for as
    /// long as it said (<c>ma</c>), in memory, for every client of this process
    /// - as RFC 7838, Section 2.2 has a client keep it - and no longer than
    /// the process. Only an alternative on the origin's own host is kept: one
    /// that names another host would have a client go where its DNS client was
    /// not asked for. The server found there is judged as the origin is, by
    /// the certificate validator of the client, which is what makes it the
    /// origin's (RFC 7838, Section 2.1). A server that is not found where it
    /// said is asked the way it was, and not believed again for an hour.
    /// </remarks>
    public static class WebSocketAlternativeServices
    {

        #region Data

        private static readonly ConcurrentDictionary<String, (UInt16 Port, DateTimeOffset Until)> overHTTP3 = new (StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<String, DateTimeOffset>                      notFound  = new (StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// For how long a server that was not found where it said is not
        /// believed when it says so again: a server behind a firewall that
        /// lets no UDP through says it with every answer, and would cost every
        /// attempt to connect the time it takes to find that out.
        /// </summary>
        public static readonly TimeSpan NotBelievedAgainFor = TimeSpan.FromHours(1);

        #endregion


        #region (private) OriginOf(URL)

        private static String? OriginOf(URL URL)

            => URL.Scheme == URIScheme.wss || URL.Scheme == URIScheme.https
                   ? $"{URL.Host}:{(URL.Port ?? IPPort.HTTPS).ToUInt16()}"
                   : null;

        #endregion

        #region Learn(URL, AlternativeServices)

        /// <summary>
        /// Keep what a server said with its answer: its first "h3" on its own
        /// host, or nothing any more where it says "clear" or names none.
        /// </summary>
        /// <param name="URL">The URL the server was asked at.</param>
        /// <param name="AlternativeServices">The Alt-Svc of its answer.</param>
        public static void Learn(URL URL, AlternativeServices AlternativeServices)
        {

            if (OriginOf(URL) is not { } origin)
                return;

            var host  = URL.Host.ToString();

            var h3    = AlternativeServices.IsClear
                            ? null
                            : AlternativeServices.Services.FirstOrDefault(
                                  service => service.ProtocolId == "h3" &&
                                             service.Port       != 0    &&
                                             service.MaxAge     >  TimeSpan.Zero &&
                                            (service.Host.Length == 0 || service.Host.Equals(host, StringComparison.OrdinalIgnoreCase))
                              );

            if (h3 is null)
            {
                overHTTP3.TryRemove(origin, out _);
                return;
            }

            // Not found there a while ago: see NotBelievedAgainFor.
            if (notFound.TryGetValue(origin, out var until))
            {

                if (until > Timestamp.Now)
                    return;

                notFound.TryRemove(origin, out _);

            }

            overHTTP3[origin] = (h3.Port, Timestamp.Now + h3.MaxAge);

        }

        #endregion

        #region TryGetHTTP3(URL, out Port)

        /// <summary>
        /// The UDP port the server of the given URL said it is reached over
        /// HTTP/3 on, while what it said holds.
        /// </summary>
        /// <param name="URL">The URL of the server.</param>
        /// <param name="Port">The UDP port.</param>
        public static Boolean TryGetHTTP3(URL URL, out UInt16 Port)
        {

            Port = 0;

            if (OriginOf(URL) is not { } origin ||
                !overHTTP3.TryGetValue(origin, out var known))
                return false;

            if (known.Until <= Timestamp.Now)
            {
                overHTTP3.TryRemove(origin, out _);
                return false;
            }

            Port = known.Port;
            return true;

        }

        #endregion

        #region NotFound(URL)

        /// <summary>
        /// The server of the given URL was not found where it said: what it
        /// said is forgotten, and not believed again for
        /// <see cref="NotBelievedAgainFor"/>.
        /// </summary>
        /// <param name="URL">The URL of the server.</param>
        public static void NotFound(URL URL)
        {
            if (OriginOf(URL) is { } origin)
            {
                overHTTP3.TryRemove(origin, out _);
                notFound[origin] = Timestamp.Now + NotBelievedAgainFor;
            }
        }

        #endregion

        #region Forget(URL)

        /// <summary>
        /// Forget everything of the server of the given URL: what it said, and
        /// that it was not found there.
        /// </summary>
        /// <param name="URL">The URL of the server.</param>
        public static void Forget(URL URL)
        {
            if (OriginOf(URL) is { } origin)
            {
                overHTTP3.TryRemove(origin, out _);
                notFound. TryRemove(origin, out _);
            }
        }

        #endregion

    }

}
