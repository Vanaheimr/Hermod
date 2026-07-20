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
    /// HTTP/2 frame types as defined in RFC 9113, Section 6.
    /// </summary>
    public enum HTTP2FrameType : byte
    {
        DATA          = 0x00,
        HEADERS       = 0x01,
        PRIORITY      = 0x02,
        RST_STREAM    = 0x03,
        SETTINGS      = 0x04,
        PUSH_PROMISE  = 0x05,
        PING          = 0x06,
        GOAWAY        = 0x07,
        WINDOW_UPDATE = 0x08,
        CONTINUATION  = 0x09,

        /// <summary>
        /// RFC 9218 (Extensible Prioritization Scheme for HTTP), Section 7.1: a
        /// connection-level frame (its own Stream Identifier is always 0) that
        /// reprioritizes another stream, named by a "Prioritized Stream ID" in
        /// the payload, without needing a fresh HEADERS/request on it.
        /// </summary>
        PRIORITY_UPDATE = 0x10
    }

}
