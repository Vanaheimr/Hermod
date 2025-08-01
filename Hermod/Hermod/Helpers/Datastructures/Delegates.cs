﻿/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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

namespace org.GraphDefined.Vanaheimr.Hermod.Sockets
{

    /// <summary>
    /// A delegate to generate a server thread name.
    /// </summary>
    /// <param name="Socket"></param>
    /// <returns></returns>
    public delegate String          ServerThreadNameCreatorDelegate(IPSocket Socket);

    /// <summary>
    /// A delegate to set the server thread priority.
    /// </summary>
    /// <param name="Socket"></param>
    /// <returns></returns>
    public delegate ThreadPriority  ServerThreadPriorityDelegate(IPSocket Socket);

    /// <summary>
    /// A delegate to generate a connection identification.
    /// </summary>
    /// <param name="Sender">The sender of this event.</param>
    /// <param name="Timestamp">The timestamp of the event.</param>
    /// <param name="LocalSocket">The local TCP/IP socket.</param>
    /// <param name="RemoteSocket">The remote TCP/IP socket.</param>
    public delegate String          ConnectionIdBuilder(Object          Sender,
                                                        DateTimeOffset  Timestamp,
                                                        IPSocket        LocalSocket,
                                                        IPSocket        RemoteSocket);

    /// <summary>
    /// A delegate to generate a thread name for a connection.
    /// </summary>
    /// <param name="Sender">The sender of this event.</param>
    /// <param name="Timestamp">The timestamp of the event.</param>
    /// <param name="LocalSocket">The local TCP/IP socket.</param>
    /// <param name="RemoteSocket">The remote TCP/IP socket.</param>
    public delegate String          ConnectionThreadsNameBuilder(Object    Sender,
                                                                 DateTime  Timestamp,
                                                                 IPSocket  LocalSocket,
                                                                 IPSocket  RemoteSocket);

    /// <summary>
    /// A delegate to generate a thread priority for a connection.
    /// </summary>
    /// <param name="Sender">The sender of this event.</param>
    /// <param name="Timestamp">The timestamp of the event.</param>
    /// <param name="LocalSocket">The local TCP/IP socket.</param>
    /// <param name="RemoteSocket">The remote TCP/IP socket.</param>
    public delegate ThreadPriority  ConnectionThreadsPriorityDelegate(Object    Sender,
                                                                      DateTime  Timestamp,
                                                                      IPSocket  LocalSocket,
                                                                      IPSocket  RemoteSocket);

}
