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
    /// Applies one shared deadline to asynchronous reads from an inner stream.
    /// </summary>
    public sealed class DeadlineStream(Stream             InnerStream,
                                       CancellationToken  DeadlineToken,
                                       TimeSpan           Timeout,
                                       Boolean            LeaveInnerStreamOpen = false)

        : Stream,
          IPrefixInfo

    {

        private readonly Stream  innerStream    = InnerStream;
        private readonly Boolean leaveInnerOpen = LeaveInnerStreamOpen;

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
            => innerStream.Read(Buffer, Offset, Count);

        public override async ValueTask<Int32> ReadAsync(Memory<Byte> Buffer,
                                                          CancellationToken CancellationToken = default)
        {
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                                               DeadlineToken,
                                               CancellationToken
                                           );

            try
            {
                return await innerStream.ReadAsync(
                                   Buffer,
                                   linkedCancellation.Token
                               ).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (DeadlineToken.IsCancellationRequested &&
                                                     !CancellationToken.IsCancellationRequested)
            {
                throw new HTTP.HTTPReadTimeoutException(Timeout);
            }
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
