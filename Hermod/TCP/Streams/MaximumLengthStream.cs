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

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A read-only stream that fails as soon as more than the configured
    /// maximum number of bytes is consumed.
    /// </summary>
    public sealed class MaximumLengthStream(Stream   InnerStream,
                                            UInt64   MaximumLength,
                                            Boolean  LeaveInnerStreamOpen = false)

        : Stream,
          IPrefixInfo

    {

        private readonly Stream  innerStream    = InnerStream;
        private readonly Boolean leaveInnerOpen = LeaveInnerStreamOpen;
        private          UInt64  bytesRead;
        private          HTTP.HTTPBodyTooLargeException? terminalReadException;

        public UInt64 MaximumBytes { get; } = MaximumLength;

        public UInt64 BytesRead
            => bytesRead;

        public UInt64 PrefixConsumed
            => innerStream is IPrefixInfo prefixInfo
                   ? prefixInfo.PrefixConsumed
                   : 0;

        public override Boolean CanRead  => true;
        public override Boolean CanSeek  => false;
        public override Boolean CanWrite => false;

        public override Int64 Length
            => throw new NotSupportedException();

        public override Int64 Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override Int32 Read(Byte[] Buffer, Int32 Offset, Int32 Count)
        {
            if (terminalReadException is not null)
                throw terminalReadException;

            if (Count == 0)
                return 0;

            if (bytesRead >= MaximumBytes)
            {
                var probe = new Byte[1];
                if (innerStream.Read(probe, 0, 1) > 0)
                    throw terminalReadException = new HTTP.HTTPBodyTooLargeException(bytesRead + 1, MaximumBytes);

                return 0;
            }

            var bytesToRead = (Int32) Math.Min((UInt64) Count, MaximumBytes - bytesRead + 1);
            var read = innerStream.Read(Buffer, Offset, bytesToRead);

            if ((UInt64) read > MaximumBytes - bytesRead)
                throw terminalReadException = new HTTP.HTTPBodyTooLargeException(bytesRead + (UInt64) read, MaximumBytes);

            bytesRead += (UInt64) read;
            return read;
        }

        public override async ValueTask<Int32> ReadAsync(Memory<Byte> Buffer,
                                                          CancellationToken CancellationToken = default)
        {
            if (terminalReadException is not null)
                throw terminalReadException;

            if (Buffer.Length == 0)
                return 0;

            if (bytesRead >= MaximumBytes)
            {
                var probe = new Byte[1];
                if (await innerStream.ReadAsync(probe.AsMemory(0, 1), CancellationToken).ConfigureAwait(false) > 0)
                    throw terminalReadException = new HTTP.HTTPBodyTooLargeException(bytesRead + 1, MaximumBytes);

                return 0;
            }

            var bytesToRead = (Int32) Math.Min((UInt64) Buffer.Length, MaximumBytes - bytesRead + 1);
            var read = await innerStream.ReadAsync(Buffer[..bytesToRead], CancellationToken).ConfigureAwait(false);

            if ((UInt64) read > MaximumBytes - bytesRead)
                throw terminalReadException = new HTTP.HTTPBodyTooLargeException(bytesRead + (UInt64) read, MaximumBytes);

            bytesRead += (UInt64) read;
            return read;
        }

        public override void Flush()
            => innerStream.Flush();

        public override Task FlushAsync(CancellationToken CancellationToken)
            => innerStream.FlushAsync(CancellationToken);

        public override void Write(Byte[] Buffer, Int32 Offset, Int32 Count)
            => throw new NotSupportedException();

        public override Int64 Seek(Int64 Offset, SeekOrigin Origin)
            => throw new NotSupportedException();

        public override void SetLength(Int64 Value)
            => throw new NotSupportedException();

        protected override void Dispose(Boolean Disposing)
        {
            if (Disposing && !leaveInnerOpen)
                innerStream.Dispose();

            base.Dispose(Disposing);
        }

    }

}
