/*
 * Copyright (c) 2011-2012, Achim 'ahzf' Friedland <achim@graph-database.org>
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
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using de.ahzf.Illias.Commons;
using de.ahzf.Styx;

#endregion

namespace de.ahzf.Vanaheimr.Hermod.Multicast
{

    public struct ArrowIPSource
    {

        public readonly String Address;
        public readonly UInt16 Port;

        public ArrowIPSource(String _IPAddress, UInt16 _Port)
        {
            Address = _IPAddress;
            Port    = _Port;
        }

    }

    /// <summary>
    /// The IdentityArrow is the most basic arrow.
    /// It simply sends the incoming message to the recipients without any processing.
    /// This arrow is useful in various test case situations.
    /// </summary>
    /// <typeparam name="TMessage">The type of the consuming and emitting messages/objects.</typeparam>
    public class UDPMulticastReceiverArrow<TMessage> : AbstractArrow<TMessage, TMessage>
    {

        private readonly Socket     MulticastSocket;
        private readonly IPEndPoint IPEndPoint;
        private readonly Task       ReceiverTask;


        #region Constructor(s)

        #region UDPMulticastReceiverArrow()

        /// <summary>
        /// The IdentityArrow is the most basic arrow.
        /// It simply sends the incoming message to the recipients without any processing.
        /// This arrow is useful in various test case situations.
        /// </summary>
        public UDPMulticastReceiverArrow(String MulticastAddress, Int32 IPPort)
        {

            this.MulticastSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            this.IPEndPoint      = new IPEndPoint(IPAddress.Parse(MulticastAddress), IPPort);

            IPEndPoint iep      = new IPEndPoint(IPAddress.Any, IPPort);
            EndPoint   EndPoint = (EndPoint) iep;
            MulticastSocket.Bind(iep);
            MulticastSocket.SetSocketOption(SocketOptionLevel.IP,
                                            SocketOptionName.AddMembership,
                                            new MulticastOption(IPAddress.Parse(MulticastAddress)));
            MulticastSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 50);

            ReceiverTask = Task.Factory.StartNew(() =>
            {
                byte[] data = new byte[1024];
                int recv = MulticastSocket.ReceiveFrom(data, ref EndPoint);
                this.ReceiveMessage((TMessage) (Object) Encoding.UTF8.GetString(data, 0, recv), EndPoint as IPEndPoint);
            }, TaskCreationOptions.LongRunning);

        }

        #endregion

        #region UDPMulticastReceiverArrow(MessageRecipients.Recipient, params MessageRecipients.Recipients)

        /// <summary>
        /// The IdentityArrow is the most basic arrow.
        /// It simply sends the incoming message to the recipients without any processing.
        /// This arrow is useful in various test case situations.
        /// </summary>
        /// <param name="Recipient">A recipient of the processed messages.</param>
        /// <param name="Recipients">The recipients of the processed messages.</param>
        public UDPMulticastReceiverArrow(String MulticastAddress, Int32 IPPort, MessageRecipient<TMessage> Recipient, params MessageRecipient<TMessage>[] Recipients)
            : base(Recipient, Recipients)
        {
            this.MulticastSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            this.IPEndPoint      = new IPEndPoint(IPAddress.Parse(MulticastAddress), IPPort);
        }

        #endregion

        #region UDPMulticastReceiverArrow(IArrowReceiver.Recipient, params IArrowReceiver.Recipients)

        /// <summary>
        /// The IdentityArrow is the most basic arrow.
        /// It simply sends the incoming message to the recipients without any processing.
        /// This arrow is useful in various test case situations.
        /// </summary>
        /// <param name="Recipient">A recipient of the processed messages.</param>
        /// <param name="Recipients">The recipients of the processed messages.</param>
        public UDPMulticastReceiverArrow(String MulticastAddress, Int32 IPPort, IArrowReceiver<TMessage> Recipient, params IArrowReceiver<TMessage>[] Recipients)
            : base(Recipient, Recipients)
        {
            this.MulticastSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            this.IPEndPoint      = new IPEndPoint(IPAddress.Parse(MulticastAddress), IPPort);
        }

        #endregion

        #endregion

        #region ProcessMessage(MessageIn, out MessageOut)

        /// <summary>
        /// Process the incoming message and return an outgoing message.
        /// </summary>
        /// <param name="MessageIn">The incoming message.</param>
        /// <param name="MessageOut">The outgoing message.</param>
        protected override Boolean ProcessMessage(TMessage MessageIn, out TMessage MessageOut)
        {

            MessageOut = MessageIn;
            return true;

        }

        #endregion


        public void Close()
        {
            MulticastSocket.Close();
        }

    }

}
