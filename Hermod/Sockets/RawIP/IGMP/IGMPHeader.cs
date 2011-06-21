using de.ahzf.Hermod.Datastructures;
using System;
class IgmpHeader : AProtocolHeader
{
    private byte igmpVersionType;
    private byte igmpMaxResponseTime;
    private ushort igmpChecksum;
    private IPv4Address igmpGroupAddress;

    static public int IgmpHeaderLength = 8;

    // IGMP message types v1
    static public byte IgmpMembershipQuery = 0x11;
    static public byte IgmpMembershipReport = 0x12;
    // IGMP message types v2
    static public byte IgmpMembershipReportV2 = 0x16;
    static public byte IgmpLeaveGroup = 0x17;

    // IGMP queries and responses are send to the all systems address
    static public IPv4Address AllSystemsAddress = IPv4Address.Parse("224.0.0.1");

    /// <summary>
    /// Simple constructor for the IGMP header that initializes the member fields.
    /// </summary>
    public IgmpHeader()
        : base()
    {
        igmpVersionType = IgmpMembershipQuery;
        igmpMaxResponseTime = 0;
        igmpChecksum = 0;
        igmpGroupAddress = IPv4Address.Any;
    }

    /// <summary>
    /// Sets both the version and type codes. Since the version and type codes
    /// are tied together there is only one property which sets both values.
    /// </summary>
    public byte VersionType
    {
        get
        {
            return igmpVersionType;
        }
        set
        {
            igmpVersionType = value;
        }
    }

    /// <summary>
    /// The maximum response time for the IGMP query.
    /// </summary>
    public byte MaximumResponseTime
    {
        get
        {
            return igmpMaxResponseTime;
        }
        set
        {
            igmpMaxResponseTime = value;
        }
    }

    /// <summary>
    /// The multicast group address for the IGMP message.
    /// </summary>
    public IPv4Address GroupAddress
    {
        get
        {
            return igmpGroupAddress;
        }
        set
        {
            igmpGroupAddress = value;
        }
    }

    /// <summary>
    /// Checksum value for the IGMP packet and payload.
    /// </summary>
    public ushort Checksum
    {
        get
        {
            return (ushort)IPAddressFactory.NetworkToHostOrder((short)igmpChecksum);
        }
        set
        {
            igmpChecksum = (ushort)IPAddressFactory.HostToNetworkOrder((short)value);
        }
    }

    /// <summary>
    /// This routine creates an instance of the IgmpHeader class from a byte
    /// array that is a received IGMP packet. This is useful when a packet
    /// is received from the network and the header object needs to be
    /// constructed from those values.
    /// </summary>
    /// <param name="igmpPacket">Byte array containing the binary IGMP header</param>
    /// <param name="bytesCopied">Number of bytes used in header</param>
    /// <returns>Returns the IgmpHeader object created from the byte array</returns>
    static public IgmpHeader Create(byte[] igmpPacket, ref int bytesCopied)
    {
        IgmpHeader igmpHeader = new IgmpHeader();
        int offset = 0;

        // Verify byte array is large enough to contain IGMP header
        if (igmpPacket.Length < IgmpHeader.IgmpHeaderLength)
            return null;

        igmpHeader.igmpVersionType = igmpPacket[offset++];
        igmpHeader.igmpMaxResponseTime = igmpPacket[offset++];
        igmpHeader.igmpChecksum = BitConverter.ToUInt16(igmpPacket, offset);

        bytesCopied = IgmpHeader.IgmpHeaderLength;
        return igmpHeader;
    }

    /// <summary>
    /// This routine creates the byte array representation of the IGMP packet as it
    /// would look on the wire.
    /// </summary>
    /// <param name="payLoad">Payload to copy after the IGMP header</param>
    /// <returns>Byte array representing the IGMP header and payload</returns>
    public override byte[] GetProtocolPacketBytes(byte[] payLoad)
    {
        byte[] igmpPacket, addressBytes, byteValue;
        int offset = 0;

        igmpPacket = new byte[IgmpHeaderLength + payLoad.Length];

        // Build the IGMP packet
        igmpPacket[offset++] = igmpVersionType;
        igmpPacket[offset++] = igmpMaxResponseTime;
        igmpPacket[offset++] = 0;  // Zero the checksum for now
        igmpPacket[offset++] = 0;

        // Copy the group address bytes
        addressBytes = igmpGroupAddress.GetBytes();
        Array.Copy(addressBytes, 0, igmpPacket, offset, addressBytes.Length);
        offset += addressBytes.Length;

        // Copy the payload if specified. Normally, there is no payload to the IGMP
        //    packet -- only the IGMP header.
        if (payLoad.Length > 0)
        {
            Array.Copy(payLoad, 0, igmpPacket, offset, payLoad.Length);
            offset += payLoad.Length;
        }

        // Compute the checksum on the IGMP packet and payload
        Checksum = ComputeChecksum(igmpPacket);

        // Put the checksum value into the packet
        byteValue = BitConverter.GetBytes(igmpChecksum);
        Array.Copy(byteValue, 0, igmpPacket, 2, byteValue.Length);

        return igmpPacket;
    }
}
