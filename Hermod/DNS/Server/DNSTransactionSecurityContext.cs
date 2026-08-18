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

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// What a verified request leaves behind for its response to be signed with.
    /// </summary>
    /// <remarks>
    /// It travels from the point where the signature was checked to the point
    /// where the answer is serialized, which on a stream or an HTTP request are
    /// far apart. A null one is the ordinary case: the request was unsigned, so
    /// the response is too.
    /// </remarks>
    public sealed class DNSTransactionSecurityContext
    {

        /// <summary>
        /// The TSIG key the request was signed with (RFC 8945).
        /// </summary>
        public TSIGKey?  TSIGKey        { get; init; }

        /// <summary>
        /// The MAC of that request, which the reply's MAC folds in.
        /// </summary>
        public Byte[]?   RequestMAC     { get; init; }

        /// <summary>
        /// The key to sign the reply with, when the request carried a SIG(0) and this server has one (RFC 2931).
        /// </summary>
        public SIG0Key?  SIG0Key        { get; init; }

        /// <summary>
        /// The request exactly as received, SIG(0) included — the "full query" of RFC 2931 §3.1.
        /// </summary>
        public Byte[]?   SignedRequest  { get; init; }

    }

}
