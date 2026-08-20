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

using System.Buffers;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Rendezvous
{

    /// <summary>
    /// A chunk of relayed data, backed by a pooled array.
    /// Whoever takes a chunk out of a queue must return it afterwards.
    /// </summary>
    /// <param name="Buffer">The pooled buffer.</param>
    /// <param name="Length">The number of valid bytes within the buffer.</param>
    internal readonly record struct RelayChunk(Byte[]  Buffer,
                                               Int32   Length)
    {

        /// <summary>
        /// The valid bytes of this chunk.
        /// </summary>
        public ReadOnlyMemory<Byte> Data
            => Buffer.AsMemory(0, Length);


        /// <summary>
        /// Copy the given data into a pooled buffer.
        /// </summary>
        /// <param name="Data">The data to copy.</param>
        public static RelayChunk Copy(ReadOnlySpan<Byte> Data)
        {

            var buffer = ArrayPool<Byte>.Shared.Rent(Data.Length);
            Data.CopyTo(buffer);

            return new RelayChunk(buffer, Data.Length);

        }


        /// <summary>
        /// Return the pooled buffer of this chunk.
        /// </summary>
        public void Return()
            => ArrayPool<Byte>.Shared.Return(Buffer);

    }

}
