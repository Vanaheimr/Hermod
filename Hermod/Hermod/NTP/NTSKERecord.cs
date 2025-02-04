/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.NTP
{

    public class NTSKERecord(Boolean  Critical,
                             UInt16   Type,
                             Byte[]   Body)
    {

        #region Properties

        /// <summary>
        /// High bit of the 16-bit record type.
        /// Indicates an unrecognized record must cause an error if true.
        /// </summary>
        public Boolean  Critical    { get; } = Critical;

        /// <summary>
        /// Lower 15 bits of the 16-bit record type.
        /// This is the actual record type per RFC 8915 (0 = End of Message, 1 = Next Protocol, etc.)
        /// </summary>
        public UInt16   Type        { get; } = Type;

        public String   TypeName    { get; } = Type switch {
                                                   0 => "End of Message",
                                                   1 => "NTS Next Protocol Negotiation",
                                                   2 => "Error",
                                                   3 => "Warning",
                                                   4 => "AEAD Algorithm Negotiation",
                                                   5 => "New Cookie for NTPv4",
                                                   6 => "NTPv4 Server Negotiation (ASCII address?)",
                                                   7 => "NTPv4 Port Negotiation",
                                                   _ => "Unknown or custom record type!"
                                               };

        /// <summary>
        /// The raw record body (exactly BodyLength bytes).
        /// </summary>
        public Byte[]   Body        { get; } = Body;

        /// <summary>
        /// Length (in bytes) of the record body, as read from the wire (big-endian).
        /// </summary>
        public UInt16   Length      { get; } = (UInt16) Body.Length;

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{TypeName} ({Type}, {(Critical ? "critical" : "non critical")}, Len={Body.Length} Body=[{BitConverter.ToString(Body)}]";

        #endregion

    }

}
