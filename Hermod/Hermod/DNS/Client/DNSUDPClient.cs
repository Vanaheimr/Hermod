/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// A DNS UDP client for a single DNS server.
    /// </summary>
    public class DNSUDPClient : IDNSClient2
    {

        #region Data

        /// <summary>
        /// The default DNS query timeout.
        /// </summary>
        public static readonly    TimeSpan                  DefaultQueryTimeout              = TimeSpan.FromSeconds(23.5);

        public static readonly    TimeSpan                  DefaultConnectTimeout            = TimeSpan.FromSeconds(5);
        public static readonly    TimeSpan                  DefaultReceiveTimeout            = TimeSpan.FromSeconds(5);
        public static readonly    TimeSpan                  DefaultSendTimeout               = TimeSpan.FromSeconds(5);
        public const              Int32                     DefaultBufferSize                = 4096;

        private Boolean disposedValue;

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
        /// The DNS query timeout.
        /// </summary>
        public TimeSpan    QueryTimeout        { get; set; }




        /// <summary>
        /// Whether the client is currently connected to the echo server.
        /// </summary>
        public Boolean      IsConnected
            => false;


        /// <summary>
        /// The local IP end point of the connected echo server.
        /// </summary>
        public IPEndPoint?  CurrentLocalEndPoint { get; private set; }

        /// <summary>
        /// The local TCP port of the connected echo server.
        /// </summary>
        public UInt16?      CurrentLocalPort

            => CurrentLocalEndPoint is not null
                   ? (UInt16) CurrentLocalEndPoint.Port
                   : null;

        /// <summary>
        /// The local IP address of the connected echo server.
        /// </summary>
        public IIPAddress?  CurrentLocalIPAddress

            => CurrentLocalEndPoint is not null
                   ? IPAddress.Parse(CurrentLocalEndPoint.Address.GetAddressBytes())
                   : null;


        /// <summary>
        /// The remote IP end point of the connected echo server.
        /// </summary>
        public IPEndPoint?  CurrentRemoteEndPoint { get; private set; }

        /// <summary>
        /// The remote TCP port of the connected echo server.
        /// </summary>
        public UInt16?      CurrentRemotePort

            => CurrentRemoteEndPoint is not null
                   ? (UInt16) CurrentRemoteEndPoint.Port
                   : null;

        /// <summary>
        /// The remote IP address of the connected echo server.
        /// </summary>
        public IIPAddress?  CurrentRemoteIPAddress

            => CurrentRemoteEndPoint is not null
                   ? IPAddress.Parse(CurrentRemoteEndPoint.Address.GetAddressBytes())
                   : null;

        public  URL?                     RemoteURL          { get; }
        public  TimeSpan                 ConnectTimeout     { get; }
        public  TimeSpan                 ReceiveTimeout     { get; }
        public  TimeSpan                 SendTimeout        { get; }
        public  UInt32                   BufferSize         { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new DNS UDP client for the given DNS server.
        /// </summary>
        /// <param name="IPAddress">The IP address of the DNS server to query.</param>
        /// <param name="Port">The UDP port of the DNS server to query.</param>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">An optional DNS query timeout. Default is 23.5 seconds.</param>
        public DNSUDPClient(IIPAddress               IPAddress,
                            IPPort?                  Port               = null,
                            Boolean?                 RecursionDesired   = null,
                            TimeSpan?                QueryTimeout       = null,

                            TimeSpan?                ConnectTimeout     = null,
                            TimeSpan?                ReceiveTimeout     = null,
                            TimeSpan?                SendTimeout        = null,
                            UInt32?                  BufferSize         = null,

                            TCPEchoLoggingDelegate?  LoggingHandler     = null)

        {

            this.RemoteIPAddress   = IPAddress;
            this.RemotePort        = Port             ?? IPPort.DNS;
            this.RecursionDesired  = RecursionDesired ?? true;
            this.QueryTimeout      = QueryTimeout     ?? DefaultQueryTimeout;


            if (ConnectTimeout.HasValue && ConnectTimeout.Value.TotalMilliseconds > Int32.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(ConnectTimeout), "Timeout too large for socket.");

            if (ReceiveTimeout.HasValue && ReceiveTimeout.Value.TotalMilliseconds > Int32.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(ReceiveTimeout), "Timeout too large for socket.");

            if (SendTimeout.   HasValue && SendTimeout.   Value.TotalMilliseconds > Int32.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(SendTimeout),    "Timeout too large for socket.");

            this.BufferSize       = BufferSize.HasValue
                                        ? BufferSize.Value > Int32.MaxValue
                                              ? throw new ArgumentOutOfRangeException(nameof(BufferSize), "The buffer size must not exceed Int32.MaxValue!")
                                              : BufferSize.Value
                                        : DefaultBufferSize;
            this.ConnectTimeout   = ConnectTimeout ?? DefaultConnectTimeout;
            this.ReceiveTimeout   = ReceiveTimeout ?? DefaultReceiveTimeout;
            this.SendTimeout      = SendTimeout    ?? DefaultSendTimeout;
       //     this.loggingHandler   = LoggingHandler;


        }

        #endregion


        #region Query (DomainName,     ResourceRecordTypes, RecursionDesired = true, BypassCache = false, ...)

        public Task<DNSInfo> Query(DomainName                           DomainName,
                                   IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                   Boolean?                             RecursionDesired    = true,
                                   Boolean?                             BypassCache         = false,
                                   CancellationToken                    CancellationToken   = default)

            => Query(
                   DNSServiceName.Parse(DomainName.FullName),
                   ResourceRecordTypes,
                   RecursionDesired,
                   BypassCache,
                   CancellationToken
               );

        #endregion

        #region Query (DNSServiceName, ResourceRecordTypes, RecursionDesired = true, BypassCache = false, ...)

        public async Task<DNSInfo> Query(DNSServiceName                       DNSServiceName,
                                         IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                         Boolean?                             RecursionDesired    = true,
                                         Boolean?                             BypassCache         = false,
                                         CancellationToken                    CancellationToken   = default)
        {

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
                           Timeout:                QueryTimeout
                       );

            var resourceRecordTypes = ResourceRecordTypes.ToList();

            if (resourceRecordTypes.Count == 0)
                resourceRecordTypes = [ DNSResourceRecordTypes.Any ];

            #endregion


            var dnsQuery = DNSPacket.Query(
                               DNSServiceName,
                               this.RecursionDesired ?? RecursionDesired ?? true,
                               [.. resourceRecordTypes]
                           );

            #region Query all DNS server(s) in parallel...

            var data = new Byte[512];
            Int32  length;
            Socket? socket = null;

            try
            {

                var serverAddress      = System.Net.IPAddress.Parse(RemoteIPAddress.ToString());
                CurrentRemoteEndPoint  = new IPEndPoint(serverAddress, RemotePort.Value.ToInt32());
                var endPoint           = (EndPoint) CurrentRemoteEndPoint;
                socket                 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout,    (Int32) QueryTimeout.TotalMilliseconds);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, (Int32) QueryTimeout.TotalMilliseconds);
                socket.Connect(endPoint);

                CurrentLocalEndPoint   = endPoint as IPEndPoint;

                var ms = new MemoryStream();
                dnsQuery.Serialize(ms, false, []);

                socket.SendTo(ms.ToArray(), endPoint);

                length = socket.ReceiveFrom(data, ref endPoint);

            }
            catch (SocketException se)
            {

                if (se.SocketErrorCode == SocketError.AddressFamilyNotSupported)
                    return new DNSInfo(
                                Origin:                 new DNSServerConfig(
                                                            RemoteIPAddress,
                                                            RemotePort.Value
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
                                Timeout:                QueryTimeout
                            );

                // A SocketException might be thrown after the timeout was reached!
                //throw new Exception("DNS server '" + DNSServer + "' did not respond within " + QueryTimeout.TotalSeconds + " seconds!");
                return DNSInfo.TimedOut(
                            new DNSServerConfig(
                                RemoteIPAddress,
                                RemotePort.Value
                            ),
                            dnsQuery.TransactionId,
                            QueryTimeout
                        );

            }
            catch
            {
                // A SocketException might be thrown after the timeout was reached!
                //throw new Exception("DNS server '" + DNSServer + "' did not respond within " + QueryTimeout.TotalSeconds + " seconds!");
                return DNSInfo.TimedOut(
                            new DNSServerConfig(
                                RemoteIPAddress,
                                RemotePort.Value
                            ),
                            dnsQuery.TransactionId,
                            QueryTimeout
                        );
            }
            finally
            {
                socket?.Shutdown(SocketShutdown.Both);
            }

            return DNSInfo.ReadResponse(
                        new DNSServerConfig(
                            RemoteIPAddress!,
                            RemotePort.Value,
                            DNSTransport.UDP,
                            QueryTimeout
                        ),
                        dnsQuery.TransactionId,
                        new MemoryStream(data)
                    );

            #endregion

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

            => Google_All(RecursionDesired, QueryTimeout).
                     Skip(Random.Shared.Next(0, 4)).
                    First();

        /// <summary>
        /// Randomly select one of the Google IPv4 DNS servers.
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Google_Random_IPv4(Boolean?   RecursionDesired   = null,
                                                      TimeSpan?  QueryTimeout       = null)

            => Google_All_IPv4(RecursionDesired, QueryTimeout).
                          Skip(Random.Shared.Next(0, 2)).
                         First();

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

            => Google_All_IPv6(RecursionDesired, QueryTimeout).
                          Skip(Random.Shared.Next(0, 2)).
                         First();


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

            => Cloudflare_All(RecursionDesired, QueryTimeout).
                         Skip(Random.Shared.Next(0, 8)).
                        First();

        /// <summary>
        /// Randomly select one of the Cloudflare IPv4 DNS servers.
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Cloudflare_Random_IPv4(Boolean?   RecursionDesired   = null,
                                                          TimeSpan?  QueryTimeout       = null)

            => Cloudflare_All_IPv4(RecursionDesired, QueryTimeout).
                              Skip(Random.Shared.Next(0, 4)).
                             First();

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

            => Cloudflare_All_IPv6(RecursionDesired, QueryTimeout).
                              Skip(Random.Shared.Next(0, 4)).
                             First();


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
            Dispose(Disposing: false);
            GC.SuppressFinalize(this);
            return default;
        }


    }

}
