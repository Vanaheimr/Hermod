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
using System.Threading;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Styx.Arrows;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;

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

        private const    String                  DefaultServiceBanner = "Vanaheimr Hermod TCP/CSV Service v0.9";

        private readonly TCPCSVProcessor         _TCPCSVProcessor;
        private readonly TCPCSVCommandProcessor  _TCPCSVCommandProcessor;

        #endregion

        #region Properties

        #region ServiceBanner

        public String ServiceBanner { get; set; }

        #endregion

        #region SplitCharacters

        private readonly Char[] _SplitCharacters;

        /// <summary>
        /// The characters to split the incoming CSV text lines.
        /// </summary>
        public Char[] SplitCharacters
        {
            get
            {
                return _SplitCharacters;
            }
        }

        #endregion

        #endregion

        #region Events

        public event BoomerangSenderHandler<String, DateTime, String[], TCPResult<String>> OnNotification;

        #endregion

        #region Constructor(s)

        #region TCPCSVServer(Port, ...)

        /// <summary>
        /// Initialize the TCP server using IPAddress.Any and the given parameters.
        /// </summary>
        /// <param name="Port">The listening port</param>
        /// <param name="ServiceBanner">Service banner.</param>
        /// <param name="Splitter">An array of delimiters to split the incoming CSV line into individual elements.</param>
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information.</param>
        /// <param name="ConnectionThreadsNameBuilder">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriorityBuilder">An optional delegate to set the priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP connection threads are background threads or not (default: yes).</param>
        /// <param name="ConnectionTimeout">The TCP client timeout for all incoming client connections in seconds (default: 30 sec).</param>
        /// <param name="MaxClientConnections">The maximum number of concurrent TCP client connections (default: 4096).</param>
        /// <param name="Autostart">Start the TCP server thread immediately (default: no).</param>
        public TCPCSVServer(IPPort                            Port,
                            String                            ServiceBanner                     = DefaultServiceBanner,
                            IEnumerable<String>               Splitter                          = null,
                            String                            ServerThreadName                  = null,
                            ThreadPriority                    ServerThreadPriority              = ThreadPriority.AboveNormal,
                            Boolean                           ServerThreadIsBackground          = true,
                            ConnectionIdBuilder               ConnectionIdBuilder               = null,
                            ConnectionThreadsNameBuilder      ConnectionThreadsNameBuilder      = null,
                            ConnectionThreadsPriorityBuilder  ConnectionThreadsPriorityBuilder  = null,
                            Boolean                           ConnectionThreadsAreBackground    = true,
                            TimeSpan?                         ConnectionTimeout                 = null,
                            UInt32                            MaxClientConnections              = __DefaultMaxClientConnections,
                            Boolean                           Autostart                         = false)

            : this(IPv4Address.Any,
                   Port,
                   ServiceBanner,
                   Splitter,
                   ServerThreadName,
                   ServerThreadPriority,
                   ServerThreadIsBackground,
                   ConnectionIdBuilder,
                   ConnectionThreadsNameBuilder,
                   ConnectionThreadsPriorityBuilder,
                   ConnectionThreadsAreBackground,
                   ConnectionTimeout,
                   MaxClientConnections,
                   Autostart)

        { }

        #endregion

        #region TCPCSVServer(IIPAddress, Port, ...)

        /// <summary>
        /// Initialize the TCP server using the given parameters.
        /// </summary>
        /// <param name="IIPAddress">The listening IP address(es)</param>
        /// <param name="Port">The listening port</param>
        /// <param name="ServiceBanner">Service banner.</param>
        /// <param name="Splitter">An array of delimiters to split the incoming CSV line into individual elements.</param>
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information.</param>
        /// <param name="ConnectionThreadsNameBuilder">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriorityBuilder">An optional delegate to set the priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP connection threads are background threads or not (default: yes).</param>
        /// <param name="ConnectionTimeout">The TCP client timeout for all incoming client connections in seconds (default: 30 sec).</param>
        /// <param name="MaxClientConnections">The maximum number of concurrent TCP client connections (default: 4096).</param>
        /// <param name="Autostart">Start the TCP/CSV server thread immediately (default: no).</param>
        public TCPCSVServer(IIPAddress                        IIPAddress,
                            IPPort                            Port,
                            String                            ServiceBanner                   = DefaultServiceBanner,
                            IEnumerable<String>               Splitter                          = null,
                            String                            ServerThreadName                  = null,
                            ThreadPriority                    ServerThreadPriority              = ThreadPriority.AboveNormal,
                            Boolean                           ServerThreadIsBackground          = true,
                            ConnectionIdBuilder               ConnectionIdBuilder               = null,
                            ConnectionThreadsNameBuilder      ConnectionThreadsNameBuilder      = null,
                            ConnectionThreadsPriorityBuilder  ConnectionThreadsPriorityBuilder  = null,
                            Boolean                           ConnectionThreadsAreBackground    = true,
                            TimeSpan?                         ConnectionTimeout                 = null,
                            UInt32                            MaxClientConnections              = __DefaultMaxClientConnections,
                            Boolean                           Autostart                         = false)

            : base(IIPAddress,
                   Port,
                   ServiceBanner,
                   ServerThreadName,
                   ServerThreadPriority,
                   ServerThreadIsBackground,
                   ConnectionIdBuilder,
                   ConnectionThreadsNameBuilder,
                   ConnectionThreadsPriorityBuilder,
                   ConnectionThreadsAreBackground,
                   ConnectionTimeout,
                   MaxClientConnections,
                   false)

        {

            this.ServiceBanner            = DefaultServiceBanner;

            this._TCPCSVProcessor         = new TCPCSVProcessor(SplitCharacters);
            this.SendTo(_TCPCSVProcessor);
            this.OnNewConnection         += (TCPServer, Timestamp, RemoteSocket, ConnectionId, TCPConnection) => SendNewConnection   (Timestamp, RemoteSocket, ConnectionId, TCPConnection);
            this.OnConnectionClosed      += (TCPServer, Timestamp, RemoteSocket, ConnectionId, ClosedBy)      => SendConnectionClosed(Timestamp, RemoteSocket, ConnectionId, ClosedBy);

            this._TCPCSVCommandProcessor  = new TCPCSVCommandProcessor();
            this._TCPCSVProcessor.ConnectTo(_TCPCSVCommandProcessor);
            this._TCPCSVCommandProcessor.OnNotification += ProcessBoomerang;

            if (Autostart)
                Start();

        }

        #endregion

        #region TCPCSVServer(IPSocket, ...)

        /// <summary>
        /// Initialize the TCP server using IPAddress.Any and the given parameters.
        /// </summary>
        /// <param name="IPSocket">The IP socket to listen.</param>
        /// <param name="ServiceBanner">Service banner.</param>
        /// <param name="Splitter">An array of delimiters to split the incoming CSV line into individual elements.</param>
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information.</param>
        /// <param name="ConnectionThreadsNameBuilder">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriorityBuilder">An optional delegate to set the priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP connection threads are background threads or not (default: yes).</param>
        /// <param name="ConnectionTimeout">The TCP client timeout for all incoming client connections in seconds (default: 30 sec).</param>
        /// <param name="MaxClientConnections">The maximum number of concurrent TCP client connections (default: 4096).</param>
        /// <param name="Autostart">Start the TCP server thread immediately (default: no).</param>
        public TCPCSVServer(IPSocket                          IPSocket,
                            String                            ServiceBanner                     = DefaultServiceBanner,
                            IEnumerable<String>               Splitter                          = null,
                            String                            ServerThreadName                  = null,
                            ThreadPriority                    ServerThreadPriority              = ThreadPriority.AboveNormal,
                            Boolean                           ServerThreadIsBackground          = true,
                            ConnectionIdBuilder               ConnectionIdBuilder               = null,
                            ConnectionThreadsNameBuilder      ConnectionThreadsNameBuilder      = null,
                            ConnectionThreadsPriorityBuilder  ConnectionThreadsPriorityBuilder  = null,
                            Boolean                           ConnectionThreadsAreBackground    = true,
                            TimeSpan?                         ConnectionTimeout                 = null,
                            UInt32                            MaxClientConnections              = __DefaultMaxClientConnections,
                            Boolean                           Autostart                         = false)

            : this(IPSocket.IPAddress,
                   IPSocket.Port,
                   ServiceBanner,
                   Splitter,
                   ServerThreadName,
                   ServerThreadPriority,
                   ServerThreadIsBackground,
                   ConnectionIdBuilder,
                   ConnectionThreadsNameBuilder,
                   ConnectionThreadsPriorityBuilder,
                   ConnectionThreadsAreBackground,
                   ConnectionTimeout,
                   MaxClientConnections,
                   Autostart)

        { }

        #endregion

        #endregion


        #region ProcessBoomerang(ConnectionId, Timestamp, CSVArray)

        private TCPResult<String> ProcessBoomerang(String    ConnectionId,
                                                   DateTime  Timestamp,
                                                   String[]  CSVArray)
        {

            var OnNotificationLocal = OnNotification;
            if (OnNotificationLocal != null)
                return OnNotificationLocal(ConnectionId,
                                           Timestamp,
                                           CSVArray);

            return new TCPResult<String>(String.Empty, false);

        }

        #endregion


    }

}
