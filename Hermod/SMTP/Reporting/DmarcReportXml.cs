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

using System.Xml;
using System.Xml.Linq;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// Serializes a <see cref="DmarcDomainReport"/> to the DMARC aggregate feedback XML
    /// (RFC 7489 Appendix C).
    /// </summary>
    public static class DmarcReportXml
    {

        public static String Build(DmarcDomainReport report,
                                   String            orgName,
                                   String            reportEmail,
                                   String            reportId)
        {

            var metadata = new XElement("report_metadata",
                new XElement("org_name", orgName),
                new XElement("email",    reportEmail),
                new XElement("report_id", reportId),
                new XElement("date_range",
                    new XElement("begin", report.WindowBegin.ToUnixTimeSeconds()),
                    new XElement("end",   report.WindowEnd.  ToUnixTimeSeconds())));

            var policyPublished = new XElement("policy_published",
                new XElement("domain", report.PolicyDomain),
                new XElement("adkim",  report.Adkim),
                new XElement("aspf",   report.Aspf),
                new XElement("p",      report.RequestedPolicy),
                report.SubdomainPolicy is not null ? new XElement("sp", report.SubdomainPolicy) : null,
                new XElement("pct",    report.Percent));

            var feedback = new XElement("feedback",
                new XElement("version", "1.0"),
                metadata,
                policyPublished);

            foreach (var row in report.Rows)
            {

                var authResults = new XElement("auth_results");

                // dkim is optional (0+); include only when a signature domain was present.
                if (!string.IsNullOrEmpty(row.DkimAuthDomain))
                    authResults.Add(new XElement("dkim",
                        new XElement("domain", row.DkimAuthDomain),
                        new XElement("result", row.DkimAuthResult)));

                // spf is required (1+); fall back to the header-from domain if MAIL FROM was empty.
                authResults.Add(new XElement("spf",
                    new XElement("domain", string.IsNullOrEmpty(row.SpfAuthDomain) ? row.HeaderFrom : row.SpfAuthDomain),
                    new XElement("result", row.SpfAuthResult)));

                feedback.Add(new XElement("record",
                    new XElement("row",
                        new XElement("source_ip", row.SourceIp),
                        new XElement("count",     row.Count),
                        new XElement("policy_evaluated",
                            new XElement("disposition", row.Disposition),
                            new XElement("dkim",        row.DkimAligned),
                            new XElement("spf",         row.SpfAligned))),
                    new XElement("identifiers",
                        new XElement("header_from", row.HeaderFrom)),
                    authResults));

            }

            var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), feedback);

            // A plain StringWriter reports UTF-16, which XmlWriter would then stamp into the
            // <?xml encoding?> declaration — but the report bytes are gzipped as UTF-8. Use a
            // writer that reports UTF-8 so the declaration matches the actual octets.
            using var sw = new Utf8StringWriter();
            using (var xw = XmlWriter.Create(sw, new XmlWriterSettings { Indent = true, Encoding = System.Text.Encoding.UTF8 }))
                doc.Save(xw);

            return sw.ToString();

        }

        private sealed class Utf8StringWriter : StringWriter
        {
            public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
        }

    }

}
