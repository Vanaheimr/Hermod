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
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using System.Collections.Concurrent;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A simple TCP echo test client that can connect to a TCP echo server,
    /// </summary>
    public class HTTPClientPool : IHTTPClient,
                                  IDisposable,
                                  IAsyncDisposable
    {

        #region Data

        private readonly  ConcurrentBag<HTTPTestClient>  httpClientPool         = [];

        private readonly  SemaphoreSlim                  MaxNumberOfClientsSemaphore;

        /// <summary>
        /// The default HTTP user agent.
        /// </summary>
        public  const     String                         DefaultHTTPUserAgent   = "Hermod HTTP Client";

        #endregion

        #region Properties

        public String Id {  get; }

        public Boolean                        IsHTTPConnected          { get; private set; } = false;

        public String?                        HTTPUserAgent            { get; }


        private Boolean isBusy;
        public Boolean                        IsBusy
            => isBusy;

        public TimeSpan                       MaxSemaphoreWaitTime     { get; set; }         = TimeSpan.FromSeconds(30);


        public const UInt16 DefaultMaxNumberOfClients = 16;

        public UInt16 MaxNumberOfClients { get; set; }


        private readonly Func<String, HTTPTestClient> httpClientBuilder;


        URL IHTTPClient.RemoteURL => throw new NotImplementedException();

        public HTTPHostname? VirtualHostname => throw new NotImplementedException();

        public RemoteTLSServerCertificateValidationHandler<IHTTPClient>? RemoteCertificateValidator => throw new NotImplementedException();

        public X509Certificate? ClientCert => throw new NotImplementedException();

        public HTTPContentType? ContentType => throw new NotImplementedException();

        public AcceptTypes? Accept => throw new NotImplementedException();

        public IHTTPAuthentication? Authentication => throw new NotImplementedException();

        public ConnectionType? Connection => throw new NotImplementedException();

        public TimeSpan RequestTimeout { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public TransmissionRetryDelayDelegate TransmissionRetryDelay => throw new NotImplementedException();

        public Boolean UseHTTPPipelining => throw new NotImplementedException();

        public HTTPClientLogger? HTTPLogger => throw new NotImplementedException();

        public Boolean Connected => throw new NotImplementedException();

        public IIPAddress? RemoteIPAddress => throw new NotImplementedException();

        public I18NString Description => throw new NotImplementedException();

        public SslProtocols TLSProtocols => throw new NotImplementedException();

        public Boolean PreferIPv4 => throw new NotImplementedException();

        public UInt16 MaxNumberOfRetries => throw new NotImplementedException();

        public IDNSClient? DNSClient => throw new NotImplementedException();

        public UInt64 KeepAliveMessageCount => throw new NotImplementedException();

        #endregion

        #region Events

        //public event ClientRequestLogHandler?   ClientRequestLogDelegate;
        //public event ClientResponseLogHandler?  ClientResponseLogDelegate;

        #endregion

        #region Constructor(s)

        #region HTTPClientPool(IPAddress, ...)

        public HTTPClientPool(IIPAddress                                                    IPAddress,
                              IPPort?                                                       TCPPort                              = null,
                              I18NString?                                                   Description                          = null,
                              String?                                                       HTTPUserAgent                        = null,
                              DefaultRequestBuilderDelegate?                                DefaultRequestBuilder                = null,

                              RemoteTLSServerCertificateValidationHandler<HTTPTestClient>?  RemoteCertificateValidationHandler   = null,
                              LocalCertificateSelectionHandler?                             LocalCertificateSelector             = null,
                              IEnumerable<X509Certificate>?                                 ClientCertificateChain               = null,
                              SslProtocols?                                                 TLSProtocols                         = null,
                              CipherSuitesPolicy?                                           CipherSuitesPolicy                   = null,
                              X509ChainPolicy?                                              CertificateChainPolicy               = null,
                              X509RevocationMode?                                           CertificateRevocationCheckMode       = null,
                              Boolean?                                                      EnforceTLS                           = null,
                              IEnumerable<SslApplicationProtocol>?                          ApplicationProtocols                 = null,
                              Boolean?                                                      AllowRenegotiation                   = null,
                              Boolean?                                                      AllowTLSResume                       = null,

                              UInt16?                                                       MaxNumberOfClients                   = null,

                              Boolean?                                                      PreferIPv4                           = null,
                              TimeSpan?                                                     ConnectTimeout                       = null,
                              TimeSpan?                                                     ReceiveTimeout                       = null,
                              TimeSpan?                                                     SendTimeout                          = null,
                              TransmissionRetryDelayDelegate?                               TransmissionRetryDelay               = null,
                              UInt16?                                                       MaxNumberOfRetries                   = null,
                              UInt32?                                                       BufferSize                           = null)
        {

            this.Id                           = $"{IPAddress}:{TCPPort ?? IPPort.HTTPS}";

            this.MaxNumberOfClients           = MaxNumberOfClients ?? DefaultMaxNumberOfClients;

            this.MaxNumberOfClientsSemaphore  = new SemaphoreSlim(
                                                    this.MaxNumberOfClients,
                                                    this.MaxNumberOfClients
                                                );

            this.httpClientBuilder            = (description) => new HTTPTestClient(

                                                                     IPAddress,
                                                                     TCPPort,
                                                                     Description,
                                                                     HTTPUserAgent,
                                                                     DefaultRequestBuilder,

                                                                     RemoteCertificateValidationHandler,
                                                                     LocalCertificateSelector,
                                                                     ClientCertificateChain,
                                                                     TLSProtocols,
                                                                     CipherSuitesPolicy,
                                                                     CertificateChainPolicy,
                                                                     CertificateRevocationCheckMode,
                                                                     EnforceTLS,
                                                                     ApplicationProtocols,
                                                                     AllowRenegotiation,
                                                                     AllowTLSResume,

                                                                     PreferIPv4,
                                                                     ConnectTimeout,
                                                                     ReceiveTimeout,
                                                                     SendTimeout,
                                                                     TransmissionRetryDelay,
                                                                     MaxNumberOfRetries,
                                                                     BufferSize

                                                                 );

        }

        #endregion

        #region HTTPClientPool(URL, DNSService = null, ...)

        public HTTPClientPool(URL                                                           URL,
                              I18NString?                                                   Description                      = null,
                              String?                                                       HTTPUserAgent                    = null,
                              DefaultRequestBuilderDelegate?                                DefaultRequestBuilder            = null,

                              RemoteTLSServerCertificateValidationHandler<HTTPTestClient>?  RemoteCertificateValidator       = null,
                              LocalCertificateSelectionHandler?                             LocalCertificateSelector         = null,
                              IEnumerable<X509Certificate>?                                 ClientCertificateChain           = null,
                              SslProtocols?                                                 TLSProtocols                     = null,
                              CipherSuitesPolicy?                                           CipherSuitesPolicy               = null,
                              X509ChainPolicy?                                              CertificateChainPolicy           = null,
                              X509RevocationMode?                                           CertificateRevocationCheckMode   = null,
                              //Boolean?                                                      EnforceTLS                       = null,
                              IEnumerable<SslApplicationProtocol>?                          ApplicationProtocols             = null,
                              Boolean?                                                      AllowRenegotiation               = null,
                              Boolean?                                                      AllowTLSResume                   = null,

                              UInt16?                                                       MaxNumberOfClients               = null,

                              Boolean?                                                      PreferIPv4                       = null,
                              TimeSpan?                                                     ConnectTimeout                   = null,
                              TimeSpan?                                                     ReceiveTimeout                   = null,
                              TimeSpan?                                                     SendTimeout                      = null,
                              TransmissionRetryDelayDelegate?                               TransmissionRetryDelay           = null,
                              UInt16?                                                       MaxNumberOfRetries               = null,
                              UInt32?                                                       BufferSize                       = null,
                              IDNSClient?                                                   DNSClient                        = null)

        {

            this.Id                           = URL.ToString();

            this.MaxNumberOfClients           = MaxNumberOfClients ?? DefaultMaxNumberOfClients;

            this.MaxNumberOfClientsSemaphore  = new SemaphoreSlim(
                                                    this.MaxNumberOfClients,
                                                    this.MaxNumberOfClients
                                                );

            this.httpClientBuilder            = (description) => new HTTPTestClient(

                                                                     URL,
                                                                     Description,
                                                                     HTTPUserAgent,
                                                                     DefaultRequestBuilder,

                                                                     RemoteCertificateValidator,
                                                                     LocalCertificateSelector,
                                                                     ClientCertificateChain,
                                                                     TLSProtocols,
                                                                     CipherSuitesPolicy,
                                                                     CertificateChainPolicy,
                                                                     CertificateRevocationCheckMode,
                                                                     //EnforceTLS,
                                                                     ApplicationProtocols,
                                                                     AllowRenegotiation,
                                                                     AllowTLSResume,

                                                                     PreferIPv4,
                                                                     ConnectTimeout,
                                                                     ReceiveTimeout,
                                                                     SendTimeout,
                                                                     TransmissionRetryDelay,
                                                                     MaxNumberOfRetries,
                                                                     BufferSize

                                                                 );

        }

        #endregion

        #region HTTPClientPool(DomainName, DNSService, ...)

        public HTTPClientPool(DomainName                                                    DomainName,
                              SRV_Spec                                                      DNSService,
                              I18NString?                                                   Description                      = null,
                              String?                                                       HTTPUserAgent                    = null,
                              DefaultRequestBuilderDelegate?                                DefaultRequestBuilder            = null,

                              RemoteTLSServerCertificateValidationHandler<HTTPTestClient>?  RemoteCertificateValidator       = null,
                              LocalCertificateSelectionHandler?                             LocalCertificateSelector         = null,
                              IEnumerable<X509Certificate>?                                 ClientCertificateChain           = null,
                              SslProtocols?                                                 TLSProtocols                     = null,
                              CipherSuitesPolicy?                                           CipherSuitesPolicy               = null,
                              X509ChainPolicy?                                              CertificateChainPolicy           = null,
                              X509RevocationMode?                                           CertificateRevocationCheckMode   = null,
                              Boolean?                                                      EnforceTLS                       = null,
                              IEnumerable<SslApplicationProtocol>?                          ApplicationProtocols             = null,
                              Boolean?                                                      AllowRenegotiation               = null,
                              Boolean?                                                      AllowTLSResume                   = null,

                              UInt16?                                                       MaxNumberOfClients               = null,

                              Boolean?                                                      PreferIPv4                       = null,
                              TimeSpan?                                                     ConnectTimeout                   = null,
                              TimeSpan?                                                     ReceiveTimeout                   = null,
                              TimeSpan?                                                     SendTimeout                      = null,
                              TransmissionRetryDelayDelegate?                               TransmissionRetryDelay           = null,
                              UInt16?                                                       MaxNumberOfRetries               = null,
                              UInt32?                                                       BufferSize                       = null,
                              IDNSClient?                                                   DNSClient                        = null)

        {

            this.Id                           = $"{DomainName} / {DNSService}";

            this.MaxNumberOfClients           = MaxNumberOfClients ?? DefaultMaxNumberOfClients;

            this.MaxNumberOfClientsSemaphore  = new SemaphoreSlim(
                                                    this.MaxNumberOfClients,
                                                    this.MaxNumberOfClients
                                                );

            this.httpClientBuilder            = (description) => new HTTPTestClient(

                                                                     DomainName,
                                                                     DNSService,
                                                                     Description,
                                                                     HTTPUserAgent,
                                                                     DefaultRequestBuilder,

                                                                     RemoteCertificateValidator,
                                                                     LocalCertificateSelector,
                                                                     ClientCertificateChain,
                                                                     TLSProtocols,
                                                                     CipherSuitesPolicy,
                                                                     CertificateChainPolicy,
                                                                     CertificateRevocationCheckMode,
                                                                     EnforceTLS,
                                                                     ApplicationProtocols,
                                                                     AllowRenegotiation,
                                                                     AllowTLSResume,

                                                                     PreferIPv4,
                                                                     ConnectTimeout,
                                                                     ReceiveTimeout,
                                                                     SendTimeout,
                                                                     TransmissionRetryDelay,
                                                                     MaxNumberOfRetries,
                                                                     BufferSize

                                                                 );

        }

        #endregion

        #endregion


        #region SendRequest   (Request)

        /// <summary>
        /// Send the given HTTP Request to the server and receive the HTTP Response.
        /// </summary>
        /// <param name="Request">The HTTP Request to send.</param>
        /// <returns>Whether the echo was successful, the echoed response, an optional error response, and the time taken to send and receive it.</returns>
        public async Task<HTTPResponse>

            SendRequest(HTTPRequest                Request,
                        ClientRequestLogHandler?   RequestLogDelegate     = null,
                        ClientResponseLogHandler?  ResponseLogDelegate    = null,
                        TimeSpan?                  MaxSemaphoreWaitTime   = null,
                        CancellationToken          CancellationToken      = default)

        {

            // Only MaxNumberOfClients concurrent requests!
            await MaxNumberOfClientsSemaphore.WaitAsync(CancellationToken);

            try
            {

                var httpClient = GetFreeConnection();

                if (httpClient is null)
                {
                    if (httpClientPool.Count < MaxNumberOfClients)
                    {
                        httpClient = httpClientBuilder($"#{httpClientPool.Count + 1}");
                        httpClientPool.Add(httpClient);
                    }
                    else
                    {
                        while ((httpClient = GetFreeConnection()) is null)
                        {
                            await Task.Delay(10, CancellationToken);
                        }
                    }
                }

                return await httpClient.SendRequest(
                                 Request,
                                 CancellationToken: CancellationToken
                             );

            }
            finally
            {
                MaxNumberOfClientsSemaphore.Release();
            }

            //return new HTTPResponse.Builder(Request) {
            //           HTTPStatusCode  = HTTPStatusCode.BadRequest,
            //           Content         = $"Could not acquire semaphore for {nameof(AHTTPTestClient)}.{nameof(SendRequest)}.".ToUTF8Bytes(),
            //           ContentType     = HTTPContentType.Text.PLAIN,
            //           Runtime         = TimeSpan.Zero
            //       };

        }

        #endregion


        private HTTPTestClient? GetFreeConnection()
        {

            foreach (var httpClient in httpClientPool)
            {
                if (!httpClient.IsBusy)// && DateTime.UtcNow - httpClient.LastUsed < _httpClientLifetime)
                {
                    if (Interlocked.CompareExchange(ref httpClient.IsBusy, true, false) == false)
                    {
                        return httpClient;
                    }
                }
            }

            return null;

        }


        #region (private)   LogEvent     (Logger, LogHandler, ...)

        //private Task LogEvent<TDelegate>(TDelegate?                                         Logger,
        //                                 Func<TDelegate, Task>                              LogHandler,
        //                                 [CallerArgumentExpression(nameof(Logger))] String  EventName     = "",
        //                                 [CallerMemberName()]                       String  OICPCommand   = "")

        //    where TDelegate : Delegate

        //    => LogEvent(
        //           nameof(AHTTPTestClient),
        //           Logger,
        //           LogHandler,
        //           EventName,
        //           OICPCommand
        //       );

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{nameof(HTTPClientPool)} ({MaxNumberOfClients}): {Id}";

        #endregion

        #region Dispose()

        public void Dispose()
        {
            //throw new NotImplementedException();
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        #endregion

    }

}
