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
    /// The header fields the DKIM signer signs (RFC 6376 §5.4): From whatever the configuration
    /// names, and the signature verified by <see cref="DNSVerifier"/> with the key of the test.
    /// The finding P-6 of SMTPConformanceTests.
    /// </summary>
    public partial class DkimVerifierTests
    {

        private static String SignedBy(RSA Key, String SignedHeaders, String Message)

            => new DkimSigner(new DkimConfig {
                                  Domain        = "client.example",
                                  Selector      = "test",
                                  PrivateKeyPem = Key.ExportPkcs8PrivateKeyPem(),
                                  SignedHeaders = SignedHeaders
                              },
                              new QuietLogger()).SignMessage(Message);

        /// <summary>
        /// The names in the h= of the DKIM-Signature the signer put on top.
        /// </summary>
        private static String[] SignedFieldsOf(String Signed)
        {
            var field = Regex.Match(Signed, @"^DKIM-Signature:(.*?)\r\n(?![ \t])", RegexOptions.Singleline).Groups[1].Value;
            return Regex.Match(field, @"(?:^|;)\s*h=([^;]*)").Groups[1].Value.
                       Split(':').Select(name => name.Trim()).Where(name => name.Length > 0).ToArray();
        }

        private static Int32 FromIn(String[] Names)
            => Names.Count(name => name.Equals("from", StringComparison.OrdinalIgnoreCase));


        #region P-6: From signed whatever the configuration names

        [TestCase("to:subject", TestName = "A signer configured with to:subject signs From all the same")]
        [TestCase("",           TestName = "A signer configured with nothing signs From all the same")]
        public async Task A_signer_configured_without_From_signs_From_all_the_same(String SignedHeaders)
        {

            using var key = RSA.Create(2048);

            var signed            = SignedBy(key, SignedHeaders, Letter);
            var (result, details) = await Verify(signed, Record(key));

            Assert.Multiple(() => {
                Assert.That(FromIn(SignedFieldsOf(signed)), Is.EqualTo(1),               signed);
                Assert.That(result,                         Is.EqualTo(DkimResult.Pass), details);
            });

        }


        [Test(Description = "RFC 6376 §5.4: the signer configured without From signs it, and a From changed after signing breaks the signature")]
        public async Task A_From_changed_after_signing_breaks_the_signature()
        {

            using var key = RSA.Create(2048);

            var signed            = SignedBy(key, "to:subject", Letter).Replace("From: <news@client.example>", "From: <ceo@client.example>");
            var (result, details) = await Verify(signed, Record(key));

            Assert.That(result, Is.EqualTo(DkimResult.Fail), details);

        }


        [TestCase("From:to:subject", TestName = "A signer configured with From signs it once")]
        [TestCase("to:from:subject", TestName = "A signer configured with From in the middle signs it once")]
        public async Task A_signer_configured_with_From_signs_it_once(String SignedHeaders)
        {

            using var key = RSA.Create(2048);

            var signed            = SignedBy(key, SignedHeaders, Letter);
            var (result, details) = await Verify(signed, Record(key));

            Assert.Multiple(() => {
                Assert.That(SignedFieldsOf(signed), Is.EqualTo(SignedHeaders.Split(':')), signed);
                Assert.That(result,                 Is.EqualTo(DkimResult.Pass),         details);
            });

        }


        [Test(Description = "RFC 6376 §5.4: a message without From has its absence signed, and a From added after signing breaks the signature")]
        public async Task A_message_without_From_has_its_absence_signed()
        {

            using var key = RSA.Create(2048);

            var signed            = SignedBy(key, "from:to:subject", Letter.Replace("From: <news@client.example>\r\n", ""));
            var added             = signed.Replace("To: <alice@hermod.test>\r\n", "From: <ceo@client.example>\r\nTo: <alice@hermod.test>\r\n");
            var (asSigned, why)   = await Verify(signed, Record(key));
            var (result, details) = await Verify(added,  Record(key));

            Assert.Multiple(() => {
                Assert.That(FromIn(SignedFieldsOf(signed)), Is.EqualTo(1),               signed);
                Assert.That(asSigned,                       Is.EqualTo(DkimResult.Pass), why);
                Assert.That(result,                         Is.EqualTo(DkimResult.Fail), details);
            });

        }

        #endregion

    }

}
