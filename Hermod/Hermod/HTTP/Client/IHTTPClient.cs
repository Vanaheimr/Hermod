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

using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The HTTP client interface.
    /// </summary>
    public interface IHTTPClient : IDisposable
    {

        /// <summary>
        /// The remote URL of the HTTP endpoint to connect to.
        /// </summary>
        URL                                                        RemoteURL                     { get; }

        /// <summary>
        /// The IP Address to connect to.
        /// </summary>
        IIPAddress?                                                RemoteIPAddress               { get; }

        /// <summary>
        /// An optional HTTP virtual hostname.
        /// </summary>
        HTTPHostname?                                              VirtualHostname               { get; }

        /// <summary>
        /// An optional description of this HTTP client.
        /// </summary>
        I18NString                                                 Description                   { get; }

        /// <summary>
        /// The remote TLS certificate validator.
        /// </summary>
        RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidator    { get; }

        /// <summary>
        /// The TLS client certificate to use for HTTP authentication.
        /// </summary>
        X509Certificate2?                                          ClientCertificate             { get; }

        /// <summary>
        /// The TLS protocol to use.
        /// </summary>
        SslProtocols                                               TLSProtocols                  { get; }

        /// <summary>
        /// Prefer IPv4 instead of IPv6.
        /// </summary>
        Boolean                                                    PreferIPv4                    { get; }

        /// <summary>
        /// An optional HTTP content type.
        /// </summary>
        HTTPContentType?                                           ContentType                   { get; }

        /// <summary>
        /// Optional HTTP accept types.
        /// </summary>
        AcceptTypes?                                               Accept                        { get; }

        /// <summary>
        /// The optional HTTP authentication to use.
        /// </summary>
        IHTTPAuthentication?                                       HTTPAuthentication            { get; set; }

        /// <summary>
        /// The optional Time-Based One-Time Password (TOTP) generator.
        /// </summary>
        TOTPConfig?                                                TOTPConfig                    { get; set; }

        /// <summary>
        /// The HTTP user agent identification.
        /// </summary>
        String?                                                    HTTPUserAgent                 { get; }

        /// <summary>
        /// The optional HTTP connection type.
        /// </summary>
        ConnectionType?                                            Connection                    { get; }

        /// <summary>
        /// The timeout for HTTP requests.
        /// </summary>
        TimeSpan                                                   RequestTimeout                { get; set; }

        /// <summary>
        /// The delay between transmission retries.
        /// </summary>
        TransmissionRetryDelayDelegate                             TransmissionRetryDelay        { get; }

        /// <summary>
        /// The maximum number of transmission retries for HTTP request.
        /// </summary>
        UInt16                                                     MaxNumberOfRetries            { get; }

        /// <summary>
        /// Make use of HTTP pipelining.
        /// </summary>
        Boolean                                                    UseHTTPPipelining             { get; }

        /// <summary>
        /// The CPO client (HTTP client) logger.
        /// </summary>
        HTTPClientLogger?                                          HTTPLogger                    { get; }

        /// <summary>
        /// The DNS client to use.
        /// </summary>
        IDNSClient?                                                DNSClient                     { get; }



        UInt64                                                     KeepAliveMessageCount         { get; }



        //int Available { get; }
        //X509Certificate ClientCert { get; }
        Boolean Connected { get; }

        //LingerOption LingerState { get; set; }
        //LocalCertificateSelectionHandler LocalCertificateSelector { get; }
        //bool NoDelay { get; set; }
        //byte TTL { get; set; }

        //event HTTPClient.OnDataReadDelegate OnDataRead;

        //void Close();




        //Task<HTTPResponse> Execute(Func<AHTTPClient, HTTPRequest>  HTTPRequestDelegate,
        //                           ClientRequestLogHandler?        RequestLogDelegate    = null,
        //                           ClientResponseLogHandler?       ResponseLogDelegate   = null,

        //                           EventTracking_Id?               EventTrackingId       = null,
        //                           TimeSpan?                       RequestTimeout        = null,
        //                           Byte                            NumberOfRetry         = 0,
        //                           CancellationToken               CancellationToken     = default);

        //Task<HTTPResponse> Execute(HTTPRequest                     Request,
        //                           ClientRequestLogHandler?        RequestLogDelegate    = null,
        //                           ClientResponseLogHandler?       ResponseLogDelegate   = null,

        //                           EventTracking_Id?               EventTrackingId       = null,
        //                           TimeSpan?                       RequestTimeout        = null,
        //                           Byte                            NumberOfRetry         = 0,
        //                           CancellationToken               CancellationToken     = default);


    }

}
