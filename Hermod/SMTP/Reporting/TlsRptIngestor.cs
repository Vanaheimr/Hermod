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
using System.Text.Json;
using System.Text.RegularExpressions;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP.Server;

/// <summary>
/// Detects and ingests inbound SMTP TLS Reporting (TLS-RPT, RFC 8460) aggregate reports —
/// the reports other sending MTAs deliver to the <c>rua</c> mailbox of a domain that publishes
/// a <c>_smtp._tls</c> policy. A report arrives as a MIME message carrying an
/// <c>application/tlsrpt+gzip</c> (or <c>+json</c>) part; this class extracts the JSON, parses
/// the RFC 8460 §4 schema, persists the raw report, and logs a summary.
/// </summary>
public sealed class TlsRptIngestor(String storageDir, ILogger logger)
{

    #region IsTlsRptReport(message)

    /// <summary>
    /// Whether a received message looks like a TLS-RPT report: it carries the
    /// <c>report-type="tlsrpt"</c> / <c>application/tlsrpt</c> media type or a
    /// <c>TLS-Report-Domain</c> header (RFC 8460 §5.3).
    /// </summary>
    public static Boolean IsTlsRptReport(EMailMessage message)
    {
        foreach (var (name, value) in message.Headers)
        {
            if (name.Equals("TLS-Report-Domain", StringComparison.OrdinalIgnoreCase))
                return true;
            if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) &&
                value.Contains("tlsrpt", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    #endregion

    #region Ingest(message)

    /// <summary>
    /// Extract, parse, persist and log a TLS-RPT report from a received message.
    /// Returns the parsed report, or null if extraction/parsing failed.
    /// </summary>
    public TlsRptReceivedReport? Ingest(EMailMessage message)
    {

        try
        {

            if (!TryExtractReportJson(message, out var json))
            {
                logger.Log(LogLevel.Warning, "TLS-RPT ingest: message looked like a report but no tlsrpt part could be extracted");
                return null;
            }

            var report = ParseJson(json);
            if (report is null)
            {
                logger.Log(LogLevel.Warning, "TLS-RPT ingest: report JSON could not be parsed");
                return null;
            }

            Persist(report, json);

            var org = report.OrganizationName ?? "unknown";
            logger.Log(LogLevel.Info,
                $"TLS-RPT report ingested from {org}: {report.TotalSuccess} successful, {report.TotalFailure} failed session(s) across {report.Policies.Count} policy block(s)");

            foreach (var p in report.Policies.Where(p => p.FailureCount > 0))
                foreach (var f in p.Failures)
                    logger.Log(LogLevel.Info,
                        $"TLS-RPT failure [{p.PolicyType} {p.PolicyDomain}]: {f.ResultType} × {f.FailedSessionCount} (mx={f.ReceivingMxHostname}, from={f.SendingMtaIp})");

            return report;

        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Warning, $"TLS-RPT ingest failed: {ex.Message}");
            return null;
        }

    }

    #endregion

    #region (private) MIME extraction

    /// <summary>
    /// Locate the <c>application/tlsrpt+*</c> body part, decode its transfer encoding and,
    /// if gzip-compressed, decompress it — yielding the report JSON.
    /// </summary>
    private static Boolean TryExtractReportJson(EMailMessage message, out String json)
    {

        json = "";

        var contentType = HeaderValue(message.Headers, "Content-Type") ?? "";
        var boundary    = BoundaryOf(contentType);

        // Candidate raw parts: either the multipart pieces or the whole body.
        var candidates = new List<(String Headers, String Body)>();

        if (boundary is not null)
        {
            foreach (var part in SplitMultipart(message.Body, boundary))
                candidates.Add(part);
        }
        else
        {
            // Non-multipart: the whole message body is the report (Content-Transfer-Encoding
            // is taken from the top-level headers).
            var cte = HeaderValue(message.Headers, "Content-Transfer-Encoding") ?? "";
            candidates.Add(($"Content-Type: {contentType}\r\nContent-Transfer-Encoding: {cte}", message.Body));
        }

        foreach (var (partHeaders, partBody) in candidates)
        {

            var partType = HeaderValueFromBlock(partHeaders, "Content-Type") ?? "";
            if (!partType.Contains("tlsrpt", StringComparison.OrdinalIgnoreCase))
                continue;

            var cte   = (HeaderValueFromBlock(partHeaders, "Content-Transfer-Encoding") ?? "").Trim();
            var bytes = DecodeBody(partBody, cte);

            // Gunzip when the payload is gzip (declared +gzip, or by magic number 0x1f 0x8b).
            if (bytes.Length >= 2 && bytes[0] == 0x1F && bytes[1] == 0x8B)
                bytes = Gunzip(bytes);

            json = Encoding.UTF8.GetString(bytes);
            return json.Contains('{');

        }

        return false;

    }

    // Split a multipart body into (headers, body) pairs using the given boundary.
    private static IEnumerable<(String Headers, String Body)> SplitMultipart(String body, String boundary)
    {

        var delimiter = "--" + boundary;
        var segments  = body.Split(delimiter);

        foreach (var segment in segments)
        {
            var seg = segment;
            if (seg.StartsWith("--"))          // closing boundary "--boundary--"
                continue;
            seg = seg.TrimStart('\r', '\n');
            if (seg.Length == 0)
                continue;

            var split = seg.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            var sep   = 4;
            if (split < 0) { split = seg.IndexOf("\n\n", StringComparison.Ordinal); sep = 2; }
            if (split < 0)
                continue;

            yield return (seg[..split], seg[(split + sep)..]);
        }

    }

    private static Byte[] DecodeBody(String body, String cte)
    {
        if (cte.Equals("base64", StringComparison.OrdinalIgnoreCase))
        {
            var cleaned = Regex.Replace(body, @"\s+", "");
            try { return Convert.FromBase64String(cleaned); }
            catch { return []; }
        }
        // 7bit / 8bit / binary / quoted-printable(best-effort as-is): treat as raw text bytes.
        return Encoding.UTF8.GetBytes(body);
    }

    private static Byte[] Gunzip(Byte[] data)
    {
        using var input  = new MemoryStream(data);
        using var gz     = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gz.CopyTo(output);
        return output.ToArray();
    }

    private static String? BoundaryOf(String contentType)
    {
        var m = Regex.Match(contentType, @"boundary\s*=\s*""?([^"";]+)""?", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static String? HeaderValue(IEnumerable<KeyValuePair<String, String>> headers, String name)
    {
        foreach (var (k, v) in headers)
            if (k.Equals(name, StringComparison.OrdinalIgnoreCase))
                return v;
        return null;
    }

    // Read a header from a raw (already unfolded-ish) header block of a MIME part.
    private static String? HeaderValueFromBlock(String block, String name)
    {
        var unfolded = Regex.Replace(block, @"\r?\n[ \t]+", " ");
        foreach (var line in unfolded.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = line.IndexOf(':');
            if (colon > 0 && line[..colon].Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                return line[(colon + 1)..].Trim();
        }
        return null;
    }

    #endregion

    #region (public static) ParseJson(json) — RFC 8460 §4

    public static TlsRptReceivedReport? ParseJson(String json)
    {

        try
        {

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return null;

            String? Str(JsonElement e, String prop)
                => e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

            Int64 Num(JsonElement e, String prop)
                => e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;

            DateTimeOffset? Date(JsonElement e, String prop)
                => e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String &&
                   DateTimeOffset.TryParse(v.GetString(), out var d) ? d : null;

            DateTimeOffset? start = null, end = null;
            if (root.TryGetProperty("date-range", out var dr) && dr.ValueKind == JsonValueKind.Object)
            {
                start = Date(dr, "start-datetime");
                end   = Date(dr, "end-datetime");
            }

            var policies = new List<TlsRptReceivedPolicy>();
            if (root.TryGetProperty("policies", out var pols) && pols.ValueKind == JsonValueKind.Array)
            {
                foreach (var pol in pols.EnumerateArray())
                {

                    var policyType   = "no-policy-found";
                    String? policyDomain = null;
                    var mxHosts      = new List<String>();

                    if (pol.TryGetProperty("policy", out var pElem) && pElem.ValueKind == JsonValueKind.Object)
                    {
                        policyType   = Str(pElem, "policy-type") ?? "no-policy-found";
                        policyDomain = Str(pElem, "policy-domain");
                        if (pElem.TryGetProperty("mx-host", out var mx) && mx.ValueKind == JsonValueKind.Array)
                            foreach (var h in mx.EnumerateArray())
                                if (h.ValueKind == JsonValueKind.String) mxHosts.Add(h.GetString()!);
                    }

                    Int64 success = 0, failure = 0;
                    if (pol.TryGetProperty("summary", out var sum) && sum.ValueKind == JsonValueKind.Object)
                    {
                        success = Num(sum, "total-successful-session-count");
                        failure = Num(sum, "total-failure-session-count");
                    }

                    var failures = new List<TlsRptReceivedFailure>();
                    if (pol.TryGetProperty("failure-details", out var fds) && fds.ValueKind == JsonValueKind.Array)
                        foreach (var fd in fds.EnumerateArray())
                            failures.Add(new TlsRptReceivedFailure(
                                ResultType:          Str(fd, "result-type") ?? "unknown",
                                SendingMtaIp:        Str(fd, "sending-mta-ip"),
                                ReceivingMxHostname: Str(fd, "receiving-mx-hostname"),
                                ReceivingIp:         Str(fd, "receiving-ip"),
                                FailedSessionCount:  Num(fd, "failed-session-count")));

                    policies.Add(new TlsRptReceivedPolicy(policyType, policyDomain, mxHosts, success, failure, failures));

                }
            }

            return new TlsRptReceivedReport(
                OrganizationName: Str(root, "organization-name"),
                ReportId:         Str(root, "report-id"),
                ContactInfo:      Str(root, "contact-info"),
                StartDateTime:    start,
                EndDateTime:      end,
                Policies:         policies);

        }
        catch
        {
            return null;
        }

    }

    #endregion

    #region (private) Persist(report, json)

    private void Persist(TlsRptReceivedReport report, String json)
    {
        try
        {
            Directory.CreateDirectory(storageDir);
            var org   = Sanitize(report.OrganizationName ?? "unknown");
            var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
            var name  = $"{stamp}-{org}-{UUIDv7.Generate():N}.json";
            File.WriteAllText(Path.Combine(storageDir, name), json);
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Warning, $"TLS-RPT ingest: failed to persist report: {ex.Message}");
        }
    }

    private static String Sanitize(String s)
    {
        var chars = s.Where(c => Char.IsLetterOrDigit(c) || c is '-' or '.').Take(40).ToArray();
        return chars.Length > 0 ? new String(chars) : "unknown";
    }

    #endregion

}
