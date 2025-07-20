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
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Net;
using System.Net.Sockets;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A simple HTTP test server that listens for incoming TCP connections and processes HTTP requests, supporting pipelining.
    /// </summary>
    public abstract class AHTTPTestServer : ATCPTestServer
    {

        #region Data

        public const Int32 DefaultBufferSize = 32768;

        private static readonly Byte[] endOfHTTPHeaderDelimiter         = Encoding.UTF8.GetBytes("\r\n\r\n");
        const                   Byte   endOfHTTPHeaderDelimiterLength   = 4;

        #endregion

        #region Properties

        /// <summary>
        /// The buffer size for the TCP stream.
        /// </summary>
        public UInt32  BufferSize    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new abstract HTTPTestServer that listens on the loopback address and the given TCP port.
        /// </summary>
        /// <param name="IPAddress">The IP address to listen on. If null, the loopback address will be used.</param>
        /// <param name="TCPPort">The TCP port to listen on. If 0, a random TCP port will be assigned.</param>
        /// <param name="BufferSize">An optional buffer size for the TCP stream. If null, the default buffer size will be used.</param>
        /// <param name="ReceiveTimeout">An optional receive timeout for the TCP stream. If null, the default receive timeout will be used.</param>
        /// <param name="SendTimeout">An optional send timeout for the TCP stream. If null, the default send timeout will be used.</param>
        /// <param name="LoggingHandler">An optional logging handler that will be called for each log message.</param>
        public AHTTPTestServer(IIPAddress?              IPAddress        = null,
                               IPPort?                  TCPPort          = null,
                               UInt32?                  BufferSize       = null,
                               TimeSpan?                ReceiveTimeout   = null,
                               TimeSpan?                SendTimeout      = null,
                               TCPEchoLoggingDelegate?  LoggingHandler   = null)

            : base(IPAddress,
                   TCPPort,
                   ReceiveTimeout,
                   SendTimeout,
                   LoggingHandler)

        {

            this.BufferSize = BufferSize.HasValue
                                  ? BufferSize.Value > Int32.MaxValue
                                        ? throw new ArgumentOutOfRangeException(nameof(BufferSize), "The buffer size must not exceed Int32.MaxValue!")
                                        : (UInt32) BufferSize.Value
                                  : DefaultBufferSize;

        }

        #endregion


        #region HandleConnection(Connection, CancellationToken = default)

        protected override async Task HandleConnection(TCPConnection      Connection,
                                                       CancellationToken  CancellationToken   = default)
        {

            try
            {

                #region Data

                Int32           bufferSize    = (Int32) BufferSize;
                await using var stream        = Connection.TCPClient.GetStream() as NetworkStream
                                                  ?? throw new InvalidOperationException("Stream is not a NetworkStream.");
                using var       bufferOwner   = MemoryPool<Byte>.Shared.Rent(bufferSize * 2);
                var             buffer        = bufferOwner.Memory;
                var             dataLength    = 0;

                var             localSocket   = IPSocket.FromIPEndPoint((stream.Socket.LocalEndPoint  as IPEndPoint)!);
                var             remoteSocket  = IPSocket.FromIPEndPoint((stream.Socket.RemoteEndPoint as IPEndPoint)!);
                var             httpSource    = new HTTPSource(remoteSocket);

                #endregion

                while (true)
                {

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
                        var bytesRead = await stream.ReadAsync(buffer.Slice(dataLength, bufferSize), CancellationToken);
                        if (bytesRead == 0)
                            break;

                        dataLength += bytesRead;

                    }

                    #endregion

                    #region Search for End-of-HTTPHeader

                    var endOfHTTPHeaderIndex = buffer.Span[0..dataLength].IndexOf(endOfHTTPHeaderDelimiter.AsSpan());
                    if (endOfHTTPHeaderIndex < 0)
                        continue;

                    #endregion

                    #region Parse HTTP Request

                    var request        = HTTPRequest.Parse(
                                             Timestamp.Now,
                                             httpSource,
                                             localSocket,
                                             remoteSocket,
                                             Encoding.UTF8.GetString(buffer[..endOfHTTPHeaderIndex].Span),
                                             CancellationToken: CancellationToken
                                         );

                    #endregion


                    #region Shift remaining data

                    var remainingStart  = endOfHTTPHeaderIndex + endOfHTTPHeaderDelimiterLength;
                    var remainingLength = dataLength           - remainingStart;
                    buffer.Slice(remainingStart, remainingLength).CopyTo(buffer[..]);
                    dataLength = remainingLength;

                    #endregion

                    #region Setup HTTP body stream

                    Stream? bodyDataStream  = null;
                    Stream? bodyStream      = null;

                    var prefix = buffer[..dataLength];
                    if (request.IsChunkedTransferEncoding || request.ContentLength.HasValue)
                    {

                        bodyDataStream = new PrefixStream(
                                             prefix,
                                             stream,
                                             LeaveInnerStreamOpen: true
                                         );

                        if (request.IsChunkedTransferEncoding)
                            bodyStream = new ChunkedTransferEncodingStream(
                                             bodyDataStream,
                                             LeaveInnerStreamOpen: true
                                         );

                        else if (request.ContentLength.HasValue && request.ContentLength.Value > 0)
                            bodyStream = new LengthLimitedStream(
                                             bodyDataStream,
                                             request.ContentLength.Value,
                                             LeaveInnerStreamOpen: true
                                         );

                    }

                    request.HTTPBodyStream = bodyStream;

                    #endregion


                    await ProcessHTTPRequest(
                              request,
                              stream,
                              CancellationToken
                          );


                    #region When the upper layer did not consume all of the body stream, we will discard the remaining data to support pipelining

                    if (bodyStream is not null)
                    {

                        var discardBuffer = new Byte[4096];
                        int read;

                        while ((read = await bodyStream.ReadAsync(discardBuffer, CancellationToken)) > 0)
                        { }

                    }

                    #endregion

                    #region Get prefix consumed and shift buffer

                    UInt64 prefixConsumed = 0;
                    if (bodyDataStream is not null)
                    {

                        if (bodyDataStream is IPrefixInfo pi)
                            prefixConsumed = pi.PrefixConsumed;

                        bodyStream?.   Dispose();
                        bodyDataStream.Dispose();

                    }

                    if (prefixConsumed < (UInt64) dataLength)
                    {
                        buffer[(int) prefixConsumed..dataLength].CopyTo(buffer[..]);
                        dataLength -= (int) prefixConsumed;
                    }
                    else
                        dataLength = 0;

                    #endregion


                    if (!request.IsKeepAlive)
                        break;

                }
            }
            catch (Exception e)
            {
                
            }

        }

        #endregion


        /// <summary>
        /// Process the given HTTP request.
        /// </summary>
        /// <param name="Request">The HTTP request to process.</param>
        /// <param name="Stream">The network stream for reading the HTTP body and sending the response.</param>
        /// <param name="CancellationToken">An optional cancellation token to cancel the processing of the HTTP request.</param>
        protected abstract Task ProcessHTTPRequest(HTTPRequest        Request,
                                                   NetworkStream      Stream,
                                                   CancellationToken  CancellationToken   = default);


        #region (private) LogEvent (Logger, LogHandler, ...)

        private Task LogEvent<TDelegate>(TDelegate?                                         Logger,
                                         Func<TDelegate, Task>                              LogHandler,
                                         [CallerArgumentExpression(nameof(Logger))] String  EventName     = "",
                                         [CallerMemberName()]                       String  OICPCommand   = "")

            where TDelegate : Delegate

            => LogEvent(
                   nameof(HTTPTestServer),
                   Logger,
                   LogHandler,
                   EventName,
                   OICPCommand
               );

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{IPAddress}:{TCPPort} (BufferSize: {BufferSize}, ReceiveTimeout: {ReceiveTimeout}, SendTimeout: {SendTimeout})";

        #endregion

    }

}
