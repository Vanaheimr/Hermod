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

using System.Xml.Linq;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SOAP
{

    /// <summary>
    /// An abstract HTTP/SOAP client.
    /// </summary>
    public abstract class ASOAPClient : AHTTPClient
    {

        #region Data

        /// <summary>
        /// The default HTTP user agent.
        /// </summary>
        public new const          String    DefaultHTTPUserAgent  = "GraphDefined HTTP/SOAP Client";

        /// <summary>
        /// The default URL path prefix.
        /// </summary>
        protected static readonly HTTPPath  DefaultURLPathPrefix  = HTTPPath.Root;

        #endregion

        #region Properties

        /// <summary>
        /// The default URL path prefix.
        /// </summary>
        public HTTPPath                URLPathPrefix       { get; }

        /// <summary>
        /// The WebService-Security username/password.
        /// </summary>
        public Tuple<String, String>?  WSSLoginPassword    { get; }

        #endregion

        #region Events

        /// <summary>
        /// A delegate called whenever a SOAP error occured.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the error.</param>
        /// <param name="Sender">The sender of the error.</param>
        /// <param name="SOAPXML">The SOAP error message.</param>
        public delegate void OnSOAPErrorDelegate(DateTime Timestamp, Object Sender, XElement SOAPXML);

        /// <summary>
        /// An event fired whenever a SOAP error occured.
        /// </summary>
        public event OnSOAPErrorDelegate? OnSOAPError;


        /// <summary>
        /// An event fired whenever an exception occured.
        /// </summary>
        public event OnExceptionDelegate? OnException;


        /// <summary>
        /// A delegate called whenever a HTTP error occured.
        /// </summary>
        public delegate void OnHTTPErrorDelegate(DateTime Timestamp, Object Sender, HTTPResponse HttpResponse);

        /// <summary>
        /// An event fired whenever a HTTP error occured.
        /// </summary>
        public event OnHTTPErrorDelegate? OnHTTPError;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new abstract HTTP/SOAP client.
        /// </summary>
        /// <param name="RemoteURL">The remote URL of the HTTP endpoint to connect to.</param>
        /// <param name="VirtualHostname">An optional HTTP virtual hostname.</param>
        /// <param name="Description">An optional description of this HTTP/SOAP client.</param>
        /// <param name="PreferIPv4">Prefer IPv4 instead of IPv6.</param>
        /// <param name="RemoteCertificateValidator">The remote TLS certificate validator.</param>
        /// <param name="LocalCertificateSelector">A delegate to select a TLS client certificate.</param>
        /// <param name="ClientCert">The TLS client certificate to use of HTTP authentication.</param>
        /// <param name="TLSProtocol">The TLS protocol to use.</param>
        /// <param name="ContentType">An optional HTTP content type.</param>
        /// <param name="Accept">The optional HTTP accept header.</param>
        /// <param name="HTTPAuthentication">The optional HTTP authentication to use, e.g. HTTP Basic Auth.</param>
        /// <param name="HTTPUserAgent">The HTTP user agent identification.</param>
        /// <param name="URLPathPrefix">An optional default URL path prefix.</param>
        /// <param name="WSSLoginPassword">The WebService-Security username/password.</param>
        /// <param name="HTTPContentType">The HTTP content type to use.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for HTTP request.</param>
        /// <param name="InternalBufferSize">The internal buffer size.</param>
        /// <param name="UseHTTPPipelining">Whether to pipeline multiple HTTP request through a single HTTP/TCP connection.</param>
        /// <param name="DisableLogging">Disable HTTP logging.</param>
        /// <param name="HTTPLogger">A HTTP logger.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        protected ASOAPClient(URL                                                        RemoteURL,
                              HTTPHostname?                                              VirtualHostname              = null,
                              I18NString?                                                Description                  = null,
                              Boolean?                                                   PreferIPv4                   = null,
                              RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidator   = null,
                              LocalCertificateSelectionHandler?                          LocalCertificateSelector     = null,
                              X509Certificate?                                           ClientCert                   = null,
                              SslProtocols?                                              TLSProtocol                  = null,
                              HTTPContentType?                                           ContentType                  = null,
                              AcceptTypes?                                               Accept                       = null,
                              IHTTPAuthentication?                                       HTTPAuthentication           = null,
                              String?                                                    HTTPUserAgent                = DefaultHTTPUserAgent,
                              HTTPPath?                                                  URLPathPrefix                = null,
                              Tuple<String, String>?                                     WSSLoginPassword             = null,
                              ConnectionType?                                            Connection                   = null,
                              TimeSpan?                                                  RequestTimeout               = null,
                              TransmissionRetryDelayDelegate?                            TransmissionRetryDelay       = null,
                              UInt16?                                                    MaxNumberOfRetries           = null,
                              UInt32?                                                    InternalBufferSize           = null,
                              Boolean                                                    UseHTTPPipelining            = false,
                              Boolean?                                                   DisableLogging               = false,
                              HTTPClientLogger?                                          HTTPLogger                   = null,
                              DNSClient?                                                 DNSClient                    = null)

            : base(RemoteURL,
                   VirtualHostname,
                   Description,
                   PreferIPv4,
                   RemoteCertificateValidator,
                   LocalCertificateSelector,
                   ClientCert,
                   TLSProtocol,
                   ContentType ?? HTTPContentType.Application.SOAPXML_UTF8,
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

        {

            this.URLPathPrefix     = URLPathPrefix ?? DefaultURLPathPrefix;
            this.WSSLoginPassword  = WSSLoginPassword;

        }

        #endregion


        #region (protected) SendSOAPError(Timestamp, Sender, SOAPXML)

        /// <summary>
        /// Notify that an HTTP error occured.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the error received.</param>
        /// <param name="Sender">The sender of this error message.</param>
        /// <param name="SOAPXML">The SOAP fault/error.</param>
        protected void SendSOAPError(DateTime  Timestamp,
                                     Object    Sender,
                                     XElement  SOAPXML)
        {

            DebugX.Log("ASOAPClient => SOAP Fault: " + SOAPXML != null ? SOAPXML.ToString() : "<null>");

            OnSOAPError?.Invoke(Timestamp, Sender, SOAPXML);

        }

        #endregion

        #region (protected) SendHTTPError(Timestamp, Sender, HTTPResponse)

        /// <summary>
        /// Notify that an HTTP error occured.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the error received.</param>
        /// <param name="Sender">The sender of this error message.</param>
        /// <param name="HTTPResponse">The HTTP response related to this error message.</param>
        protected void SendHTTPError(DateTime      Timestamp,
                                     Object        Sender,
                                     HTTPResponse  HTTPResponse)
        {

            DebugX.Log("ASOAPClient => HTTP Status Code: " + HTTPResponse is not null
                                                                 ? HTTPResponse.HTTPStatusCode.ToString()
                                                                 : "<null>");

            OnHTTPError?.Invoke(Timestamp, Sender, HTTPResponse);

        }

        #endregion

        #region (protected) SendException(Timestamp, Sender, Exception)

        /// <summary>
        /// Notify that an exception occured.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the exception.</param>
        /// <param name="Sender">The sender of this exception.</param>
        /// <param name="Exception">The exception itself.</param>
        protected void SendException(DateTime   Timestamp,
                                     Object     Sender,
                                     Exception  Exception)
        {

            DebugX.Log("ASOAPClient => Exception: " + Exception.Message);

            OnException?.Invoke(Timestamp, Sender, Exception);

        }

        #endregion


    }

}
