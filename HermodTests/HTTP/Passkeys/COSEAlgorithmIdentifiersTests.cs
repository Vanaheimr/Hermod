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

using org.GraphDefined.Vanaheimr.Hermod.Passkeys;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.Passkeys
{

    /// <summary>
    /// The COSE algorithms are the numbers IANA gave them - each its own.
    /// </summary>
    /// <remarks>
    /// ES256K was -36, the number of ES512: offered to an authenticator, it
    /// asked for ECDSA P-521/SHA-512, and the name of either could come back
    /// for the other.
    /// </remarks>
    [TestFixture]
    public class COSEAlgorithmIdentifiersTests
    {

        #region The_Algorithms_Are_The_Numbers_Of_IANA()

        /// <summary>
        /// https://www.iana.org/assignments/cose/cose.xhtml#algorithms
        /// </summary>
        [Test]
        public void The_Algorithms_Are_The_Numbers_Of_IANA()
        {

            Assert.Multiple(() => {
                Assert.That((Int32) COSEAlgorithmIdentifiers.ES256,   Is.EqualTo(   -7));
                Assert.That((Int32) COSEAlgorithmIdentifiers.EdDSA,   Is.EqualTo(   -8));
                Assert.That((Int32) COSEAlgorithmIdentifiers.ES384,   Is.EqualTo(  -35));
                Assert.That((Int32) COSEAlgorithmIdentifiers.ES512,   Is.EqualTo(  -36));
                Assert.That((Int32) COSEAlgorithmIdentifiers.ES256K,  Is.EqualTo(  -47));
            });

        }

        #endregion

        #region Every_Algorithm_Has_A_Number_Of_Its_Own()

        [Test]
        public void Every_Algorithm_Has_A_Number_Of_Its_Own()
        {

            var numbers = Enum.GetValues<COSEAlgorithmIdentifiers>().Select(algorithm => (Int32) algorithm).ToArray();

            Assert.That(numbers,                                                       Is.Unique);
            Assert.That(Enum.GetNames<COSEAlgorithmIdentifiers>(),                     Has.Length.EqualTo(numbers.Length));
            Assert.That(COSEAlgorithmIdentifiers.ES256K.ToString(),                    Is.EqualTo("ES256K"));
            Assert.That((Int32?) new PublicKeyCredentialParameters(PublicKeyCredentialType.PublicKey,
                                                                   COSEAlgorithmIdentifiers.ES256K).ToJSON()["alg"],
                                                                                       Is.EqualTo(-47));

        }

        #endregion

    }

}
