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
    /// IPv4 Packet (RFC 791)
    /// </summary>
    public class IPv4Packet
    {

        public Byte           Version               { get; }
        public Byte           HeaderLength          { get; }
        public Byte           TypeOfService         { get; }
        public UInt16         TotalLength           { get; }
        public UInt16         Identification        { get; }
        public Byte           Flags                 { get; }
        public UInt16         FragmentOffset        { get; }
        public Byte           TimeToLive            { get; }
        public IPv4Protocols  Protocol              { get; }
        public UInt16         HeaderChecksum        { get; }
        public IPv4Address    SourceAddress         { get; }
        public IPv4Address    DestinationAddress    { get; }
        public Byte[]         Options               { get; }
        public Byte[]         Payload               { get; }


        public IPv4Packet(Byte           Version,
                          Byte           HeaderLength,
                          Byte           TypeOfService,
                          UInt16         TotalLength,
                          UInt16         Identification,
                          Byte           Flags,
                          UInt16         FragmentOffset,
                          Byte           TimeToLive,
                          IPv4Protocols  Protocol,
                          UInt16         HeaderChecksum,
                          IPv4Address    SourceAddress,
                          IPv4Address    DestinationAddress,
                          Byte[]         Options,
                          Byte[]         Payload)
        {

            this.Version             = Version;
            this.HeaderLength        = HeaderLength;
            this.TypeOfService       = TypeOfService;
            this.TotalLength         = TotalLength;
            this.Identification      = Identification;
            this.Flags               = Flags;
            this.FragmentOffset      = FragmentOffset;
            this.TimeToLive          = TimeToLive;
            this.Protocol            = Protocol;
            this.HeaderChecksum      = HeaderChecksum;
            this.SourceAddress       = SourceAddress;
            this.DestinationAddress  = DestinationAddress;
            this.Options             = Options;
            this.Payload             = Payload;

        }

        public IPv4Packet(Byte           TypeOfService,
                          UInt16         Identification,
                          Byte           Flags,
                          IPv4Protocols  Protocol,
                          IPv4Address    SourceAddress,
                          IPv4Address    DestinationAddress,
                          Byte[]         Payload)
        {

            this.Version             = 4;
            this.HeaderLength        = 20;
            this.TypeOfService       = TypeOfService;
            this.TotalLength         = (UInt16) (HeaderLength + (Payload?.Length ?? 0));
            this.Identification      = Identification;
            this.Flags               = Flags;
            this.FragmentOffset      = 0;
            this.TimeToLive          = 128;
            this.Protocol            = Protocol;
            this.SourceAddress       = SourceAddress;
            this.DestinationAddress  = DestinationAddress;
            this.Options             = new Byte[0];
            this.Payload             = Payload ?? new Byte[0];

        }


        public static Boolean TryParse(Byte[] PacketBytes, out IPv4Packet IPPacket)
        {

            try
            {


                var HeaderLength  = (Byte) ((PacketBytes[0] & 0x0F) * 4);
                var TotalLength   = (UInt16) System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(PacketBytes, 2));

                var options       = new Byte[HeaderLength - 20];
                if (options.Length > 0)
                    Buffer.BlockCopy(PacketBytes, 20, options, 0, options.Length);

                var payload       = new Byte[TotalLength - HeaderLength];
                if (payload.Length > 0)
                    Buffer.BlockCopy(PacketBytes, HeaderLength, payload, 0, payload.Length);

                IPPacket = new IPv4Packet(
                               (Byte) (PacketBytes[0] >> 4),
                               HeaderLength,
                               PacketBytes[1],
                               TotalLength,
                               (UInt16)  System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(PacketBytes,  4)),
                               (Byte)  ((PacketBytes[6] & 0xE0) >> 5),
                               (UInt16) (System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(PacketBytes,  6)) & 0x1FFF),
                               PacketBytes[8],
                               (IPv4Protocols) PacketBytes[9],
                               (UInt16)  System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(PacketBytes, 10)),
                               new IPv4Address(BitConverter.ToUInt32(PacketBytes, 12) & 0x00000000FFFFFFFF),
                               new IPv4Address(BitConverter.ToUInt32(PacketBytes, 16) & 0x00000000FFFFFFFF),
                               options,
                               payload
                           );

                return true;

            } catch
            { }

            IPPacket = default;
            return false;

        }

        public Byte[] GetBytes()
        {

            var packetBytes = new Byte[TotalLength];

            packetBytes[0] = (byte)(((Version & 0x0F) << 4) | ((HeaderLength / 4) & 0x0F));
            packetBytes[1] = TypeOfService;
            Buffer.BlockCopy(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((Int16) TotalLength)),                                        0, packetBytes, 2, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((Int16) Identification)),                                     0, packetBytes, 4, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((Int16) ((FragmentOffset & 0x1F) | ((Flags & 0x03) << 13)))), 0, packetBytes, 6, 2);
            packetBytes[8] = TimeToLive;
            packetBytes[9] = (Byte) Protocol;
            Buffer.BlockCopy(BitConverter.      GetBytes(0), 0, packetBytes, 10, 2); // header checksum = 0x0000
            Buffer.BlockCopy(SourceAddress.     GetBytes(),  0, packetBytes, 12, 4);
            Buffer.BlockCopy(DestinationAddress.GetBytes(),  0, packetBytes, 16, 4);

            if (Options.Length > 20)
                Buffer.BlockCopy(Options, 0, packetBytes, 20, Options.Length);

            Buffer.BlockCopy(Payload, 0, packetBytes, HeaderLength, Payload.Length);

            // Calculate and update header checksum
            Buffer.BlockCopy(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((Int16) GetChecksum(packetBytes, 0, HeaderLength - 1))), 0, packetBytes, 10, 2);

            return packetBytes;

        }

        public static UInt16 GetChecksum(Byte[] IPv4Packet, Int32 Start, Int32 End)
        {

            UInt32 CheckSum = 0;
            Int32  i;

            for (i = Start; i < End; i += 2)
                CheckSum += (UInt16) System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(IPv4Packet, i));

            if (i == End)
                CheckSum += (UInt16) System.Net.IPAddress.NetworkToHostOrder((UInt16) IPv4Packet[End]);

            while (CheckSum >> 16 != 0)
                CheckSum = (CheckSum & 0xFFFF) + (CheckSum >> 16);

            return (UInt16) ~CheckSum;

        }

    }

}
