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

using System.Diagnostics;

using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// The DSN parameters a sender gives (RFC 3461), as they go on the wire: ENVID as xtext and
    /// at most 100 characters of it (§4, §4.4), never a command of its own; RET and NOTIFY=NEVER
    /// as they were given (§4.1, §4.3); and an ENVID received with a message passed on as it
    /// came (§5.2.1 (a)).
    /// </summary>
    public partial class SMTPOutboundClientTests
    {

        #region The parameters

        [TestCase("QQ314159",    "QQ314159")]
        [TestCase("order 42",    "order+2042")]
        [TestCase("batch=7",     "batch+3D7")]
        [TestCase("QQ+314159",   "QQ+2B314159")]
        public void ENVID_goes_as_xtext(String EnvId, String XText)

            => Assert.That(DsnCommands.MailFromParams(new DsnParameters(DsnNotify.Failure, EnvId: EnvId), remoteSupportsDsn: true),
                           Is.EqualTo(" ENVID=" + XText));


        [TestCase("x\r\nRCPT TO:<intruder@evil.test>")]
        [TestCase("")]
        [TestCase("Grüße")]
        public void An_ENVID_that_cannot_be_one_is_refused(String EnvId)
        {

            var dsn = new DsnParameters(DsnNotify.Failure, EnvId: EnvId);

            Assert.That(dsn.EnvIdError, Is.Not.Null);
            Assert.That(() => DsnCommands.MailFromParams(dsn, remoteSupportsDsn: true), Throws.ArgumentException);

        }


        [Test]
        public void An_ENVID_of_up_to_100_characters_as_xtext_is_one()
        {

            Assert.That(new DsnParameters(EnvId: new String('e', 100)).EnvIdError,          Is.Null);
            Assert.That(new DsnParameters(EnvId: new String('e', 101)).EnvIdError,          Is.Not.Null);
            Assert.That(new DsnParameters(EnvId: new String(' ', 34)).EnvIdError,           Is.Not.Null, "34 spaces are 102 characters as xtext");

        }


        [Test]
        public void RET_goes_without_NOTIFY_and_ENVID()
        {

            Assert.That(DsnCommands.MailFromParams(new DsnParameters(Ret: DsnRet.Hdrs), remoteSupportsDsn: true),  Is.EqualTo(" RET=HDRS"));
            Assert.That(DsnCommands.MailFromParams(new DsnParameters(Ret: DsnRet.Hdrs), remoteSupportsDsn: false), Is.EqualTo(""));

        }


        [Test]
        public void NOTIFY_NEVER_goes_and_no_NOTIFY_is_none()
        {

            Assert.That(DsnCommands.RcptToParams(new DsnParameters(DsnNotify.Never), "you@example.org", remoteSupportsDsn: true),
                        Is.EqualTo(" NOTIFY=NEVER ORCPT=rfc822;you@example.org"));

            Assert.That(DsnCommands.RcptToParams(DsnParameters.None,                 "you@example.org", remoteSupportsDsn: true), Is.EqualTo(""));
            Assert.That(DsnCommands.MailFromParams(DsnParameters.None,                                  remoteSupportsDsn: true), Is.EqualTo(""));
            Assert.That(DsnParameters.None.IsRequested, Is.False);

        }

        #endregion

        #region Through the queue

        private static EMailEnvelop DsnEnvelop(DsnParameters Dsn)
        {

            var mail = new TextEMailBuilder { Text = "body" };
            mail.From    = (EMailAddress) SimpleEMailAddress.Parse("anna@example.org");
            mail.To      = (EMailAddress) SimpleEMailAddress.Parse("you@next.example");
            mail.Subject = "DSN";

            return new EMailEnvelop((EMail) mail) { Dsn = Dsn };

        }

        /// <summary>
        /// Deliver what is queued to a next hop, and give its commands.
        /// </summary>
        private static async Task<String[]> DeliverQueued(Func<MemoryQueue, ILogger, Task<String>> Enqueue)
        {

            using var nextHop = new NextHop();

            var queue     = new MemoryQueue();
            var logger    = new QuietLogger();
            var client    = new SMTPOutboundClient(new SmtpOutboundConfig {
                                                       LocalHostname     = "relay.hermod.test",
                                                       SmartHost         = "127.0.0.1",
                                                       SmartHostPort     = nextHop.Port,
                                                       ConnectTimeoutMs  = 3_000,
                                                       ReadTimeoutMs     = 5_000,
                                                       WriteTimeoutMs    = 3_000
                                                   },
                                                   null,
                                                   new NoDNS(),
                                                   logger);

            var processor = new QueueProcessor(queue,
                                               client,
                                               new BounceHandler(new SMTPServerConfig { Hostname = "relay.hermod.test" }, queue, logger),
                                               new QueueProcessorConfig { DomainCooldownSeconds = 0 },
                                               logger);

            var queued    = queue.Mails[await Enqueue(queue, logger)];

            await processor.StartAsync();

            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(10) && queued.Status is QueueItemStatus.Pending or QueueItemStatus.Processing)
                await Task.Delay(50);

            await Task.Delay(100);
            await processor.StopAsync();
            await processor.DisposeAsync();

            return [ .. nextHop.Commands ];

        }


        [Test]
        public async Task A_queued_message_goes_with_the_DSN_parameters_its_sender_gave()
        {

            var commands = await DeliverQueued(async (queue, logger) =>
                                   (await new MailSender(queue, logger).SendAsync(DsnEnvelop(new DsnParameters(DsnNotify.Never, DsnRet.Hdrs, "order 42")))).Single());

            Assert.That(commands.Single(command => command.StartsWith("MAIL")), Does.Contain(" RET=HDRS").And.Contain(" ENVID=order+2042"));
            Assert.That(commands.Single(command => command.StartsWith("RCPT")), Does.Contain(" NOTIFY=NEVER"));

        }


        [Test]
        public async Task A_queued_message_that_asks_for_nothing_goes_without_DSN_parameters()
        {

            var commands = await DeliverQueued(async (queue, logger) =>
                                   (await new MailSender(queue, logger).SendAsync(DsnEnvelop(DsnParameters.None))).Single());

            Assert.That(commands.Single(command => command.StartsWith("MAIL")), Does.Not.Contain("RET=").And.Not.Contain("ENVID="));
            Assert.That(commands.Single(command => command.StartsWith("RCPT")), Does.Not.Contain("NOTIFY=").And.Not.Contain("ORCPT="));

        }


        /// <summary>
        /// RFC 3461 §5.2.1 (a): the ENVID of a message relayed goes "with the same value" - not
        /// decoded and encoded again, which would turn "+41" into "A".
        /// </summary>
        [Test]
        public async Task A_received_ENVID_is_relayed_as_it_came()
        {

            var commands = await DeliverQueued(async (queue, _) => {

                var mail = new QueuedMail {
                               Id              = "relayed-envid",
                               EnvelopeFrom    = "anna@example.org",
                               EnvelopeTo      = [ "you@next.example" ],
                               MessageContent  = "From: anna@example.org\r\nTo: you@next.example\r\nSubject: relayed\r\n\r\nbody\r\n",
                               TargetDomain    = "next.example",
                               EnvId           = "QQ+41+2B1"
                           };

                await queue.EnqueueAsync(mail);
                return mail.Id;

            });

            Assert.That(commands.Single(command => command.StartsWith("MAIL")), Does.Contain(" ENVID=QQ+41+2B1"));

        }


        [Test]
        public async Task An_ENVID_that_cannot_be_one_is_not_queued()
        {

            var queue = new MemoryQueue();

            Assert.That(() => new MailSender(queue, new QuietLogger()).SendAsync(DsnEnvelop(new DsnParameters(DsnNotify.Failure, EnvId: "x\r\nRCPT TO:<intruder@evil.test>"))),
                        Throws.ArgumentException);
            Assert.That(queue.Mails, Is.Empty);

            await Task.CompletedTask;

        }

        #endregion

        #region Direct

        [Test]
        public async Task An_ENVID_that_cannot_be_one_goes_to_no_next_hop()
        {

            using var nextHop = new NextHop();

            var logger  = new QuietLogger();
            var client  = new SMTPOutboundClient(new SmtpOutboundConfig {
                                                     LocalHostname     = "relay.hermod.test",
                                                     SmartHost         = "127.0.0.1",
                                                     SmartHostPort     = nextHop.Port,
                                                     ConnectTimeoutMs  = 3_000,
                                                     ReadTimeoutMs     = 5_000,
                                                     WriteTimeoutMs    = 3_000
                                                 },
                                                 null,
                                                 new NoDNS(),
                                                 logger);

            var results = await new MailSender(new MemoryQueue(), logger, client).SendDirectAsync(DsnEnvelop(new DsnParameters(DsnNotify.Failure, EnvId: "x\r\nRCPT TO:<intruder@evil.test>")));

            await Task.Delay(100);

            Assert.That(results.Single().Result.Status, Is.EqualTo(SendStatus.PermFail));
            Assert.That(nextHop.Commands,               Has.None.StartsWith("MAIL").And.None.Contains("intruder"));

        }


        [Test]
        public async Task A_direct_message_goes_with_the_DSN_parameters_its_sender_gave()
        {

            using var nextHop = new NextHop();

            var logger  = new QuietLogger();
            var client  = new SMTPOutboundClient(new SmtpOutboundConfig {
                                                     LocalHostname     = "relay.hermod.test",
                                                     SmartHost         = "127.0.0.1",
                                                     SmartHostPort     = nextHop.Port,
                                                     ConnectTimeoutMs  = 3_000,
                                                     ReadTimeoutMs     = 5_000,
                                                     WriteTimeoutMs    = 3_000
                                                 },
                                                 null,
                                                 new NoDNS(),
                                                 logger);

            var results = await new MailSender(new MemoryQueue(), logger, client).SendDirectAsync(DsnEnvelop(new DsnParameters(DsnNotify.Never, DsnRet.Hdrs, "order 42")));

            await Task.Delay(100);

            Assert.That(results.Single().Result.Status,                                  Is.EqualTo(SendStatus.Success));
            Assert.That(nextHop.Commands.Single(command => command.StartsWith("MAIL")), Does.Contain(" RET=HDRS").And.Contain(" ENVID=order+2042"));
            Assert.That(nextHop.Commands.Single(command => command.StartsWith("RCPT")), Does.Contain(" NOTIFY=NEVER"));

        }

        #endregion

    }

}
