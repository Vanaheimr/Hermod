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
using System.Text;
using System.Net.Sockets;

using eu.Vanaheimr.Styx.Arrows;

#endregion

namespace eu.Vanaheimr.Hermod.Sockets.TCP
{

    /// <summary>
    /// The interface for all TCP connections.
    /// </summary>
    public interface ITCPConnection : ILocalSocket, IRemoteSocket
    {

        #region Properties

        /// <summary>
        /// Is False if the client is disconnected from the server
        /// </summary>
        Boolean IsConnected { get; }

        /// <summary>
        /// Gets a value that indicates whether data is available
        /// on the System.Net.Sockets.NetworkStream to be read.
        /// </summary>
        Boolean DataAvailable { get; }

        /// <summary>
        /// Gets or sets the amount of time that a read operation
        /// blocks waiting for data.
        /// </summary>
        Int32 ReadTimeout { get; set; }

        /// <summary>
        /// Gets or sets a value that disables a delay when send or receive
        /// buffers are not full.
        /// </summary>
        Boolean NoDelay { get; set; }

        /// <summary>
        /// The connection is keepalive
        /// </summary>
        Boolean KeepAlive { get; set; }

        /// <summary>
        /// Server was requested to stop.
        /// </summary>
        Boolean StopRequested { get; set; }

        #endregion

        #region Events

        event ExceptionOccuredEventHandler OnExceptionOccured;

        #endregion

        #region Read(...)

        /// <summary>
        /// Read a byte value from the TCP connection.
        /// </summary>
        /// <param name="SleepingTimeMS">When no data is currently available wait at least this amount of time [milliseconds].</param>
        /// <param name="MaxInitialWaitingTimeMS">When no data is currently available wait at most this amount of time [milliseconds].</param>
        /// <returns>The read byte OR 0x00 if nothing could be read.</returns>
        Byte Read(UInt16 SleepingTimeMS = 5, UInt32 MaxInitialWaitingTimeMS = 500);

        /// <summary>
        /// Try to read a byte value from the TCP connection.
        /// </summary>
        /// <param name="Byte">The byte value OR 0x00 if nothing could be read.</param>
        /// <param name="SleepingTimeMS">When no data is currently available wait at least this amount of time [milliseconds].</param>
        /// <param name="MaxInitialWaitingTimeMS">When no data is currently available wait at most this amount of time [milliseconds].</param>
        /// <returns>True, if the byte value is valid; False otherwise.</returns>
        TCPClientResponse TryRead(out Byte Byte, UInt16 SleepingTimeMS = 5, UInt32 MaxInitialWaitingTimeMS = 500);

        /// <summary>
        /// Read multiple byte values from the TCP connection into the given buffer.
        /// </summary>
        /// <param name="Buffer">An array of byte values.</param>
        /// <param name="SleepingTimeMS">When no data is currently available wait at least this amount of time [milliseconds].</param>
        /// <param name="MaxInitialWaitingTimeMS">When no data is currently available wait at most this amount of time [milliseconds].</param>
        /// <returns>The number of read bytes.</returns>
        Int32 Read(Byte[] Buffer, UInt16 SleepingTimeMS = 5, UInt32 MaxInitialWaitingTimeMS = 500);

        /// <summary>
        /// Read a string value from the TCP connection.
        /// </summary>
        /// <param name=param name="MaxLength">The maximal length of the string.</param>
        /// <param name="Encoding">The character encoding of the string (default: UTF8).</param>
        /// <param name="SleepingTimeMS">When no data is currently available wait at least this amount of time [milliseconds].</param>
        /// <param name="MaxInitialWaitingTimeMS">When no data is currently available wait at most this amount of time [milliseconds].</param>
        String ReadString(Int32 MaxLength = 1024, Encoding Encoding = null, UInt16 SleepingTimeMS = 5, UInt32 MaxInitialWaitingTimeMS = 500);

        #endregion

        #region Write(...)

        /// <summary>
        /// Writes some UTF-8 text to the underlying stream.
        /// </summary>
        /// <param name="Text">Some UTF-8 text.</param>
        void WriteToResponseStream(String Text);

        /// <summary>
        /// Writes some UTF-8 text to the underlying stream.
        /// </summary>
        /// <param name="Text">Some UTF-8 text.</param>
        void WriteLineToResponseStream(String Text);

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
