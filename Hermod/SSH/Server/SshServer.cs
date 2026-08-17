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

using System.IO.Pipelines;
using System.Buffers;
using System.Net.Sockets;

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Server
{

    using IPAddress = System.Net.IPAddress;

    /// <summary>
    /// Options for a high-level <see cref="SshServer"/>: the host keys, the authenticator, an exec handler,
    /// the port-forwarding policy and an optional typed audit sink.
    /// </summary>
    public sealed record SshServerOptions
    {
        /// <summary>
        /// The host keys offered during the handshake (the first is used; certificates supported).
        /// </summary>
        public required IReadOnlyList<ISshHostKey>  HostKeys          { get; init; }

        /// <summary>
        /// The user authenticator (public keys / certificates / password / 2FA).
        /// </summary>
        public required ISshUserAuthenticator       Authenticator     { get; init; }

        /// <summary>
        /// Handles <c>exec</c>/<c>shell</c> sessions; when null (and no SFTP), session channels are refused.
        /// </summary>
        public SshExecHandler?                      ExecHandler       { get; init; }

        /// <summary>
        /// Enables the <c>sftp</c> subsystem over the given file system; when null, SFTP is refused.
        /// </summary>
        public ISftpFileSystem?                     SftpFileSystem    { get; init; }

        /// <summary>
        /// Optional SFTP access profile (least-privilege gating).
        /// </summary>
        public SshAccessProfile?                    SftpProfile       { get; init; }

        /// <summary>
        /// Optional SFTP quotas / bandwidth limits.
        /// </summary>
        public SftpLimits?                          SftpLimits        { get; init; }

        /// <summary>
        /// The port-forwarding policy (default: off).
        /// </summary>
        public ForwardingPolicy                     ForwardingPolicy  { get; init; } = ForwardingPolicy.None;

        /// <summary>
        /// Resolves a forwarding target's hostname to addresses; defaults to system DNS. The same seam
        /// <see cref="SshForwarding"/> uses — injectable so the ACL's dial-time binding can be tested.
        /// </summary>
        public SshAddressResolver?                  AddressResolver   { get; init; }

        /// <summary>
        /// An optional typed audit-event sink.
        /// </summary>
        public ISshAuditSink?                       AuditSink         { get; init; }

        /// <summary>
        /// Advertise all <see cref="HostKeys"/> to authenticated clients via
        /// <c>hostkeys-00@openssh.com</c>, and answer the matching proof challenges — the server half of
        /// OpenSSH's <c>UpdateHostKeys</c>, which lets a host key be rotated without clients tripping
        /// their host-key warning. Enabled by default; harmless for clients that ignore it.
        /// </summary>
        public Boolean                              AdvertiseHostKeys { get; init; } = true;
    }


    /// <summary>
    /// A high-level SSH server over the connection multiplexer: it accepts connections, authenticates, and
    /// per connection multiplexes session channels (dispatched to the exec handler), <c>direct-tcpip</c>
    /// tunnels (ACL-gated) and remote (<c>-R</c>) forwards — all concurrent on the one connection.
    /// </summary>
    public sealed class SshServer : IAsyncDisposable
    {

        #region Data

        private readonly SshServerOptions        options;
        private SshTcpListener?                  listener;
        private CancellationTokenSource?         cts;
        private Task                             acceptLoop = Task.CompletedTask;

        #endregion

        #region Properties

        /// <summary>
        /// The bound endpoint (after <see cref="StartAsync"/>).
        /// </summary>
        public IPSocket LocalEndPoint => listener!.LocalEndPoint;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a server with the given options (call <see cref="StartAsync"/> to bind and listen).
        /// </summary>
        public SshServer(SshServerOptions Options)
        {
            this.options = Options;
        }

        #endregion


        #region StartAsync(Endpoint, CancellationToken)

        /// <summary>
        /// Bind the listener and start accepting connections.
        /// </summary>
        public ValueTask StartAsync(IPSocket Endpoint, CancellationToken CancellationToken = default)
        {
            listener   = SshTcpListener.Start(Endpoint);
            cts        = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            acceptLoop = Task.Run(() => AcceptLoopAsync(cts.Token));
            return ValueTask.CompletedTask;
        }

        #endregion


        #region (private) accept / handle

        private async Task AcceptLoopAsync(CancellationToken CancellationToken)
        {
            try
            {
                while (!CancellationToken.IsCancellationRequested)
                {
                    var (pipe, peer) = await listener!.AcceptWithPeerAsync(CancellationToken);
                    _ = Task.Run(() => HandleConnectionAsync(pipe, peer, CancellationToken));
                }
            }
            catch { }
        }

        private async Task HandleConnectionAsync(IDuplexPipe Pipe, IPSocket? Peer, CancellationToken CancellationToken)
        {

            SshTransport? transport = null;

            try
            {

                transport = await SshTransport.ServerHandshakeAsync(Pipe, options.HostKeys[0], CancellationToken: CancellationToken);

                // What we actually agreed on is the first thing anyone asks when a connection misbehaves,
                // and until now the audit catalog defined this event without anything ever raising it.
                if (options.AuditSink is not null)
                {
                    var negotiated = transport.Algorithms;
                    await options.AuditSink.WriteAsync(
                              new KexCompletedEvent(DateTimeOffset.UtcNow,
                                                    negotiated.KeyExchange,
                                                    negotiated.CipherClientToServer,
                                                    negotiated.MacClientToServer,
                                                    negotiated.HostKey,
                                                    negotiated.KeyExchange.Contains("mlkem",   StringComparison.Ordinal) ||
                                                    negotiated.KeyExchange.Contains("sntrup",  StringComparison.Ordinal),
                                                    negotiated.StrictKex),
                              CancellationToken);
                }

                var auth = await UserAuthentication.ServerAuthenticateAsync(transport, options.Authenticator, AuditSink: options.AuditSink, CancellationToken: CancellationToken);
                var user = auth.Username;

                // A source-address certificate may only be used from the addresses the CA named. The
                // check belongs here rather than in the validator: only the server knows where the
                // client actually connected from. An undeterminable peer address counts as a mismatch —
                // a restriction that cannot be evaluated must not be treated as satisfied.
                if (!auth.Restrictions.AllowsSource(Peer?.IPAddress.ToDotNet()))
                {

                    if (options.AuditSink is not null)
                        await options.AuditSink.WriteAsync(new AuthenticationFailedEvent(DateTimeOffset.UtcNow, user, 0), CancellationToken);

                    // Say so rather than dropping the connection silently: the client learns at once
                    // that its credential is not valid from here, instead of hanging until a timeout.
                    var abw = new ArrayBufferWriter<Byte>();
                    var w   = new SshPacketWriter(abw);
                    w.WriteByte((Byte) SshMessageNumber.Disconnect);
                    w.WriteUInt32((UInt32) DisconnectReason.NoMoreAuthMethodsAvailable);
                    w.WriteString("this credential is not permitted from your address");
                    w.WriteString("");

                    try { await transport.SendPacketAsync(abw.WrittenSpan.ToArray(), CancellationToken); } catch { }
                    return;

                }

                await using var mux = new SshChannelMultiplexer(transport);
                mux.ChannelAcceptor = info => AcceptChannelAsync(info, auth.Restrictions);
                SshRemoteForwarding.ServeRemoteForwards(mux, options.ForwardingPolicy, CancellationToken);

                if (options.AdvertiseHostKeys)
                {

                    SshHostKeyRotation.ServeHostKeyProofs(mux, options.HostKeys);

                    // Advertise every host key we hold, so clients can learn a rotated-in key before the
                    // old one is retired. This must go out BEFORE the dispatch loop starts (sshd sends
                    // notify_hostkeys before entering its connection loop for the same reason): the
                    // announcement then precedes our channel-open confirmation on the wire, so an OpenSSH
                    // client challenges the unknown keys before it issues exec — and the sequential
                    // dispatch loop answers the challenge before the exec can run. Announced after
                    // Start(), the proof reply races the exec's exit-status/close, and a client running a
                    // short-lived command can disconnect before the reply — silently never updating its
                    // known_hosts.
                    await SshHostKeyRotation.AnnounceAsync(mux, options.HostKeys, CancellationToken);

                }

                mux.Start();

                while (!CancellationToken.IsCancellationRequested)
                {
                    var channel = await mux.AcceptChannelAsync(CancellationToken);
                    if (channel.ChannelType == "session")           _ = ServeSessionAsync(channel, user, auth.Restrictions, CancellationToken);
                    else if (channel.ChannelType == "direct-tcpip") _ = ServeDirectTcpIpAsync(channel, CancellationToken);
                }

            }
            catch (Exception exception)
            {

                // Never leave the peer waiting. Anything reaching here — a malformed packet, a failed
                // negotiation, a torn-down socket — used to be swallowed silently while the connection
                // stayed open, so the client hung until its own timeout and we leaked the socket.
                // The peer gets a protocol-level goodbye with a deliberately generic reason (it is not
                // necessarily authenticated, so it learns nothing about our internals) while the detail
                // goes to the audit sink, where an operator can actually see it.
                if (transport is not null)
                {
                    try
                    {

                        var abw = new ArrayBufferWriter<Byte>();
                        var w   = new SshPacketWriter(abw);
                        w.WriteByte((Byte) SshMessageNumber.Disconnect);
                        w.WriteUInt32((UInt32) DisconnectReason.ProtocolError);
                        w.WriteString("protocol error");
                        w.WriteString("");

                        await transport.SendPacketAsync(abw.WrittenSpan.ToArray(), CancellationToken);

                    }
                    catch { /* the peer may already be gone */ }
                }

                if (options.AuditSink is not null)
                {
                    try
                    {
                        await options.AuditSink.WriteAsync(
                                  new DisconnectedEvent(DateTimeOffset.UtcNow,
                                                        (UInt32) DisconnectReason.ProtocolError,
                                                        $"{exception.GetType().Name}: {exception.Message}"),
                                  CancellationToken);
                    }
                    catch { }
                }

            }
            finally
            {

                transport?.Dispose();

                // The pipe owns the socket and closes it on completion — without this the connection
                // would linger for as long as the process lives.
                try { await Pipe.Output.CompleteAsync(); } catch { }
                try { await Pipe.Input. CompleteAsync(); } catch { }

            }

        }

        private async ValueTask<Boolean> AcceptChannelAsync(SshChannelOpenInfo Info, SshSessionRestrictions Restrictions)
        {
            if (Info.ChannelType == "session")
                return options.ExecHandler is not null || options.SftpFileSystem is not null;

            // no-port-forwarding / restrict on the authorizing entry narrows the server's own policy;
            // the stricter of the two always wins.
            if (Info.ChannelType == "direct-tcpip" && !Restrictions.AllowPortForwarding)
                return false;

            if (Info.ChannelType == "direct-tcpip" && options.ForwardingPolicy.DirectTcpIp is not null)
            {

                var (host, port) = ParseDirectTcpIp(Info.TypeData);
                var addresses    = await ResolveAsync(host).ConfigureAwait(false);

                // An early refusal, so a forbidden target is rejected with CHANNEL_OPEN_FAILURE rather
                // than being accepted and then dropped. This is *not* the authoritative check: the
                // binding decision is made at dial time against the addresses actually dialed.
                return options.ForwardingPolicy.DirectTcpIp.AllowsAll(addresses, port, host);

            }

            return false;
        }

        private async Task ServeSessionAsync(SshMuxChannel Channel, String User, SshSessionRestrictions Restrictions, CancellationToken CancellationToken)
        {
            Dictionary<String, Func<SshMuxChannel, CancellationToken, ValueTask>>? subsystems = null;
            if (options.SftpFileSystem is not null)
                subsystems = new () {
                    ["sftp"] = (ch, ct) => SftpServer.ServeAsync(new StreamSftpDuplex(ch.AsStream()), options.SftpFileSystem, options.SftpProfile, options.SftpLimits, ct)
                };

            try { await SshSessionChannel.ServeAsync(Channel, User, options.ExecHandler, subsystems, CancellationToken, Restrictions); }
            catch { }
        }

        private async Task ServeDirectTcpIpAsync(SshMuxChannel Channel, CancellationToken CancellationToken)
        {
            try
            {
                var (host, port) = ParseDirectTcpIp(Channel.OpenData);

                // Resolve ONCE and gate exactly what we are about to dial. Checking one resolution and
                // then dialing a second lets an attacker-controlled name return an allowed address for
                // the check and a forbidden one for the connection (DNS rebinding), which is precisely
                // what AllowsAll's caller contract forbids.
                var addresses = await ResolveAsync(host).ConfigureAwait(false);

                if (options.ForwardingPolicy.DirectTcpIp is null ||
                    !options.ForwardingPolicy.DirectTcpIp.AllowsAll(addresses, port, host))
                {
                    await Channel.CloseAsync(CancellationToken).ConfigureAwait(false);
                    return;
                }

                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(new System.Net.IPEndPoint(addresses[0], port), CancellationToken).ConfigureAwait(false);
                await SshChannelRelay.RelayAsync(Channel.AsStream(), new NetworkStream(socket, ownsSocket: true), CancellationToken);
            }
            catch { try { await Channel.CloseAsync(CancellationToken); } catch { } }
        }

        private static (String Host, UInt16 Port) ParseDirectTcpIp(Byte[] TypeData)
        {
            var r    = new SshPacketReader(TypeData);
            var host = r.ReadString();
            var port = (UInt16) r.ReadUInt32();
            return (host, port);
        }

        private async ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(String Host)
        {

            if (options.AddressResolver is not null)
                return await options.AddressResolver(Host, CancellationToken.None).ConfigureAwait(false);

            return IPAddress.TryParse(Host, out var literal)
                       ? [literal]
                       : await System.Net.Dns.GetHostAddressesAsync(Host).ConfigureAwait(false);

        }

        #endregion

        #region DisposeAsync()

        /// <summary>
        /// Stop accepting and shut down.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (cts is not null) await cts.CancelAsync().ConfigureAwait(false);
            try { await acceptLoop.ConfigureAwait(false); } catch { }
            listener?.Dispose();
            cts?.Dispose();
        }

        #endregion

    }

}
