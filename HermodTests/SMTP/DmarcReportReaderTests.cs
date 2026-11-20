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


        #region I-4: the encoding of the XML

        private static Byte[] Gzipped(Byte[] Content)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
                gzip.Write(Content);
            return output.ToArray();
        }

        public static IEnumerable<TestCaseData> Encodings()
        {

            Byte[] Bom(Encoding Encoding, String Text) => [ .. Encoding.GetPreamble(), .. Encoding.GetBytes(Text) ];

            var utf16 = Report.Replace("UTF-8", "UTF-16").Replace("google.com", "Müller GmbH");

            yield return new TestCaseData(Bom(new UTF8Encoding(true), Report),                                    "google.com"). SetName("UTF-8 with a Byte Order Mark is read");
            yield return new TestCaseData(Gzipped(Bom(new UTF8Encoding(true), Report)),                           "google.com"). SetName("UTF-8 with a Byte Order Mark is read, gzipped");
            yield return new TestCaseData(Bom(Encoding.Unicode, utf16),                                           "Müller GmbH").SetName("UTF-16 little-endian is read");
            yield return new TestCaseData(Bom(Encoding.BigEndianUnicode, utf16),                                  "Müller GmbH").SetName("UTF-16 big-endian is read");
            yield return new TestCaseData(Encoding.Latin1.GetBytes(Report.Replace("UTF-8", "ISO-8859-1").Replace("google.com", "Müller GmbH")),
                                                                                                                 "Müller GmbH").SetName("ISO-8859-1 as the declaration says is read");

        }

        [TestCaseSource(nameof(Encodings))]
        [Description("I-4 - XML 1.0 §4.3.3: \"All XML processors MUST be able to read entities in both the UTF-8 and UTF-16 encodings\"; \"Entities encoded in UTF-16 MUST and entities encoded in UTF-8 MAY begin with the Byte Order Mark\" - RFC 7489 §7.2.1.1: the report \"MUST be an XML file\"")]
        public void A_report_in_the_encoding_its_octets_are_in_is_read(Byte[] Attachment, String Organization)
        {

            var report = DmarcReportReader.Unpack(Attachment) is { } xml ? DmarcReportReader.Parse(xml) : null;

            Assert.Multiple(() => {
                Assert.That(report?.Domain,            Is.EqualTo("example.org"));
                Assert.That(report?.OrganizationName,  Is.EqualTo(Organization));
            });

        }

        [Test(Description = "I-4 - XML 1.0 §4.3.3: the Byte Order Mark is no part of the document - XML text that still starts with one is read")]
        public void XML_text_starting_with_a_Byte_Order_Mark_is_read()
        {

            Assert.That(DmarcReportReader.Parse("﻿" + Report)?.Domain, Is.EqualTo("example.org"));

        }

        #endregion

        #region I-5: the date range

        [TestCase("99999999999999",       "1792108799",           TestName = "A begin beyond the year 9999 is none, and does not throw")]
        [TestCase("1792022400",           "-99999999999",         TestName = "An end before the year 1 is none, and does not throw")]
        [TestCase("1792022400",           "99999999999999999999", TestName = "An end beyond Int64 is none, and does not throw")]
        [Description("I-5 - RFC 7489 Appendix C: begin and end are xs:integer - any integer a foreign receiver writes; one outside what a DateTimeOffset holds is no time, and the report is still read")]
        public void A_date_range_out_of_range_is_none(String Begin, String End)
        {

            var xml = Report.Replace("<begin>1792022400</begin><end>1792108799</end>", $"<begin>{Begin}</begin><end>{End}</end>");

            // The times within range are the ones written.
            static DateTimeOffset? Expected(String Seconds)
                => Seconds.Length == 10 ? DateTimeOffset.FromUnixTimeSeconds(Int64.Parse(Seconds)) : null;

            DmarcReceivedReport? report = null;

            Assert.That(() => report = DmarcReportReader.Parse(xml), Throws.Nothing);
            Assert.Multiple(() => {
                Assert.That(report?.Domain, Is.EqualTo("example.org"), "the report is still read");
                Assert.That(report?.Begin,  Is.EqualTo(Expected(Begin)));
                Assert.That(report?.End,    Is.EqualTo(Expected(End)));
            });

        }

        [Test(Description = "I-5 - RFC 7489 Appendix C: the first and the last second a DateTimeOffset holds are read - a guard")]
        public void The_bounds_of_the_date_range_are_read()
        {

            var first  = DateTimeOffset.MinValue.ToUnixTimeSeconds();
            var last   = DateTimeOffset.MaxValue.ToUnixTimeSeconds();
            var report = DmarcReportReader.Parse(Report.Replace("<begin>1792022400</begin><end>1792108799</end>", $"<begin>{first}</begin><end>{last}</end>"));

            Assert.That((report?.Begin, report?.End), Is.EqualTo(((DateTimeOffset?) DateTimeOffset.FromUnixTimeSeconds(first), (DateTimeOffset?) DateTimeOffset.FromUnixTimeSeconds(last))));

        }

        #endregion

        #region I-2: how deep a report is nested

        // A report whose domain is nested the given number of levels deep.
        private static String Nested(Int32 Depth)

            => "<?xml version=\"1.0\"?><feedback><policy_published><domain>" +
               String.Concat(Enumerable.Repeat("<a>", Depth)) + "example.org" + String.Concat(Enumerable.Repeat("</a>", Depth)) +
               "</domain></policy_published></feedback>";

        // Parse on a thread with the stack of a server's worker thread on Linux: 1.5 MiB.
        private static DmarcReceivedReport? ParseOnASmallStack(String Xml)
        {

            DmarcReceivedReport? report = null;

            var thread = new Thread(() => report = DmarcReportReader.Parse(Xml), 1536 * 1024);
            thread.Start();

            Assert.That(thread.Join(TimeSpan.FromSeconds(60)), Is.True, "parsed in time");

            return report;

        }

        [TestCase(    100, TestName = "Nesting 100 deep is no report")]
        [TestCase( 50_000, TestName = "Nesting 50000 deep is no report and does not overflow the stack")]
        [Description("I-2 - RFC 7489 §7.2.1.1: aggregate reports come by mail from receivers - from strangers; a document nested deeper than the schema of Appendix C needs is no report, and reading it does not end the process")]
        public void Nesting_deeper_than_a_report_needs_is_no_report(Int32 Depth)
        {

            Assert.That(ParseOnASmallStack(Nested(Depth)), Is.Null);

        }

        [Test(Description = "I-2 - RFC 7489 Appendix C: a report nested within the limit is read - a guard")]
        public void Nesting_within_the_limit_is_read()
        {

            Assert.That(ParseOnASmallStack(Nested(20))?.Domain, Is.EqualTo("example.org"));

        }

        #endregion

    }

}
