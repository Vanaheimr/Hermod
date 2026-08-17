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

using System.Net.Sockets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    using IPAddress = System.Net.IPAddress;

    /// <summary>
    /// The restrictions a credential imposes on the session it authenticates — the enforced half of an
    /// OpenSSH certificate's critical options.
    ///
    /// <para>
    /// A CA issues a restricted certificate to grant <i>less</i> than a normal login: one fixed command,
    /// or only from one subnet. Those constraints only mean something if the server applies them, which
    /// is what this type carries from authentication into the session. A critical option that cannot be
    /// carried here must cause the certificate to be <b>rejected</b> — see
    /// <see cref="SshCertificateValidator"/>: silently dropping a restriction grants more access than
    /// the CA intended, which is the worst of the three outcomes.
    /// </para>
    /// </summary>
    /// <param name="ForcedCommand">
    /// The command this credential is confined to (<c>force-command</c>). When set, it replaces whatever
    /// the client asked to run — including a plain shell request — and the client's original request is
    /// preserved as <see cref="SshExecContext.OriginalCommand"/> (OpenSSH exposes it as
    /// <c>SSH_ORIGINAL_COMMAND</c>).
    /// </param>
    /// <param name="SourceAddresses">
    /// The addresses this credential may be used from (<c>source-address</c>). Empty means unrestricted.
    /// </param>
    /// <param name="AllowPty">
    /// Whether a pseudo-terminal may be allocated (<c>authorized_keys</c> <c>no-pty</c> / <c>restrict</c>).
    /// </param>
    /// <param name="AllowPortForwarding">
    /// Whether this session may open forwarding channels (<c>no-port-forwarding</c> / <c>restrict</c>).
    /// Intersects with the server's own <see cref="ForwardingPolicy"/> — the stricter side wins.
    /// </param>
    public sealed record SshSessionRestrictions(String?                ForcedCommand        = null,
                                                IReadOnlyList<IpCidr>? SourceAddresses      = null,
                                                Boolean                AllowPty             = true,
                                                Boolean                AllowPortForwarding  = true)
    {

        /// <summary>
        /// No restrictions — an ordinary, unrestricted credential.
        /// </summary>
        public static SshSessionRestrictions None { get; } = new ();

        /// <summary>
        /// Combine two restriction sets so that the <b>stricter</b> side always wins — a credential can
        /// only ever narrow what another source already permits, never widen it. Used where a
        /// certificate and an <c>authorized_keys</c> entry both apply.
        /// </summary>
        /// <param name="Other">The restrictions to intersect with.</param>
        public SshSessionRestrictions And(SshSessionRestrictions Other)
        {

            // Two different forced commands cannot both be honoured; keep this one and let the caller
            // decide, rather than silently picking the more permissive.
            var command = ForcedCommand ?? Other.ForcedCommand;

            var sources = (SourceAddresses, Other.SourceAddresses) switch {
                              (null, var b)            => b,
                              (var a, null)            => a,
                              var (a, b)               => (IReadOnlyList<IpCidr>) [.. a!.Where(cidr => b!.Any(o => o.Equals(cidr)))]
                          };

            return new SshSessionRestrictions(command,
                                              sources,
                                              AllowPty            && Other.AllowPty,
                                              AllowPortForwarding && Other.AllowPortForwarding);

        }

        /// <summary>
        /// The critical options this implementation actually enforces.
        /// </summary>
        public static IReadOnlySet<String> EnforcedCriticalOptions { get; }
            = new HashSet<String>(StringComparer.Ordinal) { "force-command", "source-address" };

        /// <summary>
        /// Whether anything is actually restricted.
        /// </summary>
        public Boolean IsRestricted
            => ForcedCommand is not null || SourceAddresses?.Count > 0 || !AllowPty || !AllowPortForwarding;

        /// <summary>
        /// Whether a peer at the given address may use this credential. An unknown peer address is
        /// refused whenever a <c>source-address</c> restriction exists — a restriction that cannot be
        /// evaluated must not be treated as satisfied.
        /// </summary>
        /// <param name="PeerAddress">The client's address, or null if it could not be determined.</param>
        public Boolean AllowsSource(IPAddress? PeerAddress)
        {

            if (SourceAddresses is null || SourceAddresses.Count == 0)
                return true;

            if (PeerAddress is null)
                return false;

            // An IPv4 peer arriving over a dual-stack socket shows up as ::ffff:a.b.c.d; compare it as
            // the IPv4 address the CA would have written into the certificate.
            var address = PeerAddress.IsIPv4MappedToIPv6
                              ? PeerAddress.MapToIPv4()
                              : PeerAddress;

            foreach (var cidr in SourceAddresses)
                if (cidr.Contains(address))
                    return true;

            return false;

        }

        /// <summary>
        /// The command a session should actually run, given what the client asked for.
        /// </summary>
        /// <param name="RequestedCommand">The command the client requested (empty for a shell).</param>
        public String EffectiveCommand(String RequestedCommand)
            => ForcedCommand ?? RequestedCommand;

    }


    /// <summary>
    /// Reads the critical options of an OpenSSH certificate into the restrictions the server enforces.
    /// </summary>
    public static class SshCertificateRestrictions
    {

        #region FromCertificate(Certificate)

        /// <summary>
        /// Extract <c>force-command</c> and <c>source-address</c> from a certificate's critical options.
        /// Per PROTOCOL.certkeys each option's data is itself an SSH string holding the value.
        /// </summary>
        /// <param name="Certificate">The certificate whose options to read.</param>
        public static SshSessionRestrictions FromCertificate(SshCertificate Certificate)
        {

            String?       forcedCommand = null;
            List<IpCidr>? sources       = null;

            foreach (var option in Certificate.CriticalOptions)
            {

                switch (option.Key)
                {

                    case "force-command":
                        forcedCommand = DecodeStringValue(option.Value);
                        break;

                    case "source-address":
                        {

                            var list = DecodeStringValue(option.Value);
                            if (list is null)
                                break;

                            sources = [];
                            foreach (var entry in list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                                sources.Add(IpCidr.Parse(entry));

                            break;

                        }

                }

            }

            return forcedCommand is null && sources is null
                       ? SshSessionRestrictions.None
                       : new SshSessionRestrictions(forcedCommand, sources);

        }

        #endregion

        #region (private) DecodeStringValue(Value)

        // The option data is an SSH string wrapping the value; tolerate a bare value as well, since a
        // malformed option must not be silently read as "no restriction".
        private static String? DecodeStringValue(Byte[] Value)
        {

            if (Value.Length == 0)
                return null;

            try
            {
                var reader = new SshPacketReader(Value);
                var text   = reader.ReadString();
                return text.Length == 0 ? null : text;
            }
            catch (SshWireException)
            {
                throw new SshWireException("A certificate critical option carries a malformed value.");
            }

        }

        #endregion

    }

}
