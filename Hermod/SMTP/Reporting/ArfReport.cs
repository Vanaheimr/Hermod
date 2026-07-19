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

    /// <summary>
    /// Builds a DMARC forensic (failure) report as an Abuse Reporting Format message
    /// (RFC 6591 / RFC 5965) — a multipart/report with a machine-readable
    /// message/feedback-report part and the reported message's headers. Only the headers of
    /// the offending message are included (text/rfc822-headers), not the body, to limit the
    /// exposure of private content (RFC 7489 §7.3).
    /// </summary>
    public static class ArfReport
    {

        public static String Build(DmarcEvaluation      eval,
                                   System.Net.IPAddress sourceIp,
                                   String          reportedHeaders,
                                   String          envelopeFrom,
                                   String          fromDisplayAddress,
                                   String          toAddress,
                                   String          reportingDomain,
                                   String          authenticationResults)
        {

            var boundary  = "arf-" + Guid.NewGuid().ToString("N");
            var messageId = $"<{Guid.NewGuid():N}@{reportingDomain}>";
            var date      = DateTimeOffset.UtcNow.ToString("ddd, dd MMM yyyy HH:mm:ss +0000",
                                                           System.Globalization.CultureInfo.InvariantCulture);

            var sb = new StringBuilder();

            // Outer message headers
            sb.Append("From: ").Append(fromDisplayAddress).Append("\r\n");
            sb.Append("To: ").Append(toAddress).Append("\r\n");
            sb.Append("Subject: DMARC failure report for ").Append(eval.HeaderFromDomain).Append("\r\n");
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
            sb.Append("This is a DMARC failure report for a message received from ")
              .Append(sourceIp).Append("\r\n")
              .Append("claiming to be from the domain ").Append(eval.HeaderFromDomain).Append(".\r\n\r\n");

            // Part 2: machine-readable feedback report (RFC 6591 §3.2)
            sb.Append("--").Append(boundary).Append("\r\n");
            sb.Append("Content-Type: message/feedback-report\r\n\r\n");
            sb.Append("Feedback-Type: auth-failure\r\n");
            sb.Append("User-Agent: AchimSMTP-DMARC/1.0\r\n");
            sb.Append("Version: 1\r\n");
            sb.Append("Original-Mail-From: ").Append(string.IsNullOrEmpty(envelopeFrom) ? "<>" : envelopeFrom).Append("\r\n");
            sb.Append("Arrival-Date: ").Append(date).Append("\r\n");
            sb.Append("Source-IP: ").Append(sourceIp).Append("\r\n");
            sb.Append("Reported-Domain: ").Append(eval.HeaderFromDomain).Append("\r\n");
            sb.Append("Auth-Failure: dmarc\r\n");
            sb.Append("Delivered-To: ").Append(toAddress).Append("\r\n");
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

        // Ensure the embedded header block uses CRLF line endings.
        private static String NormalizeHeaders(String headers)
            => headers.Replace("\r\n", "\n").Replace("\n", "\r\n");

    }

}
