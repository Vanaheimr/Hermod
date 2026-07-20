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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP2
{

    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;


    /// <summary>
    /// A pool of HTTP/2 connections to a <b>single origin</b> (one host:port),
    /// giving callers a connection that "just works" while individual connections
    /// come and go underneath. HTTP/2 multiplexes many streams over one connection,
    /// so a pool isn't about parallelism the way it is for HTTP/1.1 — its jobs here
    /// are:
    ///
    /// <list type="bullet">
    ///   <item><b>Failover.</b> A request that the peer provably never processed
    ///   (<see cref="HTTP2RequestNotProcessedException"/> — a GOAWAY above the
    ///   last-processed stream, a REFUSED_STREAM past the retry budget, stream-ID
    ///   exhaustion) is transparently re-issued on another connection. Only
    ///   <i>not-processed</i> failures are retried; anything that might have taken
    ///   effect on the server is surfaced, never silently repeated.</item>
    ///   <item><b>Self-healing.</b> The pool eagerly maintains a target of
    ///   <see cref="MaxConnections"/> live connections. When one dies (GOAWAY, socket
    ///   loss, keepalive timeout) it is dropped and replaced in the background — a
    ///   caller keeps being served by the survivors and never sees the churn.</item>
    ///   <item><b>Load spreading.</b> Each request goes to the least-loaded usable
    ///   connection (the one with the most free MAX_CONCURRENT_STREAMS slots).</item>
    /// </list>
    ///
    /// Deliberately explicit, not "automagic": you can read exactly how many
    /// connections are live (<see cref="ConnectionCount"/>), how many reconnects and
    /// failovers have happened, and the selection is a plain least-loaded pick — no
    /// hidden global handler state.
    ///
    /// Scope: buffered <see cref="SendRequestAsync"/> only. Streaming requests and
    /// CONNECT tunnels bind a caller to one specific connection for their lifetime,
    /// so they don't fit the "any connection will do" pooling model — open a
    /// dedicated <see cref="HTTP2ClientConnection"/> for those.
    /// </summary>
    public sealed class HTTP2ClientPool : IAsyncDisposable
    {

        #region Dialing parameters + configuration

        private readonly string                               host;
        private readonly int                                  port;
        private readonly RemoteCertificateValidationCallback? validateServerCertificate;
        private readonly X509Certificate2?                    clientCertificate;
        private readonly HTTP2ClientOptions?                  options;
        private readonly bool                                 cleartext;
        private readonly int                                  maxConnections;
        private readonly int                                  maxFailoverRetries;
        private readonly TimeSpan                             reconnectBackoff;

        #endregion

        #region State

        private readonly List<HTTP2ClientConnection> connections    = [];
        private readonly object                      gate           = new();
        private readonly SemaphoreSlim               replenishLock  = new(1, 1);
        private readonly CancellationTokenSource     poolCts        = new();
        private volatile bool                        disposed;

        private long reconnects;
        private long failovers;
        private long totalRequests;

        #endregion

        #region Public read-only stats

        /// <summary>How many live connections the pool currently holds.</summary>
        public int  ConnectionCount { get { lock (gate) return connections.Count; } }

        /// <summary>The target number of connections the pool keeps warm.</summary>
        public int  MaxConnections  => maxConnections;

        /// <summary>How many connections have been (re)opened to replace dead ones since construction.</summary>
        public long Reconnects      => Interlocked.Read(ref reconnects);

        /// <summary>How many requests were re-issued on another connection after a not-processed failure.</summary>
        public long Failovers       => Interlocked.Read(ref failovers);

        /// <summary>Total requests submitted to the pool.</summary>
        public long TotalRequests   => Interlocked.Read(ref totalRequests);

        #endregion


        private HTTP2ClientPool(
            string                               Host,
            int                                  Port,
            RemoteCertificateValidationCallback? ValidateServerCertificate,
            X509Certificate2?                    ClientCertificate,
            HTTP2ClientOptions?                  Options,
            bool                                 Cleartext,
            int                                  MaxConnections,
            int                                  MaxFailoverRetries,
            TimeSpan                             ReconnectBackoff)
        {
            host                      = Host;
            port                      = Port;
            validateServerCertificate = ValidateServerCertificate;
            clientCertificate         = ClientCertificate;
            options                   = Options;
            cleartext                 = Cleartext;
            maxConnections            = MaxConnections;
            maxFailoverRetries        = MaxFailoverRetries;
            reconnectBackoff          = ReconnectBackoff;
        }


        #region ConnectAsync (factory)

        /// <summary>
        /// Create a pool to Host:Port and open its first connection (so an
        /// unreachable origin fails fast, here), then fill up to
        /// <paramref name="MaxConnections"/> in the background. The dialing
        /// parameters mirror <see cref="HTTP2Client.ConnectAsync"/>.
        /// </summary>
        /// <param name="MaxConnections">Target number of warm connections to the origin (default 4).</param>
        /// <param name="MaxFailoverRetries">
        /// How many times a not-processed request is re-issued on another connection
        /// before giving up (default: <paramref name="MaxConnections"/>).
        /// </param>
        public static async Task<HTTP2ClientPool> ConnectAsync(
            string                               Host,
            int                                  Port,
            RemoteCertificateValidationCallback? ValidateServerCertificate = null,
            X509Certificate2?                    ClientCertificate         = null,
            HTTP2ClientOptions?                  Options                   = null,
            bool                                 Cleartext                 = false,
            int                                  MaxConnections            = 4,
            int?                                 MaxFailoverRetries        = null,
            TimeSpan?                            ReconnectBackoff          = null,
            CancellationToken                    CancellationToken         = default)
        {

            if (MaxConnections < 1)
                throw new ArgumentOutOfRangeException(nameof(MaxConnections), "A pool needs at least one connection");

            var pool = new HTTP2ClientPool(
                Host, Port, ValidateServerCertificate, ClientCertificate, Options, Cleartext,
                MaxConnections, MaxFailoverRetries ?? MaxConnections,
                ReconnectBackoff ?? TimeSpan.FromMilliseconds(500));

            // Open the first connection synchronously so an unreachable origin (or a
            // TLS/ALPN failure) surfaces immediately to the caller.
            await pool.OpenConnectionAsync(CancellationToken);

            // Fill the rest and keep the pool topped up, in the background.
            _ = Task.Run(pool.MaintenanceLoopAsync);

            return pool;

        }

        #endregion


        #region Sending requests (with failover)

        /// <summary>
        /// Send a request on a pooled connection, transparently retrying on another
        /// connection if the chosen one reports the request was never processed
        /// (RFC 9113 §8.1 / §6.8). Signature mirrors
        /// <see cref="HTTP2ClientConnection.SendRequestAsync"/>.
        /// </summary>
        public async Task<HTTP2Response> SendRequestAsync(
            string                             Method,
            string                             Scheme,
            string                             Authority,
            string                             Path,
            List<(string Name, string Value)>? ExtraHeaders = null,
            byte[]?                            Body         = null,
            HTTP2Priority?                     Priority     = null,
            CancellationToken                  CancellationToken = default)
        {

            ObjectDisposedException.ThrowIf(disposed, this);
            Interlocked.Increment(ref totalRequests);

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(poolCts.Token, CancellationToken);

            HTTP2RequestNotProcessedException? lastNotProcessed = null;

            for (var attempt = 0; attempt <= maxFailoverRetries; attempt++)
            {
                var connection = await AcquireConnectionAsync(linked.Token);

                try
                {
                    return await connection.SendRequestAsync(
                        Method, Scheme, Authority, Path, ExtraHeaders, Body, Priority, linked.Token);
                }
                catch (HTTP2RequestNotProcessedException ex)
                {
                    // The peer provably didn't process this — safe to re-issue verbatim
                    // on a different connection. Count a failover for each retry we're
                    // about to make (not for the final give-up), nudge the dead
                    // connection out, and loop.
                    lastNotProcessed = ex;
                    if (attempt < maxFailoverRetries)
                        Interlocked.Increment(ref failovers);
                    _ = ReplenishAsync();
                }
            }

            if (lastNotProcessed is not null)
                throw lastNotProcessed;

            throw new HTTP2ConnectionException(HTTP2ErrorCode.REFUSED_STREAM, "No connection could process the request");

        }

        #endregion


        #region Connection selection

        /// <summary>
        /// Pick the least-loaded usable connection (most free MAX_CONCURRENT_STREAMS
        /// slots). If none is usable right now — every connection just died and the
        /// pool is mid-reconnect — nudge a replenish and wait briefly, then retry.
        /// </summary>
        private async Task<HTTP2ClientConnection> AcquireConnectionAsync(CancellationToken CancellationToken)
        {

            while (true)
            {
                CancellationToken.ThrowIfCancellationRequested();

                HTTP2ClientConnection? best = null;
                var bestSlots = -1;

                lock (gate)
                {
                    foreach (var c in connections)
                    {
                        if (!c.IsUsable)
                            continue;

                        var slots = c.AvailableStreamSlots;
                        if (slots > bestSlots)
                        {
                            bestSlots = slots;
                            best      = c;
                        }
                    }
                }

                // Return the least-loaded usable connection. If even it is momentarily
                // at its stream limit, its own MAX_CONCURRENT_STREAMS gate queues the
                // request — correct backpressure — rather than us over-opening.
                if (best is not null)
                    return best;

                // Nothing usable: the survivors all died at once and maintenance is
                // reconnecting. Nudge it and wait for a connection to come back.
                _ = ReplenishAsync();
                await Task.Delay(25, CancellationToken);
            }

        }

        #endregion


        #region Connection lifecycle (open / watch / replenish)

        /// <summary>Dial one new connection, register it, and start watching it for death. Throws if the dial fails.</summary>
        private async Task<HTTP2ClientConnection> OpenConnectionAsync(CancellationToken CancellationToken)
        {

            var connection = await HTTP2Client.ConnectAsync(
                host, port, validateServerCertificate, clientCertificate, options, cleartext, CancellationToken);

            var keep = false;
            lock (gate)
            {
                if (!disposed && connections.Count < maxConnections)
                {
                    connections.Add(connection);
                    keep = true;
                }
            }

            if (!keep)
            {
                // Lost a race (pool full or disposed meanwhile) — don't leak it.
                await connection.CloseAsync();
                return connection;
            }

            _ = WatchConnectionAsync(connection);
            return connection;

        }

        /// <summary>
        /// Wait until a connection can no longer take new streams (GOAWAY or death),
        /// drop it from the routable set, and trigger a background replacement. In
        /// the GOAWAY case the connection object lives on until its in-flight streams
        /// finish — removing it from the pool only stops *new* routing, it doesn't
        /// disturb requests already awaiting on it.
        /// </summary>
        private async Task WatchConnectionAsync(HTTP2ClientConnection Connection)
        {

            try   { await Connection.Unusable; }
            catch { /* Unusable never faults, but be defensive */ }

            var removed = false;
            lock (gate)
                removed = connections.Remove(Connection);

            if (removed && !disposed)
            {
                Interlocked.Increment(ref reconnects);
                _ = ReplenishAsync();
            }

        }

        /// <summary>
        /// Bring the live-connection count back up to <see cref="maxConnections"/>.
        /// Serialized so concurrent triggers (a death + a request nudge) can't
        /// over-open; a dial failure backs off and leaves the count to a later
        /// trigger / the maintenance loop.
        /// </summary>
        private async Task ReplenishAsync()
        {

            if (disposed)
                return;

            // Non-blocking: if a replenish is already running, its loop will observe
            // any newly-needed slots — no need to queue another.
            if (!await replenishLock.WaitAsync(0))
                return;

            try
            {
                while (!disposed && ConnectionCount < maxConnections)
                {
                    try
                    {
                        await OpenConnectionAsync(poolCts.Token);
                    }
                    catch (OperationCanceledException) when (poolCts.IsCancellationRequested)
                    {
                        return;
                    }
                    catch
                    {
                        // Origin momentarily unreachable — back off; the maintenance
                        // loop (or the next death) will try again.
                        try { await Task.Delay(reconnectBackoff, poolCts.Token); } catch { return; }
                        return;
                    }
                }
            }
            finally
            {
                replenishLock.Release();
            }

        }

        /// <summary>
        /// Background safety net: periodically ensure the pool is at full strength,
        /// so an origin that was down when a death happened is picked back up once it
        /// recovers (deaths alone only trigger one replenish attempt).
        /// </summary>
        private async Task MaintenanceLoopAsync()
        {
            while (!disposed)
            {
                await ReplenishAsync();

                try   { await Task.Delay(TimeSpan.FromSeconds(2), poolCts.Token); }
                catch { return; }
            }
        }

        #endregion


        #region Disposal

        /// <summary>Close every pooled connection (best-effort GOAWAY each) and stop maintenance.</summary>
        public async ValueTask DisposeAsync()
        {

            if (disposed)
                return;
            disposed = true;

            poolCts.Cancel();

            List<HTTP2ClientConnection> toClose;
            lock (gate)
            {
                toClose = [.. connections];
                connections.Clear();
            }

            foreach (var c in toClose)
            {
                try { await c.CloseAsync(); } catch { /* best effort */ }
            }

            poolCts.Dispose();
            replenishLock.Dispose();

        }

        #endregion

    }

}
