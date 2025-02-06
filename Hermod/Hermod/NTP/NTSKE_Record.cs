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

namespace org.GraphDefined.Vanaheimr.Hermod.NTP
{

    /// <summary>
    /// The NTS-KE record (RFC 8915).
    /// </summary>
    /// <param name="IsCritical">Whether an unrecognized record must cause an error.</param>
    /// <param name="Type">The type of the record.</param>
    /// <param name="Value">The data of the record.</param>
    public class NTSKE_Record(Boolean  IsCritical,
                              UInt16   Type,
                              Byte[]   Value)
    {

        #region Properties

        /// <summary>
        /// Whether an unrecognized record must cause an error.
        /// </summary>
        public Boolean  IsCritical    { get; } = IsCritical;

        /// <summary>
        /// The type of the record.
        /// </summary>
        public UInt16   Type          { get; } = Type;

        /// <summary>
        /// The type name of the record.
        /// </summary>
        public String   Name

            => Type switch {
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
        /// The data of the record.
        /// </summary>
        public Byte[]   Value         { get; } = Value;

        /// <summary>
        /// Length of the record data.
        /// </summary>
        public UInt16   Length        { get; } = (UInt16) Value.Length;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{Name} ({Type}, {(IsCritical ? "critical" : "non critical")}, Len={Value.Length} Body=[{BitConverter.ToString(Value)}]";

        #endregion

    }

}
