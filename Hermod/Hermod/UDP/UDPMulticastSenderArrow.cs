/*
 * Copyright (c) 2011-2013, Achim Friedland <achim.friedland@graphdefined.com>
 * This file is part of Styx <https://www.github.com/Vanaheimr/Hermod>
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

using System.Net;
using System.Net.Sockets;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Styx.Arrows;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.UDP
{

    /// <summary>
    /// The UDPMulticastSenderArrow sends the incoming message
    /// to the given IP multicast group.
    /// </summary>
    /// <typeparam name="TIn">The type of the consuming messages/objects.</typeparam>
    public class UDPMulticastSenderArrow<TIn> : AbstractArrowReceiver<TIn>
    {

        #region Data

        private readonly Socket      multicastSocket;
        private readonly IPEndPoint  ipEndPoint;

        #endregion

        #region Properties

        /// <summary>
        /// The IPv6 hop-count or IPv4 time-to-live field
        /// of the outgoing IP multicast packets.
        /// </summary>
        public Byte HopCount
        {

            get
            {
                return (Byte) this.multicastSocket.GetSocketOption(SocketOptionLevel.IP,
                                                                   SocketOptionName.MulticastTimeToLive);
            }

            set
            {
                this.multicastSocket.SetSocketOption(SocketOptionLevel.IP,
                                                     SocketOptionName.MulticastTimeToLive,
                                                     value);
            }

        }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// The UDPMulticastSenderArrow sends the incoming message
        /// to the given IP multicast group.
        /// </summary>
        /// <param name="MulticastAddress">The multicast address to join.</param>
        /// <param name="IPPort">The outgoing IP port to use.</param>
        /// <param name="HopCount">The IPv6 hop-count or IPv4 time-to-live field of the outgoing IP multicast packets.</param>
        public UDPMulticastSenderArrow(IIPAddress  MulticastAddress,
                                       IPPort      IPPort,
                                       Byte        HopCount = 255)
        {

            this.multicastSocket  = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            this.ipEndPoint       = new IPEndPoint(System.Net.IPAddress.Parse(MulticastAddress.ToString()), IPPort.ToInt32());
            this.HopCount         = HopCount;

        }

        #endregion


        #region ReceiveMessage(MessageIn)

        /// <summary>
        /// Accepts a message of type S from a sender for further processing
        /// and delivery to the subscribers.
        /// </summary>
        /// <param name="MessageIn">The message.</param>
        public override void ProcessArrow(EventTracking_Id  EventTrackingId,
                                          TIn               MessageIn)
        {
            if (MessageIn is not null)
            {

                var data = MessageIn.ToString().ToUTF8Bytes();
                var sent = multicastSocket.SendTo(data, ipEndPoint);

                //if (data.Length != sent)
                    //OnExceptionOccured?.Invoke(this, new Exception("Not all data was sent!"));

            }
        }

        #endregion


        #region Close()

        /// <summary>
        /// Close the multicast socket.
        /// </summary>
        public void Close()
        {
            multicastSocket.Close();
        }

        #endregion

    }

}
