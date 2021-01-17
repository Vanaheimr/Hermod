/*
 * Copyright (c) 2010-2021, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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
                catch (Exception)
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






        public static Boolean TryParse(Byte[] Packet, out ICMPEchoRequest ICMPEchoRequest)
        {

            try
            {

                var data = new Byte[Packet.Length - 4];
                Buffer.BlockCopy(Packet, 4, data, 0, data.Length);

                ICMPEchoRequest = Create(
                                      (UInt16) BitConverter.ToInt16(Packet, 0),
                                      (UInt16) BitConverter.ToInt16(Packet, 2),
                                      data
                                  );

                return true;

            } catch
            { }

            ICMPEchoRequest = default;
            return false;

        }

        public Byte[] GetBytes()
        {

            var packet = new Byte[4 + Data.Length];
            Buffer.BlockCopy(BitConverter.GetBytes((Int16) Identifier),     0, packet, 0, 2);
            Buffer.BlockCopy(BitConverter.GetBytes((Int16) SequenceNumber), 0, packet, 2, 2);
            Buffer.BlockCopy(Data,                                          0, packet, 4, Data.Length);

            return packet;

        }


        public override String ToString()

            => String.Concat(Identifier, " / ", SequenceNumber, ": ", Text);


    }

}
