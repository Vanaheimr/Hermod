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

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// The result of an SSHFP lookup: the records found and whether the DNS answer was DNSSEC-authenticated.
    /// </summary>
    /// <param name="Records">The SSHFP records for the host (may be empty).</param>
    /// <param name="DnssecValidated">Whether the resolver validated the answer with DNSSEC (the AD bit).</param>
    public sealed record SshfpLookupResult(IReadOnlyList<SshfpRecord> Records, Boolean DnssecValidated)
    {
        /// <summary>
        /// An empty, unvalidated result.
        /// </summary>
        public static readonly SshfpLookupResult Empty = new ([], false);
    }


    /// <summary>
    /// The dependency seam for SSHFP DNS lookups. The SSH core takes no DNS dependency; a thin adapter binds
    /// this to the Hermod DNS client, and an in-memory fake serves the tests.
    /// </summary>
    public interface ISshfpResolver
    {
        /// <summary>
        /// Look up the SSHFP records for a host.
        /// </summary>
        ValueTask<SshfpLookupResult> QueryAsync(String Host, CancellationToken CancellationToken = default);
    }


    /// <summary>
    /// How much to trust SSHFP records for host-key verification (mirrors OpenSSH <c>VerifyHostKeyDNS</c>).
    /// </summary>
    public enum SshfpTrust
    {
        /// <summary>
        /// Ignore SSHFP entirely.
        /// </summary>
        Off,
        /// <summary>
        /// SSHFP never auto-accepts; a match only annotates the TOFU decision.
        /// </summary>
        Advisory,
        /// <summary>
        /// Auto-accept a matching record only when the answer was DNSSEC-validated.
        /// </summary>
        RequireDnssec
    }


    /// <summary>
    /// The outcome of matching a host key against its SSHFP records under a trust mode.
    /// </summary>
    public enum SshfpVerdict
    {
        /// <summary>
        /// No SSHFP records were published (or trust is Off) — SSHFP has no opinion.
        /// </summary>
        NoRecords,
        /// <summary>
        /// A record matched and the answer was DNSSEC-validated — safe to auto-accept.
        /// </summary>
        SecureMatch,
        /// <summary>
        /// A record matched but the answer was not validated — advisory only, never auto-accept.
        /// </summary>
        InsecureMatch,
        /// <summary>
        /// Records exist but none match the presented key — reject.
        /// </summary>
        Mismatch
    }


    /// <summary>
    /// Verifies a presented host key against its published SSHFP records under a <see cref="SshfpTrust"/> mode.
    /// </summary>
    public sealed class SshfpVerifier
    {

        private readonly ISshfpResolver  resolver;
        private readonly SshfpTrust      trust;

        /// <summary>
        /// Create a verifier over a resolver and a trust mode.
        /// </summary>
        public SshfpVerifier(ISshfpResolver Resolver, SshfpTrust Trust = SshfpTrust.RequireDnssec)
        {
            this.resolver  = Resolver;
            this.trust     = Trust;
        }

        /// <summary>
        /// Look up and evaluate the SSHFP records for the host against the presented key blob.
        /// </summary>
        public async ValueTask<SshfpVerdict> VerifyAsync(String Host, Byte[] HostKeyBlob, CancellationToken CancellationToken = default)
        {

            if (trust == SshfpTrust.Off)
                return SshfpVerdict.NoRecords;

            var result = await resolver.QueryAsync(Host, CancellationToken).ConfigureAwait(false);

            // Prefer SHA-256 records; SHA-1-only is at best advisory.
            var relevant = result.Records.Where(r => r.Algorithm == SshfpRecord.FromBlob(HostKeyBlob)[0].Algorithm).ToList();
            if (relevant.Count == 0)
                return SshfpVerdict.NoRecords;

            var strong = relevant.Where(r => r.FingerprintType == SshfpFingerprintType.Sha256).ToList();
            var toCheck = strong.Count > 0 ? strong : relevant;

            if (!toCheck.Any(r => r.Matches(HostKeyBlob)))
                return SshfpVerdict.Mismatch;

            // A match: DNSSEC + RequireDnssec (and a strong hash) auto-accepts; otherwise advisory only.
            var secure = result.DnssecValidated && strong.Count > 0 && trust == SshfpTrust.RequireDnssec;
            return secure ? SshfpVerdict.SecureMatch : SshfpVerdict.InsecureMatch;

        }

    }


    /// <summary>
    /// An in-memory <see cref="ISshfpResolver"/> for tests and demos: map host → records + a DNSSEC flag.
    /// </summary>
    public sealed class InMemorySshfpResolver : ISshfpResolver
    {

        private readonly Dictionary<String, SshfpLookupResult> zone = new (StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Publish records for a host (optionally marked DNSSEC-validated).
        /// </summary>
        public InMemorySshfpResolver Add(String Host, IReadOnlyList<SshfpRecord> Records, Boolean DnssecValidated = false)
        {
            zone[Host] = new SshfpLookupResult(Records, DnssecValidated);
            return this;
        }

        /// <summary>
        /// Publish the records derived from a host key for a host.
        /// </summary>
        public InMemorySshfpResolver Add(String Host, ISshHostKey HostKey, Boolean DnssecValidated = false)
            => Add(Host, SshfpRecord.FromHostKey(HostKey), DnssecValidated);

        public ValueTask<SshfpLookupResult> QueryAsync(String Host, CancellationToken CancellationToken = default)
            => ValueTask.FromResult(zone.TryGetValue(Host, out var result) ? result : SshfpLookupResult.Empty);

    }

}
