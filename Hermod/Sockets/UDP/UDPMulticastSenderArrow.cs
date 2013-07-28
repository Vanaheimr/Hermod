/*
 * Copyright (c) 2011-2013, Achim 'ahzf' Friedland <achim@graph-database.org>
 * This file is part of Styx <http://www.github.com/Vanaheimr/Hermod>
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
using System.Net;
using System.Net.Sockets;

using eu.Vanaheimr.Illias.Commons;
using eu.Vanaheimr.Styx;
using eu.Vanaheimr.Hermod.Datastructures;

#endregion

namespace eu.Vanaheimr.Hermod.Multicast
{

    /// <summary>
    /// The UDPMulticastSenderArrow sends the incoming message
    /// to the given IP multicast group.
    /// </summary>
    /// <typeparam name="TIn">The type of the consuming messages/objects.</typeparam>
    public class UDPMulticastSenderArrow<TIn> : AbstractArrowReceiver<TIn>
    {

        #region Data

        private readonly Socket     MulticastSocket;
        private readonly IPEndPoint IPEndPoint;

        #endregion

        #region Properties

        #region HopCount

        /// <summary>
        /// The IPv6 hop-count or IPv4 time-to-live field
        /// of the outgoing IP multicast packets.
        /// </summary>
        public Byte HopCount
        {

            get
            {
                return (Byte) this.MulticastSocket.GetSocketOption(SocketOptionLevel.IP,
                                                                   SocketOptionName.MulticastTimeToLive);
            }

            set
            {
                this.MulticastSocket.SetSocketOption(SocketOptionLevel.IP,
                                                     SocketOptionName.MulticastTimeToLive,
                                                     value);
            }

        }

        #endregion

        #endregion

        #region Constructor(s)

        #region UDPMulticastSenderArrow(MulticastAddress, IPPort, HopCount = 255)

        /// <summary>
        /// The UDPMulticastSenderArrow sends the incoming message
        /// to the given IP multicast group.
        /// </summary>
        /// <param name="MulticastAddress">The multicast address to join.</param>
        /// <param name="IPPort">The outgoing IP port to use.</param>
        /// <param name="HopCount">The IPv6 hop-count or IPv4 time-to-live field of the outgoing IP multicast packets.</param>
        public UDPMulticastSenderArrow(String MulticastAddress, IPPort IPPort, Byte HopCount = 255)
        {
            this.MulticastSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            this.IPEndPoint      = new IPEndPoint(IPAddress.Parse(MulticastAddress), IPPort.ToInt32());
            this.HopCount        = HopCount;
        }

        #endregion

        #endregion


        #region ReceiveMessage(MessageIn)

        /// <summary>
        /// Accepts a message of type S from a sender for further processing
        /// and delivery to the subscribers.
        /// </summary>
        /// <param name="Sender">The sender of the message.</param>
        /// <param name="MessageIn">The message.</param>
        public override void ProcessArrow(TIn MessageIn)
        {
            var sent = MulticastSocket.SendTo(MessageIn.ToString().ToUTF8Bytes(), IPEndPoint);
        }

        #endregion


        #region Close()

        /// <summary>
        /// Close the multicast socket.
        /// </summary>
        public void Close()
        {
            MulticastSocket.Close();
        }

        #endregion

    }

}
