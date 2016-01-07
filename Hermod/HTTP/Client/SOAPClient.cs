/*
 * Copyright (c) 2010-2016, Achim 'ahzf' Friedland <achim@graph-database.org>
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
using System.Threading.Tasks;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using System.Threading;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SOAP
{

    #region SOAP XML Namespace

    /// <summary>
    /// SOAP XML Namespace
    /// </summary>
    public static class NS
    {

        /// <summary>
        /// The namespace for the XML SOAP Envelope.
        /// </summary>
        public static readonly XNamespace SOAPEnvelope = "http://schemas.xmlsoap.org/soap/envelope/";

    }

    #endregion

    #region SOAPClient

    /// <summary>
    /// A specialized HTTP client for the Simple Object Access Protocol (SOAP).
    /// </summary>
    public class SOAPClient : HTTPClient
    {

        #region Properties

        #region HTTPVirtualHost

        private readonly String _HTTPVirtualHost;

        /// <summary>
        /// The HTTP virtual host to use.
        /// </summary>
        public String HTTPVirtualHost
        {
            get
            {
                return _HTTPVirtualHost;
            }
        }

        #endregion

        #region URIPrefix

        private readonly String _URIPrefix;

        /// <summary>
        /// The URI-prefix of the OICP service.
        /// </summary>
        public String URIPrefix
        {
            get
            {
                return _URIPrefix;
            }
        }

        #endregion

        #region UserAgent

        private readonly String _UserAgent;

        /// <summary>
        /// The HTTP user agent.
        /// </summary>
        public String UserAgent
        {
            get
            {
                return _UserAgent;
            }
        }

        #endregion

        #endregion

        #region Constructor

        /// <summary>
        /// Create a new specialized HTTP client for the Simple Object Access Protocol (SOAP).
        /// </summary>
        /// <param name="SOAPHost">The hostname of the remote SOAP service.</param>
        /// <param name="SOAPPort">The TCP port of the remote SOAP service.</param>
        /// <param name="HTTPVirtualHost">The HTTP virtual host to use.</param>
        /// <param name="URIPrefix">The URI-prefix of the SOAP service.</param>
        /// <param name="UserAgent">The HTTP user agent to use.</param>
        /// <param name="UseTLS">Use transport layer security [default: false].</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        public SOAPClient(String     SOAPHost,
                          IPPort     SOAPPort,
                          String     HTTPVirtualHost,
                          String     URIPrefix,
                          String     UserAgent  = "GraphDefined SOAP-Client",
                          Boolean    UseTLS     = false,
                          DNSClient  DNSClient  = null)

            : base(SOAPHost, SOAPPort, UseTLS, DNSClient)

        {

            this._HTTPVirtualHost  = HTTPVirtualHost;
            this._URIPrefix        = URIPrefix;
            this._UserAgent        = UserAgent;

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
        /// <param name="QueryTimeout">An optional timeout of the HTTP client [default 60 sec.]</param>
        /// <returns>The data structured after it had been processed by the OnSuccess delegate, or a fault.</returns>
        public Task<HTTPResponse<T>> Query<T>(XElement                                                         QueryXML,
                                              String                                                           SOAPAction,
                                              Func<HTTPResponse<XElement>,                   HTTPResponse<T>>  OnSuccess,
                                              Func<DateTime, Object, HTTPResponse<XElement>, HTTPResponse<T>>  OnSOAPFault,
                                              Func<DateTime, Object, HTTPResponse,           HTTPResponse<T>>  OnHTTPError,
                                              Func<DateTime, Object, Exception,              HTTPResponse<T>>  OnException,
                                              CancellationToken?                                               CancellationToken  = null,
                                              TimeSpan?                                                        QueryTimeout       = null)

        {

            #region Initial checks

            if (QueryXML == null)
                throw new ArgumentException("The 'Query'-string must not be null!");

            if (SOAPAction.IsNullOrEmpty())
                throw new ArgumentException("The 'SOAPAction'-string must not be null or empty!");

            if (OnSuccess == null)
                throw new ArgumentException("The 'OnSuccess'-delegate must not be null!");

            if (OnSOAPFault == null)
                throw new ArgumentException("The 'OnSuccess'-delegate must not be null!");

            if (OnHTTPError == null)
                throw new ArgumentException("The 'OnHTTPError'-delegate must not be null!");

            if (OnException == null)
                throw new ArgumentException("The 'OnException'-delegate must not be null!");

            #endregion

            var builder = this.POST(_URIPrefix);
            builder.Host               = HTTPVirtualHost;
            builder.Content            = QueryXML.ToUTF8Bytes();
            builder.ContentType        = HTTPContentType.XMLTEXT_UTF8;
            builder.Set("SOAPAction",  @"""" + SOAPAction + @"""");
            builder.UserAgent          = UserAgent;
            builder.FakeURIPrefix      = "https://" + HTTPVirtualHost;

            return this.Execute(builder,
                                Timeout:            QueryTimeout != null ? QueryTimeout : TimeSpan.FromSeconds(60),
                                CancellationToken:  CancellationToken.HasValue ? CancellationToken.Value : new CancellationTokenSource().Token).

                        ContinueWith(HttpResponseTask => {

                            if (HttpResponseTask.Result                == null              ||
                                HttpResponseTask.Result.HTTPStatusCode != HTTPStatusCode.OK ||
                                HttpResponseTask.Result.HTTPBody        == null              ||
                                HttpResponseTask.Result.HTTPBody.Length == 0)
                            {

                                var OnHTTPErrorLocal = OnHTTPError;
                                if (OnHTTPErrorLocal != null)
                                    return OnHTTPErrorLocal(DateTime.Now, this, HttpResponseTask.Result);

                                return new HTTPResponse<XElement>(HttpResponseTask.Result,
                                                                  new XElement("HTTPError"),
                                                                  IsFault: true) as HTTPResponse<T>;

                            }

                            try
                            {

                                var SOAPXML = XDocument.Parse(HttpResponseTask.Result.HTTPBody.ToUTF8String()).
                                                        Root.
                                                        Element(NS.SOAPEnvelope + "Body").
                                                        Descendants().
                                                        FirstOrDefault();

                                // <S:Fault xmlns:ns4="http://www.w3.org/2003/05/soap-envelope" xmlns:S="http://schemas.xmlsoap.org/soap/envelope/">
                                //   <faultcode>S:Client</faultcode>
                                //   <faultstring>Validation error: The request message is invalid</faultstring>
                                //   <detail>
                                //     <Validation>
                                //       <Errors>
                                //         <Error column="65" errorXpath="/eMI3:Envelope/eMI3:Body/EVSEStatus:eRoamingPullEvseStatusById/EVSEStatus:EvseId" line="3">Value '+45*045*010*0A96296' is not facet-valid with respect to pattern '([A-Za-z]{2}\*?[A-Za-z0-9]{3}\*?E[A-Za-z0-9\*]{1,30})|(\+?[0-9]{1,3}\*[0-9]{3,6}\*[0-9\*]{1,32})' for type 'EvseIDType'.</Error>
                                //         <Error column="65" errorXpath="/eMI3:Envelope/eMI3:Body/EVSEStatus:eRoamingPullEvseStatusById/EVSEStatus:EvseId" line="3">The value '+45*045*010*0A96296' of element 'EVSEStatus:EvseId' is not valid.</Error>
                                //       </Errors>
                                //       <OriginalDocument>
                                //         <eMI3:Envelope xmlns:eMI3="http://schemas.xmlsoap.org/soap/envelope/" xmlns:Authorization="http://www.hubject.com/b2b/services/authorization/v1.2" xmlns:CommonTypes="http://www.hubject.com/b2b/services/commontypes/v1.2" xmlns:EVSEData="http://www.hubject.com/b2b/services/evsedata/v1.2" xmlns:EVSESearch="http://www.hubject.com/b2b/services/evsesearch/v1.2" xmlns:EVSEStatus="http://www.hubject.com/b2b/services/evsestatus/v1.2" xmlns:MobileAuthorization="http://www.hubject.com/b2b/services/mobileauthorization/v1.2">
                                //           <eMI3:Header />
                                //           <eMI3:Body>
                                //             <EVSEStatus:eRoamingPullEvseStatusById>
                                //               <EVSEStatus:ProviderID>DE-8BD</EVSEStatus:ProviderID>
                                //               <EVSEStatus:EvseId>+45*045*010*0A96296</EVSEStatus:EvseId>
                                //               <EVSEStatus:EvseId>+46*899*02423*01</EVSEStatus:EvseId>
                                //             </EVSEStatus:eRoamingPullEvseStatusById>
                                //           </eMI3:Body>
                                //         </eMI3:Envelope>
                                //       </OriginalDocument>
                                //     </Validation>
                                //   </detail>
                                // </S:Fault>

                                if (SOAPXML.Name.LocalName != "Fault")
                                {

                                    var OnSuccessLocal = OnSuccess;
                                    if (OnSuccessLocal != null)
                                        return OnSuccessLocal(new HTTPResponse<XElement>(HttpResponseTask.Result, SOAPXML));

                                }

                                var OnSOAPFaultLocal = OnSOAPFault;
                                if (OnSOAPFaultLocal != null)
                                    return OnSOAPFaultLocal(DateTime.Now, this, new HTTPResponse<XElement>(HttpResponseTask.Result, SOAPXML));

                                return new HTTPResponse<XElement>(HttpResponseTask.Result,
                                                                  new XElement("SOAPFault"),
                                                                  IsFault: true) as HTTPResponse<T>;


                            } catch (Exception e)
                            {

                                var OnExceptionLocal = OnException;
                                if (OnExceptionLocal != null)
                                    OnExceptionLocal(DateTime.Now, this, e);

                                //var OnFaultLocal = OnSOAPFault;
                                //if (OnFaultLocal != null)
                                //    return OnFaultLocal(new HTTPResponse<XElement>(HttpResponseTask.Result, e));

                                return new HTTPResponse<XElement>(HttpResponseTask.Result,
                                                                  new XElement("exception", e.Message),
                                                                  IsFault: true) as HTTPResponse<T>;

                            }

                        });

        }

        #endregion

    }

    #endregion

}
