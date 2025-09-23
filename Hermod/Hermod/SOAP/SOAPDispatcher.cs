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

        #region Data

        private readonly List<SOAPDispatch> soapDispatches;

        #endregion

        #region Properties

        /// <summary>
        /// The URI template of this SOAP endpoint.
        /// </summary>
        public HTTPPath                   URITemplate        { get; }

        /// <summary>
        /// The HTTP content type the SOAP/XML request will be send.
        /// </summary>
        public HTTPContentType            SOAPContentType    { get; }

        /// <summary>
        /// All registered SOAP dispatches.
        /// </summary>
        public IEnumerable<SOAPDispatch>  SOAPDispatches
            => soapDispatches;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new SOAP dispatcher.
        /// </summary>
        /// <param name="URITemplate">The URI template of the SOAP dispatcher.</param>
        /// <param name="SOAPContentType">The HTTP content type the SOAP/XML request will be send.</param>
        public SOAPDispatcher(HTTPPath          URITemplate,
                              HTTPContentType  SOAPContentType)
        {

            this.URITemplate      = URITemplate;
            this.SOAPContentType  = SOAPContentType;

            this.soapDispatches   = [];

        }

        #endregion


        #region RegisterSOAPDelegate(Description, SOAPMatch, SOAPBodyDelegate)

        /// <summary>
        /// Register a SOAP delegate.
        /// </summary>
        /// <param name="Description">A description of this SOAP delegate.</param>
        /// <param name="SOAPMatch">A delegate to check whether this dispatcher matches the given XML.</param>
        /// <param name="SOAPBodyDelegate">A delegate to process a matching SOAP request.</param>
        public void RegisterSOAPDelegate(String            Description,
                                         SOAPMatch         SOAPMatch,
                                         SOAPBodyDelegate  SOAPBodyDelegate)
        {

            soapDispatches.Add(
                new SOAPDispatch(
                    Description,
                    SOAPMatch,
                    SOAPBodyDelegate
                )
            );

        }

        #endregion

        #region RegisterSOAPDelegate(Description, SOAPMatch, SOAPHeaderAndBodyDelegate)

        /// <summary>
        /// Register a SOAP delegate.
        /// </summary>
        /// <param name="Description">A description of this SOAP delegate.</param>
        /// <param name="SOAPMatch">A delegate to check whether this dispatcher matches the given XML.</param>
        /// <param name="SOAPHeaderAndBodyDelegate">A delegate to process a matching SOAP request.</param>
        public void RegisterSOAPDelegate(String                     Description,
                                         SOAPMatch                  SOAPMatch,
                                         SOAPHeaderAndBodyDelegate  SOAPHeaderAndBodyDelegate)
        {

            soapDispatches.Add(new SOAPDispatch(
                                   Description,
                                   SOAPMatch,
                                   SOAPHeaderAndBodyDelegate
                               ));

        }

        #endregion


        #region Invoke(Request)

        /// <summary>
        /// Invoke this SOAP endpoint and choose a matching dispatcher.
        /// </summary>
        /// <param name="Request">An HTTP request.</param>
        public async Task<HTTPResponse> Invoke(HTTPRequest Request)
        {

            if (Request.HTTPMethod == HTTPMethod.GET)
                return await EndpointTextInfo(Request);

            var xmlRequest = Request.ParseXMLRequestBody(SOAPContentType);
            if (xmlRequest.HasErrors)
                return xmlRequest.Error;

            var soapDispatch = soapDispatches.
                                    Select(dispatch => new {
                                        dispatch    = dispatch,
                                        SOAPHeader  = xmlRequest.Data.Root.Descendants(v1_2.NS.SOAPEnvelope + "Header").FirstOrDefault(),
                                        SOAPBody    = dispatch.Matcher(xmlRequest.Data.Root)
                                    }).
                                    FirstOrDefault(match => match.SOAPBody is not null);

            if (soapDispatch is not null)
            {

                if (soapDispatch.dispatch.BodyDelegate          is not null)
                    return await soapDispatch.dispatch.BodyDelegate(
                                     Request,
                                     soapDispatch.SOAPBody
                                 );

                if (soapDispatch.dispatch.HeaderAndBodyDelegate is not null)
                    return await soapDispatch.dispatch.HeaderAndBodyDelegate(
                                     Request,
                                     soapDispatch.SOAPHeader,
                                     soapDispatch.SOAPBody
                                 );

                return new HTTPResponse.Builder(Request) {
                           HTTPStatusCode  = HTTPStatusCode.BadRequest,
                           ContentType     = HTTPContentType.Text.PLAIN,
                           Content         = "Invalid SOAP/XML processing!".ToUTF8Bytes(),
                           Connection      = ConnectionType.Close
                       };

            }

            return new HTTPResponse.Builder(Request) {
                       HTTPStatusCode  = HTTPStatusCode.BadRequest,
                       ContentType     = HTTPContentType.Text.PLAIN,
                       Content         = "Unknown SOAP/XML!".ToUTF8Bytes(),
                       Connection      = ConnectionType.Close
                   };

        }

        #endregion

        #region EndpointTextInfo(Request)

        /// <summary>
        /// Return a short information text about this endpoint.
        /// </summary>
        /// <param name="Request">An HTTP request.</param>
        public Task<HTTPResponse> EndpointTextInfo(HTTPRequest Request)

            => Task.FromResult(new HTTPResponse.Builder(Request) {

                HTTPStatusCode  = HTTPStatusCode.OK,
                ContentType     = HTTPContentType.Text.PLAIN,
                Content         = ("Welcome at " + Request.HTTPServer.DefaultServerName + Environment.NewLine +
                                   "This is a HTTP/SOAP/XML endpoint!" + Environment.NewLine + Environment.NewLine +
                                   "Defined SOAP meassages: " + Environment.NewLine +
                                   soapDispatches.
                                       Select(dispatch => " - " + dispatch.Description).
                                       AggregateWith(Environment.NewLine)
                                  ).ToUTF8Bytes(),
                Connection      = ConnectionType.Close

            }.AsImmutable);

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => soapDispatches.Select(item => item.Description).AggregateCSV();

        #endregion

    }

}
