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
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading.Channels;

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
        /// Handles <c>exec</c> sessions, and <c>shell</c> sessions where there is no
        /// <see cref="ShellHandler"/>; when there is neither and no SFTP, session channels are refused.
        /// </summary>
        public SshExecHandler?                      ExecHandler       { get; init; }

        /// <summary>
        /// Handles interactive <c>shell</c> sessions: a conversation with the client for as long as the
        /// handler runs, with its terminal's size and signals as they come. When null, a shell request goes
        /// to the <see cref="ExecHandler"/>, as it always did.
        /// </summary>
        public SshShellHandler?                     ShellHandler      { get; init; }

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
        /// An optional typed audit-event sink. Every event of a connection carries the connection's id and
        /// its peer.
        /// </summary>
        public ISshAuditSink?                       AuditSink         { get; init; }

        /// <summary>
        /// Advertise all <see cref="HostKeys"/> to authenticated clients via
        /// <c>hostkeys-00@openssh.com</c>, and answer the matching proof challenges — the server half of
        /// OpenSSH's <c>UpdateHostKeys</c>, which lets a host key be rotated without clients tripping
        /// their host-key warning. Enabled by default; harmless for clients that ignore it.
        /// </summary>
        public Boolean                              AdvertiseHostKeys { get; init; } = true;

        /// <summary>
        /// The limits this server enforces: <see cref="SshServerLimits.MaxAuthTries"/>,
        /// <see cref="SshServerLimits.LoginGraceTime"/>, <see cref="SshServerLimits.MaxSessions"/>,
        /// <see cref="SshServerLimits.MaxConnections"/>, <see cref="SshServerLimits.MaxUnauthenticated"/>,
        /// <see cref="SshServerLimits.MaxUnauthenticatedPerAddress"/> and, where an interval is set, the
        /// <c>ClientAlive</c> probes. <see cref="SshServerLimits.MaxPacketSize"/> and
        /// <see cref="SshServerLimits.IdleTimeout"/> are not enforced here.
        /// </summary>
        public SshServerLimits                      Limits            { get; init; } = new ();

        /// <summary>
        /// How long a stopping server gives its sessions to end by themselves - a shell to say goodbye -
        /// before it closes their connections (default 5 s).
        /// </summary>
        public TimeSpan                             ShutdownGracePeriod  { get; init; } = TimeSpan.FromSeconds(5);
    }


    /// <summary>
    /// A high-level SSH server over the connection multiplexer: it accepts connections, authenticates, and
    /// per connection multiplexes session channels (dispatched to the shell or exec handler),
    /// <c>direct-tcpip</c> tunnels (ACL-gated) and remote (<c>-R</c>) forwards — all concurrent on the one
    /// connection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stopping it is orderly: no connection is accepted any more, every session is told through the token
    /// its handler was given - a shell can say goodbye on a channel that is still open - and every
    /// connection whose sessions have ended is closed with a DISCONNECT that says why. What has not ended
    /// after <see cref="SshServerOptions.ShutdownGracePeriod"/> is closed regardless.
    /// </para>
    /// <para>
    /// A client that goes away is not a protocol error. One that closed its connection - with a DISCONNECT
    /// or without one - is recorded as having gone, and only a client that sent something malformed, or
    /// failed to authenticate, is sent a DISCONNECT saying "protocol error", and the audit sink the reason.
    /// </para>
    /// </remarks>
    public sealed class SshServer : IAsyncDisposable
    {

        #region (class) Connection

        /// <summary>
        /// What one connection has: its id, its peer, its audit context, its session count, and an end of
        /// its own.
        /// </summary>
        private sealed class Connection(Int64              Id,
                                        IPSocket?          Peer,
                                        String             Address,
                                        SshAuditContext?   Audit,
                                        CancellationToken  ServerToken) : IDisposable
        {

            public Int64                    Id        { get; } = Id;
            public String                   IdText    { get; } = Id.ToString();
            public IPSocket?                Peer      { get; } = Peer;
            public String                   Address   { get; } = Address;
            public SshAuditContext?         Audit     { get; } = Audit;
            public CancellationTokenSource  Ending    { get; } = CancellationTokenSource.CreateLinkedTokenSource(ServerToken);
            public Int32                    Sessions;
            public Int32                    Authenticated;

            public void Dispose()
                => Ending.Dispose();

        }

        #endregion

        #region Data

        private readonly SshServerOptions                     options;
        private readonly ConcurrentDictionary<Int64, Task>    connections                = new ();
        private readonly ConcurrentDictionary<String, Int32>  unauthenticatedPerAddress  = new ();
        private readonly Lock                                 counting                   = new ();
        private          SshTcpListener?                      listener;
        private          CancellationTokenSource?             cts;
        private readonly CancellationTokenSource              stopping                   = new ();
        private          Task                                 acceptLoop                 = Task.CompletedTask;
        private          Int64                                nextConnectionId;
        private          Int32                                unauthenticated;

        #endregion

        #region Properties

        /// <summary>
        /// The bound endpoint (after <see cref="StartAsync"/>).
        /// </summary>
        public IPSocket LocalEndPoint => listener!.LocalEndPoint;

        /// <summary>
        /// How many connections are open now, authenticated or not.
        /// </summary>
        public Int32    ConnectionCount
            => connections.Count;

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
        /// Bind the listener and start accepting connections. A port that cannot be had is thrown - as the
        /// <see cref="SocketException"/> the operating system said - before anything is accepted.
        /// </summary>
        public ValueTask StartAsync(IPSocket Endpoint, CancellationToken CancellationToken = default)
        {
            listener   = SshTcpListener.Start(Endpoint);
            cts        = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            acceptLoop = Task.Run(() => AcceptLoopAsync(cts.Token));
            return ValueTask.CompletedTask;
        }

        #endregion


        #region (private) AcceptLoopAsync(CancellationToken)

        private async Task AcceptLoopAsync(CancellationToken CancellationToken)
        {
            while (!CancellationToken.IsCancellationRequested && !stopping.IsCancellationRequested)
            {

                IDuplexPipe  pipe;
                IPSocket?    peer;

                try
                {
                    (pipe, peer) = await listener!.AcceptWithPeerAsync(CancellationToken).ConfigureAwait(false);
                }
                catch (Exception) when (CancellationToken.IsCancellationRequested || stopping.IsCancellationRequested)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (Exception exception)
                {

                    // One connection that could not be accepted - reset before it was, say - is no reason
                    // to accept no other: this loop used to end at the first, and silently.
                    await EmitAsync(Audit(null, null), new LimitExceededEvent(DateTimeOffset.UtcNow, "accept", $"{exception.GetType().Name}: {exception.Message}")).ConfigureAwait(false);

                    try
                    {
                        await Task.Delay(100, CancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    continue;

                }

                var address  = AddressOf(peer);
                var refused  = Admit(address);

                if (refused is not null)
                {
                    await EmitAsync(Audit(null, peer), new LimitExceededEvent(DateTimeOffset.UtcNow, "connections", refused)).ConfigureAwait(false);
                    await CloseAsync(pipe).ConfigureAwait(false);
                    continue;
                }

                var id          = Interlocked.Increment(ref nextConnectionId);
                var connection  = new Connection(id, peer, address, Audit(id.ToString(), peer), CancellationToken);

                // Registered before it runs, so that a connection that ends at once is removed after it
                // was added and not before.
                var registered  = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var running     = Task.Run(async () => {
                                      await registered.Task.ConfigureAwait(false);
                                      await HandleConnectionAsync(connection, pipe).ConfigureAwait(false);
                                  });

                connections[id] = running;
                registered.SetResult();

            }
        }

        #endregion

        #region (private) Admit(Address) / Authenticated(Connection)

        /// <summary>
        /// Count a connection from the given address in - or say which limit it would exceed.
        /// </summary>
        private String? Admit(String Address)
        {

            var limits = options.Limits;

            lock (counting)
            {

                if (connections.Count >= limits.MaxConnections)
                    return $"{limits.MaxConnections} connection(s) are open";

                if (unauthenticated >= limits.MaxUnauthenticated)
                    return $"{limits.MaxUnauthenticated} connection(s) have not authenticated yet";

                if (unauthenticatedPerAddress.GetValueOrDefault(Address) >= limits.MaxUnauthenticatedPerAddress)
                    return $"{limits.MaxUnauthenticatedPerAddress} connection(s) from {Address} have not authenticated yet";

                unauthenticated++;
                unauthenticatedPerAddress[Address] = unauthenticatedPerAddress.GetValueOrDefault(Address) + 1;

                return null;

            }

        }

        /// <summary>
        /// The connection is no longer one that has not authenticated: it did, or it ended. Once.
        /// </summary>
        private void Authenticated(Connection Connection)
        {

            if (Interlocked.Exchange(ref Connection.Authenticated, 1) != 0)
                return;

            lock (counting)
            {

                unauthenticated--;

                var left = unauthenticatedPerAddress.GetValueOrDefault(Connection.Address) - 1;

                if (left > 0)
                    unauthenticatedPerAddress[Connection.Address] = left;
                else
                    unauthenticatedPerAddress.TryRemove(Connection.Address, out _);

            }

        }

        #endregion

        #region (private) HandleConnectionAsync(Connection, Pipe)

        private async Task HandleConnectionAsync(Connection   Connection,
                                                 IDuplexPipe  Pipe)
        {

            var audit             = Connection.Audit;
            var token             = Connection.Ending.Token;
            SshTransport? transport = null;

            await EmitAsync(audit, new ConnectionOpenedEvent(DateTimeOffset.UtcNow)).ConfigureAwait(false);

            try
            {

                SshAuthResult auth;

                // The handshake and the authentication within the grace time, or not at all: a client that
                // connects and says nothing would otherwise hold its slot for ever.
                using (var grace = CancellationTokenSource.CreateLinkedTokenSource(token, stopping.Token))
                {

                    grace.CancelAfter(options.Limits.LoginGraceTime);

                    try
                    {

                        transport = await SshTransport.ServerHandshakeAsync(Pipe, options.HostKeys[0], CancellationToken: grace.Token).ConfigureAwait(false);

                        if (transport.PeerIdentification is SshIdentificationString client)
                            await EmitAsync(audit, new VersionExchangedEvent(DateTimeOffset.UtcNow, SshIdentificationString.Default.ToString(), client.ToString())).ConfigureAwait(false);

                        // What we actually agreed on is the first thing anyone asks when a connection misbehaves.
                        var negotiated = transport.Algorithms;
                        await EmitAsync(audit, new KexCompletedEvent(DateTimeOffset.UtcNow,
                                                                     negotiated.KeyExchange,
                                                                     negotiated.CipherClientToServer,
                                                                     negotiated.MacClientToServer,
                                                                     negotiated.HostKey,
                                                                     negotiated.KeyExchange.Contains("mlkem",   StringComparison.Ordinal) ||
                                                                     negotiated.KeyExchange.Contains("sntrup",  StringComparison.Ordinal),
                                                                     negotiated.StrictKex)).ConfigureAwait(false);

                        auth = await UserAuthentication.ServerAuthenticateAsync(transport,
                                                                                options.Authenticator,
                                                                                MaxAuthTries:       options.Limits.MaxAuthTries,
                                                                                AuditSink:          audit,
                                                                                CancellationToken:  grace.Token).ConfigureAwait(false);

                    }
                    catch (OperationCanceledException) when (stopping.IsCancellationRequested && !token.IsCancellationRequested)
                    {
                        // Somebody not yet in has nothing to say goodbye to: the server stopping ends the
                        // handshake at once.
                        await SendDisconnectAsync(transport, DisconnectReason.ByApplication, "the server is shutting down").ConfigureAwait(false);
                        await EmitAsync(audit, new DisconnectedEvent(DateTimeOffset.UtcNow, (UInt32) DisconnectReason.ByApplication, "the server is shutting down")).ConfigureAwait(false);
                        return;
                    }
                    catch (OperationCanceledException) when (grace.IsCancellationRequested && !token.IsCancellationRequested)
                    {
                        await EmitAsync(audit, new LimitExceededEvent(DateTimeOffset.UtcNow, "login grace time", $"not authenticated within {options.Limits.LoginGraceTime.TotalSeconds:0} s")).ConfigureAwait(false);
                        await SendDisconnectAsync(transport, DisconnectReason.ByApplication, "authentication took too long").ConfigureAwait(false);
                        return;
                    }

                }

                Authenticated(Connection);

                // A source-address certificate may only be used from the addresses the CA named. The check
                // belongs here rather than in the validator: only the server knows where the client
                // actually connected from. An undeterminable peer address counts as a mismatch — a
                // restriction that cannot be evaluated must not be treated as satisfied.
                if (!auth.Restrictions.AllowsSource(Connection.Peer?.IPAddress.ToDotNet()))
                {

                    await EmitAsync(audit, new AuthenticationFailedEvent(DateTimeOffset.UtcNow, auth.Username, 0)).ConfigureAwait(false);

                    // Say so rather than dropping the connection silently: the client learns at once that
                    // its credential is not valid from here, instead of hanging until a timeout.
                    await SendDisconnectAsync(transport, DisconnectReason.NoMoreAuthMethodsAvailable, "this credential is not permitted from your address").ConfigureAwait(false);
                    return;

                }

                var session = new SshSessionInfo(Connection.IdText,
                                                 auth.Username,
                                                 auth.Method,
                                                 auth.PublicKeyBlob,
                                                 Connection.Peer,
                                                 listener?.LocalEndPoint,
                                                 transport.PeerIdentification,
                                                 auth.Restrictions);

                await using var mux = new SshChannelMultiplexer(transport);

                mux.ChannelAcceptor = info => AcceptChannelAsync(info, auth.Restrictions, Connection);

                SshRemoteForwarding.ServeRemoteForwards(mux, options.ForwardingPolicy, token);

                if (options.AdvertiseHostKeys)
                {

                    SshHostKeyRotation.ServeHostKeyProofs(mux, options.HostKeys);

                    // Advertise every host key we hold, so clients can learn a rotated-in key before the old
                    // one is retired. This must go out BEFORE the dispatch loop starts (sshd sends
                    // notify_hostkeys before entering its connection loop for the same reason): the
                    // announcement then precedes our channel-open confirmation on the wire, so an OpenSSH
                    // client challenges the unknown keys before it issues exec — and the sequential
                    // dispatch loop answers the challenge before the exec can run. Announced after Start(),
                    // the proof reply races the exec's exit-status/close, and a client running a
                    // short-lived command can disconnect before the reply — silently never updating its
                    // known_hosts.
                    await SshHostKeyRotation.AnnounceAsync(mux, options.HostKeys, token).ConfigureAwait(false);

                }

                mux.Start();

                var probing  = options.Limits.ClientAliveInterval is TimeSpan interval
                                   ? ProbeAsync(mux, interval, options.Limits.ClientAliveCountMax, Connection)
                                   : Task.CompletedTask;

                var serving  = new ConcurrentDictionary<SshMuxChannel, Task>();
                var gone     = await AcceptChannelsAsync(mux, session, serving, Connection).ConfigureAwait(false);

                if (gone)
                    await EmitAsync(audit, new DisconnectedEvent(DateTimeOffset.UtcNow, (UInt32) DisconnectReason.ByApplication, "the client closed the connection")).ConfigureAwait(false);

                else
                {

                    // Stopping: the sessions have been told, and are given the grace period to end - a
                    // shell to say goodbye. Then the client is told why the connection ends.
                    try
                    {
                        await Task.WhenAll(serving.Values).WaitAsync(options.ShutdownGracePeriod, token).ConfigureAwait(false);
                    }
                    catch
                    { }

                    await SendDisconnectAsync(transport, DisconnectReason.ByApplication, "the server is shutting down", mux).ConfigureAwait(false);
                    await EmitAsync(audit, new DisconnectedEvent(DateTimeOffset.UtcNow, (UInt32) DisconnectReason.ByApplication, "the server is shutting down")).ConfigureAwait(false);

                }

                await Connection.Ending.CancelAsync().ConfigureAwait(false);

                try
                {
                    await probing.ConfigureAwait(false);
                }
                catch
                { }

            }
            catch (Exception exception) when (IsConnectionLost(exception))
            {

                // The client went - closed its window, lost its network, or stopped answering - and that
                // is not a protocol error, whatever the bytes it left behind look like.
                await EmitAsync(audit, new DisconnectedEvent(DateTimeOffset.UtcNow,
                                                             (UInt32) DisconnectReason.ConnectionLost,
                                                             Innermost(exception).Message)).ConfigureAwait(false);

            }
            catch (Exception exception)
            {

                // Never leave the peer waiting. Anything reaching here — a malformed packet, a failed
                // negotiation, a failed authentication — used to be swallowed silently while the connection
                // stayed open, so the client hung until its own timeout and we leaked the socket. The peer
                // gets a protocol-level goodbye with a deliberately generic reason (it is not necessarily
                // authenticated, so it learns nothing about our internals) while the detail goes to the
                // audit sink, where an operator can actually see it.
                await SendDisconnectAsync(transport, DisconnectReason.ProtocolError, "protocol error").ConfigureAwait(false);

                await EmitAsync(audit, new DisconnectedEvent(DateTimeOffset.UtcNow,
                                                             (UInt32) DisconnectReason.ProtocolError,
                                                             $"{exception.GetType().Name}: {exception.Message}")).ConfigureAwait(false);

            }
            finally
            {

                Authenticated(Connection);

                // The pipe owns the socket and closes it on completion — without this the connection would
                // linger for as long as the process lives. Where there is a transport, it completes the
                // writer, after the send in flight is done with it: completed under a window adjust or a
                // DISCONNECT, its buffers went back to the pool every connection rents from while still
                // being written into.
                if (transport is not null)
                {
                    await transport.CloseOutputAsync().ConfigureAwait(false);
                    transport.Dispose();
                    try { await Pipe.Input.CompleteAsync().ConfigureAwait(false); } catch { }
                }
                else
                    await CloseAsync(Pipe).ConfigureAwait(false);

                await EmitAsync(audit, new ConnectionClosedEvent(DateTimeOffset.UtcNow)).ConfigureAwait(false);

                connections.TryRemove(Connection.Id, out _);

                Connection.Dispose();

            }

        }

        #endregion

        #region (private) AcceptChannelsAsync(Mux, Session, Serving, Connection)

        /// <summary>
        /// Serve the channels the client opens until it goes - true - or the server is stopping - false.
        /// </summary>
        private async Task<Boolean> AcceptChannelsAsync(SshChannelMultiplexer                      Mux,
                                                        SshSessionInfo                             Session,
                                                        ConcurrentDictionary<SshMuxChannel, Task>  Serving,
                                                        Connection                                 Connection)
        {

            var token     = Connection.Ending.Token;
            var stopped   = Task.Delay(Timeout.Infinite, stopping.Token);

            while (true)
            {

                var accepting = Mux.AcceptChannelAsync(token).AsTask();

                if (await Task.WhenAny(accepting, stopped).ConfigureAwait(false) == stopped)
                {
                    _ = accepting.ContinueWith(task => _ = task.Exception, TaskContinuationOptions.OnlyOnFaulted);
                    return false;
                }

                SshMuxChannel channel;

                try
                {
                    channel = await accepting.ConfigureAwait(false);
                }
                catch (ChannelClosedException closed) when (closed.InnerException is null)
                {
                    // The client said goodbye with a DISCONNECT.
                    return true;
                }
                catch (ChannelClosedException closed)
                {
                    // The connection failed: the multiplexer completed its queue with why.
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(closed.InnerException!).Throw();
                    throw;
                }

                await EmitAsync(Connection.Audit, new ChannelOpenedEvent(DateTimeOffset.UtcNow, channel.ChannelType)).ConfigureAwait(false);

                if (channel.ChannelType == "session")
                {

                    var served = ServeSessionAsync(channel, Session, Connection.Audit, token);

                    Serving[channel] = served;

                    _ = served.ContinueWith(_ => {
                            Serving.TryRemove(channel, out var __);
                            Interlocked.Decrement(ref Connection.Sessions);
                        }, TaskScheduler.Default);

                }

                else if (channel.ChannelType == "direct-tcpip")
                    _ = ServeDirectTcpIpAsync(channel, token);

            }

        }

        #endregion

        #region (private) AcceptChannelAsync(Info, Restrictions, Connection)

        private async ValueTask<Boolean> AcceptChannelAsync(SshChannelOpenInfo      Info,
                                                            SshSessionRestrictions  Restrictions,
                                                            Connection              Connection)
        {

            if (stopping.IsCancellationRequested)
                return false;

            if (Info.ChannelType == "session")
            {

                if (options.ExecHandler is null && options.ShellHandler is null && options.SftpFileSystem is null)
                    return false;

                if (Interlocked.Increment(ref Connection.Sessions) > options.Limits.MaxSessions)
                {
                    Interlocked.Decrement(ref Connection.Sessions);
                    await EmitAsync(Connection.Audit, new LimitExceededEvent(DateTimeOffset.UtcNow, "sessions", $"{options.Limits.MaxSessions} session(s) are open on this connection")).ConfigureAwait(false);
                    return false;
                }

                return true;

            }

            // no-port-forwarding / restrict on the authorizing entry narrows the server's own policy;
            // the stricter of the two always wins.
            if (Info.ChannelType == "direct-tcpip" && !Restrictions.AllowPortForwarding)
                return false;

            if (Info.ChannelType == "direct-tcpip" && options.ForwardingPolicy.DirectTcpIp is not null)
            {

                var (host, port) = ParseDirectTcpIp(Info.TypeData);
                var addresses    = await ResolveAsync(host).ConfigureAwait(false);

                // An early refusal, so a forbidden target is rejected with CHANNEL_OPEN_FAILURE rather than
                // being accepted and then dropped. This is *not* the authoritative check: the binding
                // decision is made at dial time against the addresses actually dialed.
                return options.ForwardingPolicy.DirectTcpIp.AllowsAll(addresses, port, host);

            }

            if (Info.ChannelType == "direct-tcpip")
                await EmitAsync(Connection.Audit, new PolicyDeniedEvent(DateTimeOffset.UtcNow, "direct-tcpip", "", "forwarding is off")).ConfigureAwait(false);

            return false;

        }

        #endregion

        #region (private) ServeSessionAsync(Channel, Session, Audit, CancellationToken)

        private async Task ServeSessionAsync(SshMuxChannel      Channel,
                                             SshSessionInfo     Session,
                                             SshAuditContext?   Audit,
                                             CancellationToken  CancellationToken)
        {

            Dictionary<String, Func<SshMuxChannel, CancellationToken, ValueTask>>? subsystems = null;

            if (options.SftpFileSystem is not null)
                subsystems = new () {
                    ["sftp"] = (ch, ct) => SftpServer.ServeAsync(new StreamSftpDuplex(ch.AsStream()), options.SftpFileSystem, options.SftpProfile, options.SftpLimits, ct)
                };

            try
            {
                await SshSessionChannel.ServeAsync(Channel,
                                                   Session,
                                                   options.ExecHandler,
                                                   options.ShellHandler,
                                                   subsystems,
                                                   Audit,
                                                   stopping.Token,
                                                   CancellationToken).ConfigureAwait(false);
            }
            catch
            { }

        }

        #endregion

        #region (private) ServeDirectTcpIpAsync(Channel, CancellationToken)

        private async Task ServeDirectTcpIpAsync(SshMuxChannel Channel, CancellationToken CancellationToken)
        {
            try
            {
                var (host, port) = ParseDirectTcpIp(Channel.OpenData);

                // Resolve ONCE and gate exactly what we are about to dial. Checking one resolution and then
                // dialing a second lets an attacker-controlled name return an allowed address for the check
                // and a forbidden one for the connection (DNS rebinding), which is precisely what
                // AllowsAll's caller contract forbids.
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

        #endregion

        #region (private) ProbeAsync(Mux, Interval, CountMax, Connection)

        /// <summary>
        /// Ask the client whether it is still there, every interval - <c>keepalive@openssh.com</c>, as sshd
        /// asks - and end the connection after as many unanswered questions in a row as allowed. A refusal
        /// is an answer: it says the client is there.
        /// </summary>
        private async Task ProbeAsync(SshChannelMultiplexer  Mux,
                                      TimeSpan               Interval,
                                      Int32                  CountMax,
                                      Connection             Connection)
        {

            var token       = Connection.Ending.Token;
            var unanswered  = 0;

            while (!token.IsCancellationRequested)
            {

                await Task.Delay(Interval, token).ConfigureAwait(false);

                try
                {
                    await Mux.SendGlobalRequestWithReplyAsync("keepalive@openssh.com", true, [], token).
                              AsTask().
                              WaitAsync(Interval, token).
                              ConfigureAwait(false);
                    unanswered = 0;
                }
                catch (TimeoutException)
                {
                    if (++unanswered >= CountMax)
                    {
                        await EmitAsync(Connection.Audit, new LimitExceededEvent(DateTimeOffset.UtcNow, "client alive", $"{unanswered} keepalive probe(s) unanswered")).ConfigureAwait(false);
                        await Connection.Ending.CancelAsync().ConfigureAwait(false);
                        return;
                    }
                }

            }

        }

        #endregion


        #region (private static) helpers

        private SshAuditContext? Audit(String? ConnectionId, IPSocket? Peer)

            => options.AuditSink is not null
                   ? new SshAuditContext(options.AuditSink, ConnectionId ?? "", Peer?.ToString(), SshRole.Server)
                   : null;

        private static ValueTask EmitAsync(ISshAuditSink? Sink, SshAuditEvent Event)
        {
            try
            {
                return Sink?.WriteAsync(Event) ?? ValueTask.CompletedTask;
            }
            catch
            {
                // A broken sink must not take a connection with it.
                return ValueTask.CompletedTask;
            }
        }

        /// <summary>
        /// Tell the client why the connection ends - where there is a transport to tell it on, and it is
        /// still listening.
        /// </summary>
        private static async Task SendDisconnectAsync(SshTransport?           Transport,
                                                      DisconnectReason        Reason,
                                                      String                  Description,
                                                      SshChannelMultiplexer?  Mux = null)
        {

            if (Transport is null)
                return;

            var abw = new ArrayBufferWriter<Byte>();
            var w   = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.Disconnect);
            w.WriteUInt32((UInt32) Reason);
            w.WriteString(Description);
            w.WriteString("");

            try
            {

                // Through the multiplexer where there is one, which serialises everything sent on the
                // connection; straight onto the transport before there is.
                if (Mux is not null)
                    await Mux.SendAsync(abw.WrittenSpan.ToArray()).AsTask().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                else
                    await Transport.SendPacketAsync(abw.WrittenSpan.ToArray()).AsTask().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

            }
            catch
            {
                // The peer may already be gone.
            }

        }

        /// <summary>
        /// Whether the given exception says the client went, rather than that it said something wrong.
        /// </summary>
        private static Boolean IsConnectionLost(Exception Exception)

            => Innermost(Exception) is SshConnectionClosedException
                                    or SshChannelClosedException
                                    or IOException
                                    or SocketException
                                    or ObjectDisposedException
                                    or OperationCanceledException;

        private static Exception Innermost(Exception Exception)
        {

            while (Exception is ChannelClosedException or AggregateException && Exception.InnerException is not null)
                Exception = Exception.InnerException;

            return Exception;

        }

        private static String AddressOf(IPSocket? Peer)
        {

            var address = Peer?.IPAddress.ToDotNet();

            if (address is null)
                return "";

            return address.IsIPv4MappedToIPv6
                       ? address.MapToIPv4().ToString()
                       : address.ToString();

        }

        private static async Task CloseAsync(IDuplexPipe Pipe)
        {
            try { await Pipe.Output.CompleteAsync().ConfigureAwait(false); } catch { }
            try { await Pipe.Input. CompleteAsync().ConfigureAwait(false); } catch { }
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
        /// Stop: accept no connection any more, ask every session to end - a shell is told through its
        /// token, while its channel is still open - give them <see cref="SshServerOptions.ShutdownGracePeriod"/>
        /// to do so, and then close whatever is left.
        /// </summary>
        public async ValueTask DisposeAsync()
        {

            await stopping.CancelAsync().ConfigureAwait(false);

            listener?.Dispose();

            try { await acceptLoop.ConfigureAwait(false); } catch { }

            try
            {
                // The grace period, and a little more for the goodbyes sent once it is over.
                await Task.WhenAll(connections.Values).WaitAsync(options.ShutdownGracePeriod + TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            }
            catch
            { }

            if (cts is not null)
                await cts.CancelAsync().ConfigureAwait(false);

            try
            {
                await Task.WhenAll(connections.Values).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch
            { }

            cts?.Dispose();

        }

        #endregion

    }

}
