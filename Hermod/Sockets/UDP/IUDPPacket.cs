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

using System.Net;

using de.ahzf.Hermod.Datastructures;
using System;

#endregion

namespace de.ahzf.Hermod.Sockets.UDP
{

    /// <summary>
    /// An interface for all UDP packets.
    /// </summary>
    public interface IUDPPacket
    {

        /// <summary>
        /// The local socket of this UDP packet.
        /// </summary>
        IPEndPoint LocalEndPoint { get; }


        /// <summary>
        /// The remote socket of this UDP packet.
        /// </summary>
        IPEndPoint RemoteEndPoint { get; }

        /// <summary>
        /// The remote IP host.
        /// </summary>
        IIPAddress RemoteHost     { get; }

        /// <summary>
        /// The remote IP port.
        /// </summary>
        IPPort     RemotePort     { get; }


        Byte[] Data { get; }

    }

}
