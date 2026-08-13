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

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// The signing direction of the DNSSEC algorithms: making a signature, and
    /// putting a public key into the wire form DNS expects.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="DNSSECValidator.VerifySignature"/> is the other direction, and
    /// the two lists are deliberately not the same length. Verification covers
    /// every algorithm a validator may meet in a zone someone else signed,
    /// including the RSA/SHA-1 pair that RFC 8624 §3.1 marks MUST NOT for new
    /// signatures but MAY for validation. Signing covers only what §3.1 permits
    /// a signer to choose today.
    /// </para>
    /// <para>
    /// This is not zone signing. Nothing here builds an RRSIG or walks a zone —
    /// it exists because SIG(0) (RFC 2931) needs a signature over a message, and
    /// a DNS library that can check eight algorithms should not grow a second,
    /// slightly different implementation of the same key encodings in order to
    /// produce one.
    /// </para>
    /// </remarks>
    public static class DNSSECSigning
    {

        #region (static) IsSupportedForSigning(Algorithm)

        /// <summary>
        /// Whether this implementation will make a signature with the given
        /// DNSSEC algorithm number.
        /// </summary>
        /// <remarks>
        /// RSA/SHA-1 (5 and 7) is missing on purpose rather than by omission:
        /// RFC 8624 §3.1 says MUST NOT for signing while still allowing
        /// validation, so it is verifiable here and unreachable for signing. The
        /// Edwards curves (15, 16) are missing for a duller reason — verification
        /// borrows them from BouncyCastle, and the private-key half has not been
        /// wired up.
        /// </remarks>
        public static Boolean IsSupportedForSigning(Byte Algorithm)

            => Algorithm is 8 or 10 or 13 or 14;

        #endregion

        #region (static) Sign(Algorithm, PrivateKey, Data)

        /// <summary>
        /// Sign data with a DNSSEC algorithm, in the signature encoding DNS uses.
        /// </summary>
        /// <param name="Algorithm">The DNSSEC algorithm number.</param>
        /// <param name="PrivateKey">An <see cref="RSA"/> or <see cref="ECDsa"/> holding the private key.</param>
        /// <param name="Data">The data to sign.</param>
        public static Byte[] Sign(Byte                 Algorithm,
                                  AsymmetricAlgorithm  PrivateKey,
                                  Byte[]               Data)
        {

            switch (Algorithm)
            {

                case 8:
                case 10:

                    if (PrivateKey is not RSA rsa)
                        throw new ArgumentException($"DNSSEC algorithm {Algorithm} needs an RSA key, not a {PrivateKey.GetType().Name}.", nameof(PrivateKey));

                    return rsa.SignData(
                               Data,
                               Algorithm == 8 ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA512,
                               RSASignaturePadding.Pkcs1
                           );

                case 13:
                case 14:

                    if (PrivateKey is not ECDsa ecdsa)
                        throw new ArgumentException($"DNSSEC algorithm {Algorithm} needs an ECDSA key, not a {PrivateKey.GetType().Name}.", nameof(PrivateKey));

                    // RFC 6605 §4: the signature is the fixed-width pair r || s,
                    // not the ASN.1 sequence the platform produces by default.
                    return ecdsa.SignData(
                               Data,
                               Algorithm == 13 ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA384,
                               DSASignatureFormat.IeeeP1363FixedFieldConcatenation
                           );

                case 5:
                case 7:
                    throw new NotSupportedException($"RFC 8624 §3.1: RSA/SHA-1 (algorithm {Algorithm}) MUST NOT be used to make new signatures. It can still be verified.");

                default:
                    throw new NotSupportedException($"DNSSEC algorithm {Algorithm} is not supported for signing.");

            }

        }

        #endregion

        #region (static) EncodePublicKey(Algorithm, PublicKey)

        /// <summary>
        /// The public key in the wire form its DNSSEC algorithm defines — what
        /// goes into the public key field of a DNSKEY or KEY record.
        /// </summary>
        /// <param name="Algorithm">The DNSSEC algorithm number.</param>
        /// <param name="PublicKey">An <see cref="RSA"/> or <see cref="ECDsa"/>.</param>
        public static Byte[] EncodePublicKey(Byte                 Algorithm,
                                             AsymmetricAlgorithm  PublicKey)
        {

            switch (Algorithm)
            {

                case 5:
                case 7:
                case 8:
                case 10:
                {

                    if (PublicKey is not RSA rsa)
                        throw new ArgumentException($"DNSSEC algorithm {Algorithm} needs an RSA key, not a {PublicKey.GetType().Name}.", nameof(PublicKey));

                    var parameters = rsa.ExportParameters(false);
                    var exponent   = parameters.Exponent ?? throw new ArgumentException("The RSA key has no public exponent!", nameof(PublicKey));
                    var modulus    = parameters.Modulus  ?? throw new ArgumentException("The RSA key has no modulus!",         nameof(PublicKey));

                    using var encoded = new MemoryStream();

                    // RFC 3110 §2: one octet of exponent length when it fits, and
                    // otherwise a zero octet followed by a two-octet length. The
                    // long form exists for exponents over 255 octets, which no
                    // real key uses — and which is exactly why implementations get
                    // it wrong.
                    if (exponent.Length <= Byte.MaxValue)
                        encoded.WriteByte((Byte) exponent.Length);

                    else
                    {
                        encoded.WriteByte(0);
                        encoded.WriteUInt16BE((UInt16) exponent.Length);
                    }

                    encoded.Write(exponent, 0, exponent.Length);
                    encoded.Write(modulus,  0, modulus. Length);

                    return encoded.ToArray();

                }

                case 13:
                case 14:
                {

                    if (PublicKey is not ECDsa ecdsa)
                        throw new ArgumentException($"DNSSEC algorithm {Algorithm} needs an ECDSA key, not a {PublicKey.GetType().Name}.", nameof(PublicKey));

                    var parameters = ecdsa.ExportParameters(false);
                    var x          = parameters.Q.X ?? throw new ArgumentException("The ECDSA key has no public point!", nameof(PublicKey));
                    var y          = parameters.Q.Y ?? throw new ArgumentException("The ECDSA key has no public point!", nameof(PublicKey));

                    // RFC 6605 §4: x || y, fixed width, and no 0x04 prefix — the
                    // uncompressed-point marker is left out because the algorithm
                    // number already says which curve this is.
                    return [.. x, .. y];

                }

                default:
                    throw new NotSupportedException($"DNSSEC algorithm {Algorithm} has no public key encoding here.");

            }

        }

        #endregion

    }

}
