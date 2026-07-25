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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP2
{

    /// <summary>
    /// Early data (RFC 8470): the <c>Early-Data</c> request field and the policy
    /// question behind <c>425 (Too Early)</c>.
    ///
    /// TLS 1.3 lets a client send application data in its very first flight, before
    /// the handshake completes. It is a real latency win and it comes with one
    /// specific hazard: those octets carry no proof of freshness, so an attacker who
    /// captured them can send them again and the server cannot tell the copy from
    /// the original. Everything about this file follows from that — <b>replay</b>,
    /// not eavesdropping.
    ///
    /// <b>This stack never terminates early data itself.</b> <c>SslStream</c>
    /// exposes no 0-RTT API at all — nothing to offer it with, nothing to accept it
    /// with, and no way to ask whether bytes arrived that way — so on a connection
    /// we terminate there is no replay window to defend. That is not a gap being
    /// papered over: it is why the server half here is about the <i>other</i> case
    /// the RFC defines, and the only one that can reach us.
    ///
    /// That case is an intermediary. A CDN or reverse proxy that accepted early data
    /// and forwarded the request onward must mark it <c>Early-Data: 1</c>
    /// (Section 5.1). The origin behind it then holds the risk without having seen
    /// the handshake, and the field exists precisely so it can decide. Ignoring the
    /// field is not neutral — it is silently accepting a replay the peer went out of
    /// its way to warn about.
    /// </summary>
    public static class HTTP2EarlyData
    {

        #region IsFlagged (RequestHeaders)

        /// <summary>
        /// Whether the request carries <c>Early-Data: 1</c> (RFC 8470, Section 5.1)
        /// — an intermediary telling us it forwarded this out of its own early data,
        /// and that it may therefore be a replay.
        /// </summary>
        public static Boolean IsFlagged(IEnumerable<(String Name, String Value)> RequestHeaders)

            => RequestHeaders.Any(header => header.Name == "early-data" &&
                                            header.Value.Trim() == "1");

        #endregion

        #region IsSafeMethod (Method)

        /// <summary>
        /// The safe methods (RFC 9110, Section 9.2.1), plus <c>QUERY</c>, which
        /// RFC 10008 defines as safe as well.
        ///
        /// Safety — not idempotence — is the right bar for a replay. <c>PUT</c> and
        /// <c>DELETE</c> are idempotent, yet replaying either one <i>after</i> a
        /// later request has changed the resource undoes that change; the guarantee
        /// only holds for repetition, not for reordering. A safe method has no
        /// intended effect to undo.
        /// </summary>
        public static Boolean IsSafeMethod(String? Method)

            => Method is "GET" or "HEAD" or "OPTIONS" or "TRACE" or "QUERY";

        #endregion

        #region IsSafeToProcess (RequestHeaders)

        /// <summary>
        /// The default policy for a request an intermediary flagged as early data:
        /// process it if its method is safe, decline it with <c>425</c> otherwise.
        /// </summary>
        public static Boolean IsSafeToProcess(IEnumerable<(String Name, String Value)> RequestHeaders)

            => IsSafeMethod(RequestHeaders.FirstOrDefault(header => header.Name == ":method").Value);

        #endregion

    }

}
