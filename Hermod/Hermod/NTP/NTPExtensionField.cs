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
    /// The NTP Extension Field.
    /// </summary>
    /// <param name="Type">The 16-Bit Field Type.</param>
    /// <param name="Length">The overall length of the extension field in octets (including the 4-byte header).</param>
    /// <param name="Value">The data within the extension field (excluding the 4-byte header).</param>
    public class NTPExtensionField(UInt16  Type,
                                   UInt16  Length,
                                   Byte[]? Value)
    {

        #region Properties

        /// <summary>
        /// The 16-Bit Field Type
        /// (z.B. 0x0104 = Unique Identifier, 0x0204 = NTS Cookie, 0x0404 = NTS Authenticator and Encrypted).
        /// </summary>
        public UInt16   Type      { get; } = Type;

        /// <summary>
        /// The overall length of the extension field in octets (including the 4-byte header).
        /// </summary>
        public UInt16   Length    { get; } = Length;

        /// <summary>
        /// The data within the extension field (excluding the 4-byte header).
        /// </summary>
        public Byte[]?  Value     { get; } = Value;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"Type: 0x{Type:X4}, Length: {Length}, Data: {BitConverter.ToString(Value ?? [])}";

        #endregion

    }

}
