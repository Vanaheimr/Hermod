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

using System.Numerics;
using System.Security.Cryptography;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// Verification and blob helpers for SSH public-key signatures across the supported algorithms:
    /// <c>ssh-ed25519</c> (RFC 8709), <c>ecdsa-sha2-nistp256/384/521</c> (RFC 5656) and
    /// <c>rsa-sha2-256/512</c> (RFC 8332). Used for host-key verification (transport) and, later,
    /// public-key authentication.
    /// </summary>
    public static class SshSignature
    {

        #region Verify(PublicKeyBlob, Data, SignatureBlob)

        /// <summary>
        /// Verify an SSH signature blob over <paramref name="Data"/> against an SSH public-key blob.
        /// The signature algorithm must be compatible with the key type.
        /// </summary>
        /// <param name="PublicKeyBlob">The SSH public-key blob (K_S).</param>
        /// <param name="Data">The signed data (e.g. the exchange hash H).</param>
        /// <param name="SignatureBlob">The SSH signature blob.</param>
        public static Boolean Verify(ReadOnlySpan<Byte>  PublicKeyBlob,
                                     ReadOnlySpan<Byte>  Data,
                                     ReadOnlySpan<Byte>  SignatureBlob)
        {

            try
            {

                var keyReader  = new SshPacketReader(PublicKeyBlob);
                var keyType    = keyReader.ReadString();

                // A certificate blob: verify the signature against the certificate's embedded subject key.
                if (SshCertificate.IsCertificateAlgorithm(keyType))
                    return SshCertificate.TryParse(PublicKeyBlob.ToArray(), out var certificate) &&
                           Verify(certificate!.SubjectPublicKey, Data, SignatureBlob);

                var sigReader  = new SshPacketReader(SignatureBlob);
                var sigAlg     = sigReader.ReadString();
                var sigData    = sigReader.ReadBinaryString();

                return keyType switch {

                    SshAlgorithmNames.HostKey.Ed25519
                        => sigAlg == SshAlgorithmNames.HostKey.Ed25519 &&
                           Ed25519KeyPair.Verify(keyReader.ReadBinaryString(), Data, sigData),

                    SshAlgorithmNames.HostKey.EcdsaNistP256 or
                    SshAlgorithmNames.HostKey.EcdsaNistP384 or
                    SshAlgorithmNames.HostKey.EcdsaNistP521
                        => sigAlg == keyType &&
                           VerifyEcdsa(keyType, ref keyReader, Data, sigData),

                    SshAlgorithmNames.HostKey.SshRsa
                        => sigAlg is SshAlgorithmNames.HostKey.RsaSha2_256 or SshAlgorithmNames.HostKey.RsaSha2_512 &&
                           VerifyRsa(sigAlg, ref keyReader, Data, sigData),

                    _   => false

                };

            }
            catch (SshWireException)
            {
                return false;
            }

        }

        #endregion


        #region (internal) ECDSA

        internal static (HashAlgorithmName Hash, ECCurve Curve, String CurveId, Int32 FieldSize) EcdsaParametersFor(String Algorithm)
            => Algorithm switch {
                   SshAlgorithmNames.HostKey.EcdsaNistP256 => (HashAlgorithmName.SHA256, ECCurve.NamedCurves.nistP256, "nistp256", 32),
                   SshAlgorithmNames.HostKey.EcdsaNistP384 => (HashAlgorithmName.SHA384, ECCurve.NamedCurves.nistP384, "nistp384", 48),
                   SshAlgorithmNames.HostKey.EcdsaNistP521 => (HashAlgorithmName.SHA512, ECCurve.NamedCurves.nistP521, "nistp521", 66),
                   _                                       => throw new SshWireException($"Unsupported ECDSA algorithm '{Algorithm}'.")
               };

        private static Boolean VerifyEcdsa(String Algorithm, ref SshPacketReader KeyReader, ReadOnlySpan<Byte> Data, Byte[] SignatureData)
        {

            var (hash, curve, curveId, fieldSize) = EcdsaParametersFor(Algorithm);

            var identifier = KeyReader.ReadString();
            if (identifier != curveId)
                return false;

            var point = KeyReader.ReadBinaryString();
            if (point.Length != 1 + (2 * fieldSize) || point[0] != 0x04)
                return false;

            using var ecdsa = ECDsa.Create(new ECParameters {
                                  Curve = curve,
                                  Q     = new ECPoint {
                                              X = point[1..(1 + fieldSize)],
                                              Y = point[(1 + fieldSize)..]
                                          }
                              });

            // The SSH ECDSA signature is 'mpint r || mpint s'; convert to the IEEE P1363 fixed-length r||s.
            var innerReader = new SshPacketReader(SignatureData);
            var r           = innerReader.ReadMPInt();
            var s           = innerReader.ReadMPInt();

            var ieee = new Byte[2 * fieldSize];
            ToFixedLength(r, fieldSize).CopyTo(ieee, 0);
            ToFixedLength(s, fieldSize).CopyTo(ieee, fieldSize);

            return ecdsa.VerifyData(Data, ieee, hash, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        }

        #endregion

        #region (internal) RSA

        internal static HashAlgorithmName RsaHashFor(String Algorithm)
            => Algorithm switch {
                   SshAlgorithmNames.HostKey.RsaSha2_256 => HashAlgorithmName.SHA256,
                   SshAlgorithmNames.HostKey.RsaSha2_512 => HashAlgorithmName.SHA512,
                   _                                     => throw new SshWireException($"Unsupported RSA algorithm '{Algorithm}'.")
               };

        private static Boolean VerifyRsa(String Algorithm, ref SshPacketReader KeyReader, ReadOnlySpan<Byte> Data, Byte[] SignatureData)
        {

            var e = KeyReader.ReadMPInt();
            var n = KeyReader.ReadMPInt();

            using var rsa = RSA.Create(new RSAParameters {
                                Exponent = e.ToByteArray(isUnsigned: true, isBigEndian: true),
                                Modulus  = n.ToByteArray(isUnsigned: true, isBigEndian: true)
                            });

            return rsa.VerifyData(Data, SignatureData, RsaHashFor(Algorithm), RSASignaturePadding.Pkcs1);

        }

        #endregion

        #region (internal) ToFixedLength(Value, Length)

        // Encode a non-negative integer as a fixed-length big-endian byte array (left-padded with zeros).
        internal static Byte[] ToFixedLength(BigInteger Value, Int32 Length)
        {

            var bytes = Value.ToByteArray(isUnsigned: true, isBigEndian: true);

            if (bytes.Length == Length)
                return bytes;

            if (bytes.Length < Length)
            {
                var padded = new Byte[Length];
                bytes.CopyTo(padded, Length - bytes.Length);
                return padded;
            }

            throw new SshWireException($"Integer is longer ({bytes.Length}) than the expected field size ({Length}).");

        }

        #endregion

    }

}
