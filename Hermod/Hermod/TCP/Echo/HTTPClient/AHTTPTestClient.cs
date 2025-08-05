/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Text;
using System.Buffers;
using System.Diagnostics;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    public delegate HTTPRequest.Builder DefaultRequestBuilderDelegate();


    /// <summary>
    /// A simple TCP echo test client that can connect to a TCP echo server,
    /// </summary>
    public abstract class AHTTPTestClient : ATLSTestClient,
                                            IDisposable,
                                            IAsyncDisposable
    {

        #region Data

        protected Stream?                                                        httpStream;

        /// <summary>
        /// The TLS stream.
        /// </summary>
        protected SslStream?                                                     tlsStream;

        /// <summary>
        /// The remote TLS server certificate validation handler.
        /// </summary>
        protected RemoteTLSServerCertificateValidationHandler<AHTTPTestClient>?  RemoteCertificateValidationHandler;


        public const String                                                      DefaultHTTPUserAgent                   = "Hermod HTTP Test Client";

        #endregion

        #region Properties

        public String?                        HTTPUserAgent            { get; }

        public Boolean                        IsHTTPConnected          { get; private set; } = false;

        public DefaultRequestBuilderDelegate  DefaultRequestBuilder    { get;}

        #endregion

        #region Constructor(s)

        #region AHTTPTestClient(IPAddress, ...)

        protected AHTTPTestClient(IIPAddress                                                     IPAddress,
                                  IPPort?                                                        TCPPort                              = null,
                                  I18NString?                                                    Description                          = null,
                                  String?                                                        HTTPUserAgent                        = null,

                                  RemoteTLSServerCertificateValidationHandler<AHTTPTestClient>?  RemoteCertificateValidationHandler   = null,
                                  LocalCertificateSelectionHandler?                              LocalCertificateSelector             = null,
                                  IEnumerable<X509Certificate>?                                  ClientCertificateChain               = null,
                                  SslProtocols?                                                  TLSProtocols                         = null,
                                  CipherSuitesPolicy?                                            CipherSuitesPolicy                   = null,
                                  X509ChainPolicy?                                               CertificateChainPolicy               = null,
                                  Boolean?                                                       EnforceTLS                           = null,
                                  IEnumerable<SslApplicationProtocol>?                           ApplicationProtocols                 = null,
                                  Boolean?                                                       AllowRenegotiation                   = null,
                                  Boolean?                                                       AllowTLSResume                       = null,

                                  TimeSpan?                                                      ConnectTimeout                       = null,
                                  TimeSpan?                                                      ReceiveTimeout                       = null,
                                  TimeSpan?                                                      SendTimeout                          = null,
                                  UInt32?                                                        BufferSize                           = null,
                                  TCPEchoLoggingDelegate?                                        LoggingHandler                       = null)

            : base(IPAddress,
                   TCPPort ?? IPPort.HTTP,
                   Description,

                   RemoteCertificateValidationHandler is not null
                       ? (sender,
                          certificate,
                          certificateChain,
                          tlsClient,
                          policyErrors) => RemoteCertificateValidationHandler.Invoke(
                                               sender,
                                               certificate,
                                               certificateChain,
                                               tlsClient as DNSHTTPSClient,
                                               policyErrors
                                           )
                       : null,
                   LocalCertificateSelector,
                   ClientCertificateChain,
                   TLSProtocols,
                   CipherSuitesPolicy,
                   CertificateChainPolicy,
                   EnforceTLS,
                   ApplicationProtocols,
                   AllowRenegotiation,
                   AllowTLSResume,

                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   BufferSize,
                   LoggingHandler)

        {

            this.HTTPUserAgent  = HTTPUserAgent ?? DefaultHTTPUserAgent;

        }

        #endregion

        #region AHTTPTestClient(URL, DNSService = null, ...)

        protected AHTTPTestClient(URL                                                            URL,
                                  SRV_Spec?                                                      DNSService                           = null,
                                  I18NString?                                                    Description                          = null,
                                  String?                                                        HTTPUserAgent                        = null,
                                  DefaultRequestBuilderDelegate?                                 DefaultRequestBuilder                = null,

                                  RemoteTLSServerCertificateValidationHandler<AHTTPTestClient>?  RemoteCertificateValidationHandler   = null,
                                  LocalCertificateSelectionHandler?                              LocalCertificateSelector             = null,
                                  IEnumerable<X509Certificate>?                                  ClientCertificateChain               = null,
                                  SslProtocols?                                                  TLSProtocols                         = null,
                                  CipherSuitesPolicy?                                            CipherSuitesPolicy                   = null,
                                  X509ChainPolicy?                                               CertificateChainPolicy               = null,
                                  Boolean?                                                       EnforceTLS                           = null,
                                  IEnumerable<SslApplicationProtocol>?                           ApplicationProtocols                 = null,
                                  Boolean?                                                       AllowRenegotiation                   = null,
                                  Boolean?                                                       AllowTLSResume                       = null,

                                  TimeSpan?                                                      ConnectTimeout                       = null,
                                  TimeSpan?                                                      ReceiveTimeout                       = null,
                                  TimeSpan?                                                      SendTimeout                          = null,
                                  UInt32?                                                        BufferSize                           = null,
                                  TCPEchoLoggingDelegate?                                        LoggingHandler                       = null,
                                  DNSClient?                                                     DNSClient                            = null)

            : base(URL,
                   DNSService,
                   Description,

                   RemoteCertificateValidationHandler is not null
                       ? (sender,
                          certificate,
                          certificateChain,
                          tlsClient,
                          policyErrors) => RemoteCertificateValidationHandler.Invoke(
                                               sender,
                                               certificate,
                                               certificateChain,
                                               tlsClient as DNSHTTPSClient,
                                               policyErrors
                                           )
                       : null,
                   LocalCertificateSelector,
                   ClientCertificateChain,
                   TLSProtocols,
                   CipherSuitesPolicy,
                   CertificateChainPolicy,
                   EnforceTLS,
                   ApplicationProtocols,
                   AllowRenegotiation,
                   AllowTLSResume,

                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   BufferSize,
                   LoggingHandler,
                   DNSClient)

        {

            this.HTTPUserAgent          = HTTPUserAgent ?? DefaultHTTPUserAgent;

            this.DefaultRequestBuilder  = DefaultRequestBuilder
                                              ?? (() => new HTTPRequest.Builder(this, CancellationToken.None) {
                                                            Host        = URL.Hostname,
                                                            Accept      = AcceptTypes.FromHTTPContentTypes(HTTPContentType.Application.JSON_UTF8),
                                                            UserAgent   = HTTPUserAgent ?? DefaultHTTPUserAgent,
                                                            Connection  = ConnectionType.KeepAlive
                                                        });

        }

        #endregion

        #region AHTTPTestClient(DomainName, DNSService, ...)

        protected AHTTPTestClient(DomainName                                                     DomainName,
                                  SRV_Spec                                                       DNSService,
                                  I18NString?                                                    Description                          = null,
                                  String?                                                        HTTPUserAgent                        = null,

                                  RemoteTLSServerCertificateValidationHandler<AHTTPTestClient>?  RemoteCertificateValidationHandler   = null,
                                  LocalCertificateSelectionHandler?                              LocalCertificateSelector             = null,
                                  IEnumerable<X509Certificate>?                                  ClientCertificateChain               = null,
                                  SslProtocols?                                                  TLSProtocols                         = null,
                                  CipherSuitesPolicy?                                            CipherSuitesPolicy                   = null,
                                  X509ChainPolicy?                                               CertificateChainPolicy               = null,
                                  Boolean?                                                       EnforceTLS                           = null,
                                  IEnumerable<SslApplicationProtocol>?                           ApplicationProtocols                 = null,
                                  Boolean?                                                       AllowRenegotiation                   = null,
                                  Boolean?                                                       AllowTLSResume                       = null,

                                  TimeSpan?                                                      ConnectTimeout                       = null,
                                  TimeSpan?                                                      ReceiveTimeout                       = null,
                                  TimeSpan?                                                      SendTimeout                          = null,
                                  UInt32?                                                        BufferSize                           = null,
                                  TCPEchoLoggingDelegate?                                        LoggingHandler                       = null,
                                  DNSClient?                                                     DNSClient                            = null)

            : base(DomainName,
                   DNSService,
                   Description,

                   RemoteCertificateValidationHandler is not null
                       ? (sender,
                          certificate,
                          certificateChain,
                          tlsClient,
                          policyErrors) => RemoteCertificateValidationHandler.Invoke(
                                               sender,
                                               certificate,
                                               certificateChain,
                                               tlsClient as DNSHTTPSClient,
                                               policyErrors
                                           )
                       : null,
                   LocalCertificateSelector,
                   ClientCertificateChain,
                   TLSProtocols,
                   CipherSuitesPolicy,
                   CertificateChainPolicy,
                   EnforceTLS,
                   ApplicationProtocols,
                   AllowRenegotiation,
                   AllowTLSResume,

                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   BufferSize,
                   LoggingHandler,
                   DNSClient)

        {

            this.HTTPUserAgent  = HTTPUserAgent ?? DefaultHTTPUserAgent;

        }

        #endregion

        #endregion



        #region ReconnectAsync()

        public async Task ReconnectAsync()
        {

            await base.ReconnectAsync().ConfigureAwait(false);

        }

        #endregion

        #region (protected) ConnectAsync(CancellationToken = default)

        protected override async Task ConnectAsync(CancellationToken CancellationToken = default)
        {

            await base.ConnectAsync(CancellationToken);

            httpStream = tcpClient?.GetStream();

            if (EnforceTLS ||
                RemoteURL?.Protocol == URLProtocols.https ||
                RemoteURL?.Protocol == URLProtocols.wss)
            {
                await StartTLS(CancellationToken);
            }

            IsHTTPConnected = true;

        }

        #endregion

        #region (protected) StartTLS(CancellationToken = default)

        protected async Task StartTLS(CancellationToken CancellationToken = default)
        {

            if (httpStream is not null)
            {

                tlsStream   = new SslStream(
                                  httpStream,
                                  leaveInnerStreamOpen: false
                              );

                var authenticationOptions  = new SslClientAuthenticationOptions {
                                                    //ApplicationProtocols                = new List<SslApplicationProtocol> {
                                                    //                                          SslApplicationProtocol.Http2,  // Example: Add HTTP/2   protocol
                                                    //                                          SslApplicationProtocol.Http11  // Example: Add HTTP/1.1 protocol
                                                    //                                      },
                                                    AllowRenegotiation                  = AllowRenegotiation ?? true,
                                                    AllowTlsResume                      = AllowTLSResume     ?? true,
                                                    LocalCertificateSelectionCallback   = null,
                                                    TargetHost                          = RemoteURL?.Hostname.ToString() ?? DomainName?.ToString() ?? RemoteIPAddress?.ToString(), //SNI!
                                                    ClientCertificates                  = null,
                                                    ClientCertificateContext            = null,
                                                    CertificateRevocationCheckMode      = X509RevocationMode.NoCheck,
                                                    EncryptionPolicy                    = EncryptionPolicy.RequireEncryption,
                                                    EnabledSslProtocols                 = SslProtocols.Tls12 | SslProtocols.Tls13,
                                                    CipherSuitesPolicy                  = null, // new CipherSuitesPolicy(TlsCipherSuite.),
                                                    CertificateChainPolicy              = null, // new X509ChainPolicy()
                                                };

                if (RemoteCertificateValidationHandler is not null)
                {
                    authenticationOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, policyErrors) => {

                        var result = RemoteCertificateValidationHandler(
                                            sender,
                                            certificate is not null
                                                ? new X509Certificate2(certificate)
                                                : null,
                                            chain,
                                            this,
                                            policyErrors
                                        );

                        return result.Item1;

                    };
                }
                else
                {
                    authenticationOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, policyErrors) => {
                        return true;
                    };
                }

                try
                {
                    await tlsStream.AuthenticateAsClientAsync(
                                authenticationOptions,
                                CancellationToken
                            );
                }
                catch (Exception e)
                {
                    DebugX.Log($"Error during TLS authentication: {e.Message}");
                }

                httpStream       = tlsStream;

            }

        }

        #endregion


        #region CreateRequest (HTTPMethod, HTTPPath, ...)

        /// <summary>
        /// Create a new HTTP request.
        /// </summary>
        /// <param name="HTTPMethod">An HTTP method.</param>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public HTTPRequest.Builder CreateRequest(HTTPMethod                    HTTPMethod,
                                                 HTTPPath                      HTTPPath,
                                                 QueryString?                  QueryString         = null,
                                                 AcceptTypes?                  Accept              = null,
                                                 IHTTPAuthentication?          Authentication      = null,
                                                 String?                       UserAgent           = null,
                                                 ConnectionType?               Connection          = null,
                                                 Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                 CancellationToken             CancellationToken   = default)
{

            var builder = new HTTPRequest.Builder(null, CancellationToken) {
                              Host           = HTTPHostname.Localhost, // HTTPHostname.Parse((VirtualHostname ?? RemoteURL.Hostname) + (RemoteURL.Port.HasValue && RemoteURL.Port != IPPort.HTTP && RemoteURL.Port != IPPort.HTTPS ? ":" + RemoteURL.Port.ToString() : String.Empty)),
                              HTTPMethod     = HTTPMethod,
                              Path           = HTTPPath,
                              QueryString    = QueryString ?? QueryString.Empty,
                              Authorization  = Authentication,
                            //  UserAgent      = UserAgent   ?? HTTPUserAgent,
                              Connection     = Connection
                          };

            if (Accept is not null)
                builder.Accept = Accept;

            RequestBuilder?.Invoke(builder);

            return builder;

        }

        #endregion

        #region SendRequest (Request)

        /// <summary>
        /// Send the given HTTP Request to the server and receive the HTTP Response.
        /// </summary>
        /// <param name="Request">The HTTP Request to send.</param>
        /// <returns>Whether the echo was successful, the echoed response, an optional error response, and the time taken to send and receive it.</returns>
        public async Task<(Boolean, HTTPResponse?, String?, TimeSpan)>

            SendRequest(HTTPRequest        Request,
                        CancellationToken  CancellationToken   = default)

        {

            if (!IsConnected)
                return (false, null, "Client is not connected.", TimeSpan.Zero);

            if (!IsHTTPConnected)
                await ReconnectAsync().ConfigureAwait(false);

            var stopwatch = Stopwatch.StartNew();

            try
            {

                #region Send HTTP Request

                await httpStream.WriteAsync(Encoding.UTF8.GetBytes(Request.EntireRequestHeader + "\r\n\r\n"), cts.Token).ConfigureAwait(false);

                if (Request.HTTPBody is not null && Request.ContentLength > 0)
                    await httpStream.WriteAsync(Request.HTTPBody, cts.Token).ConfigureAwait(false);

                await httpStream.FlushAsync(cts.Token).ConfigureAwait(false);

                #endregion

                IMemoryOwner<Byte>? bufferOwner = MemoryPool<Byte>.Shared.Rent(BufferSize * 2);
                var buffer = bufferOwner.Memory;
                var dataLength = 0;

                while (true)
                {

                    #region Read data if no delimiter found yet

                    if (dataLength < endOfHTTPHeaderDelimiterLength ||
                        buffer.Span[0..dataLength].IndexOf(endOfHTTPHeaderDelimiter.AsSpan()) < 0)
                    {
                        if (dataLength >= buffer.Length - BufferSize)
                            throw new Exception("Header too large.");

                        var bytesRead = await httpStream.ReadAsync(buffer.Slice(dataLength, BufferSize), Request.CancellationToken);
                        if (bytesRead == 0)
                        {
                            bufferOwner?.Dispose();
                            return (false, null, "Timeout!", stopwatch.Elapsed);
                        }

                        dataLength += bytesRead;
                        continue;
                    }

                    #endregion

                    #region Search for End-of-HTTPHeader

                    var endOfHTTPHeaderIndex = buffer.Span[0..dataLength].IndexOf(endOfHTTPHeaderDelimiter.AsSpan());
                    if (endOfHTTPHeaderIndex < 0)
                        continue;  // Should not reach here due to the if-condition above.

                    #endregion

                    #region Parse HTTP Response

                    var response = HTTPResponse.Parse(
                                       //Timestamp.Now,
                                       //httpSource,
                                       //localSocket,
                                       //remoteSocket,
                                       Encoding.UTF8.GetString(buffer[..endOfHTTPHeaderIndex].Span),
                                       CancellationToken: Request.CancellationToken
                                   );

                    #endregion

                    #region Shift remaining data

                    var remainingStart = endOfHTTPHeaderIndex + endOfHTTPHeaderDelimiterLength;
                    var remainingLength = dataLength - remainingStart;
                    buffer.Slice(remainingStart, remainingLength).CopyTo(buffer[..]);
                    dataLength = remainingLength;

                    #endregion

                    #region Setup HTTP body stream

                    Stream? bodyDataStream = null;
                    Stream? bodyStream = null;

                    var prefix = buffer[..dataLength];
                    if (response.IsChunkedTransferEncoding || response.ContentLength.HasValue)
                    {

                        bodyDataStream = new PrefixStream(
                                             prefix,
                                             httpStream,
                                             LeaveInnerStreamOpen: true
                                         );

                        if (response.IsChunkedTransferEncoding)
                            bodyStream = new ChunkedTransferEncodingStream(
                                             bodyDataStream,
                                             LeaveInnerStreamOpen: true
                                         );
                        else if (response.ContentLength.HasValue && response.ContentLength.Value > 0)
                            bodyStream = new LengthLimitedStream(
                                             bodyDataStream,
                                             response.ContentLength.Value,
                                             LeaveInnerStreamOpen: true
                                         );

                    }

                    response.HTTPBodyStream = bodyStream;
                 //   response.BufferOwner    = bufferOwner;  // Transfer ownership to response for disposal after body is consumed.

                    #endregion

                    if (response.IsConnectionClose)
                    {
                        IsHTTPConnected = false;  // Mark connection for closure after response handling
                    }

                    return (true, response, null, stopwatch.Elapsed);

                }
            }
            catch (Exception ex)
            {
                await Log($"Error in SendRequest: {ex.Message}");
                return (false, null, ex.Message, stopwatch.Elapsed);
            }
            finally
            {
                stopwatch.Stop();
            }

        }

        #endregion

        #region SendText    (Text)

        /// <summary>
        /// Send the given message to the echo server and receive the echoed response.
        /// </summary>
        /// <param name="Text">The text message to send and echo.</param>
        /// <returns>Whether the echo was successful, the echoed response, an optional error response, and the time taken to send and receive it.</returns>
        public async Task<(Boolean, String, String?, TimeSpan)> SendText(String Text)
        {

            if (!IsConnected || tcpClient is null)
                return (false, "", "Client is not connected.", TimeSpan.Zero);

            try
            {

                var stopwatch = Stopwatch.StartNew();
                var stream = tcpClient.GetStream();
                cts ??= new CancellationTokenSource();

                // Send the data
                await stream.WriteAsync(Encoding.UTF8.GetBytes(Text), cts.Token).ConfigureAwait(false);
                await stream.FlushAsync(cts.Token).ConfigureAwait(false);

                using var responseStream = new MemoryStream();
                var buffer = new Byte[8192];
                var bytesRead = 0;

                while ((bytesRead = await stream.ReadAsync(buffer, cts.Token).ConfigureAwait(false)) > 0)
                {
                    await responseStream.WriteAsync(buffer.AsMemory(0, bytesRead), cts.Token).ConfigureAwait(false);
                }

                stopwatch.Stop();

                return (true, Encoding.UTF8.GetString(responseStream.ToArray()), null, stopwatch.Elapsed);

            }
            catch (Exception ex)
            {
                await Log($"Error in SendBinary: {ex.Message}");
                return (false, "", ex.Message, TimeSpan.Zero);
            }

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override string ToString()

            => $"{nameof(HTTPTestClient)}: {RemoteIPAddress}:{RemotePort} (Connected: {IsConnected})";

        #endregion


    }

}
