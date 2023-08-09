/*
 * Copyright (c) 2010-2023 GraphDefined GmbH
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

using System;
using System.Linq;
using System.Net.Security;
using System.Threading;
using System.Collections.Generic;
using System.Security.Authentication;

using org.GraphDefined.Vanaheimr.Styx.Arrows;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Services.CSV
{

    /// <summary>
    /// A TCP service accepting incoming UTF8 encoded
    /// comma-separated values with 0x00, 0x0a (\n) or
    /// 0x0d 0x0a (\r\n) end-of-line characters.
    /// </summary>
    public class TCPCSVServer : TCPServer,
                                IBoomerangSender<String, DateTime, String[], TCPResult<String>>
    {

        #region Data

        /// <summary>
        /// The default service banner.
        /// </summary>
        public new const String                  __DefaultServiceBanner  = "Vanaheimr Hermod TCP/CSV Service v0.10";

        /// <summary>
        /// The default array of delimiters to split the incoming CSV line into individual elements.
        /// </summary>
        public static readonly Char[]            DefaultSplitCharacters   = { ',' };

        private readonly TCPCSVProcessor         _TCPCSVProcessor;
        private readonly TCPCSVCommandProcessor  _TCPCSVCommandProcessor;

        #endregion

        #region Properties

        /// <summary>
        /// The characters to split the incoming CSV text lines.
        /// </summary>
        public Char[]  SplitCharacters    { get; }

        #endregion

        #region Events

        /// <summary>
        /// A delegate called whenever new data hab been received.
        /// </summary>
        /// <param name="ConnectionId">The unique identification of the TCP connection.</param>
        /// <param name="Timestamp">The current server timestamp.</param>
        /// <param name="CSVData">The CSV data.</param>
        public delegate void OnNewDataHandler(String ConnectionId, DateTime Timestamp, String[] CSVData);

        /// <summary>
        /// An event called whenever new data hab been received.
        /// </summary>
        public event OnNewDataHandler OnNewData;


        public event BoomerangSenderHandler<String, DateTime, String[], TCPResult<String>> OnNotification;

        #endregion

        #region Constructor(s)

        #region TCPCSVServer(TCPPort, ...)

        /// <summary>
        /// Initialize the TCP server using IPAddress.Any and the given parameters.
        /// </summary>
        /// <param name="TCPPort">The listening port</param>
        /// <param name="ServerCertificateSelector">An optional delegate to select a SSL/TLS server certificate.</param>
        /// <param name="ClientCertificateValidator">An optional delegate to verify the SSL/TLS client certificate used for authentication.</param>
        /// <param name="ClientCertificateSelector">An optional delegate to select the SSL/TLS client certificate used for authentication.</param>
        /// <param name="AllowedTLSProtocols">The SSL/TLS protocol(s) allowed for this connection.</param>
        /// <param name="ServiceName">The TCP service name shown e.g. on service startup.</param>
        /// <param name="ServiceBanner">Service banner.</param>
        /// <param name="SplitCharacters">An array of delimiters to split the incoming CSV line into individual elements.</param>
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information.</param>
        /// <param name="ConnectionThreadsNameBuilder">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriorityBuilder">An optional delegate to set the priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP connection threads are background threads or not (default: yes).</param>
        /// <param name="ConnectionTimeout">The TCP client timeout for all incoming client connections in seconds (default: 30 sec).</param>
        /// <param name="MaxClientConnections">The maximum number of concurrent TCP client connections (default: 4096).</param>
        /// <param name="AutoStart">Start the TCP server thread immediately (default: no).</param>
        public TCPCSVServer(IPPort                                TCPPort,
                            ServerCertificateSelectorDelegate?    ServerCertificateSelector          = null,
                            RemoteCertificateValidationHandler?  ClientCertificateValidator         = null,
                            LocalCertificateSelectionHandler?    ClientCertificateSelector          = null,
                            SslProtocols?                         AllowedTLSProtocols                = null,
                            Boolean?                              ClientCertificateRequired          = null,
                            Boolean?                              CheckCertificateRevocation         = null,

                            String?                               ServiceName                        = null,
                            String                                ServiceBanner                      = __DefaultServiceBanner,
                            IEnumerable<Char>?                    SplitCharacters                    = null,
                            String?                               ServerThreadName                   = null,
                            ThreadPriority                        ServerThreadPriority               = ThreadPriority.AboveNormal,
                            Boolean                               ServerThreadIsBackground           = true,

                            ConnectionIdBuilder?                  ConnectionIdBuilder                = null,
                            //ConnectionThreadsNameBuilder?         ConnectionThreadsNameBuilder       = null,
                            //ConnectionThreadsPriorityBuilder?     ConnectionThreadsPriorityBuilder   = null,
                            //Boolean                               ConnectionThreadsAreBackground     = true,
                            TimeSpan?                             ConnectionTimeout                  = null,

                            UInt32                                MaxClientConnections               = __DefaultMaxClientConnections,
                            Boolean                               AutoStart                          = false)

            : this(IPv4Address.Any,
                   TCPPort,
                   ServerCertificateSelector,
                   ClientCertificateValidator,
                   ClientCertificateSelector,
                   AllowedTLSProtocols,
                   ClientCertificateRequired,
                   CheckCertificateRevocation,

                   ServiceName,
                   ServiceBanner,
                   SplitCharacters,
                   ServerThreadName,
                   ServerThreadPriority,
                   ServerThreadIsBackground,

                   ConnectionIdBuilder,
                   //ConnectionThreadsNameBuilder,
                   //ConnectionThreadsPriorityBuilder,
                   //ConnectionThreadsAreBackground,
                   ConnectionTimeout,

                   MaxClientConnections,
                   AutoStart)

        { }

        #endregion

        #region TCPCSVServer(IIPAddress, Port, ...)

        /// <summary>
        /// Initialize the TCP server using the given parameters.
        /// </summary>
        /// <param name="IIPAddress">The listening IP address(es)</param>
        /// <param name="Port">The listening port</param>
        /// <param name="ServiceName">The TCP service name shown e.g. on service startup.</param>
        /// <param name="ServerCertificateSelector">An optional delegate to select a SSL/TLS server certificate.</param>
        /// <param name="ClientCertificateValidator">An optional delegate to verify the SSL/TLS client certificate used for authentication.</param>
        /// <param name="ClientCertificateSelector">An optional delegate to select the SSL/TLS client certificate used for authentication.</param>
        /// <param name="AllowedTLSProtocols">The SSL/TLS protocol(s) allowed for this connection.</param>
        /// <param name="ServiceBanner">Service banner.</param>
        /// <param name="SplitCharacters">An enumeration of delimiters to split the incoming CSV line into individual elements.</param>
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information.</param>
        /// <param name="ConnectionThreadsNameBuilder">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriorityBuilder">An optional delegate to set the priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP connection threads are background threads or not (default: yes).</param>
        /// <param name="ConnectionTimeout">The TCP client timeout for all incoming client connections in seconds (default: 30 sec).</param>
        /// <param name="MaxClientConnections">The maximum number of concurrent TCP client connections (default: 4096).</param>
        /// <param name="AutoStart">Start the TCP/CSV server thread immediately (default: no).</param>
        public TCPCSVServer(IIPAddress                            IIPAddress,
                            IPPort                                Port,
                            ServerCertificateSelectorDelegate?    ServerCertificateSelector          = null,
                            RemoteCertificateValidationHandler?  ClientCertificateValidator         = null,
                            LocalCertificateSelectionHandler?    ClientCertificateSelector          = null,
                            SslProtocols?                         AllowedTLSProtocols                = null,
                            Boolean?                              ClientCertificateRequired          = null,
                            Boolean?                              CheckCertificateRevocation         = null,

                            String?                               ServiceName                        = null,
                            String                                ServiceBanner                      = __DefaultServiceBanner,
                            IEnumerable<Char>?                    SplitCharacters                    = null,
                            String?                               ServerThreadName                   = null,
                            ThreadPriority                        ServerThreadPriority               = ThreadPriority.AboveNormal,
                            Boolean                               ServerThreadIsBackground           = true,

                            ConnectionIdBuilder?                  ConnectionIdBuilder                = null,
                            //ConnectionThreadsNameBuilder?         ConnectionThreadsNameBuilder       = null,
                            //ConnectionThreadsPriorityBuilder?     ConnectionThreadsPriorityBuilder   = null,
                            //Boolean                               ConnectionThreadsAreBackground     = true,
                            TimeSpan?                             ConnectionTimeout                  = null,

                            UInt32                                MaxClientConnections               = __DefaultMaxClientConnections,
                            Boolean                               AutoStart                          = false)

            : base(IIPAddress,
                   Port,
                   ServerCertificateSelector,
                   ClientCertificateValidator,
                   ClientCertificateSelector,
                   AllowedTLSProtocols,
                   ClientCertificateRequired,
                   CheckCertificateRevocation,

                   ServiceName,
                   ServiceBanner,
                   ServerThreadName,
                   ServerThreadPriority,
                   ServerThreadIsBackground,

                   ConnectionIdBuilder,
                   //ConnectionThreadsNameBuilder,
                   //ConnectionThreadsPriorityBuilder,
                   //ConnectionThreadsAreBackground,
                   ConnectionTimeout,

                   MaxClientConnections,
                   false)

        {

            this.ServiceBanner            = ServiceBanner;
            this.SplitCharacters          = SplitCharacters != null ? SplitCharacters.ToArray() : DefaultSplitCharacters;

            this._TCPCSVProcessor         = new TCPCSVProcessor(this.SplitCharacters);
            this.SendTo(_TCPCSVProcessor);

            this._TCPCSVCommandProcessor  = new TCPCSVCommandProcessor();
            this._TCPCSVProcessor.ConnectTo(_TCPCSVCommandProcessor);
            this._TCPCSVCommandProcessor.OnNotification += ProcessBoomerang;

            if (AutoStart)
                Start();

        }

        #endregion

        #region TCPCSVServer(IPSocket, ...)

        /// <summary>
        /// Initialize the TCP server using IPAddress.Any and the given parameters.
        /// </summary>
        /// <param name="IPSocket">The IP socket to listen.</param>
        /// <param name="ServerCertificateSelector">An optional delegate to select a SSL/TLS server certificate.</param>
        /// <param name="ClientCertificateValidator">An optional delegate to verify the SSL/TLS client certificate used for authentication.</param>
        /// <param name="ClientCertificateSelector">An optional delegate to select the SSL/TLS client certificate used for authentication.</param>
        /// <param name="AllowedTLSProtocols">The SSL/TLS protocol(s) allowed for this connection.</param>
        /// <param name="ServiceName">The TCP service name shown e.g. on service startup.</param>
        /// <param name="ServiceBanner">Service banner.</param>
        /// <param name="SplitCharacters">An enumeration of delimiters to split the incoming CSV line into individual elements.</param>
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information.</param>
        /// <param name="ConnectionThreadsNameBuilder">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriorityBuilder">An optional delegate to set the priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP connection threads are background threads or not (default: yes).</param>
        /// <param name="ConnectionTimeout">The TCP client timeout for all incoming client connections in seconds (default: 30 sec).</param>
        /// <param name="MaxClientConnections">The maximum number of concurrent TCP client connections (default: 4096).</param>
        /// <param name="AutoStart">Start the TCP server thread immediately (default: no).</param>
        public TCPCSVServer(IPSocket                              IPSocket,
                            ServerCertificateSelectorDelegate?    ServerCertificateSelector          = null,
                            RemoteCertificateValidationHandler?  ClientCertificateValidator         = null,
                            LocalCertificateSelectionHandler?    ClientCertificateSelector          = null,
                            SslProtocols?                         AllowedTLSProtocols                = null,
                            Boolean?                              ClientCertificateRequired          = null,
                            Boolean?                              CheckCertificateRevocation         = null,

                            String?                               ServiceName                        = null,
                            String                                ServiceBanner                      = __DefaultServiceBanner,
                            IEnumerable<Char>?                    SplitCharacters                    = null,
                            String?                               ServerThreadName                   = null,
                            ThreadPriority                        ServerThreadPriority               = ThreadPriority.AboveNormal,
                            Boolean                               ServerThreadIsBackground           = true,

                            ConnectionIdBuilder?                  ConnectionIdBuilder                = null,
                            //ConnectionThreadsNameBuilder?         ConnectionThreadsNameBuilder       = null,
                            //ConnectionThreadsPriorityBuilder?     ConnectionThreadsPriorityBuilder   = null,
                            //Boolean                               ConnectionThreadsAreBackground     = true,
                            TimeSpan?                             ConnectionTimeout                  = null,

                            UInt32                                MaxClientConnections               = __DefaultMaxClientConnections,
                            Boolean                               AutoStart                          = false)

            : this(IPSocket.IPAddress,
                   IPSocket.Port,
                   ServerCertificateSelector,
                   ClientCertificateValidator,
                   ClientCertificateSelector,
                   AllowedTLSProtocols,
                   ClientCertificateRequired,
                   CheckCertificateRevocation,

                   ServiceName,
                   ServiceBanner,
                   SplitCharacters,
                   ServerThreadName,
                   ServerThreadPriority,
                   ServerThreadIsBackground,

                   ConnectionIdBuilder,
                   //ConnectionThreadsNameBuilder,
                   //ConnectionThreadsPriorityBuilder,
                   //ConnectionThreadsAreBackground,
                   ConnectionTimeout,

                   MaxClientConnections,
                   AutoStart)

        { }

        #endregion

        #endregion


        #region ProcessBoomerang(ConnectionId, Timestamp, CSVArray)

        private TCPResult<String> ProcessBoomerang(String    ConnectionId,
                                                   DateTime  Timestamp,
                                                   String[]  CSVArray)
        {

            OnNewData?.Invoke(ConnectionId,
                              Timestamp,
                              CSVArray);

            var OnNotificationLocal = OnNotification;
            if (OnNotificationLocal != null)
                return OnNotificationLocal(ConnectionId,
                                           Timestamp,
                                           CSVArray);

            return new TCPResult<String>(String.Empty, false);

        }

        #endregion


        //#region (override) ToString()

        ///// <summary>
        ///// Return a text representation of this object.
        ///// </summary>
        //public override String ToString()
        //{
        //    return String.Concat(ServiceBanner, " on ", IPSocket.ToString() + ((IsRunning) ? " (running)" : ""));
        //}

        //#endregion

    }

}
