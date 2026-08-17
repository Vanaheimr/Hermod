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
    /// A jump host in a ProxyJump chain (<c>ssh -J</c>): a <c>[user@]host[:port]</c> with — in the full
    /// client — its own host-key policy and credentials, since a bastion is not more trusted than the target.
    /// </summary>
    /// <param name="Host">The bastion host.</param>
    /// <param name="Port">The bastion port (default 22).</param>
    /// <param name="Username">The user on the bastion, if specified.</param>
    public sealed record SshJumpHost(String Host, UInt16 Port = 22, String? Username = null)
    {

        /// <summary>
        /// Parse a single <c>[user@]host[:port]</c> (bracket form <c>[::1]:22</c> for IPv6 literals).
        /// </summary>
        public static SshJumpHost Parse(String Text)
        {

            var rest      = Text.Trim();
            String? user  = null;

            var at = rest.IndexOf('@');
            if (at >= 0)
            {
                user = rest[..at];
                rest = rest[(at + 1)..];
            }

            var host  = rest;
            UInt16 port = 22;

            if (rest.StartsWith('['))
            {
                var close = rest.IndexOf(']');
                host = rest[1..close];
                if (close + 1 < rest.Length && rest[close + 1] == ':')
                    port = UInt16.Parse(rest[(close + 2)..]);
            }
            else
            {
                var colon = rest.LastIndexOf(':');
                // A single colon (and no others) is a host:port; multiple colons ⇒ a bare IPv6 literal.
                if (colon >= 0 && rest.IndexOf(':') == colon)
                {
                    host = rest[..colon];
                    port = UInt16.Parse(rest[(colon + 1)..]);
                }
            }

            return new SshJumpHost(host, port, user);

        }

        /// <summary>
        /// Parse a comma-separated jump chain (<c>-J host1,host2</c>), in traversal order.
        /// </summary>
        public static IReadOnlyList<SshJumpHost> ParseChain(String Text)
            => Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .Select(Parse)
                   .ToList();

    }


    /// <summary>
    /// A transport reached <i>through</i> a jump host: the tunneled SSH session and the underlying
    /// <c>direct-tcpip</c> stream. Disposing tears the hop down (transport first, then the tunnel), so a chain
    /// disposes in reverse.
    /// </summary>
    public sealed class SshTunneledConnection : IAsyncDisposable
    {

        /// <summary>
        /// The end-to-end transport to the target (its host-key verification and auth ran through the tunnel).
        /// </summary>
        public SshTransport Transport { get; }

        private readonly SshChannelStream tunnel;

        internal SshTunneledConnection(SshTransport Transport, SshChannelStream Tunnel)
        {
            this.Transport  = Transport;
            this.tunnel     = Tunnel;
        }

        /// <summary>
        /// Tear down this hop: dispose the tunneled transport, then the tunnel stream.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            try { Transport.Dispose(); } catch { }
            await tunnel.DisposeAsync().ConfigureAwait(false);
        }

    }


    /// <summary>
    /// ProxyJump / jump-host chaining: run a fresh SSH transport over a <c>direct-tcpip</c> tunnel opened on a bastion.
    /// </summary>
    public static class SshProxyJump
    {

        #region ConnectThroughAsync(Bastion, Host, Port, ...)

        /// <summary>
        /// Open a <c>direct-tcpip</c> tunnel to <paramref name="Host"/>:<paramref name="Port"/> on the
        /// <paramref name="Bastion"/> and complete a full SSH handshake with the target <b>through</b> it. The
        /// target's host-key verification (<paramref name="VerifyHostKey"/>) and any subsequent auth happen
        /// end-to-end — the bastion never sees the target session's plaintext or credentials.
        ///
        /// <para>
        /// That guarantee rests entirely on <paramref name="VerifyHostKey"/>, which is therefore
        /// mandatory: an unverified target lets the <b>bastion itself</b> terminate the inner handshake
        /// with a key of its own and read everything meant to pass through it.
        /// </para>
        /// </summary>
        public static async ValueTask<SshTunneledConnection> ConnectThroughAsync(SshTransport             Bastion,
                                                                                 String                   Host,
                                                                                 UInt16                   Port,
                                                                                 Func<Byte[], Boolean>    VerifyHostKey,
                                                                                 String[]?                KeyExchanges       = null,
                                                                                 String[]?                HostKeyAlgorithms  = null,
                                                                                 CancellationToken        CancellationToken   = default)
        {

            var tunnel = await SshForwarding.OpenTcpStreamAsync(Bastion, Host, Port, CancellationToken).ConfigureAwait(false);

            try
            {
                var pipe      = DuplexPipe.FromStream(tunnel);
                var transport = await SshTransport.ClientHandshakeAsync(pipe,
                                                                        VerifyHostKey:      VerifyHostKey,
                                                                        KeyExchanges:       KeyExchanges,
                                                                        HostKeyAlgorithms:  HostKeyAlgorithms,
                                                                        CancellationToken:  CancellationToken).ConfigureAwait(false);
                return new SshTunneledConnection(transport, tunnel);
            }
            catch
            {
                await tunnel.DisposeAsync().ConfigureAwait(false);
                throw;
            }

        }

        /// <summary>
        /// Open a tunnel to a <see cref="SshJumpHost"/>'s host/port through the bastion.
        /// </summary>
        public static ValueTask<SshTunneledConnection> ConnectThroughAsync(SshTransport             Bastion,
                                                                           SshJumpHost              Target,
                                                                           Func<Byte[], Boolean>    VerifyHostKey,
                                                                           String[]?                KeyExchanges       = null,
                                                                           String[]?                HostKeyAlgorithms  = null,
                                                                           CancellationToken        CancellationToken   = default)
            => ConnectThroughAsync(Bastion, Target.Host, Target.Port, VerifyHostKey, KeyExchanges, HostKeyAlgorithms, CancellationToken);

        #endregion

    }

}
