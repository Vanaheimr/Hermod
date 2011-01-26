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
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Net.Sockets;

using de.ahzf.Hermod.Datastructures;

#endregion

namespace de.ahzf.Hermod.Sockets.UDP
{

    public abstract class AUDPPacket : IUDPPacket
    {


        #region Properties

        #region RemoteSocket

        protected readonly IPSocket _RemoteSocket;

        public IPSocket RemoteSocket
        {
            get
            {
                return _RemoteSocket;
            }
        }

        #endregion

        #region RemoteHost

        public IIPAddress RemoteHost
        {
            get
            {
                return _RemoteSocket.IPAddress;
            }
        }

        #endregion

        #region RemotePort

        public IPPort RemotePort
        {
            get
            {
                return _RemoteSocket.Port;
            }
        }

        #endregion


        #region TCPClientConnection

        protected readonly TcpClient _TCPClientConnection;

        /// <summary>
        /// The TCPClient connection to a connected Client
        /// </summary>
        public TcpClient TCPClientConnection
        {
            get
            {
                return _TCPClientConnection;
            }
        }

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

        #region AUDPPacket()

        /// <summary>
        /// Initiate a new abstract AUDPPacket
        /// </summary>
        public AUDPPacket()
        { }

        #endregion

        //#region ATCPConnection(myTCPClientConnection)

        ///// <summary>
        ///// Initiate a new abstract ATCPConnection using the given TcpClient class
        ///// </summary>
        //public ATCPConnection(TcpClient myTCPClientConnection)
        //{
        //    _TCPClientConnection = myTCPClientConnection;
        //    _RemoteHost          = _TCPClientConnection.Client.RemoteEndPoint;
        //}

        //#endregion

        #endregion



        
        ///// <summary>
        ///// Read all CURRENTLY available data (in the kernel) from the Stream. Keep in mind, that there might be more data than this method returns.
        ///// </summary>
        ///// <returns>A bunch of data which arrived at the kernel.</returns>
        //public Byte[] GetAllAvailableData()
        //{

        //    #region Data Definition

        //    NetworkStream networkStream = TCPClientConnection.GetStream();

        //    Byte[] ReadBuffer = new Byte[1024];

        //    Int32 BytesRead = 0;
        //    List<Byte> FirstBytesList = new List<Byte>();

        //    #endregion

        //    #region Read the FirstBytes until no Data is available or we read more than we can store in a List of Bytes

        //    do
        //    {

        //        BytesRead = networkStream.Read(ReadBuffer, 0, ReadBuffer.Length);
                
        //        if (BytesRead == ReadBuffer.Length)
        //            FirstBytesList.AddRange(ReadBuffer);

        //        else
        //        {
        //            Byte[] Temp = new Byte[BytesRead];
        //            Array.Copy(ReadBuffer, 0, Temp, 0, BytesRead);
        //            FirstBytesList.AddRange(Temp);
        //        }

        //    } while (networkStream.DataAvailable && BytesRead > 0 && FirstBytesList.Count < (Int32.MaxValue - ReadBuffer.Length));

        //    #endregion

        //    return FirstBytesList.ToArray();

        //}

        #region WaitForStreamDataAvailable(myNetworkStream)

        public Boolean WaitForStreamDataAvailable(NetworkStream myNetworkStream)
        {

            #region Timeout

            var Start = DateTime.Now;

            if (myNetworkStream == null)
                return false;

            while (!myNetworkStream.DataAvailable
                    && (((Int32)Timeout == System.Threading.Timeout.Infinite) || (DateTime.Now.Subtract(Start).TotalMilliseconds < Timeout)))
            {
                Thread.Sleep(1);
            }

            #endregion

            // If we have any DataAvailable than proceed, even if StopRequested is true
            if (myNetworkStream.DataAvailable)
                return true;

            if (DateTime.Now.Subtract(Start).TotalMilliseconds >= Timeout)
                Debug.WriteLine("[ATcpSocketConnection][StreamDataAvailableTimeout] timedout after " + Timeout + "ms");

            return false;

        }

        #endregion

        #region WaitForStreamDataAvailable()

        /// <summary>
        /// Wait until new StreamData is available timeout or server shutdown
        /// </summary>
        /// <returns>True: if new StreamData is available. False: if timeout or server shutdown</returns>
        public Boolean WaitForStreamDataAvailable()
        {

            #region Timeout

            var Start = DateTime.Now;

            if (TCPClientConnection == null)
                return false;

            var stream = TCPClientConnection.GetStream();

            if (stream == null)
                return false;

            while (!StopRequested && TCPClientConnection.Connected
                    && !stream.DataAvailable
                    && (((Int32)Timeout == System.Threading.Timeout.Infinite) || (DateTime.Now.Subtract(Start).TotalMilliseconds < Timeout)))
            {
                Thread.Sleep(1);
            }

            #endregion

            if (StopRequested || !TCPClientConnection.Connected)
            {
                Debug.WriteLine("[ATcpSocketConnection][StreamDataAvailableTimeout] Stop requested");
                return false;
            }

            // If we have any DataAvailable than proceed, even if StopRequested is true
            if (stream.DataAvailable)
                return true;

            if (DateTime.Now.Subtract(Start).TotalMilliseconds >= Timeout)
                Debug.WriteLine("[ATcpSocketConnection][StreamDataAvailableTimeout] timedout after " + Timeout + "ms");

            return false;

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
            if (myContent != null)
                TCPClientConnection.GetStream().Write(myContent, 0, myContent.Length);
        }

        #endregion

        #region WriteToResponseStream(myInputStream, myReadTimeout = 1000)

        public void WriteToResponseStream(Stream myInputStream, Int32 myReadTimeout = 1000)
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

        #endregion


        #region Close()

        public void Close()
        {
            if (_TCPClientConnection != null)
                _TCPClientConnection.Close();
        }

        #endregion


        #region IDisposable Members

        public abstract void Dispose();

        #endregion

    }

}
