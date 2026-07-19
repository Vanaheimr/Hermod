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

using System.Collections.Concurrent;
using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP;

#region MTA-STS Policy

public enum MtaStsMode
{
    None,       // No policy or failed to fetch
    Testing,    // Report failures but don't enforce
    Enforce     // Require valid TLS
}

public sealed partial record MtaStsPolicy
{
    public MtaStsMode      Mode        { get; init; } = MtaStsMode.None;
    public List<String>    MxPatterns  { get; init; } = [];
    public TimeSpan        MaxAge      { get; init; } = TimeSpan.Zero;
    public DateTimeOffset  FetchedAt   { get; init; } = Timestamp.Now;
    public String?         PolicyId    { get; init; }
    public Boolean         IsValid
        => Mode != MtaStsMode.None &&
           Timestamp.Now - FetchedAt < MaxAge;


    /// <summary>
    /// Check if an MX host matches the policy
    /// </summary>
    public bool MatchesMx(string mxHost)
    {
        foreach (var pattern in MxPatterns)
        {
            if (pattern.StartsWith("*."))
            {
                // Wildcard match: *.example.com matches mail.example.com
                var suffix = pattern[1..]; // .example.com
                if (mxHost.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ||
                    mxHost.Equals(pattern[2..], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else
            {
                // Exact match
                if (mxHost.Equals(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public static MtaStsPolicy None => new() { Mode = MtaStsMode.None };
}

#endregion

#region MTA-STS Resolver

/// <summary>
/// Resolves and caches MTA-STS policies for domains.
/// MTA-STS requires:
/// 1. DNS TXT record at _mta-sts.domain.com
/// 2. HTTPS fetch of policy from https://mta-sts.domain.com/.well-known/mta-sts.txt
/// </summary>
public sealed partial class MtaStsResolver : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly DNSClient _dnsClient;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, MtaStsPolicy> _cache = new();
    private readonly SemaphoreSlim _fetchLock = new(5); // Max 5 concurrent fetches

    public MtaStsResolver(DNSClient dnsClient, ILogger logger)
    {
        _dnsClient = dnsClient;
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AchimSMTP/1.0 MTA-STS");
    }

    /// <summary>
    /// Get MTA-STS policy for a domain
    /// </summary>
    public async Task<MtaStsPolicy> GetPolicyAsync(string domain, CancellationToken ct = default)
    {
        domain = domain.ToLowerInvariant();

        // Check cache first
        if (_cache.TryGetValue(domain, out var cached) && cached.IsValid)
        {
            return cached;
        }

        await _fetchLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (_cache.TryGetValue(domain, out cached) && cached.IsValid)
            {
                return cached;
            }

            var policy = await FetchPolicyAsync(domain, ct);
            _cache[domain] = policy;
            return policy;
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    private async Task<MtaStsPolicy> FetchPolicyAsync(string domain, CancellationToken ct)
    {
        try
        {
            // Step 1: Check for _mta-sts DNS TXT record
            var txtRecord = await LookupMtaStsTxtAsync(domain, ct);
            if (txtRecord is null)
            {
                _logger.Log(LogLevel.Debug, $"No MTA-STS TXT record for {domain}");
                return MtaStsPolicy.None;
            }

            // Parse TXT record for policy ID
            var policyId = ParsePolicyId(txtRecord);
            
            // Step 2: Fetch policy via HTTPS
            var policyUrl = $"https://mta-sts.{domain}/.well-known/mta-sts.txt";
            
            _logger.Log(LogLevel.Debug, $"Fetching MTA-STS policy from {policyUrl}");
            
            var response = await _httpClient.GetAsync(policyUrl, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.Log(LogLevel.Debug, $"MTA-STS policy fetch failed: {response.StatusCode}");
                return MtaStsPolicy.None;
            }

            var policyText = await response.Content.ReadAsStringAsync(ct);
            var policy = ParsePolicy(policyText, policyId);
            
            if (policy.Mode != MtaStsMode.None)
            {
                _logger.Log(LogLevel.Info, $"MTA-STS policy for {domain}: mode={policy.Mode}, mx={string.Join(",", policy.MxPatterns)}");
            }

            return policy;
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Debug, $"MTA-STS lookup failed for {domain}: {ex.Message}");
            return MtaStsPolicy.None;
        }
    }

    private async Task<string?> LookupMtaStsTxtAsync(string domain, CancellationToken ct)
    {
        try
        {
            // DNSServiceName tolerates the leading-underscore label "_mta-sts".
            var response = await _dnsClient.Query(
                                     DNSServiceName.Parse($"_mta-sts.{domain}"),
                                     [ DNSResourceRecordTypes.TXT ],
                                     CancellationToken: ct
                                 );

            return response.Answers.
                       OfType<TXT>().
                       Select(txt => txt.Text).
                       FirstOrDefault(text => text.Contains("v=STSv1", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Debug, $"MTA-STS TXT lookup failed for {domain}: {ex.Message}");
            return null;
        }
    }

    private static string? ParsePolicyId(string txtRecord)
    {
        var match = PolicyIdRegex().Match(txtRecord);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static MtaStsPolicy ParsePolicy(string policyText, string? policyId)
    {
        var lines = policyText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        var mode = MtaStsMode.None;
        var mxPatterns = new List<string>();
        var maxAge = TimeSpan.FromDays(1); // Default

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("version:", StringComparison.OrdinalIgnoreCase))
            {
                // Must be STSv1
                if (!trimmed.Contains("STSv1", StringComparison.OrdinalIgnoreCase))
                    return MtaStsPolicy.None;
            }
            else if (trimmed.StartsWith("mode:", StringComparison.OrdinalIgnoreCase))
            {
                var modeValue = trimmed[5..].Trim().ToLowerInvariant();
                mode = modeValue switch
                {
                    "enforce" => MtaStsMode.Enforce,
                    "testing" => MtaStsMode.Testing,
                    "none" => MtaStsMode.None,
                    _ => MtaStsMode.None
                };
            }
            else if (trimmed.StartsWith("mx:", StringComparison.OrdinalIgnoreCase))
            {
                var mx = trimmed[3..].Trim();
                if (!string.IsNullOrEmpty(mx))
                    mxPatterns.Add(mx);
            }
            else if (trimmed.StartsWith("max_age:", StringComparison.OrdinalIgnoreCase))
            {
                if (long.TryParse(trimmed[8..].Trim(), out var seconds))
                    maxAge = TimeSpan.FromSeconds(seconds);
            }
        }

        return new MtaStsPolicy
        {
            Mode = mode,
            MxPatterns = mxPatterns,
            MaxAge = maxAge,
            PolicyId = policyId,
            FetchedAt = Timestamp.Now
        };
    }

    [GeneratedRegex(@"id=([a-zA-Z0-9]+)")]
    private static partial Regex PolicyIdRegex();

    public void Dispose()
    {
        _httpClient.Dispose();
        _fetchLock.Dispose();
    }
}

#endregion

// SMTP TLS Reporting (TLS-RPT, RFC 8460) lives in Reporting/TlsRptReporting.cs
// (TlsRptResolver / TlsRptAggregator / TlsRptReportJson / TlsRptReportService).
