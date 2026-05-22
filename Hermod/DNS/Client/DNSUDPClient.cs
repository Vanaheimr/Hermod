/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// A DNS UDP client for a single DNS server.
    /// </summary>
    public class DNSUDPClient : IDNSClientWithTransport
    {

        #region Data

        /// <summary>
        /// The default DNS query timeout.
        /// </summary>
        public static readonly    TimeSpan                  DefaultQueryTimeout              = TimeSpan.FromSeconds(23.5);

        // Note: ConnectTimeout, ReceiveTimeout, SendTimeout and InternalBufferSize are required by IDNSClient2
        // but are meaningless for a connectionless UDP client. UDP uses QueryTimeout as a single
        // unified timeout, and the receive buffer is determined by UDPPayloadSize (EDNS0).

        private Boolean disposedValue;

        private readonly ILogger<DNSUDPClient>  logger;

        #endregion

        #region Properties

        /// <summary>
        /// The IP address of the DNS server to query.
        /// </summary>
        public IIPAddress  RemoteIPAddress     { get; }

        /// <summary>
        /// The UDP port of the DNS server to query.
        /// </summary>
        public IPPort?     RemotePort          { get; }

        /// <summary>
        /// Whether DNS recursion is desired.
        /// </summary>
        public Boolean?    RecursionDesired    { get; set; }

        /// <summary>
        /// The default EDNS0 UDP payload size to advertise in DNS queries.
        /// </summary>
        public UInt16      UDPPayloadSize      { get; } = DNSPacket.DefaultUDPPayloadSize;

        /// <summary>
        /// The DNS query timeout.
        /// </summary>
        public TimeSpan    QueryTimeout        { get; set; }

        /// <summary>
        /// Optional EDNS0 options to include in every DNS query.
        /// </summary>
        public List<EDNSOption>  EDNSOptions   { get; } = [];



        /// <summary>
        /// Whether the client is currently connected to the server.
        /// </summary>
        public Boolean      IsConnected
            => false;


        // Note: UDP is connectionless — each query creates and disposes its own socket.
        // These "Current*" properties are required by IDNSClient2 but are meaningless
        // for a stateless UDP client and always return null to avoid race conditions.

        /// <summary>
        /// Always null for UDP (connectionless, no persistent endpoint).
        /// </summary>
        public IPEndPoint?  CurrentLocalEndPoint     => null;

        /// <summary>
        /// Always null for UDP (connectionless, no persistent endpoint).
        /// </summary>
        public UInt16?      CurrentLocalPort         => null;

        /// <summary>
        /// Always null for UDP (connectionless, no persistent endpoint).
        /// </summary>
        public IIPAddress?  CurrentLocalIPAddress    => null;

        /// <summary>
        /// Always null for UDP (connectionless, no persistent endpoint).
        /// </summary>
        public IPEndPoint?  CurrentRemoteEndPoint    => null;

        /// <summary>
        /// Always null for UDP (connectionless, no persistent endpoint).
        /// </summary>
        public UInt16?      CurrentRemotePort        => null;

        /// <summary>
        /// Always null for UDP (connectionless, no persistent endpoint).
        /// </summary>
        public IIPAddress?  CurrentRemoteIPAddress   => null;

        public  URL                      RemoteURL          { get; }

        // These timeouts and buffer size are required by IDNSClient2 but irrelevant for UDP.
        // UDP is connectionless — each query creates its own socket with QueryTimeout as the
        // single unified timeout, and the receive buffer is sized by UDPPayloadSize (EDNS0).
        public  TimeSpan                 ConnectTimeout     => QueryTimeout;
        public  TimeSpan                 ReceiveTimeout     => QueryTimeout;
        public  TimeSpan                 SendTimeout        => QueryTimeout;
        public  UInt32                   InternalBufferSize => (UInt32) Math.Max(4096, (Int32) UDPPayloadSize);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new DNS UDP client for the given DNS server.
        /// </summary>
        /// <param name="IPAddress">The IP address of the DNS server to query.</param>
        /// <param name="Port">The UDP port of the DNS server to query.</param>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">An optional DNS query timeout. Default is 23.5 seconds.</param>
        public DNSUDPClient(IIPAddress              IPAddress,
                            IPPort?                 Port               = null,
                            Boolean?                RecursionDesired   = null,
                            TimeSpan?               QueryTimeout       = null,
                            ILogger<DNSUDPClient>?  Logger             = null,
                            ILoggerFactory?         LoggerFactory      = null)

        {

            this.RemoteIPAddress   = IPAddress;
            this.RemotePort        = Port             ?? IPPort.DNS;
            this.RemoteURL         = URL.Parse($"dns://{IPAddress}:{this.RemotePort}");
            this.RecursionDesired  = RecursionDesired ?? true;
            this.QueryTimeout      = QueryTimeout     ?? DefaultQueryTimeout;
            this.logger            = Logger           ?? (LoggerFactory ?? NullLoggerFactory.Instance).CreateLogger<DNSUDPClient>();

        }

        #endregion


        #region Query (DomainName,     ResourceRecordTypes, Timeout = null, RecursionDesired = true, ForceUpdate = false, ...)

        public Task<DNSInfo> Query(DomainName                           DomainName,
                                   IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                   TimeSpan?                            Timeout             = null,
                                   Boolean?                             RecursionDesired    = true,
                                   Boolean?                             ForceUpdate         = false,
                                   CancellationToken                    CancellationToken   = default)

            => Query(
                   DNSServiceName.Parse(DomainName.FullName),
                   ResourceRecordTypes,
                   Timeout,
                   RecursionDesired,
                   ForceUpdate,
                   CancellationToken
               );

        #endregion

        #region Query (DNSServiceName, ResourceRecordTypes, Timeout = null, RecursionDesired = true, ForceUpdate = false, ...)

        public async Task<DNSInfo> Query(DNSServiceName                       DNSServiceName,
                                         IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                         TimeSpan?                            Timeout             = null,
                                         Boolean?                             RecursionDesired    = true,
                                         Boolean?                             ForceUpdate         = false,
                                         CancellationToken                    CancellationToken   = default)
        {

            var effectiveTimeout  = Timeout ?? QueryTimeout;
            var stopwatch         = Stopwatch.StartNew();

            #region Initial checks

            if (DNSServiceName.IsNullOrEmpty())
                return new DNSInfo(
                           Origin:                 new DNSServerConfig(
                                                       IPv4Address.Localhost,
                                                       IPPort.DNS
                                                   ),
                           QueryId:                0,
                           IsAuthoritativeAnswer:  false,
                           IsTruncated:            false,
                           RecursionDesired:       true,
                           RecursionAvailable:     false,
                           ResponseCode:           DNSResponseCodes.NameError,
                           Answers:                [],
                           Authorities:            [],
                           AdditionalRecords:      [],
                           IsValid:                true,
                           IsTimeout:              false,
                           Timeout:                effectiveTimeout,
                           Runtime:                stopwatch.Elapsed
                       );

            var resourceRecordTypes = ResourceRecordTypes.ToList();

            if (resourceRecordTypes.Count == 0)
                resourceRecordTypes = [ DNSResourceRecordTypes.Any ];

            #endregion


            var dnsQuery = DNSPacket.Query(
                               DNSServiceName,
                               UDPPayloadSize,
                               this.RecursionDesired ?? RecursionDesired ?? true,
                               EDNSOptions.Count > 0 ? EDNSOptions : null,
                               [.. resourceRecordTypes]
                           );

            Socket? socket        = null;

            try
            {

                var serverAddress      = System.Net.IPAddress.Parse(RemoteIPAddress.ToString());
                var remoteEndPoint     = new IPEndPoint(serverAddress, (RemotePort ?? IPPort.DNS).ToInt32());

                socket                 = RemoteIPAddress.IsIPv4
                                             ? new Socket(AddressFamily.InterNetwork,   SocketType.Dgram, ProtocolType.Udp)
                                             : new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);

                using var timeoutCTS   = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
                timeoutCTS.CancelAfter(effectiveTimeout);

                await socket.ConnectAsync(remoteEndPoint, timeoutCTS.Token).
                             ConfigureAwait(false);

                using var ms = new MemoryStream();
                dnsQuery.Serialize(ms, false, []);

                await socket.SendToAsync(ms.ToArray(), SocketFlags.None, remoteEndPoint, timeoutCTS.Token).
                             ConfigureAwait(false);

                var data      = new Byte[Math.Max(4096, (Int32) UDPPayloadSize)];
                var received  = await socket.ReceiveAsync(data, SocketFlags.None, timeoutCTS.Token).
                                             ConfigureAwait(false);

                var response = DNSInfo.ReadResponse(
                                  new DNSServerConfig(
                                      RemoteIPAddress!,
                                      RemotePort ?? IPPort.DNS,
                                      DNSTransport.UDP,
                                      effectiveTimeout
                                  ),
                                  dnsQuery.TransactionId,
                                  new MemoryStream(data, 0, received),
                                  effectiveTimeout,
                                  stopwatch.Elapsed
                              );

                // RFC 5966: If the UDP response is truncated, retry via TCP
                if (response.IsTruncated)
                {
                    logger.LogDebug(
                        "DNS UDP response from {RemoteIPAddress}:{RemotePort} was truncated; retrying via TCP",
                        RemoteIPAddress,
                        RemotePort
                    );

                    return await QueryViaTCPFallbackAsync(dnsQuery, effectiveTimeout, timeoutCTS.Token).
                                     ConfigureAwait(false);
                }

                return response;

            }
            catch (SocketException se) when (se.SocketErrorCode == SocketError.AddressFamilyNotSupported)
            {

                return new DNSInfo(
                           Origin:                 new DNSServerConfig(
                                                       RemoteIPAddress,
                                                       RemotePort ?? IPPort.DNS
                                                   ),
                           QueryId:                dnsQuery.TransactionId,
                           IsAuthoritativeAnswer:  false,
                           IsTruncated:            false,
                           RecursionDesired:       false,
                           RecursionAvailable:     false,
                           ResponseCode:           DNSResponseCodes.ServerFailure,
                           Answers:                [],
                           Authorities:            [],
                           AdditionalRecords:      [],
                           IsValid:                true,
                           IsTimeout:              false,
                           Timeout:                effectiveTimeout,
                           Runtime:                stopwatch.Elapsed
                       );

            }
            catch (OperationCanceledException) when (!CancellationToken.IsCancellationRequested)
            {

                logger.LogWarning(
                    "DNS UDP query to {RemoteIPAddress}:{RemotePort} timed out",
                    RemoteIPAddress,
                    RemotePort
                );

                return DNSInfo.TimedOut(
                           new DNSServerConfig(
                               RemoteIPAddress,
                               RemotePort ?? IPPort.DNS
                           ),
                           dnsQuery.TransactionId,
                           effectiveTimeout
                       );

            }
            catch (SocketException se)
            {

                logger.LogWarning(
                    se,
                    "DNS UDP query to {RemoteIPAddress}:{RemotePort} socket error: {SocketErrorCode}",
                    RemoteIPAddress,
                    RemotePort,
                    se.SocketErrorCode
                );

                return DNSInfo.Failed(
                           new DNSServerConfig(
                               RemoteIPAddress,
                               RemotePort ?? IPPort.DNS
                           ),
                           dnsQuery.TransactionId,
                           effectiveTimeout
                       );

            }
            catch (OperationCanceledException)
            {

                // External cancellation — typically the race-cancel from DNSClient
                // when another DNS server responded first, or a caller-initiated
                // cancel. Not a real failure; return silently without log noise.
                return DNSInfo.Failed(
                           new DNSServerConfig(
                               RemoteIPAddress,
                               RemotePort ?? IPPort.DNS
                           ),
                           dnsQuery.TransactionId,
                           effectiveTimeout
                       );

            }
            catch (Exception e)
            {

                logger.LogError(
                    e,
                    "DNS UDP query to {RemoteIPAddress}:{RemotePort} failed",
                    RemoteIPAddress,
                    RemotePort
                );

                return DNSInfo.Failed(
                           new DNSServerConfig(
                               RemoteIPAddress,
                               RemotePort ?? IPPort.DNS
                           ),
                           dnsQuery.TransactionId,
                           effectiveTimeout
                       );

            }
            finally
            {
                socket?.Dispose();
            }

        }

        #endregion

        #region (private) QueryViaTCPFallbackAsync(DNSQuery, Timeout, CancellationToken)

        /// <summary>
        /// TCP fallback for truncated UDP responses (RFC 5966).
        /// </summary>
        private async Task<DNSInfo> QueryViaTCPFallbackAsync(DNSPacket          DNSQuery,
                                                             TimeSpan           Timeout,
                                                             CancellationToken  CancellationToken)
        {

            Socket? socket = null;

            var stopwatch = Stopwatch.StartNew();

            try
            {

                var serverAddress  = System.Net.IPAddress.Parse(RemoteIPAddress.ToString());
                var endPoint       = new IPEndPoint(serverAddress, (RemotePort ?? IPPort.DNS).ToInt32());

                socket             = RemoteIPAddress.IsIPv4
                                         ? new Socket(AddressFamily.InterNetwork,   SocketType.Stream, ProtocolType.Tcp)
                                         : new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);

                using var timeoutCTS = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
                timeoutCTS.CancelAfter(Timeout);

                await socket.ConnectAsync(endPoint, timeoutCTS.Token).
                             ConfigureAwait(false);

                using var networkStream = new NetworkStream(socket, ownsSocket: false);

                using var ms = new MemoryStream();
                ms.WriteByte(0);
                ms.WriteByte(0);
                DNSQuery.Serialize(ms, false, []);
                var data       = ms.ToArray();
                var dataLength = data.Length - 2;
                data[0] = (Byte) (dataLength >> 8);
                data[1] = (Byte) (dataLength & 0xFF);

                await networkStream.WriteAsync(data, timeoutCTS.Token).ConfigureAwait(false);
                await networkStream.FlushAsync(timeoutCTS.Token).      ConfigureAwait(false);

                var responseLength = await networkStream.ReadUInt16BEAsync(timeoutCTS.Token).
                                                         ConfigureAwait(false);

                // A valid DNS header is at least 12 bytes.
                if (responseLength < 12)
                    return DNSInfo.Failed(
                               new DNSServerConfig(
                                   RemoteIPAddress,
                                   RemotePort ?? IPPort.DNS,
                                   DNSTransport.TCP,
                                   Timeout
                               ),
                               DNSQuery.TransactionId,
                               Timeout
                           );

                var buffer    = new Byte[responseLength];
                var totalRead = 0;

                while (totalRead < responseLength)
                {

                    var bytesRead = await networkStream.ReadAsync(
                                              buffer.AsMemory(totalRead, responseLength - totalRead),
                                              timeoutCTS.Token
                                          ).ConfigureAwait(false);

                    if (bytesRead == 0)
                        break;

                    totalRead += bytesRead;

                }

                return DNSInfo.ReadResponse(
                           new DNSServerConfig(
                               RemoteIPAddress,
                               RemotePort ?? IPPort.DNS,
                               DNSTransport.TCP,
                               Timeout
                           ),
                           DNSQuery.TransactionId,
                           new MemoryStream(buffer, 0, totalRead),
                           Timeout,
                           stopwatch.Elapsed
                       );

            }
            catch (OperationCanceledException) when (!CancellationToken.IsCancellationRequested)
            {

                logger.LogWarning(
                    "DNS TCP fallback to {RemoteIPAddress}:{RemotePort} timed out",
                    RemoteIPAddress,
                    RemotePort
                );

                return DNSInfo.TimedOut(
                           new DNSServerConfig(
                               RemoteIPAddress,
                               RemotePort ?? IPPort.DNS
                           ),
                           DNSQuery.TransactionId,
                           Timeout
                       );

            }
            catch (SocketException se)
            {

                logger.LogWarning(
                    se,
                    "DNS TCP fallback to {RemoteIPAddress}:{RemotePort} socket error: {SocketErrorCode}",
                    RemoteIPAddress,
                    RemotePort,
                    se.SocketErrorCode
                );

                return DNSInfo.Failed(
                           new DNSServerConfig(
                               RemoteIPAddress,
                               RemotePort ?? IPPort.DNS
                           ),
                           DNSQuery.TransactionId,
                           Timeout
                       );

            }
            catch (OperationCanceledException)
            {

                // External cancellation (race-cancel or caller-initiated).
                // Silent return — not a real failure.
                return DNSInfo.Failed(
                           new DNSServerConfig(
                               RemoteIPAddress,
                               RemotePort ?? IPPort.DNS
                           ),
                           DNSQuery.TransactionId,
                           Timeout
                       );

            }
            catch (Exception e)
            {

                logger.LogError(
                    e,
                    "DNS TCP fallback to {RemoteIPAddress}:{RemotePort} failed",
                    RemoteIPAddress,
                    RemotePort
                );

                return DNSInfo.Failed(
                           new DNSServerConfig(
                               RemoteIPAddress,
                               RemotePort ?? IPPort.DNS
                           ),
                           DNSQuery.TransactionId,
                           Timeout
                       );

            }
            finally
            {
                socket?.Dispose();
            }

        }

        #endregion


        #region Google DNS

        /// <summary>
        /// Randomly select one of the Google DNS servers.
        /// </summary>
        /// <remarks>
        /// IPv6 seems to be broken sometimes!
        /// </remarks>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Google_Random(Boolean?   RecursionDesired   = null,
                                                 TimeSpan?  QueryTimeout       = null)
        {
            var all = Google_All(RecursionDesired, QueryTimeout).ToList();
            return all[Random.Shared.Next(all.Count)];
        }

        /// <summary>
        /// Randomly select one of the Google IPv4 DNS servers.
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Google_Random_IPv4(Boolean?   RecursionDesired   = null,
                                                      TimeSpan?  QueryTimeout       = null)
        {
            var all = Google_All_IPv4(RecursionDesired, QueryTimeout).ToList();
            return all[Random.Shared.Next(all.Count)];
        }

        /// <summary>
        /// Randomly select one of the Google IPv6 DNS servers.
        /// </summary>
        /// <remarks>
        /// IPv6 seems to be broken sometimes!
        /// </remarks>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Google_Random_IPv6(Boolean?   RecursionDesired   = null,
                                                      TimeSpan?  QueryTimeout       = null)
        {
            var all = Google_All_IPv6(RecursionDesired, QueryTimeout).ToList();
            return all[Random.Shared.Next(all.Count)];
        }


        /// <summary>
        /// All Google DNS servers.
        /// </summary>
        /// <remarks>
        /// IPv6 seems to be broken sometimes!
        /// </remarks>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static IEnumerable<DNSUDPClient> Google_All(Boolean?   RecursionDesired   = null,
                                                           TimeSpan?  QueryTimeout       = null)

            => [
                   Google_IPv4_1(RecursionDesired, QueryTimeout),
                   Google_IPv4_2(RecursionDesired, QueryTimeout),
                   Google_IPv6_1(RecursionDesired, QueryTimeout),
                   Google_IPv6_2(RecursionDesired, QueryTimeout)
               ];

        /// <summary>
        /// All Google IPv4 DNS servers.
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static IEnumerable<DNSUDPClient> Google_All_IPv4(Boolean?   RecursionDesired   = null,
                                                                TimeSpan?  QueryTimeout       = null)

            => [
                   Google_IPv4_1(RecursionDesired, QueryTimeout),
                   Google_IPv4_2(RecursionDesired, QueryTimeout)
               ];

        /// <summary>
        /// All Google IPv6 DNS servers.
        /// </summary>
        /// <remarks>
        /// IPv6 seems to be broken sometimes!
        /// </remarks>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static IEnumerable<DNSUDPClient> Google_All_IPv6(Boolean?   RecursionDesired   = null,
                                                                TimeSpan?  QueryTimeout       = null)

            => [
                   Google_IPv6_1(RecursionDesired, QueryTimeout),
                   Google_IPv6_2(RecursionDesired, QueryTimeout)
               ];


        /// <summary>
        /// Google DNS server 8.8.8.8
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Google_IPv4_1(Boolean?   RecursionDesired   = null,
                                                 TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv4Address.Parse("8.8.8.8"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        /// <summary>
        /// Google DNS server 8.8.4.4
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Google_IPv4_2(Boolean?   RecursionDesired   = null,
                                                 TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv4Address.Parse("8.8.4.4"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );


        /// <summary>
        /// Google DNS server 2001:4860:4860::8888
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Google_IPv6_1(Boolean?   RecursionDesired   = null,
                                                 TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv6Address.Parse("2001:4860:4860::8888"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        /// <summary>
        /// Google DNS server 2001:4860:4860::8844
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Google_IPv6_2(Boolean?   RecursionDesired   = null,
                                                 TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv6Address.Parse("2001:4860:4860::8844"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        #endregion

        #region Cloudflare DNS

        /// <summary>
        /// Randomly select one of the Cloudflare DNS servers.
        /// </summary>
        /// <remarks>
        /// IPv6 seems to be broken sometimes!
        /// </remarks>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Cloudflare_Random(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)
        {
            var all = Cloudflare_All(RecursionDesired, QueryTimeout).ToList();
            return all[Random.Shared.Next(all.Count)];
        }

        /// <summary>
        /// Randomly select one of the Cloudflare IPv4 DNS servers.
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Cloudflare_Random_IPv4(Boolean?   RecursionDesired   = null,
                                                          TimeSpan?  QueryTimeout       = null)
        {
            var all = Cloudflare_All_IPv4(RecursionDesired, QueryTimeout).ToList();
            return all[Random.Shared.Next(all.Count)];
        }

        /// <summary>
        /// Randomly select one of the Cloudflare IPv6 DNS servers.
        /// </summary>
        /// <remarks>
        /// IPv6 seems to be broken sometimes!
        /// </remarks>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Cloudflare_Random_IPv6(Boolean?   RecursionDesired   = null,
                                                          TimeSpan?  QueryTimeout       = null)
        {
            var all = Cloudflare_All_IPv6(RecursionDesired, QueryTimeout).ToList();
            return all[Random.Shared.Next(all.Count)];
        }


        /// <summary>
        /// All Cloudflare DNS servers.
        /// </summary>
        /// <remarks>
        /// IPv6 seems to be broken sometimes!
        /// </remarks>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static IEnumerable<DNSUDPClient> Cloudflare_All(Boolean?   RecursionDesired   = null,
                                                               TimeSpan?  QueryTimeout       = null)

            => [
                   Cloudflare_IPv4_1(RecursionDesired, QueryTimeout),
                   Cloudflare_IPv4_2(RecursionDesired, QueryTimeout),
                   Cloudflare_IPv4_3(RecursionDesired, QueryTimeout),
                   Cloudflare_IPv4_4(RecursionDesired, QueryTimeout),
                   Cloudflare_IPv6_1(RecursionDesired, QueryTimeout),
                   Cloudflare_IPv6_2(RecursionDesired, QueryTimeout),
                   Cloudflare_IPv6_3(RecursionDesired, QueryTimeout),
                   Cloudflare_IPv6_4(RecursionDesired, QueryTimeout)
               ];

        /// <summary>
        /// All Cloudflare IPv4 DNS servers.
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static IEnumerable<DNSUDPClient> Cloudflare_All_IPv4(Boolean?   RecursionDesired   = null,
                                                                    TimeSpan?  QueryTimeout       = null)

            => [
                   Cloudflare_IPv4_1(RecursionDesired, QueryTimeout),
                   Cloudflare_IPv4_2(RecursionDesired, QueryTimeout),
                   Cloudflare_IPv4_3(RecursionDesired, QueryTimeout),
                   Cloudflare_IPv4_4(RecursionDesired, QueryTimeout),
               ];

        /// <summary>
        /// All Cloudflare IPv6 DNS servers.
        /// </summary>
        /// <remarks>
        /// IPv6 seems to be broken sometimes!
        /// </remarks>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static IEnumerable<DNSUDPClient> Cloudflare_All_IPv6(Boolean?   RecursionDesired   = null,
                                                                    TimeSpan?  QueryTimeout       = null)

            => [
                   Cloudflare_IPv6_1(RecursionDesired, QueryTimeout),
                   Cloudflare_IPv6_2(RecursionDesired, QueryTimeout),
                   Cloudflare_IPv6_3(RecursionDesired, QueryTimeout),
                   Cloudflare_IPv6_4(RecursionDesired, QueryTimeout)
               ];


        /// <summary>
        /// Cloudflare DNS server 1.1.1.1
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Cloudflare_IPv4_1(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv4Address.Parse("1.1.1.1"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        /// <summary>
        /// Cloudflare DNS server 1.0.0.1
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Cloudflare_IPv4_2(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv4Address.Parse("1.0.0.1"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        /// <summary>
        /// Cloudflare DNS server 162.159.36.1
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Cloudflare_IPv4_3(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv4Address.Parse("162.159.36.1"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        /// <summary>
        /// Cloudflare DNS server 162.159.46.1
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Cloudflare_IPv4_4(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv4Address.Parse("162.159.46.1"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );


        /// <summary>
        /// Cloudflare DNS server 2606:4700:4700::1001
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Cloudflare_IPv6_1(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv6Address.Parse("2606:4700:4700::1001"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        /// <summary>
        /// Cloudflare DNS server 2606:4700:4700::1111
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Cloudflare_IPv6_2(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv6Address.Parse("2606:4700:4700::1111"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        /// <summary>
        /// Cloudflare DNS server 2606:4700:4700::0064
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Cloudflare_IPv6_3(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv6Address.Parse("2606:4700:4700::0064"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        /// <summary>
        /// Cloudflare DNS server 2606:4700:4700::6400
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Cloudflare_IPv6_4(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv6Address.Parse("2606:4700:4700::6400"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"Using DNS server: {RemoteIPAddress}:{RemotePort}";

        #endregion


        protected virtual void Dispose(Boolean Disposing)
        {
            if (!disposedValue)
            {
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(Disposing: true);
            GC.SuppressFinalize(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose(Disposing: true);
            GC.SuppressFinalize(this);
            return default;
        }


    }

}
