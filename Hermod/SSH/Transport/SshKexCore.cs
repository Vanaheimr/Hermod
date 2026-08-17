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
using System.Security.Cryptography;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// The shared building blocks of an SSH key exchange, used by both the initial handshake
    /// (<see cref="SshHandshake"/>) and a later key re-exchange (<see cref="SshTransport"/>): the
    /// supported-algorithm gate, the directional cipher/MAC factory, the key-derivation closure and
    /// the SSH_MSG_KEX_ECDH_INIT / _REPLY message builders and parsers (RFC 4253, RFC 5656, RFC 8731).
    /// </summary>
    internal static class SshKexCore
    {

        #region EnsureSupported(Negotiated)

        // Current support: curve25519 / ecdh-nistp* / dh-group14|16 + ssh-ed25519|ecdsa|rsa-sha2 host
        // keys + (aes*-gcm | chacha20-poly1305 | aes*-ctr with an -etm MAC).
        public static void EnsureSupported(NegotiatedAlgorithms Negotiated)
        {

            if (Negotiated.KeyExchange is not (SshAlgorithmNames.Kex.Curve25519Sha256          or
                                               SshAlgorithmNames.Kex.Curve25519Sha256LibSsh    or
                                               SshAlgorithmNames.Kex.EcdhNistP256               or
                                               SshAlgorithmNames.Kex.EcdhNistP384               or
                                               SshAlgorithmNames.Kex.EcdhNistP521               or
                                               SshAlgorithmNames.Kex.DhGroup14Sha256            or
                                               SshAlgorithmNames.Kex.DhGroup16Sha512            or
                                               SshAlgorithmNames.Kex.MlKem768X25519Sha256       or
                                               SshAlgorithmNames.Kex.SntruP761X25519Sha512      or
                                               SshAlgorithmNames.Kex.SntruP761X25519Sha512LibSsh))
                throw new SshWireException($"Unsupported key exchange '{Negotiated.KeyExchange}' (supported: mlkem768x25519-sha256, sntrup761x25519-sha512, curve25519-sha256, ecdh-sha2-nistp256/384/521, diffie-hellman-group14-sha256, diffie-hellman-group16-sha512).");

            if (Negotiated.HostKey is not (SshAlgorithmNames.HostKey.Ed25519       or
                                           SshAlgorithmNames.HostKey.EcdsaNistP256 or
                                           SshAlgorithmNames.HostKey.EcdsaNistP384 or
                                           SshAlgorithmNames.HostKey.EcdsaNistP521 or
                                           SshAlgorithmNames.HostKey.RsaSha2_256   or
                                           SshAlgorithmNames.HostKey.RsaSha2_512)   &&
                !SshCertificate.IsCertificateAlgorithm(Negotiated.HostKey))
                throw new SshWireException($"Unsupported host key '{Negotiated.HostKey}' (supported: ssh-ed25519, ecdsa-sha2-nistp256/384/521, rsa-sha2-256/512 and their -cert-v01@openssh.com certificates).");

            EnsureCipherSupported(Negotiated.CipherClientToServer, Negotiated.MacClientToServer);
            EnsureCipherSupported(Negotiated.CipherServerToClient, Negotiated.MacServerToClient);

        }

        private static void EnsureCipherSupported(String Cipher, String Mac)
        {

            var isAead = Cipher is SshAlgorithmNames.Cipher.Aes256Gcm or SshAlgorithmNames.Cipher.Aes128Gcm or SshAlgorithmNames.Cipher.ChaCha20Poly1305;
            var isCtr  = Cipher is SshAlgorithmNames.Cipher.Aes256Ctr or SshAlgorithmNames.Cipher.Aes192Ctr or SshAlgorithmNames.Cipher.Aes128Ctr;

            if (!isAead && !isCtr)
                throw new SshWireException($"Unsupported cipher '{Cipher}' (supported: chacha20-poly1305@openssh.com, aes*-gcm@openssh.com, aes*-ctr).");

            if (isCtr && Mac is not (SshAlgorithmNames.Mac.HmacSha2_256Etm or SshAlgorithmNames.Mac.HmacSha2_512Etm))
                throw new SshWireException($"The CTR cipher '{Cipher}' requires an encrypt-then-MAC ('{Mac}' is not supported yet).");

        }

        #endregion

        #region MakeDeriver / BuildDirection / BuildMac

        /// <summary>
        /// A closure deriving <c>length</c> key bytes for a key-derivation letter (RFC 4253 §7.2).
        /// </summary>
        public static Func<Byte, Int32, Byte[]> MakeDeriver(HashAlgorithmName HashAlgorithm, Byte[] SharedSecretMPInt, Byte[] H, Byte[] SessionId)
            => (letter, length) => Kdf.Derive(HashAlgorithm, SharedSecretMPInt, H, letter, SessionId, length);


        /// <summary>
        /// Build the cipher (and, for CTR, the encrypt-then-MAC) for one direction.
        /// </summary>
        public static (SshTransportCipher Cipher, ISshMac? Mac) BuildDirection(String                    CipherName,
                                                                               String                    MacName,
                                                                               Func<Byte, Int32, Byte[]> Derive,
                                                                               Byte                      KeyLetter,
                                                                               Byte                      IVLetter,
                                                                               Byte                      MacLetter)

            => CipherName switch {
                   SshAlgorithmNames.Cipher.ChaCha20Poly1305 => (new ChaCha20Poly1305Cipher(Derive(KeyLetter, ChaCha20Poly1305Cipher.KeyLength)), null),
                   SshAlgorithmNames.Cipher.Aes256Gcm => (new AesGcmTransportCipher(Derive(KeyLetter, 32), Derive(IVLetter, AesGcmTransportCipher.NonceLength)), null),
                   SshAlgorithmNames.Cipher.Aes128Gcm => (new AesGcmTransportCipher(Derive(KeyLetter, 16), Derive(IVLetter, AesGcmTransportCipher.NonceLength)), null),
                   SshAlgorithmNames.Cipher.Aes256Ctr => (new AesCtrTransportCipher(Derive(KeyLetter, 32), Derive(IVLetter, AesCtrTransportCipher.CounterLength)), BuildMac(MacName, Derive, MacLetter)),
                   SshAlgorithmNames.Cipher.Aes192Ctr => (new AesCtrTransportCipher(Derive(KeyLetter, 24), Derive(IVLetter, AesCtrTransportCipher.CounterLength)), BuildMac(MacName, Derive, MacLetter)),
                   SshAlgorithmNames.Cipher.Aes128Ctr => (new AesCtrTransportCipher(Derive(KeyLetter, 16), Derive(IVLetter, AesCtrTransportCipher.CounterLength)), BuildMac(MacName, Derive, MacLetter)),
                   _                                  => throw new SshWireException($"Unsupported cipher '{CipherName}'.")
               };

        private static ISshMac BuildMac(String MacName, Func<Byte, Int32, Byte[]> Derive, Byte MacLetter)
            => MacName switch {
                   SshAlgorithmNames.Mac.HmacSha2_256Etm => HmacSha2Mac.Sha256(Derive(MacLetter, 32)),
                   SshAlgorithmNames.Mac.HmacSha2_512Etm => HmacSha2Mac.Sha512(Derive(MacLetter, 64)),
                   _                                     => throw new SshWireException($"Unsupported MAC '{MacName}'.")
               };

        #endregion

        #region KEX_ECDH message builders / parsers

        public static Byte[] BuildEcdhInit(ReadOnlySpan<Byte> Q_C)
        {
            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);
            writer.WriteByte((Byte) SshMessageNumber.KexEcdhInit);
            writer.WriteBinaryString(Q_C);
            return abw.WrittenSpan.ToArray();
        }

        public static Byte[] ParseEcdhInit(ReadOnlySpan<Byte> Payload)
        {
            var reader  = new SshPacketReader(Payload);
            if (reader.ReadByte() != (Byte) SshMessageNumber.KexEcdhInit)
                throw new SshWireException("Expected SSH_MSG_KEX_ECDH_INIT (30)!");
            return reader.ReadBinaryString();
        }

        public static Byte[] BuildEcdhReply(ReadOnlySpan<Byte> K_S, ReadOnlySpan<Byte> Q_S, ReadOnlySpan<Byte> Signature)
        {
            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);
            writer.WriteByte((Byte) SshMessageNumber.KexEcdhReply);
            writer.WriteBinaryString(K_S);
            writer.WriteBinaryString(Q_S);
            writer.WriteBinaryString(Signature);
            return abw.WrittenSpan.ToArray();
        }

        public static (Byte[] K_S, Byte[] Q_S, Byte[] Signature) ParseEcdhReply(ReadOnlySpan<Byte> Payload)
        {
            var reader  = new SshPacketReader(Payload);
            if (reader.ReadByte() != (Byte) SshMessageNumber.KexEcdhReply)
                throw new SshWireException("Expected SSH_MSG_KEX_ECDH_REPLY (31)!");
            var kS   = reader.ReadBinaryString();
            var qS   = reader.ReadBinaryString();
            var sig  = reader.ReadBinaryString();
            return (kS, qS, sig);
        }

        #endregion

    }

}
