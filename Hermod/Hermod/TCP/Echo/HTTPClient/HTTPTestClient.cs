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
    public class HTTPTestClient : IDisposable,
                                  IAsyncDisposable
    {

        #region Data

        private static readonly Byte[] endOfHTTPHeaderDelimiter         = Encoding.UTF8.GetBytes("\r\n\r\n");
        const                   Byte   endOfHTTPHeaderDelimiterLength   = 4;

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

        private HTTPTestClient(IIPAddress               Address,
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
        public static async Task<HTTPTestClient>

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
        public static async Task<HTTPTestClient>

            ConnectNew(IIPAddress               IPAddress,
                       IPPort                   TCPPort,
                       TimeSpan?                ConnectTimeout   = null,
                       TimeSpan?                ReceiveTimeout   = null,
                       TimeSpan?                SendTimeout      = null,
                       UInt32?                  BufferSize       = null,
                       TCPEchoLoggingDelegate?  LoggingHandler   = null)

        {

            var client = new HTTPTestClient(
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

                var connectTask = tcpClient.ConnectAsync(ipAddress.Convert(), tcpPort.ToUInt16());

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



        #region CreateRequest (HTTPMethod, HTTPPath, ...)

        /// <summary>
        /// Create a new HTTP request.
        /// </summary>
        /// <param name="HTTPMethod">An HTTP method.</param>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public HTTPRequest.Builder CreateRequest(HTTPMethod                    HTTPMethod,
                                                 HTTPPath                      HTTPPath,
                                                 QueryString?                  QueryString         = null,
                                                 AcceptTypes?                  Accept              = null,
                                                 IHTTPAuthentication?          Authentication      = null,
                                                 String?                       UserAgent           = null,
                                                 ConnectionType?               Connection          = null,
                                                 Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                 CancellationToken             CancellationToken   = default)
{

            var builder = new HTTPRequest.Builder(null, CancellationToken) {
                              Host           = HTTPHostname.Localhost, // HTTPHostname.Parse((VirtualHostname ?? RemoteURL.Hostname) + (RemoteURL.Port.HasValue && RemoteURL.Port != IPPort.HTTP && RemoteURL.Port != IPPort.HTTPS ? ":" + RemoteURL.Port.ToString() : String.Empty)),
                              HTTPMethod     = HTTPMethod,
                              Path           = HTTPPath,
                              QueryString    = QueryString ?? QueryString.Empty,
                              Authorization  = Authentication,
                            //  UserAgent      = UserAgent   ?? HTTPUserAgent,
                              Connection     = Connection
                          };

            if (Accept is not null)
                builder.Accept = Accept;

            RequestBuilder?.Invoke(builder);

            return builder;

        }

        #endregion


        #region SendText   (Text)

        /// <summary>
        /// Send the given message to the echo server and receive the echoed response.
        /// </summary>
        /// <param name="Text">The text message to send and echo.</param>
        /// <returns>Whether the echo was successful, the echoed response, an optional error response, and the time taken to send and receive it.</returns>
        public async Task<(Boolean, HTTPResponse?, String?, TimeSpan)> SendRequest(HTTPRequest Request)
        {

            if (!IsConnected)
                return (false, null, "Client is not connected.", TimeSpan.Zero);

            var stopwatch = Stopwatch.StartNew();

            try
            {

                var stream     = tcpClient.GetStream();

                #region Send HTTP Request

                await stream.WriteAsync(Encoding.UTF8.GetBytes(Request.EntireRequestHeader + "\r\n\r\n"), cts.Token).ConfigureAwait(false);

                if (Request.HTTPBody is not null && Request.ContentLength > 0)
                    await stream.WriteAsync(Request.HTTPBody, cts.Token).ConfigureAwait(false);

                await stream.FlushAsync(cts.Token).ConfigureAwait(false);

                #endregion

                using var       bufferOwner   = MemoryPool<Byte>.Shared.Rent(bufferSize * 2);
                var             buffer        = bufferOwner.Memory;
                var             dataLength    = 0;

                //while (true)
                //{

                    #region Read data if no delimiter found yet

                    if (dataLength < endOfHTTPHeaderDelimiterLength ||
                        buffer.Span[0..dataLength].IndexOf(endOfHTTPHeaderDelimiter.AsSpan()) < 0)
                    {

                        if (dataLength >= buffer.Length - bufferSize)
                        {
                            // Buffer nearly full, shift or error
                            throw new Exception("Header too large.");
                        }

                        // Will read data, or wait until read timeout...
                        var bytesRead = await stream.ReadAsync(buffer.Slice(dataLength, bufferSize), Request.CancellationToken);
                        if (bytesRead == 0)
                            return (false, null, "Timeout!", stopwatch.Elapsed);

                        dataLength += bytesRead;

                    }

                    #endregion

                    #region Search for End-of-HTTPHeader

                    var endOfHTTPHeaderIndex = buffer.Span[0..dataLength].IndexOf(endOfHTTPHeaderDelimiter.AsSpan());
                    if (endOfHTTPHeaderIndex < 0)
                        return (false, null, "No HTTP response header found!", stopwatch.Elapsed);

                    #endregion

                    #region Parse HTTP Response

                    var response = HTTPResponse.Parse(
                                       //Timestamp.Now,
                                       //httpSource,
                                       //localSocket,
                                       //remoteSocket,
                                       Encoding.UTF8.GetString(buffer[..endOfHTTPHeaderIndex].Span),
                                       CancellationToken: Request.CancellationToken
                                   );

                    #endregion


                    #region Shift remaining data

                    var remainingStart = endOfHTTPHeaderIndex + endOfHTTPHeaderDelimiterLength;
                    var remainingLength = dataLength - remainingStart;
                    buffer.Slice(remainingStart, remainingLength).CopyTo(buffer[..]);
                    dataLength = remainingLength;

                    #endregion

                    #region Setup HTTP body stream

                    Stream? bodyDataStream = null;
                    Stream? bodyStream = null;

                    var prefix = buffer[..dataLength];
                    if (response.IsChunkedTransferEncoding || response.ContentLength.HasValue)
                    {

                        bodyDataStream = new PrefixStream(
                                             prefix,
                                             stream,
                                             LeaveInnerStreamOpen: true
                                         );

                        if (response.IsChunkedTransferEncoding)
                            bodyStream = new ChunkedTransferEncodingStream(
                                             bodyDataStream,
                                             LeaveInnerStreamOpen: true
                                         );

                        else if (response.ContentLength.HasValue && response.ContentLength.Value > 0)
                            bodyStream = new LengthLimitedStream(
                                             bodyDataStream,
                                             response.ContentLength.Value,
                                             LeaveInnerStreamOpen: true
                                         );

                    }

                    response.HTTPBodyStream = bodyStream;

                    #endregion


                //}

                return (true, response, null, stopwatch.Elapsed);

            }
            catch (Exception ex)
            {
                await Log($"Error in SendBinary: {ex.Message}");
                return (false, null, ex.Message, TimeSpan.Zero);
            }
            finally {
                stopwatch.Stop();
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

            => $"{nameof(HTTPTestClient)}: {ipAddress}:{tcpPort} (Connected: {IsConnected})";

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
