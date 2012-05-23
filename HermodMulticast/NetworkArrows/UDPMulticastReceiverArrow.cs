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
using System.Threading.Tasks;

using de.ahzf.Styx;
using de.ahzf.Hermod.Datastructures;
using System.Threading;

#endregion

namespace de.ahzf.Vanaheimr.Hermod.Multicast
{

    /// <summary>
    /// The UDPMulticastReceiverArrow receives messages from
    /// to the given IP multicast group and forwards them to
    /// this receivers.
    /// </summary>
    /// <typeparam name="TMessage">The type of the consuming and emitting messages/objects.</typeparam>
    public class UDPMulticastReceiverArrow<TMessage> : AbstractArrowSender<TMessage>
    {

        #region Data

        private readonly Socket                   MulticastSocket;
        private readonly IPEndPoint               MulticastIPEndPoint;
        private          Task                     ReceiverTask;
        private          EndPoint                 LocalEndPoint;
        private          IPEndPoint               LocalIPEndPoint;
        private          CancellationTokenSource  CancellationTokenSource;
        private          CancellationToken        CancellationToken;

        #endregion

        #region Properties

        #region HopCount

        /// <summary>
        /// The minimal acceptable IPv6 hop-count or IPv4 time-to-live value of the
        /// incoming IP Multicast packets.
        /// It is best practice for security applications to set the HopCount on the
        /// sender side to its max value of 255 and configure an accept threshold on
        /// the receiver side to 255. This way only packets from the local network
        /// are accepted.
        /// </summary>
        public Byte HopCountThreshold { get; set; }

        #endregion

        #endregion

        #region Constructor(s)

        #region UDPMulticastReceiverArrow(MulticastAddress, IPPort, HopCountThreshold = 255)

        /// <summary>
        /// The UDPMulticastReceiverArrow receives messages from
        /// to the given IP multicast group and forwards them to
        /// this receivers.
        /// </summary>
        /// <param name="MulticastAddress">The multicast address to join.</param>
        /// <param name="IPPort">The outgoing IP port to use.</param>
        /// <param name="HopCountThreshold">The minimal acceptable IPv6 hop-count or IPv4 time-to-live value of the incoming IP Multicast packets.</param>
        public UDPMulticastReceiverArrow(String MulticastAddress, IPPort IPPort, Byte HopCountThreshold = 255)
        {

            this.MulticastSocket         = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            this.MulticastIPEndPoint     = new IPEndPoint(IPAddress.Parse(MulticastAddress), IPPort.ToInt32());
            this.LocalIPEndPoint         = new IPEndPoint(IPAddress.Any, IPPort.ToInt32());
            this.LocalEndPoint           = (EndPoint) LocalIPEndPoint;
            this.CancellationTokenSource = new CancellationTokenSource();
            this.CancellationToken       = CancellationTokenSource.Token;

            ReceiverTask = Task.Factory.StartNew((Object) =>
            {

                MulticastSocket.SetSocketOption(SocketOptionLevel.Socket,
                                                SocketOptionName.ReceiveTimeout, 1000);

                MulticastSocket.Bind(LocalIPEndPoint);

                MulticastSocket.SetSocketOption(SocketOptionLevel.IP,
                                                SocketOptionName.AddMembership,
                                                new MulticastOption(IPAddress.Parse(MulticastAddress)));



                while (!CancellationToken.IsCancellationRequested)
                {

                    var data = new Byte[65536];

                    try
                    {

                        int recv = MulticastSocket.ReceiveFrom(data, ref LocalEndPoint);

                        this.NotifyRecipients(new ArrowIPSource(
                                                 (LocalEndPoint as IPEndPoint).Address.ToString(),
                                                 IPPort.Parse((LocalEndPoint as IPEndPoint).Port)
                                             ),
                                             (TMessage) (Object) Encoding.UTF8.GetString(data, 0, recv));

                    }

                    // Catch ReadTimeout...
                    catch (SocketException SocketException)
                    { }

                }

            }, TaskCreationOptions.LongRunning,
               CancellationTokenSource.Token,
               TaskCreationOptions.LongRunning|TaskCreationOptions.AttachedToParent,
               TaskScheduler.Default);

        }

        #endregion

        #region UDPMulticastReceiverArrow(MulticastAddress, IPPort, MessageRecipients.Recipient, params MessageRecipients.Recipients)

        /// <summary>
        /// The UDPMulticastReceiverArrow receives messages from
        /// to the given IP multicast group and forwards them to
        /// this receivers.
        /// </summary>
        /// <param name="MulticastAddress">The multicast address to join.</param>
        /// <param name="IPPort">The outgoing IP port to use.</param>
        /// <param name="Recipient">A recipient of the processed messages.</param>
        /// <param name="Recipients">The recipients of the processed messages.</param>
        public UDPMulticastReceiverArrow(String MulticastAddress, IPPort IPPort, MessageRecipient<TMessage> Recipient, params MessageRecipient<TMessage>[] Recipients)
            : base()
        {
            SendTo(Recipient);
            SendTo(Recipients);
        }

        #endregion

        #region UDPMulticastReceiverArrow(MulticastAddress, IPPort, IArrowReceiver.Recipient, params IArrowReceiver.Recipients)

        /// <summary>
        /// The UDPMulticastReceiverArrow receives messages from
        /// to the given IP multicast group and forwards them to
        /// this receivers.
        /// </summary>
        /// <param name="MulticastAddress">The multicast address to join.</param>
        /// <param name="IPPort">The outgoing IP port to use.</param>
        /// <param name="Recipient">A recipient of the processed messages.</param>
        /// <param name="Recipients">The recipients of the processed messages.</param>
        public UDPMulticastReceiverArrow(String MulticastAddress, IPPort IPPort, IArrowReceiver<TMessage> Recipient, params IArrowReceiver<TMessage>[] Recipients)
            : base()
        {
            SendTo(Recipient);
            SendTo(Recipients);
        }

        #endregion

        #endregion


        private void StartMulticastServerThread(String MulticastAddress, IPPort IPPort, Byte HopCountThreshold = 255)
        {

            
            
            

            

        }


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
