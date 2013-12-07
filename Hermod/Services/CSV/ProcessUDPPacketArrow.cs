﻿/*
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

using eu.Vanaheimr.Hermod.Datastructures;
using eu.Vanaheimr.Styx;
using eu.Vanaheimr.Styx.Arrows;

#endregion

namespace eu.Vanaheimr.Hermod.Sockets.UDP
{

    public static class UDPPacketArrowExtention
    {

        public static ProcessUDPPacketArrow<TIn, TOut> ProcessUDPPacket<TIn, TOut>(this IArrowSender<UDPPacket<TIn>> In,
                                                                                   Func<TIn, TOut> MessageProcessor,
                                                                                   Func<Exception, Exception> OnError = null)
        {
            return new ProcessUDPPacketArrow<TIn, TOut>(MessageProcessor, OnError, In);
        }

    }


    public class ProcessUDPPacketArrow<TIn, TOut> : MapArrow<UDPPacket<TIn>, UDPPacket<TOut>>
    {

        public ProcessUDPPacketArrow(Func<TIn, TOut> MessageProcessor,
                                     Func<Exception, Exception> OnError = null,
                                     IArrowSender<UDPPacket<TIn>> In = null)

            : base(PacketIn => new UDPPacket<TOut>(
                                   PacketIn.ServerTimestamp,
                                   PacketIn.LocalSocket,
                                   PacketIn.RemoteSocket,
                                   MessageProcessor(PacketIn.Payload)),
                   OnError)

        {

            if (In != null)
                In.SendTo(this);

        }

    }

}