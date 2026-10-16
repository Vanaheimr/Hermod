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

using System.IO.Compression;
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// DMARC aggregate reports as receivers send them (RFC 7489 Appendix C): read
    /// as XML, gzipped or zipped, in the namespace of DMARCbis too - and what
    /// Hermod writes read back. A report with a DTD is no report.
    /// </summary>
    [TestFixture]
    public class DmarcReportReaderTests
    {

        // As a large receiver sends one: two sources, one of them failing.
        private const String Report = """
            <?xml version="1.0" encoding="UTF-8" ?>
            <feedback>
              <report_metadata>
                <org_name>google.com</org_name>
                <email>noreply-dmarc-support@google.com</email>
                <report_id>15243937426398513762</report_id>
                <date_range><begin>1792022400</begin><end>1792108799</end></date_range>
              </report_metadata>
              <policy_published>
                <domain>Example.org</domain><adkim>r</adkim><aspf>r</aspf><p>quarantine</p><sp>reject</sp><pct>100</pct>
              </policy_published>
              <record>
                <row>
                  <source_ip>203.0.113.25</source_ip><count>41</count>
                  <policy_evaluated><disposition>none</disposition><dkim>pass</dkim><spf>pass</spf></policy_evaluated>
                </row>
                <identifiers><header_from>example.org</header_from></identifiers>
                <auth_results>
                  <dkim><domain>example.org</domain><result>pass</result><selector>mail</selector></dkim>
                  <spf><domain>example.org</domain><result>pass</result></spf>
                </auth_results>
              </record>
              <record>
                <row>
                  <source_ip>198.51.100.7</source_ip><count>3</count>
                  <policy_evaluated><disposition>quarantine</disposition><dkim>fail</dkim><spf>fail</spf></policy_evaluated>
                </row>
                <identifiers><header_from>example.org</header_from></identifiers>
                <auth_results>
                  <spf><domain>spammer.example</domain><result>softfail</result></spf>
                </auth_results>
              </record>
            </feedback>
            """;


        [Test]
        public void A_report_says_who_sent_how_much_in_the_name_of_the_domain_and_how_it_fared()
        {

            var report = DmarcReportReader.Parse(Report)!;

            Assert.Multiple(() => {

                Assert.That(report.OrganizationName,  Is.EqualTo("google.com"));
                Assert.That(report.ReportId,          Is.EqualTo("15243937426398513762"));
                Assert.That(report.Begin,             Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1792022400)));
                Assert.That(report.Domain,            Is.EqualTo("example.org"), "lower case");
                Assert.That(report.Policy,            Is.EqualTo("quarantine"));
                Assert.That(report.SubdomainPolicy,   Is.EqualTo("reject"));
                Assert.That(report.Percent,           Is.EqualTo(100));

                Assert.That(report.Total,             Is.EqualTo(44));
                Assert.That(report.Passed,            Is.EqualTo(41));

                var failing = report.Records[1];
                Assert.That(failing.SourceIp,         Is.EqualTo("198.51.100.7"));
                Assert.That(failing.Disposition,      Is.EqualTo("quarantine"));
                Assert.That(failing.Passed,           Is.False);
                Assert.That(failing.DkimResults,      Is.Empty);
                Assert.That(failing.SpfResults.Single(), Is.EqualTo(new DmarcReceivedAuth("spammer.example", "softfail", null)));

                Assert.That(report.Records[0].DkimResults.Single().Selector, Is.EqualTo("mail"));

            });

        }

        [Test]
        public void A_report_is_read_as_XML_gzipped_or_zipped()
        {

            var xml      = Encoding.UTF8.GetBytes(Report);

            using var gzipped = new MemoryStream();
            using (var gzip = new GZipStream(gzipped, CompressionLevel.Optimal, leaveOpen: true))
                gzip.Write(xml);

            using var zipped = new MemoryStream();
            using (var zip = new ZipArchive(zipped, ZipArchiveMode.Create, leaveOpen: true))
                using (var entry = zip.CreateEntry("google.com!example.org!1792022400!1792108799.xml").Open())
                    entry.Write(xml);

            Assert.Multiple(() => {
                Assert.That(DmarcReportReader.Unpack(xml),                 Is.EqualTo(Report));
                Assert.That(DmarcReportReader.Unpack(gzipped.ToArray()),   Is.EqualTo(Report));
                Assert.That(DmarcReportReader.Unpack(zipped.ToArray()),    Is.EqualTo(Report));
                Assert.That(DmarcReportReader.Unpack("%PDF-1.7"u8),        Is.Null, "no report");
                Assert.That(DmarcReportReader.Unpack([ 0x1F, 0x8B, 0 ]),   Is.Null, "a broken gzip is no report either");
            });

        }

        [Test]
        public void What_Hermod_writes_is_read_back_and_DMARCbis_too()
        {

            var written = DmarcReportXml.Build(new DmarcDomainReport("example.org", "mailto:dmarc@example.org", "reject", null, 100, "s", "r",
                                                                     DateTimeOffset.FromUnixTimeSeconds(1792022400), DateTimeOffset.FromUnixTimeSeconds(1792108799),
                                                                     [ new DmarcReportRow("192.0.2.1", 5, "none", "pass", "fail", "example.org", "example.org", "pass", "bounce.example.org", "pass") ]),
                                               "mx.hermod.test", "dmarc-reports@hermod.test", "r-1");

            var read    = DmarcReportReader.Parse(written)!;
            var bis     = DmarcReportReader.Parse(Report.Replace("<feedback>", "<feedback xmlns=\"urn:ietf:params:xml:ns:dmarc-2.0\">"))!;

            Assert.Multiple(() => {

                Assert.That(read.OrganizationName,                 Is.EqualTo("mx.hermod.test"));
                Assert.That(read.Adkim,                            Is.EqualTo("s"));
                Assert.That(read.Records.Single().Count,           Is.EqualTo(5));
                Assert.That(read.Records.Single().Passed,          Is.True, "aligned DKIM passed");
                Assert.That(read.Records.Single().SpfResults.Single().Domain, Is.EqualTo("bounce.example.org"));

                Assert.That(bis.Total,                             Is.EqualTo(44), "in the namespace of DMARCbis");

            });

        }

        [Test]
        public void A_report_with_a_DTD_or_of_something_else_is_none()
        {

            var withDtd = "<?xml version=\"1.0\"?><!DOCTYPE feedback [<!ENTITY x SYSTEM \"file:///etc/passwd\">]>" +
                          "<feedback><policy_published><domain>&x;</domain></policy_published></feedback>";

            Assert.Multiple(() => {
                Assert.That(DmarcReportReader.Parse(withDtd),                         Is.Null, "no entity is resolved");
                Assert.That(DmarcReportReader.Parse("<html><body/></html>"),          Is.Null);
                Assert.That(DmarcReportReader.Parse("<feedback/>"),                   Is.Null, "no domain");
                Assert.That(DmarcReportReader.Parse("not XML"),                       Is.Null);
            });

        }

    }

}
