/*
 * Copyright (c) 2010-2021, Achim Friedland <achim.friedland@graphdefined.com>
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

using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Sockets.RawIP.ICMP
{

    /// <summary>
    /// A delegate called for each test run result.
    /// </summary>
    /// <param name="TestRunId">The unique identification of the current test run.</param>
    /// <param name="NuberOfTests">The overall number of test runs.</param>
    /// <param name="Result">The result of the current test run.</param>
    public delegate void TestRunResultDelegate(UInt32 TestRunId, UInt32 NuberOfTests, PingResult Result);


    /// <summary>
    /// The ICMP Client.
    /// </summary>
    public class ICMPClient : IICMPClient
    {

        #region Data

        private static readonly Random random = new Random();

        private readonly TestRunResultDelegate ResultHandler;

        #endregion

        #region Properties

        /// <summary>
        /// The DNS client to use.
        /// </summary>
        public DNSClient  DNSClient    {get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new ICMP client.
        /// </summary>
        /// <param name="ResultHandler">A delegate called for each ping result.</param>
        /// <param name="DNSClient">An optional DNS client to use.</param>
        public ICMPClient(TestRunResultDelegate  ResultHandler   = null,
                          DNSClient              DNSClient       = null)
        {

            this.ResultHandler  = ResultHandler;
            this.DNSClient      = DNSClient ?? new DNSClient();

        }

        #endregion


        #region Ping(Hostname, ...)

        /// <summary>
        /// Ping the given DNS hostname.
        /// </summary>
        /// <param name="Hostname">A DNS hostname.</param>
        /// <param name="NumberOfTests">The number of pings.</param>
        /// <param name="Timeout">The timeout of each ping.</param>
        /// <param name="ResultHandler">A delegate called for each ping result.</param>
        /// <param name="Identifier">The ICMP identifier.</param>
        /// <param name="SequenceStartValue">The ICMP echo request start value.</param>
        /// <param name="TestData">The ICMP echo request test data.</param>
        /// <param name="TTL">The time-to-live of the underlying IP packet.</param>
        /// <param name="DNSClient">An optional DNS client to use.</param>
        public async Task<PingResults> Ping(String                 Hostname,
                                            UInt32                 NumberOfTests        = 3,
                                            TimeSpan?              Timeout              = null,
                                            TestRunResultDelegate  ResultHandler        = null,
                                            UInt16?                Identifier           = null,
                                            UInt16                 SequenceStartValue   = 0,
                                            String                 TestData             = null,
                                            Byte                   TTL                  = 64,
                                            DNSClient              DNSClient            = null)
        {

            var startTimestamp  = Timestamp.Now;

            if (DNSClient != null || this.DNSClient != null)
            {

                var ipv6Addresses   = (DNSClient ?? this.DNSClient)?.
                                          Query<AAAA>(Hostname).
                                          ContinueWith(AAAARecords => AAAARecords.Result.Select(AAAARecord => AAAARecord.IPv6Address));

                var ipv4Addresses   = (DNSClient ?? this.DNSClient)?.
                                          Query<A>   (Hostname).
                                          ContinueWith(ARecords    => ARecords.   Result.Select(ARecord    => ARecord.   IPv4Address));

                await Task.WhenAll(ipv6Addresses,
                                   ipv4Addresses).
                           ConfigureAwait(false);


                //if (ipv6Addresses.Result.Any())
                //    return await Ping(ipv6Addresses.Result.FirstOrDefault(),
                //                      NumberOfTests,
                //                      Timeout,
                //                      ResultHandler,
                //                      Identifier,
                //                      SequenceStartValue,
                //                      TestData,
                //                      TTL);

                //else
                if (ipv4Addresses.Result.Any())
                    return await Ping(ipv4Addresses.Result.FirstOrDefault(),
                                      NumberOfTests,
                                      Timeout,
                                      ResultHandler,
                                      Identifier,
                                      SequenceStartValue,
                                      TestData,
                                      TTL);

            }


            var pingResult = new PingResult(Timestamp.Now - startTimestamp,
                                            ICMPErrors.DNSError);

            (ResultHandler ?? this.ResultHandler)?.Invoke(1, NumberOfTests, pingResult);

            return new PingResults(
                        new PingResult[] {
                            pingResult
                        },
                        Timeout ?? TimeSpan.FromMilliseconds(500),
                        Timestamp.Now - startTimestamp
                    );

        }

        #endregion

        #region Ping(IPv4Address, ...)

        /// <summary>
        /// Ping the given IPv4 address.
        /// </summary>
        /// <param name="IPv4Address">An IPv4 address.</param>
        /// <param name="NumberOfTests">The number of test runs/pings.</param>
        /// <param name="Timeout">The timeout of each ping.</param>
        /// <param name="ResultHandler">A delegate called for each ping result.</param>
        /// <param name="Identifier">The ICMP identifier.</param>
        /// <param name="SequenceStartValue">The ICMP echo request start value.</param>
        /// <param name="TestData">The ICMP echo request test data.</param>
        /// <param name="TTL">The time-to-live of the underlying IP packet.</param>
        public async Task<PingResults> Ping(IPv4Address            IPv4Address,
                                            UInt32                 NumberOfTests        = 3,
                                            TimeSpan?              Timeout              = null,
                                            TestRunResultDelegate  ResultHandler        = null,
                                            UInt16?                Identifier           = null,
                                            UInt16                 SequenceStartValue   = 0,
                                            String                 TestData             = null,
                                            Byte                   TTL                  = 64)
        {

            if (!Identifier.HasValue)
                Identifier  = (UInt16) random.Next(UInt16.MaxValue);

            if (TestData == null)
                TestData    = random.RandomString(30);

            if (!Timeout.HasValue)
                Timeout     = TimeSpan.FromSeconds(3);

            var startTimestamp  = Timestamp.Now;
            var ipEndPoint      = new System.Net.IPEndPoint(IPv4Address, 0);
            var pingResults     = new List<PingResult>();
            var socket          = new Socket(
                                      AddressFamily.InterNetwork,
                                      SocketType.Raw,
                                      ProtocolType.Icmp
                                  );

            socket.ReceiveTimeout = (Int32) Timeout.Value.TotalMilliseconds;
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout,    (Int32) Timeout.Value.TotalMilliseconds);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, (Int32) Timeout.Value.TotalMilliseconds);
            socket.SetSocketOption(SocketOptionLevel.IP,     SocketOptionName.IpTimeToLive,   TTL);
            //Socket.IOControl(System.Net.Sockets.IOControlCode.ReceiveAll, new byte[] { 1, 0, 0, 0 }, new byte[] { 1, 0, 0, 0 });

            for (var testRunId = SequenceStartValue; testRunId < NumberOfTests; testRunId++)
            {

                var pingResult         = new PingResult?();

                var runStartTimestamp  = Timestamp.Now;

                var echoRequest        = ICMPEchoRequest.Create(Identifier.Value,
                                                                testRunId,
                                                                TestData);

                //var sentLength         = socket.SendTo(echoRequest.ICMPPacket.GetBytes(),
                //                                       ipEndPoint);
                var sentLength         = await socket.SendToAsync(new ArraySegment<Byte>(echoRequest.ICMPPacket.GetBytes()),
                                                                  SocketFlags.None,
                                                                  ipEndPoint);

                if (sentLength == 0)
                    pingResult = new PingResult(Timestamp.Now - runStartTimestamp,
                                                ICMPErrors.SendError);

                else
                {
                    do
                    {

                        try
                        {

                            var packet          = new ArraySegment<Byte>(new Byte[65536]);
                            //var receivedLenght  = socket.Receive(packet);
                            var receivedLenght  = await socket.ReceiveAsync(packet, SocketFlags.None);

                            if (IPv4Packet.TryParse(packet.Array,    out IPv4Packet ipv4PacketReply) &&
                                ipv4PacketReply.Protocol == IPv4Protocols.ICMP                       &&
                                ICMPPacket.TryParse(ipv4PacketReply, out ICMPPacket icmpPacketReply))
                            {

                                if (icmpPacketReply.Type         == 0 &&
                                    icmpPacketReply.Code         == 0 &&
                                    ICMPEchoReply.TryParse(icmpPacketReply.PayloadBytes, out ICMPEchoReply icmpEchoReply)   &&
                                    icmpEchoReply.Identifier     == Identifier.Value &&
                                    icmpEchoReply.SequenceNumber == testRunId)
                                {

                                    if (icmpEchoReply.Text == TestData)
                                        pingResult = new PingResult(Timestamp.Now - runStartTimestamp,
                                                                    ICMPErrors.Success);
                                    else
                                        pingResult = new PingResult(Timestamp.Now - runStartTimestamp,
                                                                    ICMPErrors.InvalidReply);

                                    break;

                                }


                                // In theory here we could receive ICMP error messages.
                                // On Windows you might have to allow ICMP replies within your firewall settings!

                                else if (icmpPacketReply.Type                   ==  3 &&
                                         ICMPDestinationUnreachable.TryParse(icmpPacketReply, out ICMPDestinationUnreachable icmpDestinationUnreachable) &&
                                         icmpDestinationUnreachable.EmbeddedIPv4Packet != null &&
                                         icmpDestinationUnreachable.EmbeddedIPv4Packet.Protocol == IPv4Protocols.ICMP &&
                                         ICMPEchoRequest.TryParse(icmpDestinationUnreachable.EmbeddedIPv4Packet.Payload, out ICMPEchoRequest embeddedICMPEchoRequest) &&
                                         embeddedICMPEchoRequest.Identifier     == Identifier.Value &&
                                         embeddedICMPEchoRequest.SequenceNumber == testRunId       &&
                                         embeddedICMPEchoRequest.Text           == TestData)
                                {

                                    pingResult = new PingResult(Timestamp.Now - runStartTimestamp,
                                                                ICMPErrors.Unreachable);

                                    break;

                                }

                                else if (icmpPacketReply.Type                  == 11 &&
                                        (icmpPacketReply.Code                  ==  0 ||
                                         icmpPacketReply.Code                  ==  1) &&
                                        ICMPTimeExceeded.TryParse(icmpPacketReply, out ICMPTimeExceeded icmpTimeExceeded) &&
                                        icmpTimeExceeded.EmbeddedIPv4Packet          != null &&
                                        icmpTimeExceeded.EmbeddedIPv4Packet.Protocol == IPv4Protocols.ICMP &&
                                        ICMPEchoRequest.TryParse(icmpTimeExceeded.EmbeddedIPv4Packet.Payload, out embeddedICMPEchoRequest) &&
                                        embeddedICMPEchoRequest.Identifier     == Identifier.Value &&
                                        embeddedICMPEchoRequest.SequenceNumber == testRunId       &&
                                        embeddedICMPEchoRequest.Text           == TestData)
                                {

                                    pingResult = new PingResult(Timestamp.Now - runStartTimestamp,
                                                                ICMPErrors.TTLExceeded);

                                    break;

                                }

                                // Source Quench     ==  4
                                // Redirect          ==  5
                                // Parameter Problem == 12

                            }

                        }
                        catch (SocketException se)
                        {

                            switch (se.SocketErrorCode)
                            {

                                // ICMP Destination Unreachable
                                case SocketError.ConnectionReset:
                                    pingResult = new PingResult(Timestamp.Now - runStartTimestamp,
                                                                ICMPErrors.SendError);
                                    break;

                                // ICMP TTL exceeded
                                case SocketError.NetworkReset:
                                    pingResult = new PingResult(Timestamp.Now - runStartTimestamp,
                                                                ICMPErrors.SendError);
                                    break;

                                // Client sent a message larger than the max message size allowed
                                case SocketError.MessageSize:
                                    pingResult = new PingResult(Timestamp.Now - runStartTimestamp,
                                                                ICMPErrors.SendError);
                                    break;

                                case SocketError.TimedOut:
                                    pingResult = new PingResult(Timestamp.Now - runStartTimestamp,
                                                                ICMPErrors.Timeout);
                                    break;


                                default:
                                    pingResult = new PingResult(Timestamp.Now - runStartTimestamp,
                                                                ICMPErrors.Unknown);
                                    break;

                            }

                            break;

                        }
                        catch (Exception e)
                        {

                            DebugX.LogException(e, "ICMP client");

                            pingResult = new PingResult(Timestamp.Now - runStartTimestamp,
                                                        ICMPErrors.InvalidReply);

                            break;

                        }

                    // Restart, whenever the received packet is not the one we expected!
                    } while (Timestamp.Now - runStartTimestamp < Timeout.Value);
                }

                if (pingResult.HasValue)
                {

                    pingResults.Add(pingResult.Value);

                    (ResultHandler ?? this.ResultHandler)?.Invoke(testRunId,
                                                                  NumberOfTests,
                                                                  pingResult.Value);

                }

            }

            socket.Close();

            return new PingResults(pingResults,
                                   Timeout.Value,
                                   Timestamp.Now - startTimestamp);

        }

        #endregion

        #region Ping(IPv6Address, ...)

        /// <summary>
        /// Ping the given IPv6 address.
        /// </summary>
        /// <param name="IPv6Address">An IPv6 address.</param>
        /// <param name="NumberOfTests">The number of pings.</param>
        /// <param name="Timeout">The timeout of each ping.</param>
        /// <param name="ResultHandler">A delegate called for each ping result.</param>
        /// <param name="Identifier">The ICMP identifier.</param>
        /// <param name="SequenceStartValue">The ICMP echo request start value.</param>
        /// <param name="TestData">The ICMP echo request test data.</param>
        /// <param name="TTL">The time-to-live of the underlying IP packet.</param>
        public async Task<PingResults> Ping(IPv6Address         IPv6Address,
                                            UInt32              NumberOfTests        = 3,
                                            TimeSpan?           Timeout              = null,
                                            TestRunResultDelegate  ResultHandler        = null,
                                            UInt16?             Identifier           = null,
                                            UInt16              SequenceStartValue   = 0,
                                            String              TestData             = null,
                                            Byte                TTL                  = 64)
        {

            if (!Identifier.HasValue)
                Identifier  = (UInt16) random.Next(UInt16.MaxValue);

            if (TestData == null)
                TestData    = random.RandomString(30);

            if (!Timeout.HasValue)
                Timeout     = TimeSpan.FromSeconds(3);

            var startTimestamp  = Timestamp.Now;
            var ipEndPoint      = new System.Net.IPEndPoint(IPv6Address, 0);
            var pingResults     = new List<PingResult>();
            var socket          = new Socket(
                                      AddressFamily.InterNetworkV6,
                                      SocketType.Raw,
                                      ProtocolType.IcmpV6
                                  );

            socket.ReceiveTimeout = (Int32) Timeout.Value.TotalMilliseconds;
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout,    (Int32) Timeout.Value.TotalMilliseconds);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, (Int32) Timeout.Value.TotalMilliseconds);
            socket.SetSocketOption(SocketOptionLevel.IP,     SocketOptionName.IpTimeToLive,   TTL);
            //Socket.IOControl(System.Net.Sockets.IOControlCode.ReceiveAll, new byte[] { 1, 0, 0, 0 }, new byte[] { 1, 0, 0, 0 });

            for (var repetition = SequenceStartValue; repetition < NumberOfTests; repetition++)
            {

                var runStartTimestamp  = Timestamp.Now;

                var echoRequest        = ICMPEchoRequest.Create(Identifier.Value,
                                                                repetition,
                                                                TestData);

                var sentLength         = socket.SendTo(echoRequest.ICMPPacket.GetBytes(),
                                                       ipEndPoint);

                if (sentLength == 0)
                {

                    pingResults.Add(new PingResult(Timestamp.Now - runStartTimestamp,
                                                   ICMPErrors.SendError));

                    continue;

                }

                do
                {

                    try
                    {

                        var packet         = new Byte[65536];
                        var receivedLenght = socket.Receive(packet);


                        // ToDo: Implement IPv6Packet and ICMPv6!!!
                        if (IPv4Packet.TryParse(packet,          out IPv4Packet ipv4PacketReply) &&
                            ipv4PacketReply.Protocol == IPv4Protocols.ICMP                       &&
                            ICMPPacket.TryParse(ipv4PacketReply, out ICMPPacket icmpPacketReply))
                        {

                            if (icmpPacketReply.Type         == 0 &&
                                icmpPacketReply.Code         == 0 &&
                                ICMPEchoReply.TryParse(icmpPacketReply.PayloadBytes, out ICMPEchoReply icmpEchoReply)   &&
                                icmpEchoReply.Identifier     == Identifier.Value &&
                                icmpEchoReply.SequenceNumber == repetition)
                            {

                                if (icmpEchoReply.Text == TestData)
                                    pingResults.Add(new PingResult(Timestamp.Now - runStartTimestamp,
                                                                   ICMPErrors.Success));
                                else
                                    pingResults.Add(new PingResult(Timestamp.Now - runStartTimestamp,
                                                                   ICMPErrors.InvalidReply));

                                break;

                            }


                            // In theory here we could receive ICMP error messages.
                            // On Windows you might have to allow ICMP replies within your firewall settings!

                            else if (icmpPacketReply.Type                   ==  3 &&
                                     ICMPDestinationUnreachable.TryParse(icmpPacketReply, out ICMPDestinationUnreachable icmpDestinationUnreachable) &&
                                     icmpDestinationUnreachable.EmbeddedIPv4Packet != null &&
                                     icmpDestinationUnreachable.EmbeddedIPv4Packet.Protocol == IPv4Protocols.ICMP &&
                                     ICMPEchoRequest.TryParse(icmpDestinationUnreachable.EmbeddedIPv4Packet.Payload, out ICMPEchoRequest embeddedICMPEchoRequest) &&
                                     embeddedICMPEchoRequest.Identifier     == Identifier.Value &&
                                     embeddedICMPEchoRequest.SequenceNumber == repetition       &&
                                     embeddedICMPEchoRequest.Text           == TestData)
                            {

                                pingResults.Add(new PingResult(Timestamp.Now - runStartTimestamp,
                                                               ICMPErrors.Unreachable));

                                break;

                            }

                            else if (icmpPacketReply.Type                  == 11 &&
                                    (icmpPacketReply.Code                  ==  0 ||
                                     icmpPacketReply.Code                  ==  1) &&
                                    ICMPTimeExceeded.TryParse(icmpPacketReply, out ICMPTimeExceeded icmpTimeExceeded) &&
                                    icmpTimeExceeded.EmbeddedIPv4Packet          != null &&
                                    icmpTimeExceeded.EmbeddedIPv4Packet.Protocol == IPv4Protocols.ICMP &&
                                    ICMPEchoRequest.TryParse(icmpTimeExceeded.EmbeddedIPv4Packet.Payload, out embeddedICMPEchoRequest) &&
                                    embeddedICMPEchoRequest.Identifier     == Identifier.Value &&
                                    embeddedICMPEchoRequest.SequenceNumber == repetition       &&
                                    embeddedICMPEchoRequest.Text           == TestData)
                            {

                                pingResults.Add(new PingResult(Timestamp.Now - runStartTimestamp,
                                                               ICMPErrors.TTLExceeded));

                                break;

                            }

                            // Source Quench     ==  4
                            // Redirect          ==  5
                            // Parameter Problem == 12

                        }

                    }
                    catch (SocketException se)
                    {

                        switch (se.SocketErrorCode)
                        {

                            // ICMP Destination Unreachable
                            case SocketError.ConnectionReset:
                                pingResults.Add(new PingResult(Timestamp.Now - runStartTimestamp,
                                                               ICMPErrors.SendError));
                                break;

                            // ICMP TTL exceeded
                            case SocketError.NetworkReset:
                                pingResults.Add(new PingResult(Timestamp.Now - runStartTimestamp,
                                                               ICMPErrors.SendError));
                                break;

                            // Client sent a message larger than the max message size allowed
                            case SocketError.MessageSize:
                                pingResults.Add(new PingResult(Timestamp.Now - runStartTimestamp,
                                                               ICMPErrors.SendError));
                                break;

                            case SocketError.TimedOut:
                                pingResults.Add(new PingResult(Timestamp.Now - runStartTimestamp,
                                                               ICMPErrors.Timeout));
                                break;


                            default:
                                pingResults.Add(new PingResult(Timestamp.Now - runStartTimestamp,
                                                               ICMPErrors.Unknown));
                                break;

                        }

                        break;

                    }
                    catch (Exception e)
                    {

                        DebugX.LogException(e, "ICMP client");

                        pingResults.Add(new PingResult(Timestamp.Now - runStartTimestamp,
                                                       ICMPErrors.InvalidReply));

                        break;

                    }

                // Restart, whenever the received packet is not the one we expected!
                } while (Timestamp.Now - runStartTimestamp < Timeout.Value);

            }

            socket.Close();

            return new PingResults(pingResults,
                                   Timeout.Value,
                                   Timestamp.Now - startTimestamp);

        }

        #endregion


    }

}
