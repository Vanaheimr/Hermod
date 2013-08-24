/// <summary>
/// Class representing the ICMPv6 echo request header. Since the ICMPv6 protocol is
/// used for a variety of different functions other than "ping", this header is
/// broken out from the base ICMPv6 header that is common across all of its functions
/// (such as Multicast Listener Discovery, Neighbor Discovery, etc.).
/// </summary>

using eu.Vanaheimr.Hermod;
using System;
class Icmpv6EchoRequest : AProtocolHeader
{
    private ushort echoId;
    private ushort echoSequence;

    static public int Icmpv6EchoRequestLength = 4;

    /// <summary>
    /// Simple constructor for the ICMPv6 echo request header
    /// </summary>
    public Icmpv6EchoRequest()
        : base()
    {
        echoId = 0;
        echoSequence = 0;
    }

    /// <summary>
    /// Gets and sets the ID field. Also performs the necessary byte order conversion.
    /// </summary>
    public ushort Id
    {
        get
        {
            return (ushort) NetworkingHelpers.NetworkToHostOrder((short)echoId);
        }
        set
        {
            echoId = (ushort) NetworkingHelpers.HostToNetworkOrder((short)value);
        }
    }

    /// <summary>
    /// Gets and sets the echo sequence field. Also performs the necessary byte order conversion.
    /// </summary>
    public ushort Sequence
    {
        get
        {
            return (ushort) NetworkingHelpers.NetworkToHostOrder((short)echoSequence);
        }
        set
        {
            echoSequence = (ushort) NetworkingHelpers.HostToNetworkOrder((short)value);
        }
    }

    /// <summary>
    /// This routine creates an instance of the Icmpv6EchoRequest class from a byte
    /// array that is a received IGMP packet. This is useful when a packet
    /// is received from the network and the header object needs to be
    /// constructed from those values.
    /// </summary>
    /// <param name="echoData">Byte array containing the binary ICMPv6 echo request header</param>
    /// <param name="bytesCopied">Number of bytes used in header</param>
    /// <returns>Returns the Icmpv6EchoRequest object created from the byte array</returns>
    static public Icmpv6EchoRequest Create(byte[] echoData, ref int bytesCopied)
    {
        Icmpv6EchoRequest icmpv6EchoRequestHeader = new Icmpv6EchoRequest();

        // Verify buffer is large enough
        if (echoData.Length < Icmpv6EchoRequest.Icmpv6EchoRequestLength)
            return null;

        // Properties are stored in network byte order so just grab the bytes
        //    from the buffer
        icmpv6EchoRequestHeader.echoId = BitConverter.ToUInt16(echoData, 0);
        icmpv6EchoRequestHeader.echoSequence = BitConverter.ToUInt16(echoData, 2);
        bytesCopied = Icmpv6EchoRequest.Icmpv6EchoRequestLength;
        return icmpv6EchoRequestHeader;
    }

    /// <summary>
    /// This method builds the byte array representation of the ICMPv6 echo request header
    /// as it would appear on the wire.
    /// </summary>
    /// <param name="payLoad">Payload to appear after the ICMPv6 echo request header</param>
    /// <returns>Returns the byte array representing the packet and payload</returns>
    public override byte[] GetProtocolPacketBytes(byte[] payLoad)
    {
        byte[] icmpv6EchoRequestHeader = new byte[Icmpv6EchoRequestLength + payLoad.Length], byteValue;
        int offset = 0;

        byteValue = BitConverter.GetBytes(echoId);
        Array.Copy(byteValue, 0, icmpv6EchoRequestHeader, offset, byteValue.Length);
        offset += byteValue.Length;
        byteValue = BitConverter.GetBytes(echoSequence);
        Array.Copy(byteValue, 0, icmpv6EchoRequestHeader, offset, byteValue.Length);
        offset += byteValue.Length;
        Array.Copy(payLoad, 0, icmpv6EchoRequestHeader, offset, payLoad.Length);

        return icmpv6EchoRequestHeader;
    }
}
