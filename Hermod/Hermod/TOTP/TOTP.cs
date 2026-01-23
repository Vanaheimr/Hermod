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
using System.Security.Cryptography;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// Generate Time-based One-Time Passwords (TOTPs) to harden TCP/TLS connections when
    /// mutual TLS (mTLS) is not available or not practical. This implementation supports
    /// custom alphabets and variable token lengths, and can optionally incorporate TLS
    /// v1.3 key exporter material to bind the token to a specific TLS session (TLS channel
    /// binding). With channel binding enabled, a captured or logged TOTP becomes effectively
    /// useless outside the original TLS session, which makes replay attacks impractical
    /// even if the token leaks.
    /// 
    /// Classic TOTP per RFC 6238 typically emits 6–8 decimal digits (0–9) because it is
    /// designed for human entry. Here, the token is used exclusively for machine-to-machine
    /// authentication, so we deliberately expand the design space: a larger alphabet
    /// (e.g., Base32/Base62/custom) and a longer default length increase brute-force cost
    /// without harming usability.
    /// 
    /// Typical use cases include:
    ///  - OCPP QR-Code based payment/authorization flows, e.g. where a short-lived
    ///    token is embedded in a QR Code shown on a charging station display and validated
    ///    server-side to secure the server infrastructure against misuse and attacks.
    ///  - A drop-in replacement for legacy HTTP Basic Auth in constrained environments
    ///  - Strengthening e.g. OCPI token-style authentication by adding time-limiting and
    ///    optionally TLS session binding, rather than relying on long-lived static
    ///    identifiers.
    /// </summary>
    public static class TOTPGenerator
    {

        /// <summary>
        /// The default TLS exporter label for TOTP generation with TLS v1.3 channel binding.
        /// </summary>
        public const String TLSExporterLabel = "EXPORTER-Time-Based-One-Time-Password-v1";


        #region (private) CalcTOTPSlot(CurrentSlot, TOTPLength, Alphabet, HMAC, TLSExporterMaterial = null)

        private static String CalcTOTPSlot(UInt64      CurrentSlot,
                                           UInt32      TOTPLength,
                                           String      Alphabet,
                                           HMACSHA256  HMAC,
                                           Byte[]?     TLSExporterMaterial = null)
        {

            var slotBytes = BitConverter.GetBytes(CurrentSlot);

            // .NET uses little-endian byte order!
            if (BitConverter.IsLittleEndian)
                Array.Reverse(slotBytes);

            var currentHash = TLSExporterMaterial?.Length > 0
                                  ? new HMACSHA256(TLSExporterMaterial).ComputeHash(HMAC.ComputeHash(slotBytes))
                                  : HMAC.ComputeHash(slotBytes);

            var stringBuilder  = new StringBuilder((Int32) TOTPLength);

            //ToDo: Dynamic truncation method like as described in RFC 4226?

            // For additional security start at a random offset
            // based on the last bit of the hash value (see RFCs)
            var offset         = currentHash[^1] & 0x0F;

            for (var i = 0; i < TOTPLength; i++)
                stringBuilder.Append(Alphabet[currentHash[(offset + i) % currentHash.Length] % Alphabet.Length]);

            return stringBuilder.ToString();

        }

        #endregion

        #region GenerateTOTP  (           SharedSecret, ValidityTime = null, TOTPLength = 12, Alphabet = null, Timestamp = null, TLSExporterMaterial = null)

        /// <summary>
        /// Calculate the current TOTP and the remaining time until the TOTP will change.
        /// </summary>
        /// <param name="SharedSecret">The TOTP shared secret.</param>
        /// <param name="ValidityTime">The optional validity time of the TOTP.</param>
        /// <param name="TOTPLength">The optional expected length of the TOTP.</param>
        /// <param name="Alphabet">The optional alphabet of the TOTP being used.</param>
        /// <param name="Timestamp">The optional timestamp to calculate the TOTP for.</param>
        /// <param name="TLSExporterMaterial">Optional TLS exporter material for additional security.</param>
        public static (String          Current,
                       TimeSpan        RemainingTime,
                       DateTimeOffset  EndTime)

            GenerateTOTP(String           SharedSecret,
                         TimeSpan?        ValidityTime          = null,
                         UInt32?          TOTPLength            = 12,
                         String?          Alphabet              = null,
                         DateTimeOffset?  Timestamp             = null,
                         Byte[]?          TLSExporterMaterial   = null)

        {

            #region Initial Checks

            SharedSecret   = SharedSecret.Trim();
            ValidityTime ??= TimeSpan.FromSeconds(30);
            TOTPLength   ??= 12;
            Alphabet     ??= "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            Alphabet       = Alphabet.Trim();

            if (String.IsNullOrEmpty(SharedSecret))
                throw new ArgumentNullException(nameof(SharedSecret),
                                                "The given shared secret must not be null or empty!");

            if (SharedSecret.Any(Char.IsWhiteSpace))
                throw new ArgumentException    ("The given shared secret must not contain any whitespace characters!",
                                                nameof(Alphabet));

            if (SharedSecret.Length < 16)
                throw new ArgumentException    ("The length of the given shared secret must be at least 16 characters!",
                                                nameof(Alphabet));

            if (TOTPLength < 4)
                throw new ArgumentException    ("The expected length of the TOTP must be between 4 and 255 characters!",
                                                nameof(Alphabet));

            if (String.IsNullOrEmpty(Alphabet))
                throw new ArgumentNullException(nameof(Alphabet),
                                                "The given alphabet must not be null or empty!");

            if (Alphabet.Length < 4)
                throw new ArgumentException    ("The given alphabet must contain at least 4 characters!",
                                                nameof(Alphabet));

            if (Alphabet.Length != Alphabet.Distinct().Count())
                throw new ArgumentException    ("The given alphabet must not contain duplicate characters!",
                                                nameof(Alphabet));

            if (Alphabet.Any(Char.IsWhiteSpace))
                throw new ArgumentException    ("The given alphabet must not contain any whitespace characters!",
                                                nameof(Alphabet));

            #endregion

            using var hmac       = new HMACSHA256(
                                       Encoding.UTF8.GetBytes(SharedSecret)
                                   );

            var timeReference    = Timestamp ?? DateTimeOffset.UtcNow;
            var currentUnixTime  = timeReference.ToUnixTimeSeconds();
            var currentSlot      = (UInt64) (currentUnixTime / ValidityTime.Value.TotalSeconds);
            var remainingTime    = TimeSpan.FromSeconds(
                                       (Int32) ValidityTime.Value.TotalSeconds
                                         -
                                       (currentUnixTime % (Int32) ValidityTime.Value.TotalSeconds)
                                   );

            return (CalcTOTPSlot(
                        currentSlot,
                        TOTPLength.Value,
                        Alphabet,
                        hmac,
                        TLSExporterMaterial
                    ),
                    remainingTime,
                    timeReference + remainingTime);

        }

        #endregion

        #region GenerateTOTPs (           SharedSecret, ValidityTime = null, TOTPLength = 12, Alphabet = null, Timestamp = null, TLSExporterMaterial = null)

        /// <summary>
        /// Calculate TOTPs and the remaining time until the TOTPs will change.
        /// </summary>
        /// <param name="SharedSecret">The TOTP shared secret.</param>
        /// <param name="ValidityTime">The optional validity time of the TOTP.</param>
        /// <param name="TOTPLength">The optional expected length of the TOTP.</param>
        /// <param name="Alphabet">The optional alphabet of the TOTP being used.</param>
        /// <param name="Timestamp">The optional timestamp to calculate the TOTP for.</param>
        /// <param name="TLSExporterMaterial">Optional TLS exporter material for additional security.</param>
        public static (String          Previous,
                       String          Current,
                       String          Next,
                       TimeSpan        RemainingTime,
                       DateTimeOffset  EndTime)

            GenerateTOTPs(String           SharedSecret,
                          TimeSpan?        ValidityTime          = null,
                          UInt32?          TOTPLength            = 12,
                          String?          Alphabet              = null,
                          DateTimeOffset?  Timestamp             = null,
                          Byte[]?          TLSExporterMaterial   = null)

        {

            #region Initial Checks

            SharedSecret   = SharedSecret.Trim();
            ValidityTime ??= TimeSpan.FromSeconds(30);
            TOTPLength   ??= 12;
            Alphabet     ??= "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            Alphabet       = Alphabet.Trim();

            if (String.IsNullOrEmpty(SharedSecret))
                throw new ArgumentNullException(nameof(SharedSecret),
                                                "The given shared secret must not be null or empty!");

            if (SharedSecret.Any(Char.IsWhiteSpace))
                throw new ArgumentException    ("The given shared secret must not contain any whitespace characters!",
                                                nameof(Alphabet));

            if (SharedSecret.Length < 16)
                throw new ArgumentException    ("The length of the given shared secret must be at least 16 characters!",
                                                nameof(Alphabet));

            if (TOTPLength < 4)
                throw new ArgumentException    ("The expected length of the TOTP must be between 4 and 255 characters!",
                                                nameof(Alphabet));

            if (String.IsNullOrEmpty(Alphabet))
                throw new ArgumentNullException(nameof(Alphabet),
                                                "The given alphabet must not be null or empty!");

            if (Alphabet.Length < 4)
                throw new ArgumentException    ("The given alphabet must contain at least 4 characters!",
                                                nameof(Alphabet));

            if (Alphabet.Length != Alphabet.Distinct().Count())
                throw new ArgumentException    ("The given alphabet must not contain duplicate characters!",
                                                nameof(Alphabet));

            if (Alphabet.Any(Char.IsWhiteSpace))
                throw new ArgumentException    ("The given alphabet must not contain any whitespace characters!",
                                                nameof(Alphabet));

            #endregion

            using var hmac       = new HMACSHA256(
                                       Encoding.UTF8.GetBytes(SharedSecret)
                                   );

            var timeReference    = Timestamp ?? DateTimeOffset.UtcNow;
            var currentUnixTime  = timeReference.ToUnixTimeSeconds();
            var currentSlot      = (UInt64) (currentUnixTime / ValidityTime.Value.TotalSeconds);
            var remainingTime    = TimeSpan.FromSeconds(
                                       (Int32) ValidityTime.Value.TotalSeconds
                                         -
                                       (currentUnixTime % (Int32) ValidityTime.Value.TotalSeconds)
                                   );

            return (CalcTOTPSlot(currentSlot - 1, TOTPLength.Value, Alphabet, hmac, TLSExporterMaterial),
                    CalcTOTPSlot(currentSlot,     TOTPLength.Value, Alphabet, hmac, TLSExporterMaterial),
                    CalcTOTPSlot(currentSlot + 1, TOTPLength.Value, Alphabet, hmac, TLSExporterMaterial),
                    remainingTime,
                    timeReference + remainingTime);

        }

        #endregion

        #region GenerateTOTP  (Timestamp, SharedSecret, ValidityTime = null, TOTPLength = 12, Alphabet = null,                   TLSExporterMaterial = null)

        /// <summary>
        /// Calculate the current TOTP and the remaining time until the TOTP will change.
        /// </summary>
        /// <param name="Timestamp">The timestamp to calculate the TOTP for.</param>
        /// <param name="SharedSecret">The TOTP shared secret.</param>
        /// <param name="ValidityTime">The optional validity time of the TOTP.</param>
        /// <param name="TOTPLength">The optional expected length of the TOTP.</param>
        /// <param name="Alphabet">The optional alphabet of the TOTP being used.</param>
        /// <param name="TLSExporterMaterial">Optional TLS exporter material for additional security.</param>
        public static (String          Current,
                       TimeSpan        RemainingTime,
                       DateTimeOffset  EndTime)

            GenerateTOTP (DateTimeOffset  Timestamp,
                          String          SharedSecret,
                          TimeSpan?       ValidityTime          = null,
                          UInt32?         TOTPLength            = 12,
                          String?         Alphabet              = null,
                          Byte[]?         TLSExporterMaterial   = null)

                => GenerateTOTP(
                       SharedSecret,
                       ValidityTime,
                       TOTPLength,
                       Alphabet,
                       Timestamp,
                       TLSExporterMaterial
                   );

        #endregion

        #region GenerateTOTPs (Timestamp, SharedSecret, ValidityTime = null, TOTPLength = 12, Alphabet = null,                   TLSExporterMaterial = null)

        /// <summary>
        /// Calculate TOTPs and the remaining time until the TOTPs will change.
        /// </summary>
        /// <param name="Timestamp">The timestamp to calculate the TOTP for.</param>
        /// <param name="SharedSecret">The TOTP shared secret.</param>
        /// <param name="ValidityTime">The optional validity time of the TOTP.</param>
        /// <param name="TOTPLength">The optional expected length of the TOTP.</param>
        /// <param name="Alphabet">The optional alphabet of the TOTP being used.</param>
        /// <param name="TLSExporterMaterial">Optional TLS exporter material for additional security.</param>
        public static (String          Previous,
                       String          Current,
                       String          Next,
                       TimeSpan        RemainingTime,
                       DateTimeOffset  EndTime)

            GenerateTOTPs(DateTimeOffset  Timestamp,
                          String          SharedSecret,
                          TimeSpan?       ValidityTime          = null,
                          UInt32?         TOTPLength            = 12,
                          String?         Alphabet              = null,
                          Byte[]?         TLSExporterMaterial   = null)

                => GenerateTOTPs(
                       SharedSecret,
                       ValidityTime,
                       TOTPLength,
                       Alphabet,
                       Timestamp,
                       TLSExporterMaterial
                   );

        #endregion


        #region (private) ProcessURLTemplate (URLTemplate, TOTP, Version = null, EVSEId = null, ...)

        /// <summary>
        /// Processes the URL template and replaces all template parameters with the given values.
        /// </summary>
        /// <param name="URLTemplate">The URL template.</param>
        /// <param name="TOTP">The time-based one-time password.</param>
        /// <param name="Version">The version of the URL template.</param>
        /// <param name="EVSEId">The OCPP EVSE identification (1, 2, ...).</param>
        /// <param name="ConnectorId">The OCPP Connector identification (1, 2, ...).</param>
        /// <param name="EVRoamingEVSEId">The EV Roaming EVSE Id (e.g. DE*GEF*E12345678*1)</param>
        /// <param name="MaxEnergy">The maximum energy to be charged.</param>
        /// <param name="MaxTime">The maximum time to be charged.</param>
        /// <param name="MaxSoC">The maximum state of charge to be charged.</param>
        /// <param name="TariffId">The tariff identification.</param>
        /// <param name="MaxCost">The maximum cost to be charged.</param>
        /// <param name="ChargingProfile">The charging profile.</param>
        /// <param name="EndTime">The end time of the charging process.</param>
        /// <param name="MaxPower">The charging speed.</param>
        /// <param name="UILanguage">The user interface language.</param>
        /// <param name="Currency">The currency.</param>
        /// <param name="Signature">The digital signature for the URL.</param>
        private static String ProcessURLTemplate(URL          URLTemplate,
                                                 String       TOTP,
                                                 String?      Version           = null,
                                                 String?      EVSEId            = null,
                                                 String?      ConnectorId       = null,
                                                 String?      EVRoamingEVSEId   = null,
                                                 Watt?        MaxPower          = null,
                                                 WattHour?    MaxEnergy         = null,
                                                 TimeSpan?    MaxTime           = null,
                                                 Percentage?  MaxSoC            = null,
                                                 String?      MaxCost           = null,
                                                 String?      TariffId          = null,
                                                 String?      ChargingProfile   = null,
                                                 String?      EndTime           = null,
                                                 String?      UILanguage        = null,
                                                 String?      Currency          = null,
                                                 String?      Signature         = null)

            => URLTemplate.ToString().
                           Replace("{TOTP}",             TOTP).
                           Replace("{version}",          Version                            ?? "", StringComparison.OrdinalIgnoreCase).
                           Replace("{evseId}",           EVSEId                             ?? "", StringComparison.OrdinalIgnoreCase).
                           Replace("{connectorId}",      ConnectorId                        ?? "", StringComparison.OrdinalIgnoreCase).
                           Replace("{evRoamingEVSEId}",  EVRoamingEVSEId                    ?? "", StringComparison.OrdinalIgnoreCase).
                           Replace("{maxPower}",         MaxPower?. kW.          ToString() ?? "", StringComparison.OrdinalIgnoreCase).
                           Replace("{maxEnergy}",        MaxEnergy?.kWh.         ToString() ?? "", StringComparison.OrdinalIgnoreCase).
                           Replace("{maxTime}",          MaxTime?.  TotalMinutes.ToString() ?? "", StringComparison.OrdinalIgnoreCase).
                           Replace("{maxSoC}",           MaxSoC?.   Value.       ToString() ?? "", StringComparison.OrdinalIgnoreCase).
                           Replace("{maxCost}",          MaxCost                            ?? "", StringComparison.OrdinalIgnoreCase).
                           Replace("{tariffId}",         TariffId                           ?? "", StringComparison.OrdinalIgnoreCase).
                           Replace("{chargingProfile}",  ChargingProfile                    ?? "", StringComparison.OrdinalIgnoreCase).
                           Replace("{endTime}",          EndTime                            ?? "", StringComparison.OrdinalIgnoreCase).
                           Replace("{uiLanguage}",       UILanguage                         ?? "", StringComparison.OrdinalIgnoreCase).
                           Replace("{currency}",         Currency                           ?? "", StringComparison.OrdinalIgnoreCase).
                           Replace("{signature}",        Signature                          ?? "", StringComparison.OrdinalIgnoreCase);

        #endregion

        #region GenerateURL  (           URLTemplate, SharedSecret, ValidityTime = null, TOTPLength = 12, Alphabet = null, Timestamp = null, TLSExporterMaterial = null)

        /// <summary>
        /// Calculate the current TOTP URL and the remaining time until the URL will change.
        /// </summary>
        /// <param name="URLTemplate">The TOTP URL template.</param>
        /// <param name="SharedSecret">The TOTP shared secret.</param>
        /// <param name="ValidityTime">The optional validity time of the TOTP.</param>
        /// <param name="TOTPLength">The optional expected length of the TOTP.</param>
        /// <param name="Alphabet">The optional alphabet of the TOTP being used.</param>
        /// <param name="Timestamp">The optional timestamp to calculate the TOTP for.</param>
        /// <param name="TLSExporterMaterial">Optional TLS exporter material for additional security.</param>
        public static (String          Current,
                       TimeSpan        RemainingTime,
                       DateTimeOffset  EndTime)

            GenerateURL(URL              URLTemplate,
                        String           SharedSecret,
                        TimeSpan?        ValidityTime          = null,
                        UInt32?          TOTPLength            = 12,
                        String?          Alphabet              = null,
                        DateTimeOffset?  Timestamp             = null,
                        Byte[]?          TLSExporterMaterial   = null)

        {

            var (currentTOTP,
                 remainingTime,
                 endTime) = GenerateTOTP(
                                SharedSecret,
                                ValidityTime,
                                TOTPLength,
                                Alphabet,
                                Timestamp,
                                TLSExporterMaterial
                            );

            return (
                       ProcessURLTemplate(URLTemplate, currentTOTP),
                       remainingTime,
                       endTime
                   );

        }

        #endregion

        #region GenerateURLs (           URLTemplate, SharedSecret, ValidityTime = null, TOTPLength = 12, Alphabet = null, Timestamp = null, TLSExporterMaterial = null)

        /// <summary>
        /// Calculate the TOTP URLs and the remaining time until the URLs will change.
        /// </summary>
        /// <param name="URLTemplate">The TOTP URL template.</param>
        /// <param name="SharedSecret">The TOTP shared secret.</param>
        /// <param name="ValidityTime">The optional validity time of the TOTP.</param>
        /// <param name="TOTPLength">The optional expected length of the TOTP.</param>
        /// <param name="Alphabet">The optional alphabet of the TOTP being used.</param>
        /// <param name="Timestamp">The optional timestamp to calculate the TOTP for.</param>
        /// <param name="TLSExporterMaterial">Optional TLS exporter material for additional security.</param>
        public static (String          Previous,
                       String          Current,
                       String          Next,
                       TimeSpan        RemainingTime,
                       DateTimeOffset  EndTime)

            GenerateURLs(URL              URLTemplate,
                         String           SharedSecret,
                         TimeSpan?        ValidityTime          = null,
                         UInt32?          TOTPLength            = 12,
                         String?          Alphabet              = null,
                         DateTimeOffset?  Timestamp             = null,
                         Byte[]?          TLSExporterMaterial   = null)

        {

            var (previousTOTP,
                 currentTOTP,
                 nextTOTP,
                 remainingTime,
                 endTime) = GenerateTOTPs(
                                SharedSecret,
                                ValidityTime,
                                TOTPLength,
                                Alphabet,
                                Timestamp,
                                TLSExporterMaterial
                            );

            return (
                       ProcessURLTemplate(URLTemplate, previousTOTP),
                       ProcessURLTemplate(URLTemplate, currentTOTP),
                       ProcessURLTemplate(URLTemplate, nextTOTP),
                       remainingTime,
                       endTime
                   );

        }

        #endregion

        #region GenerateURL  (Timestamp, URLTemplate, SharedSecret, ValidityTime = null, TOTPLength = 12, Alphabet = null,                   TLSExporterMaterial = null)

        /// <summary>
        /// Calculate the current TOTP URL and the remaining time until the URL will change.
        /// </summary>
        /// <param name="Timestamp">The timestamp to calculate the TOTP for.</param>
        /// <param name="URLTemplate">The TOTP URL template.</param>
        /// <param name="SharedSecret">The TOTP shared secret.</param>
        /// <param name="ValidityTime">The optional validity time of the TOTP.</param>
        /// <param name="TOTPLength">The optional expected length of the TOTP.</param>
        /// <param name="Alphabet">The optional alphabet of the TOTP being used.</param>
        /// <param name="TLSExporterMaterial">Optional TLS exporter material for additional security.</param>
        public static (String          Current,
                       TimeSpan        RemainingTime,
                       DateTimeOffset  EndTime)

            GenerateURL(DateTimeOffset  Timestamp,
                        URL             URLTemplate,
                        String          SharedSecret,
                        TimeSpan?       ValidityTime          = null,
                        UInt32?         TOTPLength            = 12,
                        String?         Alphabet              = null,
                        Byte[]?         TLSExporterMaterial   = null)

        {

            var (currentTOTP,
                 remainingTime,
                 endTime) = GenerateTOTP(
                                SharedSecret,
                                ValidityTime,
                                TOTPLength,
                                Alphabet,
                                Timestamp,
                                TLSExporterMaterial
                            );

            return (
                       ProcessURLTemplate(URLTemplate, currentTOTP),
                       remainingTime,
                       endTime
                   );

        }

        #endregion

        #region GenerateURLs (Timestamp, URLTemplate, SharedSecret, ValidityTime = null, TOTPLength = 12, Alphabet = null,                   TLSExporterMaterial = null)

        /// <summary>
        /// Calculate the TOTP URLs and the remaining time until the URLs will change.
        /// </summary>
        /// <param name="Timestamp">The timestamp to calculate the TOTP for.</param>
        /// <param name="URLTemplate">The TOTP URL template.</param>
        /// <param name="SharedSecret">The TOTP shared secret.</param>
        /// <param name="ValidityTime">The optional validity time of the TOTP.</param>
        /// <param name="TOTPLength">The optional expected length of the TOTP.</param>
        /// <param name="Alphabet">The optional alphabet of the TOTP being used.</param>
        /// <param name="TLSExporterMaterial">Optional TLS exporter material for additional security.</param>
        public static (String          Previous,
                       String          Current,
                       String          Next,
                       TimeSpan        RemainingTime,
                       DateTimeOffset  EndTime)

            GenerateURLs(DateTimeOffset  Timestamp,
                         URL             URLTemplate,
                         String          SharedSecret,
                         TimeSpan?       ValidityTime          = null,
                         UInt32?         TOTPLength            = 12,
                         String?         Alphabet              = null,
                         Byte[]?         TLSExporterMaterial   = null)

        {

            var (previousTOTP,
                 currentTOTP,
                 nextTOTP,
                 remainingTime,
                 endTime) = GenerateTOTPs(
                                SharedSecret,
                                ValidityTime,
                                TOTPLength,
                                Alphabet,
                                Timestamp,
                                TLSExporterMaterial
                            );

            return (
                       ProcessURLTemplate(URLTemplate, previousTOTP),
                       ProcessURLTemplate(URLTemplate, currentTOTP),
                       ProcessURLTemplate(URLTemplate, nextTOTP),
                       remainingTime,
                       endTime
                   );

        }

        #endregion

    }

}
