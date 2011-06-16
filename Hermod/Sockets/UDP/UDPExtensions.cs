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
using System.Text;
using System.Net.Sockets;

using de.ahzf.Hermod.Datastructures;
using System.Net;

#endregion

namespace de.ahzf.Hermod.Sockets.UDP
{

    public static class UDPExtensions
    {

        #region Send(this UDPClient, UDPPacketString, Encoding = null, SocketFlags = SocketFlags.None)

        public static SocketError Send(this UDPClient UDPClient, String UDPPacketString, Encoding Encoding = null, SocketFlags SocketFlags = SocketFlags.None)
        {

            if (Encoding == null)
                Encoding = Encoding.UTF8;

            var UDPPacketData = Encoding.GetBytes(UDPPacketString);

            return UDPClient.Send(UDPPacketData, SocketFlags);

        }

        #endregion


        public static void SendTo(this UDPClient UDPClient, Byte[] UDPPacketData, IIPAddress RemoteIPAddress, IPPort IPPort, SocketFlags SocketFlags = SocketFlags.None)
        {
            var RemoteIPEndPoint = new IPEndPoint(new IPAddress(RemoteIPAddress.GetBytes()), IPPort.ToInt32());
            UDPClient.SendTo(UDPPacketData, RemoteIPEndPoint, SocketFlags);
        }

        public static void SendTo(this UDPClient UDPClient, String UDPPacketString, IIPAddress RemoteIPAddress, IPPort IPPort, Encoding Encoding = null, SocketFlags SocketFlags = SocketFlags.None)
        {

            if (Encoding == null)
                Encoding = Encoding.UTF8;

            var UDPPacketData = Encoding.GetBytes(UDPPacketString);
            var RemoteIPEndPoint = new IPEndPoint(new IPAddress(RemoteIPAddress.GetBytes()), IPPort.ToInt32());

            UDPClient.SendTo(UDPPacketData, RemoteIPEndPoint, SocketFlags);

        }


    }

}

