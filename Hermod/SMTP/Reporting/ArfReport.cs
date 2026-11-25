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
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// Builds a DMARC forensic (failure) report as an Abuse Reporting Format message
    /// (RFC 6591 / RFC 5965) — a multipart/report with a machine-readable
    /// message/feedback-report part and the reported message's headers. Only the headers of
    /// the offending message are included (text/rfc822-headers), not the body, to limit the
    /// exposure of private content (RFC 7489 §7.3).
    /// </summary>
    public static class ArfReport
    {

        /// <param name="authFailure">The Auth-Failure type (RFC 7489 §7.3.1, RFC 6591 §3.3) - what fo= asked for
        /// (RFC 7489 §6.3): "dmarc" for a DMARC failure report, "signature" for a DKIM one, "spf" for an SPF one.</param>
        public static String Build(DmarcEvaluation      eval,
                                   System.Net.IPAddress sourceIp,
                                   String          reportedHeaders,
                                   String          envelopeFrom,
                                   String          fromDisplayAddress,
                                   String          toAddress,
                                   String          reportingDomain,
                                   String          authenticationResults,
                                   String          authFailure = "dmarc")
        {

            var boundary  = "arf-" + UUIDv7.Generate().ToString("N");
            var messageId = $"<{UUIDv7.Generate():N}@{reportingDomain}>";
            var date      = DateTimeOffset.UtcNow.ToString("ddd, dd MMM yyyy HH:mm:ss +0000",
                                                           System.Globalization.CultureInfo.InvariantCulture);
            var kind      = authFailure switch {
                                "signature" => "DKIM",
                                "spf"       => "SPF",
                                _           => "DMARC"
                            };

            var sb = new StringBuilder();

            // Outer message headers
            sb.Append("From: ").Append(fromDisplayAddress).Append("\r\n");
            sb.Append("To: ").Append(toAddress).Append("\r\n");
            sb.Append("Subject: ").Append(kind).Append(" failure report for ").Append(eval.HeaderFromDomain).Append("\r\n");
            sb.Append("Date: ").Append(date).Append("\r\n");
            sb.Append("Message-ID: ").Append(messageId).Append("\r\n");
            sb.Append("Auto-Submitted: auto-generated\r\n");
            sb.Append("MIME-Version: 1.0\r\n");
            sb.Append("Content-Type: multipart/report; report-type=feedback-report;\r\n");
            sb.Append("\tboundary=\"").Append(boundary).Append("\"\r\n");
            sb.Append("\r\n");

            // Part 1: human-readable
            sb.Append("--").Append(boundary).Append("\r\n");
            sb.Append("Content-Type: text/plain; charset=utf-8\r\n\r\n");
            sb.Append("This is a ").Append(kind).Append(" failure report for a message received from ")
              .Append(sourceIp).Append("\r\n")
              .Append("claiming to be from the domain ").Append(eval.HeaderFromDomain).Append(".\r\n\r\n");

            // Part 2: machine-readable feedback report (RFC 6591 §3.2)
            sb.Append("--").Append(boundary).Append("\r\n");
            sb.Append("Content-Type: message/feedback-report\r\n\r\n");
            sb.Append("Feedback-Type: auth-failure\r\n");
            sb.Append("User-Agent: AchimSMTP-DMARC/1.0\r\n");
            sb.Append("Version: 1\r\n");
            // RFC 5965 §3.5: original-mail-from = "Original-Mail-From:" [CFWS] reverse-path [CFWS]
            // CRLF - RFC 5321 §4.1.2: Reverse-path = Path / "<>", the mailbox in angle brackets.
            sb.Append("Original-Mail-From: ").Append(ReversePath(envelopeFrom)).Append("\r\n");
            sb.Append("Arrival-Date: ").Append(date).Append("\r\n");
            // RFC 5965 §3.5: source-ip = "Source-IP:" [CFWS] ( IPv4-address-literal /
            // IPv6-address-literal ) [CFWS] CRLF - RFC 5321 §4.1.3: "IPv6:" before an IPv6 one.
            sb.Append("Source-IP: ").Append(AddressLiteral(sourceIp)).Append("\r\n");
            sb.Append(HeaderField.Of("Reported-Domain", eval.HeaderFromDomain));
            sb.Append("Auth-Failure: ").Append(authFailure).Append("\r\n");

            switch (authFailure)
            {

                // RFC 6591 §3.2.3: "Required for DKIM Reports" - DKIM-Domain, DKIM-Identity and
                // DKIM-Selector of "the signature that failed verification".
                case "signature":
                    if (FailedSignature(eval) is { } failed)
                        AppendDkimFields(sb, failed);
                    break;

                // RFC 6591 §3.2.6: "Required for SPF Reports" - SPF-DNS.
                case "spf":
                    if (!String.IsNullOrEmpty(eval.SpfDomain))
                        AppendSpfDns(sb, eval);
                    break;

                // A DMARC failure report.
                default:

                    // RFC 7489 §7.3.1: "Identity-Alignment (REQUIRED ...)" - "a comma-separated list of
                    // authentication mechanism names that produced an aligned identity, or the keyword
                    // "none" if none did".
                    var aligned = new List<String>();
                    if (eval.DkimAligned) aligned.Add("dkim");
                    if (eval.SpfAligned)  aligned.Add("spf");
                    sb.Append("Identity-Alignment: ").Append(aligned.Count > 0 ? String.Join(", ", aligned) : "none").Append("\r\n");

                    // "DKIM-Domain, DKIM-Identity, DKIM-Selector (REQUIRED if the message was signed by
                    // DKIM)" - of the signature DMARC evaluated: the one that holds, else the first tried.
                    var signature = eval.DkimSignatures?.FirstOrDefault(signature => signature.Result == DkimResult.Pass &&
                                                                                     signature.Domain.Equals(eval.DkimDomain, StringComparison.OrdinalIgnoreCase)) ??
                                    eval.DkimSignatures?.FirstOrDefault();
                    if (signature is not null)
                        AppendDkimFields(sb, signature);

                    // "SPF-DNS (REQUIRED)"; RFC 6591 §4: "SPF-DNS:" ( "txt" / "spf" ) ":" domain ":"
                    // quoted-string - the record SPF evaluated for the domain it checked, or "" where
                    // that domain has none.
                    if (!String.IsNullOrEmpty(eval.SpfDomain))
                        AppendSpfDns(sb, eval);
                    break;

            }

            // RFC 5965 defines no Delivered-To: field - and the report's own recipient, written
            // there, is no fact about the reported message.
            if (!string.IsNullOrEmpty(authenticationResults))
                sb.Append("Authentication-Results: ").Append(authenticationResults).Append("\r\n");
            sb.Append("\r\n");

            // Part 3: the reported message's headers only (privacy-preserving)
            sb.Append("--").Append(boundary).Append("\r\n");
            sb.Append("Content-Type: text/rfc822-headers\r\n\r\n");
            sb.Append(NormalizeHeaders(reportedHeaders));
            if (!reportedHeaders.EndsWith("\r\n", StringComparison.Ordinal))
                sb.Append("\r\n");
            sb.Append("\r\n");

            sb.Append("--").Append(boundary).Append("--\r\n");

            return sb.ToString();

        }

        /// <summary>
        /// The first DKIM signature of the message that failed evaluation - "fail" or
        /// "permerror" - if the evaluation knows its signatures (RFC 7489 §6.3, fo=d).
        /// </summary>
        public static DkimSignatureOutcome? FailedSignature(DmarcEvaluation eval)
            => eval.DkimSignatures?.FirstOrDefault(signature => signature.Result is DkimResult.Fail or DkimResult.PermError);

        // RFC 6591 §3.2.3, §4: the d=, i= and s= of a signature. DKIM-Identity is
        // [ local-part ] "@" domain-name: the i= tag, or "@" d= by default (RFC 6376 §3.5).
        private static void AppendDkimFields(StringBuilder sb, DkimSignatureOutcome signature)
        {
            sb.Append(HeaderField.Of("DKIM-Domain",   signature.Domain));
            sb.Append(HeaderField.Of("DKIM-Identity", signature.AgentOrUserIdentifier));
            if (!String.IsNullOrEmpty(signature.Selector))
                sb.Append(HeaderField.Of("DKIM-Selector", signature.Selector));
        }

        // RFC 6591 §4: spf-dns = "SPF-DNS:" [CFWS] ( "txt" / "spf" ) [CFWS] ":" [CFWS] domain
        // [CFWS] ":" [CFWS] quoted-string [CFWS] CRLF. The record is the sender's to publish, and
        // may have line breaks in it: a quoted-string has no CR and no LF but in folding (RFC
        // 5322 §3.2.4), and a line break made a field of the report of its own.
        private static void AppendSpfDns(StringBuilder sb, DmarcEvaluation eval)
        {
            var record = (eval.SpfRecord ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
            sb.Append(HeaderField.Of("SPF-DNS", $"txt : {eval.SpfDomain} : \"{record}\""));
        }

        /// <summary>
        /// A reverse-path (RFC 5321 §4.1.2): the mailbox of MAIL FROM in angle brackets, "&lt;&gt;"
        /// for the null one.
        /// </summary>
        internal static String ReversePath(String EnvelopeFrom)
            => String.IsNullOrEmpty(EnvelopeFrom) ? "<>"
             : EnvelopeFrom.StartsWith('<')        ? EnvelopeFrom
             : $"<{EnvelopeFrom}>";

        /// <summary>
        /// An address literal without its brackets (RFC 5321 §4.1.3): IPv4-address-literal =
        /// Snum 3("." Snum), IPv6-address-literal = "IPv6:" IPv6-addr. An IPv4 address mapped
        /// into IPv6 is the IPv4 address it is; a scope is no part of IPv6-addr.
        /// </summary>
        internal static String AddressLiteral(System.Net.IPAddress Address)
        {

            if (Address.IsIPv4MappedToIPv6)
                Address = Address.MapToIPv4();

            return Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                       ? "IPv6:" + new System.Net.IPAddress(Address.GetAddressBytes())
                       : Address.ToString();

        }

        // Ensure the embedded header block uses CRLF line endings.
        private static String NormalizeHeaders(String headers)
            => headers.Replace("\r\n", "\n").Replace("\n", "\r\n");

    }

}
