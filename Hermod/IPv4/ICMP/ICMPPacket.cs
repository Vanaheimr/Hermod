/*
 * Copyright (c) 2010-2021, Achim Friedland <achim.friedland@graphdefined.com>
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

    public class ICMPPacket<TICMPMessage> : ICMPPacket

        where TICMPMessage : IICMPMessage<TICMPMessage>

    {

        public new TICMPMessage  Payload    { get; internal set; }

        public ICMPPacket(Byte          Type,
                          Byte          Code,
                          UInt16        Checksum,
                          TICMPMessage  Payload,
                          IPv4Packet    IPv4Packet   = null)

            : base(Type,
                   Code,
                   Checksum,
                   Payload?.GetBytes(),
                   IPv4Packet)

        {

            this.Payload = Payload;

        }

    }


    /// <summary>
    /// ICMP packet, rfc792
    /// </summary>
    public class ICMPPacket
    {

        public Byte        Type            { get; }
        public Byte        Code            { get; }
        public UInt16      Checksum        { get; private set; }
        public Byte[]      PayloadBytes    { get; }
        public IPv4Packet  IPv4Packet      { get; }

        public ICMPPacket(Byte        Type,
                          Byte        Code,
                          UInt16      Checksum,
                          Byte[]      PayloadBytes   = null,
                          IPv4Packet  IPv4Packet     = null)
        {

            this.Type          = Type;
            this.Code          = Code;
            this.Checksum      = Checksum;
            this.PayloadBytes  = PayloadBytes ?? new Byte[0];
            this.IPv4Packet    = IPv4Packet;

        }


        public static Boolean TryParse(IPv4Packet IPv4Packet, out ICMPPacket ICMPPacket)

            => TryParse(IPv4Packet.Payload,
                        out ICMPPacket,
                        IPv4Packet);

        public static Boolean TryParse(Byte[] Packet, out ICMPPacket ICMPPacket, IPv4Packet IPv4Packet = null)
        {

            try
            {

                var payload = new Byte[Packet.Length - 4];
                Buffer.BlockCopy(Packet, 4, payload, 0, payload.Length);

                ICMPPacket = new ICMPPacket(
                                 Packet[0],
                                 Packet[1],
                                 BitConverter.ToUInt16(Packet, 2),
                                 payload,
                                 IPv4Packet
                             );

                return true;

            } catch
            { }

            ICMPPacket = default;
            return false;

            //switch (Type) {
            //    case  0: IPv4Packet = new ICMPEchoReply             (ref PacketData); break;
            //    case  3: IPv4Packet = new ICMPDestinationUnreachable(ref PacketData); break;
            //    case  4: IPv4Packet = new ICMPSourceQuench          (ref PacketData); break;
            //    case  5: IPv4Packet = new ICMPRedirect              (ref PacketData); break;
            //    case  8: IPv4Packet = new ICMPEcho                  (ref PacketData); break;
            //    case 11: IPv4Packet = new ICMPTimeExceeded          (ref PacketData); break;
            //    case 12: IPv4Packet = new ICMPParameterProblem      (ref PacketData); break;
            //    case 13: IPv4Packet = new ICMPTimestamp             (ref PacketData); break;
            //    case 14: IPv4Packet = new ICMPTimestampReply        (ref PacketData); break;
            //    case 15: IPv4Packet = new ICMPInformationRequest    (ref PacketData); break;
            //    case 16: IPv4Packet = new ICMPInformationReply      (ref PacketData); break;
            //}

        }

        public Byte[] GetBytes()
        {

            //if (IPv4Packet != null)
            //    PacketData = IPv4Packet.GetBytes();

            //if (IPv4Packet is ICMPEchoReply)              Type =  0; else
            //if (IPv4Packet is ICMPDestinationUnreachable) Type =  3; else
            //if (IPv4Packet is ICMPSourceQuench)           Type =  4; else
            //if (IPv4Packet is ICMPRedirect)               Type =  5; else
            //if (IPv4Packet is ICMPEcho)                   Type =  8; else
            //if (IPv4Packet is ICMPTimeExceeded)           Type = 11; else
            //if (IPv4Packet is ICMPParameterProblem)       Type = 12; else
            //if (IPv4Packet is ICMPTimestamp)              Type = 13; else
            //if (IPv4Packet is ICMPTimestampReply)         Type = 14; else
            //if (IPv4Packet is ICMPInformationRequest)     Type = 15; else
            //if (IPv4Packet is ICMPInformationReply)       Type = 16;

            var packet = new Byte[4 + PayloadBytes.Length];
            packet[0] = Type;
            packet[1] = Code;
            Buffer.BlockCopy(BitConverter.GetBytes((Int16) 0), 0, packet, 2, 2);
            Buffer.BlockCopy(PayloadBytes, 0, packet, 4, PayloadBytes.Length);

            Checksum = GetChecksum(packet, 0, packet.Length - 1);
            Buffer.BlockCopy(BitConverter.GetBytes((Int16) Checksum), 0, packet, 2, 2);

            return packet;

        }

        public UInt16 GetChecksum(Byte[] ICMPPacket, Int32 Start, Int32 End)
        {

            UInt32 CheckSum = 0;
            Int32  i;

            for (i=Start; i<End; i+=2)
                CheckSum += (UInt16) BitConverter.ToInt16(ICMPPacket, i);

            if (i == End)
                CheckSum += (UInt16) ICMPPacket[End];

            while (CheckSum >> 16 != 0)
                CheckSum = (CheckSum & 0xFFFF) + (CheckSum >> 16);

            return (UInt16) ~CheckSum;

        }

    }

}
