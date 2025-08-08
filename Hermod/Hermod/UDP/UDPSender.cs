/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
using System.Net;
using System.Net.Sockets;
using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Styx;
using org.GraphDefined.Vanaheimr.Styx.Arrows;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.UDP
{

    // For testing via NetCat use: 'nc -lup 5000'

    #region UDPSender

    /// <summary>
    /// A generic UDP sender accepting Vanaheimr Styx arrows
    /// in order to send them through the internet.
    /// </summary>
    public class UDPSender : UDPSender<Byte[]>
    {

        #region UDPSender()

        ///// <summary>
        ///// Create a new UDP sender.
        ///// </summary>
        //private UDPSender()
        //    : base(Message => Message)
        //{ }

        #endregion

        #region UDPSender(Hostname, Port)

        /// <summary>
        /// Create a new UDP sender.
        /// </summary>
        /// <param name="Hostname">The hostname to send the UDP data.</param>
        /// <param name="Port">The IP port to send the UDP data.</param>
        public UDPSender(String Hostname, IPPort Port)
            : base(Message => Message, Hostname, Port)
        { }

        #endregion

        #region UDPSender(Hostname, Port)

        /// <summary>
        /// Create a new UDP sender.
        /// </summary>
        /// <param name="IPAddress">The IP address to send the UDP data.</param>
        /// <param name="Port">The IP port to send the UDP data.</param>
        public UDPSender(IIPAddress IPAddress, IPPort Port)
            : base(Message => Message, IPAddress, Port)
        { }

        #endregion

    }

    #endregion

    #region UDPSender<T>

    /// <summary>
    /// A generic UDP sender accepting Vanaheimr Styx arrows
    /// in order to send them through the internet.
    /// </summary>
    public class UDPSender<T> : IArrowReceiver<T>,
                                IArrowReceiver<UDPPacket<T>>,
                                IDisposable
    {

        #region Data

        private readonly Func<T, Byte[]>       MessageProcessor;
        private readonly Socket                DotNetSocket;
        private          System.Net.IPAddress  DotNetIPAddress;
        private          IPEndPoint            RemoteIPEndPoint;

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

        #region IPSocket

        private IPSocket _IPSocket;

        /// <summary>
        /// The IP socket to send the UDP data.
        /// </summary>
        public IPSocket IPSocket
        {
            get
            {
                return _IPSocket;
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

        #region (private) UDPSender(MessageProcessor)

        /// <summary>
        /// Create a new UDPSender.
        /// </summary>
        /// <param name="MessageProcessor">A delegate to tranform the message into an array of bytes.</param>
        private UDPSender(Func<T, Byte[]> MessageProcessor)
        {

            if (MessageProcessor == null)
                throw new ArgumentNullException("The MessageProcessor must not be null!");

            this.MessageProcessor  = MessageProcessor;
            this.DotNetSocket      = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            this.UDPSocketFlags    = SocketFlags.None;

        }

        #endregion

        #region (private) UDPSender(MessageProcessor, Port)

        /// <summary>
        /// Create a new UDPSender.
        /// </summary>
        /// <param name="MessageProcessor">A delegate to tranform the message into an array of bytes.</param>
        /// <param name="Port">The IP port to send the UDP data.</param>
        private UDPSender(Func<T, Byte[]>  MessageProcessor,
                          IPPort           Port)

            : this (MessageProcessor)

        {

            this.Port  = Port;

        }

        #endregion

        #region UDPSender(MessageProcessor, Hostname, Port)

        /// <summary>
        /// Create a new UDPSender.
        /// </summary>
        /// <param name="MessageProcessor">A delegate to tranform the message into an array of bytes.</param>
        /// <param name="Hostname">The hostname to send the UDP data.</param>
        /// <param name="Port">The IP port to send the UDP data.</param>
        public UDPSender(Func<T, Byte[]>  MessageProcessor,
                         String           Hostname,
                         IPPort           Port)

            : this(MessageProcessor, Port)

        {

            this.Hostname  = Hostname;

        }

        #endregion

        #region UDPSender(MessageProcessor, IPAddress, Port)

        /// <summary>
        /// Create a new UDPSender.
        /// </summary>
        /// <param name="MessageProcessor">A delegate to tranform the message into an array of bytes.</param>
        /// <param name="IPAddress">The IP address to send the UDP data.</param>
        /// <param name="Port">The IP port to send the UDP data.</param>
        public UDPSender(Func<T, Byte[]>  MessageProcessor,
                         IIPAddress       IPAddress,
                         IPPort           Port)

            : this(MessageProcessor, Port)

        {

            this.IPAddress  = IPAddress;

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


        #region Send(Message)

        /// <summary>
        /// Send the given message to the predefined remote host.
        /// </summary>
        /// <param name="Message">The message to send.</param>
        public void Send(T Message)
        {

            if (RemoteIPEndPoint == null)
                throw new ArgumentNullException("The IP address and port must be defined before sending an UDP packet!");


            Byte[]? UDPPacketData = null;

            try
            {
                UDPPacketData = MessageProcessor(Message);
            }
            catch (Exception e)
            {
                ProcessExceptionOccurred(this, Timestamp.Now, EventTracking_Id.New, new Exception("The MessageProcessor lead to an error!", e));
            }

            try
            {

                SocketError SocketErrorCode;
                DotNetSocket.Send(UDPPacketData, 0, UDPPacketData.Length, UDPSocketFlags, out SocketErrorCode);

                if (SocketErrorCode != SocketError.Success)
                    ProcessExceptionOccurred(this, Timestamp.Now, EventTracking_Id.New, new Exception("The UDP packet transmission lead to an error: " + SocketErrorCode.ToString()));

            }
            catch (Exception e)
            {
                ProcessExceptionOccurred(this, Timestamp.Now, EventTracking_Id.New, new Exception("The UDP packet transmission lead to an error!", e));
            }

        }

        #endregion

        #region (ITarget<T>) ProcessArrow(Message)

        /// <summary>
        /// Send the given message to the predefined remote host.
        /// </summary>
        /// <param name="Message">The message to send.</param>
        void IArrowReceiver<T>.ProcessArrow(EventTracking_Id EventTrackingId, T Message)
        {
            this.Send(Message);
        }

        #endregion

        #region Send(UDPPacket)

        /// <summary>
        /// Send the given UDP packet to the remote host specified within the UDP packet.
        /// </summary>
        /// <param name="UDPPacket">The UDP packet to send.</param>
        public void Send(UDPPacket<T> UDPPacket)
        {

            Byte[]? UDPPacketData = null;

            try
            {
                UDPPacketData = MessageProcessor(UDPPacket.Payload);
            }
            catch (Exception e)
            {
                ProcessExceptionOccurred(this, Timestamp.Now, EventTracking_Id.New, new Exception("The MessageProcessor lead to an error!", e));
            }

            try
            {

                DotNetSocket.SendTo(UDPPacketData,
                                    UDPSocketFlags,
                                    new IPEndPoint(System.Net.IPAddress.Parse(UDPPacket.RemoteSocket.IPAddress.ToString()),
                                                   UDPPacket.RemoteSocket.Port.ToUInt16()));

            }
            catch (Exception e)
            {
                ProcessExceptionOccurred(this, Timestamp.Now, EventTracking_Id.New, new Exception("The UDP packet transmission lead to an error!", e));
            }

        }

        #endregion

        #region (ITarget<UDPPacket<T>>) ProcessArrow(UDPPacket)

        /// <summary>
        /// Send the given UDP packet to the remote host specified within the UDP packet.
        /// </summary>
        /// <param name="UDPPacket">The UDP packet to send.</param>
        void IArrowReceiver<UDPPacket<T>>.ProcessArrow(EventTracking_Id EventTrackingId, UDPPacket<T> UDPPacket)
        {
            Send(UDPPacket);
        }

        #endregion


        #region ProcessExceptionOccurred(Sender, Timestamp, ExceptionMessage)

        /// <summary>
        /// An error occured at the arrow sender.
        /// </summary>
        /// <param name="Sender">The sender of this error message.</param>
        /// <param name="Timestamp">The timestamp of the exception.</param>
        /// <param name="ExceptionMessage">The exception leading to this error.</param>
        public virtual void ProcessExceptionOccurred(dynamic Sender, DateTimeOffset Timestamp, EventTracking_Id EventTrackingId, Exception ExceptionMessage)
        {
            // Error handling should better be part of the application logic!
            // Overwrite this method to signal the error, e.g. by sending a nice UDP packet.
        }

        #endregion

        #region ProcessCompleted(Sender, Timestamp, Message)

        /// <summary>
        /// Close the UDP socket, as no more data will be send.
        /// </summary>
        /// <param name="Sender">The sender of this completed message.</param>
        /// <param name="Timestamp">The timestamp of the shutdown.</param>
        /// <param name="Message">An optional completion message.</param>
        public void ProcessCompleted(dynamic Sender, DateTimeOffset Timestamp, EventTracking_Id EventTrackingId, String? Message = null)
        {
            Close();
        }

        #endregion


        #region Close()

        /// <summary>
        /// Close this UDP client.
        /// </summary>
        public void Close()
        {
            Dispose();
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

    #endregion

}