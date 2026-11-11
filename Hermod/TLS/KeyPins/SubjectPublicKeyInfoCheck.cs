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

using System.Formats.Asn1;
using System.Numerics;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// The strict check of a DER encoded SubjectPublicKeyInfo before its key
    /// pin is computed: a known algorithm, the parameters it requires, a key
    /// of the right size, a point on the curve, and no trailing data.
    /// </summary>
    /// <remarks>
    /// A raw public key (RFC 7250) is nothing but such a structure, sent by a
    /// peer that has not been authenticated yet, so it is checked before it is
    /// hashed, compared or handed to a signature check.
    ///
    /// Known: EC keys on P-256, P-384 and P-521 as uncompressed points, Ed25519,
    /// ML-DSA-44/65/87, and RSA in certificates only: a raw RSA key makes
    /// neither the key nor its pin smaller and is refused.
    /// </remarks>
    public static class SubjectPublicKeyInfoCheck
    {

        #region Data

        private const String ecPublicKey  = "1.2.840.10045.2.1";
        private const String secp256r1    = "1.2.840.10045.3.1.7";
        private const String secp384r1    = "1.3.132.0.34";
        private const String secp521r1    = "1.3.132.0.35";
        private const String ed25519      = "1.3.101.112";
        private const String rsaEncryption = "1.2.840.113549.1.1.1";
        private const String mlDsa44      = "2.16.840.1.101.3.4.3.17";
        private const String mlDsa65      = "2.16.840.1.101.3.4.3.18";
        private const String mlDsa87      = "2.16.840.1.101.3.4.3.19";

        private sealed record WeierstrassCurve(String Name, Int32 Size, BigInteger P, BigInteger B);

        // y^2 = x^3 - 3x + b (mod p): FIPS 186-5, SEC 2
        private static readonly WeierstrassCurve P256 = new ("P-256",  32, BigInteger.Pow(2, 256) - BigInteger.Pow(2, 224) + BigInteger.Pow(2, 192) + BigInteger.Pow(2, 96) - 1,
                                                             Hex("5ac635d8aa3a93e7b3ebbd55769886bc651d06b0cc53b0f63bce3c3e27d2604b"));

        private static readonly WeierstrassCurve P384 = new ("P-384",  48, BigInteger.Pow(2, 384) - BigInteger.Pow(2, 128) - BigInteger.Pow(2, 96) + BigInteger.Pow(2, 32) - 1,
                                                             Hex("b3312fa7e23ee7e4988e056be3f82d19181d9c6efe8141120314088f5013875ac656398d8a2ed19d2a85c8edd3ec2aef"));

        private static readonly WeierstrassCurve P521 = new ("P-521",  66, BigInteger.Pow(2, 521) - 1,
                                                             Hex("0051953eb9618e1c9a1f929a21a0b68540eea2da725b99b315f3b8b489918ef109e156193951ec7e937b1652c0bd3bb1bf073573df883d2c34f1ef451fd46b503f00"));

        // -x^2 + y^2 = 1 + d x^2 y^2 (mod p): RFC 8032, section 5.1
        private static readonly BigInteger Ed25519P = BigInteger.Pow(2, 255) - 19;
        private static readonly BigInteger Ed25519D = BigInteger.Parse("37095705934669439343138083508754565189542113879843219016388785533085940283555");

        #endregion


        #region IsValid(SubjectPublicKeyInfo, CertificateType, out Reason)

        /// <summary>
        /// Whether the given DER encoded SubjectPublicKeyInfo is well formed and
        /// holds a key of a known algorithm.
        /// </summary>
        /// <param name="SubjectPublicKeyInfo">A DER encoded SubjectPublicKeyInfo.</param>
        /// <param name="CertificateType">Whether it came in a certificate or raw.</param>
        /// <param name="Reason">Why it is not, if it is not.</param>
        public static Boolean IsValid(ReadOnlySpan<Byte>  SubjectPublicKeyInfo,
                                      TLSCertificateType  CertificateType,
                                      out String?         Reason)
        {

            try
            {

                // SEQUENCE { AlgorithmIdentifier, BIT STRING } and nothing after it
                var outer = new AsnReader(SubjectPublicKeyInfo.ToArray(), AsnEncodingRules.DER);
                var spki  = outer.ReadSequence();

                if (outer.HasData)
                    return Fail("trailing data after the SubjectPublicKeyInfo", out Reason);

                var algorithmIdentifier  = spki.ReadSequence();
                var algorithm            = algorithmIdentifier.ReadObjectIdentifier();
                var key                  = spki.ReadBitString(out var unusedBits);

                if (spki.HasData)
                    return Fail("trailing data after the key", out Reason);

                if (unusedBits != 0)
                    return Fail("a key that is not a whole number of bytes", out Reason);

                switch (algorithm)
                {

                    case ecPublicKey:
                    {

                        if (!algorithmIdentifier.HasData)
                            return Fail("an EC key without its curve", out Reason);

                        var curveOID = algorithmIdentifier.ReadObjectIdentifier();

                        if (algorithmIdentifier.HasData)
                            return Fail("trailing data after the curve", out Reason);

                        var curve = curveOID switch {
                                        secp256r1 => P256,
                                        secp384r1 => P384,
                                        secp521r1 => P521,
                                        _         => null
                                    };

                        if (curve is null)
                            return Fail($"an EC key on the unknown curve {curveOID}", out Reason);

                        return IsOnCurve(key, curve, out Reason);

                    }

                    case ed25519:

                        if (algorithmIdentifier.HasData)
                            return Fail("an Ed25519 key with parameters", out Reason);

                        if (key.Length != 32)
                            return Fail($"an Ed25519 key of {key.Length} bytes instead of 32", out Reason);

                        return IsOnEdwards25519(key, out Reason);

                    case mlDsa44:
                    case mlDsa65:
                    case mlDsa87:
                    {

                        if (algorithmIdentifier.HasData)
                            return Fail("an ML-DSA key with parameters", out Reason);

                        var expected = algorithm switch {
                                           mlDsa44 => 1312,
                                           mlDsa65 => 1952,
                                           _       => 2592
                                       };

                        if (key.Length != expected)
                            return Fail($"an ML-DSA key of {key.Length} bytes instead of {expected}", out Reason);

                        Reason = null;
                        return true;

                    }

                    case rsaEncryption:
                    {

                        if (CertificateType == TLSCertificateType.RawPublicKey)
                            return Fail("a raw RSA key", out Reason);

                        // The parameters of rsaEncryption are NULL (RFC 3279, 2.3.1)
                        algorithmIdentifier.ReadNull();

                        if (algorithmIdentifier.HasData)
                            return Fail("trailing data after the RSA parameters", out Reason);

                        var rsaPublicKey = new AsnReader(key, AsnEncodingRules.DER);
                        var sequence     = rsaPublicKey.ReadSequence();

                        if (rsaPublicKey.HasData)
                            return Fail("trailing data after the RSA key", out Reason);

                        var modulus  = sequence.ReadInteger();
                        var exponent = sequence.ReadInteger();

                        if (sequence.HasData)
                            return Fail("trailing data in the RSA key", out Reason);

                        if (modulus.Sign <= 0 || modulus.IsEven || exponent < 3 || exponent.IsEven)
                            return Fail("an RSA key that cannot be one", out Reason);

                        Reason = null;
                        return true;

                    }

                    default:
                        return Fail($"a key of the unknown algorithm {algorithm}", out Reason);

                }

            }
            catch (AsnContentException e)
            {
                return Fail($"a SubjectPublicKeyInfo that is not DER: {e.Message}", out Reason);
            }

        }

        #endregion


        #region (private) IsOnCurve       (Point, Curve, out Reason)

        private static Boolean IsOnCurve(ReadOnlyMemory<Byte>  Point,
                                         WeierstrassCurve      Curve,
                                         out String?           Reason)
        {

            var point = Point.Span;

            // Uncompressed only: 0x04 | X | Y
            if (point.Length != 1 + 2 * Curve.Size || point[0] != 0x04)
                return Fail($"a {Curve.Name} key that is not an uncompressed point", out Reason);

            var x = new BigInteger(point.Slice(1,              Curve.Size), isUnsigned: true, isBigEndian: true);
            var y = new BigInteger(point.Slice(1 + Curve.Size, Curve.Size), isUnsigned: true, isBigEndian: true);

            if (x >= Curve.P || y >= Curve.P)
                return Fail($"a {Curve.Name} key with a coordinate outside the field", out Reason);

            var left  = BigInteger.ModPow(y, 2, Curve.P);
            var right = Mod(BigInteger.ModPow(x, 3, Curve.P) - 3 * x + Curve.B, Curve.P);

            if (left != right)
                return Fail($"a {Curve.Name} key whose point is not on the curve", out Reason);

            Reason = null;
            return true;

        }

        #endregion

        #region (private) IsOnEdwards25519(Key, out Reason)

        /// <summary>
        /// Whether the 32 bytes decode to a point of edwards25519 (RFC 8032, 5.1.3).
        /// </summary>
        private static Boolean IsOnEdwards25519(ReadOnlyMemory<Byte>  Key,
                                                out String?           Reason)
        {

            var bytes  = Key.ToArray();
            var sign   = (bytes[31] & 0x80) != 0;
            bytes[31] &= 0x7f;

            var p      = Ed25519P;
            var y      = new BigInteger(bytes, isUnsigned: true, isBigEndian: false);

            if (y >= p)
                return Fail("an Ed25519 key with a coordinate outside the field", out Reason);

            var y2     = BigInteger.ModPow(y, 2, p);
            var u      = Mod(y2 - 1, p);
            var v      = Mod(Ed25519D * y2 + 1, p);

            // x = u v^3 (u v^7)^((p-5)/8)
            var v3     = BigInteger.ModPow(v, 3, p);
            var v7     = BigInteger.ModPow(v, 7, p);
            var x      = Mod(u * v3 * BigInteger.ModPow(Mod(u * v7, p), (p - 5) / 8, p), p);
            var vx2    = Mod(v * BigInteger.ModPow(x, 2, p), p);

            if (vx2 != u && vx2 != Mod(-u, p))
                return Fail("an Ed25519 key that is not a point on the curve", out Reason);

            if (vx2 != u)
                x = Mod(x * BigInteger.ModPow(2, (p - 1) / 4, p), p);

            if (x.IsZero && sign)
                return Fail("an Ed25519 key that is not a point on the curve", out Reason);

            if (x.IsEven == sign)
                x = p - x;

            // One of the eight points of small order, the neutral one among them: a signature
            // under such a key says nothing about who made it (RFC 8032, 5.1.7; RFC 7748, 7).
            var (x8, y8) = (x, y);

            for (var i = 0; i < 3; i++)
                (x8, y8) = EdwardsDouble(x8, y8);

            if (x8.IsZero && y8.IsOne)
                return Fail("an Ed25519 key of small order", out Reason);

            Reason = null;
            return true;

        }

        /// <summary>
        /// 2P on edwards25519, with a = -1: the formula is complete, so no case is left out.
        /// </summary>
        private static (BigInteger X, BigInteger Y) EdwardsDouble(BigInteger X, BigInteger Y)
        {

            var p    = Ed25519P;
            var x2   = Mod(X * X, p);
            var y2   = Mod(Y * Y, p);
            var dxy  = Mod(Ed25519D * x2 % p * y2, p);

            var x3   = Mod(2 * X * Y % p * Inverse(Mod(1 + dxy, p), p), p);
            var y3   = Mod((y2 + x2) % p * Inverse(Mod(1 - dxy, p), p), p);

            return (x3, y3);

        }

        private static BigInteger Inverse(BigInteger Value, BigInteger Prime)
            => BigInteger.ModPow(Value, Prime - 2, Prime);

        #endregion


        #region (private) Helpers

        private static BigInteger Mod(BigInteger Value, BigInteger Modulus)
        {
            var result = Value % Modulus;
            return result.Sign < 0 ? result + Modulus : result;
        }

        private static BigInteger Hex(String Hex)
            => new (Convert.FromHexString(Hex), isUnsigned: true, isBigEndian: true);

        private static Boolean Fail(String       Message,
                                    out String?  Reason)
        {
            Reason = $"The peer presented {Message}.";
            return false;
        }

        #endregion

    }

}
