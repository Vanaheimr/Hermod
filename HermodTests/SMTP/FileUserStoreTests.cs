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
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.SMTP;
using org.GraphDefined.Vanaheimr.Hermod.SMTP.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// The accounts of <see cref="FileUserStore"/>: none unless written down, a client
    /// certificate only by its pinned thumbprint (SASL EXTERNAL), and passwords checked
    /// against the salted SCRAM keys.
    /// </summary>
    [TestFixture]
    public class FileUserStoreTests
    {

        #region Helpers

        private String directory = "";

        [SetUp]
        public void CreateDirectory()
            => directory = Directory.CreateTempSubdirectory("hermod-users-").FullName;

        [TearDown]
        public void DeleteDirectory()
        {
            try { Directory.Delete(directory, recursive: true); } catch { }
        }

        private String UsersFile(params String[] Lines)
        {
            var path = Path.Combine(directory, "users.txt");
            File.WriteAllLines(path, Lines);
            return path;
        }

        private static String Sha256Hex(String Password)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Password))).ToLowerInvariant();

        private static String ScramColumns(String Password)
        {
            var scram = ScramCredentialGenerator.Generate(Password);
            return $"{scram.SaltBase64}:{scram.StoredKeyBase64}:{scram.ServerKeyBase64}:{scram.Iterations}";
        }

        /// <summary>
        /// A certificate anybody can make: self-signed, any subject.
        /// </summary>
        private static X509Certificate2 SelfSigned(String Subject)
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            return new CertificateRequest(Subject, key, HashAlgorithmName.SHA256).
                       CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
        }

        private sealed class QuietLogger : ILogger
        {
            public void Log(LogLevel level, String message) { }
        }

        #endregion


        [Test]
        public async Task A_missing_users_file_is_created_without_accounts()
        {

            var path  = Path.Combine(directory, "users.txt");
            var store = new FileUserStore(path);

            Assert.That(File.Exists(path), "a template to fill in");

            // The demo accounts it used to write, with passwords published in the source.
            foreach (var (user, password) in new[] { ("admin", "test123"), ("user", "test123"), ("demo", "demo") })
            {
                Assert.That(await store.GetUserAsync(user),                    Is.Null,  user);
                Assert.That(await store.ValidatePasswordAsync(user, password), Is.False, user);
            }

            using var certificate = SelfSigned("CN=certonly");
            Assert.That(await store.GetUserByCertificateAsync(certificate), Is.Null, "no account any certificate opens");

        }


        [Test]
        public async Task A_certificate_opens_an_account_only_by_its_pinned_thumbprint()
        {

            using var pinned     = SelfSigned("CN=device");
            using var pinned256  = SelfSigned("CN=device256");
            using var admin      = SelfSigned("CN=admin");

            var store = new FileUserStore(UsersFile(
                            $"admin:{Sha256Hex("secret")}:{ScramColumns("secret")}:",
                            "anycert::::::*",                                                       // the wildcard, well-formed
                            $"device::::::{pinned.Thumbprint}",                                     // SHA-1
                            $"device256::::::{pinned256.GetCertHashString(HashAlgorithmName.SHA256)}" // SHA-256
                        ));

            Assert.That((await store.GetUserByCertificateAsync(pinned))?.   Username, Is.EqualTo("device"));
            Assert.That((await store.GetUserByCertificateAsync(pinned256))?.Username, Is.EqualTo("device256"));

            // "CN=admin" is what its maker wrote, and "*" would take any certificate at all.
            Assert.That(await store.GetUserByCertificateAsync(admin), Is.Null);

        }


        [Test]
        public async Task AUTH_EXTERNAL_with_a_self_made_admin_certificate_fails()
        {

            using var admin = SelfSigned("CN=admin");

            var store   = new FileUserStore(UsersFile($"admin:{Sha256Hex("secret")}:{ScramColumns("secret")}:"));
            var manager = new SmtpAuthManager(store, new QuietLogger());

            manager.SetClientCertificate(admin);
            manager.StartAuth("EXTERNAL");

            var result = await manager.ProcessResponseAsync("");

            Assert.That(result.Result,           Is.EqualTo(AuthResult.Fail));
            Assert.That(manager.IsAuthenticated, Is.False);

        }


        [Test]
        public async Task A_password_is_checked_against_the_salted_SCRAM_keys()
        {

            // The two columns disagree on purpose: the SCRAM keys decide.
            var store = new FileUserStore(UsersFile($"alice:{Sha256Hex("old")}:{ScramColumns("new")}:"));

            Assert.That(await store.ValidatePasswordAsync("alice", "new"), Is.True);
            Assert.That(await store.ValidatePasswordAsync("alice", "old"), Is.False, "the unsalted hash is only a fallback");
            Assert.That(await store.ValidatePasswordAsync("alice", ""),    Is.False);

        }


        [Test]
        public async Task Without_SCRAM_keys_the_SHA256_column_still_works()
        {

            var store = new FileUserStore(UsersFile(
                            $"legacy:{Sha256Hex("secret")}:::::",
                            $"placeholder:{Sha256Hex("secret")}:AAAAAAAAAAAAAAAAAAAAAA==:StoredKeyBase64:ServerKeyBase64:4096:"
                        ));

            Assert.That(await store.ValidatePasswordAsync("legacy",      "secret"), Is.True);
            Assert.That(await store.ValidatePasswordAsync("legacy",      "wrong"),  Is.False);
            Assert.That(await store.ValidatePasswordAsync("placeholder", "secret"), Is.True, "columns that are no SCRAM keys");

        }

    }

}
