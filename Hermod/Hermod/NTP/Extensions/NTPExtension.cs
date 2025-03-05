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

using System.Security.Cryptography;
using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.NTP
{

    /// <summary>
    /// A NTP Extension.
    /// 
    /// Network Time Security for the Network Time Protocol: https://datatracker.ietf.org/doc/html/rfc8915
    /// 5. NTS Extension Fields for NTPv4
    /// 
    /// Network Time Protocol Version 4 (NTPv4) Extension Fields: https://datatracker.ietf.org/doc/html/rfc7822
    /// 
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
        public ExtensionTypes  Type             { get; }

        /// <summary>
        /// The text representation of the extension type.
        /// </summary>
        public String          Name

            => Type switch {
                   ExtensionTypes.UniqueIdentifier           => "Unique Identifier",
                   ExtensionTypes.NTSCookie                  => "NTS Cookie",
                   ExtensionTypes.NTSCookiePlaceholder       => "NTS Cookie Placeholder",
                   ExtensionTypes.AuthenticatorAndEncrypted  => "Authenticator and Encrypted",
                   ExtensionTypes.Debug                      => "Debug",
                   _                                         => "<unknown>"
               };

        /// <summary>
        /// The overall length of the extension (including the 4-byte header).
        /// </summary>
        public UInt16          Length           { get; }

        /// <summary>
        /// The data within the extension.
        /// </summary>
        public Byte[]          Value            { get; }

        /// <summary>
        /// Whether the extension is/was authenticated.
        /// </summary>
        public Boolean         Authenticated    { get; internal set; }

        /// <summary>
        /// Whether the extension is/was encrypted.
        /// </summary>
        public Boolean         Encrypted        { get; internal set; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new NTP extension.
        /// </summary>
        /// <param name="Type">The extension type.</param>
        /// <param name="Value">The data within the extension.</param>
        /// <param name="Authenticated">Whether the extension is/was authenticated.</param>
        /// <param name="Encrypted">Whether the extension is/was encrypted.</param>
        public NTPExtension(ExtensionTypes  Type,
                            Byte[]          Value,
                            Boolean         Authenticated = false,
                            Boolean         Encrypted     = false)
        {

            this.Type           = Type;
            this.Value          = Value;
            this.Length         = (UInt16) (4 + Value.Length);
            this.Authenticated  = Authenticated;
            this.Encrypted      = Encrypted;

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
                               (ExtensionTypes) type,
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
            var type   = (UInt16) Type;

            result[0] = (Byte) ((type   >> 8) & 0xff);
            result[1] = (Byte)  (type         & 0xff);

            result[2] = (Byte) ((Length >> 8) & 0xff);
            result[3] = (Byte)  (Length       & 0xff);

            if (Value.Length > 0)
                Buffer.BlockCopy(Value, 0, result, 4, Value.Length);

            return result;

        }

        #endregion


        #region Static methods

        #region (static) UniqueIdentifier(UniqueId = null)

        /// <summary>
        /// Create a new Unique Identifier extension.
        /// </summary>
        /// <param name="UniqueId">The unique identifier.</param>
        public static NTPExtension UniqueIdentifier(Byte[]? UniqueId = null)
        {

            UniqueId ??= new Byte[32];
            RandomNumberGenerator.Fill(UniqueId);

            return new (
                       ExtensionTypes.UniqueIdentifier,
                       UniqueId
                   );

        }

        #endregion

        #region (static) NTSCookie(Value)

        /// <summary>
        /// Create a new NTS Cookie extension.
        /// </summary>
        /// <param name="Value">The NTS cookie.</param>
        public static NTPExtension  NTSCookie(Byte[] Value)

            => new (
                   ExtensionTypes.NTSCookie,
                   Value
               );

        #endregion

        #region (static) NTSCookiePlaceholder(Length)

        /// <summary>
        /// Create a new NTS Cookie Placeholder extension.
        /// </summary>
        /// <param name="Length">The length of the expected NTS cookie.</param>
        public static NTPExtension  NTSCookiePlaceholder(UInt16 Length)

            => new (
                   ExtensionTypes.NTSCookiePlaceholder,
                   new Byte[Length]
               );

        #endregion

        #region (static) AuthenticatorAndEncrypted(Value)

        /// <summary>
        /// Create a new Authenticator and Encrypted extension.
        /// </summary>
        /// <param name="Value">The Authenticator and Encrypted data.</param>
        public static NTPExtension  AuthenticatorAndEncrypted(Byte[] Value)

            => new (
                   ExtensionTypes.AuthenticatorAndEncrypted,
                   Value
               );

        #endregion

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"Type: {Type}, Length: {Length}, Data: {BitConverter.ToString(Value ?? [])}";

        #endregion

    }

}
