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

using System.Diagnostics.CodeAnalysis;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.NTP
{

    /// <summary>
    /// The common NTP packet.
    /// </summary>
    /// <param name="LI_VN_Mode"
    /// <param name="Stratum"></param>
    /// <param name="Poll"></param>
    /// <param name="Precision"></param>
    /// <param name="RootDelay"></param>
    /// <param name="RootDispersion"></param>
    /// <param name="ReferenceIdentifier"></param>
    /// <param name="ReferenceTimestamp"></param>
    /// <param name="OriginateTimestamp"></param>
    /// <param name="ReceiveTimestamp"></param>
    /// <param name="TransmitTimestamp"></param>
    /// <param name="Extensions"></param>
    public class NTPResponse(Byte                        LI_VN_Mode,
                             Byte                        Stratum,
                             Byte                        Poll,
                             SByte                       Precision,
                             UInt32                      RootDelay,
                             UInt32                      RootDispersion,
                             UInt32                      ReferenceIdentifier,
                             DateTime                    ReferenceTimestamp,
                             DateTime                    OriginateTimestamp,
                             DateTime                    ReceiveTimestamp,
                             DateTime                    TransmitTimestamp,
                             IEnumerable<NTPExtension>?  Extensions   = null)
    {

        #region Properties

        public Byte                       LI_VN_Mode             { get; } = LI_VN_Mode;
        public Byte                       Stratum                { get; } = Stratum;
        public Byte                       Poll                   { get; } = Poll;
        public SByte                      Precision              { get; } = Precision;
        public UInt32                     RootDelay              { get; } = RootDelay;
        public UInt32                     RootDispersion         { get; } = RootDispersion;
        public UInt32                     ReferenceIdentifier    { get; } = ReferenceIdentifier;
        public DateTime                   ReferenceTimestamp     { get; } = ReferenceTimestamp;
        public DateTime                   OriginateTimestamp     { get; } = OriginateTimestamp;
        public DateTime                   ReceiveTimestamp       { get; } = ReceiveTimestamp;
        public DateTime                   TransmitTimestamp      { get; } = TransmitTimestamp;
        public IEnumerable<NTPExtension>  Extensions             { get; } = Extensions ?? [];


        public Byte    LeapIndicator
            => (Byte) ((LI_VN_Mode >> 6) & 0x03);

        public Byte    Version
            => (Byte) ((LI_VN_Mode >> 3) & 0x07);

        public Byte    Mode
            => (Byte) (LI_VN_Mode & 0x07);

        #endregion


        #region (private) ReadUInt64(Buffer, Offset)

        private static UInt64 ReadUInt64(Byte[] Buffer, Int32 Offset)

            => ((UInt64) Buffer[Offset]     << 56) |
               ((UInt64) Buffer[Offset + 1] << 48) |
               ((UInt64) Buffer[Offset + 2] << 40) |
               ((UInt64) Buffer[Offset + 3] << 32) |
               ((UInt64) Buffer[Offset + 4] << 24) |
               ((UInt64) Buffer[Offset + 5] << 16) |
               ((UInt64) Buffer[Offset + 6] <<  8) |
                         Buffer[Offset + 7];

        #endregion

        #region TryParseNTPHeader(Buffer, out NTPHeader, out ErrorResponse, ExptectedUniqueId   = null)
        public static Boolean TryParse(Byte[]                               Buffer,
                                       [NotNullWhen(true)]  out NTPResponse?  NTPHeader,
                                       [NotNullWhen(false)] out String?     ErrorResponse,
                                       Byte[]?                              ExptectedUniqueId   = null)
        {

            ErrorResponse = null;

            if (Buffer.Length < 48)
            {
                ErrorResponse = "Buffer too short!";
                NTPHeader = null;
                return false;
            }

            var offset     = 48; // Start of extension fields
            var extensions = new List<NTPExtension>();

            while (offset + 4 <= Buffer.Length)
            {

                var type   = (UInt16) ((Buffer[offset]     << 8) | Buffer[offset + 1]);
                var length = (UInt16) ((Buffer[offset + 2] << 8) | Buffer[offset + 3]);

                if (length < 4)
                {
                    ErrorResponse  = $"Illegal length of extension {length} at offset {offset}!";
                    NTPHeader      = null;
                    return false;
                }

                if (offset + length > Buffer.Length)
                    break;

                var data = new Byte[length - 4];
                Array.Copy(Buffer, offset + 4, data, 0, length - 4);

                extensions.Add(
                    new NTPExtension(
                        type,
                        //length,
                        data
                    )
                );

                offset += length;

            }

            NTPHeader = new NTPResponse(
                            LI_VN_Mode:           Buffer[0],
                            Stratum:              Buffer[1],
                            Poll:                 Buffer[2],
                            Precision:            (SByte) Buffer[3],
                            RootDelay:            (UInt32) ((Buffer[4]  << 24) | (Buffer[5]  << 16) | (Buffer[6]  << 8) | Buffer[7]),
                            RootDispersion:       (UInt32) ((Buffer[8]  << 24) | (Buffer[9]  << 16) | (Buffer[10] << 8) | Buffer[11]),
                            ReferenceIdentifier:  (UInt32) ((Buffer[12] << 24) | (Buffer[13] << 16) | (Buffer[14] << 8) | Buffer[15]),
                            ReferenceTimestamp:   NTPTimestampToDateTime(ReadUInt64(Buffer, 16)),
                            OriginateTimestamp:   NTPTimestampToDateTime(ReadUInt64(Buffer, 24)),
                            ReceiveTimestamp:     NTPTimestampToDateTime(ReadUInt64(Buffer, 32)),
                            TransmitTimestamp:    NTPTimestampToDateTime(ReadUInt64(Buffer, 40)),
                            Extensions:           extensions
                        );

            return true;

        }

        #endregion


        #region NTPTimestampToDateTime(ntpTimestamp)

        /// <summary>
        /// Converts a 64-bit NTP timestamp to a DateTime (UTC).
        /// </summary>
        public static DateTime NTPTimestampToDateTime(UInt64 NTPTimestamp)
        {

            var secondsSinceEpoch  = (UInt32) (NTPTimestamp >> 32);
            var fraction           = (UInt32) (NTPTimestamp & 0xFFFFFFFF);
            var fractionSeconds    = fraction / (Double) 0x100000000L; // 2^32

            return new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc).
                       AddSeconds(secondsSinceEpoch + fractionSeconds);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{TransmitTimestamp}, stratum: {Stratum}";

        #endregion

    }

}
