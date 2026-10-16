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

using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Hermod.Mail;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// What DKIM signing asks of a message before it signs: which domain the
    /// message is from - the domain DMARC aligns a signature with (RFC 7489 §3.1) -
    /// and whether that domain has signed it already.
    /// </summary>
    public static partial class DkimSigning
    {

        [GeneratedRegex(@"(?:^|;)\s*d\s*=\s*([^;\s]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex DomainTag();

        #region FromDomain(Message)

        /// <summary>
        /// The domain of the From: address of the message, lower case, or null when
        /// it has none - or more than one, which DMARC does not align.
        /// </summary>
        public static String? FromDomain(String Message)
        {

            var (header, _) = DkimCanonicalization.Split(Message);

            var from = DkimCanonicalization.ParseFields(header).
                           Where(field => field.Name.Equals("From", StringComparison.OrdinalIgnoreCase)).
                           ToList();

            if (from.Count != 1)
                return null;

            var mailboxes = MailboxAddress.ParseList(from[0].RawValue);

            return mailboxes.Count == 1 && mailboxes[0].Address.LastIndexOf('@') is var at and > 0
                       ? mailboxes[0].Address[(at + 1)..].TrimEnd('.').ToLowerInvariant()
                       : null;

        }

        #endregion

        #region IsSignedBy(Message, Domain)

        /// <summary>
        /// Whether the message carries a DKIM-Signature of the given domain (d=) -
        /// whether it is valid is not asked here.
        /// </summary>
        public static Boolean IsSignedBy(String Message, String Domain)
        {

            var (header, _) = DkimCanonicalization.Split(Message);

            return DkimCanonicalization.ParseFields(header).
                       Where (field => field.Name.Equals("DKIM-Signature", StringComparison.OrdinalIgnoreCase)).
                       Select(field => DomainTag().Match(field.RawValue.Replace("\r\n", "").Replace("\n", ""))).
                       Any   (match => match.Success && match.Groups[1].Value.TrimEnd('.').Equals(Domain.TrimEnd('.'), StringComparison.OrdinalIgnoreCase));

        }

        #endregion

    }

}
