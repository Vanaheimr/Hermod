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
using System.Net;
using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP;

/// <summary>
/// Resolves and caches MTA-STS policies for domains (RFC 8461).
/// MTA-STS requires:
/// 1. DNS TXT record at _mta-sts.domain.com
/// 2. HTTPS fetch of policy from https://mta-sts.domain.com/.well-known/mta-sts.txt
/// </summary>
public sealed partial class MtaStsResolver : IDisposable
{

    /// <summary>
    /// The largest policy body taken (RFC 8461 §3.3 suggests 64 kilobytes).
    /// </summary>
    public const Int32   MaxPolicySize  = 64 * 1024;

    /// <summary>
    /// The largest max_age a policy may have (RFC 8461 §3.2): a year, in seconds.
    /// </summary>
    public const UInt32  MaxMaxAge      = 31_557_600;

    private readonly HttpClient _httpClient;
    private readonly IDNSClient _dnsClient;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, MtaStsPolicy> _cache = new();
    private readonly SemaphoreSlim _fetchLock = new(5); // Max 5 concurrent fetches

    /// <summary>
    /// Create a resolver.
    /// </summary>
    /// <param name="dnsClient">The DNS client for the _mta-sts TXT records.</param>
    /// <param name="logger">A logger.</param>
    /// <param name="httpHandler">
    /// The HTTP handler that fetches policies - for a proxy, or a policy host whose certificate
    /// the system does not trust. By default one that follows no redirects (RFC 8461 §3.3).
    /// Whichever it is, a policy reached through a redirect is not taken.
    /// </param>
    public MtaStsResolver(IDNSClient dnsClient, ILogger logger, HttpMessageHandler? httpHandler = null)
    {
        _dnsClient = dnsClient;
        _logger = logger;
        _httpClient = new HttpClient(httpHandler ?? CreateDefaultHandler(), disposeHandler: httpHandler is null)
        {
            Timeout                       = TimeSpan.FromSeconds(30),
            MaxResponseContentBufferSize  = MaxPolicySize
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AchimSMTP/1.0 MTA-STS");
    }

    /// <summary>
    /// RFC 8461 §3.3: "HTTP 3xx redirects MUST NOT be followed, and HTTP caching ... MUST NOT be
    /// used" - HttpClient does no HTTP caching, and this handler follows no redirects.
    /// </summary>
    internal static HttpMessageHandler CreateDefaultHandler()
        => new SocketsHttpHandler { AllowAutoRedirect = false };

    /// <summary>
    /// The MTA-STS policy of a Policy Domain: the one its TXT record announces, fetched when it is
    /// new, or the cached one while it is fresh and nothing newer can be had (RFC 8461 §3.3, §5.1).
    /// The TXT record is looked at on every call, so that a delivery that fails under a policy
    /// has always checked for a new one first (§5).
    /// </summary>
    public async Task<MtaStsPolicy> GetPolicyAsync(string domain, CancellationToken ct = default)
    {

        domain = domain.TrimEnd('.').ToLowerInvariant();

        var cached = _cache.TryGetValue(domain, out var entry) && entry.IsFresh
                         ? entry
                         : null;

        // Step 1: the TXT record. Without a usable one there is no live policy - but a cached one
        // still applies (§3.1: its absence "is not by itself sufficient to remove a sender's
        // previously cached policy").
        var policyId = await LookupMtaStsTxtAsync(domain, ct);
        if (policyId is null)
        {
            _logger.Log(LogLevel.Debug, $"No usable MTA-STS TXT record for {domain}{(cached is not null ? "; the cached policy applies" : "")}");
            return cached ?? MtaStsPolicy.None;
        }

        // The cached policy is the current one.
        if (cached is not null && cached.PolicyId == policyId)
            return cached;

        await _fetchLock.WaitAsync(ct);
        try
        {

            // Double-check after acquiring lock
            if (_cache.TryGetValue(domain, out entry) && entry.IsFresh && entry.PolicyId == policyId)
                return entry;

            // Step 2: the policy. When it cannot be had, the cached one applies (§3.3: "if no
            // 'live' policy can be discovered ... but a valid (non-expired) policy exists in the
            // sender's cache, the sender MUST apply that cached policy").
            var policy = await FetchPolicyAsync(domain, policyId, ct);
            if (policy is null)
                return cached ?? MtaStsPolicy.None;

            _cache[domain] = policy;
            return policy;

        }
        finally
        {
            _fetchLock.Release();
        }

    }

    /// <summary>
    /// The policy at the Policy Host, or null when there is none to be had: no 200, a redirect,
    /// not text/plain, too large, not a policy.
    /// </summary>
    private async Task<MtaStsPolicy?> FetchPolicyAsync(string domain, string policyId, CancellationToken ct)
    {
        try
        {

            var policyUrl = new Uri($"https://mta-sts.{domain}/.well-known/mta-sts.txt");

            _logger.Log(LogLevel.Debug, $"Fetching MTA-STS policy from {policyUrl}");

            using var response = await _httpClient.GetAsync(policyUrl, ct);

            // §3.3: "Policies fetched via HTTPS are only valid if the HTTP response code is 200 (OK).
            // HTTP 3xx redirects MUST NOT be followed" - also when a handler of the operator's did.
            if (response.StatusCode != HttpStatusCode.OK)
            {
                _logger.Log(LogLevel.Warning, $"MTA-STS policy fetch for {domain} failed: {(Int32) response.StatusCode} {response.StatusCode}");
                return null;
            }

            if (response.RequestMessage?.RequestUri is { } fetchedFrom && fetchedFrom != policyUrl)
            {
                _logger.Log(LogLevel.Warning, $"MTA-STS policy for {domain} came through a redirect to {fetchedFrom}; not taken");
                return null;
            }

            // §3.2: "senders SHOULD validate that the media type is 'text/plain'".
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType is not null && !mediaType.Equals("text/plain", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Log(LogLevel.Warning, $"MTA-STS policy for {domain} is {mediaType}, not text/plain; not taken");
                return null;
            }

            var policy = ParsePolicy(await response.Content.ReadAsStringAsync(ct), policyId);

            if (policy is null)
                _logger.Log(LogLevel.Warning, $"MTA-STS policy for {domain} is not a valid policy");
            else
                _logger.Log(LogLevel.Info, $"MTA-STS policy for {domain}: mode={policy.Mode}, mx={String.Join(",", policy.MxPatterns)}, max_age={policy.MaxAge.TotalSeconds}");

            return policy;

        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.Log(LogLevel.Warning, $"MTA-STS policy fetch for {domain} failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// The policy id of the Policy Domain's one MTA-STS TXT record, or null when there is not
    /// exactly one usable record (RFC 8461 §3.1).
    /// </summary>
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

            // "records that do not begin with 'v=STSv1;' are discarded. If the number of resulting
            // records is not one, or if the resulting record is syntactically invalid, senders MUST
            // assume the recipient domain does not have an available MTA-STS Policy." TXT.Text is
            // the record's strings concatenated without spaces, as the RFC asks.
            var records = response.Answers.
                              OfType<TXT>().
                              Select(txt => txt.Text).
                              Where (text => StsVersion().IsMatch(text)).
                              ToList();

            return records.Count == 1
                       ? ParsePolicyId(records[0])
                       : null;
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Debug, $"MTA-STS TXT lookup failed for {domain}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// The id of an MTA-STS TXT record (RFC 8461 §3.1), or null when the record is not
    /// syntactically valid: "v=STSv1" first, an "id" of 1 to 32 letters and digits, and every
    /// other field a well-formed extension.
    /// </summary>
    internal static string? ParsePolicyId(string txtRecord)
    {

        var fields = txtRecord.Split(';').Select(field => field.Trim(' ', '\t')).ToList();

        // A trailing delimiter is allowed; an empty field elsewhere is not.
        if (fields.Count > 1 && fields[^1].Length == 0)
            fields.RemoveAt(fields.Count - 1);

        if (fields.Count < 2 || fields[0] != "v=STSv1")
            return null;

        string? id = null;

        foreach (var field in fields.Skip(1))
        {
            var match = StsField().Match(field);
            if (!match.Success)
                return null;

            if (match.Groups["name"].Value == "id")
            {
                if (!PolicyIdValue().IsMatch(match.Groups["value"].Value))
                    return null;
                id ??= match.Groups["value"].Value;     // a repeated field: the first counts
            }
        }

        return id;

    }

    /// <summary>
    /// An MTA-STS policy (RFC 8461 §3.2), or null when the text is not a valid one: "version",
    /// "mode" and "max_age" each required, "mx" at least once unless the mode is "none". A
    /// repeated field other than "mx" counts the first time; unknown fields are ignored.
    /// </summary>
    internal static MtaStsPolicy? ParsePolicy(string policyText, string? policyId)
    {

        string? version = null;
        string? mode    = null;
        string? maxAge  = null;
        var mxPatterns  = new List<string>();

        foreach (var rawLine in policyText.Split('\n'))
        {

            var line = rawLine.TrimEnd('\r');

            if (line.Length == 0)
                continue;

            var match = PolicyField().Match(line);
            if (!match.Success)
                return null;

            var name  = match.Groups["name"].Value;
            var value = match.Groups["value"].Value.TrimEnd(' ', '\t');

            switch (name)
            {
                case "version":  version ??= value; break;
                case "mode":     mode    ??= value; break;
                case "max_age":  maxAge  ??= value; break;
                case "mx":       mxPatterns.Add(value); break;
            }

        }

        if (version != "STSv1")
            return null;

        var parsedMode = mode switch {
                             "enforce"  => MtaStsMode.Enforce,
                             "testing"  => MtaStsMode.Testing,
                             "none"     => MtaStsMode.None,
                             _          => (MtaStsMode?) null
                         };

        if (parsedMode is null || maxAge is null || !MaxAgeValue().IsMatch(maxAge))
            return null;

        if (parsedMode != MtaStsMode.None && (mxPatterns.Count == 0 || !mxPatterns.All(MxPattern().IsMatch)))
            return null;

        return new MtaStsPolicy
        {
            Mode        = parsedMode.Value,
            MxPatterns  = mxPatterns,
            MaxAge      = TimeSpan.FromSeconds(Math.Min(UInt64.Parse(maxAge), MaxMaxAge)),
            PolicyId    = policyId,
            FetchedAt   = Timestamp.Now
        };

    }

    // sts-version, then a field delimiter: "v=STSv1" *WSP ";"
    [GeneratedRegex(@"^v=STSv1[ \t]*;")]
    private static partial Regex StsVersion();

    // sts-id or sts-extension: a name of up to 32 characters, "=", a value without "=", ";", SP, CTLs
    [GeneratedRegex(@"^(?<name>[A-Za-z0-9][A-Za-z0-9_.-]{0,31})=(?<value>[\x21-\x3A\x3C\x3E-\x7E]+)$")]
    private static partial Regex StsField();

    [GeneratedRegex(@"^[A-Za-z0-9]{1,32}$")]
    private static partial Regex PolicyIdValue();

    // sts-policy-field: name ":" *WSP value
    [GeneratedRegex(@"^(?<name>[A-Za-z0-9][A-Za-z0-9_.-]{0,31}):[ \t]*(?<value>.*)$")]
    private static partial Regex PolicyField();

    [GeneratedRegex(@"^[0-9]{1,10}$")]
    private static partial Regex MaxAgeValue();

    // ["*."] Domain (RFC 5321 §4.1.2)
    [GeneratedRegex(@"^(\*\.)?[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?)*$")]
    private static partial Regex MxPattern();

    public void Dispose()
    {
        _httpClient.Dispose();
        _fetchLock.Dispose();
    }

}

// SMTP TLS Reporting (TLS-RPT, RFC 8460) lives in Reporting/TlsRptReporting.cs
// (TlsRptResolver / TlsRptAggregator / TlsRptReportJson / TlsRptReportService).
