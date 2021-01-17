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
using System.IO;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Sockets.RawIP.ICMP
{


    //public class ICMPIPHeaderReply : ICMPPacket
    //{

    //    public Byte[] Data;

    //    public ICMPIPHeaderReply(ref byte[] Packet)
    //    {
    //        try
    //        {
    //            Data = new byte[Packet.Length - 4];
    //            Buffer.BlockCopy(Packet, 4, Data, 0, Data.Length);
    //            //IP = new IPPacket(ref Data);
    //        } catch { }
    //    }

    //    public override byte[] GetBytes()
    //    {

    //        if (Data == null)
    //            Data = new byte[0];

    //        var Packet = new byte[4 + Data.Length];
    //        Buffer.BlockCopy(Data, 0, Packet, 4, Data.Length);

    //        return Packet;

    //    }

    //}





    //public class ICMPRedirect : ICMPPacket
    //{

    //    public ulong GatewayInternetAddress;
    //    public byte[] Data;

    //    public enum CodeEnum {
    //        RedirectDatagramsForTheNetwork = 0,
    //        RedirectDatagramsForTheHost = 1,
    //        RedirectDatagramsForTheTypeOfServiceAndNetwork = 2,
    //        RedirectDatagramsForTheTypeOfServiceAndHost = 3
    //    }

    //    public ICMPRedirect(ref byte[] Packet)
    //    {
    //        try {
    //            GatewayInternetAddress = (ulong)System.BitConverter.ToInt32(Packet, 0);
    //            Data = new byte[Packet.Length - 4];
    //            System.Buffer.BlockCopy(Packet, 0, Data, 4, Packet.Length);
    //        } catch { }
    //    }

    //    public override byte[] GetBytes()
    //    {
    //        if (Data == null) Data = new byte[0];
    //        byte[] Packet = new byte[4 + Data.Length];
    //        System.Buffer.BlockCopy(System.BitConverter.GetBytes((long)GatewayInternetAddress), 0, Packet, 0, 4);
    //        System.Buffer.BlockCopy(Data, 0, Packet, 4, Data.Length);
    //        return Packet;
    //    }

    //}





    //public class ICMPDestinationUnreachable : ICMPIPHeaderReply
    //{
    //    public enum CodeEnum {
    //        NetUnreachable = 0,
    //        HostUnreachable = 1,
    //        ProtocolUnreachable = 2,
    //        PortUnreachable = 3,
    //        FragmentationNeededAndDFSet = 4,
    //        SourceRouteFailed = 5
    //    }

    //    public ICMPDestinationUnreachable(ref byte[] Packet)
    //    { }

    //}




    //public class ICMPSourceQuench : ICMPIPHeaderReply
    //{
    //    public ICMPSourceQuench(ref byte[] Packet)
    //    { }

    //}




    //public class ICMPTimeExceeded : ICMPIPHeaderReply
    //{
    //    public enum CodeEnum {
    //        TimeToLiveExceededInTransit = 0,
    //        FragmentReassemblyTimeExceeded = 1
    //    }

    //    public ICMPTimeExceeded(ref byte[] Packet)
    //    { }

    //}




    //public class ICMPParameterProblem : ICMPPacket
    //{

    //    public byte Pointer;
    //    public byte[] Data;

    //    public ICMPParameterProblem(ref byte[] Packet)
    //    {
    //        try {
    //            Pointer = Packet[0];
    //            Data = new byte[Packet.Length - 4];
    //            System.Buffer.BlockCopy(Packet, 0, Data, 4, Packet.Length);
    //        } catch { }
    //    }

    //    public override byte[] GetBytes()
    //    {
    //        if (Data == null) Data = new byte[0];
    //        byte[] Packet = new byte[4 + Data.Length];
    //        Packet[0] = Pointer;
    //        System.Buffer.BlockCopy(Data, 0, Packet, 4, Data.Length);
    //        return Packet;
    //    }

    //}




    //public class ICMPTimestamp : ICMPPacket
    //{

    //    public ushort Identifier;
    //    public ushort SequenceNumber;
    //    public ulong OriginateTimestamp;
    //    public ulong ReceiveTimestamp;
    //    public ulong TransmitTimestamp;

    //    public ICMPTimestamp(ref byte[] Packet)
    //    {
    //        try {
    //            Identifier = (ushort)System.BitConverter.ToInt16(Packet, 0);
    //            SequenceNumber = (ushort)System.BitConverter.ToInt16(Packet, 2);
    //            OriginateTimestamp = (ulong)System.BitConverter.ToInt32(Packet, 4);
    //            ReceiveTimestamp = (ulong)System.BitConverter.ToInt32(Packet, 8);
    //            TransmitTimestamp = (ulong)System.BitConverter.ToInt32(Packet, 12);
    //        } catch { }
    //    }

    //    public override byte[] GetBytes()
    //    {
    //        byte[] Packet = new byte[16];
    //        System.Buffer.BlockCopy(System.BitConverter.GetBytes((short)Identifier), 0, Packet, 0, 2);
    //        System.Buffer.BlockCopy(System.BitConverter.GetBytes((short)SequenceNumber), 0, Packet, 2, 2);
    //        System.Buffer.BlockCopy(System.BitConverter.GetBytes((long)OriginateTimestamp), 0, Packet, 4, 4);
    //        System.Buffer.BlockCopy(System.BitConverter.GetBytes((long)ReceiveTimestamp), 0, Packet, 8, 4);
    //        System.Buffer.BlockCopy(System.BitConverter.GetBytes((long)TransmitTimestamp), 0, Packet, 12, 4);
    //        return Packet;
    //    }

    //}




    //public class ICMPTimestampReply : ICMPTimestamp
    //{
    //    public ICMPTimestampReply(ref byte[] Packet)
    //    { }

    //}




    //public class ICMPInformationRequest : ICMPPacket
    //{

    //    public ushort Identifier;
    //    public ushort SequenceNumber;

    //    public ICMPInformationRequest(ref byte[] Packet)
    //    {
    //        try {
    //            Identifier = (ushort)System.BitConverter.ToInt16(Packet, 0);
    //            SequenceNumber = (ushort)System.BitConverter.ToInt16(Packet, 2);
    //        } catch { }
    //    }

    //    public override byte[] GetBytes()
    //    {
    //        byte[] Packet = new byte[4];
    //        System.Buffer.BlockCopy(System.BitConverter.GetBytes((short)Identifier), 0, Packet, 0, 2);
    //        System.Buffer.BlockCopy(System.BitConverter.GetBytes((short)SequenceNumber), 0, Packet, 2, 2);
    //        return Packet;
    //    }

    //}




    //public class ICMPInformationReply : ICMPInformationRequest
    //{
    //    public ICMPInformationReply(ref byte[] Packet)
    //    { }

    //}

}
