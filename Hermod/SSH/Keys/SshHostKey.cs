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

using System.Buffers;
using System.Numerics;
using System.Security.Cryptography;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// A private key that can act as an SSH host key: it exposes its public-key blob (K_S) and can
    /// sign the exchange hash with one of the signature algorithms it supports.
    /// </summary>
    public interface ISshHostKey
    {

        /// <summary>The signature algorithm names this key supports, most preferred first.</summary>
        IReadOnlyList<String> AlgorithmNames { get; }

        /// <summary>The SSH public-key blob (K_S).</summary>
        Byte[] PublicKeyBlob { get; }

        /// <summary>Sign the given data with the requested algorithm, returning the SSH signature blob.</summary>
        Byte[] Sign(String AlgorithmName, ReadOnlySpan<Byte> Data);

    }


    /// <summary>Factory methods for generating SSH host keys.</summary>
    public static class SshHostKey
    {

        /// <summary>Generate a fresh Ed25519 host key.</summary>
        public static ISshHostKey GenerateEd25519()
            => Ed25519KeyPair.Generate();

        /// <summary>Generate a fresh ECDSA host key for one of the NIST curves.</summary>
        /// <param name="Algorithm">One of the <c>ecdsa-sha2-nistp256/384/521</c> names.</param>
        public static ISshHostKey GenerateEcdsa(String Algorithm)
            => new EcdsaHostKey(Algorithm);

        /// <summary>Generate a fresh RSA host key.</summary>
        /// <param name="KeySizeInBits">The RSA key size (default 3072).</param>
        public static ISshHostKey GenerateRsa(Int32 KeySizeInBits = 3072)
            => new RsaHostKey(KeySizeInBits);

    }


    /// <summary>An ECDSA host key over a NIST prime curve (RFC 5656).</summary>
    public sealed class EcdsaHostKey : ISshHostKey
    {

        private readonly ECDsa              ecdsa;
        private readonly String             algorithm;
        private readonly HashAlgorithmName  hash;
        private readonly String             curveId;
        private readonly Int32              fieldSize;

        /// <summary>Create a fresh ECDSA host key for the given algorithm.</summary>
        public EcdsaHostKey(String Algorithm)
        {
            (hash, var curve, curveId, fieldSize) = SshSignature.EcdsaParametersFor(Algorithm);
            algorithm  = Algorithm;
            ecdsa      = ECDsa.Create(curve);
        }

        /// <summary>Reconstruct an ECDSA host key from private EC parameters (key import).</summary>
        internal EcdsaHostKey(String Algorithm, ECParameters PrivateParameters)
        {
            (hash, _, curveId, fieldSize) = SshSignature.EcdsaParametersFor(Algorithm);
            algorithm  = Algorithm;
            ecdsa      = ECDsa.Create(PrivateParameters);
        }

        /// <summary>The SSH curve identifier (e.g. <c>nistp256</c>).</summary>
        internal String  CurveId    => curveId;

        /// <summary>The coordinate field size in bytes.</summary>
        internal Int32   FieldSize  => fieldSize;

        /// <summary>Export the private EC parameters (key export).</summary>
        internal ECParameters ExportPrivateParameters() => ecdsa.ExportParameters(true);

        public IReadOnlyList<String> AlgorithmNames => [ algorithm ];

        public Byte[] PublicKeyBlob
        {
            get
            {

                var parameters  = ecdsa.ExportParameters(false);
                var point       = new Byte[1 + (2 * fieldSize)];
                point[0]        = 0x04;
                parameters.Q.X!.CopyTo(point, 1);
                parameters.Q.Y!.CopyTo(point, 1 + fieldSize);

                var abw     = new ArrayBufferWriter<Byte>();
                var writer  = new SshPacketWriter(abw);
                writer.WriteString(algorithm);
                writer.WriteString(curveId);
                writer.WriteBinaryString(point);
                return abw.WrittenSpan.ToArray();

            }
        }

        public Byte[] Sign(String AlgorithmName, ReadOnlySpan<Byte> Data)
        {

            // IEEE P1363 gives fixed-length r||s; re-encode as the SSH 'mpint r || mpint s'.
            var ieee  = ecdsa.SignData(Data, hash, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            var r     = new BigInteger(ieee.AsSpan(0, fieldSize),  isUnsigned: true, isBigEndian: true);
            var s     = new BigInteger(ieee.AsSpan(fieldSize),     isUnsigned: true, isBigEndian: true);

            var innerAbw     = new ArrayBufferWriter<Byte>();
            var innerWriter  = new SshPacketWriter(innerAbw);
            innerWriter.WriteMPInt(r);
            innerWriter.WriteMPInt(s);

            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);
            writer.WriteString(algorithm);
            writer.WriteBinaryString(innerAbw.WrittenSpan);
            return abw.WrittenSpan.ToArray();

        }

    }


    /// <summary>An RSA host key producing SHA-2 signatures (RFC 8332). One key serves both rsa-sha2-256 and -512.</summary>
    public sealed class RsaHostKey : ISshHostKey
    {

        private readonly RSA rsa;

        /// <summary>Create a fresh RSA host key of the given size.</summary>
        public RsaHostKey(Int32 KeySizeInBits = 3072)
        {
            rsa = RSA.Create(KeySizeInBits);
        }

        /// <summary>Reconstruct an RSA host key from private RSA parameters (key import).</summary>
        internal RsaHostKey(RSAParameters PrivateParameters)
        {
            rsa = RSA.Create(PrivateParameters);
        }

        /// <summary>Export the private RSA parameters (key export).</summary>
        internal RSAParameters ExportPrivateParameters() => rsa.ExportParameters(true);

        public IReadOnlyList<String> AlgorithmNames =>
            [ SshAlgorithmNames.HostKey.RsaSha2_512, SshAlgorithmNames.HostKey.RsaSha2_256 ];

        public Byte[] PublicKeyBlob
        {
            get
            {

                var parameters  = rsa.ExportParameters(false);

                var abw     = new ArrayBufferWriter<Byte>();
                var writer  = new SshPacketWriter(abw);
                writer.WriteString(SshAlgorithmNames.HostKey.SshRsa);
                writer.WriteMPInt(new BigInteger(parameters.Exponent!, isUnsigned: true, isBigEndian: true));
                writer.WriteMPInt(new BigInteger(parameters.Modulus!,  isUnsigned: true, isBigEndian: true));
                return abw.WrittenSpan.ToArray();

            }
        }

        public Byte[] Sign(String AlgorithmName, ReadOnlySpan<Byte> Data)
        {

            var signature  = rsa.SignData(Data, SshSignature.RsaHashFor(AlgorithmName), RSASignaturePadding.Pkcs1);

            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);
            writer.WriteString(AlgorithmName);
            writer.WriteBinaryString(signature);
            return abw.WrittenSpan.ToArray();

        }

    }

}
