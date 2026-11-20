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
using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// The DKIM keys Hermod makes and signs with: at least 1024 bits (RFC 8301 §3.2), and the
    /// key record written in character-strings of at most 255 octets (RFC 1035 §3.3, RFC 6376
    /// §3.6.2.2). The findings P-2 and P-3 of SMTPConformanceTests.
    /// </summary>
    [TestFixture]
    public class DkimKeyTests
    {

        private sealed class QuietLogger : ILogger
        {
            public void Log(LogLevel level, String message) { }
        }

        private const String Letter = "From: <news@hermod.example>\r\n" +
                                      "To: <you@client.example>\r\n" +
                                      "Subject: key size\r\n" +
                                      "\r\n" +
                                      "Hello.\r\n";

        private static DkimConfig ConfigWith(Int32 Bits)
        {
            using var rsa = RSA.Create(Bits);
            return new DkimConfig { Domain = "hermod.example", Selector = "test", PrivateKeyPem = rsa.ExportPkcs8PrivateKeyPem() };
        }


        [TestCase( 512)]
        [TestCase(1023)]
        public void The_key_generators_refuse_a_key_under_1024_bits(Int32 Bits)
        {
            Assert.Multiple(() => {
                Assert.That(() => DkimKeyGenerator.GenerateKeyPair("hermod.example", "test", Bits), Throws.InstanceOf<ArgumentOutOfRangeException>());
                Assert.That(() => DkimKeyMaterial.NewRsaPem(Bits),                                   Throws.InstanceOf<ArgumentOutOfRangeException>());
            });
        }

        [TestCase(1024)]
        [TestCase(2048)]
        public void The_key_generators_make_a_key_of_the_size_asked_for(Int32 Bits)
        {

            using var generated = RSA.Create();
            generated.ImportFromPem(DkimKeyGenerator.GenerateKeyPair("hermod.example", "test", Bits).PrivateKeyPem);

            using var made = RSA.Create();
            made.ImportFromPem(DkimKeyMaterial.NewRsaPem(Bits));

            Assert.Multiple(() => {
                Assert.That(generated.KeySize, Is.EqualTo(Bits));
                Assert.That(made.KeySize,      Is.EqualTo(Bits));
            });

        }

        [Test(Description = "RFC 8301 §3.2: \"Signers MUST use RSA keys of at least 1024 bits for all keys\"")]
        public void The_signer_refuses_a_key_of_512_bits()
        {
            Assert.That(() => new DkimSigner(ConfigWith(512), new QuietLogger()), Throws.InstanceOf<ArgumentException>());
        }

        [Test(Description = "RFC 8301 §3.2: a key of 1024 bits signs")]
        public void The_signer_signs_with_a_key_of_1024_bits()
        {
            Assert.That(new DkimSigner(ConfigWith(1024), new QuietLogger()).SignMessage(Letter), Does.StartWith("DKIM-Signature:"));
        }

        [TestCase(1024)]
        [TestCase(2048)]
        [TestCase(4096)]
        public void The_key_record_is_written_in_strings_of_at_most_255_octets(Int32 Bits)
        {

            var (privateKey, _, line) = DkimKeyGenerator.GenerateKeyPair("hermod.example", "test", Bits);

            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKey);

            var strings = Regex.Matches(line, "\"((?:[^\"\\\\]|\\\\.)*)\"").Select(match => match.Groups[1].Value).ToList();

            Assert.Multiple(() => {
                Assert.That(line,                                   Does.StartWith("test._domainkey.hermod.example. IN TXT \""));
                Assert.That(strings.Select(text => text.Length),    Has.All.LessThanOrEqualTo(255));
                Assert.That(String.Concat(strings),                 Is.EqualTo($"v=DKIM1; k=rsa; p={Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo())}"));
            });

        }

    }

}
