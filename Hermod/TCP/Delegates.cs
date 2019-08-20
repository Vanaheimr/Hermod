/*
 * Copyright (c) 2010-2019, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP
{

    /// <summary>
    /// TCP socket attached delegate.
    /// </summary>
    /// <param name="TCPServer">The sender of this event.</param>
    /// <param name="Timestamp">The timestamp of the TCP socket attached event.</param>
    /// <param name="TCPSocket">The new TCP socket.</param>
    /// <param name="Message">An optional message.</param>
    public delegate void TCPSocketAttachedHandler(ATCPServers TCPServer, DateTime Timestamp, IPSocket TCPSocket, String Message = null);

    /// <summary>
    /// New connection delegate.
    /// </summary>
    /// <param name="TCPServer">The sender of this event.</param>
    /// <param name="Timestamp">The timestamp of the new TCP connection event.</param>
    /// <param name="RemoteSocket">The remote TCP/IP socket.</param>
    /// <param name="ConnectionId">The identification of this connection.</param>
    /// <param name="TCPConnection">The new TCP connection.</param>
    public delegate void NewConnectionHandler(TCPServer TCPServer, DateTime Timestamp, IPSocket RemoteSocket, String ConnectionId, TCPConnection TCPConnection);

    /// <summary>
    /// Connection closed delegate.
    /// </summary>
    /// <param name="TCPServer">The sender of this event.</param>
    /// <param name="Timestamp">The timestamp of the event.</param>
    /// <param name="RemoteSocket">The remote TCP/IP socket.</param>
    /// <param name="ConnectionId">The identification of this connection.</param>
    /// <param name="ClosedBy">Whether the connection was closed by the client or the server.</param>
    public delegate void ConnectionClosedHandler(TCPServer TCPServer, DateTime Timestamp, IPSocket RemoteSocket, String ConnectionId, ConnectionClosedBy ClosedBy);

    /// <summary>
    /// TCP socket detached delegate.
    /// </summary>
    /// <param name="TCPServer">The sender of this event.</param>
    /// <param name="Timestamp">The timestamp of the TCP socket detached event.</param>
    /// <param name="TCPSocket">The TCP socket.</param>
    public delegate void TCPSocketDetachedHandler(ATCPServers TCPServer, DateTime Timestamp, IPSocket TCPSocket, String Message = null);

}
