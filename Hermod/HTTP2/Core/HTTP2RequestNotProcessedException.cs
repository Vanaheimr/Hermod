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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP2
{

    /// <summary>
    /// Thrown for a request the peer provably did <b>not</b> process, so it is safe
    /// to retry — verbatim — on a fresh connection without risking duplicate
    /// side effects. This is the case for a stream refused with
    /// <see cref="HTTP2ErrorCode.REFUSED_STREAM"/> (RFC 9113, Section 8.1) and for
    /// any stream with an ID greater than a GOAWAY's Last-Stream-ID (Section 6.8).
    /// </summary>
    public class HTTP2RequestNotProcessedException(HTTP2ErrorCode ErrorCode, string Message)
        : Exception(Message)
    {
        public HTTP2ErrorCode  ErrorCode  { get; } = ErrorCode;
    }

}
