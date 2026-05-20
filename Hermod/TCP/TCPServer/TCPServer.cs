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

using System.Security.Authentication;

using Microsoft.Extensions.Logging;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.TCP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A TCP/TLS server.
    /// </summary>
    public class TCPServer : ATCPServer
    {

        #region Data

        private readonly new ILogger<TCPServer>? logger;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new abstract TCP/TLS server that listens on the specified IP address and TCP port.
        /// </summary>
        /// <param name="IPAddress">The IP address to listen on. If null, the loopback address will be used.</param>
        /// <param name="TCPPort">The TCP port to listen on. If 0, a random TCP port will be assigned.</param>
        /// <param name="ReceiveTimeout">An optional receive timeout for the TCP stream. If null, the default receive timeout will be used.</param>
        /// <param name="SendTimeout">An optional send timeout for the TCP stream. If null, the default send timeout will be used.</param>
        /// <param name="LoggingHandler">An optional logging handler that will be called for each log message.</param>
        /// 
        /// <param name="ServerCertificateSelector"></param>
        /// <param name="ClientCertificateValidator"></param>
        /// <param name="LocalCertificateSelector"></param>
        /// <param name="AllowedTLSProtocols"></param>
        /// <param name="ClientCertificateRequired"></param>
        /// <param name="CheckCertificateRevocation"></param>
        /// 
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information. If null, the default connection identification will be used.</param>
        /// <param name="MaxClientConnections">An optional maximum number of concurrent TCP client connections. If null, the default maximum number of concurrent TCP client connections will be used.</param>
        /// <param name="DNSClient">The DNS client to use for the warden and other DNS lookups.</param>
        /// 
        /// <param name="DisableMaintenanceTasks">Whether to disable all maintenance tasks.</param>
        /// <param name="MaintenanceInitialDelay">The initial delay of the maintenance tasks.</param>
        /// <param name="MaintenanceEvery">The maintenance interval.</param>
        /// 
        /// <param name="DisableWardenTasks">Whether to disable all warden tasks.</param>
        /// <param name="WardenInitialDelay">The initial delay of the warden tasks.</param>
        /// <param name="WardenCheckEvery">The warden check interval.</param>
        /// 
        /// <param name="Description">An optional description of this TCP server.</param>
        /// <param name="AutoStart">Whether to automatically start the TCP server.</param>

        public TCPServer(IIPAddress?                                               IPAddress                    = null,
                         IPPort?                                                   TCPPort                      = null,
                         I18NString?                                               Description                  = null,

                         TimeSpan?                                                 ReceiveTimeout               = null,
                         TimeSpan?                                                 SendTimeout                  = null,
                         TCPEchoLoggingDelegate?                                   LoggingHandler               = null,

                         ServerCertificateSelectorDelegate?                        ServerCertificateSelector    = null,
                         RemoteTLSClientCertificateValidationHandler<ITCPServer>?  ClientCertificateValidator   = null,
                         LocalCertificateSelectionHandler?                         LocalCertificateSelector     = null,
                         SslProtocols?                                             AllowedTLSProtocols          = null,
                         Boolean?                                                  ClientCertificateRequired    = null,
                         Boolean?                                                  CheckCertificateRevocation   = null,

                         ConnectionIdBuilder?                                      ConnectionIdBuilder          = null,
                         UInt32?                                                   MaxClientConnections         = null,
                         IDNSClient?                                               DNSClient                    = null,

                         Boolean?                                                  DisableMaintenanceTasks      = false,
                         TimeSpan?                                                 MaintenanceInitialDelay      = null,
                         TimeSpan?                                                 MaintenanceEvery             = null,

                         Boolean?                                                  DisableWardenTasks           = false,
                         TimeSpan?                                                 WardenInitialDelay           = null,
                         TimeSpan?                                                 WardenCheckEvery             = null,

                         ILogger<TCPServer>?                                       Logger                       = null,
                         ILoggerFactory?                                           LoggerFactory                = null,
                         Boolean?                                                  AutoStart                    = false)

            : base(IPAddress,
                   TCPPort,
                   Description,

                   ReceiveTimeout,
                   SendTimeout,
                   LoggingHandler,

                   ServerCertificateSelector,
                   ClientCertificateValidator,
                   LocalCertificateSelector,
                   AllowedTLSProtocols,
                   ClientCertificateRequired,
                   CheckCertificateRevocation,

                   ConnectionIdBuilder,
                   MaxClientConnections,
                   DNSClient,

                   DisableMaintenanceTasks,
                   MaintenanceInitialDelay,
                   MaintenanceEvery,

                   DisableWardenTasks,
                   WardenInitialDelay,
                   WardenCheckEvery,

                   LoggerFactory,
                   AutoStart: false)

        {

            this.logger = Logger ?? LoggerFactory?.CreateLogger<TCPServer>();

            if (AutoStart ?? false)
                Start().GetAwaiter().GetResult();

        }

        #endregion


        protected override Task HandleConnection(TCPConnection      Connection,
                                                 CancellationToken  CancellationToken)
        {

            this.logger?.LogDebug(
                "Handle new TCP connection from {RemoteEndPoint} to {LocalEndPoint} with ConnectionId {ConnectionId}",
                Connection.RemoteSocket.ToString(),
                Connection.LocalSocket. ToString(),
                Connection.ConnectionId
            );

            return Task.CompletedTask;

        }


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{nameof(TCPServer)}: {IPAddress}:{TCPPort} (ReceiveTimeout: {ReceiveTimeout}, SendTimeout: {SendTimeout})";

        #endregion

    }

}
