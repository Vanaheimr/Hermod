﻿/*
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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// An HTTP client.
    /// </summary>
    public class HTTPClient : AHTTPClient,
                              IHTTPClientCommands
    {

        #region Data

        /// <summary>
        /// The default HTTPS user agent.
        /// </summary>
        public new const String  DefaultHTTPUserAgent  = "GraphDefined HTTP Client";

        #endregion


        #region HTTPClient(RemoteURL, ...)

        /// <summary>
        /// Create a new HTTP client.
        /// </summary>
        /// <param name="RemoteURL">The remote URL of the OICP HTTP endpoint to connect to.</param>
        /// <param name="VirtualHostname">An optional HTTP virtual hostname.</param>
        /// <param name="Description">An optional description of this CPO client.</param>
        /// <param name="PreferIPv4">Prefer IPv4 instead of IPv6.</param>
        /// <param name="ContentType">An optional HTTP content type.</param>
        /// <param name="Accept">The optional HTTP accept header.</param>
        /// <param name="HTTPAuthentication">The optional HTTP authentication to use, e.g. HTTP Basic Auth.</param>
        /// <param name="HTTPUserAgent">The HTTP user agent identification.</param>
        /// <param name="Connection">An optional connection type.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for HTTP request.</param>
        /// <param name="InternalBufferSize">An optional size of the internal buffers.</param>
        /// <param name="UseHTTPPipelining">Whether to pipeline multiple HTTP request through a single HTTP/TCP connection.</param>
        /// <param name="DisableLogging">Disable logging.</param>
        /// <param name="HTTPLogger">An HTTP logger.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        public HTTPClient(URL                              RemoteURL,
                          HTTPHostname?                    VirtualHostname          = null,
                          I18NString?                      Description              = null,
                          Boolean?                         PreferIPv4               = null,
                          HTTPContentType?                 ContentType              = null,
                          AcceptTypes?                     Accept                   = null,
                          IHTTPAuthentication?             HTTPAuthentication       = null,
                          String?                          HTTPUserAgent            = DefaultHTTPUserAgent,
                          ConnectionType?                  Connection               = null,
                          TimeSpan?                        RequestTimeout           = null,
                          TransmissionRetryDelayDelegate?  TransmissionRetryDelay   = null,
                          UInt16?                          MaxNumberOfRetries       = null,
                          UInt32?                          InternalBufferSize       = null,
                          Boolean                          UseHTTPPipelining        = false,
                          Boolean?                         DisableLogging           = false,
                          HTTPClientLogger?                HTTPLogger               = null,
                          DNSClient?                       DNSClient                = null)

            : base(RemoteURL,
                   VirtualHostname,
                   Description,
                   PreferIPv4,
                   null,
                   null,
                   null,
                   null,
                   ContentType,
                   Accept,
                   HTTPAuthentication,
                   HTTPUserAgent ?? DefaultHTTPUserAgent,
                   Connection,
                   RequestTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   InternalBufferSize,
                   UseHTTPPipelining,
                   DisableLogging,
                   HTTPLogger,
                   DNSClient)

        { }

        #endregion

        #region HTTPClient(RemoteIPAddress, RemotePort = null, ...)

        /// <summary>
        /// Create a new HTTP client.
        /// </summary>
        /// <param name="RemoteIPAddress">The remote IP address to connect to.</param>
        /// <param name="RemotePort">An optional remote TCP port to connect to.</param>
        /// <param name="VirtualHostname">An optional HTTP virtual hostname.</param>
        /// <param name="Description">An optional description of this CPO client.</param>
        /// <param name="PreferIPv4">Prefer IPv4 instead of IPv6.</param>
        /// <param name="ContentType">An optional HTTP content type.</param>
        /// <param name="Accept">The optional HTTP accept header.</param>
        /// <param name="HTTPAuthentication">The optional HTTP authentication to use, e.g. HTTP Basic Auth.</param>
        /// <param name="HTTPUserAgent">The HTTP user agent identification.</param>
        /// <param name="Connection">An optional connection type.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for HTTP request.</param>
        /// <param name="InternalBufferSize">An optional size of the internal buffers.</param>
        /// <param name="UseHTTPPipelining">Whether to pipeline multiple HTTP request through a single HTTP/TCP connection.</param>
        /// <param name="DisableLogging">Disable logging.</param>
        /// <param name="HTTPLogger">An HTTP logger.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        public HTTPClient(IIPAddress                       RemoteIPAddress,
                          IPPort?                          RemotePort               = null,
                          HTTPHostname?                    VirtualHostname          = null,
                          I18NString?                      Description              = null,
                          Boolean?                         PreferIPv4               = null,
                          HTTPContentType?                 ContentType              = null,
                          AcceptTypes?                     Accept                   = null,
                          IHTTPAuthentication?             HTTPAuthentication       = null,
                          String?                          HTTPUserAgent            = DefaultHTTPUserAgent,
                          ConnectionType?                  Connection               = null,
                          TimeSpan?                        RequestTimeout           = null,
                          TransmissionRetryDelayDelegate?  TransmissionRetryDelay   = null,
                          UInt16?                          MaxNumberOfRetries       = null,
                          UInt32?                          InternalBufferSize       = null,
                          Boolean                          UseHTTPPipelining        = false,
                          Boolean?                         DisableLogging           = false,
                          HTTPClientLogger?                HTTPLogger               = null,
                          DNSClient?                       DNSClient                = null)

            : this(URL.Parse($"http://{RemoteIPAddress}{(RemotePort.HasValue ? ":" + RemotePort.Value.ToString() : String.Empty)}"),
                   VirtualHostname,
                   Description,
                   PreferIPv4,
                   ContentType,
                   Accept,
                   HTTPAuthentication,
                   HTTPUserAgent ?? DefaultHTTPUserAgent,
                   Connection,
                   RequestTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   InternalBufferSize,
                   UseHTTPPipelining,
                   DisableLogging,
                   HTTPLogger,
                   DNSClient)

        { }

        #endregion

        #region HTTPClient(RemoteSocket, ...)

        /// <summary>
        /// Create a new HTTP client.
        /// </summary>
        /// <param name="RemoteSocket">The remote IP socket to connect to.</param>
        /// <param name="VirtualHostname">An optional HTTP virtual hostname.</param>
        /// <param name="Description">An optional description of this CPO client.</param>
        /// <param name="PreferIPv4">Prefer IPv4 instead of IPv6.</param>
        /// <param name="Accept">The optional HTTP accept header.</param>
        /// <param name="ContentType">An optional HTTP content type.</param>
        /// <param name="HTTPAuthentication">The optional HTTP authentication to use, e.g. HTTP Basic Auth.</param>
        /// <param name="HTTPUserAgent">The HTTP user agent identification.</param>
        /// <param name="Connection">An optional connection type.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for HTTP request.</param>
        /// <param name="InternalBufferSize">An optional size of the internal buffers.</param>
        /// <param name="UseHTTPPipelining">Whether to pipeline multiple HTTP request through a single HTTP/TCP connection.</param>
        /// <param name="DisableLogging">Disable logging.</param>
        /// <param name="HTTPLogger">An HTTP logger.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        public HTTPClient(IPSocket                         RemoteSocket,
                          HTTPHostname?                    VirtualHostname          = null,
                          I18NString?                      Description              = null,
                          Boolean?                         PreferIPv4               = null,
                          AcceptTypes?                     Accept                   = null,
                          HTTPContentType?                 ContentType              = null,
                          IHTTPAuthentication?             HTTPAuthentication       = null,
                          String?                          HTTPUserAgent            = DefaultHTTPUserAgent,
                          ConnectionType?                  Connection               = null,
                          TimeSpan?                        RequestTimeout           = null,
                          TransmissionRetryDelayDelegate?  TransmissionRetryDelay   = null,
                          UInt16?                          MaxNumberOfRetries       = null,
                          UInt32?                          InternalBufferSize       = null,
                          Boolean                          UseHTTPPipelining        = false,
                          Boolean?                         DisableLogging           = false,
                          HTTPClientLogger?                HTTPLogger               = null,
                          DNSClient?                       DNSClient                = null)

            : this(URL.Parse($"http://{RemoteSocket.IPAddress}:{RemoteSocket.Port}"),
                   VirtualHostname,
                   Description,
                   PreferIPv4,
                   ContentType,
                   Accept,
                   HTTPAuthentication,
                   HTTPUserAgent ?? DefaultHTTPUserAgent,
                   Connection,
                   RequestTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   InternalBufferSize,
                   UseHTTPPipelining,
                   DisableLogging,
                   HTTPLogger,
                   DNSClient)

        { }

        #endregion

        #region HTTPClient(RemoteHost, ...)

        /// <summary>
        /// Create a new HTTP client.
        /// </summary>
        /// <param name="RemoteHost">The remote hostname to connect to.</param>
        /// <param name="RemotePort">An optional remote TCP port to connect to.</param>
        /// <param name="VirtualHostname">An optional HTTP virtual hostname.</param>
        /// <param name="Description">An optional description of this CPO client.</param>
        /// <param name="PreferIPv4">Prefer IPv4 instead of IPv6.</param>
        /// <param name="ContentType">An optional HTTP content type.</param>
        /// <param name="Accept">The optional HTTP accept header.</param>
        /// <param name="HTTPAuthentication">The optional HTTP authentication to use, e.g. HTTP Basic Auth.</param>
        /// <param name="HTTPUserAgent">The HTTP user agent identification.</param>
        /// <param name="Connection">An optional connection type.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for HTTP request.</param>
        /// <param name="InternalBufferSize">An optional size of the internal buffers.</param>
        /// <param name="UseHTTPPipelining">Whether to pipeline multiple HTTP request through a single HTTP/TCP connection.</param>
        /// <param name="DisableLogging">Disable logging.</param>
        /// <param name="HTTPLogger">An HTTP logger.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        public HTTPClient(HTTPHostname                     RemoteHost,
                          IPPort?                          RemotePort               = null,
                          HTTPHostname?                    VirtualHostname          = null,
                          I18NString?                      Description              = null,
                          Boolean?                         PreferIPv4               = null,
                          HTTPContentType?                 ContentType              = null,
                          AcceptTypes?                     Accept                   = null,
                          IHTTPAuthentication?             HTTPAuthentication       = null,
                          String?                          HTTPUserAgent            = DefaultHTTPUserAgent,
                          ConnectionType?                  Connection               = null,
                          TimeSpan?                        RequestTimeout           = null,
                          TransmissionRetryDelayDelegate?  TransmissionRetryDelay   = null,
                          UInt16?                          MaxNumberOfRetries       = null,
                          UInt32?                          InternalBufferSize       = null,
                          Boolean                          UseHTTPPipelining        = false,
                          Boolean?                         DisableLogging           = false,
                          HTTPClientLogger?                HTTPLogger               = null,
                          DNSClient?                       DNSClient                = null)

            : this(URL.Parse($"http://{RemoteHost}{(RemotePort.HasValue ? ":" + RemotePort.Value.ToString() : String.Empty)}"),
                   VirtualHostname,
                   Description,
                   PreferIPv4,
                   ContentType,
                   Accept,
                   HTTPAuthentication,
                   HTTPUserAgent ?? DefaultHTTPUserAgent,
                   Connection,
                   RequestTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   InternalBufferSize,
                   UseHTTPPipelining,
                   DisableLogging,
                   HTTPLogger,
                   DNSClient)

        { }

        #endregion


    }

}
