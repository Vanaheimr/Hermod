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

using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    #region Bounce Types

    public enum BounceType
    {
        Hard,       // Permanent failure (5xx) - mailbox doesn't exist, etc.
        Soft,       // Temporary failure (4xx) - try again later
        Delayed     // Message delayed but still trying
    }

    public sealed record BounceInfo(
        BounceType  Type,
        string      OriginalRecipient,
        string      DiagnosticCode,
        string      RemoteMta,
        DateTime    FailureTime
    );

    #endregion

    #region Bounce Handler

    public sealed class BounceHandler(SMTPServerConfig config, IMailQueue queue, ILogger logger)
    {
        /// <summary>
        /// Generate and queue a bounce message (NDR) for a failed delivery
        /// </summary>
        public async Task SendBounceAsync(
            QueuedMail          originalMail,
            SendResult          failureResult,
            CancellationToken   ct = default)
        {
            // Don't bounce null sender (prevents bounce loops)
            if (string.IsNullOrEmpty(originalMail.EnvelopeFrom) || 
                originalMail.EnvelopeFrom == "<>" ||
                originalMail.EnvelopeFrom == "")
            {
                logger.Log(LogLevel.Debug, "Not sending bounce for null sender (prevents loops)");
                return;
            }

            // Don't bounce bounces (check for empty return-path in headers)
            if (IsBouncedMessage(originalMail.MessageContent))
            {
                logger.Log(LogLevel.Debug, "Not sending bounce for bounce message (prevents loops)");
                return;
            }

            var bounceInfo = new BounceInfo(
                Type: failureResult.Status == SendStatus.PermFail ? BounceType.Hard : BounceType.Soft,
                OriginalRecipient: string.Join(", ", originalMail.EnvelopeTo),
                DiagnosticCode: $"{failureResult.ResponseCode} {failureResult.ResponseText}",
                RemoteMta: failureResult.RemoteMx ?? "unknown",
                FailureTime: DateTime.UtcNow
            );

            var bounceMessage = GenerateBounceMessage(originalMail, bounceInfo);

            // Queue the bounce with null sender (prevents bounce loops)
            var bounceQueueItem = new QueuedMail
            {
                Id = $"bounce-{Guid.NewGuid():N}",
                EnvelopeFrom = "",  // Null sender
                EnvelopeTo = [originalMail.EnvelopeFrom],
                MessageContent = bounceMessage,
                TargetDomain = ExtractDomain(originalMail.EnvelopeFrom),
                QueuedAt = DateTime.UtcNow,
                NextRetry = DateTime.UtcNow
            };

            await queue.EnqueueAsync(bounceQueueItem, ct);
            logger.Log(LogLevel.Info, $"Queued bounce message for {originalMail.EnvelopeFrom}");
        }

        /// <summary>
        /// Generate and queue a "relayed" status notification (RFC 3461) after handing a message to the
        /// next hop, but only when the sender requested NOTIFY=SUCCESS AND the next hop did not advertise
        /// DSN. If it did, that server takes over the delivered-DSN responsibility (RFC 3461 §5.3.1) and
        /// we must not also notify, to avoid a duplicate. No-op otherwise.
        /// </summary>
        public async Task SendRelayNotificationAsync(
            QueuedMail          mail,
            Boolean             remoteSupportsDsn,
            CancellationToken   ct = default)
        {
            if (!mail.Notify.HasFlag(DsnNotify.Success) || remoteSupportsDsn)
                return;

            if (string.IsNullOrEmpty(mail.EnvelopeFrom) || mail.EnvelopeFrom == "<>" ||
                IsBouncedMessage(mail.MessageContent))
                return;

            var dsnGenerator = new DsnGenerator(config);

            foreach (var recipient in mail.EnvelopeTo)
            {

                var dsn = dsnGenerator.GenerateRelayDsn(
                    originalFrom:     mail.EnvelopeFrom,
                    recipient:        new RecipientDsn { Recipient = recipient, Notify = mail.Notify },
                    envId:            mail.EnvId,
                    ret:              mail.Ret,
                    originalMessage:  mail.MessageContent,
                    relayedTo:        mail.TargetDomain);

                if (dsn is null)
                    continue;

                await EnqueueReportAsync(dsn, mail.EnvelopeFrom, ct);

            }

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
            DsnRet                     ret,
            CancellationToken          ct = default)
        {
            if (string.IsNullOrEmpty(envelopeFrom) || envelopeFrom == "<>" ||
                IsBouncedMessage(originalMessage))
                return;

            var dsnGenerator = new DsnGenerator(config);

            foreach (var recipient in localRecipients)
            {

                var dsn = dsnGenerator.GenerateDeliveryDsn(
                    originalFrom:     envelopeFrom,
                    recipient:        recipient,
                    envId:            envId,
                    ret:              ret,
                    originalMessage:  originalMessage);   // null unless NOTIFY=SUCCESS

                if (dsn is null)
                    continue;

                await EnqueueReportAsync(dsn, envelopeFrom, ct);
                logger.Log(LogLevel.Info, $"Queued delivered DSN for {envelopeFrom} (recipient {recipient.Recipient})");

            }
        }

        // Queue a DSN report back to the original sender with a null return-path (loop-safe).
        private Task EnqueueReportAsync(String dsnMessage, String originalSender, CancellationToken ct)
            => queue.EnqueueAsync(new QueuedMail
               {
                   Id              = $"dsn-{Guid.NewGuid():N}",
                   EnvelopeFrom    = "",   // null sender (this is a report)
                   EnvelopeTo      = [originalSender],
                   MessageContent  = dsnMessage,
                   TargetDomain    = ExtractDomain(originalSender),
                   QueuedAt        = DateTime.UtcNow,
                   NextRetry       = DateTime.UtcNow
               }, ct);

        /// <summary>
        /// Generate a delay notification (mail still in queue, will keep trying)
        /// </summary>
        public async Task SendDelayNotificationAsync(
            QueuedMail          mail,
            CancellationToken   ct = default)
        {
            // Only send delay notifications for messages that have been queued for a while
            var queuedDuration = DateTime.UtcNow - mail.QueuedAt;
            if (queuedDuration < TimeSpan.FromHours(4))
            {
                return;  // Too early for delay notification
            }

            // Don't send delay notification for null sender
            if (string.IsNullOrEmpty(mail.EnvelopeFrom) || mail.EnvelopeFrom == "<>")
            {
                return;
            }

            var delayMessage = GenerateDelayNotification(mail);

            var delayQueueItem = new QueuedMail
            {
                Id = $"delay-{Guid.NewGuid():N}",
                EnvelopeFrom = "",
                EnvelopeTo = [mail.EnvelopeFrom],
                MessageContent = delayMessage,
                TargetDomain = ExtractDomain(mail.EnvelopeFrom),
                QueuedAt = DateTime.UtcNow,
                NextRetry = DateTime.UtcNow
            };

            await queue.EnqueueAsync(delayQueueItem, ct);
            logger.Log(LogLevel.Info, $"Queued delay notification for {mail.EnvelopeFrom}");
        }

        #region Message Generation

        private string GenerateBounceMessage(QueuedMail original, BounceInfo bounce)
        {
            var messageId = $"<bounce.{Guid.NewGuid():N}@{config.Hostname}>";
            var date = DateTime.UtcNow.ToString("R");
            var boundary = $"=_bounce_{Guid.NewGuid():N}";

            var sb = new StringBuilder();

            // Headers
            sb.AppendLine($"From: Mail Delivery System <mailer-daemon@{config.Hostname}>");
            sb.AppendLine($"To: {original.EnvelopeFrom}");
            sb.AppendLine($"Subject: Undelivered Mail Returned to Sender");
            sb.AppendLine($"Date: {date}");
            sb.AppendLine($"Message-ID: {messageId}");
            sb.AppendLine("MIME-Version: 1.0");
            sb.AppendLine("Auto-Submitted: auto-replied");
            sb.AppendLine($"Content-Type: multipart/report; report-type=delivery-status; boundary=\"{boundary}\"");
            sb.AppendLine();

            // Human-readable part
            sb.AppendLine($"--{boundary}");
            sb.AppendLine("Content-Type: text/plain; charset=utf-8");
            sb.AppendLine();
            sb.AppendLine("This is the mail system at host " + config.Hostname + ".");
            sb.AppendLine();
        
            if (bounce.Type == BounceType.Hard)
            {
                sb.AppendLine("I'm sorry to have to inform you that your message could not");
                sb.AppendLine("be delivered to one or more recipients. It's attached below.");
                sb.AppendLine();
                sb.AppendLine("For further assistance, please send mail to postmaster.");
                sb.AppendLine();
                sb.AppendLine("If you do so, please include this problem report.");
            }
            else
            {
                sb.AppendLine("Your message could not be delivered at this time.");
                sb.AppendLine("Delivery will be retried.");
            }
        
            sb.AppendLine();
            sb.AppendLine($"<{bounce.OriginalRecipient}>: delivery failed");
            sb.AppendLine($"    Remote server: {bounce.RemoteMta}");
            sb.AppendLine($"    Error: {bounce.DiagnosticCode}");
            sb.AppendLine();

            // Delivery Status Notification (DSN) part
            sb.AppendLine($"--{boundary}");
            sb.AppendLine("Content-Type: message/delivery-status");
            sb.AppendLine();
            sb.AppendLine($"Reporting-MTA: dns; {config.Hostname}");
            sb.AppendLine($"X-Queue-ID: {original.Id}");
            sb.AppendLine($"Arrival-Date: {original.QueuedAt:R}");
            sb.AppendLine();
        
            foreach (var recipient in original.EnvelopeTo)
            {
                sb.AppendLine($"Final-Recipient: rfc822; {recipient}");
                sb.AppendLine($"Original-Recipient: rfc822; {recipient}");
                sb.AppendLine($"Action: {(bounce.Type == BounceType.Hard ? "failed" : "delayed")}");
                sb.AppendLine($"Status: {GetStatusCode(bounce)}");
                sb.AppendLine($"Remote-MTA: dns; {bounce.RemoteMta}");
                sb.AppendLine($"Diagnostic-Code: smtp; {bounce.DiagnosticCode}");
                sb.AppendLine($"Last-Attempt-Date: {bounce.FailureTime:R}");
                sb.AppendLine();
            }

            // Original message headers (truncated)
            sb.AppendLine($"--{boundary}");
            sb.AppendLine("Content-Type: message/rfc822");
            sb.AppendLine("Content-Disposition: inline");
            sb.AppendLine();
        
            // Include original headers and first 100 lines of body
            var truncatedOriginal = TruncateMessage(original.MessageContent, 100);
            sb.Append(truncatedOriginal);
            sb.AppendLine();

            sb.AppendLine($"--{boundary}--");

            return sb.ToString().Replace("\n", "\r\n");
        }

        private string GenerateDelayNotification(QueuedMail mail)
        {
            var messageId = $"<delay.{Guid.NewGuid():N}@{config.Hostname}>";
            var date = DateTime.UtcNow.ToString("R");
            var queuedDuration = DateTime.UtcNow - mail.QueuedAt;

            var sb = new StringBuilder();

            // Headers
            sb.AppendLine($"From: Mail Delivery System <mailer-daemon@{config.Hostname}>");
            sb.AppendLine($"To: {mail.EnvelopeFrom}");
            sb.AppendLine($"Subject: Delayed Mail (still trying to deliver)");
            sb.AppendLine($"Date: {date}");
            sb.AppendLine($"Message-ID: {messageId}");
            sb.AppendLine("MIME-Version: 1.0");
            sb.AppendLine("Auto-Submitted: auto-replied");
            sb.AppendLine("Content-Type: text/plain; charset=utf-8");
            sb.AppendLine();

            // Body
            sb.AppendLine("This is the mail system at host " + config.Hostname + ".");
            sb.AppendLine();
            sb.AppendLine("####################################################################");
            sb.AppendLine("# THIS IS A WARNING MESSAGE ONLY - YOUR MESSAGE HAS NOT YET BEEN  #");
            sb.AppendLine("# DELIVERED. DELIVERY ATTEMPTS WILL CONTINUE.                      #");
            sb.AppendLine("####################################################################");
            sb.AppendLine();
            sb.AppendLine($"Your message to <{string.Join(", ", mail.EnvelopeTo)}> has been");
            sb.AppendLine($"queued for {FormatDuration(queuedDuration)} and delivery is still being attempted.");
            sb.AppendLine();
            sb.AppendLine($"Reason for delay: {mail.LastError ?? "Unknown"}");
            sb.AppendLine();
            sb.AppendLine("The mail system will continue trying to deliver your message");
            sb.AppendLine($"for approximately {FormatDuration(RetryCalculator.MaxQueueTime - queuedDuration)} more.");
            sb.AppendLine();
            sb.AppendLine("No further action is required on your part.");
            sb.AppendLine();
            sb.AppendLine($"Original message ID: {mail.Id}");
            sb.AppendLine($"Target domain: {mail.TargetDomain}");

            return sb.ToString().Replace("\n", "\r\n");
        }

        #endregion

        #region Helpers

        private static bool IsBouncedMessage(string content)
        {
            // Check for indicators that this is already a bounce
            return content.Contains("Auto-Submitted: auto-replied", StringComparison.OrdinalIgnoreCase) ||
                   content.Contains("multipart/report", StringComparison.OrdinalIgnoreCase) ||
                   content.Contains("message/delivery-status", StringComparison.OrdinalIgnoreCase) ||
                   content.Contains("From: Mail Delivery System", StringComparison.OrdinalIgnoreCase) ||
                   content.Contains("From: MAILER-DAEMON", StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractDomain(string email)
        {
            var atIndex = email.IndexOf('@');
            if (atIndex > 0 && atIndex < email.Length - 1)
            {
                var domain = email[(atIndex + 1)..];
                // Remove any trailing >
                return domain.TrimEnd('>');
            }
            return email;
        }

        private static string GetStatusCode(BounceInfo bounce)
        {
            // DSN status codes (RFC 3463)
            var code = bounce.DiagnosticCode;
        
            if (code.Contains("550") || code.Contains("551") || code.Contains("552") || code.Contains("553"))
                return "5.1.1";  // Bad destination mailbox address
            if (code.Contains("554"))
                return "5.7.1";  // Delivery not authorized
            if (code.Contains("450") || code.Contains("451"))
                return "4.0.0";  // Other/undefined temporary error
            if (code.Contains("452"))
                return "4.2.2";  // Mailbox full
        
            return bounce.Type == BounceType.Hard ? "5.0.0" : "4.0.0";
        }

        private static string TruncateMessage(string message, int maxBodyLines)
        {
            var headerEnd = message.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (headerEnd < 0)
                headerEnd = message.IndexOf("\n\n", StringComparison.Ordinal);

            if (headerEnd < 0)
                return message;

            var headers = message[..(headerEnd + 4)];
            var body = message[(headerEnd + 4)..];

            var bodyLines = body.Split('\n');
            if (bodyLines.Length <= maxBodyLines)
                return message;

            var truncatedBody = string.Join("\n", bodyLines.Take(maxBodyLines));
            return headers + truncatedBody + "\n\n[... message truncated ...]";
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalDays >= 1)
                return $"{(int)duration.TotalDays} day(s) and {duration.Hours} hour(s)";
            if (duration.TotalHours >= 1)
                return $"{(int)duration.TotalHours} hour(s) and {duration.Minutes} minute(s)";
            return $"{(int)duration.TotalMinutes} minute(s)";
        }

        #endregion
    }

    #endregion

}
