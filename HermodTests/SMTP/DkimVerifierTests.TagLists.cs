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

using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// The tag-lists of a DKIM-Signature and of a key record (RFC 6376 §3.2, §3.5, §3.6.1): the
    /// canonicalization a c= tag of one name stands for, a tag named twice, the case of a tag's
    /// name, and the place of a key record's v=. The findings V-26 to V-28 of
    /// SMTPConformanceTests.
    /// </summary>
    public partial class DkimVerifierTests
    {

        private const String Spaced = "From: <news@client.example>\r\n" +
                                      "To: <alice@hermod.test>\r\n" +
                                      "Subject: DKIM tag-lists\r\n" +
                                      "\r\n" +
                                      "Hello,  world.  \r\n" +
                                      "Bye.\r\n";

        /// <summary>
        /// The message signed by hand with the given c= tag: the header relaxed, the body as c=
        /// says - "simple" when it names one algorithm only (RFC 6376 §3.5).
        /// </summary>
        private static String SignCanonicalized(RSA Key, String Message, String Canonicalization)
        {

            var names         = Canonicalization.Split('/');
            var bodyMethod    = names.Length > 1 ? names[1] : "simple";

            var fields        = new[] { "from", "to", "subject" };
            var (head, body)  = DkimCanonicalization.Split(Message);
            var parsed        = DkimCanonicalization.ParseFields(head);
            var bodyHash      = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(DkimCanonicalization.CanonicalizeBody(body, bodyMethod))));
            var tags          = $"v=1; a=rsa-sha256; c={Canonicalization}; d=client.example; s=test; h={String.Join(':', fields)}; bh={bodyHash}; b=";

            var input         = new StringBuilder();

            foreach (var name in fields)
                input.Append(DkimCanonicalization.CanonicalizeHeader(parsed.Last(field => field.Name.Equals(name, StringComparison.OrdinalIgnoreCase)), "relaxed")).Append("\r\n");

            input.Append(DkimCanonicalization.CanonicalizeHeader(new DkimHeaderField("DKIM-Signature", " " + tags, "DKIM-Signature: " + tags), "relaxed"));

            var signature     = Convert.ToBase64String(Key.SignData(Encoding.UTF8.GetBytes(input.ToString()), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

            return $"DKIM-Signature: {tags}{signature}\r\n{Message}";

        }


        [TestCase("relaxed",          TestName = "c=relaxed hashes the body simple")]
        [TestCase("relaxed/simple",   TestName = "c=relaxed/simple hashes the body simple")]
        [TestCase("relaxed/relaxed",  TestName = "c=relaxed/relaxed hashes the body relaxed")]
        public async Task A_signature_over_a_body_with_white_space_verifies(String Canonicalization)
        {

            using var key = RSA.Create(2048);

            var (result, details) = await Verify(SignCanonicalized(key, Spaced, Canonicalization), Record(key));

            Assert.That(result, Is.EqualTo(DkimResult.Pass), details);

        }


        [TestCase("relaxed",          false, TestName = "White space added to a body signed with c=relaxed breaks the signature")]
        [TestCase("relaxed/simple",   false, TestName = "White space added to a body signed with c=relaxed/simple breaks the signature")]
        [TestCase("relaxed/relaxed",  true,  TestName = "White space added to a body signed with c=relaxed/relaxed keeps the signature")]
        public async Task White_space_added_after_signing(String Canonicalization, Boolean Verifies)
        {

            using var key = RSA.Create(2048);

            // A body with no white space to canonicalize: simple and relaxed hash it the same, until
            // white space is put into it.
            var signed   = SignCanonicalized(key, Letter, Canonicalization);
            var modified = signed.Replace("Hello.\r\n", "Hello.   \r\n");

            Assert.That(modified, Is.Not.EqualTo(signed));

            var (result, details) = await Verify(modified, Record(key));

            if (Verifies)
                Assert.That(result, Is.EqualTo(DkimResult.Pass), details);
            else
                Assert.That(result, Is.Not.EqualTo(DkimResult.Pass), details);

        }


        [TestCase("v=1; a=rsa-sha256; c=relaxed/relaxed; d=other.example; d=client.example; s=test; h={h}; bh={bh}",  false, TestName = "A DKIM-Signature with d= twice is invalid")]
        [TestCase("v=1; v=1; a=rsa-sha256; c=relaxed/relaxed; d=client.example; s=test; h={h}; bh={bh}",              false, TestName = "A DKIM-Signature with v= twice is invalid")]
        [TestCase("v=1; a=rsa-sha256; c=relaxed/relaxed; d=client.example; s=test; s=test; h={h}; bh={bh}",           false, TestName = "A DKIM-Signature with s= twice is invalid")]
        [TestCase("v=1; a=rsa-sha256; c=relaxed/relaxed; d=client.example; D=other.example; s=test; h={h}; bh={bh}",  true,  TestName = "D= is no d= - a tag's name is case-sensitive")]
        public async Task A_tag_named_twice(String Tags, Boolean Verifies)
        {

            using var key = RSA.Create(2048);

            var (result, details) = await Verify(Sign(key, Tags), Record(key));

            if (Verifies)
                Assert.That(result, Is.EqualTo(DkimResult.Pass), details);
            else
                Assert.That(result, Is.Not.EqualTo(DkimResult.Pass), details);

        }


        [TestCase("k=rsa; v=DKIM1; ",           false, TestName = "A key record whose v= is not the first tag is not used")]
        [TestCase("v=DKIM1; k=rsa; k=rsa; ",    false, TestName = "A key record with a tag named twice is not used")]
        [TestCase("v=DKIM2; k=rsa; ",           false, TestName = "A key record of another version is not used")]
        [TestCase("k=rsa; ",                    true,  TestName = "A key record without v= is used")]
        [TestCase("v = DKIM1 ;  k = rsa ; ",    true,  TestName = "A key record with white space around its tags is used")]
        public async Task The_tag_list_of_the_key_record(String TagsBeforeKey, Boolean Verifies)
        {

            using var key = RSA.Create(2048);

            var (result, details) = await Verify(Sign(key, Usual), Record(key, TagsBeforeKey));

            if (Verifies)
                Assert.That(result, Is.EqualTo(DkimResult.Pass), details);
            else
                Assert.That(result, Is.Not.EqualTo(DkimResult.Pass), details);

        }

    }

}
