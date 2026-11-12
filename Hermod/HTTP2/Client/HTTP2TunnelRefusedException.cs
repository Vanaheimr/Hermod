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
    /// A CONNECT the server answered with something other than a 2xx: there is
    /// no tunnel. <see cref="Status"/> and <see cref="Headers"/> are the
    /// server's answer - a 401 with its challenge, a 503 with its Retry-After -
    /// for a caller that wants to know why, or when to come back.
    /// </summary>
    public sealed class HTTP2TunnelRefusedException(UInt32                                      StreamId,
                                                    Int32                                       Status,
                                                    IReadOnlyList<(String Name, String Value)>  Headers,
                                                    String                                      Message)

        : HTTP2StreamException(HTTP2ErrorCode.REFUSED_STREAM, StreamId, Message)

    {

        /// <summary>
        /// The status the server answered the CONNECT with.
        /// </summary>
        public Int32                                       Status   { get; } = Status;

        /// <summary>
        /// The header fields of the server's answer, <c>:status</c> among them.
        /// </summary>
        public IReadOnlyList<(String Name, String Value)>  Headers  { get; } = Headers;

    }

}
