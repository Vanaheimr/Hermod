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
using System.Text;
using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// The body hash the DKIM signer writes (RFC 6376 §2.8, §3.4.4, §3.5), compared with one
    /// computed by hand: the relaxed canonicalization deletes WSP - a space or a tab - and
    /// nothing else, both when the message is signed as text and as its octets; and a c= of one
    /// name hashes the body simple. The findings P-4 and P-5 of SMTPConformanceTests.
    /// </summary>
    [TestFixture]
    public class DkimSignerTests
    {

        #region (private) helpers

        private sealed class QuietLogger : ILogger
        {
            public void Log(LogLevel level, String message) { }
        }

        private static DkimSigner Signer(String Canonicalization = "relaxed/relaxed")
        {
            using var rsa = RSA.Create(2048);
            return new DkimSigner(new DkimConfig {
                                      Domain           = "hermod.test",
                                      Selector         = "test",
                                      PrivateKeyPem    = rsa.ExportPkcs8PrivateKeyPem(),
                                      SignedHeaders    = "from:to:subject",
                                      Canonicalization = Canonicalization
                                  },
                                  new QuietLogger());
        }

        private static String Message(String Body)

            => "From: <news@hermod.test>\r\n" +
               "To: <you@outbound.test>\r\n" +
               "Subject: white space\r\n" +
               "MIME-Version: 1.0\r\n" +
               "Content-Type: text/plain; charset=utf-8\r\n" +
               "Content-Transfer-Encoding: 8bit\r\n" +
               "\r\n" +
               Body;

        /// <summary>
        /// The SHA-256 of the UTF-8 octets of a body, as it is or canonicalized by hand.
        /// </summary>
        private static String HashOf(String Body)
            => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(Body)));

        /// <summary>
        /// The value of a tag of the DKIM-Signature the signer put on top, without white space.
        /// </summary>
        private static String TagOf(String Signed, String Tag)
        {
            var field = Signed[..Signed.IndexOf("\r\nFrom:", StringComparison.Ordinal)].Replace("\r\n", "");
            Assert.That(field, Does.StartWith("DKIM-Signature:"), Signed);
            return Regex.Replace(Regex.Match(field, $@"(?:^DKIM-Signature:|;)\s*{Tag}=([^;]*)").Groups[1].Value, @"\s+", "");
        }

        #endregion


        #region P-4: WSP, and nothing else

        [TestCase("Rendez-vous demain, voilà",  TestName = "Signed as its octets, a line ending in à (C3 A0) keeps it")]
        [TestCase("Do zobaczenia z tobą",       TestName = "Signed as its octets, a line ending in ą (C4 85) keeps it")]
        [TestCase("До встречи в выходных",      TestName = "Signed as its octets, a line ending in х (D1 85) keeps it")]
        [TestCase("Total: 10 EUR ",        TestName = "Signed as its octets, a line ending in a NO-BREAK SPACE keeps it")]
        [TestCase("Page 1\f",                   TestName = "Signed as its octets, a line ending in a form feed keeps it")]
        public void Signed_as_its_octets_a_line_ending_in_no_WSP_keeps_it(String Line)
        {

            var body   = $"Hello,\r\n{Line}\r\nBye.\r\n";
            var signed = Encoding.UTF8.GetString(Signer().SignOctets(Encoding.UTF8.GetBytes(Message(body))));

            Assert.That(TagOf(signed, "bh"), Is.EqualTo(HashOf(body)), signed);

        }


        [TestCase("Total: 10 EUR ",  TestName = "Signed as text, a line ending in a NO-BREAK SPACE keeps it")]
        [TestCase("Page 1\f",             TestName = "Signed as text, a line ending in a form feed keeps it")]
        [TestCase("Do zobaczenia z tobą", TestName = "Signed as text, a line ending in ą keeps it")]
        public void Signed_as_text_a_line_ending_in_no_WSP_keeps_it(String Line)
        {

            var body   = $"Hello,\r\n{Line}\r\nBye.\r\n";
            var signed = Signer().SignMessage(Message(body));

            Assert.That(TagOf(signed, "bh"), Is.EqualTo(HashOf(body)), signed);

        }


        [Test(Description = "RFC 6376 §3.4.4: spaces and tabs at the end of a line are deleted and runs of them are one space, signed as text and as octets (a guard)")]
        public void Spaces_and_tabs_at_the_end_of_a_line_are_deleted()
        {

            var body     = "Hello, \t world. \t \r\nGrüße  \r\nBye.\r\n";
            var expected = HashOf("Hello, world.\r\nGrüße\r\nBye.\r\n");

            var asText   = Signer().SignMessage(Message(body));
            var asOctets = Encoding.UTF8.GetString(Signer().SignOctets(Encoding.UTF8.GetBytes(Message(body))));

            Assert.Multiple(() => {
                Assert.That(TagOf(asText,   "bh"), Is.EqualTo(expected), asText);
                Assert.That(TagOf(asOctets, "bh"), Is.EqualTo(expected), asOctets);
            });

        }

        #endregion

        #region P-5: c= of one name

        [TestCase("relaxed",          "Hello,  world.  \r\nBye.\r\n",  TestName = "A signer configured with c=relaxed hashes the body simple")]
        [TestCase("relaxed/simple",   "Hello,  world.  \r\nBye.\r\n",  TestName = "A signer configured with c=relaxed/simple hashes the body simple")]
        [TestCase("relaxed/relaxed",  "Hello, world.\r\nBye.\r\n",     TestName = "A signer configured with c=relaxed/relaxed hashes the body relaxed")]
        [TestCase("simple/simple",    "Hello,  world.  \r\nBye.\r\n",  TestName = "A signer configured with c=simple/simple hashes the body simple")]
        public void The_body_is_hashed_as_c_says(String Canonicalization, String Hashed)
        {

            var signed = Signer(Canonicalization).SignMessage(Message("Hello,  world.  \r\nBye.\r\n"));

            Assert.Multiple(() => {
                Assert.That(TagOf(signed, "c"),  Is.EqualTo(Canonicalization), signed);
                Assert.That(TagOf(signed, "bh"), Is.EqualTo(HashOf(Hashed)),  signed);
            });

        }

        #endregion

    }

}
