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
    /// The NTP request (RFC 5905).
    /// </summary>
    /// <param name="LI"></param>
    /// <param name="VN"></param>
    /// <param name="Mode"></param>
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
    public class NTPRequest(Byte?                       LI                    = null,
                            Byte?                       VN                    = null,
                            Byte?                       Mode                  = null,
                            Byte?                       Stratum               = null,
                            Byte?                       Poll                  = null,
                            SByte?                      Precision             = null,
                            UInt32?                     RootDelay             = null,
                            UInt32?                     RootDispersion        = null,
                            UInt32?                     ReferenceIdentifier   = null,
                            UInt64?                     ReferenceTimestamp    = null,
                            UInt64?                     OriginateTimestamp    = null,
                            UInt64?                     ReceiveTimestamp      = null,
                            UInt64?                     TransmitTimestamp     = null,
                            IEnumerable<NTPExtension>?  Extensions            = null)
    {

        #region Properties

        /// <summary>
        /// Leap Indicator (2 Bit, default: 0)
        /// </summary>
        public Byte                       LI                     { get; } = LI                  ?? 0;

        /// <summary>
        /// Version Number (3 Bit, default: 4)
        /// </summary>
        public Byte                       VN                     { get; } = VN                  ?? 4;

        /// <summary>
        /// Mode (3 Bit, client default: 3)
        /// </summary>
        public Byte                       Mode                   { get; } = Mode                ?? 3;

        /// <summary>
        /// Stratum (client default: 0)
        /// </summary>
        public Byte                       Stratum                { get; } = Stratum             ?? 0;

        /// <summary>
        /// Poll (2^n exponential value, e.g. 4 for 16 seconds)
        /// </summary>
        public Byte                       Poll                   { get; } = Poll                ?? 4;

        /// <summary>
        /// Precision (als Zweierkomplement, z. B. -6)
        /// </summary>
        public SByte                      Precision              { get; } = Precision           ?? -6;

        /// <summary>
        /// Root Delay (16.16 fixed point format)
        /// </summary>
        public UInt32                     RootDelay              { get; } = RootDelay           ?? 0;

        /// <summary>
        /// Root Dispersion (16.16 fixed point format)
        /// </summary>
        public UInt32                     RootDispersion         { get; } = RootDispersion      ?? 0;

        /// <summary>
        /// Reference Identifier (32 Bit)
        /// </summary>
        public UInt32                     ReferenceIdentifier    { get; } = ReferenceIdentifier ?? 0;

        /// <summary>
        /// Reference Timestamp (64 Bit)
        /// Default for clients: 0
        /// </summary>
        public UInt64                     ReferenceTimestamp     { get; } = ReferenceTimestamp  ?? 0;

        /// <summary>
        /// Originate Timestamp (64 Bit)
        /// Default for clients: 0
        /// </summary>
        public UInt64                     OriginateTimestamp     { get; } = OriginateTimestamp  ?? 0;

        /// <summary>
        /// Receive Timestamp (64 Bit)
        /// Default for clients: 0
        /// </summary>
        public UInt64                     ReceiveTimestamp       { get; } = ReceiveTimestamp    ?? 0;

        /// <summary>
        /// Transmit Timestamp (64 Bit)
        /// Will normally be set to the current time when the request is sent.
        /// </summary>
        public UInt64?                    TransmitTimestamp      { get; } = TransmitTimestamp;

        /// <summary>
        /// The optional enumeration of NTP extensions.
        /// </summary>
        public IEnumerable<NTPExtension>  Extensions             { get; } = Extensions          ?? [];

        #endregion

        #region Constructor(s)

        public NTPRequest(NTPRequest                  NTPRequest,
                          IEnumerable<NTPExtension>?  Extensions = null)

            : this(NTPRequest.LI,
                   NTPRequest.VN,
                   NTPRequest.Mode,
                   NTPRequest.Stratum,
                   NTPRequest.Poll,
                   NTPRequest.Precision,
                   NTPRequest.RootDelay,
                   NTPRequest.RootDispersion,
                   NTPRequest.ReferenceIdentifier,
                   NTPRequest.ReferenceTimestamp,
                   NTPRequest.OriginateTimestamp,
                   NTPRequest.ReceiveTimestamp,
                   NTPRequest.TransmitTimestamp,
                   Extensions)

        {

        }

        #endregion


        #region ToByteArray()

        /// <summary>
        /// Get a binary of the NTP request (big-endian).
        /// </summary>
        public Byte[] ToByteArray()
        {

            var buffer = new Byte[48];

            // Byte 0: LI (2 Bit), VN (3 Bit), Mode (3 Bit)
            buffer[0] = (Byte) (((LI & 0x03) << 6) | ((VN & 0x07) << 3) | (Mode & 0x07));
            buffer[1] = Stratum;
            buffer[2] = Poll;
            buffer[3] = (Byte) Precision;

            WriteUInt32BigEndian(buffer,  4, RootDelay);
            WriteUInt32BigEndian(buffer,  8, RootDispersion);
            WriteUInt32BigEndian(buffer, 12, ReferenceIdentifier);
            WriteUInt64BigEndian(buffer, 16, ReferenceTimestamp);
            WriteUInt64BigEndian(buffer, 24, OriginateTimestamp);
            WriteUInt64BigEndian(buffer, 32, ReceiveTimestamp);

            if (TransmitTimestamp.HasValue)
                WriteUInt64BigEndian(buffer, 40, TransmitTimestamp.Value);

            else
            {

                var ntpTimestamp = GetCurrentNTPTimestamp();

                // Bytes 40-47:  Transmit Timestamp as big-endian
                for (var i = 0; i < 8; i++)
                    buffer[40 + i] = (Byte) (ntpTimestamp >> (56 - i * 8));

            }

            if (Extensions.Any())
            {

                var bufferLength = buffer.Length;

                Array.Resize(ref buffer, bufferLength + Extensions.Sum(extension => extension.Length));
                var offset       = bufferLength;

                foreach (var extension in Extensions)
                {
                    Buffer.BlockCopy(extension.ToByteArray(), 0, buffer, offset, extension.Length);
                    offset += extension.Length;
                }

            }

            return buffer;

        }

        #endregion

        #region (static) WriteUInt32BigEndian(buffer, offset, value)

        /// <summary>
        /// Schreibt eine 32-Bit-Zahl im big-endian Format in den Buffer.
        /// </summary>
        private static void WriteUInt32BigEndian(byte[] buffer, int offset, uint value)
        {
            buffer[offset]     = (byte)((value >> 24) & 0xFF);
            buffer[offset + 1] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 3] = (byte)(value & 0xFF);
        }

        #endregion

        #region (static) WriteUInt64BigEndian(buffer, offset, value)

        /// <summary>
        /// Schreibt eine 64-Bit-Zahl im big-endian Format in den Buffer.
        /// </summary>
        private static void WriteUInt64BigEndian(byte[] buffer, int offset, ulong value)
        {
            buffer[offset]     = (byte)((value >> 56) & 0xFF);
            buffer[offset + 1] = (byte)((value >> 48) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 40) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 32) & 0xFF);
            buffer[offset + 4] = (byte)((value >> 24) & 0xFF);
            buffer[offset + 5] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 6] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 7] = (byte)(value & 0xFF);
        }

        #endregion

        #region (static) GetCurrentNTPTimestamp(Timestamp = null)

        /// <summary>
        /// Converts DateTime.UtcNow to a 64-bit NTP time format (seconds since 1900).
        /// The upper 32 bits contain the seconds, the lower 32 bits the fraction of a second as 32-bit fixed-point (2^32 is 1 second).
        /// </summary>
        /// <param name="Timestamp">An optional timestamp (UTC) to be converted to a NTP timestamp.</param>
        public static UInt64 GetCurrentNTPTimestamp(DateTime? Timestamp = null)
        {

            var ntpEpoch  = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var now       = Timestamp ?? DateTime.UtcNow;
            var ts        = now - ntpEpoch;

            var seconds   = (UInt64) ts.TotalSeconds;
            var fraction  = (UInt64) ((ts.TotalSeconds - seconds) * 0x100000000L);

            return (seconds << 32) | fraction;

        }

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

        #region TryParseNTPHeader(Buffer, out NTPResponse, out ErrorResponse, ExptectedUniqueId   = null)
        public static Boolean TryParse(Byte[]                                 Buffer,
                                       [NotNullWhen(true)]  out NTPResponse?  NTPResponse,
                                       [NotNullWhen(false)] out String?       ErrorResponse,
                                       Byte[]?                                ExptectedUniqueId   = null)
        {

            ErrorResponse = null;

            if (Buffer.Length < 48)
            {
                ErrorResponse = "Buffer too short!";
                NTPResponse   = null;
                return false;
            }

            var offset     = 48; // Start position of NTP extension fields
            var extensions = new List<NTPExtension>();

            while (offset + 4 <= Buffer.Length)
            {

                var type   = (UInt16) ((Buffer[offset]     << 8) | Buffer[offset + 1]);
                var length = (UInt16) ((Buffer[offset + 2] << 8) | Buffer[offset + 3]);

                if (length < 4)
                {
                    ErrorResponse  = $"Illegal length of extension {length} at offset {offset}!";
                    NTPResponse      = null;
                    return false;
                }

                if (offset + length > Buffer.Length)
                    break;

                var data = new Byte[length - 4];
                Array.Copy(Buffer, offset + 4, data, 0, length - 4);

                extensions.Add(
                    new NTPExtension(
                        type,
                        data
                    )
                );

                offset += length;

            }

            NTPResponse = new NTPResponse(
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
