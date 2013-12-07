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
using System.Threading;
using System.Net.Sockets;
using System.Collections.Generic;

using eu.Vanaheimr.Illias.Commons;
using eu.Vanaheimr.Hermod.HTTP;
using eu.Vanaheimr.Hermod;

using NUnit.Framework;

#endregion

namespace eu.Vanaheimr.Hermod.UnitTests
{

    /// <summary>
    /// Initiate a TCP client request.
    /// </summary>
    public class TCPClientRequest
    {

        #region Data

        private readonly TcpClient TCPClient;
        private readonly NetworkStream TCPStream;

        #endregion

        public TCPClientRequest(String Hostname, Int32 Port)
        {
            TCPClient = new TcpClient(Hostname, Port);
            TCPStream = TCPClient.GetStream();
        }

        #region Send(RequestHeader)

        public TCPClientRequest Send(String Request)
        {
            var _Request = Request.ToUTF8Bytes();
            TCPStream.Write(_Request, 0, _Request.Length);
            return this;
        }

        #endregion

        #region Wait

        public TCPClientRequest Wait(UInt32 Milliseconds)
        {
            Thread.Sleep((Int32)Milliseconds);
            return this;
        }

        #endregion

        public TCPClientRequest FinishCurrentRequest()
        {
            Send(Environment.NewLine + Environment.NewLine);
            return this;
        }

        public String Response
        {
            get
            {

                var _Buffer         = new Byte[65335];
                var _ResponseString = "";

                while (!TCPStream.DataAvailable)
                { }

                while (TCPStream.DataAvailable)
                {
                    var _Read = TCPStream.Read(_Buffer, 0, _Buffer.Length);
                    var _Response = new Byte[_Read];
                    Array.Copy(_Buffer, _Response, _Read);
                    _ResponseString += _Response.ToUTF8String();
                    Thread.Sleep(3);
                }

                return _ResponseString;

            }
        }


        public void Close()
        {
            Send(Environment.NewLine + Environment.NewLine);
            TCPClient.Close();
        }

    }

}
