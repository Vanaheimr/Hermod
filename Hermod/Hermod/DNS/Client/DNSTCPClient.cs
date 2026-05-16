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

using System.Diagnostics;
using System.Net.Sockets;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// A DNS TCP client for a single DNS server.
    /// Reuses the TCP connection across queries; concurrent callers
    /// are serialized via an internal semaphore.
    /// </summary>
    public class DNSTCPClient : ATCPClient,
                                IDNSClientWithTransport
    {

        #region Data

        /// <summary>
        /// The default DNS query timeout.
        /// </summary>
        public static readonly TimeSpan DefaultQueryTimeout = TimeSpan.FromSeconds(23.5);

        private readonly SemaphoreSlim tcpStreamLock = new(1, 1);

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

        /// <summary>
        /// Optional EDNS0 options to include in every DNS query.
        /// </summary>
        public List<EDNSOption>  EDNSOptions  { get; } = [];

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new DNS TCP client for the given DNS server.
        /// </summary>
        /// <param name="IPAddress">The DNS server to query.</param>
        public DNSTCPClient(IIPAddress                       IPAddress,
                            IPPort?                          Port                     = null,
                            I18NString?                      Description              = null,
                            Boolean?                         RecursionDesired         = null,
                            TimeSpan?                        QueryTimeout             = null,

                            IPVersionPreference?             PreferIPv4               = null,
                            TimeSpan?                        ConnectTimeout           = null,
                            TimeSpan?                        ReceiveTimeout           = null,
                            TimeSpan?                        SendTimeout              = null,
                            TransmissionRetryDelayDelegate?  TransmissionRetryDelay   = null,
                            UInt16?                          MaxNumberOfRetries       = null,
                            UInt32?                          BufferSize               = null)

            : base(IPAddress,
                   Port ?? IPPort.DNS,
                   Description,

                   PreferIPv4,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize ?? 4096)

        {

            this.RecursionDesired  = RecursionDesired ?? true;
            this.QueryTimeout      = QueryTimeout     ?? TimeSpan.FromSeconds(23.5);

        }

        #endregion


        #region Query (DomainName,     ResourceRecordTypes, Timeout = null, RecursionDesired = true, BypassCache = false, ...)

        public Task<DNSInfo> Query(DomainName                           DomainName,
                                   IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                   TimeSpan?                            Timeout             = null,
                                   Boolean?                             RecursionDesired    = true,
                                   Boolean?                             BypassCache         = false,
                                   CancellationToken                    CancellationToken   = default)

            => Query(
                   DNSServiceName.Parse(DomainName.FullName),
                   ResourceRecordTypes,
                   Timeout,
                   RecursionDesired,
                   BypassCache,
                   CancellationToken
               );

        #endregion

        #region Query (DNSServiceName, ResourceRecordTypes, Timeout = null, RecursionDesired = true, BypassCache = false, ...)

        public async Task<DNSInfo> Query(DNSServiceName                       DNSServiceName,
                                         IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                         TimeSpan?                            Timeout             = null,
                                         Boolean?                             RecursionDesired    = true,
                                         Boolean?                             BypassCache         = false,
                                         CancellationToken                    CancellationToken   = default)
        {

            #region Initial checks

            var resourceRecordTypes = ResourceRecordTypes.ToList();

            if (resourceRecordTypes.Count == 0)
                resourceRecordTypes = [ DNSResourceRecordTypes.Any ];

            #endregion


            var dnsQuery = DNSPacket.Query(
                               DNSServiceName,
                               0,
                               this.RecursionDesired ?? RecursionDesired ?? true,
                               EDNSOptions.Count > 0 ? EDNSOptions : null,
                               [.. resourceRecordTypes]
                           );


            Byte[] data;

            using (var ms = new MemoryStream())
            {
                ms.WriteByte(0);
                ms.WriteByte(0);
                dnsQuery.Serialize(ms, false, []);
                data = ms.ToArray();
            }

            var dataLength  = data.Length - 2;
            data[0] = (Byte) (dataLength >> 8);
            data[1] = (Byte) (dataLength & 0xFF);

            #region TCP (serialized via semaphore, auto-reconnect on broken connection)

            var effectiveTimeout = Timeout ?? QueryTimeout;

            await tcpStreamLock.WaitAsync(CancellationToken).
                                ConfigureAwait(false);

            try
            {

                if (!IsConnected || tcpClient is null)
                    await ReconnectAsync(CancellationToken).
                              ConfigureAwait(false);

                var stopwatch = Stopwatch.StartNew();
                var tcpStream = tcpClient!.GetStream();
                clientCancellationTokenSource ??= new CancellationTokenSource();

                using var timeoutCTS = CancellationTokenSource.CreateLinkedTokenSource(
                                           clientCancellationTokenSource.Token,
                                           CancellationToken
                                       );
                timeoutCTS.CancelAfter(effectiveTimeout);

                try
                {
                    return await SendAndReceiveTCPAsync(tcpStream, data, dnsQuery, effectiveTimeout, timeoutCTS.Token).
                                     ConfigureAwait(false);
                }
                catch (IOException)
                {
                    await ReconnectAsync(CancellationToken).ConfigureAwait(false);
                    tcpStream = tcpClient!.GetStream();
                    return await SendAndReceiveTCPAsync(tcpStream, data, dnsQuery, effectiveTimeout, timeoutCTS.Token).
                                     ConfigureAwait(false);
                }

            }
            catch (OperationCanceledException) when (!CancellationToken.IsCancellationRequested)
            {

                return DNSInfo.TimedOut(
                           new DNSServerConfig(
                               RemoteIPAddress!,
                               RemotePort ?? IPPort.DNS
                           ),
                           dnsQuery.TransactionId,
                           effectiveTimeout
                       );

            }
            catch (Exception ex)
            {

                await Log($"Error in SendBinary: {ex.Message}");

                return DNSInfo.TimedOut(
                           new DNSServerConfig(
                               RemoteIPAddress!,
                               RemotePort ?? IPPort.DNS
                           ),
                           dnsQuery.TransactionId,
                           effectiveTimeout
                       );

            }
            finally
            {
                tcpStreamLock.Release();
            }

            #endregion

        }

        #endregion

        #region (private) SendAndReceiveTCPAsync(...)

        /// <summary>
        /// Send a DNS query over the TCP stream and read the response.
        /// Extracted so that the IOException retry logic covers both write and read.
        /// </summary>
        private async Task<DNSInfo> SendAndReceiveTCPAsync(NetworkStream      TCPStream,
                                                           Byte[]             Data,
                                                           DNSPacket          DNSQuery,
                                                           TimeSpan           EffectiveTimeout,
                                                           CancellationToken  CancellationToken)
        {

            await TCPStream.WriteAsync(Data, CancellationToken).ConfigureAwait(false);
            await TCPStream.FlushAsync(CancellationToken).      ConfigureAwait(false);

            var responseLength = await TCPStream.ReadUInt16BEAsync(CancellationToken).
                                                 ConfigureAwait(false);

            var buffer    = new Byte[responseLength];
            var totalRead = 0;

            while (totalRead < responseLength)
            {

                var bytesRead = await TCPStream.ReadAsync(
                                          buffer.AsMemory(totalRead, responseLength - totalRead),
                                          CancellationToken
                                      ).ConfigureAwait(false);

                if (bytesRead == 0)
                    break;

                totalRead += bytesRead;

            }

            return DNSInfo.ReadResponse(
                       new DNSServerConfig(
                           RemoteIPAddress!,
                           RemotePort ?? IPPort.DNS,
                           DNSTransport.TCP,
                           EffectiveTimeout
                       ),
                       DNSQuery.TransactionId,
                       new MemoryStream(buffer, 0, totalRead)
                   );

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
        public static DNSTCPClient Google_Random(Boolean?   RecursionDesired   = null,
                                                 TimeSpan?  QueryTimeout       = null)

            => Google_All(RecursionDesired, QueryTimeout).
                     Skip(Random.Shared.Next(0, 4)).
                    First();

        /// <summary>
        /// Randomly select one of the Google IPv4 DNS servers.
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSTCPClient Google_Random_IPv4(Boolean?   RecursionDesired   = null,
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
        public static DNSTCPClient Google_Random_IPv6(Boolean?   RecursionDesired   = null,
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
        public static IEnumerable<DNSTCPClient> Google_All(Boolean?   RecursionDesired   = null,
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
        public static IEnumerable<DNSTCPClient> Google_All_IPv4(Boolean?   RecursionDesired   = null,
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
        public static IEnumerable<DNSTCPClient> Google_All_IPv6(Boolean?   RecursionDesired   = null,
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
        public static DNSTCPClient Google_IPv4_1(Boolean?   RecursionDesired   = null,
                                                 TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv4Address.Parse("8.8.8.8"),
                   IPPort.DNS,
                   I18NString.Create("Google (8.8.8.8)"),
                   RecursionDesired,
                   QueryTimeout
               );

        /// <summary>
        /// Google DNS server 8.8.4.4
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSTCPClient Google_IPv4_2(Boolean?   RecursionDesired   = null,
                                                 TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv4Address.Parse("8.8.4.4"),
                   IPPort.DNS,
                   I18NString.Create("Google (8.8.4.4)"),
                   RecursionDesired,
                   QueryTimeout
               );


        /// <summary>
        /// Google DNS server 2001:4860:4860::8888
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSTCPClient Google_IPv6_1(Boolean?   RecursionDesired   = null,
                                                 TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv6Address.Parse("2001:4860:4860::8888"),
                   IPPort.DNS,
                   I18NString.Create("Google (2001:4860:4860::8888)"),
                   RecursionDesired,
                   QueryTimeout
               );

        /// <summary>
        /// Google DNS server 2001:4860:4860::8844
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSTCPClient Google_IPv6_2(Boolean?   RecursionDesired   = null,
                                                 TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv6Address.Parse("2001:4860:4860::8844"),
                   IPPort.DNS,
                   I18NString.Create("Google (2001:4860:4860::8844)"),
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
        public static DNSTCPClient Cloudflare_Random(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => Cloudflare_All(RecursionDesired, QueryTimeout).
                         Skip(Random.Shared.Next(0, 8)).
                        First();

        /// <summary>
        /// Randomly select one of the Cloudflare IPv4 DNS servers.
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSTCPClient Cloudflare_Random_IPv4(Boolean?   RecursionDesired   = null,
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
        public static DNSTCPClient Cloudflare_Random_IPv6(Boolean?   RecursionDesired   = null,
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
        public static IEnumerable<DNSTCPClient> Cloudflare_All(Boolean?   RecursionDesired   = null,
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
        public static IEnumerable<DNSTCPClient> Cloudflare_All_IPv4(Boolean?   RecursionDesired   = null,
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
        public static IEnumerable<DNSTCPClient> Cloudflare_All_IPv6(Boolean?   RecursionDesired   = null,
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
        public static DNSTCPClient Cloudflare_IPv4_1(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv4Address.Parse("1.1.1.1"),
                   IPPort.DNS,
                   I18NString.Create("Cloudflare (1.1.1.1)"),
                   RecursionDesired,
                   QueryTimeout
               );

        /// <summary>
        /// Cloudflare DNS server 1.0.0.1
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSTCPClient Cloudflare_IPv4_2(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv4Address.Parse("1.0.0.1"),
                   IPPort.DNS,
                   I18NString.Create("Cloudflare (1.0.0.1)"),
                   RecursionDesired,
                   QueryTimeout
               );

        /// <summary>
        /// Cloudflare DNS server 162.159.36.1
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSTCPClient Cloudflare_IPv4_3(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv4Address.Parse("162.159.36.1"),
                   IPPort.DNS,
                   I18NString.Create("Cloudflare (162.159.36.1)"),
                   RecursionDesired,
                   QueryTimeout
               );

        /// <summary>
        /// Cloudflare DNS server 162.159.46.1
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSTCPClient Cloudflare_IPv4_4(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv4Address.Parse("162.159.46.1"),
                   IPPort.DNS,
                   I18NString.Create("Cloudflare (162.159.46.1)"),
                   RecursionDesired,
                   QueryTimeout
               );


        /// <summary>
        /// Cloudflare DNS server 2606:4700:4700::1001
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSTCPClient Cloudflare_IPv6_1(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv6Address.Parse("2606:4700:4700::1001"),
                   IPPort.DNS,
                   I18NString.Create("Cloudflare (2606:4700:4700::1001)"),
                   RecursionDesired,
                   QueryTimeout
               );

        /// <summary>
        /// Cloudflare DNS server 2606:4700:4700::1111
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSTCPClient Cloudflare_IPv6_2(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv6Address.Parse("2606:4700:4700::1111"),
                   IPPort.DNS,
                   I18NString.Create("Cloudflare (2606:4700:4700::1111)"),
                   RecursionDesired,
                   QueryTimeout
               );

        /// <summary>
        /// Cloudflare DNS server 2606:4700:4700::0064
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSTCPClient Cloudflare_IPv6_3(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv6Address.Parse("2606:4700:4700::0064"),
                   IPPort.DNS,
                   I18NString.Create("Cloudflare (2606:4700:4700::0064)"),
                   RecursionDesired,
                   QueryTimeout
               );

        /// <summary>
        /// Cloudflare DNS server 2606:4700:4700::6400
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSTCPClient Cloudflare_IPv6_4(Boolean?   RecursionDesired   = null,
                                                     TimeSpan?  QueryTimeout       = null)

            => new (
                   IPv6Address.Parse("2606:4700:4700::6400"),
                   IPPort.DNS,
                   I18NString.Create("Cloudflare (2606:4700:4700::6400)"),
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


        public override async ValueTask DisposeAsync()
        {
            tcpStreamLock.Dispose();
            await base.DisposeAsync();
        }


    }

}
