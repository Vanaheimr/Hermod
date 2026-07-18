/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 */

#region Usings

using System.Net;
using System.Net.Sockets;
using System.Text;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// A raw HTTP/1.x TCP connection for wire-level interoperability tests.
    /// It deliberately does not normalize requests or responses like an HTTP client would.
    /// </summary>
    internal sealed class HTTPRawSocketClient : IAsyncDisposable
    {

        #region Data

        private static readonly Byte[] headerDelimiter = "\r\n\r\n".ToUTF8Bytes();
        private static readonly Byte[] lineDelimiter   = "\r\n".ToUTF8Bytes();

        private readonly TcpClient    tcpClient;
        private readonly NetworkStream stream;
        private readonly List<Byte>   receiveBuffer = [];

        #endregion

        #region Constructor(s)

        private HTTPRawSocketClient(TcpClient TCPClient)
        {

            tcpClient = TCPClient;
            stream    = TCPClient.GetStream();

        }

        #endregion

        #region ConnectAsync(Address, Port, CancellationToken = default)

        public static async Task<HTTPRawSocketClient> ConnectAsync(System.Net.IPAddress  Address,
                                                                    IPPort                 Port,
                                                                    CancellationToken      CancellationToken   = default)
        {

            var tcpClient = new TcpClient(Address.AddressFamily);

            try
            {

                await tcpClient.ConnectAsync(
                          Address,
                          Port.ToInt32(),
                          CancellationToken
                      );

                return new HTTPRawSocketClient(tcpClient);

            }
            catch
            {
                tcpClient.Dispose();
                throw;
            }

        }

        #endregion

        #region SendAsync(Data, CancellationToken = default)

        public Task SendAsync(String              Data,
                              CancellationToken   CancellationToken   = default)
            => SendAsync(
                   Encoding.Latin1.GetBytes(Data),
                   CancellationToken
               );

        public async Task SendAsync(ReadOnlyMemory<Byte>  Data,
                                    CancellationToken      CancellationToken   = default)
        {

            await stream.WriteAsync(Data, CancellationToken);
            await stream.FlushAsync(CancellationToken);

        }

        #endregion

        #region ShutdownSend()

        /// <summary>
        /// Signal end-of-input while keeping the receive direction open.
        /// </summary>
        public void ShutdownSend()
            => tcpClient.Client.Shutdown(SocketShutdown.Send);

        #endregion

        #region SendSegmentsAsync(Segments, CancellationToken = default)

        /// <summary>
        /// Send a request in explicit TCP write segments. This is essential for testing
        /// a server's incremental request parsing without relying on packet timing.
        /// </summary>
        public async Task SendSegmentsAsync(IEnumerable<HTTPRawSocketSegment>  Segments,
                                            CancellationToken                   CancellationToken   = default)
        {

            foreach (var segment in Segments)
            {

                await SendAsync(segment.Data, CancellationToken);

                if (segment.DelayAfter > TimeSpan.Zero)
                    await Task.Delay(segment.DelayAfter, CancellationToken);

            }

        }

        #endregion

        #region ReadResponseAsync(ResponseHasNoBody = false, CancellationToken = default)

        /// <summary>
        /// Read exactly one framed HTTP response. Supports Content-Length and chunked bodies.
        /// For close-delimited bodies use <see cref="ReadCloseDelimitedResponseAsync"/> explicitly.
        /// </summary>
        public async Task<HTTPRawSocketResponse> ReadResponseAsync(Boolean            ResponseHasNoBody    = false,
                                                                    CancellationToken  CancellationToken      = default)
        {

            var headerBytes   = await ReadUntilAsync(headerDelimiter, CancellationToken);
            var headerText    = Encoding.Latin1.GetString(headerBytes);
            var response      = HTTPRawSocketResponse.ParseHeader(headerText);
            var statusCode    = response.StatusCode;
            var hasNoBody     = ResponseHasNoBody ||
                                statusCode is >= 100 and < 200 or 204 or 304;

            if (hasNoBody)
                return response;

            if (response.IsChunked)
            {

                var (body, trailers) = await ReadChunkedBodyAsync(CancellationToken);

                return response with {
                           Body     = body,
                           Trailers = trailers
                       };

            }

            if (response.ContentLength is UInt64 contentLength)
            {

                if (contentLength > Int32.MaxValue)
                    throw new InvalidOperationException("The test helper does not buffer HTTP bodies larger than Int32.MaxValue.");

                return response with {
                           Body = await ReadExactlyAsync((Int32) contentLength, CancellationToken)
                       };

            }

            throw new InvalidOperationException(
                      "The response body is close-delimited. Use ReadCloseDelimitedResponseAsync() to make that expectation explicit."
                  );

        }

        #endregion

        #region ReadCloseDelimitedResponseAsync(ResponseHasNoBody = false, CancellationToken = default)

        public async Task<HTTPRawSocketResponse> ReadCloseDelimitedResponseAsync(Boolean            ResponseHasNoBody    = false,
                                                                                   CancellationToken  CancellationToken      = default)
        {

            var headerBytes   = await ReadUntilAsync(headerDelimiter, CancellationToken);
            var headerText    = Encoding.Latin1.GetString(headerBytes);
            var response      = HTTPRawSocketResponse.ParseHeader(headerText);
            var statusCode    = response.StatusCode;
            var hasNoBody     = ResponseHasNoBody ||
                                statusCode is >= 100 and < 200 or 204 or 304;

            if (hasNoBody)
                return response;

            if (response.IsChunked || response.ContentLength.HasValue)
                throw new InvalidOperationException("The response is self-delimited; use ReadResponseAsync() instead.");

            var body = new List<Byte>(receiveBuffer);
            receiveBuffer.Clear();

            var readBuffer = new Byte[4096];

            while (true)
            {

                var bytesRead = await stream.ReadAsync(readBuffer, CancellationToken);
                if (bytesRead == 0)
                    break;

                body.AddRange(readBuffer.AsSpan(0, bytesRead).ToArray());

            }

            return response with { Body = body.ToArray() };

        }

        #endregion

        #region (private) ReadChunkedBodyAsync(CancellationToken)

        private async Task<(Byte[] Body, IReadOnlyDictionary<String, IReadOnlyList<String>> Trailers)>
            ReadChunkedBodyAsync(CancellationToken CancellationToken)
        {

            var body     = new List<Byte>();
            var trailers = new Dictionary<String, List<String>>(StringComparer.OrdinalIgnoreCase);

            while (true)
            {

                var sizeLine       = Encoding.Latin1.GetString(await ReadUntilAsync(lineDelimiter, CancellationToken));
                var extensionIndex = sizeLine.IndexOf(';');
                var sizeText       = extensionIndex >= 0
                                         ? sizeLine[..extensionIndex]
                                         : sizeLine;

                if (!UInt64.TryParse(
                         sizeText,
                         System.Globalization.NumberStyles.AllowHexSpecifier,
                         System.Globalization.CultureInfo.InvariantCulture,
                         out var chunkLength
                     ) || chunkLength > Int32.MaxValue)
                {
                    throw new FormatException($"Invalid chunk-size '{sizeLine}' in HTTP response.");
                }

                if (chunkLength == 0)
                {

                    while (true)
                    {

                        var trailerLine = Encoding.Latin1.GetString(await ReadUntilAsync(lineDelimiter, CancellationToken));
                        if (trailerLine.Length == 0)
                            break;

                        HTTPRawSocketResponse.AddHeader(trailers, trailerLine);

                    }

                    return (
                               body.ToArray(),
                               trailers.ToDictionary(
                                   pair => pair.Key,
                                   pair => (IReadOnlyList<String>) pair.Value
                               )
                           );

                }

                body.AddRange(await ReadExactlyAsync((Int32) chunkLength, CancellationToken));

                var chunkTerminator = await ReadExactlyAsync(2, CancellationToken);
                if (chunkTerminator[0] != (Byte) '\r' || chunkTerminator[1] != (Byte) '\n')
                    throw new FormatException("Missing CRLF after an HTTP response chunk.");

            }

        }

        #endregion

        #region (private) ReadUntilAsync(Delimiter, CancellationToken)

        private async Task<Byte[]> ReadUntilAsync(Byte[]             Delimiter,
                                                   CancellationToken  CancellationToken)
        {

            while (true)
            {

                var delimiterIndex = IndexOf(receiveBuffer, Delimiter);
                if (delimiterIndex >= 0)
                {

                    var data = receiveBuffer.Take(delimiterIndex).ToArray();
                    receiveBuffer.RemoveRange(0, delimiterIndex + Delimiter.Length);

                    return data;

                }

                var readBuffer = new Byte[4096];
                var bytesRead  = await stream.ReadAsync(readBuffer, CancellationToken);

                if (bytesRead == 0)
                    throw new EndOfStreamException("Unexpected end of stream while reading an HTTP response.");

                receiveBuffer.AddRange(readBuffer.AsSpan(0, bytesRead).ToArray());

            }

        }

        #endregion

        #region (private) IndexOf(Buffer, Pattern)

        private static Int32 IndexOf(List<Byte> Buffer,
                                     Byte[]     Pattern)
        {

            for (var index = 0; index <= Buffer.Count - Pattern.Length; index++)
            {

                var matches = true;

                for (var patternIndex = 0; patternIndex < Pattern.Length; patternIndex++)
                {

                    if (Buffer[index + patternIndex] != Pattern[patternIndex])
                    {
                        matches = false;
                        break;
                    }

                }

                if (matches)
                    return index;

            }

            return -1;

        }

        #endregion

        #region (private) ReadExactlyAsync(Length, CancellationToken)

        private async Task<Byte[]> ReadExactlyAsync(Int32              Length,
                                                     CancellationToken  CancellationToken)
        {

            while (receiveBuffer.Count < Length)
            {

                var readBuffer = new Byte[Math.Max(4096, Length - receiveBuffer.Count)];
                var bytesRead  = await stream.ReadAsync(readBuffer, CancellationToken);

                if (bytesRead == 0)
                    throw new EndOfStreamException("Unexpected end of stream while reading an HTTP response body.");

                receiveBuffer.AddRange(readBuffer.AsSpan(0, bytesRead).ToArray());

            }

            var data = receiveBuffer.Take(Length).ToArray();
            receiveBuffer.RemoveRange(0, Length);

            return data;

        }

        #endregion

        #region DisposeAsync()

        public ValueTask DisposeAsync()
        {

            stream.Dispose();
            tcpClient.Dispose();

            return ValueTask.CompletedTask;

        }

        #endregion

    }


    /// <summary>
    /// One explicit TCP write segment for <see cref="HTTPRawSocketClient.SendSegmentsAsync"/>.
    /// </summary>
    internal readonly record struct HTTPRawSocketSegment(ReadOnlyMemory<Byte> Data,
                                                         TimeSpan              DelayAfter)
    {

        public static HTTPRawSocketSegment Text(String Text,
                                                TimeSpan? DelayAfter = null)
            => new (
                   Encoding.Latin1.GetBytes(Text),
                   DelayAfter ?? TimeSpan.Zero
               );

    }


    /// <summary>
    /// A fully parsed HTTP response header plus its wire-framed body and trailers.
    /// </summary>
    internal sealed record HTTPRawSocketResponse(
        String                                              StatusLine,
        Int32                                               StatusCode,
        IReadOnlyDictionary<String, IReadOnlyList<String>> Headers,
        Byte[]                                              Body,
        IReadOnlyDictionary<String, IReadOnlyList<String>> Trailers)
    {

        public UInt64? ContentLength
            => Headers.TryGetValue("Content-Length", out var values) &&
               values.Count == 1 &&
               UInt64.TryParse(values[0], out var contentLength)
                   ? contentLength
                   : null;

        public Boolean IsChunked
            => Headers.TryGetValue("Transfer-Encoding", out var values) &&
               values.Any(value => value.Split(',').Any(
                                       transferCoding => transferCoding.Trim().Equals(
                                                             "chunked",
                                                             StringComparison.OrdinalIgnoreCase
                                                         )
                                   ));

        public static HTTPRawSocketResponse ParseHeader(String HeaderText)
        {

            var lines = HeaderText.Split("\r\n", StringSplitOptions.None);

            if (lines.Length == 0 || lines[0].Length == 0)
                throw new FormatException("The HTTP response does not contain a status line.");

            var statusParts = lines[0].Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (statusParts.Length < 2 || !Int32.TryParse(statusParts[1], out var statusCode))
                throw new FormatException($"Invalid HTTP response status line '{lines[0]}'.");

            var headers = new Dictionary<String, List<String>>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in lines.Skip(1))
            {

                if (line.Length > 0)
                    AddHeader(headers, line);

            }

            return new HTTPRawSocketResponse(
                       lines[0],
                       statusCode,
                       headers.ToDictionary(
                           pair => pair.Key,
                           pair => (IReadOnlyList<String>) pair.Value
                       ),
                       [],
                       new Dictionary<String, IReadOnlyList<String>>(StringComparer.OrdinalIgnoreCase)
                   );

        }

        internal static void AddHeader(Dictionary<String, List<String>> Headers,
                                       String                            HeaderLine)
        {

            var separatorIndex = HeaderLine.IndexOf(':');
            if (separatorIndex <= 0)
                throw new FormatException($"Invalid HTTP header line '{HeaderLine}'.");

            var name  = HeaderLine[..separatorIndex];
            var value = HeaderLine[(separatorIndex + 1)..].Trim();

            if (!Headers.TryGetValue(name, out var values))
            {
                values = [];
                Headers.Add(name, values);
            }

            values.Add(value);

        }

    }

}
