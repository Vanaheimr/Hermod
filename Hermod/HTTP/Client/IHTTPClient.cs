/*
 * Copyright (c) 2010-2021, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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

using System;
using System.Threading;
using System.Net.Security;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The HTTP client interface.
    /// </summary>
    public interface IHTTPClient : IDisposable
    {

        /// <summary>
        /// The remote URL of the OICP HTTP endpoint to connect to.
        /// </summary>
        URL                                  RemoteURL                     { get; }

        /// <summary>
        /// An optional HTTP virtual hostname.
        /// </summary>
        HTTPHostname?                        VirtualHostname               { get; }

        /// <summary>
        /// An optional description of this CPO client.
        /// </summary>
        String                               Description                   { get; set; }

        /// <summary>
        /// The remote SSL/TLS certificate validator.
        /// </summary>
        RemoteCertificateValidationCallback  RemoteCertificateValidator    { get; }

        /// <summary>
        /// The SSL/TLS client certificate to use of HTTP authentication.
        /// </summary>
        X509Certificate                      ClientCert                    { get; }

        /// <summary>
        /// The HTTP user agent identification.
        /// </summary>
        String                               HTTPUserAgent                 { get; }

        /// <summary>
        /// The timeout for HTTP requests.
        /// </summary>
        TimeSpan                             RequestTimeout                { get; set; }

        /// <summary>
        /// The delay between transmission retries.
        /// </summary>
        TransmissionRetryDelayDelegate       TransmissionRetryDelay        { get; }

        /// <summary>
        /// The maximum number of transmission retries for HTTP request.
        /// </summary>
        UInt16                               MaxNumberOfRetries            { get; }

        /// <summary>
        /// Make use of HTTP pipelining.
        /// </summary>
        Boolean                              UseHTTPPipelining             { get; }

        /// <summary>
        /// The CPO client (HTTP client) logger.
        /// </summary>
        HTTPClientLogger                     HTTPLogger                    { get; set; }

        /// <summary>
        /// The DNS client to use.
        /// </summary>
        DNSClient                            DNSClient                     { get; }




        //int Available { get; }
        //X509Certificate ClientCert { get; }
        //bool Connected { get; }

        //LingerOption LingerState { get; set; }
        //LocalCertificateSelectionCallback LocalCertificateSelector { get; }
        //bool NoDelay { get; set; }
        //byte TTL { get; set; }

        //event HTTPClient.OnDataReadDelegate OnDataRead;

        //void Close();


    }

    public interface IHTTPClientCommands : IHTTPClient
    {

        Task<HTTPResponse> Execute(Func<AHTTPClient, HTTPRequest>  HTTPRequestDelegate,
                                   ClientRequestLogHandler         RequestLogDelegate    = null,
                                   ClientResponseLogHandler        ResponseLogDelegate   = null,

                                   CancellationToken?              CancellationToken     = null,
                                   EventTracking_Id                EventTrackingId       = null,
                                   TimeSpan?                       RequestTimeout        = null,
                                   Byte                            NumberOfRetry         = 0);

        Task<HTTPResponse> Execute(HTTPRequest                     Request,
                                   ClientRequestLogHandler         RequestLogDelegate    = null,
                                   ClientResponseLogHandler        ResponseLogDelegate   = null,

                                   CancellationToken?              CancellationToken     = null,
                                   EventTracking_Id                EventTrackingId       = null,
                                   TimeSpan?                       RequestTimeout        = null,
                                   Byte                            NumberOfRetry         = 0);

    }

}