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

using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.NTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.NTP
{

    public class AESTestss
    {

        #region TestCMAC()

        [Test]
        public void TestCMAC()
        {

            var key           = "2b7e1516 28aed2a6 abf71588 09cf4f3c".FromHEX();
            var message       = "6bc1bee2 2e409f96 e93d7e11 7393172a".FromHEX();
            var expectedCMAC  = "070a16b4 6b4d4144 f79bdd9d d04a287c".FromHEX();

            var computedCMAC  = AES_SIV.CMAC(key, message);

            Assert.That(computedCMAC, Is.EqualTo(expectedCMAC));

        }

        #endregion

        #region TestCMACSubkeyGeneration()

        [Test]
        public void TestCMACSubkeyGeneration()
        {

            var key         = "2b7e1516 28aed2a6 abf71588 09cf4f3c".FromHEX();
            var aesKeyZero  = "7df76b0c 1ab899b3 3e42f047 b91b546f".FromHEX();
            var expectedK1  = "fbeed618 35713366 7c85e08f 7236a8de".FromHEX();
            var expectedK2  = "f7ddac30 6ae266cc f90bc11e e46d513b".FromHEX();

            // Schritt 1: Berechne AES-128(key, 0)
            var computedAesKeyZero = AES_Encrypt(key, new Byte[16]);
            Assert.That(computedAesKeyZero, Is.EqualTo(aesKeyZero), "AES-128(key, 0) ist falsch.");

            // Schritt 2: Generiere K1 und K2
            var K1 = GenerateSubkey(computedAesKeyZero);
            var K2 = GenerateSubkey(K1);

            // Schritt 3: Vergleiche die generierten Subkeys mit den erwarteten Werten
            Assert.That(K1, Is.EqualTo(expectedK1), "Subkey K1 ist falsch.");
            Assert.That(K2, Is.EqualTo(expectedK2), "Subkey K2 ist falsch.");

        }

        #endregion

        #region TestSubkeyGeneration2()

        [Test]
        public void TestSubkeyGeneration2()
        {

            var key           = "2b7e151628aed2a6abf7158809cf4f3c".FromHEX();

            // Berechne L = AES-128(key, 0^128)
            var zeroBlock     = new Byte[16];
            var engine        = new AesEngine();
            engine.Init(true, new KeyParameter(key));
            var L             = new Byte[16];
            engine.ProcessBlock(zeroBlock, 0, L, 0);

            var expectedLHex  = "7df76b0c1ab899b33e42f047b91b546f";
            Assert.That(L.ToHexString(), Is.EqualTo(expectedLHex));

            // Berechne K1 = dbl(L) und K2 = dbl(K1)
            var K1            = AES_SIV.DoubleBlock(L);
            var K2            = AES_SIV.DoubleBlock(K1);

            var expectedK1Hex = "fbeed618357133667c85e08f7236a8de";
            var expectedK2Hex = "f7ddac306ae266ccf90bc11ee46d513b";

            Assert.That(K1.ToHexString(), Is.EqualTo(expectedK1Hex));
            Assert.That(K2.ToHexString(), Is.EqualTo(expectedK2Hex));

        }

        #endregion

        private byte[] AES_Encrypt(byte[] key, byte[] input)
        {

            var cipher = new AesEngine();
            cipher.Init(true, new KeyParameter(key));

            var output = new byte[16];
            cipher.ProcessBlock(input, 0, output, 0);
            return output;

        }

        private byte[] GenerateSubkey(byte[] input)
        {

            // Double block in GF(2^128)
            var output = new Byte[16];
            Byte carry  = 0;

            for (var i = 15; i >= 0; i--)
            {
                var b = input[i];
                output[i] = (Byte) ((b << 1) | carry);
                carry     = (Byte) ((b & 0x80) != 0 ? 1 : 0);
            }

            // Usage of polynom x^128 + x^7 + x^2 + x + 1 (R = 0x87)
            if (carry != 0)
                output[15] ^= 0x87;

            return output;

        }



        #region AES128Test1()

        /// <summary>
        /// https://www.rfc-editor.org/rfc/rfc5297.html
        /// A.1. Deterministic Authenticated Encryption Example
        /// </summary>
        [Test]
        public void AES128Test1()
        {

            var key             = "fffefdfc fbfaf9f8 f7f6f5f4 f3f2f1f0 f0f1f2f3 f4f5f6f7 f8f9fafb fcfdfeff".FromHEX();
            var associatedData  = "10111213 14151617 18191a1b 1c1d1e1f 20212223 24252627".                  FromHEX();
            var plaintext       = "11223344 55667788 99aabbcc ddee".                                        FromHEX();

            var siv             = "85632d07 c6e8f37f 950acd32 0a2ecc93".                                    FromHEX();
            var ciphertext      = "40c02b96 90c4dc04 daef7f6a fe5c".                                        FromHEX();

            var expectedResult  = "85632d07 c6e8f37f 950acd32 0a2ecc93 40c02b96 90c4dc04 daef7f6a fe5c".    FromHEX(); // 30 Bytes

            // https://www.rfc-editor.org/rfc/rfc5297.html
            // A.1.  Deterministic Authenticated Encryption Example
            // 
            //    Input:
            //    -----
            //    Key:
            //            fffefdfc fbfaf9f8 f7f6f5f4 f3f2f1f0
            //            f0f1f2f3 f4f5f6f7 f8f9fafb fcfdfeff
            // 
            //    AD:
            //            10111213 14151617 18191a1b 1c1d1e1f
            //            20212223 24252627
            // 
            //    Plaintext:
            //            11223344 55667788 99aabbcc ddee

            //    S2V-CMAC-AES
            //    ------------
            //    CMAC(zero):
            //            0e04dfaf c1efbf04 01405828 59bf073a
            // 
            //    double():
            //            1c09bf5f 83df7e08 0280b050 b37e0e74
            // 
            //    CMAC(ad):
            //            f1f922b7 f5193ce6 4ff80cb4 7d93f23b
            // 
            //    xor:
            //            edf09de8 76c642ee 4d78bce4 ceedfc4f
            //
            //    double():
            //            dbe13bd0 ed8c85dc 9af179c9 9ddbf819
            // 
            //    pad:
            //            11223344 55667788 99aabbcc ddee8000
            // 
            //    xor:
            //            cac30894 b8eaf254 035bc205 40357819
            // 
            //    CMAC(final):
            //            85632d07 c6e8f37f 950acd32 0a2ecc93

            var aes_siv = new AES_SIV(key);

            var key1 = aes_siv.Key1_CMAC;

            // 1. Init with: D_0 = CMAC(key1, 0^128)
            var D = AES_SIV.CMAC(key1, new Byte[16]);
            Assert.That(D.ToHexString(),      Is.EqualTo("0e04dfaf c1efbf04 01405828 59bf073a".Replace(" ", "")));


            // 2. Hash all associated data blocks
            //foreach (var associatedData in AssociatedData)
            //{
            D = AES_SIV.DoubleBlock(D);
            Assert.That(D.ToHexString(),      Is.EqualTo("1c09bf5f 83df7e08 0280b050 b37e0e74".Replace(" ", "")));

            var cmacX = AES_SIV.CMAC(key1, associatedData);
            Assert.That(cmacX.ToHexString(),  Is.EqualTo("f1f922b7 f5193ce6 4ff80cb4 7d93f23b".Replace(" ", "")));

            D = AES_SIV.XOR_Blocks(D, cmacX);
            Assert.That(D.ToHexString(),      Is.EqualTo("edf09de8 76c642ee 4d78bce4 ceedfc4f".Replace(" ", "")));
            //}

            Byte[] T;
            if (plaintext.Length >= 16)
            {
                // Take the last 16 bytes of the plaintext
                var lastBlock = new Byte[16];
                Buffer.BlockCopy(plaintext, plaintext.Length - 16, lastBlock, 0, 16);
                T = AES_SIV.XOR_Blocks(lastBlock, D);
            }
            else
            {

                D = AES_SIV.DoubleBlock(D);
                Assert.That(D.ToHexString(),       Is.EqualTo("dbe13bd0 ed8c85dc 9af179c9 9ddbf819".Replace(" ", "")));

                var padded = AES_SIV.Pad(plaintext);
                Assert.That(padded.ToHexString(),  Is.EqualTo("11223344 55667788 99aabbcc ddee8000".Replace(" ", "")));

                T = AES_SIV.XOR_Blocks(D, padded);
                Assert.That(T.ToHexString(),       Is.EqualTo("cac30894 b8eaf254 035bc205 40357819".Replace(" ", "")));

            }

            // 4. Final: V = CMAC(K, T)
            var final = AES_SIV.CMAC(key1, T);
            Assert.That(final.ToHexString(),  Is.EqualTo(siv.ToHexString()));



            //    CTR-AES
            //    -------
            //    CTR:
            //            85632d07 c6e8f37f 150acd32 0a2ecc93
            // 
            //    E(K,CTR):
            //            51e218d2 c5a2ab8c 4345c4a6 23b2f08f
            // 
            //    ciphertext:
            //            40c02b96 90c4dc04 daef7f6a fe5c
            // 
            //    output
            //    ------
            //    IV || C:
            //            85632d07 c6e8f37f 950acd32 0a2ecc93
            //            40c02b96 90c4dc04 daef7f6a fe5c

            //var iv         = final.ToHexString().FromHEX();
            var ivCTR      = new Byte[16];
            Buffer.BlockCopy(final, 0, ivCTR, 0, 16);

            // rfc5297 Section 2.5: Clear the 31st and 63rd bit (rightmost bit = 0th)
            ivCTR[ 8] &= 0x7F;
            ivCTR[12] &= 0x7F;
            Assert.That(ivCTR.ToHexString(), Is.EqualTo("85632d07 c6e8f37f 150acd32 0a2ecc93".Replace(" ", "")));

            var cipher     = new SicBlockCipher(new AesEngine());
            var parameters = new ParametersWithIV(new KeyParameter(aes_siv.Key2_AESCTR), ivCTR);
            cipher.Init(true, parameters);

            var blockSize        = cipher.GetBlockSize();
            var paddedLength     = ((plaintext.Length + blockSize - 1) / blockSize) * blockSize;
            var paddedPlaintext  = new Byte[paddedLength];
            Array.Copy(plaintext, paddedPlaintext, plaintext.Length);

            var paddedCiphertext = new Byte[paddedLength];

            for (var i = 0; i < paddedPlaintext.Length; i += blockSize)
                cipher.ProcessBlock(paddedPlaintext, i, paddedCiphertext, i);

            var actualCiphertext = new Byte[plaintext.Length];
            Array.Copy(paddedCiphertext, actualCiphertext, plaintext.Length);



            Assert.That(actualCiphertext.ToHexString(), Is.EqualTo(ciphertext.ToHexString()));


            // The same via library...
            var result = aes_siv.Encrypt([associatedData], plaintext);

            Assert.That(result.ToHexString(), Is.EqualTo(final.ToHexString() + ciphertext.ToHexString()));

        }

        #endregion

        #region AES128Test2()

        /// <summary>
        /// https://www.rfc-editor.org/rfc/rfc5297.html
        /// A.2. Nonce-Based Authenticated Encryption Example
        /// </summary>
        [Test]
        public void AES128Test2()
        {

            var key             =  "7f7e7d7c 7b7a7978 77767574 73727170 40414243 44454647 48494a4b 4c4d4e4f".FromHEX();
            var associatedData1 = ("00112233 44556677 8899aabb ccddeeff deaddada deaddada ffeeddcc bbaa9988" +
                                   "77665544 33221100").                                                     FromHEX();
            var associatedData2 =  "10203040 50607080 90a0".                                                 FromHEX();
            var nonce           =  "09f91102 9d74e35b d84156c5 635688c0".                                    FromHEX();
            var plaintext       = ("74686973 20697320 736f6d65 20706c61 696e7465 78742074 6f20656e 63727970" +
                                   "74207573 696e6720 5349562d 414553").                                     FromHEX();

            var siv             =  "7bdb6e3b 432667eb 06f4d14b ff2fbd0f".                                    FromHEX();
            var ciphertext      = ("cb900f2f ddbe4043 26601965 c889bf17 dba77ceb 094fa663 b7a3f748 ba8af829" +
                                   "ea64ad54 4a272e9c 485b62a3 fd5c0d").                                     FromHEX();

            var expectedResult  = ("7bdb6e3b 432667eb 06f4d14b ff2fbd0f cb900f2f ddbe4043 26601965 c889bf17" +
                                   "dba77ceb 094fa663 b7a3f748 ba8af829 ea64ad54 4a272e9c 485b62a3 fd5c0d"). FromHEX();

            // https://www.rfc-editor.org/rfc/rfc5297.html
            // A.2.  Nonce-Based Authenticated Encryption Example
            //
            //   Input:
            //   -----
            //   Key:
            //           7f7e7d7c 7b7a7978 77767574 73727170
            //           40414243 44454647 48494a4b 4c4d4e4f
            //
            //   AD1:
            //           00112233 44556677 8899aabb ccddeeff
            //           deaddada deaddada ffeeddcc bbaa9988
            //           77665544 33221100
            //
            //   AD2:
            //           10203040 50607080 90a0
            //
            //   Nonce:
            //           09f91102 9d74e35b d84156c5 635688c0
            //
            //   Plaintext:
            //           74686973 20697320 736f6d65 20706c61
            //           696e7465 78742074 6f20656e 63727970
            //           74207573 696e6720 5349562d 414553

            // S2V-CMAC-AES
            //    ------------
            //    CMAC(zero):
            //            c8b43b59 74960e7c e6a5dd85 231e591a
            // 
            //    double():
            //            916876b2 e92c1cf9 cd4bbb0a 463cb2b3
            // 
            //    CMAC(ad1)
            //            3c9b689a b41102e4 80954714 1dd0d15a
            // 
            //    xor:
            //            adf31e28 5d3d1e1d 4ddefc1e 5bec63e9
            // 
            //    double():
            //            5be63c50 ba7a3c3a 9bbdf83c b7d8c755
            // 
            //    CMAC(ad2)
            //            d98c9b0b e42cb2d7 aa98478e d11eda1b
            // 
            //    xor:
            //            826aa75b 5e568eed 3125bfb2 66c61d4e
            // 
            //    double():
            //            04d54eb6 bcad1dda 624b7f64 cd8c3a1b
            // 
            //    CMAC(nonce)
            //            128c62a1 ce3747a8 372c1c05 a538b96d
            // 
            //    xor:
            //            16592c17 729a5a72 55676361 68b48376
            // 
            //    xorend:
            //            74686973 20697320 736f6d65 20706c61
            //            696e7465 78742074 6f20656e 63727966
            //            2d0c6201 f3341575 342a3745 f5c625
            // 
            //    CMAC(final)
            //            7bdb6e3b 432667eb 06f4d14b ff2fbd0f

            var aes_siv = new AES_SIV(key);

            var key1 = aes_siv.Key1_CMAC;

            // 1. Init with: D_0 = CMAC(key1, 0^128)
            var D = AES_SIV.CMAC(key1, new Byte[16]);
            Assert.That(D.ToHexString(),      Is.EqualTo("c8b43b59 74960e7c e6a5dd85 231e591a".Replace(" ", "")));


            // 2. Hash all associated data blocks
            //foreach (var associatedData in new Byte[][] { associatedData1, associatedData2 })
            //{

                D = AES_SIV.DoubleBlock(D);
                Assert.That(D.ToHexString(),       Is.EqualTo("916876b2 e92c1cf9 cd4bbb0a 463cb2b3".Replace(" ", "")));

                var cmacX1 = AES_SIV.CMAC(key1, associatedData1);
                Assert.That(cmacX1.ToHexString(),  Is.EqualTo("3c9b689a b41102e4 80954714 1dd0d15a".Replace(" ", "")));

                D = AES_SIV.XOR_Blocks(D, cmacX1);
                Assert.That(D.ToHexString(),       Is.EqualTo("adf31e28 5d3d1e1d 4ddefc1e 5bec63e9".Replace(" ", "")));


                D = AES_SIV.DoubleBlock(D);
                Assert.That(D.ToHexString(),       Is.EqualTo("5be63c50 ba7a3c3a 9bbdf83c b7d8c755".Replace(" ", "")));

                var cmacX2 = AES_SIV.CMAC(key1, associatedData2);
                Assert.That(cmacX2.ToHexString(),  Is.EqualTo("d98c9b0b e42cb2d7 aa98478e d11eda1b".Replace(" ", "")));

                D = AES_SIV.XOR_Blocks(D, cmacX2);
                Assert.That(D.ToHexString(),       Is.EqualTo("826aa75b 5e568eed 3125bfb2 66c61d4e".Replace(" ", "")));

            //}

            if (nonce.Length > 0)
            {

                D = AES_SIV.DoubleBlock(D);
                Assert.That(D.ToHexString(),       Is.EqualTo("04d54eb6 bcad1dda 624b7f64 cd8c3a1b".Replace(" ", "")));

                var cmacX3 = AES_SIV.CMAC(key1, nonce);
                Assert.That(cmacX3.ToHexString(),  Is.EqualTo("128c62a1 ce3747a8 372c1c05 a538b96d".Replace(" ", "")));

                D = AES_SIV.XOR_Blocks(D, cmacX3);
                Assert.That(D.ToHexString(),       Is.EqualTo("16592c17 729a5a72 55676361 68b48376".Replace(" ", "")));

            }

            Byte[] T;
            if (plaintext.Length >= 16)
            {
                // Take the last 16 bytes of the plaintext
                var lastBlock = new Byte[16];
                Buffer.BlockCopy(plaintext, plaintext.Length - 16, lastBlock, 0, 16);
                var T1 = AES_SIV.XOR_Blocks(lastBlock, D);

                // 66 2d0c6201 f3341575 342a3745 f5c625

                Assert.That(T1.ToHexString(), Is.EqualTo("66 2d0c6201 f3341575 342a3745 f5c625".Replace(" ", "")));

                T = new Byte[plaintext.Length];
                Buffer.BlockCopy(plaintext, 0, T,                   0, plaintext.Length);
                Buffer.BlockCopy(T1,        0, T, plaintext.Length-16,               16);

                Assert.That(T.ToHexString(), Is.EqualTo("74686973 20697320 736f6d65 20706c61 696e7465 78742074 6f20656e 63727966 2d0c6201 f3341575 342a3745 f5c625".Replace(" ", "")));

            }
            else
            {

                D = AES_SIV.DoubleBlock(D);
                Assert.That(D.ToHexString(),       Is.EqualTo("dbe13bd0 ed8c85dc 9af179c9 9ddbf819".Replace(" ", "")));

                var padded = AES_SIV.Pad(plaintext);
                Assert.That(padded.ToHexString(),  Is.EqualTo("11223344 55667788 99aabbcc ddee8000".Replace(" ", "")));

                T = AES_SIV.XOR_Blocks(D, padded);
                Assert.That(T.ToHexString(),       Is.EqualTo("cac30894 b8eaf254 035bc205 40357819".Replace(" ", "")));

            }

            // 4. Final: V = CMAC(K, T)
            var final = AES_SIV.CMAC(key1, T);
            Assert.That(final.ToHexString(),  Is.EqualTo(siv.ToHexString()));



            // CTR-AES
            // -------
            // CTR:
            //         7bdb6e3b 432667eb 06f4d14b 7f2fbd0f
            //
            // E(K,CTR):
            //         bff8665c fdd73363 550f7400 e8f9d376
            //
            // CTR+1:
            //         7bdb6e3b 432667eb 06f4d14b 7f2fbd10
            //
            // E(K,CTR+1):
            //         b2c9088e 713b8617 d8839226 d9f88159
            //
            // CTR+2
            //         7bdb6e3b 432667eb 06f4d14b 7f2fbd11
            //
            // E(K,CTR+2):
            //         9e44d827 234949bc 1b12348e bc195ec7
            //
            // ciphertext:
            //         cb900f2f ddbe4043 26601965 c889bf17
            //         dba77ceb 094fa663 b7a3f748 ba8af829
            //         ea64ad54 4a272e9c 485b62a3 fd5c0d
            //
            // output
            // ------
            // IV || C:
            //         7bdb6e3b 432667eb 06f4d14b ff2fbd0f
            //         cb900f2f ddbe4043 26601965 c889bf17
            //         dba77ceb 094fa663 b7a3f748 ba8af829
            //         ea64ad54 4a272e9c 485b62a3 fd5c0d

            //var iv         = final.ToHexString().FromHEX();
            var ivCTR      = new Byte[16];
            Buffer.BlockCopy(final, 0, ivCTR, 0, 16);

            // rfc5297 Section 2.5: Clear the 31st and 63rd bit (rightmost bit = 0th)
            ivCTR[ 8] &= 0x7F;
            ivCTR[12] &= 0x7F;
            Assert.That(ivCTR.ToHexString(), Is.EqualTo("7bdb6e3b 432667eb 06f4d14b 7f2fbd0f".Replace(" ", "")));

            var cipher     = new SicBlockCipher(new AesEngine());
            var parameters = new ParametersWithIV(new KeyParameter(aes_siv.Key2_AESCTR), ivCTR);
            cipher.Init(true, parameters);

            var blockSize        = cipher.GetBlockSize();
            var paddedLength     = ((plaintext.Length + blockSize - 1) / blockSize) * blockSize;
            var paddedPlaintext  = new Byte[paddedLength];
            Array.Copy(plaintext, paddedPlaintext, plaintext.Length);

            var paddedCiphertext = new Byte[paddedLength];

            for (var i = 0; i < paddedPlaintext.Length; i += blockSize)
                cipher.ProcessBlock(paddedPlaintext, i, paddedCiphertext, i);

            var actualCiphertext = new Byte[plaintext.Length];
            Array.Copy(paddedCiphertext, actualCiphertext, plaintext.Length);


            Assert.That(actualCiphertext.ToHexString(), Is.EqualTo(("cb900f2f ddbe4043 26601965 c889bf17" +
                                                                    "dba77ceb 094fa663 b7a3f748 ba8af829" +
                                                                    "ea64ad54 4a272e9c 485b62a3 fd5c0d").Replace(" ", "")));


            // The same via library...
            var result = aes_siv.Encrypt([ associatedData1, associatedData2, nonce ], plaintext);

            Assert.That(result.ToHexString(), Is.EqualTo(("7bdb6e3b 432667eb 06f4d14b ff2fbd0f" +
                                                          "cb900f2f ddbe4043 26601965 c889bf17" +
                                                          "dba77ceb 094fa663 b7a3f748 ba8af829" +
                                                          "ea64ad54 4a272e9c 485b62a3 fd5c0d").Replace(" ", "")));

        }

        #endregion


        #region AES128TestX()

        ///// <summary>
        ///// Based on rfc5297 Appendix A
        ///// </summary>
        //[Test]
        //public void AES128TestX()
        //{

        //    var key = "fffefdfc fbfaf9f8 f7f6f5f4 f3f2f1f0 f0f1f2f3 f4f5f6f7 f8f9fafb fcfdfeff".FromHEX();
        //    var associatedData1 = "10111213 14151617 18191a1b 1c1d1e1f".FromHEX();
        //    var associatedData2 = "20212223 24252627".FromHEX();
        //    var plaintext = "11223344 55667788 99aabbcc ddee".FromHEX();
        //    var siv = "85632d07 c6e8f37f 950acd32 0a2ecc93".FromHEX();
        //    var ciphertext = "40c02b96 90c4dc04 daef7f6a fe5c".FromHEX();

        //    var expectedResult = "85632d07 c6e8f37f 950acd32 0a2ecc93 40c02b96 90c4dc04 daef7f6a fe5c".FromHEX(); // 30 Bytes


        //    var aes_siv = new AES_SIV(key);
        //    var result = aes_siv.Encrypt([associatedData1, associatedData2], plaintext);

        //    Assert.That(result.ToHexString(), Is.EqualTo(expectedResult.ToHexString()));


        //}

        #endregion


    }

}
