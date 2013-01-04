/*
 * Copyright (c) 2010-2012, Achim 'ahzf' Friedland <achim@graph-database.org>
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

using de.ahzf.Illias.Commons;
using de.ahzf.Vanaheimr.Hermod.Datastructures;

#endregion

namespace de.ahzf.Vanaheimr.Hermod.Sockets.TCP
{

    /// <summary>
    /// An abstract class for all TCP connections.
    /// </summary>
    public abstract class ATCPConnection : ALocalRemoteSockets, ITCPConnection
    {

        #region Data

        private readonly NetworkStream Stream;

        #endregion

        #region Properties

        #region TCPClientConnection

        /// <summary>
        /// The TCPClient connection to a connected Client
        /// </summary>
        public TcpClient TCPClientConnection { get; protected set; }

        #endregion

        #region IsConnected

        /// <summary>
        /// Is False if the client is disconnected from the server
        /// </summary>
        public Boolean IsConnected
        {
            get
            {

                if (TCPClientConnection != null)
                    return TCPClientConnection.Connected;

                return false;

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
        /// Gets or sets the amount of time that a read operation
        /// blocks waiting for data.
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

        public event ExceptionOccuredHandler OnExceptionOccured;

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

            Stream = TCPClientConnection.GetStream();

        }

        #endregion

        #endregion


        #region ReadByte()

        public Byte ReadByte()
        {

            var _byte = Stream.ReadByte();

            if (_byte == -1)
                return 0x00;

            return (Byte) _byte;

        }

        #endregion

        #region ReadByte(out Byte)

        public Boolean ReadByte(out Byte Byte)
        {

            var _byte = Stream.ReadByte();

            if (_byte == -1)
            {
                Byte = 0;
                return false;
            }

            Byte = (Byte) _byte;
            return true;

        }

        #endregion

        #region ReadString(MaxLength)

        public String ReadString(Int32 MaxLength)
        {

            if (Stream == null)
                throw new ArgumentNullException();

            while (!Stream.DataAvailable)
                Thread.Sleep(1);

            Byte read;
            var array = new Byte[MaxLength];
            var pos = 0;

            while (Stream.DataAvailable)
            {

                read = (Byte) Stream.ReadByte();

                array[pos++] = read;

                if (read == 0x00 || pos == MaxLength)
                    break;

            }

            var str = Encoding.UTF8.GetString(array).Trim();

            return str;

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
