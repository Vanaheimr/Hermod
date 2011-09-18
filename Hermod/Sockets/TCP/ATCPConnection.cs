/*
 * Copyright (c) 2010-2011, Achim 'ahzf' Friedland <code@ahzf.de>
 * This file is part of Hermod
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
using System.Diagnostics;
using System.Net.Sockets;

using de.ahzf.Hermod.Datastructures;

#endregion

namespace de.ahzf.Hermod.Sockets.TCP
{

    /// <summary>
    /// An abstract class for all TCP connections.
    /// </summary>
    public abstract class ATCPConnection : ALocalRemoteSockets, ITCPConnection
    {

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

        #region Timeout

        /// <summary>
        /// The Client ConnectionEstablished should timeout after this Timeout in
        /// Milliseconds - should be impemented in ConnectionEstablished logic.
        /// </summary>
        public UInt32 Timeout { get; set; }

        #endregion

        #region KeepAlive

        /// <summary>
        ///  The connection is keepalive
        /// </summary>
        public Boolean KeepAlive { get; set; }

        #endregion

        #region StopRequested

        /// <summary>
        /// Server requested stopping
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

        }

        #endregion

        #endregion


        #region WriteToResponseStream(Text)

        /// <summary>
        /// Writes some UTF-8 text to the underlying stream.
        /// </summary>
        /// <param name="Text">Some UTF-8 text.</param>
        public void WriteToResponseStream(String Text)
        {
            WriteToResponseStream(Text.ToUTF8Bytes());
        }

        #endregion

        #region WriteToResponseStream(Content)

        /// <summary>
        /// Writes the given byte array to the underlying stream.
        /// </summary>
        /// <param name="Content">An array of bytes.</param>
        public void WriteToResponseStream(Byte[] Content)
        {
            if (IsConnected)
                if (Content != null)
                {
                    var Stream = TCPClientConnection.GetStream();
                    if (Stream != null)
                        Stream.Write(Content, 0, Content.Length);
                }
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

                var Stream = TCPClientConnection.GetStream();

                if (Stream != null)
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
