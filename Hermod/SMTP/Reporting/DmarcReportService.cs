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

        /// <summary>Record one evaluated inbound message for the aggregate report.</summary>
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

        /// <summary>Drain the aggregator and send one report per reportable domain.</summary>
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

            var reportId = $"{options.ReportingDomain}!{report.PolicyDomain}!{report.WindowBegin.ToUnixTimeSeconds()}!{Guid.NewGuid():N}";
            var xml      = DmarcReportXml.Build(report, options.OrgName, options.ReportFromAddress, reportId);

            foreach (var uri in ParseReportUris(report.Rua))
            {

                var destDomain = AddressDomain(uri);
                if (destDomain.Length == 0)
                    continue;

                // RFC 7489 §7.1 external-destination consent.
                if (!await dnsVerifier.IsExternalReportingAuthorizedAsync(report.PolicyDomain, destDomain, ct))
                {
                    logger.Log(LogLevel.Info, $"DMARC report for {report.PolicyDomain} to {uri} not authorized (no _report._dmarc consent) — skipped");
                    continue;
                }

                var message = BuildAggregateMime(report, uri, reportId, xml);

                await mailQueue.EnqueueAsync(new QueuedMail
                {
                    Id             = UUIDv7.Generate().ToString("N"),
                    EnvelopeFrom   = options.ReportFromAddress,
                    EnvelopeTo     = [uri],
                    MessageContent = message,
                    TargetDomain   = destDomain
                }, ct);

                logger.Log(LogLevel.Info, $"DMARC aggregate report for {report.PolicyDomain} ({report.Rows.Count} rows) queued to {uri}");

            }

        }

        private String BuildAggregateMime(DmarcDomainReport report, String toAddress, String reportId, String xml)
        {

            var gz        = Gzip(xml);
            var b64       = WrapBase64(Convert.ToBase64String(gz));
            var filename  = $"{options.ReportingDomain}!{report.PolicyDomain}!{report.WindowBegin.ToUnixTimeSeconds()}!{report.WindowEnd.ToUnixTimeSeconds()}.xml.gz";
            var boundary  = "dmarc-" + UUIDv7.Generate().ToString("N");
            var date      = DateTimeOffset.UtcNow.ToString("ddd, dd MMM yyyy HH:mm:ss +0000",
                                                           System.Globalization.CultureInfo.InvariantCulture);

            var sb = new StringBuilder();
            sb.Append("From: ").Append(options.ReportFromDisplay).Append("\r\n");
            sb.Append("To: ").Append(toAddress).Append("\r\n");
            // RFC 7489 §7.2.1.1 subject format.
            sb.Append("Subject: Report Domain: ").Append(report.PolicyDomain)
              .Append(" Submitter: ").Append(options.ReportingDomain)
              .Append(" Report-ID: ").Append(reportId).Append("\r\n");
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
        /// Emit a forensic (failure) report for one DMARC-failing message, if forensic
        /// reporting is enabled, the policy requests <c>ruf</c>, <c>fo</c> is not disabled, the
        /// destination consents, and the per-domain hourly budget is not exhausted.
        /// </summary>
        public async Task SendForensicAsync(DmarcEvaluation      eval,
                                            System.Net.IPAddress sourceIp,
                                            String            reportedHeaders,
                                            String            envelopeFrom,
                                            String            authenticationResults,
                                            CancellationToken ct = default)
        {

            if (!options.EnableForensic || !eval.Failed || string.IsNullOrEmpty(eval.Ruf))
                return;

            // fo=none-equivalent? The default "0" reports when all mechanisms fail (which is the
            // case for a DMARC failure); we send unless fo explicitly requests nothing usable.
            var fo = eval.FailureOptions?.Trim();
            if (fo is not null && fo.Length == 0)
                fo = null;

            if (!ClaimForensicBudget(eval.PolicyDomain))
            {
                logger.Log(LogLevel.Debug, $"DMARC forensic report for {eval.HeaderFromDomain} suppressed (hourly budget)");
                return;
            }

            foreach (var uri in ParseReportUris(eval.Ruf!))
            {
                var destDomain = AddressDomain(uri);
                if (destDomain.Length == 0)
                    continue;

                if (!await dnsVerifier.IsExternalReportingAuthorizedAsync(eval.PolicyDomain, destDomain, ct))
                {
                    logger.Log(LogLevel.Info, $"DMARC forensic report to {uri} not authorized — skipped");
                    continue;
                }

                var message = ArfReport.Build(eval, sourceIp, reportedHeaders, envelopeFrom,
                                              options.ReportFromDisplay, uri, options.ReportingDomain,
                                              authenticationResults);

                await mailQueue.EnqueueAsync(
                          new QueuedMail {
                              Id              = UUIDv7.Generate().ToString("N"),
                              EnvelopeFrom    = options.ReportFromAddress,
                              EnvelopeTo      = [uri],
                              MessageContent  = message,
                              TargetDomain    = destDomain
                          },
                          ct
                      );

                logger.Log(LogLevel.Info, $"DMARC forensic report for {eval.HeaderFromDomain} queued to {uri}");
            }

        }

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
        {
            foreach (var raw in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var u   = raw;
                var bang = u.IndexOf('!');
                if (bang >= 0) u = u[..bang];                      // drop the size limit
                if (u.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                {
                    var addr = u[7..].Trim();
                    if (addr.Contains('@')) yield return addr;
                }
            }
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
