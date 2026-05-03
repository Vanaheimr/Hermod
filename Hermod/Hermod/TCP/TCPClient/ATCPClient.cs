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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.TCP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

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

        public const              UInt32                   DefaultBufferSize                = 4096;

        protected                 TcpClient?               tcpClient;
        protected                 CancellationTokenSource  clientCancellationTokenSource;

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
        public  UInt16                          MaxNumberOfRetries        { get; } = 3;
        public  UInt32                          BufferSize                { get; }


        /// <summary>
        /// Disable logging of connection events and errors.
        /// </summary>
        public Boolean                          DisableLogging            { get; }

        /// <summary>
        /// The DNS client defines which DNS servers to use.
        /// </summary>
        public  IDNSClient?                     DNSClient                 { get; }

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
                           UInt32?                          BufferSize               = null,

                           Boolean?                         DisableLogging           = null,
                             // String?                       LoggingPath              = null,
                             // String?                       LoggingContext           = Logger.DefaultContext,
                             // LogfileCreatorDelegate?       LogfileCreator           = null,
                           IDNSClient?                      DNSClient                = null)
        {

            if (ConnectTimeout.HasValue && ConnectTimeout.Value.TotalMilliseconds > Int32.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(ConnectTimeout), "Timeout too large for socket.");

            if (ReceiveTimeout.HasValue && ReceiveTimeout.Value.TotalMilliseconds > Int32.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(ReceiveTimeout), "Timeout too large for socket.");

            if (SendTimeout.   HasValue && SendTimeout.   Value.TotalMilliseconds > Int32.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(SendTimeout),    "Timeout too large for socket.");

            this.Description                    = Description            ?? I18NString.Empty;
            this.IPVersionPreference            = IPVersionPreference    ?? Hermod.IPVersionPreference.PreferIPv6;
            this.BufferSize                     = BufferSize.HasValue
                                                      ? BufferSize.Value > Int32.MaxValue
                                                            ? throw new ArgumentOutOfRangeException(nameof(BufferSize), "The buffer size must not exceed Int32.MaxValue!")
                                                            : BufferSize.Value
                                                      : DefaultBufferSize;
            this.ConnectTimeout                 = ConnectTimeout         ?? DefaultConnectTimeout;
            this.ReceiveTimeout                 = ReceiveTimeout         ?? DefaultReceiveTimeout;
            this.SendTimeout                    = SendTimeout            ?? DefaultSendTimeout;
            this.TransmissionRetryDelay         = TransmissionRetryDelay ?? (retryCounter => TimeSpan.FromSeconds(retryCounter * retryCounter * DefaultTransmissionRetryDelay.TotalSeconds));
            this.MaxNumberOfRetries             = MaxNumberOfRetries     ?? DefaultMaxNumberOfRetries;

            this.DisableLogging                 = DisableLogging         ?? false;
            this.DNSClient                      = DNSClient              ?? new DNSClient();

            this.clientCancellationTokenSource  = new CancellationTokenSource();

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
                             UInt32?                          BufferSize               = null,

                             // String?                         LoggingPath              = null,
                             // String?                         LoggingContext           = Logger.DefaultContext,
                             // LogfileCreatorDelegate?         LogfileCreator           = null,
                             Boolean?                         DisableLogging           = null)

            : this(Description,
                   IPVersionPreference,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize,

                   DisableLogging)

        {

            this.RemotePort       = TCPPort;
            this.RemoteIPAddress  = IPAddress;

            this.RemoteSocket     = new IPSocket(
                                        IPAddress,
                                        TCPPort
                                    );

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
                             UInt32?                          BufferSize               = null,

                             Boolean?                         DisableLogging           = null,
                             // String?                         LoggingPath              = null,
                             // String?                         LoggingContext           = Logger.DefaultContext,
                             // LogfileCreatorDelegate?         LogfileCreator           = null,
                             IDNSClient?                      DNSClient                = null)

            : this(Description,
                   IPVersionPreference,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize,

                   DisableLogging,
                   DNSClient)

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
                             UInt32?                          BufferSize               = null,

                             Boolean?                         DisableLogging           = null,
                             // String?                         LoggingPath              = null,
                             // String?                         LoggingContext           = Logger.DefaultContext,
                             // LogfileCreatorDelegate?         LogfileCreator           = null,
                             IDNSClient?                      DNSClient                = null)

            : this(Description,
                   IPVersionPreference,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize,

                   DisableLogging,
                   DNSClient)

        {

            this.DomainName  = DomainName;
            this.DNSService  = DNSService;

        }

        #endregion

        #endregion


        #region ReconnectAsync(CancellationToken = default)

        public virtual async Task<TCPConnectionResult>

            ReconnectAsync(CancellationToken CancellationToken = default)

        {

            try
            {

                clientCancellationTokenSource?.Cancel();

                try { tcpClient?.Client?.Shutdown(SocketShutdown.Both); } catch { }
                try { tcpClient?.Close();                               } catch { }
                try { tcpClient?.Dispose();                             } catch { }

                ResolvedIPAddress = null;
                ResolvedIPAddresses.Clear();
                clientCancellationTokenSource?.Dispose();

                CurrentLocalEndPoint  = null;
                CurrentRemoteEndPoint = null;
                LocalSocket           = null;
                RemoteSocket          = null;

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

            try
            {

                DomainName? dnsSRVRemoteHost   = null;
                IPPort?     dnsSRVRemotePort   = null;

                if (ResolvedIPAddresses.Count == 0)
                {

                    var hostname = (DomainName?.FullName ?? RemoteURL.Hostname.Name).Trim();

                    #region Localhost / URL looks like an IP address...

                    if      (IPAddress.IsIPv4Localhost(hostname))
                        ResolvedIPAddresses.Add(IPv4Address.Localhost);

                    else if (IPAddress.IsIPv6Localhost(hostname))
                        ResolvedIPAddresses.Add(IPv6Address.Localhost);

                    else if (IPAddress.IsIPv4(hostname))
                        ResolvedIPAddresses.Add(IPv4Address.Parse(hostname));

                    else if (IPAddress.IsIPv6(hostname))
                        ResolvedIPAddresses.Add(IPv6Address.Parse(hostname));

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
                                                               BypassCache:        false,
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
                        var ipv4AddressLookupTask = DNSClient.Query_IPv4Addresses(
                                                        dnsSRVRemoteHost ?? DomainName.Parse(hostname),
                                                        RecursionDesired:   true,
                                                        BypassCache:        false,
                                                        CancellationToken:  CancellationToken
                                                    );

                        var ipv6AddressLookupTask = DNSClient.Query_IPv6Addresses(
                                                        dnsSRVRemoteHost ?? DomainName.Parse(hostname),
                                                        RecursionDesired:   true,
                                                        BypassCache:        false,
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

                    if (clientCancellationTokenSource.IsCancellationRequested)
                        clientCancellationTokenSource = new CancellationTokenSource();

                    var connectTokenSource  = new CancellationTokenSource();
                    var linkedTokenSource   = CancellationTokenSource.CreateLinkedTokenSource(
                                                  clientCancellationTokenSource.Token,
                                                  connectTokenSource.           Token
                                              );

                    tcpClient               = new TcpClient {
                                                  ReceiveTimeout  = (Int32) ReceiveTimeout.TotalMilliseconds, // Only relevant for sync I/O!
                                                  SendTimeout     = (Int32) SendTimeout.   TotalMilliseconds, // Only relevant for sync I/O!
                                                  LingerState     = new LingerOption(true, 5),
                                                  NoDelay         = false
                                              };

                    tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive,              true);
                    tcpClient.Client.SetSocketOption(SocketOptionLevel.Tcp,    SocketOptionName.TcpKeepAliveInterval,     10);
                    tcpClient.Client.SetSocketOption(SocketOptionLevel.Tcp,    SocketOptionName.TcpKeepAliveRetryCount,    3);
                    tcpClient.Client.SetSocketOption(SocketOptionLevel.Tcp,    SocketOptionName.TcpKeepAliveTime,        600);

                    try
                    {

                        ResolvedIPAddress  = IPVersionPreference switch {
                                                 IPVersionPreference.PreferIPv4  => ResolvedIPAddresses.Where(ipAddress => ipAddress is IPv4Address).TryGetRandomElement(),
                                                 IPVersionPreference.PreferIPv6  => ResolvedIPAddresses.Where(ipAddress => ipAddress is IPv6Address).TryGetRandomElement(),
                                                 _                         => ResolvedIPAddresses.GetRandomElement()
                                             } ?? ResolvedIPAddresses.GetRandomElement();

                        var connectTask    = tcpClient.ConnectAsync(
                                                 ResolvedIPAddress.ToDotNet(),
                                                 remotePort.Value. ToUInt16(),
                                                 linkedTokenSource.Token
                                             ).AsTask();

                        var waitTask       = Task.Delay(
                                                 ConnectTimeout,
                                                 CancellationToken.None
                                             );

                        if (await Task.WhenAny(connectTask, waitTask ) == waitTask)
                            connectTokenSource.Cancel();

                        // Await to throw if failed
                        await connectTask;

                    }
                    catch (OperationCanceledException)
                    {
                        ResolvedIPAddresses.Clear();
                        return TCPConnectionResult.Failed("Connection timeout!");
                    }
                    finally
                    {
                        // Clean up on failure
                        if (!tcpClient.Connected)
                            tcpClient.Dispose();
                    }

                    if (!tcpClient.Connected)
                    {
                        ResolvedIPAddresses.Clear();
                        return TCPConnectionResult.Failed($"Error connecting {nameof(ATCPClient)}");
                    }

                    var localEndpoint = tcpClient.Client.LocalEndPoint;
                    if (localEndpoint is not null)
                    {
                        this.CurrentLocalEndPoint   = (localEndpoint as IPEndPoint)!;
                        this.LocalSocket            = IPSocket.FromIPEndPoint(localEndpoint)!.Value;
                    }

                    var remoteEndpoint = tcpClient.Client.RemoteEndPoint;
                    if (remoteEndpoint is not null)
                    {
                        this.CurrentRemoteEndPoint  = (remoteEndpoint as IPEndPoint)!;
                        this.RemoteSocket           = IPSocket.FromIPEndPoint(remoteEndpoint)!.Value;
                    }

                    await Log("Client connected!");

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

        protected async Task LogEvent<TDelegate>(String                                             Module,
                                                 TDelegate?                                         Logger,
                                                 Func<TDelegate, Task>                              LogHandler,
                                                 [CallerArgumentExpression(nameof(Logger))] String  EventName   = "",
                                                 [CallerMemberName()]                       String  Command     = "")

            where TDelegate : Delegate

        {
            if (Logger is not null)
            {
                try
                {

                    await Task.WhenAll(
                              Logger.GetInvocationList().
                                     OfType<TDelegate>().
                                     Select(LogHandler)
                          ).ConfigureAwait(false);

                }
                catch (Exception e)
                {
                    await HandleErrors(Module, $"{Command}.{EventName}", e);
                }
            }
        }

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


        #region Close()

        /// <summary>
        /// Close the TCP connection.
        /// </summary>
        public async Task Close()
        {

            if (IsConnected)
            {

                try
                {
                    tcpClient?.Client.Shutdown(SocketShutdown.Both);
                    tcpClient?.Close();
                }
                catch { }

                await Log("TCP Client closed!");

            }

            ResolvedIPAddresses.Clear();
            clientCancellationTokenSource.Cancel();

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
            clientCancellationTokenSource?.Dispose();
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
