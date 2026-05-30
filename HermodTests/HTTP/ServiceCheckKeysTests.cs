/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Text;

using NUnit.Framework;

using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// Service check key tests.
    /// </summary>
    [TestFixture]
    public class ServiceCheckKeysTests
    {

        #region Generate_EC_Keys_Test()

        /// <summary>
        /// EC service check keys expose explicitly named EC properties.
        /// </summary>
        [Test]
        public void Generate_EC_Keys_Test()
        {

            var keys = ServiceCheckKeys.GenerateECKeys();

            Assert.That(keys.PrivateKeyEC,       Is.Not.Null);
            Assert.That(keys.PublicKeyEC,        Is.Not.Null);
            Assert.That(keys.PrivateKeyECHEX,    Is.Not.Empty);
            Assert.That(keys.PublicKeyECHEX,     Is.Not.Empty);
            Assert.That(keys.PrivateKeyMLDSA,    Is.Null);
            Assert.That(keys.PublicKeyMLDSA,     Is.Null);

        }

        #endregion

        #region Parse_EC_ASN1_Keys_Test()

        /// <summary>
        /// ASN.1 encoded EC keys contain their curve information and can be parsed without passing the curve explicitly.
        /// </summary>
        [Test]
        public void Parse_EC_ASN1_Keys_Test()
        {

            var generated = ServiceCheckKeys.GenerateECKeys("secp384r1");
            var parsed    = ServiceCheckKeys.ParseECKeysASN1(
                                generated.PrivateKeyECASN1HEX!,
                                generated.PublicKeyECASN1HEX!
                            );

            Assert.That(parsed.PrivateKeyECBytes, Is.EqualTo(generated.PrivateKeyECBytes));
            Assert.That(parsed.PublicKeyECBytes,  Is.EqualTo(generated.PublicKeyECBytes));

        }

        #endregion

        #region Generate_MLDSA_65_Keys_Test()

        /// <summary>
        /// ML-DSA service check keys default to ML-DSA-65.
        /// </summary>
        [Test]
        public void Generate_MLDSA_65_Keys_Test()
        {

            var keys = ServiceCheckKeys.GenerateMLDSAKeys();

            Assert.That(keys.PrivateKeyEC,       Is.Null);
            Assert.That(keys.PublicKeyEC,        Is.Null);
            Assert.That(keys.PrivateKeyMLDSA,    Is.Not.Null);
            Assert.That(keys.PublicKeyMLDSA,     Is.Not.Null);
            Assert.That(keys.PrivateKeyMLDSAHEX, Is.Not.Empty);
            Assert.That(keys.PublicKeyMLDSAHEX,  Is.Not.Empty);
            Assert.That(keys.PublicKeyMLDSABytes, Has.Length.EqualTo(1952));

        }

        #endregion

        #region Parse_MLDSA_65_Keys_Test()

        /// <summary>
        /// ML-DSA private keys can be round-tripped from their encoded form.
        /// </summary>
        [Test]
        public void Parse_MLDSA_65_Keys_Test()
        {

            var generated = ServiceCheckKeys.GenerateMLDSAKeys();
            var parsed    = ServiceCheckKeys.ParseMLDSAKeysHEX(generated.PrivateKeyMLDSAHEX!);

            Assert.That(parsed.PrivateKeyMLDSABytes, Is.EqualTo(generated.PrivateKeyMLDSABytes));
            Assert.That(parsed.PublicKeyMLDSABytes,  Is.EqualTo(generated.PublicKeyMLDSABytes));

        }

        #endregion

        #region Parse_MLDSA_ASN1_Keys_Test()

        /// <summary>
        /// ASN.1 encoded ML-DSA keys contain their parameter set and can be parsed without passing it explicitly.
        /// </summary>
        [Test]
        public void Parse_MLDSA_ASN1_Keys_Test()
        {

            var generated = ServiceCheckKeys.GenerateMLDSAKeys("ml_dsa_87");
            var parsed    = ServiceCheckKeys.ParseMLDSAKeysASN1(generated.PrivateKeyMLDSAASN1HEX!);
            var publicKey = ServiceCheckKeys.ParseMLDSAPublicKeyASN1(generated.PublicKeyMLDSAASN1HEX!);

            Assert.That(parsed.PrivateKeyMLDSABytes, Is.EqualTo(generated.PrivateKeyMLDSABytes));
            Assert.That(parsed.PublicKeyMLDSABytes,  Is.EqualTo(generated.PublicKeyMLDSABytes));
            Assert.That(publicKey.GetEncoded(),      Is.EqualTo(generated.PublicKeyMLDSABytes));

        }

        #endregion

        #region Sign_And_Verify_MLDSA_65_Test()

        /// <summary>
        /// Generated ML-DSA-65 service check keys can sign and verify payloads.
        /// </summary>
        [Test]
        public void Sign_And_Verify_MLDSA_65_Test()
        {

            var keys     = ServiceCheckKeys.GenerateMLDSAKeys();
            var message  = Encoding.UTF8.GetBytes("Hermod service check");
            var signer   = new MLDsaSigner(MLDsaParameters.ml_dsa_65, false);

            signer.Init(true, keys.PrivateKeyMLDSA);
            signer.BlockUpdate(message, 0, message.Length);
            var signature = signer.GenerateSignature();

            var verifier = new MLDsaSigner(MLDsaParameters.ml_dsa_65, false);
            verifier.Init(false, keys.PublicKeyMLDSA);
            verifier.BlockUpdate(message, 0, message.Length);

            Assert.That(verifier.VerifySignature(signature), Is.True);

        }

        #endregion

        #region Generate_Hybrid_Keys_Test()

        /// <summary>
        /// Hybrid key generation creates both EC and ML-DSA key pairs.
        /// </summary>
        [Test]
        public void Generate_Hybrid_Keys_Test()
        {

            var keys = ServiceCheckKeys.GenerateKeys();

            Assert.That(keys.PrivateKeyEC,       Is.Not.Null);
            Assert.That(keys.PublicKeyEC,        Is.Not.Null);
            Assert.That(keys.PrivateKeyMLDSA,    Is.Not.Null);
            Assert.That(keys.PublicKeyMLDSA,     Is.Not.Null);

        }

        #endregion

    }

}
