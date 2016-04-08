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
using System.Threading;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Sockets.UDP
{

    /// <summary>
    /// A Styx arrow sender that listens on an UDP socket and
    /// notifies about incoming UDP packets which will be
    /// converted to UTF-8, then splitted by EoL-delimiters
    /// and finally splitted by the given CSV delimiters.
    /// </summary>
    public class UDPCSVReceiver : UDPReceiver<IEnumerable<String>>
    {

        #region Data

        private readonly static String[] LineEndings      = new String[2] { "\n", "\r\n" };
        private readonly static String[] DefaultSplitter  = new String[1] { "/" };

        #endregion

        #region Properties

        #region Splitter

        private readonly String[]  _Splitter;

        /// <summary>
        /// The delimiters for splitting a line into
        /// multiple CSV elements.
        /// </summary>
        public IEnumerable<String> Splitter
        {
            get
            {
                return _Splitter;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region UDPCSVReceiver(Port, ServiceBanner, Splitter = null, ...)

        /// <summary>
        /// Create a new UDP/CSV receiver using IPAddress.Any and the given parameters.
        /// </summary>
        /// <param name="Port">The port to listen.</param>
        /// <param name="ServiceBanner">Service banner.</param>
        /// <param name="Splitter">An array of delimiters to split the incoming CSV line into individual elements.</param>
        /// <param name="ReceiverThreadName">The optional name of the UDP receiver thread.</param>
        /// <param name="ReceiverThreadPriority">The optional priority of the UDP receiver thread.</param>
        /// <param name="ReceiverThreadIsBackground">Whether the UDP receiver thread is a background thread or not.</param>
        /// <param name="PacketThreadsNameCreator">An optional delegate to set the name of the UDP packet threads.</param>
        /// <param name="PacketThreadsPriority">The optional priority of the UDP packet threads.</param>
        /// <param name="PacketThreadsAreBackground">Whether the UDP packet threads are background threads or not.</param>
        /// <param name="Autostart">Start the UDP receiver thread immediately.</param>
        public UDPCSVReceiver(IPPort                                        Port,
                              String                                        ServiceBanner               = DefaultServiceBanner,
                              IEnumerable<String>                           Splitter                    = null,
                              String                                        ReceiverThreadName          = "UDP receiver thread",
                              ThreadPriority                                ReceiverThreadPriority      = ThreadPriority.AboveNormal,
                              Boolean                                       ReceiverThreadIsBackground  = true,
                              Func<UDPPacket<IEnumerable<String>>, String>  PacketThreadsNameCreator    = null,
                              ThreadPriority                                PacketThreadsPriority       = ThreadPriority.AboveNormal,
                              Boolean                                       PacketThreadsAreBackground  = true,
                              Boolean                                       Autostart                   = false)

            : this(IPv4Address.Any,
                   Port,
                   ServiceBanner,
                   Splitter,
                   ReceiverThreadName,
                   ReceiverThreadPriority,
                   ReceiverThreadIsBackground,
                   PacketThreadsNameCreator,
                   PacketThreadsPriority,
                   PacketThreadsAreBackground,
                   Autostart)

        { }

        #endregion

        #region UDPCSVReceiver(IPAddress, Port, ServiceBanner, Splitter = null, ...) <= main constructor

        /// <summary>
        /// Create a new UDP/CSV receiver listening on the given IP address and port.
        /// </summary>
        /// <param name="IPAddress">The IP address to listen.</param>
        /// <param name="Port">The port to listen.</param>
        /// <param name="ServiceBanner">Service banner.</param>
        /// <param name="Splitter">An array of delimiters to split the incoming CSV line into individual elements.</param>
        /// <param name="ReceiverThreadName">The optional name of the UDP receiver thread.</param>
        /// <param name="ReceiverThreadPriority">The optional priority of the UDP receiver thread.</param>
        /// <param name="ReceiverThreadIsBackground">Whether the UDP receiver thread is a background thread or not.</param>
        /// <param name="PacketThreadsNameCreator">An optional delegate to set the name of the UDP packet threads.</param>
        /// <param name="PacketThreadsPriority">The optional priority of the UDP packet threads.</param>
        /// <param name="PacketThreadsAreBackground">Whether the UDP packet threads are background threads or not.</param>
        /// <param name="Autostart">Start the UDP receiver thread immediately.</param>
        public UDPCSVReceiver(IIPAddress                                    IPAddress,
                              IPPort                                        Port,
                              String                                        ServiceBanner               = DefaultServiceBanner,
                              IEnumerable<String>                           Splitter                    = null,
                              String                                        ReceiverThreadName          = "UDP receiver thread",
                              ThreadPriority                                ReceiverThreadPriority      = ThreadPriority.AboveNormal,
                              Boolean                                       ReceiverThreadIsBackground  = true,
                              Func<UDPPacket<IEnumerable<String>>, String>  PacketThreadsNameCreator    = null,
                              ThreadPriority                                PacketThreadsPriority       = ThreadPriority.AboveNormal,
                              Boolean                                       PacketThreadsAreBackground  = true,
                              Boolean                                       Autostart                   = false)

            : base(IPAddress,
                   Port,
                   ServiceBanner,

                   // Mapper delegate <= do not use!
                   null,

                   // MapReduce delegate <= will automatically be reduced to multiple events!
                   (UDPReceiver, Timestamp, LocalSocket, RemoteSocket, Message) =>
                             Message.ToUTF8String().
                                     Trim().
                                     Split(LineEndings,
                                           StringSplitOptions.RemoveEmptyEntries).
                                     Select(CSVLine => CSVLine.Trim().
                                                               Split ((Splitter != null) ? Splitter.ToArray() : DefaultSplitter,
                                                                      StringSplitOptions.None).
                                                               Select(CSVElement => CSVElement.Trim())),

                   ReceiverThreadName,
                   ReceiverThreadPriority,
                   ReceiverThreadIsBackground,
                   PacketThreadsNameCreator,
                   PacketThreadsPriority,
                   PacketThreadsAreBackground,
                   Autostart)

        {

            this._Splitter  = (Splitter != null) ? Splitter.ToArray() : DefaultSplitter;

        }

        #endregion

        #region UDPCSVReceiver(IPSocket, ServiceBanner, Splitter = null, ...)

        /// <summary>
        /// Create a new UDP/CSV receiver listening on the given IP socket.
        /// </summary>
        /// <param name="IPSocket">The IP socket to listen.</param>
        /// <param name="ServiceBanner">Service banner.</param>
        /// <param name="Splitter">An array of delimiters to split the incoming CSV line into individual elements.</param>
        /// <param name="ReceiverThreadName">The optional name of the UDP receiver thread.</param>
        /// <param name="ReceiverThreadPriority">The optional priority of the UDP receiver thread.</param>
        /// <param name="ReceiverThreadIsBackground">Whether the UDP receiver thread is a background thread or not.</param>
        /// <param name="PacketThreadsNameCreator">An optional delegate to set the name of the UDP packet threads.</param>
        /// <param name="PacketThreadsPriority">The optional priority of the UDP packet threads.</param>
        /// <param name="PacketThreadsAreBackground">Whether the UDP packet threads are background threads or not.</param>
        /// <param name="Autostart">Start the UDP receiver thread immediately.</param>
        public UDPCSVReceiver(IPSocket                                      IPSocket,
                              String                                        ServiceBanner               = DefaultServiceBanner,
                              IEnumerable<String>                           Splitter                    = null,
                              String                                        ReceiverThreadName          = "UDP receiver thread",
                              ThreadPriority                                ReceiverThreadPriority      = ThreadPriority.AboveNormal,
                              Boolean                                       ReceiverThreadIsBackground  = true,
                              Func<UDPPacket<IEnumerable<String>>, String>  PacketThreadsNameCreator    = null,
                              ThreadPriority                                PacketThreadsPriority       = ThreadPriority.AboveNormal,
                              Boolean                                       PacketThreadsAreBackground  = true,
                              Boolean                                       Autostart                   = false)

            : this(IPSocket.IPAddress,
                   IPSocket.Port,
                   ServiceBanner,
                   Splitter,
                   ReceiverThreadName,
                   ReceiverThreadPriority,
                   ReceiverThreadIsBackground,
                   PacketThreadsNameCreator,
                   PacketThreadsPriority,
                   PacketThreadsAreBackground,
                   Autostart)

        { }

        #endregion

        #endregion

    }

}
