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

using eu.Vanaheimr.Styx.Arrows;
using eu.Vanaheimr.Hermod.Sockets.TCP;

#endregion

namespace eu.Vanaheimr.Hermod.Services.CSV
{

    /// <summary>
    /// A TCP service accepting incoming UTF8 encoded
    /// comma-separated values with 0x00, 0x0a (\n) or
    /// 0x0d 0x0a (\r\n) end-of-line characters.
    /// </summary>
    public class TCPCSVServer : ACustomTCPServer,
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
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder"></param>
        /// <param name="ConnectionThreadsNameCreator">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriority">The optional priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP conncection threads are background threads or not.</param>
        /// <param name="ConnectionTimeoutSeconds">The TCP client timeout for all incoming client connections in seconds.</param>
        /// <param name="Autostart">Start the TCP server thread immediately.</param>
        public TCPCSVServer(IPPort                       Port,
                            Char[]                       SplitCharacters                 = null,
                            String                       ServerThreadName                = null,
                            ThreadPriority               ServerThreadPriority            = ThreadPriority.AboveNormal,
                            Boolean                      ServerThreadIsBackground        = true,
                            Func<IPSocket, String>       ConnectionIdBuilder             = null,
                            Func<TCPConnection, String>  ConnectionThreadsNameCreator    = null,
                            ThreadPriority               ConnectionThreadsPriority       = ThreadPriority.AboveNormal,
                            Boolean                      ConnectionThreadsAreBackground  = true,
                            UInt64                       ConnectionTimeoutSeconds        = 30,
                            Boolean                      Autostart                       = false)

            : this(IPv4Address.Any,
                   Port,
                   SplitCharacters,
                   ServerThreadName,
                   ServerThreadPriority,
                   ServerThreadIsBackground,
                   ConnectionIdBuilder,
                   ConnectionThreadsNameCreator,
                   ConnectionThreadsPriority,
                   ConnectionThreadsAreBackground,
                   ConnectionTimeoutSeconds,
                   Autostart)

        { }

        #endregion

        #region TCPCSVServer(IIPAddress, Port, ...)

        /// <summary>
        /// Initialize the TCP server using the given parameters.
        /// </summary>
        /// <param name="IIPAddress">The listening IP address(es)</param>
        /// <param name="Port">The listening port</param>
        /// <param name="Mapper">A delegate to transform the incoming TCP connection data into custom data structures.</param>
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder"></param>
        /// <param name="ConnectionThreadsNameCreator">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriority">The optional priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP conncection threads are background threads or not.</param>
        /// <param name="ConnectionTimeoutSeconds">The TCP client timeout for all incoming client connections in seconds.</param>
        /// <param name="Autostart">Start the TCP server thread immediately.</param>
        public TCPCSVServer(IIPAddress                   IIPAddress,
                            IPPort                       Port,
                            Char[]                       SplitCharacters                 = null,
                            String                       ServerThreadName                = null,
                            ThreadPriority               ServerThreadPriority            = ThreadPriority.AboveNormal,
                            Boolean                      ServerThreadIsBackground        = true,
                            Func<IPSocket, String>       ConnectionIdBuilder             = null,
                            Func<TCPConnection, String>  ConnectionThreadsNameCreator    = null,
                            ThreadPriority               ConnectionThreadsPriority       = ThreadPriority.AboveNormal,
                            Boolean                      ConnectionThreadsAreBackground  = true,
                            UInt64                       ConnectionTimeoutSeconds        = 30,
                            Boolean                      Autostart                       = false)

            : base(IIPAddress,
                   Port,
                   ServerThreadName,
                   ServerThreadPriority,
                   ServerThreadIsBackground,
                   ConnectionIdBuilder,
                   ConnectionThreadsNameCreator,
                   ConnectionThreadsPriority,
                   ConnectionThreadsAreBackground,
                   ConnectionTimeoutSeconds)

        {

            this.ServiceBanner            = DefaultServiceBanner;

            this._TCPCSVProcessor         = new TCPCSVProcessor(SplitCharacters);
            this._TCPServer.SendTo(_TCPCSVProcessor);
            this._TCPServer.OnNewConnection    += SendNewConnection;
            this._TCPServer.OnConnectionClosed += SendConnectionClosed;

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
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder"></param>
        /// <param name="ConnectionThreadsNameCreator">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriority">The optional priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP conncection threads are background threads or not.</param>
        /// <param name="ConnectionTimeoutSeconds">The TCP client timeout for all incoming client connections in seconds.</param>
        /// <param name="Autostart">Start the TCP server thread immediately.</param>
        public TCPCSVServer(IPSocket                     IPSocket,
                            Char[]                       SplitCharacters                 = null,
                            String                       ServerThreadName                = null,
                            ThreadPriority               ServerThreadPriority            = ThreadPriority.AboveNormal,
                            Boolean                      ServerThreadIsBackground        = true,
                            Func<IPSocket, String>       ConnectionIdBuilder             = null,
                            Func<TCPConnection, String>  ConnectionThreadsNameCreator    = null,
                            ThreadPriority               ConnectionThreadsPriority       = ThreadPriority.AboveNormal,
                            Boolean                      ConnectionThreadsAreBackground  = true,
                            UInt64                       ConnectionTimeoutSeconds        = 30,
                            Boolean                      Autostart                       = false)

            : this(IPSocket.IPAddress,
                   IPSocket.Port,
                   SplitCharacters,
                   ServerThreadName,
                   ServerThreadPriority,
                   ServerThreadIsBackground,
                   ConnectionIdBuilder,
                   ConnectionThreadsNameCreator,
                   ConnectionThreadsPriority,
                   ConnectionThreadsAreBackground,
                   ConnectionTimeoutSeconds,
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
