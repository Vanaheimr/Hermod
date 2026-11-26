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
    /// RFC 3461 §9.2: "For a single-line SMTP reply to an SMTP command, the diagnostic-code
    /// SHOULD be an exact transcription of the reply. For multi-line SMTP replies, it is necessary
    /// to insert a SPACE before each line after the first" - so the result of an attempt keeps a
    /// reply of the next hop as it was received, every line with its reply code, for the report
    /// that transcribes it. Finding D-16 of SMTPConformanceTests.
    /// </summary>
    public partial class SMTPOutboundClientTests
    {

        private const String RefusedTwice  = "550-5.1.1 mailbox unavailable\r\n550 5.1.1 user has moved with no forwarding address";


        [Test]
        public async Task A_multi_line_refusal_of_a_recipient_is_kept_line_by_line()
        {

            using var nextHop = new NextHop(Answering("RCPT", RefusedTwice));

            var result  = await Send(nextHop);
            var outcome = result.Recipients.Single();

            Assert.Multiple(() => {
                Assert.That(result.Status,    Is.EqualTo(SendStatus.PermFail));
                Assert.That(outcome.Code,     Is.EqualTo(550));
                Assert.That(outcome.Reply,    Is.EqualTo(RefusedTwice),                                           "the reply as it was received");
                Assert.That(outcome.Text,     Is.EqualTo("5.1.1 mailbox unavailable 5.1.1 user has moved with no forwarding address"), "the text, as before");
                Assert.That(outcome.Status,   Is.EqualTo(SendStatus.PermFail));
            });

        }


        [TestCase("MAIL", "550-5.7.1 Sender refused\r\n550 5.7.1 see https://next.example/why")]
        [TestCase(".",    "554-5.7.1 Message refused\r\n554-5.7.1 it looks like spam to us\r\n554 5.7.1 see https://next.example/why")]
        [TestCase(".",    "451-4.3.0 Try again\r\n451 4.3.0 later")]
        public async Task A_multi_line_refusal_of_the_message_is_kept_line_by_line(String Command, String Reply)
        {

            using var nextHop = new NextHop(Answering(Command, Reply));

            var result = await Send(nextHop);

            Assert.Multiple(() => {
                Assert.That(result.Status,        Is.Not.EqualTo(SendStatus.Success));
                Assert.That(result.ResponseCode,  Is.EqualTo(Int32.Parse(Reply[..3])));
                Assert.That(result.ResponseText,  Is.EqualTo(Reply));
            });

        }


        [Test]
        public async Task A_multi_line_reply_to_the_end_of_data_that_takes_the_message_is_kept_line_by_line()
        {

            using var nextHop = new NextHop(Answering(".", "250-2.0.0 Ok\r\n250 2.0.0 queued as 4711"));

            var result = await Send(nextHop);

            Assert.Multiple(() => {
                Assert.That(result.Status,        Is.EqualTo(SendStatus.Success));
                Assert.That(result.ResponseText,  Is.EqualTo("250-2.0.0 Ok\r\n250 2.0.0 queued as 4711"));
            });

        }


        [Test]
        public async Task A_single_line_reply_is_kept_as_it_was()
        {

            using var nextHop = new NextHop(Answering("RCPT", "550 5.1.1 <you@next.example>: Recipient address rejected"));

            var result = await Send(nextHop);

            Assert.That(result.Recipients.Single().Reply, Is.EqualTo("550 5.1.1 <you@next.example>: Recipient address rejected"));

        }

    }

}
