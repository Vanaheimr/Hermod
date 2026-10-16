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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// Whom a reply goes to.
    /// </summary>
    public enum ReplyKind
    {

        /// <summary>
        /// Whoever wrote it - Reply-To, or else From.
        /// </summary>
        Sender,

        /// <summary>
        /// Whoever wrote it, and everybody else it went to.
        /// </summary>
        All,

        /// <summary>
        /// The mailing list it came through (List-Post).
        /// </summary>
        List

    }

    /// <summary>
    /// Who a reply goes to.
    /// </summary>
    public sealed record ReplyAddressing(IReadOnlyList<MailboxAddress>  To,
                                         IReadOnlyList<MailboxAddress>  Cc);


    /// <summary>
    /// What an answer or a forward takes from the message it answers: a subject
    /// with one "Re:" or "Fwd:", the thread (In-Reply-To, References - RFC 5322
    /// §3.6.4), whom it goes to, and the text it quotes.
    /// </summary>
    public static partial class MailReplies
    {

        #region Data

        // What mail programs put before a subject, in the languages they are most
        // often set to: Re, Aw (de), Sv (da, no, sv), Antw (nl), Fwd, Fw, Wg (de),
        // Tr (fr), Rv (es), Doorst (nl) - with a count, "Re[2]:", or a list tag.
        [GeneratedRegex(@"^\s*(?:(?:re|aw|sv|antw|fwd?|wg|tr|rv|doorst)\s*(?:\[\d+\]|\(\d+\))?\s*:\s*)+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex Prefixes();

        [GeneratedRegex(@"<mailto:([^>?]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex MailtoTarget();

        /// <summary>
        /// How many message ids References keeps: the first, which names the thread,
        /// and the newest - enough for every mail program to find its place.
        /// </summary>
        public const Int32 MaxReferences = 20;

        #endregion


        #region BaseSubject(Subject) / ReplySubject(Subject) / ForwardSubject(Subject)

        /// <summary>
        /// The subject without what mail programs put before it - "Re: AW: Fwd:".
        /// </summary>
        public static String BaseSubject(String Subject)
            => Prefixes().Replace(Subject, "").Trim();

        /// <summary>
        /// The subject of an answer: one "Re:" before what it was about.
        /// </summary>
        public static String ReplySubject(String Subject)
            => "Re: " + BaseSubject(Subject);

        /// <summary>
        /// The subject of a forward: one "Fwd:" before what it was about.
        /// </summary>
        public static String ForwardSubject(String Subject)
            => "Fwd: " + BaseSubject(Subject);

        #endregion

        #region InReplyTo(Original) / References(Original)

        /// <summary>
        /// The message id an answer names in In-Reply-To - none for a message that has none.
        /// </summary>
        public static IReadOnlyList<String> InReplyTo(MimeMessage Original)
            => Original.MessageId is { } id ? [ id ] : [];

        /// <summary>
        /// The References of an answer: those of the message it answers, and that
        /// message - the first and the newest when they are too many.
        /// </summary>
        public static IReadOnlyList<String> References(MimeMessage Original)
        {

            var thread = Original.References.
                             Concat(Original.MessageId is { } id ? [ id ] : []).
                             Distinct().
                             ToList();

            return thread.Count <= MaxReferences
                       ? thread
                       : [ thread[0], .. thread.TakeLast(MaxReferences - 1) ];

        }

        #endregion

        #region ListPost(Original)

        /// <summary>
        /// The address a list takes posts at - the mailto: of List-Post (RFC 2369) -
        /// or null when it takes none ("NO") or names no list.
        /// </summary>
        public static String? ListPost(MimeMessage Original)
        {

            var match = MailtoTarget().Match(Original.Root.Headers.Get("List-Post") ?? "");

            return match.Success && MimeEncoding.IsAddress(Uri.UnescapeDataString(match.Groups[1].Value))
                       ? Uri.UnescapeDataString(match.Groups[1].Value)
                       : null;

        }

        #endregion

        #region Addressing(Original, Kind, OwnAddresses)

        /// <summary>
        /// Whom an answer goes to.
        /// </summary>
        /// <remarks>
        /// <para>
        /// To the sender: Reply-To when the message has one, else From - or, for a
        /// message one sent oneself, whom it went to.
        /// </para>
        /// <para>
        /// To all: that, and everybody else in To and Cc, but none of one's own
        /// addresses and nobody twice: those in To stay in To, those in Cc in Cc.
        /// </para>
        /// <para>
        /// To the list: its List-Post address - or, when there is none, the sender.
        /// </para>
        /// </remarks>
        /// <param name="OwnAddresses">The addresses of whoever answers - left out of the recipients.</param>
        public static ReplyAddressing Addressing(MimeMessage          Original,
                                                 ReplyKind            Kind,
                                                 IEnumerable<String>  OwnAddresses)
        {

            var own    = new HashSet<String>(OwnAddresses.Select(address => address.ToLowerInvariant()));
            var mine   = Original.From.Any(from => own.Contains(from.Address.ToLowerInvariant()));

            if (Kind == ReplyKind.List && ListPost(Original) is { } list)
                return new ReplyAddressing([ new MailboxAddress(null, list) ], []);

            var sender = mine
                             ? Original.To
                             : Original.ReplyTo.Count > 0 ? Original.ReplyTo : Original.From;

            // One's own addresses are seen already - except, in an answer to one's own
            // message, those it went to: a note to oneself is answered to oneself.
            var seen   = new HashSet<String>(mine ? [] : own);
            var to     = Distinct(sender, seen);

            seen.UnionWith(own);

            if (Kind != ReplyKind.All)
                return new ReplyAddressing(to, []);

            return new ReplyAddressing(
                       [ .. to, .. Distinct(Original.To, seen) ],
                       Distinct(Original.Cc, seen)
                   );

        }

        /// <summary>
        /// The mailboxes whose address is not seen yet - each then seen.
        /// </summary>
        private static List<MailboxAddress> Distinct(IEnumerable<MailboxAddress>  Mailboxes,
                                                     HashSet<String>              Seen)

            => [ .. Mailboxes.Where(mailbox => Seen.Add(mailbox.Address.ToLowerInvariant())) ];

        #endregion

        #region Quote(Text)

        /// <summary>
        /// Text as an answer quotes it: "> " before every line, ">" before a line
        /// that is a quote already (RFC 3676 §4.5), with the white space at the end
        /// of the text taken away.
        /// </summary>
        public static String Quote(String Text)
        {

            var quoted = new StringBuilder();

            foreach (var line in Text.TrimEnd().Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
                quoted.Append(line.StartsWith('>') ? ">" : line.Length == 0 ? ">" : "> ").Append(line.TrimEnd()).Append('\n');

            return quoted.ToString();

        }

        #endregion

    }

}
