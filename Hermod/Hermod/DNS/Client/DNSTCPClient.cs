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

using System.Diagnostics;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// A DNS TCP client for a single DNS server.
    /// Requests will be pipelined when the server supports it.
    /// </summary>
    public class DNSTCPClient : ATCPTestClient,
                                IDNSClient
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
        /// Whether DNS recursion is desired.
        /// </summary>
        public Boolean?  RecursionDesired    { get; set; }

        /// <summary>
        /// The DNS query timeout.
        /// </summary>
        public TimeSpan  QueryTimeout        { get; set; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new DNS TCP client for the given DNS server.
        /// </summary>
        /// <param name="IPAddress">The DNS server to query.</param>
        public DNSTCPClient(IIPAddress               IPAddress,
                            IPPort?                  Port               = null,
                            Boolean?                 RecursionDesired   = null,
                            TimeSpan?                QueryTimeout       = null,
                            TimeSpan?                ConnectTimeout     = null,
                            TimeSpan?                ReceiveTimeout     = null,
                            TimeSpan?                SendTimeout        = null,
                            UInt32?                  BufferSize         = null,
                            TCPEchoLoggingDelegate?  LoggingHandler     = null)

            : base(IPAddress,
                   Port ?? IPPort.DNS,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   BufferSize ?? 512,
                   LoggingHandler)

        {

            this.RecursionDesired  = RecursionDesired ?? true;
            this.QueryTimeout      = QueryTimeout     ?? TimeSpan.FromSeconds(23.5);

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

            var resourceRecordTypes = ResourceRecordTypes.ToList();

            if (resourceRecordTypes.Count == 0)
                resourceRecordTypes = [ DNSResourceRecordTypes.Any ];

            #endregion


            var dnsQuery = DNSPacket.Query(
                               DNSServiceName,
                               this.RecursionDesired ?? RecursionDesired,
                               [.. resourceRecordTypes]
                           );


            var ms = new MemoryStream();
            ms.WriteByte(0);
            ms.WriteByte(0);
            dnsQuery.Serialize(ms, false, []);

            if (!IsConnected || tcpClient is null)
                await reconnectAsync().ConfigureAwait(false);

            var data        = ms.ToArray();
            var dataLength  = data.Length-2;
            data[0] = (Byte) (dataLength >> 8);
            data[1] = (Byte) (dataLength & 0xFF);

            #region TCP

            try
            {

                var stopwatch = Stopwatch.StartNew();
                var tcpStream = tcpClient.GetStream();
                cts ??= new CancellationTokenSource();

                // Send the data
                await tcpStream.WriteAsync(data, cts.Token).ConfigureAwait(false);
                await tcpStream.FlushAsync(cts.Token).      ConfigureAwait(false);

                var responseLength = tcpStream.ReadUInt16BE();

                using var responseStream = new MemoryStream();
                var buffer    = new Byte[responseLength];
                var bytesRead = await tcpStream.ReadAsync(buffer, cts.Token).ConfigureAwait(false);

                stopwatch.Stop();


                var dnsInfo  = DNSInfo.ReadResponse(
                                   new DNSServerConfig(
                                       RemoteIPAddress!,
                                       RemoteTCPPort ?? IPPort.DNS,
                                       DNSTransport.TCP,
                                       QueryTimeout
                                   ),
                                   dnsQuery.TransactionId,
                                   new MemoryStream(buffer)
                               );

                return dnsInfo;

            }
            catch (Exception ex)
            {
                await Log($"Error in SendBinary: {ex.Message}");
                return null;
            }

            #endregion

        }

        #endregion


        #region Google DNS

        public static DNSTCPClient Google_IPv4_1(Boolean?   RecursionDesired   = null,
                                                 TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv4Address.Parse("8.8.8.8"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        public static DNSTCPClient Google_IPv4_2(Boolean?   RecursionDesired   = null,
                                                 TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv4Address.Parse("8.8.4.4"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        public static DNSTCPClient Google_IPv6_1(Boolean?   RecursionDesired   = null,
                                                 TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv6Address.Parse("2001:4860:4860::8888"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        public static DNSTCPClient Google_IPv6_2(Boolean?   RecursionDesired   = null,
                                                 TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv6Address.Parse("2001:4860:4860::8844"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        #endregion

        #region Cloudflare DNS

        public static DNSTCPClient Cloudflare_IPv4_1(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv4Address.Parse("1.1.1.1"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        public static DNSTCPClient Cloudflare_IPv4_2(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv4Address.Parse("1.0.0.1"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        public static DNSTCPClient Cloudflare_IPv4_3(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv4Address.Parse("162.159.36.1"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        public static DNSTCPClient Cloudflare_IPv4_4(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv4Address.Parse("162.159.46.1"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        public static DNSTCPClient Cloudflare_IPv6_1(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv6Address.Parse("2606:4700:4700::1001"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        public static DNSTCPClient Cloudflare_IPv6_2(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv6Address.Parse("2606:4700:4700::1111"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        public static DNSTCPClient Cloudflare_IPv6_3(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv6Address.Parse("2606:4700:4700::0064"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout
               );

        public static DNSTCPClient Cloudflare_IPv6_4(Boolean?   RecursionDesired   = null,
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

            => $"Using DNS server: {RemoteIPAddress}:{RemoteTCPPort}";

        #endregion


        protected virtual void Dispose(Boolean Disposing)
        {

            base.Dispose();

            if (!disposedValue)
                disposedValue = true;

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
