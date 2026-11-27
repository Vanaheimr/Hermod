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
    /// White space in ARC (RFC 8617 §4.1.2: the AMS "is syntactically and semantically identical
    /// to a DKIM-Signature header field"): the c= of an AMS read as RFC 6376 §3.5 reads it, white
    /// space in its bh=, and the sealer's relaxed canonicalization deleting WSP and nothing else.
    /// The findings A-6 and P-4 of SMTPConformanceTests.
    /// </summary>
    public partial class ArcTests
    {

        /// <summary>
        /// The message with a body that the two canonicalizations make differ: two spaces in a
        /// line, two at its end.
        /// </summary>
        private static String WithWhiteSpace()
            => Message().Replace("Hello through a list.\r\n", "Hello  through a list.  \r\nBye.\r\n");

        private static String FoldedBh(String Tags)
            => Regex.Replace(Tags, "bh=(.{20})(.+)$", "bh=$1\r\n\t$2");


        #region A-6: the AMS read as a DKIM-Signature

        [Test(Description = "RFC 6376 §3.5: \"If no canonicalization algorithm is specified, the Signer MUST use \"simple/simple\"\" - an AMS without c=, made simple/simple over a body with white space, passes")]
        public async Task An_AMS_without_c_is_simple_simple()
            => Assert.That(await Validate(HandSeal(WithWhiteSpace(), 1, "none", AmsCanonicalization: null)), Is.EqualTo(ArcResult.Pass));

        [Test(Description = "RFC 6376 §3.5: \"c=relaxed\" is treated the same as \"c=relaxed/simple\"\" - an AMS with c=relaxed, its body hashed simple over a body with white space, passes")]
        public async Task An_AMS_with_c_relaxed_hashes_the_body_simple()
            => Assert.That(await Validate(HandSeal(WithWhiteSpace(), 1, "none", AmsCanonicalization: "relaxed")), Is.EqualTo(ArcResult.Pass));

        [Test(Description = "RFC 6376 §3.5: the body of an AMS with c=relaxed is hashed simple - white space put into it after sealing fails the chain")]
        public async Task White_space_added_to_a_body_sealed_with_c_relaxed_fails()
        {

            var sealed_  = HandSeal(Message(), 1, "none", AmsCanonicalization: "relaxed");
            var modified = sealed_.Replace("Hello through a list.\r\n", "Hello  through a list.\r\n");

            Assert.That(modified, Is.Not.EqualTo(sealed_));
            Assert.That(await Validate(modified), Is.EqualTo(ArcResult.Fail));

        }

        [Test(Description = "RFC 6376 §3.5, bh=: \"Whitespace is ignored in this value\" - an AMS with its bh= folded passes")]
        public async Task An_AMS_with_bh_folded_passes()
            => Assert.That(await Validate(HandSeal(Message(), 1, "none", AmsTags: FoldedBh)), Is.EqualTo(ArcResult.Pass));

        [Test(Description = "RFC 6376 §3.5: an AMS with its bh= folded, of another body than the message has, fails (a guard)")]
        public async Task An_AMS_with_bh_folded_over_another_body_fails()
        {

            var sealed_  = HandSeal(Message(), 1, "none", AmsTags: FoldedBh);
            var modified = sealed_.Replace("Hello through a list.\r\n", "Hallo through a list.\r\n");

            Assert.That(modified, Is.Not.EqualTo(sealed_));
            Assert.That(await Validate(modified), Is.EqualTo(ArcResult.Fail));

        }

        [TestCase("relaxed/simple",   TestName = "An AMS with c=relaxed/simple passes")]
        [TestCase("simple/simple",    TestName = "An AMS with c=simple/simple passes")]
        [TestCase("relaxed/relaxed",  TestName = "An AMS with c=relaxed/relaxed passes")]
        public async Task An_AMS_with_both_canonicalizations_named_passes(String Canonicalization)
            => Assert.That(await Validate(HandSeal(WithWhiteSpace(), 1, "none", AmsCanonicalization: Canonicalization)), Is.EqualTo(ArcResult.Pass));

        #endregion

        #region P-4: the sealer deletes WSP, and nothing else

        [TestCase("Page 1\f",             TestName = "The sealer keeps a form feed at the end of a line")]
        [TestCase("Total: 10 EUR ",  TestName = "The sealer keeps a NO-BREAK SPACE at the end of a line")]
        public async Task The_sealer_keeps_what_is_no_WSP_at_the_end_of_a_line(String Line)
        {

            var message = Message().Replace("Hello through a list.\r\n", $"Hello,\r\n{Line}\r\nBye.\r\n");
            var sealed_ = Sealer().Seal(message, "forwarder.example", "spf=pass smtp.mailfrom=origin.example", ArcResult.None);

            var ams     = Fields(sealed_, AMS).Single().RawValue.Replace("\r\n", "");
            var bh      = Regex.Replace(Regex.Match(ams, @"(?:^|;)\s*bh=([^;]*)").Groups[1].Value, @"\s+", "");

            var result  = await Validate(sealed_);

            Assert.Multiple(() => {
                Assert.That(bh, Is.EqualTo(Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes($"Hello,\r\n{Line}\r\nBye.\r\n")))), sealed_);
                Assert.That(result, Is.EqualTo(ArcResult.Pass), sealed_);
            });

        }

        #endregion

    }

}
