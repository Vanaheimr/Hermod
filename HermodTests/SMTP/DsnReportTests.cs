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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// The reports a relay owes a sender (RFC 3461 §5.2, §6) and their format (RFC 3464, RFC 6522):
    /// what BounceHandler queues for a message that failed, waits, or was relayed. The findings
    /// D-1 to D-12 of SMTPConformanceTests.
    /// </summary>
    [TestFixture]
    public class DsnReportTests
    {

        #region Setup

        private sealed class NullLogger : ILogger
        {
            public void Log(LogLevel level, String message) { }
        }

        private sealed class ReportQueue : IMailQueue
        {
            public readonly List<QueuedMail> Enqueued = [];
            public Task EnqueueAsync(QueuedMail mail, CancellationToken ct = default) { Enqueued.Add(mail); return Task.CompletedTask; }
            public Task<IReadOnlyList<QueuedMail>> GetPendingAsync(Int32 maxItems = 50, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<QueuedMail>>([]);
            public Task<QueuedMail?> GetByIdAsync(String id, CancellationToken ct = default) => Task.FromResult<QueuedMail?>(null);
            public Task UpdateAsync(QueuedMail mail, CancellationToken ct = default) => Task.CompletedTask;
            public Task RemoveAsync(String id, CancellationToken ct = default) => Task.CompletedTask;
            public Task<Int32> GetQueueLengthAsync(CancellationToken ct = default) => Task.FromResult(Enqueued.Count);
            public Task<IReadOnlyList<QueuedMail>> GetFailedAsync(Int32 maxItems = 100, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<QueuedMail>>([]);
            public ChannelReader<QueuedMail> NewMailReader { get; } = Channel.CreateUnbounded<QueuedMail>().Reader;
            public void SignalRetryCheck() { }
            public ChannelReader<Boolean> RetryCheckReader { get; } = Channel.CreateUnbounded<Boolean>().Reader;
        }

        private const String Message = "From: <sender@client.example>\r\nTo: <gone@elsewhere.example>\r\nSubject: hello\r\n\r\nThe body of the message.\r\n";

        /// <summary>
        /// A relayed message as the server queues it: one recipient, with the DSN request it came with.
        /// </summary>
        private static QueuedMail Relayed(DsnNotify?  Notify    = null,
                                          String?     Orcpt     = null,
                                          String?     EnvId     = null,
                                          DsnRet?     Ret       = null,
                                          String      Content   = Message,
                                          params String[] To)

            => new () {
                   Id             = $"test-{Guid.NewGuid():N}",
                   EnvelopeFrom   = "sender@client.example",
                   EnvelopeTo     = To.Length > 0 ? To : [ "gone@elsewhere.example" ],
                   MessageContent = Content,
                   TargetDomain   = "elsewhere.example",
                   EnvId          = EnvId,
                   Ret            = Ret,
                   RecipientDsns  = [.. (To.Length > 0 ? To : [ "gone@elsewhere.example" ]).
                                            Select(to => new RecipientDsn { Recipient = to, Notify = Notify, OriginalRecipient = Orcpt })]
               };

        private static SendResult Refused(String Reply = "550 5.1.1 No such user", String Recipient = "gone@elsewhere.example")

            => SendResult.PermFail(Int32.Parse(Reply[..3]), $"No recipient accepted: {Reply[4..]}", "mx.elsewhere.example")
                   with { Recipients = [ new RecipientOutcome(Recipient, Int32.Parse(Reply[..3]), Reply[4..]) ] };

        private static async Task<List<QueuedMail>> Bounce(QueuedMail Mail, SendResult? Result = null)
        {
            var queue = new ReportQueue();
            await new BounceHandler(new SMTPServerConfig { Hostname = "relay.example" }, queue, new NullLogger()).SendBounceAsync(Mail, Result ?? Refused());
            return queue.Enqueued;
        }

        #endregion


        [Test(Description = "D-1 - RFC 5322 §2.3: CR LF line ends only, also around the returned message")]
        public async Task A_bounce_has_CR_LF_line_ends_only()
        {

            var bounce = (await Bounce(Relayed())).Single().MessageContent;

            Assert.Multiple(() => {
                Assert.That(Regex.Count(bounce, "\r(?!\n)"),  Is.Zero, "CRs without LF");
                Assert.That(Regex.Count(bounce, "(?<!\r)\n"), Is.Zero, "LFs without CR");
            });

        }


        [Test(Description = "D-2 - RFC 5322 §3.6.4, RFC 2046 §5.1.1: a Message-ID of its own, and a boundary of bchars")]
        public async Task Every_bounce_has_its_own_valid_Message_ID_and_boundary()
        {

            var bounces    = (await Bounce(Relayed())).Concat(await Bounce(Relayed())).Select(mail => mail.MessageContent).ToArray();
            var messageIds = bounces.Select(bounce => Regex.Match(bounce, @"^Message-ID: (.*)\r$", RegexOptions.Multiline).Groups[1].Value).ToArray();
            var boundaries = bounces.Select(bounce => Regex.Match(bounce, "boundary=\"([^\"]*)\"").Groups[1].Value).ToArray();

            Assert.Multiple(() => {
                Assert.That(messageIds, Is.Unique);
                Assert.That(messageIds, Has.All.Matches(@"^<[A-Za-z0-9.-]+@relay\.example>$"));
                Assert.That(boundaries, Has.All.Matches(@"^[0-9A-Za-z'()+_,./:=?-][0-9A-Za-z'()+_,./:=? -]{0,68}(?<! )$"));
            });

        }


        [TestCase(DsnNotify.Never)]
        [TestCase(DsnNotify.Success)]
        [TestCase(DsnNotify.Delay)]
        [TestCase(DsnNotify.Success | DsnNotify.Delay)]
        [Description("D-3 - RFC 3461 §5.2.6 (b): NOTIFY without FAILURE, no bounce")]
        public async Task No_bounce_when_NOTIFY_leaves_out_FAILURE(DsnNotify Notify)
        {
            Assert.That(await Bounce(Relayed(Notify)), Is.Empty);
        }


        [TestCase(null)]
        [TestCase(DsnNotify.Failure)]
        [TestCase(DsnNotify.Success | DsnNotify.Failure)]
        [Description("D-3 - RFC 3461 §5.2.6 (a), (c): NOTIFY with FAILURE, or none, a bounce")]
        public async Task A_bounce_when_NOTIFY_asks_or_is_absent(DsnNotify? Notify)
        {
            Assert.That(await Bounce(Relayed(Notify)), Has.Count.EqualTo(1));
        }


        [TestCase("This is about multipart/report and message/delivery-status.")]
        [TestCase("Auto-Submitted: auto-replied")]
        [TestCase("From: MAILER-DAEMON@example.org")]
        [Description("D-4 - RFC 3461 §5.2.6 (c), RFC 5321 §6.1: what a message says about itself does not stop its bounce")]
        public async Task A_bounce_whatever_the_message_says(String BodyLine)
        {
            Assert.That(await Bounce(Relayed(Content: $"Subject: hello\r\n\r\n{BodyLine}\r\n")), Has.Count.EqualTo(1));
        }


        [Test(Description = "D-4 - RFC 3461 §6 NOTE: no report on a message with a null reverse-path - that, and only that, keeps reports from looping")]
        public async Task No_bounce_for_a_null_reverse_path()
        {

            var mail = Relayed();
            mail = new QueuedMail { Id = mail.Id, EnvelopeFrom = "", EnvelopeTo = mail.EnvelopeTo, MessageContent = mail.MessageContent, TargetDomain = mail.TargetDomain };

            Assert.That(await Bounce(mail), Is.Empty);

        }


        [Test(Description = "D-5 - RFC 3461 §5.2.6: a message given up on after its retries has failed - Action: failed, even when the last answer was 4xx")]
        public async Task A_message_given_up_on_is_reported_as_failed()
        {

            var bounce = (await Bounce(Relayed(), Refused("451 4.3.0 Try again later") with { Status = SendStatus.TempFail })).Single().MessageContent;

            Assert.Multiple(() => {
                Assert.That(bounce, Does.Contain("\r\nAction: failed\r\n"));
                Assert.That(bounce, Does.Not.Contain("Action: delayed"));
                Assert.That(bounce, Does.Not.Contain("retried"));
            });

        }


        [TestCase("550 5.7.1 Relaying denied",  "5.7.1")]
        [TestCase("552 5.2.2 Mailbox full",     "5.2.2")]
        [TestCase("550 No such user",           "5.0.0")]
        [TestCase("550 4.2.2 Wrong class",      "5.0.0")]
        [Description("D-6 - RFC 3461 §6.3 (g), (i): the status the reply carries, else X.0.0 of its class; the reply as it was")]
        public async Task A_bounce_carries_the_status_and_reply_of_the_next_hop(String Reply, String Status)
        {

            var bounce = (await Bounce(Relayed(), Refused(Reply))).Single().MessageContent;

            Assert.Multiple(() => {
                Assert.That(bounce, Does.Contain($"\r\nStatus: {Status}\r\n"));
                Assert.That(bounce, Does.Contain($"\r\nDiagnostic-Code: smtp; {Reply}\r\n"));
                Assert.That(bounce, Does.Contain("\r\nRemote-MTA: dns; mx.elsewhere.example\r\n"));
            });

        }


        [Test(Description = "D-6 - a refusal at the end of data, whose text starts with its code, is quoted once")]
        public async Task A_reply_is_quoted_once()
        {

            var bounce = (await Bounce(Relayed(), SendResult.PermFail(554, "554 5.6.0 Content rejected", "mx.elsewhere.example"))).Single().MessageContent;

            Assert.That(bounce, Does.Contain("\r\nDiagnostic-Code: smtp; 554 5.6.0 Content rejected\r\n"));

        }


        [Test(Description = "D-7 - RFC 3461 §6.3 (d): Original-Recipient from ORCPT, xtext decoded")]
        public async Task Original_Recipient_is_the_ORCPT()
        {

            var bounce = (await Bounce(Relayed(Orcpt: "rfc822;Alias+2Bone@Client.example"))).Single().MessageContent;

            Assert.That(bounce, Does.Contain("\r\nOriginal-Recipient: rfc822; Alias+one@Client.example\r\n"));

        }


        [Test(Description = "D-7 - RFC 3461 §6.3 (d): without ORCPT, no Original-Recipient")]
        public async Task Without_ORCPT_no_Original_Recipient()
        {
            Assert.That((await Bounce(Relayed())).Single().MessageContent, Does.Not.Contain("Original-Recipient:"));
        }


        [Test(Description = "D-8 - RFC 3461 §6.3 (a): Original-Envelope-Id from ENVID, xtext decoded - and only with ENVID")]
        public async Task Original_Envelope_Id_is_the_ENVID()
        {

            Assert.Multiple(async () => {
                Assert.That((await Bounce(Relayed(EnvId: "QQ+2B314159"))).Single().MessageContent, Does.Contain("\r\nOriginal-Envelope-Id: QQ+314159\r\n"));
                Assert.That((await Bounce(Relayed())).Single().MessageContent,                     Does.Not.Contain("Original-Envelope-Id:"));
            });

        }


        [Test(Description = "D-9 - RFC 3461 §4.3: RET=HDRS returns the header only; RET=FULL and no RET the whole message")]
        public async Task RET_decides_what_is_returned()
        {

            var headers = (await Bounce(Relayed(Ret: DsnRet.Hdrs))).Single().MessageContent;
            var full    = (await Bounce(Relayed(Ret: DsnRet.Full))).Single().MessageContent;
            var none    = (await Bounce(Relayed())).Single().MessageContent;

            Assert.Multiple(() => {
                Assert.That(headers, Does.Contain("Content-Type: text/rfc822-headers").And.Not.Contain("The body of the message."));
                Assert.That(full,    Does.Contain("Content-Type: message/rfc822").And.Contain("The body of the message."));
                Assert.That(none,    Does.Contain("Content-Type: message/rfc822").And.Contain("The body of the message."));
            });

        }


        [Test(Description = "D-10 - RFC 3461 §5.2.5, §6.2: a delay is reported as a DSN, when the caller decides it is time")]
        public async Task A_delay_is_a_delayed_DSN()
        {

            var queue = new ReportQueue();
            await new BounceHandler(new SMTPServerConfig { Hostname = "relay.example" }, queue, new NullLogger())
                      .SendDelayNotificationAsync(Relayed(DsnNotify.Delay | DsnNotify.Failure), Refused("451 4.3.0 Try again later") with { Status = SendStatus.TempFail });

            var report = queue.Enqueued.Single().MessageContent;

            Assert.Multiple(() => {
                Assert.That(report, Does.Contain("multipart/report; report-type=delivery-status"));
                Assert.That(report, Does.Contain("\r\nAction: delayed\r\n"));
                Assert.That(report, Does.Contain("\r\nStatus: 4.3.0\r\n"));
                Assert.That(report, Does.Contain("\r\nWill-Retry-Until: "));
                Assert.That(report, Does.Contain("Content-Type: text/rfc822-headers"), "RFC 3461 §6.2: no failure, the header only");
            });

        }


        [Test(Description = "D-10 - RFC 3461 §5.2.5 (c): NOTIFY without DELAY, no delayed DSN")]
        public async Task No_delayed_DSN_when_NOTIFY_leaves_out_DELAY()
        {

            var queue = new ReportQueue();
            await new BounceHandler(new SMTPServerConfig { Hostname = "relay.example" }, queue, new NullLogger())
                      .SendDelayNotificationAsync(Relayed(DsnNotify.Failure));

            Assert.That(queue.Enqueued, Is.Empty);

        }


        [Test(Description = "D-11 - RFC 3461 §5.2.2 (b), (e): a relayed DSN for the recipients that asked, and none for the others")]
        public async Task A_relayed_DSN_only_for_those_that_asked()
        {

            var mail = new QueuedMail {
                           Id             = "relayed",
                           EnvelopeFrom   = "sender@client.example",
                           EnvelopeTo     = [ "asked@elsewhere.example", "quiet@elsewhere.example" ],
                           MessageContent = Message,
                           TargetDomain   = "elsewhere.example",
                           Notify         = DsnNotify.Success | DsnNotify.Failure,
                           RecipientDsns  = [
                               new RecipientDsn { Recipient = "asked@elsewhere.example", Notify = DsnNotify.Success },
                               new RecipientDsn { Recipient = "quiet@elsewhere.example", Notify = DsnNotify.Failure }
                           ]
                       };

            var queue = new ReportQueue();
            await new BounceHandler(new SMTPServerConfig { Hostname = "relay.example" }, queue, new NullLogger()).SendRelayNotificationAsync(mail, remoteSupportsDsn: false);

            var report = queue.Enqueued.Single().MessageContent;

            Assert.Multiple(() => {
                Assert.That(report, Does.Contain("Final-Recipient: rfc822; asked@elsewhere.example"));
                Assert.That(report, Does.Not.Contain("Final-Recipient: rfc822; quiet@elsewhere.example"));
                Assert.That(report, Does.Contain("Content-Type: text/rfc822-headers"), "RFC 3461 §6.2: no failure, the header only");
            });

        }


        [Test(Description = "D-12 - RFC 3461 §5.2.1 (b), (c): no RET and no NOTIFY are received as none, and relayed as none")]
        public void What_was_not_given_is_not_made_up()
        {

            var (envId, ret)     = DsnParser.ParseMailFromParams("ENVID=QQ314159");
            var (notify, orcpt)  = DsnParser.ParseRcptToParams("");

            Assert.Multiple(() => {
                Assert.That(ret,    Is.Null, "no RET");
                Assert.That(notify, Is.Null, "no NOTIFY");
                Assert.That(DsnCommands.MailFromParams(new DsnParameters(DsnNotify.Never, ret, envId), remoteSupportsDsn: true), Is.EqualTo(" ENVID=QQ314159"));
                Assert.That(DsnCommands.RcptToParams(new RecipientDsn { Recipient = "bob@elsewhere.example", Notify = notify }, remoteSupportsDsn: true),
                            Is.EqualTo(" ORCPT=rfc822;bob@elsewhere.example"));
                Assert.That(DsnCommands.RcptToParams(new RecipientDsn { Recipient = "bob@elsewhere.example", Notify = DsnNotify.Never }, remoteSupportsDsn: true),
                            Is.EqualTo(" NOTIFY=NEVER ORCPT=rfc822;bob@elsewhere.example"), "NEVER was given, and is passed on");
            });

        }

    }

}
