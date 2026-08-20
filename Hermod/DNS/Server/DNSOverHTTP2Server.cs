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

using System.Net;
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
    /// <i>One port each.</i> A real deployment serves both versions on 443 and
    /// lets ALPN choose. Hermod cannot do that in one listener yet: its HTTP/1.1
    /// pipeline is a TCP server rather than something a negotiated stream can be
    /// handed to, so <see cref="HTTP2Server"/>'s HTTP/1.1 fallback has nothing to
    /// hand it to. Until it does, this listener is h2-only and says so in the
    /// handshake — an ALPN offer of <c>http/1.1</c> alone fails rather than being
    /// accepted and then not served, which is the honest half of the choice.
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

        /// <inheritdoc />
        public IPPort                TCPPort       { get; }

        /// <inheritdoc />
        public Boolean               IsSecured     { get; }

        /// <inheritdoc />
        public String                HTTPVersion
            => IsSecured ? "HTTP/2" : "HTTP/2 (h2c)";

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
        /// <param name="TCPPort">The TCP port to listen on. Zero takes a free ephemeral one, since <see cref="HTTP2Server"/> binds inside its accept loop and never reports back what it got.</param>
        /// <param name="DNSQueryPath">The path to answer on, <c>/dns-query</c> by default.</param>
        /// <param name="Pipeline">An existing pipeline to share with other transports, instead of building one from the two parameters above.</param>
        /// <param name="LoggerFactory">Where to report a query that could not be read.</param>
        public DNSOverHTTP2Server(IDNSRequestHandler?  RequestHandler     = null,
                                  DNSServerOptions?    DNSServerOptions   = null,
                                  IIPAddress?          IPAddress          = null,
                                  IPPort?              TCPPort            = null,
                                  HTTPPath?            DNSQueryPath       = null,
                                  DNSMessagePipeline?  Pipeline           = null,
                                  ILoggerFactory?      LoggerFactory      = null)
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

            // HTTP2Server takes the port up front and creates its TcpListener
            // inside RunAsync, so a port of zero would be bound to something
            // nobody ever learns. Choosing a free one here keeps "port 0 means
            // pick one for me" working, at the cost of the usual race — the port
            // is free a moment before it is taken, not while.
            this.TCPPort      = TCPPort is null || TCPPort.Value.ToUInt16() == 0
                                    ? IPPort.Parse(FreeTCPPort())
                                    : TCPPort.Value;

            this.http2Server  = new HTTP2Server(
                                    Address:             System.Net.IPAddress.Parse(this.IPAddress.ToString()),
                                    Port:                this.TCPPort.ToUInt16(),
                                    Certificate:         certificate,
                                    RequestHandler:      HandleHTTP2RequestAsync,
                                    Cleartext:           certificate is null,

                                    // RFC 8484 §6 caps a DNS message at 65535 octets,
                                    // so a longer body cannot be one.
                                    MaxRequestBodySize:  (Int64) DNSOverHTTPSResource.MaxDNSMessageSize
                                );

        }

        #endregion


        #region StartNew(...)

        /// <summary>
        /// Create an HTTP/2 DoH server and start listening.
        /// </summary>
        public static async Task<DNSOverHTTP2Server>

            StartNew(IDNSRequestHandler?  RequestHandler     = null,
                     DNSServerOptions?    DNSServerOptions   = null,
                     IIPAddress?          IPAddress          = null,
                     IPPort?              TCPPort            = null,
                     HTTPPath?            DNSQueryPath       = null,
                     DNSMessagePipeline?  Pipeline           = null,
                     ILoggerFactory?      LoggerFactory      = null)

        {

            var server = new DNSOverHTTP2Server(
                             RequestHandler,
                             DNSServerOptions,
                             IPAddress,
                             TCPPort,
                             DNSQueryPath,
                             Pipeline,
                             LoggerFactory
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
        /// this waits for the listener, the same way
        /// <see cref="DNSServer"/> waits for its own.
        /// </remarks>
        public async Task Start()
        {

            if (IsRunning)
                return;

            cancellationTokenSource = new CancellationTokenSource();
            listenerTask            = http2Server.RunAsync(cancellationTokenSource.Token);

            await WaitUntilListening(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

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

        #region (private)        WaitUntilListening (Deadline)

        /// <summary>
        /// Poll the port until it accepts a connection, or the deadline passes.
        /// </summary>
        private async Task WaitUntilListening(TimeSpan Deadline)
        {

            var until = Timestamp.Now + Deadline;

            while (Timestamp.Now < until)
            {

                try
                {
                    using var probe = new TcpClient();
                    await probe.ConnectAsync(
                                   System.Net.IPAddress.Parse(IPAddress.ToString()),
                                   TCPPort.ToUInt16()
                               ).ConfigureAwait(false);
                    return;
                }
                catch (SocketException)
                {
                    await Task.Delay(20).ConfigureAwait(false);
                }

            }

            throw new TimeoutException($"The HTTP/2 DoH listener did not bind {IPAddress}:{TCPPort}.");

        }

        #endregion

        #region (private static) FreeTCPPort()

        /// <summary>
        /// A TCP port that was free a moment ago.
        /// </summary>
        private static UInt16 FreeTCPPort()
        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);

            listener.Start();
            var port = (UInt16) ((IPEndPoint) listener.LocalEndpoint).Port;
            listener.Stop();

            return port;

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
