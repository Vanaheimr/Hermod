/*
 * Copyright (c) 2010-2016, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SOAP
{

    /// <summary>
    /// A SOAP dispatcher.
    /// </summary>
    public class SOAPDispatcher
    {

        #region Properties

        #region URITemplate

        private readonly String _URITemplate;

        /// <summary>
        /// The URI template of this SOAP endpoint.
        /// </summary>
        public String URITemplate
        {
            get
            {
                return _URITemplate;
            }
        }

        #endregion

        #region SOAPDispatches

        private readonly List<SOAPDispatch> _SOAPDispatches;

        /// <summary>
        /// All registeres SOAP dispatches.
        /// </summary>
        public IEnumerable<SOAPDispatch> SOAPDispatches
        {
            get
            {
                return _SOAPDispatches;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new SOAP dispatcher.
        /// </summary>
        /// <param name="URITemplate">The URI template of the SOAP dispatcher.</param>
        public SOAPDispatcher(String URITemplate)
        {

            #region Initial checks

            if (URITemplate == null)
                throw new ArgumentNullException(nameof(URITemplate), "The given URI template must not be null!");

            #endregion

            this._URITemplate     = URITemplate;
            this._SOAPDispatches  = new List<SOAPDispatch>();

        }

        #endregion


        #region RegisterSOAPDelegate(Description, SOAPContentType, SOAPMatch, SOAPBodyDelegate)

        /// <summary>
        /// Register a SOAP delegate.
        /// </summary>
        /// <param name="Description">A description of this SOAP delegate.</param>
        /// <param name="SOAPContentType">The HTTP content type the SOAP/XML request will be send.</param>
        /// <param name="SOAPMatch">A delegate to check whether this dispatcher matches the given XML.</param>
        /// <param name="SOAPBodyDelegate">A delegate to process a matching SOAP request.</param>
        public void RegisterSOAPDelegate(String            Description,
                                         HTTPContentType   SOAPContentType,
                                         SOAPMatch         SOAPMatch,
                                         SOAPBodyDelegate  SOAPBodyDelegate)
        {

            _SOAPDispatches.Add(new SOAPDispatch(Description, SOAPMatch, SOAPBodyDelegate));

        }

        #endregion

        #region RegisterSOAPDelegate(Description, SOAPContentType, SOAPMatch, SOAPHeaderAndBodyDelegate)

        /// <summary>
        /// Register a SOAP delegate.
        /// </summary>
        /// <param name="Description">A description of this SOAP delegate.</param>
        /// <param name="SOAPContentType">The HTTP content type the SOAP/XML request will be send.</param>
        /// <param name="SOAPMatch">A delegate to check whether this dispatcher matches the given XML.</param>
        /// <param name="SOAPHeaderAndBodyDelegate">A delegate to process a matching SOAP request.</param>
        public void RegisterSOAPDelegate(String                     Description,
                                         HTTPContentType            SOAPContentType,
                                         SOAPMatch                  SOAPMatch,
                                         SOAPHeaderAndBodyDelegate  SOAPHeaderAndBodyDelegate)
        {

            _SOAPDispatches.Add(new SOAPDispatch(Description, SOAPMatch, SOAPHeaderAndBodyDelegate));

        }

        #endregion


        #region Invoke(Request)

        /// <summary>
        /// Invoke this SOAP endpoint and choose a matching dispatcher.
        /// </summary>
        /// <param name="Request">A HTTP request.</param>
        public HTTPResponse Invoke(HTTPRequest Request)
        {

            if (Request.HTTPMethod == HTTPMethod.GET)
                return EndpointTextInfo(Request);

            var XMLRequest = Request.ParseXMLRequestBody(HTTPContentType.SOAPXML_UTF8);
            if (XMLRequest.HasErrors)
                return XMLRequest.Error;

            var SOAPDispatch  = _SOAPDispatches.
                                    Select(dispatch => new {
                                                           dispatch    = dispatch,
                                                           SOAPHeader  = XMLRequest.Data.Root.Descendants(NS.SOAPEnvelope_v1_2 + "Header").FirstOrDefault(),
                                                           SOAPBody    = dispatch.Matcher(XMLRequest.Data.Root)
                                                       }).
                                    Where (match    => match.SOAPBody != null).
                                    FirstOrDefault();

            if (SOAPDispatch != null)
            {

                if (SOAPDispatch.dispatch.BodyDelegate != null)
                    return SOAPDispatch.dispatch.BodyDelegate(Request, SOAPDispatch.SOAPBody);

                else if (SOAPDispatch.dispatch.HeaderAndBodyDelegate != null)
                    return SOAPDispatch.dispatch.HeaderAndBodyDelegate(Request, SOAPDispatch.SOAPHeader, SOAPDispatch.SOAPBody);

                return new HTTPResponseBuilder(Request) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    ContentType     = HTTPContentType.TEXT_UTF8,
                    Content         = "Invalid SOAP/XML processing!".ToUTF8Bytes(),
                    Connection      = "close"
                };

            }

            else
                return new HTTPResponseBuilder(Request) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    ContentType     = HTTPContentType.TEXT_UTF8,
                    Content         = "Unknown SOAP/XML!".ToUTF8Bytes(),
                    Connection      = "close"
                };

        }

        #endregion

        #region EndpointTextInfo(Request)

        /// <summary>
        /// Return a short information text about this endpoint.
        /// </summary>
        /// <param name="Request">A HTTP request.</param>
        public HTTPResponse EndpointTextInfo(HTTPRequest Request)
        {

            return new HTTPResponseBuilder(Request) {

                HTTPStatusCode  = HTTPStatusCode.OK,
                ContentType     = HTTPContentType.TEXT_UTF8,
                Content         = ("Welcome at " + Request.HTTPServer.DefaultServerName + Environment.NewLine +
                                   "This is a HTTP/SOAP/XML endpoint!" + Environment.NewLine + Environment.NewLine +
                                   "Defined SOAP meassages: " + Environment.NewLine +
                                   _SOAPDispatches.
                                       Select(dispatch => " - " + dispatch.Description).
                                       AggregateWith(Environment.NewLine)
                                  ).ToUTF8Bytes(),
                Connection      = "close"

            };

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a string representation of this object.
        /// </summary>
        public override String ToString()
        {
            return _SOAPDispatches.Select(item => item.Description).AggregateWith(", ");
        }

        #endregion

    }

}
