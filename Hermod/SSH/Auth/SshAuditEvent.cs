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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// Which end of the connection emitted an audit event.
    /// </summary>
    public enum SshRole
    {
        /// <summary>
        /// The client side.
        /// </summary>
        Client,
        /// <summary>
        /// The server side.
        /// </summary>
        Server
    }


    /// <summary>
    /// The base of the typed audit event stream — a single source of truth for security-relevant events
    /// that a host can route to logs, metrics or a SIEM. Records are immutable and carry a timestamp plus a
    /// common envelope (a monotonic sequence number, a connection id correlating all events of one
    /// connection, the peer endpoint and the emitting role) that a sink stamps on the way out.
    /// </summary>
    public abstract record SshAuditEvent(DateTimeOffset Timestamp)
    {
        /// <summary>
        /// The event type name (for structured logging / filtering).
        /// </summary>
        public String EventType => GetType().Name;

        /// <summary>
        /// A monotonic sequence number stamped by the sink (0 until stamped).
        /// </summary>
        public Int64 SequenceNumber { get; init; }

        /// <summary>
        /// An id correlating every event of one connection.
        /// </summary>
        public String? ConnectionId { get; init; }

        /// <summary>
        /// The peer endpoint (host:port) the connection is with.
        /// </summary>
        public String? PeerEndpoint { get; init; }

        /// <summary>
        /// The role that emitted this event.
        /// </summary>
        public SshRole Role { get; init; }
    }


    /// <summary>
    /// A pre-authentication banner was sent to the peer (SSH_MSG_USERAUTH_BANNER).
    /// </summary>
    public sealed record BannerSentEvent(DateTimeOffset Timestamp) : SshAuditEvent(Timestamp);

    /// <summary>
    /// A single authentication method succeeded for the user (may be one factor of several).
    /// </summary>
    public sealed record AuthMethodSucceededEvent(DateTimeOffset Timestamp, String Username, String Method) : SshAuditEvent(Timestamp);

    /// <summary>
    /// A single authentication attempt failed. The reason is for the audit log only, never the wire.
    /// </summary>
    public sealed record AuthMethodFailedEvent(DateTimeOffset Timestamp, String Username, String Method, String Reason) : SshAuditEvent(Timestamp);

    /// <summary>
    /// Authentication completed successfully (all required factors satisfied).
    /// </summary>
    public sealed record AuthenticationSucceededEvent(DateTimeOffset Timestamp, String Username, IReadOnlyList<String> Methods) : SshAuditEvent(Timestamp);

    /// <summary>
    /// Authentication failed and the connection is being torn down (attempt budget exhausted).
    /// </summary>
    public sealed record AuthenticationFailedEvent(DateTimeOffset Timestamp, String Username, Int32 FailedAttempts) : SshAuditEvent(Timestamp);


    /// <summary>
    /// A sink for the typed audit event stream.
    /// </summary>
    public interface ISshAuditSink
    {
        /// <summary>
        /// Record one audit event.
        /// </summary>
        ValueTask WriteAsync(SshAuditEvent Event, CancellationToken CancellationToken = default);
    }


    /// <summary>
    /// A sink that invokes a callback for each event (e.g. to bridge to a logging framework).
    /// </summary>
    public sealed class DelegateAuditSink : ISshAuditSink
    {

        private readonly Func<SshAuditEvent, CancellationToken, ValueTask> handler;

        /// <summary>
        /// Create a delegate-backed sink.
        /// </summary>
        public DelegateAuditSink(Func<SshAuditEvent, CancellationToken, ValueTask> Handler)
        {
            this.handler = Handler;
        }

        /// <summary>
        /// Create a delegate-backed sink from a synchronous callback.
        /// </summary>
        public DelegateAuditSink(Action<SshAuditEvent> Handler)
        {
            this.handler = (e, _) => { Handler(e); return ValueTask.CompletedTask; };
        }

        public ValueTask WriteAsync(SshAuditEvent Event, CancellationToken CancellationToken = default)
            => handler(Event, CancellationToken);

    }


    /// <summary>
    /// An in-memory sink that collects events, mainly for tests and diagnostics.
    /// </summary>
    public sealed class CollectingAuditSink : ISshAuditSink
    {

        private readonly ConcurrentQueue<SshAuditEvent> events = new ();

        /// <summary>
        /// All events recorded so far, in order.
        /// </summary>
        public IReadOnlyList<SshAuditEvent> Events => events.ToArray();

        public ValueTask WriteAsync(SshAuditEvent Event, CancellationToken CancellationToken = default)
        {
            events.Enqueue(Event);
            return ValueTask.CompletedTask;
        }

    }

}
