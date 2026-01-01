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

using org.GraphDefined.Vanaheimr.Illias;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A stream that en-/decodes HTTP Chunked Transfer Encoding.
    /// Assumes no trailer headers are processed (just skipped).
    /// </summary>
    public class ChunkedTransferEncodingStream(Stream   InnerStream,
                                               Boolean  LeaveInnerStreamOpen = false)

        : Stream,
          IPrefixInfo

    {

        #region Data

        private readonly        Stream   innerStream        = InnerStream;
        private                 Int64    currentChunkSize   = 0;
        private                 Int64    currentChunkRead   = 0;
        private                 Boolean  done               = false;
        private                 Boolean  hasStarted         = false;
        private readonly        Boolean  leaveInnerOpen     = LeaveInnerStreamOpen;
        private readonly static Byte[]   crlfBytes          = Encoding.ASCII.GetBytes("\r\n");

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

        #endregion


        #region (private) ReadLineAsync(CancellationToken)

        private async Task<String> ReadLineAsync(CancellationToken CancellationToken)
        {

            var stringBuilder  = new StringBuilder();
            var byteBuf        = new Byte[1];
            var sawCR          = false;

            while (true)
            {

                var r = await innerStream.ReadAsync(
                                  byteBuf,
                                  CancellationToken
                              );

                if (r == 0)
                    throw new EndOfStreamException("Unexpected end during line read.");

                var b = byteBuf[0];
                if (b == (Byte) '\r')
                {
                    sawCR = true;
                    continue;
                }

                if (sawCR && b == (Byte) '\n')
                    break;

                stringBuilder.Append((Char) b);

                sawCR = false;

            }

            return stringBuilder.ToString();

        }

        #endregion

        #region (private) ReadLine()

        private String ReadLine()
        {

            var stringBuilder  = new StringBuilder();
            var byteBuf        = new Byte[1];
            var sawCR          = false;

            while (true)
            {

                var r = innerStream.Read(byteBuf, 0, 1);
                if (r == 0)
                    throw new EndOfStreamException("Unexpected end during line read.");

                var b = byteBuf[0];
                if (b == (Byte) '\r')
                {
                    sawCR = true;
                    continue;
                }

                if (sawCR && b == (Byte) '\n')
                    break;

                stringBuilder.Append((Char)b);
                sawCR = false;

            }

            return stringBuilder.ToString();

        }

        #endregion

        #region (private) ReadCRLFAsync(CancellationToken)
        private async Task ReadCRLFAsync(CancellationToken CancellationToken)
        {

            var buf  = new Byte[2];
            var r    = await innerStream.ReadAsync(
                                 buf,
                                 CancellationToken
                             );

            if (r < 2 || buf[0] != (Byte) '\r' || buf[1] != (Byte) '\n')
                throw new Exception("Expected CRLF");

        }

        #endregion

        #region (private) ReadCRLF()

        private void ReadCRLF()
        {

            var buf  = new Byte[2];
            var r    = innerStream.Read(buf, 0, 2);

            if (r < 2 || buf[0] != (Byte) '\r' || buf[1] != (Byte) '\n')
                throw new Exception("Expected CRLF");

        }

        #endregion

        #region Read       (Buffer, Offset, Count)

        public override Int32 Read(Byte[]  Buffer,
                                   Int32   Offset,
                                   Int32   Count)
        {

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

                    var sizeLine      = ReadLine();
                    // Ignore extensions after ;
                    var sizeStr       = sizeLine.Split(';')[0].Trim();
                    currentChunkSize  = Int64.Parse(sizeStr, NumberStyles.HexNumber);
                    currentChunkRead  = 0;

                    if (currentChunkSize == 0)
                    {

                        ReadCRLF();

                        // Skip trailers
                        while (true)
                        {

                            var line = ReadLine();

                            if (String.IsNullOrWhiteSpace(line))
                                break;
                            // Ignore trailers for now

                        }

                        done = true;
                        return totalRead;

                    }

                }

                var canRead = (Int32) Math.Min(remainingCount, currentChunkSize - currentChunkRead);
                if (canRead > 0)
                {

                    var read = innerStream.Read(Buffer, currentOffset, canRead);
                    if (read == 0)
                        throw new Exception("Unexpected end of stream during chunk data");

                    currentChunkRead += read;
                    totalRead        += read;
                    currentOffset    += read;
                    remainingCount   -= read;

                }
                else
                {
                    // 0 canRead, but since if == size, loop will go to next chunk
                }

            }

            return totalRead;

        }

        #endregion


        #region ReadAsync  (Destination, CancellationToken = default)

        public override async ValueTask<Int32> ReadAsync(Memory<Byte>       Destination,
                                                         CancellationToken  CancellationToken   = default)
        {

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

                    // Ignore extensions after ;
                    var sizeLine      = await ReadLineAsync(CancellationToken);

                    var sizeStr       = sizeLine.Split(';')[0].Trim();
                    currentChunkSize  = Int64.Parse(sizeStr, NumberStyles.HexNumber);
                    currentChunkRead  = 0;

                    if (currentChunkSize == 0)
                    {

                        await ReadCRLFAsync(CancellationToken);

                        // Skip trailers
                        while (true)
                        {

                            var line = await ReadLineAsync(CancellationToken);

                            if (String.IsNullOrWhiteSpace(line))
                                break;

                            // Ignore trailers for now

                        }

                        done = true;
                        return totalRead;

                    }

                }

                var canRead = (Int32) Math.Min(currentDestination.Length, currentChunkSize - currentChunkRead);
                if (canRead > 0)
                {

                    var read = await innerStream.ReadAsync(currentDestination, CancellationToken);
                    if (read == 0)
                        throw new Exception("Unexpected end of stream during chunk data");

                    currentChunkRead += read;
                    totalRead += read;

                    var bytes = currentDestination.ToArray();
                    Array.Resize(ref bytes, read);
                    //OnChunkReceived?.Invoke(bytes);

                    currentDestination = currentDestination[read..];

                }
                else
                {
                    // 0 canRead, but since if == size, loop will go to next chunk
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

            if (done)
                return [];

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
            var       trailers     = new List<(String, String)>();
            var       counter      = 1UL;
            var       sw           = Stopwatch.StartNew();

            while (!CancellationToken.IsCancellationRequested && !done)
            {

                // Ignore extensions after ;
                var sizeLine          = await ReadLineAsync(CancellationToken);
                var sizeStr           = sizeLine.Split(';')[0].Trim();  // Ignore extensions
                var currentChunkSize  = Int64.Parse(sizeStr, NumberStyles.HexNumber);
                var currentChunkRead  = 0;

                if (currentChunkSize == 0)
                {

                    while (true)
                    {

                        var line = await ReadLineAsync(CancellationToken);

                        if (String.IsNullOrWhiteSpace(line))
                            break;

                        var parts = line.Split(':', 2);
                        trailers.Add((
                            parts[0].Trim(),
                            parts.Length > 1
                                ? parts[1].Trim()
                                : String.Empty
                        ));

                    }

                    done = true;
                    break;

                }

                using var ms = new MemoryStream((Int32) currentChunkSize);
                while (currentChunkRead < currentChunkSize)
                {
                    var remaining = (int) Math.Min(buffer.Length, currentChunkSize - currentChunkRead);
                    var bytesRead = await innerStream.ReadAsync(buffer[..remaining], CancellationToken);
                    if (bytesRead == 0)
                        throw new Exception("Unexpected end of stream during chunk data");

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

            return trailers;

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

        #region Finish (Trailers = null, CancellationToken = default)

        public async ValueTask Finish(Dictionary<String, String>?  Trailers            = null,
                                      CancellationToken            CancellationToken   = default)
        {

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
