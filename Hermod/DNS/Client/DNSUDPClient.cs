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

        /// <summary>
        /// How long to wait for an answer before asking again, doubling with
        /// every further attempt until the query timeout is spent.
        /// </summary>
        public static readonly    TimeSpan                  DefaultRetransmissionInterval    = TimeSpan.FromSeconds(1);

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
        /// The TSIG key to sign queries with, or null to leave them unsigned
        /// (RFC 8945).
        /// </summary>
        public TSIGKey?     TSIGKey
            => TransactionSecurity.TSIGKey;

        /// <summary>
        /// The SIG(0) key to sign queries with, or null to leave them unsigned
        /// (RFC 2931).
        /// </summary>
        public SIG0Key?     SIG0Key
            => TransactionSecurity.SIG0Key;

        /// <summary>
        /// How this client signs its queries and checks the replies — shared
        /// with every other transport rather than reimplemented per socket.
        /// </summary>
        public DNSTransactionSecurity  TransactionSecurity   { get; }


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
        /// How long to wait for an answer before asking again, doubling with
        /// every further attempt until <see cref="QueryTimeout"/> is spent.
        /// </summary>
        /// <remarks>
        /// UDP loses datagrams; that is what it is. This client used to send one
        /// and wait out the whole timeout, so a single lost packet - measured at
        /// roughly one query in a hundred and twenty from here, in both
        /// directions and to both Google and Cloudflare - became a 23.5 second
        /// failure. Asking a second time recovered every one of them.
        /// </remarks>
        public TimeSpan    RetransmissionInterval   { get; set; } = DefaultRetransmissionInterval;

        /// <summary>
        /// Optional EDNS0 options to include in every DNS query.
        /// </summary>
        public List<EDNSOption>  EDNSOptions   { get; } = [];

        /// <inheritdoc />
        public Boolean           DnssecOK      { get; set; }



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
                            ILoggerFactory?         LoggerFactory      = null,
                            TSIGKey?                TSIGKey            = null,
                            SIG0Key?                SIG0Key            = null,
                            IEnumerable<KEY>?       SIG0ServerKeys     = null)

        {

            this.TransactionSecurity = new DNSTransactionSecurity(TSIGKey, SIG0Key, SIG0ServerKeys);
            this.RemoteIPAddress   = IPAddress;
            this.RemotePort        = Port             ?? IPPort.DNS;
            this.RemoteURL         = IPAddress.IsIPv6
                                         ? URL.Parse($"dns://[{IPAddress}]:{this.RemotePort}")
                                         : URL.Parse($"dns://{IPAddress}:{this.RemotePort}");
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
                               this.DnssecOK,
                               EDNSOptions.Count > 0 ? EDNSOptions : null,
                               [.. resourceRecordTypes]
                           );

            Socket? socket        = null;
            var     transmissions = 0;

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

                // RFC 8945 §5.3: sign the query, and keep the MAC — the response
                // folds it in, which is what binds the answer to this question.
                // RFC 8945 §5.3 / RFC 2931 §3.1: sign the query, and keep what the
                // response's own signature will fold in — the MAC for TSIG, and
                // for SIG(0) the query as it went on the wire, signature and all.
                var wire = TransactionSecurity.SignQuery(ms.ToArray(), out var requestMAC);

                await socket.SendToAsync(wire, SocketFlags.None, remoteEndPoint, timeoutCTS.Token).
                             ConfigureAwait(false);

                transmissions++;

                var data              = new Byte[Math.Max(4096, (Int32) UDPPayloadSize)];
                var attemptTimeout    = RetransmissionInterval;

                while (true)
                {

                    Int32 received;

                    // Wait for this attempt's share of the budget rather than for
                    // all of it. UDP loses datagrams, and with one transmission
                    // and no second question a lost packet cost the entire query
                    // timeout - 23.5 seconds of waiting for an answer to a
                    // question nobody heard.
                    using (var attemptCTS = CancellationTokenSource.CreateLinkedTokenSource(timeoutCTS.Token))
                    {

                        attemptCTS.CancelAfter(attemptTimeout);

                        try
                        {
                            received = await socket.ReceiveAsync(data, SocketFlags.None, attemptCTS.Token).
                                                    ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (!timeoutCTS.IsCancellationRequested)
                        {

                            // This attempt's share is spent and the query's is not:
                            // ask again. The same transaction id, so a late answer
                            // to any earlier transmission still counts - and every
                            // one of them is still checked below.
                            await socket.SendToAsync(wire, SocketFlags.None, remoteEndPoint, timeoutCTS.Token).
                                         ConfigureAwait(false);

                            transmissions++;
                            attemptTimeout += attemptTimeout;

                            logger.LogDebug(
                                "No answer from {RemoteIPAddress}:{RemotePort} yet; asked again (transmission {Transmissions}), next wait {AttemptTimeout}s",
                                RemoteIPAddress,
                                RemotePort,
                                transmissions,
                                attemptTimeout.TotalSeconds
                            );

                            continue;

                        }

                    }

                    // RFC 5452 §4.2: a resolver MUST ignore responses that do not
                    // match the outstanding query. "Ignore" means keep waiting for
                    // the genuine reply — treating the first datagram as final would
                    // let any off-path attacker cancel a lookup with one forged
                    // packet. The overall timeout still bounds the wait.
                    if (received < 2 ||
                        ((data[0] << 8) | data[1]) != dnsQuery.TransactionId)
                    {

                        logger.LogDebug(
                            "Ignoring a DNS UDP datagram from {RemoteIPAddress}:{RemotePort} that does not match transaction id {TransactionId}",
                            RemoteIPAddress,
                            RemotePort,
                            dnsQuery.TransactionId
                        );

                        continue;

                    }

                    var body = new Byte[received];
                    Buffer.BlockCopy(data, 0, body, 0, received);

                    // A response that does not authenticate is not an answer.
                    // Discarding it and waiting takes the same posture RFC 5452
                    // §4.2 takes for a mismatched transaction id: one forged
                    // datagram must not be able to end a query. The overall
                    // timeout still bounds the wait.
                    if (!TryAcceptSignedResponse(ref body, requestMAC, wire, "UDP"))
                        continue;

                    var response = DNSInfo.ReadResponse(
                                      new DNSServerConfig(
                                          RemoteIPAddress,
                                          RemotePort ?? IPPort.DNS,
                                          DNSTransport.UDP,
                                          effectiveTimeout
                                      ),
                                      dnsQuery.TransactionId,
                                      new MemoryStream(body),
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
                    "DNS UDP query to {RemoteIPAddress}:{RemotePort} timed out after asking {Transmissions} time(s)",
                    RemoteIPAddress,
                    RemotePort,
                    transmissions
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

        #region (private) TryAcceptSignedResponse(ref Body, RequestMAC, SignedQuery, Transport)

        /// <summary>
        /// Check and strip a response's transaction signature, logging why when
        /// it is rejected.
        /// </summary>
        private Boolean TryAcceptSignedResponse(ref Byte[]  Body,
                                                Byte[]?     RequestMAC,
                                                Byte[]      SignedQuery,
                                                String      Transport)
        {

            if (TransactionSecurity.TryAcceptResponse(ref Body, RequestMAC, SignedQuery, out var reason))
                return true;

            logger.LogDebug(
                "Discarding a DNS {Transport} response from {RemoteIPAddress}:{RemotePort} that failed transaction-signature verification: {Reason}",
                Transport,
                RemoteIPAddress,
                RemotePort,
                reason
            );

            return false;

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
                DNSQuery.Serialize(ms, false, []);

                // The retry carries the same signature the UDP query did.
                // Serializing afresh and sending it unsigned is the quiet failure
                // this used to have: the server serves an unsigned request
                // happily, so nothing anywhere reports an error, and an exchange
                // the caller believes is authenticated simply is not — precisely
                // when the answer was too big for a datagram, which for a signed
                // zone is the ordinary case rather than the rare one.
                var signedQuery  = TransactionSecurity.SignQuery(ms.ToArray(), out var requestMAC);

                var data         = new Byte[signedQuery.Length + 2];
                data[0] = (Byte) (signedQuery.Length >> 8);
                data[1] = (Byte) (signedQuery.Length & 0xFF);
                Buffer.BlockCopy(signedQuery, 0, data, 2, signedQuery.Length);

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

                var body = buffer[..totalRead];

                if (!TryAcceptSignedResponse(ref body, requestMAC, signedQuery, "TCP"))
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

                return DNSInfo.ReadResponse(
                           new DNSServerConfig(
                               RemoteIPAddress,
                               RemotePort ?? IPPort.DNS,
                               DNSTransport.TCP,
                               Timeout
                           ),
                           DNSQuery.TransactionId,
                           new MemoryStream(body),
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
        public static DNSUDPClient Google_Random(Boolean?         RecursionDesired = null,
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
        public static DNSUDPClient Google_Random_IPv4(Boolean?         RecursionDesired = null,
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
        public static DNSUDPClient Google_Random_IPv6(Boolean?         RecursionDesired = null,
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
        public static IEnumerable<DNSUDPClient> Google_All(Boolean?         RecursionDesired = null,
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
        public static IEnumerable<DNSUDPClient> Google_All_IPv4(Boolean?         RecursionDesired = null,
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
        public static IEnumerable<DNSUDPClient> Google_All_IPv6(Boolean?         RecursionDesired = null,
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
        public static DNSUDPClient Google_IPv4_1(Boolean?         RecursionDesired = null,
                                                 TimeSpan?        QueryTimeout     = null,
                                                 ILoggerFactory?  LoggerFactory    = null)

            => new (
                   IPv4Address.Parse("8.8.8.8"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout,
                   LoggerFactory: LoggerFactory
               );

        /// <summary>
        /// Google DNS server 8.8.4.4
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Google_IPv4_2(Boolean?         RecursionDesired = null,
                                                 TimeSpan?        QueryTimeout     = null,
                                                 ILoggerFactory?  LoggerFactory    = null)

            => new (
                   IPv4Address.Parse("8.8.4.4"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout,
                   LoggerFactory: LoggerFactory
               );


        /// <summary>
        /// Google DNS server 2001:4860:4860::8888
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Google_IPv6_1(Boolean?         RecursionDesired = null,
                                                 TimeSpan?        QueryTimeout     = null,
                                                 ILoggerFactory?  LoggerFactory    = null)

            => new (
                   IPv6Address.Parse("2001:4860:4860::8888"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout,
                   LoggerFactory: LoggerFactory
               );

        /// <summary>
        /// Google DNS server 2001:4860:4860::8844
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Google_IPv6_2(Boolean?         RecursionDesired = null,
                                                 TimeSpan?        QueryTimeout     = null,
                                                 ILoggerFactory?  LoggerFactory    = null)

            => new (
                   IPv6Address.Parse("2001:4860:4860::8844"),
                   IPPort.DNS,
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
        public static DNSUDPClient Cloudflare_Random(Boolean?         RecursionDesired = null,
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
        public static DNSUDPClient Cloudflare_Random_IPv4(Boolean?         RecursionDesired = null,
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
        public static DNSUDPClient Cloudflare_Random_IPv6(Boolean?         RecursionDesired = null,
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
        public static IEnumerable<DNSUDPClient> Cloudflare_All(Boolean?         RecursionDesired = null,
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
        public static IEnumerable<DNSUDPClient> Cloudflare_All_IPv4(Boolean?         RecursionDesired = null,
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
        public static IEnumerable<DNSUDPClient> Cloudflare_All_IPv6(Boolean?         RecursionDesired = null,
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
        public static DNSUDPClient Cloudflare_IPv4_1(Boolean?         RecursionDesired = null,
                                                     TimeSpan?        QueryTimeout     = null,
                                                     ILoggerFactory?  LoggerFactory    = null)

            => new (
                   IPv4Address.Parse("1.1.1.1"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout,
                   LoggerFactory: LoggerFactory
               );

        /// <summary>
        /// Cloudflare DNS server 1.0.0.1
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Cloudflare_IPv4_2(Boolean?         RecursionDesired = null,
                                                     TimeSpan?        QueryTimeout     = null,
                                                     ILoggerFactory?  LoggerFactory    = null)

            => new (
                   IPv4Address.Parse("1.0.0.1"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout,
                   LoggerFactory: LoggerFactory
               );

        /// <summary>
        /// Cloudflare DNS server 162.159.36.1
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Cloudflare_IPv4_3(Boolean?         RecursionDesired = null,
                                                     TimeSpan?        QueryTimeout     = null,
                                                     ILoggerFactory?  LoggerFactory    = null)

            => new (
                   IPv4Address.Parse("162.159.36.1"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout,
                   LoggerFactory: LoggerFactory
               );

        /// <summary>
        /// Cloudflare DNS server 162.159.46.1
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Cloudflare_IPv4_4(Boolean?         RecursionDesired = null,
                                                     TimeSpan?        QueryTimeout     = null,
                                                     ILoggerFactory?  LoggerFactory    = null)

            => new (
                   IPv4Address.Parse("162.159.46.1"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout,
                   LoggerFactory: LoggerFactory
               );


        /// <summary>
        /// Cloudflare DNS server 2606:4700:4700::1001
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Cloudflare_IPv6_1(Boolean?         RecursionDesired = null,
                                                     TimeSpan?        QueryTimeout     = null,
                                                     ILoggerFactory?  LoggerFactory    = null)

            => new (
                   IPv6Address.Parse("2606:4700:4700::1001"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout,
                   LoggerFactory: LoggerFactory
               );

        /// <summary>
        /// Cloudflare DNS server 2606:4700:4700::1111
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Cloudflare_IPv6_2(Boolean?         RecursionDesired = null,
                                                     TimeSpan?        QueryTimeout     = null,
                                                     ILoggerFactory?  LoggerFactory    = null)

            => new (
                   IPv6Address.Parse("2606:4700:4700::1111"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout,
                   LoggerFactory: LoggerFactory
               );

        /// <summary>
        /// Cloudflare DNS server 2606:4700:4700::0064
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Cloudflare_IPv6_3(Boolean?         RecursionDesired = null,
                                                     TimeSpan?        QueryTimeout     = null,
                                                     ILoggerFactory?  LoggerFactory    = null)

            => new (
                   IPv6Address.Parse("2606:4700:4700::0064"),
                   IPPort.DNS,
                   RecursionDesired,
                   QueryTimeout,
                   LoggerFactory: LoggerFactory
               );

        /// <summary>
        /// Cloudflare DNS server 2606:4700:4700::6400
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        public static DNSUDPClient Cloudflare_IPv6_4(Boolean?         RecursionDesired = null,
                                                     TimeSpan?        QueryTimeout     = null,
                                                     ILoggerFactory?  LoggerFactory    = null)

            => new (
                   IPv6Address.Parse("2606:4700:4700::6400"),
                   IPPort.DNS,
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
