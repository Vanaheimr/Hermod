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
using System.Net.Sockets;
using System.Net;
using System.Text;

using de.ahzf.Illias.Commons;
using de.ahzf.Styx;

#endregion

namespace de.ahzf.Vanaheimr.Hermod.Multicast
{

    /// <summary>
    /// The IdentityArrow is the most basic arrow.
    /// It simply sends the incoming message to the recipients without any processing.
    /// This arrow is useful in various test case situations.
    /// </summary>
    /// <typeparam name="TMessage">The type of the consuming and emitting messages/objects.</typeparam>
    public class UDPMulticastSenderArrow<TMessage> : AbstractArrow<TMessage, TMessage>
    {

        private readonly Socket     MulticastSocket;
        private readonly IPEndPoint IPEndPoint;


        #region Constructor(s)

        #region UDPMulticastSenderArrow()

        /// <summary>
        /// The IdentityArrow is the most basic arrow.
        /// It simply sends the incoming message to the recipients without any processing.
        /// This arrow is useful in various test case situations.
        /// </summary>
        public UDPMulticastSenderArrow(String MulticastAddress, Int32 IPPort)
        {
            this.MulticastSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            this.IPEndPoint      = new IPEndPoint(IPAddress.Parse(MulticastAddress), IPPort);
        }

        #endregion

        #region UDPMulticastSenderArrow(MessageRecipients.Recipient, params MessageRecipients.Recipients)

        /// <summary>
        /// The IdentityArrow is the most basic arrow.
        /// It simply sends the incoming message to the recipients without any processing.
        /// This arrow is useful in various test case situations.
        /// </summary>
        /// <param name="Recipient">A recipient of the processed messages.</param>
        /// <param name="Recipients">The recipients of the processed messages.</param>
        public UDPMulticastSenderArrow(String MulticastAddress, Int32 IPPort, MessageRecipient<TMessage> Recipient, params MessageRecipient<TMessage>[] Recipients)
            : base(Recipient, Recipients)
        {
            this.MulticastSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            this.IPEndPoint      = new IPEndPoint(IPAddress.Parse(MulticastAddress), IPPort);
        }

        #endregion

        #region UDPMulticastSenderArrow(IArrowReceiver.Recipient, params IArrowReceiver.Recipients)

        /// <summary>
        /// The IdentityArrow is the most basic arrow.
        /// It simply sends the incoming message to the recipients without any processing.
        /// This arrow is useful in various test case situations.
        /// </summary>
        /// <param name="Recipient">A recipient of the processed messages.</param>
        /// <param name="Recipients">The recipients of the processed messages.</param>
        public UDPMulticastSenderArrow(String MulticastAddress, Int32 IPPort, IArrowReceiver<TMessage> Recipient, params IArrowReceiver<TMessage>[] Recipients)
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

            MulticastSocket.SendTo(MessageIn.ToString().ToUTF8Bytes(), IPEndPoint);

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
