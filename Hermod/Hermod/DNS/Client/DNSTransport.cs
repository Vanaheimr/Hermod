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

    public enum DNSTransport
    {

        /// <summary>
        /// Normal binary transport over UDP.
        /// </summary>
        UDP,


        /// <summary>
        /// Normal binary transport over TCP.
        /// </summary>
        TCP,

        /// <summary>
        /// Encrypted binary transport over TLS.
        /// </summary>
        TLS,


        /// <summary>
        /// DNS-over-HTTP without encryption.
        /// </summary>
        HTTP,

        /// <summary>
        /// DNS-over-HTTP (DoH) without encryption and with binary payloads.
        /// </summary>
        HTTP_Binary,

        /// <summary>
        /// DNS-over-HTTP (DoH) without encryption and with JSON payloads.
        /// </summary>
        HTTP_JSON,


        /// <summary>
        /// DNS-over-HTTPS (DoH)
        /// </summary>
        HTTPS,

        /// <summary>
        /// DNS-over-HTTPS (DoH) with binary payloads.
        /// </summary>
        HTTPS_Binary,

        /// <summary>
        /// DNS-over-HTTPS (DoH) with JSON payloads.
        /// </summary>
        HTTPS_JSON

    }

}
