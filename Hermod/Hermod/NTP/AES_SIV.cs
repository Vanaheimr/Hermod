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
using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using System.Linq;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.NTP
{

    /// <summary>
    /// AES Synthetic Initialization Vector Encryption (AES-SIV-CMAC-256)
    /// </summary>
    public class AES_SIV
    {

        #region Properties

        /// <summary>
        /// Key1 for CMAC (S2V).
        /// </summary>
        public Byte[]  Key1_CMAC      { get; }

        /// <summary>
        /// Key2 for AES-Counter Mode.
        /// </summary>
        public Byte[]  Key2_AESCTR    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new AES Synthetic Initialization Vector (AES-SIV) encryption.
        /// </summary>
        public AES_SIV(Byte[] Key)
        {

            if (Key.Length != 32 && Key.Length != 64)
                throw new ArgumentException("Key length must be either 32 or 64 Bytes for AES-SIV", nameof(Key));

            var half = Key.Length / 2;

            Key1_CMAC    = new Byte[half];
            Key2_AESCTR  = new Byte[half];

            Array.Copy(Key,    0, Key1_CMAC,   0, half);
            Array.Copy(Key, half, Key2_AESCTR, 0, half);

        }

        #endregion


        #region            Encrypt(AssociatedData, Plaintext)

        /// <summary>
        /// Encrypts the given plaintext message considering the associated data using AES Counter Mode (AES-CTR)
        /// with a synthetic initialization vector based on plaintext and associated data, and the second part of the key.
        /// 
        /// It will return the synthetic initialization vector (SIV) concatenated with the ciphertext: SIV|Ciphertext
        /// </summary>
        public Byte[] Encrypt(IEnumerable<Byte[]>  AssociatedData,
                              Byte[]               Nonce,
                              Byte[]               Plaintext)
        {

            var syntheticIV  = String2InitializationVector(AssociatedData, Nonce, Plaintext);
            var ciphertext   = AES_CTR_Encrypt(Plaintext, syntheticIV, Key2_AESCTR);
            var result       = new Byte[syntheticIV.Length + ciphertext.Length];

            DebugX.Log($"key (CMAC):   '{Key1_CMAC.  ToHexString()}'");
            DebugX.Log($"key (CTR):    '{Key2_AESCTR.ToHexString()}'");
            DebugX.Log($"syntheticIV:  '{syntheticIV.ToHexString()}'");
            foreach (var ad in AssociatedData.SelectCounted((ad, c) => $"assocdata({c}): '{ad.ToHexString()}'"))
                DebugX.Log(ad);
            DebugX.Log($"nonce:        '{Nonce.      ToHexString()}'");
            DebugX.Log($"ciphertext:   '{ciphertext. ToHexString()}'");

            Array.Copy(syntheticIV, 0, result,                  0, syntheticIV.Length);
            Array.Copy(ciphertext,  0, result, syntheticIV.Length, ciphertext. Length);

            return result;

        }

        #endregion

        #region            Decrypt(AssociatedData, Nonce, SyntheticIVAndCiphertext)

        /// <summary>
        /// Decrypts an encrypted message of the form syntheticIV || ciphertext.
        /// Recomputes the expected synthetic IV using the associated data and nonce (which may be a salt)
        /// and verifies that it matches the one embedded in the message.
        /// Throws CryptographicException if authentication fails.
        /// </summary>
        public Byte[] Decrypt(IEnumerable<Byte[]>  AssociatedData,
                              Byte[]               Nonce,
                              Byte[]               SyntheticIVAndCiphertext)
        {

            var sivLen = 16; // Assume synthetic IV is 16 bytes.
            if (SyntheticIVAndCiphertext.Length < sivLen)
                throw new ArgumentException("Encrypted message too short.");

            var syntheticIV  = new Byte[sivLen];
            Array.Copy(SyntheticIVAndCiphertext, 0, syntheticIV, 0, sivLen);

            var ciphertext   = new Byte[SyntheticIVAndCiphertext.Length - sivLen];
            Array.Copy(SyntheticIVAndCiphertext, sivLen, ciphertext, 0, SyntheticIVAndCiphertext.Length - sivLen);

            // AES-CTR decryption is the same as encryption:
            var plaintext    = AES_CTR_Encrypt(ciphertext, syntheticIV, Key2_AESCTR);

            // Recompute the synthetic IV from associated data, nonce, and the decrypted plaintext:
            var computedSIV  = String2InitializationVector(AssociatedData, Nonce, plaintext);

            var a = AssociatedData.Select(ad => ad.ToHexString()).AggregateWith(" / ");
            var n = Nonce.      ToHexString();
            var s = syntheticIV.ToHexString();
            var c = ciphertext. ToHexString();
            var z = computedSIV.ToHexString();

            var o = $"{a} - {n} - {s} - {c} - {z}";

            if (!syntheticIV.SequenceEqual(computedSIV))
                throw new Exception("Authentication failed: SIV mismatch!");

            return plaintext;

        }

        #endregion


        #region (private)        String2InitializationVector (AssociatedData, Plaintext)

        /// <summary>
        /// Implements the "String to Initialization Vector" (S2V) function according to RFC 5297.
        /// https://www.rfc-editor.org/rfc/rfc5297.html#section-2.4
        /// </summary>
        private Byte[] String2InitializationVector(IEnumerable<Byte[]>  AssociatedData,
                                                   Byte[]               Nonce,
                                                   Byte[]               Plaintext)
        {

            // Special case: If there are no associated data blocks
            // and no plaintext, return CMAC(K, <one>)
            if (!AssociatedData.Any() && Plaintext.Length == 0)
                return CMAC(Key1_CMAC, [ 0x01 ]);


            // Step 1: Initialize D with CMAC(K, <zero>), meaning 0^128
            var D          = CMAC(Key1_CMAC, new Byte[16]);


            #region Build byte blocks

            var byteBlocks = new List<Byte[]>();

            foreach (var associatedData in AssociatedData)
                byteBlocks.Add(associatedData);

            if (Nonce.    Length > 0)
                byteBlocks.Add(Nonce);

            if (Plaintext.Length > 0)
                byteBlocks.Add(Plaintext);

            #endregion

            foreach (var byteBlock in byteBlocks.Take(byteBlocks.Count - 1))
            {
                D = DoubleBlock(D);
                var cmacX = CMAC(Key1_CMAC, byteBlock);
                D = XOR_Blocks(D, cmacX);
            }

            Byte[] T;
            var lastBlock = byteBlocks.Last();
            if (lastBlock.Length >= 16)
            {

                // Take the last 16 bytes of the last block
                var lastBytesOfTheBlock = new Byte[16];
                Buffer.BlockCopy(lastBlock, lastBlock.Length - 16, lastBytesOfTheBlock, 0, 16);
                var T1 = AES_SIV.XOR_Blocks(lastBytesOfTheBlock, D);

                T = new Byte[lastBlock.Length];
                Buffer.BlockCopy(lastBlock, 0, T,                     0, lastBlock.Length);
                Buffer.BlockCopy(T1,        0, T, lastBlock.Length - 16,               16);

            }
            else
            {
                D = AES_SIV.DoubleBlock(D);
                var padded = Pad(lastBlock);
                T = XOR_Blocks(D, padded);
            }


            //// 2. Hash all associated data blocks
            //foreach (var associatedData in AssociatedData)
            //{
            //    D          = DoubleBlock(D);
            //    var cmacX  = CMAC(Key1_CMAC, associatedData);
            //    D          = XOR_Blocks(D, cmacX);
            //}

            //// Nonce
            //if (Nonce.Length > 0)
            //{
            //    D          = DoubleBlock(D);
            //    var cmacN  = CMAC(Key1_CMAC, Nonce);
            //    D          = XOR_Blocks(D, cmacN);
            //}

            //Byte[] T;
            //if (Plaintext.Length >= 16)
            //{

            //    // Take the last 16 bytes of the plaintext
            //    var lastBlock = new Byte[16];
            //    Buffer.BlockCopy(Plaintext, Plaintext.Length - 16, lastBlock, 0, 16);
            //    var T1 = AES_SIV.XOR_Blocks(lastBlock, D);

            //    T = new Byte[Plaintext.Length];
            //    Buffer.BlockCopy(Plaintext, 0, T,                     0, Plaintext.Length);
            //    Buffer.BlockCopy(T1,        0, T, Plaintext.Length - 16,               16);

            //}
            //else
            //{
            //    D = AES_SIV.DoubleBlock(D);
            //    var padded = Pad(Plaintext);
            //    T = XOR_Blocks(D, padded);
            //}


            // 4. Final: V = CMAC(K, T)
            return CMAC(Key1_CMAC, T);

        }

        #endregion

        #region         (static) CMAC                        (Key, Message)

        /// <summary>
        /// Calculate the AES Cipher-based Message Authentication Code (AES-CMAC)
        /// over the message using the given key.
        /// </summary>
        /// <param name="Key">The key to use.</param>
        /// <param name="Message">The message to calculate the CMAC for.</param>
        public static Byte[] CMAC(Byte[] Key,
                                  Byte[] Message)
        {

            var cmac   = new CMac(new AesEngine());
            cmac.Init(new KeyParameter(Key));
            cmac.BlockUpdate(Message, 0, Message.Length);

            var output = new Byte[cmac.GetMacSize()];
            cmac.DoFinal(output, 0);

            return output;

        }

        #endregion

        #region (static) DoubleBlock                 (Block)

        /// <summary>
        /// Verdoppelt einen 16-Byte Block in GF(2^128) gemäß RFC 5297 (dbl()-Funktion).
        /// </summary>
        /// <param name="Block">A 16-Byte block.</param>
        public static Byte[] DoubleBlock(Byte[] Block)
        {

            if (Block.Length != 16)
                throw new ArgumentException("Block must be 16 Bytes", nameof(Block));

            var output = new Byte[16];
            Byte carry = 0;

            for (var i = 15; i >= 0; i--)
            {
                var b        = Block[i];
                var shifted  = (b << 1) | carry;
                output[i]    = (Byte) (shifted & 0xFF);
                carry        = (Byte) ((b & 0x80) != 0 ? 1 : 0);
            }

            // Polynom: x^128 + x^7 + x^2 + x + 1  -> R = 0x87
            if (carry != 0)
                output[15] ^= 0x87;

            return output;

        }

        #endregion

        #region (static) XOR_Blocks                  (BlockA, BlockB)

        /// <summary>
        /// XOR two equally long byte arrays.
        /// </summary>
        /// <param name="BlockA">The first byte array.</param>
        /// <param name="BlockB">The second byte array.</param>
        public static Byte[] XOR_Blocks(Byte[] BlockA,
                                        Byte[] BlockB)
        {

            if (BlockA.Length != BlockB.Length)
                throw new ArgumentException("Arrays must have same length");

            var result = new Byte[BlockA.Length];

            for (var i = 0; i < BlockA.Length; i++)
                result[i] = (Byte) (BlockA[i] ^ BlockB[i]);

            return result;

        }

        #endregion

        #region (static) Pad                         (Data)

        public static Byte[] Pad(Byte[] Data)
        {

            // https://www.rfc-editor.org/rfc/rfc5297.html
            // 2. Specification of SIV
            // 2.1. Notation
            // pad(X)
            // indicates padding of string X, len(X) < 128, out to 128 bits by
            // the concatenation of a single bit of 1 followed by as many 0 bits
            // as are necessary.
            //
            // pad(data) = data || 0x80 || 0^j, till the total length is 16 Bytes.

            var padded = new Byte[16];
            Buffer.BlockCopy(Data, 0, padded, 0, Data.Length);
            padded[Data.Length] = 0x80;

            return padded;

        }

        #endregion

        #region (static) AES_CTR_Encrypt             (Plaintext, InitializationVector, SharedKey)

        /// <summary>
        /// Encrypts the given plaintext message using AES Counter Mode (AES-CTR)
        /// with the given initialization vector and shared key.
        /// </summary>
        public static Byte[] AES_CTR_Encrypt(Byte[] Plaintext,
                                             Byte[] InitializationVector,
                                             Byte[] SharedKey)
        {

            var ivCTR       = new Byte[InitializationVector.Length];
            Buffer.BlockCopy(InitializationVector, 0, ivCTR, 0, InitializationVector.Length);

            // rfc5297 Section 2.5: Clear the 31st and 63rd bit (rightmost bit = 0th)
            ivCTR[ 8] &= 0x7F;
            ivCTR[12] &= 0x7F;

            var cipher      = new SicBlockCipher  (new AesEngine());
            var parameters  = new ParametersWithIV(new KeyParameter(SharedKey), ivCTR);
            cipher.Init(true, parameters);

            var blockSize        = cipher.GetBlockSize();
            var paddedLength     = ((Plaintext.Length + blockSize - 1) / blockSize) * blockSize;
            var paddedPlaintext  = new Byte[paddedLength];
            Array.Copy(Plaintext, paddedPlaintext, Plaintext.Length);

            var paddedCiphertext = new Byte[paddedLength];

            for (var i = 0; i < paddedPlaintext.Length; i += blockSize)
                cipher.ProcessBlock(paddedPlaintext, i, paddedCiphertext, i);

            var actualCiphertext = new Byte[Plaintext.Length];
            Array.Copy(paddedCiphertext, actualCiphertext, Plaintext.Length);

            //var output      = new Byte[Plaintext.Length];

            //var blockIn     = new Byte[blockSize];
            //var blockOut    = new Byte[blockSize];

            //for (var i = 0; i < Plaintext.Length; i += blockSize)
            //{

            //    var chunk = Math.Min(blockSize, Plaintext.Length - i);

            //    if (chunk == blockSize)
            //        cipher.ProcessBlock(Plaintext, i, output, i);

            //    else
            //    {
            //        Array.Clear(blockIn,   0, blockSize);
            //        Array.Copy (Plaintext, i, blockIn, 0, chunk);
            //        cipher.ProcessBlock(blockIn, 0, blockOut, 0);
            //        Array.Copy (blockOut,  0, output,  i, chunk);
            //    }

            //}

            return actualCiphertext;

        }

        #endregion


    }

}
