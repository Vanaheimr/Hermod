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
using System.Globalization;
using System.Net;
using System.Net.Sockets;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// Interface for streams that can report prefix consumption.
    /// </summary>
    public interface IPrefixInfo
    {
        /// <summary>
        /// The number of bytes consumed from the prefix.
        /// </summary>
        long PrefixConsumed { get; }
    }

    /// <summary>
    /// A stream that first reads from a prefix memory, then from an inner stream.
    /// </summary>
    public class PrefixStream : Stream, IPrefixInfo
    {

        private readonly ReadOnlyMemory<byte> _prefix;
        private int _prefixPosition = 0;
        private readonly Stream _inner;
        private readonly bool _leaveInnerOpen;

        public long PrefixConsumed => _prefixPosition;

        public PrefixStream(ReadOnlyMemory<byte> prefix, Stream inner, bool leaveInnerOpen = false)
        {
            _prefix = prefix;
            _inner = inner;
            _leaveInnerOpen = leaveInnerOpen;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int bytesRead = 0;

            if (_prefixPosition < _prefix.Length)
            {
                int toCopy = Math.Min(buffer.Length, _prefix.Length - _prefixPosition);
                _prefix.Slice(_prefixPosition, toCopy).CopyTo(buffer);
                _prefixPosition += toCopy;
                bytesRead += toCopy;

                if (toCopy == buffer.Length)
                    return bytesRead;

                buffer = buffer.Slice(toCopy);
            }

            int innerRead = await _inner.ReadAsync(buffer, cancellationToken);
            bytesRead += innerRead;

            return bytesRead;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = 0;

            if (_prefixPosition < _prefix.Length)
            {
                int toCopy = Math.Min(count, _prefix.Length - _prefixPosition);
                _prefix.Slice(_prefixPosition, toCopy).Span.CopyTo(buffer.AsSpan(offset, toCopy));
                _prefixPosition += toCopy;
                bytesRead += toCopy;

                if (toCopy == count)
                    return bytesRead;

                offset += toCopy;
                count -= toCopy;
            }

            int innerRead = _inner.Read(buffer, offset, count);
            bytesRead += innerRead;

            return bytesRead;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_leaveInnerOpen)
            {
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }

    }

    /// <summary>
    /// A stream that limits the total bytes that can be read.
    /// </summary>
    public class LengthLimitedStream : Stream, IPrefixInfo
    {
        private readonly Stream _inner;
        private long _remaining;
        private readonly bool _leaveInnerOpen;

        public LengthLimitedStream(Stream inner, long length, bool leaveInnerOpen = false)
        {
            _inner = inner;
            _remaining = length;
            _leaveInnerOpen = leaveInnerOpen;
        }

        public long PrefixConsumed => _inner is IPrefixInfo pi ? pi.PrefixConsumed : 0;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _remaining;
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_remaining <= 0)
                return 0;

            int maxRead = (int)Math.Min(buffer.Length, _remaining);
            int bytesRead = await _inner.ReadAsync(buffer.Slice(0, maxRead), cancellationToken);
            _remaining -= bytesRead;
            return bytesRead;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_remaining <= 0)
                return 0;

            int maxRead = (int)Math.Min(count, _remaining);
            int bytesRead = _inner.Read(buffer, offset, maxRead);
            _remaining -= bytesRead;
            return bytesRead;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_leaveInnerOpen)
            {
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// A stream that decodes chunked transfer encoding.
    /// Assumes no trailer headers are processed (just skipped).
    /// </summary>
    public class ChunkedDecodingStream : Stream, IPrefixInfo
    {
        private readonly Stream _inner;
        private long _currentChunkSize = 0;
        private long _currentChunkRead = 0;
        private bool _done = false;
        private bool _hasStarted = false;
        private readonly bool _leaveInnerOpen;

        public long PrefixConsumed => _inner is IPrefixInfo pi ? pi.PrefixConsumed : 0;

        public ChunkedDecodingStream(Stream inner, bool leaveInnerOpen = false)
        {
            _inner = inner;
            _leaveInnerOpen = leaveInnerOpen;
        }

        private async Task<string> ReadLineAsync(CancellationToken ct)
        {
            var sb = new StringBuilder();
            byte[] byteBuf = new byte[1];
            bool sawCR = false;

            while (true)
            {
                int r = await _inner.ReadAsync(byteBuf, ct);
                if (r == 0) throw new EndOfStreamException("Unexpected end during line read.");

                byte b = byteBuf[0];
                if (b == (byte)'\r')
                {
                    sawCR = true;
                    continue;
                }

                if (sawCR && b == (byte)'\n')
                    break;

                sb.Append((char)b);
                sawCR = false;
            }

            return sb.ToString();

        }

        private string ReadLine()
        {
            var sb = new StringBuilder();
            byte[] byteBuf = new byte[1];
            bool sawCR = false;

            while (true)
            {
                int r = _inner.Read(byteBuf, 0, 1);
                if (r == 0) throw new EndOfStreamException("Unexpected end during line read.");

                byte b = byteBuf[0];
                if (b == (byte)'\r')
                {
                    sawCR = true;
                    continue;
                }

                if (sawCR && b == (byte)'\n')
                    break;

                sb.Append((char)b);
                sawCR = false;
            }

            return sb.ToString();

        }

        private async Task ReadCRLFAsync(CancellationToken ct)
        {
            byte[] buf = new byte[2];
            int r = await _inner.ReadAsync(buf, ct);
            if (r < 2 || buf[0] != (byte)'\r' || buf[1] != (byte)'\n')
                throw new Exception("Expected CRLF");
        }

        private void ReadCRLF()
        {
            byte[] buf = new byte[2];
            int r = _inner.Read(buf, 0, 2);
            if (r < 2 || buf[0] != (byte)'\r' || buf[1] != (byte)'\n')
                throw new Exception("Expected CRLF");
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
        {

            if (_done)
                return 0;

            int totalRead = 0;
            Memory<byte> currentDest = destination;

            while (currentDest.Length > 0 && !_done)
            {
                if (_currentChunkRead == _currentChunkSize)
                {
                    if (_hasStarted)
                    {
                        await ReadCRLFAsync(cancellationToken);
                    }
                    _hasStarted = true;

                    string sizeLine = await ReadLineAsync(cancellationToken);
                    // Ignore extensions after ;
                    var sizeStr = sizeLine.Split(';')[0].Trim();
                    _currentChunkSize = long.Parse(sizeStr, NumberStyles.HexNumber);
                    _currentChunkRead = 0;

                    if (_currentChunkSize == 0)
                    {

                        await ReadCRLFAsync(cancellationToken);

                        // Skip trailers
                        while (true)
                        {
                            var line = await ReadLineAsync(cancellationToken);
                            if (string.IsNullOrWhiteSpace(line))
                                break;
                            // Ignore trailers for now
                        }

                        _done = true;
                        return totalRead;
                    }
                }

                int canRead = (int)Math.Min(currentDest.Length, _currentChunkSize - _currentChunkRead);
                if (canRead > 0)
                {

                    int read = await _inner.ReadAsync(currentDest, cancellationToken);
                    if (read == 0)
                        throw new Exception("Unexpected end of stream during chunk data");

                    _currentChunkRead += read;
                    totalRead += read;
                    currentDest = currentDest.Slice(read);
                }
                else
                {
                    // 0 canRead, but since if == size, loop will go to next chunk
                }
            }

            return totalRead;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {

            if (_done)
                return 0;

            int totalRead = 0;
            int currentOffset = offset;
            int remainingCount = count;

            while (remainingCount > 0 && !_done)
            {
                if (_currentChunkRead == _currentChunkSize)
                {
                    if (_hasStarted)
                    {
                        ReadCRLF();
                    }
                    _hasStarted = true;

                    string sizeLine = ReadLine();
                    // Ignore extensions after ;
                    var sizeStr = sizeLine.Split(';')[0].Trim();
                    _currentChunkSize = long.Parse(sizeStr, NumberStyles.HexNumber);
                    _currentChunkRead = 0;

                    if (_currentChunkSize == 0)
                    {

                        ReadCRLF();

                        // Skip trailers
                        while (true)
                        {
                            var line = ReadLine();
                            if (string.IsNullOrWhiteSpace(line))
                                break;
                            // Ignore trailers for now
                        }

                        _done = true;
                        return totalRead;
                    }
                }

                int canRead = (int)Math.Min(remainingCount, _currentChunkSize - _currentChunkRead);
                if (canRead > 0)
                {

                    int read = _inner.Read(buffer, currentOffset, canRead);
                    if (read == 0)
                        throw new Exception("Unexpected end of stream during chunk data");

                    _currentChunkRead += read;
                    totalRead += read;
                    currentOffset += read;
                    remainingCount -= read;
                }
                else
                {
                    // 0 canRead, but since if == size, loop will go to next chunk
                }
            }

            return totalRead;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_leaveInnerOpen)
            {
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }

    }

    /// <summary>
    /// A simple HTTP test server that listens for incoming TCP connections and processes HTTP requests, supporting pipelining.
    /// </summary>
    public class HTTPTestServer : ATCPTestServer
    {

        #region Data

        public const Int32 DefaultBufferSize = 32768;

        private static readonly Byte[] Delimiter = Encoding.UTF8.GetBytes("\r\n\r\n");

        #endregion

        #region Properties

        /// <summary>
        /// The buffer size for the TCP stream.
        /// </summary>
        public UInt32 BufferSize { get; }

        #endregion

        #region Events

        /// <summary>
        /// An event fired whenever an HTTP request is ready for processing.
        /// The handler must consume the entire body stream if present to support pipelining.
        /// Use the provided NetworkStream to send the response.
        /// </summary>
        public event Func<HTTPRequest, NetworkStream, CancellationToken, Task>? OnHTTPRequestAsync;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTPTestServer that listens on the loopback address and the given TCP port.
        /// </summary>
        /// <param name="IPAddress">The IP address to listen on. If null, the loopback address will be used.</param>
        /// <param name="TCPPort">The TCP port to listen on. If 0, a random TCP port will be assigned.</param>
        /// <param name="BufferSize">An optional buffer size for the TCP stream. If null, the default buffer size will be used.</param>
        /// <param name="ReceiveTimeout">An optional receive timeout for the TCP stream. If null, the default receive timeout will be used.</param>
        /// <param name="SendTimeout">An optional send timeout for the TCP stream. If null, the default send timeout will be used.</param>
        /// <param name="LoggingHandler">An optional logging handler that will be called for each log message.</param>
        public HTTPTestServer(IIPAddress? IPAddress = null,
                                  IPPort? TCPPort = null,
                                  UInt32? BufferSize = null,
                                  TimeSpan? ReceiveTimeout = null,
                                  TimeSpan? SendTimeout = null,
                                  TCPEchoLoggingDelegate? LoggingHandler = null)

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

        #region StartNew(...)

        // The StartNew method remains the same, but now returns the refactored server
        public static async Task<HTTPTestServer> StartNew(IIPAddress? IPAddress = null,
                                                              IPPort? TCPPort = null,
                                                              UInt32? BufferSize = null,
                                                              TimeSpan? ReceiveTimeout = null,
                                                              TimeSpan? SendTimeout = null,
                                                              TCPEchoLoggingDelegate? LoggingHandler = null)
        {
            var server = new HTTPTestServer(IPAddress, TCPPort, BufferSize, ReceiveTimeout, SendTimeout, LoggingHandler);
            await server.Start();
            return server;
        }

        #endregion

        public override async Task HandleConnection(TCPConnection Connection, CancellationToken Token)
        {

            try
            {

                int bufferSize = (int)BufferSize;
                await using var stream = Connection.TCPClient.GetStream() as NetworkStream ?? throw new InvalidOperationException("Stream is not a NetworkStream.");
                using var bufferOwner = MemoryPool<byte>.Shared.Rent(bufferSize * 2);
                var buffer = bufferOwner.Memory;
                int dataLength = 0;
                const int delimiterLength = 4;

                var socket = Connection.TCPClient.GetStream().Socket!;
                var httpSource = new HTTPSource(IPSocket.FromIPEndPoint(socket.RemoteEndPoint as IPEndPoint));
                var httpLocal = IPSocket.FromIPEndPoint(socket.LocalEndPoint as IPEndPoint);
                var httpRemote = IPSocket.FromIPEndPoint(socket.RemoteEndPoint as IPEndPoint);

                while (true)
                {

                    // Read data if no delimiter found yet
                    if (dataLength < delimiterLength || buffer.Span[0..dataLength].IndexOf(Delimiter.AsSpan()) < 0)
                    {

                        if (dataLength >= buffer.Length - bufferSize)
                        {
                            // Buffer nearly full, shift or error
                            throw new Exception("Header too large.");
                        }

                        var bytesRead = await stream.ReadAsync(buffer.Slice(dataLength, bufferSize), Token);
                        if (bytesRead == 0)
                            break;

                        dataLength += bytesRead;

                    }

                    // Search for delimiter
                    var delimiterIndex = buffer.Span[0..dataLength].IndexOf(Delimiter.AsSpan());
                    if (delimiterIndex < 0)
                        continue;

                    // Parse headers
                    var headersMemory = buffer.Slice(0, delimiterIndex);
                    var headersText = Encoding.UTF8.GetString(headersMemory.Span);
                    var request = HTTPRequest.Parse(
                                      Timestamp.Now,
                                      httpSource,
                                      httpLocal,
                                      httpRemote,
                                      headersText,
                                      CancellationToken: Token
                                  );

                    // Shift remaining data
                    var remainingStart = delimiterIndex + delimiterLength;
                    var remainingLength = dataLength - remainingStart;
                    buffer.Slice(remainingStart, remainingLength).CopyTo(buffer.Slice(0));
                    dataLength = remainingLength;

                    // Setup body stream
                    Stream? bodyDataStream = null;
                    Stream? bodyStream = null;
                    bool isChunked = false;
                    long contentLength = -1;

                    //if (request.TryGetHeaderField("Transfer-Encoding", out var te) && string.Equals(te, "chunked", StringComparison.OrdinalIgnoreCase))
                    //{
                    //    isChunked = true;
                    //}
                    if (request.ContentLength.HasValue)
                    {
                        contentLength = (Int64)request.ContentLength.Value;
                    }


                    var prefix = buffer.Slice(0, dataLength);
                    if (isChunked || contentLength >= 0)
                    {

                        bodyDataStream = new PrefixStream(prefix, stream, leaveInnerOpen: true);

                        if (isChunked)
                        {
                            bodyStream = new ChunkedDecodingStream(bodyDataStream, leaveInnerOpen: true);
                        }
                        else if (contentLength >= 0)
                        {
                            bodyStream = new LengthLimitedStream(bodyDataStream, contentLength, leaveInnerOpen: true);
                        }

                    }

                    request.HTTPBodyStream = bodyStream;

                    // Process request
                    if (OnHTTPRequestAsync is not null)
                        await OnHTTPRequestAsync(request, stream, Token);


                    // Drain remaining body if any to support pipelining
                    if (bodyStream is not null)
                    {
                        byte[] discardBuffer = new byte[4096];
                        int read;
                        while ((read = await bodyStream.ReadAsync(discardBuffer, Token)) > 0) { }
                    }

                    // Get prefix consumed and shift buffer
                    long prefixConsumed = 0;
                    if (bodyDataStream != null)
                    {
                        if (bodyDataStream is IPrefixInfo pi)
                            prefixConsumed = pi.PrefixConsumed;

                        if (bodyStream != null)
                            bodyStream.Dispose();
                        bodyDataStream.Dispose();
                    }

                    if (prefixConsumed < dataLength)
                    {
                        buffer.Slice((int)prefixConsumed, dataLength - (int)prefixConsumed).CopyTo(buffer.Slice(0));
                        dataLength -= (int)prefixConsumed;
                    }
                    else
                    {
                        dataLength = 0;
                    }

                    // Determine if keep-alive
                    bool keepAlive = true;
                    //var version = request.HTTPVersion?.ToUpperInvariant() ?? "HTTP/1.1";
                    //var connectionHeader = request.Headers.FirstOrDefault(h => h.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase)).Value;

                    //if (version == "HTTP/1.1")
                    //{
                    //    keepAlive = !(connectionHeader?.Equals("close", StringComparison.OrdinalIgnoreCase) ?? false);
                    //}
                    //else if (version == "HTTP/1.0")
                    //{
                    //    keepAlive = connectionHeader?.Equals("keep-alive", StringComparison.OrdinalIgnoreCase) ?? false;
                    //}

                    if (!keepAlive)
                        break;





















                    //// Get prefix consumed and shift buffer
                    //long prefixConsumed = 0;
                    //if (bodyDataStream != null)
                    //{
                    //    if (bodyDataStream is IPrefixInfo pi)
                    //        prefixConsumed = pi.PrefixConsumed;

                    //    if (bodyStream != null)
                    //        await bodyStream.DisposeAsync();
                    ////    await bodyDataStream.DisposeAsync();
                    //}

                    //if (prefixConsumed < dataLength)
                    //{
                    //    buffer.Slice((int)prefixConsumed, dataLength - (int)prefixConsumed).CopyTo(buffer.Slice(0));
                    //    dataLength -= (int)prefixConsumed;
                    //}
                    //else
                    //{
                    //    dataLength = 0;
                    //}

                    //// Determine if keep-alive
                    //bool keepAlive = false;
                    ////var version = request.HTTPVersion?.ToUpperInvariant() ?? "HTTP/1.1";
                    ////var connectionHeader = request.Headers.FirstOrDefault(h => h.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase)).Value;

                    ////if (version == "HTTP/1.1")
                    ////{
                    ////    keepAlive = !(connectionHeader?.Equals("close", StringComparison.OrdinalIgnoreCase) ?? false);
                    ////}
                    ////else if (version == "HTTP/1.0")
                    ////{
                    ////    keepAlive = connectionHeader?.Equals("keep-alive", StringComparison.OrdinalIgnoreCase) ?? false;
                    ////}

                    var aa = request.GetHeaderField("Connection") == "keep-alive";

                    if (!keepAlive)
                        break;

                }
            }
            catch (Exception ex)
            {
                // Log error
            }

        }

        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override string ToString()
            => $"{nameof(HTTPTestServer)}: {IPAddress}:{TCPPort} (BufferSize: {BufferSize}, ReceiveTimeout: {ReceiveTimeout}, SendTimeout: {SendTimeout})";

        #endregion

    }

}
