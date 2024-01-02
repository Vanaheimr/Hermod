/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com> <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Illias;
using System;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Sockets.RawIP.ICMP
{

    /// <summary>
    /// The ICMP Echo Request
    /// </summary>
    public class ICMPEchoRequest : IICMPMessage<ICMPEchoRequest>
    {

        #region Properties

        public UInt16                       Identifier        { get; }
        public UInt16                       SequenceNumber    { get; }
        public Byte[]                       Data              { get; }
        public ICMPPacket<ICMPEchoRequest>  ICMPPacket        { get; internal set; }

        public String Text
        {
            get
            {
                try
                {
                    return Data != null
                               ? System.Text.Encoding.UTF8.GetString(Data)
                               : "";
                }
                catch
                {
                    return "";
                }
            }
        }

        #endregion

        #region (private) ICMPEchoRequest(Identifier, SequenceNumber, Data, ICMPPacket = null)

        private ICMPEchoRequest(UInt16                       Identifier,
                                UInt16                       SequenceNumber,
                                Byte[]                       Data,
                                ICMPPacket<ICMPEchoRequest>  ICMPPacket = null)
        {

            this.Identifier      = Identifier;
            this.SequenceNumber  = SequenceNumber;
            this.Data            = Data;
            this.ICMPPacket      = ICMPPacket;

        }

        #endregion


        #region (static) Create(Identifier, SequenceNumber, Text, ICMPPacket = null)

        public static ICMPEchoRequest Create(UInt16                       Identifier,
                                             UInt16                       SequenceNumber,
                                             String                       Text,
                                             ICMPPacket<ICMPEchoRequest>  ICMPPacket = null)

            => Create(Identifier,
                      SequenceNumber,
                      System.Text.Encoding.UTF8.GetBytes(Text ?? ""),
                      ICMPPacket);

        #endregion

        #region (static) Create(Identifier, SequenceNumber, Data, ICMPPacket = null)

        public static ICMPEchoRequest Create(UInt16                       Identifier,
                                             UInt16                       SequenceNumber,
                                             Byte[]                       Data,
                                             ICMPPacket<ICMPEchoRequest>  ICMPPacket = null)
        {

            var echoRequest =  new ICMPEchoRequest(Identifier,
                                                   SequenceNumber,
                                                   Data,
                                                   ICMPPacket);

            if (ICMPPacket == null)
                echoRequest.ICMPPacket = new ICMPPacket<ICMPEchoRequest>(Type:      8,
                                                                         Code:      0,
                                                                         Checksum:  0,
                                                                         Payload:   echoRequest);

            else
                echoRequest.ICMPPacket.Payload = echoRequest;

            // Will calculate the checksum
            echoRequest.ICMPPacket?.GetBytes();

            return echoRequest;

        }

        #endregion






        public static Boolean TryParse(ICMPPacket Packet, out ICMPEchoRequest ICMPEchoRequest)
        {

            ICMPEchoRequest = null;

            try
            {

                var data = new Byte[Packet.PayloadBytes.Length - 4];
                Buffer.BlockCopy(Packet.PayloadBytes, 4, data, 0, data.Length);

                ICMPEchoRequest = Create(
                                      (UInt16) System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(data, 0)),
                                      (UInt16) System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(data, 2)),
                                      data
                                  );

                return true;

            } catch
            { }

            return false;

        }

        public static Boolean TryParse(Byte[] Packet, out ICMPEchoRequest ICMPEchoRequest)
        {

            ICMPEchoRequest = null;

            try
            {

                var type      = Packet[0];
                var code      = Packet[1];

                if (type != 8 || code != 0)
                    return false;

                var checksum  = (UInt16) System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(Packet, 2));

                var data      = new Byte[Packet.Length - 8];
                Buffer.BlockCopy(Packet, 8, data, 0, data.Length);

                ICMPEchoRequest = Create(
                                      (UInt16) System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(Packet, 4)),
                                      (UInt16) System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(Packet, 6)),
                                      data
                                  );

                return true;

            } catch
            { }

            return false;

        }

        public Byte[] GetBytes()
        {

            var packet = new Byte[4 + Data.Length];
            Buffer.BlockCopy(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((Int16) Identifier)),     0, packet, 0, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((Int16) SequenceNumber)), 0, packet, 2, 2);
            Buffer.BlockCopy(Data,                                                                                   0, packet, 4, Data.Length);

            return packet;

        }


        public override String ToString()

            => String.Concat(Identifier, " / ", SequenceNumber, ": ", Text);


    }

}
