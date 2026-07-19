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

using System.Text.Json;
using System.Text.Json.Serialization;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    #region Snapshot types (immutable, consumed by the XML builder)

    /// <summary>One aggregated &lt;record&gt; row of a DMARC aggregate report.</summary>
    public sealed record DmarcReportRow(
        String   SourceIp,
        Int64    Count,
        String   Disposition,   // none|quarantine|reject
        String   DkimAligned,   // pass|fail  (policy_evaluated/dkim)
        String   SpfAligned,    // pass|fail  (policy_evaluated/spf)
        String   HeaderFrom,
        String?  DkimAuthDomain,
        String   DkimAuthResult,
        String?  SpfAuthDomain,
        String   SpfAuthResult
    );

    /// <summary>A per-domain aggregate report ready for XML serialization + delivery.</summary>
    public sealed record DmarcDomainReport(
        String            PolicyDomain,
        String            Rua,
        String            RequestedPolicy,
        String?           SubdomainPolicy,
        Int32             Percent,
        String            Adkim,          // r|s
        String            Aspf,           // r|s
        DateTimeOffset    WindowBegin,
        DateTimeOffset    WindowEnd,
        IReadOnlyList<DmarcReportRow> Rows
    );

    #endregion

    /// <summary>
    /// Accumulates per-message DMARC evaluation results into aggregate counts, grouped by the
    /// policy (From/organizational) domain and, within a domain, by source IP + authentication
    /// results (RFC 7489 §7.2). The running window is persisted to a JSON file so counts
    /// survive a restart. <see cref="Drain"/> atomically snapshots the reportable domains and
    /// starts fresh windows for them.
    /// </summary>
    public sealed class DmarcAggregator
    {

        #region (internal, mutable, serialized) state

        private sealed class DomainWindow
        {
            public String                       PolicyDomain { get; set; } = "";
            public String?                      Rua          { get; set; }
            public String                       P            { get; set; } = "none";
            public String?                      Sp           { get; set; }
            public Int32                        Pct          { get; set; } = 100;
            public String                       Adkim        { get; set; } = "r";
            public String                       Aspf         { get; set; } = "r";
            public DateTimeOffset               WindowBegin  { get; set; } = DateTimeOffset.UtcNow;
            public Dictionary<String, RowState> Rows         { get; set; } = [];
        }

        private sealed class RowState
        {
            public String   SourceIp       { get; set; } = "";
            public String   Disposition    { get; set; } = "none";
            public String   DkimAligned    { get; set; } = "fail";
            public String   SpfAligned     { get; set; } = "fail";
            public String   HeaderFrom     { get; set; } = "";
            public String?  DkimAuthDomain { get; set; }
            public String   DkimAuthResult { get; set; } = "none";
            public String?  SpfAuthDomain  { get; set; }
            public String   SpfAuthResult  { get; set; } = "none";
            public Int64    Count          { get; set; }
        }

        #endregion

        private readonly Dictionary<String, DomainWindow> _domains = new (StringComparer.OrdinalIgnoreCase);
        private readonly Object   _lock = new ();
        private readonly String   _statePath;
        private readonly ILogger  _logger;
        private Boolean           _dirty;

        private static readonly JsonSerializerOptions JsonOptions = new ()
        {
            WriteIndented = false,
            Converters    = { new JsonStringEnumConverter() }
        };

        public DmarcAggregator(String statePath, ILogger logger)
        {
            _statePath = statePath;
            _logger    = logger;
            Load();
        }

        #region Record(evaluation, sourceIp)

        /// <summary>Add one evaluated message to the current window for its policy domain.</summary>
        public void Record(DmarcEvaluation eval, System.Net.IPAddress sourceIp)
        {

            lock (_lock)
            {

                if (!_domains.TryGetValue(eval.PolicyDomain, out var window))
                {
                    window = new DomainWindow
                    {
                        PolicyDomain = eval.PolicyDomain,
                        WindowBegin  = DateTimeOffset.UtcNow
                    };
                    _domains[eval.PolicyDomain] = window;
                }

                // Keep the published-policy fields fresh from the latest observation.
                window.Rua   = eval.Rua;
                window.P     = eval.RequestedPolicy;
                window.Sp    = eval.SubdomainPolicy;
                window.Pct   = eval.Percent;
                window.Adkim = eval.StrictDkim ? "s" : "r";
                window.Aspf  = eval.StrictSpf  ? "s" : "r";

                var row = new RowState
                {
                    SourceIp       = sourceIp.ToString(),
                    Disposition    = Disp(eval.Disposition),
                    DkimAligned    = eval.DkimAligned ? "pass" : "fail",
                    SpfAligned     = eval.SpfAligned  ? "pass" : "fail",
                    HeaderFrom     = eval.HeaderFromDomain,
                    DkimAuthDomain = eval.DkimDomain,
                    DkimAuthResult = DkimStr(eval.DkimResult),
                    SpfAuthDomain  = eval.SpfDomain,
                    SpfAuthResult  = SpfStr(eval.SpfResult),
                    Count          = 1
                };

                var key = string.Join('|', row.SourceIp, row.Disposition, row.DkimAligned, row.SpfAligned,
                                            row.HeaderFrom, row.DkimAuthDomain, row.DkimAuthResult,
                                            row.SpfAuthDomain, row.SpfAuthResult);

                if (window.Rows.TryGetValue(key, out var existing))
                    existing.Count++;
                else
                    window.Rows[key] = row;

                _dirty = true;

            }

        }

        #endregion

        #region Drain()

        /// <summary>
        /// Snapshot every domain that has an <c>rua</c> destination and at least one row, then
        /// reset those windows to empty (new window begins now). Domains without an rua are
        /// left accumulating. The returned reports carry the window's begin/end times.
        /// </summary>
        public IReadOnlyList<DmarcDomainReport> Drain()
        {

            var now     = DateTimeOffset.UtcNow;
            var reports = new List<DmarcDomainReport>();

            lock (_lock)
            {

                foreach (var (domain, window) in _domains)
                {

                    if (string.IsNullOrEmpty(window.Rua) || window.Rows.Count == 0)
                        continue;

                    reports.Add(new DmarcDomainReport(
                        PolicyDomain:    window.PolicyDomain,
                        Rua:             window.Rua!,
                        RequestedPolicy: window.P,
                        SubdomainPolicy: window.Sp,
                        Percent:         window.Pct,
                        Adkim:           window.Adkim,
                        Aspf:            window.Aspf,
                        WindowBegin:     window.WindowBegin,
                        WindowEnd:       now,
                        Rows:            window.Rows.Values.Select(r => new DmarcReportRow(
                                             r.SourceIp, r.Count, r.Disposition, r.DkimAligned, r.SpfAligned,
                                             r.HeaderFrom, r.DkimAuthDomain, r.DkimAuthResult,
                                             r.SpfAuthDomain, r.SpfAuthResult)).ToList()
                    ));

                    // Reset this domain's window.
                    window.Rows.Clear();
                    window.WindowBegin = now;

                }

                if (reports.Count > 0)
                    _dirty = true;

                Save_NoLock();

            }

            return reports;

        }

        #endregion

        #region Persistence

        public void Flush()
        {
            lock (_lock)
                Save_NoLock();
        }

        private void Save_NoLock()
        {
            if (!_dirty)
                return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
                var tmp = _statePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(_domains, JsonOptions));
                File.Move(tmp, _statePath, overwrite: true);   // atomic-ish replace
                _dirty = false;
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Warning, $"DMARC aggregator: failed to persist state: {ex.Message}");
            }
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_statePath))
                    return;
                var loaded = JsonSerializer.Deserialize<Dictionary<String, DomainWindow>>(
                                 File.ReadAllText(_statePath), JsonOptions);
                if (loaded is null)
                    return;
                foreach (var (k, v) in loaded)
                    _domains[k] = v;
                _logger.Log(LogLevel.Info, $"DMARC aggregator: restored {_domains.Count} domain window(s)");
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Warning, $"DMARC aggregator: failed to load state: {ex.Message}");
            }
        }

        #endregion

        #region (private) result-to-string helpers

        private static String Disp(DmarcDisposition d) => d switch {
            DmarcDisposition.Reject     => "reject",
            DmarcDisposition.Quarantine => "quarantine",
            _                           => "none"
        };

        private static String SpfStr(SPFResult r) => r switch {
            SPFResult.Pass      => "pass",
            SPFResult.Fail      => "fail",
            SPFResult.SoftFail  => "softfail",
            SPFResult.Neutral   => "neutral",
            SPFResult.TempError => "temperror",
            SPFResult.PermError => "permerror",
            _                   => "none"
        };

        private static String DkimStr(DkimResult r) => r switch {
            DkimResult.Pass      => "pass",
            DkimResult.Fail      => "fail",
            DkimResult.TempError => "temperror",
            DkimResult.PermError => "permerror",
            _                    => "none"
        };

        #endregion

    }

}
