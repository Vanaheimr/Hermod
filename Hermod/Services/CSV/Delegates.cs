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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Collections.Generic;

using eu.Vanaheimr.Hermod.Sockets.TCP;
using eu.Vanaheimr.Hermod.Datastructures;

#endregion

namespace eu.Vanaheimr.Hermod.Services
{

    /// <summary>
    /// Service started delegate.
    /// </summary>
    /// <param name="Sender">The sender of this event.</param>
    /// <param name="Timestamp">The timestamp of the event.</param>
    public delegate void OnStartedDelegate(ICSVTCPServer Sender, DateTime Timestamp);

    /// <summary>
    /// New connection delegate.
    /// </summary>
    /// <param name="Sender">The sender of this event.</param>
    /// <param name="Timestamp">The timestamp of the event.</param>
    /// <param name="RemoteIPAddress">The IP address of the remote TCP client.</param>
    /// <param name="RemotePort">The IP port of the remote TCP client.</param>
    public delegate void NewConnectionDelegate(ICSVTCPServer Sender, DateTime Timestamp, IIPAddress RemoteIPAddress, IPPort RemotePort);

    /// <summary>
    /// Data available delegate.
    /// </summary>
    /// <param name="Sender">The message sender.</param>
    /// <param name="Timestamp">The timestamp of the event.</param>
    /// <param name="Values">The received data as an enumeration of strings.</param>
    public delegate void DataAvailableDelegate(ICSVTCPServer Sender, List<String> Results, DateTime Timestamp, String[] Values);

    /// <summary>
    /// A result is available.
    /// </summary>
    /// <param name="Sender">The sender of this event.</param>
    /// <param name="Timestamp">The timestamp of the event.</param>
    /// <param name="Results">The results.</param>
    public delegate void ResultDelegate(ICSVTCPServer Sender, DateTime Timestamp, IEnumerable<String> Results);

    /// <summary>
    /// An exception occured.
    /// </summary>
    /// <param name="Sender">The sender of this event.</param>
    /// <param name="Timestamp">The timestamp of the event.</param>
    /// <param name="Exception">The exception.</param>
    /// <param name="CurrentBuffer">The state of the receive buffer when the exception occured.</param>
    public delegate void ExceptionOccurredDelegate(ICSVTCPServer Sender, DateTime Timestamp, Exception Exception, MemoryStream CurrentBuffer);

    /// <summary>
    /// Service stopped delegate.
    /// </summary>
    /// <param name="Sender">The sender of this event.</param>
    /// <param name="Timestamp">The timestamp of the event.</param>
    public delegate void OnStoppededDelegate(ICSVTCPServer Sender, DateTime Timestamp);

}
