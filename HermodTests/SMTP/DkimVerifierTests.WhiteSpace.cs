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
    /// White space as DKIM has it, in the verifier (RFC 6376 §2.8, §3.4.2, §3.4.4, §3.5): the
    /// relaxed canonicalization deletes WSP - a space or a tab - and nothing else, and white space
    /// in a bh= is ignored. The findings V-29 and V-30 of SMTPConformanceTests.
    /// </summary>
    public partial class DkimVerifierTests
    {

        /// <summary>
        /// A message signed c=relaxed/relaxed over From, To and Subject without Hermod's
        /// canonicalization: the body and the header field values are written so that relaxed
        /// canonicalization leaves them as they are - one space between words, no WSP at the end
        /// of a line - and the DKIM-Signature is canonicalized by hand. Signed over the UTF-8
        /// octets, as every signer does.
        /// </summary>
        private static String SignAsItIs(RSA Key, String Subject, String Body, Func<String, String>? Tags = null)
        {

            var fields    = new[] { ("From", "<news@client.example>"), ("To", "<alice@hermod.test>"), ("Subject", Subject) };
            var bodyHash  = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(Body)));
            var tags      = (Tags ?? (tags => tags))($"v=1; a=rsa-sha256; c=relaxed/relaxed; d=client.example; s=test; h=from:to:subject; bh={bodyHash}") + "; b=";

            // §3.4.2: unfold, runs of WSP to one space, none at the start and the end.
            var input     = String.Concat(fields.Select(field => $"{field.Item1.ToLowerInvariant()}:{field.Item2}\r\n")) +
                            "dkim-signature:" + Regex.Replace(tags.Replace("\r\n", ""), "[ \t]+", " ").Trim(' ', '\t');

            var signature = Convert.ToBase64String(Key.SignData(Encoding.UTF8.GetBytes(input), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

            return $"DKIM-Signature: {tags}{signature}\r\n" +
                   String.Concat(fields.Select(field => $"{field.Item1}: {field.Item2}\r\n")) +
                   "MIME-Version: 1.0\r\n" +
                   "Content-Type: text/plain; charset=utf-8\r\n" +
                   "Content-Transfer-Encoding: 8bit\r\n" +
                   "\r\n" +
                   Body;

        }


        #region V-29: WSP, and nothing else

        [TestCase("Rendez-vous demain, voilà",  TestName = "A relaxed body whose line ends in à (C3 A0) verifies")]
        [TestCase("Do zobaczenia z tobą",       TestName = "A relaxed body whose line ends in ą (C4 85) verifies")]
        [TestCase("До встречи в выходных",      TestName = "A relaxed body whose line ends in х (D1 85) verifies")]
        [TestCase("Total: 10 EUR ",        TestName = "A relaxed body whose line ends in a NO-BREAK SPACE (C2 A0) verifies")]
        [TestCase("Page 1\f",                   TestName = "A relaxed body whose line ends in a form feed verifies")]
        public async Task A_relaxed_body_whose_line_ends_in_no_WSP_verifies(String Line)
        {

            using var key = RSA.Create(2048);

            var (result, details) = await Verify(SignAsItIs(key, "White space", $"Hello,\r\n{Line}\r\nBye.\r\n"), Record(key));

            Assert.That(result, Is.EqualTo(DkimResult.Pass), details);

        }


        [TestCase("Spotkanie z tobą",     TestName = "A relaxed Subject that ends in ą (C4 85) verifies")]
        [TestCase("Le programme, voilà",  TestName = "A relaxed Subject that ends in à (C3 A0) verifies")]
        public async Task A_relaxed_header_field_that_ends_in_no_WSP_verifies(String Subject)
        {

            using var key = RSA.Create(2048);

            var (result, details) = await Verify(SignAsItIs(key, Subject, "Hello.\r\n"), Record(key));

            Assert.That(result, Is.EqualTo(DkimResult.Pass), details);

        }


        [Test(Description = "RFC 6376 §3.4.4: spaces and tabs at the end of a line are deleted, runs of them inside it are one space - white space changed after signing keeps a relaxed/relaxed signature (a guard)")]
        public async Task White_space_that_is_WSP_is_deleted_at_the_end_of_a_line()
        {

            using var key = RSA.Create(2048);

            var signed   = SignAsItIs(key, "White space", "Hello, world.\r\nBye.\r\n");
            var modified = signed.Replace("Hello, world.\r\n", "Hello, \t world. \t \r\n");

            Assert.That(modified, Is.Not.EqualTo(signed));

            var (result, details) = await Verify(modified, Record(key));

            Assert.That(result, Is.EqualTo(DkimResult.Pass), details);

        }

        #endregion

        #region V-30: white space in bh=

        [TestCase("bh=$1\r\n\t$2",  TestName = "A bh= folded verifies")]
        [TestCase("bh= $1 $2 ",     TestName = "A bh= with spaces in it and around it verifies")]
        public async Task A_bh_with_white_space_verifies(String Replacement)
        {

            using var key = RSA.Create(2048);

            var (result, details) = await Verify(SignAsItIs(key, "White space", "Hello.\r\n", tags => Regex.Replace(tags, "bh=(.{20})(.+)$", Replacement)), Record(key));

            Assert.That(result, Is.EqualTo(DkimResult.Pass), details);

        }


        [Test(Description = "RFC 6376 §3.5: a bh= folded, of another body than the message has, does not verify (a guard)")]
        public async Task A_bh_with_white_space_of_another_body_does_not_verify()
        {

            using var key = RSA.Create(2048);

            var signed   = SignAsItIs(key, "White space", "Hello.\r\n", tags => Regex.Replace(tags, "bh=(.{20})(.+)$", "bh=$1\r\n\t$2"));
            var modified = signed.Replace("\r\nHello.\r\n", "\r\nHallo.\r\n");

            Assert.That(modified, Is.Not.EqualTo(signed));

            var (result, details) = await Verify(modified, Record(key));

            Assert.That(result, Is.EqualTo(DkimResult.Fail), details);

        }

        #endregion

    }

}
