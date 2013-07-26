/*
 * Copyright (c) 2010-2013, Achim 'ahzf' Friedland <achim@graph-database.org>
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
using System.Net;
using System.Net.Sockets;
using eu.Vanaheimr.Styx;
using eu.Vanaheimr.Hermod.Datastructures;

#endregion

namespace eu.Vanaheimr.Hermod.Sockets.UDP
{

    /// <summary>
    /// An UDP client acceptiong Vanaheimr Stxy arrows/notifications.
    /// For testing via NetCat type: 'nc -lup 5000'
    /// </summary>
    public class UDPClient<T> : ITarget<T>,
                                ITarget<UDPPacket<T>>,
                                IDisposable
    {

        #region Data

        private readonly Func<T, Byte[]>  MessageProcessor;
        private readonly Socket           DotNetSocket;
        private          IPAddress        DotNetIPAddress;
        private          IPEndPoint       RemoteIPEndPoint;

        #endregion

        #region Properties

        #region Hostname

        private String _Hostname;

        /// <summary>
        /// The hostname to send the UDP data.
        /// </summary>
        public String Hostname
        {

            get
            {
                return _Hostname;
            }

            set
            {

                if (value == null)
                    throw new ArgumentNullException("The hostname must not be null!");

                try
                {

                    var IPAdresses = Dns.GetHostAddresses(value);

                    _IPAddress      = IPv4Address.Parse(IPAdresses[0].ToString());
                    DotNetIPAddress = IPAdresses[0];

                }
                catch (Exception e)
                {
                    throw new ArgumentException("The DNS lookup of the hostname lead to an error!", e);
                }

                _Hostname = value;

                if (_Port != null)
                    SetRemoteIPEndPoint();

            }

        }

        #endregion

        #region IPAddress

        private IIPAddress _IPAddress;

        /// <summary>
        /// The IP address to send the UDP data.
        /// </summary>
        public IIPAddress IPAddress
        {

            get
            {
                return _IPAddress;
            }

            set
            {

                if (value == null)
                    throw new ArgumentNullException("The IPAddress must not be null!");

                try
                {
                    DotNetIPAddress = System.Net.IPAddress.Parse(value.ToString());
                }
                catch (Exception e)
                {
                    throw new ArgumentException("The IPAddress is invalid!", e);
                }

                _IPAddress = value;

                if (_Port != null)
                    SetRemoteIPEndPoint();

            }

        }

        #endregion

        #region Port

        private IPPort _Port;

        /// <summary>
        /// The IP port to send the UDP data.
        /// </summary>
        public IPPort Port
        {

            get
            {
                return _Port;
            }

            set
            {

                if (value == null)
                    throw new ArgumentNullException("The port must not be null!");

                _Port = value;

                if (_Hostname != null && DotNetIPAddress != null)
                    SetRemoteIPEndPoint();

            }

        }

        #endregion

        #region UDPSocketFlags

        /// <summary>
        /// Specifies socket send behaviors.
        /// </summary>
        public SocketFlags UDPSocketFlags { get; set; }

        #endregion

        #endregion

        #region Constructor(s)

        #region (private) UDPClient(MessageProcessor, Port)

        /// <summary>
        /// Create a new UDPClient.
        /// </summary>
        /// <param name="MessageProcessor">A delegate to tranform the message into an array of bytes.</param>
        /// <param name="Port">The IP port to send the UDP data.</param>
        private UDPClient(Func<T, Byte[]>  MessageProcessor,
                          IPPort           Port)
        {

            if (MessageProcessor == null)
                throw new ArgumentNullException("The MessageProcessor must not be null!");

            this.MessageProcessor  = MessageProcessor;
            this.DotNetSocket      = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            this.UDPSocketFlags    = SocketFlags.None;
            this.Port              = Port;

        }

        #endregion

        #region UDPClient(MessageProcessor, Hostname, Port)

        /// <summary>
        /// Create a new UDPClient.
        /// </summary>
        /// <param name="MessageProcessor">A delegate to tranform the message into an array of bytes.</param>
        /// <param name="Hostname">The hostname to send the UDP data.</param>
        /// <param name="Port">The IP port to send the UDP data.</param>
        public UDPClient(Func<T, Byte[]>  MessageProcessor,
                         String           Hostname,
                         IPPort           Port)

            : this(MessageProcessor, Port)

        {
            this.Hostname = Hostname;
        }

        #endregion

        #region UDPClient(MessageProcessor, IPAddress, Port)

        /// <summary>
        /// Create a new UDPClient.
        /// </summary>
        /// <param name="MessageProcessor">A delegate to tranform the message into an array of bytes.</param>
        /// <param name="IPAddress">The IP address to send the UDP data.</param>
        /// <param name="Port">The IP port to send the UDP data.</param>
        public UDPClient(Func<T, Byte[]>  MessageProcessor,
                         IIPAddress       IPAddress,
                         IPPort           Port)

            : this(MessageProcessor, Port)

        {
            this.IPAddress = IPAddress;
        }

        #endregion

        #endregion


        #region (private) SetRemoteIPEndPoint(Connect = true)

        private void SetRemoteIPEndPoint(Boolean Connect = true)
        {

            try
            {
                RemoteIPEndPoint = new IPEndPoint(DotNetIPAddress, _Port.ToUInt16());
            }
            catch (Exception e)
            {
                throw new ArgumentException("The hostname is invalid!", e);
            }

            if (Connect)
                try
                {
                    DotNetSocket.Connect(RemoteIPEndPoint);
                }
                catch (Exception e)
                {
                    throw new ArgumentException("Could not set the remote endpoint!", e);
                }

        }

        #endregion


        /// <summary>
        /// Send the given message to the predefined remote host.
        /// </summary>
        /// <param name="Message">The message to send.</param>
        public void ProcessNotification(T Message)
        {

            var UDPPacketData = MessageProcessor(Message);

            SocketError SocketErrorCode;
            DotNetSocket.Send(UDPPacketData, 0, UDPPacketData.Length, UDPSocketFlags, out SocketErrorCode);

            if (SocketErrorCode != SocketError.Success)
                ProcessError(this, new Exception(SocketErrorCode.ToString()));

        }

        public void ProcessError(dynamic Sender, Exception ExceptionMessage)
        {
            
        }

        public void ProcessCompleted(dynamic Sender, string Message)
        {
            
        }


        public void ProcessNotification(UDPPacket<T> UDPPacket)
        {

            DotNetSocket.SendTo(MessageProcessor(UDPPacket.Message),
                                UDPSocketFlags,
                                new IPEndPoint(System.Net.IPAddress.Parse(UDPPacket.RemoteSocket.IPAddress.ToString()),
                                               UDPPacket.RemoteSocket.Port.ToUInt16()));

        }


        #region Close()

        /// <summary>
        /// Close this UDP client.
        /// </summary>
        public void Close()
        {
            if (DotNetSocket != null)
                DotNetSocket.Close();
        }

        #endregion

        #region Dispose()

        public void Dispose()
        {
            if (DotNetSocket != null)
                DotNetSocket.Close();
        }

        #endregion

    }



    public class UDPClient : UDPClient<Byte[]>
    {

        public UDPClient(String Hostname, IPPort Port)
            :base(msg => msg, Hostname, Port)
        { }

    }


}