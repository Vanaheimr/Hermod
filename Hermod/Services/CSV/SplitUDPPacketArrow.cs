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
using System.Linq;
using System.Collections.Generic;

using eu.Vanaheimr.Hermod.Datastructures;
using eu.Vanaheimr.Styx;
using eu.Vanaheimr.Styx.Arrows;

#endregion

namespace eu.Vanaheimr.Hermod.Sockets.UDP
{

    public static class SplitUDPPacketArrowExtention
    {

        /// <summary>
        /// Split an UDP packet having multiple messages
        /// into an enumeration of UDP packets.
        /// </summary>
        /// <typeparam name="T">The type of the UDP messages.</typeparam>
        /// <param name="In">The arrow sender.</param>
        public static SplitUDPPacketArrow<T> SplitUDPPacket<T>(this IArrowSender<UDPPacket<IEnumerable<T>>> In)
        {
            return new SplitUDPPacketArrow<T>(In);
        }

    }


    /// <summary>
    /// Split an UDP packet having multiple messages
    /// into an enumeration of UDP packets.
    /// </summary>
    /// <typeparam name="T">The type of the UDP messages.</typeparam>
    public class SplitUDPPacketArrow<T> : MapArrow<UDPPacket<IEnumerable<T>>, IEnumerable<UDPPacket<T>>>
    {

        public SplitUDPPacketArrow(IArrowSender<UDPPacket<IEnumerable<T>>> In = null)

            : base(Messages => Messages.Payload.
                                   Select(Message => new UDPPacket<T>(Messages.ServerTimestamp,
                                                                      Messages.LocalSocket,
                                                                      Messages.RemoteSocket,
                                                                      Message)))

        {

            if (In != null)
                In.SendTo(this);

        }

    }

}
