/*
 * Copyright (c) 2010-2023 GraphDefined GmbH
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
    /// The ICMP Time Exceeded message.
    /// </summary>
    public class ICMPTimeExceeded : IICMPMessage<ICMPTimeExceeded>
    {

        #region Properties

        public Byte[]                        Data          { get; }

        public ICMPPacket<ICMPTimeExceeded>  ICMPPacket    { get; internal set; }

        public IPv4Packet                    EmbeddedIPv4Packet    { get; internal set; }

        #endregion

        #region (private) ICMPTTLExceeded(Data, IPv4Packet = null)

        private ICMPTimeExceeded(Byte[]                        Data,
                                 IPv4Packet                    EmbeddedIPv4Packet   = null,
                                 ICMPPacket<ICMPTimeExceeded>  ICMPPacket           = null)
        {

            this.Data                = Data;
            this.EmbeddedIPv4Packet  = EmbeddedIPv4Packet;
            this.ICMPPacket          = ICMPPacket;

        }

        #endregion


        #region (static) Create(Data, ICMPPacket = null)

        public static ICMPTimeExceeded Create(Byte[]      Data,
                                              IPv4Packet  IPv4Packet = null)
        {

            var echoReply =  new ICMPTimeExceeded(Data,
                                                  IPv4Packet);

            //if (ICMPPacket == null)
            //    echoReply.ICMPPacket = new ICMPPacket<ICMPTimeExceeded>(Type:      8,
            //                                                         Code:      0,
            //                                                         Checksum:  0,
            //                                                         Payload:   echoReply);

            //else
            //    echoReply.ICMPPacket.Payload = echoReply;

            //// Will calculate the checksum
            //echoReply.ICMPPacket?.GetBytes();

            return echoReply;

        }

        #endregion


        public static Boolean TryParse(ICMPPacket Packet, out ICMPTimeExceeded ICMPTimeExceeded)
        {

            ICMPTimeExceeded = null;

            try
            {

                var data = new Byte[Packet.PayloadBytes.Length - 4];
                Buffer.BlockCopy(Packet.PayloadBytes, 4, data, 0, data.Length);

                if (IPv4Packet.TryParse(data, out IPv4Packet ipv4Packet))
                {

                    ICMPTimeExceeded = new ICMPTimeExceeded(data,
                                                            ipv4Packet);

                    ICMPTimeExceeded.ICMPPacket = new ICMPPacket<ICMPTimeExceeded>(Packet.Type, Packet.Code, Packet.Checksum, ICMPTimeExceeded, ipv4Packet);

                    return true;

                }

                ICMPTimeExceeded = new ICMPTimeExceeded(data);
                return true;

            }
            catch
            { }

            return false;

        }


        public static Boolean TryParse(Byte[] Data, out ICMPTimeExceeded ICMPTimeExceeded)
        {

            ICMPTimeExceeded = null;

            try
            {

                if (IPv4Packet.TryParse(Data, out IPv4Packet ipv4Packet))
                {

                }


                ICMPTimeExceeded = new ICMPTimeExceeded(Data,
                                                        ipv4Packet);
                return true;

            } catch
            { }

            return false;

        }



        public Byte[] GetBytes()

            => GetBytes(false);


        public Byte[] GetBytes(Boolean WithPadding = false)
        {

            if (WithPadding)
            {

                var packet = new Byte[4 + Data.Length];
                Buffer.BlockCopy(Data, 0, packet, 4, Data.Length);

                return packet;

            }

            else
                return Data;

        }


        public override String ToString()

            => "ICMP Time Exceeded";

    }

}
