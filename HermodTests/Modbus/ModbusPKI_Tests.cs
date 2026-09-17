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
using Org.BouncyCastle.X509;

using org.GraphDefined.Vanaheimr.Hermod.SunSpecModbusTLS.Common;
using org.GraphDefined.Vanaheimr.Hermod.SunSpecModbusTLS.PKI;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.Modbus
{

    /// <summary>
    /// The demo PKI for SunSpec Modbus-TLS, in every kind of key this library
    /// makes.
    /// </summary>
    /// <remarks>
    /// It used to call ECDsa.Create with a curve written into the line, so the
    /// one corner of this library whose job is generating a PKI was also the
    /// only corner that could generate nothing but what .NET can. It goes
    /// through PKIFactory now, which means a chain in Ed448 or ML-DSA can be
    /// made and handed to a device to see what it says - which is the point of
    /// a demo PKI.
    ///
    /// That the result is usable is measured elsewhere and harder:
    /// SunSpecModbusTLSTests builds one of these and completes real TLS
    /// handshakes with it.
    /// </remarks>
    [TestFixture]
    public class ModbusPKI_Tests
    {

        #region Data

        private String directory = "";

        #endregion

        #region SetUp / TearDown

        [SetUp]
        public void MakeADirectory()
        {
            directory = Path.Combine(Path.GetTempPath(), "modbus-pki-" + Guid.NewGuid().ToString("N")[..8]);
        }

        [TearDown]
        public void TakeItAwayAgain()
        {
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
            catch
            { }
        }

        #endregion

        #region (private) Read(BaseName)

        private X509Certificate Read(String BaseName)

            => new X509CertificateParser().ReadCertificate(
                   File.ReadAllBytes(Path.Combine(directory, $"{BaseName}.crt"))
               );

        #endregion


        #region EveryKindOfKeyMakesAWholePKI(Algorithm)

        /// <summary>
        /// A root, two issuing authorities, a server and five clients - in each
        /// kind of key.
        /// </summary>
        /// <remarks>
        /// One case per family rather than all thirteen: what is being asked is
        /// whether the generator reaches for the right key and gets a chain
        /// that verifies, not how large the key was. The post-quantum ones are
        /// the point - none of them could be made here at all before.
        ///
        /// The chain is checked by verifying each certificate against the key
        /// of the one above it, which is the same arithmetic a client does
        /// while walking it.
        /// </remarks>
        [Test]
        [TestCase("ecdsa-p256")]
        [TestCase("rsa-2048")]
        [TestCase("ed25519")]
        [TestCase("ed448")]
        [TestCase("ml-dsa-44")]
        public async Task EveryKindOfKeyMakesAWholePKI(String Algorithm)
        {

            await new ModbusPKI().BuildPKI(directory,
                                           CAAlgorithm:    Algorithm,
                                           LeafAlgorithm:  Algorithm);

            var root           = Read("ca");
            var deviceCA       = Read("issuing-device-ca");
            var clientsCA      = Read("issuing-clients-ca");
            var server         = Read("server");

            Assert.Multiple(() => {

                Assert.That(() => root.     Verify(root.     GetPublicKey()), Throws.Nothing,
                            "The root does not verify against its own key.");

                Assert.That(() => deviceCA. Verify(root.     GetPublicKey()), Throws.Nothing,
                            "The device authority does not verify against the root.");

                Assert.That(() => clientsCA.Verify(root.     GetPublicKey()), Throws.Nothing,
                            "The clients authority does not verify against the root.");

                Assert.That(() => server.   Verify(deviceCA. GetPublicKey()), Throws.Nothing,
                            "The server certificate does not verify against the device authority.");

                foreach (var role in SunSpecRoles.AllMandatory)
                    Assert.That(() => Read($"client-{role}").Verify(clientsCA.GetPublicKey()), Throws.Nothing,
                                $"The '{role}' client certificate does not verify against the clients authority.");

                // Every key is written out, whatever kind it is - a PEM has no
                // opinion about what .NET can hold.
                Assert.That(File.Exists(Path.Combine(directory, "server.key")), Is.True,
                            "The server's private key was not written.");

            });

        }

        #endregion

        #region TheRoleIsInTheClientCertificates(Role)

        /// <summary>
        /// The SunSpec role extension survived the move to another certificate
        /// builder.
        /// </summary>
        /// <remarks>
        /// It is the one thing in this PKI that no certificate library knows
        /// about - a private arc with a UTF8String in it, per [MBTLS] section
        /// 8.4 - so it is the one thing a rewrite is most likely to lose
        /// quietly. A client certificate without it is a certificate a SunSpec
        /// device will not let do anything.
        /// </remarks>
        [Test]
        public async Task TheRoleIsInTheClientCertificates()
        {

            await new ModbusPKI().BuildPKI(directory);

            Assert.Multiple(() => {

                foreach (var role in SunSpecRoles.AllMandatory)
                {

                    var extension = Read($"client-{role}").GetExtensionValue(new DerObjectIdentifier(SunSpecRoles.RoleOid));

                    Assert.That(extension, Is.Not.Null,
                                $"The '{role}' client certificate carries no role extension at all.");

                    Assert.That(DerUtf8String.GetInstance(
                                    Asn1Object.FromByteArray(extension!.GetOctets())
                                ).GetString(),
                                Is.EqualTo(role),
                                $"The '{role}' client certificate names a different role.");

                }

                // And the one that deliberately has none.
                Assert.That(Read("client-NO-ROLE").GetExtensionValue(new DerObjectIdentifier(SunSpecRoles.RoleOid)),
                            Is.Null,
                            "The certificate kept for negative tests grew a role.");

            });

        }

        #endregion

        #region TheDefaultsAreWhatTheyAlwaysWere()

        /// <summary>
        /// P-384 for the authorities, P-256 for the leaves.
        /// </summary>
        /// <remarks>
        /// Unchanged by the rewrite on purpose: this is what SunSpec
        /// deployments interoperate with, and a generator that quietly started
        /// issuing something else would be a demo that stops matching the
        /// devices it is a demo for.
        /// </remarks>
        [Test]
        public async Task TheDefaultsAreWhatTheyAlwaysWere()
        {

            await new ModbusPKI().BuildPKI(directory);

            Assert.Multiple(() => {

                Assert.That(Read("ca").    SigAlgName, Does.Contain("384").IgnoreCase,
                            "The root is no longer signed with a P-384 key.");

                Assert.That(Read("server").SigAlgName, Does.Contain("384").IgnoreCase,
                            "The server certificate is no longer signed by a P-384 authority.");

                // The leaf's own key, which is the P-256 one - read off the
                // certificate rather than off the signature, because the
                // signature is the issuer's.
                Assert.That(Read("server").GetPublicKey().GetType().Name,
                            Does.Contain("ECPublicKey"),
                            "The server's own key is no longer an elliptic curve one.");

            });

        }

        #endregion

        #region AKindOfKeyThisLibraryDoesNotMakeIsRefused()

        /// <summary>
        /// And the refusal lists what it does make.
        /// </summary>
        [Test]
        public void AKindOfKeyThisLibraryDoesNotMakeIsRefused()
        {

            Assert.That(async () => await new ModbusPKI().BuildPKI(directory, CAAlgorithm: "quantum-banana"),
                        Throws.ArgumentException.With.Message.Contains("ecdsa-p256"));

        }

        #endregion

    }

}
