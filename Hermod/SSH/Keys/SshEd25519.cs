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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// Encoding of <c>ssh-ed25519</c> public-key and signature blobs (RFC 8709): each is a sequence of
    /// SSH strings, the first being the algorithm name.
    /// </summary>
    public static class SshEd25519
    {

        /// <summary>The wire algorithm name.</summary>
        public const String AlgorithmName = SshAlgorithmNames.HostKey.Ed25519;


        #region EncodePublicKeyBlob(PublicKey)

        /// <summary>
        /// Encode a 32-byte Ed25519 public key as the SSH public-key blob
        /// (<c>string "ssh-ed25519" || string publickey</c>).
        /// </summary>
        public static Byte[] EncodePublicKeyBlob(ReadOnlySpan<Byte> PublicKey)
        {

            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);

            writer.WriteString(AlgorithmName);
            writer.WriteBinaryString(PublicKey);

            return abw.WrittenSpan.ToArray();

        }

        #endregion

        #region ParsePublicKeyBlob(Blob)

        /// <summary>
        /// Parse an SSH <c>ssh-ed25519</c> public-key blob and return the 32-byte public key.
        /// </summary>
        public static Byte[] ParsePublicKeyBlob(ReadOnlySpan<Byte> Blob)
        {

            var reader     = new SshPacketReader(Blob);
            var algorithm  = reader.ReadString();

            if (algorithm != AlgorithmName)
                throw new SshWireException($"Expected an '{AlgorithmName}' public-key blob, but found '{algorithm}'!");

            var publicKey  = reader.ReadBinaryString();

            if (publicKey.Length != Ed25519KeyPair.KeySize)
                throw new SshWireException($"An Ed25519 public key must be {Ed25519KeyPair.KeySize} bytes!");

            return publicKey;

        }

        #endregion

        #region EncodeSignatureBlob(Signature)

        /// <summary>
        /// Encode a 64-byte Ed25519 signature as the SSH signature blob
        /// (<c>string "ssh-ed25519" || string signature</c>).
        /// </summary>
        public static Byte[] EncodeSignatureBlob(ReadOnlySpan<Byte> Signature)
        {

            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);

            writer.WriteString(AlgorithmName);
            writer.WriteBinaryString(Signature);

            return abw.WrittenSpan.ToArray();

        }

        #endregion

        #region ParseSignatureBlob(Blob)

        /// <summary>
        /// Parse an SSH <c>ssh-ed25519</c> signature blob and return the 64-byte signature.
        /// </summary>
        public static Byte[] ParseSignatureBlob(ReadOnlySpan<Byte> Blob)
        {

            var reader     = new SshPacketReader(Blob);
            var algorithm  = reader.ReadString();

            if (algorithm != AlgorithmName)
                throw new SshWireException($"Expected an '{AlgorithmName}' signature blob, but found '{algorithm}'!");

            var signature  = reader.ReadBinaryString();

            if (signature.Length != Ed25519KeyPair.SignatureSize)
                throw new SshWireException($"An Ed25519 signature must be {Ed25519KeyPair.SignatureSize} bytes!");

            return signature;

        }

        #endregion

    }

}
