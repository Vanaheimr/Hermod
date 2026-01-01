/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Sockets.RawIP.ICMP
{

    /// <summary>
    /// The ICMP Redirect
    /// </summary>
    /// <seealso cref="https://en.wikipedia.org/wiki/Internet_Control_Message_Protocol"/>
    public class ICMPRedirect : IICMPMessage<ICMPRedirect>
    {

        public enum CodeEnum : Byte
        {
            RedirectDatagramsForTheNetwork                   = 0,
            RedirectDatagramsForTheHost                      = 1,
            RedirectDatagramsForTheTypeOfServiceAndNetwork   = 2,
            RedirectDatagramsForTheTypeOfServiceAndHost      = 3
        }


        #region Properties

        public CodeEnum                  Code                      { get; }
        public IPv4Address               GatewayInternetAddress    { get; }
        public Byte[]                    Data                      { get; }
        public ICMPPacket<ICMPRedirect>  ICMPPacket                { get; internal set; }

        #endregion

        #region (private) ICMPRedirect(Code, GatewayInternetAddress, Data, ICMPPacket = null)

        private ICMPRedirect(CodeEnum                  Code,
                             IPv4Address               GatewayInternetAddress,
                             Byte[]                    Data,
                             ICMPPacket<ICMPRedirect>  ICMPPacket = null)
        {

            this.Code                    = Code;
            this.GatewayInternetAddress  = GatewayInternetAddress;
            this.Data                    = Data;
            this.ICMPPacket              = ICMPPacket;

        }

        #endregion



        #region (static) Create(Code, GatewayInternetAddress, Data, ICMPPacket = null)

        /// <summary>
        /// Create a new ICMP redirect packet.
        /// </summary>
        /// <param name="Code">The ICMP redirect code.</param>
        /// <param name="GatewayInternetAddress">IPv4 address.</param>
        /// <param name="Data">IP header and first 8 bytes of original datagram's data.</param>
        /// <param name="ICMPPacket">ICMP packet.</param>
        public static ICMPRedirect Create(CodeEnum                  Code,
                                          IPv4Address               GatewayInternetAddress,
                                          Byte[]                    Data,
                                          ICMPPacket<ICMPRedirect>  ICMPPacket = null)
        {

            var echoRedirect =  new ICMPRedirect(Code,
                                                 GatewayInternetAddress,
                                                 Data,
                                                 ICMPPacket);

            if (ICMPPacket is null)
                echoRedirect.ICMPPacket = new ICMPPacket<ICMPRedirect>(Type:      5,
                                                                       Code:      (Byte) Code,
                                                                       Checksum:  0,
                                                                       Payload:   echoRedirect);

            else
                echoRedirect.ICMPPacket.Payload = echoRedirect;

            // Will calculate the checksum
            echoRedirect.ICMPPacket?.GetBytes();

            return echoRedirect;

        }

        #endregion


        public static Boolean TryParse(CodeEnum Code, Byte[] Packet, out ICMPRedirect ICMPRedirect)
        {

            try
            {

                var data = new Byte[Packet.Length - 4];
                Buffer.BlockCopy(Packet, 4, data, 0, data.Length);

                ICMPRedirect = Create(
                                   Code,
                                   new IPv4Address(System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt32(Packet, 0))),
                                   data
                               );

                return true;

            } catch
            { }

            ICMPRedirect = default;
            return false;

        }

        public Byte[] GetBytes()
        {

            var packet = new Byte[4 + Data.Length];
            Buffer.BlockCopy(GatewayInternetAddress.GetBytes(), 0, packet, 0, 4);
            Buffer.BlockCopy(Data,                              0, packet, 4, Data.Length);

            return packet;

        }


        public override String ToString()

            => String.Concat(Code.ToString(), " => ", GatewayInternetAddress.ToString());

    }

}
