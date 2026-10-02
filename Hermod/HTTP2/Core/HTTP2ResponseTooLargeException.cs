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
    /// A buffered response — one <see cref="HTTP2ClientConnection.SendRequestAsync"/>
    /// collects whole before it hands it over — that would have taken more than
    /// <see cref="HTTP2ClientOptions.MaxResponseBodySize"/>: its body went past
    /// the limit, or the content-length it declared did. The client has reset the
    /// stream, with the <see cref="HTTP2StreamException.ErrorCode"/> carried here,
    /// rather than take in the rest. Not a protocol error — the server may well
    /// have sent what it was asked for — but more than a buffered response may
    /// hold. A response that large is read chunk by chunk, with
    /// <see cref="HTTP2ClientConnection.StartStreamingRequestAsync"/> or
    /// <see cref="HTTP2ClientConnection.DownloadAsync"/>.
    /// </summary>
    /// <param name="Limit">The <see cref="HTTP2ClientOptions.MaxResponseBodySize"/> in force.</param>
    /// <param name="ContentLength">The content-length the response declared, if it declared one the client could read.</param>
    /// <param name="Received">The body octets the response had brought when the client refused it, those of the DATA frame it refused included.</param>
    public class HTTP2ResponseTooLargeException(HTTP2ErrorCode  ErrorCode,
                                                UInt32          StreamId,
                                                Int64           Limit,
                                                Int64?          ContentLength,
                                                Int64           Received,
                                                String          Message)

        : HTTP2StreamException(ErrorCode, StreamId, Message)

    {

        /// <summary>
        /// The <see cref="HTTP2ClientOptions.MaxResponseBodySize"/> in force.
        /// </summary>
        public Int64   Limit          { get; } = Limit;

        /// <summary>
        /// The content-length the response declared, if it declared one the client
        /// could read. Above <see cref="Limit"/> when the response was refused at
        /// its HEADERS, before any of its body arrived.
        /// </summary>
        public Int64?  ContentLength  { get; } = ContentLength;

        /// <summary>
        /// The body octets the response had brought when the client refused it,
        /// those of the DATA frame it refused included: above <see cref="Limit"/>
        /// when the body went past it, zero when the response was refused at its
        /// HEADERS.
        /// </summary>
        public Int64   Received       { get; } = Received;

    }

}
