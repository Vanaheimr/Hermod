using System;
using de.ahzf.Hermod.Datastructures;

class Icmpv6Header : AProtocolHeader
{

    private byte icmpType;
    private byte icmpCode;
    private ushort icmpChecksum;

    public Ipv6Header ipv6Header;

    // Common values for the ICMPv6 type and code fields
    static public byte Icmpv6EchoRequestType = 128;      // ICMPv6 echo request type
    static public byte Icmpv6EchoRequestCode = 0;         // ICMPv6 echo request code
    static public byte Icmpv6EchoReplyType = 129;          // ICMPv6 echo reply type
    static public byte Icmpv6EchoReplyCode = 0;             // ICMPv6 echo reply code
    static public int Icmpv6HeaderLength = 4;                    // ICMPv6 header length

    /// <summary>
    /// Simple constructor for the ICMPv6 protocol header.
    /// </summary>
    public Icmpv6Header()
        : base()
    {
        icmpType = 0;
        icmpCode = 0;
        icmpChecksum = 0;
    }

    /// <summary>
    /// Constructor for the ICMPv6 header which also takes a reference to the
    /// encompassing IPv6 header. This is necessary since the IPv6 protocol
    /// defines a pseudo header checksum which requires the checksum to be
    /// calculated over fields in the ICMPv6 header and payload as well as
    /// fields from the IPv6 packet.
    /// </summary>
    /// <param name="packetHeader">Reference to the Ipv6Header object encompassing the ICMPv6 packet</param>
    public Icmpv6Header(Ipv6Header packetHeader)
        : base()
    {
        icmpType = 0;
        icmpCode = 0;
        icmpChecksum = 0;
        ipv6Header = packetHeader;
    }

    /// <summary>
    /// Sets the ICMPv6 message type.
    /// </summary>
    public byte Type
    {
        get
        {
            return icmpType;
        }
        set
        {
            icmpType = value;
        }
    }

    /// <summary>
    /// Sets the ICMPv6 code type.
    /// </summary>
    public byte Code
    {
        get
        {
            return icmpCode;
        }
        set
        {
            icmpCode = value;
        }
    }

    /// <summary>
    /// The ICMPv6 checksum value. This value is computed over the ICMPv6 header, payload,
    /// and the IPv6 header as well.
    /// </summary>
    public ushort Checksum
    {
        get
        {
            return (ushort)NetworkingHelpers.NetworkToHostOrder((short)icmpChecksum);
        }
        set
        {
            icmpChecksum = (ushort)NetworkingHelpers.HostToNetworkOrder((short)value);
        }
    }

    /// <summary>
    /// This routine creates an instance of the Icmpv6Header class from a byte
    /// array that is a received IGMP packet. This is useful when a packet
    /// is received from the network and the header object needs to be
    /// constructed from those values.
    /// </summary>
    /// <param name="icmpv6Packet">Byte array containing the binary ICMPv6 header</param>
    /// <param name="bytesCopied">Number of bytes used in header</param>
    /// <returns>Returns the Icmpv6Header object created from the byte array</returns>
    static public Icmpv6Header Create(byte[] icmpv6Packet, ref int bytesCopied)
    {
        Icmpv6Header icmpv6Header = new Icmpv6Header();
        int offset = 0;

        // Verify buffer is large enough to contain an ICMPv6 header
        if (icmpv6Packet.Length < Icmpv6Header.Icmpv6HeaderLength)
            return null;

        icmpv6Header.icmpType = icmpv6Packet[offset++];
        icmpv6Header.icmpCode = icmpv6Packet[offset++];
        icmpv6Header.icmpChecksum = BitConverter.ToUInt16(icmpv6Packet, offset);
        bytesCopied = Icmpv6Header.Icmpv6HeaderLength;
        return icmpv6Header;
    }

    /// <summary>
    /// This routine builds the ICMPv6 packet and payload into a byte array.
    /// It also computes the IPv6 pseudo header checksum that appears in the
    /// ICMPv6 packet.
    /// </summary>
    /// <param name="payLoad">A byte array representing the ICMPv6 payload</param>
    /// <returns>A byte array of the ICMPv6 packet and payload</returns>
    public override byte[] GetProtocolPacketBytes(byte[] payLoad)
    {
        byte[] icmpv6Packet, pseudoHeader, byteValue;
        int offset = 0, payLoadLength;

        // Build the ICMPv6 packet first since its required in the pseudo header calculation
        icmpv6Packet = new byte[Icmpv6HeaderLength + payLoad.Length];

        offset = 0;
        icmpv6Packet[offset++] = icmpType;
        icmpv6Packet[offset++] = icmpCode;
        icmpv6Packet[offset++] = 0;
        icmpv6Packet[offset++] = 0;

        // Copy the payload into the build ICMPv6 packet
        Array.Copy(payLoad, 0, icmpv6Packet, offset, payLoad.Length);

        // Now build the pseudo header
        pseudoHeader = new byte[40 + icmpv6Packet.Length];

        offset = 0;

        byteValue = ipv6Header.SourceAddress.GetBytes();
        Array.Copy(byteValue, 0, pseudoHeader, offset, byteValue.Length);
        offset += byteValue.Length;

        byteValue = ipv6Header.DestinationAddress.GetBytes();
        Array.Copy(byteValue, 0, pseudoHeader, offset, byteValue.Length);
        offset += byteValue.Length;

        // Packet total length
        payLoadLength = NetworkingHelpers.HostToNetworkOrder(Icmpv6HeaderLength + payLoad.Length);

        byteValue = BitConverter.GetBytes(payLoadLength);
        Array.Copy(byteValue, 0, pseudoHeader, offset, byteValue.Length);
        offset += byteValue.Length;

        // 3 bytes of zero padding
        pseudoHeader[offset++] = (byte)0;
        pseudoHeader[offset++] = (byte)0;
        pseudoHeader[offset++] = (byte)0;
        pseudoHeader[offset++] = (byte)ipv6Header.NextHeader;

        // Next is the icmpv6 header and its payload
        Array.Copy(icmpv6Packet, 0, pseudoHeader, offset, icmpv6Packet.Length);
        offset += icmpv6Packet.Length;

        // Compute checksum on pseudo header
        Checksum = ComputeChecksum(pseudoHeader);

        // Go back and put the checksum value into the marshalled byte array
        byteValue = BitConverter.GetBytes(icmpChecksum);
        Array.Copy(byteValue, 0, icmpv6Packet, 2, byteValue.Length);
        return icmpv6Packet;

    }

}
