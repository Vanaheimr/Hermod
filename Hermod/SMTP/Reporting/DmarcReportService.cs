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
using System.IO.Compression;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// Generates and sends DMARC aggregate (RUA) and forensic (RUF) reports (RFC 7489 §7).
    /// Inbound evaluations are fed in via <see cref="RecordInbound"/>; a background loop
    /// (<see cref="RunAsync"/>) drains the aggregator once per interval, builds the RFC 7489
    /// Appendix-C XML, gzips it into a MIME message, verifies external-destination consent
    /// (§7.1), and enqueues it through the normal outbound mail queue (which DKIM-signs it).
    /// </summary>
    public sealed class DmarcReportService(
        DmarcAggregator        aggregator,
        IMailQueue             mailQueue,
        DNSVerifier            dnsVerifier,
        DmarcReportingOptions  options,
        ILogger                logger)
    {

        // Simple per-domain hourly cap on forensic reports (they are per-message and noisy).
        private readonly Dictionary<String, (long Hour, int Count)> _forensicBudget = new (StringComparer.OrdinalIgnoreCase);
        private const int MaxForensicPerDomainPerHour = 10;

        #region RecordInbound(eval, sourceIp)

        /// <summary>
        /// Record one evaluated inbound message for the aggregate report.
        /// </summary>
        public void RecordInbound(DmarcEvaluation eval, System.Net.IPAddress sourceIp)
        {
            try { aggregator.Record(eval, sourceIp); }
            catch (Exception ex) { logger.Log(LogLevel.Warning, $"DMARC report: record failed: {ex.Message}"); }
        }

        #endregion

        #region RunAsync — periodic aggregate generation

        public async Task RunAsync(CancellationToken ct)
        {

            logger.Log(LogLevel.Info, $"DMARC aggregate reporting active (interval {options.Interval.TotalHours:0.#} h)");

            // Tick more often than the report interval so counts get persisted regularly and
            // the report boundary is honoured without a 24 h sleep.
            var tick     = TimeSpan.FromMinutes(Math.Clamp(options.Interval.TotalMinutes / 12, 1, 15));
            var lastRun  = DateTimeOffset.UtcNow;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(tick, ct);
                    aggregator.Flush();

                    if (DateTimeOffset.UtcNow - lastRun >= options.Interval)
                    {
                        lastRun = DateTimeOffset.UtcNow;
                        await GenerateAndSendAggregatesAsync(ct);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { logger.Log(LogLevel.Warning, $"DMARC report loop error: {ex.Message}"); }
            }

            aggregator.Flush();

        }

        /// <summary>
        /// Drain the aggregator and send one report per reportable domain.
        /// </summary>
        public async Task GenerateAndSendAggregatesAsync(CancellationToken ct)
        {
            var reports = aggregator.Drain();
            foreach (var report in reports)
            {
                try { await SendAggregateAsync(report, ct); }
                catch (Exception ex) { logger.Log(LogLevel.Warning, $"DMARC report: send failed for {report.PolicyDomain}: {ex.Message}"); }
            }
        }

        #endregion

        #region SendAggregateAsync(report)

        private async Task SendAggregateAsync(DmarcDomainReport report, CancellationToken ct)
        {

            // RFC 7489 §7.2.1.1: the Report-ID of the subject is a msg-id (RFC 5322 §3.6.4),
            // "<" id-left "@" id-right ">"; the report_id of the XML is the same, without its brackets.
            var reportId = $"{report.PolicyDomain}!{report.WindowBegin.ToUnixTimeSeconds()}!{Guid.NewGuid():N}@{options.ReportingDomain}";
            var xml      = DmarcReportXml.Build(report, options.OrgName, options.ReportFromAddress, reportId);

            // RFC 7489 §7.2.1: "Any reporting URI that includes a size limitation exceeded by the
            // generated report (after compression and after any encoding required by the
            // particular transport mechanism) MUST NOT be used" - the gzipped report in base64.
            var b64          = WrapBase64(Convert.ToBase64String(Gzip(xml)));
            var size         = (UInt64) b64.Length;
            var destinations = await DestinationsAsync(report.PolicyDomain, report.Rua, Aggregate: true, ct);
            var tooLarge     = new List<String>();

            foreach (var (uri, limit) in destinations)
            {

                if (size > limit)
                {
                    logger.Log(LogLevel.Info, $"DMARC aggregate report for {report.PolicyDomain} ({size} octets) exceeds the size limit of {uri} ({limit} octets) — skipped");
                    tooLarge.Add(uri);
                    continue;
                }

                await EnqueueAsync(uri, BuildAggregateMime(report, uri, reportId, b64), ct);

                logger.Log(LogLevel.Info, $"DMARC aggregate report for {report.PolicyDomain} ({report.Rows.Count} rows) queued to {uri}");

            }

            // RFC 7489 §7.2.1: when "all provided URIs specify size limits exceeded by the generated
            // record", the Mail Receiver "SHOULD send a short report (see Section 7.2.2) indicating
            // that a report is available but could not be sent".
            if (tooLarge.Count > 0 && tooLarge.Count == destinations.Count)
                await SendErrorReportAsync(report, reportId, size, destinations, ct);

        }

        /// <summary>
        /// RFC 7489 §7.2.2: the error report - a text/plain part with Report-Date, Report-Domain,
        /// Report-ID, Report-Size, Submitter and Submitting-URI - to every mailto destination
        /// whose own size limit it keeps to.
        /// </summary>
        private async Task SendErrorReportAsync(DmarcDomainReport                                  report,
                                                String                                             reportId,
                                                UInt64                                             size,
                                                IReadOnlyList<(String Address, UInt64? SizeLimit)>  destinations,
                                                CancellationToken                                  ct)
        {

            var date = DateTimeOffset.UtcNow.ToString("ddd, dd MMM yyyy HH:mm:ss +0000", System.Globalization.CultureInfo.InvariantCulture);

            foreach (var (uri, limit) in destinations)
            {

                var message = new StringBuilder().
                                  Append("From: ").Append(options.ReportFromDisplay).Append("\r\n").
                                  Append("To: ").Append(uri).Append("\r\n").
                                  Append("Subject: DMARC aggregate report not sent: Report Domain: ").Append(report.PolicyDomain).
                                      Append(" Submitter: ").Append(options.ReportingDomain).Append(" Report-ID: <").Append(reportId).Append(">\r\n").
                                  Append("Date: ").Append(date).Append("\r\n").
                                  Append("Message-ID: <").Append(UUIDv7.Generate().ToString("N")).Append('@').Append(options.ReportingDomain).Append(">\r\n").
                                  Append("Auto-Submitted: auto-generated\r\n").
                                  Append("MIME-Version: 1.0\r\n").
                                  Append("Content-Type: text/plain; charset=us-ascii\r\n").
                                  Append("\r\n").
                                  Append("Report-Date: ").Append(date).Append("\r\n").
                                  Append("Report-Domain: ").Append(report.PolicyDomain).Append("\r\n").
                                  Append("Report-ID: <").Append(reportId).Append(">\r\n").
                                  Append("Report-Size: ").Append(size).Append("\r\n").
                                  Append("Submitter: ").Append(options.ReportingDomain).Append("\r\n").
                                  Append("Submitting-URI: ").Append(String.Join(", ", destinations.Select(destination => "mailto:" + destination.Address))).Append("\r\n").
                                  ToString();

                if ((UInt64) message.Length > limit)
                    continue;

                await EnqueueAsync(uri, message, ct);

                logger.Log(LogLevel.Info, $"DMARC aggregate report for {report.PolicyDomain} too large for every destination; error report queued to {uri}");

            }

        }

        private Task EnqueueAsync(String To, String Message, CancellationToken ct)

            => mailQueue.EnqueueAsync(new QueuedMail {
                                          Id              = UUIDv7.Generate().ToString("N"),
                                          EnvelopeFrom    = options.ReportFromAddress,
                                          EnvelopeTo      = [To],
                                          MessageContent  = Message,
                                          TargetDomain    = AddressDomain(To)
                                      },
                                      ct);

        private String BuildAggregateMime(DmarcDomainReport report, String toAddress, String reportId, String b64)
        {

            var filename  =$"{options.ReportingDomain}!{report.PolicyDomain}!{report.WindowBegin.ToUnixTimeSeconds()}!{report.WindowEnd.ToUnixTimeSeconds()}.xml.gz";
            var boundary  = "dmarc-" + UUIDv7.Generate().ToString("N");
            var date      = DateTimeOffset.UtcNow.ToString("ddd, dd MMM yyyy HH:mm:ss +0000",
                                                           System.Globalization.CultureInfo.InvariantCulture);

            var sb = new StringBuilder();
            sb.Append("From: ").Append(options.ReportFromDisplay).Append("\r\n");
            sb.Append("To: ").Append(toAddress).Append("\r\n");
            // RFC 7489 §7.2.1.1 subject format.
            sb.Append("Subject: Report Domain: ").Append(report.PolicyDomain)
              .Append(" Submitter: ").Append(options.ReportingDomain)
              .Append(" Report-ID: <").Append(reportId).Append(">\r\n");
            sb.Append("Date: ").Append(date).Append("\r\n");
            sb.Append("Message-ID: <").Append(UUIDv7.Generate().ToString("N")).Append('@').Append(options.ReportingDomain).Append(">\r\n");
            sb.Append("Auto-Submitted: auto-generated\r\n");
            sb.Append("MIME-Version: 1.0\r\n");
            sb.Append("Content-Type: multipart/mixed; boundary=\"").Append(boundary).Append("\"\r\n");
            sb.Append("\r\n");

            sb.Append("--").Append(boundary).Append("\r\n");
            sb.Append("Content-Type: text/plain; charset=utf-8\r\n\r\n");
            sb.Append("This is a DMARC aggregate report for ").Append(report.PolicyDomain)
              .Append(" covering ").Append(report.WindowBegin.UtcDateTime.ToString("u"))
              .Append(" .. ").Append(report.WindowEnd.UtcDateTime.ToString("u")).Append(".\r\n\r\n");

            sb.Append("--").Append(boundary).Append("\r\n");
            sb.Append("Content-Type: application/gzip; name=\"").Append(filename).Append("\"\r\n");
            sb.Append("Content-Disposition: attachment; filename=\"").Append(filename).Append("\"\r\n");
            sb.Append("Content-Transfer-Encoding: base64\r\n\r\n");
            sb.Append(b64).Append("\r\n");

            sb.Append("--").Append(boundary).Append("--\r\n");

            return sb.ToString();

        }

        #endregion

        #region SendForensicAsync(eval, ...)  — RUF

        /// <summary>
        /// Emit the failure reports the policy's <c>fo</c> asks for one evaluated message - passed
        /// or failed - if forensic reporting is enabled, the policy requests <c>ruf</c>, the
        /// destination consents, and the per-domain hourly budget is not exhausted.
        /// </summary>
        /// <param name="authenticationResults">The Authentication-Results of the DMARC result;
        /// its authserv-id is that of a DKIM or SPF failure report too.</param>
        public async Task SendForensicAsync(DmarcEvaluation      eval,
                                            System.Net.IPAddress sourceIp,
                                            String            reportedHeaders,
                                            String            envelopeFrom,
                                            String            authenticationResults,
                                            CancellationToken ct = default)
        {

            if (!options.EnableForensic || string.IsNullOrEmpty(eval.Ruf))
                return;

            var failures = RequestedFailureReports(eval);
            if (failures.Count == 0)
                return;

            if (!ClaimForensicBudget(eval.PolicyDomain))
            {
                logger.Log(LogLevel.Debug, $"DMARC forensic report for {eval.HeaderFromDomain} suppressed (hourly budget)");
                return;
            }

            var authservId = authenticationResults.Split(';')[0].Trim();
            if (authservId.Length == 0)
                authservId = options.ReportingDomain;

            foreach (var (uri, limit) in await DestinationsAsync(eval.PolicyDomain, eval.Ruf!, Aggregate: false, ct))
            {
                foreach (var failure in failures)
                {

                    // RFC 6591 §3.1: the Authentication-Results "MUST reflect only a single
                    // authentication method's result" - the one the report is about.
                    var results = failure switch {
                                      "signature" => $"{authservId}; dkim={DkimResultText(eval)}" +
                                                         (ArfReport.FailedSignature(eval)?.Domain is { } d ? $" header.d={d}" : ""),
                                      "spf"       => $"{authservId}; spf={SpfResultText(eval.SpfResult)} smtp.{(eval.SpfOnHelo ? "helo" : "mailfrom")}={(eval.SpfOnHelo ? eval.SpfDomain : envelopeFrom)}",
                                      _           => authenticationResults
                                  };

                    var message = ArfReport.Build(eval, sourceIp, reportedHeaders, envelopeFrom,
                                                  options.ReportFromDisplay, uri, options.ReportingDomain,
                                                  results, failure);

                    // RFC 7489 §6.2: "Each URI can have associated with it a maximum report size that may
                    // be sent to it" - a ruf URI as a rua one.
                    if ((UInt64) Encoding.UTF8.GetByteCount(message) > limit)
                    {
                        logger.Log(LogLevel.Info, $"DMARC forensic report for {eval.HeaderFromDomain} exceeds the size limit of {uri} — skipped");
                        continue;
                    }

                    await EnqueueAsync(uri, message, ct);

                    logger.Log(LogLevel.Info, $"DMARC forensic report ({failure}) for {eval.HeaderFromDomain} queued to {uri}");

                }
            }

        }

        /// <summary>
        /// The failure reports fo= asks for (RFC 7489 §6.3), a colon-separated list, "0" by
        /// default - each an Auth-Failure type (RFC 7489 §7.3.1, RFC 6591 §3.3):
        /// "0: Generate a DMARC failure report if all underlying authentication mechanisms fail
        /// to produce an aligned "pass" result"; "1: ... if any underlying authentication
        /// mechanism produced something other than an aligned "pass" result"; "d: Generate a DKIM
        /// failure report if the message had a signature that failed evaluation, regardless of
        /// its alignment"; "s: Generate an SPF failure report if the message failed SPF
        /// evaluation, regardless of its alignment".
        /// </summary>
        public static IReadOnlyList<String> RequestedFailureReports(DmarcEvaluation eval)
        {

            var fo = (eval.FailureOptions ?? "").Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).
                                                 Select(option => option.ToLowerInvariant()).
                                                 ToHashSet();
            if (fo.Count == 0)
                fo.Add("0");

            var reports = new List<String>();

            if ((fo.Contains("0") && !eval.SpfAligned && !eval.DkimAligned) ||
                (fo.Contains("1") && (!eval.SpfAligned || !eval.DkimAligned)))
                reports.Add("dmarc");

            if (fo.Contains("d") && (ArfReport.FailedSignature(eval) is not null ||
                                     (eval.DkimSignatures is null && eval.DkimResult is DkimResult.Fail or DkimResult.PermError)))
                reports.Add("signature");

            if (fo.Contains("s") && eval.SpfResult is SPFResult.Fail or SPFResult.SoftFail or SPFResult.PermError)
                reports.Add("spf");

            return reports;

        }

        private static String DkimResultText(DmarcEvaluation eval)
            => (ArfReport.FailedSignature(eval)?.Result ?? eval.DkimResult) == DkimResult.PermError ? "permerror" : "fail";

        private static String SpfResultText(SPFResult result) => result switch {
            SPFResult.Fail      => "fail",
            SPFResult.SoftFail  => "softfail",
            SPFResult.PermError => "permerror",
            SPFResult.TempError => "temperror",
            SPFResult.Neutral   => "neutral",
            SPFResult.Pass      => "pass",
            _                   => "none"
        };

        private bool ClaimForensicBudget(string domain)
        {
            var hour = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 3600;
            lock (_forensicBudget)
            {
                var cur = _forensicBudget.GetValueOrDefault(domain);
                if (cur.Hour != hour) cur = (hour, 0);
                if (cur.Count >= MaxForensicPerDomainPerHour) return false;
                _forensicBudget[domain] = (hour, cur.Count + 1);
                return true;
            }
        }

        #endregion

        #region (private) helpers

        // RFC 7489 §6.2: rua/ruf are comma-separated URIs, each "mailto:addr" with an optional
        // "!size" limit suffix. Only mailto is supported here.
        public static IEnumerable<String> ParseReportUris(String value)
            => ParseReportDestinations(value).Select(destination => destination.Address);

        /// <summary>
        /// The mailto addresses of a rua or ruf value, in the order given, each with its size
        /// limit in octets, or null where it has none. RFC 7489 §6.4: dmarc-uri = URI [ "!"
        /// 1*DIGIT [ "k" / "m" / "g" / "t" ] ], and §6.4 on the units: "a kilobyte is 2^10, a
        /// megabyte is 2^20, etc."
        /// </summary>
        public static IEnumerable<(String Address, UInt64? SizeLimit)> ParseReportDestinations(String value)
        {
            foreach (var raw in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {

                var     uri    = raw;
                UInt64? limit  = null;
                var     size   = SizeLimit.Match(uri);

                if (size.Success)
                {

                    uri        = uri[..size.Index];
                    var shift  = 10 * "kmgt".IndexOf(Char.ToLowerInvariant(size.Groups[2].Value.FirstOrDefault('b'))) + 10;
                    limit      = UInt64.TryParse(size.Groups[1].Value, out var count) && count <= UInt64.MaxValue >> shift
                                     ? count << shift
                                     : UInt64.MaxValue;   // more than anything this reporter writes

                }

                if (uri.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                {
                    var addr = uri[7..].Trim();
                    if (addr.Contains('@')) yield return (addr, limit);
                }

            }
        }

        private static readonly System.Text.RegularExpressions.Regex SizeLimit = new (@"!([0-9]+)([kmgtKMGT]?)$");

        /// <summary>
        /// The destinations of a report for a policy domain - of its rua or its ruf - in the
        /// order given, each with its size limit (RFC 7489 §7.2.1). RFC 7489 §7.1: a destination
        /// outside the policy domain's organizational domain only with its consent; and where
        /// its own record has a rua or ruf, that replaces the policy domain's: "However, to
        /// prevent loops or indirect abuse, the overriding URI MUST use the same destination
        /// host", and "if the confirming record includes a URI whose host is again different
        /// than the domain publishing that override, the Mail Receiver generating the report
        /// MUST NOT generate a report to either the original or the override URI".
        /// </summary>
        private async Task<IReadOnlyList<(String Address, UInt64? SizeLimit)>> DestinationsAsync(String             PolicyDomain,
                                                                                                 String             Uris,
                                                                                                 Boolean            Aggregate,
                                                                                                 CancellationToken  ct)
        {

            var destinations = new List<(String Address, UInt64? SizeLimit)>();

            foreach (var destination in ParseReportDestinations(Uris))
            {

                var host = AddressDomain(destination.Address);
                if (host.Length == 0)
                    continue;

                var (authorized, rua, ruf) = await dnsVerifier.CheckExternalReportingAsync(PolicyDomain, host, ct);

                if (!authorized)
                {
                    logger.Log(LogLevel.Info, $"DMARC report for {PolicyDomain} to {destination.Address} not authorized (no _report._dmarc consent) — skipped");
                    continue;
                }

                var replacement = Aggregate ? rua : ruf;

                if (replacement is null)
                    destinations.Add(destination);

                else
                {

                    var overrides = ParseReportDestinations(replacement).ToList();

                    if (overrides.Any(@override => !AddressDomain(@override.Address).Equals(host, StringComparison.OrdinalIgnoreCase)))
                    {
                        logger.Log(LogLevel.Info, $"DMARC report for {PolicyDomain}: {host} replaces {destination.Address} with a destination on another host — none used");
                        continue;
                    }

                    destinations.AddRange(overrides);

                }

            }

            return [.. destinations.DistinctBy(destination => destination.Address, StringComparer.OrdinalIgnoreCase)];

        }

        public static String AddressDomain(String address)
        {
            var at = address.LastIndexOf('@');
            return at >= 0 && at < address.Length - 1 ? address[(at + 1)..].Trim().TrimEnd('.').ToLowerInvariant() : "";
        }

        private static byte[] Gzip(String text)
        {
            using var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                gz.Write(bytes, 0, bytes.Length);
            }
            return ms.ToArray();
        }

        // Wrap base64 to 76-character CRLF lines (RFC 2045).
        private static String WrapBase64(String b64)
        {
            var sb = new StringBuilder(b64.Length + b64.Length / 76 * 2);
            for (var i = 0; i < b64.Length; i += 76)
                sb.Append(b64, i, Math.Min(76, b64.Length - i)).Append("\r\n");
            return sb.ToString().TrimEnd('\r', '\n');
        }

        #endregion

    }

}
