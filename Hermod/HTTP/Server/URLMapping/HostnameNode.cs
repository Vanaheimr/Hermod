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
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A node which stores information for maintaining multiple http hostnames.
    /// </summary>
    public class HostnameNode
    {

        #region Properties

        #region Hostname

        private readonly HTTPHostname _Hostname;

        /// <summary>
        /// The hostname for this (virtual) http service.
        /// </summary>
        public HTTPHostname Hostname
        {
            get
            {
                return _Hostname;
            }
        }

        #endregion

        #region HostAuthentication

        private readonly HTTPAuthentication _HostAuthentication;

        /// <summary>
        /// This and all subordinated nodes demand an explicit host authentication.
        /// </summary>
        public HTTPAuthentication HostAuthentication
        {
            get
            {
                return _HostAuthentication;
            }
        }

        #endregion

        #region RequestHandler

        private readonly HTTPDelegate _RequestHandler;

        public HTTPDelegate RequestHandler
        {
            get
            {
                return _RequestHandler;
            }
        }

        #endregion

        #region DefaultErrorHandler

        private readonly HTTPDelegate _DefaultErrorHandler;

        /// <summary>
        /// A general error handling method.
        /// </summary>
        public HTTPDelegate DefaultErrorHandler
        {
            get
            {
                return _DefaultErrorHandler;
            }
        }

        #endregion

        #region ErrorHandlers

        private readonly Dictionary<HTTPStatusCode, HTTPDelegate> _ErrorHandlers;

        /// <summary>
        /// Error handling methods for specific http status codes.
        /// </summary>
        public Dictionary<HTTPStatusCode, HTTPDelegate> ErrorHandlers
        {
            get
            {
                return _ErrorHandlers;
            }
        }

        #endregion

        #region URINodes

        private readonly Dictionary<HTTPURI, URINode> _URINodes;

        /// <summary>
        /// A mapping from URIs to URINodes.
        /// </summary>
        public Dictionary<HTTPURI, URINode> URINodes
        {
            get
            {
                return _URINodes;
            }
        }

        #endregion

        #endregion

        #region (internal) Constructor(s)

        /// <summary>
        /// Creates a new hostname node.
        /// </summary>
        /// <param name="Hostname">The hostname(s) for this (virtual) http service.</param>
        /// <param name="RequestHandler">The default delegate to call for any request to this hostname.</param>
        /// <param name="HostAuthentication">This and all subordinated nodes demand an explicit host authentication.</param>
        /// <param name="DefaultErrorHandler">The default error handling delegate.</param>
        internal HostnameNode(HTTPHostname        Hostname,
                              HTTPAuthentication  HostAuthentication   = null,
                              HTTPDelegate        RequestHandler       = null,
                              HTTPDelegate        DefaultErrorHandler  = null)

        {

            #region Check Hostname

            if (Hostname == null)
                throw new ArgumentNullException("Hostname", "The given HTTP hostname must not be null!");

            var    HostHeader  = Hostname.ToString().Split(new Char[1] { ':' }, StringSplitOptions.None).Select(v => v.Trim()).ToArray();
            UInt16 HostPort    = 80;

            // 1.2.3.4          => 1.2.3.4:80
            // 1.2.3.4:80       => ok
            // 1.2.3.4 : 80     => ok
            // 1.2.3.4:*        => ok
            // 1.2.3.4:a        => invalid
            // 1.2.3.4:80:      => ok
            // 1.2.3.4:80:0     => invalid

            // rfc 2616 - 3.2.2
            // If the port is empty or not given, port 80 is assumed.
            if (HostHeader.Length == 1)
                this._Hostname = HTTPHostname.Parse(Hostname + ":" + HostPort);

            else if ((HostHeader.Length == 2 && (!UInt16.TryParse(HostHeader[1], out HostPort) && HostHeader[1] != "*")) ||
                      HostHeader.Length  > 2)
                      throw new ArgumentException("Invalid Hostname!", "Hostname");

            else
                this._Hostname = HTTPHostname.Parse(HostHeader[0] + ":" + HostHeader[1]);

            #endregion

            this._HostAuthentication   = (HostAuthentication != null) ? HostAuthentication : _ => true;
            this._RequestHandler       = RequestHandler;
            this._DefaultErrorHandler  = DefaultErrorHandler;
            this._URINodes             = new Dictionary<HTTPURI,        URINode>();
            this._ErrorHandlers        = new Dictionary<HTTPStatusCode, HTTPDelegate>();

        }

        #endregion


        #region AddHandler(...)

        public void AddHandler(HTTPDelegate        HTTPDelegate,

                               HTTPURI?            URITemplate                 = null,
                               HTTPMethod          HTTPMethod                  = null,
                               HTTPContentType     HTTPContentType             = null,

                               HTTPAuthentication  URIAuthentication           = null,
                               HTTPAuthentication  HTTPMethodAuthentication    = null,
                               HTTPAuthentication  ContentTypeAuthentication   = null,

                               HTTPDelegate        DefaultErrorHandler         = null,
                               URIReplacement      AllowReplacement            = URIReplacement.Fail)

        {

            if (!URITemplate.HasValue)
                URITemplate = HTTPURI.Parse("/");

            if (!_URINodes.TryGetValue(URITemplate.Value, out URINode _URINode))
            {
                _URINode = new URINode(URITemplate.Value, URIAuthentication, HTTPDelegate, DefaultErrorHandler);
                _URINodes.Add(URITemplate.Value, _URINode);
            }

            _URINode.AddHandler(HTTPDelegate,

                                HTTPMethod,
                                HTTPContentType,

                                HTTPMethodAuthentication,
                                ContentTypeAuthentication,

                                DefaultErrorHandler,
                                AllowReplacement);

        }

        #endregion


        #region (override) ToString()

        public override String ToString()
        {

            var _HostAuthentication = "";
            if (HostAuthentication != null)
                _HostAuthentication = " (auth)";

            var __DefaultErrorHandler = "";
            if (_DefaultErrorHandler != null)
                __DefaultErrorHandler = " (errhdl)";

            return String.Concat(Hostname, _HostAuthentication, __DefaultErrorHandler);

        }

        #endregion

    }

}
