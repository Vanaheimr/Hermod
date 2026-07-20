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

using System.Text;
using System.Buffers;
using System.Diagnostics;
using System.Globalization;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A stream that en-/decodes HTTP Chunked Transfer Encoding.
    /// Assumes no trailer headers are processed (just skipped).
    /// </summary>
    public class ChunkedTransferEncodingStream

        : Stream,
          IPrefixInfo

    {

        #region Data

        public const UInt32 DefaultMaxChunkSizeLineLength =  8 * 1024;
        public const UInt32 DefaultMaxTrailerLineLength   =  8 * 1024;
        public const UInt32 DefaultMaxTrailerCount        =      100;
        public const UInt32 DefaultMaxTrailerSize         = 32 * 1024;
        public const UInt32 DefaultMaxChunkMetadataSize   = 64 * 1024;

        private readonly        Stream   innerStream;
        private                 Int64    currentChunkSize   = 0;
        private                 Int64    currentChunkRead   = 0;
        private                 Boolean  done               = false;
        private                 Boolean  hasStarted         = false;
        private readonly        Boolean  leaveInnerOpen;
        private                 UInt64   metadataBytesRead;
        private                 UInt64   trailerBytesRead;
        private                 UInt32   trailerCount;
        private readonly        List<(String Name, String Value)> trailerHeaders = [];
        private readonly        List<IReadOnlyDictionary<String, IReadOnlyList<String>>> chunkExtensions = [];
        private                 Exception? terminalReadException;
        private readonly static Byte[]   crlfBytes          = Encoding.ASCII.GetBytes("\r\n");
        private readonly static HashSet<String> forbiddenTrailerFieldNames = new(StringComparer.OrdinalIgnoreCase) {
            "Authorization",
            "Cache-Control",
            "Content-Encoding",
            "Content-Length",
            "Content-Range",
            "Content-Type",
            "Host",
            "Max-Forwards",
            "TE",
            "Trailer",
            "Transfer-Encoding"
        };

        #endregion

        #region Constructor(s)

        public ChunkedTransferEncodingStream(Stream   InnerStream,
                                             Boolean  LeaveInnerStreamOpen    = false,
                                             UInt32   MaxChunkSizeLineLength  = DefaultMaxChunkSizeLineLength,
                                             UInt32   MaxTrailerLineLength    = DefaultMaxTrailerLineLength,
                                             UInt32   MaxTrailerCount         = DefaultMaxTrailerCount,
                                             UInt32   MaxTrailerSize          = DefaultMaxTrailerSize,
                                             UInt32   MaxChunkMetadataSize    = DefaultMaxChunkMetadataSize)
        {

            ArgumentNullException.ThrowIfNull(InnerStream);

            if (MaxChunkSizeLineLength == 0)
                throw new ArgumentOutOfRangeException(nameof(MaxChunkSizeLineLength));

            if (MaxTrailerLineLength == 0)
                throw new ArgumentOutOfRangeException(nameof(MaxTrailerLineLength));

            if (MaxTrailerCount == 0)
                throw new ArgumentOutOfRangeException(nameof(MaxTrailerCount));

            if (MaxTrailerSize == 0)
                throw new ArgumentOutOfRangeException(nameof(MaxTrailerSize));

            if (MaxChunkMetadataSize == 0)
                throw new ArgumentOutOfRangeException(nameof(MaxChunkMetadataSize));

            innerStream                 = InnerStream;
            leaveInnerOpen              = LeaveInnerStreamOpen;
            this.MaxChunkSizeLineLength = MaxChunkSizeLineLength;
            this.MaxTrailerLineLength   = MaxTrailerLineLength;
            this.MaxTrailerCount        = MaxTrailerCount;
            this.MaxTrailerSize         = MaxTrailerSize;
            this.MaxChunkMetadataSize   = MaxChunkMetadataSize;

        }

        #endregion

        #region Properties

        public override Boolean  CanRead
            => true;

        public override Boolean  CanSeek
            => false;

        public override Boolean  CanWrite
            => true;

        public override Int64    Length
            => throw new NotSupportedException();

        public override Int64    Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public UInt64 PrefixConsumed

            => innerStream is IPrefixInfo pi
                   ? pi.PrefixConsumed
                   : 0;

        public UInt32 MaxChunkSizeLineLength { get; }
        public UInt32 MaxTrailerLineLength   { get; }
        public UInt32 MaxTrailerCount        { get; }
        public UInt32 MaxTrailerSize         { get; }
        public UInt32 MaxChunkMetadataSize   { get; }

        /// <summary>
        /// The trailer header fields received after the terminal chunk.
        /// </summary>
        public IReadOnlyList<(String Name, String Value)> TrailerHeaders
            => trailerHeaders;

        /// <summary>
        /// The chunk extensions received for each chunk-size line, in wire order.
        /// </summary>
        public IReadOnlyList<IReadOnlyDictionary<String, IReadOnlyList<String>>> ChunkExtensions
            => chunkExtensions;

        #endregion


        #region Metadata limits

        private void ThrowIfFaulted()
        {
            if (terminalReadException is not null)
                throw terminalReadException;
        }

        private void ThrowMetadataLimitExceeded(String Component,
                                                UInt64 Actual,
                                                UInt64 Maximum)
        {
            terminalReadException ??= new HTTPChunkMetadataTooLargeException(
                                          Component,
                                          Actual,
                                          Maximum
                                      );

            throw terminalReadException;
        }

        private void AccountMetadataByte(Boolean IsTrailer)
        {
            metadataBytesRead++;
            if (metadataBytesRead > MaxChunkMetadataSize)
            {
                ThrowMetadataLimitExceeded(
                    "metadata section",
                    metadataBytesRead,
                    MaxChunkMetadataSize
                );
            }

            if (IsTrailer)
            {
                trailerBytesRead++;
                if (trailerBytesRead > MaxTrailerSize)
                {
                    ThrowMetadataLimitExceeded(
                        "trailer section",
                        trailerBytesRead,
                        MaxTrailerSize
                    );
                }
            }
        }

        private void AccountTrailer()
        {
            trailerCount++;
            if (trailerCount > MaxTrailerCount)
            {
                ThrowMetadataLimitExceeded(
                    "trailer count",
                    trailerCount,
                    MaxTrailerCount
                );
            }
        }

        private static Int64 ParseChunkSize(String                                                         SizeLine,
                                            out IReadOnlyDictionary<String, IReadOnlyList<String>>?  Extensions)
        {
            var extensionStart = SizeLine.IndexOf(';');
            var sizeText       = extensionStart >= 0
                                     ? SizeLine[..extensionStart]
                                     : SizeLine;

            if (sizeText.Length == 0 ||
                sizeText.Any(character => !IsHexDigit(character)) ||
                !UInt64.TryParse(sizeText, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var size) ||
                size > Int64.MaxValue)
                throw new HTTPInvalidChunkException("Invalid HTTP chunk-size line.");

            if (extensionStart >= 0 && !IsValidChunkExtensions(SizeLine.AsSpan(extensionStart)))
                throw new HTTPInvalidChunkException("Invalid HTTP chunk extension.");

            Extensions = extensionStart >= 0
                             ? ParseChunkExtensions(SizeLine.AsSpan(extensionStart))
                             : null;

            return (Int64) size;
        }

        private static IReadOnlyDictionary<String, IReadOnlyList<String>> ParseChunkExtensions(ReadOnlySpan<Char> Extensions)
        {

            var parsedExtensions = new Dictionary<String, List<String>>(StringComparer.Ordinal);
            var index            = 0;

            while (index < Extensions.Length)
            {
                index++;
                while (index < Extensions.Length && IsBWS(Extensions[index])) index++;

                var nameStart = index;
                while (index < Extensions.Length && IsTokenCharacter(Extensions[index])) index++;
                var name = Extensions[nameStart..index].ToString();

                while (index < Extensions.Length && IsBWS(Extensions[index])) index++;

                var value = String.Empty;
                if (index < Extensions.Length && Extensions[index] == '=')
                {
                    index++;
                    while (index < Extensions.Length && IsBWS(Extensions[index])) index++;

                    if (Extensions[index] == '"')
                    {
                        index++;
                        var valueBuilder = new StringBuilder();
                        while (Extensions[index] != '"')
                        {
                            if (Extensions[index] == '\\') index++;
                            valueBuilder.Append(Extensions[index++]);
                        }
                        index++;
                        value = valueBuilder.ToString();
                    }
                    else
                    {
                        var valueStart = index;
                        while (index < Extensions.Length && IsTokenCharacter(Extensions[index])) index++;
                        value = Extensions[valueStart..index].ToString();
                    }

                    while (index < Extensions.Length && IsBWS(Extensions[index])) index++;
                }

                if (!parsedExtensions.TryGetValue(name, out var values))
                    parsedExtensions[name] = values = [];

                values.Add(value);
            }

            return parsedExtensions.ToDictionary(
                       extension => extension.Key,
                       extension => (IReadOnlyList<String>) extension.Value.AsReadOnly(),
                       StringComparer.Ordinal
                   );

        }

        private static Boolean IsHexDigit(Char Character)
            => Character is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f';

        private static Boolean IsTokenCharacter(Char Character)
            => Character is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z' or
               '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.' or
               '^' or '_' or '`' or '|' or '~';

        private static Boolean IsBWS(Char Character)
            => Character is ' ' or '\t';

        private static Boolean IsValidChunkExtensions(ReadOnlySpan<Char> Extensions)
        {

            var index = 0;

            while (index < Extensions.Length)
            {

                if (Extensions[index++] != ';')
                    return false;

                while (index < Extensions.Length && IsBWS(Extensions[index]))
                    index++;

                var nameStart = index;
                while (index < Extensions.Length && IsTokenCharacter(Extensions[index]))
                    index++;

                if (index == nameStart)
                    return false;

                while (index < Extensions.Length && IsBWS(Extensions[index]))
                    index++;

                if (index < Extensions.Length && Extensions[index] == '=')
                {

                    index++;

                    while (index < Extensions.Length && IsBWS(Extensions[index]))
                        index++;

                    if (index >= Extensions.Length)
                        return false;

                    if (Extensions[index] == '"')
                    {

                        index++;
                        var closed = false;

                        while (index < Extensions.Length)
                        {

                            var character = Extensions[index++];

                            if (character == '\\')
                            {
                                if (index >= Extensions.Length || Extensions[index] is '\r' or '\n' or < ' ')
                                    return false;

                                index++;
                                continue;
                            }

                            if (character == '"')
                            {
                                closed = true;
                                break;
                            }

                            if (character is '\r' or '\n' or < ' ')
                                return false;

                        }

                        if (!closed)
                            return false;

                    }
                    else
                    {

                        var valueStart = index;
                        while (index < Extensions.Length && IsTokenCharacter(Extensions[index]))
                            index++;

                        if (index == valueStart)
                            return false;

                    }

                    while (index < Extensions.Length && IsBWS(Extensions[index]))
                        index++;

                }

                if (index < Extensions.Length && Extensions[index] != ';')
                    return false;

            }

            return true;

        }

        #endregion


        #region (private) ReadLineAsync(MaximumLineLength, IsTrailer, CancellationToken)

        private async Task<String> ReadLineAsync(UInt32             MaximumLineLength,
                                                 Boolean            IsTrailer,
                                                 CancellationToken  CancellationToken)
        {

            var stringBuilder = new StringBuilder((Int32) Math.Min(MaximumLineLength, 256));
            var byteBuffer    = new Byte[1];

            while (true)
            {

                var bytesRead = await innerStream.ReadAsync(
                                          byteBuffer,
                                          CancellationToken
                                      ).ConfigureAwait(false);

                if (bytesRead == 0)
                    throw new HTTPInvalidChunkException("Unexpected end during HTTP chunk metadata read.");

                AccountMetadataByte(IsTrailer);

                var currentByte = byteBuffer[0];
                if (currentByte == (Byte) '\r')
                {
                    bytesRead = await innerStream.ReadAsync(
                                          byteBuffer,
                                          CancellationToken
                                      ).ConfigureAwait(false);

                    if (bytesRead == 0)
                        throw new HTTPInvalidChunkException("Unexpected end during HTTP chunk metadata read.");

                    AccountMetadataByte(IsTrailer);

                    if (byteBuffer[0] != (Byte) '\n')
                        throw new FormatException("HTTP chunk metadata lines must end with CRLF.");

                    return stringBuilder.ToString();
                }

                if (currentByte == (Byte) '\n')
                    throw new FormatException("HTTP chunk metadata lines must end with CRLF.");

                if ((UInt32) stringBuilder.Length >= MaximumLineLength)
                {
                    ThrowMetadataLimitExceeded(
                        IsTrailer ? "trailer line" : "chunk-size line",
                        (UInt64) stringBuilder.Length + 1,
                        MaximumLineLength
                    );
                }

                stringBuilder.Append((Char) currentByte);

            }

        }

        #endregion

        #region (private) ReadLine(MaximumLineLength, IsTrailer)

        private String ReadLine(UInt32   MaximumLineLength,
                                 Boolean  IsTrailer)
        {

            var stringBuilder = new StringBuilder((Int32) Math.Min(MaximumLineLength, 256));
            var byteBuffer    = new Byte[1];

            while (true)
            {

                var bytesRead = innerStream.Read(byteBuffer, 0, 1);
                if (bytesRead == 0)
                    throw new HTTPInvalidChunkException("Unexpected end during HTTP chunk metadata read.");

                AccountMetadataByte(IsTrailer);

                var currentByte = byteBuffer[0];
                if (currentByte == (Byte) '\r')
                {
                    bytesRead = innerStream.Read(byteBuffer, 0, 1);
                    if (bytesRead == 0)
                        throw new HTTPInvalidChunkException("Unexpected end during HTTP chunk metadata read.");

                    AccountMetadataByte(IsTrailer);

                    if (byteBuffer[0] != (Byte) '\n')
                        throw new FormatException("HTTP chunk metadata lines must end with CRLF.");

                    return stringBuilder.ToString();
                }

                if (currentByte == (Byte) '\n')
                    throw new FormatException("HTTP chunk metadata lines must end with CRLF.");

                if ((UInt32) stringBuilder.Length >= MaximumLineLength)
                {
                    ThrowMetadataLimitExceeded(
                        IsTrailer ? "trailer line" : "chunk-size line",
                        (UInt64) stringBuilder.Length + 1,
                        MaximumLineLength
                    );
                }

                stringBuilder.Append((Char) currentByte);

            }

        }

        #endregion


        #region (private) StoreTrailerHeader(Line)

        private void StoreTrailerHeader(String Line)
        {

            var separatorIndex = Line.IndexOf(':');

            if (separatorIndex <= 0 ||
                !Line[..separatorIndex].All(IsTokenCharacter))
            {
                terminalReadException ??= new HTTPInvalidChunkException("Invalid HTTP trailer header field.");
                throw terminalReadException;
            }

            var fieldName = Line[..separatorIndex];

            if (forbiddenTrailerFieldNames.Contains(fieldName))
            {
                terminalReadException ??= new HTTPInvalidChunkException($"HTTP trailer header field '{fieldName}' is forbidden.");
                throw terminalReadException;
            }

            trailerHeaders.Add((
                fieldName,
                Line[(separatorIndex + 1)..].Trim()
            ));

        }

        #endregion

        #region (private) ReadTrailerHeaders()

        private void ReadTrailerHeaders()
        {

            while (true)
            {

                var line = ReadLine(MaxTrailerLineLength, true);

                if (line.Length == 0)
                    break;

                AccountTrailer();
                StoreTrailerHeader(line);

            }

        }

        #endregion

        #region (private) ReadTrailerHeadersAsync(CancellationToken)

        private async Task ReadTrailerHeadersAsync(CancellationToken CancellationToken)
        {

            while (true)
            {

                var line = await ReadLineAsync(
                                     MaxTrailerLineLength,
                                     true,
                                     CancellationToken
                                 ).ConfigureAwait(false);

                if (line.Length == 0)
                    break;

                AccountTrailer();
                StoreTrailerHeader(line);

            }

        }

        #endregion

        #region (private) ReadCRLFAsync(CancellationToken)
        private async Task ReadCRLFAsync(CancellationToken CancellationToken)
        {

            var buf  = new Byte[2];
            var r    = 0;

            while (r < buf.Length)
            {

                var bytesRead = await innerStream.ReadAsync(
                                          buf.AsMemory(r, buf.Length - r),
                                          CancellationToken
                                      ).ConfigureAwait(false);

                if (bytesRead == 0)
                    break;

                r += bytesRead;

            }

            if (r < 2 || buf[0] != (Byte) '\r' || buf[1] != (Byte) '\n')
                throw new HTTPInvalidChunkException("Expected CRLF after HTTP chunk data.");

        }

        #endregion

        #region (private) ReadCRLF()

        private void ReadCRLF()
        {

            var buf  = new Byte[2];
            var r    = 0;

            while (r < buf.Length)
            {

                var bytesRead = innerStream.Read(buf, r, buf.Length - r);
                if (bytesRead == 0)
                    break;

                r += bytesRead;

            }

            if (r < 2 || buf[0] != (Byte) '\r' || buf[1] != (Byte) '\n')
                throw new Exception("Expected CRLF");

        }

        #endregion

        #region Read       (Buffer, Offset, Count)

        public override Int32 Read(Byte[]  Buffer,
                                   Int32   Offset,
                                   Int32   Count)
        {

            ThrowIfFaulted();

            if (done)
                return 0;

            var totalRead       = 0;
            var currentOffset   = Offset;
            var remainingCount  = Count;

            while (remainingCount > 0 && !done)
            {
                if (currentChunkRead == currentChunkSize)
                {

                    if (hasStarted)
                        ReadCRLF();

                    hasStarted = true;

                    var sizeLine      = ReadLine(MaxChunkSizeLineLength, false);
                    currentChunkSize  = ParseChunkSize(sizeLine, out var extensions);
                    if (extensions is not null)
                        chunkExtensions.Add(extensions);
                    currentChunkRead  = 0;

                    if (currentChunkSize == 0)
                    {

                        ReadTrailerHeaders();

                        done = true;
                        return totalRead;

                    }

                }

                var canRead = (Int32) Math.Min(remainingCount, currentChunkSize - currentChunkRead);
                if (canRead > 0)
                {

                    var read = innerStream.Read(Buffer, currentOffset, canRead);
                    if (read == 0)
                        throw new HTTPInvalidChunkException("Unexpected end during HTTP chunk data read.");

                    currentChunkRead += read;
                    totalRead        += read;
                    currentOffset    += read;
                    remainingCount   -= read;

                }
                else
                {
                    throw new InvalidOperationException("Invalid chunk state: no bytes can be read, but the current chunk is not complete.");
                }

            }

            return totalRead;

        }

        #endregion


        #region ReadAsync  (Destination, CancellationToken = default)

        public override async ValueTask<Int32> ReadAsync(Memory<Byte>       Destination,
                                                         CancellationToken  CancellationToken   = default)
        {

            ThrowIfFaulted();

            if (done)
                return 0;

            var          totalRead           = 0;
            Memory<Byte> currentDestination  = Destination;

            while (currentDestination.Length > 0 && !done)
            {
                if (currentChunkRead == currentChunkSize)
                {

                    if (hasStarted)
                        await ReadCRLFAsync(CancellationToken);

                    hasStarted = true;

                    var sizeLine      = await ReadLineAsync(
                                                   MaxChunkSizeLineLength,
                                                   false,
                                                   CancellationToken
                                               );
                    currentChunkSize  = ParseChunkSize(sizeLine, out var extensions);
                    if (extensions is not null)
                        chunkExtensions.Add(extensions);
                    currentChunkRead  = 0;

                    if (currentChunkSize == 0)
                    {

                        await ReadTrailerHeadersAsync(CancellationToken).ConfigureAwait(false);

                        done = true;
                        return totalRead;

                    }

                }

                var canRead = (Int32) Math.Min(currentDestination.Length, currentChunkSize - currentChunkRead);
                if (canRead > 0)
                {

                    var read = await innerStream.ReadAsync(
                                           currentDestination[..canRead],
                                           CancellationToken
                                       ).ConfigureAwait(false);

                    if (read == 0)
                        throw new HTTPInvalidChunkException("Unexpected end during HTTP chunk data read.");

                    currentChunkRead += read;
                    totalRead += read;

                    var bytes = currentDestination.ToArray();
                    Array.Resize(ref bytes, read);
                    //OnChunkReceived?.Invoke(bytes);

                    currentDestination = currentDestination[read..];

                }
                else
                {
                    throw new InvalidOperationException("Invalid chunk state: no bytes can be read, but the current chunk is not complete.");
                }

            }

            return totalRead;

        }

        #endregion



        #region ReadChunks  (OnChunkReceived, CancellationToken = default)

        public async Task<IEnumerable<(String, String)>>

            ReadAllChunks(Action<DateTimeOffset, TimeSpan, UInt64,  Byte[]>  OnChunkReceived,
                          CancellationToken                                  CancellationToken   = default)

        {

            ThrowIfFaulted();

            if (done)
                return TrailerHeaders;

            // 5\r\n
            // Hello\r\n
            // 5\r\n
            // World\r\n
            // 0\r\n
            // Expires: Wed, 21 Oct 2025 07:28:00 GMT\r\n
            // ETag: "abc123"\r\n
            // \r\n

            Int32 bufferSize       = 8192;
            using var bufferOwner  = MemoryPool<Byte>.Shared.Rent(bufferSize * 2);
            var       buffer       = bufferOwner.Memory;
            var       dataLength   = 0;
            var       counter      = 1UL;
            var       sw           = Stopwatch.StartNew();

            while (!CancellationToken.IsCancellationRequested && !done)
            {

                var sizeLine          = await ReadLineAsync(
                                                  MaxChunkSizeLineLength,
                                                  false,
                                                  CancellationToken
                                              );
                var currentChunkSize  = ParseChunkSize(sizeLine, out var extensions);
                if (extensions is not null)
                    chunkExtensions.Add(extensions);
                var currentChunkRead  = 0;

                if (currentChunkSize == 0)
                {

                    await ReadTrailerHeadersAsync(CancellationToken).ConfigureAwait(false);

                    done = true;
                    break;

                }

                using var ms = new MemoryStream((Int32) currentChunkSize);
                while (currentChunkRead < currentChunkSize)
                {
                    var remaining = (int) Math.Min(buffer.Length, currentChunkSize - currentChunkRead);
                    var bytesRead = await innerStream.ReadAsync(buffer[..remaining], CancellationToken);
                    if (bytesRead == 0)
                        throw new HTTPInvalidChunkException("Unexpected end during HTTP chunk data read.");

                    await ms.WriteAsync(buffer[..bytesRead], CancellationToken);
                    currentChunkRead += bytesRead;
                }

                var chunkData = ms.ToArray();

                OnChunkReceived?.Invoke(
                    Timestamp.Now,
                    sw.Elapsed,
                    counter++,
                    chunkData
                );

                // Read \r\n after each chunk...
                await ReadCRLFAsync(CancellationToken);

            }

            sw.Stop();

            return TrailerHeaders;

        }

        #endregion




        #region Write      (Buffer, Offset, Count)

        public override void Write(Byte[]  Buffer,
                                   Int32   Offset,
                                   Int32   Count)
        {

            if (Count == 0)
                return;

            var headerBytes = Encoding.ASCII.GetBytes($"{Count:X}\r\n");

            innerStream.Write(headerBytes,      0, headerBytes.Length);
            innerStream.Write(Buffer,      Offset, Count);
            innerStream.Write(crlfBytes,        0, crlfBytes.  Length);

        }

        #endregion

        #region Write      (Buffer, Offset, Count, ChunkExtensions)

        /// <summary>
        /// Write one chunk with optional chunk extensions. A null extension value writes
        /// a flag extension; non-token values are emitted as quoted strings.
        /// </summary>
        public void Write(Byte[]                                            Buffer,
                          Int32                                             Offset,
                          Int32                                             Count,
                          IEnumerable<KeyValuePair<String, String?>>?       ChunkExtensions)
        {

            if (Count == 0)
                return;

            var headerBytes = Encoding.ASCII.GetBytes(
                                  $"{Count:X}{FormatChunkExtensions(ChunkExtensions)}\r\n"
                              );

            innerStream.Write(headerBytes,      0, headerBytes.Length);
            innerStream.Write(Buffer,      Offset, Count);
            innerStream.Write(crlfBytes,        0, crlfBytes.  Length);

        }

        #endregion


        #region WriteAsync (Buffer, CancellationToken = default)

        public override async ValueTask WriteAsync(ReadOnlyMemory<Byte>  Buffer,
                                                   CancellationToken     CancellationToken   = default)
        {

            if (Buffer.Length == 0)
                return;

            var headerBytes = Encoding.ASCII.GetBytes($"{Buffer.Length:X}\r\n");

            await innerStream.WriteAsync(headerBytes, CancellationToken);
            await innerStream.WriteAsync(Buffer,      CancellationToken);
            await innerStream.WriteAsync(crlfBytes,   CancellationToken);

        }

        #endregion

        #region WriteAsync (Buffer, ChunkExtensions, CancellationToken = default)

        /// <summary>
        /// Write one chunk with optional chunk extensions. A null extension value writes
        /// a flag extension; non-token values are emitted as quoted strings.
        /// </summary>
        public async ValueTask WriteAsync(ReadOnlyMemory<Byte>                         Buffer,
                                          IEnumerable<KeyValuePair<String, String?>>?  ChunkExtensions,
                                          CancellationToken                            CancellationToken   = default)
        {

            if (Buffer.Length == 0)
                return;

            var headerBytes = Encoding.ASCII.GetBytes(
                                  $"{Buffer.Length:X}{FormatChunkExtensions(ChunkExtensions)}\r\n"
                              );

            await innerStream.WriteAsync(headerBytes, CancellationToken);
            await innerStream.WriteAsync(Buffer,      CancellationToken);
            await innerStream.WriteAsync(crlfBytes,   CancellationToken);

        }

        #endregion


        #region (private) FormatChunkExtensions(ChunkExtensions)

        private static String FormatChunkExtensions(IEnumerable<KeyValuePair<String, String?>>? ChunkExtensions)
        {

            if (ChunkExtensions is null)
                return String.Empty;

            var text = new StringBuilder();

            foreach (var (name, value) in ChunkExtensions)
            {

                if (name.IsNullOrEmpty() ||
                    !name.All(IsTokenCharacter))
                {
                    throw new ArgumentException("Chunk extension names must be HTTP tokens.", nameof(ChunkExtensions));
                }

                text.Append(';');
                text.Append(name);

                if (value is null)
                    continue;

                text.Append('=');

                if (value.Length > 0 &&
                    value.All(IsTokenCharacter))
                {
                    text.Append(value);
                }

                else
                {

                    if (value.Any(character => character is < ' ' or > '~'))
                        throw new ArgumentException("Chunk extension values must not contain control or non-ASCII characters.", nameof(ChunkExtensions));

                    text.Append('"');
                    text.Append(value.Replace("\\", "\\\\").Replace("\"", "\\\""));
                    text.Append('"');

                }

            }

            return text.ToString();

        }

        #endregion


        #region ValidateOutgoingTrailerHeaders(Trailers)

        /// <summary>
        /// Validate trailer header fields before they are written to the wire.
        /// </summary>
        public static void ValidateOutgoingTrailerHeaders(IEnumerable<KeyValuePair<String, String>> Trailers)
        {

            ArgumentNullException.ThrowIfNull(Trailers);

            foreach (var (name, value) in Trailers)
            {

                if (name.IsNullOrEmpty() ||
                    !name.All(IsTokenCharacter))
                {
                    throw new ArgumentException("HTTP trailer field names must be HTTP tokens.", nameof(Trailers));
                }

                if (forbiddenTrailerFieldNames.Contains(name))
                    throw new ArgumentException($"HTTP trailer header field '{name}' is forbidden.", nameof(Trailers));

                if (value is null ||
                    value.Any(character => character is '\r' or '\n' or < ' ' or > '~'))
                {
                    throw new ArgumentException("HTTP trailer field values must not contain control or non-ASCII characters.", nameof(Trailers));
                }

            }

        }

        #endregion


        #region Finish (Trailers = null, CancellationToken = default)

        public async ValueTask Finish(Dictionary<String, String>?  Trailers            = null,
                                      CancellationToken            CancellationToken   = default)
        {

            if (Trailers is not null)
                ValidateOutgoingTrailerHeaders(Trailers);

            await innerStream.WriteAsync(Encoding.ASCII.GetBytes($"0\r\n"), CancellationToken);

            foreach (var trailer in Trailers ?? [])
            {
                await innerStream.WriteAsync(
                          Encoding.ASCII.GetBytes($"{trailer.Key}: {trailer.Value}\r\n"),
                          CancellationToken
                      );
            }

            await innerStream.WriteAsync(crlfBytes, CancellationToken);

        }

        #endregion


        #region Seek       (Offset, Origin)

        public override Int64 Seek(Int64 Offset, SeekOrigin Origin)
            => throw new NotSupportedException();

        #endregion

        #region SetLength  (Value)

        public override void SetLength(Int64 Value)
            => throw new NotSupportedException();

        #endregion

        #region Flush()

        public override Task FlushAsync(CancellationToken CancellationToken)
        {
            return base.FlushAsync(CancellationToken);
        }

        public override void Flush()
        {
            innerStream.Flush();
        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"LeaveInnerStreamOpen: {leaveInnerOpen}, HasStarted: {hasStarted}, Done: {done}";

        #endregion

        #region Dispose(Disposing)

        protected override void Dispose(Boolean Disposing)
        {

            // Write the final chunk if in write mode(but since it's duplex, assume if written, finalize)
            // But to avoid always writing, perhaps add a flag if any write occurred.
            // For simplicity, skip auto-finalize, user should call a Finalize method or something.
            // Or assume it's for reading, as original.
            // Since user added write, perhaps user handles final 0.
            if (Disposing && !leaveInnerOpen)
                innerStream.Dispose();

            base.Dispose(Disposing);

        }

        #endregion

    }

}
