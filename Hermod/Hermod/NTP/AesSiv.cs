/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.NTP
{

    public class AesSiv
    {

        private readonly Byte[] k1; // Für CMAC (S2V)
        private readonly Byte[] k2; // Für CTR-Verschlüsselung

        /// <summary>
        /// Erzeugt einen AES-SIV-Encryptor.
        /// Erwartet einen Schlüssel der Länge 32 (für AES-128 SIV) oder 64 (für AES-256 SIV) Bytes.
        /// </summary>
        public AesSiv(Byte[] key)
        {

            if (key.Length != 32 && key.Length != 64)
                throw new ArgumentException("Key length must be either 32 or 64 Bytes for AES-SIV", nameof(key));

            var half = key.Length / 2;
            k1 = new Byte[half];
            k2 = new Byte[half];

            Array.Copy(key,    0, k1, 0, half);
            Array.Copy(key, half, k2, 0, half);

        }


        public Byte[] Encrypt(Byte[] associatedData, Byte[] plaintext)
        {
            return Encrypt([ associatedData ], plaintext);
        }

        /// <summary>
        /// Verschlüsselt die gegebene Plaintext-Nachricht unter Berücksichtigung der Associated Data.
        /// Rückgabe: SIV || Ciphertext
        /// </summary>
        public Byte[] Encrypt(IList<Byte[]> associatedData, Byte[] plaintext)
        {

            // 1. Berechne den SIV (synthetischer IV) mittels S2V
            var siv         = S2V(associatedData, plaintext);

            // 2. Verschlüssele den Plaintext mit AES-CTR (SICBlockCipher) unter Verwendung von k2 und IV = siv.
            var ciphertext  = AesCtrEncrypt(plaintext, siv, k2);

            // 3. Ausgabe: SIV || ciphertext
            var result      = new Byte[siv.Length + ciphertext.Length];

            Array.Copy(siv,        0, result,          0, siv.       Length);
            Array.Copy(ciphertext, 0, result, siv.Length, ciphertext.Length);

            return result;

        }

        /// <summary>
        /// Implementiert die S2V-Funktion gemäß RFC 5297.
        /// Eingaben: eine Liste von Associated Data-Blöcken und der Plaintext (als letzter Block).
        /// </summary>
        private Byte[] S2V(IList<Byte[]> ad, Byte[] plaintext)
        {

            // Initial: D_0 = CMAC(k1, 0^128)
            var D = Cmac(k1, new Byte[16]);

            // Für jeden Associated Data Block
            foreach (var X in ad)
            {
                D = Dbl(D);
                Byte[] cmacX = Cmac(k1, X);
                D = Xor(D, cmacX);
            }

            // Letzter Block: Plaintext
            if (plaintext.Length > 0)
            {
                Byte[] T = Cmac(k1, plaintext);
                return Xor(D, T);
            }
            else
                return Dbl(D);

        }

        /// <summary>
        /// Berechnet den AES-CMAC über die Nachricht mithilfe von k.
        /// </summary>
        private Byte[] Cmac(Byte[] key, Byte[] message)
        {

            var cmac = new CMac(new AesEngine());
            cmac.Init(new KeyParameter(key));
            cmac.BlockUpdate(message, 0, message.Length);

            var output = new Byte[cmac.GetMacSize()];
            cmac.DoFinal(output, 0);

            return output;

        }

        /// <summary>
        /// Verdoppelt einen 16-Byte Block in GF(2^128) gemäß RFC 5297 (dbl()-Funktion).
        /// </summary>
        private Byte[] Dbl(Byte[] block)
        {

            if (block.Length != 16)
                throw new ArgumentException("Block must be 16 Bytes", nameof(block));

            var output = new Byte[16];
            Byte carry = 0;

            for (var i = 15; i >= 0; i--)
            {
                var b        = block[i];
                var shifted  = (b << 1) | carry;
                output[i]    = (Byte) (shifted & 0xFF);
                carry        = (Byte) ((b & 0x80) != 0 ? 1 : 0);
            }

            // Polynom: x^128 + x^7 + x^2 + x + 1  -> R = 0x87
            if (carry != 0)
                output[15] ^= 0x87;

            return output;

        }

        /// <summary>
        /// XOR verknüpft zwei gleich lange Byte-Arrays.
        /// </summary>
        private Byte[] Xor(Byte[] a, Byte[] b)
        {

            if (a.Length != b.Length)
                throw new ArgumentException("Arrays must have same length");

            var result = new Byte[a.Length];
            for (var i = 0; i < a.Length; i++)
                result[i] = (Byte)(a[i] ^ b[i]);

            return result;

        }

        /// <summary>
        /// Verschlüsselt mit AES-CTR (SICBlockCipher von BC) unter Verwendung von key und iv.
        /// </summary>
        private Byte[] AesCtrEncrypt(Byte[] plaintext, Byte[] iv, Byte[] key)
        {

            // Setze AES-CTR (SIC) ein
            var ctr         = new SicBlockCipher(new AesEngine());
            var keyParam    = new KeyParameter(key);
            var parameters  = new ParametersWithIV(keyParam, iv);

            ctr.Init(true, parameters);
            var output      = new Byte[plaintext.Length];
            var blockSize   = ctr.GetBlockSize();

            //ToDo: Add padding, when the plaintext is not a multiple of the block size!
            for (var i = 0; i < plaintext.Length; i += blockSize)
            {
                var chunk = Math.Min(blockSize, plaintext.Length - i);
                ctr.ProcessBlock(plaintext, i, output, i);
            }

            return output;

        }

    }

}
