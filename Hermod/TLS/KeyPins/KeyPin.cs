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

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A key pin: SHA-256 over the DER encoded SubjectPublicKeyInfo of a key.
    /// </summary>
    /// <remarks>
    /// The same hash as the matching data of a DANE record "3 1 1" (RFC 7671),
    /// RFC 7469's pin-sha256 and curl's --pinnedpubkey. It covers the key and
    /// its algorithm, nothing else: a certificate renewed with the same key
    /// keeps its pin, and a key presented without any certificate (RFC 7250)
    /// has the same pin as in one.
    ///
    /// Not a certificate pin, which hashes the whole certificate, and not an
    /// OpenSSH fingerprint, which hashes the key's SSH wire format: neither of
    /// the two ever equals the key pin of the same key.
    ///
    /// Written as 64 hexadecimal digits, with or without colons between the
    /// bytes, or in curl's notation "sha256//" followed by the base64 of the
    /// hash. All of them are read; <see cref="ToString"/> writes the first.
    /// </remarks>
    public sealed class KeyPin : IEquatable<KeyPin>
    {

        #region Data

        /// <summary>
        /// The length of a key pin in bytes.
        /// </summary>
        public const Int32   Length       = 32;

        /// <summary>
        /// The prefix of curl's notation of a key pin.
        /// </summary>
        public const String  CurlPrefix   = "sha256//";

        private readonly Byte[] hash;

        #endregion

        #region Properties

        /// <summary>
        /// The 32 bytes of the hash.
        /// </summary>
        public ReadOnlySpan<Byte> Hash
            => hash;

        #endregion

        #region Constructor(s)

        private KeyPin(Byte[] Hash)
        {
            this.hash = Hash;
        }

        #endregion


        #region (static) FromSHA256                (Hash)

        /// <summary>
        /// The key pin with the given SHA-256 hash, e.g. the matching data of a
        /// TLSA record with selector 1 and matching type 1.
        /// </summary>
        /// <param name="Hash">The 32 bytes of the hash.</param>
        public static KeyPin FromSHA256(ReadOnlySpan<Byte> Hash)
        {

            if (Hash.Length != Length)
                throw new ArgumentException($"A key pin has {Length} bytes, not {Hash.Length}!", nameof(Hash));

            return new (Hash.ToArray());

        }

        #endregion

        #region (static) FromSubjectPublicKeyInfo  (SubjectPublicKeyInfo)

        /// <summary>
        /// The key pin of the given DER encoded SubjectPublicKeyInfo.
        /// </summary>
        /// <remarks>
        /// Hashes the bytes as they are. A SubjectPublicKeyInfo a peer sent
        /// goes through <see cref="KeyPinValidator"/>, which checks it first.
        /// </remarks>
        /// <param name="SubjectPublicKeyInfo">A DER encoded SubjectPublicKeyInfo.</param>
        public static KeyPin FromSubjectPublicKeyInfo(ReadOnlySpan<Byte> SubjectPublicKeyInfo)
        {

            if (SubjectPublicKeyInfo.IsEmpty)
                throw new ArgumentException("A SubjectPublicKeyInfo must not be empty!", nameof(SubjectPublicKeyInfo));

            return new (SHA256.HashData(SubjectPublicKeyInfo));

        }

        #endregion

        #region (static) FromCertificate           (Certificate)

        /// <summary>
        /// The key pin of the key in the given certificate.
        /// </summary>
        /// <param name="Certificate">An X.509 certificate.</param>
        public static KeyPin FromCertificate(X509Certificate2 Certificate)

            => FromSubjectPublicKeyInfo(Certificate.PublicKey.ExportSubjectPublicKeyInfo());

        #endregion

        #region (static) FromKey                   (Key)

        /// <summary>
        /// The key pin of the public part of the given key.
        /// </summary>
        /// <param name="Key">An asymmetric key, e.g. an ECDsa or RSA key.</param>
        public static KeyPin FromKey(AsymmetricAlgorithm Key)

            => FromSubjectPublicKeyInfo(Key.ExportSubjectPublicKeyInfo());

        #endregion


        #region (static) Parse    (Text)

        /// <summary>
        /// Parse the given text representation of a key pin.
        /// </summary>
        /// <param name="Text">64 hexadecimal digits, with or without colons, or "sha256//" and base64.</param>
        public static KeyPin Parse(String Text)
        {

            if (TryParse(Text, out var keyPin))
                return keyPin;

            throw new FormatException($"Not a key pin: '{Text}'! Expected 64 hexadecimal digits, with or without colons between the bytes, or '{CurlPrefix}' and the base64 of 32 bytes.");

        }

        #endregion

        #region (static) TryParse (Text, out KeyPin)

        /// <summary>
        /// Try to parse the given text representation of a key pin.
        /// </summary>
        /// <param name="Text">64 hexadecimal digits, with or without colons, or "sha256//" and base64.</param>
        /// <param name="KeyPin">The parsed key pin.</param>
        public static Boolean TryParse(String?                          Text,
                                       [NotNullWhen(true)] out KeyPin?  KeyPin)
        {

            KeyPin = null;

            var text = Text?.Trim();

            if (String.IsNullOrEmpty(text))
                return false;

            // curl's notation: one pin, the base64 of exactly 32 bytes
            if (text.StartsWith(CurlPrefix, StringComparison.OrdinalIgnoreCase))
            {

                var hash = new Byte[Length];

                if (Convert.TryFromBase64String(text[CurlPrefix.Length..], hash, out var written) &&
                    written == Length &&
                    text.Length - CurlPrefix.Length == 44)
                {
                    KeyPin = new KeyPin(hash);
                    return true;
                }

                return false;

            }

            // 64 hexadecimal digits
            if (text.Length == 2 * Length)
                return TryParseHex(text, out KeyPin);

            // 32 pairs of hexadecimal digits, separated by single colons
            if (text.Length == 3 * Length - 1)
            {

                var digits = new Char[2 * Length];

                for (var i = 0; i < Length; i++)
                {

                    if (i > 0 && text[3 * i - 1] != ':')
                        return false;

                    digits[2 * i]     = text[3 * i];
                    digits[2 * i + 1] = text[3 * i + 1];

                }

                return TryParseHex(new String(digits), out KeyPin);

            }

            return false;

        }

        private static Boolean TryParseHex(String                           Hex,
                                           [NotNullWhen(true)] out KeyPin?  KeyPin)
        {

            KeyPin = null;

            foreach (var digit in Hex)
                if (!Char.IsAsciiHexDigit(digit))
                    return false;

            KeyPin = new KeyPin(Convert.FromHexString(Hex));
            return true;

        }

        #endregion


        #region ToHex          ()

        /// <summary>
        /// The 64 lowercase hexadecimal digits of the key pin, as "openssl dgst -sha256" writes them.
        /// </summary>
        public String ToHex()

            => Convert.ToHexStringLower(hash);

        #endregion

        #region ToHexWithColons()

        /// <summary>
        /// The 32 bytes of the key pin as uppercase hexadecimal pairs, separated by colons.
        /// </summary>
        public String ToHexWithColons()

            => String.Join(':', hash.Select(b => b.ToString("X2")));

        #endregion

        #region ToBase64       ()

        /// <summary>
        /// The base64 of the key pin, as RFC 7469 and Chrome write it.
        /// </summary>
        public String ToBase64()

            => Convert.ToBase64String(hash);

        #endregion

        #region ToCurl         ()

        /// <summary>
        /// The key pin in curl's notation: "sha256//" and the base64 of the hash.
        /// </summary>
        public String ToCurl()

            => CurlPrefix + ToBase64();

        #endregion


        #region Operator overloading

        public static Boolean operator == (KeyPin? KeyPin1, KeyPin? KeyPin2)

            => KeyPin1 is null
                   ? KeyPin2 is null
                   : KeyPin1.Equals(KeyPin2);

        public static Boolean operator != (KeyPin? KeyPin1, KeyPin? KeyPin2)

            => !(KeyPin1 == KeyPin2);

        #endregion

        #region IEquatable<KeyPin> Members

        /// <summary>
        /// Whether the given object is the same key pin.
        /// </summary>
        public override Boolean Equals(Object? Object)

            => Object is KeyPin keyPin &&
                   Equals(keyPin);

        /// <summary>
        /// Whether the given key pin is the same.
        /// </summary>
        public Boolean Equals(KeyPin? KeyPin)

            => KeyPin is not null &&
               hash.AsSpan().SequenceEqual(KeyPin.hash);

        #endregion

        #region (override) GetHashCode()

        public override Int32 GetHashCode()

            => BitConverter.ToInt32(hash, 0);

        #endregion

        #region (override) ToString()

        /// <summary>
        /// The 64 lowercase hexadecimal digits of the key pin.
        /// </summary>
        public override String ToString()

            => ToHex();

        #endregion

    }

}
