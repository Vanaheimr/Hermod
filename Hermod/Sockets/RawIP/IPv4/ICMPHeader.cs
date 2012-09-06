using System;
using de.ahzf.Vanaheimr.Hermod.Datastructures;
public class IcmpHeader : AProtocolHeader
{
    private byte icmpType;                   // ICMP message type
    private byte icmpCode;                  // ICMP message code
    private ushort icmpChecksum;      // Checksum of ICMP header and payload
    private ushort icmpId;                     // Message ID
    private ushort icmpSequence;      // ICMP sequence number

    static public byte EchoRequestType = 8;     // ICMP echo request
    static public byte EchoRequestCode = 0;    // ICMP echo request code
    static public byte EchoReplyType = 0;     // ICMP echo reply
    static public byte EchoReplyCode = 0;    // ICMP echo reply code

    static public int IcmpHeaderLength = 8;    // Length of ICMP header

    /// <summary>
    /// Default constructor for ICMP packet
    /// </summary>
    public IcmpHeader()
        : base()
    {
        icmpType = 0;
        icmpCode = 0;
        icmpChecksum = 0;
        icmpId = 0;
        icmpSequence = 0;
    }

    /// <summary>
    /// ICMP message type.
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
    /// ICMP message code.
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
    /// Checksum of ICMP packet and payload.  Performs the necessary byte order conversion.
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
    /// ICMP message ID. Used to uniquely identify the source of the ICMP packet.
    /// Performs the necessary byte order conversion.
    /// </summary>
    public ushort Id
    {
        get
        {
            return (ushort)NetworkingHelpers.NetworkToHostOrder((short)icmpId);
        }
        set
        {
            icmpId = (ushort)NetworkingHelpers.HostToNetworkOrder((short)value);
        }
    }

    /// <summary>
    /// ICMP sequence number. As each ICMP message is sent the sequence should be incremented.
    /// Performs the necessary byte order conversion.
    /// </summary>
    public ushort Sequence
    {
        get
        {
            return (ushort)NetworkingHelpers.NetworkToHostOrder((short)icmpSequence);
        }
        set
        {
            icmpSequence = (ushort)NetworkingHelpers.HostToNetworkOrder((short)value);
        }
    }

    /// <summary>
    /// This routine creates an instance of the IcmpHeader class from a byte
    /// array that is a received IGMP packet. This is useful when a packet
    /// is received from the network and the header object needs to be
    /// constructed from those values.
    /// </summary>
    /// <param name="icmpPacket">Byte array containing the binary ICMP header</param>
    /// <param name="bytesCopied">Number of bytes used in header</param>
    /// <returns>Returns the IcmpHeader object created from the byte array</returns>
    static public IcmpHeader Create(byte[] icmpPacket, ref int bytesCopied)
    {
        IcmpHeader icmpHeader = new IcmpHeader();
        int offset = 0;

        // Make sure byte array is large enough to contain an ICMP header
        if (icmpPacket.Length < IcmpHeader.IcmpHeaderLength)
            return null;

        icmpHeader.icmpType = icmpPacket[offset++];
        icmpHeader.icmpCode = icmpPacket[offset++];
        icmpHeader.icmpChecksum = BitConverter.ToUInt16(icmpPacket, offset);
        offset += 2;
        icmpHeader.icmpId = BitConverter.ToUInt16(icmpPacket, offset);
        offset += 2;
        icmpHeader.icmpSequence = BitConverter.ToUInt16(icmpPacket, offset);
        bytesCopied = IcmpHeader.IcmpHeaderLength;
        return icmpHeader;
    }

    /// <summary>
    /// This routine builds the ICMP packet suitable for sending on a raw socket.
    /// It builds the ICMP packet and payload into a byte array and computes
    /// the checksum.
    /// </summary>
    /// <param name="payLoad">Data payload of the ICMP packet</param>
    /// <returns>Byte array representing the ICMP packet and payload</returns>
    public override byte[] GetProtocolPacketBytes(byte[] payLoad)
    {
        byte[] icmpPacket, byteValue;
        int offset = 0;

        icmpPacket = new byte[IcmpHeaderLength + payLoad.Length];
        icmpPacket[offset++] = icmpType;
        icmpPacket[offset++] = icmpCode;
        icmpPacket[offset++] = 0;          // Zero out the checksum until the packet is assembled
        icmpPacket[offset++] = 0;

        byteValue = BitConverter.GetBytes(icmpId);
        Array.Copy(byteValue, 0, icmpPacket, offset, byteValue.Length);
        offset += byteValue.Length;

        byteValue = BitConverter.GetBytes(icmpSequence);
        Array.Copy(byteValue, 0, icmpPacket, offset, byteValue.Length);
        offset += byteValue.Length;

        if (payLoad.Length > 0)
        {
            Array.Copy(payLoad, 0, icmpPacket, offset, payLoad.Length);
            offset += payLoad.Length;
        }

        // Compute the checksum over the entire packet
        Checksum = ComputeChecksum(icmpPacket);

        // Put the checksum back into the packet
        byteValue = BitConverter.GetBytes(icmpChecksum);
        Array.Copy(byteValue, 0, icmpPacket, 2, byteValue.Length);
        return icmpPacket;
    }
}
