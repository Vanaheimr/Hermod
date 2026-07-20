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
using System.Text.Json;
using System.Globalization;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP;

/// <summary>
/// Generates and sends SMTP TLS Reporting (TLS-RPT, RFC 8460) aggregate reports. Outbound TLS
/// session outcomes are fed in via <see cref="Record"/>; a background loop (<see cref="RunAsync"/>)
/// drains the aggregator once per interval, and for each domain that publishes a <c>_smtp._tls</c>
/// policy with an <c>rua</c> mailto destination, builds the RFC 8460 §4 JSON, gzips it into an
/// <c>application/tlsrpt+gzip</c> MIME message, and enqueues it through the outbound mail queue
/// (which DKIM-signs it). Unlike DMARC, TLS-RPT has no external-destination consent handshake.
/// </summary>
public sealed class TlsRptReportService(TlsRptAggregator        aggregator,
                                        TlsRptResolver          resolver,
                                        IMailQueue              mailQueue,
                                        TlsRptReportingOptions  options,
                                        ILogger                 logger)
{

    #region Record(ev)

    /// <summary>Record one outbound TLS session outcome for the aggregate report.</summary>
    public void Record(TlsRptEvent ev)
    {
        try { aggregator.Record(ev); }
        catch (Exception ex) { logger.Log(LogLevel.Warning, $"TLS-RPT: record failed: {ex.Message}"); }
    }

    #endregion

    #region RunAsync — periodic aggregate generation

    public async Task RunAsync(CancellationToken ct)
    {

        logger.Log(LogLevel.Info, $"TLS-RPT reporting active (interval {options.Interval.TotalHours:0.#} h)");

        var tick    = TimeSpan.FromMinutes(Math.Clamp(options.Interval.TotalMinutes / 12, 1, 15));
        var lastRun = DateTimeOffset.UtcNow;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(tick, ct);
                aggregator.Flush();

                if (DateTimeOffset.UtcNow - lastRun >= options.Interval)
                {
                    lastRun = DateTimeOffset.UtcNow;
                    await GenerateAndSendAsync(ct);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.Log(LogLevel.Warning, $"TLS-RPT report loop error: {ex.Message}"); }
        }

        aggregator.Flush();

    }

    /// <summary>Drain the aggregator and send one report per domain that publishes a TLS-RPT policy.</summary>
    public async Task GenerateAndSendAsync(CancellationToken ct)
    {
        foreach (var report in aggregator.Drain())
        {
            try { await SendReportAsync(report, ct); }
            catch (Exception ex) { logger.Log(LogLevel.Warning, $"TLS-RPT: send failed for {report.PolicyDomain}: {ex.Message}"); }
        }
    }

    #endregion

    #region SendReportAsync(report)

    private async Task SendReportAsync(TlsRptDomainReport report, CancellationToken ct)
    {

        // Only report to domains that actually publish a TLS-RPT policy with an rua destination.
        var policy = await resolver.GetPolicyAsync(report.PolicyDomain, ct).ConfigureAwait(false);

        if (policy.RuaMailto.Count == 0)
        {
            if (policy.RuaHttps.Count > 0)
                logger.Log(LogLevel.Debug, $"TLS-RPT: {report.PolicyDomain} publishes only https rua (unsupported) — skipped");
            return;
        }

        var reportId = $"{report.WindowEnd.ToUnixTimeSeconds()}.{UUIDv7.Generate():N}@{options.ReportingDomain}";
        var json     = TlsRptReportJson.Build(report, options.OrgName, options.ContactInfo, reportId);

        foreach (var toAddress in policy.RuaMailto)
        {

            var destDomain = AddressDomain(toAddress);
            if (destDomain.Length == 0)
                continue;

            var message = BuildMime(report, toAddress, reportId, json);

            await mailQueue.EnqueueAsync(new QueuedMail
            {
                Id             = UUIDv7.Generate().ToString("N"),
                EnvelopeFrom   = options.ReportFromAddress,
                EnvelopeTo     = [toAddress],
                MessageContent = message,
                TargetDomain   = destDomain
            }, ct).ConfigureAwait(false);

            logger.Log(LogLevel.Info, $"TLS-RPT report for {report.PolicyDomain} ({report.Policies.Count} policy block(s)) queued to {toAddress}");

        }

    }

    // RFC 8460 §5.3: application/tlsrpt+gzip attachment, subject and filename conventions.
    private String BuildMime(TlsRptDomainReport report, String toAddress, String reportId, String json)
    {

        var gz       = Gzip(json);
        var b64      = WrapBase64(Convert.ToBase64String(gz));
        var filename = $"{options.ReportingDomain}!{report.PolicyDomain}!{report.WindowBegin.ToUnixTimeSeconds()}!{report.WindowEnd.ToUnixTimeSeconds()}.json.gz";
        var boundary = "tlsrpt-" + UUIDv7.Generate().ToString("N");
        var date     = DateTimeOffset.UtcNow.ToString("ddd, dd MMM yyyy HH:mm:ss +0000", CultureInfo.InvariantCulture);

        var sb = new StringBuilder();
        sb.Append("From: ").Append(options.ReportFromDisplay).Append("\r\n");
        sb.Append("To: ").Append(toAddress).Append("\r\n");
        // RFC 8460 §5.3 subject convention.
        sb.Append("Subject: Report Domain: ").Append(report.PolicyDomain)
          .Append(" Submitter: ").Append(options.ReportingDomain)
          .Append(" Report-ID: <").Append(reportId).Append(">\r\n");
        sb.Append("TLS-Report-Domain: ").Append(report.PolicyDomain).Append("\r\n");
        sb.Append("TLS-Report-Submitter: ").Append(options.ReportingDomain).Append("\r\n");
        sb.Append("Date: ").Append(date).Append("\r\n");
        sb.Append("Message-ID: <").Append(UUIDv7.Generate().ToString("N")).Append('@').Append(options.ReportingDomain).Append(">\r\n");
        sb.Append("Auto-Submitted: auto-generated\r\n");
        sb.Append("MIME-Version: 1.0\r\n");
        sb.Append("Content-Type: multipart/report; report-type=\"tlsrpt\"; boundary=\"").Append(boundary).Append("\"\r\n");
        sb.Append("\r\n");

        sb.Append("--").Append(boundary).Append("\r\n");
        sb.Append("Content-Type: text/plain; charset=utf-8\r\n\r\n");
        sb.Append("This is an SMTP TLS Reporting (RFC 8460) report for ").Append(report.PolicyDomain)
          .Append(" covering ").Append(report.WindowBegin.UtcDateTime.ToString("u"))
          .Append(" .. ").Append(report.WindowEnd.UtcDateTime.ToString("u")).Append(".\r\n\r\n");

        sb.Append("--").Append(boundary).Append("\r\n");
        sb.Append("Content-Type: application/tlsrpt+gzip\r\n");
        sb.Append("Content-Disposition: attachment; filename=\"").Append(filename).Append("\"\r\n");
        sb.Append("Content-Transfer-Encoding: base64\r\n\r\n");
        sb.Append(b64).Append("\r\n");

        sb.Append("--").Append(boundary).Append("--\r\n");

        return sb.ToString();

    }

    #endregion

    #region (private) helpers

    public static String AddressDomain(String address)
    {
        var at = address.LastIndexOf('@');
        return at >= 0 && at < address.Length - 1 ? address[(at + 1)..].Trim().TrimEnd('.').ToLowerInvariant() : "";
    }

    private static Byte[] Gzip(String text)
    {
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            gz.Write(bytes, 0, bytes.Length);
        }
        return ms.ToArray();
    }

    private static String WrapBase64(String b64)
    {
        var sb = new StringBuilder(b64.Length + b64.Length / 76 * 2);
        for (var i = 0; i < b64.Length; i += 76)
            sb.Append(b64, i, Math.Min(76, b64.Length - i)).Append("\r\n");
        return sb.ToString().TrimEnd('\r', '\n');
    }

    #endregion

}
