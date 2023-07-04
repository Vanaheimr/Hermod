/*
 * Copyright (c) 2010-2023, Achim Friedland <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod;

using NUnit.Framework;
using System.IO;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.UnitTests
{

    /// <summary>
    /// Initiate a TCP client request.
    /// </summary>
    public class TCPClientRequest
    {

        #region Data

        private readonly TcpClient      TCPClient;
        private readonly NetworkStream  TCPStream;

        #endregion

        public TCPClientRequest(String Hostname, Int32 Port)
        {
            TCPClient = new TcpClient(Hostname, Port);
            TCPStream = TCPClient.GetStream();
        }

        #region Send(Data)

        public TCPClientRequest Send(String Data)
        {
            var data = Data.ToUTF8Bytes();
            TCPStream.Write(data, 0, data.Length);
            return this;
        }

        #endregion

        #region Wait(Milliseconds)

        public TCPClientRequest Wait(UInt32 Milliseconds)
        {
            Thread.Sleep((Int32) Milliseconds);
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

                var memoryStream  = new MemoryStream();
                var buffer        = new Byte[65335];

                while (!TCPStream.DataAvailable)
                    Thread.Sleep(1);

                while (TCPStream.DataAvailable)
                {
                    var read = TCPStream.Read(buffer, 0, buffer.Length);
                    memoryStream.Write(buffer, 0, read);
                    Thread.Sleep(10);
                }

                return memoryStream.ToArray().ToUTF8String();

            }
        }


        public void Close()
        {
            Send(Environment.NewLine + Environment.NewLine);
            TCPClient.Close();
        }

    }

}
