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
using System.Runtime.CompilerServices;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.TCP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP.Notifications;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A delegate to calculate the delay between transmission retries.
    /// </summary>
    /// <param name="RetryCount">The retry counter.</param>
    public delegate TimeSpan TransmissionRetryDelayDelegate(UInt32 RetryCount);


    /// <summary>
    /// An abstract TCP client.
    /// </summary>
    public abstract class ATCPClient : IDisposable,
                                       IAsyncDisposable
    {

        #region Data

        public static readonly    TimeSpan                 DefaultConnectTimeout            = TimeSpan.FromSeconds(5);
        public static readonly    TimeSpan                 DefaultReceiveTimeout            = TimeSpan.FromSeconds(5);
        public static readonly    TimeSpan                 DefaultSendTimeout               = TimeSpan.FromSeconds(5);

        /// <summary>
        /// The default maximum number of transmission retries for HTTP request.
        /// </summary>
        public const              UInt16                   DefaultMaxNumberOfRetries        = 3;

        /// <summary>
        /// The default delay between transmission retries.
        /// </summary>
        public static readonly    TimeSpan                 DefaultTransmissionRetryDelay    = TimeSpan.FromSeconds(2);

        public const              UInt32                   DefaultInternalBufferSize                = 4096;

        protected                 TcpClient?               tcpClient;

        /// <summary>
        /// Cancelled when this <b>client</b> goes away - <see cref="Close"/> and
        /// <see cref="DisposeAsync"/>, and nothing else. A caller's request
        /// links its own token to this one, so shutting the client down stops
        /// what is in flight.
        /// </summary>
        protected                 CancellationTokenSource  clientCancellationTokenSource;

        /// <summary>
        /// Cancelled when this <b>connection</b> goes away - every reconnect -
        /// for whatever belongs to the socket rather than to the caller.
        /// </summary>
        /// <remarks>
        /// The two used to be one, and a reconnect cancelled the client's token.
        /// That is not a shade of meaning: a request whose token derives from
        /// the client's is killed by the very reconnect it asked for, so
        /// SendRequest's retry loop reconnected, cancelled itself, found the
        /// token dead on the next two attempts, ran out of retries, and returned
        /// a response no server had sent - back then an "HTTP 400 - Maximum HTTP
        /// retries reached!", which the DNS clients duly reported as the
        /// resolver's own answer. It says 0-ClientError now.
        ///
        /// A reconnect tears down a socket. It has no business ending the
        /// request that provoked it.
        /// </remarks>
        protected                 CancellationTokenSource  connectionCancellationTokenSource;
        private                   Int32                    forceDNSCacheUpdateOnNextConnect;
        private                   DateTimeOffset?          connectionEstablishedAt;

        private readonly          ILogger<ATCPClient>      logger;
        private readonly          ILoggerFactory           loggerFactory;

        /// <summary>
        /// Whether this client made its DNS client itself, and so is the one
        /// to dispose of it. One that was handed in belongs to whoever handed
        /// it in, and may be serving others as well.
        /// </summary>
        private readonly          Boolean                  ownsDNSClient;

        /// <summary>
        /// The DNS client, held as a <see cref="Lazy{T}"/> so that one of this
        /// client's own making is built when it is first asked for, and not
        /// before. A DNS client that was handed in is already there and is
        /// wrapped as a value, so both cases read the same below.
        /// </summary>
        private readonly          Lazy<IDNSClient>         dnsClient;

        #endregion

        #region Properties

        /// <summary>
        /// The description of this TCP client.
        /// </summary>
        public I18NString                       Description               { get; }


        #region Local Socket

        /// <summary>
        /// The local IP socket.
        /// </summary>
        public IPSocket?                        LocalSocket               { get; private set; }

        /// <summary>
        /// The local IP end point.
        /// </summary>
        public IPEndPoint?                      CurrentLocalEndPoint      { get; private set; }

        /// <summary>
        /// The local TCP port.
        /// </summary>
        public UInt16?                          CurrentLocalPort

            => CurrentLocalEndPoint is not null
                   ? (UInt16) CurrentLocalEndPoint.Port
                   : null;

        /// <summary>
        /// The local IP address.
        /// </summary>
        public IIPAddress?                      CurrentLocalIPAddress

            => CurrentLocalEndPoint is not null
                   ? IPAddress.Parse(CurrentLocalEndPoint.Address.GetAddressBytes())
                   : null;

        #endregion

        #region Remote Socket

        /// <summary>
        /// The remote IP socket.
        /// </summary>
        public IPSocket?                        RemoteSocket              { get; private set; }

        /// <summary>
        /// The remote IP end point.
        /// </summary>
        public IPEndPoint?                      CurrentRemoteEndPoint     { get; private set; }

        /// <summary>
        /// The remote TCP port.
        /// </summary>
        public UInt16?                          CurrentRemotePort

            => CurrentRemoteEndPoint is not null
                   ? (UInt16) CurrentRemoteEndPoint.Port
                   : null;

        /// <summary>
        /// The remote IP address.
        /// </summary>
        public IIPAddress?                      CurrentRemoteIPAddress

            => CurrentRemoteEndPoint is not null
                   ? IPAddress.Parse(CurrentRemoteEndPoint.Address.GetAddressBytes())
                   : null;

        #endregion


        public  URL                             RemoteURL                 { get; }
        public  IIPAddress?                     RemoteIPAddress           { get; private set; }
        public  IPPort?                         RemotePort                { get; protected set; }


        /// <summary>
        /// The DNS Name to lookup in order to resolve high available IP addresses and TCP ports.
        /// </summary>
        public  DomainName?                     DomainName                { get; }

        /// <summary>
        /// The DNS Service to lookup in order to resolve high available IP addresses and TCP ports.
        /// </summary>
        public  SRV_Spec?                       DNSService                { get; }


        public  IIPAddress?                     ResolvedIPAddress         { get; protected set; }
        public  HashSet<IIPAddress>             ResolvedIPAddresses       { get; } = [];


        /// <summary>
        /// Whether the client is currently connected to the.
        /// </summary>
        public  Boolean                         IsConnected
            => tcpClient?.Connected ?? false;

        public  IPVersionPreference             IPVersionPreference       { get; }
        public  TimeSpan                        ConnectTimeout            { get; }
        public  TimeSpan                        ReceiveTimeout            { get; }
        public  TimeSpan                        SendTimeout               { get; }
        public  TransmissionRetryDelayDelegate  TransmissionRetryDelay    { get; }
        public  UInt16                          MaxNumberOfRetries        { get; } = DefaultMaxNumberOfRetries;
        public  UInt32                          InternalBufferSize        { get; }

        /// <summary>
        /// An optional maximum lifetime for the current TCP connection.
        /// When this value is set, long-lived clients should reconnect once the lifetime is exceeded.
        /// </summary>
        public  TimeSpan?                       MaxConnectionLifetime     { get; set; }

        /// <summary>
        /// The timestamp when the current TCP connection was established.
        /// </summary>
        public  DateTimeOffset?                 ConnectionEstablishedAt
            => connectionEstablishedAt;

        /// <summary>
        /// The current age of the TCP connection, if connected.
        /// </summary>
        public  TimeSpan?                       ConnectionAge
            => connectionEstablishedAt.HasValue
                   ? Timestamp.Now - connectionEstablishedAt.Value
                   : null;

        /// <summary>
        /// The timestamp when the current TCP connection should be renewed, if a maximum lifetime is configured.
        /// </summary>
        public  DateTimeOffset?                 NextConnectionRenewalAt
            => connectionEstablishedAt.HasValue && MaxConnectionLifetime.HasValue
                   ? connectionEstablishedAt.Value + MaxConnectionLifetime.Value
                   : null;

        /// <summary>
        /// Whether the current TCP connection exceeded its configured maximum lifetime.
        /// </summary>
        public  Boolean                         IsConnectionLifetimeExceeded
            => NextConnectionRenewalAt.HasValue &&
               Timestamp.Now >= NextConnectionRenewalAt.Value;

        /// <summary>
        /// When enabled, reconnects force an upstream DNS lookup and refresh the DNS cache with the new result.
        /// </summary>
        public  Boolean                         ForceDNSCacheUpdateOnReconnect { get; set; } = true;


        /// <summary>
        /// Disable logging of connection events and errors.
        /// </summary>
        public Boolean                          DisableLogging            { get; }

        /// <summary>
        /// The DNS client defines which DNS servers to use.
        /// </summary>
        /// <remarks>
        /// Reading this builds the default DNS client if none was handed in and
        /// none has been built yet. The default searches the machine's network
        /// configuration for resolvers, which costs ~38 ms - more than thirty
        /// times a request on loopback - so a client that never resolves a name
        /// must never pay for it. Dialling a literal IP address resolves
        /// nothing: see where ResolvedIPAddresses is filled in Connect.
        /// </remarks>
        public  IDNSClient?                     DNSClient
            => dnsClient.Value;

        #endregion

        #region Events

        public event TCPEchoLoggingDelegate?  OnLogs;

        #endregion

        #region Constructor(s)

        #region (private)   ATCPClient(...)

        private ATCPClient(I18NString?                      Description              = null,
                           IPVersionPreference?             IPVersionPreference      = null,
                           TimeSpan?                        ConnectTimeout           = null,
                           TimeSpan?                        ReceiveTimeout           = null,
                           TimeSpan?                        SendTimeout              = null,
                           TransmissionRetryDelayDelegate?  TransmissionRetryDelay   = null,
                           UInt16?                          MaxNumberOfRetries       = null,
                           UInt32?                          InternalBufferSize       = null,

                           Boolean?                         DisableLogging           = null,
                             // String?                       LoggingPath              = null,
                             // String?                       LoggingContext           = Logger.DefaultContext,
                             // LogfileCreatorDelegate?       LogfileCreator           = null,
                           IDNSClient?                      DNSClient                = null,
                           ILogger<ATCPClient>?             Logger                   = null,
                           ILoggerFactory?                  LoggerFactory            = null)
        {

            if (ConnectTimeout.HasValue && ConnectTimeout.Value.TotalMilliseconds > Int32.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(ConnectTimeout), "Timeout too large for socket.");

            if (ReceiveTimeout.HasValue && ReceiveTimeout.Value.TotalMilliseconds > Int32.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(ReceiveTimeout), "Timeout too large for socket.");

            if (SendTimeout.   HasValue && SendTimeout.   Value.TotalMilliseconds > Int32.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(SendTimeout),    "Timeout too large for socket.");

            this.Description                    = Description            ?? I18NString.Empty;
            this.IPVersionPreference            = IPVersionPreference    ?? Hermod.IPVersionPreference.PreferIPv6;
            this.InternalBufferSize             = InternalBufferSize.HasValue
                                                      ? InternalBufferSize.Value > Int32.MaxValue
                                                            ? throw new ArgumentOutOfRangeException(nameof(InternalBufferSize), "The buffer size must not exceed Int32.MaxValue!")
                                                            : InternalBufferSize.Value
                                                      : DefaultInternalBufferSize;
            this.ConnectTimeout                 = ConnectTimeout         ?? DefaultConnectTimeout;
            this.ReceiveTimeout                 = ReceiveTimeout         ?? DefaultReceiveTimeout;
            this.SendTimeout                    = SendTimeout            ?? DefaultSendTimeout;
            this.TransmissionRetryDelay         = TransmissionRetryDelay ?? (retryCounter => TimeSpan.FromSeconds(retryCounter * retryCounter * DefaultTransmissionRetryDelay.TotalSeconds));
            this.MaxNumberOfRetries             = MaxNumberOfRetries     ?? DefaultMaxNumberOfRetries;

            this.DisableLogging                 = DisableLogging         ?? false;
            this.logger                         = Logger                 ?? NullLogger<ATCPClient>.Instance;
            this.loggerFactory                  = LoggerFactory          ?? NullLoggerFactory.Instance;
            this.ownsDNSClient                  = DNSClient is null;

            // Deferred rather than built here: the default DNS client searches
            // the machine's network configuration for resolvers - two full
            // sweeps of every network interface - and a client per request paid
            // 38.3 ms of that for a request taking 1.06 ms. Measured by
            // `tests/h1bench -- connect` in HTTP1ConformanceTests, which is
            // also the regression test.
            var dnsLoggerFactory                = this.loggerFactory;
            this.dnsClient                      = DNSClient is not null
                                                      ? new Lazy<IDNSClient>(DNSClient)
                                                      : new Lazy<IDNSClient>(() => new DNSClient(
                                                                                       Logger: dnsLoggerFactory.CreateLogger<IDNSClient>()
                                                                                   ));

            this.clientCancellationTokenSource      = new CancellationTokenSource();
            this.connectionCancellationTokenSource  = new CancellationTokenSource();

        }

        #endregion

        #region (protected) ATCPClient(IPAddress,  TCPPort,           ...)

        protected ATCPClient(IIPAddress                       IPAddress,
                             IPPort                           TCPPort,
                             I18NString?                      Description              = null,
                             IPVersionPreference?             IPVersionPreference      = null,
                             TimeSpan?                        ConnectTimeout           = null,
                             TimeSpan?                        ReceiveTimeout           = null,
                             TimeSpan?                        SendTimeout              = null,
                             TransmissionRetryDelayDelegate?  TransmissionRetryDelay   = null,
                             UInt16?                          MaxNumberOfRetries       = null,
                             UInt32?                          InternalBufferSize       = null,

                             // String?                         LoggingPath              = null,
                             // String?                         LoggingContext           = Logger.DefaultContext,
                             // LogfileCreatorDelegate?         LogfileCreator           = null,
                             Boolean?                         DisableLogging           = null,
                             ILogger<ATCPClient>?             Logger                   = null,
                             ILoggerFactory?                  LoggerFactory            = null)

            : this(Description,
                   IPVersionPreference,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   InternalBufferSize,

                   DisableLogging,
                   null,
                   Logger,
                   LoggerFactory)

        {

            this.RemotePort       = TCPPort;
            this.RemoteIPAddress  = IPAddress;

            this.RemoteSocket     = new IPSocket(
                                        IPAddress,
                                        TCPPort
                                    );

            // Note: This used to stay default(URL), so every consumer of RemoteURL had to
            //       cope with a URL that has neither a scheme nor a host. Most notably the
            //       TLS decision in AHTTPClient.ConnectAsync() reads RemoteURL.Scheme and
            //       therefore silently depended on whatever default(URL) happened to yield.
            //
            //       The scheme is tcp, because that is all this transport layer knows: it
            //       neither enforces TLS nor implies a default port. Whether the connection
            //       becomes TLS is decided by EnforceTLS and the derived clients.
            if (URL.TryParse($"{URIScheme.tcp.Prefix}{IPAddress}:{TCPPort}", out var remoteURL))
                this.RemoteURL = remoteURL;

        }

        #endregion

        #region (protected) ATCPClient(URL, ...)

        protected ATCPClient(URL                              URL,
                             I18NString?                      Description              = null,
                             IPVersionPreference?             IPVersionPreference      = null,
                             TimeSpan?                        ConnectTimeout           = null,
                             TimeSpan?                        ReceiveTimeout           = null,
                             TimeSpan?                        SendTimeout              = null,
                             TransmissionRetryDelayDelegate?  TransmissionRetryDelay   = null,
                             UInt16?                          MaxNumberOfRetries       = null,
                             UInt32?                          InternalBufferSize       = null,

                             Boolean?                         DisableLogging           = null,
                             // String?                         LoggingPath              = null,
                             // String?                         LoggingContext           = Logger.DefaultContext,
                             // LogfileCreatorDelegate?         LogfileCreator           = null,
                             IDNSClient?                      DNSClient                = null,
                             ILogger<ATCPClient>?             Logger                   = null,
                             ILoggerFactory?                  LoggerFactory            = null)

            : this(Description,
                   IPVersionPreference,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   InternalBufferSize,

                   DisableLogging,
                   DNSClient,
                   Logger,
                   LoggerFactory)

        {

            this.RemoteURL = URL;

        }

        #endregion

        #region (protected) ATCPClient(DomainName, DNSService,        ...)

        protected ATCPClient(DomainName                       DomainName,
                             SRV_Spec                         DNSService,
                             I18NString?                      Description              = null,
                             IPVersionPreference?             IPVersionPreference      = null,
                             TimeSpan?                        ConnectTimeout           = null,
                             TimeSpan?                        ReceiveTimeout           = null,
                             TimeSpan?                        SendTimeout              = null,
                             TransmissionRetryDelayDelegate?  TransmissionRetryDelay   = null,
                             UInt16?                          MaxNumberOfRetries       = null,
                             UInt32?                          InternalBufferSize       = null,

                             Boolean?                         DisableLogging           = null,
                             // String?                         LoggingPath              = null,
                             // String?                         LoggingContext           = Logger.DefaultContext,
                             // LogfileCreatorDelegate?         LogfileCreator           = null,
                             IDNSClient?                      DNSClient                = null,
                             ILogger<ATCPClient>?             Logger                   = null,
                             ILoggerFactory?                  LoggerFactory            = null)

            : this(Description,
                   IPVersionPreference,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   InternalBufferSize,

                   DisableLogging,
                   DNSClient,
                   Logger,
                   LoggerFactory)

        {

            this.DomainName  = DomainName;
            this.DNSService  = DNSService;

        }

        #endregion

        #endregion


        #region (protected) LiveClientCancellationTokenSource

        /// <summary>
        /// The client's cancellation token source, replaced if the one on hand
        /// has already been cancelled.
        /// </summary>
        /// <remarks>
        /// Every place that reaches for this used to say
        /// <c>clientCancellationTokenSource ??= new CancellationTokenSource()</c>,
        /// and that asks the wrong question: <c>??=</c> fills in a token source
        /// which is <b>missing</b>, never one which is <b>spent</b>. Cancelling
        /// it does not make it null.
        ///
        /// What that cost is worth writing down, because the symptom named
        /// nothing. <see cref="ReconnectAsync"/> cancels this source to tear the
        /// old connection down, and the reconnect below installs a fresh one -
        /// but between the two, and on every path that cancels without getting
        /// that far, the field holds a source which is already spent. A caller
        /// then links its own timeout to a token that is dead on arrival, the
        /// very first await throws <c>TaskCanceledException</c>, and it does so
        /// in under a millisecond of, say, a 23.5 second budget. In the DNS over
        /// HTTPS client that arrived as "the query timed out" - silently, and
        /// after no time at all.
        ///
        /// Not thread-safe, and no less so than the <c>??=</c> it replaces: two
        /// callers arriving together can still each make one. Whoever needs more
        /// than that wants a lock here, not a second field.
        /// </remarks>
        protected CancellationTokenSource LiveClientCancellationTokenSource
        {
            get
            {

                // Deliberately not disposed. A query still in flight may hold a
                // linked source built from it, and disposing underneath that one
                // trades this fault for an ObjectDisposedException. The teardown
                // path in ReconnectAsync disposes, because there nothing is left
                // to be in flight.
                if (clientCancellationTokenSource is null ||
                    clientCancellationTokenSource.IsCancellationRequested)
                {
                    clientCancellationTokenSource = new CancellationTokenSource();
                }

                return clientCancellationTokenSource;

            }
        }

        #endregion

        #region (protected) LiveConnectionCancellationTokenSource / CycleConnectionToken()

        /// <summary>
        /// The current connection's cancellation token source, for work which
        /// belongs to the socket and should end when the socket does.
        /// </summary>
        protected CancellationTokenSource LiveConnectionCancellationTokenSource
        {
            get
            {

                if (connectionCancellationTokenSource is null ||
                    connectionCancellationTokenSource.IsCancellationRequested)
                {
                    connectionCancellationTokenSource = new CancellationTokenSource();
                }

                return connectionCancellationTokenSource;

            }
        }

        /// <summary>
        /// Ends the current connection's token and hands out a fresh one. What
        /// every reconnect does, and what none of them may do to the client's
        /// token: a caller's request is linked to that one.
        /// </summary>
        protected void CycleConnectionToken()
        {

            var spent = connectionCancellationTokenSource;

            connectionCancellationTokenSource = new CancellationTokenSource();

            // Cancelled after the replacement is in place, so that anything
            // waking up on the cancellation already finds the new one.
            try { spent?.Cancel();  } catch { }
            try { spent?.Dispose(); } catch { }

        }

        #endregion


        #region ReconnectAsync(CancellationToken = default)

        public virtual async Task<TCPConnectionResult>

            ReconnectAsync(CancellationToken CancellationToken = default)

        {

            try
            {

                // The connection's token, not the client's. A caller's request
                // links to the client's, and this method is most often called
                // from inside such a request - SendRequest's retry loop - so
                // cancelling that one here ended the very request that asked
                // for the reconnect.
                CycleConnectionToken();

                try { tcpClient?.Client?.Shutdown(SocketShutdown.Both); } catch { }
                try { tcpClient?.Close();                               } catch { }
                try { tcpClient?.Dispose();                             } catch { }
                tcpClient = null;

                ResolvedIPAddress = null;
                ResolvedIPAddresses.Clear();
                connectionEstablishedAt = null;

                CurrentLocalEndPoint  = null;
                CurrentRemoteEndPoint = null;
                LocalSocket           = null;
                RemoteSocket          = null;

                if (ForceDNSCacheUpdateOnReconnect)
                    ForceDNSCacheUpdateOnNextConnect();

            }
            catch (Exception e)
            {

                await Log(e.Message);

                if (e.StackTrace is not null)
                    await Log(e.StackTrace);

            }

            // recreates _cts and tcpClient
            return await ConnectAsync__(CancellationToken);

        }

        #endregion

        #region (protected) ConnectAsync(CancellationToken = default)

        protected virtual async Task<TCPConnectionResult>

            ConnectAsync(CancellationToken CancellationToken = default)

        {

            return await ConnectAsync__(CancellationToken);

        }

        #endregion



        #region (protected) ConnectAsync(CancellationToken = default)

        private async Task<TCPConnectionResult>

            ConnectAsync__(CancellationToken CancellationToken = default)

        {

            var timings = new TCPClientConnectTimings();
            var forceDNSCacheUpdate = Interlocked.Exchange(ref forceDNSCacheUpdateOnNextConnect, 0) == 1;

            try
            {

                DomainName? dnsSRVRemoteHost   = null;
                IPPort?     dnsSRVRemotePort   = null;

                // IPvXAddress.Localhost - what the port-only constructors and
                // ConnectNew connect to - is both loopback addresses, and the
                // preference picks one of them below, as it does for a host name
                // with both an A and an AAAA record. As itself it was neither an
                // IPv4Address nor an IPv6Address to that choice: IPv4Only and
                // IPv6Only found nothing and failed without dialling, and
                // PreferIPv4 fell back to it and dialled what ToDotNet() makes
                // of it, [::1].
                if (RemoteIPAddress is IPvXAddress { IsLocalhost: true })
                {
                    ResolvedIPAddresses.Add(IPv4Address.Localhost);
                    ResolvedIPAddresses.Add(IPv6Address.Localhost);
                }

                else if (RemoteIPAddress is not null)
                    ResolvedIPAddresses.Add(RemoteIPAddress);

                if (ResolvedIPAddresses.Count == 0)
                {

                    var hostname = (DomainName?.FullName ?? RemoteURL.Host.ToString())?.Trim() ?? "";

                    #region URL looks like an IP address / Localhost...

                    // An address is itself. Every one starting with "127." used
                    // to be taken for localhost, and 127.0.0.2 was dialled as
                    // 127.0.0.1.
                    if      (IPAddress.IsIPv4(hostname))
                        ResolvedIPAddresses.Add(IPv4Address.Parse(hostname));

                    // TryParseURIHost rather than TryParse, because this text came
                    // out of an URL: RFC 6874 has the "%" before a zone identifier
                    // percent-encoded there, so fe80::a%en1 is written
                    // http://[fe80::a%25en1]. The plain parser splits at the first
                    // "%" and read the zone of that URL as "25en1" — a link-local
                    // address dialled through an interface that does not exist, and
                    // a success rather than a failure, which is the worse of the
                    // two.
                    else if (IPv6Address.TryParseURIHost(hostname, out var ipv6FromURI))
                        ResolvedIPAddresses.Add(ipv6FromURI);

                    // "localhost" stays 127.0.0.1 - a client that names no
                    // preference asks for PreferIPv6, and must still reach the
                    // servers that listen on 127.0.0.1 or 0.0.0.0 alone - except
                    // where only IPv6 will do. IPv6Only used to fail here, saying
                    // that none of the resolved addresses was of its family.
                    else if (IPAddress.IsIPv4Localhost(hostname))
                        ResolvedIPAddresses.Add(IPVersionPreference == IPVersionPreference.IPv6Only
                                                    ? IPv6Address.Localhost
                                                    : IPv4Address.Localhost);

                    else if (IPAddress.IsIPv6Localhost(hostname))
                        ResolvedIPAddresses.Add(IPv6Address.Localhost);

                    #endregion

                    #region DNS SRV    lookups...

                    if (ResolvedIPAddresses.Count == 0         &&
                        DNSService.         IsNotNullOrEmpty() &&
                        DNSClient           is not null)
                    {

                        DebugX.LogT($"DNS SRV queries for '{DNSService}.{hostname}'...");

                        // Look up the DNS Name or the hostname of the URL...
                        var serviceRecords         = await DNSClient.Query_DNSService(
                                                                DNSServiceName:     DNSServiceName.Parse($"{DNSService}.{hostname}"),
                                                                RecursionDesired:   true,
                                                                ForceUpdate:        forceDNSCacheUpdate,
                                                                CancellationToken:  CancellationToken
                                                            ).ConfigureAwait(false);

                        DebugX.LogT($"DNS SRV: {serviceRecords.Count()} service records found:" + serviceRecords.Select(serviceRecord => serviceRecord.ToString()).AggregateWith(", "));

                        var minPriority            = serviceRecords. Min  (serviceRecord => serviceRecord.Priority);
                        var priorityRecords        = serviceRecords. Where(serviceRecord => serviceRecord.Priority == minPriority).ToArray();
                        var totalWeight            = priorityRecords.Sum  (serviceRecord => serviceRecord.Weight);

                        // Uniform random selection if no weighted choice was made...
                        var selectedServiceRecord  = priorityRecords[Random.Shared.Next(priorityRecords.Length)];

                        if (totalWeight > 0)
                        {

                            var random      = Random.Shared.Next(totalWeight);
                            var cumulative  = 0;

                            foreach (var rec in priorityRecords)
                            {
                                cumulative += rec.Weight;
                                if (random < cumulative)
                                {
                                    selectedServiceRecord = rec;
                                    break;
                                }
                            }

                        }

                        dnsSRVRemoteHost = selectedServiceRecord.Target;
                        dnsSRVRemotePort = selectedServiceRecord.Port;

                        timings.DNSSRVLookup = timings.Elapsed;

                    }

                    #endregion

                    #region DNS A/AAAA lookups...

                    if (ResolvedIPAddresses.Count == 0 &&
                        DNSClient           is not null)
                    {

                        var remote = dnsSRVRemoteHost ?? DomainName.Parse(hostname);

                        //DebugX.LogT($"DNS A/AAAA queries for '{remote}'...");

                        // Look up the DNS SRV remote host or the hostname of the URL...
                        var ipv4AddressLookupTask  = DNSClient.Query_IPv4Addresses(
                                                         dnsSRVRemoteHost ?? DomainName.Parse(hostname),
                                                         RecursionDesired:   true,
                                                         ForceUpdate:        forceDNSCacheUpdate,
                                                         CancellationToken:  CancellationToken
                                                     );

                        var ipv6AddressLookupTask  = DNSClient.Query_IPv6Addresses(
                                                         dnsSRVRemoteHost ?? DomainName.Parse(hostname),
                                                         RecursionDesired:   true,
                                                         ForceUpdate:        forceDNSCacheUpdate,
                                                         CancellationToken:  CancellationToken
                                                     );

                        await Task.WhenAll(
                                  ipv4AddressLookupTask,
                                  ipv6AddressLookupTask
                              ).ConfigureAwait(false);

                        //DebugX.LogT(   $"A{(PreferIPv4 == IPVersionPreference.IPv4 ? " (preferred)" : "")}: {ipv4AddressLookupTask.Result.Count()} IPv4 addresses found: {ipv4AddressLookupTask.Result.Select(ip => ip.ToString()).AggregateWith(", ")}");
                        //DebugX.LogT($"AAAA{(PreferIPv4 == IPVersionPreference.IPv6 ? "" : " (preferred)")}: {ipv6AddressLookupTask.Result.Count()} IPv6 addresses found: {ipv6AddressLookupTask.Result.Select(ip => ip.ToString()).AggregateWith(", ")}");

                        if (ipv4AddressLookupTask.Result.Any())
                            foreach (var ipAddress in ipv4AddressLookupTask.Result.Cast<IIPAddress>())
                                ResolvedIPAddresses.Add(ipAddress);

                        if (ipv6AddressLookupTask.Result.Any())
                            foreach (var ipAddress in ipv6AddressLookupTask.Result.Cast<IIPAddress>())
                                ResolvedIPAddresses.Add(ipAddress);

                        timings.DNSLookup = timings.Elapsed;

                    }

                    #endregion

                }

                if (ResolvedIPAddresses.Count > 0)
                {

                    var remotePort   = RemoteURL.Port ?? dnsSRVRemotePort ?? RemotePort;

                    if (!remotePort.HasValue)
                        return TCPConnectionResult.Failed("The remote TCP port must not be null!");

                    RemotePort     ??= remotePort;

                    // Both disposed of as this block is left, however it is left.
                    // Neither used to be, and a linked token source stays
                    // registered on the token sources it is linked to until it
                    // is disposed of. Every connect left a registration on the
                    // client's token source, which lives as long as the client,
                    // and both of these with it, whether the connect went
                    // through or not - and an HTTP client connects anew for
                    // every request to a server that closes its connections.
                    using var connectTokenSource  = new CancellationTokenSource();
                    using var linkedTokenSource   = CancellationTokenSource.CreateLinkedTokenSource(
                                                        LiveClientCancellationTokenSource.Token,
                                                        connectTokenSource.               Token
                                                    );

                    // This connect's TcpClient, which it works on from here on,
                    // looking at the field only to see whether a close has taken
                    // it away. Close() and CloseConnection() do that - set the
                    // field to null, then close the socket - and may do so while
                    // the connect is pending. The finally below used to read the
                    // field to dispose of what was in it. Where that was null,
                    // its NullReferenceException took the place of whatever the
                    // connect had failed with, and "Error connecting ATCPClient:
                    // Object reference not set to an instance of an object." was
                    // all a caller got to read. Where a connect started right
                    // after the close had put a client of its own there, that
                    // one was disposed, and the new connect failed with it.
                    var client              = new TcpClient {
                                                  ReceiveTimeout  = (Int32) ReceiveTimeout.TotalMilliseconds, // Only relevant for sync I/O!
                                                  SendTimeout     = (Int32) SendTimeout.   TotalMilliseconds, // Only relevant for sync I/O!
                                                  LingerState     = new LingerOption(true, 5),
                                                  NoDelay         = false
                                              };

                    tcpClient               = client;

                    client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive,              true);
                    client.Client.SetSocketOption(SocketOptionLevel.Tcp,    SocketOptionName.TcpKeepAliveInterval,     10);
                    client.Client.SetSocketOption(SocketOptionLevel.Tcp,    SocketOptionName.TcpKeepAliveRetryCount,    3);
                    client.Client.SetSocketOption(SocketOptionLevel.Tcp,    SocketOptionName.TcpKeepAliveTime,        600);

                    try
                    {

                        // IPv4Only and IPv6Only used to have no arm of their own
                        // and fell through to the default one, which picks from
                        // both families at random - the exact opposite of what
                        // their names and their documentation promise. The
                        // trailing "?? GetRandomElement()" then finished the job:
                        // it applied to every arm, so even a matched preference
                        // could end up on the other family.
                        //
                        // That is why the DoH fixtures stayed flaky after being
                        // set to IPv4Only. On a host with no IPv6 route the pick
                        // was a coin toss, three times per query, and a query
                        // failed when all three came up AAAA - "Error connecting
                        // ATCPClient: Network is unreachable".
                        //
                        // Only means only: these two may find nothing, and then
                        // the connection fails saying so, rather than quietly
                        // using the family the caller ruled out.
                        ResolvedIPAddress  = IPVersionPreference switch {

                                                 IPVersionPreference.IPv4Only    =>  ResolvedIPAddresses.Where(ipAddress => ipAddress is IPv4Address).TryGetRandomElement(),
                                                 IPVersionPreference.IPv6Only    =>  ResolvedIPAddresses.Where(ipAddress => ipAddress is IPv6Address).TryGetRandomElement(),

                                                 IPVersionPreference.PreferIPv4  =>  ResolvedIPAddresses.Where(ipAddress => ipAddress is IPv4Address).TryGetRandomElement()
                                                                                         ?? ResolvedIPAddresses.GetRandomElement(),

                                                 IPVersionPreference.PreferIPv6  =>  ResolvedIPAddresses.Where(ipAddress => ipAddress is IPv6Address).TryGetRandomElement()
                                                                                         ?? ResolvedIPAddresses.GetRandomElement(),

                                                 _                               =>  ResolvedIPAddresses.GetRandomElement()

                                             };

                        if (ResolvedIPAddress is null)
                        {
                            ResolvedIPAddresses.Clear();
                            return TCPConnectionResult.Failed($"{nameof(ATCPClient)}: {IPVersionPreference} was asked for, and none of the resolved addresses is of that family!");
                        }

                        var connectTask    = client.ConnectAsync(
                                                 ResolvedIPAddress.ToDotNet(),
                                                 remotePort.Value. ToUInt16(),
                                                 linkedTokenSource.Token
                                             ).AsTask();

                        // The delay gets a token of its own, cancelled as soon as
                        // the race is decided. A connect that went through, or
                        // failed, used to leave the delay pending, and its timer
                        // with it, for the whole ConnectTimeout. The token is
                        // linked to no other, just as the delay took none before.
                        using var waitCancellation = new CancellationTokenSource();

                        var waitTask       = Task.Delay(
                                                 ConnectTimeout,
                                                 waitCancellation.Token
                                             );

                        var timedOut       = await Task.WhenAny(connectTask, waitTask) == waitTask;

                        waitCancellation.Cancel();

                        if (timedOut)
                            connectTokenSource.Cancel();

                        // Await to throw if failed
                        await connectTask;

                    }
                    catch (Exception) when (!ReferenceEquals(tcpClient, client))
                    {

                        // Closed while the connect was pending. A close takes the
                        // client out of the field before it closes the socket, so
                        // by the time the connect fails, the field holds no client
                        // or another connect's. Whatever the connect failed with -
                        // the socket closed under it, or the client's token that
                        // Close() cancels, depending on which comes first - the
                        // close is what happened to it: no timeout, no refusal.
                        //
                        // Nothing the close has reset is touched again here, the
                        // resolved addresses least of all: a connect started right
                        // after the close may be using them by now. Cleared under
                        // it, they failed that one with "The given enumeration must
                        // not be null or empty!".
                        return TCPConnectionResult.Failed("Closed while connecting!");

                    }
                    catch (OperationCanceledException)
                    {
                        ResolvedIPAddresses.Clear();
                        return TCPConnectionResult.Failed("Connection timeout!");
                    }
                    finally
                    {
                        // Clean up on failure
                        if (!client.Connected)
                            client.Dispose();
                    }

                    // Or closed right after the connect went through: a disposed
                    // client hands out no socket to read the end points from.
                    if (!ReferenceEquals(tcpClient, client))
                        return TCPConnectionResult.Failed("Closed while connecting!");

                    if (!client.Connected)
                    {
                        ResolvedIPAddresses.Clear();
                        return TCPConnectionResult.Failed($"Error connecting {nameof(ATCPClient)}");
                    }

                    var localEndpoint = client.Client.LocalEndPoint;
                    if (localEndpoint is not null)
                    {
                        this.CurrentLocalEndPoint   = (localEndpoint as IPEndPoint)!;
                        this.LocalSocket            = IPSocket.FromIPEndPoint(localEndpoint)!.Value;
                    }

                    var remoteEndpoint = client.Client.RemoteEndPoint;
                    if (remoteEndpoint is not null)
                    {
                        this.CurrentRemoteEndPoint  = (remoteEndpoint as IPEndPoint)!;
                        this.RemoteSocket           = IPSocket.FromIPEndPoint(remoteEndpoint)!.Value;
                    }

                    await Log("Client connected!");
                    connectionEstablishedAt = Timestamp.Now;

                }

                else
                    return TCPConnectionResult.Failed("No valid remote IP address found!");

            }
            catch (Exception ex)
            {
                ResolvedIPAddresses.Clear();
                return TCPConnectionResult.Failed($"Error connecting {nameof(ATCPClient)}: {ex.Message}");
            }

            return TCPConnectionResult.Success();

        }

        #endregion


        #region (protected) ForceDNSCacheUpdateOnNextConnect()

        protected void ForceDNSCacheUpdateOnNextConnect()
        {

            Interlocked.Exchange(ref forceDNSCacheUpdateOnNextConnect, 1);

        }

        #endregion


        /// <summary>
        /// SelectMode.SelectRead is "true" if:
        /// - Data is available to read (Available > 0)
        ///     _OR_
        /// - The remote has closed the connection (FIN) or the connection is in an error state
        /// </summary>
        protected Boolean PollConnectionRead
            => tcpClient?.GetStream().Socket.Poll(0, SelectMode.SelectRead) == true;

        /// <summary>
        /// It doesn't mean the other end is still reachable!
        /// It only says: your kernel will accept data into the send buffer right now
        /// </summary>
        protected Boolean PollConnectionWrite
            => tcpClient?.GetStream().Socket.Poll(0, SelectMode.SelectWrite) == true;

        /// <summary>
        /// Poll non-blocking for readability
        /// If poll indicates readable but no data available, it's likely closed (EOF detected)
        /// </summary>
        protected Boolean IsConnectionClosed
        {
            get
            {

                var socket = tcpClient?.GetStream().Socket;

                if (socket is null)
                    return true;

                return socket.Poll(0, SelectMode.SelectRead) &&
                      (socket.Available == 0);

            }
        }


        #region (protected) LogEvent     (Module, Logger, LogHandler, ...)

        /// <remarks>
        /// <c>EventName</c> is passed on rather than left to the compiler a
        /// second time: down there the call site is this method, so
        /// <c>CallerArgumentExpression</c> would fill in "Logger" for every
        /// event there is.
        /// </remarks>
        protected Task LogEvent<TDelegate>(String                                             Module,
                                           TDelegate?                                         Logger,
                                           Func<TDelegate, Task>                              LogHandler,
                                           [CallerArgumentExpression(nameof(Logger))] String  EventName   = "",
                                           [CallerMemberName()]                       String  Command     = "")

            where TDelegate : Delegate

            => Logger.InvokeAllAsync(
                   LogHandler,
                   (exception, eventName) => HandleErrors(Module, $"{Command}.{eventName}", exception),
                   EventName
               );

        #endregion

        #region (virtual)   HandleErrors (Module, Caller, ErrorResponse)

        public virtual Task HandleErrors(String  Module,
                                         String  Caller,
                                         String  ErrorResponse)
        {

            DebugX.Log($"{Module}.{Caller}: {ErrorResponse}");

            return Task.CompletedTask;

        }

        #endregion

        #region (virtual)   HandleErrors (Module, Caller, ExceptionOccurred)

        public virtual Task HandleErrors(String     Module,
                                         String     Caller,
                                         Exception  ExceptionOccurred)
        {

            DebugX.LogException(ExceptionOccurred, $"{Module}.{Caller}");

            return Task.CompletedTask;

        }

        #endregion


        #region (private)   LogEvent     (Logger, LogHandler, ...)

        private Task LogEvent<TDelegate>(TDelegate?                                         Logger,
                                         Func<TDelegate, Task>                              LogHandler,
                                         [CallerArgumentExpression(nameof(Logger))] String  EventName     = "",
                                         [CallerMemberName()]                       String  OICPCommand   = "")

            where TDelegate : Delegate

            => LogEvent(
                   nameof(ATCPClient),
                   Logger,
                   LogHandler,
                   EventName,
                   OICPCommand
               );

        #endregion


        #region (protected) Log          (Message)

        protected Task Log(String Message)
        {

            if (DisableLogging)
                return Task.CompletedTask;

            var onLogs = OnLogs;
            if (onLogs is not null)
            {
                try
                {
                    return onLogs(Message);
                }
                catch (Exception e)
                {
                    DebugX.LogT($"Error in logging handler: {e.Message}");
                }
            }

            return Task.CompletedTask;

        }

        #endregion


        #region CloseConnection()

        /// <summary>
        /// Drop the socket and end the current connection's token, leaving the
        /// client itself - and whatever a caller has in flight - alone.
        /// </summary>
        /// <remarks>
        /// What every "close this connection after a failure" wants, and what
        /// <see cref="Close"/> is not: closing the <i>client</i> cancels the
        /// token a caller's request is linked to, so a failed attempt inside a
        /// retry loop used to end the request it was retrying, and every attempt
        /// after it started from an already-cancelled token.
        /// </remarks>
        public async Task CloseConnection()
        {

            var tcpClientToClose = tcpClient;
            tcpClient = null;
            connectionEstablishedAt = null;

            try { tcpClientToClose?.Close();   } catch { }
            try { tcpClientToClose?.Dispose(); } catch { }

            CurrentLocalEndPoint  = null;
            CurrentRemoteEndPoint = null;
            LocalSocket           = null;
            RemoteSocket          = null;

            await Log("TCP connection closed!");

            ResolvedIPAddresses.Clear();

            CycleConnectionToken();

        }

        #endregion

        #region Close()

        /// <summary>
        /// Close the TCP client: the connection, and everything a caller still
        /// has in flight on it.
        /// </summary>
        public async Task Close()
        {

            await CloseConnection();

            // The client's own token, and here it is right - this client is
            // going away, so a request waiting on it should stop waiting.
            try { clientCancellationTokenSource?.Cancel(); } catch { }

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{nameof(ATCPClient)}: {LocalSocket} -> {RemoteSocket} (Connected: {IsConnected})";

        #endregion


        #region Dispose / IAsyncDisposable

        public virtual async ValueTask DisposeAsync()
        {

            await Close();
            connectionCancellationTokenSource?.Dispose();
            clientCancellationTokenSource?.    Dispose();

            // A DNS client of this client's own making used to outlive it. Its
            // cache cleans up on a timer, and a running timer keeps what it
            // calls alive: every client made and disposed of left a cache
            // behind that ticked every ten seconds for the rest of the process.
            //
            // Asked of the Lazy rather than of the property: reading the
            // property would build the very DNS client this line then throws
            // away, and a client that never resolved anything would pay the
            // search on its way out instead of on its way in.
            if (ownsDNSClient && dnsClient.IsValueCreated)
                await dnsClient.Value.DisposeAsync().ConfigureAwait(false);

            GC.SuppressFinalize(this);

        }

        public virtual void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }

        #endregion

    }

}
