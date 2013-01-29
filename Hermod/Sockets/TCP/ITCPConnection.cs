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
using System.Net;
using System.Net.Sockets;

using de.ahzf.Vanaheimr.Hermod.Datastructures;

#endregion

namespace de.ahzf.Vanaheimr.Hermod.Sockets.TCP
{

    /// <summary>
    /// The interface for all TCP connections.
    /// </summary>
    public interface ITCPConnection : ILocalSocket, IRemoteSocket
    {

        #region Properties

        /// <summary>
        /// The TCPClient connection to a connected Client
        /// </summary>
        TcpClient  TCPClientConnection { get; }

        /// <summary>
        /// Is False if the client is disconnected from the server
        /// </summary>
        Boolean    IsConnected         { get; }

        /// <summary>
        /// Gets a value that indicates whether data is available
        /// on the System.Net.Sockets.NetworkStream to be read.
        /// </summary>
        Boolean    DataAvailable       { get; }

        /// <summary>
        /// Gets or sets the amount of time that a read operation
        /// blocks waiting for data.
        /// </summary>
        Int32      ReadTimeout         { get; set; }

        /// <summary>
        /// Gets or sets a value that disables a delay when send or receive
        /// buffers are not full.
        /// </summary>
        Boolean    NoDelay             { get; set; }

        /// <summary>
        /// The connection is keepalive
        /// </summary>
        Boolean    KeepAlive           { get; set; }

        /// <summary>
        /// Server was requested to stop.
        /// </summary>
        Boolean    StopRequested       { get; set; }

        #endregion

        #region Events

        event ExceptionOccuredHandler OnExceptionOccured;

        #endregion

        #region Read(...)

        Boolean ReadByte(out Byte Byte);

        #endregion

        #region Write(...)

        /// <summary>
        /// Writes some UTF-8 text to the underlying stream.
        /// </summary>
        /// <param name="Text">Some UTF-8 text.</param>
        void WriteToResponseStream(String Text);

        /// <summary>
        /// Writes the given byte array to the underlying stream.
        /// </summary>
        /// <param name="Content">An array of bytes.</param>
        void WriteToResponseStream(Byte[] Content);

        /// <summary>
        /// Reads the given input stream and writes its content to the underlying stream.
        /// </summary>
        /// <param name="InputStream">A data source.</param>
        /// <param name="ReadTimeout">A read timeout on the source.</param>
        /// <param name="BufferSize">The buffer size for reading.</param>
        void WriteToResponseStream(Stream InputStream, Int32 ReadTimeout = 1000, Int32 BufferSize = 65535);

        #endregion

        void Close();

    }

}
