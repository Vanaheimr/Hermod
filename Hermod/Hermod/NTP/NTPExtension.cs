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

using System.Diagnostics.CodeAnalysis;
using System.Xml;

namespace org.GraphDefined.Vanaheimr.Hermod.NTP
{

    /// <summary>
    /// The NTP Extension.
    /// </summary>
    /// <param name="Type">The extension type.</param>
    /// <param name="Length">The overall length of the extension in octets (including the 4-byte header).</param>
    /// <param name="Value">The data within the extension (excluding the 4-byte header).</param>
    public class NTPExtension
    {

        #region Properties

        /// <summary>
        /// The extension type.
        /// </summary>
        public UInt16  Type      { get; }

        /// <summary>
        /// The text representation of the extension type.
        /// </summary>
        public String  Name

            => Type switch {
                   0x0104  => "Unique Identifier",
                   0x0204  => "NTS Cookie",
                   0x0404  => "Authenticator and Encrypted",
                   _       => "<unknown>"
               };

        /// <summary>
        /// The overall length of the extension (including the 4-byte header).
        /// </summary>
        public UInt16  Length    { get; }

        /// <summary>
        /// The data within the extension.
        /// </summary>
        public Byte[]  Value     { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new NTP extension.
        /// </summary>
        /// <param name="Type">The extension type.</param>
        /// <param name="Value">The data within the extension.</param>
        public NTPExtension(UInt16  Type,
                            Byte[]  Value)
        {

            this.Type   = Type;
            this.Value  = Value;
            this.Length = (UInt16) (4 + Value.Length);

            // Must be multiple of 4, so if needed, pad up
            while ((this.Length % 4) != 0)
                this.Length++;

        }

        #endregion


        #region TryParse

        /// <summary>
        /// Try to parse the given byte representation of a NTP extension.
        /// </summary>
        /// <param name="packet">The byte representation of a NTP extension to be parsed.</param>
        /// <param name="NTPExtension">The parsed NTP extension.</param>
        /// <param name="ErrorResponse">An optional error message.</param>
        public static Boolean TryParse(Byte[]                                  packet,
                                       [NotNullWhen(true)]  out NTPExtension?  NTPExtension,
                                       [NotNullWhen(false)] out String?        ErrorResponse)
        {

            ErrorResponse  = null;
            NTPExtension   = null;

            if (packet.Length < 4)
            {
                ErrorResponse = "The packet is too short!";
                return false;
            }

            var type   = (UInt16) ((packet[0] << 8) | packet[1]);
            var length = (UInt16) ((packet[2] << 8) | packet[3]);

            if (length < 4)
            {
                ErrorResponse = "Extension field too short!";
                return false;
            }

            if (length > packet.Length)
            {
                ErrorResponse = "Extension field too long!";
                return false;
            }

            var value = new Byte[length - 4];
            Buffer.BlockCopy(packet, 4, value, 0, length - 4);

            NTPExtension = new NTPExtension(
                               type,
                               value
                           );

            return true;

        }

        #endregion

        #region ToByteArray()

        /// <summary>
        /// Return a binary representation of this object.
        /// </summary>
        public Byte[] ToByteArray()
        {

            var result = new Byte[Length];

            result[0] = (Byte) ((Type   >> 8) & 0xff);
            result[1] = (Byte)  (Type         & 0xff);

            result[2] = (Byte) ((Length >> 8) & 0xff);
            result[3] = (Byte)  (Length       & 0xff);

            if (Value.Length > 0)
                Buffer.BlockCopy(Value, 0, result, 4, Value.Length);

            return result;

        }

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
