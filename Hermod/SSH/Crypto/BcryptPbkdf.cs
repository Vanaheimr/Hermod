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

using System.Reflection;
using System.Security.Cryptography;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// The OpenSSH <c>bcrypt_pbkdf</c> key-derivation function used to decrypt passphrase-protected
    /// <c>openssh-key-v1</c> private keys. It is PBKDF2 with a bcrypt-based PRF (an "expensive key
    /// schedule" Blowfish, aka eksblowfish), as specified by OpenBSD/OpenSSH.
    /// </summary>
    /// <remarks>
    /// This is a construction, not a primitive: the underlying Blowfish rounds and the vetted initial
    /// P-array / S-box constants come from BouncyCastle's <c>BCrypt</c> (read once via reflection), while
    /// the eksblowfish key schedule and the PBKDF2 wrapping are implemented here per the reference. The
    /// end-to-end result is validated against keys produced by <c>ssh-keygen</c>.
    /// </remarks>
    public static class BcryptPbkdf
    {

        #region Data

        private const Int32 BcryptHashSize = 32;   // bytes produced by one bcrypt_hash
        private const Int32 BcryptWords    = 8;    // 32-bit words in one bcrypt block group

        // The standard Blowfish initial P-array (18) and S-boxes (4 × 256), taken from BouncyCastle.
        private static readonly UInt32[]  InitialP;
        private static readonly UInt32[]  InitialS;   // flat: 1024 = 4 × 256

        // "OxychromaticBlowfishSwatDynamite" as eight big-endian 32-bit words (the bcrypt_pbkdf magic).
        private static readonly UInt32[]  Magic;

        static BcryptPbkdf()
        {

            var bcrypt = typeof(Org.BouncyCastle.Security.SecureRandom).Assembly
                             .GetType("Org.BouncyCastle.Crypto.Generators.BCrypt")
                         ?? throw new InvalidOperationException("BouncyCastle BCrypt type not found.");

            UInt32[] Field(String Name)
                => (UInt32[]) bcrypt.GetField(Name, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

            InitialP = (UInt32[]) Field("KP").Clone();

            var s0 = Field("KS0"); var s1 = Field("KS1"); var s2 = Field("KS2"); var s3 = Field("KS3");
            InitialS = new UInt32[s0.Length + s1.Length + s2.Length + s3.Length];
            s0.CopyTo(InitialS,                                 0);
            s1.CopyTo(InitialS, s0.Length);
            s2.CopyTo(InitialS, s0.Length + s1.Length);
            s3.CopyTo(InitialS, s0.Length + s1.Length + s2.Length);

            var magicBytes = "OxychromaticBlowfishSwatDynamite"u8;
            Magic = new UInt32[BcryptWords];
            for (var i = 0; i < BcryptWords; i++)
                Magic[i] = (UInt32) ((magicBytes[4 * i] << 24) | (magicBytes[4 * i + 1] << 16) | (magicBytes[4 * i + 2] << 8) | magicBytes[4 * i + 3]);

        }

        #endregion

        #region DeriveKey(Passphrase, Salt, Rounds, KeyLength)

        /// <summary>
        /// Derive <paramref name="KeyLength"/> bytes of key material from a passphrase and salt.
        /// </summary>
        /// <param name="Passphrase">The passphrase bytes (UTF-8).</param>
        /// <param name="Salt">The salt bytes (16 for openssh-key-v1).</param>
        /// <param name="Rounds">The number of rounds (the KDF work factor).</param>
        /// <param name="KeyLength">The number of output bytes (cipher key + IV).</param>
        public static Byte[] DeriveKey(ReadOnlySpan<Byte> Passphrase, ReadOnlySpan<Byte> Salt, Int32 Rounds, Int32 KeyLength)
        {

            if (Rounds < 1)
                throw new ArgumentOutOfRangeException(nameof(Rounds));
            if (KeyLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(KeyLength));

            var sha2pass    = SHA512.HashData(Passphrase);
            var key         = new Byte[KeyLength];

            var origKeyLen  = KeyLength;
            var remaining   = KeyLength;
            var stride      = (KeyLength + BcryptHashSize - 1) / BcryptHashSize;
            var amt         = (KeyLength + stride - 1) / stride;

            var countSalt   = new Byte[Salt.Length + 4];
            Salt.CopyTo(countSalt);

            var outBytes    = new Byte[BcryptHashSize];
            var tmpOut      = new Byte[BcryptHashSize];

            for (var count = 1; remaining > 0; count++)
            {

                countSalt[Salt.Length + 0] = (Byte) (count >> 24);
                countSalt[Salt.Length + 1] = (Byte) (count >> 16);
                countSalt[Salt.Length + 2] = (Byte) (count >> 8);
                countSalt[Salt.Length + 3] = (Byte)  count;

                var sha2salt = SHA512.HashData(countSalt);
                BcryptHash(sha2pass, sha2salt, tmpOut);
                tmpOut.CopyTo(outBytes, 0);

                for (var i = 1; i < Rounds; i++)
                {
                    sha2salt = SHA512.HashData(tmpOut);
                    BcryptHash(sha2pass, sha2salt, tmpOut);
                    for (var j = 0; j < BcryptHashSize; j++)
                        outBytes[j] ^= tmpOut[j];
                }

                var take = Math.Min(amt, remaining);
                var written = 0;
                for (var i = 0; i < take; i++)
                {
                    var dest = (i * stride) + (count - 1);
                    if (dest >= origKeyLen)
                        break;
                    key[dest] = outBytes[i];
                    written++;
                }

                remaining -= written;

            }

            CryptographicOperations.ZeroMemory(sha2pass);
            CryptographicOperations.ZeroMemory(outBytes);
            CryptographicOperations.ZeroMemory(tmpOut);

            return key;

        }

        #endregion


        #region (private) BcryptHash(Sha2Pass, Sha2Salt, Output)

        // One bcrypt_hash: eksblowfish keyed by (salt, pass), then 64 encryptions of the magic string.
        private static void BcryptHash(ReadOnlySpan<Byte> Sha2Pass, ReadOnlySpan<Byte> Sha2Salt, Span<Byte> Output)
        {

            var p = (UInt32[]) InitialP.Clone();
            var s = (UInt32[]) InitialS.Clone();

            ExpandState(p, s, Sha2Salt, Sha2Pass);
            for (var i = 0; i < 64; i++)
            {
                Expand0State(p, s, Sha2Salt);
                Expand0State(p, s, Sha2Pass);
            }

            var cdata = (UInt32[]) Magic.Clone();
            for (var i = 0; i < 64; i++)
                for (var block = 0; block < BcryptWords / 2; block++)
                    Encipher(p, s, ref cdata[2 * block], ref cdata[(2 * block) + 1]);

            // Output is little-endian words.
            for (var i = 0; i < BcryptWords; i++)
            {
                Output[(4 * i) + 3] = (Byte) (cdata[i] >> 24);
                Output[(4 * i) + 2] = (Byte) (cdata[i] >> 16);
                Output[(4 * i) + 1] = (Byte) (cdata[i] >> 8);
                Output[(4 * i) + 0] = (Byte)  cdata[i];
            }

        }

        #endregion

        #region (private) eksblowfish

        private static void ExpandState(UInt32[] P, UInt32[] S, ReadOnlySpan<Byte> Data, ReadOnlySpan<Byte> Key)
        {

            var keyOffset = 0;
            for (var i = 0; i < P.Length; i++)
                P[i] ^= Stream2Word(Key, ref keyOffset);

            UInt32 dataL = 0, dataR = 0;
            var dataOffset = 0;

            for (var i = 0; i < P.Length; i += 2)
            {
                dataL ^= Stream2Word(Data, ref dataOffset);
                dataR ^= Stream2Word(Data, ref dataOffset);
                Encipher(P, S, ref dataL, ref dataR);
                P[i]     = dataL;
                P[i + 1] = dataR;
            }

            for (var box = 0; box < 4; box++)
            {
                for (var k = 0; k < 256; k += 2)
                {
                    dataL ^= Stream2Word(Data, ref dataOffset);
                    dataR ^= Stream2Word(Data, ref dataOffset);
                    Encipher(P, S, ref dataL, ref dataR);
                    S[(box * 256) + k]     = dataL;
                    S[(box * 256) + k + 1] = dataR;
                }
            }

        }

        private static void Expand0State(UInt32[] P, UInt32[] S, ReadOnlySpan<Byte> Key)
        {

            var keyOffset = 0;
            for (var i = 0; i < P.Length; i++)
                P[i] ^= Stream2Word(Key, ref keyOffset);

            UInt32 dataL = 0, dataR = 0;

            for (var i = 0; i < P.Length; i += 2)
            {
                Encipher(P, S, ref dataL, ref dataR);
                P[i]     = dataL;
                P[i + 1] = dataR;
            }

            for (var box = 0; box < 4; box++)
            {
                for (var k = 0; k < 256; k += 2)
                {
                    Encipher(P, S, ref dataL, ref dataR);
                    S[(box * 256) + k]     = dataL;
                    S[(box * 256) + k + 1] = dataR;
                }
            }

        }

        // Read four bytes big-endian from Data, cycling back to the start when exhausted.
        private static UInt32 Stream2Word(ReadOnlySpan<Byte> Data, ref Int32 Offset)
        {
            UInt32 word = 0;
            for (var i = 0; i < 4; i++)
            {
                if (Offset >= Data.Length)
                    Offset = 0;
                word = (word << 8) | Data[Offset];
                Offset++;
            }
            return word;
        }

        // Standard 16-round Blowfish encipher over (L, R).
        private static void Encipher(UInt32[] P, UInt32[] S, ref UInt32 L, ref UInt32 R)
        {

            var xl = L;
            var xr = R;

            for (var round = 0; round < 16; round++)
            {
                xl ^= P[round];
                xr ^= F(S, xl);
                (xl, xr) = (xr, xl);
            }

            (xl, xr) = (xr, xl);   // undo the final swap
            xr ^= P[16];
            xl ^= P[17];

            L = xl;
            R = xr;

        }

        private static UInt32 F(UInt32[] S, UInt32 X)
            => ((S[            (Byte) (X >> 24)]  +
                 S[256 +       (Byte) (X >> 16)]) ^
                 S[512 +       (Byte) (X >> 8)])  +
                 S[768 +       (Byte)  X];

        #endregion

    }

}
