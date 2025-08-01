﻿/*
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

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Illias;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    #region (class) HTTPClientConnectTimings

    public class HTTPClientConnectTimings
    {

        public TimeSpan               Elapsed
            => Timestamp.Now - Start;

        public List<Elapsed<String>>  Errors                  { get; }


        public DateTimeOffset         Start                   { get; }
        public TimeSpan?              DNSSRVLookup            { get; internal set; }
        public TimeSpan?              DNSLookup               { get; internal set; }
        public TimeSpan?              Connected               { get; internal set; }
        public TimeSpan?              TLSHandshake            { get; internal set; }
        public Byte                   RestartCounter          { get; internal set; }


        public HTTPClientConnectTimings()
        {

            this.Start         = Timestamp.Now;
            this.Errors        = [];

        }


        public void AddError(String Error)
        {
            Errors.Add(new Elapsed<String>(Timestamp.Now - Start, Error));
        }


        public String ErrorsAsString()

            => Errors.Select(elapsed => elapsed.Time.TotalMilliseconds.ToString("F2") + ": " + elapsed.Value).AggregateWith(Environment.NewLine);


        public override String ToString()

            => String.Concat(
                    "Start: ",                Start.                                      ToISO8601(),                             " > ",
                    "DNS SRV Lookup: ",       DNSSRVLookup?.                              TotalMilliseconds.ToString("F2") ?? "-", " > ",
                    "DNS Lookup: ",           DNSLookup?.                                 TotalMilliseconds.ToString("F2") ?? "-", " > ",
                    "Connected: ",            Connected?.                                 TotalMilliseconds.ToString("F2") ?? "-", " > ",
                    "TLSHandshake: ",         TLSHandshake?.                              TotalMilliseconds.ToString("F2") ?? "-", " > ",
                    "RestartCounter: ",       RestartCounter - 1,                                                                  " > "
                );

    }

    #endregion


    /// <summary>
    /// A simple TCP echo test client that can connect to a TCP echo server,
    /// </summary>
    public abstract class ATCPTestClient : IDisposable,
                                           IAsyncDisposable
    {

        #region Data

        protected static readonly Byte[]                    endOfHTTPHeaderDelimiter         = Encoding.UTF8.GetBytes("\r\n\r\n");
        protected const           Byte                      endOfHTTPHeaderDelimiterLength   = 4;

        public static readonly    TimeSpan                  DefaultConnectTimeout            = TimeSpan.FromSeconds(5);
        public static readonly    TimeSpan                  DefaultReceiveTimeout            = TimeSpan.FromSeconds(5);
        public static readonly    TimeSpan                  DefaultSendTimeout               = TimeSpan.FromSeconds(5);
        public const              Int32                     DefaultBufferSize                = 4096;

        protected readonly        TCPEchoLoggingDelegate?   loggingHandler;
        protected                 TcpClient?                tcpClient;
        protected                 CancellationTokenSource?  cts;

        #endregion

        #region Properties

        /// <summary>
        /// Whether the client is currently connected to the echo server.
        /// </summary>
        public Boolean      IsConnected
            => tcpClient?.Connected ?? false;


        /// <summary>
        /// The local IP end point of the connected echo server.
        /// </summary>
        public IPEndPoint?  CurrentLocalEndPoint
            => tcpClient?.Client.LocalEndPoint as IPEndPoint;

        /// <summary>
        /// The local TCP port of the connected echo server.
        /// </summary>
        public UInt16?      CurrentLocalTCPPort

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
        public IPEndPoint?  CurrentRemoteEndPoint
            => tcpClient?.Client.RemoteEndPoint as IPEndPoint;

        /// <summary>
        /// The remote TCP port of the connected echo server.
        /// </summary>
        public UInt16?      CurrentRemoteTCPPort

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
        public  IIPAddress?              RemoteIPAddress    { get; private   set; }
        public  IPPort?                  RemoteTCPPort      { get; protected set; }
        public  TimeSpan                 ConnectTimeout     { get; }
        public  TimeSpan                 ReceiveTimeout     { get; }
        public  TimeSpan                 SendTimeout        { get; }
        public  Int32                    BufferSize         { get; }


        /// <summary>
        /// The DNS Name to lookup in order to resolve high available IP addresses and TCP ports.
        /// </summary>
        public DomainName?               DomainName            { get; }

        /// <summary>
        /// The DNS Service to lookup in order to resolve high available IP addresses and TCP ports.
        /// </summary>
        public SRV_Spec?                 DNSService         { get; }

        /// <summary>
        /// Prefer IPv4 instead of IPv6.
        /// </summary>
        public Boolean                   PreferIPv4         { get; }

        /// <summary>
        /// The DNS client defines which DNS servers to use.
        /// </summary>
        public DNSClient?                DNSClient          { get; }

        #endregion

        #region Constructor(s)

        #region (private)   ATCPTestClient(...)

        private ATCPTestClient(TimeSpan?                ConnectTimeout   = null,
                               TimeSpan?                ReceiveTimeout   = null,
                               TimeSpan?                SendTimeout      = null,
                               UInt32?                  BufferSize       = null,
                               TCPEchoLoggingDelegate?  LoggingHandler   = null,
                               DNSClient?               DNSClient        = null)
        {

            if (ConnectTimeout.HasValue && ConnectTimeout.Value.TotalMilliseconds > Int32.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(ConnectTimeout), "Timeout too large for socket.");

            if (ReceiveTimeout.HasValue && ReceiveTimeout.Value.TotalMilliseconds > Int32.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(ReceiveTimeout), "Timeout too large for socket.");

            if (SendTimeout.   HasValue && SendTimeout.   Value.TotalMilliseconds > Int32.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(SendTimeout),    "Timeout too large for socket.");

            this.BufferSize       = BufferSize.HasValue
                                        ? BufferSize.Value > Int32.MaxValue
                                              ? throw new ArgumentOutOfRangeException(nameof(BufferSize), "The buffer size must not exceed Int32.MaxValue!")
                                              : (Int32) BufferSize.Value
                                        : DefaultBufferSize;
            this.ConnectTimeout   = ConnectTimeout ?? DefaultConnectTimeout;
            this.ReceiveTimeout   = ReceiveTimeout ?? DefaultReceiveTimeout;
            this.SendTimeout      = SendTimeout    ?? DefaultSendTimeout;
            this.loggingHandler   = LoggingHandler;

        }

        #endregion

        #region (protected) ATCPTestClient(IPAddress, TCPPort, ...)

        protected ATCPTestClient(IIPAddress               IPAddress,
                                 IPPort                   TCPPort,
                                 TimeSpan?                ConnectTimeout   = null,
                                 TimeSpan?                ReceiveTimeout   = null,
                                 TimeSpan?                SendTimeout      = null,
                                 UInt32?                  BufferSize       = null,
                                 TCPEchoLoggingDelegate?  LoggingHandler   = null)

            : this(ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   BufferSize,
                   LoggingHandler)

        {

            this.RemoteTCPPort    = TCPPort;
            this.RemoteIPAddress  = IPAddress;

        }

        #endregion

        #region (protected) ATCPTestClient(URL,     DNSService = null, ..., DNSClient = null)

        protected ATCPTestClient(URL                      URL,
                                 SRV_Spec?                DNSService       = null,
                                 TimeSpan?                ConnectTimeout   = null,
                                 TimeSpan?                ReceiveTimeout   = null,
                                 TimeSpan?                SendTimeout      = null,
                                 UInt32?                  BufferSize       = null,
                                 TCPEchoLoggingDelegate?  LoggingHandler   = null,
                                 DNSClient?               DNSClient        = null)

            : this(ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   BufferSize,
                   LoggingHandler,
                   DNSClient)

        {

            this.RemoteURL   = URL;
            this.DNSService  = DNSService;
            this.DNSClient   = DNSClient ?? new DNSClient();

        }

        #endregion

        #region (protected) ATCPTestClient(DomainName, DNSService,        ..., DNSClient = null)

        protected ATCPTestClient(DomainName               DomainName,
                                 SRV_Spec                 DNSService,
                                 TimeSpan?                ConnectTimeout   = null,
                                 TimeSpan?                ReceiveTimeout   = null,
                                 TimeSpan?                SendTimeout      = null,
                                 UInt32?                  BufferSize       = null,
                                 TCPEchoLoggingDelegate?  LoggingHandler   = null,
                                 DNSClient?               DNSClient        = null)

            : this(ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   BufferSize,
                   LoggingHandler,
                   DNSClient)

        {

            this.DomainName  = DomainName;
            this.DNSService  = DNSService;
            this.DNSClient   = DNSClient ?? new DNSClient();

        }

        #endregion

        #endregion


        #region ReconnectAsync(CancellationToken = default)

        public virtual async Task reconnectAsync(CancellationToken CancellationToken = default)
        {

            cts?.      Cancel();
            tcpClient?.Close();
            cts?.      Dispose();

            // recreate _cts and tcpClient
            await connectAsync(CancellationToken);

        }

        #endregion

        #region (protected) ConnectAsync(CancellationToken = default)

        protected virtual async Task connectAsync(CancellationToken CancellationToken = default)
        {

            var timings = new HTTPClientConnectTimings();

            try
            {

                DomainName? dnsSRVRemoteHost   = null;
                IPPort?     dnsSRVRemotePort   = null;

                if (RemoteURL.HasValue)
                {

                    #region Localhost / URL looks like an IP address...

                    if      (IPAddress.IsIPv4Localhost(RemoteURL.Value.Hostname))
                        RemoteIPAddress = IPv4Address.Localhost;

                    else if (IPAddress.IsIPv6Localhost(RemoteURL.Value.Hostname))
                        RemoteIPAddress = IPv6Address.Localhost;

                    else if (IPAddress.IsIPv4(RemoteURL.Value.Hostname.Name))
                        RemoteIPAddress = IPv4Address.Parse(RemoteURL.Value.Hostname.Name);

                    else if (IPAddress.IsIPv6(RemoteURL.Value.Hostname.Name))
                        RemoteIPAddress = IPv6Address.Parse(RemoteURL.Value.Hostname.Name);

                    #endregion

                    #region DNS SRV    lookups...

                    if (RemoteIPAddress is null &&
                        DNSClient       is not null &&
                        DNSService.IsNotNullOrEmpty())
                    {

                        // Look up the DNS Name or the hostname of the URL...
                        var serviceRecords         = await DNSClient.Query_DNSService(DNSServiceName.Parse($"{DNSService}.{DomainName ?? DomainName.Parse(RemoteURL.Value.Hostname.Name)}")).
                                                                     ConfigureAwait(false);

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

                    if (RemoteIPAddress is null &&
                        DNSClient       is not null)
                    {

                        // Look up the DNS SRV remote host or the hostname of the URL...
                        var ipv4AddressLookupTask = DNSClient.Query_IPv4Addresses(dnsSRVRemoteHost ?? DomainName.Parse(RemoteURL.Value.Hostname.Name));
                        var ipv6AddressLookupTask = DNSClient.Query_IPv6Addresses(dnsSRVRemoteHost ?? DomainName.Parse(RemoteURL.Value.Hostname.Name));

                        await Task.WhenAll(
                                  ipv4AddressLookupTask,
                                  ipv6AddressLookupTask
                              ).ConfigureAwait(false);

                        if (PreferIPv4)
                        {
                            if (ipv6AddressLookupTask.Result.Any())
                                RemoteIPAddress = ipv6AddressLookupTask.Result.First();

                            if (ipv4AddressLookupTask.Result.Any())
                                RemoteIPAddress = ipv4AddressLookupTask.Result.First();
                        }
                        else
                        {
                            if (ipv4AddressLookupTask.Result.Any())
                                RemoteIPAddress = ipv4AddressLookupTask.Result.First();

                            if (ipv6AddressLookupTask.Result.Any())
                                RemoteIPAddress = ipv6AddressLookupTask.Result.First();
                        }

                        timings.DNSLookup = timings.Elapsed;

                    }

                    #endregion

                }

                if (RemoteIPAddress is not null)
                {

                    var remotePort    = dnsSRVRemotePort ?? RemoteTCPPort;

                    if (!remotePort.HasValue)
                        throw new ArgumentNullException(nameof(RemoteTCPPort), "The remote TCP port must not be null!");

                    cts               = new CancellationTokenSource();
                    tcpClient         = new TcpClient();
                    var connectTask   = tcpClient.ConnectAsync(RemoteIPAddress.ToDotNet(), remotePort.Value.ToUInt16());

                    if (await Task.WhenAny(connectTask, Task.Delay(ConnectTimeout, cts.Token)) == connectTask)
                    {
                        await connectTask; // Await to throw if failed
                        tcpClient.ReceiveTimeout = (Int32) ReceiveTimeout.TotalMilliseconds;
                        tcpClient.SendTimeout    = (Int32) SendTimeout.TotalMilliseconds;
                        tcpClient.LingerState    = new LingerOption(true, 1);
                        await Log("Client connected!");
                    }
                    else
                    {
                        throw new TimeoutException("Connection timeout");
                    }

                }

                else
                    throw new ArgumentNullException(nameof(RemoteIPAddress), "The remote IP address must not be null!");

            }
            catch (Exception ex)
            {
                await Log($"Error connecting ATCPTestClient: {ex.Message}");
                throw;
            }

        }

        #endregion


        #region (protected) SendText   (Text)

        /// <summary>
        /// Send the given message to the echo server and receive the echoed response.
        /// </summary>
        /// <param name="Text">The text message to send and echo.</param>
        /// <returns>Whether the echo was successful, the echoed response, an optional error response, and the time taken to send and receive it.</returns>
        protected async Task<(Boolean, String, String?, TimeSpan)> SendText(String Text)
        {

            var response  = await SendBinary(Encoding.UTF8.GetBytes(Text));
            var text      = Encoding.UTF8.GetString(response.Item2, 0, response.Item2.Length);

            return (response.Item1,
                    text,
                    response.Item3,
                    response.Item4);

        }

        #endregion

        #region (protected) SendBinary (Bytes)

        /// <summary>
        /// Send the given bytes to the echo server and receive the echoed response.
        /// </summary>
        /// <param name="Bytes">The bytes to send and echo.</param>
        /// <returns>Whether the echo was successful, the echoed response, an optional error response, and the time taken to send and receive it.</returns>
        protected async Task<(Boolean, Byte[], String?, TimeSpan)> SendBinary(Byte[] Bytes)
        {

            if (!IsConnected || tcpClient is null)
                return (false, Array.Empty<Byte>(), "Client is not connected.", TimeSpan.Zero);

            try
            {

                var stopwatch   = Stopwatch.StartNew();
                var stream      = tcpClient.GetStream();
                cts           ??= new CancellationTokenSource();

                // Send the data
                await stream.WriteAsync(Bytes, cts.Token).ConfigureAwait(false);
                await stream.FlushAsync(cts.Token).ConfigureAwait(false);

                using var responseStream = new MemoryStream();
                var buffer     = new Byte[8192];
                var bytesRead  = 0;

                while ((bytesRead = await stream.ReadAsync(buffer, cts.Token).ConfigureAwait(false)) > 0)
                {
                    await responseStream.WriteAsync(buffer.AsMemory(0, bytesRead), cts.Token).ConfigureAwait(false);
                }

                stopwatch.Stop();

                return (true, responseStream.ToArray(), null, stopwatch.Elapsed);

            }
            catch (Exception ex)
            {
                await Log($"Error in SendBinary: {ex.Message}");
                return (false, Array.Empty<Byte>(), ex.Message, TimeSpan.Zero);
            }

        }

        #endregion


        #region (protected) Log        (Message)

        protected Task Log(String Message)
        {

            if (loggingHandler is not null)
            {
                try
                {
                    return loggingHandler(Message);
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Error in logging handler: {e.Message}");
                }
            }

            return Task.CompletedTask;

        }

        #endregion


        #region Close()

        /// <summary>
        /// Close the TCP connection to the echo server.
        /// </summary>
        public async Task Close()
        {

            if (IsConnected)
            {
                try
                {
                    tcpClient.Client.Shutdown(SocketShutdown.Both);
                }
                catch { }
                tcpClient.Close();
                await Log("Client closed!");
            }

            cts.Cancel();

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{nameof(ATCPTestClient)}: {RemoteIPAddress}:{RemoteTCPPort} (Connected: {IsConnected})";

        #endregion


        #region Dispose / IAsyncDisposable

        public async ValueTask DisposeAsync()
        {
            await Close();
            cts?.Dispose();
            GC.SuppressFinalize(this);
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }

        #endregion

    }

}
