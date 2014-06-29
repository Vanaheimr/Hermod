/*
 * Copyright (c) 2010-2014, Achim 'ahzf' Friedland <achim@graphdefined.org>
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

using eu.Vanaheimr.Styx.Arrows;
using System.Threading;

#endregion

namespace eu.Vanaheimr.Hermod.Sockets.TCP
{

    /// <summary>
    /// A TCP server interface.
    /// </summary>
    public interface ITCPServer : IServer
    {

        /// <summary>
        /// The optional name of the TCP server thread.
        /// </summary>
        String ServerThreadName { get; set; }

        /// <summary>
        /// The optional priority of the TCP server thread.
        /// </summary>
        ThreadPriority ServerThreadPriority { get; set; }

        /// <summary>
        /// Whether the TCP server thread is a background thread or not.
        /// </summary>
        Boolean ServerThreadIsBackground { get; set; }


        /// <summary>
        /// The optional name of the TCP server thread.
        /// </summary>
        Func<IPSocket, String> ConnectionIdBuilder { get; set; }

        /// <summary>
        /// The optional name of the TCP server thread.
        /// </summary>
        String ConnectionThreadsNameCreator { get; set; }

        /// <summary>
        /// The optional priority of the TCP server thread.
        /// </summary>
        ThreadPriority ConnectionThreadsPriority { get; set; }

        /// <summary>
        /// Whether the TCP server thread is a background thread or not.
        /// </summary>
        Boolean ConnectionThreadsAreBackground { get; set; }

        /// <summary>
        /// The tcp client timeout for all incoming client connections.
        /// </summary>
        TimeSpan ConnectionTimeout { get; set; }



        /// <summary>
        /// The current number of connected clients.
        /// </summary>
        UInt64 NumberOfClients { get; }

        /// <summary>
        /// The maximum number of pending client connections.
        /// </summary>
        UInt32 MaxClientConnections { get; }

    }

}
