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

using org.GraphDefined.Vanaheimr.Hermod.Mail;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP.Server
{

    /// <summary>
    /// An <see cref="IMailStorage"/> decorator that turns the local-delivery step into a minimal
    /// delivery agent: after a message is stored in a local mailbox, it automatically generates a
    /// Message Disposition Notification (read receipt, RFC 8098) for any message that requested one
    /// (via <c>Disposition-Notification-To</c>) and queues it back to the requester.
    ///
    /// The reported disposition is <c>processed</c> with <c>automatic-action</c>: the message was
    /// handled automatically by the delivery agent, NOT displayed by a human — claiming "displayed"
    /// would be false.
    ///
    /// <para>
    /// This is deliberately opt-in and privacy-sensitive: RFC 8098 §2.1 warns that automatically
    /// generated MDNs confirm a live recipient address (a spam/abuse signal) and can create mail loops.
    /// The generated MDN carries <c>Auto-Submitted: auto-replied</c> to guard against reply loops, and
    /// MDN generation is best-effort — it never fails or delays the actual local delivery.
    /// </para>
    /// </summary>
    public sealed class MdnGeneratingMailStorage : IMailStorage
    {

        private readonly IMailStorage        inner;
        private readonly MailSender          sender;
        private readonly ILogger             logger;
        private readonly MessageDisposition  disposition;
        private readonly String?             reportingUAProduct;

        /// <summary>
        /// Wrap <paramref name="Inner"/> so that stored messages requesting a read receipt trigger an
        /// automatic MDN, queued for delivery through <paramref name="Queue"/>.
        /// </summary>
        /// <param name="Inner">The underlying storage that actually persists the message.</param>
        /// <param name="Queue">The outbound queue used to send the generated MDN back to the requester.</param>
        /// <param name="Logger">A logger.</param>
        /// <param name="Disposition">The disposition to report; defaults to processed / automatic.</param>
        /// <param name="ReportingUAProduct">The product name for the MDN's Reporting-UA field.</param>
        public MdnGeneratingMailStorage(IMailStorage         Inner,
                                        IMailQueue           Queue,
                                        ILogger              Logger,
                                        MessageDisposition?  Disposition          = null,
                                        String?              ReportingUAProduct   = null)
        {
            this.inner               = Inner;
            this.sender              = new MailSender(Queue, Logger);
            this.logger              = Logger;
            this.disposition         = Disposition ?? new MessageDisposition(
                                           DispositionActionMode.AutomaticAction,
                                           DispositionSendingMode.SentAutomatically,
                                           MessageDispositionType.Processed);
            this.reportingUAProduct  = ReportingUAProduct;
        }


        public async Task<String> StoreAsync(EMailMessage         message,
                                             String               envelopeFrom,
                                             IEnumerable<String>  envelopeTo,
                                             CancellationToken    ct = default)
        {

            // The actual delivery must always happen and own the return value.
            var path = await inner.StoreAsync(message, envelopeFrom, envelopeTo, ct);

            // MDN generation is a best-effort side effect — never fail or delay local delivery for it.
            try
            {
                await GenerateReadReceiptsAsync(message, envelopeTo, ct);
            }
            catch (Exception e)
            {
                logger.Log(LogLevel.Warning, $"Automatic MDN generation failed: {e.Message}");
            }

            return path;

        }


        private async Task GenerateReadReceiptsAsync(EMailMessage         message,
                                                     IEnumerable<String>  envelopeTo,
                                                     CancellationToken    ct)
        {

            var mail = EMail.Parse(message.RawMessage.Split(["\r\n", "\n"], StringSplitOptions.None));

            if (!mail.IsReadReceiptRequested)
                return;

            // One MDN per local recipient — each mailbox reports its own disposition (RFC 8098).
            foreach (var recipient in envelopeTo)
            {

                var reporter = (EMailAddress) SimpleEMailAddress.Parse(recipient);
                var mdn      = mail.CreateReadReceipt(reporter, disposition, reportingUAProduct);

                if (mdn is null)
                    continue;

                await sender.SendAsync(mdn, CancellationToken: ct);
                logger.Log(LogLevel.Info, $"Queued automatic MDN from {recipient} to {mail.DispositionNotificationTo}");

            }

        }

    }

}
