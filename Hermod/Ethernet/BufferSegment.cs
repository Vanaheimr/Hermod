/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using System.Buffers;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Ethernet
{

    /// <summary>
    /// A single link of a <see cref="ReadOnlySequence{T}"/> of bytes.
    ///
    /// The BCL declares <see cref="ReadOnlySequenceSegment{T}"/> as abstract, but does not
    /// ship a public implementation of it, so everyone assembling a multi-segment sequence
    /// has to provide one. This is ours: it lets a protocol header be prepended to a payload
    /// without concatenating the two into a fresh buffer.
    /// </summary>
    public sealed class BufferSegment : ReadOnlySequenceSegment<Byte>
    {

        #region Constructor(s)

        /// <summary>
        /// Create a new buffer segment.
        /// </summary>
        /// <param name="Memory">The memory of this segment.</param>
        /// <param name="RunningIndex">The sum of the lengths of all preceding segments.</param>
        private BufferSegment(ReadOnlyMemory<Byte>  Memory,
                              Int64                 RunningIndex)
        {
            this.Memory        = Memory;
            this.RunningIndex  = RunningIndex;
        }

        #endregion


        #region Append   (Memory)

        /// <summary>
        /// Append the given memory as the next segment and return it,
        /// so that further segments can be appended to it.
        /// </summary>
        /// <param name="Memory">The memory of the next segment.</param>
        public BufferSegment Append(ReadOnlyMemory<Byte> Memory)
        {

            var next = new BufferSegment(
                           Memory,
                           RunningIndex + this.Memory.Length
                       );

            Next = next;

            return next;

        }

        #endregion

        #region Sequence (Segments)

        /// <summary>
        /// Create a read-only sequence over the given segments, without copying any of them.
        /// Empty segments are skipped, and a single remaining segment does not need a chain
        /// at all.
        /// </summary>
        /// <param name="Segments">The segments, in order.</param>
        public static ReadOnlySequence<Byte> Sequence(params ReadOnlyMemory<Byte>[] Segments)
        {

            var segments = Segments.Where(segment => !segment.IsEmpty).ToArray();

            if (segments.Length == 0)
                return ReadOnlySequence<Byte>.Empty;

            if (segments.Length == 1)
                return new ReadOnlySequence<Byte>(segments[0]);

            var first = new BufferSegment(segments[0], 0);
            var last  = first;

            for (var i = 1; i < segments.Length; i++)
                last = last.Append(segments[i]);

            return new ReadOnlySequence<Byte>(first, 0, last, last.Memory.Length);

        }

        #endregion

        #region Prepend  (First, Rest)

        /// <summary>
        /// Create a read-only sequence of the given memory followed by the given sequence,
        /// without copying either of them.
        /// </summary>
        /// <param name="First">The memory to put in front, e.g. a protocol header.</param>
        /// <param name="Rest">The sequence to append to it, e.g. a payload.</param>
        public static ReadOnlySequence<Byte> Prepend(ReadOnlyMemory<Byte>    First,
                                                     ReadOnlySequence<Byte>  Rest)
        {

            if (Rest.IsEmpty)
                return new ReadOnlySequence<Byte>(First);

            if (First.IsEmpty)
                return Rest;

            var first = new BufferSegment(First, 0);
            var last  = first;

            foreach (var segment in Rest)
            {
                if (!segment.IsEmpty)
                    last = last.Append(segment);
            }

            return new ReadOnlySequence<Byte>(first, 0, last, last.Memory.Length);

        }

        #endregion

    }

}
