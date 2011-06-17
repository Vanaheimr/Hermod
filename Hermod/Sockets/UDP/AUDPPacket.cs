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
        /// The data of the UDP Packet.
        /// </summary>
        protected readonly Byte[] PacketData;

        #endregion

        #region Properties

        #region LocalEndPoint

        /// <summary>
        /// The local socket of the UDP packet.
        /// </summary>
        public IPEndPoint LocalEndPoint { get; protected set; }

        #endregion

        #region LocalPort

        /// <summary>
        /// The local IP port.
        /// </summary>
        public IPPort LocalPort
        {
            get
            {

                if (LocalEndPoint != null)
                    return new IPPort((UInt16)LocalEndPoint.Port);

                return new IPPort(0);

            }
        }

        #endregion


        #region RemoteSocket

        protected IPEndPoint _RemoteEndPoint;

        /// <summary>
        /// The remote socket of the UDP packet.
        /// </summary>
        public IPEndPoint RemoteEndPoint
        {

            get
            {
                return _RemoteEndPoint;
            }

            set
            {
                if (value != null)
                {
                    _RemoteEndPoint = value;
                    _RemoteHost = new IPv4Address(RemoteEndPoint.Address);

                }
            }

        }

        #endregion

        #region RemoteHost

        private IIPAddress _RemoteHost;

        /// <summary>
        /// The remote IP host.
        /// </summary>
        public IIPAddress RemoteHost
        {
            get
            {
                return new IPv4Address(RemoteEndPoint.Address);
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
                
                if (RemoteEndPoint != null)
                    return new IPPort((UInt16) RemoteEndPoint.Port);

                return new IPPort(0);

            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region AUDPPacket()

        /// <summary>
        /// Initiate a new abstract AUDPPacket
        /// </summary>
        public AUDPPacket()
        { }

        #endregion

        #region AUDPPacket(UDPPacketData, LocalEndPoint, RemoteEndPoint)

        /// <summary>
        /// Initiate a new abstract AUDPPacket
        /// </summary>
        /// <param name="UDPPacketData">The UDP packet data.</param>
        /// <param name="LocalEndPoint">The local socket of this UDP packet.</param>
        /// <param name="RemoteEndPoint">The remote socket of this UDP packet.</param>
        public AUDPPacket(Byte[] UDPPacketData, IPEndPoint LocalEndPoint, IPEndPoint RemoteEndPoint)
        {
            this.PacketData     = UDPPacketData;
            this.LocalEndPoint  = LocalEndPoint;
            this.RemoteEndPoint = RemoteEndPoint;
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
