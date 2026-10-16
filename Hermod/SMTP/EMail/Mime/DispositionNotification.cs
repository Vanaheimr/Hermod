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

using System.Globalization;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// Message disposition notifications - read receipts (RFC 8098): who asks for
    /// one, and the one a reader sends - by hand, when asked whether to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A read receipt tells the sender that, and when, the mail was read: it is
    /// sent only when the reader says so (RFC 8098 §2.1), never on its own. An
    /// answer either way is to be remembered, so that nobody is asked twice - the
    /// $MDNSent keyword of RFC 3503.
    /// </para>
    /// <para>
    /// The notification goes from a null reverse-path (§2: MAIL FROM:&lt;&gt;), so
    /// that nothing answers it. It does not carry the header of the message it
    /// is about: the Original-Message-ID says which one it was.
    /// </para>
    /// </remarks>
    public static class DispositionNotification
    {

        #region RequestedBy(Message)

        /// <summary>
        /// Where the message asks a read receipt to be sent - its
        /// Disposition-Notification-To - or null when it asks for none.
        /// </summary>
        public static MailboxAddress? RequestedBy(MimeMessage Message)

            => MailboxAddress.ParseList(Message.Headers.Get("Disposition-Notification-To")).
                              FirstOrDefault(mailbox => MimeEncoding.IsAddress(mailbox.Address));

        #endregion

        #region GoesElsewhere(Message, To, EnvelopeFrom)

        /// <summary>
        /// Whether the receipt would go to another address than the one the message
        /// came from - its From: and its reverse-path. RFC 8098 §2.1 asks to say so:
        /// it may be somebody who wants to learn that an address is read.
        /// </summary>
        public static Boolean GoesElsewhere(MimeMessage     Message,
                                            MailboxAddress  To,
                                            String?         EnvelopeFrom)

            => !Message.From.Any(from => from.Address.Equals(To.Address, StringComparison.OrdinalIgnoreCase)) ||
               (EnvelopeFrom is { Length: > 0 } envelopeFrom && !envelopeFrom.Equals(To.Address, StringComparison.OrdinalIgnoreCase));

        #endregion

        #region Build(Message, From, FinalRecipient, ...)

        /// <summary>
        /// The read receipt for a message the reader displayed, and chose to confirm:
        /// multipart/report; report-type=disposition-notification, with a text for
        /// people and the message/disposition-notification for programs.
        /// </summary>
        /// <param name="Message">The message read.</param>
        /// <param name="From">The reader: the address the message came to.</param>
        /// <param name="ReportingUA">The name of the reading program, for Reporting-UA.</param>
        /// <param name="Displayed">When it was read.</param>
        /// <returns>The notification - to be sent from MAIL FROM:&lt;&gt;.</returns>
        /// <exception cref="ArgumentException">The message asks for no read receipt.</exception>
        public static MimeMessageBuilder Build(MimeMessage     Message,
                                               MailboxAddress  From,
                                               String          ReportingUA,
                                               DateTimeOffset  Displayed)
        {

            var to       = RequestedBy(Message) ?? throw new ArgumentException("The message asks for no read receipt.");
            var domain   = From.Address[(From.Address.LastIndexOf('@') + 1)..];
            var subject  = Message.Subject.Length > 0 ? Message.Subject : "(kein Betreff)";
            var when     = Displayed.ToString("dd.MM.yyyy, HH:mm", CultureInfo.InvariantCulture);

            var builder  = new MimeMessageBuilder {
                               From       = From,
                               Subject    = $"Gelesen: {subject}",
                               Date       = Displayed,
                               MessageId  = MimeEncoding.NewMessageId(domain)
                           };

            builder.To.Add(to);

            if (Message.MessageId is { } original)
            {
                builder.InReplyTo. Add(original);
                builder.References.Add(original);
            }

            // RFC 8098 §3.1, in the order it gives them.
            var report = new StringBuilder().
                             Append($"Reporting-UA: {domain}; {ReportingUA}\r\n").
                             Append($"Final-Recipient: rfc822; {From.Address}\r\n");

            if (Message.MessageId is { } messageId)
                report.Append($"Original-Message-ID: <{messageId}>\r\n");

            report.Append("Disposition: manual-action/MDN-sent-manually; displayed\r\n");

            builder.Body = new MimeMultipart("report",
                                             [
                                                 new MimeTextPart($"Ihre Nachricht „{subject}“ an {From.Address} wurde am {when} angezeigt.\n" +
                                                                  $"Your message \"{subject}\" to {From.Address} was displayed on {when}.\n\n" +
                                                                  "Das heißt nicht, dass sie gelesen oder verstanden wurde.\n" +
                                                                  "This does not mean that it was read or understood.\n"),
                                                 new MimeRawEntity(Encoding.UTF8.GetBytes("Content-Type: message/disposition-notification\r\n" +
                                                                                          "Content-Transfer-Encoding: 7bit\r\n" +
                                                                                          "\r\n" +
                                                                                          report.ToString()))
                                             ],
                                             [ ("report-type", "disposition-notification") ]);

            return builder;

        }

        #endregion

    }

}
