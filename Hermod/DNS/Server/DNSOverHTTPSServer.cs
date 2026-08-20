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
    /// A DNS-over-HTTPS server (RFC 8484) speaking HTTP/1.1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What RFC 8484 requires is <see cref="DNSOverHTTPSResource"/>'s, and the
    /// DNS underneath it is <see cref="DNSMessagePipeline"/>'s, shared verbatim
    /// with <see cref="DNSServer"/>'s UDP, TCP and DoT listeners. This class is
    /// the HTTP/1.1 rendering of the result and nothing else.
    /// </para>
    /// <para>
    /// RFC 8484 §5.2 permits this version while recommending better: "HTTP/2
    /// […] is the minimum RECOMMENDED version of HTTP for use with DoH", since
    /// "Earlier versions of HTTP are capable of conveying the semantic
    /// requirements of DoH but may result in very poor performance." The
    /// semantics are complete either way — what HTTP/1.1 costs is the
    /// parallelism of §5.2, one query at a time per connection. For the
    /// recommended version, see <see cref="DNSOverHTTP2Server"/>; the two are
    /// separate listeners on separate ports, because ALPN selects between two
    /// protocols on one port and Hermod's HTTP/1.1 pipeline is a TCP server
    /// rather than something a negotiated stream can be handed to.
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
    public class DNSOverHTTPSServer : AHTTPServer,
                                      IDNSOverHTTPSServer
    {

        #region Data

        /// <summary>
        /// The default HTTP server name.
        /// </summary>
        public new const  String  DefaultHTTPServerName  = "GraphDefined Hermod DNS-over-HTTPS Server";

        /// <summary>
        /// The path a DoH server answers on by default — see
        /// <see cref="DNSOverHTTPSResource.DefaultDNSQueryPath"/>, which is where
        /// the reasoning lives.
        /// </summary>
        public static HTTPPath  DefaultDNSQueryPath
            => DNSOverHTTPSResource.DefaultDNSQueryPath;

        /// <summary>
        /// The name of the URI Template variable that carries a GET query.
        /// </summary>
        public const  String  DNSQueryParameterName  = DNSOverHTTPSResource.DNSQueryParameterName;

        /// <summary>
        /// RFC 8484 §6: "This media type restricts the maximum size of the DNS
        /// message to 65535 bytes." A POST body longer than that cannot be a DNS
        /// message, so it is refused before it is read.
        /// </summary>
        public const  UInt64  MaxDNSMessageSize  = DNSOverHTTPSResource.MaxDNSMessageSize;

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
        /// The RFC 8484 resource this listener renders.
        /// </summary>
        public DNSOverHTTPSResource  Resource        { get; }

        /// <summary>
        /// The transport-independent half of the DNS server: signature
        /// verification, the request handler, padding and serialization.
        /// </summary>
        public DNSMessagePipeline    Pipeline
            => Resource.Pipeline;

        /// <summary>
        /// The path this server answers RFC 8484 requests on. Anything else on
        /// this listener is a 404 — a DoH server is one resource, not a site.
        /// </summary>
        public HTTPPath              DNSQueryPath
            => Resource.DNSQueryPath;

        /// <summary>
        /// The DNS-level options — zone keys, padding, compression — shared with
        /// every other transport built on the same pipeline.
        /// </summary>
        public DNSServerOptions      DNSOptions
            => Pipeline.Options;

        /// <summary>
        /// Whether this listener actually speaks DoH, which RFC 8484 §5 makes a
        /// question about TLS: "This protocol MUST be used with the https URI
        /// scheme." False means the same resource on cleartext HTTP, for a
        /// TLS-terminating proxy in front or a test that wants the HTTP layer
        /// visible.
        /// </summary>
        public Boolean               IsSecured       { get; }

        /// <inheritdoc />
        public String                HTTPVersion
            => "HTTP/1.1";

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

            this.dnsLogger  = (LoggerFactory ?? NullLoggerFactory.Instance).CreateLogger<DNSOverHTTPSServer>();

            this.Resource   = new DNSOverHTTPSResource(
                                  Pipeline ?? new DNSMessagePipeline(
                                                  RequestHandler,
                                                  DNSServerOptions,
                                                  this.dnsLogger
                                              ),
                                  DNSQueryPath,
                                  this.dnsLogger
                              );

            this.IsSecured  = Resource.Pipeline.Options.TLSServerCertificate is not null;

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
        /// Render one RFC 8484 exchange as HTTP/1.1.
        /// </summary>
        /// <remarks>
        /// All of the deciding is <see cref="DNSOverHTTPSResource"/>'s. What is
        /// left here is the part that is genuinely about this version of HTTP:
        /// reading the body off a stream that may be chunked, taking the query
        /// variable out of a parsed query string, and turning a result back into
        /// an <see cref="HTTPResponse"/>.
        /// </remarks>
        protected override async Task<HTTPResponse>

            ProcessHTTPRequest(HTTPRequest        Request,
                               Stream             Stream,
                               CancellationToken  CancellationToken   = default)

        {

            try
            {

                // Only for the methods that carry one. Asking for the body of a
                // GET would wait for octets the request never promised.
                if (Request.HTTPMethod == HTTPMethod.POST)
                    await Request.TryReadHTTPBodyStreamAsync(CancellationToken).ConfigureAwait(false);

                var result = await Resource.ProcessAsync(
                                       new DNSOverHTTPSRequest(
                                           Request.HTTPMethod,
                                           Request.Path,
                                           Request.QueryString.GetString(DNSOverHTTPSResource.DNSQueryParameterName),
                                           Request.ContentType?.ToString(),
                                           Request.GetHeaderField(HTTPRequestHeaderField.Accept)?.ToString(),
                                           Request.HTTPBody,
                                           Request.LocalSocket,
                                           Request.RemoteSocket
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

                var builder = new HTTPResponse.Builder(Request) {
                                  HTTPStatusCode  = result.StatusCode,
                                  Server          = HTTPServerName,
                                  Date            = Timestamp.Now,
                                  CacheControl    = result.CacheControl,
                                  ContentType     = result.ContentType,
                                  ContentLength   = (UInt64) result.Content.Length,
                                  Content         = result.Content
                              };

                // RFC 9110 §10.2.1: "An origin server MUST generate an Allow
                // header field in a 405 (Method Not Allowed) response."
                if (result.Allow is not null)
                    builder.Allow = [.. result.Allow];

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

                return builder.AsImmutable;

            }
            catch (Exception e)
            {

                dnsLogger.LogError(e, "A DoH request from {RemoteSocket} failed", Request.RemoteSocket);

                return new HTTPResponse.Builder(Request) {
                           HTTPStatusCode  = HTTPStatusCode.InternalServerError,
                           Server          = HTTPServerName,
                           Date            = Timestamp.Now,
                           CacheControl    = "no-store",
                           ContentType     = HTTPContentType.Text.PLAIN,
                           Content         = "The DNS query could not be processed.".ToUTF8Bytes()
                       }.AsImmutable;

            }

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
