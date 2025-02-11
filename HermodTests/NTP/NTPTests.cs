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





    }

}
