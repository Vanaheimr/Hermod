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
    /// The common NTP header.
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
    public readonly struct NTPHeader(Byte    LI_VN_Mode,
                                     Byte    Stratum,
                                     Byte    Poll,
                                     SByte   Precision,
                                     UInt32  RootDelay,
                                     UInt32  RootDispersion,
                                     UInt32  ReferenceIdentifier,
                                     UInt64  ReferenceTimestamp,
                                     UInt64  OriginateTimestamp,
                                     UInt64  ReceiveTimestamp,
                                     UInt64  TransmitTimestamp)
    {

        #region Properties

        public Byte    LI_VN_Mode             { get; } = LI_VN_Mode;
        public Byte    Stratum                { get; } = Stratum;
        public Byte    Poll                   { get; } = Poll;
        public SByte   Precision              { get; } = Precision;
        public UInt32  RootDelay              { get; } = RootDelay;
        public UInt32  RootDispersion         { get; } = RootDispersion;
        public UInt32  ReferenceIdentifier    { get; } = ReferenceIdentifier;
        public UInt64  ReferenceTimestamp     { get; } = ReferenceTimestamp;
        public UInt64  OriginateTimestamp     { get; } = OriginateTimestamp;
        public UInt64  ReceiveTimestamp       { get; } = ReceiveTimestamp;
        public UInt64  TransmitTimestamp      { get; } = TransmitTimestamp;


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

        #region TryParseNTPHeader(Buffer, out NTPHeader, out ErrorResponse)
        public static Boolean TryParseNTPHeader(Byte[]                               Buffer,
                                                [NotNullWhen(true)]  out NTPHeader?  NTPHeader,
                                                [NotNullWhen(false)] out String?     ErrorResponse)
        {

            ErrorResponse = null;

            if (Buffer.Length < 48)
            {
                ErrorResponse = "Buffer too short!";
                NTPHeader = null;
                return false;
            }


            NTPHeader = new NTPHeader(
                            LI_VN_Mode:           Buffer[0],
                            Stratum:              Buffer[1],
                            Poll:                 Buffer[2],
                            Precision:            (SByte) Buffer[3],
                            RootDelay:            (UInt32) ((Buffer[4]  << 24) | (Buffer[5]  << 16) | (Buffer[6]  << 8) | Buffer[7]),
                            RootDispersion:       (UInt32) ((Buffer[8]  << 24) | (Buffer[9]  << 16) | (Buffer[10] << 8) | Buffer[11]),
                            ReferenceIdentifier:  (UInt32) ((Buffer[12] << 24) | (Buffer[13] << 16) | (Buffer[14] << 8) | Buffer[15]),
                            ReferenceTimestamp:   ReadUInt64(Buffer, 16),
                            OriginateTimestamp:   ReadUInt64(Buffer, 24),
                            ReceiveTimestamp:     ReadUInt64(Buffer, 32),
                            TransmitTimestamp:    ReadUInt64(Buffer, 40)
                        );

            return true;

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
