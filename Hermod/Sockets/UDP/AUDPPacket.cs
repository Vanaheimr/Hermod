/*
 * Copyright (c) 2010-2012, Achim 'ahzf' Friedland <achim@graph-database.org>
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
    public abstract class AUDPPacket : ALocalRemoteSockets
    {

        #region Data

        /// <summary>
        /// The transported data within this UDP Packet.
        /// </summary>
        protected readonly Byte[] PacketData;

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
            : base(LocalSocket, RemoteSocket)
        {
            this.PacketData   = UDPPacketData;
        }

        #endregion

        #endregion

        #region ToString()

        /// <summary>
        /// Return a string representation of this object.
        /// </summary>
        public override String ToString()
        {
            return this.GetType().Name + ": " + base.ToString();
        }

        #endregion

    }

}
