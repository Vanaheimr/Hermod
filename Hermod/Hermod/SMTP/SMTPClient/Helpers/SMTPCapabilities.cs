/*
 * Copyright (c) 2010-2023 GraphDefined GmbH
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

#region Usings

using System;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// Capabilities supported by an SMTP server, which are read as part of the response
    /// to an HELO/EHLO command that is issued during the connection phase.
    /// </summary>
    [Flags]
    public enum SmtpCapabilities
    {

        /// <summary>
        /// The server does not support any additional extensions.
        /// </summary>
        None                    =   0,

        /// <summary>
        /// The server supports the SIZE extension (rfc1870) and may have a maximum
        /// message size limitation.
        /// </summary>
        Size                    =   1,

        /// <summary>
        /// The server supports the DSN extension (rfc1891), allowing clients to
        /// specify which (if any) recipients they would like to receive delivery
        /// notifications for.
        /// </summary>
        Dsn                     =   2,

        /// <summary>
        /// The server supports the ENHANCEDSTATUSCODES extension (rfc2034).
        /// </summary>
        EnhancedStatusCodes     =   4,

        /// <summary>
        /// The server supports the AUTH extension (rfc2554), allowing clients to
        /// authenticate via supported SASL mechanisms.
        /// </summary>
        Authentication          =   8,

        /// <summary>
        /// The server supports the 8BITMIME extension (rfc2821), allowing clients
        /// to send messages using the "8bit" Content-Transfer-Encoding.
        /// </summary>
        EightBitMime            =  16,

        /// <summary>
        /// The server supports the PIPELINING extension (rfc2920), allowing clients
        /// to send multiple commands at once in order to reduce round-trip latency.
        /// </summary>
        Pipelining              =  32,

        /// <summary>
        /// The server supports the BINARYMIME extension (rfc3030).
        /// </summary>
        BinaryMime              =  64,

        /// <summary>
        /// The server supports the CHUNKING extension (rfc3030), allowing clients
        /// to upload messages in chunks.
        /// </summary>
        Chunking                = 128,

        /// <summary>
        /// The server supports the STARTTLS extension (rfc3207), allowing clients
        /// to switch to an encrypted SSL/TLS connection after connecting.
        /// </summary>
        StartTLS                = 256,

        /// <summary>
        /// The server supports the SMTPUTF8 extension (rfc6531).
        /// </summary>
        UTF8                    = 512,

    }

}
