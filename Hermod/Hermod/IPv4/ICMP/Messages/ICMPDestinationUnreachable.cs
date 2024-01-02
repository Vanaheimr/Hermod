/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com> <achim.friedland@graphdefined.com>
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
    /// The ICMP Destination Unreachable
    /// </summary>
    /// <seealso cref="https://en.wikipedia.org/wiki/Internet_Control_Message_Protocol"/>
    public class ICMPDestinationUnreachable : IICMPMessage<ICMPDestinationUnreachable>
    {

        public enum CodeEnum : Byte
        {
            DestinationNetUnreachable                 =  0,
            DestinationHostUnreachable                =  1,
            DestinationProtocolUnreachable            =  2,
            DestinationPortUnreachable                =  3,
            FragmentationNeededAndDFSet               =  4,
            SourceRouteFailed                         =  5,
            DestinationNetworkUnknown                 =  6,
            DestinationHostUnknown                    =  7,
            SourceHostIsolated                        =  8,
            NetworkAdministrativelyProhibited         =  9,
            HostAdministrativelyProhibited            = 10,
            NetworkUnreachableForToS                  = 11,
            HostUnreachableForToS                     = 12,
            CommunicationAdministrativelyProhibited   = 13,
            HostPrecedenceViolation                   = 14,
            PrecedenceCutoffInEffect                  = 15
        }


        #region Properties

        public CodeEnum                                Code                  { get; }

        public UInt16                                  NextHopMTU            { get; }

        public Byte[]                                  Data                  { get; }

        public ICMPPacket<ICMPDestinationUnreachable>  ICMPPacket            { get; internal set; }

        public IPv4Packet                              EmbeddedIPv4Packet    { get; internal set; }

        #endregion

        #region (private) ICMPDestinationUnreachable(Code, ICMPPacket = null)

        private ICMPDestinationUnreachable(CodeEnum                                Code,
                                           UInt16                                  NextHopMTU,
                                           Byte[]                                  Data,
                                           IPv4Packet                              EmbeddedIPv4Packet   = null,
                                           ICMPPacket<ICMPDestinationUnreachable>  ICMPPacket           = null)
        {

            this.Code                = Code;
            this.NextHopMTU          = NextHopMTU;
            this.Data                = Data;
            this.EmbeddedIPv4Packet  = EmbeddedIPv4Packet;
            this.ICMPPacket          = ICMPPacket;

        }

        #endregion



        #region (static) Create(Code, NextHopMTU, Data, ICMPPacket = null)

        public static ICMPDestinationUnreachable Create(CodeEnum                                Code,
                                                        UInt16                                  NextHopMTU,
                                                        Byte[]                                  Data,
                                                        ICMPPacket<ICMPDestinationUnreachable>  ICMPPacket = null)
        {

            var echoReply =  new ICMPDestinationUnreachable(Code,
                                                            NextHopMTU,
                                                            Data,
                                                            null,
                                                            ICMPPacket);

            if (ICMPPacket == null)
                echoReply.ICMPPacket = new ICMPPacket<ICMPDestinationUnreachable>(Type:      3,
                                                                                  Code:      (Byte) Code,
                                                                                  Checksum:  0,
                                                                                  Payload:   echoReply);

            else
                echoReply.ICMPPacket.Payload = echoReply;

            // Will calculate the checksum
            echoReply.ICMPPacket?.GetBytes();

            return echoReply;

        }

        #endregion




        public static Boolean TryParse(ICMPPacket Packet, out ICMPDestinationUnreachable ICMPDestinationUnreachable)
        {

            try
            {

                var data = new Byte[Packet.PayloadBytes.Length - 4];
                Buffer.BlockCopy(Packet.PayloadBytes, 4, data, 0, data.Length);

                if (IPv4Packet.TryParse(data, out IPv4Packet ipv4Packet))
                {

                    ICMPDestinationUnreachable = new ICMPDestinationUnreachable((CodeEnum) Packet.Code,
                                                                                0,
                                                                                data,
                                                                                ipv4Packet);

                    ICMPDestinationUnreachable.ICMPPacket = new ICMPPacket<ICMPDestinationUnreachable>(Packet.Type, Packet.Code, Packet.Checksum, ICMPDestinationUnreachable, ipv4Packet);

                    return true;

                }


                ICMPDestinationUnreachable = Create(
                                                 (CodeEnum) Packet.Code,
                                                 (UInt16) System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt32(data, 0)),
                                                 data
                                             );

                return true;

            } catch
            { }

            ICMPDestinationUnreachable = default;
            return false;

        }

        public static Boolean TryParse(CodeEnum Code, Byte[] Packet, out ICMPDestinationUnreachable ICMPDestinationUnreachable)
        {

            try
            {

                var data = new Byte[Packet.Length - 4];
                Buffer.BlockCopy(Packet, 4, data, 0, data.Length);

                ICMPDestinationUnreachable = Create(
                                                 Code,
                                                 (UInt16) System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt32(Packet, 0)),
                                                 data
                                             );

                return true;

            } catch
            { }

            ICMPDestinationUnreachable = default;
            return false;

        }


        public Byte[] GetBytes()
        {

            var packet = new Byte[4 + Data.Length];
            Buffer.BlockCopy(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((Int16) NextHopMTU)), 0, packet, 0, 2);
            Buffer.BlockCopy(Data,                                                                               0, packet, 4, Data.Length);

            return packet;

        }



        public override String ToString()

            => Code.ToString();

    }

}
