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
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Sockets.RawIP.ICMP
{

    /// <summary>
    /// The ICMP Client.
    /// </summary>
    public class ICMPClient
    {

        private static Random random = new Random();



        public class PingResults
        {

            public Boolean   Success          { get; }

            public UInt32    NumberOfTests    { get; }

            public TimeSpan  Min              { get; }

            public TimeSpan  Avg              { get; }

            public Double    StdDev           { get; }

            public TimeSpan  Max              { get; }


            public PingResults(Boolean   Success,
                               UInt32    NumberOfTests,
                               TimeSpan  Min,
                               TimeSpan  Avg,
                               Double    StdDev,
                               TimeSpan  Max)
            {

                this.Success        = Success;
                this.NumberOfTests  = NumberOfTests;
                this.Min            = Min;
                this.Avg            = Avg;
                this.StdDev         = StdDev;
                this.Max            = Max;

            }

        }



        public async Task<PingResults> Ping(IPv4Address  IPAddress,
                                            UInt32       Repetitions          = 3,
                                            TimeSpan?    Timeout              = null,
                                            UInt16?      Identifier           = null,
                                            UInt16       SequenceStartValue   = 0,
                                            String       TestData             = null,
                                            Byte         TTL                  = 64)
        {

            if (!Identifier.HasValue)
                Identifier = (UInt16) random.Next((Int32) UInt16.MaxValue);

            if (TestData == null)
                TestData   = random.RandomString(30);

            var Socket          = new System.Net.Sockets.Socket(
                                      System.Net.Sockets.AddressFamily.InterNetwork,
                                      System.Net.Sockets.SocketType.Raw,
                                      System.Net.Sockets.ProtocolType.Icmp
                                  );

            //Socket.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Parse("10.1.1.2"), 0));
            //Socket.IOControl(System.Net.Sockets.IOControlCode.ReceiveAll, new byte[] { 1, 0, 0, 0 }, new byte[] { 1, 0, 0, 0 });

            Socket.SetSocketOption(System.Net.Sockets.SocketOptionLevel.Socket, System.Net.Sockets.SocketOptionName.SendTimeout,    (Int32) (Timeout?.TotalSeconds ?? 3000));
            Socket.SetSocketOption(System.Net.Sockets.SocketOptionLevel.Socket, System.Net.Sockets.SocketOptionName.ReceiveTimeout, (Int32) (Timeout?.TotalSeconds ?? 3000));
            Socket.SetSocketOption(System.Net.Sockets.SocketOptionLevel.IP,     System.Net.Sockets.SocketOptionName.IpTimeToLive,   TTL);

            var ipEndPoint      = new System.Net.IPEndPoint(System.Net.IPAddress.Parse(IPAddress.ToString()), 0);
            var endPoint        = (System.Net.EndPoint) ipEndPoint;
            var replies         = new List<TimeSpan>();
            var failures        = new List<TimeSpan>();

            for (var repetition = SequenceStartValue; repetition < Repetitions; repetition++)
            {

                var echoRequest     = ICMPEchoRequest.Create(Identifier.Value,
                                                             repetition,
                                                             TestData);

                var startTimestamp  = DateTime.Now;
                var c               = Socket.SendTo(echoRequest.ICMPPacket.GetBytes(),
                                                    ipEndPoint);

                try
                {

                    var Packet          = new Byte[65536];
                    var ReceivedLenght  = Socket.ReceiveFrom(Packet, ref endPoint);

                    if (IPv4Packet.TryParse(Packet, out IPv4Packet IPv4PacketReply) &&
                        IPv4PacketReply.Protocol == IPv4Protocols.ICMP)
                    {

                        if (ICMPPacket.TryParse(IPv4PacketReply, out ICMPPacket ICMPPacketReply) &&
                            ICMPPacketReply.Type == 0 &&
                            ICMPPacketReply.Code == 0)
                        {

                            if (ICMPEchoReply.TryParse(ICMPPacketReply.PayloadBytes, out ICMPEchoReply ICMPEchoReplyReply) &&
                                ICMPEchoReplyReply.Identifier == Identifier.Value &&
                                ICMPEchoReplyReply.Text       == TestData)
                            {
                                replies.Add(DateTime.Now - startTimestamp);
                            }
                            else
                                failures.Add(DateTime.Now - startTimestamp);

                        }

                    }

                    // Time Exceeded, Destination Unreachable and other errors
                    //if (IPReply.ICMP.Message is ICMPIPHeaderReply)
                    //{
                    //    IPv4Packet IPAttached = ((ICMPIPHeaderReply)IPReply.ICMP.Message).IP;
                    //    if (IPEndPoint.Address.ToString() == IPAttached.DestinationAddress.ToString())
                    //        if (IPAttached.ICMP.Message is ICMPEchoReply)
                    //            break;
                    //}

                    // Check if received packet is the one we expected, discard it if not
                    //if ((IPReply.ICMP.Message is ICMPEchoReply) && (IPReply.ICMP.Code == 0) && (IPEndPoint.ToString() == EndPoint.ToString()) &&
                    //    (((ICMPEchoReply)IPReply.ICMP.Message).Identifier == ((ICMPEcho)ICMP.Message).Identifier) &&
                    //    (((ICMPEchoReply)IPReply.ICMP.Message).SequenceNumber == ((ICMPEcho)ICMP.Message).SequenceNumber)) break;

                }
                catch (Exception)
                {
                    failures.Add(DateTime.Now - startTimestamp);
                }

            }

            Socket.Close();

            var average         = replies.Select(timestamp => timestamp.TotalMilliseconds).AverageAndStdDev();

            return new PingResults(replies.Count == Repetitions,
                                   Repetitions,
                                   TimeSpan.FromMilliseconds(replies.Select(timestamp => timestamp.TotalMilliseconds).Min()),
                                   TimeSpan.FromMilliseconds(average.Item1),
                                   average.Item2,
                                   TimeSpan.FromMilliseconds(replies.Select(timestamp => timestamp.TotalMilliseconds).Max()));

        }

    }

}
