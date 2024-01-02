/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

namespace org.GraphDefined.Vanaheimr.Hermod.Sockets.RawIP.ICMP
{

    /// <summary>
    /// ICMP results/errors.
    /// </summary>
    public enum ICMPErrors
    {

        /// <summary>
        /// Success / No error(s).
        /// </summary>
        Success,

        /// <summary>
        /// DNS error(s).
        /// </summary>
        DNSError,

        /// <summary>
        /// The ping(s) could not be sent.
        /// </summary>
        SendError,

        /// <summary>
        /// A timeout occured.
        /// </summary>
        Timeout,

        /// <summary>
        /// The time-to-live of the underlying IP packet was exceeded.
        /// </summary>
        TTLExceeded,

        /// <summary>
        /// The host or network was unreachable.
        /// </summary>
        Unreachable,

        /// <summary>
        /// Invalid reply/replies.
        /// </summary>
        InvalidReply,

        /// <summary>
        /// Mixed result(s).
        /// </summary>
        Mixed,

        /// <summary>
        /// Unknown result(s).
        /// </summary>
        Unknown

    }

}
