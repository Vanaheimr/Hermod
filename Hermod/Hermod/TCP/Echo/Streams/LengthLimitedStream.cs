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
    /// A stream that limits the total bytes that can be read.
    /// </summary>
    public class LengthLimitedStream(Stream   InnerStream,
                                     UInt64   LengthLimit,
                                     Boolean  LeaveInnerStreamOpen   = false)

        : Stream,
          IPrefixInfo

    {

        #region Data

        private readonly Stream   innerStream            = InnerStream;
        private readonly Boolean  leaveInnerStreamOpen   = LeaveInnerStreamOpen;
        private          UInt64   lengthLimit            = LengthLimit;

        #endregion

        #region Properties

        public UInt64            InitialLengthLimit    { get; } = LengthLimit;


        public override Boolean  CanRead
            => true;

        public override Boolean  CanSeek
            => false;

        public override Boolean  CanWrite
            => false;

        public override Int64    Length
            => (Int64) lengthLimit;

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


        #region Read      (Buffer, Offset, Count)

        public override Int32 Read(Byte[]  Buffer,
                                   Int32   Offset,
                                   Int32   Count)
        {

            if (lengthLimit <= 0)
                return 0;

            var bytesRead  = innerStream.Read(
                                 Buffer,
                                 Offset,
                                 (Int32) Math.Min((UInt64) Count, lengthLimit)
                             );

            lengthLimit     -= (UInt64) bytesRead;

            return bytesRead;

        }

        #endregion

        #region ReadAsync (Buffer, CancellationToken)

        public override async ValueTask<Int32> ReadAsync(Memory<Byte>       Buffer,
                                                         CancellationToken  CancellationToken   = default)
        {

            if (lengthLimit <= 0)
                return 0;

            var bytesRead  = await innerStream.ReadAsync(
                                       Buffer[..(Int32) Math.Min((UInt64) Buffer.Length, lengthLimit)],
                                       CancellationToken
                                   );

            lengthLimit     -= (UInt64) bytesRead;

            return bytesRead;

        }

        #endregion


        #region Write     (Buffer, Offset, Count)

        public override void Write(Byte[]  Buffer,
                                   Int32   Offset,
                                   Int32   Count)

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

            => $"Length limit: {lengthLimit}, LeaveInnerStreamOpen: {leaveInnerStreamOpen}";

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
