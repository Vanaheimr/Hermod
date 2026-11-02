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

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// The client half of RFC 7828 §3.2.2, for a DNS client that holds its
    /// connection open between queries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is shared rather than written into each client because it has been
    /// written into each client before. RFC 7828 §3: "This specification does not
    /// distinguish between different types of DNS client and server in the use of
    /// this option." DoT and plain TCP are one client as far as it is concerned,
    /// and the last time the two were treated separately the TIMEOUT 0 rule was
    /// implemented on one of them and left off the other for a release.
    /// </para>
    /// <para>
    /// Two rules live here, both SHOULDs, both about when a session ends:
    /// </para>
    /// <para>
    /// *TIMEOUT 0* — "A DNS client that receives a response that includes the
    /// edns-tcp-keepalive option with a TIMEOUT value of 0 SHOULD send no more
    /// queries on that connection and initiate closing the connection as soon as
    /// it has received all outstanding responses." Both clients pipeline their
    /// queries (RFC 7766 §6.2.1.1), so others may still be outstanding when the
    /// option arrives. The connection is closed by the answer that finds nothing
    /// outstanding and nothing being written, or else by the next query, which
    /// waits for the outstanding answers before it closes it and writes nothing
    /// to it in the meantime.
    /// </para>
    /// <para>
    /// *A non-zero timeout* — the client "SHOULD honour the timeout received in
    /// that response (overriding any previous timeout) and initiate close of the
    /// connection before the timeout expires." That is what the timer is for.
    /// </para>
    /// <para>
    /// Both are about one connection. §3.1 has TIMEOUT be "an idle timeout value
    /// for the TCP connection", and §3.3.2 has a server send "the TIMEOUT value
    /// that is currently associated with the TCP session". So what a server
    /// advertised is forgotten when the client connects again, and an answer read
    /// on a connection that is gone says nothing about the one in use. A TIMEOUT
    /// of 0 kept from an earlier connection closed every later one after its first
    /// answer; and once queries were pipelined, the late answer of a connection
    /// being drained closed the next one while it was still being opened.
    /// </para>
    /// <para>
    /// Neither rule needs the client to have asked for anything: §3.3.2 lets a
    /// server volunteer the option to any TCP query carrying an OPT RR.
    /// </para>
    /// </remarks>
    internal sealed class DNSKeepalivePolicy : IDisposable
    {

        #region Data

        /// <summary>
        /// How much of the advertised timeout is allowed to pass before the
        /// connection is closed.
        /// </summary>
        /// <remarks>
        /// RFC 7828 §3.2.2 asks for the close to be initiated "before the timeout
        /// expires" and names no margin. It has to be a proportion rather than a
        /// fixed span: TIMEOUT is a 16-bit count of 100 ms units, so what the
        /// server may advertise runs from 100 ms to a little under two hours, and
        /// no single number of seconds is a sensible margin at both ends. A tenth
        /// leaves the client early rather than late at every scale.
        /// </remarks>
        private const Double IdleCloseFraction = 0.9;

        private static readonly TimeSpan Never = System.Threading.Timeout.InfiniteTimeSpan;

        private readonly SemaphoreSlim      streamLock;
        private readonly Func<Int32, Task>  closeConnection;
        private readonly Func<Boolean>      isIdle;
        private readonly Timer              idleTimer;
        private readonly Lock               sync = new();

        /// <summary>
        /// The connection what the server advertised belongs to, as the client
        /// counts its connections.
        /// </summary>
        private Int32 connection;

        /// <summary>
        /// The connection the idle timer was last armed for.
        /// </summary>
        private Int32 deadlineOf;

        /// <summary>
        /// A connection that received a TIMEOUT of 0 and has not been replaced.
        /// </summary>
        private Int32? closeWhenDrained;

        private Boolean disposed;

        #endregion

        #region Properties

        /// <summary>
        /// The idle timeout the server most recently advertised on the current
        /// connection, or null if it has not sent the option on it.
        /// </summary>
        public TimeSpan? ServerTimeout { get; private set; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create the RFC 7828 §3.2.2 policy for one connection-holding client.
        /// </summary>
        /// <param name="StreamLock">The lock the client connects and writes its queries under. The policy closes a connection only while it holds the lock, and only if it gets it without waiting: a query that holds it is writing to the connection or opening the next one, and neither may be cut off.</param>
        /// <param name="CloseConnection">How this client drops the given connection, if it is still the client's, without cancelling what a caller has in flight.</param>
        /// <param name="IsIdle">Whether no query is outstanding on the connection: with queries pipelined, the lock being free no longer says so.</param>
        public DNSKeepalivePolicy(SemaphoreSlim      StreamLock,
                                  Func<Int32, Task>  CloseConnection,
                                  Func<Boolean>?     IsIdle = null)
        {

            this.streamLock       = StreamLock;
            this.closeConnection  = CloseConnection;
            this.isIdle           = IsIdle ?? (() => true);
            this.idleTimer        = new Timer(OnIdleDeadline, null, Never, Never);

        }

        #endregion


        #region Connected(Connection)

        /// <summary>
        /// A new connection is being made: nothing the server said about the last
        /// one holds for it. Called under the stream lock.
        /// </summary>
        /// <param name="Connection">The new connection, as the client counts them.</param>
        public void Connected(Int32 Connection)
        {
            lock (sync)
            {

                connection        = Connection;
                closeWhenDrained  = null;
                ServerTimeout     = null;

                if (!disposed)
                    idleTimer.Change(Never, Never);

            }
        }

        #endregion

        #region ClosesWhenDrained(Connection)

        /// <summary>
        /// Whether the given connection received a TIMEOUT of 0: nothing more is
        /// to be written to it, and it is to be closed once the answers still
        /// outstanding on it are in. Asked under the stream lock, before a query
        /// is written.
        /// </summary>
        public Boolean ClosesWhenDrained(Int32 Connection)
        {
            lock (sync)
                return Connection == closeWhenDrained;
        }

        #endregion

        #region ApplyAsync(Connection, Response)

        /// <summary>
        /// Apply §3.2.2 to a response that has just been read, and restart the
        /// idle clock.
        /// </summary>
        /// <param name="Connection">The connection the response was read on.</param>
        /// <param name="Response">The response as the client parsed it.</param>
        public async Task ApplyAsync(Int32    Connection,
                                     DNSInfo  Response)
        {

            var advertised = Response.EDNSOptions.
                                 OfType<EDNSKeepaliveOption>().
                                 FirstOrDefault()?.
                                 IdleTimeout;

            lock (sync)
            {

                // Read on a connection that a newer one has replaced: whatever it
                // says about keepalive was about that one.
                if (Connection != connection || disposed)
                    return;

                // "overriding any previous timeout" — but only when there is one to
                // override with. A response that carries no option is not a
                // withdrawal, so the deadline that was in force stays in force.
                if (advertised is not null)
                    ServerTimeout = advertised;

                if (ServerTimeout is null)
                    return;

                if (ServerTimeout != TimeSpan.Zero)
                {

                    // RFC 7828 §3: the idle timeout "should be reset when that
                    // condition is lifted, i.e., when a client sends a message".
                    // This runs at the end of every exchange, so the deadline is
                    // always measured from the last one rather than from whenever
                    // the option first arrived.
                    deadlineOf = Connection;
                    idleTimer.Change(ServerTimeout.Value * IdleCloseFraction, Never);
                    return;

                }

                idleTimer.Change(Never, Never);
                closeWhenDrained = Connection;

            }

            // Now, if nothing is outstanding and nothing is being written. If
            // something is, the next answer comes back through here, and the next
            // query finds the connection marked.
            await CloseIfIdleAsync(Connection).ConfigureAwait(false);

        }

        #endregion

        #region (private) OnIdleDeadline(State)

        private void OnIdleDeadline(Object? State)
        {

            // The connection the deadline was set for, not whichever is current
            // when it fires: a new one may have been opened in between.
            Int32 armedFor;

            lock (sync)
                armedFor = deadlineOf;

            _ = CloseIfIdleAsync(armedFor);

        }

        #endregion

        #region (private) CloseIfIdleAsync(Connection)

        private async Task CloseIfIdleAsync(Int32 Connection)
        {
            try
            {

                // A query started between the deadline being scheduled and the
                // timer firing, or between the TIMEOUT 0 being read and now: the
                // connection is not idle, or a new one is being opened. Waiting for
                // the lock here would close a live session instead.
                if (!await streamLock.WaitAsync(TimeSpan.Zero).ConfigureAwait(false))
                    return;

                try
                {

                    // Nothing being written, but queries written earlier may still
                    // be waiting for their answers - and those answers will start
                    // the clock again when they arrive.
                    if (!isIdle())
                        return;

                    lock (sync)
                    {
                        if (Connection != connection)
                            return;
                    }

                    await closeConnection(Connection).ConfigureAwait(false);

                }
                finally
                {
                    streamLock.Release();
                }

            }

            // Nothing is awaiting the timer's call, so an exception here would be
            // unobserved; and an answer is not to fail for a close that did not
            // happen. The client being disposed underneath a deadline that had
            // already fired is the ordinary way to get one.
            catch (ObjectDisposedException) { }
            catch (Exception)               { }

        }

        #endregion

        #region Dispose()

        public void Dispose()
        {

            lock (sync)
            {

                if (disposed)
                    return;

                disposed = true;

            }

            idleTimer.Change(Never, Never);
            idleTimer.Dispose();

        }

        #endregion

    }

}
