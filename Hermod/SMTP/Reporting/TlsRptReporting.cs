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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP;

#region TLS-RPT event (recorded per outbound TLS session)

/// <summary>Which TLS security policy governed an outbound delivery attempt (RFC 8460 §4.2).</summary>
public enum TlsRptPolicyType
{
    /// <summary>MTA-STS policy (RFC 8461).</summary>
    Sts,
    /// <summary>DANE / TLSA policy (RFC 7672).</summary>
    Tlsa,
    /// <summary>No STS or DANE policy applied to the session.</summary>
    NoPolicyFound
}

/// <summary>
/// One outbound TLS session outcome, fed to the TLS-RPT aggregator (RFC 8460).
/// </summary>
/// <param name="PolicyDomain">The recipient (next-hop) domain whose TLS-RPT policy this belongs to.</param>
/// <param name="PolicyType">The governing policy type.</param>
/// <param name="MxHost">The receiving MX host name.</param>
/// <param name="ReceivingIp">The receiving MX IP address (if known).</param>
/// <param name="SendingIp">Our sending IP address (if known).</param>
/// <param name="Success">Whether a compliant TLS session was established.</param>
/// <param name="FailureType">On failure, the RFC 8460 §4.3 result-type; null on success.</param>
public sealed record TlsRptEvent(String            PolicyDomain,
                                 TlsRptPolicyType  PolicyType,
                                 String?           MxHost,
                                 String?           ReceivingIp,
                                 String?           SendingIp,
                                 Boolean           Success,
                                 String?           FailureType);

#endregion

#region TLS-RPT policy resolver

/// <summary>The parsed TLS-RPT reporting policy of a domain (RFC 8460 §3).</summary>
public sealed record TlsRptPolicy(IReadOnlyList<String> RuaMailto,
                                  IReadOnlyList<String> RuaHttps)
{
    public Boolean HasDestination => RuaMailto.Count > 0 || RuaHttps.Count > 0;
    public static readonly TlsRptPolicy None = new([], []);
}

/// <summary>
/// Resolves and caches the TLS-RPT policy (<c>_smtp._tls.&lt;domain&gt;</c> TXT record,
/// <c>v=TLSRPTv1; rua=...</c>) of a domain (RFC 8460 §3).
/// </summary>
public sealed class TlsRptResolver(DNSClient dnsClient, ILogger logger)
{

    private readonly Dictionary<String, (TlsRptPolicy Policy, DateTimeOffset Expires)> cache = new (StringComparer.OrdinalIgnoreCase);
    private readonly Object cacheLock = new ();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    public async Task<TlsRptPolicy> GetPolicyAsync(String domain, CancellationToken ct = default)
    {

        domain = domain.TrimEnd('.').ToLowerInvariant();

        lock (cacheLock)
        {
            if (cache.TryGetValue(domain, out var hit) && hit.Expires > DateTimeOffset.UtcNow)
                return hit.Policy;
        }

        var policy = await FetchAsync(domain, ct).ConfigureAwait(false);

        lock (cacheLock)
            cache[domain] = (policy, DateTimeOffset.UtcNow + CacheTtl);

        return policy;

    }

    private async Task<TlsRptPolicy> FetchAsync(String domain, CancellationToken ct)
    {
        try
        {
            // DNSServiceName tolerates the leading-underscore labels "_smtp._tls".
            var response = await dnsClient.Query(
                                     DNSServiceName.Parse($"_smtp._tls.{domain}"),
                                     [ DNSResourceRecordTypes.TXT ],
                                     CancellationToken: ct
                                 ).ConfigureAwait(false);

            var record = response.Answers
                                 .OfType<TXT>()
                                 .Select(txt => txt.Text)
                                 .FirstOrDefault(t => t.Contains("v=TLSRPTv1", StringComparison.OrdinalIgnoreCase));

            if (record is null)
                return TlsRptPolicy.None;

            return Parse(record);
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Debug, $"TLS-RPT: policy lookup for {domain} failed: {ex.Message}");
            return TlsRptPolicy.None;
        }
    }

    /// <summary>
    /// Parse a TLS-RPT record: "v=TLSRPTv1; rua=mailto:a@b,https://c/d" (RFC 8460 §3).
    /// </summary>
    public static TlsRptPolicy Parse(String record)
    {

        String? rua = null;
        var version = false;

        foreach (var part in record.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=');
            if (eq < 0) continue;
            var key = part[..eq].Trim();
            var val = part[(eq + 1)..].Trim();

            if (key.Equals("v", StringComparison.OrdinalIgnoreCase))
                version = val.Equals("TLSRPTv1", StringComparison.OrdinalIgnoreCase);
            else if (key.Equals("rua", StringComparison.OrdinalIgnoreCase))
                rua = val;
        }

        if (!version || rua is null)
            return TlsRptPolicy.None;

        var mailto = new List<String>();
        var https  = new List<String>();

        foreach (var uri in rua.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (uri.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            {
                var addr = uri[7..].Trim();
                if (addr.Contains('@')) mailto.Add(addr);
            }
            else if (uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                https.Add(uri);
        }

        return new TlsRptPolicy(mailto, https);

    }

}

#endregion

#region TLS-RPT aggregator

#region Snapshot types (immutable, consumed by the JSON builder)

/// <summary>One failure-details entry of a TLS-RPT report (RFC 8460 §4.4).</summary>
public sealed record TlsRptFailureRow(String   ResultType,
                                      String?  SendingMtaIp,
                                      String?  ReceivingMxHostname,
                                      String?  ReceivingIp,
                                      Int64    FailedSessionCount);

/// <summary>One policy block of a TLS-RPT report (RFC 8460 §4.2/§4.4).</summary>
public sealed record TlsRptPolicyReport(String                          PolicyType,   // sts|tlsa|no-policy-found
                                        IReadOnlyList<String>           MxHosts,
                                        Int64                           SuccessCount,
                                        IReadOnlyList<TlsRptFailureRow> Failures);

/// <summary>A per-domain TLS-RPT report ready for JSON serialization + delivery.</summary>
public sealed record TlsRptDomainReport(String                            PolicyDomain,
                                        DateTimeOffset                    WindowBegin,
                                        DateTimeOffset                    WindowEnd,
                                        IReadOnlyList<TlsRptPolicyReport> Policies);

#endregion

/// <summary>
/// Accumulates outbound TLS session outcomes into aggregate counts, grouped by recipient
/// (policy) domain and, within a domain, by policy type and failure signature (RFC 8460 §4).
/// The running window is persisted to a JSON file so counts survive a restart.
/// <see cref="Drain"/> snapshots every domain with recorded sessions and starts fresh windows.
/// </summary>
public sealed class TlsRptAggregator
{

    #region (internal, mutable, serialized) state

    private sealed class DomainWindow
    {
        public String                            PolicyDomain { get; set; } = "";
        public DateTimeOffset                    WindowBegin  { get; set; } = DateTimeOffset.UtcNow;
        public Dictionary<String, PolicyBucket>  Policies     { get; set; } = [];   // key = policy type
    }

    private sealed class PolicyBucket
    {
        public String                            PolicyType { get; set; } = "no-policy-found";
        public HashSet<String>                   MxHosts    { get; set; } = [];
        public Int64                             Success    { get; set; }
        public Dictionary<String, FailureBucket> Failures   { get; set; } = [];
    }

    private sealed class FailureBucket
    {
        public String   ResultType          { get; set; } = "";
        public String?  SendingMtaIp        { get; set; }
        public String?  ReceivingMxHostname { get; set; }
        public String?  ReceivingIp         { get; set; }
        public Int64    Count               { get; set; }
    }

    #endregion

    private readonly Dictionary<String, DomainWindow> domains = new (StringComparer.OrdinalIgnoreCase);
    private readonly Object   stateLock = new ();
    private readonly String   statePath;
    private readonly ILogger  logger;
    private Boolean           dirty;

    private static readonly JsonSerializerOptions JsonOptions = new () { WriteIndented = false };

    public TlsRptAggregator(String StatePath, ILogger Logger)
    {
        statePath = StatePath;
        logger    = Logger;
        Load();
    }

    #region Record(ev)

    /// <summary>Add one outbound TLS session outcome to the current window for its policy domain.</summary>
    public void Record(TlsRptEvent ev)
    {

        if (String.IsNullOrEmpty(ev.PolicyDomain))
            return;

        var policyType = PolicyTypeString(ev.PolicyType);

        lock (stateLock)
        {

            if (!domains.TryGetValue(ev.PolicyDomain, out var window))
            {
                window = new DomainWindow { PolicyDomain = ev.PolicyDomain, WindowBegin = DateTimeOffset.UtcNow };
                domains[ev.PolicyDomain] = window;
            }

            if (!window.Policies.TryGetValue(policyType, out var bucket))
            {
                bucket = new PolicyBucket { PolicyType = policyType };
                window.Policies[policyType] = bucket;
            }

            if (ev.MxHost is not null)
                bucket.MxHosts.Add(ev.MxHost.TrimEnd('.'));

            if (ev.Success)
                bucket.Success++;
            else
            {
                var resultType = String.IsNullOrEmpty(ev.FailureType) ? "validation-failure" : ev.FailureType!;
                var key        = String.Join('|', resultType, ev.SendingIp, ev.MxHost, ev.ReceivingIp);

                if (bucket.Failures.TryGetValue(key, out var fail))
                    fail.Count++;
                else
                    bucket.Failures[key] = new FailureBucket
                    {
                        ResultType          = resultType,
                        SendingMtaIp        = ev.SendingIp,
                        ReceivingMxHostname = ev.MxHost?.TrimEnd('.'),
                        ReceivingIp         = ev.ReceivingIp,
                        Count               = 1
                    };
            }

            dirty = true;

        }

    }

    #endregion

    #region Drain()

    /// <summary>
    /// Snapshot every domain that has recorded sessions, then reset those windows. The caller
    /// decides (via the domain's TLS-RPT policy) whether an actual report is sent.
    /// </summary>
    public IReadOnlyList<TlsRptDomainReport> Drain()
    {

        var now     = DateTimeOffset.UtcNow;
        var reports = new List<TlsRptDomainReport>();

        lock (stateLock)
        {

            foreach (var (_, window) in domains)
            {

                if (window.Policies.Count == 0)
                    continue;

                var policies = window.Policies.Values
                    .Where(p => p.Success > 0 || p.Failures.Count > 0)
                    .Select(p => new TlsRptPolicyReport(
                        PolicyType:   p.PolicyType,
                        MxHosts:      p.MxHosts.OrderBy(h => h, StringComparer.Ordinal).ToList(),
                        SuccessCount: p.Success,
                        Failures:     p.Failures.Values.Select(f => new TlsRptFailureRow(
                                          f.ResultType, f.SendingMtaIp, f.ReceivingMxHostname, f.ReceivingIp, f.Count)).ToList()))
                    .ToList();

                if (policies.Count == 0)
                    continue;

                reports.Add(new TlsRptDomainReport(window.PolicyDomain, window.WindowBegin, now, policies));

                window.Policies.Clear();
                window.WindowBegin = now;

            }

            if (reports.Count > 0)
                dirty = true;

            Save_NoLock();

        }

        return reports;

    }

    #endregion

    #region Persistence

    public void Flush()
    {
        lock (stateLock)
            Save_NoLock();
    }

    private void Save_NoLock()
    {
        if (!dirty)
            return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
            var tmp = statePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(domains, JsonOptions));
            File.Move(tmp, statePath, overwrite: true);
            dirty = false;
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Warning, $"TLS-RPT aggregator: failed to persist state: {ex.Message}");
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(statePath))
                return;
            var loaded = JsonSerializer.Deserialize<Dictionary<String, DomainWindow>>(File.ReadAllText(statePath), JsonOptions);
            if (loaded is null)
                return;
            foreach (var (k, v) in loaded)
                domains[k] = v;
            logger.Log(LogLevel.Info, $"TLS-RPT aggregator: restored {domains.Count} domain window(s)");
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Warning, $"TLS-RPT aggregator: failed to load state: {ex.Message}");
        }
    }

    #endregion

    private static String PolicyTypeString(TlsRptPolicyType t) => t switch {
        TlsRptPolicyType.Sts   => "sts",
        TlsRptPolicyType.Tlsa  => "tlsa",
        _                      => "no-policy-found"
    };

}

#endregion

#region TLS-RPT JSON report builder (RFC 8460 §4)

/// <summary>Serializes a <see cref="TlsRptDomainReport"/> to the RFC 8460 §4 JSON schema.</summary>
public static class TlsRptReportJson
{

    public static String Build(TlsRptDomainReport  report,
                               String              organizationName,
                               String              contactInfo,
                               String              reportId)
    {

        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {

            w.WriteStartObject();

            w.WriteString("organization-name", organizationName);

            w.WriteStartObject("date-range");
            w.WriteString("start-datetime", report.WindowBegin.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            w.WriteString("end-datetime",   report.WindowEnd.UtcDateTime.ToString  ("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            w.WriteEndObject();

            w.WriteString("contact-info", contactInfo);
            w.WriteString("report-id",    reportId);

            w.WriteStartArray("policies");
            foreach (var policy in report.Policies)
            {
                w.WriteStartObject();

                w.WriteStartObject("policy");
                w.WriteString("policy-type", policy.PolicyType);
                if (policy.MxHosts.Count > 0)
                {
                    w.WriteStartArray("mx-host");
                    foreach (var mx in policy.MxHosts)
                        w.WriteStringValue(mx);
                    w.WriteEndArray();
                }
                w.WriteString("policy-domain", report.PolicyDomain);
                w.WriteEndObject();

                w.WriteStartObject("summary");
                w.WriteNumber("total-successful-session-count", policy.SuccessCount);
                w.WriteNumber("total-failure-session-count",    policy.Failures.Sum(f => f.FailedSessionCount));
                w.WriteEndObject();

                if (policy.Failures.Count > 0)
                {
                    w.WriteStartArray("failure-details");
                    foreach (var f in policy.Failures)
                    {
                        w.WriteStartObject();
                        w.WriteString("result-type", f.ResultType);
                        if (f.SendingMtaIp        is not null) w.WriteString("sending-mta-ip",        f.SendingMtaIp);
                        if (f.ReceivingMxHostname is not null) w.WriteString("receiving-mx-hostname", f.ReceivingMxHostname);
                        if (f.ReceivingIp         is not null) w.WriteString("receiving-ip",          f.ReceivingIp);
                        w.WriteNumber("failed-session-count", f.FailedSessionCount);
                        w.WriteEndObject();
                    }
                    w.WriteEndArray();
                }

                w.WriteEndObject();
            }
            w.WriteEndArray();

            w.WriteEndObject();

        }

        return Encoding.UTF8.GetString(stream.ToArray());

    }

}

#endregion

#region TLS-RPT report service

/// <summary>Static options for <see cref="TlsRptReportService"/>.</summary>
public sealed record TlsRptReportingOptions(String    OrgName,
                                            String    ReportFromDisplay,   // e.g. "TLS Reports <tls-reports@mx.example>"
                                            String    ReportFromAddress,   // bare address used as envelope sender
                                            String    ReportingDomain,     // domain of the report sender (filenames / message-ids)
                                            String    ContactInfo,         // contact-info field (e.g. postmaster@...)
                                            TimeSpan  Interval);

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

        var reportId = $"{report.WindowEnd.ToUnixTimeSeconds()}.{Guid.NewGuid():N}@{options.ReportingDomain}";
        var json     = TlsRptReportJson.Build(report, options.OrgName, options.ContactInfo, reportId);

        foreach (var toAddress in policy.RuaMailto)
        {

            var destDomain = AddressDomain(toAddress);
            if (destDomain.Length == 0)
                continue;

            var message = BuildMime(report, toAddress, reportId, json);

            await mailQueue.EnqueueAsync(new QueuedMail
            {
                Id             = Guid.NewGuid().ToString("N"),
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
        var boundary = "tlsrpt-" + Guid.NewGuid().ToString("N");
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
        sb.Append("Message-ID: <").Append(Guid.NewGuid().ToString("N")).Append('@').Append(options.ReportingDomain).Append(">\r\n");
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

#endregion
