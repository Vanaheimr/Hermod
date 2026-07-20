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
    /// HTTP/2 settings parameters as defined in RFC 9113, Section 6.5.2.
    /// </summary>
    public enum HTTP2SettingsParameter : ushort
    {
        HEADER_TABLE_SIZE        = 0x01,
        ENABLE_PUSH              = 0x02,
        MAX_CONCURRENT_STREAMS   = 0x03,
        INITIAL_WINDOW_SIZE      = 0x04,
        MAX_FRAME_SIZE           = 0x05,
        MAX_HEADER_LIST_SIZE     = 0x06,

        /// <summary>
        /// RFC 8441, Section 3: a value of 1 tells the peer that extended CONNECT
        /// (the ":protocol" pseudo-header, used to bootstrap e.g. WebSocket) is
        /// supported on this connection.
        /// </summary>
        ENABLE_CONNECT_PROTOCOL = 0x08,

        /// <summary>
        /// RFC 9218 (Extensible Prioritization Scheme for HTTP), Section 3: a
        /// value of 1 tells the peer the sender will not use RFC 7540's
        /// deprecated stream-dependency/weight priority signaling (the PRIORITY
        /// frame, and the PRIORITY flag on HEADERS) — this server already
        /// ignores both unconditionally, so it is always advertised.
        /// </summary>
        NO_RFC7540_PRIORITIES = 0x09
    }

}
