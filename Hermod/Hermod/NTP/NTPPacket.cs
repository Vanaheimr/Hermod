﻿/*
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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.NTP
{

    // https://datatracker.ietf.org/doc/html/rfc4330 Simple Network Time Protocol (SNTP) Version 4
    // https://datatracker.ietf.org/doc/html/rfc5297 Synthetic Initialization Vector (SIV) Authenticated Encryption Using the Advanced Encryption Standard (AES)
    // https://datatracker.ietf.org/doc/html/rfc5905 Network Time Protocol Version 4: Protocol and Algorithms Specification
    // https://datatracker.ietf.org/doc/html/rfc7384 Security Requirements of Time Protocols in Packet Switched Networks
    // https://datatracker.ietf.org/doc/html/rfc7822 Network Time Protocol Version 4 (NTPv4) Extension Fields
    // https://datatracker.ietf.org/doc/html/rfc8915 Network Time Security for the Network Time Protocol

    // Stratum  Meaning
    //   ----------------------------------------------
    //   0        kiss-o'-death message (see below)
    //   1        primary reference (e.g., synchronized by radio clock)
    //   2-15     secondary reference (synchronized by NTP or SNTP)
    //   16-255   reserved


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
    public class NTPPacket(Byte?                       LI                     = null,
                           Byte?                       VN                     = null,
                           Byte?                       Mode                   = null,
                           Byte?                       Stratum                = null,
                           Byte?                       Poll                   = null,
                           SByte?                      Precision              = null,
                           UInt32?                     RootDelay              = null,
                           UInt32?                     RootDispersion         = null,
                           UInt32?                     ReferenceIdentifier    = null,
                           UInt64?                     ReferenceTimestamp     = null,
                           UInt64?                     OriginateTimestamp     = null,
                           UInt64?                     ReceiveTimestamp       = null,
                           UInt64?                     TransmitTimestamp      = null,
                           IEnumerable<NTPExtension>?  Extensions             = null,
                           Int32?                      KeyId                  = null,
                           Byte[]?                     MessageDigest          = null,
                           UInt64?                     DestinationTimestamp   = null)
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

        // Code       External Reference Source
        // ------------------------------------------------------------------
        // LOCL       uncalibrated local clock
        // CESM       calibrated Cesium clock
        // RBDM       calibrated Rubidium clock
        // PPS        calibrated quartz clock or other pulse-per-second
        //            source
        // IRIG       Inter-Range Instrumentation Group
        // ACTS       NIST telephone modem service
        // USNO       USNO telephone modem service
        // PTB        PTB (Germany) telephone modem service
        // TDF        Allouis (France) Radio 164 kHz
        // DCF        Mainflingen (Germany) Radio 77.5 kHz
        // MSF        Rugby (UK) Radio 60 kHz
        // WWV        Ft. Collins (US) Radio 2.5, 5, 10, 15, 20 MHz
        // WWVB       Boulder (US) Radio 60 kHz
        // WWVH       Kauai Hawaii (US) Radio 2.5, 5, 10, 15 MHz
        // CHU        Ottawa (Canada) Radio 3330, 7335, 14670 kHz
        // LORC       LORAN-C radionavigation system
        // OMEG       OMEGA radionavigation system
        // GPS        Global Positioning Service

        /// <summary>
        /// Reference Identifier (32 Bit)
        /// This is a 32-bit bitstring identifying the
        /// particular reference source.This field is significant only in
        /// server messages, where for stratum 0 (kiss-o'-death message) and 1
        /// (primary server), the value is a four-character ASCII string, left
        /// justified and zero padded to 32 bits.For IPv4 secondary servers,
        /// the value is the 32-bit IPv4 address of the synchronization source.
        /// For IPv6 and OSI secondary servers, the value is the first 32 bits of
        /// the MD5 hash of the IPv6 or NSAP address of the synchronization
        /// source.
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

        /// <summary>
        /// The value of an optional UniqueIdentifier extension.
        /// </summary>
        public Byte[]?                    UniqueIdentifier
            => Extensions.FirstOrDefault(ext => ext.Type == ExtensionTypes.UniqueIdentifier)?.Value;


        /// <summary>
        /// Optional 4 byte key identification
        /// </summary>
        public Int32?                     KeyId                  { get; } = KeyId;

        /// <summary>
        /// Optional 16 byte message digest
        /// </summary>
        public Byte[]?                    MessageDigest          { get; } = MessageDigest;

        /// <summary>
        /// Optional 64 bit destination timestamp
        /// Note: This timestamp is not part of the packet itself!
        /// It is captured upon arrival and returned in the receive buffer along with the buffer length and data.
        /// </summary>
        public UInt64?                    DestinationTimestamp   { get; } = DestinationTimestamp;

        #endregion

        #region Constructor(s)

        public NTPPacket(NTPPacket                   NTPRequest,
                         IEnumerable<NTPExtension>?  Extensions   = null)

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

        #region TryParseRequest(Buffer, out NTPPacket, out ErrorResponse, NTSKey = null, ExptectedUniqueId = null)

        public static Boolean TryParseRequest(Byte[]                               Buffer,
                                              [NotNullWhen(true)]  out NTPPacket?  NTPPacket,
                                              [NotNullWhen(false)] out String?     ErrorResponse,
                                              Byte[]?                              NTSKey             = null,
                                              Byte[]?                              ExpectedUniqueId   = null,
                                              Byte[]?                              Nonce              = null)
        {

            #region Initial checks

            ErrorResponse = null;
            NTPPacket     = null;

            if (Buffer.Length < 48)
            {
                ErrorResponse = "The NTP request is too short!";
                NTPPacket     = null;
                return false;
            }

            #endregion

            var ntpPacketBytes = new Byte[48];
            Array.Copy(Buffer, ntpPacketBytes, 48);
            var things         = new List<Byte[]>() { ntpPacketBytes };

            #region Parse Extensions

            var offset     = 48;
            var extensions = new List<NTPExtension>();

            while (offset + 4 <= Buffer.Length)
            {

                var type   = (ExtensionTypes) ((Buffer[offset]     << 8) | Buffer[offset + 1]);
                var length = (UInt16)         ((Buffer[offset + 2] << 8) | Buffer[offset + 3]);

                if (length < 4)
                {
                    ErrorResponse  = $"Illegal length of extension {length} at offset {offset}!";
                    NTPPacket      = null;
                    return false;
                }

                if (offset + length > Buffer.Length)
                    break;

                var copy = new Byte[length];
                Array.Copy(Buffer, offset, copy, 0, length);
                things.Add(copy);

                var data = new Byte[length - 4];
                Array.Copy(Buffer, offset + 4, data, 0, length - 4);

                switch (type)
                {

                    case ExtensionTypes.UniqueIdentifier:
                        var uid = new UniqueIdentifierExtension(data);
                        if (ExpectedUniqueId is not null &&
                            !uid.Value.SequenceEqual(ExpectedUniqueId))
                        {
                            ErrorResponse = $"Unexpected UniqueIdentifier '{uid.Value}' != '{ExpectedUniqueId}'!";
                            return false;
                        }
                        extensions.Add(uid);
                        break;

                    case ExtensionTypes.NTSCookie:
                        extensions.Add(
                            new NTSCookieExtension(data)
                        );
                        break;

                    case ExtensionTypes.NTSCookiePlaceholder:
                        extensions.Add(
                            new NTSCookiePlaceholderExtension(100) // Nonsense!
                        );
                        break;

                    case ExtensionTypes.AuthenticatorAndEncrypted:
                        if (NTSKey is null)
                        {
                            ErrorResponse = "Missing NTS key!";
                            return false;
                        }
                        if (!AuthenticatorAndEncryptedExtension.TryParse(data,
                                                                         things.Take(things.Count-1),
                                                                         ref extensions,
                                                                         NTSKey,
                                                                         out var authenticatorAndEncryptedExtension,
                                                                         out ErrorResponse))
                        {
                            return false;
                        }
                        extensions.Add(authenticatorAndEncryptedExtension);

                        if (authenticatorAndEncryptedExtension.EncryptedExtensions.Any())
                            extensions.AddRange(authenticatorAndEncryptedExtension.EncryptedExtensions);

                        break;

                    case ExtensionTypes.Debug:
                        if (!DebugExtension.TryParse(data, out var debugExtension, out ErrorResponse))
                        {
                            return false;
                        }
                        extensions.Add(debugExtension);
                        break;

                    default:
                        extensions.Add(
                            new NTPExtension(
                                type,
                                data
                            )
                        );
                        break;

                }

                offset += length;

            }

            #endregion

            #region Parse NTP packet

            NTPPacket = new NTPPacket(

                            LI:                   (Byte) ((Buffer[0] >> 6) & 0x03),
                            VN:                   (Byte) ((Buffer[0] >> 3) & 0x07),
                            Mode:                 (Byte)  (Buffer[0]       & 0x07),
                            Stratum:              Buffer[1],
                            Poll:                 Buffer[2],
                            Precision:            (SByte) Buffer[3],
                            RootDelay:            (UInt32) ((Buffer[4]  << 24) | (Buffer[5]  << 16) | (Buffer[6]  << 8) | Buffer[7]),
                            RootDispersion:       (UInt32) ((Buffer[8]  << 24) | (Buffer[9]  << 16) | (Buffer[10] << 8) | Buffer[11]),
                            ReferenceIdentifier:  (UInt32) ((Buffer[12] << 24) | (Buffer[13] << 16) | (Buffer[14] << 8) | Buffer[15]),
                            ReferenceTimestamp:   ReadUInt64(Buffer, 16),
                            OriginateTimestamp:   ReadUInt64(Buffer, 24),
                            ReceiveTimestamp:     ReadUInt64(Buffer, 32),
                            TransmitTimestamp:    ReadUInt64(Buffer, 40),

                            Extensions:           extensions

                        );

            #endregion

            return true;

        }

        #endregion

        #region TryParseResponse(Buffer, out NTPPacket, out ErrorResponse, NTSKey = null, ExptectedUniqueId = null)

        public static Boolean TryParseResponse(Byte[]                               Buffer,
                                               [NotNullWhen(true)]  out NTPPacket?  NTPPacket,
                                               [NotNullWhen(false)] out String?     ErrorResponse,
                                               Byte[]?                              NTSKey             = null,
                                               Byte[]?                              ExpectedUniqueId   = null,
                                               Byte[]?                              Nonce              = null)
        {

            #region Initial checks

            ErrorResponse = null;
            NTPPacket     = null;

            if (Buffer.Length < 48)
            {
                ErrorResponse = "The NTP response is too short!";
                NTPPacket     = null;
                return false;
            }

            #endregion

            var ntpPacketBytes = new Byte[48];
            Array.Copy(Buffer, ntpPacketBytes, 48);
            var things         = new List<Byte[]>() { ntpPacketBytes };

            #region Parse Extensions

            var offset     = 48;
            var extensions = new List<NTPExtension>();

            while (offset + 4 <= Buffer.Length)
            {

                var type   = (ExtensionTypes) ((Buffer[offset]     << 8) | Buffer[offset + 1]);
                var length = (UInt16)         ((Buffer[offset + 2] << 8) | Buffer[offset + 3]);

                if (length < 4)
                {
                    ErrorResponse  = $"Illegal length of extension {length} at offset {offset}!";
                    NTPPacket      = null;
                    return false;
                }

                if (offset + length > Buffer.Length)
                    break;

                var copy = new Byte[length];
                Array.Copy(Buffer, offset, copy, 0, length);
                things.Add(copy);

                var data = new Byte[length - 4];
                Array.Copy(Buffer, offset + 4, data, 0, length - 4);

                switch (type)
                {

                    case ExtensionTypes.UniqueIdentifier:
                        var uid = new UniqueIdentifierExtension(data);
                        if (ExpectedUniqueId is not null &&
                            !uid.Value.SequenceEqual(ExpectedUniqueId))
                        {
                            ErrorResponse = $"Unexpected UniqueIdentifier '{uid.Value}' != '{ExpectedUniqueId}'!";
                            return false;
                        }
                        extensions.Add(uid);
                        break;

                    case ExtensionTypes.NTSCookie:
                        extensions.Add(
                            new NTSCookieExtension(data)
                        );
                        break;

                    case ExtensionTypes.NTSCookiePlaceholder:
                        extensions.Add(
                            new NTSCookiePlaceholderExtension(100) // Nonsense!
                        );
                        break;

                    case ExtensionTypes.AuthenticatorAndEncrypted:
                        if (NTSKey is null)
                        {
                            ErrorResponse = "Missing NTS key!";
                            return false;
                        }
                        if (!AuthenticatorAndEncryptedExtension.TryParse(data,
                                                                         things.Take(things.Count-1),
                                                                         ref extensions,
                                                                         NTSKey,
                                                                         out var authenticatorAndEncryptedExtension,
                                                                         out ErrorResponse))
                        {
                            return false;
                        }
                        extensions.Add(authenticatorAndEncryptedExtension);

                        if (authenticatorAndEncryptedExtension.EncryptedExtensions.Any())
                            extensions.AddRange(authenticatorAndEncryptedExtension.EncryptedExtensions);

                        break;

                    case ExtensionTypes.Debug:
                        if (!DebugExtension.TryParse(data, out var debugExtension, out ErrorResponse))
                        {
                            return false;
                        }
                        extensions.Add(debugExtension);
                        break;

                    default:
                        extensions.Add(
                            new NTPExtension(
                                type,
                                data
                            )
                        );
                        break;

                }

                offset += length;

            }

            #endregion

            #region Parse NTP packet

            NTPPacket = new NTPPacket(

                            LI:                   (Byte) ((Buffer[0] >> 6) & 0x03),
                            VN:                   (Byte) ((Buffer[0] >> 3) & 0x07),
                            Mode:                 (Byte)  (Buffer[0]       & 0x07),
                            Stratum:              Buffer[1],
                            Poll:                 Buffer[2],
                            Precision:            (SByte) Buffer[3],
                            RootDelay:            (UInt32) ((Buffer[4]  << 24) | (Buffer[5]  << 16) | (Buffer[6]  << 8) | Buffer[7]),
                            RootDispersion:       (UInt32) ((Buffer[8]  << 24) | (Buffer[9]  << 16) | (Buffer[10] << 8) | Buffer[11]),
                            ReferenceIdentifier:  (UInt32) ((Buffer[12] << 24) | (Buffer[13] << 16) | (Buffer[14] << 8) | Buffer[15]),
                            ReferenceTimestamp:   ReadUInt64(Buffer, 16),
                            OriginateTimestamp:   ReadUInt64(Buffer, 24),
                            ReceiveTimestamp:     ReadUInt64(Buffer, 32),
                            TransmitTimestamp:    ReadUInt64(Buffer, 40),

                            Extensions:           extensions

                        );

            #endregion

            #region Parse Kiss-o'-Death

            if (NTPPacket.Stratum == 0)
            {

                var bytes = BitConverter.GetBytes(NTPPacket.ReferenceIdentifier);

                if (BitConverter.IsLittleEndian)
                    Array.Reverse(bytes);

                var error = System.Text.Encoding.ASCII.GetString(bytes);

                ErrorResponse = error switch {

                    // https://datatracker.ietf.org/doc/html/rfc5905
                    // 7.4. The Kiss-o'-Death Packet
                    "ACST" => $"'{error}' The association belongs to a unicast server.",
                    "AUTH" => $"'{error}' Server authentication failed.",
                    "AUTO" => $"'{error}' Autokey sequence failed.",
                    "BCST" => $"'{error}' The association belongs to a broadcast server.",
                    "CRYP" => $"'{error}' Cryptographic authentication or identification failed.",
                    "DENY" => $"'{error}' Access denied by remote server.",
                    "DROP" => $"'{error}' Lost peer in symmetric mode.",
                    "RSTR" => $"'{error}' Access denied due to local policy.",
                    "INIT" => $"'{error}' The association has not yet synchronized for the first time.",
                    "MCST" => $"'{error}' The association belongs to a dynamically discovered server.",
                    "NKEY" => $"'{error}' No key found.Either the key was never installed or is not trusted.",
                    "RATE" => $"'{error}' Rate exceeded.The server has temporarily denied access because the client exceeded the rate threshold.",
                    "RMOT" => $"'{error}' Alteration of association from a remote host running ntpdc.",
                    "STEP" => $"'{error}' A step change in system time has occurred, but the association has not yet resynchronized.",

                    // https://datatracker.ietf.org/doc/html/rfc8915
                    // 5.7. Protocol Details
                    "NTSN" => $"'{error}' NTS Negative Acknowledgment (NAK).",

                     _     => $"'{error}' Unknown error!",

                };

                return false;

            }

            #endregion

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
