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
    /// RFC 5321 §4.2.1: "a sender-SMTP MUST be prepared to handle codes not specified in this
    /// document and MUST do so by interpreting the first digit only." And §4.5.3.1.10: a 552 to
    /// RCPT is "too many recipients", to be treated "as a temporary, rather than permanent, failure".
    /// </summary>
    public partial class SMTPOutboundClientTests
    {

        private static Func<String, String?> Answering(String Command, String Reply)
            => line => line.Split(' ')[0].Equals(Command, StringComparison.OrdinalIgnoreCase) ? Reply : null;


        [Test]
        public async Task A_252_to_MAIL_goes_on_to_the_recipients_and_the_message()
        {

            using var nextHop = new NextHop(Answering("MAIL", "252 2.1.0 Sender accepted"));

            var result = await Send(nextHop);

            Assert.That(result.Status,      Is.EqualTo(SendStatus.Success));
            Assert.That(nextHop.Commands,   Has.Some.StartsWith("RCPT TO:<you@next.example>"));
            Assert.That(nextHop.Commands,   Has.Some.EqualTo("DATA"));
            Assert.That(nextHop.DataLines,  Does.Contain("hello"), "a 252 is a 250: the message is sent, not taken as delivered without it");

        }


        [Test]
        public async Task A_252_to_RCPT_accepts_the_recipient()
        {

            using var nextHop = new NextHop(Answering("RCPT", "252 2.1.5 Cannot verify, will attempt delivery"));

            var result = await Send(nextHop);

            Assert.That(result.Status,                                   Is.EqualTo(SendStatus.Success));
            Assert.That(result.Recipients.Select(outcome => outcome.Status), Is.EqualTo(new[] { SendStatus.Success }));
            Assert.That(nextHop.DataLines,                               Does.Contain("hello"));

        }


        [Test]
        public async Task A_350_to_DATA_is_the_go_ahead_for_the_message()
        {

            using var nextHop = new NextHop(Answering("DATA", "350 Send it"));

            var result = await Send(nextHop);

            Assert.That(result.Status,      Is.EqualTo(SendStatus.Success));
            Assert.That(nextHop.DataLines,  Does.Contain("hello"));

        }


        [Test]
        public async Task A_251_to_the_end_of_data_is_a_delivery()
        {

            using var nextHop = new NextHop(Answering(".", "251 2.0.0 Taken, will forward"));

            var result = await Send(nextHop);

            Assert.That(result.Status, Is.EqualTo(SendStatus.Success));

        }


        [TestCase("220")]
        [TestCase("221 2.0.0 Other positive")]
        public async Task Another_2yz_greeting_opens_the_session(String Greeting)
        {

            using var nextHop = new NextHop(Greeting: Greeting);

            var result = await Send(nextHop);

            Assert.That(result.Status,      Is.EqualTo(SendStatus.Success));
            Assert.That(nextHop.DataLines,  Does.Contain("hello"));

        }


        [TestCase("MAIL", "250")]
        [TestCase("RCPT", "250")]
        [TestCase("DATA", "354")]
        [TestCase(".",    "250")]
        public async Task A_code_without_text_is_read(String Command, String Reply)
        {

            using var nextHop = new NextHop(Answering(Command, Reply));

            var result = await Send(nextHop);

            Assert.That(result.Status, Is.EqualTo(SendStatus.Success));

        }


        [Test]
        public async Task A_2yz_to_DATA_is_no_delivery()
        {

            // No 3yz, no message: not a line of it was sent, so it is not delivered either.
            using var nextHop = new NextHop(Answering("DATA", "250 2.0.0 ok"));

            var result = await Send(nextHop);

            Assert.That(result.Status,      Is.EqualTo(SendStatus.TempFail));
            Assert.That(nextHop.DataLines,  Is.Empty);

        }


        [Test]
        public async Task A_3yz_to_MAIL_is_no_delivery()
        {

            using var nextHop = new NextHop(Answering("MAIL", "354 Huh"));

            var result = await Send(nextHop);

            Assert.That(result.Status,      Is.EqualTo(SendStatus.TempFail));
            Assert.That(nextHop.Commands,   Has.None.EqualTo("DATA"));

        }


        [Test]
        public async Task A_5yz_to_MAIL_still_refuses_the_message()
        {

            using var nextHop = new NextHop(Answering("MAIL", "553 5.1.8 Sender refused"));

            var result = await Send(nextHop);

            Assert.That(result.Status,        Is.EqualTo(SendStatus.PermFail));
            Assert.That(result.ResponseCode,  Is.EqualTo(553));

        }


        [TestCase(552, "5.5.3 Too many recipients",  SendStatus.TempFail)]
        [TestCase(552, "Too many recipients",        SendStatus.TempFail)]
        [TestCase(552, "",                           SendStatus.TempFail)]
        [TestCase(552, "5.2.2 Mailbox full",         SendStatus.PermFail)]
        [TestCase(550, "5.1.1 No such user",         SendStatus.PermFail)]
        [TestCase(553, "",                           SendStatus.PermFail)]
        [TestCase(452, "4.5.3 Too many recipients",  SendStatus.TempFail)]
        [TestCase(252, "",                           SendStatus.Success)]
        [TestCase(251, "",                           SendStatus.Success)]
        public void The_answer_to_RCPT_is_read(Int32 Code, String Text, SendStatus Status)
        {
            Assert.That(new RecipientOutcome("you@next.example", Code, Text).Status, Is.EqualTo(Status));
        }


        [Test]
        public async Task A_recipient_refused_with_552_too_many_recipients_is_tried_again_not_bounced()
        {

            using var nextHop = new NextHop(Refusing("later", "552 5.5.3 Too many recipients"));

            var (queue, original) = await Relay(nextHop, "now@next.example", "later@next.example");

            var retry = queue.Mails.Values.SingleOrDefault(mail => mail.Id != original.Id && mail.EnvelopeFrom.Length > 0);

            Assert.That(original.Status,    Is.EqualTo(QueueItemStatus.Delivered));
            Assert.That(retry,              Is.Not.Null, "later@ is to be tried again");
            Assert.That(retry!.EnvelopeTo,  Is.EqualTo(new[] { "later@next.example" }));
            Assert.That(queue.Mails.Values, Has.None.Matches<QueuedMail>(mail => mail is not null && mail.EnvelopeFrom.Length == 0), "nobody is bounced");

        }


        [Test]
        public async Task A_recipient_refused_with_550_is_still_bounced()
        {

            using var nextHop = new NextHop(Refusing("gone", "550 5.1.1 No such user"));

            var (queue, _) = await Relay(nextHop, "here@next.example", "gone@next.example");

            Assert.That(queue.Mails.Values, Has.One.Matches<QueuedMail>(mail => mail is not null && mail.EnvelopeFrom.Length == 0));

        }

    }

}
