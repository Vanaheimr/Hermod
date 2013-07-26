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
using System.Threading;
using System.Reflection;
using System.Net.Sockets;
using System.Threading.Tasks;

using eu.Vanaheimr.Hermod.Datastructures;
using eu.Vanaheimr.Styx;

#endregion

namespace eu.Vanaheimr.Hermod.Sockets.UDP
{

    public class UDPPacket<TData>
    {

        public DateTime ServerTimestamp { get; private set; }
        public IPSocket LocalSocket     { get; private set; }
        public IPSocket RemoteSocket    { get; private set; }
        public TData    Message         { get; private set; }

        public UDPPacket(DateTime ServerTimestamp,
                         IPSocket LocalSocket,
                         IPSocket RemoteSocket,
                         TData    Message)
        {

            this.ServerTimestamp  = ServerTimestamp;
            this.LocalSocket      = LocalSocket;
            this.RemoteSocket     = RemoteSocket;
            this.Message          = Message;

        }

    }

}
