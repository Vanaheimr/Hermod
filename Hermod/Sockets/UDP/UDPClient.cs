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
using System.Net;
using System.Net.Sockets;

#endregion

namespace de.ahzf.Hermod.Sockets.UDP
{

    public class UDPClient : IDisposable
    {

        #region Data

        private IPEndPoint _RemoteIPEndPoint;
        private readonly Socket _Socket;

        #endregion

        #region Properties

        #region BufferSize

        /// <summary>
        /// The size of the receive buffer.
        /// </summary>
        public UInt32 BufferSize { get; set; }

        #endregion

        #endregion

        #region Constructor(s)

        #region UDPClient()

        public UDPClient()
        {
            this._Socket           = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            this._RemoteIPEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5000);
        }

        #endregion

        #endregion


        public SocketError Send(Byte[] UDPPacketData, SocketFlags SocketFlags = SocketFlags.None)
        {

            if (_Socket == null)
                throw new Exception("Socket == null!");

            if (_RemoteIPEndPoint == null)
                throw new Exception("IPEndPoint == null!");

            SocketError SocketErrorCode;
            _Socket.Send(UDPPacketData, 0, UDPPacketData.Length, SocketFlags, out SocketErrorCode);
            return SocketErrorCode;

        }


        public void SendTo(Byte[] UDPPacketData, IPEndPoint RemoteIPEndPoint, SocketFlags SocketFlags = SocketFlags.None)
        {

            if (_Socket == null)
                throw new Exception("Socket == null!");

            if (_RemoteIPEndPoint == null)
                throw new Exception("IPEndPoint == null!");

            _Socket.SendTo(UDPPacketData, SocketFlags, RemoteIPEndPoint);

        }


        public Byte[] SendAndWaitForReponse(Byte[] UDPPacketData, SocketFlags SocketFlags = SocketFlags.None)
        {

            var _SocketError =  Send(UDPPacketData, SocketFlags);
            
            var _UDPRepose             = new Byte[BufferSize];
            var _RemoteEndPoint        = (EndPoint) _RemoteIPEndPoint;
            var _NumberOfReceivedBytes = _Socket.ReceiveFrom(_UDPRepose, ref _RemoteEndPoint);

            if (_NumberOfReceivedBytes > 0)
            {
                Array.Resize(ref _UDPRepose, _NumberOfReceivedBytes);
                return _UDPRepose;
            }

            return new Byte[0];
        
        }


        #region Close()

        /// <summary>
        /// Close this UDP client.
        /// </summary>
        public void Close()
        {
            if (_Socket != null)
                _Socket.Close();
        }

        #endregion

        #region Dispose()

        public void Dispose()
        {
            if (_Socket != null)
                _Socket.Close();
        }

        #endregion

    }

}