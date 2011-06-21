using System.Collections;
using System;
public abstract class AProtocolHeader
    {
        /// <summary>
        /// This abstracted method returns a byte array that is the protocol
        /// header and the payload. This is used by the BuildPacket method
        /// to build the entire packet which may consist of multiple headers
        /// and data payload.
        /// </summary>
        /// <param name="payLoad">The byte array of the data encapsulated in this header</param>
        /// <returns>A byte array of the serialized header and payload</returns>
        abstract public byte[ ] GetProtocolPacketBytes(byte[ ] payLoad);
 
        /// <summary>
        /// This method builds the entire packet to be sent on the socket. It takes
        /// an ArrayList of all encapsulated headers as well as the payload. The
        /// ArrayList of headers starts with the outermost header towards the
        /// innermost. For example when sending an IPv4/UDP packet, the first entry
        /// would be the IPv4 header followed by the UDP header. The byte payload of
        /// the UDP packet is passed as the second parameter.
        /// </summary>
        /// <param name="headerList">An array list of all headers to build the packet from</param>
        /// <param name="payLoad">Data payload appearing after all the headers</param>
        /// <returns>Returns a byte array representing the entire packet</returns>
        public byte[ ] BuildPacket(ArrayList headerList, byte[ ] payLoad)
        {
            AProtocolHeader protocolHeader;
            byte[ ] newPayload = null;
 
            // Traverse the array in reverse order since the outer headers may need
            //    the inner headers and payload to compute checksums on.
            for (int i = headerList.Count - 1; i >= 0; i--)
            {
                protocolHeader = (AProtocolHeader)headerList[i];
                newPayload = protocolHeader.GetProtocolPacketBytes(payLoad);
 
                // The payLoad for the next iteration of the loop is now any
                //    encapsulated headers plus the original payload data.
                payLoad = newPayload;
            }
 
            return payLoad;
        }
 
        /// <summary>
        /// This is a simple method for computing the 16-bit one's complement
        /// checksum of a byte buffer. The byte buffer will be padded with
        /// a zero byte if an uneven number.
        /// </summary>
        /// <param name="payLoad">Byte array to compute checksum over</param>
        /// <returns></returns>
        static public ushort ComputeChecksum(byte[ ] payLoad)
        {
            uint xsum = 0;
            ushort shortval = 0, hiword = 0, loword = 0;
 
            // Sum up the 16-bits
            for (int i = 0; i < payLoad.Length / 2; i++)
            {
                hiword = (ushort)(((ushort)payLoad[i * 2]) << 8);
                loword = (ushort)payLoad[(i * 2) + 1];
                shortval = (ushort)(hiword | loword);
                xsum = xsum + (uint)shortval;
            }
            // Pad if necessary
            if ((payLoad.Length % 2) != 0)
            {
                xsum += (uint)payLoad[payLoad.Length - 1];
            }
 
            xsum = ((xsum >> 16) + (xsum & 0xFFFF));
            xsum = (xsum + (xsum >> 16));
            shortval = (ushort)(~xsum);
 
            return shortval;
        }
 
        /// <summary>
        /// Utility function for printing a byte array into a series of 4 byte hex digits with
        /// four such hex digits displayed per line.
        /// </summary>
        /// <param name="printBytes">Byte array to display</param>
        static public void PrintByteArray(byte[ ] printBytes)
        {
            int index = 0;
 
            while (index < printBytes.Length)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (index >= printBytes.Length)
                        break;
 
                    for (int j = 0; j < 4; j++)
                    {
                        if (index >= printBytes.Length)
                            break;
 
                        Console.Write("{0}", printBytes[index++].ToString("x2"));
                    }
                    Console.Write(" ");
                }
                Console.WriteLine("");
            }
        }
    }
