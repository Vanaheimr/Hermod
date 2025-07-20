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

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A stream that first reads from a prefix memory, then from an inner stream.
    /// </summary>
    public class PrefixStream(ReadOnlyMemory<Byte>  Prefix,
                              Stream                InnerStream,
                              Boolean               LeaveInnerStreamOpen = false)

        : Stream,
          IPrefixInfo

    {

        #region Data

        private readonly ReadOnlyMemory<Byte>  prefix                 = Prefix;
        private          Int32                 prefixPosition         = 0;
        private readonly Stream                innerStream            = InnerStream;
        private readonly Boolean               leaveInnerStreamOpen   = LeaveInnerStreamOpen;

        #endregion

        #region Properties

        public override Boolean  CanRead
            => true;

        public override Boolean  CanSeek
            => false;

        public override Boolean  CanWrite
            => false;

        public override Int64    Length
            => throw new NotSupportedException();

        public override Int64    Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public UInt64 PrefixConsumed

            => (UInt64) prefixPosition;

        #endregion


        #region Read      (Buffer, Offset, Count)

        public override Int32 Read(Byte[]  Buffer,
                                   Int32   Offset,
                                   Int32   Count)
        {

            var bytesRead = 0;

            if (prefixPosition < prefix.Length)
            {

                var toCopy = Math.Min(Count, prefix.Length - prefixPosition);
                prefix.Slice(prefixPosition, toCopy).Span.CopyTo(Buffer.AsSpan(Offset, toCopy));
                prefixPosition += toCopy;
                bytesRead      += toCopy;

                if (toCopy == Count)
                    return bytesRead;

                Offset += toCopy;
                Count  -= toCopy;

            }

            var innerRead = innerStream.Read(Buffer, Offset, Count);
            bytesRead += innerRead;

            return bytesRead;

        }

        #endregion

        #region ReadAsync (Buffer, CancellationToken)

        public override async ValueTask<Int32> ReadAsync(Memory<Byte>       Buffer,
                                                         CancellationToken  CancellationToken   = default)
        {

            var bytesRead = 0;

            if (prefixPosition < prefix.Length)
            {

                var toCopy = Math.Min(Buffer.Length, prefix.Length - prefixPosition);
                prefix.Slice(prefixPosition, toCopy).CopyTo(Buffer);
                prefixPosition += toCopy;
                bytesRead      += toCopy;

                if (toCopy == Buffer.Length)
                    return bytesRead;

                Buffer = Buffer[toCopy..];

            }

            var innerRead = await innerStream.ReadAsync(Buffer, CancellationToken);
            bytesRead += innerRead;

            return bytesRead;

        }

        #endregion


        #region Write     (Buffer, Offset, Count)

        public override void Write(Byte[] Buffer,
                                   Int32 Offset,
                                   Int32 Count)

            => throw new NotSupportedException();

        #endregion


        #region Seek      (Offset, Origin)

        public override Int64 Seek(Int64 Offset, SeekOrigin Origin)
            => throw new NotSupportedException();

        #endregion

        #region SetLength (Value)

        public override void SetLength(Int64 Value)
            => throw new NotSupportedException();

        #endregion

        #region Flush()

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

            => $"LeaveInnerStreamOpen: {leaveInnerStreamOpen}";

        #endregion

        #region Dispose(Disposing)

        protected override void Dispose(Boolean Disposing)
        {

            // Write the final chunk if in write mode(but since it's duplex, assume if written, finalize)
            // But to avoid always writing, perhaps add a flag if any write occurred.
            // For simplicity, skip auto-finalize, user should call a Finalize method or something.
            // Or assume it's for reading, as original.
            // Since user added write, perhaps user handles final 0.
            if (Disposing && !leaveInnerStreamOpen)
                innerStream.Dispose();

            base.Dispose(Disposing);

        }

        #endregion

    }

}
