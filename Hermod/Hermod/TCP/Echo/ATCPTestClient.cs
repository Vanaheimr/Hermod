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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A simple TCP echo test client that can connect to a TCP echo server,
    /// </summary>
    public abstract class ATCPTestClient : IDisposable,
                                           IAsyncDisposable
    {

        #region Data

        protected static readonly Byte[] endOfHTTPHeaderDelimiter         = Encoding.UTF8.GetBytes("\r\n\r\n");
        protected const           Byte   endOfHTTPHeaderDelimiterLength   = 4;

        public static readonly TimeSpan  DefaultConnectTimeout  = TimeSpan.FromSeconds(5);
        public static readonly TimeSpan  DefaultReceiveTimeout  = TimeSpan.FromSeconds(5);
        public static readonly TimeSpan  DefaultSendTimeout     = TimeSpan.FromSeconds(5);
        public const           Int32     DefaultBufferSize      = 4096;

        protected readonly  TCPEchoLoggingDelegate?   loggingHandler;
        protected           TcpClient?                tcpClient;
        protected           CancellationTokenSource?  cts;

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
        public IPEndPoint?  LocalEndPoint
            => tcpClient?.Client.LocalEndPoint as IPEndPoint;

        /// <summary>
        /// The local TCP port of the connected echo server.
        /// </summary>
        public UInt16?      LocalTCPPort

            => LocalEndPoint is not null
                   ? (UInt16) LocalEndPoint.Port
                   : null;

        /// <summary>
        /// The local IP address of the connected echo server.
        /// </summary>
        public IIPAddress?  LocalIPAddress

            => LocalEndPoint is not null
                   ? IPAddress.Parse(LocalEndPoint.Address.GetAddressBytes())
                   : null;


        /// <summary>
        /// The remote IP end point of the connected echo server.
        /// </summary>
        public IPEndPoint?  RemoteEndPoint
            => tcpClient?.Client.RemoteEndPoint as IPEndPoint;

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


        public  IIPAddress               ipAddress          { get; }
        public  IPPort                   tcpPort            { get; }
        public  TimeSpan                 ConnectTimeout     { get; }
        public  TimeSpan                 ReceiveTimeout     { get; }
        public  TimeSpan                 SendTimeout        { get; }
        public  Int32                    bufferSize         { get; }

        #endregion

        #region Constructor(s)

        protected ATCPTestClient(IIPAddress               Address,
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

        }

        #endregion


        #region ReconnectAsync()

        public async Task reconnectAsync()
        {

            cts?.      Cancel();
            tcpClient?.Close();
            cts?.      Dispose();

            // recreate _cts and tcpClient
            await connectAsync();

        }

        #endregion

        #region (protected) ConnectAsync()

        protected async Task connectAsync()
        {

            try
            {

                cts               = new CancellationTokenSource();
                tcpClient         = new TcpClient();
                var connectTask   = tcpClient.ConnectAsync(ipAddress.Convert(), tcpPort.ToUInt16());

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


        #region sendText   (Text)

        /// <summary>
        /// Send the given message to the echo server and receive the echoed response.
        /// </summary>
        /// <param name="Text">The text message to send and echo.</param>
        /// <returns>Whether the echo was successful, the echoed response, an optional error response, and the time taken to send and receive it.</returns>
        protected async Task<(Boolean, String, String?, TimeSpan)> sendText(String Text)
        {

            var response  = await sendBinary(Encoding.UTF8.GetBytes(Text));
            var text      = Encoding.UTF8.GetString(response.Item2, 0, response.Item2.Length);

            return (response.Item1,
                    text,
                    response.Item3,
                    response.Item4);

        }

        #endregion

        #region sendBinary (Bytes)

        /// <summary>
        /// Send the given bytes to the echo server and receive the echoed response.
        /// </summary>
        /// <param name="Bytes">The bytes to send and echo.</param>
        /// <returns>Whether the echo was successful, the echoed response, an optional error response, and the time taken to send and receive it.</returns>
        protected async Task<(Boolean, Byte[], String?, TimeSpan)> sendBinary(Byte[] Bytes)
        {

            if (!IsConnected)
                return (false, Array.Empty<byte>(), "Client is not connected.", TimeSpan.Zero);

            try
            {

                var stopwatch = Stopwatch.StartNew();
                var stream    = tcpClient.GetStream();

                // Send the data
                await stream.WriteAsync(Bytes, cts.Token).ConfigureAwait(false);
                await stream.FlushAsync(cts.Token).ConfigureAwait(false);

                using var responseStream = new MemoryStream();
                var buffer = new Byte[8192];
                int bytesRead;

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
                return (false, Array.Empty<byte>(), ex.Message, TimeSpan.Zero);
            }

        }

        #endregion


        #region (protected) Log(Message)

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

            => $"{nameof(ATCPTestClient)}: {ipAddress}:{tcpPort} (Connected: {IsConnected})";

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
