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

#endregion

namespace de.ahzf.Hermod.Sockets.UDP
{

    /// <summary>
    /// A class representing a UDP packet
    /// </summary>
    public class UDPPacket : AUDPPacket
    {

        #region Constructor(s)

        #region UDPPacket()

        /// <summary>
        /// Create a new UDP packet.
        /// </summary>
        public UDPPacket()
        { }

        #endregion

        #region UDPPacket()

        /// <summary>
        /// Create a new UDP packet.
        /// </summary>
        public UDPPacket(Byte[] UDPPacket, IPEndPoint RemoteEndPoint)
            : base(UDPPacket, RemoteEndPoint)
        { }

        #endregion

        #endregion


        #region Dispose()

        /// <summary>
        /// Dispose this object
        /// </summary>
        public override void Dispose()
        { }

        #endregion

    }

}
