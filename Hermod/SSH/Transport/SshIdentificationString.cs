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

#region Usings

using System.Text;
using System.Diagnostics.CodeAnalysis;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// The SSH protocol version / identification string exchanged before the binary packet protocol
    /// starts (RFC 4253, section 4.2): <c>SSH-protoversion-softwareversion SP comments</c>.
    /// </summary>
    /// <remarks>
    /// The full line (including the trailing CR LF) must not exceed 255 bytes. The bytes returned by
    /// <see cref="ToWireBytes"/> (the line without CR LF) are what enters the key-exchange hash as
    /// V_C and V_S.
    /// </remarks>
    public sealed class SshIdentificationString
    {

        #region Data

        /// <summary>
        /// The maximum length of the identification line including CR LF (RFC 4253).
        /// </summary>
        public const Int32 MaxLineLength = 255;

        #endregion

        #region Properties

        /// <summary>
        /// The protocol version, normally "2.0".
        /// </summary>
        public String   ProtocolVersion    { get; }

        /// <summary>
        /// The software version, printable US-ASCII without spaces or minus signs.
        /// </summary>
        public String   SoftwareVersion    { get; }

        /// <summary>
        /// Optional free-text comments (everything after the first space).
        /// </summary>
        public String?  Comments           { get; }

        /// <summary>
        /// The identification line as sent on the wire, without the trailing CR LF.
        /// </summary>
        public String   Line

            => Comments is null
                   ? $"SSH-{ProtocolVersion}-{SoftwareVersion}"
                   : $"SSH-{ProtocolVersion}-{SoftwareVersion} {Comments}";

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new SSH identification string.
        /// </summary>
        /// <param name="SoftwareVersion">The software version (no spaces, no minus signs).</param>
        /// <param name="Comments">Optional comments.</param>
        /// <param name="ProtocolVersion">The protocol version; defaults to "2.0".</param>
        public SshIdentificationString(String   SoftwareVersion,
                                       String?  Comments         = null,
                                       String   ProtocolVersion  = "2.0")
        {

            if (String.IsNullOrEmpty(SoftwareVersion))
                throw new ArgumentException("The software version must not be empty!", nameof(SoftwareVersion));

            foreach (var c in SoftwareVersion)
                if (c < 0x20 || c > 0x7E || c == ' ' || c == '-')
                    throw new ArgumentException("The software version must be printable US-ASCII without spaces or minus signs!", nameof(SoftwareVersion));

            this.ProtocolVersion  = ProtocolVersion;
            this.SoftwareVersion  = SoftwareVersion;
            this.Comments         = Comments;

        }

        #endregion


        /// <summary>
        /// The default HermodSSH identification string.
        /// </summary>
        public static SshIdentificationString Default { get; } = new ("HermodSSH_0.1");


        #region ToWireBytes()

        /// <summary>
        /// The identification line as US-ASCII bytes, without the trailing CR LF (this is what enters
        /// the key-exchange hash as V_C / V_S).
        /// </summary>
        public Byte[] ToWireBytes()
            => Encoding.ASCII.GetBytes(Line);

        #endregion

        #region (static) Parse   (Text)

        /// <summary>
        /// Parse an SSH identification line (with or without a trailing CR LF).
        /// </summary>
        /// <param name="Text">The line to parse.</param>
        public static SshIdentificationString Parse(String Text)
        {

            if (TryParse(Text, out var result, out var errorResponse))
                return result;

            throw new SshWireException(errorResponse);

        }

        #endregion

        #region (static) TryParse(Text, out Result, out ErrorResponse)

        /// <summary>
        /// Try to parse an SSH identification line (with or without a trailing CR LF).
        /// </summary>
        /// <param name="Text">The line to parse.</param>
        /// <param name="Result">The parsed identification string, if successful.</param>
        /// <param name="ErrorResponse">An error description, if parsing failed.</param>
        public static Boolean TryParse(String                                             Text,
                                       [NotNullWhen(true)]  out SshIdentificationString?  Result,
                                       [NotNullWhen(false)] out String?                   ErrorResponse)
        {

            Result         = null;
            ErrorResponse  = null;

            var line = Text.TrimEnd('\r', '\n');

            if (!line.StartsWith("SSH-", StringComparison.Ordinal))
            {
                ErrorResponse = "An SSH identification string must start with 'SSH-'!";
                return false;
            }

            // SSH-<protoversion>-<softwareversion>[ <comments>]
            var afterPrefix     = line[4..];
            var firstDash       = afterPrefix.IndexOf('-');

            if (firstDash < 1)
            {
                ErrorResponse = "Malformed SSH identification string: missing '-' after the protocol version!";
                return false;
            }

            var protocolVersion = afterPrefix[..firstDash];

            if (protocolVersion is not "2.0" and not "1.99")
            {
                ErrorResponse = $"Unsupported SSH protocol version '{protocolVersion}' (only 2.0 is supported)!";
                return false;
            }

            var rest            = afterPrefix[(firstDash + 1)..];
            var spaceIndex      = rest.IndexOf(' ');

            var softwareVersion = spaceIndex < 0 ? rest : rest[..spaceIndex];
            var comments        = spaceIndex < 0 ? null : rest[(spaceIndex + 1)..];

            if (softwareVersion.Length == 0)
            {
                ErrorResponse = "Malformed SSH identification string: empty software version!";
                return false;
            }

            Result = new SshIdentificationString(softwareVersion, comments, protocolVersion);
            return true;

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return the identification line (without CR LF).
        /// </summary>
        public override String ToString()
            => Line;

        #endregion

    }

}
