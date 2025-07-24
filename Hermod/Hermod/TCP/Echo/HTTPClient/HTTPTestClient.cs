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
    public class HTTPTestClient : ATCPTestClient,
                                  IDisposable,
                                  IAsyncDisposable
    {

        #region Data

        public Boolean IsHTTPConnected { get; private set; } = false;

        #endregion

        #region Properties


        #endregion

        #region Constructor(s)

        private HTTPTestClient(IIPAddress               Address,
                               IPPort                   TCPPort,
                               TimeSpan?                ConnectTimeout   = null,
                               TimeSpan?                ReceiveTimeout   = null,
                               TimeSpan?                SendTimeout      = null,
                               UInt32?                  BufferSize       = null,
                               TCPEchoLoggingDelegate?  LoggingHandler   = null)

            : base(Address,
                   TCPPort,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   BufferSize,
                   LoggingHandler)

        { }

        #endregion


        #region ConnectNew (         TCPPort, ConnectTimeout = null, ReceiveTimeout = null, SendTimeout = null, BufferSize = null, LoggingHandler = null)

        /// <summary>
        /// Create a new HTTPTestClient and connect to the given address and TCP port.
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
        /// Create a new HTTPTestClient and connect to the given address and TCP port.
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

            await reconnectAsync().ConfigureAwait(false);

            IsHTTPConnected = true;

        }

        #endregion

        #region ConnectAsync()

        public async Task ConnectAsync()
        {

            await connectAsync().ConfigureAwait(false);

            IsHTTPConnected = true;

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

        #region SendRequest (Request)

        /// <summary>
        /// Send the given HTTP Request to the server and receive the HTTP Response.
        /// </summary>
        /// <param name="Request">The HTTP Request to send.</param>
        /// <returns>Whether the echo was successful, the echoed response, an optional error response, and the time taken to send and receive it.</returns>
        public async Task<(Boolean, HTTPResponse?, String?, TimeSpan)> SendRequest(HTTPRequest Request)
        {

            if (!IsConnected)
                return (false, null, "Client is not connected.", TimeSpan.Zero);

            if (!IsHTTPConnected)
                await ReconnectAsync().ConfigureAwait(false);

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

                IMemoryOwner<Byte>? bufferOwner = MemoryPool<Byte>.Shared.Rent(bufferSize * 2);
                var buffer = bufferOwner.Memory;
                var dataLength = 0;

                while (true)
                {

                    #region Read data if no delimiter found yet

                    if (dataLength < endOfHTTPHeaderDelimiterLength ||
                        buffer.Span[0..dataLength].IndexOf(endOfHTTPHeaderDelimiter.AsSpan()) < 0)
                    {
                        if (dataLength >= buffer.Length - bufferSize)
                            throw new Exception("Header too large.");

                        var bytesRead = await stream.ReadAsync(buffer.Slice(dataLength, bufferSize), Request.CancellationToken);
                        if (bytesRead == 0)
                        {
                            bufferOwner?.Dispose();
                            return (false, null, "Timeout!", stopwatch.Elapsed);
                        }

                        dataLength += bytesRead;
                        continue;
                    }

                    #endregion

                    #region Search for End-of-HTTPHeader

                    var endOfHTTPHeaderIndex = buffer.Span[0..dataLength].IndexOf(endOfHTTPHeaderDelimiter.AsSpan());
                    if (endOfHTTPHeaderIndex < 0)
                        continue;  // Should not reach here due to the if-condition above.

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
                 //   response.BufferOwner    = bufferOwner;  // Transfer ownership to response for disposal after body is consumed.

                    #endregion

                    if (response.IsConnectionClose)
                    {
                        IsHTTPConnected = false;  // Mark connection for closure after response handling
                    }

                    return (true, response, null, stopwatch.Elapsed);

                }
            }
            catch (Exception ex)
            {
                await Log($"Error in SendRequest: {ex.Message}");
                return (false, null, ex.Message, stopwatch.Elapsed);
            }
            finally
            {
                stopwatch.Stop();
            }

        }

        #endregion

        #region SendText    (Text)

        /// <summary>
        /// Send the given message to the echo server and receive the echoed response.
        /// </summary>
        /// <param name="Text">The text message to send and echo.</param>
        /// <returns>Whether the echo was successful, the echoed response, an optional error response, and the time taken to send and receive it.</returns>
        public Task<(Boolean, String, String?, TimeSpan)> SendText(String Text)

            => sendText(Text);

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override string ToString()

            => $"{nameof(HTTPTestClient)}: {ipAddress}:{tcpPort} (Connected: {IsConnected})";

        #endregion


    }

}
