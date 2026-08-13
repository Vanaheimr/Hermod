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

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Connection
{

    /// <summary>
    /// A stream could not be opened because the peer's stream limit is exhausted (RFC 9000 §4.6).
    /// </summary>
    /// <remarks>
    /// This is a refusal, not a failure: opening the stream anyway would oblige the peer to close
    /// the connection with STREAM_LIMIT_ERROR, taking every other stream with it. A STREAMS_BLOCKED
    /// frame has been queued (§19.14), so a peer that intends to grant more will; retrying after
    /// its MAX_STREAMS arrives is the intended response.
    /// </remarks>
    public sealed class QuicStreamLimitException : Exception
    {

        /// <summary>
        /// Whether the exhausted limit was the bidirectional one.
        /// </summary>
        public Boolean  Bidirectional    { get; }

        /// <summary>
        /// The number of streams of this kind the peer currently allows.
        /// </summary>
        public UInt64   MaximumStreams   { get; }

        public QuicStreamLimitException(Boolean  Bidirectional,
                                        UInt64   MaximumStreams)

            : base($"The peer allows {MaximumStreams} {(Bidirectional ? "bidirectional" : "unidirectional")} " +
                   $"stream(s) and all of them are used. A STREAMS_BLOCKED frame was queued; retry once " +
                   $"the peer has sent MAX_STREAMS.")

        {
            this.Bidirectional   = Bidirectional;
            this.MaximumStreams  = MaximumStreams;
        }

    }

}
