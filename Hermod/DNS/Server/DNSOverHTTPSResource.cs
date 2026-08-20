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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// One RFC 8484 request, in the terms RFC 9110 defines it: a method, a
    /// target, and a representation.
    /// </summary>
    /// <remarks>
    /// Deliberately not an <see cref="HTTPRequest"/>. HTTP/1.1 carries these as
    /// a request line and field lines, HTTP/2 as pseudo-headers and a HEADERS
    /// frame; RFC 8484 §4 mentions neither, and the resource that implements it
    /// should not have to either.
    /// </remarks>
    /// <param name="Method">The request method.</param>
    /// <param name="Path">The request target, without its query.</param>
    /// <param name="DNSParameter">The value of the <c>dns</c> query variable, or null when the target carried none.</param>
    /// <param name="ContentType">The Content-Type field value, or null when the request stated none.</param>
    /// <param name="Accept">The Accept field value, or null when the request stated none.</param>
    /// <param name="Body">The message body, or null for a request that has none.</param>
    /// <param name="LocalSocket">Where the request arrived.</param>
    /// <param name="RemoteSocket">Where it came from.</param>
    public sealed record DNSOverHTTPSRequest(
        HTTPMethod  Method,
        HTTPPath    Path,
        String?     DNSParameter,
        String?     ContentType,
        String?     Accept,
        Byte[]?     Body,
        IPSocket    LocalSocket,
        IPSocket    RemoteSocket
    );


    /// <summary>
    /// What to answer one RFC 8484 request with, before any version of HTTP has
    /// been asked to render it.
    /// </summary>
    /// <param name="StatusCode">The status code.</param>
    /// <param name="ContentType">The media type of <paramref name="Content"/>.</param>
    /// <param name="Content">The representation to send.</param>
    /// <param name="CacheControl">The Cache-Control field value.</param>
    /// <param name="Allow">The methods to name in an Allow field, or null to send none.</param>
    /// <param name="DNSRequest">The DNS query, once there was one — for the caller's events, not for the wire.</param>
    /// <param name="DNSResponse">The DNS response, once there was one.</param>
    public sealed record DNSOverHTTPSResult(
        HTTPStatusCode               StatusCode,
        HTTPContentType              ContentType,
        Byte[]                       Content,
        String                       CacheControl,
        IEnumerable<HTTPMethod>?     Allow         = null,
        DNSPacket?                   DNSRequest    = null,
        DNSResponse?                 DNSResponse   = null
    );


    /// <summary>
    /// The RFC 8484 resource: a DNS query in, a DNS response out, and the HTTP
    /// semantics that go around them — but no HTTP version.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The DNS half of the work is <see cref="DNSMessagePipeline"/>'s, shared
    /// with <see cref="DNSServer"/>'s UDP, TCP and DoT listeners. The HTTP half
    /// is here, and it is version-independent on purpose: RFC 8484 §4 is written
    /// in RFC 9110 semantics — methods, media types, status codes — and says
    /// nothing about how they reach the wire. §5.2 then recommends HTTP/2 as the
    /// version to carry them, which is a statement about performance, not about
    /// meaning.
    /// </para>
    /// <para>
    /// So <see cref="DNSOverHTTPSServer"/> renders these results as HTTP/1.1 and
    /// <see cref="DNSOverHTTP2Server"/> renders them as HEADERS frames, and
    /// neither gets an opinion about what 415 means. A third version later would
    /// be another renderer.
    /// </para>
    /// <para>
    /// Two things about DoH are easy to get wrong, and both are places where
    /// "the same as DoT" would be incorrect:
    /// </para>
    /// <para>
    /// A DNS error is not an HTTP error. RFC 8484 §4.2.1: "A successful HTTP
    /// response with a 2xx status code […] is used for any valid DNS response,
    /// regardless of the DNS response code." NXDOMAIN and SERVFAIL are answers,
    /// and they travel with 200. The 4xx and 5xx codes below are reserved for
    /// requests that never became a DNS question at all.
    /// </para>
    /// <para>
    /// The requestor's EDNS(0) payload size means nothing here. RFC 8484 §6:
    /// "DoH servers using this media type MUST ignore the value given for the
    /// EDNS UDP payload size in DNS requests." There is no datagram, so there is
    /// no buffer to overflow — the response is never truncated to fit it, and it
    /// never shortens the padding either.
    /// </para>
    /// </remarks>
    public class DNSOverHTTPSResource
    {

        #region Data

        /// <summary>
        /// The path a DoH server answers on by default. RFC 8484 defines no
        /// well-known path — §3 leaves the URI to "a URI Template" the server
        /// publishes — but <c>/dns-query</c> is what its own examples use and
        /// what every deployed resolver settled on.
        /// </summary>
        public static readonly HTTPPath  DefaultDNSQueryPath  = HTTPPath.Parse("/dns-query");

        /// <summary>
        /// The name of the URI Template variable that carries a GET query.
        /// RFC 8484 §4.1: "the single variable 'dns' is defined as the content of
        /// the DNS request […] encoded with base64url".
        /// </summary>
        public const           String    DNSQueryParameterName  = "dns";

        /// <summary>
        /// RFC 8484 §6: "This media type restricts the maximum size of the DNS
        /// message to 65535 bytes." A longer body cannot be a DNS message.
        /// </summary>
        public const           UInt64    MaxDNSMessageSize      = 65535;

        /// <summary>
        /// The methods RFC 8484 §4.1 requires, plus the HEAD that RFC 9110 §9.1
        /// requires of any general-purpose server that answers GET.
        /// </summary>
        public static readonly HTTPMethod[]  AllowedMethods  = [ HTTPMethod.GET, HTTPMethod.HEAD, HTTPMethod.POST ];

        private readonly ILogger  logger;

        #endregion

        #region Properties

        /// <summary>
        /// The transport-independent half of the DNS server: signature
        /// verification, the request handler, padding and serialization.
        /// </summary>
        public DNSMessagePipeline  Pipeline      { get; }

        /// <summary>
        /// The path this resource answers on. Anything else is a 404 — a DoH
        /// server is one resource, not a site.
        /// </summary>
        public HTTPPath            DNSQueryPath  { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create the RFC 8484 resource.
        /// </summary>
        /// <param name="Pipeline">The DNS side of the work.</param>
        /// <param name="DNSQueryPath">The path to answer on, <c>/dns-query</c> by default.</param>
        /// <param name="Logger">Where to report a query that could not be read.</param>
        public DNSOverHTTPSResource(DNSMessagePipeline  Pipeline,
                                    HTTPPath?           DNSQueryPath   = null,
                                    ILogger?            Logger         = null)
        {

            this.Pipeline      = Pipeline;
            this.DNSQueryPath  = DNSQueryPath ?? DefaultDNSQueryPath;
            this.logger        = Logger       ?? NullLogger.Instance;

        }

        #endregion


        #region ProcessAsync(Request, CancellationToken = default)

        /// <summary>
        /// Answer one RFC 8484 request.
        /// </summary>
        /// <remarks>
        /// The order of the checks is the order in which a request stops being an
        /// HTTP problem and starts being a DNS one. Everything before
        /// <see cref="DNSMessagePipeline.AcceptSignedRequest"/> can only be
        /// answered in HTTP terms, because there is no DNS message yet to answer
        /// with; everything after it is a DNS answer carried by a 200, however
        /// unhappy that answer is.
        /// </remarks>
        public async Task<DNSOverHTTPSResult> ProcessAsync(DNSOverHTTPSRequest  Request,
                                                           CancellationToken    CancellationToken   = default)
        {

            #region The resource

            // RFC 8484 §3 leaves the URI to the server to publish, so a DoH server
            // is one resource rather than a site. Anything else here is not it.
            if (Request.Path != DNSQueryPath)
                return Failed(HTTPStatusCode.NotFound,
                              $"This server answers DNS queries on {DNSQueryPath} only.");

            #endregion

            #region The method

            // RFC 8484 §4.1: "DoH servers MUST implement both the POST and GET
            // methods." HEAD comes along with GET, per the RFC 9110 §9.1 MUST on
            // general-purpose servers; dropping the body it would have carried is
            // the renderer's job, and the headers stay honest either way.
            var isGET  = Request.Method == HTTPMethod.GET ||
                         Request.Method == HTTPMethod.HEAD;

            var isPOST = Request.Method == HTTPMethod.POST;

            if (!isGET && !isPOST)
                return Failed(HTTPStatusCode.MethodNotAllowed,
                              "A DNS query is a GET or a POST (RFC 8484, Section 4.1).",
                              AllowedMethods);

            #endregion

            #region What the client will accept

            // RFC 8484 §5.4: "DoH clients and DoH servers MUST support the
            // 'application/dns-message' media type." It is the only one this
            // server produces, so a client that has ruled it out has ruled out
            // every answer there is — §4.2.1 names 406 for exactly that, "where
            // the server cannot generate a representation suitable for the
            // client".
            //
            // Ruling it out has to be deliberate to count. A request with no
            // Accept field has said nothing, and one asking for */* has said the
            // opposite; both are served. Only a list that names media types, none
            // of which this one satisfies, is a refusal — and RFC 8484 §4.1
            // expects even that to be rare, since it has the client "be prepared
            // to process 'application/dns-message' […] responses" whatever it
            // asked for.
            if (Request.Accept is not null &&
                AcceptTypes.TryParse(Request.Accept, out var acceptTypes) &&
                acceptTypes.Any() &&
               !acceptTypes.Any(AcceptsDNSMessages))
            {
                return Failed(HTTPStatusCode.NotAcceptable,
                              "This server answers with application/dns-message (RFC 8484, Section 5.4).");
            }

            #endregion

            #region The DNS query

            Byte[]? queryBytes;

            if (isGET)
            {

                // RFC 8484 §4.1: "the single variable 'dns' is defined as the
                // content of the DNS request […] encoded with base64url".
                if (Request.DNSParameter is null || Request.DNSParameter.Length == 0)
                    return Failed(HTTPStatusCode.BadRequest,
                                  $"A GET carries its DNS query in the '{DNSQueryParameterName}' parameter (RFC 8484, Section 4.1).");

                if (!Request.DNSParameter.TryParseBASE64URL(out queryBytes, out var errorResponse))
                    return Failed(HTTPStatusCode.BadRequest,
                                  $"The '{DNSQueryParameterName}' parameter is not base64url: {errorResponse}");

            }

            else
            {

                // RFC 8484 §4.1: "the DNS query is included as the message body of
                // the HTTP request, and the Content-Type request header field
                // indicates the media type of the message." A body announced as
                // something else is the 415 of §4.2.1 — the client is told to try
                // a different server rather than left guessing.
                //
                // A POST with no Content-Type at all is served: RFC 9110 §8.3
                // leaves an unlabelled body to the recipient, which "MAY either
                // assume a media type of 'application/octet-stream' […] or examine
                // the data to determine its type" — and on this resource there is
                // only one type it could be, so examining it is what the DNS
                // parser below does anyway.
                if (Request.ContentType is not null &&
                   !(HTTPContentType.TryParse(Request.ContentType, out var contentType) &&
                     contentType == HTTPContentType.Application.DNSMessage))
                {
                    return Failed(HTTPStatusCode.UnsupportedMediaType,
                                  "A DNS query is posted as application/dns-message (RFC 8484, Section 4.1).");
                }

                queryBytes = Request.Body;

                if (queryBytes is null || queryBytes.Length == 0)
                    return Failed(HTTPStatusCode.BadRequest,
                                  "A POST carries its DNS query in the message body (RFC 8484, Section 4.1).");

            }

            if (queryBytes.Length > (Int32) MaxDNSMessageSize)
                return Failed(HTTPStatusCode.RequestEntityTooLarge,
                              $"A DNS message is at most {MaxDNSMessageSize} bytes (RFC 8484, Section 6).");

            #endregion

            #region Transaction security (RFC 8945, RFC 2931)

            // From here on a refusal is a DNS refusal, and RFC 8484 §4.2.1 has it
            // travel with a 2xx like any other valid DNS response.
            if (!Pipeline.AcceptSignedRequest(queryBytes,
                                              out var message,
                                              out var securityContext,
                                              out var securityError))
            {

                return securityError is not null
                           ? DNSMessage(securityError, TimeSpan.Zero)
                           : Failed(HTTPStatusCode.BadRequest,
                                    "The DNS query carries a transaction signature this server will not accept.");

            }

            #endregion

            #region Parse

            DNSPacket dnsRequest;

            try
            {
                dnsRequest = DNSPacket.Parse(
                                 Request.LocalSocket,
                                 Request.RemoteSocket,
                                 new MemoryStream(message)
                             );
            }
            catch (Exception e)
            {

                logger.LogDebug(e, "A DoH request from {RemoteSocket} could not be parsed", Request.RemoteSocket);

                // FORMERR is a valid DNS response and says more than a bare 400:
                // the client learns its message was unreadable, not that its HTTP
                // was.
                var formatError = DNSMessagePipeline.BuildFormatErrorResponse(queryBytes);

                return formatError is not null
                           ? DNSMessage(formatError, TimeSpan.Zero)
                           : Failed(HTTPStatusCode.BadRequest,
                                    "The message is not a DNS query (RFC 8484, Section 6).");

            }

            #endregion

            #region Answer

            var dnsResponse = await Pipeline.ProcessRequest(
                                        dnsRequest,
                                        CancellationToken
                                    ).ConfigureAwait(false);

            // On a datagram, saying nothing is an answer in itself. Over HTTP it
            // is not available: the exchange has to end somehow, and RFC 8484 §10
            // allows the honest ending — "A DoH server can reply to queries with
            // an HTTP error for queries that it cannot fulfill."
            if (dnsResponse is null)
                return Failed(HTTPStatusCode.InternalServerError,
                              "This server has no answer to that query.");

            var responseBytes = Pipeline.SerializeMessageResponse(
                                    dnsResponse,
                                    dnsRequest,
                                    securityContext,
                                    HonorRequestorPayloadSize: false
                                );

            return DNSMessage(
                       responseBytes,
                       FreshnessLifetimeOf(dnsResponse),
                       dnsRequest,
                       dnsResponse
                   );

            #endregion

        }

        #endregion


        #region (private static) DNSMessage          (ResponseBytes, FreshnessLifetime, DNSRequest = null, DNSResponse = null)

        /// <summary>
        /// The 200 that carries a DNS response, whatever that response says.
        /// </summary>
        /// <remarks>
        /// <para>
        /// RFC 8484 §5.1: "DoH servers SHOULD assign an explicit HTTP freshness
        /// lifetime […] so that the DoH client is more likely to use fresh DNS
        /// data. This requirement is due to HTTP caches being able to assign their
        /// own heuristic freshness […] which would take control of the cache
        /// contents out of the hands of the DoH server." So the header is always
        /// sent, and a zero lifetime is sent as <c>max-age=0</c> rather than
        /// omitted: saying nothing is what hands the decision to the cache.
        /// </para>
        /// <para>
        /// Always, including on the answer to a POST, which §5.1 notes is not
        /// cached anyway — "responses to POST requests are not cacheable unless
        /// specific response header fields are sent; this is not widely
        /// implemented and is not advised for DoH". The field is sent there too
        /// because the requirement it satisfies is about the value, not the
        /// method: whatever lifetime this answer is given, it is the smallest TTL
        /// in it, and a responder that states that consistently cannot state it
        /// wrongly on the one path where somebody does cache.
        /// </para>
        /// </remarks>
        private static DNSOverHTTPSResult DNSMessage(Byte[]        ResponseBytes,
                                                     TimeSpan      FreshnessLifetime,
                                                     DNSPacket?    DNSRequest    = null,
                                                     DNSResponse?  DNSResponse   = null)

            => new (
                   HTTPStatusCode.OK,
                   HTTPContentType.Application.DNSMessage,
                   ResponseBytes,
                   $"max-age={(UInt32) Math.Max(0, FreshnessLifetime.TotalSeconds)}",
                   null,
                   DNSRequest,
                   DNSResponse
               );

        #endregion

        #region (private static) Failed              (StatusCode, Description, Allow = null)

        /// <summary>
        /// The answer to a request that never became a DNS question.
        /// </summary>
        /// <remarks>
        /// Deliberately not a DNS message: RFC 8484 §4.2.1 — "HTTP responses with
        /// non-successful HTTP status codes do not contain replies to the original
        /// DNS question in the HTTP request." The plain-text body says what went
        /// wrong to whoever is reading a log; the status code is what the client
        /// acts on.
        /// </remarks>
        private static DNSOverHTTPSResult Failed(HTTPStatusCode            StatusCode,
                                                 String                    Description,
                                                 IEnumerable<HTTPMethod>?  Allow   = null)

            => new (
                   StatusCode,
                   HTTPContentType.Text.PLAIN,
                   Description.ToUTF8Bytes(),
                   "no-store",
                   Allow
               );

        #endregion

        #region (private static) AcceptsDNSMessages  (AcceptType)

        /// <summary>
        /// Whether one entry of an Accept field leaves room for
        /// <c>application/dns-message</c>.
        /// </summary>
        /// <remarks>
        /// The media type itself, or either wildcard that covers it. A quality of
        /// zero is not room: RFC 9110 §12.4.2 — "a value of 0 means 'not
        /// acceptable'" — so <c>application/dns-message;q=0</c> names the type in
        /// order to exclude it, and is read as the exclusion it is.
        /// </remarks>
        private static Boolean AcceptsDNSMessages(AcceptType AcceptType)
        {

            if (AcceptType.Quality <= 0)
                return false;

            var contentType = AcceptType.ContentType;

            return contentType == HTTPContentType.Application.DNSMessage ||

                   contentType.MediaSubType  == "*" &&
                  (contentType.MediaMainType == "*" ||
                   contentType.MediaMainType.Equals(HTTPContentType.Application.DNSMessage.MediaMainType,
                                                    StringComparison.OrdinalIgnoreCase));

        }

        #endregion

        #region (private static) FreshnessLifetimeOf (Response)

        /// <summary>
        /// How long an HTTP cache may treat this DNS response as fresh.
        /// </summary>
        /// <remarks>
        /// <para>
        /// RFC 8484 §5.1: "The assigned freshness lifetime of a DoH HTTP response
        /// MUST be less than or equal to the smallest TTL in the Answer section of
        /// the DNS response. A freshness lifetime equal to the smallest TTL in the
        /// Answer section is RECOMMENDED." Taking the smallest is what keeps the
        /// cached message honest as a whole: an HTTP cache stores one message and
        /// hands back all of it, so the first record to expire has to end the
        /// lifetime of the rest.
        /// </para>
        /// <para>
        /// With nothing in the Answer section the answer is a denial, and §5.1
        /// says where its lifetime comes from: "If the DNS response has no records
        /// in the Answer section, and the DNS response has an SOA record in the
        /// Authority section, the response freshness lifetime MUST NOT be greater
        /// than the MINIMUM field from that SOA record." RFC 2308 §3 bounds it
        /// from the other side as well — a negative answer is cached for "the
        /// minimum of the MINIMUM field of the SOA record and the TTL of the SOA
        /// itself" — so both are applied and the smaller wins, which satisfies the
        /// MUST above by construction.
        /// </para>
        /// <para>
        /// Anything else — a bare NXDOMAIN with no SOA to cite, a FORMERR, an
        /// empty NOERROR — has nothing in it that says how long it stays true, so
        /// it is sent with a lifetime of zero. That is not the same as sending no
        /// header at all, which is what §5.1 warns against.
        /// </para>
        /// </remarks>
        private static TimeSpan FreshnessLifetimeOf(DNSPacket Response)
        {

            var answers = Response.AnswerRRs.
                                   Where(rr => rr is not OPT).
                                   ToArray();

            if (answers.Length != 0)
                return answers.Min(rr => rr.TimeToLive);

            var soa = Response.AuthorityRRs.OfType<SOA>().FirstOrDefault();

            if (soa is not null)
                return soa.Minimum < soa.TimeToLive
                           ? soa.Minimum
                           : soa.TimeToLive;

            return TimeSpan.Zero;

        }

        #endregion

    }

}
