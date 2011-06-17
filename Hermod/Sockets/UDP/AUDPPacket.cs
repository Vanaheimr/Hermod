/*
 * Copyright (c) 2010-2011, Achim 'ahzf' Friedland <code@ahzf.de>
 * This file is part of Hermod
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

using de.ahzf.Hermod.Datastructures;

#endregion

namespace de.ahzf.Hermod.Sockets.UDP
{

    /// <summary>
    /// An abstract class for all UDP packets.
    /// </summary>
    public abstract class AUDPPacket : IDisposable
    {

        #region Data

        /// <summary>
        /// The transported data within this UDP Packet.
        /// </summary>
        protected readonly Byte[] PacketData;

        #endregion

        #region Properties

        #region LocalSocket

        private IPSocket _LocalSocket;

        /// <summary>
        /// The local socket of the UDP packet.
        /// </summary>
        public IPSocket LocalSocket
        {

            get
            {
                return _LocalSocket;
            }

            set
            {

                if (value != null)
                    _LocalSocket = value;

                else
                    throw new ArgumentException("The LocalSocket must not be null!");

            }

        }

        #endregion

        #region LocalHost

        protected IIPAddress _LocalHost;

        /// <summary>
        /// The local IP host.
        /// </summary>
        public IIPAddress LocalHost
        {
            get
            {
                return IPv4Address.Parse("127.0.0.1");
            }
        }

        #endregion

        #region LocalPort

        /// <summary>
        /// The local IP port.
        /// </summary>
        public IPPort LocalPort
        {
            get
            {

                if (LocalSocket != null)
                    return LocalSocket.Port;

                throw new Exception("The LocalSocket is null!");

            }
        }

        #endregion


        #region RemoteSocket

        private IPSocket _RemoteSocket;

        /// <summary>
        /// The remote socket of the UDP packet.
        /// </summary>
        public IPSocket RemoteSocket
        {

            get
            {
                return _RemoteSocket;
            }

            set
            {
                
                if (value != null)
                    _RemoteSocket = value;

                else
                    throw new ArgumentException("The RemoteSocket must not be null!");

            }

        }

        #endregion

        #region RemoteHost

        /// <summary>
        /// The remote IP host.
        /// </summary>
        public IIPAddress RemoteHost
        {
            get
            {

                if (_RemoteSocket != null)
                    return _RemoteSocket.IPAddress;

                throw new Exception("The RemoteSocket is null!");

            }
        }

        #endregion

        #region RemotePort

        /// <summary>
        /// The remote IP port.
        /// </summary>
        public IPPort RemotePort
        {
            get
            {
                
                if (_RemoteSocket != null)
                    return _RemoteSocket.Port;

                throw new Exception("The RemoteSocket is null!");

            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region AUDPPacket()

        /// <summary>
        /// Create a new abstract AUDPPacket.
        /// </summary>
        public AUDPPacket()
        { }

        #endregion

        #region AUDPPacket(UDPPacketData, LocalSocket, RemoteSocket)

        /// <summary>
        /// Create a new abstract AUDPPacket.
        /// </summary>
        /// <param name="UDPPacketData">The UDP packet data.</param>
        /// <param name="LocalSocket">The local socket of this UDP packet.</param>
        /// <param name="RemoteSocket">The remote socket of this UDP packet.</param>
        public AUDPPacket(Byte[] UDPPacketData, IPSocket LocalSocket, IPSocket RemoteSocket)
        {
            this.PacketData   = UDPPacketData;
            this.LocalSocket  = LocalSocket;
            this.RemoteSocket = RemoteSocket;
        }

        #endregion

        #endregion


        #region IDisposable Members

        /// <summary>
        /// Dispose this UDP packet.
        /// </summary>
        public virtual void Dispose()
        { }

        #endregion

    }

}
