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
using System.IO;
using System.Collections.Generic;

using eu.Vanaheimr.Illias.Commons;

#endregion

namespace eu.Vanaheimr.Hermod.Services.CSV
{

    /// <summary>
    /// TCP Port added delegate.
    /// </summary>
    /// <param name="Sender">The sender of this event.</param>
    /// <param name="Timestamp">The timestamp of the event.</param>
    /// <param name="TCPPort">The TCPPort to listen.</param>
    public delegate void OnTCPPortAddedDelegate(CSVTCPServer Sender, DateTime Timestamp, IPPort TCPPort);

    /// <summary>
    /// TCP Port removed delegate.
    /// </summary>
    /// <param name="Sender">The sender of this event.</param>
    /// <param name="Timestamp">The timestamp of the event.</param>
    /// <param name="TCPPort">The TCPPort to listen.</param>
    public delegate void OnTCPPortRemovedDelegate(CSVTCPServer Sender, DateTime Timestamp, IPPort TCPPort);

    /// <summary>
    /// New connection delegate.
    /// </summary>
    /// <param name="Sender">The sender of this event.</param>
    /// <param name="Timestamp">The timestamp of the event.</param>
    /// <param name="ConnectionId">The identification of this connection.</param>
    public delegate void OnNewConnectionDelegate(CSVTCPServer Sender, DateTime Timestamp, String ConnectionId);

    /// <summary>
    /// Data available delegate.
    /// </summary>
    /// <param name="Sender">The message sender.</param>
    /// <param name="Timestamp">The timestamp of the event.</param>
    /// <param name="ConnectionId">The identification of this connection.</param>
    /// <param name="Values">The received data as an enumeration of strings.</param>
    /// <param name="Results">A list of data processing results.</param>
    public delegate void OnDataAvailableDelegate(CSVTCPServer Sender, DateTime Timestamp, String ConnectionId, String[] Values, List<CSVResult> Results);

    /// <summary>
    /// A result is available.
    /// </summary>
    /// <param name="Sender">The sender of this event.</param>
    /// <param name="Timestamp">The timestamp of the event.</param>
    /// <param name="ConnectionId">The identification of this connection.</param>
    /// <param name="Results">The results.</param>
    public delegate void OnResultDelegate(CSVTCPServer Sender, DateTime Timestamp, String ConnectionId, IEnumerable<CSVResult> Results);

    /// <summary>
    /// An exception occured.
    /// </summary>
    /// <param name="Sender">The sender of this event.</param>
    /// <param name="Timestamp">The timestamp of the event.</param>
    /// <param name="ConnectionId">The identification of this connection.</param>
    /// <param name="Exception">The exception.</param>
    /// <param name="CurrentBuffer">The state of the receive buffer when the exception occured.</param>
    public delegate void OnExceptionOccurredDelegate(CSVTCPServer Sender, DateTime Timestamp, String ConnectionId, Exception Exception, MemoryStream CurrentBuffer);


    /// <summary>
    /// Wether the connection was closed by the client or the server.
    /// </summary>
    public enum ConnectionClosedBy
    {

        /// <summary>
        /// The connection was closed by the client.
        /// </summary>
        Client,

        /// <summary>
        /// The connection was closed by the server.
        /// </summary>
        Server

    }

    /// <summary>
    /// Connection closed delegate.
    /// </summary>
    /// <param name="Sender">The sender of this event.</param>
    /// <param name="Timestamp">The timestamp of the event.</param>
    /// <param name="ConnectionId">The identification of this connection.</param>
    /// <param name="ClosedBy">Wether the connection was closed by the client or the server.</param>
    public delegate void OnConnectionClosedDelegate(CSVTCPServer Sender, DateTime Timestamp, String ConnectionId, ConnectionClosedBy ClosedBy);

}
