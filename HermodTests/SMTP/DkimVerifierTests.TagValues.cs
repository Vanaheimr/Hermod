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
    /// The values of the optional tags of a DKIM-Signature - t=, x=, l= and q= (RFC 6376 §3.5) -
    /// and §6.1.1: "any inconsistency or unexpected values MUST cause the header field to be
    /// completely ignored and the Verifier to return PERMFAIL (signature syntax error)". Every
    /// signature is a valid one over what it says, made by hand. The findings V-33 to V-35 of
    /// SMTPConformanceTests.
    /// </summary>
    public partial class DkimVerifierTests
    {

        private const String Timestamp = "1795000000";

        private static async Task<(DkimResult Result, String? Details)> VerifyWith(String MoreTags)
        {
            using var key = RSA.Create(2048);
            return await Verify(Sign(key, Usual + MoreTags), Record(key));
        }

        private static String BodyHashOf(String CanonicalizedBody)
            => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalizedBody)));


        #region V-33: t= and x=

        [TestCase("; t=yesterday",                                TestName = "A t= that is no number is a permanent error")]
        [TestCase("; t=",                                         TestName = "A t= without a value is a permanent error")]
        [TestCase("; t=-5",                                       TestName = "A negative t= is a permanent error")]
        [TestCase("; x=tomorrow",                                 TestName = "An x= that is no number is a permanent error")]
        [TestCase("; t=" + Timestamp + "; x=" + Timestamp,        TestName = "An x= the same as t= is a permanent error")]
        [TestCase("; t=" + Timestamp + "; x=1794996400",          TestName = "An x= before t= is a permanent error")]
        [TestCase("; x=1794996400; t=" + Timestamp,               TestName = "An x= before t= is a permanent error, x= first")]
        public async Task A_t_or_x_of_an_unexpected_value_is_a_permanent_error(String MoreTags)
        {
            var (result, details) = await VerifyWith(MoreTags);
            Assert.That(result, Is.EqualTo(DkimResult.PermError), details);
        }


        [TestCase("; t=" + Timestamp,                             TestName = "A signature with a t= passes")]
        [TestCase("; t=" + Timestamp + "; x=1795086400",          TestName = "A signature whose x= is after its t= passes")]
        [TestCase("; x=1795086400",                               TestName = "A signature with an x= and no t= passes")]
        [TestCase("; t=9999999999999",                            TestName = "A t= of 13 digits is taken for infinite and passes")]
        public async Task A_t_or_x_of_an_expected_value_passes(String MoreTags)
        {
            var (result, details) = await VerifyWith(MoreTags);
            Assert.That(result, Is.EqualTo(DkimResult.Pass), details);
        }

        #endregion

        #region V-34: l=

        [TestCase("; l=abc",                                      TestName = "An l= that is no number is a permanent error")]
        [TestCase("; l=-1",                                       TestName = "A negative l= is a permanent error")]
        [TestCase("; l=",                                         TestName = "An l= without a value is a permanent error")]
        [TestCase("; l=9",                                        TestName = "An l= one octet larger than the body is a permanent error")]
        [TestCase("; l=1000",                                     TestName = "An l= much larger than the body is a permanent error")]
        [TestCase("; l=11111111111111111111111111111111111111111111111111111111111111111111111111111",
                                                                  TestName = "An l= of 77 digits is a permanent error")]
        public async Task An_l_of_an_unexpected_value_is_a_permanent_error(String MoreTags)
        {
            var (result, details) = await VerifyWith(MoreTags);
            Assert.That(result, Is.EqualTo(DkimResult.PermError), details);
        }


        [Test(Description = "RFC 6376 §3.5, §3.7: an l= of the length of the canonicalized body - \"Hello.\" and CRLF, 8 octets - passes")]
        public async Task An_l_of_the_length_of_the_body_passes()
        {
            var (result, details) = await VerifyWith("; l=8");
            Assert.That(result, Is.EqualTo(DkimResult.Pass), details);
        }


        [Test(Description = "RFC 6376 §3.7: the body hash is over the first l= octets - l=4 with the hash of \"Hell\" matches - and §6.1.3: the octets after them are unsigned content, and the signature is not taken")]
        public async Task An_l_shorter_than_the_body_leaves_unsigned_content()
        {

            using var key = RSA.Create(2048);

            var tags              = "v=1; a=rsa-sha256; c=relaxed/relaxed; d=client.example; s=test; h={h}; l=4; bh=" + BodyHashOf("Hell");
            var (result, details) = await Verify(Sign(key, tags), Record(key));

            Assert.Multiple(() => {
                Assert.That(result,  Is.EqualTo(DkimResult.Fail), details);
                Assert.That(details, Does.Contain("unsigned content"));
            });

        }


        [Test(Description = "RFC 6376 §3.7: the body hash is over the first l= octets - l=4 with the hash of the whole body does not match")]
        public async Task An_l_shorter_than_the_body_with_the_hash_of_the_whole_body_fails()
        {

            using var key = RSA.Create(2048);

            var (result, details) = await Verify(Sign(key, Usual + "; l=4"), Record(key));

            Assert.Multiple(() => {
                Assert.That(result,  Is.EqualTo(DkimResult.Fail), details);
                Assert.That(details, Does.Contain("Body hash mismatch"));
            });

        }

        #endregion

        #region V-35: q=

        [TestCase("; q=",                                         TestName = "A q= without a value is a permanent error")]
        [TestCase("; q=dns",                                      TestName = "A q=dns without the option txt is a permanent error")]
        [TestCase("; q=dns/srv",                                  TestName = "A q=dns with another option is a permanent error")]
        [TestCase("; q=foo/bar",                                  TestName = "A q= of an unknown method alone is a permanent error")]
        [TestCase("; q=dns/txt:",                                 TestName = "A q= with an empty method is a permanent error")]
        public async Task A_q_without_a_query_method_the_verifier_knows_is_a_permanent_error(String MoreTags)
        {
            var (result, details) = await VerifyWith(MoreTags);
            Assert.That(result, Is.EqualTo(DkimResult.PermError), details);
        }


        [TestCase("; q=dns/txt",                                  TestName = "A q=dns/txt passes")]
        [TestCase("; q=DNS/TXT",                                  TestName = "A q=DNS/TXT passes")]
        [TestCase("; q=foo/bar:dns/txt",                          TestName = "A q= with an unknown method before dns/txt passes")]
        [TestCase("; q=dns/txt : foo",                            TestName = "A q= with white space around its colon passes")]
        public async Task A_q_with_dns_txt_passes(String MoreTags)
        {
            var (result, details) = await VerifyWith(MoreTags);
            Assert.That(result, Is.EqualTo(DkimResult.Pass), details);
        }

        #endregion

    }

}
