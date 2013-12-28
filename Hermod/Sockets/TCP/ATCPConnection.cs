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
using System.Net;
using System.Text;
using System.Threading;
using System.Net.Sockets;

using eu.Vanaheimr.Illias.Commons;
using eu.Vanaheimr.Hermod.Datastructures;

#endregion

namespace eu.Vanaheimr.Hermod.Sockets.TCP
{

    public enum TCPClientResponse
    {
        CanNotRead,
        DataAvailable,
        ClientClose,
        Timeout
    }

    /// <summary>
    /// An abstract class for all TCP connections.
    /// </summary>
    public abstract class ATCPConnection : ALocalRemoteSockets, ITCPConnection
    {

        #region Data

        protected readonly TcpClient      TCPClientConnection;
        protected readonly NetworkStream  Stream;

        #endregion

        #region Properties

        #region IsConnected

        /// <summary>
        /// Check if the client is still connected to the server.
        /// </summary>
        public Boolean IsConnected
        {

            get
            {

                if (TCPClientConnection == null)
                    return false;

                // This is not working as expected! Damn you Microsoft! Perhaps
                // you better hire some good networking people!
                //return TCPClientConnection.Connected;

                // A better, but not really smart way to check if the
                // TCP connection is/was closed
                if (TCPClientConnection.Client.Poll(1, SelectMode.SelectRead) &&
                    TCPClientConnection.Available == 0)
                    return false;

                return true;

            }

        }

        #endregion

        #region DataAvailable

        /// <summary>
        /// Gets a value that indicates whether data is available
        /// on the System.Net.Sockets.NetworkStream to be read.
        /// </summary>
        public Boolean DataAvailable
        {
            get
            {
                return this.Stream.DataAvailable;
            }
        }

        #endregion

        #region ReadTimeout

        /// <summary>
        /// Gets or sets the amount of time, in milliseconds, that a read operation
        /// blocks waiting for data. On default the read operation does not time out.
        /// </summary>
        public Int32 ReadTimeout
        {

            get
            {
                return this.Stream.ReadTimeout;
            }

            set
            {
                this.Stream.ReadTimeout = value;
            }

        }

        #endregion

        #region NoDelay

        /// <summary>
        /// Gets or sets a value that disables a delay when send or receive
        /// buffers are not full.
        /// </summary>
        public Boolean NoDelay
        {

            get
            {
                return TCPClientConnection.NoDelay;
            }

            set
            {
                TCPClientConnection.NoDelay = value;
            }

        }

        #endregion

        #region KeepAlive

        /// <summary>
        /// The connection is keepalive
        /// </summary>
        public Boolean KeepAlive { get; set; }

        #endregion

        #region StopRequested

        /// <summary>
        /// Server was requested to stop.
        /// </summary>
        public Boolean StopRequested { get; set; }

        #endregion

        #endregion

        #region Events

        public event OnExceptionOccuredDelegate OnExceptionOccured;

        #endregion

        #region Constructor(s)

        #region ATCPConnection()

        /// <summary>
        /// Initiate a new abstract ATCPConnection
        /// </summary>
        public ATCPConnection()
        { }

        #endregion

        #region ATCPConnection(TCPClientConnection)

        /// <summary>
        /// Initiate a new abstract ATCPConnection using the given TcpClient class
        /// </summary>
        public ATCPConnection(TcpClient TCPClientConnection)
        {

            this.TCPClientConnection = TCPClientConnection;
            var _IPEndPoint          = this.TCPClientConnection.Client.RemoteEndPoint as IPEndPoint;
            RemoteSocket             = new IPSocket(new IPv4Address(_IPEndPoint.Address), new IPPort((UInt16) _IPEndPoint.Port));

            if (RemoteSocket == null)
                throw new ArgumentNullException("The RemoteEndPoint is invalid!");

            this.Stream              = TCPClientConnection.GetStream();

        }

        #endregion

        #endregion


        #region Read(SleepingTimeMS = 5, MaxInitialWaitingTimeMS = 500)

        /// <summary>
        /// Read a byte value from the TCP connection.
        /// </summary>
        /// <param name="SleepingTimeMS">When no data is currently available wait at least this amount of time [milliseconds].</param>
        /// <param name="MaxInitialWaitingTimeMS">When no data is currently available wait at most this amount of time [milliseconds].</param>
        /// <returns>The read byte OR 0x00 if nothing could be read.</returns>
        public Byte Read(UInt16 SleepingTimeMS = 5, UInt32 MaxInitialWaitingTimeMS = 500)
        {

            if (!Stream.CanRead)
                return 0x00;

            var WaitingTimeMS = 0;
            var Value         = -1;

            while (!Stream.DataAvailable && (WaitingTimeMS < MaxInitialWaitingTimeMS))
            {
                Thread.Sleep(SleepingTimeMS);
                WaitingTimeMS += SleepingTimeMS;
            }

            if (Stream.DataAvailable)
                Value = Stream.ReadByte();

            if (Value == -1)
                return 0x00;

            return (Byte) Value;

        }

        #endregion

        #region TryRead(out Byte, SleepingTimeMS = 5, MaxInitialWaitingTimeMS = 500)

        /// <summary>
        /// Try to read a byte value from the TCP connection.
        /// </summary>
        /// <param name="Byte">The byte value OR 0x00 if nothing could be read.</param>
        /// <param name="SleepingTimeMS">When no data is currently available wait at least this amount of time [milliseconds].</param>
        /// <param name="MaxInitialWaitingTimeMS">When no data is currently available wait at most this amount of time [milliseconds].</param>
        /// <returns>True, if the byte value is valid; False otherwise.</returns>
        public TCPClientResponse TryRead(out Byte Byte, UInt16 SleepingTimeMS = 5, UInt32 MaxInitialWaitingTimeMS = 50)
        {

            Byte = 0x00;

            if (!Stream.CanRead)
                return TCPClientResponse.CanNotRead;

            var WaitingTimeMS = 0;
            var Value         = -1;


            //if (TCPClientConnection.Client.Poll(1, SelectMode.SelectRead) &&
            //    TCPClientConnection.Available == 0)


            while (TCPClientConnection.Client.Poll(1, SelectMode.SelectRead) == false &&
                   Stream.DataAvailable == false &&
                   (WaitingTimeMS < MaxInitialWaitingTimeMS))
            {
                Thread.Sleep(SleepingTimeMS);
                WaitingTimeMS += SleepingTimeMS;
            }

            if (Stream.DataAvailable)
                Value = Stream.ReadByte();
            else
            {
                if (WaitingTimeMS >= MaxInitialWaitingTimeMS)
                    return TCPClientResponse.Timeout;
                else
                    return TCPClientResponse.ClientClose;
            }

            if (Value == -1)
                return TCPClientResponse.CanNotRead;

            Byte = (Byte) Value;

            return TCPClientResponse.DataAvailable;

        }

        #endregion

        #region Read(Buffer, SleepingTimeMS = 5, MaxInitialWaitingTimeMS = 500)

        /// <summary>
        /// Read multiple byte values from the TCP connection into the given buffer.
        /// </summary>
        /// <param name="Buffer">An array of byte values.</param>
        /// <param name="SleepingTimeMS">When no data is currently available wait at least this amount of time [milliseconds].</param>
        /// <param name="MaxInitialWaitingTimeMS">When no data is currently available wait at most this amount of time [milliseconds].</param>
        /// <returns>The number of read bytes.</returns>
        public Int32 Read(Byte[] Buffer, UInt16 SleepingTimeMS = 5, UInt32 MaxInitialWaitingTimeMS = 500)
        {

            if (Buffer == null || Buffer.Length < 1)
                throw new ArgumentNullException("The given buffer is not valid!");

            if (!Stream.CanRead)
                return 0;

            var WaitingTimeMS = 0;

            while (!Stream.DataAvailable && (WaitingTimeMS < MaxInitialWaitingTimeMS))
            {
                Thread.Sleep(SleepingTimeMS);
                WaitingTimeMS += SleepingTimeMS;
            }

            if (Stream.DataAvailable)
                return Stream.Read(Buffer, 0, Buffer.Length);

            return 0;

        }

        #endregion

        #region ReadString(MaxLength = 1024, Encoding = null, SleepingTimeMS = 5, MaxInitialWaitingTimeMS = 500)

        /// <summary>
        /// Read a string value from the TCP connection.
        /// </summary>
        /// <param name=param name="MaxLength">The maximal length of the string.</param>
        /// <param name="Encoding">The character encoding of the string (default: UTF8).</param>
        /// <param name="SleepingTimeMS">When no data is currently available wait at least this amount of time [milliseconds].</param>
        /// <param name="MaxInitialWaitingTimeMS">When no data is currently available wait at most this amount of time [milliseconds].</param>
        public String ReadString(Int32 MaxLength = 1024, Encoding Encoding = null, UInt16 SleepingTimeMS = 5, UInt32 MaxInitialWaitingTimeMS = 500)
        {

            if (!Stream.CanRead)
                return String.Empty;

            var WaitingTimeMS = 0;

            while (!Stream.DataAvailable && (WaitingTimeMS < MaxInitialWaitingTimeMS))
            {
                Thread.Sleep(SleepingTimeMS);
                WaitingTimeMS += SleepingTimeMS;
            }

            if (Stream.DataAvailable)
            {

                Byte NumberOfBytes;
                var array = new Byte[MaxLength];
                var pos = 0;

                while (Stream.DataAvailable)
                {

                    NumberOfBytes = (Byte) Stream.ReadByte();

                    array[pos++] = NumberOfBytes;

                    if (NumberOfBytes == 0x00 || pos == MaxLength)
                        break;

                }

                if (pos > 0)
                {

                    if (Encoding == null)
                        Encoding = Encoding.UTF8;

                    return Encoding.GetString(array).Trim();

                }

            }

            return String.Empty;

        }

        #endregion


        #region WriteToResponseStream(UTF8Text)

        /// <summary>
        /// Writes some UTF-8 text to the underlying stream.
        /// </summary>
        /// <param name="UTF8Text">Some UTF-8 text.</param>
        public void WriteToResponseStream(String UTF8Text)
        {
            WriteToResponseStream(UTF8Text.ToUTF8Bytes());
        }

        #endregion

        #region WriteToResponseStream(Byte)

        /// <summary>
        /// Writes the given byte to the underlying stream.
        /// </summary>
        /// <param name="Byte">A single byte.</param>
        public void WriteToResponseStream(Byte Byte)
        {
            if (IsConnected)
                Stream.WriteByte(Byte);
        }

        #endregion

        #region WriteToResponseStream(ByteArray)

        /// <summary>
        /// Writes the given byte array to the underlying stream.
        /// </summary>
        /// <param name="ByteArray">An array of bytes.</param>
        public void WriteToResponseStream(Byte[] ByteArray)
        {
            if (ByteArray != null && IsConnected)
                Stream.Write(ByteArray, 0, ByteArray.Length);
        }

        #endregion

        #region WriteToResponseStream(InputStream, ReadTimeout = 1000, BufferSize = 65535)

        /// <summary>
        /// Reads the given input stream and writes its content to the underlying stream.
        /// </summary>
        /// <param name="InputStream">A data source.</param>
        /// <param name="ReadTimeout">A read timeout on the source.</param>
        /// <param name="BufferSize">The buffer size for reading.</param>
        public void WriteToResponseStream(Stream InputStream, Int32 ReadTimeout = 1000, Int32 BufferSize = 65535)
        {

            if (IsConnected)
            {

                var _Buffer = new Byte[BufferSize];
                var _BytesRead = 0;

                if (InputStream.CanTimeout && ReadTimeout != 1000)
                    InputStream.ReadTimeout = ReadTimeout;

                if (IsConnected)
                    do
                    {
                        _BytesRead = InputStream.Read(_Buffer, 0, _Buffer.Length);
                        Stream.Write(_Buffer, 0, _BytesRead);
                    } while (_BytesRead != 0);

            }

        }

        #endregion


        #region Close()

        /// <summary>
        /// Close this TCP connection.
        /// </summary>
        public void Close()
        {
            if (TCPClientConnection != null)
                TCPClientConnection.Close();
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Dispose this packet.
        /// </summary>
        public override void Dispose()
        {
            Close();
        }

        #endregion

    }

}
