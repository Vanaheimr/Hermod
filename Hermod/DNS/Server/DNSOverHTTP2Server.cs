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

using System.Net.Sockets;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// A DNS-over-HTTPS server (RFC 8484) speaking HTTP/2 — the version §5.2
    /// recommends.
    /// </summary>
    /// <remarks>
    /// <para>
    /// RFC 8484 §5.2: "HTTP/2 […] is the minimum RECOMMENDED version of HTTP for
    /// use with DoH", because "The messages in classic UDP-based DNS [RFC1035]
    /// are inherently unordered and have low overhead. A competitive HTTP
    /// transport needs to support reordering, parallelism, priority, and header
    /// compression to achieve similar performance." That is what this listener
    /// adds and the HTTP/1.1 one cannot: many queries in flight on one
    /// connection, answered in whatever order they finish.
    /// </para>
    /// <para>
    /// What it does not add is meaning. Every requirement of §4 — the methods,
    /// the media type, the status codes, the freshness lifetime, the ignored
    /// payload size — is <see cref="DNSOverHTTPSResource"/>'s, the same object
    /// <see cref="DNSOverHTTPSServer"/> renders as HTTP/1.1. This class turns
    /// pseudo-headers into a request and a result into HEADERS frames, and holds
    /// no opinion of its own about RFC 8484.
    /// </para>
    /// <para>
    /// <i>One port, both versions.</i> Over TLS this listener advertises
    /// <c>h2</c> and <c>http/1.1</c> and lets ALPN choose, which is how a real
    /// deployment serves 443: RFC 9113 §3.2 requires ALPN to select h2, and a
    /// client that cannot speak it asks for http/1.1 in the same handshake. The
    /// h2 side is rendered here; the http/1.1 side is handed to a
    /// <see cref="DNSOverHTTPSServer"/> that never listens on a port of its own
    /// and exists only to serve the negotiated stream, through
    /// <see cref="AHTTPServer.HandleHTTPStreamAsync"/>. Both share this
    /// listener's <see cref="DNSOverHTTPSResource"/>, so the two versions cannot
    /// drift apart in what they answer.
    /// </para>
    /// <para>
    /// Set <c>ServeHTTP11ViaALPN</c> to false for an h2-only endpoint, which then
    /// says so in the handshake — an ALPN offer of <c>http/1.1</c> alone fails
    /// rather than being accepted and not served. There is no negotiation in
    /// cleartext at all: h2c is prior knowledge, so the switch is ignored there.
    /// </para>
    /// <para>
    /// <i>Cleartext.</i> Without a certificate this serves h2c with prior
    /// knowledge (RFC 9113 §3.3) instead of DoH, which §5 defines as requiring
    /// the https scheme. Useful behind a TLS-terminating proxy, and in tests
    /// where TLS would only hide the layer under examination.
    /// </para>
    /// </remarks>
    public class DNSOverHTTP2Server : IDNSOverHTTPSServer
    {

        #region Data

        private readonly HTTP2Server                    http2Server;
        private readonly DNSOverHTTPSServer?            http11Renderer;
        private readonly ILogger<DNSOverHTTP2Server>    dnsLogger;

        private          CancellationTokenSource?       cancellationTokenSource;
        private          Task?                          listenerTask;

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

        /// <inheritdoc />
        public DNSOverHTTPSResource  Resource      { get; }

        /// <inheritdoc />
        public HTTPPath              DNSQueryPath
            => Resource.DNSQueryPath;

        /// <summary>
        /// The DNS-level options — zone keys, padding, compression — shared with
        /// every other transport built on the same pipeline.
        /// </summary>
        public DNSServerOptions      DNSOptions
            => Resource.Pipeline.Options;

        /// <summary>
        /// The address this listener bound.
        /// </summary>
        public IIPAddress            IPAddress     { get; }

        /// <summary>
        /// The port this listener bound. Known only once <see cref="Start"/> has
        /// returned, when the caller asked for an ephemeral one.
        /// </summary>
        public IPPort                TCPPort       { get; private set; }

        /// <inheritdoc />
        public Boolean               IsSecured     { get; }

        /// <summary>
        /// Whether an ALPN offer of <c>http/1.1</c> is answered on this port too.
        /// </summary>
        public Boolean               ServesHTTP11
            => http11Renderer is not null;

        /// <inheritdoc />
        public String                HTTPVersion
            => IsSecured
                   ? ServesHTTP11
                         ? "HTTP/2 + HTTP/1.1 (ALPN)"
                         : "HTTP/2"
                   : "HTTP/2 (h2c)";

        /// <summary>
        /// Whether the listener is accepting connections.
        /// </summary>
        public Boolean               IsRunning
            => cancellationTokenSource is not null &&
              !cancellationTokenSource.IsCancellationRequested;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new DNS-over-HTTPS server speaking HTTP/2 (RFC 8484 §5.2).
        /// </summary>
        /// <param name="RequestHandler">Whatever answers the questions. Defaults to the demo zone, exactly as <see cref="DNSServer"/> does.</param>
        /// <param name="DNSServerOptions">The DNS-level options: TLS certificate, TSIG/SIG(0) keys, compression.</param>
        /// <param name="IPAddress">The IP address to listen on.</param>
        /// <param name="TCPPort">The TCP port to listen on. Zero binds a free ephemeral one, which <see cref="TCPPort"/> reports once <see cref="Start"/> has returned.</param>
        /// <param name="DNSQueryPath">The path to answer on, <c>/dns-query</c> by default.</param>
        /// <param name="Pipeline">An existing pipeline to share with other transports, instead of building one from the two parameters above.</param>
        /// <param name="LoggerFactory">Where to report a query that could not be read.</param>
        /// <param name="ServeHTTP11ViaALPN">
        /// Answer an ALPN offer of <c>http/1.1</c> on this port as well, by
        /// handing the negotiated stream to an HTTP/1.1 renderer over the same
        /// resource. True by default, which is what a deployment on 443 wants.
        /// False makes the endpoint h2-only, and it stops advertising
        /// <c>http/1.1</c> rather than advertising what it will not serve. Has no
        /// effect in cleartext, where h2c is prior knowledge and nothing is
        /// negotiated.
        /// </param>
        public DNSOverHTTP2Server(IDNSRequestHandler?  RequestHandler       = null,
                                  DNSServerOptions?    DNSServerOptions     = null,
                                  IIPAddress?          IPAddress            = null,
                                  IPPort?              TCPPort              = null,
                                  HTTPPath?            DNSQueryPath         = null,
                                  DNSMessagePipeline?  Pipeline             = null,
                                  ILoggerFactory?      LoggerFactory        = null,
                                  Boolean              ServeHTTP11ViaALPN   = true)
        {

            this.dnsLogger    = (LoggerFactory ?? NullLoggerFactory.Instance).CreateLogger<DNSOverHTTP2Server>();

            var pipeline      = Pipeline ?? new DNSMessagePipeline(
                                                RequestHandler,
                                                DNSServerOptions,
                                                this.dnsLogger
                                            );

            this.Resource     = new DNSOverHTTPSResource(
                                    pipeline,
                                    DNSQueryPath,
                                    this.dnsLogger
                                );

            var certificate   = pipeline.Options.TLSServerCertificate;

            this.IsSecured    = certificate is not null;
            this.IPAddress    = IPAddress ?? IPv4Address.Localhost;

            // Zero is passed straight through: HTTP2Server reports back what it
            // bound, so Start() can read the real port off the listener rather
            // than grabbing one here and hoping it survives until the bind.
            this.TCPPort      = TCPPort ?? IPPort.Zero;

            // The HTTP/1.1 half of the ALPN offer: a DoH server over the very same
            // resource, constructed but never started. It binds nothing and
            // accepts nothing — its only job is to render streams this listener
            // has already negotiated.
            this.http11Renderer  = ServeHTTP11ViaALPN && certificate is not null
                                       ? new DNSOverHTTPSServer(
                                             DNSServerOptions:  pipeline.Options,
                                             IPAddress:         this.IPAddress,
                                             TCPPort:           IPPort.Zero,
                                             DNSQueryPath:      Resource.DNSQueryPath,
                                             Pipeline:          pipeline,
                                             LoggerFactory:     LoggerFactory
                                         )
                                       : null;

            this.http2Server  = new HTTP2Server(
                                    Address:             System.Net.IPAddress.Parse(this.IPAddress.ToString()),
                                    Port:                this.TCPPort.ToUInt16(),
                                    Certificate:         certificate,
                                    RequestHandler:      HandleHTTP2RequestAsync,
                                    Cleartext:           certificate is null,

                                    // RFC 8484 §6 caps a DNS message at 65535 octets,
                                    // so a longer body cannot be one.
                                    MaxRequestBodySize:  (Int64) DNSOverHTTPSResource.MaxDNSMessageSize,

                                    // Supplying this is also what makes the listener
                                    // advertise http/1.1 in the first place; without
                                    // it the endpoint is h2-only and says so.
                                    HTTP11Fallback:      http11Renderer is null
                                                             ? null
                                                             : ServeHTTP11StreamAsync
                                );

        }

        #endregion


        #region StartNew(...)

        /// <summary>
        /// Create an HTTP/2 DoH server and start listening.
        /// </summary>
        public static async Task<DNSOverHTTP2Server>

            StartNew(IDNSRequestHandler?  RequestHandler       = null,
                     DNSServerOptions?    DNSServerOptions     = null,
                     IIPAddress?          IPAddress            = null,
                     IPPort?              TCPPort              = null,
                     HTTPPath?            DNSQueryPath         = null,
                     DNSMessagePipeline?  Pipeline             = null,
                     ILoggerFactory?      LoggerFactory        = null,
                     Boolean              ServeHTTP11ViaALPN   = true)

        {

            var server = new DNSOverHTTP2Server(
                             RequestHandler,
                             DNSServerOptions,
                             IPAddress,
                             TCPPort,
                             DNSQueryPath,
                             Pipeline,
                             LoggerFactory,
                             ServeHTTP11ViaALPN
                         );

            await server.Start();

            return server;

        }

        #endregion

        #region Start()

        /// <summary>
        /// Start accepting connections, and return once the port does.
        /// </summary>
        /// <remarks>
        /// <see cref="HTTP2Server.RunAsync"/> runs until cancelled, so it is
        /// started rather than awaited — but returning before the socket is bound
        /// would hand the caller a port that refuses the first connection. So
        /// this waits for the listener to report the endpoint it took, which is
        /// also where a failed bind surfaces: as an exception out of
        /// <c>Start()</c>, rather than as a listener task that faulted quietly
        /// while everything downstream carried on.
        /// </remarks>
        public async Task Start()
        {

            if (IsRunning)
                return;

            // A stopped listener stays stopped: HTTP2Server cancels itself in
            // StopAsync and reports its endpoint exactly once, so starting again
            // would bind nothing and hand back the port of the previous run.
            // Saying so beats returning as though it had worked.
            if (http2Server.BoundEndPoint.IsCompleted)
                throw new InvalidOperationException(
                          $"This {nameof(DNSOverHTTP2Server)} has already run and cannot be started again. Create a new one."
                      );

            cancellationTokenSource = new CancellationTokenSource();
            listenerTask            = http2Server.RunAsync(cancellationTokenSource.Token);

            var bound = await http2Server.BoundEndPoint.
                                  WaitAsync(TimeSpan.FromSeconds(5)).
                                  ConfigureAwait(false);

            TCPPort   = IPPort.Parse((UInt16) bound.Port);

            dnsLogger.LogDebug(
                "DNS-over-HTTPS ({HTTPVersion}) listening on {IPAddress}:{TCPPort}{DNSQueryPath}",
                HTTPVersion,
                IPAddress,
                TCPPort,
                DNSQueryPath
            );

        }

        #endregion

        #region Stop()

        /// <summary>
        /// Stop accepting connections, letting every open connection see a GOAWAY
        /// first.
        /// </summary>
        public async Task Stop()
        {

            if (cancellationTokenSource is null)
                return;

            try
            {
                await http2Server.StopAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                dnsLogger.LogError(e, "Error stopping the HTTP/2 DoH listener");
            }

            cancellationTokenSource.Cancel();

            if (listenerTask is not null)
            {
                try
                {
                    await listenerTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                { }
                catch (ObjectDisposedException)
                { }
                catch (SocketException)
                { }
            }

            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
            listenerTask            = null;

        }

        #endregion


        #region (private) ServeHTTP11StreamAsync  (Stream, CancellationToken)

        /// <summary>
        /// Serve a connection whose ALPN negotiation chose <c>http/1.1</c>.
        /// </summary>
        /// <remarks>
        /// The stream arrives authenticated and positioned at the first request
        /// octet, which is exactly what Hermod's HTTP/1.1 pipeline needs — and
        /// what it could not be given until <c>HandleHTTPStreamAsync</c> existed,
        /// because a TCP server cannot be handed a connection it did not accept.
        ///
        /// The peer's address is not among what comes through: an
        /// <c>SslStream</c> does not carry the socket it was built on, and
        /// <see cref="HTTP2Server"/> passes only the stream. So the request is
        /// labelled with this listener's own endpoint and a zero remote, the same
        /// as the HTTP/2 path above — which matters to nothing here, since a DoH
        /// answer does not depend on who asked.
        /// </remarks>
        private Task ServeHTTP11StreamAsync(Stream             Stream,
                                            CancellationToken  CancellationToken)

            => http11Renderer is null
                   ? Task.CompletedTask
                   : http11Renderer.HandleHTTPStreamAsync(
                         Stream,
                         new IPSocket(IPAddress, TCPPort),
                         IPSocket.Zero,
                         Resource.Pipeline.Options.TLSServerCertificate,
                         null,
                         null,
                         CancellationToken
                     );

        #endregion

        #region (private) HandleHTTP2RequestAsync (StreamId, RequestHeaders, RequestBody, CancellationToken)

        /// <summary>
        /// Render one RFC 8484 exchange as HTTP/2.
        /// </summary>
        /// <remarks>
        /// The whole of this method is translation. RFC 9113 §8.3 replaces the
        /// request line with the <c>:method</c> and <c>:path</c> pseudo-header
        /// fields and the status line with <c>:status</c>; §8.2.1 requires field
        /// names to be lowercase. None of that changes what RFC 8484 asks for,
        /// which is why the deciding happens in
        /// <see cref="DNSOverHTTPSResource"/> and not here.
        /// </remarks>
        private async Task<(List<(String Name, String Value)> ResponseHeaders, Byte[]? ResponseBody)>

            HandleHTTP2RequestAsync(UInt32                            StreamId,
                                    List<(String Name, String Value)> RequestHeaders,
                                    Byte[]?                           RequestBody,
                                    CancellationToken                 CancellationToken)

        {

            try
            {

                var target      = HeaderValue(RequestHeaders, ":path")   ?? "/";
                var methodText  = HeaderValue(RequestHeaders, ":method") ?? "GET";
                var method      = HTTPMethod.TryParse(methodText);

                if (method is null)
                    return Render(
                               new DNSOverHTTPSResult(
                                   HTTPStatusCode.MethodNotAllowed,
                                   HTTPContentType.Text.PLAIN,
                                   $"'{methodText}' is not an HTTP method this server knows.".ToUTF8Bytes(),
                                   "no-store",
                                   DNSOverHTTPSResource.AllowedMethods
                               ),
                               IsHEAD: false
                           );

                // The target carries path and query together; QueryString.Parse
                // takes it whole and keeps only what follows the '?', which is
                // exactly how the HTTP/1.1 listener reads the same variable — so
                // both versions decode a ?dns= the same way, down to the quirks.
                var pathText    = target.IndexOf('?') is var mark && mark >= 0
                                      ? target[..mark]
                                      : target;

                var result      = await Resource.ProcessAsync(
                                            new DNSOverHTTPSRequest(
                                                method,
                                                HTTPPath.Parse(pathText),
                                                QueryString.Parse(target).GetString(DNSOverHTTPSResource.DNSQueryParameterName),
                                                HeaderValue(RequestHeaders, "content-type"),
                                                HeaderValue(RequestHeaders, "accept"),
                                                RequestBody,
                                                new IPSocket(IPAddress, TCPPort),
                                                IPSocket.Zero
                                            ),
                                            CancellationToken
                                        ).ConfigureAwait(false);

                if (result.DNSRequest is not null)
                    await LogEvent(
                              OnDoHQueryReceived,
                              loggingDelegate => loggingDelegate.Invoke(
                                  Timestamp.Now,
                                  this,
                                  result.DNSRequest,
                                  CancellationToken
                              )
                          );

                if (result.DNSResponse is not null)
                    await LogEvent(
                              OnDoHResponseSent,
                              loggingDelegate => loggingDelegate.Invoke(
                                  Timestamp.Now,
                                  this,
                                  result.DNSResponse,
                                  CancellationToken
                              )
                          );

                return Render(result, method == HTTPMethod.HEAD);

            }
            catch (OperationCanceledException)
            {
                // The peer reset the stream. HTTP2Connection wants this to
                // propagate rather than become a response nobody asked for.
                throw;
            }
            catch (Exception e)
            {

                dnsLogger.LogError(e, "An HTTP/2 DoH request on stream {StreamId} failed", StreamId);

                return Render(
                           new DNSOverHTTPSResult(
                               HTTPStatusCode.InternalServerError,
                               HTTPContentType.Text.PLAIN,
                               "The DNS query could not be processed.".ToUTF8Bytes(),
                               "no-store"
                           ),
                           IsHEAD: false
                       );

            }

        }

        #endregion

        #region (private static) Render      (Result, IsHEAD)

        /// <summary>
        /// A result as HTTP/2 response headers and a body.
        /// </summary>
        /// <remarks>
        /// Field names are lowercase because RFC 9113 §8.2.1 requires it — "field
        /// names MUST be converted to lowercase when constructing an HTTP/2
        /// message" — and a peer that finds an uppercase one MUST treat the
        /// message as malformed. Content-Length is stated even for a HEAD, whose
        /// body is dropped: RFC 9110 §9.3.2 has the fields of a HEAD response be
        /// "the same as ... a GET", which is the entire use for the method.
        /// </remarks>
        private static (List<(String Name, String Value)>, Byte[]?) Render(DNSOverHTTPSResult  Result,
                                                                           Boolean             IsHEAD)
        {

            var headers = new List<(String Name, String Value)> {
                              (":status",         Result.StatusCode.Code.ToString()),
                              ("server",          DNSOverHTTPSServer.DefaultHTTPServerName),
                              ("date",            Timestamp.Now.UtcDateTime.ToString("r")),
                              ("content-type",    Result.ContentType.ToString()),
                              ("content-length",  Result.Content.Length.ToString()),
                              ("cache-control",   Result.CacheControl)
                          };

            // RFC 9110 §10.2.1: "An origin server MUST generate an Allow header
            // field in a 405 (Method Not Allowed) response."
            if (Result.Allow is not null)
                headers.Add(("allow", Result.Allow.AggregateWith(", ")));

            return (headers, IsHEAD ? null : Result.Content);

        }

        #endregion

        #region (private static) HeaderValue (Headers, Name)

        /// <summary>
        /// One header field value, or null. Case-insensitive although RFC 9113
        /// §8.2.1 already requires lowercase — reading leniently costs nothing
        /// and does not weaken the rule, which is the sender's to keep.
        /// </summary>
        private static String? HeaderValue(List<(String Name, String Value)>  Headers,
                                           String                             Name)
        {

            foreach (var (name, value) in Headers)
            {
                if (name.Equals(Name, StringComparison.OrdinalIgnoreCase))
                    return value;
            }

            return null;

        }

        #endregion

        #region (private)        LogEvent (Logger, LogHandler, ...)

        private Task LogEvent<TDelegate>(TDelegate?                                         Logger,
                                         Func<TDelegate, Task>                              LogHandler,
                                         [CallerArgumentExpression(nameof(Logger))] String  EventName   = "",
                                         [CallerMemberName()]                       String  Command     = "")

            where TDelegate : Delegate

            => Logger.InvokeAllAsync(
                   LogHandler,
                   (exception, eventName) => {

                       dnsLogger.LogError(
                           exception,
                           "{Module}.{Command}.{EventName} failed",
                           nameof(DNSOverHTTP2Server),
                           Command,
                           eventName
                       );

                       return Task.CompletedTask;

                   },
                   EventName
               );

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"DNS-over-HTTP{(IsSecured ? "S" : "")} ({HTTPVersion}) @ {IPAddress}:{TCPPort}{DNSQueryPath}";

        #endregion

    }

}
