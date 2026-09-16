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

using Org.BouncyCastle.Crypto;

using org.GraphDefined.Vanaheimr.Hermod.PKI;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.PKI
{

    /// <summary>
    /// A certificate authority of one kind of key, issuing to a subject of
    /// another - every combination, not just the ones that match.
    /// </summary>
    /// <remarks>
    /// The signature on a certificate is made by the issuer, so it is the
    /// issuer's key that decides which signature it is. That was chosen from
    /// the subject's key instead, which is right exactly when the two are of
    /// the same kind - and a root certificate authority signs itself, where
    /// they always are. So every test that stayed on the diagonal passed, and
    /// the first attempt to sign an Ed448 request with the elliptic curve
    /// authority that everything else here uses ended in a cast failing on a
    /// type nobody in the call had named.
    ///
    /// Hence a matrix rather than a list. Off the diagonal is where this can
    /// break again, so off the diagonal is what is measured; the diagonal comes
    /// along because a matrix is cheaper to read than a matrix with holes.
    ///
    /// One issuer per signature family is enough, and it is the cheapest member
    /// of each: what is being asked is whether the right key was reached for,
    /// not how large it was.
    /// </remarks>
    [TestFixture]
    public class IssuerSignsWithItsOwnKey_Tests
    {

        #region Data

        /// <summary>
        /// One per signature family, cheapest member.
        /// </summary>
        public static IEnumerable<String> EveryFamily
            => [ "ecdsa-p256", "rsa-2048", "ed25519", "ed448", "ml-dsa-44", "slh-dsa-sha2-128s" ];

        /// <summary>
        /// Every pair, including those where issuer and subject differ.
        /// </summary>
        public static IEnumerable<TestCaseData> EveryPairing
            => from issuer  in EveryFamily
               from subject in EveryFamily
               select new TestCaseData(issuer, subject).SetName($"{{m}}(issuer {issuer}, subject {subject})");

        /// <summary>
        /// Made once each: a key pair costs real time, an RSA one most of all,
        /// and nothing here is about how it was made.
        /// </summary>
        private static readonly Dictionary<String, AsymmetricCipherKeyPair> keyPairs = [];

        #endregion

        #region (private static) KeyPairFor(Algorithm)

        private static AsymmetricCipherKeyPair KeyPairFor(String Algorithm)
        {
            lock (keyPairs)
            {

                if (!keyPairs.TryGetValue(Algorithm, out var keyPair))
                {

                    var algorithm = KeyAlgorithm.Find(Algorithm);

                    Assert.That(algorithm, Is.Not.Null, $"'{Algorithm}' is not in the list.");

                    keyPair = algorithm!.Generate();
                    keyPairs.Add(Algorithm, keyPair);

                }

                return keyPair;

            }
        }

        #endregion

        #region (private static) AuthorityOf(Algorithm)

        private static (AsymmetricCipherKeyPair KeyPair, Org.BouncyCastle.X509.X509Certificate Certificate)

            AuthorityOf(String Algorithm)

        {

            var keyPair = KeyPairFor(Algorithm);

            return (keyPair,
                    PKIFactory.CreateRootCACertificate(
                        $"Test Root CA ({Algorithm})",
                        keyPair
                    ));

        }

        #endregion


        #region AnAuthorityOfEveryFamilySignsASubjectOfEveryFamily(Issuer, Subject)

        /// <summary>
        /// Every issuer signs every subject, and what comes out verifies
        /// against the issuer.
        /// </summary>
        /// <remarks>
        /// Verifying against the issuer's public key is the assertion that
        /// matters: it is the same check a client makes while walking a chain,
        /// and it fails both when the wrong key signed and when the right key
        /// signed under the wrong algorithm name.
        /// </remarks>
        [Test]
        [TestCaseSource(nameof(EveryPairing))]
        public void AnAuthorityOfEveryFamilySignsASubjectOfEveryFamily(String Issuer, String Subject)
        {

            var authority   = AuthorityOf(Issuer);
            var subjectKey  = KeyPairFor(Subject);

            var certificate = PKIFactory.SignClientCertificate(
                                  $"device.{Subject}.example.org",
                                  subjectKey.Public,
                                  authority.KeyPair.Private,
                                  authority.Certificate
                              );

            Assert.Multiple(() => {

                Assert.That(() => certificate.Verify(authority.KeyPair.Public),
                            Throws.Nothing,
                            $"A {Issuer} authority signed a {Subject} subject with something a {Issuer} key cannot check.");

                Assert.That(certificate.GetPublicKey().GetType(),
                            Is.EqualTo(subjectKey.Public.GetType()),
                            "The certificate is not about the subject's key.");

                Assert.That(certificate.IssuerDN.ToString(),
                            Is.EqualTo(authority.Certificate.SubjectDN.ToString()),
                            "The certificate does not name the authority that signed it.");

            });

        }

        #endregion

        #region TheSignatureIsNamedAfterTheIssuersKeyAndNotTheSubjects(Issuer, Subject)

        /// <summary>
        /// A certificate says which signature it carries, and that name has to
        /// be the issuer's.
        /// </summary>
        /// <remarks>
        /// Verification alone would not catch every version of this mistake.
        /// Two elliptic curve keys of different sizes verify each other's
        /// certificates quite happily, so a P-256 authority taking the hash
        /// from a P-521 subject produced a working certificate that claimed
        /// SHA-512withECDSA over a SHA-256 signature. Nothing rejects it, and
        /// nobody reading it learns what was actually done.
        /// </remarks>
        [Test]
        [TestCase("ecdsa-p256", "ecdsa-p521", "SHA-256withECDSA")]
        [TestCase("ecdsa-p521", "ecdsa-p256", "SHA-512withECDSA")]
        [TestCase("ecdsa-p384", "ecdsa-p256", "SHA-384withECDSA")]
        [TestCase("ecdsa-p256", "rsa-4096",   "SHA-256withECDSA")]
        [TestCase("rsa-2048",   "ecdsa-p521", "SHA256withRSAandMGF1")]
        [TestCase("ed25519",    "ecdsa-p521", "Ed25519")]
        [TestCase("ed448",      "ml-dsa-87",  "Ed448")]
        public void TheSignatureIsNamedAfterTheIssuersKeyAndNotTheSubjects(String Issuer,
                                                                           String Subject,
                                                                           String Expected)
        {

            var authority    = AuthorityOf(Issuer);

            var certificate  = PKIFactory.SignClientCertificate(
                                   $"device.{Subject}.example.org",
                                   KeyPairFor(Subject).Public,
                                   authority.KeyPair.Private,
                                   authority.Certificate
                               );

            Assert.That(certificate.SigAlgName,
                        Is.EqualTo(Expected).IgnoreCase,
                        $"A {Issuer} authority issued to a {Subject} subject and called the signature something else.");

        }

        #endregion

        #region ARootCertificateOfEveryFamilySignsItself(Algorithm)

        /// <summary>
        /// The self-signed case, for every family.
        /// </summary>
        /// <remarks>
        /// This is the case that always worked, and it is kept because the
        /// repair touches it: the signature is now chosen from the issuer's key
        /// rather than the subject's, and for a root those are the same key -
        /// so if anything moved, it shows up here first. SLH-DSA is the
        /// exception that did not work before: the chooser had no branch for
        /// it, so even signing itself was out of reach.
        /// </remarks>
        [Test]
        [TestCaseSource(nameof(EveryFamily))]
        public void ARootCertificateOfEveryFamilySignsItself(String Algorithm)
        {

            var authority = AuthorityOf(Algorithm);

            Assert.Multiple(() => {

                Assert.That(() => authority.Certificate.Verify(authority.KeyPair.Public),
                            Throws.Nothing,
                            $"A {Algorithm} root certificate does not verify against its own key.");

                Assert.That(authority.Certificate.IssuerDN.ToString(),
                            Is.EqualTo(authority.Certificate.SubjectDN.ToString()),
                            "A root certificate has to name itself as its own issuer.");

            });

        }

        #endregion

        #region ACertificateCannotBeSignedWithoutASigningKey()

        /// <summary>
        /// No issuer, no certificate - and said so, rather than a null
        /// reference from inside Bouncy Castle.
        /// </summary>
        [Test]
        public void ACertificateCannotBeSignedWithoutASigningKey()
        {

            Assert.That(() => PKIFactory.SignCertificate(
                                  CertificateTypes.RootCA,
                                  "Test Root CA",
                                  KeyPairFor("ecdsa-p256").Public,
                                  null
                              ),
                        Throws.ArgumentException,
                        "A certificate was signed without anything to sign it with.");

        }

        #endregion

    }

}
