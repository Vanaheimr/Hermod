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

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    public delegate Task TCPEchoLoggingDelegate(String Message);

    /// <summary>
    /// A simple echo test server that listens for incoming TCP echo connections.
    /// </summary>
    public class TCPEchoTestServer : IDisposable,
                                     IAsyncDisposable
    {

        #region Data

        public const           Int32     DefaultBufferSize      = 81920; // .NET default for CopyToAsync!
        public static readonly TimeSpan  DefaultReceiveTimeout  = TimeSpan.FromSeconds(5);
        public static readonly TimeSpan  DefaultSendTimeout     = TimeSpan.FromSeconds(5);

        private readonly  IIPAddress                             ipAddress;
        private readonly  TcpListener                            tcpListener;
        private readonly  Int32                                  bufferSize;
        private readonly  TimeSpan                               ReceiveTimeout;
        private readonly  TimeSpan                               SendTimeout;
        private readonly  TCPEchoLoggingDelegate?                loggingHandler;
        private readonly  CancellationTokenSource                cts;
        private           Task?                                  serverTask;

        private readonly  ConcurrentDictionary<TcpClient, Task>  activeClients  = [];

        #endregion

        #region Properties

        /// <summary>
        /// The TCP port this EchoTestServer is listening on.
        /// </summary>
        public IPPort?  TCPPort
            => tcpListener.LocalEndpoint is IPEndPoint endpoint
                   ? IPPort.Parse(endpoint.Port)
                   : null;

        #endregion

        #region Constructor(s)

        #region (private) EchoTestServer (TCPPort, IPAddress = null, BufferSize = null, ReceiveTimeout = null, SendTimeout = null, LoggingHandler = null)

        private TCPEchoTestServer(IPPort                   TCPPort,
                                  IIPAddress?              IPAddress        = null,
                                  UInt32?                  BufferSize       = null,
                                  TimeSpan?                ReceiveTimeout   = null,
                                  TimeSpan?                SendTimeout      = null,
                                  TCPEchoLoggingDelegate?  LoggingHandler   = null)
        {

            if (ReceiveTimeout.HasValue && ReceiveTimeout.Value.TotalMilliseconds > Int32.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(ReceiveTimeout), "Timeout too large for socket.");

            if (SendTimeout.   HasValue && SendTimeout.   Value.TotalMilliseconds > Int32.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(SendTimeout),    "Timeout too large for socket.");

            this.ipAddress       = IPAddress ?? IPv6Address.Localhost;
            this.tcpListener     = new TcpListener(ipAddress.ToDotNet(), TCPPort.ToUInt16());
            this.bufferSize      = BufferSize.HasValue
                                       ? BufferSize.Value > Int32.MaxValue
                                             ? throw new ArgumentOutOfRangeException(nameof(BufferSize), "The buffer size must not exceed Int32.MaxValue!")
                                             : (Int32) BufferSize.Value
                                       : DefaultBufferSize;
            this.ReceiveTimeout  = ReceiveTimeout ?? DefaultReceiveTimeout;
            this.SendTimeout     = SendTimeout    ?? DefaultSendTimeout;
            this.loggingHandler  = LoggingHandler;
            this.cts             = new CancellationTokenSource();

        }

        #endregion


        #region EchoTestServer (         TCPPort, BufferSize = null, ReceiveTimeout = null, SendTimeout = null, LoggingHandler = null)

        /// <summary>
        /// Create a new EchoTestServer that listens on the loopback address and the given TCP port.
        /// </summary>
        /// <param name="TCPPort">The TCP port to listen on. If 0, a random TCP port will be assigned.</param>
        /// <param name="BufferSize">An optional buffer size for the TCP stream. If null, the default buffer size will be used.</param>
        /// <param name="ReceiveTimeout">An optional receive timeout for the TCP stream. If null, the default receive timeout will be used.</param>
        /// <param name="SendTimeout">An optional send timeout for the TCP stream. If null, the default send timeout will be used.</param>
        /// <param name="LoggingHandler">An optional logging handler that will be called for each log message.</param>
        public TCPEchoTestServer(IPPort                   TCPPort,
                                 UInt32?                  BufferSize       = null,
                                 TimeSpan?                ReceiveTimeout   = null,
                                 TimeSpan?                SendTimeout      = null,
                                 TCPEchoLoggingDelegate?  LoggingHandler   = null)

            : this(TCPPort,
                   IPv6Address.Localhost,
                   BufferSize,
                   ReceiveTimeout,
                   SendTimeout,
                   LoggingHandler)

        { }

        #endregion

        #region EchoTestServer (Address, TCPPort, BufferSize = null, ReceiveTimeout = null, SendTimeout = null, LoggingHandler = null)

        /// <summary>
        /// Create a new EchoTestServer that listens on the specified IP address and TCP port.
        /// </summary>
        /// <param name="IPAddress">The IP address to listen on. If null, the loopback address will be used.</param>
        /// <param name="TCPPort">The TCP port to listen on. If 0, a random TCP port will be assigned.</param>
        /// <param name="BufferSize">An optional buffer size for the TCP stream. If null, the default buffer size will be used.</param>
        /// <param name="ReceiveTimeout">An optional receive timeout for the TCP stream. If null, the default receive timeout will be used.</param>
        /// <param name="SendTimeout">An optional send timeout for the TCP stream. If null, the default send timeout will be used.</param>
        /// <param name="LoggingHandler">An optional logging handler that will be called for each log message.</param>
        public TCPEchoTestServer(IIPAddress               IPAddress,
                                 IPPort                   TCPPort,
                                 UInt32?                  BufferSize       = null,
                                 TimeSpan?                ReceiveTimeout   = null,
                                 TimeSpan?                SendTimeout      = null,
                                 TCPEchoLoggingDelegate?  LoggingHandler   = null)

            : this(TCPPort,
                   IPAddress,
                   BufferSize,
                   ReceiveTimeout,
                   SendTimeout,
                   LoggingHandler)

        { }

        #endregion

        #endregion


        #region (private) Log(Message)

        private Task Log(String Message)
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


        #region StartNew (         TCPPort, BufferSize = null, ReceiveTimeout = null, SendTimeout = null, LoggingHandler = null)

        /// <summary>
        /// Start a new EchoTestServer that listens on the loopback address and the given TCP port.
        /// </summary>
        /// <param name="TCPPort">The TCP port to listen on. If 0, a random TCP port will be assigned.</param>
        /// <param name="BufferSize">An optional buffer size for the TCP stream. If null, the default buffer size will be used.</param>
        /// <param name="ReceiveTimeout">An optional receive timeout for the TCP stream. If null, the default receive timeout will be used.</param>
        /// <param name="SendTimeout">An optional send timeout for the TCP stream. If null, the default send timeout will be used.</param>
        /// <param name="LoggingHandler">An optional logging handler that will be called for each log message.</param>
        public static async Task StartNew(IPPort                   TCPPort,
                                          UInt32?                  BufferSize       = null,
                                          TimeSpan?                ReceiveTimeout   = null,
                                          TimeSpan?                SendTimeout      = null,
                                          TCPEchoLoggingDelegate?  LoggingHandler   = null)
        {

            var server = new TCPEchoTestServer(
                             TCPPort,
                             BufferSize,
                             ReceiveTimeout,
                             SendTimeout,
                             LoggingHandler
                         );

            await server.Start();

        }

        #endregion

        #region StartNew (Address, TCPPort, BufferSize = null, ReceiveTimeout = null, SendTimeout = null, LoggingHandler = null)

        /// <summary>
        /// Start a new EchoTestServer that listens on the specified IP address and TCP port.
        /// </summary>
        /// <param name="IPAddress">The IP address to listen on. If null, the loopback address will be used.</param>
        /// <param name="TCPPort">The TCP port to listen on. If 0, a random TCP port will be assigned.</param>
        /// <param name="BufferSize">An optional buffer size for the TCP stream. If null, the default buffer size will be used.</param>
        /// <param name="ReceiveTimeout">An optional receive timeout for the TCP stream. If null, the default receive timeout will be used.</param>
        /// <param name="SendTimeout">An optional send timeout for the TCP stream. If null, the default send timeout will be used.</param>
        /// <param name="LoggingHandler">An optional logging handler that will be called for each log message.</param>
        public static async Task StartNew(IIPAddress               IPAddress,
                                          IPPort                   TCPPort,
                                          UInt32?                  BufferSize       = null,
                                          TimeSpan?                ReceiveTimeout   = null,
                                          TimeSpan?                SendTimeout      = null,
                                          TCPEchoLoggingDelegate?  LoggingHandler   = null)
        {

            var server = new TCPEchoTestServer(
                             IPAddress,
                             TCPPort,
                             BufferSize,
                             ReceiveTimeout,
                             SendTimeout,
                             LoggingHandler
                         );

            await server.Start();

        }

        #endregion

        #region Start()

        /// <summary>
        /// Start the EchoTestServer and begin accepting incoming TCP connections.
        /// </summary>
        public async Task Start()
        {

            try
            {

                tcpListener.Start();

                serverTask = Task.Factory.StartNew(async () => {

                    await Log("Server started!");

                    while (!cts.IsCancellationRequested)
                    {
                        try
                        {

                            var client     = await tcpListener.AcceptTcpClientAsync(cts.Token);

                            var clientTask = Task.Run(async () => {
                                                 try
                                                 {
                                                     await HandleClientAsync(client);
                                                 }
                                                 catch (Exception ex)
                                                 {
                                                     await Log($"Unhandled error in client handler wrapper: {ex.Message}");
                                                 }
                                                 finally
                                                 {
                                                     activeClients.TryRemove(client, out _);
                                                     client.Close();
                                                 }
                                             }, cts.Token);

                            activeClients.TryAdd(client, clientTask);

                        }
                        catch (OperationCanceledException) {
                            // Graceful exit on cancel
                        }
                        catch (ObjectDisposedException) {
                            // Expected when listener is stopped
                        }
                        catch (SocketException) {
                            // Expected during shutdown
                        }
                        catch (Exception ex)
                        {
                            await Log($"Error accepting client: {ex.Message}");
                        }
                    }

                },
                cts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            }
            catch (Exception ex)
            {
                await Log($"Error starting EchoTestServer: {ex.Message}");
                throw;
            }

        }

        #endregion

        #region (private) HandleClientAsync(client)

        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {

                var remoteEndpoint     = client.Client.RemoteEndPoint?.ToString();
                await Log($"Accepted connection from {remoteEndpoint}");

                client.ReceiveTimeout  = (Int32) ReceiveTimeout.TotalMilliseconds;
                client.SendTimeout     = (Int32) SendTimeout.   TotalMilliseconds;
                client.LingerState     = new LingerOption(true, 0);

                await using var stream = client.GetStream();
                await stream.CopyToAsync(stream, bufferSize: bufferSize, cts.Token).ConfigureAwait(false);

            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
            }
            catch (IOException ex)
            {
                // Connection closed or reset
                await Log($"Echo stream closed: {ex.Message}");
            }
            catch (ObjectDisposedException)
            {
                // Stream closed on other side
            }
            catch (Exception ex)
            {
                await Log($"Unhandled error in client handler: {ex.Message}");
            }
            finally
            {
                try
                {
                    client.Client.Shutdown(SocketShutdown.Send);
                }
                catch (SocketException) { }
                client.Close();
            }
        }

        #endregion

        #region Stop()

        /// <summary>
        /// Stop the EchoTestServer and close all active client connections.
        /// </summary>
        public async Task Stop()
        {

            foreach (var client in activeClients.Keys)
            {
                try
                {
                    if (activeClients.TryRemove(client, out var task))
                    {

                        client.Close();

                        // Wait for client task to complete
                        await task;

                    }
                }
                catch (Exception ex)
                {
                    await Log($"Error stopping client: {ex.Message}");
                }
            }

            cts.Cancel();
            tcpListener.Stop();

            if (serverTask is not null)
                await serverTask;

            await Log("Server stopped!");

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override string ToString()

            => $"{nameof(TCPEchoTestServer)}: {ipAddress}:{TCPPort} (BufferSize: {bufferSize}, ReceiveTimeout: {ReceiveTimeout}, SendTimeout: {SendTimeout})";

        #endregion


        #region Dispose/Async()

        public async ValueTask DisposeAsync()
        {
            await Stop();
            cts.Dispose();
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
