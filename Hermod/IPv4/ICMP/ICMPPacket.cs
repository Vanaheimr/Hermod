/*
 * Copyright (c) 2010-2022, Achim Friedland <achim.friedland@graphdefined.com>
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
    /// A generic ICMP packet.
    /// </summary>
    /// <typeparam name="TICMPMessage">The type of the ICMP message.</typeparam>
    /// <seealso cref="https://www.rfc-editor.org/rfc/rfc792.html"/>
    public class ICMPPacket<TICMPMessage> : ICMPPacket

        where TICMPMessage : IICMPMessage<TICMPMessage>

    {

        #region Properties

        /// <summary>
        /// The ICMP message.
        /// </summary>
        public TICMPMessage  Payload    { get; internal set; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new generic ICMP packet.
        /// </summary>
        /// <param name="Type">The ICMP message type.</param>
        /// <param name="Code">The ICMP code.</param>
        /// <param name="Checksum">The ICMP checksum.</param>
        /// <param name="Payload">The ICMP payload.</param>
        /// <param name="IPv4Packet">The optional transporting IPv4 packet.</param>
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

        #endregion

    }


    /// <summary>
    /// An ICMP packet.
    /// </summary>
    /// <seealso cref="https://www.rfc-editor.org/rfc/rfc792.html"/>
    public class ICMPPacket
    {

        #region Properties

        /// <summary>
        /// The ICMP message type.
        /// </summary>
        public Byte        Type            { get; }

        /// <summary>
        /// The ICMP code.
        /// </summary>
        public Byte        Code            { get; }

        /// <summary>
        /// The ICMP checksum.
        /// </summary>
        public UInt16      Checksum        { get; private set; }

        /// <summary>
        /// The binary ICMP payload.
        /// </summary>
        public Byte[]      PayloadBytes    { get; }

        /// <summary>
        /// The optional transporting IPv4 packet.
        /// </summary>
        public IPv4Packet  IPv4Packet      { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new generic ICMP packet.
        /// </summary>
        /// <param name="Type">The ICMP message type.</param>
        /// <param name="Code">The ICMP code.</param>
        /// <param name="Checksum">The ICMP checksum.</param>
        /// <param name="PayloadBytes">The binary ICMP payload.</param>
        /// <param name="IPv4Packet">The optional transporting IPv4 packet.</param>
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

        #endregion


        #region TryParse(IPv4Packet, out ICMPPacket, Offset = 4)

        /// <summary>
        /// Try to parse the ICMP packet embedded within the given IPv4 packet.
        /// </summary>
        /// <param name="IPv4Packet">An IPv4 packet.</param>
        /// <param name="ICMPPacket">The parsed ICMP packet.</param>
        /// <param name="Offset">The offset of the ICMP pakcet within the array of bytes.</param>
        public static Boolean TryParse(IPv4Packet      IPv4Packet,
                                       out ICMPPacket  ICMPPacket,
                                       Byte            Offset   = 4)

            => TryParse(IPv4Packet.Payload,
                        out ICMPPacket,
                        Offset,
                        IPv4Packet);

        #endregion

        #region (Packet, out ICMPPacket, Offset = 4, IPv4Packet = null)

        /// <summary>
        /// Try to parse the given ICMP packet.
        /// </summary>
        /// <param name="Packet">An array of bytes to parse.</param>
        /// <param name="ICMPPacket">The parsed ICMP packet.</param>
        /// <param name="Offset">The offset of the ICMP pakcet within the array of bytes.</param>
        /// <param name="IPv4Packet"></param>
        public static Boolean TryParse(Byte[]          Packet,
                                       out ICMPPacket  ICMPPacket,
                                       Byte            Offset       = 4,
                                       IPv4Packet      IPv4Packet   = null)
        {

            try
            {

                var payload = new Byte[Packet.Length - Offset];
                Buffer.BlockCopy(Packet, Offset, payload, 0, payload.Length);

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

        #endregion

        #region GetBytes()

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

        #endregion

        #region GetChecksum(ICMPPacket, Start, End)

        public UInt16 GetChecksum(Byte[] ICMPPacket, Int32 Start, Int32 End)
        {

            UInt32 checkSum = 0;
            Int32  i;

            for (i=Start; i<End; i+=2)
                checkSum += (UInt16) BitConverter.ToInt16(ICMPPacket, i);

            if (i == End)
                checkSum += (UInt16) ICMPPacket[End];

            while (checkSum >> 16 != 0)
                checkSum = (checkSum & 0xFFFF) + (checkSum >> 16);

            return (UInt16) ~checkSum;

        }

        #endregion

    }

}
