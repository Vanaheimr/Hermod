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

using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// An event fired whenever a DNS query arrived inside an RFC 8484 request.
    /// </summary>
    public delegate Task OnDoHQueryReceivedDelegate (DateTimeOffset      Timestamp,
                                                     DNSOverHTTPSServer  Server,
                                                     HTTPRequest         HTTPRequest,
                                                     DNSPacket           Request,
                                                     CancellationToken   CancellationToken);

    /// <summary>
    /// An event fired whenever a DNS response left inside an RFC 8484 response.
    /// </summary>
    public delegate Task OnDoHResponseSentDelegate  (DateTimeOffset      Timestamp,
                                                     DNSOverHTTPSServer  Server,
                                                     HTTPRequest         HTTPRequest,
                                                     DNSPacket           Response,
                                                     CancellationToken   CancellationToken);


    /// <summary>
    /// A DNS-over-HTTPS server (RFC 8484): one HTTP resource that takes a DNS
    /// query as <c>application/dns-message</c> and gives back a DNS response.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The DNS half of the work is <see cref="DNSMessagePipeline"/>'s, shared
    /// verbatim with <see cref="DNSServer"/>'s UDP, TCP and DoT listeners — so
    /// zones, TSIG keys, SIG(0) keys and padding behave here exactly as they do
    /// there. What is left, and all this class does, is the HTTP half: which
    /// method, which media type, which status code, and how long the answer may
    /// be considered fresh.
    /// </para>
    /// <para>
    /// Two things about DoH are easy to get wrong and are worth stating up front,
    /// because both are places where "the same as DoT" would be incorrect:
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
    /// <para>
    /// <i>HTTP versions.</i> This is Hermod's HTTP/1.1 pipeline, which RFC 8484
    /// §5.2 permits while recommending better: "HTTP/2 […] is the minimum
    /// RECOMMENDED version of HTTP for use with DoH", since "Earlier versions of
    /// HTTP are capable of conveying the semantic requirements of DoH but may
    /// result in very poor performance." The semantics below are complete; what
    /// HTTP/1.1 costs is the parallelism of §5.2, one query at a time per
    /// connection.
    /// </para>
    /// <para>
    /// <i>Cleartext.</i> RFC 8484 §5 is unambiguous — "This protocol MUST be used
    /// with the https URI scheme" — and so a server started without a certificate
    /// is not serving DoH. It is still useful, and deliberately supported: behind
    /// a TLS-terminating reverse proxy, and in tests where TLS would only hide
    /// the RFC 8484 layer under examination. Give <see cref="DNSServerOptions"/> a
    /// <c>TLSServerCertificate</c> and it is DoH; leave it null and it is the same
    /// endpoint on plain HTTP.
    /// </para>
    /// </remarks>
    public class DNSOverHTTPSServer : AHTTPServer
    {

        #region Data

        /// <summary>
        /// The default HTTP server name.
        /// </summary>
        public new const     String    DefaultHTTPServerName  = "GraphDefined Hermod DNS-over-HTTPS Server";

        /// <summary>
        /// The path this server answers RFC 8484 requests on. The RFC defines no
        /// well-known path — §3 leaves the URI to "a URI Template" the server
        /// publishes — but <c>/dns-query</c> is what its own examples use and what
        /// every deployed resolver settled on.
        /// </summary>
        public static readonly HTTPPath  DefaultDNSQueryPath  = HTTPPath.Parse("/dns-query");

        /// <summary>
        /// The name of the URI Template variable that carries a GET query.
        /// RFC 8484 §4.1: "the single variable 'dns' is defined as the content of
        /// the DNS request […] encoded with base64url".
        /// </summary>
        public const         String    DNSQueryParameterName  = "dns";

        /// <summary>
        /// RFC 8484 §6: "This media type restricts the maximum size of the DNS
        /// message to 65535 bytes." A POST body longer than that cannot be a DNS
        /// message, so it is refused before it is read.
        /// </summary>
        public const         UInt64    MaxDNSMessageSize      = 65535;

        private readonly ILogger<DNSOverHTTPSServer>  dnsLogger;

        #endregion

        #region Events

        /// <summary>
        /// An event fired whenever a DNS query arrived inside an RFC 8484 request.
        /// </summary>
        public event OnDoHQueryReceivedDelegate?  OnDoHQueryReceived;

        /// <summary>
        /// An event fired whenever a DNS response left inside an RFC 8484 response.
        /// </summary>
        public event OnDoHResponseSentDelegate?   OnDoHResponseSent;

        #endregion

        #region Properties

        /// <summary>
        /// The transport-independent half of the server: signature verification,
        /// the request handler, padding and serialization.
        /// </summary>
        public DNSMessagePipeline  Pipeline        { get; }

        /// <summary>
        /// The path this server answers RFC 8484 requests on. Anything else on
        /// this listener is a 404 — a DoH server is one resource, not a site.
        /// </summary>
        public HTTPPath            DNSQueryPath    { get; }

        /// <summary>
        /// The DNS-level options — zone keys, padding, compression — shared with
        /// every other transport built on the same pipeline.
        /// </summary>
        public DNSServerOptions    DNSOptions
            => Pipeline.Options;

        /// <summary>
        /// Whether this listener actually speaks DoH, which RFC 8484 §5 makes a
        /// question about TLS: "This protocol MUST be used with the https URI
        /// scheme." False means the same resource on cleartext HTTP, for a
        /// TLS-terminating proxy in front or a test that wants the HTTP layer
        /// visible.
        /// </summary>
        public Boolean             IsSecured       { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new DNS-over-HTTPS server (RFC 8484).
        /// </summary>
        /// <param name="RequestHandler">Whatever answers the questions. Defaults to the demo zone, exactly as <see cref="DNSServer"/> does.</param>
        /// <param name="DNSServerOptions">The DNS-level options: TLS certificate, TSIG/SIG(0) keys, compression.</param>
        /// <param name="IPAddress">The IP address to listen on.</param>
        /// <param name="TCPPort">The TCP port to listen on. Defaults to 443, the port RFC 8484 §5 implies by requiring the https scheme.</param>
        /// <param name="DNSQueryPath">The path to answer on, <c>/dns-query</c> by default.</param>
        /// <param name="Pipeline">An existing pipeline to share with other transports, instead of building one from the two parameters above.</param>
        /// <param name="HTTPServerName">An optional HTTP server name.</param>
        /// <param name="LoggerFactory">Where to report a refused query.</param>
        /// <param name="AutoStart">Start listening right away.</param>
        public DNSOverHTTPSServer(IDNSRequestHandler?  RequestHandler     = null,
                                  DNSServerOptions?    DNSServerOptions   = null,
                                  IIPAddress?          IPAddress          = null,
                                  IPPort?              TCPPort            = null,
                                  HTTPPath?            DNSQueryPath       = null,
                                  DNSMessagePipeline?  Pipeline           = null,
                                  String?              HTTPServerName     = null,
                                  I18NString?          Description        = null,
                                  TimeSpan?            ReceiveTimeout     = null,
                                  TimeSpan?            SendTimeout        = null,
                                  ILoggerFactory?      LoggerFactory      = null,
                                  Boolean?             AutoStart          = false)

            : base(IPAddress,
                   TCPPort ?? IPPort.HTTPS,
                   HTTPServerName ?? DefaultHTTPServerName,
                   Description,

                   BufferSize:                 null,
                   ReceiveTimeout:             ReceiveTimeout,
                   SendTimeout:                SendTimeout,
                   LoggingHandler:             null,

                   // No certificate means no TLS, and ATCPServer decides exactly
                   // that way: a null selector leaves the connection in cleartext.
                   ServerCertificateSelector:  (DNSServerOptions ?? Pipeline?.Options)?.TLSServerCertificate is X509Certificate2 certificate
                                                   ? (tcpServer, tcpClient) => certificate
                                                   : null,
                   ClientCertificateValidator: null,
                   LocalCertificateSelector:   null,
                   AllowedTLSProtocols:        (DNSServerOptions ?? Pipeline?.Options)?.TLSProtocols,
                   ClientCertificateRequired:  (DNSServerOptions ?? Pipeline?.Options)?.TLSClientCertificateRequired,
                   CheckCertificateRevocation: false,

                   ConnectionIdBuilder:        null,
                   MaxClientConnections:       null,
                   DNSClient:                  null,

                   DisableMaintenanceTasks:    true,
                   MaintenanceInitialDelay:    null,
                   MaintenanceEvery:           null,

                   DisableWardenTasks:         true,
                   WardenInitialDelay:         null,
                   WardenCheckEvery:           null,

                   LoggerFactory:              LoggerFactory,
                   AutoStart:                  false,

                   // RFC 8484 §6 caps a DNS message at 65535 bytes; a longer body
                   // is refused with 413 before it is read into memory.
                   MaxHTTPBodySize:            MaxDNSMessageSize)

        {

            this.Pipeline      = Pipeline ?? new DNSMessagePipeline(
                                                 RequestHandler,
                                                 DNSServerOptions,
                                                 (LoggerFactory ?? NullLoggerFactory.Instance).CreateLogger<DNSOverHTTPSServer>()
                                             );

            this.DNSQueryPath  = DNSQueryPath ?? DefaultDNSQueryPath;
            this.IsSecured     = this.Pipeline.Options.TLSServerCertificate is not null;
            this.dnsLogger     = (LoggerFactory ?? NullLoggerFactory.Instance).CreateLogger<DNSOverHTTPSServer>();

            if (AutoStart ?? false)
                Start().GetAwaiter().GetResult();

        }

        #endregion


        #region StartNew(...)

        /// <summary>
        /// Create a DNS-over-HTTPS server and start listening.
        /// </summary>
        public static async Task<DNSOverHTTPSServer>

            StartNew(IDNSRequestHandler?  RequestHandler     = null,
                     DNSServerOptions?    DNSServerOptions   = null,
                     IIPAddress?          IPAddress          = null,
                     IPPort?              TCPPort            = null,
                     HTTPPath?            DNSQueryPath       = null,
                     DNSMessagePipeline?  Pipeline           = null,
                     String?              HTTPServerName     = null,
                     ILoggerFactory?      LoggerFactory      = null)

        {

            var server = new DNSOverHTTPSServer(
                             RequestHandler,
                             DNSServerOptions,
                             IPAddress,
                             TCPPort,
                             DNSQueryPath,
                             Pipeline,
                             HTTPServerName,
                             LoggerFactory:  LoggerFactory
                         );

            await server.Start();

            return server;

        }

        #endregion


        #region (override) ProcessHTTPRequest(Request, Stream, CancellationToken = default)

        /// <summary>
        /// Answer one RFC 8484 request.
        /// </summary>
        /// <remarks>
        /// The order of the checks below is the order in which a request stops
        /// being an HTTP problem and starts being a DNS one. Everything before
        /// <see cref="DNSMessagePipeline.AcceptSignedRequest"/> can only be
        /// answered in HTTP terms, because there is no DNS message yet to answer
        /// with; everything after it is a DNS answer carried by a 200, however
        /// unhappy that answer is.
        /// </remarks>
        protected override async Task<HTTPResponse>

            ProcessHTTPRequest(HTTPRequest        Request,
                               Stream             Stream,
                               CancellationToken  CancellationToken   = default)

        {

            try
            {

                #region The resource

                // A DoH server is one resource, and RFC 8484 §3 leaves its URI to
                // the server to publish. Anything else here is simply not it.
                if (Request.Path != DNSQueryPath)
                    return Failed(Request, HTTPStatusCode.NotFound,
                                  $"This server answers DNS queries on {DNSQueryPath} only.");

                #endregion

                #region The method

                // RFC 8484 §4.1: "DoH servers MUST implement both the POST and GET
                // methods." HEAD comes along with GET, per the RFC 9110 §9.1 MUST
                // on general-purpose servers; the body it would have carried is
                // dropped by the HTTP layer, and the headers stay honest.
                var isGET  = Request.HTTPMethod == HTTPMethod.GET ||
                             Request.HTTPMethod == HTTPMethod.HEAD;

                var isPOST = Request.HTTPMethod == HTTPMethod.POST;

                if (!isGET && !isPOST)
                    return Failed(Request, HTTPStatusCode.MethodNotAllowed,
                                  "A DNS query is a GET or a POST (RFC 8484, Section 4.1).",
                                  builder => builder.Allow = [ HTTPMethod.GET, HTTPMethod.HEAD, HTTPMethod.POST ]);

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
                // Accept field has said nothing, and one asking for */* has said
                // the opposite; both are served. Only a list that names media types,
                // none of which this one satisfies, is a refusal — and RFC 8484 §4.1
                // expects even that to be rare, since it has the client "be prepared
                // to process 'application/dns-message' […] responses" whatever it
                // asked for.
                if (Request.Accept.Any() &&
                   !Request.Accept.Any(AcceptsDNSMessages))
                {
                    return Failed(Request, HTTPStatusCode.NotAcceptable,
                                  "This server answers with application/dns-message (RFC 8484, Section 5.4).");
                }

                #endregion

                #region The DNS query

                Byte[]? queryBytes;

                if (isGET)
                {

                    // RFC 8484 §4.1: "the single variable 'dns' is defined as the
                    // content of the DNS request […] encoded with base64url".
                    var dnsParameter = Request.QueryString.GetString(DNSQueryParameterName);

                    if (dnsParameter is null || dnsParameter.Length == 0)
                        return Failed(Request, HTTPStatusCode.BadRequest,
                                      $"A GET carries its DNS query in the '{DNSQueryParameterName}' parameter (RFC 8484, Section 4.1).");

                    if (!dnsParameter.TryParseBASE64URL(out queryBytes, out var errorResponse))
                        return Failed(Request, HTTPStatusCode.BadRequest,
                                      $"The '{DNSQueryParameterName}' parameter is not base64url: {errorResponse}");

                }

                else
                {

                    // RFC 8484 §4.1: "the DNS query is included as the message body
                    // of the HTTP request, and the Content-Type request header field
                    // indicates the media type of the message." A body announced as
                    // something else is the 415 of §4.2.1 — the client is told to
                    // try a different server rather than left guessing.
                    //
                    // A POST with no Content-Type at all is served: RFC 9110 §8.3
                    // leaves an unlabelled body to the recipient, which "MAY either
                    // assume a media type of 'application/octet-stream' […] or
                    // examine the data to determine its type" — and on this
                    // resource there is only one type it could be, so examining it
                    // is what the DNS parser below does anyway.
                    if (Request.ContentType is not null &&
                        Request.ContentType != HTTPContentType.Application.DNSMessage)
                    {
                        return Failed(Request, HTTPStatusCode.UnsupportedMediaType,
                                      "A DNS query is posted as application/dns-message (RFC 8484, Section 4.1).");
                    }

                    await Request.TryReadHTTPBodyStreamAsync(CancellationToken).ConfigureAwait(false);

                    queryBytes = Request.HTTPBody;

                    if (queryBytes is null || queryBytes.Length == 0)
                        return Failed(Request, HTTPStatusCode.BadRequest,
                                      "A POST carries its DNS query in the message body (RFC 8484, Section 4.1).");

                }

                if (queryBytes.Length > (Int32) MaxDNSMessageSize)
                    return Failed(Request, HTTPStatusCode.RequestEntityTooLarge,
                                  $"A DNS message is at most {MaxDNSMessageSize} bytes (RFC 8484, Section 6).");

                #endregion

                #region Transaction security (RFC 8945, RFC 2931)

                // From here on a refusal is a DNS refusal, and RFC 8484 §4.2.1 has
                // it travel with a 2xx like any other valid DNS response.
                if (!Pipeline.AcceptSignedRequest(queryBytes,
                                                  out var message,
                                                  out var securityContext,
                                                  out var securityError))
                {

                    return securityError is not null
                               ? DNSMessage(Request, securityError, TimeSpan.Zero)
                               : Failed(Request, HTTPStatusCode.BadRequest,
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

                    dnsLogger.LogDebug(e, "A DoH request from {RemoteSocket} could not be parsed", Request.RemoteSocket);

                    // FORMERR is a valid DNS response and says more than a bare 400:
                    // the client learns its message was unreadable, not that its
                    // HTTP was.
                    var formatError = DNSMessagePipeline.BuildFormatErrorResponse(queryBytes);

                    return formatError is not null
                               ? DNSMessage(Request, formatError, TimeSpan.Zero)
                               : Failed(Request, HTTPStatusCode.BadRequest,
                                        "The message is not a DNS query (RFC 8484, Section 6).");

                }

                await LogEvent(
                          OnDoHQueryReceived,
                          loggingDelegate => loggingDelegate.Invoke(
                              Timestamp.Now,
                              this,
                              Request,
                              dnsRequest,
                              CancellationToken
                          )
                      );

                #endregion

                #region Answer

                var dnsResponse = await Pipeline.ProcessRequest(
                                            dnsRequest,
                                            CancellationToken
                                        ).ConfigureAwait(false);

                // On a datagram, saying nothing is an answer in itself. Over HTTP
                // it is not available: the exchange has to end somehow, and RFC 8484
                // §10 allows the honest ending — "A DoH server can reply to queries
                // with an HTTP error for queries that it cannot fulfill."
                if (dnsResponse is null)
                    return Failed(Request, HTTPStatusCode.InternalServerError,
                                  "This server has no answer to that query.");

                var responseBytes = Pipeline.SerializeMessageResponse(
                                        dnsResponse,
                                        dnsRequest,
                                        securityContext,
                                        HonorRequestorPayloadSize: false
                                    );

                await LogEvent(
                          OnDoHResponseSent,
                          loggingDelegate => loggingDelegate.Invoke(
                              Timestamp.Now,
                              this,
                              Request,
                              dnsResponse,
                              CancellationToken
                          )
                      );

                return DNSMessage(
                           Request,
                           responseBytes,
                           FreshnessLifetimeOf(dnsResponse)
                       );

                #endregion

            }
            catch (Exception e)
            {

                dnsLogger.LogError(e, "A DoH request from {RemoteSocket} failed", Request.RemoteSocket);

                return Failed(Request, HTTPStatusCode.InternalServerError,
                              "The DNS query could not be processed.");

            }

        }

        #endregion


        #region (private)         DNSMessage           (Request, ResponseBytes, FreshnessLifetime)

        /// <summary>
        /// The 200 that carries a DNS response, whatever that response says.
        /// </summary>
        /// <param name="Request">The HTTP request being answered.</param>
        /// <param name="ResponseBytes">The DNS response, on the wire format of RFC 1035 §4.1.</param>
        /// <param name="FreshnessLifetime">How long this answer may be treated as fresh.</param>
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
        /// specific response header fields are sent; this is not widely implemented
        /// and is not advised for DoH". The header is sent there too because the
        /// requirement it satisfies is about the value, not the method: whatever
        /// lifetime this answer is given, it is the smallest TTL in it, and a
        /// responder that states that consistently cannot state it wrongly on the
        /// one path where somebody does cache.
        /// </para>
        /// </remarks>
        private HTTPResponse DNSMessage(HTTPRequest  Request,
                                        Byte[]       ResponseBytes,
                                        TimeSpan     FreshnessLifetime)

            => new HTTPResponse.Builder(Request) {
                   HTTPStatusCode  = HTTPStatusCode.OK,
                   Server          = HTTPServerName,
                   Date            = Timestamp.Now,
                   CacheControl    = $"max-age={(UInt32) Math.Max(0, FreshnessLifetime.TotalSeconds)}",
                   ContentType     = HTTPContentType.Application.DNSMessage,
                   ContentLength   = (UInt64) ResponseBytes.Length,
                   Content         = ResponseBytes
               }.AsImmutable;

        #endregion

        #region (private)         Failed               (Request, StatusCode, Description, Decorate = null)

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
        private HTTPResponse Failed(HTTPRequest                       Request,
                                    HTTPStatusCode                    StatusCode,
                                    String                            Description,
                                    Action<HTTPResponse.Builder>?     Decorate   = null)
        {

            var builder = new HTTPResponse.Builder(Request) {
                              HTTPStatusCode  = StatusCode,
                              Server          = HTTPServerName,
                              Date            = Timestamp.Now,
                              CacheControl    = "no-store",
                              ContentType     = HTTPContentType.Text.PLAIN,
                              Content         = Description.ToUTF8Bytes()
                          };

            Decorate?.Invoke(builder);

            return builder.AsImmutable;

        }

        #endregion

        #region (private static)  AcceptsDNSMessages   (AcceptType)

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

        #region (private static)  FreshnessLifetimeOf  (Response)

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
        /// Anything else — a bare NXDOMAIN with no SOA to cite, a FORMERR, an empty
        /// NOERROR — has nothing in it that says how long it stays true, so it is
        /// sent with a lifetime of zero. That is not the same as sending no header
        /// at all, which is what §5.1 warns against.
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


        #region (private) LogEvent (Logger, LogHandler, ...)

        private Task LogEvent<TDelegate>(TDelegate?                                         Logger,
                                         Func<TDelegate, Task>                              LogHandler,
                                         [CallerArgumentExpression(nameof(Logger))] String  EventName     = "",
                                         [CallerMemberName()]                       String  Command       = "")

            where TDelegate : Delegate

            => LogEvent(
                   nameof(DNSOverHTTPSServer),
                   Logger,
                   LogHandler,
                   EventName,
                   Command
               );

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"DNS-over-HTTP{(IsSecured ? "S" : "")} @ {IPAddress}:{TCPPort}{DNSQueryPath}";

        #endregion

    }

}
