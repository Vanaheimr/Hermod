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
using System.Text;
using System.Buffers;
using System.Security.Authentication;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.TCP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// HTTP server started delegate.
    /// </summary>
    /// <param name="HTTPServer">The sender of this event.</param>
    /// <param name="Timestamp">The timestamp of the TCP server started event.</param>
    /// <param name="EventTrackingId">An unique event tracking identification for correlating this request with other events.</param>
    /// <param name="Message">An optional message.</param>
    public delegate Task HTTPServerStartedDelegate(DateTimeOffset    Timestamp,
                                                   AHTTPServer       HTTPServer,
                                                   EventTracking_Id  EventTrackingId,
                                                   String?           Message   = null);

    /// <summary>
    /// HTTP server stopped delegate.
    /// </summary>
    /// <param name="HTTPServer">The sender of this event.</param>
    /// <param name="Timestamp">The timestamp of the TCP server stopped event.</param>
    /// <param name="EventTrackingId">An unique event tracking identification for correlating this request with other events.</param>
    /// <param name="Message">An optional message.</param>
    public delegate Task HTTPServerStoppedDelegate(DateTimeOffset    Timestamp,
                                                   AHTTPServer       HTTPServer,
                                                   EventTracking_Id  EventTrackingId,
                                                   String?           Message   = null);


    /// <summary>
    /// A simple HTTP test server that listens for incoming TCP connections and processes HTTP requests, supporting pipelining.
    /// </summary>
    public abstract class AHTTPServer : ATCPServer//, IHTTPServer
    {

        #region Data

        public const Int32  DefaultBufferSize                  =     32768;
        public const UInt32 DefaultMaxHTTPHeaderSize           = 32 * 1024;
        public const UInt32 DefaultMaxHTTPHeaderLineLength     =  8 * 1024;
        public const UInt32 DefaultMaxHTTPRequestTargetLength  =  8 * 1024;
        public const UInt32 DefaultMaxHTTPHeaderCount          =       100;
        public const UInt32 DefaultMaxHTTPChunkSizeLineLength  = ChunkedTransferEncodingStream.DefaultMaxChunkSizeLineLength;
        public const UInt32 DefaultMaxHTTPChunkTrailerLineLength = ChunkedTransferEncodingStream.DefaultMaxTrailerLineLength;
        public const UInt32 DefaultMaxHTTPChunkTrailerCount      = ChunkedTransferEncodingStream.DefaultMaxTrailerCount;
        public const UInt32 DefaultMaxHTTPChunkTrailerSize       = ChunkedTransferEncodingStream.DefaultMaxTrailerSize;
        public const UInt32 DefaultMaxHTTPChunkMetadataSize      = ChunkedTransferEncodingStream.DefaultMaxChunkMetadataSize;
        protected readonly ILogger<AHTTPServer> httpLogger;

        private static readonly Byte[] endOfHTTPHeaderDelimiter         = Encoding.UTF8.GetBytes("\r\n\r\n");
        const                   Byte   endOfHTTPHeaderDelimiterLength   = 4;
        private static readonly Byte[] endOfHTTPLineDelimiter           = Encoding.ASCII.GetBytes("\r\n");

        private static readonly Byte[] continueResponse                 = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");

        /// <summary>
        /// The default HTTP server name.
        /// </summary>
        public  const  String  DefaultHTTPServerName    = "GraphDefined Hermod HTTP Server v2.0";

        ///// <summary>
        ///// The default HTTP service name.
        ///// </summary>
        //public  const  String  DefaultHTTPServiceName   = "GraphDefined Hermod HTTP Service v2.0";

        #endregion

        #region Properties

        /// <summary>
        /// The buffer size for the TCP stream.
        /// </summary>
        public UInt32  BufferSize        { get; }

        /// <summary>
        /// The HTTP server name.
        /// </summary>
        public String  HTTPServerName    { get; } = DefaultHTTPServerName;

        /// <summary>
        /// The maximum request-body size accepted by this HTTP server.
        /// </summary>
        public UInt64  MaxHTTPBodySize   { get; }

        public UInt32  MaxHTTPHeaderSize          { get; }
        public UInt32  MaxHTTPHeaderLineLength    { get; }
        public UInt32  MaxHTTPRequestTargetLength { get; }
        public UInt32  MaxHTTPHeaderCount         { get; }
        public UInt32  MaxHTTPChunkSizeLineLength    { get; }
        public UInt32  MaxHTTPChunkTrailerLineLength { get; }
        public UInt32  MaxHTTPChunkTrailerCount      { get; }
        public UInt32  MaxHTTPChunkTrailerSize       { get; }
        public UInt32  MaxHTTPChunkMetadataSize      { get; }

        /// <summary>
        /// The maximum time allowed to receive one complete HTTP request header.
        /// </summary>
        public TimeSpan HeaderReadTimeout { get; }

        /// <summary>
        /// The maximum time allowed to receive one complete HTTP request body.
        /// </summary>
        public TimeSpan BodyReadTimeout   { get; }

        #endregion

        #region Events

        /// <summary>
        /// An event fired whenever the HTTP server started.
        /// </summary>
        public event HTTPServerStartedDelegate?  OnHTTPServerStarted;

        /// <summary>
        /// An event fired whenever the HTTP server stopped.
        /// </summary>
        public event HTTPServerStoppedDelegate?  OnHTTPServerStopped;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new abstract HTTPTestServer that listens on the loopback address and the given TCP port.
        /// </summary>
        /// <param name="IPAddress">The IP address to listen on. If null, the loopback address will be used.</param>
        /// <param name="TCPPort">The TCP port to listen on. If 0, a random TCP port will be assigned.</param>
        /// <param name="HTTPServerName">An optional HTTP server name. If null or empty, the default HTTP server name will be used.</param>
        /// <param name="BufferSize">An optional buffer size for the TCP stream. If null, the default buffer size will be used.</param>
        /// <param name="ReceiveTimeout">An optional receive timeout for the TCP stream. If null, the default receive timeout will be used.</param>
        /// <param name="SendTimeout">An optional send timeout for the TCP stream. If null, the default send timeout will be used.</param>
        /// <param name="LoggingHandler">An optional logging handler that will be called for each log message.</param>
        /// 
        /// <param name="DisableMaintenanceTasks">Disable all maintenance tasks.</param>
        /// <param name="MaintenanceInitialDelay">The initial delay of the maintenance tasks.</param>
        /// <param name="MaintenanceEvery">The maintenance interval.</param>
        /// 
        /// <param name="DisableWardenTasks">Disable all warden tasks.</param>
        /// <param name="WardenInitialDelay">The initial delay of the warden tasks.</param>
        /// <param name="WardenCheckEvery">The warden interval.</param>
        /// 
        /// <param name="ServerCertificateSelector"></param>
        /// <param name="ClientCertificateValidator"></param>
        /// <param name="LocalCertificateSelector"></param>
        /// <param name="AllowedTLSProtocols"></param>
        /// <param name="ClientCertificateRequired"></param>
        /// <param name="CheckCertificateRevocation"></param>
        /// 
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information. If null, the default connection identification will be used.</param>
        /// <param name="MaxClientConnections">An optional maximum number of concurrent TCP client connections. If null, the default maximum number of concurrent TCP client connections will be used.</param>
        /// <param name="DNSClient"></param>
        /// 
        /// <param name="DisableMaintenanceTasks">Disable all maintenance tasks.</param>
        /// <param name="MaintenanceInitialDelay">The initial delay of the maintenance tasks.</param>
        /// <param name="MaintenanceEvery">The maintenance interval.</param>
        /// 
        /// <param name="DisableWardenTasks">Disable all warden tasks.</param>
        /// <param name="WardenInitialDelay">The initial delay of the warden tasks.</param>
        /// <param name="WardenCheckEvery">The warden interval.</param>
        public AHTTPServer(IIPAddress?                                               IPAddress                       = null,
                           IPPort?                                                   TCPPort                         = null,
                           String?                                                   HTTPServerName                  = null,
                           I18NString?                                               Description                     = null,

                           UInt32?                                                   BufferSize                      = null,
                           TimeSpan?                                                 ReceiveTimeout                  = null,
                           TimeSpan?                                                 SendTimeout                     = null,
                           TCPEchoLoggingDelegate?                                   LoggingHandler                  = null,

                           ServerCertificateSelectorDelegate?                        ServerCertificateSelector       = null,
                           RemoteTLSClientCertificateValidationHandler<ITCPServer>?  ClientCertificateValidator      = null,
                           LocalCertificateSelectionHandler?                         LocalCertificateSelector        = null,
                           SslProtocols?                                             AllowedTLSProtocols             = null,
                           Boolean?                                                  ClientCertificateRequired       = null,
                           Boolean?                                                  CheckCertificateRevocation      = null,

                           ConnectionIdBuilder?                                      ConnectionIdBuilder             = null,
                           UInt32?                                                   MaxClientConnections            = null,
                           IDNSClient?                                               DNSClient                       = null,

                           Boolean?                                                  DisableMaintenanceTasks         = false,
                           TimeSpan?                                                 MaintenanceInitialDelay         = null,
                           TimeSpan?                                                 MaintenanceEvery                = null,

                           Boolean?                                                  DisableWardenTasks              = false,
                           TimeSpan?                                                 WardenInitialDelay              = null,
                           TimeSpan?                                                 WardenCheckEvery                = null,

                           ILoggerFactory?                                           LoggerFactory                   = null,
                           Boolean?                                                  AutoStart                       = false,
                           UInt64?                                                   MaxHTTPBodySize                 = null,
                           UInt32?                                                   MaxHTTPHeaderSize               = null,
                           UInt32?                                                   MaxHTTPHeaderLineLength         = null,
                           UInt32?                                                   MaxHTTPRequestTargetLength      = null,
                           UInt32?                                                   MaxHTTPHeaderCount              = null,
                           UInt32?                                                   MaxHTTPChunkSizeLineLength      = null,
                           UInt32?                                                   MaxHTTPChunkTrailerLineLength   = null,
                           UInt32?                                                   MaxHTTPChunkTrailerCount        = null,
                           UInt32?                                                   MaxHTTPChunkTrailerSize         = null,
                           UInt32?                                                   MaxHTTPChunkMetadataSize        = null,
                           TimeSpan?                                                 HeaderReadTimeout               = null,
                           TimeSpan?                                                 BodyReadTimeout                 = null)

            : base(IPAddress,
                   TCPPort,
                   Description,
                   ReceiveTimeout,
                   SendTimeout,
                   LoggingHandler,

                   ServerCertificateSelector,
                   ClientCertificateValidator,
                   LocalCertificateSelector,
                   AllowedTLSProtocols,
                   ClientCertificateRequired,
                   CheckCertificateRevocation,

                   ConnectionIdBuilder,
                   MaxClientConnections,
                   DNSClient,

                   DisableMaintenanceTasks,
                   MaintenanceInitialDelay,
                   MaintenanceEvery,

                   DisableWardenTasks,
                   WardenInitialDelay,
                   WardenCheckEvery,

                   LoggerFactory,
                   AutoStart: false)

        {

            this.HTTPServerName  = HTTPServerName.IsNullOrEmpty()
                                       ? DefaultHTTPServerName
                                       : HTTPServerName.Trim();

            this.httpLogger      = (LoggerFactory ?? NullLoggerFactory.Instance).CreateLogger<AHTTPServer>();

            this.BufferSize      = BufferSize.HasValue
                                       ? BufferSize.Value > Int32.MaxValue
                                             ? throw new ArgumentOutOfRangeException(nameof(BufferSize), "The buffer size must not exceed Int32.MaxValue!")
                                             : (UInt32) BufferSize.Value
                                       : DefaultBufferSize;

            this.MaxHTTPBodySize = MaxHTTPBodySize ?? AHTTPPDU.DefaultMaxHTTPBodySize;
            if (this.MaxHTTPBodySize == 0 || this.MaxHTTPBodySize > Int32.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(MaxHTTPBodySize), "The maximum HTTP body size must be between 1 and Int32.MaxValue bytes.");

            this.MaxHTTPHeaderSize = MaxHTTPHeaderSize ?? Math.Min(DefaultMaxHTTPHeaderSize, this.BufferSize);
            if (this.MaxHTTPHeaderSize == 0 || this.MaxHTTPHeaderSize > this.BufferSize)
                throw new ArgumentOutOfRangeException(nameof(MaxHTTPHeaderSize), "The maximum HTTP header size must be between 1 and BufferSize bytes.");

            this.MaxHTTPHeaderLineLength = MaxHTTPHeaderLineLength ?? Math.Min(DefaultMaxHTTPHeaderLineLength, this.MaxHTTPHeaderSize);
            if (this.MaxHTTPHeaderLineLength == 0 || this.MaxHTTPHeaderLineLength > this.MaxHTTPHeaderSize)
                throw new ArgumentOutOfRangeException(nameof(MaxHTTPHeaderLineLength), "The maximum HTTP header line length must not exceed MaxHTTPHeaderSize.");

            this.MaxHTTPRequestTargetLength = MaxHTTPRequestTargetLength ?? Math.Min(DefaultMaxHTTPRequestTargetLength, this.MaxHTTPHeaderLineLength);
            if (this.MaxHTTPRequestTargetLength == 0 || this.MaxHTTPRequestTargetLength > this.MaxHTTPHeaderLineLength)
                throw new ArgumentOutOfRangeException(nameof(MaxHTTPRequestTargetLength), "The maximum HTTP request-target length must not exceed MaxHTTPHeaderLineLength.");

            this.MaxHTTPHeaderCount = MaxHTTPHeaderCount ?? DefaultMaxHTTPHeaderCount;
            if (this.MaxHTTPHeaderCount == 0)
                throw new ArgumentOutOfRangeException(nameof(MaxHTTPHeaderCount), "The maximum HTTP header count must be greater than zero.");

            this.MaxHTTPChunkSizeLineLength = MaxHTTPChunkSizeLineLength ?? DefaultMaxHTTPChunkSizeLineLength;
            if (this.MaxHTTPChunkSizeLineLength == 0)
                throw new ArgumentOutOfRangeException(nameof(MaxHTTPChunkSizeLineLength));

            this.MaxHTTPChunkTrailerLineLength = MaxHTTPChunkTrailerLineLength ?? DefaultMaxHTTPChunkTrailerLineLength;
            if (this.MaxHTTPChunkTrailerLineLength == 0)
                throw new ArgumentOutOfRangeException(nameof(MaxHTTPChunkTrailerLineLength));

            this.MaxHTTPChunkTrailerCount = MaxHTTPChunkTrailerCount ?? DefaultMaxHTTPChunkTrailerCount;
            if (this.MaxHTTPChunkTrailerCount == 0)
                throw new ArgumentOutOfRangeException(nameof(MaxHTTPChunkTrailerCount));

            this.MaxHTTPChunkTrailerSize = MaxHTTPChunkTrailerSize ?? DefaultMaxHTTPChunkTrailerSize;
            if (this.MaxHTTPChunkTrailerSize == 0)
                throw new ArgumentOutOfRangeException(nameof(MaxHTTPChunkTrailerSize));

            this.MaxHTTPChunkMetadataSize = MaxHTTPChunkMetadataSize ?? DefaultMaxHTTPChunkMetadataSize;
            if (this.MaxHTTPChunkMetadataSize == 0)
                throw new ArgumentOutOfRangeException(nameof(MaxHTTPChunkMetadataSize));

            this.HeaderReadTimeout = HeaderReadTimeout ?? this.ReceiveTimeout;
            this.BodyReadTimeout   = BodyReadTimeout   ?? this.ReceiveTimeout;

            ValidateReadTimeout(nameof(HeaderReadTimeout), this.HeaderReadTimeout);
            ValidateReadTimeout(nameof(BodyReadTimeout),   this.BodyReadTimeout);

            #region Subscribe to TCP server events

            base.OnTCPServerStarted += async (sender, timestamp, eventTrackingId, message) => {
                if (OnHTTPServerStarted is not null)
                    await OnHTTPServerStarted(timestamp, this, eventTrackingId, message);
            };

            base.OnTCPServerStopped += async (sender, timestamp, eventTrackingId, message) => {
                if (OnHTTPServerStopped is not null)
                    await OnHTTPServerStopped(timestamp, this, eventTrackingId, message);
            };

            #endregion

            if (AutoStart ?? false)
                Start().GetAwaiter().GetResult();

        }

        #endregion


        #region HandleConnection(TCPConnection, CancellationToken = default)

        protected override async Task HandleConnection(TCPConnection      TCPConnection,
                                                       CancellationToken  CancellationToken   = default)
        {

            var connection = new HTTPConnection(TCPConnection);

            if (activeClients.TryRemove(TCPConnection, out var task))
                activeClients.TryAdd   (connection,    task);

            CancellationTokenSource? bodyReadTimeoutCancellation = null;

            try
            {

                #region Data

                Int32           bufferSize     = (Int32) BufferSize;
                var             networkstream  = connection.TCPClient.GetStream();
                await using var stream         = (connection.SSLStream as Stream) ?? networkstream
                                                     ?? throw new InvalidOperationException("Stream is not a NetworkStream.");
                using var       bufferOwner    = MemoryPool<Byte>.Shared.Rent(bufferSize * 2);
                var             buffer         = bufferOwner.Memory;
                var             dataLength     = 0;

                var             localSocket    = IPSocket.FromIPEndPoint((networkstream.Socket.LocalEndPoint  as IPEndPoint)!);
                var             remoteSocket   = IPSocket.FromIPEndPoint((networkstream.Socket.RemoteEndPoint as IPEndPoint)!);
                var             httpSource     = new HTTPSource(remoteSocket);

                #endregion

                while (true)
                {

                    using var headerReadTimeoutCancellation = CreateReadTimeoutCancellationSource(
                                                                  CancellationToken,
                                                                  HeaderReadTimeout
                                                              );

                    #region Read data if no delimiter found yet

                    if (dataLength < endOfHTTPHeaderDelimiterLength ||
                        buffer.Span[0..dataLength].IndexOf(endOfHTTPHeaderDelimiter.AsSpan()) < 0)
                    {

                        if (dataLength >= MaxHTTPHeaderSize)
                        {
                            await SendResponse(
                                      stream,
                                      CreateRequestParsingErrorResponse(
                                          httpSource,
                                          localSocket,
                                          remoteSocket,
                                          HTTPStatusCode.RequestHeaderFieldsTooLarge,
                                          "The HTTP request header section is too large.",
                                          CancellationToken
                                      ),
                                      CancellationToken
                                  );

                            break;
                        }

                        // Will read data, or wait until read timeout...
                        var bytesRead = await stream.ReadAsync(
                                             buffer.Slice(dataLength, bufferSize),
                                             headerReadTimeoutCancellation.Token
                                         );
                        if (bytesRead == 0)
                            break;

                        dataLength += bytesRead;

                    }

                    #endregion

                    #region Search for End-of-HTTPHeader

                    var endOfHTTPHeaderIndex = buffer.Span[0..dataLength].IndexOf(endOfHTTPHeaderDelimiter.AsSpan());
                    if (endOfHTTPHeaderIndex < 0)
                        continue;

                    if (!TryValidateHeaderLimits(
                             buffer.Span[..endOfHTTPHeaderIndex],
                             out var headerLimitStatusCode,
                             out var headerLimitDescription
                         ))
                    {
                        await SendResponse(
                                  stream,
                                  CreateRequestParsingErrorResponse(
                                      httpSource,
                                      localSocket,
                                      remoteSocket,
                                      headerLimitStatusCode,
                                      headerLimitDescription,
                                      CancellationToken
                                  ),
                                  CancellationToken
                              );

                        break;
                    }

                    #endregion

                    #region Parse HTTP Request

                    if (HTTPRequest.TryParse(

                            Timestamp.Now,
                            httpSource,
                            localSocket,
                            remoteSocket,

                            Encoding.Latin1.GetString(buffer[..endOfHTTPHeaderIndex].Span),

                            out var request,
                            out var httpParsingFailedResponse,

                            HTTPBody:                   null,
                            HTTPBodyStream:             null,
                            HTTPServer:                 this,
                            ServerCertificate:          TCPConnection.ServerCertificate,
                            ClientCertificate:          TCPConnection.ClientCertificate,

                            //HTTPBodyReceiveBufferSize:  null,
                            EventTrackingId:            EventTracking_Id.New,
                            CancellationToken:          CancellationToken

                        ))
                    {

                        connection.IsHTTPKeepAlive        = request.IsKeepAlive;
                        connection.IncrementKeepAliveMessageCount();
                        request.   KeepAliveMessageCount  = connection.KeepAliveMessageCount;

                        if (request.HTTPMethod    == HTTPMethod.OPTIONS &&
                            request.RequestTarget == "*")
                        {
                            await SendResponse(
                                      stream,
                                      CreateServerOptionsResponse(request),
                                      CancellationToken
                                  );

                            break;
                        }

                        if (request.ContentLength is UInt64 contentLength &&
                            contentLength > MaxHTTPBodySize)
                        {
                            await SendResponse(
                                      stream,
                                      CreateRequestBodyTooLargeResponse(request),
                                      CancellationToken
                                  );

                            break;
                        }

                        // Chunked transfer coding was introduced with HTTP/1.1.
                        // Accepting it on an HTTP/1.0 request would make message
                        // framing ambiguous for peers that only implement 1.0.
                        if (request.ProtocolVersion == HTTPVersion.HTTP_1_0 &&
                            request.TransferEncoding.IsNotNullOrEmpty())
                        {
                            await SendResponse(
                                      stream,
                                      new HTTPResponse.Builder(request) {
                                          HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                          Server          = HTTPServerName,
                                          Date            = Timestamp.Now,
                                          Connection      = ConnectionType.Close,
                                          Content         = "Transfer-Encoding is not supported for HTTP/1.0 requests.".ToUTF8Bytes()
                                      }.AsImmutable,
                                      CancellationToken
                                  );

                            break;
                        }

                        #region Shift remaining data

                        var remainingStart  = endOfHTTPHeaderIndex + endOfHTTPHeaderDelimiterLength;
                        var remainingLength = dataLength           - remainingStart;
                        buffer.Slice(remainingStart, remainingLength).CopyTo(buffer[..]);
                        dataLength = remainingLength;

                        #endregion

                        #region Validate Expect header and send 100 Continue before reading the body

                        var expects100Continue = false;

                        if (!TryGet100ContinueExpectation(request, out expects100Continue))
                        {

                            await SendResponse(
                                      stream,
                                      CreateExpectationFailedResponse(request),
                                      CancellationToken
                                  );

                            break;

                        }

                        // RFC 9110 allows omitting 100 Continue when body bytes have already arrived.
                        // HTTP/1.0 does not support interim 100 Continue responses.
                        if (request.ProtocolVersion == HTTPVersion.HTTP_1_1 &&
                            expects100Continue &&
                            dataLength == 0     &&
                            (request.IsChunkedTransferEncoding ||
                             request.ContentLength is UInt64 expectedContentLength && expectedContentLength > 0) &&
                            !await SendContinueResponse(stream, CancellationToken))
                        {
                            break;
                        }

                        #endregion

                        #region Setup HTTP body stream

                        Stream? bodyDataStream  = null;
                        Stream? bodyStream      = null;
                        ChunkedTransferEncodingStream? chunkedTransferEncodingStream = null;

                        var prefix = buffer[..dataLength];
                        if (request.IsChunkedTransferEncoding || request.ContentLength.HasValue)
                        {

                            bodyDataStream = new PrefixStream(
                                                 prefix,
                                                 stream,
                                                 LeaveInnerStreamOpen: true
                                             );

                            if (request.IsChunkedTransferEncoding)
                                bodyStream = chunkedTransferEncodingStream = new ChunkedTransferEncodingStream(
                                                                           bodyDataStream,
                                                                           LeaveInnerStreamOpen:    true,
                                                                           MaxChunkSizeLineLength:  MaxHTTPChunkSizeLineLength,
                                                                           MaxTrailerLineLength:    MaxHTTPChunkTrailerLineLength,
                                                                           MaxTrailerCount:         MaxHTTPChunkTrailerCount,
                                                                           MaxTrailerSize:          MaxHTTPChunkTrailerSize,
                                                                           MaxChunkMetadataSize:    MaxHTTPChunkMetadataSize
                                                                       );

                            else if (request.ContentLength.HasValue && request.ContentLength.Value > 0)
                                bodyStream = new LengthLimitedStream(
                                                 bodyDataStream,
                                                 request.ContentLength.Value,
                                                 LeaveInnerStreamOpen: true
                                              );

                            if (request.IsChunkedTransferEncoding)
                                bodyStream = new MaximumLengthStream(
                                                 bodyStream,
                                                 MaxHTTPBodySize,
                                                 LeaveInnerStreamOpen: false
                                             );

                            if (bodyStream is not null)
                            {
                                bodyReadTimeoutCancellation = CreateReadTimeoutCancellationSource(
                                                                  CancellationToken,
                                                                  BodyReadTimeout
                                                              );

                                bodyStream = new DeadlineStream(
                                                 bodyStream,
                                                 bodyReadTimeoutCancellation.Token,
                                                 BodyReadTimeout,
                                                 LeaveInnerStreamOpen: false
                                             );
                            }

                        }

                        request.HTTPBodyStream = bodyStream;
                        request.ChunkedTransferEncodingStream = chunkedTransferEncodingStream;

                        #endregion


                        var httpResponse = await ProcessHTTPRequest(
                                                     request,
                                                     stream,
                                                     CancellationToken
                                                 );

                        #region When the upper layer did not consume all of the body stream, we will discard the remaining data to support pipelining

                        if (bodyStream is not null)
                        {

                            var discardBuffer = new Byte[4096];
                            int read;

                            try
                            {
                                while ((read = await bodyStream.ReadAsync(discardBuffer, CancellationToken)) > 0)
                                { }
                            }
                            catch (HTTPBodyTooLargeException)
                            {
                                bodyStream.Dispose();
                                httpResponse = CreateRequestBodyTooLargeResponse(request);
                            }
                            catch (HTTPIncompleteBodyException)
                            {
                                bodyStream.Dispose();
                                httpResponse = CreateIncompleteRequestBodyResponse(request);
                            }
                            catch (HTTPChunkMetadataTooLargeException)
                            {
                                bodyStream.Dispose();
                                httpResponse = CreateInvalidChunkMetadataResponse(request);
                            }
                            catch (HTTPInvalidChunkException)
                            {
                                bodyStream.Dispose();
                                httpResponse = CreateInvalidChunkResponse(request);
                            }
                            catch (HTTPReadTimeoutException)
                            {
                                bodyStream.Dispose();
                                httpResponse = CreateRequestTimeoutResponse(request);
                            }

                        }

                        #endregion

                        #region Get prefix consumed and shift buffer

                        UInt64 prefixConsumed = 0;
                        if (bodyDataStream is not null)
                        {

                            if (bodyDataStream is IPrefixInfo pi)
                                prefixConsumed = pi.PrefixConsumed;

                            bodyStream?.   Dispose();
                            bodyDataStream.Dispose();

                            bodyReadTimeoutCancellation?.Dispose();
                            bodyReadTimeoutCancellation = null;

                        }

                        if (prefixConsumed < (UInt64) dataLength)
                        {
                            buffer[(int) prefixConsumed..dataLength].CopyTo(buffer[..]);
                            dataLength -= (int) prefixConsumed;
                        }
                        else
                            dataLength = 0;

                        #endregion


                        if (request.ProtocolVersion == HTTPVersion.HTTP_1_0 &&
                            httpResponse.IsChunkedTransferEncoding &&
                            !httpResponse.AutomaticallyChunkContent)
                        {
                            httpResponse = new HTTPResponse.Builder(request) {
                                               HTTPStatusCode  = HTTPStatusCode.InternalServerError,
                                               Server          = HTTPServerName,
                                               Date            = Timestamp.Now,
                                               Connection      = ConnectionType.Close,
                                               ContentType     = HTTPContentType.Text.PLAIN,
                                               Content         = "Manually streamed chunked responses are not supported for HTTP/1.0 clients.".ToUTF8Bytes()
                                           }.AsImmutable;
                        }

                        var hasLiveChunkWorker = !HasNoResponseBody(httpResponse) &&
                                                 httpResponse.HTTPBodyStream is ChunkedTransferEncodingStream;
                        var hasSSEWorker       = !HasNoResponseBody(httpResponse) &&
                                                 httpResponse.ContentType == HTTPContentType.Text.EVENTSTREAM;

                        await SendResponse(
                                  stream,
                                  httpResponse,
                                  CancellationToken,
                                  KeepStreamOpen: hasLiveChunkWorker || hasSSEWorker
                              );

                        #region Run the worker for live chunked HTTP responses

                        if (hasLiveChunkWorker &&
                            httpResponse.HTTPBodyStream is ChunkedTransferEncodingStream chunkedStream)
                        {
                            try
                            {
                                await httpResponse.ChunkWorker(
                                          httpResponse,
                                          chunkedStream
                                      );
                            }
                            catch (Exception e)
                            {
                                httpLogger.LogError(e, "HTTP server response worker failed.");
                            }
                        }

                        #endregion

                        #region Run the worker for HTTP SSE responses

                        if (hasSSEWorker)
                        {

                            httpResponse.HTTPBodyStream = stream;

                            try
                            {
                                await httpResponse.HTTPSSEWorker(
                                          httpResponse,
                                          new StreamWriter(httpResponse.HTTPBodyStream)
                                      );
                            }
                            catch (Exception e)
                            {
                                httpLogger.LogError(e, "HTTP server SSE response worker failed.");
                            }

                        }

                        #endregion

                        if (!httpResponse.IsKeepAlive)
                            break;

                    }

                    #endregion

                    #region Parsing failed, send error response

                    else
                    {

                        await SendResponse(
                                  stream,
                                  httpParsingFailedResponse,
                                  CancellationToken
                              );

                        break;

                    }

                    #endregion

                }

            }
            catch (OperationCanceledException) when (!CancellationToken.IsCancellationRequested)
            {
                // The header deadline expired before a complete request header arrived.
                httpLogger.LogDebug("HTTP header read deadline exceeded.");
            }
            catch (OperationCanceledException)
            {
                // The operation was cancelled, e.g. by a timeout.
                httpLogger.LogDebug("HTTP connection handling was cancelled.");
            }
            catch (ObjectDisposedException)
            {
                // The connection was disposed, e.g. by the client closing the connection.
                //DebugX.LogT("ObjectDisposedException in HTTPTestServer.HandleConnection(...)!");
            }
            catch (IOException)
            {
                // An I/O error occurred, e.g. the connection was closed by the client.
                //DebugX.LogException(ie, "IOException in HTTPTestServer.HandleConnection(...)");
            }
            catch (Exception e)
            {

                if (e.Message == "HTTP version not supported!")


                httpLogger.LogError(e, "Exception while handling HTTP connection.");

            }
            finally
            {

                bodyReadTimeoutCancellation?.Dispose();
                activeClients.TryRemove(connection, out _);

                try
                {
                    connection.IsClosed = true;
                    //await Connection.DisposeAsync();
                    connection.Dispose();
                }
                catch (Exception e)
                {
                    httpLogger.LogError(e, "Exception while disposing HTTP connection.");
                }
            }

        }

        #endregion


        #region (protected) ProcessHTTPRequest(Request, Stream, CancellationToken = default)

        private Boolean TryValidateHeaderLimits(ReadOnlySpan<Byte>  Header,
                                                out HTTPStatusCode  ErrorStatusCode,
                                                out String          ErrorDescription)
        {

            ErrorStatusCode  = HTTPStatusCode.BadRequest;
            ErrorDescription = "Invalid HTTP request header.";

            if (Header.Length > MaxHTTPHeaderSize)
            {
                ErrorStatusCode  = HTTPStatusCode.RequestHeaderFieldsTooLarge;
                ErrorDescription = "The HTTP request header section is too large.";
                return false;
            }

            var firstLineEnd = Header.IndexOf(endOfHTTPLineDelimiter.AsSpan());
            var firstLine    = firstLineEnd >= 0
                                   ? Header[..firstLineEnd]
                                   : Header;
            var firstSpace   = firstLine.IndexOf((Byte) ' ');
            var secondSpace  = firstSpace >= 0
                                   ? firstLine[(firstSpace + 1)..].IndexOf((Byte) ' ')
                                   : -1;

            if (secondSpace >= 0)
            {
                secondSpace += firstSpace + 1;

                if (secondSpace - firstSpace - 1 > MaxHTTPRequestTargetLength)
                {
                    ErrorStatusCode  = HTTPStatusCode.RequestURITooLong;
                    ErrorDescription = "The HTTP request-target is too long.";
                    return false;
                }
            }

            var headerCount = 0U;
            var offset      = firstLineEnd >= 0
                                  ? firstLineEnd + endOfHTTPLineDelimiter.Length
                                  : Header.Length;

            while (offset < Header.Length)
            {
                var remaining   = Header[offset..];
                var relativeEnd = remaining.IndexOf(endOfHTTPLineDelimiter.AsSpan());
                var lineLength  = relativeEnd >= 0
                                      ? relativeEnd
                                      : remaining.Length;

                if (lineLength > MaxHTTPHeaderLineLength)
                {
                    ErrorStatusCode  = HTTPStatusCode.RequestHeaderFieldsTooLarge;
                    ErrorDescription = "An HTTP request header field line is too large.";
                    return false;
                }

                headerCount++;
                if (headerCount > MaxHTTPHeaderCount)
                {
                    ErrorStatusCode  = HTTPStatusCode.RequestHeaderFieldsTooLarge;
                    ErrorDescription = "The HTTP request contains too many header fields.";
                    return false;
                }

                if (relativeEnd < 0)
                    break;

                offset += relativeEnd + endOfHTTPLineDelimiter.Length;
            }

            return true;

        }

        private HTTPResponse CreateRequestParsingErrorResponse(HTTPSource         HTTPSource,
                                                               IPSocket           LocalSocket,
                                                               IPSocket           RemoteSocket,
                                                               HTTPStatusCode     HTTPStatusCode,
                                                               String             Description,
                                                               CancellationToken  CancellationToken)
            => new HTTPResponse.Builder(
                   Timestamp.Now,
                   EventTracking_Id.New,
                   TimeSpan.Zero,
                   HTTPSource,
                   LocalSocket,
                   RemoteSocket,
                   ConnectionType.Close,
                   HTTPStatusCode,
                   Description,
                   CancellationToken
               ) {
                   Server = HTTPServerName
               }.AsImmutable;

        private static void ValidateReadTimeout(String Name,
                                                 TimeSpan Value)
        {
            if (Value != System.Threading.Timeout.InfiniteTimeSpan &&
                (Value <= TimeSpan.Zero || Value.TotalMilliseconds > Int32.MaxValue))
            {
                throw new ArgumentOutOfRangeException(
                    Name,
                    "The HTTP read timeout must be positive, infinite, or fit into the socket timeout range."
                );
            }
        }

        private static CancellationTokenSource CreateReadTimeoutCancellationSource(
                                         CancellationToken CancellationToken,
                                         TimeSpan          Timeout)
        {
            var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);

            if (Timeout != System.Threading.Timeout.InfiniteTimeSpan)
                cancellationSource.CancelAfter(Timeout);

            return cancellationSource;
        }

        protected HTTPResponse CreateRequestBodyTooLargeResponse(HTTPRequest Request)
            => new HTTPResponse.Builder(Request) {
                   HTTPStatusCode = HTTPStatusCode.RequestEntityTooLarge,
                   Server         = HTTPServerName,
                   Date           = Timestamp.Now,
                   Connection     = ConnectionType.Close,
                   ContentType    = HTTPContentType.Application.JSON_UTF8,
                   Content        = JSONObject.Create(
                                        new JProperty("description", "The request body is too large."),
                                        new JProperty("maximumBytes", MaxHTTPBodySize)
                                     ).ToUTF8Bytes()
                  }.AsImmutable;

        protected HTTPResponse CreateIncompleteRequestBodyResponse(HTTPRequest Request)
            => new HTTPResponse.Builder(Request) {
                   HTTPStatusCode = HTTPStatusCode.BadRequest,
                   Server         = HTTPServerName,
                   Date           = Timestamp.Now,
                   Connection     = ConnectionType.Close,
                   ContentType    = HTTPContentType.Application.JSON_UTF8,
                   Content        = JSONObject.Create(
                                        new JProperty("description", "The request body ended before its declared Content-Length was received.")
                                    ).ToUTF8Bytes()
               }.AsImmutable;

        private HTTPResponse CreateServerOptionsResponse(HTTPRequest Request)
            => new HTTPResponse.Builder(Request) {
                   HTTPStatusCode = HTTPStatusCode.NoContent,
                   Server         = HTTPServerName,
                   Date           = Timestamp.Now,
                   Connection     = ConnectionType.Close
               }.AsImmutable;

        protected HTTPResponse CreateInvalidChunkMetadataResponse(HTTPRequest Request)
            => new HTTPResponse.Builder(Request) {
                   HTTPStatusCode = HTTPStatusCode.BadRequest,
                   Server         = HTTPServerName,
                   Date           = Timestamp.Now,
                   Connection     = ConnectionType.Close,
                   ContentType    = HTTPContentType.Application.JSON_UTF8,
                   Content        = JSONObject.Create(
                                        new JProperty("description", "The chunked request metadata exceeds the configured limits.")
                                    ).ToUTF8Bytes()
               }.AsImmutable;

        protected HTTPResponse CreateInvalidChunkResponse(HTTPRequest Request)
            => new HTTPResponse.Builder(Request) {
                   HTTPStatusCode = HTTPStatusCode.BadRequest,
                   Server         = HTTPServerName,
                   Date           = Timestamp.Now,
                   Connection     = ConnectionType.Close,
                   ContentType    = HTTPContentType.Application.JSON_UTF8,
                   Content        = JSONObject.Create(
                                        new JProperty("description", "The request contains invalid chunked transfer-coding syntax.")
                                    ).ToUTF8Bytes()
               }.AsImmutable;

        private HTTPResponse CreateExpectationFailedResponse(HTTPRequest Request)
            => new HTTPResponse.Builder(Request) {
                   HTTPStatusCode = HTTPStatusCode.ExpectationFailed,
                   Server         = HTTPServerName,
                   Date           = Timestamp.Now,
                   Connection     = ConnectionType.Close,
                   ContentType    = HTTPContentType.Application.JSON_UTF8,
                   Content        = JSONObject.Create(
                                        new JProperty("description", "The request contains an unsupported Expect header field.")
                                    ).ToUTF8Bytes()
               }.AsImmutable;

        private HTTPResponse CreateRequestTimeoutResponse(HTTPRequest Request)
            => new HTTPResponse.Builder(Request) {
                   HTTPStatusCode = HTTPStatusCode.RequestTimeout,
                   Server         = HTTPServerName,
                   Date           = Timestamp.Now,
                   Connection     = ConnectionType.Close,
                   ContentType    = HTTPContentType.Application.JSON_UTF8,
                   Content        = JSONObject.Create(
                                        new JProperty("description", "The request body read timed out.")
                                    ).ToUTF8Bytes()
               }.AsImmutable;

        /// <summary>
        /// Process the given HTTP request.
        /// </summary>
        /// <param name="Request">The HTTP request to process.</param>
        /// <param name="Stream">The network stream for reading the HTTP body and sending the response.</param>
        /// <param name="CancellationToken">An optional cancellation token to cancel the processing of the HTTP request.</param>
        protected abstract Task<HTTPResponse> ProcessHTTPRequest(HTTPRequest        Request,
                                                                 Stream             Stream,
                                                                 CancellationToken  CancellationToken   = default);

        #endregion

        #region (private) HasNoResponseBody(Response)

        /// <summary>
        /// Whether the response semantics prohibit a message body.
        /// </summary>
        private static Boolean HasNoResponseBody(HTTPResponse Response)

            => Response.HTTPRequest?.HTTPMethod == HTTPMethod.HEAD ||
               Response.HTTPStatusCode.Code is >= 100 and < 200 or 204 or 205 or 304;

        #endregion


        #region (protected) SendResponse(Stream, Response, CancellationToken = default, KeepStreamOpen = false)

        protected async Task SendResponse(Stream             Stream,
                                          HTTPResponse       Response,
                                          CancellationToken  CancellationToken   = default,
                                          Boolean            KeepStreamOpen      = false)
        {

            try
            {

                var isKeepAlive    = Response.IsKeepAlive;
                var responseHeader = Response.RawHTTPHeader;
                var useHTTP10CloseDelimitedBody = Response.HTTPRequest?.ProtocolVersion == HTTPVersion.HTTP_1_0 &&
                                                   Response.AutomaticallyChunkContent &&
                                                   Response.IsChunkedTransferEncoding;

                if (useHTTP10CloseDelimitedBody)
                {
                    responseHeader = String.Join(
                                         Environment.NewLine,
                                         responseHeader.Split(Environment.NewLine).
                                                        Where(line => !line.StartsWith("Transfer-Encoding:", StringComparison.OrdinalIgnoreCase) &&
                                                                      !line.StartsWith("Trailer:",           StringComparison.OrdinalIgnoreCase))
                                     );
                }

                if (!isKeepAlive &&
                    Response.Connection != ConnectionType.Close)
                {

                    var headerLines    = responseHeader.Split(Environment.NewLine);
                    var hasConnection  = false;

                    for (var i = 0; i < headerLines.Length; i++)
                    {

                        if (headerLines[i].StartsWith("Connection:", StringComparison.OrdinalIgnoreCase))
                        {
                            headerLines[i] = "Connection: close";
                            hasConnection  = true;
                            break;
                        }

                    }

                    responseHeader = hasConnection
                                         ? String.Join(Environment.NewLine, headerLines)
                                         : String.Concat(responseHeader,
                                                         Environment.NewLine,
                                                         "Connection: close");

                }

                await Stream.WriteAsync(
                          (responseHeader + "\r\n\r\n").ToUTF8Bytes(),
                          CancellationToken
                      );

                var hasStaticResponseBody = !HasNoResponseBody(Response) &&
                                            (Response.ContentLength.HasValue ||
                                             Response.IsChunkedTransferEncoding);

                // A ChunkedTransferEncodingStream is the output stream used by a
                // live ChunkWorker below. Other response streams and byte arrays
                // already contain the complete, wire-framed response body and
                // must be copied before the connection can be closed.
                if (hasStaticResponseBody &&
                    Response.HTTPBodyStream is not ChunkedTransferEncodingStream)
                {
                    if (Response.AutomaticallyChunkContent &&
                        !useHTTP10CloseDelimitedBody &&
                        Response.IsChunkedTransferEncoding)
                    {
                        var chunkedStream = new ChunkedTransferEncodingStream(Stream, LeaveInnerStreamOpen: true);

                        if (Response.HTTPBodyStream is not null)
                        {
                            await Response.HTTPBodyStream.CopyToAsync(
                                      chunkedStream,
                                      CancellationToken
                                  );
                        }
                        else if (Response.HTTPBody is Byte[] responseBody && responseBody.Length > 0)
                        {
                            await chunkedStream.WriteAsync(responseBody, CancellationToken);
                        }

                        await chunkedStream.Finish(
                                  Response.TrailingHeaders.ToDictionary(trailer => trailer.Key, trailer => trailer.Value),
                                  CancellationToken
                              );
                    }
                    else if (Response.HTTPBodyStream is not null)
                    {
                        await Response.HTTPBodyStream.CopyToAsync(
                                  Stream,
                                  CancellationToken
                              );
                    }
                    else if (Response.HTTPBody is Byte[] responseBody && responseBody.Length > 0)
                    {
                        await Stream.WriteAsync(
                                  responseBody,
                                  CancellationToken
                              );
                    }
                }

                await Stream.FlushAsync(CancellationToken);

                if (!isKeepAlive &&
                    !KeepStreamOpen)
                    await Stream.DisposeAsync();

            }
            catch (Exception e)
            {
                httpLogger.LogError(e, "Exception while sending HTTP response.");
            }

        }

        #endregion


        #region (protected) SendContinueResponse(Stream, CancellationToken = default)

        /// <summary>
        /// Send an HTTP/1.1 100 Continue interim response.
        /// </summary>
        /// <param name="Stream">The network stream.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        /// <returns>True when the interim response was sent successfully; otherwise false.</returns>
        protected async Task<Boolean> SendContinueResponse(Stream             Stream,
                                                            CancellationToken  CancellationToken   = default)
        {

            try
            {

                await Stream.WriteAsync(continueResponse, CancellationToken);
                await Stream.FlushAsync(CancellationToken);

                return true;

            }
            catch (Exception e)
            {
                httpLogger.LogError(e, "Exception while sending HTTP 100 Continue response.");
                return false;
            }

        }

        #endregion


        #region TryGet100ContinueExpectation(Request, out Expects100Continue)

        /// <summary>
        /// Validate all Expect header field values and detect 100-continue.
        /// </summary>
        /// <param name="Request">The HTTP request.</param>
        /// <param name="Expects100Continue">Whether the request expects 100 Continue.</param>
        /// <returns>True when every expectation is supported; otherwise false.</returns>
        private static Boolean TryGet100ContinueExpectation(HTTPRequest  Request,
                                                             out Boolean  Expects100Continue)
        {

            Expects100Continue = false;

            if (!Request.TryGetHeaderField(HTTPRequestHeaderField.Expect.Name, out var expectHeader))
                return true;

            var expectationValues = expectHeader switch {
                                        String   expectationValue  => new[] { expectationValue },
                                        String[] multipleExpectationValues => multipleExpectationValues,
                                        _                           => new[] { expectHeader?.ToString() ?? String.Empty }
                                    };

            foreach (var expectationValue in expectationValues)
            {

                var expectations = expectationValue.Split(',');

                if (expectations.Length == 0)
                    return false;

                foreach (var expectation in expectations)
                {

                    if (!expectation.Trim().Equals("100-continue", StringComparison.OrdinalIgnoreCase))
                        return false;

                    Expects100Continue = true;

                }

            }

            return Expects100Continue;

        }

        #endregion


        #region (private) LogEvent (Logger, LogHandler, ...)

        private Task LogEvent<TDelegate>(TDelegate?                                         Logger,
                                         Func<TDelegate, Task>                              LogHandler,
                                         [CallerArgumentExpression(nameof(Logger))] String  EventName     = "",
                                         [CallerMemberName()]                       String  OICPCommand   = "")

            where TDelegate : Delegate

            => LogEvent(
                   nameof(HTTPTestServer),
                   Logger,
                   LogHandler,
                   EventName,
                   OICPCommand
               );

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{IPAddress}:{TCPPort} (BufferSize: {BufferSize}, ReceiveTimeout: {ReceiveTimeout}, SendTimeout: {SendTimeout})";

        #endregion

    }

}
