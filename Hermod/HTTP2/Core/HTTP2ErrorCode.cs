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
    /// HTTP/2 error codes as defined in RFC 9113, Section 7.
    /// </summary>
    public enum HTTP2ErrorCode : uint
    {
        NO_ERROR              = 0x00,
        PROTOCOL_ERROR        = 0x01,
        INTERNAL_ERROR        = 0x02,
        FLOW_CONTROL_ERROR    = 0x03,
        SETTINGS_TIMEOUT      = 0x04,
        STREAM_CLOSED         = 0x05,
        FRAME_SIZE_ERROR      = 0x06,
        REFUSED_STREAM        = 0x07,
        CANCEL                = 0x08,
        COMPRESSION_ERROR     = 0x09,
        CONNECT_ERROR         = 0x0A,
        ENHANCE_YOUR_CALM     = 0x0B,
        INADEQUATE_SECURITY   = 0x0C,
        HTTP_1_1_REQUIRED     = 0x0D
    }

}
