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

using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Security.Cryptography;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A simple TCP echo test client that can connect to a TCP echo server,
    /// </summary>
    public class TCPEchoTestClient : IDisposable,
                                     IAsyncDisposable
    {

        #region Data

        public static readonly TimeSpan  DefaultConnectTimeout  = TimeSpan.FromSeconds(5);
        public static readonly TimeSpan  DefaultReceiveTimeout  = TimeSpan.FromSeconds(5);
        public static readonly TimeSpan  DefaultSendTimeout     = TimeSpan.FromSeconds(5);
        public const           Int32     DefaultBufferSize      = 4096;

        private readonly  IIPAddress               ipAddress;
        private readonly  IPPort                   tcpPort;
        private readonly  TimeSpan                 ConnectTimeout;
        private readonly  TimeSpan                 ReceiveTimeout;
        private readonly  TimeSpan                 SendTimeout;
        private readonly  Int32                    bufferSize;
        private readonly  TCPEchoLoggingDelegate?  loggingHandler;
        private readonly  TcpClient                tcpClient;
        private readonly  CancellationTokenSource  cts;

        #endregion

        #region Properties

        /// <summary>
        /// Whether the client is currently connected to the echo server.
        /// </summary>
        public Boolean      IsConnected
            => tcpClient.Connected;

        /// <summary>
        /// The remote IP end point of the connected echo server.
        /// </summary>
        public IPEndPoint?  RemoteEndPoint
            => tcpClient.Client.RemoteEndPoint as IPEndPoint;

        /// <summary>
        /// The remote TCP port of the connected echo server.
        /// </summary>
        public UInt16?      RemoteTCPPort

            => RemoteEndPoint is not null
                   ? (UInt16) RemoteEndPoint.Port
                   : null;

        /// <summary>
        /// The remote IP address of the connected echo server.
        /// </summary>
        public IIPAddress?  RemoteIPAddress

            => RemoteEndPoint is not null
                   ? IPAddress.Parse(RemoteEndPoint.Address.GetAddressBytes())
                   : null;

        #endregion

        #region Constructor(s)

        private TCPEchoTestClient(IIPAddress               Address,
                                  IPPort                   TCPPort,
                                  TimeSpan?                ConnectTimeout   = null,
                                  TimeSpan?                ReceiveTimeout   = null,
                                  TimeSpan?                SendTimeout      = null,
                                  UInt32?                  BufferSize       = null,
                                  TCPEchoLoggingDelegate?  LoggingHandler   = null)
        {

            if (ConnectTimeout.HasValue && ConnectTimeout.Value.TotalMilliseconds > Int32.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(ConnectTimeout), "Timeout too large for socket.");

            if (ReceiveTimeout.HasValue && ReceiveTimeout.Value.TotalMilliseconds > Int32.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(ReceiveTimeout), "Timeout too large for socket.");

            if (SendTimeout.   HasValue && SendTimeout.   Value.TotalMilliseconds > Int32.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(SendTimeout),    "Timeout too large for socket.");

            this.ipAddress       = Address;
            this.tcpPort         = TCPPort;
            this.bufferSize      = BufferSize.HasValue
                                       ? BufferSize.Value > Int32.MaxValue
                                             ? throw new ArgumentOutOfRangeException(nameof(BufferSize), "The buffer size must not exceed Int32.MaxValue!")
                                             : (Int32) BufferSize.Value
                                       : DefaultBufferSize;
            this.ConnectTimeout  = ConnectTimeout ?? DefaultConnectTimeout;
            this.ReceiveTimeout  = ReceiveTimeout ?? DefaultReceiveTimeout;
            this.SendTimeout     = SendTimeout    ?? DefaultSendTimeout;
            this.loggingHandler  = LoggingHandler;
            this.tcpClient       = new TcpClient();
            this.cts             = new CancellationTokenSource();

        }

        #endregion


        #region ConnectNew (         TCPPort, ConnectTimeout = null, ReceiveTimeout = null, SendTimeout = null, BufferSize = null, LoggingHandler = null)

        /// <summary>
        /// Create a new EchoTestClient and connect to the given address and TCP port.
        /// </summary>
        /// <param name="TCPPort">The TCP port to connect to.</param>
        /// <param name="ConnectTimeout">An optional timeout for the connection attempt.</param>
        /// <param name="ReceiveTimeout">An optional timeout for receiving data.</param>
        /// <param name="SendTimeout">An optional timeout for sending data.</param>
        /// <param name="BufferSize">An optional buffer size for sending and receiving data.</param>
        /// <param name="LoggingHandler">An optional logging handler to log messages.</param>
        public static async Task<TCPEchoTestClient>

            ConnectNew(IPPort                   TCPPort,
                       TimeSpan?                ConnectTimeout   = null,
                       TimeSpan?                ReceiveTimeout   = null,
                       TimeSpan?                SendTimeout      = null,
                       UInt32?                  BufferSize       = null,
                       TCPEchoLoggingDelegate?  LoggingHandler   = null)

                => await ConnectNew(
                             IPvXAddress.Localhost,
                             TCPPort,
                             ConnectTimeout,
                             ReceiveTimeout,
                             SendTimeout,
                             BufferSize,
                             LoggingHandler
                         );

        #endregion

        #region ConnectNew (Address, TCPPort, ConnectTimeout = null, ReceiveTimeout = null, SendTimeout = null, BufferSize = null, LoggingHandler = null)

        /// <summary>
        /// Create a new EchoTestClient and connect to the given address and TCP port.
        /// </summary>
        /// <param name="IPAddress">The IP address to connect to.</param>
        /// <param name="TCPPort">The TCP port to connect to.</param>
        /// <param name="ConnectTimeout">An optional timeout for the connection attempt.</param>
        /// <param name="ReceiveTimeout">An optional timeout for receiving data.</param>
        /// <param name="SendTimeout">An optional timeout for sending data.</param>
        /// <param name="BufferSize">An optional buffer size for sending and receiving data.</param>
        /// <param name="LoggingHandler">An optional logging handler to log messages.</param>
        public static async Task<TCPEchoTestClient>

            ConnectNew(IIPAddress               IPAddress,
                       IPPort                   TCPPort,
                       TimeSpan?                ConnectTimeout   = null,
                       TimeSpan?                ReceiveTimeout   = null,
                       TimeSpan?                SendTimeout      = null,
                       UInt32?                  BufferSize       = null,
                       TCPEchoLoggingDelegate?  LoggingHandler   = null)

        {

            var client = new TCPEchoTestClient(
                             IPAddress,
                             TCPPort,
                             ConnectTimeout,
                             ReceiveTimeout,
                             SendTimeout,
                             BufferSize,
                             LoggingHandler
                         );

            await client.ConnectAsync();

            return client;

        }

        #endregion

        #region ReconnectAsync()

        public async Task ReconnectAsync()
        {

            cts.     Cancel();
            tcpClient.Close();
            cts.     Dispose();

            // recreate _cts and tcpClient
            await ConnectAsync();

        }

        #endregion


        #region (private) ConnectAsync()

        private async Task ConnectAsync()
        {

            try
            {

                var connectTask = tcpClient.ConnectAsync(ipAddress.ToDotNet(), tcpPort.ToUInt16());

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
            catch (Exception ex)
            {
                await Log($"Error connecting EchoTestClient: {ex.Message}");
                throw;
            }

        }

        #endregion


        #region SendText   (Text)

        /// <summary>
        /// Send the given message to the echo server and receive the echoed response.
        /// </summary>
        /// <param name="Text">The text message to send and echo.</param>
        /// <returns>Whether the echo was successful, the echoed response, an optional error response, and the time taken to send and receive it.</returns>
        public async Task<(Boolean, String, String?, TimeSpan)> SendText(String Text)
        {

            var response  = await SendBinary(Encoding.UTF8.GetBytes(Text));
            var text      = Encoding.UTF8.GetString(response.Item2, 0, response.Item2.Length);

            return (response.Item1,
                    text,
                    response.Item3,
                    response.Item4);

        }

        #endregion

        #region SendBinary (Bytes)

        /// <summary>
        /// Send the given bytes to the echo server and receive the echoed response.
        /// </summary>
        /// <param name="Bytes">The bytes to send and echo.</param>
        /// <returns>Whether the echo was successful, the echoed response, an optional error response, and the time taken to send and receive it.</returns>
        public async Task<(Boolean, Byte[], String?, TimeSpan)> SendBinary(Byte[] Bytes)
        {

            if (!IsConnected)
                return (false, [], "Client is not connected.", TimeSpan.Zero);

            try
            {

                var stopwatch = Stopwatch.StartNew();
                var stream    = tcpClient.GetStream();

                await stream.WriteAsync(Bytes, cts.Token).ConfigureAwait(false);
                await stream.FlushAsync(cts.Token).       ConfigureAwait(false);

                var buffer    = new Byte[Bytes.Length];
                var offset    = 0;
                while (offset < buffer.Length)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(offset), cts.Token);
                    if (read == 0)
                        return (false, [], "Connection closed before full echo received.", stopwatch.Elapsed);
                    offset += read;
                }

                if (offset != Bytes.Length)
                    return (false, [], "Echoed response length mismatch.",  stopwatch.Elapsed);

                if (!Bytes.SequenceEqual(buffer))
                    return (false, [], "Echoed response content mismatch.", stopwatch.Elapsed);

                stopwatch.Stop();

                return (true, buffer, null, stopwatch.Elapsed);

            }
            catch (Exception ex)
            {
                await Log($"Error in SendAndEchoAsync: {ex.Message}");
                throw;
            }

        }

        #endregion

        #region SendStreamAndVerifyEcho(SourceStream, DestinationStream = null, VerifyData = true)

        /// <summary>
        /// Stream data to the echo server without full buffering, receive the echo, and optionally verify or save it.
        /// </summary>
        /// <param name="SourceStream">The input stream to send (e.g., FileStream or MemoryStream).</param>
        /// <param name="DestinationStream">Optional stream to write the received echo to (e.g., for saving).</param>
        /// <param name="VerifyData">If true, compute SHA256 hashes on send/receive to verify echo without buffering full data.</param>
        public async Task<(String, TimeSpan)>

            SendStreamAndVerifyEcho(Stream   SourceStream,
                                    Stream?  DestinationStream  = null,
                                    Boolean  VerifyData         = true)

        {

            if (!IsConnected)
                return ("Client is not connected!", TimeSpan.Zero);

            var stopwatch = Stopwatch.StartNew();

            try
            {

                Byte[]? sendHash     = null;
                Byte[]? receiveHash  = null;

                var networkStream    = tcpClient.GetStream();

                #region Send:     Copy source to network, hash if verifying

                if (VerifyData)
                {

                    using var sha256 = SHA256.Create();
                    var       buffer = new Byte[bufferSize];

                    int read;
                    while ((read = await SourceStream.ReadAsync(buffer, cts.Token).ConfigureAwait(false)) > 0)
                    {
                        sha256.TransformBlock(buffer, 0, read, null, 0);
                        await networkStream.WriteAsync(buffer.AsMemory(0, read), cts.Token).ConfigureAwait(false);
                    }

                    sha256.TransformFinalBlock([], 0, 0);
                    sendHash = sha256.Hash;

                }
                else
                {
                    await SourceStream.CopyToAsync(networkStream, bufferSize, cts.Token).ConfigureAwait(false);
                }

                await networkStream.FlushAsync(cts.Token).ConfigureAwait(false);

                #endregion

                #region Receive: Copy network to destination or discard, hash if verifying

                if (VerifyData)
                {

                    using var sha256 = SHA256.Create();
                    var       buffer = new Byte[bufferSize];

                    int read;
                    while ((read = await networkStream.ReadAsync(buffer, cts.Token).ConfigureAwait(false)) > 0)
                    {
                        sha256.TransformBlock(buffer, 0, read, null, 0);
                        if (DestinationStream is not null)
                            await DestinationStream.WriteAsync(buffer.AsMemory(0, read), cts.Token).ConfigureAwait(false);
                    }

                    sha256.TransformFinalBlock([], 0, 0);
                    receiveHash = sha256.Hash;

                }
                else if (DestinationStream is not null)
                {
                    await networkStream.CopyToAsync(DestinationStream, bufferSize, cts.Token).ConfigureAwait(false);
                }
                else
                {

                    // Discard: Read until EOF without storing
                    var buffer = new Byte[bufferSize];

                    while (await networkStream.ReadAsync(buffer, cts.Token).ConfigureAwait(false) > 0)
                    { }

                }

                #endregion

                #region Verify data, if requested

                stopwatch.Stop();

                if (VerifyData)
                {

                    if (sendHash is null)
                        return ("Send hash is null!", stopwatch.Elapsed);

                    if (receiveHash is null)
                        return ("Receive hash is null!", stopwatch.Elapsed);

                    if (!sendHash.SequenceEqual(receiveHash))
                        return ($"Hash mismatch! Send: {Convert.ToHexString(sendHash)}, Receive: {Convert.ToHexString(receiveHash)}", stopwatch.Elapsed);

                }

                #endregion

                return ("Ok!", stopwatch.Elapsed);

            }
            catch (Exception e)
            {
                stopwatch.Stop();
                return ($"Error in SendStreamAndVerifyEcho: {e.Message}", stopwatch.Elapsed);
            }

        }

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
                    DebugX.LogT($"Error in logging handler: {e.Message}");
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
        public override string ToString()

            => $"{nameof(TCPEchoTestClient)}: {ipAddress}:{tcpPort} (Connected: {IsConnected})";

        #endregion


        #region Dispose / IAsyncDisposable

        public async ValueTask DisposeAsync()
        {
            await Close();
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
