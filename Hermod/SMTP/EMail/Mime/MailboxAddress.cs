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
using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// A mailbox as an address header names it: the address, and the name of
    /// whoever has it, decoded - "Jörg Müller" of
    /// "=?UTF-8?Q?J=C3=B6rg_M=C3=BCller?= &lt;joerg@example.org&gt;".
    /// </summary>
    /// <param name="Name">The display name, or null when there is none.</param>
    /// <param name="Address">The addr-spec, local@domain.</param>
    public sealed record MailboxAddress(String?  Name,
                                        String   Address)
    {

        #region Data

        private static readonly Regex Whitespace = new (@"\s+", RegexOptions.CultureInvariant);

        #endregion


        #region (static) ParseList(HeaderValue)

        /// <summary>
        /// The mailboxes of an address header - From, To, Cc, Reply-To - in their
        /// order. Groups ("Team: a@x, b@y;") are flattened into their members, an
        /// empty group ("undisclosed-recipients:;") into nothing.
        /// </summary>
        /// <remarks>
        /// The splitting is <see cref="MailAddressParser"/>'s, which knows that a
        /// comma inside quotes, angle brackets or a comment separates nothing:
        /// "Müller, Jörg" &lt;j@x&gt; is one mailbox. The name comes from the display
        /// name, unquoted and with its encoded words decoded, or - in the old form
        /// "joerg@example.org (Jörg Müller)" - from the comment.
        /// </remarks>
        /// <param name="HeaderValue">The raw header value.</param>
        public static IReadOnlyList<MailboxAddress> ParseList(String? HeaderValue)
        {

            var result = new List<MailboxAddress>();

            if (String.IsNullOrWhiteSpace(HeaderValue))
                return result;

            foreach (var piece in MailAddressParser.SplitTopLevel(HeaderValue, ','))
            {

                var mailbox = piece;

                // A group's name ends at its colon, the group at its semicolon.
                var colon   = TopLevelColon(mailbox);

                if (colon >= 0)
                    mailbox = mailbox[(colon + 1)..];

                mailbox = mailbox.Trim().TrimEnd(';').Trim();

                if (mailbox.Length == 0)
                    continue;

                if (Parse(mailbox) is { } address)
                    result.Add(address);

            }

            return result;

        }

        #endregion

        #region (static) Parse(Mailbox)

        /// <summary>
        /// One mailbox: name-addr ("Name &lt;a@b&gt;") or a bare addr-spec, or null when
        /// there is no address in it.
        /// </summary>
        public static MailboxAddress? Parse(String Mailbox)
        {

            var lt = LastTopLevel(Mailbox, '<');

            if (lt >= 0)
            {

                var gt       = Mailbox.IndexOf('>', lt + 1);
                var address  = (gt > lt ? Mailbox[(lt + 1)..gt] : Mailbox[(lt + 1)..]).Trim();

                // A source route ("<@relay:joe@example.org>") is obsolete and means
                // only the part after the colon.
                if (address.StartsWith('@') && address.IndexOf(':') is var routeEnd and > 0)
                    address = address[(routeEnd + 1)..];

                if (address.Length == 0)
                    return null;

                return new MailboxAddress(DisplayName(Mailbox[..lt]), address);

            }

            var comment  = FirstComment(Mailbox);
            var bare     = MailAddressParser.StripComments(Mailbox).Trim();

            if (!bare.Contains('@'))
                return null;

            return new MailboxAddress(comment is not null ? DisplayName(comment) : null, bare);

        }

        #endregion


        #region (private static) DisplayName(Phrase)

        /// <summary>
        /// A phrase as a person reads it: quotes and their escapes gone, encoded
        /// words decoded, comments out, white space collapsed - or null when nothing
        /// is left.
        /// </summary>
        private static String? DisplayName(String Phrase)
        {

            var unquoted = new StringBuilder(Phrase.Length);
            var inQuote  = false;

            for (var i = 0; i < Phrase.Length; i++)
            {

                var c = Phrase[i];

                // Inside quotes a backslash makes the next character itself.
                if (inQuote && c == '\\' && i + 1 < Phrase.Length)
                    unquoted.Append(Phrase[++i]);

                else if (c == '"')
                    inQuote = !inQuote;

                else
                    unquoted.Append(c);

            }

            // Encoded words are decoded inside quotes too: RFC 2047 §5 forbids them
            // there, and mail programs write them there anyway.
            var name = Whitespace.Replace(EncodedWords.Decode(MailAddressParser.StripComments(unquoted.ToString())), " ").Trim();

            return name.Length > 0
                       ? name
                       : null;

        }

        #endregion

        #region (private static) TopLevelColon(Text)

        /// <summary>
        /// The colon that ends a group's name: the first one outside quotes, angle
        /// brackets, domain literals and comments - "a@x (note: hi)" is no group.
        /// </summary>
        private static Int32 TopLevelColon(String Text)
        {

            var inQuote = false;
            Int32 angle = 0, bracket = 0, paren = 0;

            for (var i = 0; i < Text.Length; i++)
            {

                var c = Text[i];

                if (inQuote)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"')  inQuote = false;
                    continue;
                }

                switch (c)
                {
                    case '"': inQuote = true;              break;
                    case '<': angle++;                     break;
                    case '>': if (angle   > 0) angle--;    break;
                    case '[': bracket++;                   break;
                    case ']': if (bracket > 0) bracket--;  break;
                    case '(': paren++;                     break;
                    case ')': if (paren   > 0) paren--;    break;
                    case ':': if (angle == 0 && bracket == 0 && paren == 0) return i; break;
                }

            }

            return -1;

        }

        #endregion

        #region (private static) FirstComment(Text) / LastTopLevel(Text, Character)

        private static String? FirstComment(String Text)
        {

            var start = -1;
            var depth = 0;

            for (var i = 0; i < Text.Length; i++)
            {
                if (Text[i] == '(' && depth++ == 0)
                    start = i + 1;
                else if (Text[i] == ')' && depth > 0 && --depth == 0)
                    return Text[start..i];
            }

            return null;

        }

        private static Int32 LastTopLevel(String Text, Char Character)
        {

            var found   = -1;
            var inQuote = false;

            for (var i = 0; i < Text.Length; i++)
            {

                var c = Text[i];

                if (inQuote)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"')  inQuote = false;
                    continue;
                }

                if (c == '"')
                    inQuote = true;

                else if (c == Character)
                    found = i;

            }

            return found;

        }

        #endregion


        #region (override) ToString()

        public override String ToString()

            => Name is null
                   ? Address
                   : $"{Name} <{Address}>";

        #endregion

    }

}
