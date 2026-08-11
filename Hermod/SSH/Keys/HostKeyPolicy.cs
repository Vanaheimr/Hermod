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

using org.GraphDefined.Vanaheimr.Hermod;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>The verdict a host-key source reaches for a presented key.</summary>
    public enum HostKeyVerdict
    {
        /// <summary>Accept the key (trusted).</summary>
        Accept,
        /// <summary>Reject the key (explicitly distrusted, e.g. a mismatch or revocation).</summary>
        Reject,
        /// <summary>No opinion — defer to the next source in the chain.</summary>
        Unknown
    }


    /// <summary>The context passed to an interactive TOFU callback.</summary>
    public sealed record HostKeyPrompt(String Host, IPPort Port, SshPublicKey PublicKey)
    {
        /// <summary>The <c>SHA256:…</c> fingerprint of the presented key.</summary>
        public String Sha256Fingerprint => PublicKey.Sha256Fingerprint;
        /// <summary>The legacy <c>MD5:…</c> fingerprint of the presented key.</summary>
        public String Md5Fingerprint    => PublicKey.Md5Fingerprint;
    }


    /// <summary>
    /// An ordered chain of host-key trust sources (RFC 4251 §4.1 host authentication). Evaluation stops
    /// at the first source that reaches an <see cref="HostKeyVerdict.Accept"/> or
    /// <see cref="HostKeyVerdict.Reject"/>; a source with no opinion defers to the next. If every source
    /// defers, the key is rejected (strict by default). Sources: explicit pins, <c>known_hosts</c> files,
    /// and an interactive trust-on-first-use callback.
    /// </summary>
    public sealed class HostKeyPolicy
    {

        #region Data

        private readonly List<Func<HostKeyPrompt, HostKeyVerdict>> sources;

        #endregion

        #region Constructor(s)

        private HostKeyPolicy(List<Func<HostKeyPrompt, HostKeyVerdict>> Sources)
        {
            this.sources = Sources;
        }

        #endregion


        #region (static) Pin(FingerprintsOrKeyLines) / PinKey(Key)

        /// <summary>
        /// Start a policy that accepts a key matching any of the given pins. A pin is either an
        /// <c>SHA256:…</c> / <c>MD5:…</c> fingerprint or a full public-key line (<c>ssh-ed25519 AAAA…</c>).
        /// A key that matches no pin defers to the next source (so pins can be combined with known_hosts).
        /// </summary>
        public static HostKeyPolicy Pin(params String[] FingerprintsOrKeyLines)
        {

            var fingerprints  = new List<String>();
            var keyBlobs      = new List<Byte[]>();

            foreach (var pin in FingerprintsOrKeyLines)
            {
                if (pin.StartsWith("SHA256:", StringComparison.Ordinal) || pin.StartsWith("MD5:", StringComparison.OrdinalIgnoreCase))
                    fingerprints.Add(pin);
                else if (SshPublicKey.TryParse(pin, out var key))
                    keyBlobs.Add(key!.Blob);
                else
                    throw new ArgumentException($"'{pin}' is neither a fingerprint nor a public-key line.", nameof(FingerprintsOrKeyLines));
            }

            return new HostKeyPolicy([
                prompt =>
                    fingerprints.Any(fp => SshFingerprint.Matches(prompt.PublicKey.Blob, fp)) ||
                    keyBlobs.Any(blob => blob.AsSpan().SequenceEqual(prompt.PublicKey.Blob))
                        ? HostKeyVerdict.Accept
                        : HostKeyVerdict.Unknown
            ]);

        }

        /// <summary>Start a policy that pins the exact given public key.</summary>
        public static HostKeyPolicy PinKey(SshPublicKey Key)
            => Pin(Key.ToAuthorizedKeyLine());

        /// <summary>Start a policy that accepts host certificates signed by a trusted host CA.</summary>
        public static HostKeyPolicy HostCertificate(SshCertificateAuthorityTrust Trust, TimeProvider? TimeProvider = null)
            => new HostKeyPolicy([]).OrHostCertificate(Trust, TimeProvider);

        #endregion

        #region OrHostCertificate(Trust, TimeProvider = null)

        /// <summary>
        /// Add a host-certificate source: a presented host certificate is accepted when it validates
        /// (host type, trusted CA, signature, validity, the host as a principal) against <paramref name="Trust"/>.
        /// </summary>
        public HostKeyPolicy OrHostCertificate(SshCertificateAuthorityTrust Trust, TimeProvider? TimeProvider = null)
        {

            var clock = TimeProvider ?? System.TimeProvider.System;

            sources.Add(prompt =>
            {

                if (!SshCertificate.IsCertificateAlgorithm(prompt.PublicKey.Algorithm))
                    return HostKeyVerdict.Unknown;

                if (!SshCertificate.TryParse(prompt.PublicKey.Blob, out var certificate))
                    return HostKeyVerdict.Reject;

                return SshCertificateValidator.Validate(certificate!, SshCertType.Host, prompt.Host, Trust, clock.GetUtcNow()).IsValid
                           ? HostKeyVerdict.Accept
                           : HostKeyVerdict.Reject;

            });

            return this;

        }

        #endregion

        #region OrKnownHosts(KnownHosts) / OrKnownHostsFile(Path)

        /// <summary>
        /// Add a <c>known_hosts</c> source: accepts a matching known key, rejects a revoked key or a
        /// changed host key (host known but key differs), and defers when the host is unknown.
        /// </summary>
        public HostKeyPolicy OrKnownHosts(KnownHostsFile KnownHosts)
        {

            sources.Add(prompt =>
            {

                var matches = KnownHosts.Lookup(prompt.Host, prompt.Port);
                if (matches.Count == 0)
                    return HostKeyVerdict.Unknown;

                // A host certificate: validate it against the matching @cert-authority entries.
                if (SshCertificate.IsCertificateAlgorithm(prompt.PublicKey.Algorithm))
                {

                    if (!SshCertificate.TryParse(prompt.PublicKey.Blob, out var certificate))
                        return HostKeyVerdict.Reject;

                    var caEntries = matches.Where(e => e.Marker == KnownHostMarker.CertAuthority).ToList();
                    if (caEntries.Count == 0)
                        return HostKeyVerdict.Unknown;

                    var trust = new SshCertificateAuthorityTrust();
                    foreach (var entry in caEntries)
                        trust.TrustCA(entry.PublicKey.Blob);

                    return SshCertificateValidator.Validate(certificate!, SshCertType.Host, prompt.Host, trust, DateTimeOffset.UtcNow).IsValid
                               ? HostKeyVerdict.Accept
                               : HostKeyVerdict.Reject;

                }

                if (matches.Any(e => e.Marker == KnownHostMarker.Revoked && e.PublicKey.Blob.AsSpan().SequenceEqual(prompt.PublicKey.Blob)))
                    return HostKeyVerdict.Reject;

                if (matches.Any(e => e.Marker == KnownHostMarker.None && e.PublicKey.Blob.AsSpan().SequenceEqual(prompt.PublicKey.Blob)))
                    return HostKeyVerdict.Accept;

                // The host is known but presents a key we do not have on file — a host-key change.
                return matches.Any(e => e.Marker == KnownHostMarker.None)
                           ? HostKeyVerdict.Reject
                           : HostKeyVerdict.Unknown;

            });

            return this;

        }

        /// <summary>Add a <c>known_hosts</c> file source from a path (missing file ⇒ no entries).</summary>
        public HostKeyPolicy OrKnownHostsFile(String Path)
            => OrKnownHosts(KnownHostsFile.Parse(File.Exists(Path) ? File.ReadAllText(Path) : ""));

        #endregion

        #region OrInteractiveTofu(Callback) / Or(Source)

        /// <summary>
        /// Add a trust-on-first-use source: the callback receives the host and key fingerprints and
        /// returns true to accept. Typically the last source in the chain.
        /// </summary>
        public HostKeyPolicy OrInteractiveTofu(Func<HostKeyPrompt, Boolean> Callback)
        {
            sources.Add(prompt => Callback(prompt) ? HostKeyVerdict.Accept : HostKeyVerdict.Reject);
            return this;
        }

        /// <summary>Add a custom source returning a verdict.</summary>
        public HostKeyPolicy Or(Func<HostKeyPrompt, HostKeyVerdict> Source)
        {
            sources.Add(Source);
            return this;
        }

        #endregion


        #region Verify(Host, Port, HostKeyBlob)

        /// <summary>Evaluate the chain for a presented host-key blob.</summary>
        public Boolean Verify(String Host, IPPort Port, Byte[] HostKeyBlob)
        {

            var prompt = new HostKeyPrompt(Host, Port, new SshPublicKey(HostKeyBlob));

            foreach (var source in sources)
            {
                var verdict = source(prompt);
                if (verdict == HostKeyVerdict.Accept) return true;
                if (verdict == HostKeyVerdict.Reject) return false;
            }

            return false;   // strict: no source vouched for the key

        }

        #endregion

        #region ForHost(Host, Port)

        /// <summary>
        /// A verification callback bound to a host and port, suitable for the transport's
        /// <c>VerifyHostKey</c> seam (which sees only the key blob).
        /// </summary>
        public Func<Byte[], Boolean> ForHost(String Host, IPPort Port)
            => blob => Verify(Host, Port, blob);

        #endregion

    }

}
