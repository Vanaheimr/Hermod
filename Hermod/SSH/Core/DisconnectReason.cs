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

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// Reason codes carried by an SSH_MSG_DISCONNECT message (RFC 4253, section 11.1;
    /// RFC 4250, section 4.2.2).
    /// </summary>
    public enum DisconnectReason : UInt32
    {

        /// <summary>SSH_DISCONNECT_HOST_NOT_ALLOWED_TO_CONNECT (1).</summary>
        HostNotAllowedToConnect       =  1,

        /// <summary>SSH_DISCONNECT_PROTOCOL_ERROR (2).</summary>
        ProtocolError                 =  2,

        /// <summary>SSH_DISCONNECT_KEY_EXCHANGE_FAILED (3).</summary>
        KeyExchangeFailed             =  3,

        /// <summary>SSH_DISCONNECT_RESERVED (4).</summary>
        Reserved                      =  4,

        /// <summary>SSH_DISCONNECT_MAC_ERROR (5).</summary>
        MacError                      =  5,

        /// <summary>SSH_DISCONNECT_COMPRESSION_ERROR (6).</summary>
        CompressionError              =  6,

        /// <summary>SSH_DISCONNECT_SERVICE_NOT_AVAILABLE (7).</summary>
        ServiceNotAvailable           =  7,

        /// <summary>SSH_DISCONNECT_PROTOCOL_VERSION_NOT_SUPPORTED (8).</summary>
        ProtocolVersionNotSupported   =  8,

        /// <summary>SSH_DISCONNECT_HOST_KEY_NOT_VERIFIABLE (9).</summary>
        HostKeyNotVerifiable          =  9,

        /// <summary>SSH_DISCONNECT_CONNECTION_LOST (10).</summary>
        ConnectionLost                = 10,

        /// <summary>SSH_DISCONNECT_BY_APPLICATION (11).</summary>
        ByApplication                 = 11,

        /// <summary>SSH_DISCONNECT_TOO_MANY_CONNECTIONS (12).</summary>
        TooManyConnections            = 12,

        /// <summary>SSH_DISCONNECT_AUTH_CANCELLED_BY_USER (13).</summary>
        AuthCancelledByUser           = 13,

        /// <summary>SSH_DISCONNECT_NO_MORE_AUTH_METHODS_AVAILABLE (14).</summary>
        NoMoreAuthMethodsAvailable    = 14,

        /// <summary>SSH_DISCONNECT_ILLEGAL_USER_NAME (15).</summary>
        IllegalUserName               = 15

    }

}
