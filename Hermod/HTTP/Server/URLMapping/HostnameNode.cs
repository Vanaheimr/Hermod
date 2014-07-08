/*
 * Copyright (c) 2010-2014, Achim 'ahzf' Friedland <achim@graphdefined.org>
 * This file is part of Hermod <http://www.github.com/Vanaheimr/Hermod>
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

#endregion

namespace eu.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A node which stores information for maintaining multiple http hostnames.
    /// </summary>
    public class HostnameNode
    {

        #region Properties

        #region Hostname

        private readonly String _Hostname;

        /// <summary>
        /// The hostname for this (virtual) http service.
        /// </summary>
        public String Hostname
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

        #region ErrorHandler

        private readonly HTTPDelegate _DefaultErrorHandler;

        /// <summary>
        /// A general error handling method.
        /// </summary>
        public HTTPDelegate ErrorHandler
        {
            get
            {
                return _DefaultErrorHandler;
            }
        }

        #endregion

        /// <summary>
        /// Error handling methods for specific http status codes.
        /// </summary>
        public Dictionary<HTTPStatusCode, HTTPDelegate>  ErrorHandlers       { get; private set; }

        /// <summary>
        /// A mapping from URIs to URINodes.
        /// </summary>
        public Dictionary<String, URINode>               URINodes                { get; private set; }

        #endregion

        #region (internal) Constructor(s)

        /// <summary>
        /// Creates a new hostname node.
        /// </summary>
        /// <param name="Hostname">The hostname(s) for this (virtual) http service.</param>
        /// <param name="HostAuthentication">This and all subordinated nodes demand an explicit host authentication.</param>
        /// <param name="RequestHandler">The default delegate to call for any request to this hostname.</param>
        /// <param name="DefaultErrorHandler">A general error handling method.</param>
        internal HostnameNode(String              Hostname,
                              HTTPAuthentication  HostAuthentication   = null,
                              HTTPDelegate        RequestHandler       = null,
                              HTTPDelegate        DefaultErrorHandler  = null)
        {

            #region Check Hostname

            var    HostHeader  = Hostname.Split(new Char[1] { ':' }, StringSplitOptions.None).Select(v => v.Trim()).ToArray();
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
                this._Hostname = Hostname + ":" + HostPort;

            else if ((HostHeader.Length == 2 && (!UInt16.TryParse(HostHeader[1], out HostPort) && HostHeader[1] != "*")) ||
                      HostHeader.Length  > 2)
                      throw new ArgumentException("Invalid Hostname!", "Hostname");

            else
                this._Hostname = HostHeader[0] + ":" + HostHeader[1];

            #endregion

            this._HostAuthentication   = (HostAuthentication != null) ? HostAuthentication : _ => true;
            this._RequestHandler       = RequestHandler;
            this._DefaultErrorHandler  = DefaultErrorHandler;

            this.URINodes              = new Dictionary<String,         URINode>();
            this.ErrorHandlers         = new Dictionary<HTTPStatusCode, HTTPDelegate>();

        }

        #endregion

        #region ToString()

        public override String ToString()
        {

            var _HostAuthentication = "";
            if (HostAuthentication != null)
                _HostAuthentication = " (auth)";

            var _HostErrorHandler = "";
            if (ErrorHandler != null)
                _HostErrorHandler = " (errhdl)";

            return String.Concat(Hostname, _HostAuthentication, _HostErrorHandler);

        }

        #endregion

    }

}
