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

using System.Globalization;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// Unit tests for the elliptic-curve primitives underpinning the modern handshake:
    /// X25519 (RFC 7748) and Ed25519 (RFC 8032 / RFC 8709).
    /// </summary>
    [TestFixture]
    public class CryptoPrimitiveTests
    {

        #region (static) FromHex(Text)

        private static Byte[] FromHex(String Text)
        {
            var bytes = new Byte[Text.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
                bytes[i] = Byte.Parse(Text.AsSpan(i * 2, 2), NumberStyles.HexNumber);
            return bytes;
        }

        #endregion


        #region X25519

        [Test]
        public void X25519_Rfc7748_TestVector()
        {

            // RFC 7748, section 5.2 — first scalar-multiplication test vector.
            var scalar    = FromHex("a546e36bf0527c9d3b16154b82465edd62144c0ac1fc5a18506a2244ba449ac4");
            var uCoord    = FromHex("e6db6867583030db3594c1a424b15f7c726624ec26b3353b10a903a6d0ab1c4c");
            var expected  = FromHex("c3da55379de9c6908e94ea4df28d084f32eccf03491c71f754b4075577a28552");

            var keyPair   = X25519KeyPair.FromPrivateKey(scalar);
            var result    = keyPair.Agree(uCoord);

            Assert.That(result, Is.EqualTo(expected));

        }

        [Test]
        public void X25519_SharedSecret_MatchesBothWays()
        {

            var alice        = X25519KeyPair.Generate();
            var bob          = X25519KeyPair.Generate();

            var aliceSecret  = alice.Agree(bob.PublicKey);
            var bobSecret    = bob.Agree(alice.PublicKey);

            Assert.Multiple(() => {
                Assert.That(alice.PublicKey.Length, Is.EqualTo(X25519KeyPair.KeySize));
                Assert.That(aliceSecret.Length,     Is.EqualTo(X25519KeyPair.KeySize));
                Assert.That(aliceSecret,            Is.EqualTo(bobSecret));
            });

        }

        [Test]
        public void X25519_DifferentPeers_DifferentSecrets()
        {

            var alice   = X25519KeyPair.Generate();
            var bob     = X25519KeyPair.Generate();
            var mallory = X25519KeyPair.Generate();

            Assert.That(alice.Agree(bob.PublicKey), Is.Not.EqualTo(alice.Agree(mallory.PublicKey)));

        }

        #endregion

        #region Ed25519

        [Test]
        public void Ed25519_SignAndVerify_RoundTrip()
        {

            var keyPair    = Ed25519KeyPair.Generate();
            var message    = "The exchange hash H"u8.ToArray();
            var signature  = keyPair.Sign(message);

            Assert.Multiple(() => {
                Assert.That(keyPair.PublicKey.Length, Is.EqualTo(Ed25519KeyPair.KeySize));
                Assert.That(signature.Length,         Is.EqualTo(Ed25519KeyPair.SignatureSize));
                Assert.That(Ed25519KeyPair.Verify(keyPair.PublicKey, message, signature), Is.True);
            });

        }

        [Test]
        public void Ed25519_IsDeterministic()
        {

            // Ed25519 signatures are deterministic (RFC 8032): the same key + message => the same signature.
            var seed     = FromHex("833fe62409237b9d62ec77587520911e9a759cec1d19755b7da901b96dca3d42");
            var keyPair  = Ed25519KeyPair.FromSeed(seed);
            var message  = "hermod"u8.ToArray();

            var firstSignature   = keyPair.Sign(message);
            var secondSignature  = keyPair.Sign(message);

            Assert.That(secondSignature, Is.EqualTo(firstSignature));

        }

        [Test]
        public void Ed25519_TamperedMessage_FailsVerification()
        {

            var keyPair    = Ed25519KeyPair.Generate();
            var message    = "authentic"u8.ToArray();
            var signature  = keyPair.Sign(message);
            var tampered   = "authentiC"u8.ToArray();

            Assert.That(Ed25519KeyPair.Verify(keyPair.PublicKey, tampered, signature), Is.False);

        }

        [Test]
        public void Ed25519_WrongPublicKey_FailsVerification()
        {

            var signer     = Ed25519KeyPair.Generate();
            var impostor    = Ed25519KeyPair.Generate();
            var message    = "authentic"u8.ToArray();
            var signature  = signer.Sign(message);

            Assert.That(Ed25519KeyPair.Verify(impostor.PublicKey, message, signature), Is.False);

        }

        [Test]
        public void Ed25519_SeedRoundTrip_ProducesSamePublicKey()
        {

            var seed  = FromHex("4ccd089b28ff96da9db6c346ec114e0f5b8a319f35aba624da8cf6ed4fb8a6fb");
            var a     = Ed25519KeyPair.FromSeed(seed);
            var b     = Ed25519KeyPair.FromSeed(seed);

            Assert.That(a.PublicKey, Is.EqualTo(b.PublicKey));

        }

        #endregion

    }

}
