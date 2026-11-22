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

using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// <see cref="SmtpPath"/>: Path = "&lt;" [ A-d-l ":" ] Mailbox "&gt;" (RFC 5321 §4.1.2).
    /// </summary>
    [TestFixture]
    public class SmtpPathTests
    {

        [TestCase("FROM:<sender@client.example>",                        "sender@client.example",         "")]
        [TestCase("from:<sender@client.example> SIZE=10 BODY=8BITMIME",  "sender@client.example",         "SIZE=10 BODY=8BITMIME")]
        [TestCase("FROM: <sender@client.example>",                       "sender@client.example",         "")]
        [TestCase("FROM:<>",                                             "",                              "")]
        [TestCase("FROM:<> RET=HDRS",                                    "",                              "RET=HDRS")]
        [TestCase("FROM:<\"a>b\"@client.example> SMTPUTF8",              "\"a>b\"@client.example",        "SMTPUTF8")]
        [TestCase("FROM:<\"a\\\">\"@client.example>",                    "\"a\\\">\"@client.example",     "")]
        [TestCase("FROM:<@one.example,@two.example:joe@three.example>",  "joe@three.example",             "")]
        public void A_path_and_its_parameters(String Argument, String Mailbox, String Parameters)
        {

            Assert.That(SmtpPath.TryParse(Argument, "FROM:", out var mailbox, out var parameters), Is.True);

            Assert.Multiple(() => {
                Assert.That(mailbox,    Is.EqualTo(Mailbox));
                Assert.That(parameters, Is.EqualTo(Parameters));
            });

        }


        [TestCase("FROM:sender@client.example")]
        [TestCase("FROM:<sender@client.example")]
        [TestCase("FROM:<\"sender@client.example>")]
        [TestCase("FROM:<@relay.example>")]
        [TestCase("FROM:<@relay.example;joe@three.example>")]
        [TestCase("TO:<sender@client.example>")]
        [TestCase("XFROM:<sender@client.example>")]
        public void What_is_no_path(String Argument)

            => Assert.That(SmtpPath.TryParse(Argument, "FROM:", out _, out _), Is.False);


        [TestCase("alice@hermod.test",              "hermod.test",         "alice")]
        [TestCase("\"alice@home\"@hermod.test",     "hermod.test",         "\"alice@home\"")]
        [TestCase("\"a\\\"@b\"@hermod.test",        "hermod.test",         "\"a\\\"@b\"")]
        [TestCase("alice@[IPv6:2001:db8::1]",       "[IPv6:2001:db8::1]",  "alice")]
        [TestCase("postmaster",                     "",                    "postmaster")]
        [TestCase("\"unclosed@hermod.test",         "",                    "\"unclosed@hermod.test")]
        public void The_domain_follows_the_local_part(String Mailbox, String Domain, String LocalPart)
        {

            Assert.Multiple(() => {
                Assert.That(SmtpPath.DomainOf   (Mailbox), Is.EqualTo(Domain));
                Assert.That(SmtpPath.LocalPartOf(Mailbox), Is.EqualTo(LocalPart));
            });

        }

    }

}
