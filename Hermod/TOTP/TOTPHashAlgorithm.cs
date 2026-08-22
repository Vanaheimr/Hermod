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

using System.Security.Cryptography;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// The HMAC hash algorithm used to derive a Time-based One-Time Password (TOTP).
    ///
    /// The token format is shared with the TypeScript implementation TOTP.ts
    /// (https://github.com/OpenChargingCloud/TOTP.ts). Both implementations default to
    /// HMAC-SHA256, so that a token generated here is byte-identical to a token generated
    /// there. HMAC-SHA384 and HMAC-SHA512 exist on both sides as well, but have to be
    /// configured explicitly and identically on both ends.
    ///
    /// The hash length also bounds the useful token length: the token characters are read
    /// from the HMAC output as a ring buffer, therefore a token longer than 32/48/64
    /// characters just repeats itself and gains no further entropy.
    /// </summary>
    public enum TOTPHashAlgorithm
    {

        /// <summary>
        /// HMAC-SHA256, 32 hash bytes (the default).
        /// </summary>
        SHA256 = 0,

        /// <summary>
        /// HMAC-SHA384, 48 hash bytes.
        /// </summary>
        SHA384 = 1,

        /// <summary>
        /// HMAC-SHA512, 64 hash bytes.
        /// </summary>
        SHA512 = 2

    }


    /// <summary>
    /// Extension methods for TOTP hash algorithms.
    /// </summary>
    public static class TOTPHashAlgorithmExtensions
    {

        #region (static) TryParse   (Text, out HashAlgorithm)

        /// <summary>
        /// Try to parse the given text as a TOTP hash algorithm.
        /// Matching is case-insensitive and ignores '-' and '_', so that "sha256",
        /// "SHA-256" and "HMAC_SHA256" are all accepted.
        /// </summary>
        /// <param name="Text">A text representation of a TOTP hash algorithm.</param>
        /// <param name="HashAlgorithm">The parsed TOTP hash algorithm.</param>
        public static Boolean TryParse(ReadOnlySpan<Char>     Text,
                                       out TOTPHashAlgorithm  HashAlgorithm)
        {

            Span<Char> normalized = stackalloc Char[16];
            var length = 0;

            foreach (var character in Text)
            {

                if (character is '-' or '_')
                    continue;

                if (length == normalized.Length)
                {
                    HashAlgorithm = default;
                    return false;
                }

                normalized[length++] = Char.ToLowerInvariant(character);

            }

            switch (normalized[..length])
            {

                case "sha256":
                case "hmacsha256":
                    HashAlgorithm = TOTPHashAlgorithm.SHA256;
                    return true;

                case "sha384":
                case "hmacsha384":
                    HashAlgorithm = TOTPHashAlgorithm.SHA384;
                    return true;

                case "sha512":
                case "hmacsha512":
                    HashAlgorithm = TOTPHashAlgorithm.SHA512;
                    return true;

                default:
                    HashAlgorithm = default;
                    return false;

            }

        }

        #endregion

        #region (static) AsText     (this HashAlgorithm)

        /// <summary>
        /// Return the canonical text representation of the given TOTP hash algorithm,
        /// which is the spelling used by TOTP.ts and within JSON configurations.
        /// </summary>
        /// <param name="HashAlgorithm">A TOTP hash algorithm.</param>
        public static String AsText(this TOTPHashAlgorithm HashAlgorithm)

            => HashAlgorithm switch {
                   TOTPHashAlgorithm.SHA384  => "sha384",
                   TOTPHashAlgorithm.SHA512  => "sha512",
                   _                         => "sha256"
               };

        #endregion

        #region (static) CreateHMAC (this HashAlgorithm, Key)

        /// <summary>
        /// Create a new keyed HMAC for the given TOTP hash algorithm.
        /// </summary>
        /// <param name="HashAlgorithm">A TOTP hash algorithm.</param>
        /// <param name="Key">The key of the HMAC.</param>
        public static HMAC CreateHMAC(this TOTPHashAlgorithm  HashAlgorithm,
                                      Byte[]                  Key)

            => HashAlgorithm switch {
                   TOTPHashAlgorithm.SHA256  => new HMACSHA256(Key),
                   TOTPHashAlgorithm.SHA384  => new HMACSHA384(Key),
                   TOTPHashAlgorithm.SHA512  => new HMACSHA512(Key),
                   _                         => throw new ArgumentException("The hash algorithm must be one of: sha256, sha384, sha512!",
                                                                            nameof(HashAlgorithm))
               };

        #endregion

    }

}
