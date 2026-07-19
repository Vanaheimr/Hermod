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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    /// <summary>
    /// An incremental UTF-8 validator for fragmented WebSocket text messages
    /// (RFC 6455 Section 8.1): Invalid UTF-8 must fail the connection as soon
    /// as possible, while an UTF-8 code point truncated at a fragment boundary
    /// is fine, as long as it is completed by the next fragment.
    /// </summary>
    public sealed class IncrementalUtf8Validator
    {

        #region Data

        private Byte[] incompleteTail = [];

        #endregion

        #region Properties

        /// <summary>
        /// Whether all data appended so far was valid UTF-8.
        /// </summary>
        public Boolean IsValid { get; private set; } = true;

        #endregion


        #region Append(Chunk, IsFinal)

        /// <summary>
        /// Append the given chunk of a fragmented text message and validate it.
        /// </summary>
        /// <param name="Chunk">The next chunk of the text message.</param>
        /// <param name="IsFinal">Whether this is the final chunk of the text message.</param>
        /// <returns>False, when the data appended so far is not valid UTF-8.</returns>
        public Boolean Append(ReadOnlySpan<Byte>  Chunk,
                              Boolean             IsFinal)
        {

            if (!IsValid)
                return false;

            var buffer = new Byte[incompleteTail.Length + Chunk.Length];
            incompleteTail.CopyTo(buffer, 0);
            Chunk.CopyTo(buffer.AsSpan(incompleteTail.Length));

            var span = buffer.AsSpan();

            while (!span.IsEmpty)
            {

                var status = Rune.DecodeFromUtf8(span, out _, out var consumed);

                if (status == OperationStatus.Done)
                {
                    span = span[consumed..];
                    continue;
                }

                // A code point truncated at the end of a non-final
                // fragment might be completed by the next fragment...
                if (status == OperationStatus.NeedMoreData && !IsFinal && span.Length < 4)
                {
                    incompleteTail = span.ToArray();
                    return true;
                }

                IsValid = false;
                return false;

            }

            incompleteTail = [];
            return true;

        }

        #endregion

        #region Reset()

        /// <summary>
        /// Reset this validator for the next message.
        /// </summary>
        public void Reset()
        {
            incompleteTail  = [];
            IsValid         = true;
        }

        #endregion

    }

}
