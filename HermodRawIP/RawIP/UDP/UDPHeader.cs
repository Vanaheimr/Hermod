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

using eu.Vanaheimr.Hermod;
using System;

#endregion

public class UdpHeader : AProtocolHeader
{

    private ushort srcPort;
    private ushort destPort;
    private ushort udpLength;
    private ushort udpChecksum;

    public Ipv6Header ipv6PacketHeader;
    public IPv4Header_old ipv4PacketHeader;

    static public int UdpHeaderLength = 8;

    /// <summary>
    /// Simple constructor for the UDP header.
    /// </summary>
    public UdpHeader()
        : base()
    {
        srcPort = 0;
        destPort = 0;
        udpLength = 0;
        udpChecksum = 0;

        ipv6PacketHeader = null;
        ipv4PacketHeader = null;
    }

    /// <summary>
    /// Gets and sets the destination port. Performs the necessary byte order conversion.
    /// </summary>
    public ushort SourcePort
    {
        get
        {
            return (ushort) NetworkingHelpers.NetworkToHostOrder((short)srcPort);
        }
        set
        {
            srcPort = (ushort) NetworkingHelpers.HostToNetworkOrder((short)value);
        }
    }

    /// <summary>
    /// Gets and sets the destination port. Performs the necessary byte order conversion.
    /// </summary>
    public ushort DestinationPort
    {
        get
        {
            return (ushort)NetworkingHelpers.NetworkToHostOrder((short)destPort);
        }
        set
        {
            destPort = (ushort)NetworkingHelpers.HostToNetworkOrder((short)value);
        }
    }

    /// <summary>
    /// Gets and sets the UDP payload length. This is the length of the payload
    /// plus the size of the UDP header itself.
    /// </summary>
    public ushort Length
    {
        get
        {
            return (ushort)NetworkingHelpers.NetworkToHostOrder((short)udpLength);
        }
        set
        {
            udpLength = (ushort)NetworkingHelpers.HostToNetworkOrder((short)value);
        }
    }

    /// <summary>
    /// Gets and sets the checksum value. It performs the necessary byte order conversion.
    /// </summary>
    public ushort Checksum
    {
        get
        {
            return (ushort)NetworkingHelpers.NetworkToHostOrder((short)udpChecksum);
        }
        set
        {
            udpChecksum = (ushort)NetworkingHelpers.HostToNetworkOrder((short)value);
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="udpData"></param>
    /// <param name="bytesCopied"></param>
    /// <returns></returns>
    static public UdpHeader Create(byte[] udpData, ref int bytesCopied)
    {
        UdpHeader udpPacketHeader = new UdpHeader();

        udpPacketHeader.srcPort = BitConverter.ToUInt16(udpData, 0);
        udpPacketHeader.destPort = BitConverter.ToUInt16(udpData, 2);
        udpPacketHeader.udpLength = BitConverter.ToUInt16(udpData, 4);
        udpPacketHeader.udpChecksum = BitConverter.ToUInt16(udpData, 6);

        return udpPacketHeader;
    }

    /// <summary>
    /// This method builds the byte array representation of the UDP header as it would appear
    /// on the wire. To do this it must build the IPv4 or IPv6 pseudo header in order to
    /// calculate the checksum on the packet. This requires knowledge of the IPv4 or IPv6 header
    /// so one of these must be set before a UDP packet can be set.
    ///
    /// The IPv4 pseudo header consists of:
    ///   4-byte source IP address
    ///   4-byte destination address
    ///   1-byte zero field
    ///   1-byte protocol field
    ///   2-byte UDP length
    ///   2-byte source port
    ///   2-byte destination port
    ///   2-byte UDP packet length
    ///   2-byte UDP checksum (zero)
    ///   UDP payload (padded to the next 16-bit boundary)

    /// The IPv6 pseudo header consists of:
    ///   16-byte source address
    ///   16-byte destination address
    ///   4-byte payload length
    ///   3-byte zero pad
    ///   1-byte protocol value
    ///   2-byte source port
    ///   2-byte destination port
    ///   2-byte UDP length
    ///   2-byte UDP checksum (zero)
    ///   UDP payload (padded to the next 16-bit boundary)
    /// </summary>
    /// <param name="payLoad">Payload that follows the UDP header</param>
    /// <returns></returns>
    public override byte[] GetProtocolPacketBytes(byte[] payLoad)
    {
        byte[] udpPacket = new byte[UdpHeaderLength + payLoad.Length], pseudoHeader = null, byteValue = null;
        int offset = 0;

        // Build the UDP packet first
        byteValue = BitConverter.GetBytes(srcPort);
        Array.Copy(byteValue, 0, udpPacket, offset, byteValue.Length);
        offset += byteValue.Length;

        byteValue = BitConverter.GetBytes(destPort);
        Array.Copy(byteValue, 0, udpPacket, offset, byteValue.Length);
        offset += byteValue.Length;

        byteValue = BitConverter.GetBytes(udpLength);
        Array.Copy(byteValue, 0, udpPacket, offset, byteValue.Length);
        offset += byteValue.Length;

        udpPacket[offset++] = 0;      // Checksum is initially zero
        udpPacket[offset++] = 0;

        // Copy payload to end of packet
        Array.Copy(payLoad, 0, udpPacket, offset, payLoad.Length);

        if (ipv4PacketHeader != null)
        {
            pseudoHeader = new byte[UdpHeaderLength + 12 + payLoad.Length];

            // Build the IPv4 pseudo header
            offset = 0;

            // Source address
            byteValue = ipv4PacketHeader.SourceAddress.GetBytes();
            Array.Copy(byteValue, 0, pseudoHeader, offset, byteValue.Length);
            offset += byteValue.Length;

            // Destination address
            byteValue = ipv4PacketHeader.DestinationAddress.GetBytes();
            Array.Copy(byteValue, 0, pseudoHeader, offset, byteValue.Length);
            offset += byteValue.Length;

            // 1 byte zero pad plus next header protocol value
            pseudoHeader[offset++] = 0;
            pseudoHeader[offset++] = ipv4PacketHeader.Protocol;

            // Packet length
            byteValue = BitConverter.GetBytes(udpLength);
            Array.Copy(byteValue, 0, pseudoHeader, offset, byteValue.Length);
            offset += byteValue.Length;

            // Copy the UDP packet to the end of this
            Array.Copy(udpPacket, 0, pseudoHeader, offset, udpPacket.Length);
        }

        else if (ipv6PacketHeader != null)
        {
            uint ipv6PayloadLength;

            pseudoHeader = new byte[UdpHeaderLength + 40 + payLoad.Length];

            // Build the IPv6 pseudo header
            offset = 0;

            // Source address
            byteValue = ipv6PacketHeader.SourceAddress.GetBytes();
            Array.Copy(byteValue, 0, pseudoHeader, offset, byteValue.Length);
            offset += byteValue.Length;

            // Destination address
            byteValue = ipv6PacketHeader.DestinationAddress.GetBytes();
            Array.Copy(byteValue, 0, pseudoHeader, offset, byteValue.Length);
            offset += byteValue.Length;

            ipv6PayloadLength = (uint)NetworkingHelpers.HostToNetworkOrder((int)(payLoad.Length + UdpHeaderLength));

            // Packet payload: ICMPv6 headers plus payload
            byteValue = BitConverter.GetBytes(ipv6PayloadLength);
            Array.Copy(byteValue, 0, pseudoHeader, offset, byteValue.Length);
            offset += byteValue.Length;

            // 3 bytes zero pad plus next header protocol value
            pseudoHeader[offset++] = 0;
            pseudoHeader[offset++] = 0;
            pseudoHeader[offset++] = 0;
            pseudoHeader[offset++] = ipv6PacketHeader.NextHeader;

            // Copy the UDP packet to the end of this
            Array.Copy(udpPacket, 0, pseudoHeader, offset, udpPacket.Length);
        }

        if (pseudoHeader != null)
        {
            Checksum = ComputeChecksum(pseudoHeader);
        }

        // Put checksum back into packet
        byteValue = BitConverter.GetBytes(udpChecksum);
        Array.Copy(byteValue, 0, udpPacket, 6, byteValue.Length);

        return udpPacket;

    }

}
