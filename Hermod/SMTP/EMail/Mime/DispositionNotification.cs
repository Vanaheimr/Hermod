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

        #region Data

        private static readonly System.Text.RegularExpressions.Regex Whitespace = new (@"\s+", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        #endregion

        #region RequestedBy(Message)

        /// <summary>
        /// Where the message asks a read receipt to be sent - its
        /// Disposition-Notification-To - or null when it asks for none.
        /// </summary>
        public static MailboxAddress? RequestedBy(MimeMessage Message)

            => MailboxAddress.ParseList(Message.Headers.Get("Disposition-Notification-To")).
                              FirstOrDefault(mailbox => MimeEncoding.IsAddress(mailbox.Address));

        #endregion

        #region (internal) Requesters(HeaderValue)

        /// <summary>
        /// The distinct addresses of a Disposition-Notification-To value, in their
        /// order: one address written twice, or in another spelling, is one (RFC 8098
        /// §2.1 compares addresses with <see cref="SameAddress"/>).
        /// </summary>
        internal static IReadOnlyList<MailboxAddress> Requesters(String? HeaderValue)

            => [ .. MailboxAddress.ParseList(HeaderValue).
                                   Where     (mailbox => mailbox.Address.Contains('@')).
                                   DistinctBy(mailbox => Canonical(mailbox.Address)) ];

        #endregion

        #region (internal) ReversePath(EnvelopeFrom)

        /// <summary>
        /// The address of an envelope's reverse-path - without angle brackets - or null
        /// for the null reverse-path (&lt;&gt;) and for one that is not known.
        /// </summary>
        internal static String? ReversePath(String? EnvelopeFrom)
        {

            var path = EnvelopeFrom?.Trim() ?? "";

            if (path.StartsWith('<') && path.EndsWith('>'))
                path = path[1..^1].Trim();

            return path.Length > 0 ? path : null;

        }

        #endregion

        #region (internal) SameAddress(A, B)

        /// <summary>
        /// Whether two addr-specs are one address as RFC 8098 §2.1 compares them: "the
        /// comparison is case sensitive for the local-part and case insensitive for the
        /// domain part", the local-part "after removing the surrounding double-quote
        /// characters, if any, as well as any escaping "\" characters".
        /// </summary>
        internal static Boolean SameAddress(String A, String B)

            => String.Equals(Canonical(A), Canonical(B), StringComparison.Ordinal);


        private static String Canonical(String Address)
        {

            var address = ReversePath(Address) ?? "";
            var at      = address.LastIndexOf('@');
            var local   = at >= 0 ? address[..at]         : address;
            var domain  = at >= 0 ? address[(at + 1)..]   : "";

            if (local.Length >= 2 && local[0] == '"' && local[^1] == '"')
            {

                var unquoted = new StringBuilder(local.Length);

                for (var i = 1; i < local.Length - 1; i++)
                {

                    if (local[i] == '\\' && i + 1 < local.Length - 1)
                        i++;

                    unquoted.Append(local[i]);

                }

                local = unquoted.ToString();

            }

            return local + "@" + domain.ToLowerInvariant();

        }

        #endregion

        #region GoesElsewhere(Message, To, EnvelopeFrom)

        /// <summary>
        /// Whether the receipt would go to another address than the one the message
        /// came from - its From: and its reverse-path - and the reader must be asked
        /// first. RFC 8098 §2.1 asks to say so: it may be somebody who wants to learn
        /// that an address is read.
        /// </summary>
        /// <remarks>
        /// True, too, when the reverse-path is not known or is the null one, and when
        /// the message asks for receipts to more than one address: "Confirmation from
        /// the user MUST be obtained (or no MDN sent) if there is no Return-Path header
        /// field in the message or if there is more than one distinct address in the
        /// Disposition-Notification-To header field". Addresses are compared "case
        /// sensitive for the local-part and case insensitive for the domain part".
        /// </remarks>
        /// <param name="Message">The message read.</param>
        /// <param name="To">Where the receipt would go - <see cref="RequestedBy"/>.</param>
        /// <param name="EnvelopeFrom">The reverse-path the message came with - its Return-Path - "" or "&lt;&gt;" for the null one, null when it is not known.</param>
        public static Boolean GoesElsewhere(MimeMessage     Message,
                                            MailboxAddress  To,
                                            String?         EnvelopeFrom)
        {

            if (ReversePath(EnvelopeFrom) is not { } reversePath)
                return true;

            if (Requesters(Message.Headers.Get("Disposition-Notification-To")).Count > 1)
                return true;

            return !SameAddress(To.Address, reversePath) ||
                   !Message.From.Any(from => SameAddress(from.Address, To.Address));

        }

        #endregion

        #region Build(Message, From, FinalRecipient, ...)

        /// <summary>
        /// The read receipt for a message the reader displayed, and chose to confirm:
        /// multipart/report; report-type=disposition-notification, with a text for
        /// people and the message/disposition-notification for programs - to every
        /// address of the message's Disposition-Notification-To.
        /// </summary>
        /// <param name="Message">The message read.</param>
        /// <param name="From">The reader: the address the message came to.</param>
        /// <param name="ReportingUA">The name of the reading program, for Reporting-UA.</param>
        /// <param name="Displayed">When it was read.</param>
        /// <param name="OriginalRecipient">The original recipient as known some other way than from the message's Original-Recipient header field - address-type ";" address, e.g. the xtext-decoded ORCPT of RFC 3461: "rfc822;alias@example.org".</param>
        /// <returns>The notification - to be sent from MAIL FROM:&lt;&gt;.</returns>
        /// <exception cref="ArgumentException">The message asks for no read receipt.</exception>
        public static MimeMessageBuilder Build(MimeMessage     Message,
                                               MailboxAddress  From,
                                               String          ReportingUA,
                                               DateTimeOffset  Displayed,
                                               String?         OriginalRecipient   = null)
        {

            // RFC 8098 §3: "The MDN MUST be addressed (in both the message header field and the
            // transport envelope) to the address(es) from the Disposition-Notification-To header
            // field" - to every one of them, now that the reader confirmed.
            var to       = Requesters(Message.Headers.Get("Disposition-Notification-To")).
                               Where(mailbox => MimeEncoding.IsAddress(mailbox.Address)).
                               ToList();

            if (to.Count == 0)
                throw new ArgumentException("The message asks for no read receipt.");

            var domain   = From.Address[(From.Address.LastIndexOf('@') + 1)..];
            var subject  = Message.Subject.Length > 0 ? Message.Subject : "(kein Betreff)";
            var when     = Displayed.ToString("dd.MM.yyyy, HH:mm", CultureInfo.InvariantCulture);

            var builder  = new MimeMessageBuilder {
                               From       = From,
                               Subject    = $"Gelesen: {subject}",
                               Date       = Displayed,
                               MessageId  = MimeEncoding.NewMessageId(domain)
                           };

            foreach (var requester in to)
                builder.To.Add(requester);

            if (Message.MessageId is { } original)
            {
                builder.InReplyTo. Add(original);
                builder.References.Add(original);
            }

            // RFC 8098 §3.1, in the order it gives them.
            var report = new StringBuilder().
                             Append($"Reporting-UA: {domain}; {ReportingUA}\r\n");

            // §3.2.3: "If there is an Original-Recipient header field in the message, or if
            // information about the original recipient is reliably available some other way, then
            // the Original-Recipient field MUST be included." Of several header fields the first,
            // the one the delivering server wrote at the top (§2.3); written only as 7-bit text,
            // what message/disposition-notification is (§3.1).
            var originalRecipient = Whitespace.Replace(OriginalRecipient ?? Message.Headers.Get("Original-Recipient") ?? "", " ").Trim();

            if (originalRecipient.Length > 0 && MimeEncoding.IsPrintableAscii(originalRecipient))
                report.Append($"Original-Recipient: {originalRecipient}\r\n");

            report.Append($"Final-Recipient: rfc822; {From.Address}\r\n");

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
