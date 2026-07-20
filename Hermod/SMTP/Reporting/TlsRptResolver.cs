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
