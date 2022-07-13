using org.GraphDefined.Vanaheimr.Hermod;

using System;
public class Ipv6Header : AProtocolHeader
{
    private byte ipVersion;
    private byte ipTrafficClass;
    private uint ipFlow;
    private ushort ipPayloadLength;
    private byte ipNextHeader;
    private byte ipHopLimit;
    private IPv6Address ipSourceAddress;
    private IPv6Address ipDestinationAddress;

    static public int Ipv6HeaderLength = 40;

    /// <summary>
    /// Simple constructor for the IPv6 header that initializes the fields to zero.
    /// </summary>
    public Ipv6Header()
        : base()
    {
        ipVersion = 6;
        ipTrafficClass = 0;
        ipFlow = 0;
        ipPayloadLength = 0;
        ipNextHeader = 0;
        ipHopLimit = 32;
        ipSourceAddress = IPv6Address.Any;
        ipDestinationAddress = IPv6Address.Any;
    }

    /// <summary>
    /// Gets and sets the IP version. This value should be 6.
    /// </summary>
    public byte Version
    {
        get
        {
            return ipVersion;
        }
        set
        {
            ipVersion = value;
        }
    }

    /// <summary>
    /// Gets and sets the traffic class for the header.
    /// </summary>
    public byte TrafficClass
    {
        get
        {
            return ipTrafficClass;
        }
        set
        {
            ipTrafficClass = value;
        }
    }

    /// <summary>
    /// Gets and sets the flow value for the packet. Byte order conversion
    /// is required for this field.
    /// </summary>
    public uint Flow
    {
        get
        {
            return (uint)NetworkingHelpers.NetworkToHostOrder((int)ipFlow);
        }
        set
        {
            ipFlow = (uint)NetworkingHelpers.HostToNetworkOrder((int)value);
        }
    }

    /// <summary>
    /// Gets and sets the payload length for the IPv6 packet. Note for IPv6, the
    /// payload length counts only the payload and not the IPv6 header (since
    /// the IPv6 header is a fixed length). Byte order conversion is required.
    /// </summary>
    public ushort PayloadLength
    {
        get
        {
            return (ushort)NetworkingHelpers.NetworkToHostOrder((short)ipPayloadLength);
        }
        set
        {
            ipPayloadLength = (ushort)NetworkingHelpers.HostToNetworkOrder((short)value);
        }
    }

    /// <summary>
    /// The protocol value of the header encapsulated by the IPv6 header.
    /// </summary>
    public byte NextHeader
    {
        get
        {
            return ipNextHeader;
        }
        set
        {
            ipNextHeader = value;
        }
    }

    /// <summary>
    /// Time-to-live (TTL) of the IPv6 header.
    /// </summary>
    public byte HopLimit
    {
        get
        {
            return ipHopLimit;
        }
        set
        {
            ipHopLimit = value;
        }
    }

    /// <summary>
    /// IPv6 source address in the IPv6 header.
    /// </summary>
    public IPv6Address SourceAddress
    {
        get
        {
            return ipSourceAddress;
        }
        set
        {
            ipSourceAddress = value;
        }
    }

    /// <summary>
    /// IPv6 destination address in the IPv6 header.
    /// </summary>
    public IPv6Address DestinationAddress
    {
        get
        {
            return ipDestinationAddress;
        }
        set
        {
            ipDestinationAddress = value;
        }
    }

    /// <summary>
    /// This routine creates an instance of the Ipv6Header class from a byte
    /// array that is a received IGMP packet. This is useful when a packet
    /// is received from the network and the header object needs to be
    /// constructed from those values.
    /// </summary>
    /// <param name="ipv6Packet">Byte array containing the binary IPv6 header</param>
    /// <param name="bytesCopied">Number of bytes used in header</param>
    /// <returns>Returns the Ipv6Header object created from the byte array</returns>
    static public Ipv6Header Create(byte[] ipv6Packet, ref int bytesCopied)
    {
        Ipv6Header ipv6Header = new Ipv6Header();
        byte[] addressBytes = new byte[16];
        uint tempVal = 0, tempVal2 = 0;

        // Ensure byte array is large enough to contain an IPv6 header
        if (ipv6Packet.Length < Ipv6Header.Ipv6HeaderLength)
            return null;

        tempVal = ipv6Packet[0];
        tempVal = (tempVal >> 4) & 0xF;
        ipv6Header.ipVersion = (byte)tempVal;

        tempVal = ipv6Packet[0];
        tempVal = (tempVal & 0xF) >> 4;
        ipv6Header.ipTrafficClass = (byte)(tempVal | (uint)((ipv6Packet[1] >> 4) & 0xF));

        tempVal2 = ipv6Packet[1];
        tempVal2 = (tempVal2 & 0xF) << 16;
        tempVal = ipv6Packet[2];
        tempVal = tempVal << 8;
        ipv6Header.ipFlow = tempVal2 | tempVal | ipv6Packet[3];

        ipv6Header.ipNextHeader = ipv6Packet[4];
        ipv6Header.ipHopLimit = ipv6Packet[5];

        Array.Copy(ipv6Packet, 6, addressBytes, 0, 16);
        ipv6Header.SourceAddress = new IPv6Address(addressBytes);

        Array.Copy(ipv6Packet, 24, addressBytes, 0, 16);
        ipv6Header.DestinationAddress = new IPv6Address(addressBytes);

        bytesCopied = Ipv6Header.Ipv6HeaderLength;

        return ipv6Header;
    }

    /// <summary>
    /// Packages up the IPv6 header and the given payload into a byte array
    /// suitable for sending on a socket.
    /// </summary>
    /// <param name="payLoad">Data encapsulated by the IPv6 header</param>
    /// <returns>Byte array of the IPv6 packet and payload</returns>
    public override byte[] GetProtocolPacketBytes(byte[] payLoad)
    {
        byte[] byteValue, ipv6Packet;
        int offset = 0;

        ipv6Packet = new byte[Ipv6HeaderLength + payLoad.Length];
        ipv6Packet[offset++] = (byte)((ipVersion << 4) | ((ipTrafficClass >> 4) & 0xF));

        // tmpbyte1 = (byte) ( ( ipTrafficClass << 4) & 0xF0);
        // tmpbyte2 = (byte) ( ( ipFlow >> 16 ) & 0xF );

        ipv6Packet[offset++] = (byte)((uint)((ipTrafficClass << 4) & 0xF0) | (uint)((Flow >> 16) & 0xF));
        ipv6Packet[offset++] = (byte)((Flow >> 8) & 0xFF);
        ipv6Packet[offset++] = (byte)(Flow & 0xFF);

        Console.WriteLine("Next header = {0}", ipNextHeader);

        byteValue = BitConverter.GetBytes(ipPayloadLength);
        Array.Copy(byteValue, 0, ipv6Packet, offset, byteValue.Length);
        offset += byteValue.Length;

        ipv6Packet[offset++] = (byte)ipNextHeader;
        ipv6Packet[offset++] = (byte)ipHopLimit;

        byteValue = ipSourceAddress.GetBytes();
        Array.Copy(byteValue, 0, ipv6Packet, offset, byteValue.Length);
        offset += byteValue.Length;

        byteValue = ipDestinationAddress.GetBytes();
        Array.Copy(byteValue, 0, ipv6Packet, offset, byteValue.Length);
        offset += byteValue.Length;

        Array.Copy(payLoad, 0, ipv6Packet, offset, payLoad.Length);

        return ipv6Packet;
    }
}
