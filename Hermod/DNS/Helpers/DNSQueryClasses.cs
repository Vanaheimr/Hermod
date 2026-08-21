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
    /// Query Class or Scope
    /// </summary>
    public enum DNSQueryClasses : UInt16
    {

        /// <summary>
        /// Internet IPv4/IPv6
        /// </summary>
        IN    = 1,

        /// <summary>
        /// CSNET
        /// </summary>
        CS    = 2,

        /// <summary>
        /// Chaosnet
        /// </summary>
        CH    = 3,

        /// <summary>
        /// Hesiod
        /// </summary>
        HS    = 4,

        /// <summary>
        /// QCLASS NONE (RFC 2136 §2.4), used in the prerequisite and update
        /// sections of a dynamic update to mean "no records of this set".
        /// </summary>
        /// <remarks>
        /// RFC 6895 §3.2 places this at 254 and reserves 0, which has no
        /// mnemonic at all. Naming 0 here instead put the reserved code point
        /// under the wrong label in both directions of the presentation
        /// format: a class-0 record was written as "NONE", which every other
        /// reader takes for 254, while a record genuinely in class 254 came
        /// out as RFC 3597 §5's generic "CLASS254".
        /// </remarks>
        NONE  = 254,

        /// <summary>
        /// QCLASS * (RFC 1035 §3.2.5), matching any class. Like NONE this is
        /// a QCLASS: legal in a question, never on a record.
        /// </summary>
        ANY   = 255

    }

}
