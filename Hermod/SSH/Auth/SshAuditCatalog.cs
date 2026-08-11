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

using System.Threading.Channels;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    #region Event catalog (grows with the milestones)

    /// <summary>A connection was opened.</summary>
    public sealed record ConnectionOpenedEvent(DateTimeOffset Timestamp) : SshAuditEvent(Timestamp);

    /// <summary>A connection was closed.</summary>
    public sealed record ConnectionClosedEvent(DateTimeOffset Timestamp) : SshAuditEvent(Timestamp);

    /// <summary>The SSH version strings were exchanged.</summary>
    public sealed record VersionExchangedEvent(DateTimeOffset Timestamp, String LocalVersion, String RemoteVersion) : SshAuditEvent(Timestamp);

    /// <summary>A key exchange completed with the negotiated algorithms.</summary>
    public sealed record KexCompletedEvent(DateTimeOffset Timestamp, String KeyExchange, String Cipher, String Mac, String HostKeyAlgorithm, Boolean PostQuantum, Boolean StrictKex) : SshAuditEvent(Timestamp);

    /// <summary>A rekey (key re-exchange) completed.</summary>
    public sealed record RekeyedEvent(DateTimeOffset Timestamp, Int32 KeyExchangeCount) : SshAuditEvent(Timestamp);

    /// <summary>The peer's host key was accepted, and how it was trusted (pin / known_hosts / cert / sshfp).</summary>
    public sealed record HostKeyAcceptedEvent(DateTimeOffset Timestamp, String Fingerprint, String TrustSource) : SshAuditEvent(Timestamp);

    /// <summary>The peer's host key was rejected, with the real reason.</summary>
    public sealed record HostKeyRejectedEvent(DateTimeOffset Timestamp, String Fingerprint, String Reason) : SshAuditEvent(Timestamp);

    /// <summary>An authentication attempt was made with a given method and identity.</summary>
    public sealed record AuthAttemptEvent(DateTimeOffset Timestamp, String Username, String Method, String? Identity) : SshAuditEvent(Timestamp);

    /// <summary>Authentication succeeded and an access profile was assigned.</summary>
    public sealed record AuthorizedEvent(DateTimeOffset Timestamp, String Username, String? AccessProfile) : SshAuditEvent(Timestamp);

    /// <summary>A session channel was opened.</summary>
    public sealed record SessionOpenedEvent(DateTimeOffset Timestamp, String Username) : SshAuditEvent(Timestamp);

    /// <summary>A session channel was closed.</summary>
    public sealed record SessionClosedEvent(DateTimeOffset Timestamp, String Username) : SshAuditEvent(Timestamp);

    /// <summary>A channel of the given type was opened (session / direct-tcpip / tcpip-forward).</summary>
    public sealed record ChannelOpenedEvent(DateTimeOffset Timestamp, String ChannelType) : SshAuditEvent(Timestamp);

    /// <summary>A command was requested via <c>exec</c>.</summary>
    public sealed record ExecRequestedEvent(DateTimeOffset Timestamp, String Command) : SshAuditEvent(Timestamp);

    /// <summary>A subsystem was requested.</summary>
    public sealed record SubsystemRequestedEvent(DateTimeOffset Timestamp, String Subsystem) : SshAuditEvent(Timestamp);

    /// <summary>An SFTP operation was handled.</summary>
    public sealed record SftpOperationEvent(DateTimeOffset Timestamp, String Operation, String Path, Int64 Bytes, String Result) : SshAuditEvent(Timestamp);

    /// <summary>A request was denied by an ACL, access profile or quota — what and why.</summary>
    public sealed record PolicyDeniedEvent(DateTimeOffset Timestamp, String PolicyType, String Target, String Reason) : SshAuditEvent(Timestamp);

    /// <summary>A configured limit was exceeded.</summary>
    public sealed record LimitExceededEvent(DateTimeOffset Timestamp, String Limit, String Detail) : SshAuditEvent(Timestamp);

    /// <summary>The connection was disconnected with a code and description.</summary>
    public sealed record DisconnectedEvent(DateTimeOffset Timestamp, UInt32 Code, String Description) : SshAuditEvent(Timestamp);

    #endregion


    #region SshAuditContext

    /// <summary>
    /// Stamps the per-connection envelope (connection id, peer endpoint, role) onto every event before
    /// forwarding it to an inner sink, so individual emit sites don't have to carry that context.
    /// </summary>
    public sealed class SshAuditContext : ISshAuditSink
    {

        private readonly ISshAuditSink  inner;
        private readonly String         connectionId;
        private readonly String?        peerEndpoint;
        private readonly SshRole        role;

        /// <summary>Create a context that stamps the given envelope onto events routed to <paramref name="Inner"/>.</summary>
        public SshAuditContext(ISshAuditSink Inner, String ConnectionId, String? PeerEndpoint, SshRole Role)
        {
            this.inner         = Inner;
            this.connectionId  = ConnectionId;
            this.peerEndpoint  = PeerEndpoint;
            this.role          = Role;
        }

        public ValueTask WriteAsync(SshAuditEvent Event, CancellationToken CancellationToken = default)
            => inner.WriteAsync(Event with {
                                    ConnectionId  = Event.ConnectionId ?? connectionId,
                                    PeerEndpoint  = Event.PeerEndpoint ?? peerEndpoint,
                                    Role          = Event.Role == default && role != default ? role : Event.Role
                                }, CancellationToken);

    }

    #endregion

    #region BoundedAuditSink

    /// <summary>How a <see cref="BoundedAuditSink"/> behaves when its queue is full.</summary>
    public enum AuditOverflowPolicy
    {
        /// <summary>Drop the oldest queued event to make room (and count it).</summary>
        DropOldest,
        /// <summary>Drop the incoming event (and count it).</summary>
        DropNewest,
        /// <summary>Apply backpressure — wait for room (may briefly block the caller).</summary>
        Block
    }


    /// <summary>
    /// A non-blocking audit sink: it stamps a monotonic sequence number, enqueues each event into a bounded
    /// buffer and forwards to an inner sink on a background pump — so a slow SIEM never stalls the connection.
    /// On overflow it applies an <see cref="AuditOverflowPolicy"/> and counts the drops (dropping audit events
    /// is itself worth surfacing).
    /// </summary>
    public sealed class BoundedAuditSink : ISshAuditSink, IAsyncDisposable
    {

        #region Data

        private readonly ISshAuditSink                inner;
        private readonly AuditOverflowPolicy          policy;
        private readonly Channel<SshAuditEvent>       channel;
        private readonly Task                         pump;
        private Int64                                 sequence;
        private Int64                                 dropped;

        #endregion

        #region Properties

        /// <summary>How many events have been dropped due to overflow.</summary>
        public Int64 DroppedCount => Interlocked.Read(ref dropped);

        #endregion

        #region Constructor(s)

        /// <summary>Create a bounded audit sink forwarding to <paramref name="Inner"/>.</summary>
        public BoundedAuditSink(ISshAuditSink Inner, Int32 Capacity = 1024, AuditOverflowPolicy Policy = AuditOverflowPolicy.DropOldest)
        {
            this.inner    = Inner;
            this.policy   = Policy;
            this.channel  = System.Threading.Channels.Channel.CreateBounded<SshAuditEvent>(
                                new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });
            this.pump     = Task.Run(PumpAsync);
        }

        #endregion


        #region WriteAsync(Event, CancellationToken)

        public async ValueTask WriteAsync(SshAuditEvent Event, CancellationToken CancellationToken = default)
        {

            var stamped = Event with { SequenceNumber = Interlocked.Increment(ref sequence) };

            switch (policy)
            {

                case AuditOverflowPolicy.Block:
                    await channel.Writer.WriteAsync(stamped, CancellationToken).ConfigureAwait(false);
                    break;

                case AuditOverflowPolicy.DropNewest:
                    if (!channel.Writer.TryWrite(stamped))
                        Interlocked.Increment(ref dropped);
                    break;

                case AuditOverflowPolicy.DropOldest:
                default:
                    while (!channel.Writer.TryWrite(stamped))
                    {
                        if (channel.Reader.TryRead(out _))
                            Interlocked.Increment(ref dropped);
                        else
                            break;
                    }
                    break;

            }

        }

        #endregion

        #region (private) pump

        private async Task PumpAsync()
        {
            await foreach (var e in channel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                try   { await inner.WriteAsync(e).ConfigureAwait(false); }
                catch { /* a broken sink must not kill the pump */ }
            }
        }

        #endregion

        #region DisposeAsync()

        /// <summary>Complete the queue and flush the pump to the inner sink.</summary>
        public async ValueTask DisposeAsync()
        {
            channel.Writer.TryComplete();
            try { await pump.ConfigureAwait(false); } catch { }
        }

        #endregion

    }

    #endregion

}
