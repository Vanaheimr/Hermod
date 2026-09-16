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

using NUnit.Framework;

using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;

using org.GraphDefined.Vanaheimr.Hermod.PKI;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.PKI
{

    /// <summary>
    /// Every kind of key this library will make, and the certificate signing
    /// request that goes with it - from the curve everything understands to the
    /// ones being prepared for.
    /// </summary>
    /// <remarks>
    /// Two questions are kept apart throughout, because they have different
    /// answers: whether a key and its signing request can be <em>made</em>, and
    /// whether the platform underneath can then <em>present</em> the
    /// certificate that comes back. The first is the same everywhere. The
    /// second depends on the operating system, the runtime and the year - so
    /// nothing here asserts which algorithms are presentable.
    ///
    /// The list is the test cases: a new algorithm in <c>KeyAlgorithm.All</c>
    /// is a new case without a line being added here.
    /// </remarks>
    [TestFixture]
    public class KeyAlgorithm_Tests
    {

        #region Data

        private static readonly String[] ReachableAs = [ "device01.example.org", "192.168.1.10" ];

        public static IEnumerable<String> EveryAlgorithm
            => KeyAlgorithm.All.Select(algorithm => algorithm.Id);

        #endregion

        #region (private static) RequestFor(Algorithm, For = null)

        private static Pkcs10CertificationRequest RequestFor(String         Algorithm,
                                                             KeyPurposeID?  For   = null)
        {

            var algorithm = KeyAlgorithm.Find(Algorithm);

            Assert.That(algorithm, Is.Not.Null, $"'{Algorithm}' is not in the list.");

            return PKIFactory.GenerateCertificateSigningRequest(
                       algorithm!.Generate(),
                       new X509Name("CN=device01.example.org, O=Example, C=DE"),
                       algorithm,
                       ReachableAs,
                       For
                   );

        }

        #endregion

        #region (private static) ExtensionsOf(Request)

        /// <summary>
        /// The extensions somebody asked for, dug back out of the request.
        /// </summary>
        private static X509Extensions ExtensionsOf(Pkcs10CertificationRequest Request)
        {

            var attributes = Request.GetCertificationRequestInfo().Attributes;

            Assert.That(attributes, Is.Not.Null, "The request carries no attributes at all.");

            foreach (var entry in attributes!)
            {

                var attribute = AttributePkcs.GetInstance(entry);

                if (attribute.AttrType.Equals(PkcsObjectIdentifiers.Pkcs9AtExtensionRequest))
                    return X509Extensions.GetInstance(attribute.AttrValues[0]);

            }

            Assert.Fail("The request carries no extension request.");
            return null!;

        }

        #endregion


        #region EveryAlgorithmMakesARequestThatVerifies(Algorithm)

        /// <summary>
        /// The signing request is signed by the key it names, and says so.
        /// </summary>
        /// <remarks>
        /// A certificate authority checks exactly this before it issues
        /// anything, so a request that does not verify is a wasted trip - and
        /// for Ed448 and ML-DSA it is the whole point: .NET cannot sign one of
        /// these at all, and this is what says Bouncy Castle did.
        /// </remarks>
        [Test]
        [TestCaseSource(nameof(EveryAlgorithm))]
        public void EveryAlgorithmMakesARequestThatVerifies(String Algorithm)
        {

            var request = RequestFor(Algorithm);

            Assert.Multiple(() => {

                Assert.That(request.Verify(), Is.True,
                            "The signing request is not signed by the key it names.");

                Assert.That(request.GetCertificationRequestInfo().Subject.ToString(),
                            Does.Contain("device01.example.org"));

            });

        }

        #endregion

        #region EveryRequestSurvivesBeingWrittenDownAndReadBack(Algorithm)

        /// <summary>
        /// A request is handed over as PEM, so PEM is what has to hold it.
        /// </summary>
        [Test]
        [TestCaseSource(nameof(EveryAlgorithm))]
        public void EveryRequestSurvivesBeingWrittenDownAndReadBack(String Algorithm)
        {

            var pem = RequestFor(Algorithm).ToPEM();

            Assert.That(pem, Does.Contain("BEGIN CERTIFICATE REQUEST"));

            var read = new PemReader(new StringReader(pem)).ReadObject() as Pkcs10CertificationRequest;

            Assert.That(read, Is.Not.Null, "What was written down does not read back as a signing request.");
            Assert.That(read!.Verify(), Is.True, "The request no longer verifies after a round trip through PEM.");

        }

        #endregion

        #region EveryRequestSaysWhatItIsReachableAs(Algorithm)

        /// <summary>
        /// A host name stays a host name and an address stays an address.
        /// </summary>
        [Test]
        [TestCaseSource(nameof(EveryAlgorithm))]
        public void EveryRequestSaysWhatItIsReachableAs(String Algorithm)
        {

            var names = GeneralNames.GetInstance(
                            ExtensionsOf(RequestFor(Algorithm)).
                                GetExtensionParsedValue(X509Extensions.SubjectAlternativeName)
                        ).GetNames();

            Assert.Multiple(() => {

                Assert.That(names.Any(name => name.TagNo == GeneralName.DnsName &&
                                              name.Name.ToString() == "device01.example.org"),
                            Is.True, "The host name is not in the request as a host name.");

                Assert.That(names.Any(name => name.TagNo == GeneralName.IPAddress),
                            Is.True, "The address is not in the request as an address.");

            });

        }

        #endregion

        #region OnlyAnRSAKeyClaimsItCanEncipher(Algorithm)

        /// <summary>
        /// A signing key does not ask to be allowed to encipher.
        /// </summary>
        /// <remarks>
        /// RFC 8410 section 5 says plainly that an Ed25519 or an Ed448 key is
        /// for signing, and the same is true of every post-quantum signature
        /// scheme here; an elliptic curve key agrees rather than enciphers. A
        /// request that claims keyEncipherment for one of those is a request a
        /// strict certificate authority is entitled to refuse - and every one
        /// of them used to claim it.
        /// </remarks>
        [Test]
        [TestCaseSource(nameof(EveryAlgorithm))]
        public void OnlyAnRSAKeyClaimsItCanEncipher(String Algorithm)
        {

            var usage = KeyUsage.GetInstance(
                            ExtensionsOf(RequestFor(Algorithm)).
                                GetExtensionParsedValue(X509Extensions.KeyUsage)
                        );

            Assert.Multiple(() => {

                Assert.That((usage.IntValue & KeyUsage.DigitalSignature), Is.Not.Zero,
                            "A key that is to sign did not ask to be allowed to.");

                Assert.That((usage.IntValue & KeyUsage.KeyEncipherment) != 0,
                            Is.EqualTo(Algorithm.StartsWith("rsa-")),
                            $"'{Algorithm}' asked for the wrong thing: only RSA can encipher.");

            });

        }

        #endregion

        #region ARequestSaysWhatTheCertificateIsFor(Algorithm)

        /// <summary>
        /// Server authentication unless something else is asked for.
        /// </summary>
        /// <remarks>
        /// Said in the request rather than hoped for in the answer: a
        /// certificate authority handed a request without it frequently issues
        /// something that is not the kind of certificate that was wanted.
        /// </remarks>
        [Test]
        [TestCaseSource(nameof(EveryAlgorithm))]
        public void ARequestSaysWhatTheCertificateIsFor(String Algorithm)
        {

            var asServer = ExtendedKeyUsage.GetInstance(
                               ExtensionsOf(RequestFor(Algorithm)).
                                   GetExtensionParsedValue(X509Extensions.ExtendedKeyUsage)
                           );

            Assert.That(asServer.HasKeyPurposeId(KeyPurposeID.id_kp_serverAuth), Is.True);

            var asClient = ExtendedKeyUsage.GetInstance(
                               ExtensionsOf(RequestFor(Algorithm, KeyPurposeID.id_kp_clientAuth)).
                                   GetExtensionParsedValue(X509Extensions.ExtendedKeyUsage)
                           );

            Assert.Multiple(() => {
                Assert.That(asClient.HasKeyPurposeId(KeyPurposeID.id_kp_clientAuth), Is.True);
                Assert.That(asClient.HasKeyPurposeId(KeyPurposeID.id_kp_serverAuth), Is.False,
                            "A request for a client certificate also asked to be a server.");
            });

        }

        #endregion

        #region TheListIsUsableAsAList()

        /// <summary>
        /// What a page and a configuration read it by.
        /// </summary>
        [Test]
        public void TheListIsUsableAsAList()
        {

            Assert.Multiple(() => {

                Assert.That(KeyAlgorithm.All.Select(one => one.Id).Distinct().Count(),
                            Is.EqualTo(KeyAlgorithm.All.Count),
                            "Two algorithms are written the same way.");

                Assert.That(KeyAlgorithm.Find(KeyAlgorithm.DefaultId), Is.Not.Null,
                            "What is generated when nobody says otherwise is not in the list.");

                Assert.That(KeyAlgorithm.Find("ED448"),   Is.Not.Null, "The list is read case-sensitively.");
                Assert.That(KeyAlgorithm.Find(" ed448 "), Is.Not.Null, "The list is read without trimming.");
                Assert.That(KeyAlgorithm.Find("nonsense"), Is.Null);
                Assert.That(KeyAlgorithm.Find(null),       Is.Null);

                foreach (var algorithm in KeyAlgorithm.All)
                {
                    Assert.That(algorithm.Name,    Is.Not.Empty, $"'{algorithm.Id}' has nothing to be called on a page.");
                    Assert.That(algorithm.Remark,  Is.Not.Empty, $"'{algorithm.Id}' says nothing about itself.");
                }

                // Not tried yet is a third answer, and a page has to be able to
                // tell it from "no".
                Assert.That(KeyAlgorithm.Find("ed448")!.ToJSON().ContainsKey("presentable"), Is.False);

            });

        }

        #endregion

    }

}
