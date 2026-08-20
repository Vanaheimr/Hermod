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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// An event fired whenever a DNS query arrived inside an RFC 8484 request.
    /// </summary>
    public delegate Task OnDoHQueryReceivedDelegate (DateTimeOffset       Timestamp,
                                                     IDNSOverHTTPSServer  Server,
                                                     DNSPacket            Request,
                                                     CancellationToken    CancellationToken);

    /// <summary>
    /// An event fired whenever a DNS response left inside an RFC 8484 response.
    /// </summary>
    public delegate Task OnDoHResponseSentDelegate  (DateTimeOffset       Timestamp,
                                                     IDNSOverHTTPSServer  Server,
                                                     DNSPacket            Response,
                                                     CancellationToken    CancellationToken);


    /// <summary>
    /// What every DNS-over-HTTPS listener has, whichever version of HTTP it
    /// speaks.
    /// </summary>
    /// <remarks>
    /// RFC 8484 §5.2 recommends HTTP/2 without requiring it, so Hermod has two
    /// listeners — <see cref="DNSOverHTTPSServer"/> for HTTP/1.1 and
    /// <see cref="DNSOverHTTP2Server"/> for h2 — over one
    /// <see cref="DNSOverHTTPSResource"/>. This is what they have in common, and
    /// it is what an event handler or a log line needs: which resource, on which
    /// port, encrypted or not.
    /// </remarks>
    public interface IDNSOverHTTPSServer
    {

        /// <summary>
        /// The RFC 8484 resource being served — and through it the DNS pipeline,
        /// the zone and the keys.
        /// </summary>
        DNSOverHTTPSResource  Resource      { get; }

        /// <summary>
        /// The path it answers on.
        /// </summary>
        HTTPPath              DNSQueryPath  { get; }

        /// <summary>
        /// The port it bound.
        /// </summary>
        IPPort                TCPPort       { get; }

        /// <summary>
        /// Whether this listener actually speaks DoH, which RFC 8484 §5 makes a
        /// question about TLS: "This protocol MUST be used with the https URI
        /// scheme." False means the same resource in cleartext — for a
        /// TLS-terminating proxy in front, or a test that wants the HTTP layer
        /// visible.
        /// </summary>
        Boolean               IsSecured     { get; }

        /// <summary>
        /// The HTTP version this listener speaks, for a log line that would
        /// otherwise not say.
        /// </summary>
        String                HTTPVersion   { get; }

    }

}
