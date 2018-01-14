/*
 * Copyright (c) 2010-2018, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
using System.Linq;
using System.Xml.Linq;
using System.Threading;
using System.Net.Security;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SOAP
{

    public delegate T        CustomXMLParserDelegate<T>(XElement XML, T Data);

    public delegate XElement CustomXMLSerializerDelegate<T>(T ResponseBuilder, XElement XML);

    public delegate XElement XMLNamespacesDelegate(XElement XML);

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
        /// SOAP Adressing extentions.
        /// </summary>
        public static readonly XNamespace SOAPAdressing  = "http://www.w3.org/2005/08/addressing";

    }

    /// <summary>
    /// A specialized HTTP client for the Simple Object Access Protocol (SOAP) v1.1.
    /// </summary>
    public class SOAPClient : HTTPClient
    {

        #region Data

        /// <summary>
        /// The default HTTP/SOAP user agent.
        /// </summary>
        public new const String DefaultUserAgent  = "GraphDefined HTTP/SOAP Client";

        #endregion

        #region Properties

        /// <summary>
        /// The HTTP virtual host to use.
        /// </summary>
        public String  HTTPVirtualHost   { get; }

        /// <summary>
        /// The URI-prefix of the HTTP/SOAP service.
        /// </summary>
        public String  URIPrefix         { get; }

        #endregion

        #region Constructor

        /// <summary>
        /// Create a new specialized HTTP client for the Simple Object Access Protocol (SOAP).
        /// </summary>
        /// <param name="Hostname">The hostname of the remote HTTP/SOAP service.</param>
        /// <param name="HTTPPort">The HTTP port of the remote HTTP/SOAP service.</param>
        /// <param name="HTTPVirtualHost">The HTTP virtual host to use.</param>
        /// <param name="URIPrefix">The URI-prefix of the HTTP/SOAP service.</param>
        /// <param name="RemoteCertificateValidator">A delegate to verify the remote TLS certificate.</param>
        /// <param name="LocalCertificateSelector">Selects the local certificate used for authentication.</param>
        /// <param name="UserAgent">The HTTP user agent to use.</param>
        /// <param name="RequestTimeout">An optional default HTTP request timeout.</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        public SOAPClient(String                               Hostname,
                          IPPort                               HTTPPort,
                          String                               HTTPVirtualHost,
                          String                               URIPrefix,
                          RemoteCertificateValidationCallback  RemoteCertificateValidator   = null,
                          LocalCertificateSelectionCallback    LocalCertificateSelector     = null,
                          String                               UserAgent                    = DefaultUserAgent,
                          TimeSpan?                            RequestTimeout               = null,
                          DNSClient                            DNSClient                    = null)

            : base(Hostname,
                   HTTPPort       ?? DefaultHTTPPort,
                   RemoteCertificateValidator,
                   LocalCertificateSelector,
                   null,
                   UserAgent      ?? DefaultUserAgent,
                   RequestTimeout ?? DefaultRequestTimeout,
                   DNSClient)

        {

            this.HTTPVirtualHost  = HTTPVirtualHost;
            this.URIPrefix        = URIPrefix.IsNotNullOrEmpty() ? URIPrefix : "/";

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
        /// <param name="OnSOAPFault">The delegate to call whenever a SOAP fault occured.</param>
        /// <param name="OnHTTPError">The delegate to call whenever a HTTP error occured.</param>
        /// <param name="OnException">The delegate to call whenever an exception occured.</param>
        /// <param name="RequestTimeout">An optional timeout of the HTTP client [default 60 sec.]</param>
        /// <param name="NumberOfRetry">The number of retransmissions of this request.</param>
        /// <returns>The data structured after it had been processed by the OnSuccess delegate, or a fault.</returns>
        public async Task<HTTPResponse<T>>

            Query<T>(XElement                                                         QueryXML,
                     String                                                           SOAPAction,
                     Func<HTTPResponse<XElement>,                   HTTPResponse<T>>  OnSuccess,
                     Func<DateTime, Object, HTTPResponse<XElement>, HTTPResponse<T>>  OnSOAPFault,
                     Func<DateTime, Object, HTTPResponse,           HTTPResponse<T>>  OnHTTPError,
                     Func<DateTime, Object, Exception,              HTTPResponse<T>>  OnException,
                     Action<HTTPRequest.Builder>                                      HTTPRequestBuilder    = null,
                     ClientRequestLogHandler                                          RequestLogDelegate    = null,
                     ClientResponseLogHandler                                         ResponseLogDelegate   = null,
                     HTTPContentType                                                  ContentType           = null,
                     XNamespace                                                       Namespace             = null,

                     CancellationToken?                                               CancellationToken     = null,
                     EventTracking_Id                                                 EventTrackingId       = null,
                     TimeSpan?                                                        RequestTimeout        = null,
                     Byte                                                             NumberOfRetry         = 0)

        {

            #region Initial checks

            if (QueryXML    == null)
                throw new ArgumentNullException(nameof(QueryXML),     "The 'Query'-string must not be null!");

            if (SOAPAction.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(SOAPAction),   "The 'SOAPAction'-string must not be null or empty!");

            if (OnSuccess   == null)
                throw new ArgumentNullException(nameof(OnSuccess),    "The 'OnSuccess'-delegate must not be null!");

            if (OnSOAPFault == null)
                throw new ArgumentNullException(nameof(OnSOAPFault),  "The 'OnSOAPFault'-delegate must not be null!");

            if (OnHTTPError == null)
                throw new ArgumentNullException(nameof(OnHTTPError),  "The 'OnHTTPError'-delegate must not be null!");

            if (OnException == null)
                throw new ArgumentNullException(nameof(OnException),  "The 'OnException'-delegate must not be null!");

            #endregion

            var _RequestBuilder = new HTTPRequest.Builder(this) {
                                      HTTPMethod         = HTTPMethod.POST,
                                      Host               = HTTPVirtualHost,
                                      URI                = URIPrefix,
                                      Content            = QueryXML.ToUTF8Bytes(),
                                      ContentType        = ContentType ?? HTTPContentType.XMLTEXT_UTF8,
                                      UserAgent          = UserAgent,
                                      FakeURIPrefix      = "https://" + HTTPVirtualHost
                                  };

            // Always send a Content-Length header, even when it's value is zero
            _RequestBuilder.SetContentLength(0);

            _RequestBuilder.Set("SOAPAction", @"""" + SOAPAction + @"""");


            HTTPRequestBuilder?.Invoke(_RequestBuilder);

            var HttpResponse = await Execute(_RequestBuilder,
                                             RequestLogDelegate,
                                             ResponseLogDelegate,
                                             CancellationToken.HasValue ? CancellationToken.Value : new CancellationTokenSource().Token,
                                             EventTrackingId,
                                             RequestTimeout ?? DefaultRequestTimeout,
                                             NumberOfRetry);


            if (HttpResponse                 != null              &&
                HttpResponse.HTTPStatusCode  == HTTPStatusCode.OK &&
                HttpResponse.HTTPBody        != null              &&
                HttpResponse.HTTPBody.Length > 0)
            {

                try
                {

                    var SOAPXML = XDocument.Parse(HttpResponse.HTTPBody.ToUTF8String()).
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
                        if (OnSuccessLocal != null)
                            return OnSuccessLocal(new HTTPResponse<XElement>(HttpResponse, SOAPXML));

                    }

                    var OnSOAPFaultLocal = OnSOAPFault;
                    if (OnSOAPFaultLocal != null)
                        return OnSOAPFaultLocal(DateTime.UtcNow, this, new HTTPResponse<XElement>(HttpResponse, SOAPXML));

                    return new HTTPResponse<XElement>(HttpResponse,
                                                      new XElement("SOAPFault"),
                                                      IsFault: true) as HTTPResponse<T>;


                } catch (Exception e)
                {

                    OnException?.Invoke(DateTime.UtcNow, this, e);

                    //var OnFaultLocal = OnSOAPFault;
                    //if (OnFaultLocal != null)
                    //    return OnFaultLocal(new HTTPResponse<XElement>(HttpResponseTask.Result, e));

                    return new HTTPResponse<XElement>(HttpResponse,
                                                      new XElement("exception", e.Message),
                                                      IsFault: true) as HTTPResponse<T>;

                }

            }

            else
            {

                var OnHTTPErrorLocal = OnHTTPError;
                if (OnHTTPErrorLocal != null)
                    return OnHTTPErrorLocal(DateTime.UtcNow, this, HttpResponse);

                return new HTTPResponse<XElement>(HttpResponse,
                                                  new XElement("HTTPError"),
                                                  IsFault: true) as HTTPResponse<T>;

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
        /// SOAP Adressing extentions.
        /// </summary>
        public static readonly XNamespace SOAPAdressing  = "http://www.w3.org/2005/08/addressing";

    }

    /// <summary>
    /// A specialized HTTP client for the Simple Object Access Protocol (SOAP) v1.2.
    /// </summary>
    public class SOAPClient : HTTPClient
    {

        #region Data

        /// <summary>
        /// The default HTTP/SOAP user agent.
        /// </summary>
        public new const String DefaultUserAgent  = "GraphDefined HTTP/SOAPv1.2 Client";

        #endregion

        #region Properties

        /// <summary>
        /// The HTTP virtual host to use.
        /// </summary>
        public String  HTTPVirtualHost   { get; }

        /// <summary>
        /// The URI-prefix of the HTTP/SOAP service.
        /// </summary>
        public String  URIPrefix         { get; }

        #endregion

        #region Constructor

        /// <summary>
        /// Create a new specialized HTTP client for the Simple Object Access Protocol (SOAP).
        /// </summary>
        /// <param name="Hostname">The hostname of the remote HTTP/SOAP service.</param>
        /// <param name="HTTPPort">The HTTP port of the remote HTTP/SOAP service.</param>
        /// <param name="HTTPVirtualHost">The HTTP virtual host to use.</param>
        /// <param name="URIPrefix">The URI-prefix of the HTTP/SOAP service.</param>
        /// <param name="RemoteCertificateValidator">A delegate to verify the remote TLS certificate.</param>
        /// <param name="ClientCertificateSelector">A delegate to select a TLS client certificate.</param>
        /// <param name="UserAgent">The HTTP user agent to use.</param>
        /// <param name="RequestTimeout">An optional default HTTP request timeout.</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        public SOAPClient(String                               Hostname,
                          IPPort                               HTTPPort,
                          String                               HTTPVirtualHost,
                          String                               URIPrefix,
                          RemoteCertificateValidationCallback  RemoteCertificateValidator   = null,
                          LocalCertificateSelectionCallback    ClientCertificateSelector    = null,
                          String                               UserAgent                    = DefaultUserAgent,
                          TimeSpan?                            RequestTimeout               = null,
                          DNSClient                            DNSClient                    = null)

            : base(Hostname,
                   HTTPPort       ?? DefaultHTTPPort,
                   RemoteCertificateValidator,
                   ClientCertificateSelector,
                   null,
                   UserAgent      ?? DefaultUserAgent,
                   RequestTimeout ?? DefaultRequestTimeout,
                   DNSClient)

        {

            this.HTTPVirtualHost  = HTTPVirtualHost;
            this.URIPrefix        = URIPrefix.IsNotNullOrEmpty() ? URIPrefix : "/";

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
        /// <param name="OnSOAPFault">The delegate to call whenever a SOAP fault occured.</param>
        /// <param name="OnHTTPError">The delegate to call whenever a HTTP error occured.</param>
        /// <param name="OnException">The delegate to call whenever an exception occured.</param>
        /// <param name="RequestTimeout">An optional timeout of the HTTP client [default 60 sec.]</param>
        /// <param name="NumberOfRetry">The number of retransmissions of this request.</param>
        /// <returns>The data structured after it had been processed by the OnSuccess delegate, or a fault.</returns>
        public async Task<HTTPResponse<T>>

            Query<T>(XElement                                                         QueryXML,
                     String                                                           SOAPAction,
                     Func<HTTPResponse<XElement>,                   HTTPResponse<T>>  OnSuccess,
                     Func<DateTime, Object, HTTPResponse<XElement>, HTTPResponse<T>>  OnSOAPFault,
                     Func<DateTime, Object, HTTPResponse,           HTTPResponse<T>>  OnHTTPError,
                     Func<DateTime, Object, Exception,              HTTPResponse<T>>  OnException,
                     Action<HTTPRequest.Builder>                                      HTTPRequestBuilder    = null,
                     ClientRequestLogHandler                                          RequestLogDelegate    = null,
                     ClientResponseLogHandler                                         ResponseLogDelegate   = null,
                     HTTPContentType                                                  ContentType           = null,
                     XNamespace                                                       Namespace             = null,

                     CancellationToken?                                               CancellationToken     = null,
                     EventTracking_Id                                                 EventTrackingId       = null,
                     TimeSpan?                                                        RequestTimeout        = null,
                     Byte                                                             NumberOfRetry         = 0)

        {

            #region Initial checks

            if (QueryXML    == null)
                throw new ArgumentNullException(nameof(QueryXML),     "The 'Query'-string must not be null!");

            if (SOAPAction.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(SOAPAction),   "The 'SOAPAction'-string must not be null or empty!");

            if (OnSuccess   == null)
                throw new ArgumentNullException(nameof(OnSuccess),    "The 'OnSuccess'-delegate must not be null!");

            if (OnSOAPFault == null)
                throw new ArgumentNullException(nameof(OnSOAPFault),  "The 'OnSOAPFault'-delegate must not be null!");

            if (OnHTTPError == null)
                throw new ArgumentNullException(nameof(OnHTTPError),  "The 'OnHTTPError'-delegate must not be null!");

            if (OnException == null)
                throw new ArgumentNullException(nameof(OnException),  "The 'OnException'-delegate must not be null!");

            #endregion

            var _RequestBuilder = new HTTPRequest.Builder(this) {
                                      HTTPMethod         = HTTPMethod.POST,
                                      Host               = HTTPVirtualHost,
                                      URI                = URIPrefix,
                                      Content            = QueryXML.ToUTF8Bytes(),
                                      ContentType        = ContentType ?? HTTPContentType.XMLTEXT_UTF8,
                                      UserAgent          = UserAgent,
                                      FakeURIPrefix      = "https://" + HTTPVirtualHost
                                  };

            // Always send a Content-Length header, even when it's value is zero
            _RequestBuilder.SetContentLength(0);

            _RequestBuilder.Set("SOAPAction", @"""" + SOAPAction + @"""");


            HTTPRequestBuilder?.Invoke(_RequestBuilder);

            var HttpResponse = await Execute(_RequestBuilder,
                                             RequestLogDelegate,
                                             ResponseLogDelegate,
                                             CancellationToken.HasValue ? CancellationToken.Value : new CancellationTokenSource().Token,
                                             EventTrackingId,
                                             RequestTimeout ?? DefaultRequestTimeout,
                                             NumberOfRetry);


            if (HttpResponse                 != null              &&
                HttpResponse.HTTPStatusCode  == HTTPStatusCode.OK &&
                HttpResponse.HTTPBody        != null              &&
                HttpResponse.HTTPBody.Length > 0)
            {

                try
                {

                    var SOAPXML = XDocument.Parse(HttpResponse.HTTPBody.ToUTF8String()).
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
                        if (OnSuccessLocal != null)
                            return OnSuccessLocal(new HTTPResponse<XElement>(HttpResponse, SOAPXML));

                    }

                    var OnSOAPFaultLocal = OnSOAPFault;
                    if (OnSOAPFaultLocal != null)
                        return OnSOAPFaultLocal(DateTime.UtcNow, this, new HTTPResponse<XElement>(HttpResponse, SOAPXML));

                    return new HTTPResponse<XElement>(HttpResponse,
                                                      new XElement("SOAPFault"),
                                                      IsFault: true) as HTTPResponse<T>;


                } catch (Exception e)
                {

                    OnException?.Invoke(DateTime.UtcNow, this, e);

                    //var OnFaultLocal = OnSOAPFault;
                    //if (OnFaultLocal != null)
                    //    return OnFaultLocal(new HTTPResponse<XElement>(HttpResponseTask.Result, e));

                    return new HTTPResponse<XElement>(HttpResponse,
                                                      new XElement("exception", e.Message),
                                                      IsFault: true) as HTTPResponse<T>;

                }

            }

            else
            {

                var OnHTTPErrorLocal = OnHTTPError;
                if (OnHTTPErrorLocal != null)
                    return OnHTTPErrorLocal(DateTime.UtcNow, this, HttpResponse);

                return new HTTPResponse<XElement>(HttpResponse,
                                                  new XElement("HTTPError"),
                                                  IsFault: true) as HTTPResponse<T>;

            }

        }

        #endregion


    }

}
