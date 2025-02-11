/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using NUnit.Framework;

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.NTP
{

    [TestFixture]
    public class AES_CTR_Tests
    {

        #region TestAES_CTR_Encryption_TestVector2a()

        /// <summary>
        /// https://www.rfc-editor.org/rfc/rfc3686#section-6
        /// Test Vector #2: Encrypting 32 octets using AES-CTR with 128-bit key
        /// </summary>
        [Test]
        public void TestAES_CTR_Encryption_TestVector2a()
        {

            // Note: Very unintuitive, but the IV is actually the concatenation of the nonce, the IV and the counter!

            var key                 = "7E 24 06 78 17 FA E0 D7 43 D6 CE 1F 32 53 91 63".FromHEX();
            var iv                  =             "C0 54 3B 59 DA 48 D9 0B".            FromHEX();
            var nonce               = "00 6C B6 DB".                                    FromHEX();
            var counter             =                                     "00 00 00 01".FromHEX();
            var fullIV              = nonce.Concat(iv).Concat(counter).ToArray();
            var plaintext           = "00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F 10 11 12 13 14 15 16 17 18 19 1A 1B 1C 1D 1E 1F".FromHEX();
            var expectedCiphertext  = "51 04 A1 06 16 8A 72 D9 79 0D 41 EE 8E DA D3 88 EB 2E 1E FC 46 DA 57 C8 FC E6 30 DF 91 41 BE 28".FromHEX();

            var actualCiphertext    = new Byte[plaintext.Length];

            var cipher              = new BufferedBlockCipher(new SicBlockCipher(new AesEngine()));
            cipher.Init(true, new ParametersWithIV(new KeyParameter(key), fullIV));

            var len                 = cipher.ProcessBytes(plaintext, 0, plaintext.Length, actualCiphertext, 0);
            cipher.DoFinal(actualCiphertext, len);

            Assert.That(actualCiphertext, Is.EqualTo(expectedCiphertext), "AES-CTR encryption failed");

        }

        #endregion

        #region TestAES_CTR_Encryption_TestVector2b()

        /// <summary>
        /// https://www.rfc-editor.org/rfc/rfc3686#section-6
        /// Test Vector #2: Encrypting 32 octets using AES-CTR with 128-bit key
        /// </summary>
        [Test]
        public void TestAES_CTR_Encryption_TestVector2b()
        {

            // Note: Very unintuitive, but the IV is actually the concatenation of the nonce, the IV and the counter!

            var key                 = "7E 24 06 78 17 FA E0 D7 43 D6 CE 1F 32 53 91 63".FromHEX();
            var iv                  =             "C0 54 3B 59 DA 48 D9 0B".            FromHEX();
            var nonce               = "00 6C B6 DB".                                    FromHEX();
            var counter             =                                     "00 00 00 01".FromHEX();
            var fullIV              = nonce.Concat(iv).Concat(counter).ToArray();
            var plaintext           = "00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F 10 11 12 13 14 15 16 17 18 19 1A 1B 1C 1D 1E 1F".FromHEX();
            var expectedCiphertext  = "51 04 A1 06 16 8A 72 D9 79 0D 41 EE 8E DA D3 88 EB 2E 1E FC 46 DA 57 C8 FC E6 30 DF 91 41 BE 28".FromHEX();

            var actualCiphertext    = new Byte[plaintext.Length];

            var cipher              = new SicBlockCipher(new AesEngine());
            var blockSize           = cipher.GetBlockSize();
            cipher.Init(true, new ParametersWithIV(new KeyParameter(key), fullIV));

            for (var i = 0; i < plaintext.Length; i += blockSize)
                cipher.ProcessBlock(plaintext, i, actualCiphertext, i);

            Assert.That(actualCiphertext, Is.EqualTo(expectedCiphertext), "AES-CTR encryption failed");

        }

        #endregion

        #region TestAES_CTR_Encryption_TestVector2_butTooShort()

        /// <summary>
        /// https://www.rfc-editor.org/rfc/rfc3686#section-6
        /// Test Vector #2: Encrypting 32 octets using AES-CTR with 128-bit key
        /// 
        /// Here the plaintext is just 31 bytes long, so the last byte is missing
        /// and the cipher should pad it with zeros internally!
        /// </summary>
        [Test]
        public void TestAES_CTR_Encryption_TestVector2_butTooShort()
        {

            // Note: Very unintuitive, but the IV is actually the concatenation of the nonce, the IV and the counter!

            var key                 = "7E 24 06 78 17 FA E0 D7 43 D6 CE 1F 32 53 91 63".FromHEX();
            var iv                  =             "C0 54 3B 59 DA 48 D9 0B".            FromHEX();
            var nonce               = "00 6C B6 DB".                                    FromHEX();
            var counter             =                                     "00 00 00 01".FromHEX();
            var fullIV              = nonce.Concat(iv).Concat(counter).ToArray();
            var plaintextTooShort   = "00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F 10 11 12 13 14 15 16 17 18 19 1A 1B 1C 1D 1E".FromHEX();
            var expectedCiphertext  = "51 04 A1 06 16 8A 72 D9 79 0D 41 EE 8E DA D3 88 EB 2E 1E FC 46 DA 57 C8 FC E6 30 DF 91 41 BE".FromHEX();

            var cipher              = new SicBlockCipher(new AesEngine());
            var blockSize           = cipher.GetBlockSize();

            var paddedLength        = ((plaintextTooShort.Length + blockSize - 1) / blockSize) * blockSize;
            var paddedPlaintext     = new Byte[paddedLength];
            Array.Copy(plaintextTooShort, paddedPlaintext, plaintextTooShort.Length);

            var paddedCiphertext    = new Byte[paddedLength];

            cipher.Init(true, new ParametersWithIV(new KeyParameter(key), fullIV));

            for (var i = 0; i < paddedPlaintext.Length; i += blockSize)
                cipher.ProcessBlock(paddedPlaintext, i, paddedCiphertext, i);

            var actualCiphertext    = new Byte[plaintextTooShort.Length];
            Array.Copy(paddedCiphertext, actualCiphertext, plaintextTooShort.Length);

            Assert.That(actualCiphertext, Is.EqualTo(expectedCiphertext), "AES-CTR encryption failed");

        }

        #endregion


    }

}
