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

using System.Diagnostics.CodeAnalysis;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.IPv4
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

            if (Version != 4)
                throw new ArgumentException("IPv4 Version must be 4.", nameof(Version));

            if (HeaderLength < 20 || HeaderLength % 4 != 0)
                throw new ArgumentException("HeaderLength must be >= 20 and a multiple of 4.", nameof(HeaderLength));

            if (TotalLength < HeaderLength)
                throw new ArgumentException("TotalLength must be at least HeaderLength.", nameof(TotalLength));

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

            // Store copies of the options and payload to ensure immutability of the packet!
            this.Options             = Options?.ToArray() ?? [];
            this.Payload             = Payload?.ToArray() ?? [];

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
            this.Options             = [];
            this.Payload             = Payload ?? [];

        }


        public static Boolean TryParse(Byte[]                               PacketBytes,
                                       [NotNullWhen(true)] out IPv4Packet?  IPPacket)
        {

            IPPacket = default;

            if (PacketBytes is null || PacketBytes.Length < 20)
                return false;

            try
            {

                var versionAndIHL = PacketBytes[0];
                var version       = (Byte) (versionAndIHL >>    4);
                var ihl           = (Byte) (versionAndIHL  & 0x0F);
                var headerLength  = (Byte) (ihl * 4);

                if (version != 4 || headerLength < 20 || headerLength > PacketBytes.Length)
                    return false;

                var totalLength   = (UInt16) ((PacketBytes[2] << 8) | PacketBytes[3]);

                if (totalLength < headerLength || totalLength > PacketBytes.Length)
                    return false;

                var options       = headerLength > 20
                                        ? PacketBytes[20..headerLength]
                                        : [];

                var payload       = totalLength > headerLength
                                        ? PacketBytes[headerLength..totalLength]
                                        : [];

                IPPacket = new IPv4Packet(
                               version,
                               headerLength,
                               PacketBytes[1],
                               totalLength,
                               (UInt16)      ((PacketBytes[4]         << 8) | PacketBytes[5]),  // Identification
                               (Byte)        ((PacketBytes[6] & 0xE0) >> 5),                    // Flags
                               (UInt16)     (((PacketBytes[6] & 0x1F) << 8) | PacketBytes[7]),  // FragmentOffset
                               PacketBytes[8],
                               (IPv4Protocols) PacketBytes[9],
                               (UInt16)      ((PacketBytes[10]        << 8) | PacketBytes[11]), // HeaderChecksum
                               new IPv4Address(new ReadOnlySpan<Byte>(PacketBytes, 12, 4)),
                               new IPv4Address(new ReadOnlySpan<Byte>(PacketBytes, 16, 4)),
                               options,
                               payload
                           );

                return true;

            }
            catch
            {
                return false;
            }

        }


        public Byte[] GetBytes()
        {

            var packetBytes = new Byte[TotalLength];

            // Byte 0: Version (4 Bit) + Internet Header Length (IHL, 4 Bit)
            packetBytes[0]  = (Byte) (((Version & 0x0F) << 4) | ((HeaderLength / 4) & 0x0F));

            packetBytes[1]  = TypeOfService;

            // Total Length – Big-Endian (Network Order)
            Buffer.BlockCopy(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((Int16)TotalLength)), 0, packetBytes, 2, 2);

            // Identification – Big-Endian
            Buffer.BlockCopy(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((Int16)Identification)), 0, packetBytes, 4, 2);

            // Flags (3 Bit) + Fragment Offset (13 Bit) – korrekt gepackt
            UInt16 flagsAndOffset = (UInt16)(((Flags & 0x07) << 13) | (FragmentOffset & 0x1FFF));
            Buffer.BlockCopy(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((Int16)flagsAndOffset)), 0, packetBytes, 6, 2);

            packetBytes[ 8] = TimeToLive;
            packetBytes[ 9] = (Byte)Protocol;

            // Set header checksum to 0 for checksum calculation
            packetBytes[10] = 0;
            packetBytes[11] = 0;

            // Source Address (already in network order!)
            Buffer.BlockCopy(SourceAddress.     GetBytes(), 0, packetBytes, 12, 4);

            // Destination Address
            Buffer.BlockCopy(DestinationAddress.GetBytes(), 0, packetBytes, 16, 4);

            // The header length was already adjusted in the constructor!
            if (Options.Length > 0 && HeaderLength > 20)
            {
                Buffer.BlockCopy(
                    Options,
                    0,
                    packetBytes,
                    20,
                    Math.Min(
                        Options.Length,
                        HeaderLength - 20
                    )
                );
            }

            if (Payload.Length > 0)
            {
                Buffer.BlockCopy(
                    Payload,
                    0,
                    packetBytes,
                    HeaderLength,
                    Payload.Length
                );
            }

            // Calculate checksum over the entire header
            Buffer.BlockCopy(
                BitConverter.GetBytes(
                    System.Net.IPAddress.HostToNetworkOrder(
                        (Int16) GetChecksum(packetBytes, 0, HeaderLength)
                    )
                ),
                0,
                packetBytes,
                10,
                2
            );

            return packetBytes;

        }


        /// <summary>
        /// Calculates the IPv4 header checksum (Internet Checksum) according to RFC 791 / RFC 1071.
        /// The checksum is calculated over the specified header area (checksum field must be set to 0 beforehand).
        /// </summary>
        /// <param name="IPv4Header">A byte array containing the IPv4 header.</param>
        /// <param name="Start">The starting index of the header in the byte array (usually 0).</param>
        /// <param name="Length">The length of the header in bytes (must be a multiple of 4, typically 20 for a header without options).</param>
        /// <returns>The calculated 16-bit checksum (One's Complement).</returns>
        public static UInt16 GetChecksum(Byte[]  IPv4Header,
                                         Int32   Start,
                                         Int32   Length)
        {

            if (IPv4Header is null || IPv4Header.Length < 20 || Length <= 0)
                return 0;

            UInt32 checksum = 0;
            int i = Start;
            int end = Start + Length;

            // 16-bit words are added in big-endian (network order) directly to the checksum
            while (i < end - 1)
            {
                checksum += (UInt32) ((IPv4Header[i] << 8) | IPv4Header[i + 1]);
                i += 2;
            }

            // If the length is odd (which should not happen for IPv4 headers, as they are always a multiple of 4),
            // treat the last byte as the high byte (low byte = 0x00)
            if (i < end)
                checksum += (UInt32) (IPv4Header[i] << 8);

            // Carry bits folded back into the checksum until no more carry exists (checksum fits in 16 bits)
            // (Standard One's Complement Addition)
            while ((checksum >> 16) != 0)
            {
                checksum = (checksum & 0xFFFFu) + (checksum >> 16);
            }

            return (UInt16) ~checksum;

        }

    }

}
