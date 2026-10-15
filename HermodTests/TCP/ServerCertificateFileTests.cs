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

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using NUnit.Framework;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TCP
{

    /// <summary>
    /// A server certificate in files as certbot writes them - fullchain.pem and
    /// privkey.pem - read by <see cref="ServerCertificateChain.Load"/>, and
    /// read again by <see cref="ServerCertificateFile"/> whenever they change:
    /// a renewal is presented without a restart, and half of one - the
    /// certificate replaced, the key not yet - leaves the old certificate in
    /// place instead of none.
    /// </summary>
    /// <remarks>
    /// No TLS context is built here, so nothing is put into a certificate
    /// store, and the names need not be unique (see ServerCertificateChainTests).
    /// </remarks>
    [TestFixture]
    public class ServerCertificateFileTests
    {

        #region Data

        private String directory      = "";
        private String fullchainPath  = "";
        private String privkeyPath    = "";

        #endregion

        #region Setup / TearDown

        [SetUp]
        public void MakeADirectory()
        {
            directory      = Directory.CreateTempSubdirectory("hermod-certificates-").FullName;
            fullchainPath  = Path.Combine(directory, "fullchain.pem");
            privkeyPath    = Path.Combine(directory, "privkey.pem");
        }

        [TearDown]
        public void Clean()
        {
            try { Directory.Delete(directory, recursive: true); } catch { }
        }

        #endregion

        #region (private static) Certificates

        private sealed record Issued(String CertificatePem, String KeyPem, String Thumbprint);

        /// <summary>
        /// A self-signed certificate and its PKCS#8 key as PEM - or one signed by
        /// the given issuer.
        /// </summary>
        private static Issued Certificate(String                                   Name,
                                          Boolean                                  Ecdsa    = false,
                                          (X509Certificate2 Certificate, RSA Key)? Issuer   = null)
        {

            var now = DateTimeOffset.UtcNow;

            if (Ecdsa)
            {
                using var ecdsa       = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                using var certificate = new CertificateRequest($"CN={Name}", ecdsa, HashAlgorithmName.SHA256).CreateSelfSigned(now.AddMinutes(-5), now.AddDays(90));
                return new (certificate.ExportCertificatePem(), ecdsa.ExportPkcs8PrivateKeyPem(), certificate.Thumbprint);
            }

            using var rsa  = RSA.Create(2048);
            var request    = new CertificateRequest($"CN={Name}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            using var issued = Issuer is { } issuer
                                   ? request.Create(issuer.Certificate.CopyWithPrivateKey(issuer.Key), now.AddMinutes(-5), now.AddDays(90), RandomNumberGenerator.GetBytes(16))
                                   : request.CreateSelfSigned(now.AddMinutes(-5), now.AddDays(90));

            return new (issued.ExportCertificatePem(), rsa.ExportPkcs8PrivateKeyPem(), issued.Thumbprint);

        }

        private void Write(String CertificatePem, String KeyPem)
        {
            File.WriteAllText(fullchainPath, CertificatePem);
            File.WriteAllText(privkeyPath,   KeyPem);
        }

        #endregion


        #region ServerCertificateChain.Load

        [TestCase(false)]
        [TestCase(true)]
        public void A_PEM_pair_is_read_RSA_or_ECDSA(Boolean Ecdsa)
        {

            var issued = Certificate("mx.hermod.test", Ecdsa);
            Write(issued.CertificatePem, issued.KeyPem);

            var chain  = ServerCertificateChain.Load(fullchainPath, privkeyPath);

            Assert.Multiple(() => {
                Assert.That(chain.Certificate.Thumbprint,      Is.EqualTo(issued.Thumbprint));
                Assert.That(chain.Certificate.HasPrivateKey,   Is.True);
                Assert.That(chain.HasIntermediates,            Is.False);
            });

        }

        [Test]
        public void The_server_certificate_is_the_one_the_key_belongs_to_whatever_the_order()
        {

            using var caKey   = RSA.Create(2048);
            var caRequest     = new CertificateRequest("CN=Hermod Test Issuer", caKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            using var ca      = caRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
            using var caPublic = X509CertificateLoader.LoadCertificate(ca.RawData);

            var leaf          = Certificate("mx.hermod.test", Issuer: (caPublic, caKey));

            // The intermediate first: a fullchain.pem put together by hand.
            Write(ca.ExportCertificatePem() + "\n" + leaf.CertificatePem, leaf.KeyPem);

            var chain         = ServerCertificateChain.Load(fullchainPath, privkeyPath);

            Assert.Multiple(() => {
                Assert.That(chain.Certificate.Thumbprint,      Is.EqualTo(leaf.Thumbprint), "not the first certificate in the file");
                Assert.That(chain.Intermediates,               Is.Empty, "a self-signed issuer is a root, and roots are not sent");
            });

        }

        [Test]
        public void A_key_that_belongs_to_no_certificate_is_said_in_words()
        {

            var one = Certificate("one.hermod.test");
            var two = Certificate("two.hermod.test");

            Write(one.CertificatePem, two.KeyPem);

            Assert.That(() => ServerCertificateChain.Load(fullchainPath, privkeyPath),
                        Throws.InstanceOf<CryptographicException>().With.Message.Contains("does not belong"));

        }

        [Test]
        public void The_key_may_be_in_the_certificate_file()
        {

            var issued = Certificate("mx.hermod.test", Ecdsa: true);
            File.WriteAllText(fullchainPath, issued.CertificatePem + Environment.NewLine + issued.KeyPem);

            Assert.That(ServerCertificateChain.Load(fullchainPath).Certificate.Thumbprint, Is.EqualTo(issued.Thumbprint));

        }

        [Test]
        public void PKCS12_is_read_as_well()
        {

            using var rsa         = RSA.Create(2048);
            using var certificate = new CertificateRequest("CN=pfx.hermod.test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).
                                        CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));

            var pfx = Path.Combine(directory, "server.pfx");
            File.WriteAllBytes(pfx, certificate.Export(X509ContentType.Pkcs12, "secret"));

            var chain = ServerCertificateChain.Load(pfx, Password: "secret");

            Assert.Multiple(() => {
                Assert.That(chain.Certificate.Subject,         Is.EqualTo("CN=pfx.hermod.test"));
                Assert.That(chain.Certificate.HasPrivateKey,   Is.True);
            });

        }

        #endregion

        #region ServerCertificateFile

        [Test]
        public void No_files_yet_is_no_certificate_yet_and_no_exception()
        {

            var file = new ServerCertificateFile(fullchainPath, privkeyPath, CheckInterval: TimeSpan.Zero);

            Assert.Multiple(() => {
                Assert.That(file.Current,     Is.Null);
                Assert.That(file.LastError,   Does.Contain("cannot be read"));
            });

            var issued = Certificate("mx.hermod.test");
            Write(issued.CertificatePem, issued.KeyPem);

            Assert.Multiple(() => {
                Assert.That(file.Current?.Certificate.Thumbprint, Is.EqualTo(issued.Thumbprint), "as soon as certbot has written them");
                Assert.That(file.LastError,                       Is.Null);
                Assert.That(file.LoadedAt,                        Is.Not.Null);
            });

        }

        [Test]
        public void A_renewal_is_presented_and_half_of_one_keeps_the_old_certificate()
        {

            var old      = Certificate("mx.hermod.test");
            var renewed  = Certificate("mx.hermod.test");

            Write(old.CertificatePem, old.KeyPem);

            var file     = new ServerCertificateFile(fullchainPath, privkeyPath, CheckInterval: TimeSpan.Zero);
            var loaded   = new List<String>();
            var failures = new List<String>();

            file.OnLoaded     += chain => loaded.Add(chain.Certificate.Thumbprint);
            file.OnLoadFailed += error => failures.Add(error);

            Assert.That(file.Current?.Certificate.Thumbprint, Is.EqualTo(old.Thumbprint));

            // certbot replaces the certificate first ...
            File.WriteAllText(fullchainPath, renewed.CertificatePem);

            Assert.Multiple(() => {
                Assert.That(file.Current?.Certificate.Thumbprint, Is.EqualTo(old.Thumbprint), "the old one, not none");
                Assert.That(file.Current?.Certificate.Thumbprint, Is.EqualTo(old.Thumbprint), "asked twice");
                Assert.That(file.LastError,                       Does.Contain("does not belong"));
                Assert.That(failures,                             Has.Count.EqualTo(1), "said once, not at every look");
            });

            // ... and then the key.
            File.WriteAllText(privkeyPath, renewed.KeyPem);

            Assert.Multiple(() => {
                Assert.That(file.Current?.Certificate.Thumbprint, Is.EqualTo(renewed.Thumbprint));
                Assert.That(file.LastError,                       Is.Null);
                Assert.That(loaded,                               Is.EqualTo(new[] { renewed.Thumbprint }));
            });

        }

        [Test]
        public void The_files_are_looked_at_no_more_often_than_asked()
        {

            var old      = Certificate("mx.hermod.test");
            var renewed  = Certificate("mx.hermod.test");

            Write(old.CertificatePem, old.KeyPem);

            var file     = new ServerCertificateFile(fullchainPath, privkeyPath, CheckInterval: TimeSpan.FromHours(1));

            Write(renewed.CertificatePem, renewed.KeyPem);

            Assert.That(file.Current?.Certificate.Thumbprint, Is.EqualTo(old.Thumbprint), "not looked at again within the hour");
            Assert.That(file.Reload(),                         Is.True);
            Assert.That(file.Current?.Certificate.Thumbprint, Is.EqualTo(renewed.Thumbprint), "unless asked to");

        }

        #endregion

    }

}
