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

using System.Xml.Linq;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using System.Net.Security;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SOAP
{

    public delegate T        CustomXMLParserDelegate<T>     (XElement XML,              T        Data);

    public delegate XElement CustomXMLSerializerDelegate<T> (T        ResponseBuilder,  XElement XML);

    public delegate XElement XMLNamespacesDelegate          (XElement XML);

}

namespace org.GraphDefined.Vanaheimr.Hermod.SOAP.v1_1
{

    /// <summary>
    /// SOAP v1.1 XML Namespace
    /// </summary>
    public static class NS
    {

        /// <summary>
        /// The namespace for the XML SOAP v1.1 envelope.
        /// </summary>
        public static readonly XNamespace SOAPEnvelope   = "http://schemas.xmlsoap.org/soap/envelope/";

        /// <summary>
        /// SOAP Adressing extensions.
        /// </summary>
        public static readonly XNamespace SOAPAdressing  = "http://www.w3.org/2005/08/addressing";

    }


    /// <summary>
    /// A specialized HTTP client for the Simple Object Access Protocol (SOAP) v1.1.
    /// </summary>
    public class SOAPClient : ASOAPClient
    {

        #region Data

        /// <summary>
        /// The default HTTP/SOAP user agent.
        /// </summary>
        public new const String  DefaultHTTPUserAgent  = "GraphDefined HTTP/SOAPv1.1 Client";

        #endregion

        #region Constructor

        /// <summary>
        /// Create a new specialized HTTP client for the Simple Object Access Protocol (SOAP) v1.1.
        /// </summary>
        /// <param name="RemoteURL">The remote URL of the OICP HTTP endpoint to connect to.</param>
        /// <param name="VirtualHostname">An optional HTTP virtual hostname.</param>
        /// <param name="Description">An optional description of this CPO client.</param>
        /// <param name="PreferIPv4">Prefer IPv4 instead of IPv6.</param>
        /// <param name="RemoteCertificateValidator">The remote TLS certificate validator.</param>
        /// <param name="LocalCertificateSelector">A delegate to select a TLS client certificate.</param>
        /// <param name="ClientCert">The TLS client certificate to use for HTTP authentication.</param>
        /// <param name="TLSProtocol">The TLS protocol to use.</param>
        /// <param name="ContentType">An optional HTTP content type.</param>
        /// <param name="Accept">The optional HTTP accept header.</param>
        /// <param name="HTTPAuthentication">The optional HTTP authentication to use, e.g. HTTP Basic Auth.</param>
        /// <param name="HTTPUserAgent">The HTTP user agent identification.</param>
        /// <param name="URLPathPrefix">An optional default URL path prefix.</param>
        /// <param name="WSSLoginPassword">The WebService-Security username/password.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for HTTP request.</param>
        /// <param name="InternalBufferSize">An optional size of the internal buffers.</param>
        /// <param name="UseHTTPPipelining">Whether to pipeline multiple HTTP request through a single HTTP/TCP connection.</param>
        /// <param name="DisableLogging">Disable logging.</param>
        /// <param name="HTTPLogger">An HTTP logger.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        public SOAPClient(URL                                                        RemoteURL,
                          HTTPHostname?                                              VirtualHostname              = null,
                          I18NString?                                                Description                  = null,
                          Boolean?                                                   PreferIPv4                   = null,
                          RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidator   = null,
                          LocalCertificateSelectionHandler?                          LocalCertificateSelector     = null,
                          IEnumerable<X509Certificate2>?                             ClientCertificates           = null,
                          SslStreamCertificateContext?                               ClientCertificateContext     = null,
                          IEnumerable<X509Certificate2>?                             ClientCertificateChain       = null,
                          SslProtocols?                                              TLSProtocol                  = null,
                          HTTPContentType?                                           ContentType                  = null,
                          AcceptTypes?                                               Accept                       = null,
                          IHTTPAuthentication?                                       HTTPAuthentication           = null,
                          TOTPConfig?                                                TOTPConfig                   = null,
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
                          IDNSClient?                                                DNSClient                    = null)

            : base(RemoteURL,
                   VirtualHostname,
                   Description,
                   PreferIPv4,
                   RemoteCertificateValidator,
                   LocalCertificateSelector,
                   ClientCertificates,
                   ClientCertificateContext,
                   ClientCertificateChain,
                   TLSProtocol,
                   ContentType,
                   Accept,
                   HTTPAuthentication,
                   TOTPConfig,
                   HTTPUserAgent,
                   URLPathPrefix,
                   WSSLoginPassword,
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


        #region Query(QueryXML, SOAPAction, OnSuccess, OnSOAPFault, OnHTTPError, OnException, TimeoutMSec = 60000)

        /// <summary>
        /// Create a new SOAP query task.
        /// </summary>
        /// <typeparam name="T">The type of the return data structure.</typeparam>
        /// <param name="QueryXML">The SOAP query XML.</param>
        /// <param name="SOAPAction">The SOAP action.</param>
        /// <param name="OnSuccess">The delegate to call for every successful result.</param>
        /// <param name="OnSOAPFault">The delegate to call whenever a SOAP fault occurred.</param>
        /// <param name="OnHTTPError">The delegate to call whenever a HTTP error occurred.</param>
        /// <param name="OnException">The delegate to call whenever an exception occurred.</param>
        /// <param name="RequestTimeout">An optional timeout of the HTTP client [default 60 sec.]</param>
        /// <param name="NumberOfRetry">The number of retransmissions of this request.</param>
        /// <returns>The data structured after it had been processed by the OnSuccess delegate, or a fault.</returns>
        public async Task<HTTPResponse<T>>

            Query<T>(XElement                                                               QueryXML,
                     String                                                                 SOAPAction,
                     Func<HTTPResponse<XElement>,                         HTTPResponse<T>>  OnSuccess,
                     Func<DateTimeOffset, Object, HTTPResponse<XElement>, HTTPResponse<T>>  OnSOAPFault,
                     Func<DateTimeOffset, Object, HTTPResponse,           HTTPResponse<T>>  OnHTTPError,
                     Func<DateTimeOffset, Object, Exception,              HTTPResponse<T>>  OnException,
                     Action<HTTPRequest.Builder>?                                           HTTPRequestBuilder    = null,
                     ClientRequestLogHandler?                                               RequestLogDelegate    = null,
                     ClientResponseLogHandler?                                              ResponseLogDelegate   = null,
                     HTTPContentType?                                                       ContentType           = null,
                     XNamespace?                                                            Namespace             = null,

                     EventTracking_Id?                                                      EventTrackingId       = null,
                     TimeSpan?                                                              RequestTimeout        = null,
                     Byte                                                                   NumberOfRetry         = 0,
                     CancellationToken                                                      CancellationToken     = default)

        {

            #region Initial checks

            if (QueryXML    is null)
                throw new ArgumentNullException(nameof(QueryXML),     "The 'Query'-string must not be null!");

            if (SOAPAction.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(SOAPAction),   "The 'SOAPAction'-string must not be null or empty!");

            if (OnSuccess   is null)
                throw new ArgumentNullException(nameof(OnSuccess),    "The 'OnSuccess'-delegate must not be null!");

            if (OnSOAPFault is null)
                throw new ArgumentNullException(nameof(OnSOAPFault),  "The 'OnSOAPFault'-delegate must not be null!");

            if (OnHTTPError is null)
                throw new ArgumentNullException(nameof(OnHTTPError),  "The 'OnHTTPError'-delegate must not be null!");

            if (OnException is null)
                throw new ArgumentNullException(nameof(OnException),  "The 'OnException'-delegate must not be null!");

            #endregion

            var requestBuilder = new HTTPRequest.Builder(this) {
                                     HTTPMethod         = HTTPMethod.POST,
                                     Host               = VirtualHostname ?? RemoteURL.Hostname,
                                     Path               = RemoteURL.Path,
                                     Accept             = AcceptTypes.FromHTTPContentTypes(HTTPContentType.Text.XML_UTF8),
                                     Content            = QueryXML.ToUTF8Bytes(),
                                     ContentType        = ContentType ?? HTTPContentType.Text.XML_UTF8,
                                     UserAgent          = HTTPUserAgent
                                    // FakeURLPrefix      = "https://" + (VirtualHostname ?? Hostname)
                                 };

            // Always send a Content-Length header, even when it's value is zero
            requestBuilder.SetContentLength(0);

            requestBuilder.Set("SOAPAction", @"""" + SOAPAction + @"""");


            HTTPRequestBuilder?.Invoke(requestBuilder);

            var httpResponse = await Execute(requestBuilder,
                                             RequestLogDelegate,
                                             ResponseLogDelegate,

                                             EventTrackingId,
                                             RequestTimeout ?? DefaultRequestTimeout,
                                             NumberOfRetry,
                                             CancellationToken);


            if (httpResponse                is not null          &&
                httpResponse.HTTPStatusCode == HTTPStatusCode.OK &&
                httpResponse.HTTPBody       is not null          &&
                httpResponse.HTTPBody.Length > 0)
            {

                try
                {

                    var SOAPXML = XDocument.Parse(httpResponse.HTTPBody.ToUTF8String()).
                                            Root.
                                            Element((Namespace ?? NS.SOAPEnvelope) + "Body").
                                            Descendants().
                                            FirstOrDefault();

                    // <S:Fault xmlns:ns4="http://www.w3.org/2003/05/soap-envelope" xmlns:S="http://schemas.xmlsoap.org/soap/envelope/">
                    //   <faultcode>S:Client</faultcode>
                    //   <faultstring>Validation error: The request message is invalid</faultstring>
                    //   <detail>
                    //     <Validation>
                    //       <Errors>
                    //         <Error column="65" errorXpath="/OICP:Envelope/OICP:Body/EVSEStatus:eRoamingPullEvseStatusById/EVSEStatus:EvseId" line="3">Value '+45*045*010*0A96296' is not facet-valid with respect to pattern '([A-Za-z]{2}\*?[A-Za-z0-9]{3}\*?E[A-Za-z0-9\*]{1,30})|(\+?[0-9]{1,3}\*[0-9]{3,6}\*[0-9\*]{1,32})' for type 'EvseIDType'.</Error>
                    //         <Error column="65" errorXpath="/OICP:Envelope/OICP:Body/EVSEStatus:eRoamingPullEvseStatusById/EVSEStatus:EvseId" line="3">The value '+45*045*010*0A96296' of element 'EVSEStatus:EvseId' is not valid.</Error>
                    //       </Errors>
                    //       <OriginalDocument>
                    //         ...
                    //       </OriginalDocument>
                    //     </Validation>
                    //   </detail>
                    // </S:Fault>

                    if (SOAPXML.Name.LocalName != "Fault")
                    {

                        var OnSuccessLocal = OnSuccess;
                        if (OnSuccessLocal is not null)
                            return OnSuccessLocal(new HTTPResponse<XElement>(httpResponse, SOAPXML));

                    }

                    var OnSOAPFaultLocal = OnSOAPFault;
                    if (OnSOAPFaultLocal is not null)
                        return OnSOAPFaultLocal(Timestamp.Now, this, new HTTPResponse<XElement>(httpResponse, SOAPXML));

                    return HTTPResponse<XElement>.IsFault(httpResponse,
                                                          new XElement("SOAPFault")) as HTTPResponse<T>;


                } catch (Exception e)
                {

                    OnException?.Invoke(Timestamp.Now, this, e);

                    //var OnFaultLocal = OnSOAPFault;
                    //if (OnFaultLocal is not null)
                    //    return OnFaultLocal(new HTTPResponse<XElement>(HttpResponseTask.Result, e));

                    return HTTPResponse<XElement>.IsFault(httpResponse,
                                                          new XElement("exception", e.Message)) as HTTPResponse<T>;

                }

            }

            else
            {

                var OnHTTPErrorLocal = OnHTTPError;
                if (OnHTTPErrorLocal is not null)
                    return OnHTTPErrorLocal(Timestamp.Now, this, httpResponse);

                return HTTPResponse<XElement>.IsFault(httpResponse,
                                                      new XElement("HTTPError")) as HTTPResponse<T>;

            }

        }

        #endregion


    }

}


namespace org.GraphDefined.Vanaheimr.Hermod.SOAP.v1_2
{

    /// <summary>
    /// SOAP v1.2 XML Namespace
    /// </summary>
    public static class NS
    {

        /// <summary>
        /// The namespace for the XML SOAP v1.2 envelope.
        /// </summary>
        public static readonly XNamespace SOAPEnvelope   = "http://www.w3.org/2003/05/soap-envelope";

        /// <summary>
        /// SOAP Adressing extensions.
        /// </summary>
        public static readonly XNamespace SOAPAdressing  = "http://www.w3.org/2005/08/addressing";

    }


    /// <summary>
    /// A specialized HTTP client for the Simple Object Access Protocol (SOAP) v1.2.
    /// </summary>
    public class SOAPClient : ASOAPClient
    {

        #region Data

        /// <summary>
        /// The default HTTP/SOAP user agent.
        /// </summary>
        public new const String  DefaultHTTPUserAgent  = "GraphDefined HTTP/SOAPv1.2 Client";

        #endregion

        #region Properties

        public Boolean  UseFakeURLPrefix    { get; }

        #endregion

        #region Constructor

        /// <summary>
        /// Create a new specialized HTTP client for the Simple Object Access Protocol (SOAP) v1.2.
        /// </summary>
        /// <param name="RemoteURL">The remote URL of the OICP HTTP endpoint to connect to.</param>
        /// <param name="VirtualHostname">An optional HTTP virtual hostname.</param>
        /// <param name="Description">An optional description of this CPO client.</param>
        /// <param name="PreferIPv4">Prefer IPv4 instead of IPv6.</param>
        /// <param name="RemoteCertificateValidator">The remote TLS certificate validator.</param>
        /// <param name="LocalCertificateSelector">A delegate to select a TLS client certificate.</param>
        /// <param name="ClientCert">The TLS client certificate to use for HTTP authentication.</param>
        /// <param name="Accept">The optional HTTP accept header.</param>
        /// <param name="HTTPAuthentication">The optional HTTP authentication to use, e.g. HTTP Basic Auth.</param>
        /// <param name="HTTPUserAgent">The HTTP user agent identification.</param>
        /// <param name="URLPathPrefix">An optional default URL path prefix.</param>
        /// <param name="WSSLoginPassword">The WebService-Security username/password.</param>
        /// <param name="HTTPContentType">The HTTP content type to use.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for HTTP request.</param>
        /// <param name="InternalBufferSize">An optional size of the internal buffers.</param>
        /// <param name="UseHTTPPipelining">Whether to pipeline multiple HTTP request through a single HTTP/TCP connection.</param>
        /// <param name="DisableLogging">Disable logging.</param>
        /// <param name="HTTPLogger">An HTTP logger.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        public SOAPClient(URL                                                        RemoteURL,
                          HTTPHostname?                                              VirtualHostname              = null,
                          Boolean                                                    UseFakeURLPrefix             = true,
                          I18NString?                                                Description                  = null,
                          Boolean?                                                   PreferIPv4                   = null,
                          RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidator   = null,
                          LocalCertificateSelectionHandler?                          LocalCertificateSelector     = null,
                          IEnumerable<X509Certificate2>?                             ClientCertificates           = null,
                          SslStreamCertificateContext?                               ClientCertificateContext     = null,
                          IEnumerable<X509Certificate2>?                             ClientCertificateChain       = null,
                          SslProtocols?                                              TLSProtocol                  = null,
                          HTTPContentType?                                           ContentType                  = null,
                          AcceptTypes?                                               Accept                       = null,
                          IHTTPAuthentication?                                       HTTPAuthentication           = null,
                          TOTPConfig?                                                TOTPConfig                   = null,
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
                          IDNSClient?                                                DNSClient                    = null)

            : base(RemoteURL,
                   VirtualHostname,
                   Description,
                   PreferIPv4,
                   RemoteCertificateValidator,
                   LocalCertificateSelector,
                   ClientCertificates,
                   ClientCertificateContext,
                   ClientCertificateChain,
                   TLSProtocol,
                   ContentType,
                   Accept,
                   HTTPAuthentication,
                   TOTPConfig,
                   HTTPUserAgent,
                   URLPathPrefix,
                   WSSLoginPassword,
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

            this.UseFakeURLPrefix  = UseFakeURLPrefix;

        }

        #endregion


        #region Query(QueryXML, SOAPAction, OnSuccess, OnSOAPFault, OnHTTPError, OnException, TimeoutMSec = 60000)

        /// <summary>
        /// Create a new SOAP query task.
        /// </summary>
        /// <typeparam name="T">The type of the return data structure.</typeparam>
        /// <param name="QueryXML">The SOAP query XML.</param>
        /// <param name="SOAPAction">The SOAP action.</param>
        /// <param name="OnSuccess">The delegate to call for every successful result.</param>
        /// <param name="OnSOAPFault">The delegate to call whenever a SOAP fault occurred.</param>
        /// <param name="OnHTTPError">The delegate to call whenever a HTTP error occurred.</param>
        /// <param name="OnException">The delegate to call whenever an exception occurred.</param>
        /// <param name="RequestTimeout">An optional timeout of the HTTP client [default 60 sec.]</param>
        /// <param name="NumberOfRetry">The number of retransmissions of this request.</param>
        /// <returns>The data structured after it had been processed by the OnSuccess delegate, or a fault.</returns>
        public async Task<HTTPResponse<T>>

            Query<T>(XElement                                                               QueryXML,
                     String                                                                 SOAPAction,
                     Func<HTTPResponse<XElement>,                         HTTPResponse<T>>  OnSuccess,
                     Func<DateTimeOffset, Object, HTTPResponse<XElement>, HTTPResponse<T>>  OnSOAPFault,
                     Func<DateTimeOffset, Object, HTTPResponse,           HTTPResponse<T>>  OnHTTPError,
                     Func<DateTimeOffset, Object, Exception,              HTTPResponse<T>>  OnException,
                     Action<HTTPRequest.Builder>?                                           HTTPRequestBuilder    = null,
                     ClientRequestLogHandler?                                               RequestLogDelegate    = null,
                     ClientResponseLogHandler?                                              ResponseLogDelegate   = null,
                     HTTPContentType?                                                       ContentType           = null,
                     XNamespace?                                                            Namespace             = null,

                     EventTracking_Id?                                                      EventTrackingId       = null,
                     TimeSpan?                                                              RequestTimeout        = null,
                     Byte                                                                   NumberOfRetry         = 0,
                     CancellationToken                                                      CancellationToken     = default)

        {

            #region Initial checks

            if (QueryXML    is null)
                throw new ArgumentNullException(nameof(QueryXML),     "The 'Query'-string must not be null!");

            if (SOAPAction.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(SOAPAction),   "The 'SOAPAction'-string must not be null or empty!");

            if (OnSuccess   is null)
                throw new ArgumentNullException(nameof(OnSuccess),    "The 'OnSuccess'-delegate must not be null!");

            if (OnSOAPFault is null)
                throw new ArgumentNullException(nameof(OnSOAPFault),  "The 'OnSOAPFault'-delegate must not be null!");

            if (OnHTTPError is null)
                throw new ArgumentNullException(nameof(OnHTTPError),  "The 'OnHTTPError'-delegate must not be null!");

            if (OnException is null)
                throw new ArgumentNullException(nameof(OnException),  "The 'OnException'-delegate must not be null!");

            #endregion

            var requestBuilder = new HTTPRequest.Builder(this) {
                                     HTTPMethod     = HTTPMethod.POST,
                                     Host           = VirtualHostname ?? HTTPHostname.Parse(RemoteURL.Hostname.ToString() + ":" + RemoteURL.Port.ToString()),
                                     Path           = URLPathPrefix + RemoteURL.Path,
                                     Content        = QueryXML.ToUTF8Bytes(),
                                     ContentType    = ContentType ?? this.ContentType ?? new HTTPContentType("application",
                                                                                                             "soap+xml",
                                                                                                             "utf-8",
                                                                                                             SOAPAction,
                                                                                                             null),
                                     Accept         = AcceptTypes.FromHTTPContentTypes(ContentType ?? this.ContentType ?? new HTTPContentType("application",
                                                                                                                                              "soap+xml",
                                                                                                                                              "utf-8",
                                                                                                                                              null,
                                                                                                                                              null)),
                                     UserAgent      = HTTPUserAgent,
                                     FakeURLPrefix  = UseFakeURLPrefix ? "https://" + (VirtualHostname ?? RemoteURL.Hostname) : null
                                 };

            // Always send a Content-Length header, even when it's value is zero
            requestBuilder.SetContentLength(0);

            HTTPRequestBuilder?.Invoke(requestBuilder);

            var httpResponse = await Execute(requestBuilder,
                                             RequestLogDelegate,
                                             ResponseLogDelegate,

                                             EventTrackingId,
                                             RequestTimeout ?? DefaultRequestTimeout,
                                             NumberOfRetry,
                                             CancellationToken);


            if (httpResponse                is not null          &&
                httpResponse.HTTPStatusCode == HTTPStatusCode.OK &&
                httpResponse.HTTPBody       is not null          &&
                httpResponse.HTTPBody.Length > 0)
            {

                try
                {

                    var SOAPXML = XDocument.Parse(httpResponse.HTTPBody.ToUTF8String()).
                                            Root.
                                            Element((Namespace ?? NS.SOAPEnvelope) + "Body").
                                            Descendants().
                                            FirstOrDefault();

                    // <S:Fault xmlns:ns4="http://www.w3.org/2003/05/soap-envelope" xmlns:S="http://schemas.xmlsoap.org/soap/envelope/">
                    //   <faultcode>S:Client</faultcode>
                    //   <faultstring>Validation error: The request message is invalid</faultstring>
                    //   <detail>
                    //     <Validation>
                    //       <Errors>
                    //         <Error column="65" errorXpath="/OICP:Envelope/OICP:Body/EVSEStatus:eRoamingPullEvseStatusById/EVSEStatus:EvseId" line="3">Value '+45*045*010*0A96296' is not facet-valid with respect to pattern '([A-Za-z]{2}\*?[A-Za-z0-9]{3}\*?E[A-Za-z0-9\*]{1,30})|(\+?[0-9]{1,3}\*[0-9]{3,6}\*[0-9\*]{1,32})' for type 'EvseIDType'.</Error>
                    //         <Error column="65" errorXpath="/OICP:Envelope/OICP:Body/EVSEStatus:eRoamingPullEvseStatusById/EVSEStatus:EvseId" line="3">The value '+45*045*010*0A96296' of element 'EVSEStatus:EvseId' is not valid.</Error>
                    //       </Errors>
                    //       <OriginalDocument>
                    //         ...
                    //       </OriginalDocument>
                    //     </Validation>
                    //   </detail>
                    // </S:Fault>

                    if (SOAPXML.Name.LocalName != "Fault")
                    {

                        var OnSuccessLocal = OnSuccess;
                        if (OnSuccessLocal is not null)
                            return OnSuccessLocal(new HTTPResponse<XElement>(httpResponse, SOAPXML));

                    }

                    var OnSOAPFaultLocal = OnSOAPFault;
                    if (OnSOAPFaultLocal is not null)
                        return OnSOAPFaultLocal(Timestamp.Now, this, new HTTPResponse<XElement>(httpResponse, SOAPXML));

                    return HTTPResponse<XElement>.IsFault(httpResponse,
                                                          new XElement("SOAPFault")) as HTTPResponse<T>;


                } catch (Exception e)
                {

                    OnException?.Invoke(Timestamp.Now, this, e);

                    //var OnFaultLocal = OnSOAPFault;
                    //if (OnFaultLocal is not null)
                    //    return OnFaultLocal(new HTTPResponse<XElement>(HttpResponseTask.Result, e));

                    return HTTPResponse<XElement>.IsFault(httpResponse,
                                                          new XElement("exception", e.Message)) as HTTPResponse<T>;

                }

            }

            else
            {

                var OnHTTPErrorLocal = OnHTTPError;
                if (OnHTTPErrorLocal is not null)
                    return OnHTTPErrorLocal(Timestamp.Now, this, httpResponse);

                return HTTPResponse<XElement>.IsFault(httpResponse,
                                                      new XElement("HTTPError")) as HTTPResponse<T>;

            }

        }

        #endregion


    }

}
