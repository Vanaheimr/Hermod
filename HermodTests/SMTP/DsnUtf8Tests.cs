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
using org.GraphDefined.Vanaheimr.Hermod.SMTP.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// DSN and UTF-8 (RFC 6533): the UTF-8 address type in ORCPT, received (§3) and written,
    /// the ORCPT as xtext (RFC 3461 §4.2), and the report of an SMTPUTF8 message (§4).
    /// </summary>
    [TestFixture]
    public class DsnUtf8Tests
    {

        #region ORCPT received

        private static Int32 Rcpt(String Parameters, Boolean Smtputf8)
        {
            var (parsed, error) = ESMTPParameters.Parse(Parameters);
            return (error ?? ESMTPParameters.ValidateRcpt(parsed, ExtendedSmtp: true, Smtputf8: Smtputf8))?.Code ?? 250;
        }


        [TestCase("ORCPT=utf-8;jörg@hermod.test",            true,   250)]
        [TestCase("ORCPT=UTF-8;jörg@hermod.test",            true,   250)]
        [TestCase("ORCPT=utf-8;j\\x{F6}rg@hermod.test",      true,   250)]
        [TestCase("ORCPT=utf-8;j\\x{F6}rg@hermod.test",      false,  250)]
        [TestCase("ORCPT=utf-8;jörg@hermod.test",            false,  501)]
        [TestCase("ORCPT=rfc822;jörg@hermod.test",           true,   501)]
        [TestCase("ORCPT=utf-8;jörg+tag@hermod.test",        true,   501)]
        [TestCase("ORCPT=utf-8;jörg\\x{2B}tag@hermod.test",  true,   250)]
        [Test(Description = "RFC 6533 §3: a server that advertises SMTPUTF8 and DSN \"MUST accept a UTF-8 address type in the ORCPT parameter including 8-bit UTF-8 characters\" - utf-8-addr-unitext in an SMTPUTF8 transaction, utf-8-addr-xtext in any; \"+\" only as \\x{2B}, and UTF-8 never in another address type")]
        public void An_ORCPT_of_the_UTF_8_address_type(String Parameters, Boolean Smtputf8, Int32 Code)

            => Assert.That(Rcpt(Parameters, Smtputf8), Is.EqualTo(Code));

        #endregion


        #region ORCPT written

        [TestCase("bob@elsewhere.example",       false, "rfc822;bob@elsewhere.example")]
        [TestCase("you+tag@sink.example",        false, "rfc822;you+2Btag@sink.example")]
        [TestCase("a=b@sink.example",            false, "rfc822;a+3Db@sink.example")]
        [TestCase("\"john doe\"@sink.example",   false, "rfc822;\"john+20doe\"@sink.example")]
        [TestCase("jörg@sink.example",           false, "utf-8;j\\x{F6}rg@sink.example")]
        [TestCase("jörg@sink.example",           true,  "utf-8;jörg@sink.example")]
        [TestCase("jörg+tag@sink.example",       true,  "utf-8;jörg\\x{2B}tag@sink.example")]
        [TestCase("用户@例子.广告",                false, "utf-8;\\x{7528}\\x{6237}@\\x{4F8B}\\x{5B50}.\\x{5E7F}\\x{544A}")]
        [Test(Description = "RFC 3461 §4.2: original-recipient-address = addr-type \";\" xtext, \"+\" and \"=\" as \"+2B\" and \"+3D\"; RFC 6533 §3: an address in UTF-8 is of the UTF-8 address type, with \"\\x{HEXPOINT}\" for what QCHAR is not - and for UTF-8 too, unless the transaction is SMTPUTF8")]
        public void The_ORCPT_of_an_address(String Address, Boolean Smtputf8, String Orcpt)

            => Assert.That(DsnCommands.OriginalRecipientOf(Address, Smtputf8), Is.EqualTo(Orcpt));


        [Test(Description = "RFC 3461 §5.2.1 (d): a received ORCPT goes on with \"the same value\"; RFC 6533 §3: its UTF-8 as \"\\x{HEXPOINT}\" to a transaction that is not SMTPUTF8 - and a recipient without one gets an ORCPT in xtext")]
        public void A_relayed_ORCPT()
        {

            var utf8    = new RecipientDsn { Recipient = "alice@elsewhere.example", OriginalRecipient = "utf-8;jörg@elsewhere.example" };
            var ascii   = new RecipientDsn { Recipient = "alice@elsewhere.example", OriginalRecipient = "rfc822;team+2Bsales@elsewhere.example" };
            var none    = new RecipientDsn { Recipient = "you+tag@elsewhere.example" };

            Assert.Multiple(() => {
                Assert.That(DsnCommands.RcptToParams(utf8,  remoteSupportsDsn: true, smtpUtf8: true),   Is.EqualTo(" ORCPT=utf-8;jörg@elsewhere.example"));
                Assert.That(DsnCommands.RcptToParams(utf8,  remoteSupportsDsn: true, smtpUtf8: false),  Is.EqualTo(" ORCPT=utf-8;j\\x{F6}rg@elsewhere.example"));
                Assert.That(DsnCommands.RcptToParams(ascii, remoteSupportsDsn: true),                   Is.EqualTo(" ORCPT=rfc822;team+2Bsales@elsewhere.example"));
                Assert.That(DsnCommands.RcptToParams(none,  remoteSupportsDsn: true),                   Is.EqualTo(" ORCPT=rfc822;you+2Btag@elsewhere.example"));
                Assert.That(DsnCommands.RcptTo("you+tag@elsewhere.example", new DsnParameters(DsnNotify.Failure, null, null), remoteSupportsDsn: true),
                            Is.EqualTo("RCPT TO:<you+tag@elsewhere.example> NOTIFY=FAILURE ORCPT=rfc822;you+2Btag@elsewhere.example"));
            });

        }

        #endregion


        #region The report of an SMTPUTF8 message

        private static String Report(String Recipient, String Message, DsnRet? Ret = null)

            => new DsnGenerator(new SMTPServerConfig { Hostname = "relay.hermod.test" }).
                   Generate("sender@client.example", null, Ret, DateTimeOffset.UtcNow, Message,
                            [ new DsnRecipient(Recipient, DsnAction.Failed, "5.1.1", DiagnosticCode: "smtp; 550 5.1.1 No such user") ]);


        [Test(Description = "RFC 6533 §4.1: a status with UTF-8 in it is message/global-delivery-status, with the UTF-8 address type for the address (\"SHOULD use the utf-8-address form\"); RFC 6522 §3: report-type \"is the MIME subtype of the second body part\"; RFC 6532 §3.7: the message with UTF-8 header values is returned as message/global")]
        public void The_report_of_a_UTF_8_recipient_is_global()
        {

            var report = Report("jörg@outbound.test", "Received: from a by b for <jörg@outbound.test>; Sun, 22 Nov 2026 06:00:00 +0000\r\nSubject: test\r\n\r\nbody\r\n");

            Assert.Multiple(() => {
                Assert.That(report, Does.Contain("Content-Type: multipart/report; report-type=global-delivery-status;"));
                Assert.That(report, Does.Contain("\r\nContent-Type: message/global-delivery-status\r\nContent-Transfer-Encoding: 8bit\r\n"));
                Assert.That(report, Does.Contain("\r\nFinal-Recipient: utf-8; jörg@outbound.test\r\n"));
                Assert.That(report, Does.Contain("\r\nContent-Type: message/global\r\nContent-Transfer-Encoding: 8bit\r\n"));
                Assert.That(report, Does.Not.Contain("message/delivery-status").And.Not.Contain("message/rfc822"));
            });

        }


        [Test(Description = "RFC 6533 §4: the header of a message with UTF-8 header values, returned alone (RET=HDRS), is message/global-headers")]
        public void The_header_of_a_UTF_8_message_is_message_global_headers()
        {

            var report = Report("bob@outbound.test", "Subject: Grüße\r\n\r\nbody\r\n", DsnRet.Hdrs);

            Assert.Multiple(() => {
                Assert.That(report, Does.Contain("report-type=delivery-status;"), "an ASCII status");
                Assert.That(report, Does.Contain("\r\nContent-Type: message/delivery-status\r\n"));
                Assert.That(report, Does.Contain("\r\nContent-Type: message/global-headers\r\nContent-Transfer-Encoding: 8bit\r\n"));
            });

        }


        [Test(Description = "RFC 3464: the report of an ASCII message stays as it was - message/delivery-status, message/rfc822, rfc822 addresses")]
        public void The_report_of_an_ASCII_message_is_as_before()
        {

            var report = Report("bob@outbound.test", "Subject: test\r\n\r\nGrüße im Text\r\n");

            Assert.Multiple(() => {
                Assert.That(report, Does.Contain("report-type=delivery-status;"));
                Assert.That(report, Does.Contain("\r\nContent-Type: message/delivery-status\r\n\r\n"));
                Assert.That(report, Does.Contain("\r\nFinal-Recipient: rfc822; bob@outbound.test\r\n"));
                Assert.That(report, Does.Contain("\r\nContent-Type: message/rfc822\r\nContent-Transfer-Encoding: 8bit\r\n"), "an 8-bit body, an ASCII header");
            });

        }

        #endregion

    }

}
