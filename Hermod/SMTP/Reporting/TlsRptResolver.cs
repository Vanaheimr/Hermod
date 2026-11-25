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
using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP;

/// <summary>
/// Resolves and caches the TLS-RPT policy (<c>_smtp._tls.&lt;domain&gt;</c> TXT record,
/// <c>v=TLSRPTv1; rua=...</c>) of a domain (RFC 8460 §3).
/// </summary>
public sealed partial class TlsRptResolver(IDNSClient dnsClient, ILogger logger)
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

            // RFC 8460 §3: "records that do not begin with "v=TLSRPTv1;" are discarded. If the
            // number of resulting records is not one, senders MUST assume the recipient domain does
            // not implement TLSRPT." TXT.Text is the record's strings concatenated without spaces,
            // as the RFC asks.
            var records = response.Answers
                                  .OfType<TXT>()
                                  .Select(txt => txt.Text)
                                  .Where (text => TlsRptVersion().IsMatch(text))
                                  .ToList();

            return records.Count == 1
                       ? Parse(records[0])
                       : TlsRptPolicy.None;
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Debug, $"TLS-RPT: policy lookup for {domain} failed: {ex.Message}");
            return TlsRptPolicy.None;
        }
    }

    /// <summary>
    /// Parse a TLS-RPT record: "v=TLSRPTv1; rua=mailto:a@b,https://c/d" (RFC 8460 §3). The
    /// version comes first, and it and the "rua=" key are case-sensitive (%s); a field of
    /// another name is an extension, and ignored.
    /// </summary>
    public static TlsRptPolicy Parse(String record)
    {

        if (!TlsRptVersion().IsMatch(record))
            return TlsRptPolicy.None;

        String? rua = null;

        foreach (var part in record.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Skip(1))
        {
            var eq = part.IndexOf('=');
            if (eq < 0) continue;
            var key = part[..eq].Trim();
            var val = part[(eq + 1)..].Trim();

            if (key == "rua")
                rua = val;
        }

        if (rua is null)
            return TlsRptPolicy.None;

        var mailto = new List<String>();
        var https  = new List<String>();

        foreach (var uri in rua.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // RFC 8460 §3: "In the case of "mailto", reports should be submitted to the
            // specified email address [RFC6068]" - percent-encoded in the URI, commas,
            // exclamation points and semicolons of its own too. A URI with a line break in it is
            // none (RFC 3986 §2), and names no recipient - nor a header field of the report.
            if (uri.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                mailto.AddRange(MailtoUri.Addresses(uri).Where(address => !mailto.Contains(address)));
            else if (uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                https.Add(uri);
        }

        return new TlsRptPolicy(mailto, https);

    }

    // tlsrpt-version, then a field-delim: %s"v=TLSRPTv1" *WSP ";"
    [GeneratedRegex(@"^v=TLSRPTv1[ 	]*;")]
    private static partial Regex TlsRptVersion();

}
