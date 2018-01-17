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
using System.Threading.Tasks;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;
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
        public const           String           DefaultHTTPServerName   = "GraphDefined HTTP/SOAP/XML Server API";

        /// <summary>
        /// The default HTTP/SOAP/XML server TCP port.
        /// </summary>
        public static readonly IPPort           DefaultHTTPServerPort   = new IPPort(443);

        /// <summary>
        /// The default HTTP/SOAP/XML server URI prefix.
        /// </summary>
        public const           String           DefaultURIPrefix        = "";

        /// <summary>
        /// The default HTTP/SOAP/XML content type.
        /// </summary>
        public static readonly HTTPContentType  DefaultContentType      = SOAPServer.DefaultSOAPContentType;

        /// <summary>
        /// The default request timeout.
        /// </summary>
        public static readonly TimeSpan         DefaultRequestTimeout   = TimeSpan.FromMinutes(1);

        #endregion

        #region Properties

        /// <summary>
        /// The HTTP/SOAP server.
        /// </summary>
        public SOAPServer  SOAPServer   { get; }

        /// <summary>
        /// The common URI prefix for this HTTP/SOAP service.
        /// </summary>
        public String      URIPrefix    { get; }

        /// <summary>
        /// The DNS client used by this server.
        /// </summary>
        public DNSClient   DNSClient    { get; }

        /// <summary>
        /// All TCP ports this SOAP server listens on.
        /// </summary>
        public IEnumerable<IPPort> IPPorts
            => SOAPServer.IPPorts;

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
                SOAPServer.RequestLog += value;
            }

            remove
            {
                SOAPServer.RequestLog -= value;
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
                SOAPServer.AccessLog += value;
            }

            remove
            {
                SOAPServer.AccessLog -= value;
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
                SOAPServer.ErrorLog += value;
            }

            remove
            {
                SOAPServer.ErrorLog -= value;
            }

        }

        #endregion

        #endregion

        #region Constructor(s)

        #region ASOAPServer(HTTPServerName, TCPPort = default, URIPrefix = default, SOAPContentType = default, DNSClient = null, RegisterHTTPRootService = true, AutoStart = false)

        /// <summary>
        /// Initialize a new HTTP server for the HTTP/SOAP/XML Server API using IPAddress.Any.
        /// </summary>
        /// <param name="HTTPServerName">An optional identification string for the HTTP server.</param>
        /// <param name="TCPPort">An optional TCP port for the HTTP server.</param>
        /// <param name="URIPrefix">An optional prefix for the HTTP URIs.</param>
        /// <param name="SOAPContentType">The HTTP content type for SOAP messages.</param>
        /// <param name="DNSClient">An optional DNS client to use.</param>
        /// <param name="RegisterHTTPRootService">Register HTTP root services for sending a notice to clients connecting via HTML or plain text.</param>
        /// <param name="AutoStart">Start the server immediately.</param>
        public ASOAPServer(String          HTTPServerName           = DefaultHTTPServerName,
                           IPPort          TCPPort                  = null,
                           String          URIPrefix                = DefaultURIPrefix,
                           HTTPContentType SOAPContentType          = null,
                           Boolean         RegisterHTTPRootService  = true,
                           DNSClient       DNSClient                = null,
                           Boolean         AutoStart                = false)

            : this(new SOAPServer(TCPPort ?? DefaultHTTPServerPort,
                                  HTTPServerName,
                                  SOAPContentType ?? DefaultContentType,
                                  DNSClient:  DNSClient,
                                  Autostart:  false),
                   URIPrefix)

        {

            if (RegisterHTTPRootService)
                RegisterRootService();

            if (AutoStart)
#pragma warning disable RECS0021 //Virtual member call in constructor
                Start();
#pragma warning restore RECS0021

        }

        #endregion

        #region ASOAPServer(SOAPServer, URIPrefix = default, RegisterHTTPRootService = false)

        /// <summary>
        /// Use the given HTTP server for the HTTP/SOAP/XML Server API.
        /// </summary>
        /// <param name="SOAPServer">A SOAP server.</param>
        /// <param name="URIPrefix">An optional URI prefix for the SOAP URI templates.</param>
        public ASOAPServer(SOAPServer  SOAPServer,
                           String      URIPrefix  = DefaultURIPrefix)
        {

            #region Initial checks

            if (SOAPServer == null)
                throw new ArgumentNullException(nameof(SOAPServer),  "The given SOAP server must not be null!");

            if (URIPrefix != null && URIPrefix.Trim().IsNotNullOrEmpty())
                URIPrefix = URIPrefix.Trim();
            else
                URIPrefix = DefaultURIPrefix;

            if (URIPrefix.Length > 0 && !URIPrefix.StartsWith("/", StringComparison.Ordinal))
                URIPrefix = "/" + URIPrefix;

            while (URIPrefix.EndsWith("/", StringComparison.Ordinal))
                URIPrefix = URIPrefix.Substring(0, URIPrefix.Length - 1);

            #endregion

            this.SOAPServer  = SOAPServer;
            this.URIPrefix   = URIPrefix;
            this.DNSClient   = SOAPServer.DNSClient;

        }

        #endregion

        #endregion


        #region (private) RegisterRootService()

        private void RegisterRootService()
        {

            SOAPServer.AddMethodCallback(HTTPHostname.Any,
                                         HTTPMethod.GET,

                                         new String[] {
                                             "/",
                                             URIPrefix + "/"
                                         },

                                         new HTTPContentType[] {
                                             HTTPContentType.TEXT_UTF8,
                                             HTTPContentType.HTML_UTF8
                                         },

                                         HTTPDelegate: Request => {

                                             return Task.FromResult(
                                                 new HTTPResponseBuilder(Request) {

                                                     HTTPStatusCode  = HTTPStatusCode.BadGateway,
                                                     ContentType     = HTTPContentType.TEXT_UTF8,
                                                     Content         = ("Welcome at " + DefaultHTTPServerName + Environment.NewLine +
                                                                        "This is a HTTP/SOAP/XML endpoint!" + Environment.NewLine + Environment.NewLine +
                                                                        "Defined endpoints: " + Environment.NewLine + Environment.NewLine +
                                                                        SOAPServer.
                                                                            SOAPDispatchers.
                                                                            Select(group => " - " + group.Key + Environment.NewLine +
                                                                                            "   " + group.SelectMany(dispatcher => dispatcher.SOAPDispatches).
                                                                                                          Select    (dispatch   => dispatch.  Description).
                                                                                                          AggregateWith(", ")
                                                                                  ).AggregateWith(Environment.NewLine + Environment.NewLine)
                                                                       ).ToUTF8Bytes(),
                                                     Connection      = "close"

                                                 }.AsImmutable);

                                         },

                                         AllowReplacement: URIReplacement.Allow);

        }

        #endregion

        #region (protected abstract) RegisterURITemplates()

        ///// <summary>
        ///// Register all URI templates for this SOAP API.
        ///// </summary>
        //protected abstract void RegisterURITemplates();

        #endregion


        #region Start()

        /// <summary>
        /// Start the SOAP API.
        /// </summary>
        public virtual void Start()
        {
            SOAPServer.Start();
        }

        #endregion

        #region Shutdown(Message = null, Wait = true)

        /// <summary>
        /// Stop the SOAP API.
        /// </summary>
        /// <param name="Message">An optional shutdown message.</param>
        /// <param name="Wait">Wait for a clean shutdown of the API.</param>
        public virtual void Shutdown(String   Message  = null,
                                     Boolean  Wait     = true)
        {
            SOAPServer.Shutdown(Message, Wait);
        }

        #endregion


    }

}
