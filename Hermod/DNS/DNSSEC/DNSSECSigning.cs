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

using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Crypto.Parameters;

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
        /// validation, so it is verifiable here and unreachable for signing.
        /// </remarks>
        public static Boolean IsSupportedForSigning(Byte Algorithm)

            => Algorithm is 8 or 10 or 13 or 14 or 15 or 16;

        #endregion

        #region (static) UsesRawPrivateKey(Algorithm) / PrivateKeySize(Algorithm)

        /// <summary>
        /// Whether this algorithm's private key is a plain octet string rather
        /// than a platform <see cref="AsymmetricAlgorithm"/>.
        /// </summary>
        /// <remarks>
        /// True for the Edwards curves and nothing else. RFC 8080 §3 gives their
        /// keys no structure to export or import — an Ed25519 private key is 32
        /// uniformly random octets and an Ed448 one is 57 — so there is nothing
        /// for <see cref="RSA"/> or <see cref="ECDsa"/> to hold, and .NET offers
        /// no EdDSA of its own. They come from BouncyCastle, which the validator
        /// has been using for the verifying half all along.
        /// </remarks>
        /// <param name="Algorithm">The DNSSEC algorithm number.</param>
        public static Boolean UsesRawPrivateKey(Byte Algorithm)

            => Algorithm is 15 or 16;


        /// <summary>
        /// The exact size, in octets, of a raw private key for this algorithm.
        /// </summary>
        /// <param name="Algorithm">The DNSSEC algorithm number.</param>
        public static Int32 PrivateKeySize(Byte Algorithm)

            => Algorithm switch {
                   15  => Ed25519PrivateKeyParameters.KeySize,   // 32 (RFC 8032 §5.1.5)
                   16  => Ed448PrivateKeyParameters.  KeySize,   // 57 (RFC 8032 §5.2.5)
                   _   => throw new NotSupportedException($"DNSSEC algorithm {Algorithm} has no raw private key.")
               };

        #endregion

        #region (static) GeneratePrivateKey(Algorithm)

        /// <summary>
        /// A fresh raw private key for one of the Edwards curves.
        /// </summary>
        /// <param name="Algorithm">The DNSSEC algorithm number, 15 or 16.</param>
        /// <remarks>
        /// Any octet string of the right length is a valid EdDSA private key —
        /// RFC 8032 derives the scalar by hashing it, so there is no candidate to
        /// reject and no rejection sampling to get wrong.
        /// </remarks>
        public static Byte[] GeneratePrivateKey(Byte Algorithm)

            => RandomNumberGenerator.GetBytes(PrivateKeySize(Algorithm));

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

                case 15:
                case 16:
                    throw new ArgumentException($"DNSSEC algorithm {Algorithm} takes a raw private key, not an AsymmetricAlgorithm — use the Byte[] overload.", nameof(PrivateKey));

                default:
                    throw new NotSupportedException($"DNSSEC algorithm {Algorithm} is not supported for signing.");

            }

        }

        #endregion

        #region (static) Sign(Algorithm, PrivateKey, Data) — Edwards curves

        /// <summary>
        /// Sign data with one of the Edwards curves (RFC 8080).
        /// </summary>
        /// <param name="Algorithm">The DNSSEC algorithm number, 15 or 16.</param>
        /// <param name="PrivateKey">The raw private key: 32 octets for Ed25519, 57 for Ed448.</param>
        /// <param name="Data">The data to sign.</param>
        /// <remarks>
        /// <para>
        /// PureEdDSA in both cases — the data is signed as it stands, with no
        /// pre-hashing step, and Ed448 takes an empty context. RFC 8080 §2 and §3
        /// say so plainly, and the distinction matters: Ed25519ph and Ed448ph are
        /// different algorithms producing different signatures over the same
        /// message, so an implementation that reached for the pre-hashed variant
        /// would verify perfectly against itself and against nothing else.
        /// </para>
        /// <para>
        /// EdDSA is deterministic (RFC 8032 §5.1.6): one key and one message give
        /// one signature, every time. That is what makes RFC 8080 §6's examples
        /// testable as exact byte strings rather than as "something that
        /// verifies".
        /// </para>
        /// </remarks>
        public static Byte[] Sign(Byte    Algorithm,
                                  Byte[]  PrivateKey,
                                  Byte[]  Data)
        {

            if (!UsesRawPrivateKey(Algorithm))
                throw new ArgumentException($"DNSSEC algorithm {Algorithm} takes an AsymmetricAlgorithm, not a raw private key.", nameof(Algorithm));

            var expected = PrivateKeySize(Algorithm);

            if (PrivateKey.Length != expected)
                throw new ArgumentException($"An algorithm {Algorithm} private key is exactly {expected} octets, not {PrivateKey.Length}.", nameof(PrivateKey));

            var signer = Algorithm == 15
                             ? (Org.BouncyCastle.Crypto.ISigner) new Ed25519Signer()
                             : new Ed448Signer([]);

            signer.Init(
                true,
                Algorithm == 15
                    ? new Ed25519PrivateKeyParameters(PrivateKey, 0)
                    : new Ed448PrivateKeyParameters  (PrivateKey, 0)
            );

            signer.BlockUpdate(Data, 0, Data.Length);

            return signer.GenerateSignature();

        }

        #endregion

        #region (static) PublicKeyFromPrivateKey(Algorithm, PrivateKey)

        /// <summary>
        /// The public half of an Edwards private key, in the wire form RFC 8080 §3
        /// defines: the raw point, 32 octets for Ed25519 and 57 for Ed448.
        /// </summary>
        /// <param name="Algorithm">The DNSSEC algorithm number, 15 or 16.</param>
        /// <param name="PrivateKey">The raw private key.</param>
        public static Byte[] PublicKeyFromPrivateKey(Byte    Algorithm,
                                                     Byte[]  PrivateKey)
        {

            if (!UsesRawPrivateKey(Algorithm))
                throw new ArgumentException($"DNSSEC algorithm {Algorithm} has no raw private key to derive from.", nameof(Algorithm));

            var expected = PrivateKeySize(Algorithm);

            if (PrivateKey.Length != expected)
                throw new ArgumentException($"An algorithm {Algorithm} private key is exactly {expected} octets, not {PrivateKey.Length}.", nameof(PrivateKey));

            return Algorithm == 15
                       ? new Ed25519PrivateKeyParameters(PrivateKey, 0).GeneratePublicKey().GetEncoded()
                       : new Ed448PrivateKeyParameters  (PrivateKey, 0).GeneratePublicKey().GetEncoded();

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

                case 15:
                case 16:
                    throw new ArgumentException($"DNSSEC algorithm {Algorithm} has a raw key, not an AsymmetricAlgorithm — its public form is already the wire form.", nameof(PublicKey));

                default:
                    throw new NotSupportedException($"DNSSEC algorithm {Algorithm} has no public key encoding here.");

            }

        }

        #endregion

    }

}
