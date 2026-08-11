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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// Official test vectors for the vectorised ChaCha20 core (RFC 8439).
    ///
    /// <para>
    /// This is in-house crypto replacing a third-party implementation, so it is validated against the
    /// published vectors rather than only against itself: a round-trip test would pass just as happily
    /// with a wrong-but-symmetric keystream.
    /// </para>
    ///
    /// <para>
    /// RFC 8439 uses a 32-bit counter with a 96-bit nonce, while OpenSSH's
    /// <c>chacha20-poly1305@openssh.com</c> uses Bernstein's original 64-bit counter with a 64-bit
    /// nonce. They are the same function over the same 16-word state — only the split of words 12–15
    /// differs — so the vectors are applied to the raw state via <see cref="ChaCha20.Block"/>, which is
    /// exactly the layer where the two agree.
    /// </para>
    /// </summary>
    [TestFixture]
    [Category("Crypto")]
    public class ChaCha20VectorTests
    {

        #region Rfc8439_BlockFunction_Section232

        /// <summary>RFC 8439 §2.3.2 — the block function, key 00..1f, nonce 00:00:00:09:00:00:00:4a:00:00:00:00, counter 1.</summary>
        [Test]
        public void Rfc8439_BlockFunction_Section232()
        {

            UInt32[] state = [
                0x61707865, 0x3320646e, 0x79622d32, 0x6b206574,
                0x03020100, 0x07060504, 0x0b0a0908, 0x0f0e0d0c,
                0x13121110, 0x17161514, 0x1b1a1918, 0x1f1e1d1c,
                0x00000001, 0x09000000, 0x4a000000, 0x00000000
            ];

            var expected = Convert.FromHexString(
                "10f1e7e4d13b5915500fdd1fa32071c4" +
                "c7d1f4c733c0680304 22aa9ac3d46c4e".Replace(" ", "") +
                "d28264 46079faa0914c2d705d98b02a2".Replace(" ", "") +
                "b5129cd1de164eb9cbd083e8a2503c4e");

            var actual = new Byte[ChaCha20.BlockSize];
            ChaCha20.Block(state, actual);

            Assert.That(Convert.ToHexString(actual).ToLowerInvariant(),
                        Is.EqualTo(Convert.ToHexString(expected).ToLowerInvariant()));

        }

        #endregion

        #region Rfc8439_Encryption_Section242

        /// <summary>
        /// RFC 8439 §2.4.2 — encrypting the "Ladies and Gentlemen…" plaintext with counter 1.
        /// Exercises the full <see cref="ChaCha20.Xor"/> path including the counter increment across
        /// block boundaries (the plaintext is 114 bytes, i.e. one full block plus a partial one).
        /// </summary>
        [Test]
        public void Rfc8439_Encryption_Section242()
        {

            var key = Convert.FromHexString("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f");

            // §2.4.2's nonce is 00:00:00:00:00:00:00:4a:00:00:00:00 — note it differs from §2.3.2's,
            // which has 09 in the fourth byte. The RFC's 32-bit counter + 96-bit nonce maps onto the
            // 64-bit form as: counter word 12 = 1, word 13 = the nonce's first 4 bytes (all zero here),
            // and the 8-byte nonce = the nonce's remaining 8 bytes.
            const UInt64 counter = 1UL;
            var nonce = Convert.FromHexString("0000004a00000000");

            var plaintext = Encoding.ASCII.GetBytes(
                "Ladies and Gentlemen of the class of '99: If I could offer you only one tip for the future, sunscreen would be it.");

            var expected = Convert.FromHexString(
                "6e2e359a2568f98041ba0728dd0d6981" +
                "e97e7aec1d4360c20a27afccfd9fae0b" +
                "f91b65c5524733ab8f593dabcd62b357" +
                "1639d624e65152ab8f530c359f0861d8" +
                "07ca0dbf500d6a6156a38e088a22b65e" +
                "52bc514d16ccf806818ce91ab7793736" +
                "5af90bbf74a35be6b40b8eedf2785e42" +
                "874d");

            var actual = new Byte[plaintext.Length];
            ChaCha20.Xor(key, nonce, counter, plaintext, actual);

            Assert.That(Convert.ToHexString(actual).ToLowerInvariant(),
                        Is.EqualTo(Convert.ToHexString(expected).ToLowerInvariant()));

        }

        #endregion

        #region Xor_IsItsOwnInverse_AndWorksInPlace

        [Test]
        public void Xor_IsItsOwnInverse_AndWorksInPlace()
        {

            var key       = Convert.FromHexString("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f");
            var plaintext = new Byte[1000];
            Random.Shared.NextBytes(plaintext);

            var buffer = (Byte[]) plaintext.Clone();

            var streamNonce = Convert.FromHexString("0102030405060708");
            ChaCha20.Xor(key, streamNonce, 7, buffer, buffer);      // in place
            Assert.That(buffer, Is.Not.EqualTo(plaintext), "the data must actually be transformed");

            ChaCha20.Xor(key, streamNonce, 7, buffer, buffer);      // and back
            Assert.That(buffer, Is.EqualTo(plaintext));

        }

        #endregion

        #region CounterAdvances_AcrossBlockBoundaries

        /// <summary>
        /// Keystream generated in one call must match the same range generated block by block — the
        /// counter has to advance exactly once per 64 bytes, including across a 32-bit wrap.
        /// </summary>
        [Test]
        public void CounterAdvances_AcrossBlockBoundaries()
        {

            var key = Convert.FromHexString("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f");

            var whole = new Byte[ChaCha20.BlockSize * 4];
            var wrapNonce = Convert.FromHexString("0900000000000000");
            ChaCha20.Keystream(key, wrapNonce, 0xFFFFFFFEUL, whole);   // straddles the 32-bit counter wrap

            for (var block = 0; block < 4; block++)
            {
                var single = new Byte[ChaCha20.BlockSize];
                ChaCha20.Keystream(key, wrapNonce, 0xFFFFFFFEUL + (UInt64) block, single);
                Assert.That(single,
                            Is.EqualTo(whole[(block * ChaCha20.BlockSize)..((block + 1) * ChaCha20.BlockSize)]),
                            $"block {block} must match the streamed output");
            }

        }

        #endregion

    }

}
