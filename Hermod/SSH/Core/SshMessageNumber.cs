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
    /// SSH message numbers — the first byte of every SSH packet payload.
    /// Ranges are assigned by RFC 4250, section 4.1.2:
    /// 1..19 transport (generic), 20..29 transport (algorithm negotiation),
    /// 30..49 transport (key exchange method specific), 50..59 user authentication (generic),
    /// 60..79 user authentication (method specific), 80..89 connection (generic),
    /// 90..127 connection (channel related), 128..191 reserved for client protocols,
    /// 192..255 local extensions / private use.
    /// </summary>
    public enum SshMessageNumber : Byte
    {

        #region Transport layer (RFC 4253)

        /// <summary>
        /// SSH_MSG_DISCONNECT (1) — terminate the connection with a reason code.
        /// </summary>
        Disconnect                    =   1,

        /// <summary>
        /// SSH_MSG_IGNORE (2) — a message to be ignored (e.g. traffic-analysis padding).
        /// </summary>
        Ignore                        =   2,

        /// <summary>
        /// SSH_MSG_UNIMPLEMENTED (3) — response to an unrecognized message.
        /// </summary>
        Unimplemented                 =   3,

        /// <summary>
        /// SSH_MSG_DEBUG (4) — optional debug information.
        /// </summary>
        Debug                         =   4,

        /// <summary>
        /// SSH_MSG_SERVICE_REQUEST (5) — request a service by name (e.g. "ssh-userauth").
        /// </summary>
        ServiceRequest                =   5,

        /// <summary>
        /// SSH_MSG_SERVICE_ACCEPT (6) — the requested service was accepted.
        /// </summary>
        ServiceAccept                 =   6,

        /// <summary>
        /// SSH_MSG_EXT_INFO (7) — extension negotiation (RFC 8308).
        /// </summary>
        ExtInfo                       =   7,

        /// <summary>
        /// SSH_MSG_KEXINIT (20) — start key exchange, carrying the algorithm name-lists.
        /// </summary>
        KexInit                       =  20,

        /// <summary>
        /// SSH_MSG_NEWKEYS (21) — subsequent packets use the newly negotiated keys.
        /// </summary>
        NewKeys                       =  21,

        #endregion

        #region Key exchange method specific (30..49, meaning depends on the negotiated method)

        /// <summary>
        /// (30) SSH_MSG_KEXDH_INIT / SSH_MSG_KEX_ECDH_INIT / SSH_MSG_KEX_HYBRID_INIT — client's key
        /// exchange initiation; the exact meaning depends on the negotiated key exchange method.
        /// </summary>
        KexEcdhInit                   =  30,

        /// <summary>
        /// (31) SSH_MSG_KEXDH_REPLY / SSH_MSG_KEX_ECDH_REPLY / SSH_MSG_KEX_HYBRID_REPLY — server's key
        /// exchange reply; the exact meaning depends on the negotiated key exchange method.
        /// </summary>
        KexEcdhReply                  =  31,

        #endregion

        #region User authentication (RFC 4252)

        /// <summary>
        /// SSH_MSG_USERAUTH_REQUEST (50).
        /// </summary>
        UserAuthRequest               =  50,

        /// <summary>
        /// SSH_MSG_USERAUTH_FAILURE (51) — carries the remaining methods and a partial-success flag.
        /// </summary>
        UserAuthFailure               =  51,

        /// <summary>
        /// SSH_MSG_USERAUTH_SUCCESS (52).
        /// </summary>
        UserAuthSuccess               =  52,

        /// <summary>
        /// SSH_MSG_USERAUTH_BANNER (53) — pre-authentication banner text.
        /// </summary>
        UserAuthBanner                =  53,

        /// <summary>
        /// (60) Method specific: SSH_MSG_USERAUTH_PK_OK ("publickey"),
        /// SSH_MSG_USERAUTH_PASSWD_CHANGEREQ ("password") or
        /// SSH_MSG_USERAUTH_INFO_REQUEST ("keyboard-interactive", RFC 4256).
        /// </summary>
        UserAuth60                    =  60,

        /// <summary>
        /// SSH_MSG_USERAUTH_INFO_RESPONSE (61) — "keyboard-interactive" response (RFC 4256).
        /// </summary>
        UserAuthInfoResponse          =  61,

        #endregion

        #region Connection protocol (RFC 4254)

        /// <summary>
        /// SSH_MSG_GLOBAL_REQUEST (80).
        /// </summary>
        GlobalRequest                 =  80,

        /// <summary>
        /// SSH_MSG_REQUEST_SUCCESS (81).
        /// </summary>
        RequestSuccess                =  81,

        /// <summary>
        /// SSH_MSG_REQUEST_FAILURE (82).
        /// </summary>
        RequestFailure                =  82,

        /// <summary>
        /// SSH_MSG_CHANNEL_OPEN (90).
        /// </summary>
        ChannelOpen                   =  90,

        /// <summary>
        /// SSH_MSG_CHANNEL_OPEN_CONFIRMATION (91).
        /// </summary>
        ChannelOpenConfirmation       =  91,

        /// <summary>
        /// SSH_MSG_CHANNEL_OPEN_FAILURE (92).
        /// </summary>
        ChannelOpenFailure            =  92,

        /// <summary>
        /// SSH_MSG_CHANNEL_WINDOW_ADJUST (93).
        /// </summary>
        ChannelWindowAdjust           =  93,

        /// <summary>
        /// SSH_MSG_CHANNEL_DATA (94).
        /// </summary>
        ChannelData                   =  94,

        /// <summary>
        /// SSH_MSG_CHANNEL_EXTENDED_DATA (95) — e.g. stderr (SSH_EXTENDED_DATA_STDERR).
        /// </summary>
        ChannelExtendedData           =  95,

        /// <summary>
        /// SSH_MSG_CHANNEL_EOF (96).
        /// </summary>
        ChannelEof                    =  96,

        /// <summary>
        /// SSH_MSG_CHANNEL_CLOSE (97).
        /// </summary>
        ChannelClose                  =  97,

        /// <summary>
        /// SSH_MSG_CHANNEL_REQUEST (98).
        /// </summary>
        ChannelRequest                =  98,

        /// <summary>
        /// SSH_MSG_CHANNEL_SUCCESS (99).
        /// </summary>
        ChannelSuccess                =  99,

        /// <summary>
        /// SSH_MSG_CHANNEL_FAILURE (100).
        /// </summary>
        ChannelFailure                = 100,

        #endregion

        #region Local extensions / private use (192..255)

        /// <summary>
        /// SSH_MSG_PING (192) — OpenSSH "ping@openssh.com" keep-alive / keystroke-timing chaff.
        /// </summary>
        Ping                          = 192,

        /// <summary>
        /// SSH_MSG_PONG (193) — OpenSSH response to <see cref="Ping"/>.
        /// </summary>
        Pong                          = 193

        #endregion

    }

}
