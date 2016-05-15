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

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SOAP
{

    /// <summary>
    /// A HTTP/SOAP/XML server API.
    /// </summary>
    public abstract class ASOAPServer
    {

        #region Data

        /// <summary>
        /// The default HTTP/SOAP/XML server name.
        /// </summary>
        public const           String    DefaultHTTPServerName  = "GraphDefined HTTP/SOAP/XML Server API";

        /// <summary>
        /// The default HTTP/SOAP/XML server TCP port.
        /// </summary>
        public static readonly IPPort    DefaultHTTPServerPort  = new IPPort(443);

        /// <summary>
        /// The default query timeout.
        /// </summary>
        public static readonly TimeSpan  DefaultQueryTimeout    = TimeSpan.FromMinutes(1);

        #endregion

        #region Properties

        #region SOAPServer

        private readonly SOAPServer _SOAPServer;

        /// <summary>
        /// The HTTP/SOAP server.
        /// </summary>
        public SOAPServer SOAPServer
        {
            get
            {
                return _SOAPServer;
            }
        }

        #endregion

        #region URIPrefix

        private readonly String _URIPrefix;

        /// <summary>
        /// The common URI prefix for this HTTP/SOAP service.
        /// </summary>
        public String URIPrefix
        {
            get
            {
                return _URIPrefix;
            }
        }

        #endregion

        #region DNSClient

        private readonly DNSClient _DNSClient;

        /// <summary>
        /// The DNS client used by this server.
        /// </summary>
        public DNSClient DNSClient
        {
            get
            {
                return _DNSClient;
            }
        }

        #endregion

        #endregion

        #region Events

        #region RequestLog

        /// <summary>
        /// An event called whenever a request came in.
        /// </summary>
        public event RequestLogHandler RequestLog
        {

            add
            {
                _SOAPServer.RequestLog += value;
            }

            remove
            {
                _SOAPServer.RequestLog -= value;
            }

        }

        #endregion

        #region AccessLog

        /// <summary>
        /// An event called whenever a request could successfully be processed.
        /// </summary>
        public event AccessLogHandler AccessLog
        {

            add
            {
                _SOAPServer.AccessLog += value;
            }

            remove
            {
                _SOAPServer.AccessLog -= value;
            }

        }

        #endregion

        #region ErrorLog

        /// <summary>
        /// An event called whenever a request resulted in an error.
        /// </summary>
        public event ErrorLogHandler ErrorLog
        {

            add
            {
                _SOAPServer.ErrorLog += value;
            }

            remove
            {
                _SOAPServer.ErrorLog -= value;
            }

        }

        #endregion

        #endregion

        #region Constructor(s)

        #region ASOAPServer(HTTPServerName, TCPPort = null, URIPrefix = "", DNSClient = null, AutoStart = false)

        /// <summary>
        /// Initialize a new HTTP server for the HTTP/SOAP/XML Server API using IPAddress.Any.
        /// </summary>
        /// <param name="HTTPServerName">An optional identification string for the HTTP server.</param>
        /// <param name="TCPPort">An optional TCP port for the HTTP server.</param>
        /// <param name="URIPrefix">An optional prefix for the HTTP URIs.</param>
        /// <param name="DNSClient">An optional DNS client to use.</param>
        /// <param name="AutoStart">Start the server immediately.</param>
        public ASOAPServer(String    HTTPServerName  = DefaultHTTPServerName,
                           IPPort    TCPPort         = null,
                           String    URIPrefix       = "",
                           DNSClient DNSClient       = null,
                           Boolean   AutoStart       = false)

            : this(new SOAPServer(TCPPort != null ? TCPPort : DefaultHTTPServerPort,
                                  DefaultServerName:  HTTPServerName,
                                  DNSClient:          DNSClient,
                                  Autostart:          AutoStart),
                   URIPrefix)

        {

            if (AutoStart)
                Start();

        }

        #endregion

        #region ASOAPServer(SOAPServer, URIPrefix = "")

        /// <summary>
        /// Use the given HTTP server for the HTTP/SOAP/XML Server API using IPAddress.Any.
        /// </summary>
        /// <param name="SOAPServer">A SOAP server.</param>
        /// <param name="URIPrefix">An optional prefix for the HTTP URIs.</param>
        public ASOAPServer(SOAPServer  SOAPServer,
                           String      URIPrefix  = "")
        {

            #region Initial checks

            if (SOAPServer == null)
                throw new ArgumentNullException(nameof(SOAPServer),  "The given SOAP server must not be null!");

            if (URIPrefix == null)
                URIPrefix = "";

            if (URIPrefix.Length > 0 && !URIPrefix.StartsWith("/"))
                URIPrefix = "/" + URIPrefix;

            #endregion

            this._SOAPServer  = SOAPServer;
            this._URIPrefix   = URIPrefix;
            this._DNSClient   = SOAPServer.DNSClient;

            RegisterURITemplates();

        }

        #endregion

        #endregion


        #region (protected abstract) RegisterURITemplates()

        protected abstract void RegisterURITemplates();

        #endregion


        #region Start()

        public virtual void Start()
        {
            _SOAPServer.Start();
        }

        #endregion

        #region Shutdown(Message = null, Wait = true)

        public virtual void Shutdown(String Message = null, Boolean Wait = true)
        {
            _SOAPServer.Shutdown(Message, Wait);
        }

        #endregion

    }

}
