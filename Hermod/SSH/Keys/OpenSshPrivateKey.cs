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
using System.Buffers;
using System.Numerics;
using System.Security.Cryptography;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// The result of loading an <c>openssh-key-v1</c> private key: the usable signing key and its comment.
    /// </summary>
    public sealed record SshLoadedKey(ISshHostKey Key, String Comment);


    /// <summary>
    /// Reads and writes the modern OpenSSH private-key format (<c>-----BEGIN OPENSSH PRIVATE KEY-----</c>,
    /// magic <c>openssh-key-v1</c>). Reading supports both unencrypted keys and passphrase-protected keys
    /// (<c>aes256-ctr</c> + <c>bcrypt</c> KDF); writing produces unencrypted keys.
    /// </summary>
    public static class OpenSshPrivateKey
    {

        #region Data

        private const String PemHeader   = "-----BEGIN OPENSSH PRIVATE KEY-----";
        private const String PemFooter   = "-----END OPENSSH PRIVATE KEY-----";
        private static ReadOnlySpan<Byte> AuthMagic => "openssh-key-v1\0"u8;

        #endregion


        #region Parse(Text, Passphrase = null)

        /// <summary>
        /// Parse an <c>openssh-key-v1</c> private key from its PEM text.
        /// </summary>
        /// <param name="Text">The <c>-----BEGIN OPENSSH PRIVATE KEY-----</c> block.</param>
        /// <param name="Passphrase">The passphrase for an encrypted key (null for unencrypted keys).</param>
        public static SshLoadedKey Parse(String Text, String? Passphrase = null)
        {

            var blob    = DecodePem(Text);
            var reader  = new SshPacketReader(blob);

            if (!reader.ReadBytes(AuthMagic.Length).SequenceEqual(AuthMagic))
                throw new SshWireException("Not an openssh-key-v1 private key (bad magic).");

            var cipherName  = reader.ReadString();
            var kdfName     = reader.ReadString();
            var kdfOptions  = reader.ReadBinaryString();
            var keyCount    = reader.ReadUInt32();

            if (keyCount != 1)
                throw new SshWireException($"Only single-key openssh-key-v1 files are supported (found {keyCount}).");

            _               = reader.ReadBinaryString();        // public key (re-derived from the private key)
            var privateArea = reader.ReadBinaryString();

            var plaintext = cipherName == "none"
                                ? privateArea
                                : Decrypt(privateArea, cipherName, kdfName, kdfOptions, Passphrase);

            return ParsePrivateArea(plaintext);

        }

        #endregion

        #region Format(Key, Comment = "")

        /// <summary>
        /// Serialize a signing key as an unencrypted <c>openssh-key-v1</c> private key.
        /// </summary>
        public static String Format(ISshHostKey Key, String Comment = "")
        {

            var privateArea = BuildPrivateArea(Key, Comment, blockSize: 8);

            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);

            var magic = abw.GetSpan(AuthMagic.Length);
            AuthMagic.CopyTo(magic);
            abw.Advance(AuthMagic.Length);

            writer.WriteString("none");             // ciphername
            writer.WriteString("none");             // kdfname
            writer.WriteBinaryString([]);           // kdfoptions
            writer.WriteUInt32(1);                  // number of keys
            writer.WriteBinaryString(Key.PublicKeyBlob);
            writer.WriteBinaryString(privateArea);

            return EncodePem(abw.WrittenSpan);

        }

        #endregion


        #region (private) PEM

        private static Byte[] DecodePem(String Text)
        {

            var normalized  = Text.Replace("\r\n", "\n").Trim();
            var start       = normalized.IndexOf(PemHeader, StringComparison.Ordinal);
            var end         = normalized.IndexOf(PemFooter, StringComparison.Ordinal);

            if (start < 0 || end < 0 || end <= start)
                throw new SshWireException("Missing OPENSSH PRIVATE KEY PEM armor.");

            var body = normalized.Substring(start + PemHeader.Length, end - start - PemHeader.Length)
                                 .Replace("\n", "").Replace(" ", "");

            return Convert.FromBase64String(body);

        }

        private static String EncodePem(ReadOnlySpan<Byte> Blob)
        {

            var base64   = Convert.ToBase64String(Blob);
            var builder  = new StringBuilder();

            builder.Append(PemHeader).Append('\n');
            for (var offset = 0; offset < base64.Length; offset += 70)
                builder.Append(base64, offset, Math.Min(70, base64.Length - offset)).Append('\n');
            builder.Append(PemFooter).Append('\n');

            return builder.ToString();

        }

        #endregion

        #region (private) Decrypt(PrivateArea, CipherName, KdfName, KdfOptions, Passphrase)

        private static Byte[] Decrypt(Byte[] PrivateArea, String CipherName, String KdfName, Byte[] KdfOptions, String? Passphrase)
        {

            if (CipherName != "aes256-ctr")
                throw new SshWireException($"Unsupported openssh-key-v1 cipher '{CipherName}' (only aes256-ctr is supported).");

            if (KdfName != "bcrypt")
                throw new SshWireException($"Unsupported openssh-key-v1 KDF '{KdfName}'.");

            if (Passphrase is null)
                throw new SshAuthenticationException("A passphrase is required to decrypt this private key.");

            var optionsReader  = new SshPacketReader(KdfOptions);
            var salt           = optionsReader.ReadBinaryString();
            var rounds         = (Int32) optionsReader.ReadUInt32();

            // aes256-ctr: 32-byte key + 16-byte IV.
            var keyIv  = BcryptPbkdf.DeriveKey(Encoding.UTF8.GetBytes(Passphrase), salt, rounds, 32 + 16);
            var key    = keyIv.AsSpan(0, 32).ToArray();
            var iv     = keyIv.AsSpan(32, 16).ToArray();

            var plaintext = (Byte[]) PrivateArea.Clone();
            AesCtrXor(key, iv, plaintext);

            CryptographicOperations.ZeroMemory(keyIv);
            CryptographicOperations.ZeroMemory(key);

            return plaintext;

        }

        // Raw AES-256-CTR: keystream = AES_ECB(counter), big-endian 128-bit counter starting at the IV.
        private static void AesCtrXor(Byte[] Key, Byte[] Iv, Span<Byte> Data)
        {

            using var aes = Aes.Create();
            aes.Key      = Key;
            aes.Mode     = CipherMode.ECB;
            aes.Padding  = PaddingMode.None;

            using var encryptor = aes.CreateEncryptor();

            var counter    = (Byte[]) Iv.Clone();
            var keystream  = new Byte[16];

            for (var offset = 0; offset < Data.Length; offset += 16)
            {
                encryptor.TransformBlock(counter, 0, 16, keystream, 0);
                var count = Math.Min(16, Data.Length - offset);
                for (var i = 0; i < count; i++)
                    Data[offset + i] ^= keystream[i];
                IncrementBigEndian(counter);
            }

        }

        private static void IncrementBigEndian(Byte[] Counter)
        {
            for (var i = Counter.Length - 1; i >= 0; i--)
                if (++Counter[i] != 0)
                    break;
        }

        #endregion

        #region (private) Private area (de)serialization

        private static SshLoadedKey ParsePrivateArea(ReadOnlySpan<Byte> Plaintext)
        {

            var reader  = new SshPacketReader(Plaintext);
            var check1  = reader.ReadUInt32();
            var check2  = reader.ReadUInt32();

            if (check1 != check2)
                throw new SshAuthenticationException("Private-key check integers do not match (wrong passphrase?).");

            var keyType = reader.ReadString();
            var key     = ReadPrivateKey(keyType, ref reader);
            var comment = reader.ReadString();

            return new SshLoadedKey(key, comment);

        }

        private static ISshHostKey ReadPrivateKey(String KeyType, ref SshPacketReader Reader)
        {

            switch (KeyType)
            {

                case SshAlgorithmNames.HostKey.Ed25519:
                {
                    _         = Reader.ReadBinaryString();          // public key (32)
                    var priv  = Reader.ReadBinaryString();          // seed(32) || public(32)
                    return Ed25519KeyPair.FromSeed(priv.AsSpan(0, 32));
                }

                case SshAlgorithmNames.HostKey.EcdsaNistP256 or
                     SshAlgorithmNames.HostKey.EcdsaNistP384 or
                     SshAlgorithmNames.HostKey.EcdsaNistP521:
                {

                    var (_, curve, curveId, fieldSize) = SshSignature.EcdsaParametersFor(KeyType);

                    var curveName  = Reader.ReadString();
                    if (curveName != curveId)
                        throw new SshWireException($"ECDSA curve mismatch: key type {KeyType} but curve {curveName}.");

                    var point  = Reader.ReadBinaryString();
                    if (point.Length != 1 + (2 * fieldSize) || point[0] != 0x04)
                        throw new SshWireException("Invalid ECDSA public point in private key.");

                    var d = Reader.ReadMPInt();

                    var parameters = new ECParameters {
                                         Curve = curve,
                                         Q     = new ECPoint {
                                                     X = point[1..(1 + fieldSize)],
                                                     Y = point[(1 + fieldSize)..]
                                                 },
                                         D     = SshSignature.ToFixedLength(d, fieldSize)
                                     };

                    return new EcdsaHostKey(KeyType, parameters);

                }

                case SshAlgorithmNames.HostKey.SshRsa:
                {

                    var n     = Reader.ReadMPInt();
                    var e     = Reader.ReadMPInt();
                    var d     = Reader.ReadMPInt();
                    var iqmp  = Reader.ReadMPInt();
                    var p     = Reader.ReadMPInt();
                    var q     = Reader.ReadMPInt();

                    var modLen   = Unsigned(n).Length;
                    var halfLen  = (modLen + 1) / 2;

                    var parameters = new RSAParameters {
                                         Modulus   = Unsigned(n),
                                         Exponent  = Unsigned(e),
                                         D         = SshSignature.ToFixedLength(d,                modLen),
                                         P         = SshSignature.ToFixedLength(p,                halfLen),
                                         Q         = SshSignature.ToFixedLength(q,                halfLen),
                                         InverseQ  = SshSignature.ToFixedLength(iqmp,             halfLen),
                                         DP        = SshSignature.ToFixedLength(d % (p - 1),      halfLen),
                                         DQ        = SshSignature.ToFixedLength(d % (q - 1),      halfLen)
                                     };

                    return new RsaHostKey(parameters);

                }

                default:
                    throw new SshWireException($"Unsupported private-key type '{KeyType}'.");

            }

        }

        private static Byte[] BuildPrivateArea(ISshHostKey Key, String Comment, Int32 blockSize)
        {

            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);

            var check   = RandomNumberGenerator.GetBytes(4);
            writer.WriteUInt32(BinaryPrimitivesReadUInt32(check));
            writer.WriteUInt32(BinaryPrimitivesReadUInt32(check));

            WritePrivateKey(ref writer, Key);
            writer.WriteString(Comment);

            // Pad with 1, 2, 3, … up to a multiple of the cipher block size.
            Byte pad = 1;
            while (abw.WrittenCount % blockSize != 0)
                writer.WriteByte(pad++);

            return abw.WrittenSpan.ToArray();

        }

        private static void WritePrivateKey(ref SshPacketWriter Writer, ISshHostKey Key)
        {

            switch (Key)
            {

                case Ed25519KeyPair ed:
                {
                    var seedAndPublic = new Byte[64];
                    ed.PrivateSeed.CopyTo(seedAndPublic, 0);
                    ed.PublicKey.CopyTo(seedAndPublic, 32);

                    Writer.WriteString(SshAlgorithmNames.HostKey.Ed25519);
                    Writer.WriteBinaryString(ed.PublicKey);
                    Writer.WriteBinaryString(seedAndPublic);
                    break;
                }

                case EcdsaHostKey ecdsa:
                {
                    var parameters = ecdsa.ExportPrivateParameters();

                    var point = new Byte[1 + (2 * ecdsa.FieldSize)];
                    point[0]  = 0x04;
                    parameters.Q.X!.CopyTo(point, 1);
                    parameters.Q.Y!.CopyTo(point, 1 + ecdsa.FieldSize);

                    Writer.WriteString(ecdsa.AlgorithmNames[0]);
                    Writer.WriteString(ecdsa.CurveId);
                    Writer.WriteBinaryString(point);
                    Writer.WriteMPInt(new BigInteger(parameters.D!, isUnsigned: true, isBigEndian: true));
                    break;
                }

                case RsaHostKey rsa:
                {
                    var p = rsa.ExportPrivateParameters();

                    Writer.WriteString(SshAlgorithmNames.HostKey.SshRsa);
                    Writer.WriteMPInt(new BigInteger(p.Modulus!,  isUnsigned: true, isBigEndian: true));
                    Writer.WriteMPInt(new BigInteger(p.Exponent!, isUnsigned: true, isBigEndian: true));
                    Writer.WriteMPInt(new BigInteger(p.D!,        isUnsigned: true, isBigEndian: true));
                    Writer.WriteMPInt(new BigInteger(p.InverseQ!, isUnsigned: true, isBigEndian: true));
                    Writer.WriteMPInt(new BigInteger(p.P!,        isUnsigned: true, isBigEndian: true));
                    Writer.WriteMPInt(new BigInteger(p.Q!,        isUnsigned: true, isBigEndian: true));
                    break;
                }

                default:
                    throw new SshWireException($"Cannot export a private key of type {Key.GetType().Name}.");

            }

        }

        #endregion

        #region (private) helpers

        private static Byte[] Unsigned(BigInteger Value)
            => Value.ToByteArray(isUnsigned: true, isBigEndian: true);

        private static UInt32 BinaryPrimitivesReadUInt32(ReadOnlySpan<Byte> Bytes)
            => System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(Bytes);

        #endregion

    }

}
