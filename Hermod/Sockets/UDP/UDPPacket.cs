/*
 * Copyright (c) 2010-2012, Achim 'ahzf' Friedland <code@ahzf.de>
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
    /// A class representing a UDP packet
    /// </summary>
    public class UDPPacket : AUDPPacket, IUDPPacket
    {

        #region Properties

        /// <summary>
        /// The UDP packet data.
        /// </summary>
        public Byte[] Data { get; private set; }

        #endregion

        #region Constructor(s)

        #region UDPPacket()

        /// <summary>
        /// Create a new UDP packet.
        /// </summary>
        public UDPPacket()
        { }

        #endregion

        #region UDPPacket(UDPPacketData, LocalSocket, RemoteSocket)

        /// <summary>
        /// Create a new UDP packet.
        /// </summary>
        /// <param name="UDPPacketData">The UDP packet data.</param>
        /// <param name="LocalSocket">The local socket of this UDP packet.</param>
        /// <param name="RemoteSocket">The remote socket of this UDP packet.</param>
        public UDPPacket(Byte[] UDPPacketData, IPSocket LocalSocket, IPSocket RemoteSocket)
            : base(UDPPacketData, LocalSocket, RemoteSocket)
        {
            Data = UDPPacketData;
        }

        #endregion

        #endregion


        public void Reply(Byte[] Data)
        {

            var _ReplyPacket = new UDPPacket(Data, this.RemoteSocket, this.LocalSocket);

        }

    }

}
