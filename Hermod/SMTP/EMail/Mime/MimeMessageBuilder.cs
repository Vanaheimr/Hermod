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

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// A mail to be written: its header and its body - a <see cref="MimeEntity"/> -
    /// as octets, and who it goes to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The counterpart of <see cref="MimeMessage"/>, and what the older
    /// EMail builders are not: headers in a fixed order, non-ASCII headers as
    /// encoded words and folded, a message id in angle brackets made when none was
    /// given, the date as RFC 5322 writes it, and no Bcc: header - the blind
    /// copies are envelope recipients only (<see cref="Recipients"/>).
    /// </para>
    /// <para>
    /// A header value with a line break in it is refused, wherever it came from:
    /// that is how a header of one's own - a second From:, a Bcc: - would be
    /// smuggled in.
    /// </para>
    /// </remarks>
    public sealed class MimeMessageBuilder
    {

        #region Properties

        /// <summary>
        /// Who the mail is from.
        /// </summary>
        public MailboxAddress?                      From        { get; set; }

        /// <summary>
        /// Who sent it on their behalf, or null.
        /// </summary>
        public MailboxAddress?                      Sender      { get; set; }

        public List<MailboxAddress>                 To          { get; } = [];
        public List<MailboxAddress>                 Cc          { get; } = [];

        /// <summary>
        /// The blind copies: envelope recipients, never written into the header.
        /// </summary>
        public List<MailboxAddress>                 Bcc         { get; } = [];

        public List<MailboxAddress>                 ReplyTo     { get; } = [];

        public String                               Subject     { get; set; } = "";

        public DateTimeOffset                       Date        { get; set; } = DateTimeOffset.Now;

        /// <summary>
        /// The message id, without its angle brackets - made from the domain of From
        /// when the mail is written without one.
        /// </summary>
        public String?                              MessageId   { get; set; }

        /// <summary>
        /// The message ids this one answers, without their angle brackets.
        /// </summary>
        public List<String>                         InReplyTo   { get; } = [];

        /// <summary>
        /// The message ids of the thread, without their angle brackets.
        /// </summary>
        public List<String>                         References  { get; } = [];

        /// <summary>
        /// Further headers - User-Agent, Autocrypt, List-Unsubscribe - written after
        /// the others, in their order; free text, encoded where it is not ASCII.
        /// </summary>
        public List<(String Name, String Value)>    Headers     { get; } = [];

        /// <summary>
        /// The body: text, a multipart, a PGP/MIME envelope.
        /// </summary>
        public MimeEntity?                          Body        { get; set; }

        #endregion


        #region Recipients

        /// <summary>
        /// Everybody the mail goes to - To, Cc and Bcc - each address once, in that
        /// order: the RCPT TO of its envelope.
        /// </summary>
        public IReadOnlyList<String> Recipients

            => [ .. To.Concat(Cc).Concat(Bcc).
                       Select(mailbox => mailbox.Address).
                       DistinctBy(address => address.ToLowerInvariant()) ];

        #endregion


        #region ToBytes()

        /// <summary>
        /// The mail as octets, CRLF and all, ending with a line break.
        /// </summary>
        /// <exception cref="InvalidOperationException">It has no From or no body.</exception>
        /// <exception cref="ArgumentException">An address cannot be written, or a header value holds a line break.</exception>
        public Byte[] ToBytes()
        {

            if (From is null)
                throw new InvalidOperationException("A mail without From is none.");

            if (Body is null)
                throw new InvalidOperationException("A mail without a body is none.");

            MessageId ??= MimeEncoding.NewMessageId(From.Address[(From.Address.LastIndexOf('@') + 1)..]);

            using var output = new MemoryStream();

            void Line(String line)
                => MimeEntity.WriteLine(output, line);

            Line($"Date: {MimeEncoding.Date(Date)}");
            Line(MimeEncoding.MailboxListHeader("From", [ From ]));

            if (Sender is not null)
                Line(MimeEncoding.MailboxListHeader("Sender", [ Sender ]));

            if (ReplyTo.Count > 0)
                Line(MimeEncoding.MailboxListHeader("Reply-To", ReplyTo));

            // Nobody but blind copies: the header says so without naming them (RFC 5322 §3.6.3).
            if (To.Count > 0)
                Line(MimeEncoding.MailboxListHeader("To", To));
            else if (Cc.Count == 0)
                Line("To: undisclosed-recipients:;");

            if (Cc.Count > 0)
                Line(MimeEncoding.MailboxListHeader("Cc", Cc));

            Line(MimeEncoding.UnstructuredHeader("Subject", Subject));
            Line($"Message-ID: {Id(MessageId)}");

            if (InReplyTo.Count > 0)
                Line(MimeEncoding.Fold("In-Reply-To", InReplyTo.Select(id => (Id(id), false))));

            if (References.Count > 0)
                Line(MimeEncoding.Fold("References", References.Select(id => (Id(id), false))));

            foreach (var (name, value) in Headers)
            {
                if (!IsFieldName(name))
                    throw new ArgumentException($"'{name}' is no header name.");
                Line(MimeEncoding.UnstructuredHeader(name, value));
            }

            Line("MIME-Version: 1.0");

            Body.Write(output);

            return output.ToArray();

        }

        #endregion


        #region (private static) Id(MessageId) / IsFieldName(Name)

        private static String Id(String MessageId)
        {

            var id = MessageId.Trim().TrimStart('<').TrimEnd('>');

            if (id.Length == 0 || !id.Contains('@') || id.Any(character => character is <= ' ' or > '~' or '<' or '>'))
                throw new ArgumentException($"'{MessageId}' is no message id.");

            return $"<{id}>";

        }

        private static Boolean IsFieldName(String Name)

            => Name.Length > 0 && Name.All(character => character is > ' ' and <= '~' and not ':');

        #endregion

    }

}
