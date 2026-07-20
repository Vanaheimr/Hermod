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
