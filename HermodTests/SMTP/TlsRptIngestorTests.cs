/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.SMTP;
using org.GraphDefined.Vanaheimr.Hermod.SMTP.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// TLS reports other sending MTAs deliver (RFC 8460 §5.3), as <see cref="TlsRptIngestor"/>
    /// reads them: the mail of §5.3.1, its MIME (RFC 2045, RFC 2046, RFC 2049) - and what a
    /// stranger may send instead. The findings I-1, I-3 and I-7 of SMTPConformanceTests.
    /// </summary>
    [TestFixture]
    public class TlsRptIngestorTests
    {

        #region Setup

        private sealed class CapturingLogger : ILogger
        {

            public ConcurrentQueue<String> Lines { get; } = new();

            public void Log(LogLevel level, String message)
                => Lines.Enqueue(message);

        }

        private String          directory = "";
        private CapturingLogger log       = new();

        [SetUp]
        public void CreateDirectory()
        {
            directory = Directory.CreateTempSubdirectory("tlsrpt-received-").FullName;
            log       = new CapturingLogger();
        }

        [TearDown]
        public void DeleteDirectory()
        {
            try { Directory.Delete(directory, recursive: true); } catch { }
        }

        /// <summary>
        /// The JSON of RFC 8460 §4.4, with the values of the example in Appendix B.
        /// </summary>
        private static String Json(String Organization)

            => $$"""
                 {"organization-name":"{{Organization}}","date-range":{"start-datetime":"2016-04-01T00:00:00Z","end-datetime":"2016-04-01T23:59:59Z"},"contact-info":"sts-reporting@company-x.example","report-id":"5065427c-23d3-47ca-b6e0-946ea0e8c4be","policies":[{"policy":{"policy-type":"sts","policy-string":["version: STSv1","mode: testing","mx: *.mail.company-y.example","max_age: 86400"],"policy-domain":"company-y.example","mx-host":["*.mail.company-y.example"]},"summary":{"total-successful-session-count":5326,"total-failure-session-count":303},"failure-details":[{"result-type":"certificate-expired","sending-mta-ip":"2001:db8:abcd:0012::1","receiving-mx-hostname":"mx1.mail.company-y.example","failed-session-count":100},{"result-type":"starttls-not-supported","sending-mta-ip":"2001:db8:abcd:0013::1","receiving-mx-hostname":"mx2.mail.company-y.example","receiving-ip":"203.0.113.56","failed-session-count":200,"additional-information":"https://reports.company-x.example/report_info?id=5065427c-23d3#StarttlsNotSupported"},{"result-type":"validation-failure","sending-mta-ip":"198.51.100.62","receiving-ip":"203.0.113.58","receiving-mx-hostname":"mx-backup.mail.company-y.example","failed-session-count":3,"failure-reason-code":"X509_V_ERR_PROXY_PATH_LENGTH_EXCEEDED"}]}]}
                 """;

        /// <summary>
        /// The report mail of RFC 8460 §5.3.1: multipart/report, report-type="tlsrpt", a
        /// text/plain part and the report part - with the preamble, text and epilogue given.
        /// </summary>
        private static EMailMessage Mail(String  ReportPart,
                                         String  Preamble  = "This is a multipart message in MIME format.\r\n",
                                         String  Text      = "This is an aggregate TLS report from mail.sender.example\r\n",
                                         String  Epilogue  = "")

            => EMailMessage.Parse("From: tlsrpt@mail.sender.example\r\n" +
                                  "To: tlsrpt@hermod.test\r\n" +
                                  "Subject: Report Domain: hermod.test\r\n" +
                                  "TLS-Report-Domain: hermod.test\r\n" +
                                  "MIME-Version: 1.0\r\n" +
                                  "Content-Type: multipart/report; report-type=\"tlsrpt\";\r\n" +
                                  "    boundary=\"----=_NextPart_000_024E_01CC9B0A.AFE54C00\"\r\n" +
                                  "\r\n" +
                                  Preamble +
                                  "\r\n" +
                                  "------=_NextPart_000_024E_01CC9B0A.AFE54C00\r\n" +
                                  "Content-Type: text/plain; charset=\"us-ascii\"\r\n" +
                                  "\r\n" +
                                  Text +
                                  "\r\n" +
                                  "------=_NextPart_000_024E_01CC9B0A.AFE54C00\r\n" +
                                  ReportPart + "\r\n" +
                                  "------=_NextPart_000_024E_01CC9B0A.AFE54C00--\r\n" +
                                  Epilogue);

        private static String GzipPart(Byte[] Gzipped)

            => "Content-Type: application/tlsrpt+gzip\r\n" +
               "Content-Transfer-Encoding: base64\r\n" +
               "\r\n" +
               Convert.ToBase64String(Gzipped, Base64FormattingOptions.InsertLineBreaks) + "\r\n";

        private static Byte[] Gzip(String Text)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
                gzip.Write(Encoding.UTF8.GetBytes(Text));
            return output.ToArray();
        }

        /// <summary>
        /// A report whose JSON is padded with spaces to <paramref name="MiB"/> MiB - written
        /// straight into the compressor, never held whole.
        /// </summary>
        private static Byte[] Bomb(Int32 MiB)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                gzip.Write("{\"organization-name\":\"bomb\""u8);
                var spaces = new Byte[1024 * 1024];
                Array.Fill(spaces, (Byte) ' ');
                for (var i = 0; i < MiB; i++)
                    gzip.Write(spaces);
                gzip.Write("}"u8);
            }
            return output.ToArray();
        }

        /// <summary>
        /// Quoted-printable (RFC 2045 §6.7): the UTF-8 octets, "=" and those above 126 as =XX,
        /// in lines of at most 76 characters ending in a soft line break "=".
        /// </summary>
        private static String QuotedPrintable(String Text)
        {

            var output = new StringBuilder();
            var line   = 0;

            foreach (var octet in Encoding.UTF8.GetBytes(Text))
            {
                var encoded = octet == '=' || octet > 126 ? $"={octet:X2}" : ((Char) octet).ToString();
                if (line + encoded.Length > 75)
                {
                    output.Append("=\r\n");
                    line = 0;
                }
                output.Append(encoded);
                line += encoded.Length;
            }

            return output.ToString();

        }

        private TlsRptReceivedReport? Ingest(EMailMessage Message)
            => new TlsRptIngestor(directory, log).Ingest(Message);

        private String[] Kept
            => Directory.GetFiles(directory);

        #endregion


        #region I-1: what a report may unpack to

        [Test(Description = "I-1 - RFC 8460 §6.5: \"Security considerations related to gzip compression are discussed in RFC 6713\"; RFC 6713 §4: \"Applications should treat any data with due skepticism\" - a report that unpacks to 128 MiB is unpacked no further than allowed, and neither read nor kept")]
        public async Task A_gzip_bomb_is_not_unpacked_whole_nor_kept()
        {

            var message = Mail(GzipPart(Bomb(128)));

            var (report, allocated) = await Task.Run(() => {
                                                  var before = GC.GetAllocatedBytesForCurrentThread();
                                                  var report = Ingest(message);
                                                  return (report, GC.GetAllocatedBytesForCurrentThread() - before);
                                              }).WaitAsync(TimeSpan.FromSeconds(60));

            Assert.Multiple(() => {
                Assert.That(report,     Is.Null,                         "no report");
                Assert.That(allocated,  Is.LessThan(64L * 1024 * 1024),  $"{allocated / 1048576} MiB allocated");
                Assert.That(Kept,       Is.Empty,                        "nothing kept");
                Assert.That(log.Lines,  Has.Some.Contains("larger than"));
            });

        }


        [Test(Description = "I-1 - RFC 8460 §5.3: a report within the limit is read and kept - a guard")]
        public void A_report_within_the_limit_is_read_and_kept()
        {

            var report = Ingest(Mail(GzipPart(Gzip(Json("Company-X")))));

            Assert.Multiple(() => {
                Assert.That(report?.OrganizationName,  Is.EqualTo("Company-X"));
                Assert.That(report?.TotalFailure,      Is.EqualTo(303));
                Assert.That(Kept,                      Has.Length.EqualTo(1));
            });

        }


        [Test(Description = "I-1 - the limit is the one configured: a report larger than it is no report, uncompressed too")]
        public void The_limit_is_the_one_configured()
        {

            var json = Json("Company-X");

            Assert.Multiple(() => {
                Assert.That(new TlsRptIngestor(directory, log, json.Length - 1).Ingest(Mail(GzipPart(Gzip(json)))),                                   Is.Null, "gzipped");
                Assert.That(new TlsRptIngestor(directory, log, json.Length - 1).Ingest(Mail("Content-Type: application/tlsrpt+json\r\n\r\n" + json)), Is.Null, "uncompressed");
                Assert.That(new TlsRptIngestor(directory, log, json.Length + 2).Ingest(Mail(GzipPart(Gzip(json)))),                                   Is.Not.Null, "within it");
                Assert.That(new SMTPServerConfig { Hostname = "mx.hermod.test" }.MaxTlsRptReportSize,                                                 Is.EqualTo(TlsRptIngestor.DefaultMaxReportSize));
            });

        }

        #endregion

        #region I-3: the transfer encodings

        [Test(Description = "I-3 - RFC 2049 §2: a MIME-conformant agent MUST \"decode all received data encoded by either quoted-printable or base64 implementations\" - an application/tlsrpt+json report in quoted-printable, soft line breaks, =3D and UTF-8 as =XX, is read as it was written")]
        public void A_quoted_printable_report_is_read()
        {

            var report = Ingest(Mail("Content-Type: application/tlsrpt+json\r\n" +
                                     "Content-Transfer-Encoding: quoted-printable\r\n" +
                                     "\r\n" +
                                     QuotedPrintable(Json("Müller & Söhne")) + "\r\n"));

            Assert.Multiple(() => {
                Assert.That(report?.OrganizationName,                                  Is.EqualTo("Müller & Söhne"));
                Assert.That(report?.Policies.Single().Failures[1].FailedSessionCount,  Is.EqualTo(200));
            });

        }


        [Test(Description = "I-3 - RFC 8460 §5.3: an uncompressed report in 7bit, and one in base64, are read - guards")]
        public void A_report_in_7bit_or_base64_is_read()
        {

            var json = Json("Company-X");

            Assert.Multiple(() => {
                Assert.That(Ingest(Mail("Content-Type: application/tlsrpt+json\r\nContent-Transfer-Encoding: 7bit\r\n\r\n" + json.Replace(",\"", ",\r\n \"")))?.OrganizationName,
                            Is.EqualTo("Company-X"), "7bit");
                Assert.That(Ingest(Mail("Content-Type: application/tlsrpt+json\r\nContent-Transfer-Encoding: base64\r\n\r\n" +
                                        Convert.ToBase64String(Encoding.UTF8.GetBytes(json), Base64FormattingOptions.InsertLineBreaks)))?.OrganizationName,
                            Is.EqualTo("Company-X"), "base64");
            });

        }

        #endregion

        #region I-7: the parts of the multipart

        [Test(Description = "I-7 - RFC 2046 §5.1.1: multipart-body := [preamble CRLF] dash-boundary ..., preamble := discard-text; \"implementations must ignore anything that appears before the first boundary delimiter line\" - a report written into the preamble is not the report, the one in the report part is")]
        public void The_preamble_is_not_read_as_a_report()
        {

            var report = Ingest(Mail(GzipPart(Gzip(Json("Company-X"))),
                                     Preamble: "Content-Type: application/tlsrpt+json\r\n\r\n" + Json("Forger") + "\r\n"));

            Assert.That(report?.OrganizationName, Is.EqualTo("Company-X"));

        }


        [Test(Description = "I-7 - RFC 2046 §5.1.1: \"implementations must ignore anything that appears ... after the last one\" - a report in the epilogue, after the close-delimiter, is no report")]
        public void The_epilogue_is_not_read_as_a_report()
        {

            var report = Ingest(Mail("Content-Type: text/plain\r\n\r\nNo report today.",
                                     Epilogue: "\r\n------=_NextPart_000_024E_01CC9B0A.AFE54C00\r\n" +
                                               "Content-Type: application/tlsrpt+json\r\n\r\n" + Json("Forger") + "\r\n"));

            Assert.That(report, Is.Null);

        }


        [Test(Description = "I-7 - RFC 2046 §5.1.1: \"The boundary delimiter MUST occur at the beginning of a line\" - the boundary within a line of text is text, and what follows it no part")]
        public void A_boundary_within_a_line_is_no_delimiter()
        {

            var report = Ingest(Mail(GzipPart(Gzip(Json("Company-X"))),
                                     Text: "Quoted: >------=_NextPart_000_024E_01CC9B0A.AFE54C00\r\n" +
                                           "Content-Type: application/tlsrpt+json\r\n\r\n" + Json("Forger") + "\r\n"));

            Assert.That(report?.OrganizationName, Is.EqualTo("Company-X"));

        }


        [Test(Description = "I-7 - RFC 2046 §5.1.1: transport-padding after a delimiter is allowed, and a body that ends without its close-delimiter keeps its last part - guards")]
        public void Transport_padding_and_a_missing_close_delimiter_are_taken()
        {

            var padded    = EMailMessage.Parse("TLS-Report-Domain: hermod.test\r\n" +
                                               "Content-Type: multipart/report; report-type=tlsrpt; boundary=b1\r\n" +
                                               "\r\n" +
                                               "--b1 \t\r\n" +
                                               "Content-Type: application/tlsrpt+json\r\n\r\n" + Json("Company-X") + "\r\n" +
                                               "--b1-- \r\n");

            var truncated = EMailMessage.Parse("TLS-Report-Domain: hermod.test\r\n" +
                                               "Content-Type: multipart/report; report-type=tlsrpt; boundary=b1\r\n" +
                                               "\r\n" +
                                               "--b1\r\n" +
                                               "Content-Type: application/tlsrpt+json\r\n\r\n" + Json("Company-X") + "\r\n");

            Assert.Multiple(() => {
                Assert.That(Ingest(padded)?.   OrganizationName, Is.EqualTo("Company-X"), "transport-padding");
                Assert.That(Ingest(truncated)?.OrganizationName, Is.EqualTo("Company-X"), "no close-delimiter");
            });

        }

        #endregion

    }

}
