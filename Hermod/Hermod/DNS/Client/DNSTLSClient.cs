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
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

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

                            Boolean?                                                    DisableLogging                   = null)

            : base(IPAddress,
                   TCPPort ?? IPPort.DNS_TLS,
                   Description,

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

                   DisableLogging)

        {

            this.RecursionDesired  = RecursionDesired ?? true;
            this.QueryTimeout      = QueryTimeout     ?? TimeSpan.FromSeconds(23.5);

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
                            TCPEchoLoggingDelegate?                                     LoggingHandler                   = null,

                            Boolean?                                                    DisableLogging                   = null,
                            IDNSClient?                                                 DNSClient                        = null)

            : base(URL,
                   Description,

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
                   DNSClient)

        {

            this.RecursionDesired  = RecursionDesired ?? true;
            this.QueryTimeout      = QueryTimeout     ?? TimeSpan.FromSeconds(23.5);

            RemotePort ??= URL.Port ?? IPPort.DNS_TLS;

        }

        #endregion

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
                clientCancellationTokenSource ??= new CancellationTokenSource();

                using var timeoutCTS = CancellationTokenSource.CreateLinkedTokenSource(
                                           clientCancellationTokenSource.Token,
                                           CancellationToken
                                       );
                timeoutCTS.CancelAfter(effectiveTimeout);

                try
                {
                    return await SendAndReceiveTLSAsync(tlsStream!, data, dnsQuery, effectiveTimeout, timeoutCTS.Token).
                                     ConfigureAwait(false);
                }
                catch (IOException)
                {
                    await ReconnectAsync(CancellationToken).ConfigureAwait(false);
                    return await SendAndReceiveTLSAsync(tlsStream!, data, dnsQuery, effectiveTimeout, timeoutCTS.Token).
                                     ConfigureAwait(false);
                }

            }
            catch (OperationCanceledException) when (!CancellationToken.IsCancellationRequested)
            {

                return DNSInfo.TimedOut(
                           new DNSServerConfig(
                               RemoteIPAddress!,
                               RemotePort ?? IPPort.DNS_TLS
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
                               RemotePort ?? IPPort.DNS_TLS
                           ),
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

        #region (private) SendAndReceiveTLSAsync(...)

        /// <summary>
        /// Send a DNS query over the TLS stream and read the response.
        /// Extracted so that the IOException retry logic covers both write and read.
        /// </summary>
        private async Task<DNSInfo> SendAndReceiveTLSAsync(SslStream           TLSStream,
                                                           Byte[]              Data,
                                                           DNSPacket           DNSQuery,
                                                           TimeSpan            EffectiveTimeout,
                                                           CancellationToken   CancellationToken)
        {

            await TLSStream.WriteAsync(Data, CancellationToken).ConfigureAwait(false);
            await TLSStream.FlushAsync(CancellationToken).      ConfigureAwait(false);

            var responseLength = await TLSStream.ReadUInt16BEAsync(CancellationToken).
                                                  ConfigureAwait(false);

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

            return DNSInfo.ReadResponse(
                       new DNSServerConfig(
                           RemoteIPAddress!,
                           RemotePort ?? IPPort.DNS_TLS,
                           DNSTransport.TLS,
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
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Google_Random(Boolean?                                                    RecursionDesired             = null,
                                                 TimeSpan?                                                   QueryTimeout                 = null,
                                                 RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator   = null)

            => Google_All(RecursionDesired, QueryTimeout, RemoteCertificateValidator).
                     Skip(Random.Shared.Next(0, 4)).
                    First();

        /// <summary>
        /// Randomly select one of the Google IPv4 DNS servers.
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Google_Random_IPv4(Boolean?                                                    RecursionDesired             = null,
                                                      TimeSpan?                                                   QueryTimeout                 = null,
                                                      RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator   = null)

            => Google_All_IPv4(RecursionDesired, QueryTimeout, RemoteCertificateValidator).
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
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Google_Random_IPv6(Boolean?                                                    RecursionDesired             = null,
                                                      TimeSpan?                                                   QueryTimeout                 = null,
                                                      RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator   = null)

            => Google_All_IPv6(RecursionDesired, QueryTimeout, RemoteCertificateValidator).
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
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static IEnumerable<DNSTLSClient> Google_All(Boolean?                                                    RecursionDesired             = null,
                                                           TimeSpan?                                                   QueryTimeout                 = null,
                                                           RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator   = null)

            => [
                   Google_IPv4_1(RecursionDesired, QueryTimeout, RemoteCertificateValidator),
                   Google_IPv4_2(RecursionDesired, QueryTimeout, RemoteCertificateValidator),
                   Google_IPv6_1(RecursionDesired, QueryTimeout, RemoteCertificateValidator),
                   Google_IPv6_2(RecursionDesired, QueryTimeout, RemoteCertificateValidator)
               ];

        /// <summary>
        /// All Google IPv4 DNS servers.
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static IEnumerable<DNSTLSClient> Google_All_IPv4(Boolean?                                                    RecursionDesired             = null,
                                                                TimeSpan?                                                   QueryTimeout                 = null,
                                                                RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator   = null)

            => [
                   Google_IPv4_1(RecursionDesired, QueryTimeout, RemoteCertificateValidator),
                   Google_IPv4_2(RecursionDesired, QueryTimeout, RemoteCertificateValidator)
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
        public static IEnumerable<DNSTLSClient> Google_All_IPv6(Boolean?                                                    RecursionDesired             = null,
                                                                TimeSpan?                                                   QueryTimeout                 = null,
                                                                RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator   = null)

            => [
                   Google_IPv6_1(RecursionDesired, QueryTimeout, RemoteCertificateValidator),
                   Google_IPv6_2(RecursionDesired, QueryTimeout, RemoteCertificateValidator)
               ];


        /// <summary>
        /// Google DNS server 8.8.8.8
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Google_IPv4_1(Boolean?                                                    RecursionDesired             = null,
                                                 TimeSpan?                                                   QueryTimeout                 = null,
                                                 RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator   = null)

            => new (
                   IPv4Address.Parse("8.8.8.8"),
                   IPPort.DNS_TLS,
                   I18NString.Create("Google (8.8.8.8)"),
                   RecursionDesired,
                   QueryTimeout,
                   RemoteCertificateValidator
               );

        /// <summary>
        /// Google DNS server 8.8.4.4
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Google_IPv4_2(Boolean?                                                    RecursionDesired             = null,
                                                 TimeSpan?                                                   QueryTimeout                 = null,
                                                 RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator   = null)

            => new (
                   URL.Parse("tls://8.8.4.4:853"),
                   I18NString.Create("Google (8.8.4.4)"),
                   RecursionDesired,
                   QueryTimeout,
                   RemoteCertificateValidator
               );

        /// <summary>
        /// Google DNS server 2001:4860:4860::8888
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Google_IPv6_1(Boolean?                                                    RecursionDesired             = null,
                                                 TimeSpan?                                                   QueryTimeout                 = null,
                                                 RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator   = null)

            => new (
                   IPv6Address.Parse("2001:4860:4860::8888"),
                   IPPort.DNS_TLS,
                   I18NString.Create("Google (2001:4860:4860::8888)"),
                   RecursionDesired,
                   QueryTimeout,
                   RemoteCertificateValidator
               );

        /// <summary>
        /// Google DNS server 2001:4860:4860::8844
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Google_IPv6_2(Boolean?                                                    RecursionDesired             = null,
                                                 TimeSpan?                                                   QueryTimeout                 = null,
                                                 RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator   = null)

            => new (
                   URL.Parse("tls://[2001:4860:4860::8844]:853"),
                   I18NString.Create("Google (2001:4860:4860::8844)"),
                   RecursionDesired,
                   QueryTimeout,
                   RemoteCertificateValidator
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
        public static DNSTLSClient Cloudflare_Random(Boolean?                                                    RecursionDesired             = null,
                                                     TimeSpan?                                                   QueryTimeout                 = null,
                                                     RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator   = null)

            => Cloudflare_All(RecursionDesired, QueryTimeout, RemoteCertificateValidator).
                         Skip(Random.Shared.Next(0, 8)).
                        First();

        /// <summary>
        /// Randomly select one of the Cloudflare IPv4 DNS servers.
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Cloudflare_Random_IPv4(Boolean?                                                    RecursionDesired             = null,
                                                          TimeSpan?                                                   QueryTimeout                 = null,
                                                          RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator   = null)

            => Cloudflare_All_IPv4(RecursionDesired, QueryTimeout, RemoteCertificateValidator).
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
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Cloudflare_Random_IPv6(Boolean?                                                    RecursionDesired             = null,
                                                          TimeSpan?                                                   QueryTimeout                 = null,
                                                          RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator   = null)

            => Cloudflare_All_IPv6(RecursionDesired, QueryTimeout, RemoteCertificateValidator).
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
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static IEnumerable<DNSTLSClient> Cloudflare_All(Boolean?                                                    RecursionDesired             = null,
                                                               TimeSpan?                                                   QueryTimeout                 = null,
                                                               RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator   = null)

            => [
                   Cloudflare_IPv4_1(RecursionDesired, QueryTimeout, RemoteCertificateValidator),
                   Cloudflare_IPv4_2(RecursionDesired, QueryTimeout, RemoteCertificateValidator),
                   Cloudflare_IPv4_3(RecursionDesired, QueryTimeout, RemoteCertificateValidator),
                   Cloudflare_IPv4_4(RecursionDesired, QueryTimeout, RemoteCertificateValidator),
                   Cloudflare_IPv6_1(RecursionDesired, QueryTimeout, RemoteCertificateValidator),
                   Cloudflare_IPv6_2(RecursionDesired, QueryTimeout, RemoteCertificateValidator),
                   Cloudflare_IPv6_3(RecursionDesired, QueryTimeout, RemoteCertificateValidator),
                   Cloudflare_IPv6_4(RecursionDesired, QueryTimeout, RemoteCertificateValidator)
               ];

        /// <summary>
        /// All Cloudflare IPv4 DNS servers.
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static IEnumerable<DNSTLSClient> Cloudflare_All_IPv4(Boolean?                                                    RecursionDesired             = null,
                                                                    TimeSpan?                                                   QueryTimeout                 = null,
                                                                    RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator   = null)

            => [
                   Cloudflare_IPv4_1(RecursionDesired, QueryTimeout, RemoteCertificateValidator),
                   Cloudflare_IPv4_2(RecursionDesired, QueryTimeout, RemoteCertificateValidator),
                   Cloudflare_IPv4_3(RecursionDesired, QueryTimeout, RemoteCertificateValidator),
                   Cloudflare_IPv4_4(RecursionDesired, QueryTimeout, RemoteCertificateValidator),
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
        public static IEnumerable<DNSTLSClient> Cloudflare_All_IPv6(Boolean?                                                    RecursionDesired             = null,
                                                                    TimeSpan?                                                   QueryTimeout                 = null,
                                                                    RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator   = null)

            => [
                   Cloudflare_IPv6_1(RecursionDesired, QueryTimeout, RemoteCertificateValidator),
                   Cloudflare_IPv6_2(RecursionDesired, QueryTimeout, RemoteCertificateValidator),
                   Cloudflare_IPv6_3(RecursionDesired, QueryTimeout, RemoteCertificateValidator),
                   Cloudflare_IPv6_4(RecursionDesired, QueryTimeout, RemoteCertificateValidator)
               ];


        /// <summary>
        /// Cloudflare DNS server one.one.one.one
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        public static DNSTLSClient Cloudflare_DNSName(Boolean?                                                    RecursionDesired             = null,
                                                      TimeSpan?                                                   QueryTimeout                 = null,
                                                      RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator   = null,
                                                      IDNSClient?                                                 DNSClient                    = null)

            => new (
                   URL.Parse("tls://one.one.one.one:853"),
                   I18NString.Create("Cloudflare (one.one.one.one)"),
                   RecursionDesired,
                   QueryTimeout,
                   RemoteCertificateValidator,
                   DNSClient: DNSClient
               );

        /// <summary>
        /// Cloudflare DNS server 1.1.1.1
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Cloudflare_IPv4_1(Boolean?                                                    RecursionDesired             = null,
                                                     TimeSpan?                                                   QueryTimeout                 = null,
                                                     RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator   = null)

            => new (
                   URL.Parse("tls://1.1.1.1:853"),
                   I18NString.Create("Cloudflare (1.1.1.1)"),
                   RecursionDesired,
                   QueryTimeout,
                   RemoteCertificateValidator
               );

        /// <summary>
        /// Cloudflare DNS server 1.0.0.1
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Cloudflare_IPv4_2(Boolean?                                                    RecursionDesired             = null,
                                                     TimeSpan?                                                   QueryTimeout                 = null,
                                                     RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator   = null)

            => new (
                   URL.Parse("tls://1.0.0.1:853"),
                   I18NString.Create("Cloudflare (1.0.0.1)"),
                   RecursionDesired,
                   QueryTimeout,
                   RemoteCertificateValidator
               );

        /// <summary>
        /// Cloudflare DNS server 162.159.36.1
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Cloudflare_IPv4_3(Boolean?                                                    RecursionDesired             = null,
                                                     TimeSpan?                                                   QueryTimeout                 = null,
                                                     RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator   = null)

            => new (
                   URL.Parse("tls://162.159.36.1:853"),
                   I18NString.Create("Cloudflare (162.159.36.1)"),
                   RecursionDesired,
                   QueryTimeout,
                   RemoteCertificateValidator
               );

        /// <summary>
        /// Cloudflare DNS server 162.159.46.1
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Cloudflare_IPv4_4(Boolean?                                                    RecursionDesired             = null,
                                                     TimeSpan?                                                   QueryTimeout                 = null,
                                                     RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator   = null)

            => new (
                   URL.Parse("tls://162.159.46.1:853"),
                   I18NString.Create("Cloudflare (162.159.46.1)"),
                   RecursionDesired,
                   QueryTimeout,
                   RemoteCertificateValidator
               );

        /// <summary>
        /// Cloudflare DNS server 2606:4700:4700::1001
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Cloudflare_IPv6_1(Boolean?                                                    RecursionDesired             = null,
                                                     TimeSpan?                                                   QueryTimeout                 = null,
                                                     RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator   = null)

            => new (
                   URL.Parse("tls://[2606:4700:4700::1001]:853"),
                   I18NString.Create("Cloudflare (2606:4700:4700::1001)"),
                   RecursionDesired,
                   QueryTimeout,
                   RemoteCertificateValidator
               );

        /// <summary>
        /// Cloudflare DNS server 2606:4700:4700::1111
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Cloudflare_IPv6_2(Boolean?                                                    RecursionDesired             = null,
                                                     TimeSpan?                                                   QueryTimeout                 = null,
                                                     RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator   = null)

            => new (
                   URL.Parse("tls://[2606:4700:4700::1111]:853"),
                   I18NString.Create("Cloudflare (2606:4700:4700::1111)"),
                   RecursionDesired,
                   QueryTimeout,
                   RemoteCertificateValidator
               );

        /// <summary>
        /// Cloudflare DNS server 2606:4700:4700::0064
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Cloudflare_IPv6_3(Boolean?                                                    RecursionDesired             = null,
                                                     TimeSpan?                                                   QueryTimeout                 = null,
                                                     RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator   = null)

            => new (
                   URL.Parse("tls://[2606:4700:4700::0064]:853"),
                   I18NString.Create("Cloudflare (2606:4700:4700::0064)"),
                   RecursionDesired,
                   QueryTimeout,
                   RemoteCertificateValidator
               );

        /// <summary>
        /// Cloudflare DNS server 2606:4700:4700::6400
        /// </summary>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        public static DNSTLSClient Cloudflare_IPv6_4(Boolean?                                                    RecursionDesired             = null,
                                                     TimeSpan?                                                   QueryTimeout                 = null,
                                                     RemoteTLSServerCertificateValidationHandler<DNSTLSClient>?  RemoteCertificateValidator   = null)

            => new (
                   URL.Parse("tls://[2606:4700:4700::6400]:853"),
                   I18NString.Create("Cloudflare (2606:4700:4700::6400)"),
                   RecursionDesired,
                   QueryTimeout,
                   RemoteCertificateValidator
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
            tlsStreamLock.Dispose();
            await base.DisposeAsync();
        }


    }

}
