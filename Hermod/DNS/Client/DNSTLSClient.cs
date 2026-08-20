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
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// A DNS TLS client (DNS-over-TLS) for a single DNS server.
    /// Reuses the TLS connection across queries; concurrent callers
    /// are serialized via an internal semaphore.
    /// </summary>
    public class DNSTLSClient : ATLSClient,
                                IDNSClientWithTransport
    {

        #region Data

        /// <summary>
        /// The default DNS query timeout.
        /// </summary>
        public static readonly TimeSpan DefaultQueryTimeout = TimeSpan.FromSeconds(23.5);

        private readonly SemaphoreSlim tlsStreamLock = new(1, 1);

        /// <summary>
        /// RFC 7828 §3.2.2 for this session: what the server last advertised, and
        /// the clock that ends the session before that timeout expires.
        /// </summary>
        private readonly DNSKeepalivePolicy keepalive;

        #endregion

        #region Properties

        /// <summary>
        /// Whether DNS recursion is desired.
        /// </summary>
        public Boolean?          RecursionDesired          { get; set; }

        /// <summary>
        /// The DNS query timeout.
        /// </summary>
        public TimeSpan          QueryTimeout              { get; set; }

        /// <summary>
        /// Optional EDNS0 options to include in every DNS query.
        /// </summary>
        public List<EDNSOption>  EDNSOptions               { get; } = [];

        /// <summary>
        /// Where this client says what went wrong. The <c>Logger</c> parameter
        /// its constructors have always taken was passed no further than the
        /// signature; the base class keeps its own private, so a failure down
        /// here had nowhere to go.
        /// </summary>
        private readonly ILogger<DNSTLSClient>  logger;

        /// <inheritdoc />
        public Boolean           DnssecOK                  { get; set; }

        /// <summary>
        /// How this client signs its queries and checks the replies — TSIG
        /// (RFC 8945) or SIG(0) (RFC 2931). Unsigned by default.
        /// </summary>
        /// <remarks>
        /// A transaction signature covers the DNS message, so it is indifferent
        /// to what carries it: DoT is RFC 7766 framing inside a TLS session, and
        /// the signed octets are the same ones the datagram path signs. TLS
        /// authenticates the *server* to the client and secures the channel; it
        /// says nothing about who sent the query, which is the question these
        /// mechanisms answer.
        /// </remarks>
        public DNSTransactionSecurity  TransactionSecurity { get; set; } = DNSTransactionSecurity.None;

        /// <summary>
        /// The EDNS(0) payload size this client advertises (RFC 6891 §6.2.3),
        /// which is also the ceiling RFC 7830 §4 puts on how far the responder
        /// may pad its reply. Set to 0 to send no OPT record at all, which
        /// announces no EDNS(0) support and rules out padding in either
        /// direction.
        /// </summary>
        public UInt16            UDPPayloadSize            { get; set; } = DNSPacket.DefaultUDPPayloadSize;

        /// <summary>
        /// The block length queries are padded to, or 0 to send them unpadded.
        /// </summary>
        /// <remarks>
        /// RFC 8467 §4.1: "Clients SHOULD pad queries to the closest multiple of
        /// 128 octets", with the note that "the recommendation above only applies
        /// if the DNS transport is encrypted". DoT is encrypted by construction,
        /// so the recommendation always applies here and padding is on by
        /// default — a plaintext DNS client would have nothing to gain from it.
        /// </remarks>
        public UInt16            PaddingBlockSize          { get; set; } = DNSPadding.QueryBlockSize;

        /// <summary>
        /// The server-advertised idle timeout from the last EDNS TCP Keepalive
        /// response option (RFC 7828). Null if no keepalive option was received.
        /// </summary>
        /// <remarks>
        /// Acting on it is not left to the caller: RFC 7828 §3.2.2 asks the
        /// client to close before this expires, and it does. The value is exposed
        /// because it says what the peer is willing to hold, which is worth
        /// seeing from outside.
        /// </remarks>
        public TimeSpan?         ServerKeepaliveTimeout
            => keepalive.ServerTimeout;


        /// <summary>
        /// How to name this resolver in a log line.
        /// </summary>
        /// <remarks>
        /// RemoteIPAddress is only ever set by the constructor which is given
        /// one; the URL constructor beside it learns the address when the socket
        /// connects and knows none at all when it never did. See
        /// <see cref="DNSHTTPSClient"/>, where logging that field directly is
        /// what produced "(null):443".
        /// </remarks>
        private String DNSServerLabel
        {
            get
            {

                var ipAddress = CurrentRemoteIPAddress ?? RemoteIPAddress;

                if (ipAddress is null)
                    return RemoteURL.ToString();

                // Brackets, or "2001:...:8844:853" is anyone's guess as to where
                // the address stops and the port starts - RFC 3986 §3.2.2.
                return $"{ipAddress.ToIPLiteral()}:{RemotePort ?? IPPort.DNS_TLS}";

            }
        }


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
                             RemotePort ?? IPPort.DNS_TLS,
                             DNSTransport.TLS,
                             QueryTimeout
                         )

                       : new DNSServerConfig(
                             domainName!,
                             RemotePort ?? IPPort.DNS_TLS,
                             DNSTransport.TLS,
                             QueryTimeout
                         );

        }

        #endregion

        #region Constructor(s)

        #region DNSTLSClient(IPAddress, ...)

        /// <summary>
        /// Create a new DNS TLS client for the given DNS server.
        /// </summary>
        /// <param name="IPAddress">The IP address of the DNS server to query.</param>
        public DNSTLSClient(IIPAddress                                                  IPAddress,
                            IPPort?                                                     TCPPort                          = null,
                            I18NString?                                                 Description                      = null,
                            Boolean?                                                    RecursionDesired                 = null,
                            TimeSpan?                                                   QueryTimeout                     = null,

                            String?                                                     TLSHostname                      = null,
                            RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator       = null,
                            SslProtocols?                                               TLSProtocols                     = null,
                            CipherSuitesPolicy?                                         CipherSuitesPolicy               = null,
                            X509ChainPolicy?                                            CertificateChainPolicy           = null,
                            X509RevocationMode?                                         CertificateRevocationCheckMode   = null,
                            IEnumerable<SslApplicationProtocol>?                        ApplicationProtocols             = null,
                            Boolean?                                                    AllowRenegotiation               = null,
                            Boolean?                                                    AllowTLSResume                   = null,

                            IPVersionPreference?                                        PreferIPv4                       = null,
                            TimeSpan?                                                   ConnectTimeout                   = null,
                            TimeSpan?                                                   ReceiveTimeout                   = null,
                            TimeSpan?                                                   SendTimeout                      = null,
                            TransmissionRetryDelayDelegate?                             TransmissionRetryDelay           = null,
                            UInt16?                                                     MaxNumberOfRetries               = null,
                            UInt32?                                                     BufferSize                       = null,

                            Boolean?                                                    DisableLogging                   = null,
                            ILogger<DNSTLSClient>?                                      Logger                           = null,
                            ILoggerFactory?                                             LoggerFactory                    = null)

            : base(IPAddress,
                   TCPPort ?? IPPort.DNS_TLS,
                   Description,

                   TLSHostname,
                   RemoteCertificateValidator is not null
                       ? (sender,
                          certificate,
                          certificateChain,
                          tlsClient,
                          policyErrors) => RemoteCertificateValidator.Invoke(
                                               sender,
                                               certificate,
                                               certificateChain,
                                               tlsClient as DNSTLSClient,
                                               policyErrors
                                           )
                       : null,
                   null,
                   null,
                   null,
                   null,
                   TLSProtocols,
                   CipherSuitesPolicy,
                   CertificateChainPolicy,
                   CertificateRevocationCheckMode,
                   true,
                   ApplicationProtocols,
                   AllowRenegotiation,
                   AllowTLSResume,

                   PreferIPv4,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize ?? 4096,

                   DisableLogging,
                   null,
                   LoggerFactory)

        {

            this.RecursionDesired  = RecursionDesired ?? true;
            this.QueryTimeout      = QueryTimeout     ?? TimeSpan.FromSeconds(23.5);
            this.logger            = Logger           ?? (LoggerFactory ?? NullLoggerFactory.Instance).CreateLogger<DNSTLSClient>();
            this.keepalive         = new DNSKeepalivePolicy(tlsStreamLock, CloseConnection);

        }

        #endregion

        #region DNSTLSClient(URL, ...)

        /// <summary>
        /// Create a new DNS TLS client for the given DNS server.
        /// </summary>
        /// <param name="URL">The URL of the DNS server to query.".</param>
        public DNSTLSClient(URL                                                         URL,
                            I18NString?                                                 Description                      = null,
                            Boolean?                                                    RecursionDesired                 = null,
                            TimeSpan?                                                   QueryTimeout                     = null,

                            String?                                                     TLSHostname                      = null,
                            RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator       = null,
                            SslProtocols?                                               TLSProtocols                     = null,
                            CipherSuitesPolicy?                                         CipherSuitesPolicy               = null,
                            X509ChainPolicy?                                            CertificateChainPolicy           = null,
                            X509RevocationMode?                                         CertificateRevocationCheckMode   = null,
                            IEnumerable<SslApplicationProtocol>?                        ApplicationProtocols             = null,
                            Boolean?                                                    AllowRenegotiation               = null,
                            Boolean?                                                    AllowTLSResume                   = null,

                            IPVersionPreference?                                        PreferIPv4                       = null,
                            TimeSpan?                                                   ConnectTimeout                   = null,
                            TimeSpan?                                                   ReceiveTimeout                   = null,
                            TimeSpan?                                                   SendTimeout                      = null,
                            TransmissionRetryDelayDelegate?                             TransmissionRetryDelay           = null,
                            UInt16?                                                     MaxNumberOfRetries               = null,
                            UInt32?                                                     BufferSize                       = null,
                            //TCPEchoLoggingDelegate?                                     LoggingHandler                   = null,

                            Boolean?                                                    DisableLogging                   = null,
                            IDNSClient?                                                 DNSClient                        = null,
                            ILogger<DNSTLSClient>?                                      Logger                           = null,
                            ILoggerFactory?                                             LoggerFactory                    = null)

            : base(URL,
                   Description,

                   TLSHostname,
                   RemoteCertificateValidator is not null
                       ? (sender,
                          certificate,
                          certificateChain,
                          tlsClient,
                          policyErrors) => RemoteCertificateValidator.Invoke(
                                               sender,
                                               certificate,
                                               certificateChain,
                                               tlsClient as DNSTLSClient,
                                               policyErrors
                                           )
                       : null,
                   null,
                   null,
                   null,
                   null,
                   TLSProtocols,
                   CipherSuitesPolicy,
                   CertificateChainPolicy,
                   CertificateRevocationCheckMode,
                   true,
                   ApplicationProtocols,
                   AllowRenegotiation,
                   AllowTLSResume,

                   PreferIPv4,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize ?? 4096,

                   DisableLogging,
                   DNSClient,
                   null,
                   LoggerFactory)

        {

            this.RecursionDesired  = RecursionDesired ?? true;
            this.QueryTimeout      = QueryTimeout     ?? TimeSpan.FromSeconds(23.5);
            this.logger            = Logger           ?? (LoggerFactory ?? NullLoggerFactory.Instance).CreateLogger<DNSTLSClient>();
            this.keepalive         = new DNSKeepalivePolicy(tlsStreamLock, CloseConnection);

            RemotePort ??= URL.Port ?? IPPort.DNS_TLS;

        }

        #endregion

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

            var signedQuery = SerializeQuery(dnsQuery, out var requestMAC);

            var data        = new Byte[signedQuery.Length + 2];
            data[0] = (Byte) (signedQuery.Length >> 8);
            data[1] = (Byte) (signedQuery.Length & 0xFF);
            Buffer.BlockCopy(signedQuery, 0, data, 2, signedQuery.Length);

            #region TLS (serialized via semaphore, auto-reconnect on broken connection)

            var effectiveTimeout = Timeout ?? QueryTimeout;

            await tlsStreamLock.WaitAsync(CancellationToken).
                                ConfigureAwait(false);

            try
            {

                if (!IsConnected || tcpClient is null)
                    await ReconnectAsync(CancellationToken).
                              ConfigureAwait(false);

                var stopwatch = Stopwatch.StartNew();

                // LiveClient..., not ??= - see DNSHTTPSClient for what a spent
                // token source costs here.
                using var timeoutCTS = CancellationTokenSource.CreateLinkedTokenSource(
                                           LiveClientCancellationTokenSource.Token,
                                           CancellationToken
                                       );
                timeoutCTS.CancelAfter(effectiveTimeout);

                try
                {
                    return await SendAndReceiveTLSAsync(tlsStream!, data, dnsQuery, signedQuery, requestMAC, effectiveTimeout, timeoutCTS.Token).
                                     ConfigureAwait(false);
                }
                catch (IOException)
                {
                    await ReconnectAsync(CancellationToken).ConfigureAwait(false);
                    return await SendAndReceiveTLSAsync(tlsStream!, data, dnsQuery, signedQuery, requestMAC, effectiveTimeout, timeoutCTS.Token).
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
                        "DNS TLS query to {DNSServer} was cancelled by the client - it did not time out",
                        DNSServerLabel
                    );

                    return DNSInfo.Failed(
                               OriginOf(),
                               dnsQuery.TransactionId,
                               effectiveTimeout
                           );

                }

                logger.LogWarning(
                    "DNS TLS query to {DNSServer} timed out after {Timeout}s",
                    DNSServerLabel,
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

                await Log($"DNS TLS query to {DNSServerLabel} socket error: {se.SocketErrorCode} — {se.Message}");

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

                await Log($"DNS TLS query to {DNSServerLabel} failed: [{ex.GetType().Name}] {ex.Message}");

                return DNSInfo.Failed(
                           OriginOf(),
                           dnsQuery.TransactionId,
                           effectiveTimeout
                       );

            }
            finally
            {
                tlsStreamLock.Release();
            }

            #endregion

        }

        #endregion

        #region (private) SerializeQuery(Query, out RequestMAC)

        /// <summary>
        /// Serialize a query for the wire: padded if this client pads, and
        /// signed if it signs.
        /// </summary>
        /// <param name="Query">The query to put on the wire.</param>
        /// <param name="RequestMAC">The TSIG MAC of the query that actually goes out, which the response's MAC folds in (RFC 8945 §4.3.1).</param>
        /// <remarks>
        /// <para>
        /// RFC 8467 §4.1: "Clients SHOULD pad queries to the closest multiple of
        /// 128 octets." Queries are short and alike, and a query is the message
        /// whose length gives away the most — encrypting the transport hides the
        /// name and leaves the length behind.
        /// </para>
        /// <para>
        /// How much padding a message needs depends on how long it already is, so
        /// this serializes twice. The trial run carries an empty Padding option,
        /// which puts the four octets of option header inside the length that
        /// comes back rather than leaving them to be remembered separately.
        /// </para>
        /// <para>
        /// The measurement is taken after signing. A TSIG or SIG(0) record is
        /// part of what an observer counts, so it is the signed message that has
        /// to land on the block boundary; both RFCs are silent on the
        /// combination. A signature is a fixed size for a given key and
        /// algorithm, so the trial run reports the length the real one will have.
        /// </para>
        /// <para>
        /// Nothing caps the query's own padding. RFC 7830 §4's ceiling is the
        /// Requestor's Payload Size, which says what this client is willing to
        /// receive, not what it may send.
        /// </para>
        /// </remarks>
        private Byte[] SerializeQuery(DNSPacket   Query,
                                      out Byte[]? RequestMAC)
        {

            static Byte[] serialize(DNSPacket message)
            {
                using var stream = new MemoryStream();
                message.Serialize(stream, false, []);
                return stream.ToArray();
            }

            // No block length, or no OPT record to carry the option: send the
            // query as it is. Inventing an OPT record here would announce EDNS(0)
            // support the caller switched off.
            if (PaddingBlockSize == 0 || !DNSPadding.HasEDNS(Query))
                return TransactionSecurity.SignQuery(serialize(Query), out RequestMAC);

            var trial   = TransactionSecurity.SignQuery(serialize(DNSPadding.WithPadding(Query, 0)), out var trialMAC);

            var octets  = DNSPadding.OctetsFor(trial.Length, PaddingBlockSize);

            if (octets == 0)
            {
                RequestMAC = trialMAC;
                return trial;
            }

            return TransactionSecurity.SignQuery(serialize(DNSPadding.WithPadding(Query, octets)), out RequestMAC);

        }

        #endregion

        #region (private) SendAndReceiveTLSAsync(...)

        /// <summary>
        /// Send a DNS query over the TLS stream and read the response.
        /// Extracted so that the IOException retry logic covers both write and read.
        /// </summary>
        private async Task<DNSInfo> SendAndReceiveTLSAsync(SslStream           TLSStream,
                                                           Byte[]              Data,
                                                           DNSPacket           DNSQuery,
                                                           Byte[]              SignedQuery,
                                                           Byte[]?             RequestMAC,
                                                           TimeSpan            EffectiveTimeout,
                                                           CancellationToken   CancellationToken)
        {

            var stopwatch = Stopwatch.StartNew();

            await TLSStream.WriteAsync(Data, CancellationToken).ConfigureAwait(false);
            await TLSStream.FlushAsync(CancellationToken).      ConfigureAwait(false);

            var responseLength = await TLSStream.ReadUInt16BEAsync(CancellationToken).
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

                var bytesRead = await TLSStream.ReadAsync(
                                          buffer.AsMemory(totalRead, responseLength - totalRead),
                                          CancellationToken
                                      ).ConfigureAwait(false);

                if (bytesRead == 0)
                    break;

                totalRead += bytesRead;

            }

            var body = buffer[..totalRead];

            if (!TransactionSecurity.TryAcceptResponse(ref body, RequestMAC, SignedQuery, out var reason))
            {

                await Log($"Discarding a DoT response from {DNSServerLabel} that failed transaction-signature verification: {reason}");

                return DNSInfo.Failed(
                           OriginOf(EffectiveTimeout),
                           DNSQuery.TransactionId,
                           EffectiveTimeout
                       );

            }

            var response = DNSInfo.ReadResponse(
                               OriginOf(EffectiveTimeout),
                               DNSQuery.TransactionId,
                               new MemoryStream(body),
                               EffectiveTimeout,
                               stopwatch.Elapsed
                           );

            // RFC 7828 §3.2.2, both halves: record what the server advertised,
            // drop the session at once on a TIMEOUT of 0, and otherwise restart
            // the idle clock so the session does not outlive the timeout it was
            // given. Nothing else has to change for either — every query begins
            // by reconnecting when IsConnected is false, so the next one opens a
            // fresh TLS session on its own.
            await keepalive.ApplyAsync(response).ConfigureAwait(false);

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
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Google_Random(Boolean?                                                    RecursionDesired           = null,
                                                 TimeSpan?                                                   QueryTimeout               = null,
                                                 RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator = null,
                                                 ILoggerFactory?                                             LoggerFactory              = null)
        {
            var all = Google_All(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory).ToList();
            return all[Random.Shared.Next(all.Count)];
        }

        /// <summary>
        /// Randomly select one of the Google IPv4 DNS servers.
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Google_Random_IPv4(Boolean?                                                    RecursionDesired           = null,
                                                      TimeSpan?                                                   QueryTimeout               = null,
                                                      RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator = null,
                                                      ILoggerFactory?                                             LoggerFactory              = null)
        {
            var all = Google_All_IPv4(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory).ToList();
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
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Google_Random_IPv6(Boolean?                                                    RecursionDesired           = null,
                                                      TimeSpan?                                                   QueryTimeout               = null,
                                                      RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator = null,
                                                      ILoggerFactory?                                             LoggerFactory              = null)
        {
            var all = Google_All_IPv6(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory).ToList();
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
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static IEnumerable<DNSTLSClient> Google_All(Boolean?                                                    RecursionDesired           = null,
                                                           TimeSpan?                                                   QueryTimeout               = null,
                                                           RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator = null,
                                                           ILoggerFactory?                                             LoggerFactory              = null)

            => [
                   Google_IPv4_1(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory),
                   Google_IPv4_2(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory),
                   Google_IPv6_1(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory),
                   Google_IPv6_2(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory)
               ];

        /// <summary>
        /// All Google IPv4 DNS servers.
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static IEnumerable<DNSTLSClient> Google_All_IPv4(Boolean?                                                    RecursionDesired           = null,
                                                                TimeSpan?                                                   QueryTimeout               = null,
                                                                RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator = null,
                                                                ILoggerFactory?                                             LoggerFactory              = null)

            => [
                   Google_IPv4_1(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory),
                   Google_IPv4_2(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory)
               ];

        /// <summary>
        /// All Google IPv6 DNS servers.
        /// </summary>
        /// <remarks>
        /// IPv6 seems to be broken sometimes!
        /// </remarks>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static IEnumerable<DNSTLSClient> Google_All_IPv6(Boolean?                                                    RecursionDesired           = null,
                                                                TimeSpan?                                                   QueryTimeout               = null,
                                                                RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator = null,
                                                                ILoggerFactory?                                             LoggerFactory              = null)

            => [
                   Google_IPv6_1(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory),
                   Google_IPv6_2(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory)
               ];


        /// <summary>
        /// Google DNS server 8.8.8.8
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Google_IPv4_1(Boolean?                                                    RecursionDesired           = null,
                                                 TimeSpan?                                                   QueryTimeout               = null,
                                                 RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator = null,
                                                 ILoggerFactory?                                             LoggerFactory              = null)

            => new (
                   IPv4Address.Parse("8.8.8.8"),
                   IPPort.DNS_TLS,
                   I18NString.Create("Google (8.8.8.8)"),
                   RecursionDesired,
                   QueryTimeout,
                   RemoteCertificateValidator: RemoteCertificateValidator,
                   LoggerFactory:              LoggerFactory
               );

        /// <summary>
        /// Google DNS server 8.8.4.4
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Google_IPv4_2(Boolean?                                                    RecursionDesired           = null,
                                                 TimeSpan?                                                   QueryTimeout               = null,
                                                 RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator = null,
                                                 ILoggerFactory?                                             LoggerFactory              = null)

            => new (
                   URL.Parse("tls://8.8.4.4:853"),
                   I18NString.Create("Google (8.8.4.4)"),
                   RecursionDesired,
                   QueryTimeout,
                   RemoteCertificateValidator: RemoteCertificateValidator,
                   LoggerFactory:              LoggerFactory
               );

        /// <summary>
        /// Google DNS server 2001:4860:4860::8888
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Google_IPv6_1(Boolean?                                                    RecursionDesired           = null,
                                                 TimeSpan?                                                   QueryTimeout               = null,
                                                 RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator = null,
                                                 ILoggerFactory?                                             LoggerFactory              = null)

            => new (
                   IPv6Address.Parse("2001:4860:4860::8888"),
                   IPPort.DNS_TLS,
                   I18NString.Create("Google (2001:4860:4860::8888)"),
                   RecursionDesired,
                   QueryTimeout,
                   RemoteCertificateValidator: RemoteCertificateValidator,
                   LoggerFactory:              LoggerFactory
               );

        /// <summary>
        /// Google DNS server 2001:4860:4860::8844
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Google_IPv6_2(Boolean?                                                    RecursionDesired           = null,
                                                 TimeSpan?                                                   QueryTimeout               = null,
                                                 RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator = null,
                                                 ILoggerFactory?                                             LoggerFactory              = null)

            => new (
                   URL.Parse("tls://[2001:4860:4860::8844]:853"),
                   I18NString.Create("Google (2001:4860:4860::8844)"),
                   RecursionDesired,
                   QueryTimeout,
                   RemoteCertificateValidator: RemoteCertificateValidator,
                   LoggerFactory:              LoggerFactory
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
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Cloudflare_Random(Boolean?                                                    RecursionDesired           = null,
                                                     TimeSpan?                                                   QueryTimeout               = null,
                                                     RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator = null,
                                                     ILoggerFactory?                                             LoggerFactory              = null)
        {
            var all = Cloudflare_All(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory).ToList();
            return all[Random.Shared.Next(all.Count)];
        }

        /// <summary>
        /// Randomly select one of the Cloudflare IPv4 DNS servers.
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Cloudflare_Random_IPv4(Boolean?                                                    RecursionDesired           = null,
                                                          TimeSpan?                                                   QueryTimeout               = null,
                                                          RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator = null,
                                                          ILoggerFactory?                                             LoggerFactory              = null)
        {
            var all = Cloudflare_All_IPv4(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory).ToList();
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
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Cloudflare_Random_IPv6(Boolean?                                                    RecursionDesired           = null,
                                                          TimeSpan?                                                   QueryTimeout               = null,
                                                          RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator = null,
                                                          ILoggerFactory?                                             LoggerFactory              = null)
        {
            var all = Cloudflare_All_IPv6(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory).ToList();
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
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static IEnumerable<DNSTLSClient> Cloudflare_All(Boolean?                                                    RecursionDesired           = null,
                                                               TimeSpan?                                                   QueryTimeout               = null,
                                                               RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator = null,
                                                               ILoggerFactory?                                             LoggerFactory              = null)

            => [
                   Cloudflare_IPv4_1(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory),
                   Cloudflare_IPv4_2(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory),
                   Cloudflare_IPv4_3(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory),
                   Cloudflare_IPv4_4(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory),
                   Cloudflare_IPv6_1(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory),
                   Cloudflare_IPv6_2(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory),
                   Cloudflare_IPv6_3(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory),
                   Cloudflare_IPv6_4(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory)
               ];

        /// <summary>
        /// All Cloudflare IPv4 DNS servers.
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static IEnumerable<DNSTLSClient> Cloudflare_All_IPv4(Boolean?                                                    RecursionDesired           = null,
                                                                    TimeSpan?                                                   QueryTimeout               = null,
                                                                    RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator = null,
                                                                    ILoggerFactory?                                             LoggerFactory              = null)

            => [
                   Cloudflare_IPv4_1(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory),
                   Cloudflare_IPv4_2(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory),
                   Cloudflare_IPv4_3(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory),
                   Cloudflare_IPv4_4(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory),
               ];

        /// <summary>
        /// All Cloudflare IPv6 DNS servers.
        /// </summary>
        /// <remarks>
        /// IPv6 seems to be broken sometimes!
        /// </remarks>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static IEnumerable<DNSTLSClient> Cloudflare_All_IPv6(Boolean?                                                    RecursionDesired           = null,
                                                                    TimeSpan?                                                   QueryTimeout               = null,
                                                                    RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator = null,
                                                                    ILoggerFactory?                                             LoggerFactory              = null)

            => [
                   Cloudflare_IPv6_1(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory),
                   Cloudflare_IPv6_2(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory),
                   Cloudflare_IPv6_3(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory),
                   Cloudflare_IPv6_4(RecursionDesired, QueryTimeout, RemoteCertificateValidator, LoggerFactory)
               ];


        /// <summary>
        /// Cloudflare DNS server one.one.one.one
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        public static DNSTLSClient Cloudflare_DNSName(Boolean?                                                    RecursionDesired           = null,
                                                      TimeSpan?                                                   QueryTimeout               = null,
                                                      RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator = null,
                                                      IDNSClient?                                                 DNSClient                  = null,
                                                      ILoggerFactory?                                             LoggerFactory              = null)

            => new (
                   URL.Parse("tls://one.one.one.one:853"),
                   I18NString.Create("Cloudflare (one.one.one.one)"),
                   RecursionDesired,
                   QueryTimeout,
                   RemoteCertificateValidator:  RemoteCertificateValidator,
                   DNSClient:                   DNSClient,
                   LoggerFactory:               LoggerFactory
               );

        /// <summary>
        /// Cloudflare DNS server 1.1.1.1
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Cloudflare_IPv4_1(Boolean?                                                    RecursionDesired           = null,
                                                     TimeSpan?                                                   QueryTimeout               = null,
                                                     RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator = null,
                                                     ILoggerFactory?                                             LoggerFactory              = null)

            => new (
                   URL.Parse("tls://1.1.1.1:853"),
                   I18NString.Create("Cloudflare (1.1.1.1)"),
                   RecursionDesired,
                   QueryTimeout,
                   RemoteCertificateValidator: RemoteCertificateValidator,
                   LoggerFactory:              LoggerFactory
               );

        /// <summary>
        /// Cloudflare DNS server 1.0.0.1
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Cloudflare_IPv4_2(Boolean?                                                    RecursionDesired           = null,
                                                     TimeSpan?                                                   QueryTimeout               = null,
                                                     RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator = null,
                                                     ILoggerFactory?                                             LoggerFactory              = null)

            => new (
                   URL.Parse("tls://1.0.0.1:853"),
                   I18NString.Create("Cloudflare (1.0.0.1)"),
                   RecursionDesired,
                   QueryTimeout,
                   RemoteCertificateValidator: RemoteCertificateValidator,
                   LoggerFactory:              LoggerFactory
               );

        /// <summary>
        /// Cloudflare DNS server 162.159.36.1
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Cloudflare_IPv4_3(Boolean?                                                    RecursionDesired           = null,
                                                     TimeSpan?                                                   QueryTimeout               = null,
                                                     RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator = null,
                                                     ILoggerFactory?                                             LoggerFactory              = null)

            => new (
                   URL.Parse("tls://162.159.36.1:853"),
                   I18NString.Create("Cloudflare (162.159.36.1)"),
                   RecursionDesired,
                   QueryTimeout,
                   RemoteCertificateValidator: RemoteCertificateValidator,
                   LoggerFactory:              LoggerFactory
               );

        /// <summary>
        /// Cloudflare DNS server 162.159.46.1
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Cloudflare_IPv4_4(Boolean?                                                    RecursionDesired           = null,
                                                     TimeSpan?                                                   QueryTimeout               = null,
                                                     RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator = null,
                                                     ILoggerFactory?                                             LoggerFactory              = null)

            => new (
                   URL.Parse("tls://162.159.46.1:853"),
                   I18NString.Create("Cloudflare (162.159.46.1)"),
                   RecursionDesired,
                   QueryTimeout,
                   RemoteCertificateValidator: RemoteCertificateValidator,
                   LoggerFactory:              LoggerFactory
               );

        /// <summary>
        /// Cloudflare DNS server 2606:4700:4700::1001
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Cloudflare_IPv6_1(Boolean?                                                    RecursionDesired           = null,
                                                     TimeSpan?                                                   QueryTimeout               = null,
                                                     RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator = null,
                                                     ILoggerFactory?                                             LoggerFactory              = null)

            => new (
                   URL.Parse("tls://[2606:4700:4700::1001]:853"),
                   I18NString.Create("Cloudflare (2606:4700:4700::1001)"),
                   RecursionDesired,
                   QueryTimeout,
                   RemoteCertificateValidator: RemoteCertificateValidator,
                   LoggerFactory:              LoggerFactory
               );

        /// <summary>
        /// Cloudflare DNS server 2606:4700:4700::1111
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Cloudflare_IPv6_2(Boolean?                                                    RecursionDesired           = null,
                                                     TimeSpan?                                                   QueryTimeout               = null,
                                                     RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator = null,
                                                     ILoggerFactory?                                             LoggerFactory              = null)

            => new (
                   URL.Parse("tls://[2606:4700:4700::1111]:853"),
                   I18NString.Create("Cloudflare (2606:4700:4700::1111)"),
                   RecursionDesired,
                   QueryTimeout,
                   RemoteCertificateValidator: RemoteCertificateValidator,
                   LoggerFactory:              LoggerFactory
               );

        /// <summary>
        /// Cloudflare DNS server 2606:4700:4700::0064
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Cloudflare_IPv6_3(Boolean?                                                    RecursionDesired           = null,
                                                     TimeSpan?                                                   QueryTimeout               = null,
                                                     RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator = null,
                                                     ILoggerFactory?                                             LoggerFactory              = null)

            => new (
                   URL.Parse("tls://[2606:4700:4700::0064]:853"),
                   I18NString.Create("Cloudflare (2606:4700:4700::0064)"),
                   RecursionDesired,
                   QueryTimeout,
                   RemoteCertificateValidator: RemoteCertificateValidator,
                   LoggerFactory:              LoggerFactory
               );

        /// <summary>
        /// Cloudflare DNS server 2606:4700:4700::6400
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Cloudflare_IPv6_4(Boolean?                                                    RecursionDesired           = null,
                                                     TimeSpan?                                                   QueryTimeout               = null,
                                                     RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator = null,
                                                     ILoggerFactory?                                             LoggerFactory              = null)

            => new (
                   URL.Parse("tls://[2606:4700:4700::6400]:853"),
                   I18NString.Create("Cloudflare (2606:4700:4700::6400)"),
                   RecursionDesired,
                   QueryTimeout,
                   RemoteCertificateValidator: RemoteCertificateValidator,
                   LoggerFactory:              LoggerFactory
               );

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"Using DNS server: {DNSServerLabel}";

        #endregion


        public override async ValueTask DisposeAsync()
        {
            keepalive.Dispose();
            tlsStreamLock.Dispose();
            await base.DisposeAsync();
        }


    }

}
