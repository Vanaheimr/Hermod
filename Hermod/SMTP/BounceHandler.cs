/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// The reports a relay owes the sender of a message (RFC 3461 §5.2, RFC 5321 §6.1): a "failed"
    /// DSN when it cannot deliver, a "delayed" one while it keeps trying, a "relayed" one when the
    /// next hop will not report delivery. Each is a DSN (RFC 3464), sent to the reverse-path with
    /// a null reverse-path of its own (RFC 3461 §6.1), for the recipients that asked for it.
    /// </summary>
    public sealed class BounceHandler(SMTPServerConfig config, IMailQueue queue, ILogger logger)
    {

        /// <summary>
        /// Generate and queue a "failed" DSN for a message that cannot be delivered - refused, or
        /// given up on after its retries. One report covers the recipients of the message that
        /// did not leave FAILURE out of their NOTIFY (RFC 3461 §5.2.6).
        /// </summary>
        public async Task SendBounceAsync(
            QueuedMail          originalMail,
            SendResult          failureResult,
            CancellationToken   ct = default)
        {

            // RFC 3461 §6 NOTE: "A DSN MUST NOT be returned to the sender for any message for which the
            // return address from the SMTP MAIL command was NULL" - which is also what keeps reports
            // from looping, since every report goes out with a null reverse-path. What the message
            // says about itself does not count: a message that mentions "multipart/report" or quotes
            // a bounce is owed its report like any other (RFC 5321 §6.1).
            if (IsNullReversePath(originalMail.EnvelopeFrom))
            {
                logger.Log(LogLevel.Debug, $"No bounce for {originalMail.Id}: its reverse-path is null");
                return;
            }

            var recipients = originalMail.EnvelopeTo.
                                 Select(recipient => DsnOf(originalMail, recipient)).
                                 Where (recipient => recipient.ReportsFailure).
                                 Select(recipient => Outcome(recipient, failureResult, DsnAction.Failed)).
                                 ToList();

            if (recipients.Count == 0)
            {
                logger.Log(LogLevel.Debug, $"No bounce for {originalMail.Id}: NOTIFY did not ask for one");
                return;
            }

            await EnqueueReportAsync(new DsnGenerator(config).Generate(originalMail.EnvelopeFrom,
                                                                       originalMail.EnvId,
                                                                       originalMail.Ret,
                                                                       originalMail.QueuedAt,
                                                                       originalMail.MessageContent,
                                                                       recipients),
                                     originalMail.EnvelopeFrom, "bounce", ct);

            logger.Log(LogLevel.Info, $"Queued bounce message for {originalMail.EnvelopeFrom}");

        }

        /// <summary>
        /// Generate and queue a "relayed" status notification (RFC 3461) after handing a message to the
        /// next hop, but only when the sender requested NOTIFY=SUCCESS AND the next hop did not advertise
        /// DSN. If it did, that server takes over the delivered-DSN responsibility (RFC 3461 §5.3.1) and
        /// we must not also notify, to avoid a duplicate. Only the recipients that asked are reported
        /// on (§5.2.2 (b), (e)). No-op otherwise.
        /// </summary>
        public async Task SendRelayNotificationAsync(
            QueuedMail          mail,
            Boolean             remoteSupportsDsn,
            CancellationToken   ct = default)
        {

            if (remoteSupportsDsn || IsNullReversePath(mail.EnvelopeFrom))
                return;

            var reply      = mail.RemoteResponse is { } response ? DsnGenerator.Reply(250, response) : null;
            var recipients = mail.EnvelopeTo.
                                 Select(recipient => DsnOf(mail, recipient)).
                                 Where (recipient => recipient.ReportsSuccess).
                                 Select(recipient => new DsnRecipient(recipient.Recipient,
                                                                      DsnAction.Relayed,
                                                                      "2.0.0",
                                                                      recipient.OriginalRecipient,
                                                                      mail.RemoteMx,
                                                                      reply is not null ? $"smtp; {reply}" : null,
                                                                      Timestamp.Now)).
                                 ToList();

            if (recipients.Count == 0)
                return;

            await EnqueueReportAsync(new DsnGenerator(config).Generate(mail.EnvelopeFrom, mail.EnvId, mail.Ret, mail.QueuedAt, mail.MessageContent, recipients),
                                     mail.EnvelopeFrom, "dsn", ct);

            logger.Log(LogLevel.Info, $"Queued relayed DSN for {mail.EnvelopeFrom}");

        }

        /// <summary>
        /// Generate and queue positive "delivered" status notifications (RFC 3461) after a message was
        /// finally delivered to one or more local mailboxes, for each recipient that requested
        /// NOTIFY=SUCCESS. No-op otherwise.
        /// </summary>
        public async Task SendLocalDeliveryNotificationAsync(
            String                     envelopeFrom,
            IEnumerable<RecipientDsn>  localRecipients,
            String                     originalMessage,
            String?                    envId,
            DsnRet?                    ret,
            CancellationToken          ct = default)
        {

            if (IsNullReversePath(envelopeFrom))
                return;

            var recipients = localRecipients.
                                 Where (recipient => recipient.ReportsSuccess).
                                 Select(recipient => new DsnRecipient(recipient.Recipient, DsnAction.Delivered, "2.0.0",
                                                                      recipient.OriginalRecipient, LastAttemptDate: Timestamp.Now)).
                                 ToList();

            if (recipients.Count == 0)
                return;

            await EnqueueReportAsync(new DsnGenerator(config).Generate(envelopeFrom, envId, ret, Timestamp.Now, originalMessage, recipients),
                                     envelopeFrom, "dsn", ct);

            logger.Log(LogLevel.Info, $"Queued delivered DSN for {envelopeFrom} ({String.Join(", ", recipients.Select(recipient => recipient.Recipient))})");

        }

        /// <summary>
        /// Generate and queue a "delayed" DSN (RFC 3461 §5.2.5) for a message still in the queue, for
        /// the recipients that did not leave DELAY out of their NOTIFY. When to send one is the
        /// caller's decision (QueueProcessorConfig.DelayNotificationAfter).
        /// </summary>
        /// <param name="mail">The message still being tried.</param>
        /// <param name="lastResult">The answer to the last attempt, when there was one.</param>
        /// <param name="ct">A cancellation token.</param>
        public async Task SendDelayNotificationAsync(
            QueuedMail          mail,
            SendResult?         lastResult   = null,
            CancellationToken   ct           = default)
        {

            if (IsNullReversePath(mail.EnvelopeFrom))
                return;

            var result     = lastResult ?? SendResult.TempFail(mail.LastError ?? "not yet delivered");
            var retryUntil = mail.QueuedAt + RetryCalculator.MaxQueueTime;
            var recipients = mail.EnvelopeTo.
                                 Select(recipient => DsnOf(mail, recipient)).
                                 Where (recipient => recipient.ReportsDelay).
                                 Select(recipient => Outcome(recipient, result, DsnAction.Delayed) with { WillRetryUntil = retryUntil }).
                                 ToList();

            if (recipients.Count == 0)
                return;

            await EnqueueReportAsync(new DsnGenerator(config).Generate(mail.EnvelopeFrom, mail.EnvId, mail.Ret, mail.QueuedAt, mail.MessageContent, recipients),
                                     mail.EnvelopeFrom, "delay", ct);

            logger.Log(LogLevel.Info, $"Queued delay notification for {mail.EnvelopeFrom}");

        }


        #region Helpers

        // RFC 3461 §6.1: a report has a null reverse-path, and goes to the reverse-path of the
        // message it reports on. No RET, and no NOTIFY but NEVER - none here.
        private Task EnqueueReportAsync(String dsnMessage, String originalSender, String kind, CancellationToken ct)
            => queue.EnqueueAsync(new QueuedMail
               {
                   Id              = $"{kind}-{UUIDv7.Generate():N}",
                   EnvelopeFrom    = "",
                   EnvelopeTo      = [originalSender],
                   MessageContent  = dsnMessage,
                   TargetDomain    = ExtractDomain(originalSender),
                   QueuedAt        = Timestamp.Now,
                   NextRetry       = Timestamp.Now
               }, ct);

        /// <summary>
        /// The DSN request of one recipient: as it was received with the message, or - for a message
        /// of our own (MailSender) - the message-wide NOTIFY, where Never means none was given.
        /// </summary>
        private static RecipientDsn DsnOf(QueuedMail mail, String recipient)

            => mail.RecipientDsns.FirstOrDefault(recipientDsn => recipientDsn.Recipient == recipient)
                   ?? new RecipientDsn {
                          Recipient  = recipient,
                          Notify     = mail.Notify == DsnNotify.Never ? null : mail.Notify
                      };

        /// <summary>
        /// One recipient's entry for a report on a failed or delayed attempt: the next hop's answer
        /// to its RCPT when there was one, else the answer to the attempt - its status code
        /// (RFC 3461 §6.3 (g)), and the reply as it was (§6.3 (i)).
        /// </summary>
        private static DsnRecipient Outcome(RecipientDsn recipient, SendResult result, DsnAction action)
        {

            var outcome = result.Recipients.FirstOrDefault(outcome => outcome.Recipient == recipient.Recipient);
            var code    = outcome?.Code ?? result.ResponseCode;
            var text    = outcome?.Text ?? result.ResponseText;
            var reply   = DsnGenerator.Reply(code, text);

            return new DsnRecipient(recipient.Recipient,
                                    action,
                                    DsnGenerator.StatusOf(code, text, Permanent: result.Status == SendStatus.PermFail),
                                    recipient.OriginalRecipient,
                                    reply is not null ? result.RemoteMx : null,
                                    reply is not null ? $"smtp; {reply}" : $"X-Hermod; {text}",
                                    Timestamp.Now);

        }

        private static Boolean IsNullReversePath(String? reversePath)
            => String.IsNullOrEmpty(reversePath) || reversePath == "<>";

        private static string ExtractDomain(string email)
        {
            var atIndex = email.LastIndexOf('@');
            if (atIndex > 0 && atIndex < email.Length - 1)
            {
                var domain = email[(atIndex + 1)..];
                // Remove any trailing >
                return domain.TrimEnd('>');
            }
            return email;
        }

        #endregion

    }

}
