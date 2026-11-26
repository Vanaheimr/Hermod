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

using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Xml.Linq;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// The DMARC reports DmarcReportService writes (RFC 7489 §7): the aggregate report (§7.2,
    /// its XML after Appendix C, its mail after §7.2.1.1), who receives one (§7.1, §7.2.1),
    /// and the failure report (§7.3, RFC 6591). The evaluations come from DNSVerifier, as the
    /// server's do; the reports are read from the mail the service queues. The findings R-1
    /// to R-6 of SMTPConformanceTests.
    /// </summary>
    [TestFixture]
    public class DmarcReportTests
    {

        #region Setup

        private static readonly System.Net.IPAddress client = System.Net.IPAddress.Parse("192.0.2.25");

        private sealed class QuietLogger : ILogger
        {
            public void Log(LogLevel level, String message) { }
        }

        private sealed class ReportQueue : IMailQueue
        {
            public readonly List<QueuedMail> Enqueued = [];
            public Task EnqueueAsync(QueuedMail mail, CancellationToken ct = default) { Enqueued.Add(mail); return Task.CompletedTask; }
            public Task<IReadOnlyList<QueuedMail>> GetPendingAsync(Int32 maxItems = 50, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<QueuedMail>>([]);
            public Task<QueuedMail?> GetByIdAsync(String id, CancellationToken ct = default) => Task.FromResult<QueuedMail?>(null);
            public Task UpdateAsync(QueuedMail mail, CancellationToken ct = default) => Task.CompletedTask;
            public Task RemoveAsync(String id, CancellationToken ct = default) => Task.CompletedTask;
            public Task<Int32> GetQueueLengthAsync(CancellationToken ct = default) => Task.FromResult(Enqueued.Count);
            public Task<IReadOnlyList<QueuedMail>> GetFailedAsync(Int32 maxItems = 100, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<QueuedMail>>([]);
            public ChannelReader<QueuedMail> NewMailReader { get; } = Channel.CreateUnbounded<QueuedMail>().Reader;
            public void SignalRetryCheck() { }
            public ChannelReader<Boolean> RetryCheckReader { get; } = Channel.CreateUnbounded<Boolean>().Reader;
        }

        /// <summary>
        /// TXT records by owner name; every other query has no answer.
        /// </summary>
        private sealed class TxtDNS(params (String Name, String Text)[] Records) : IDNSClient
        {

            private static readonly DNSServerConfig origin = new (IPv4Address.Localhost, IPPort.Parse(53));

            private Task<DNSInfo> Answer(String Name, IEnumerable<DNSResourceRecordTypes> Types)
            {

                var name    = Name.TrimEnd('.').ToLowerInvariant();
                var answers = Types.Contains(DNSResourceRecordTypes.TXT)
                                  ? Records.Where (record => record.Name == name).
                                            Select(record => (IDNSResourceRecord) new TXT(DNSServiceName.Parse(name), DNSQueryClasses.IN, TimeSpan.FromMinutes(5), record.Text)).
                                            ToArray()
                                  : [];

                return Task.FromResult(new DNSInfo(origin, 0, true, false, true, false, DNSResponseCodes.NoError,
                                                   answers, [], [], true, false, TimeSpan.FromSeconds(1), TimeSpan.Zero));

            }

            public Task<DNSInfo> Query(DomainName DomainName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null,
                                       Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
                => Answer(DomainName.FullName, ResourceRecordTypes);

            public Task<DNSInfo> Query(DNSServiceName DNSServiceName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null,
                                       Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
                => Answer(DNSServiceName.FullName, ResourceRecordTypes);

            public override String ToString()
                => nameof(TxtDNS);

            public void Dispose() { }

            public ValueTask DisposeAsync()
                => ValueTask.CompletedTask;

        }

        private const String SpfPass = "v=spf1 ip4:192.0.2.25 -all";

        /// <summary>
        /// The DNS of client.example - its SPF record lets 192.0.2.25 send, its DMARC record
        /// is the given one - and the DMARC reporter of mx.hermod.test.
        /// </summary>
        private sealed class Reporter : IDisposable
        {

            private readonly String     directory = Directory.CreateTempSubdirectory("hermod-dmarc-reports-").FullName;

            public  ReportQueue         Queue    { get; } = new();
            public  DNSVerifier         Verifier { get; }
            public  DmarcReportService  Service  { get; }

            public Reporter(String Dmarc, params (String Name, String Text)[] Records)
            {

                var dns  = new TxtDNS([ ("client.example", SpfPass), ("_dmarc.client.example", Dmarc), .. Records ]);
                var log  = new QuietLogger();

                Verifier = new DNSVerifier(dns, log);
                Service  = new DmarcReportService(new DmarcAggregator(Path.Combine(directory, "state.json"), log),
                                                  Queue,
                                                  Verifier,
                                                  new DmarcReportingOptions("Hermod", "DMARC Reports <dmarc-reports@mx.hermod.test>",
                                                                            "dmarc-reports@mx.hermod.test", "mx.hermod.test",
                                                                            TimeSpan.FromDays(1), EnableForensic: true),
                                                  log);

            }

            /// <summary>
            /// DMARC of one message, as the server evaluates it: SPF of the reverse-path's
            /// domain, or of the HELO identity for a null reverse-path.
            /// </summary>
            public async Task<DmarcEvaluation> Evaluate(String      Message,
                                                        String      MailFrom  = "sender@client.example",
                                                        String      Helo      = "mx.client.example",
                                                        System.Net.IPAddress?  Client    = null)
            {

                var mailFromDomain  = MailFrom.Contains('@') ? MailFrom[(MailFrom.LastIndexOf('@') + 1)..] : "";
                var verification    = await Verifier.VerifyAsync(mailFromDomain.Length > 0 ? mailFromDomain : Helo,
                                                                 Client ?? client,
                                                                 mailFromDomain.Length > 0 ? MailFrom : "",
                                                                 Helo,
                                                                 EMailMessage.Parse(Message));

                return verification.DmarcDetail ?? throw new AssertionException("no DMARC evaluation");

            }

            /// <summary>
            /// Record the evaluations, send the aggregate reports, and return what was queued.
            /// </summary>
            public async Task<IReadOnlyList<QueuedMail>> Aggregate(params (DmarcEvaluation Evaluation, System.Net.IPAddress Client)[] Evaluations)
            {

                foreach (var (evaluation, from) in Evaluations)
                    Service.RecordInbound(evaluation, from);

                await Service.GenerateAndSendAggregatesAsync(CancellationToken.None);

                return [.. Queue.Enqueued];

            }

            public Task<IReadOnlyList<QueuedMail>> Aggregate(params DmarcEvaluation[] Evaluations)
                => Aggregate([.. Evaluations.Select(evaluation => (evaluation, client))]);

            /// <summary>
            /// Send the failure report of one message, and return what was queued.
            /// </summary>
            public async Task<IReadOnlyList<QueuedMail>> FailureReport(DmarcEvaluation Evaluation, String Message, String MailFrom = "sender@client.example")
            {

                await Service.SendForensicAsync(Evaluation, client, Message[..(Message.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 2)], MailFrom,
                                                $"mx.hermod.test; dmarc={(Evaluation.Failed ? "fail" : "pass")} header.from={Evaluation.HeaderFromDomain}");

                return [.. Queue.Enqueued];

            }

            public void Dispose()
            {
                try { Directory.Delete(directory, recursive: true); } catch { }
            }

        }

        private static String Letter(String From = "sender@client.example")

            => $"From: Sender <{From}>\r\n" +
               "To: alice@hermod.test\r\n" +
               "Subject: reported\r\n" +
               "Date: Sat, 17 Oct 2026 20:00:00 +0200\r\n" +
               $"Message-ID: <{Guid.NewGuid():N}@client.example>\r\n" +
               "\r\n" +
               "body\r\n";

        /// <summary>
        /// A DKIM key of client.example, selector "test" - its signer and its record in DNS.
        /// </summary>
        private sealed record DkimKey(DkimSigner Signer, (String Name, String Text) Record)
        {

            public static DkimKey Make()
            {

                using var rsa = System.Security.Cryptography.RSA.Create(2048);

                var config = new DkimConfig {
                                 Domain         = "client.example",
                                 Selector       = "test",
                                 PrivateKeyPem  = rsa.ExportPkcs8PrivateKeyPem(),
                                 SignedHeaders  = "from:to:subject:date:message-id"
                             };

                return new DkimKey(new DkimSigner(config, new QuietLogger()),
                                   ("test._domainkey.client.example", $"v=DKIM1; k=rsa; p={Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo())}"));

            }

            /// <summary>
            /// The letter, signed - and, if so asked, its body changed after: the signature fails.
            /// </summary>
            public String Sign(String Letter, Boolean Broken = false)
            {
                var signed = Signer.SignMessage(Letter);
                return Broken ? signed.Replace("\r\n\r\nbody\r\n", "\r\n\r\nanother body\r\n") : signed;
            }

        }

        private static readonly System.Net.IPAddress elsewhere = System.Net.IPAddress.Parse("198.51.100.7");   // not in client.example's SPF record

        #endregion

        #region Reading the reports

        /// <summary>
        /// The header fields of a message or part, unfolded, by name.
        /// </summary>
        private static Dictionary<String, String> Headers(String Text)
        {

            var fields = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            var block  = Text[..Math.Max(0, Text.IndexOf("\r\n\r\n", StringComparison.Ordinal))];

            foreach (var line in Regex.Replace(block, @"\r\n[ \t]+", " ").Split("\r\n"))
            {
                var colon = line.IndexOf(':');
                if (colon > 0)
                    fields.TryAdd(line[..colon].Trim(), line[(colon + 1)..].Trim());
            }

            return fields;

        }

        /// <summary>
        /// The body of the first MIME part of the given type.
        /// </summary>
        private static String Part(String Message, String ContentType)
        {

            var boundary = Regex.Match(Headers(Message)["Content-Type"], "boundary=\"?([^\";]+)\"?").Groups[1].Value;

            foreach (var part in Message.Split("--" + boundary).Skip(1))
            {
                if (Headers(part.TrimStart('\r', '\n')).GetValueOrDefault("Content-Type", "").StartsWith(ContentType, StringComparison.OrdinalIgnoreCase))
                {
                    var text = part.TrimStart('\r', '\n');
                    return text[(text.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..];
                }
            }

            throw new AssertionException($"no {ContentType} part in:\r\n{Message}");

        }

        private static XDocument Xml(QueuedMail Mail)
        {

            using var gzip   = new GZipStream(new MemoryStream(Convert.FromBase64String(Part(Mail.MessageContent, "application/gzip").Replace("\r\n", "").Trim('-'))),
                                              CompressionMode.Decompress);
            using var reader = new StreamReader(gzip, Encoding.UTF8);

            return XDocument.Parse(reader.ReadToEnd());

        }

        private static String? Value(XElement? Element, String Path)
            => Path.Split('/').Aggregate(Element, (element, name) => element?.Element(name))?.Value;

        private static String? Value(XDocument Report, String Path)
            => Value(Report.Root, Path);

        #endregion


        #region The aggregate report

        [Test(Description = "RFC 7489 §7.2.1.1: the Subject's Report-ID is a msg-id - \"Report-ID:\" msg-id, from RFC 5322: \"<\" id-left \"@\" id-right \">\" - and names the report whose report_id it is")]
        public async Task The_Report_ID_of_the_subject_is_a_msg_id()
        {

            using var reporter = new Reporter("v=DMARC1; p=reject; rua=mailto:dmarc@client.example");

            var mail    = (await reporter.Aggregate(await reporter.Evaluate(Letter()))).Single();
            var subject = Headers(mail.MessageContent)["Subject"];
            var id      = Regex.Match(subject, @" Report-ID: <([^<>@\s]+@[^<>@\s]+)>$");

            Assert.Multiple(() => {
                Assert.That(id.Success,                               Is.True, subject);
                Assert.That(Value(Xml(mail), "report_metadata/report_id"), Is.EqualTo(id.Groups[1].Value), "the report_id of the XML is the same");
            });

        }


        [Test(Description = "RFC 7489 Appendix C: in PolicyPublishedType sp and fo have no minOccurs, so one each; without sp=, p= applies to subdomains (§6.3); fo= is \"0\" by default")]
        public async Task The_published_policy_has_sp_and_fo_also_where_the_record_has_neither()
        {

            using var reporter = new Reporter("v=DMARC1; p=reject; rua=mailto:dmarc@client.example");

            var report = Xml((await reporter.Aggregate(await reporter.Evaluate(Letter()))).Single());

            Assert.Multiple(() => {
                Assert.That(Value(report, "policy_published/sp"), Is.EqualTo("reject"));
                Assert.That(Value(report, "policy_published/fo"), Is.EqualTo("0"));
            });

        }


        [Test(Description = "RFC 7489 Appendix C: policy_published says the sp and fo the record published")]
        public async Task The_published_policy_has_sp_and_fo_as_published()
        {

            using var reporter = new Reporter("v=DMARC1; p=none; sp=quarantine; fo=1; rua=mailto:dmarc@client.example; ruf=mailto:ruf@client.example");

            var report = Xml((await reporter.Aggregate(await reporter.Evaluate(Letter()))).Single());

            Assert.Multiple(() => {
                Assert.That(Value(report, "policy_published/p"),  Is.EqualTo("none"));
                Assert.That(Value(report, "policy_published/sp"), Is.EqualTo("quarantine"));
                Assert.That(Value(report, "policy_published/fo"), Is.EqualTo("1"));
            });

        }


        [Test(Description = "RFC 7489 Appendix C: IdentifierType has envelope_from, \"The RFC5321.MailFrom domain\", minOccurs=\"1\"; SPFAuthResultType has scope, \"helo\" or \"mfrom\", minOccurs=\"1\"")]
        public async Task A_record_names_the_envelope_sender_and_the_scope_of_SPF()
        {

            using var reporter = new Reporter("v=DMARC1; p=reject; rua=mailto:dmarc@client.example");

            var record = Xml((await reporter.Aggregate(await reporter.Evaluate(Letter(), MailFrom: "sender@client.example"))).Single()).Root?.Element("record");

            Assert.Multiple(() => {
                Assert.That(Value(record, "identifiers/envelope_from"),  Is.EqualTo("client.example"));
                Assert.That(Value(record, "identifiers/header_from"),    Is.EqualTo("client.example"));
                Assert.That(Value(record, "auth_results/spf/domain"),    Is.EqualTo("client.example"));
                Assert.That(Value(record, "auth_results/spf/scope"),     Is.EqualTo("mfrom"));
                Assert.That(Value(record, "auth_results/spf/result"),    Is.EqualTo("pass"));
            });

        }


        [Test(Description = "RFC 7208 §2.4: a null reverse-path is checked on the HELO identity - RFC 7489 Appendix C: SPF scope \"helo\", and envelope_from, the RFC5321.MailFrom domain, empty")]
        public async Task A_null_senders_SPF_result_has_the_scope_helo_and_no_envelope_from()
        {

            using var reporter = new Reporter("v=DMARC1; p=reject; rua=mailto:dmarc@client.example");

            var record = Xml((await reporter.Aggregate(await reporter.Evaluate(Letter(), MailFrom: "", Helo: "client.example"))).Single()).Root?.Element("record");

            Assert.Multiple(() => {
                Assert.That(Value(record, "identifiers/envelope_from"),  Is.Empty);
                Assert.That(Value(record, "auth_results/spf/domain"),    Is.EqualTo("client.example"), "the HELO identity");
                Assert.That(Value(record, "auth_results/spf/scope"),     Is.EqualTo("helo"));
                Assert.That(Value(record, "auth_results/spf/result"),    Is.EqualTo("pass"));
            });

        }


        [Test(Description = "RFC 7489 Appendix C: a record is the messages alike in its identifiers and auth_results - a null sender and a letter with the same SPF domain differ in envelope_from and scope")]
        public async Task A_null_sender_and_a_letter_are_two_records()
        {

            using var reporter = new Reporter("v=DMARC1; p=reject; rua=mailto:dmarc@client.example");

            var report = Xml((await reporter.Aggregate(await reporter.Evaluate(Letter(), MailFrom: "sender@client.example", Helo: "client.example"),
                                                       await reporter.Evaluate(Letter(), MailFrom: "",                      Helo: "client.example"))).Single());

            Assert.That(report.Root!.Elements("record").Select(record => Value(record, "auth_results/spf/scope") + " " + Value(record, "row/count")),
                        Is.EquivalentTo(new[] { "mfrom 1", "helo 1" }));

        }

        #endregion

        #region Who receives it

        private const String Consent = "client.example._report._dmarc.reports.example";

        [Test(Description = "RFC 7489 §7.1: a destination outside the policy domain's organization receives reports only if it publishes <policy-domain>._report._dmarc.<destination>")]
        public async Task An_outside_destination_receives_reports_only_with_its_consent()
        {

            using var without  = new Reporter("v=DMARC1; p=reject; rua=mailto:dmarc@reports.example");
            using var with     = new Reporter("v=DMARC1; p=reject; rua=mailto:dmarc@reports.example", (Consent, "v=DMARC1"));

            var refused        = await without.Aggregate(await without.Evaluate(Letter()));
            var accepted       = await with.   Aggregate(await with.   Evaluate(Letter()));

            Assert.Multiple(() => {
                Assert.That(refused,                                    Is.Empty);
                Assert.That(accepted.SelectMany(mail => mail.EnvelopeTo), Is.EqualTo(new[] { "dmarc@reports.example" }));
            });

        }


        [Test(Description = "RFC 7489 §7.1: \"If a \"rua\" or \"ruf\" tag is thus discovered, replace the corresponding value extracted from the domain's DMARC policy record with the one found in this record\"")]
        public async Task The_destinations_own_rua_replaces_the_policy_domains()
        {

            using var reporter = new Reporter("v=DMARC1; p=reject; rua=mailto:dmarc@reports.example",
                                              (Consent, "v=DMARC1; rua=mailto:client-example@reports.example"));

            var queued = await reporter.Aggregate(await reporter.Evaluate(Letter()));

            Assert.That(queued.SelectMany(mail => mail.EnvelopeTo), Is.EqualTo(new[] { "client-example@reports.example" }));

        }


        [Test(Description = "RFC 7489 §7.1: the destination's ruf replaces the policy domain's for failure reports")]
        public async Task The_destinations_own_ruf_replaces_the_policy_domains()
        {

            using var reporter = new Reporter("v=DMARC1; p=reject; ruf=mailto:dmarc@reports.example",
                                              (Consent, "v=DMARC1; ruf=mailto:failures@reports.example"));

            // From an address client.example's SPF record does not name: DMARC fails.
            var evaluation = await reporter.Evaluate(Letter(), Client: System.Net.IPAddress.Parse("198.51.100.7"));
            var queued     = await reporter.FailureReport(evaluation, Letter());

            Assert.That(queued.SelectMany(mail => mail.EnvelopeTo), Is.EqualTo(new[] { "failures@reports.example" }));

        }


        [Test(Description = "RFC 7489 §7.1: \"if the confirming record includes a URI whose host is again different than the domain publishing that override, the Mail Receiver generating the report MUST NOT generate a report to either the original or the override URI\"")]
        public async Task A_replacement_on_another_host_is_used_as_little_as_the_original()
        {

            using var reporter = new Reporter("v=DMARC1; p=reject; rua=mailto:dmarc@reports.example",
                                              (Consent, "v=DMARC1; rua=mailto:dmarc@elsewhere.example"));

            Assert.That(await reporter.Aggregate(await reporter.Evaluate(Letter())), Is.Empty);

        }


        [Test(Description = "RFC 7489 §7.2.1: \"Any reporting URI that includes a size limitation exceeded by the generated report ... MUST NOT be used\" - !10 is ten octets, and no error report fits either")]
        public async Task A_destination_whose_size_limit_the_report_exceeds_receives_none()
        {

            using var reporter = new Reporter("v=DMARC1; p=reject; rua=mailto:dmarc@client.example!10");

            Assert.That(await reporter.Aggregate(await reporter.Evaluate(Letter())), Is.Empty);

        }


        [Test(Description = "RFC 7489 §7.2.1: \"An attempt MUST be made to deliver an aggregate report to every remaining URI\" - the one whose limit, 10m, the report keeps to")]
        public async Task Only_the_destination_whose_size_limit_the_report_keeps_to_receives_it()
        {

            using var reporter = new Reporter("v=DMARC1; p=reject; rua=mailto:small@client.example!10,mailto:large@client.example!10m");

            var queued = await reporter.Aggregate(await reporter.Evaluate(Letter()));

            Assert.That(queued.SelectMany(mail => mail.EnvelopeTo), Is.EqualTo(new[] { "large@client.example" }));

        }


        [Test(Description = "RFC 7489 §7.2.1: where \"all provided URIs specify size limits exceeded by the generated record, the Mail Receiver SHOULD send a short report (see Section 7.2.2)\" - with Report-Date, Report-Domain, Report-ID, Report-Size, Submitter and Submitting-URI")]
        public async Task A_report_too_large_for_every_destination_is_announced_by_a_short_report()
        {

            using var reporter = new Reporter("v=DMARC1; p=reject; rua=mailto:dmarc@client.example!2k");

            // Four hundred sources: a report of some kilobytes, even gzipped.
            var evaluation = await reporter.Evaluate(Letter());
            var queued     = await reporter.Aggregate([.. Enumerable.Range(0, 400).Select(i => (evaluation, System.Net.IPAddress.Parse($"10.0.{i / 256}.{i % 256}")))]);
            var mail       = queued.Single();
            var report     = Headers(mail.MessageContent.Split("\r\n\r\n", 2)[1] + "\r\n");   // its body: fields as of a DSN

            Assert.Multiple(() => {
                Assert.That(mail.EnvelopeTo,                                   Is.EqualTo(new[] { "dmarc@client.example" }));
                Assert.That(Headers(mail.MessageContent)["Content-Type"],      Does.StartWith("text/plain"));
                Assert.That(report.Keys,                                       Is.SupersetOf(new[] { "Report-Date", "Report-Domain", "Report-ID", "Report-Size", "Submitter", "Submitting-URI" }));
                Assert.That(report.GetValueOrDefault("Report-Domain"),         Is.EqualTo("client.example"));
                Assert.That(Int64.Parse(report.GetValueOrDefault("Report-Size") ?? "0"), Is.GreaterThan(2048));
                Assert.That(report.GetValueOrDefault("Submitting-URI"),        Is.EqualTo("mailto:dmarc@client.example"));
            });

        }

        #endregion

        #region The failure report

        /// <summary>
        /// The fields of the message/feedback-report part of a failure report.
        /// </summary>
        private static Dictionary<String, String> Feedback(QueuedMail Mail)
            => Headers(Part(Mail.MessageContent, "message/feedback-report") + "\r\n\r\n");


        [Test(Description = "RFC 7489 §7.3.1: \"Identity-Alignment (REQUIRED ...)\", \"DKIM-Domain, DKIM-Identity, DKIM-Selector (REQUIRED if the message was signed by DKIM)\", \"SPF-DNS (REQUIRED)\" - RFC 6591 §4: \"SPF-DNS:\" ( \"txt\" / \"spf\" ) \":\" domain \":\" quoted-string")]
        public async Task The_failure_report_has_the_fields_RFC_7489_requires()
        {

            var key            = DkimKey.Make();
            using var reporter = new Reporter("v=DMARC1; p=reject; ruf=mailto:ruf@client.example", key.Record);

            // From an address SPF does not allow, its signature broken: DMARC fails.
            var message    = key.Sign(Letter(), Broken: true);
            var evaluation = await reporter.Evaluate(message, Client: elsewhere);
            var feedback   = Feedback((await reporter.FailureReport(evaluation, message)).Single());

            Assert.Multiple(() => {
                Assert.That(feedback.GetValueOrDefault("Auth-Failure"),        Is.EqualTo("dmarc"));
                Assert.That(feedback.GetValueOrDefault("Identity-Alignment"),  Is.EqualTo("none"));
                Assert.That(feedback.GetValueOrDefault("DKIM-Domain"),         Is.EqualTo("client.example"));
                Assert.That(feedback.GetValueOrDefault("DKIM-Identity"),       Is.EqualTo("@client.example"), "i= by default");
                Assert.That(feedback.GetValueOrDefault("DKIM-Selector"),       Is.EqualTo("test"));
                Assert.That(feedback.GetValueOrDefault("SPF-DNS"),             Is.EqualTo($"txt : client.example : \"{SpfPass}\""));
            });

        }


        [Test(Description = "RFC 7489 §7.3.1: the DKIM fields are \"REQUIRED if the message was signed by DKIM\" - an unsigned message has none, and SPF-DNS still")]
        public async Task The_failure_report_of_an_unsigned_message_has_SPF_DNS_and_no_DKIM_fields()
        {

            using var reporter = new Reporter("v=DMARC1; p=reject; ruf=mailto:ruf@client.example");

            var evaluation = await reporter.Evaluate(Letter(), Client: elsewhere);
            var feedback   = Feedback((await reporter.FailureReport(evaluation, Letter())).Single());

            Assert.Multiple(() => {
                Assert.That(feedback.GetValueOrDefault("SPF-DNS"),  Is.EqualTo($"txt : client.example : \"{SpfPass}\""));
                Assert.That(feedback.Keys,                          Has.None.StartWith("DKIM-"));
            });

        }


        [Test(Description = "RFC 5965: the recipient of the reported message is Original-Rcpt-To (§3.3); its fields (§3.5) have no Delivered-To")]
        public async Task The_failure_report_has_no_Delivered_To_field()
        {

            using var reporter = new Reporter("v=DMARC1; p=reject; ruf=mailto:ruf@client.example");

            var evaluation = await reporter.Evaluate(Letter(), Client: elsewhere);
            var feedback   = Feedback((await reporter.FailureReport(evaluation, Letter())).Single());

            Assert.That(feedback.Keys, Has.No.Member("Delivered-To"));

        }

        #endregion

        #region What fo= asks for (RFC 7489 §6.3)

        [Test(Description = "RFC 7489 §6.3: fo=0, the default - \"Generate a DMARC failure report if all underlying authentication mechanisms fail to produce an aligned \"pass\" result\": DMARC passed, so none")]
        public async Task With_fo_0_a_message_that_passed_DMARC_is_not_reported()
        {

            var key            = DkimKey.Make();
            using var reporter = new Reporter("v=DMARC1; p=reject; ruf=mailto:ruf@client.example", key.Record);

            // SPF fails, the signature holds.
            var message    = key.Sign(Letter());
            var evaluation = await reporter.Evaluate(message, Client: elsewhere);

            Assert.That(await reporter.FailureReport(evaluation, message), Is.Empty);

        }


        [Test(Description = "RFC 7489 §6.3: fo=1 - \"Generate a DMARC failure report if any underlying authentication mechanism produced something other than an aligned \"pass\" result\" - DMARC passed on DKIM, SPF failed")]
        public async Task With_fo_1_a_failing_SPF_is_reported_although_DMARC_passed()
        {

            var key            = DkimKey.Make();
            using var reporter = new Reporter("v=DMARC1; p=reject; ruf=mailto:ruf@client.example; fo=1", key.Record);

            var message    = key.Sign(Letter());
            var evaluation = await reporter.Evaluate(message, Client: elsewhere);
            var queued     = await reporter.FailureReport(evaluation, message);

            Assert.Multiple(() => {
                Assert.That(evaluation.Failed,                                                   Is.False, "DMARC passed");
                Assert.That(queued,                                                              Has.Count.EqualTo(1));
                Assert.That(queued.Select(Feedback).FirstOrDefault()?.GetValueOrDefault("Auth-Failure"),       Is.EqualTo("dmarc"));
                Assert.That(queued.Select(Feedback).FirstOrDefault()?.GetValueOrDefault("Identity-Alignment"), Is.EqualTo("dkim"));
            });

        }


        [Test(Description = "RFC 7489 §6.3: fo=d - \"Generate a DKIM failure report if the message had a signature that failed evaluation, regardless of its alignment\" - RFC 6591 §3.2.3: with DKIM-Domain, DKIM-Identity, DKIM-Selector")]
        public async Task With_fo_d_a_failed_signature_is_reported_in_a_DKIM_failure_report()
        {

            var key            = DkimKey.Make();
            using var reporter = new Reporter("v=DMARC1; p=reject; ruf=mailto:ruf@client.example; fo=d", key.Record);

            // SPF passes, DMARC with it; the signature is broken.
            var message    = key.Sign(Letter(), Broken: true);
            var evaluation = await reporter.Evaluate(message);
            var queued     = await reporter.FailureReport(evaluation, message);
            var feedback   = queued.Select(Feedback).FirstOrDefault() ?? new Dictionary<String, String>();

            Assert.Multiple(() => {
                Assert.That(evaluation.Failed,                                    Is.False, "DMARC passed");
                Assert.That(queued,                                               Has.Count.EqualTo(1));
                Assert.That(feedback.GetValueOrDefault("Auth-Failure"),           Is.EqualTo("signature"));
                Assert.That(feedback.GetValueOrDefault("DKIM-Domain"),            Is.EqualTo("client.example"));
                Assert.That(feedback.GetValueOrDefault("DKIM-Selector"),          Is.EqualTo("test"));
                Assert.That(feedback.GetValueOrDefault("Authentication-Results"), Is.EqualTo("mx.hermod.test; dkim=fail header.d=client.example"),
                                                                                  "RFC 6591 §3.1: \"it MUST reflect only a single authentication method's result\"");
            });

        }


        [Test(Description = "RFC 7489 §6.3: fo=s - \"Generate an SPF failure report if the message failed SPF evaluation, regardless of its alignment\" - RFC 6591 §3.2.6: with SPF-DNS")]
        public async Task With_fo_s_a_failed_SPF_is_reported_in_an_SPF_failure_report()
        {

            var key            = DkimKey.Make();
            using var reporter = new Reporter("v=DMARC1; p=reject; ruf=mailto:ruf@client.example; fo=s", key.Record);

            // SPF fails; DMARC passes on the signature.
            var message    = key.Sign(Letter());
            var evaluation = await reporter.Evaluate(message, Client: elsewhere);
            var queued     = await reporter.FailureReport(evaluation, message);
            var feedback   = queued.Select(Feedback).FirstOrDefault() ?? new Dictionary<String, String>();

            Assert.Multiple(() => {
                Assert.That(queued,                                               Has.Count.EqualTo(1));
                Assert.That(feedback.GetValueOrDefault("Auth-Failure"),           Is.EqualTo("spf"));
                Assert.That(feedback.GetValueOrDefault("SPF-DNS"),                Is.EqualTo($"txt : client.example : \"{SpfPass}\""));
                Assert.That(feedback.GetValueOrDefault("Authentication-Results"), Is.EqualTo("mx.hermod.test; spf=fail smtp.mailfrom=sender@client.example"));
            });

        }


        [Test(Description = "RFC 7489 §6.3: \"The value of this tag is a colon-separated list\" - fo=d:s asks for a DKIM and an SPF failure report, and no DMARC one")]
        public async Task With_fo_d_s_a_message_failing_both_gets_a_DKIM_and_an_SPF_failure_report()
        {

            var key            = DkimKey.Make();
            using var reporter = new Reporter("v=DMARC1; p=reject; ruf=mailto:ruf@client.example; fo=d:s", key.Record);

            var message    = key.Sign(Letter(), Broken: true);
            var evaluation = await reporter.Evaluate(message, Client: elsewhere);
            var queued     = await reporter.FailureReport(evaluation, message);

            Assert.That(queued.Select(mail => Feedback(mail).GetValueOrDefault("Auth-Failure")), Is.EquivalentTo(new[] { "signature", "spf" }));

        }


        [Test(Description = "RFC 7489 §6.3: fo=d reports a signature that failed - an unsigned message that passed DMARC has none")]
        public async Task With_fo_d_an_unsigned_message_is_not_reported()
        {

            using var reporter = new Reporter("v=DMARC1; p=reject; ruf=mailto:ruf@client.example; fo=d");

            var evaluation = await reporter.Evaluate(Letter());

            Assert.That(await reporter.FailureReport(evaluation, Letter()), Is.Empty);

        }

        #endregion


        #region R-8 to R-12: where a report goes, and what the failure report says of the message

        /// <summary>
        /// The lines of a message's header, split at CR LF, at a bare LF and at a bare CR.
        /// </summary>
        private static String[] HeaderLines(String Message)
            => Regex.Split(Message[..Math.Max(0, Message.IndexOf("\r\n\r\n", StringComparison.Ordinal))], "\r\n|\r|\n");


        [TestCase("ruf", "\r\n", TestName = "A line break in a ruf URI: no recipient, no header field (CR LF)")]
        [TestCase("ruf", "\n",   TestName = "A line break in a ruf URI: no recipient, no header field (LF)")]
        [TestCase("rua", "\r\n", TestName = "A line break in a rua URI: no recipient, no header field (CR LF)")]
        [TestCase("rua", "\r",   TestName = "A line break in a rua URI: no recipient, no header field (CR)")]
        [Description("R-8 - RFC 7489 §6.4: dmarc-uri = URI (RFC 3986), which has no line break; RFC 5322 §2.2: \"A field body MUST NOT include CR and LF except when used in 'folding' and 'unfolding'\" - the DMARC record from DNS, a line break in a tag value kept")]
        public async Task A_line_break_in_a_report_URI_makes_no_recipient_and_no_header_field(String Tag, String LineBreak)
        {

            using var reporter = new Reporter($"v=DMARC1; p=reject; {Tag}=mailto:dmarc@client.example{LineBreak}Bcc: victim@client.example");

            var evaluation = await reporter.Evaluate(Letter(), Client: elsewhere);
            var queued     = Tag == "rua"
                                 ? await reporter.Aggregate(evaluation)
                                 : await reporter.FailureReport(evaluation, Letter());

            Assert.Multiple(() => {
                Assert.That(queued.SelectMany(mail => mail.EnvelopeTo),                Has.None.Matches<String>(address => address is not null && address.IndexOfAny([ '\r', '\n' ]) >= 0));
                Assert.That(queued.SelectMany(mail => HeaderLines(mail.MessageContent)), Has.None.StartWith("Bcc:"));
            });

        }


        [TestCase("mailto:dmarc%21reports@client.example",               "dmarc!reports@client.example", TestName = "A report URI's percent-encoding is decoded")]
        [TestCase("mailto:dmarc@client.example?subject=DMARC%20report",  "dmarc@client.example",         TestName = "A report URI's header fields are no part of its address")]
        [TestCase("mailto:dmarc+reports@client.example!10m",             "dmarc+reports@client.example", TestName = "A report URI of the usual kind, with a size limit")]
        [Description("R-9 - RFC 7489 §6.2: a comma or an exclamation point of a DMARC URI \"MUST be encoded per Section 2.1 of [URI]\", and a Mail Receiver \"MUST implement support for a \"mailto:\" URI\"; RFC 6068 §2: mailtoURI = \"mailto:\" [ to ] [ hfields ]")]
        public async Task A_report_goes_to_the_address_its_URI_names(String Uri, String Address)
        {

            using var reporter = new Reporter($"v=DMARC1; p=none; rua={Uri}");

            var queued = await reporter.Aggregate(await reporter.Evaluate(Letter()));

            Assert.Multiple(() => {
                Assert.That(queued.SelectMany(mail => mail.EnvelopeTo),              Is.EqualTo(new[] { Address }));
                Assert.That(HeaderLines(queued.Single().MessageContent),             Has.One.EqualTo("To: " + Address));
            });

        }


        [TestCase("\r\n", TestName = "A line break in the SPF record makes no field of the failure report (CR LF)")]
        [TestCase("\n",   TestName = "A line break in the SPF record makes no field of the failure report (LF)")]
        [TestCase("\r",   TestName = "A line break in the SPF record makes no field of the failure report (CR)")]
        [Description("R-10 - RFC 6591 §4: SPF-DNS ends in a quoted-string, which has no CR and no LF but in folding (RFC 5322 §3.2.4) - the record the sender published, a line break in it a space")]
        public async Task A_line_break_in_the_SPF_record_makes_no_field_of_the_failure_report(String LineBreak)
        {

            using var reporter = new Reporter("v=DMARC1; p=reject; ruf=mailto:ruf@client.example");

            var evaluation = await reporter.Evaluate(Letter(), Client: elsewhere) with { SpfRecord = $"v=spf1 -all{LineBreak}Identity-Alignment: dkim, spf" };
            var mail       = (await reporter.FailureReport(evaluation, Letter())).Single();
            var part       = Part(mail.MessageContent, "message/feedback-report");
            var feedback   = Feedback(mail);

            Assert.Multiple(() => {
                Assert.That(Regex.Split(part, "\r\n|\r|\n"),          Has.None.StartWith("Identity-Alignment: dkim"));
                Assert.That(feedback.GetValueOrDefault("SPF-DNS"),     Is.EqualTo("txt : client.example : \"v=spf1 -all Identity-Alignment: dkim, spf\""));
            });

        }


        [Test(Description = "R-10 - RFC 6591 §4, RFC 5322 §2.1.1: a record longer than a line is folded in its quoted-string, and unfolds to itself")]
        public async Task A_long_SPF_record_is_folded_in_SPF_DNS()
        {

            using var reporter = new Reporter("v=DMARC1; p=reject; ruf=mailto:ruf@client.example");

            var record     = "v=spf1 " + String.Join(" ", Enumerable.Range(0, 120).Select(number => $"ip4:192.0.2.{number}")) + " -all";
            var evaluation = await reporter.Evaluate(Letter(), Client: elsewhere) with { SpfRecord = record };
            var part       = Part((await reporter.FailureReport(evaluation, Letter())).Single().MessageContent, "message/feedback-report");

            Assert.Multiple(() => {
                Assert.That(part.Split("\r\n"),                       Has.All.Matches<String>(line => line is not null && Encoding.UTF8.GetByteCount(line) <= 998));
                Assert.That(Regex.Replace(part, "\r\n(?=[ \t])", ""),  Does.Contain($"SPF-DNS: txt : client.example : \"{record}\"\r\n"));
            });

        }


        [TestCase("sender@client.example", "<sender@client.example>", TestName = "Original-Mail-From: the reverse-path, in angle brackets")]
        [TestCase("",                      "<>",                      TestName = "Original-Mail-From: the null reverse-path")]
        [Description("R-11 - RFC 5965 §3.5: original-mail-from = \"Original-Mail-From:\" [CFWS] reverse-path [CFWS] CRLF; RFC 5321 §4.1.2: Reverse-path = Path / \"<>\", Path = \"<\" [ A-d-l \":\" ] Mailbox \">\"")]
        public async Task Original_Mail_From_is_a_reverse_path(String MailFrom, String ReversePath)
        {

            using var reporter = new Reporter("v=DMARC1; p=reject; ruf=mailto:ruf@client.example");

            var evaluation = await reporter.Evaluate(Letter(), Client: elsewhere);
            var feedback   = Feedback((await reporter.FailureReport(evaluation, Letter(), MailFrom)).Single());

            Assert.That(feedback.GetValueOrDefault("Original-Mail-From"), Is.EqualTo(ReversePath));

        }


        [TestCase("192.0.2.25",               "192.0.2.25",        TestName = "Source-IP: an IPv4 address")]
        [TestCase("2001:db8::25",             "IPv6:2001:db8::25", TestName = "Source-IP: an IPv6 address")]
        [TestCase("::ffff:192.0.2.25",        "192.0.2.25",        TestName = "Source-IP: an IPv4 address mapped into IPv6")]
        [TestCase("fe80::25%3",               "IPv6:fe80::25",     TestName = "Source-IP: an IPv6 address with a scope")]
        [Description("R-12 - RFC 5965 §3.5: source-ip = \"Source-IP:\" [CFWS] ( IPv4-address-literal / IPv6-address-literal ) [CFWS] CRLF; RFC 5321 §4.1.3: IPv4-address-literal = Snum 3(\".\" Snum), IPv6-address-literal = \"IPv6:\" IPv6-addr")]
        public void Source_IP_is_an_address_literal(String Address, String Literal)

            => Assert.That(ArfReport.AddressLiteral(System.Net.IPAddress.Parse(Address)), Is.EqualTo(Literal));

        #endregion


        #region R-13 and R-14: a report in answer to a report, and what a report says it is

        /// <summary>
        /// A report from client.example - a multipart/report of the given Content-Type.
        /// </summary>
        private static String Report(String ContentType)

            => "From: Sender <sender@client.example>\r\n" +
               "To: alice@hermod.test\r\n" +
               "Subject: a report\r\n" +
               "Date: Sat, 17 Oct 2026 20:00:00 +0200\r\n" +
               $"Message-ID: <{Guid.NewGuid():N}@client.example>\r\n" +
               "MIME-Version: 1.0\r\n" +
               $"Content-Type: {ContentType}\r\n" +
               "\r\n" +
               "--r\r\n" +
               "Content-Type: text/plain\r\n" +
               "\r\n" +
               "A report.\r\n" +
               "--r--\r\n";

        private static String? AutoSubmitted(QueuedMail Mail)
            => Headers(Mail.MessageContent).GetValueOrDefault("Auto-Submitted");


        [TestCase("multipart/report; report-type=feedback-report;\r\n\tboundary=\"r\"",  TestName = "R-13: no failure report in answer to an ARF report")]
        [TestCase("Multipart/Report; Report-Type=\"Feedback-Report\"; boundary=\"r\"",   TestName = "R-13: no failure report in answer to an ARF report, its media type in capitals and the report-type quoted")]
        [TestCase("multipart/report; boundary=\"r\"; report-type=feedback-report",       TestName = "R-13: no failure report in answer to an ARF report, its report-type the last parameter")]
        [Description("R-13 - RFC 6650 §6: \"If the message under evaluation by the Verifier is an ARF [RFC5965] message, a report MUST NOT be automatically generated\" - RFC 5965 §2: a multipart/report of report-type \"feedback-report\". Neither a DMARC failure report nor an SPF one (fo=0:s)")]
        public async Task No_failure_report_is_sent_in_answer_to_an_ARF_report(String ContentType)
        {

            using var reporter = new Reporter("v=DMARC1; p=reject; ruf=mailto:ruf@client.example; fo=0:s");

            var message    = Report(ContentType);
            var evaluation = await reporter.Evaluate(message, Client: elsewhere);
            var queued     = await reporter.FailureReport(evaluation, message);

            Assert.Multiple(() => {
                Assert.That(evaluation.Failed,  Is.True, "DMARC failed");
                Assert.That(queued,             Is.Empty);
            });

        }


        [TestCase("multipart/report; report-type=delivery-status; boundary=\"r\"",          TestName = "R-13: a DSN that fails DMARC is reported")]
        [TestCase("multipart/report; report-type=disposition-notification; boundary=\"r\"", TestName = "R-13: an MDN that fails DMARC is reported")]
        [TestCase("multipart/mixed; boundary=\"r\"; x-report-type=feedback-report",         TestName = "R-13: a message that is no report, with a parameter of another name, is reported")]
        [Description("R-13 - RFC 6650 §6 forbids a failure report in answer to an ARF message only: another report, or a message that is none, is reported")]
        public async Task A_message_that_is_no_ARF_report_is_reported(String ContentType)
        {

            using var reporter = new Reporter("v=DMARC1; p=reject; ruf=mailto:ruf@client.example");

            var message    = Report(ContentType);
            var evaluation = await reporter.Evaluate(message, Client: elsewhere);

            Assert.That(await reporter.FailureReport(evaluation, message), Has.Count.EqualTo(1));

        }


        [Test(Description = "R-14 - RFC 3834 §5.2: auto-generated \"MUST NOT be used on a message issued in direct response to another message\"; auto-replied \"SHOULD be used on messages sent in direct response to another message by an automatic process\" - the DMARC, DKIM and SPF failure reports alike")]
        public async Task A_failure_report_is_labelled_auto_replied()
        {

            var key            = DkimKey.Make();
            using var reporter = new Reporter("v=DMARC1; p=reject; ruf=mailto:ruf@client.example; fo=0:d:s", key.Record);

            var message    = key.Sign(Letter(), Broken: true);
            var evaluation = await reporter.Evaluate(message, Client: elsewhere);
            var queued     = await reporter.FailureReport(evaluation, message);

            Assert.Multiple(() => {
                Assert.That(queued.Select(mail => Feedback(mail).GetValueOrDefault("Auth-Failure")),  Is.EquivalentTo(new[] { "dmarc", "signature", "spf" }));
                Assert.That(queued.Select(AutoSubmitted),                                             Has.All.EqualTo("auto-replied"));
            });

        }


        [Test(Description = "R-14 - RFC 3834 §5: auto-generated \"indicates that a message was generated by an automatic process, and is not a direct response to another message\" - the aggregate report, of the messages of a day")]
        public async Task The_aggregate_report_is_labelled_auto_generated()
        {

            using var reporter = new Reporter("v=DMARC1; p=reject; rua=mailto:dmarc@client.example");

            var queued = await reporter.Aggregate(await reporter.Evaluate(Letter()));

            Assert.That(queued.Select(AutoSubmitted), Is.EqualTo(new[] { "auto-generated" }));

        }

        #endregion

    }

}
