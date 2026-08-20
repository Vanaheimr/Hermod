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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
        /// The EDNS(0) payload size this client advertises (RFC 6891 §6.2.3).
        /// Set to 0 to send no OPT record at all, which announces no EDNS(0)
        /// support.
        /// </summary>
        /// <remarks>
        /// The number itself is a UDP reassembly limit and has nothing to do on
        /// a stream transport. What the field decides here is whether there is
        /// an OPT record at all: <see cref="DNSPacket.Query"/> omits the record
        /// entirely when this is 0, and that record is the only place
        /// <see cref="DnssecOK"/> can travel - RFC 3225 §3 puts the DO bit in
        /// the OPT flags - and the only place <see cref="EDNSOptions"/> can.
        /// Padding is deliberately not applied: RFC 8467 §4.1's recommendation
        /// "only applies if the DNS transport is encrypted", and this transport
        /// is not.
        /// </remarks>
        public UInt16            UDPPayloadSize            { get; set; } = DNSPacket.DefaultUDPPayloadSize;

        /// <summary>
        /// Optional EDNS0 options to include in every DNS query.
        /// </summary>
        public List<EDNSOption>  EDNSOptions  { get; } = [];

        /// <summary>
        /// Where this client says what went wrong, as
        /// <see cref="DNSHTTPSClient"/> has always had one. The base class keeps
        /// its own logger private, so a failure down here had nowhere to go -
        /// which is how a cancellation reported as a timeout stayed unexplained.
        /// </summary>
        private readonly ILogger<DNSTCPClient>  logger;

        /// <inheritdoc />
        public Boolean           DnssecOK     { get; set; }

        /// <summary>
        /// The server-advertised idle timeout from the last EDNS TCP Keepalive
        /// response option (RFC 7828). Null if no keepalive option was received.
        /// The connection should be closed after this duration of inactivity.
        /// </summary>
        public TimeSpan?  ServerKeepaliveTimeout    { get; private set; }


        /// <summary>
        /// Where an answer from this client came from.
        /// </summary>
        /// <remarks>
        /// The address when there is one - the one actually connected, or the one
        /// this client was given - and the resolver's name when there is not.
        /// Every one of these used to be written as RemoteIPAddress!, putting a
        /// null into a field which did not admit one.
        /// </remarks>
        private DNSServerConfig OriginOf(TimeSpan? QueryTimeout = null)
        {

            var ipAddress   = CurrentRemoteIPAddress ?? RemoteIPAddress ?? RemoteURL.Host.IPAddress;
            var domainName  = RemoteURL.Host.DomainName;

            // A URL host is one or the other, so this is the name whenever there
            // is no address at all.
            return ipAddress is not null

                       ? new DNSServerConfig(
                             ipAddress,
                             RemotePort ?? IPPort.DNS,
                             DNSTransport.TCP,
                             QueryTimeout
                         )

                       : new DNSServerConfig(
                             domainName!,
                             RemotePort ?? IPPort.DNS,
                             DNSTransport.TCP,
                             QueryTimeout
                         );

        }

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
                            UInt32?                          BufferSize               = null,
                            ILoggerFactory?                  LoggerFactory            = null)

            : base(IPAddress,
                   Port ?? IPPort.DNS,
                   Description,

                   PreferIPv4,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize ?? 4096,
                   LoggerFactory: LoggerFactory)

        {

            this.RecursionDesired  = RecursionDesired ?? true;
            this.QueryTimeout      = QueryTimeout     ?? TimeSpan.FromSeconds(23.5);
            this.logger            = (LoggerFactory ?? NullLoggerFactory.Instance).CreateLogger<DNSTCPClient>();

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

            #region Initial checks

            var resourceRecordTypes = ResourceRecordTypes.ToList();

            if (resourceRecordTypes.Count == 0)
                resourceRecordTypes = [ DNSResourceRecordTypes.Any ];

            #endregion


            var dnsQuery = DNSPacket.Query(
                               DNSServiceName,
                               UDPPayloadSize,
                               this.RecursionDesired ?? RecursionDesired ?? true,
                               this.DnssecOK,
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

                // LiveClient..., not ??= - see DNSHTTPSClient for what a spent
                // token source costs here.
                using var timeoutCTS = CancellationTokenSource.CreateLinkedTokenSource(
                                           LiveClientCancellationTokenSource.Token,
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
            catch (OperationCanceledException ocx) when (!CancellationToken.IsCancellationRequested)
            {

                // A cancellation from elsewhere is not a timeout - see
                // DNSHTTPSClient for what calling it one silently cost.
                if (clientCancellationTokenSource?.IsCancellationRequested == true)
                {

                    logger.LogWarning(
                        ocx,
                        "DNS TCP query to {RemoteIPAddress}:{RemotePort} was cancelled by the client - it did not time out",
                        RemoteIPAddress,
                        RemotePort
                    );

                    return DNSInfo.Failed(
                               OriginOf(),
                               dnsQuery.TransactionId,
                               effectiveTimeout
                           );

                }

                logger.LogWarning(
                    "DNS TCP query to {RemoteIPAddress}:{RemotePort} timed out after {Timeout}s",
                    RemoteIPAddress,
                    RemotePort,
                    effectiveTimeout.TotalSeconds
                );

                return DNSInfo.TimedOut(
                           OriginOf(),
                           dnsQuery.TransactionId,
                           effectiveTimeout
                       );

            }
            catch (SocketException se)
            {

                await Log($"DNS TCP query to {RemoteIPAddress}:{RemotePort} socket error: {se.SocketErrorCode} — {se.Message}");

                return DNSInfo.Failed(
                           OriginOf(),
                           dnsQuery.TransactionId,
                           effectiveTimeout
                       );

            }
            catch (OperationCanceledException)
            {

                // External cancellation (race-cancel or caller-initiated).
                // Silent return — not a real failure.
                return DNSInfo.Failed(
                           OriginOf(),
                           dnsQuery.TransactionId,
                           effectiveTimeout
                       );

            }
            catch (Exception ex)
            {

                await Log($"DNS TCP query to {RemoteIPAddress}:{RemotePort} failed: [{ex.GetType().Name}] {ex.Message}");

                return DNSInfo.Failed(
                           OriginOf(),
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

            var stopwatch = Stopwatch.StartNew();

            await TCPStream.WriteAsync(Data, CancellationToken).ConfigureAwait(false);
            await TCPStream.FlushAsync(CancellationToken).      ConfigureAwait(false);

            var responseLength = await TCPStream.ReadUInt16BEAsync(CancellationToken).
                                                 ConfigureAwait(false);

            // DNS header requires at least 12 bytes
            if (responseLength < 12)
                return DNSInfo.Failed(
                           OriginOf(EffectiveTimeout),
                           DNSQuery.TransactionId,
                           EffectiveTimeout
                       );

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

            var response = DNSInfo.ReadResponse(
                               OriginOf(EffectiveTimeout),
                               DNSQuery.TransactionId,
                               new MemoryStream(buffer, 0, totalRead),
                               EffectiveTimeout,
                               stopwatch.Elapsed
                           );

            // RFC 7828: Extract server-advertised idle timeout from the response OPT record.
            var keepalive = response.EDNSOptions
                                    .OfType<EDNSKeepaliveOption>()
                                    .FirstOrDefault();

            if (keepalive?.IdleTimeout is not null)
                ServerKeepaliveTimeout = keepalive.IdleTimeout;

            return response;

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
        public static DNSTCPClient Google_Random(Boolean?         RecursionDesired = null,
                                                 TimeSpan?        QueryTimeout     = null,
                                                 ILoggerFactory?  LoggerFactory    = null)
        {
            var all = Google_All(RecursionDesired, QueryTimeout, LoggerFactory).ToList();
            return all[Random.Shared.Next(all.Count)];
        }

        /// <summary>
        /// Randomly select one of the Google IPv4 DNS servers.
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSTCPClient Google_Random_IPv4(Boolean?         RecursionDesired = null,
                                                      TimeSpan?        QueryTimeout     = null,
                                                      ILoggerFactory?  LoggerFactory    = null)
        {
            var all = Google_All_IPv4(RecursionDesired, QueryTimeout, LoggerFactory).ToList();
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
        public static DNSTCPClient Google_Random_IPv6(Boolean?         RecursionDesired = null,
                                                      TimeSpan?        QueryTimeout     = null,
                                                      ILoggerFactory?  LoggerFactory    = null)
        {
            var all = Google_All_IPv6(RecursionDesired, QueryTimeout, LoggerFactory).ToList();
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
        public static IEnumerable<DNSTCPClient> Google_All(Boolean?         RecursionDesired = null,
                                                           TimeSpan?        QueryTimeout     = null,
                                                           ILoggerFactory?  LoggerFactory    = null)

            => [
                   Google_IPv4_1(RecursionDesired, QueryTimeout, LoggerFactory),
                   Google_IPv4_2(RecursionDesired, QueryTimeout, LoggerFactory),
                   Google_IPv6_1(RecursionDesired, QueryTimeout, LoggerFactory),
                   Google_IPv6_2(RecursionDesired, QueryTimeout, LoggerFactory)
               ];

        /// <summary>
        /// All Google IPv4 DNS servers.
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static IEnumerable<DNSTCPClient> Google_All_IPv4(Boolean?         RecursionDesired = null,
                                                                TimeSpan?        QueryTimeout     = null,
                                                                ILoggerFactory?  LoggerFactory    = null)

            => [
                   Google_IPv4_1(RecursionDesired, QueryTimeout, LoggerFactory),
                   Google_IPv4_2(RecursionDesired, QueryTimeout, LoggerFactory)
               ];

        /// <summary>
        /// All Google IPv6 DNS servers.
        /// </summary>
        /// <remarks>
        /// IPv6 seems to be broken sometimes!
        /// </remarks>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static IEnumerable<DNSTCPClient> Google_All_IPv6(Boolean?         RecursionDesired = null,
                                                                TimeSpan?        QueryTimeout     = null,
                                                                ILoggerFactory?  LoggerFactory    = null)

            => [
                   Google_IPv6_1(RecursionDesired, QueryTimeout, LoggerFactory),
                   Google_IPv6_2(RecursionDesired, QueryTimeout, LoggerFactory)
               ];


        /// <summary>
        /// Google DNS server 8.8.8.8
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSTCPClient Google_IPv4_1(Boolean?         RecursionDesired = null,
                                                 TimeSpan?        QueryTimeout     = null,
                                                 ILoggerFactory?  LoggerFactory    = null)

            => new (
                   IPv4Address.Parse("8.8.8.8"),
                   IPPort.DNS,
                   I18NString.Create("Google (8.8.8.8)"),
                   RecursionDesired,
                   QueryTimeout,
                   LoggerFactory: LoggerFactory
               );

        /// <summary>
        /// Google DNS server 8.8.4.4
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSTCPClient Google_IPv4_2(Boolean?         RecursionDesired = null,
                                                 TimeSpan?        QueryTimeout     = null,
                                                 ILoggerFactory?  LoggerFactory    = null)

            => new (
                   IPv4Address.Parse("8.8.4.4"),
                   IPPort.DNS,
                   I18NString.Create("Google (8.8.4.4)"),
                   RecursionDesired,
                   QueryTimeout,
                   LoggerFactory: LoggerFactory
               );


        /// <summary>
        /// Google DNS server 2001:4860:4860::8888
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSTCPClient Google_IPv6_1(Boolean?         RecursionDesired = null,
                                                 TimeSpan?        QueryTimeout     = null,
                                                 ILoggerFactory?  LoggerFactory    = null)

            => new (
                   IPv6Address.Parse("2001:4860:4860::8888"),
                   IPPort.DNS,
                   I18NString.Create("Google (2001:4860:4860::8888)"),
                   RecursionDesired,
                   QueryTimeout,
                   LoggerFactory: LoggerFactory
               );

        /// <summary>
        /// Google DNS server 2001:4860:4860::8844
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSTCPClient Google_IPv6_2(Boolean?         RecursionDesired = null,
                                                 TimeSpan?        QueryTimeout     = null,
                                                 ILoggerFactory?  LoggerFactory    = null)

            => new (
                   IPv6Address.Parse("2001:4860:4860::8844"),
                   IPPort.DNS,
                   I18NString.Create("Google (2001:4860:4860::8844)"),
                   RecursionDesired,
                   QueryTimeout,
                   LoggerFactory: LoggerFactory
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
        public static DNSTCPClient Cloudflare_Random(Boolean?         RecursionDesired = null,
                                                     TimeSpan?        QueryTimeout     = null,
                                                     ILoggerFactory?  LoggerFactory    = null)
        {
            var all = Cloudflare_All(RecursionDesired, QueryTimeout, LoggerFactory).ToList();
            return all[Random.Shared.Next(all.Count)];
        }

        /// <summary>
        /// Randomly select one of the Cloudflare IPv4 DNS servers.
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSTCPClient Cloudflare_Random_IPv4(Boolean?         RecursionDesired = null,
                                                          TimeSpan?        QueryTimeout     = null,
                                                          ILoggerFactory?  LoggerFactory    = null)
        {
            var all = Cloudflare_All_IPv4(RecursionDesired, QueryTimeout, LoggerFactory).ToList();
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
        public static DNSTCPClient Cloudflare_Random_IPv6(Boolean?         RecursionDesired = null,
                                                          TimeSpan?        QueryTimeout     = null,
                                                          ILoggerFactory?  LoggerFactory    = null)
        {
            var all = Cloudflare_All_IPv6(RecursionDesired, QueryTimeout, LoggerFactory).ToList();
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
        public static IEnumerable<DNSTCPClient> Cloudflare_All(Boolean?         RecursionDesired = null,
                                                               TimeSpan?        QueryTimeout     = null,
                                                               ILoggerFactory?  LoggerFactory    = null)

            => [
                   Cloudflare_IPv4_1(RecursionDesired, QueryTimeout, LoggerFactory),
                   Cloudflare_IPv4_2(RecursionDesired, QueryTimeout, LoggerFactory),
                   Cloudflare_IPv4_3(RecursionDesired, QueryTimeout, LoggerFactory),
                   Cloudflare_IPv4_4(RecursionDesired, QueryTimeout, LoggerFactory),
                   Cloudflare_IPv6_1(RecursionDesired, QueryTimeout, LoggerFactory),
                   Cloudflare_IPv6_2(RecursionDesired, QueryTimeout, LoggerFactory),
                   Cloudflare_IPv6_3(RecursionDesired, QueryTimeout, LoggerFactory),
                   Cloudflare_IPv6_4(RecursionDesired, QueryTimeout, LoggerFactory)
               ];

        /// <summary>
        /// All Cloudflare IPv4 DNS servers.
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static IEnumerable<DNSTCPClient> Cloudflare_All_IPv4(Boolean?         RecursionDesired = null,
                                                                    TimeSpan?        QueryTimeout     = null,
                                                                    ILoggerFactory?  LoggerFactory    = null)

            => [
                   Cloudflare_IPv4_1(RecursionDesired, QueryTimeout, LoggerFactory),
                   Cloudflare_IPv4_2(RecursionDesired, QueryTimeout, LoggerFactory),
                   Cloudflare_IPv4_3(RecursionDesired, QueryTimeout, LoggerFactory),
                   Cloudflare_IPv4_4(RecursionDesired, QueryTimeout, LoggerFactory),
               ];

        /// <summary>
        /// All Cloudflare IPv6 DNS servers.
        /// </summary>
        /// <remarks>
        /// IPv6 seems to be broken sometimes!
        /// </remarks>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static IEnumerable<DNSTCPClient> Cloudflare_All_IPv6(Boolean?         RecursionDesired = null,
                                                                    TimeSpan?        QueryTimeout     = null,
                                                                    ILoggerFactory?  LoggerFactory    = null)

            => [
                   Cloudflare_IPv6_1(RecursionDesired, QueryTimeout, LoggerFactory),
                   Cloudflare_IPv6_2(RecursionDesired, QueryTimeout, LoggerFactory),
                   Cloudflare_IPv6_3(RecursionDesired, QueryTimeout, LoggerFactory),
                   Cloudflare_IPv6_4(RecursionDesired, QueryTimeout, LoggerFactory)
               ];


        /// <summary>
        /// Cloudflare DNS server 1.1.1.1
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSTCPClient Cloudflare_IPv4_1(Boolean?         RecursionDesired = null,
                                                     TimeSpan?        QueryTimeout     = null,
                                                     ILoggerFactory?  LoggerFactory    = null)

            => new (
                   IPv4Address.Parse("1.1.1.1"),
                   IPPort.DNS,
                   I18NString.Create("Cloudflare (1.1.1.1)"),
                   RecursionDesired,
                   QueryTimeout,
                   LoggerFactory: LoggerFactory
               );

        /// <summary>
        /// Cloudflare DNS server 1.0.0.1
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSTCPClient Cloudflare_IPv4_2(Boolean?         RecursionDesired = null,
                                                     TimeSpan?        QueryTimeout     = null,
                                                     ILoggerFactory?  LoggerFactory    = null)

            => new (
                   IPv4Address.Parse("1.0.0.1"),
                   IPPort.DNS,
                   I18NString.Create("Cloudflare (1.0.0.1)"),
                   RecursionDesired,
                   QueryTimeout,
                   LoggerFactory: LoggerFactory
               );

        /// <summary>
        /// Cloudflare DNS server 162.159.36.1
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSTCPClient Cloudflare_IPv4_3(Boolean?         RecursionDesired = null,
                                                     TimeSpan?        QueryTimeout     = null,
                                                     ILoggerFactory?  LoggerFactory    = null)

            => new (
                   IPv4Address.Parse("162.159.36.1"),
                   IPPort.DNS,
                   I18NString.Create("Cloudflare (162.159.36.1)"),
                   RecursionDesired,
                   QueryTimeout,
                   LoggerFactory: LoggerFactory
               );

        /// <summary>
        /// Cloudflare DNS server 162.159.46.1
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSTCPClient Cloudflare_IPv4_4(Boolean?         RecursionDesired = null,
                                                     TimeSpan?        QueryTimeout     = null,
                                                     ILoggerFactory?  LoggerFactory    = null)

            => new (
                   IPv4Address.Parse("162.159.46.1"),
                   IPPort.DNS,
                   I18NString.Create("Cloudflare (162.159.46.1)"),
                   RecursionDesired,
                   QueryTimeout,
                   LoggerFactory: LoggerFactory
               );


        /// <summary>
        /// Cloudflare DNS server 2606:4700:4700::1001
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSTCPClient Cloudflare_IPv6_1(Boolean?         RecursionDesired = null,
                                                     TimeSpan?        QueryTimeout     = null,
                                                     ILoggerFactory?  LoggerFactory    = null)

            => new (
                   IPv6Address.Parse("2606:4700:4700::1001"),
                   IPPort.DNS,
                   I18NString.Create("Cloudflare (2606:4700:4700::1001)"),
                   RecursionDesired,
                   QueryTimeout,
                   LoggerFactory: LoggerFactory
               );

        /// <summary>
        /// Cloudflare DNS server 2606:4700:4700::1111
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSTCPClient Cloudflare_IPv6_2(Boolean?         RecursionDesired = null,
                                                     TimeSpan?        QueryTimeout     = null,
                                                     ILoggerFactory?  LoggerFactory    = null)

            => new (
                   IPv6Address.Parse("2606:4700:4700::1111"),
                   IPPort.DNS,
                   I18NString.Create("Cloudflare (2606:4700:4700::1111)"),
                   RecursionDesired,
                   QueryTimeout,
                   LoggerFactory: LoggerFactory
               );

        /// <summary>
        /// Cloudflare DNS server 2606:4700:4700::0064
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSTCPClient Cloudflare_IPv6_3(Boolean?         RecursionDesired = null,
                                                     TimeSpan?        QueryTimeout     = null,
                                                     ILoggerFactory?  LoggerFactory    = null)

            => new (
                   IPv6Address.Parse("2606:4700:4700::0064"),
                   IPPort.DNS,
                   I18NString.Create("Cloudflare (2606:4700:4700::0064)"),
                   RecursionDesired,
                   QueryTimeout,
                   LoggerFactory: LoggerFactory
               );

        /// <summary>
        /// Cloudflare DNS server 2606:4700:4700::6400
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSTCPClient Cloudflare_IPv6_4(Boolean?         RecursionDesired = null,
                                                     TimeSpan?        QueryTimeout     = null,
                                                     ILoggerFactory?  LoggerFactory    = null)

            => new (
                   IPv6Address.Parse("2606:4700:4700::6400"),
                   IPPort.DNS,
                   I18NString.Create("Cloudflare (2606:4700:4700::6400)"),
                   RecursionDesired,
                   QueryTimeout,
                   LoggerFactory: LoggerFactory
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
