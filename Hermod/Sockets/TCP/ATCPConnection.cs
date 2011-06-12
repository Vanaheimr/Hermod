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

    public abstract class ATCPConnection : ITCPConnection
    {

        #region Properties

        #region LocalSocket

        /// <summary>
        /// The local socket.
        /// </summary>
        public IPSocket LocalSocket { get; protected set; }


        /// <summary>
        /// The local host.
        /// </summary>
        public IIPAddress LocalHost
        {
            get
            {
                return LocalSocket.IPAddress;
            }
        }


        /// <summary>
        /// The local port.
        /// </summary>
        public IPPort LocalPort
        {
            get
            {
                return LocalSocket.Port;
            }
        }

        #endregion

        #region RemoteSocket

        /// <summary>
        /// The remote socket.
        /// </summary>
        public IPSocket RemoteSocket { get; protected set; }


        /// <summary>
        /// The remote host.
        /// </summary>
        public IIPAddress RemoteHost
        {
            get
            {
                return RemoteSocket.IPAddress;
            }
        }


        /// <summary>
        /// The remote port.
        /// </summary>
        public IPPort RemotePort
        {
            get
            {
                return RemoteSocket.Port;
            }
        }

        #endregion


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
            var _IPEndPoint = this.TCPClientConnection.Client.RemoteEndPoint as IPEndPoint;
            RemoteSocket      = new IPSocket(new IPv4Address(_IPEndPoint.Address), new IPPort((UInt16) _IPEndPoint.Port));

            if (RemoteSocket == null)
                throw new ArgumentNullException("The RemoteEndPoint is invalid!");

        }

        #endregion

        #endregion


        #region OnExceptionOccured

        public delegate void ExceptionOccuredHandler(Object mySender, Exception myException);

        public event TCPServer.ExceptionOccuredHandler OnExceptionOccured
        {
            add    { throw new NotImplementedException(); }
            remove { throw new NotImplementedException(); }
        }

        #endregion


        #region WriteToResponseStream(myText)

        public void WriteToResponseStream(String myText)
        {
            WriteToResponseStream(Encoding.UTF8.GetBytes(myText));
        }

        #endregion

        #region WriteToResponseStream(myContent)

        public void WriteToResponseStream(Byte[] myContent)
        {
            if (IsConnected)
                if (myContent != null)
                    TCPClientConnection.GetStream().Write(myContent, 0, myContent.Length);
        }

        #endregion

        #region WriteToResponseStream(myInputStream, myReadTimeout = 1000)

        public void WriteToResponseStream(Stream myInputStream, Int32 myReadTimeout = 1000)
        {

            if (IsConnected)
            {

                var _Buffer = new Byte[65535];
                var _BytesRead = 0;

                if (myInputStream.CanTimeout && myReadTimeout != 1000)
                    myInputStream.ReadTimeout = myReadTimeout;

                do
                {
                    _BytesRead = myInputStream.Read(_Buffer, 0, _Buffer.Length);
                    TCPClientConnection.GetStream().Write(_Buffer, 0, _BytesRead);
                } while (_BytesRead != 0);

            }

        }

        #endregion


        #region Close()

        public void Close()
        {
            if (TCPClientConnection != null)
                TCPClientConnection.Close();
        }

        #endregion


        #region IDisposable Members

        public abstract void Dispose();

        #endregion

    }

}
