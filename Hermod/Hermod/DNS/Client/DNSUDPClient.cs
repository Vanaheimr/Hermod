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
using System.Net.NetworkInformation;
using System.Net.Sockets;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// A DNS UDP client for a single DNS server.
    /// </summary>
    public class DNSUDPClient : IDNSClient
    {

        #region Data

        /// <summary>
        /// The default DNS query timeout.
        /// </summary>
        public static readonly TimeSpan DefaultQueryTimeout = TimeSpan.FromSeconds(23.5);

        private Boolean disposedValue;

        #endregion

        #region Properties

        /// <summary>
        /// The IP address of the DNS server to query.
        /// </summary>
        public IIPAddress  IPAddress           { get; }

        /// <summary>
        /// The UDP port of the DNS server to query.
        /// </summary>
        public IPPort      Port                { get; }

        /// <summary>
        /// Whether DNS recursion is desired.
        /// </summary>
        public Boolean?    RecursionDesired    { get; set; }

        /// <summary>
        /// The DNS query timeout.
        /// </summary>
        public TimeSpan    QueryTimeout        { get; set; }

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
                            TCPEchoLoggingDelegate?  LoggingHandler     = null)

        {

            this.IPAddress         = IPAddress;
            this.Port              = Port             ?? IPPort.DNS;
            this.RecursionDesired  = RecursionDesired ?? true;
            this.QueryTimeout      = QueryTimeout     ?? DefaultQueryTimeout;

        }

        #endregion


        #region Query (DomainName,     ResourceRecordTypes, RecursionDesired = true, ...)

        public Task<DNSInfo> Query(DomainName                           DomainName,
                                   IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                   Boolean                              RecursionDesired    = true,
                                   CancellationToken                    CancellationToken   = default)

            => Query(
                   DNSServiceName.Parse(DomainName.FullName),
                   ResourceRecordTypes,
                   RecursionDesired,
                   CancellationToken
               );

        #endregion

        #region Query (DNSServiceName, ResourceRecordTypes, RecursionDesired = true, ...)

        public async Task<DNSInfo> Query(DNSServiceName                       DNSServiceName,
                                         IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                         Boolean                              RecursionDesired    = true,
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
                               this.RecursionDesired ?? RecursionDesired,
                               [.. resourceRecordTypes]
                           );

            #region Query all DNS server(s) in parallel...

            var data = new Byte[512];
            Int32  length;
            Socket? socket = null;

            try
            {

                var serverAddress  = System.Net.IPAddress.Parse(IPAddress.ToString());
                var endPoint       = (EndPoint) new IPEndPoint(serverAddress, Port.ToInt32());
                socket             = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout,    (Int32) QueryTimeout.TotalMilliseconds);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, (Int32) QueryTimeout.TotalMilliseconds);
                socket.Connect(endPoint);

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
                                                            IPAddress,
                                                            Port
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
                                IPAddress,
                                Port
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
                                IPAddress,
                                Port
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
                            IPAddress!,
                            Port,
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

        public static DNSUDPClient Google_IPv4_1(Boolean?   RecursionDesired   = null,
                                                 TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv4Address.Parse("8.8.8.8"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        public static DNSUDPClient Google_IPv4_2(Boolean?   RecursionDesired   = null,
                                                 TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv4Address.Parse("8.8.4.4"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        public static DNSUDPClient Google_IPv6_1(Boolean?   RecursionDesired   = null,
                                                 TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv6Address.Parse("2001:4860:4860::8888"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

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

        public static DNSUDPClient Cloudflare_IPv4_1(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv4Address.Parse("1.1.1.1"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        public static DNSUDPClient Cloudflare_IPv4_2(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv4Address.Parse("1.0.0.1"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        public static DNSUDPClient Cloudflare_IPv4_3(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv4Address.Parse("162.159.36.1"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        public static DNSUDPClient Cloudflare_IPv4_4(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv4Address.Parse("162.159.46.1"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        public static DNSUDPClient Cloudflare_IPv6_1(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv6Address.Parse("2606:4700:4700::1001"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        public static DNSUDPClient Cloudflare_IPv6_2(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv6Address.Parse("2606:4700:4700::1111"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        public static DNSUDPClient Cloudflare_IPv6_3(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv6Address.Parse("2606:4700:4700::0064"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

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

            => $"Using DNS server: {IPAddress}:{Port}";

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
