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
    /// it has received all outstanding responses." Both clients serialize their
    /// queries behind the stream lock this is handed, so the response being
    /// processed is the only outstanding one and the connection can go at once.
    /// </para>
    /// <para>
    /// *A non-zero timeout* — the client "SHOULD honour the timeout received in
    /// that response (overriding any previous timeout) and initiate close of the
    /// connection before the timeout expires." That is what the timer is for.
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

        private readonly SemaphoreSlim  streamLock;
        private readonly Func<Task>     closeConnection;
        private readonly Timer          idleTimer;

        private Boolean disposed;

        #endregion

        #region Properties

        /// <summary>
        /// The idle timeout most recently advertised by the server, or null if it
        /// has never sent the option.
        /// </summary>
        public TimeSpan? ServerTimeout { get; private set; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create the RFC 7828 §3.2.2 policy for one connection-holding client.
        /// </summary>
        /// <param name="StreamLock">The lock that serializes the client's queries. The timer takes it before closing, so a deadline that lands during an exchange is skipped rather than cutting it off.</param>
        /// <param name="CloseConnection">How this client drops its connection without cancelling what a caller has in flight.</param>
        public DNSKeepalivePolicy(SemaphoreSlim  StreamLock,
                                  Func<Task>     CloseConnection)
        {

            this.streamLock       = StreamLock;
            this.closeConnection  = CloseConnection;
            this.idleTimer        = new Timer(OnIdleDeadline, null, Never, Never);

        }

        #endregion


        #region ApplyAsync(Response)

        /// <summary>
        /// Apply §3.2.2 to a response that has just been read, and restart the
        /// idle clock.
        /// </summary>
        /// <param name="Response">The response as the client parsed it.</param>
        public async Task ApplyAsync(DNSInfo Response)
        {

            var advertised = Response.EDNSOptions.
                                 OfType<EDNSKeepaliveOption>().
                                 FirstOrDefault()?.
                                 IdleTimeout;

            // "overriding any previous timeout" — but only when there is one to
            // override with. A response that carries no option is not a
            // withdrawal, so the deadline that was in force stays in force.
            if (advertised is not null)
                ServerTimeout = advertised;

            if (ServerTimeout is null)
                return;

            if (ServerTimeout == TimeSpan.Zero)
            {
                idleTimer.Change(Never, Never);
                await closeConnection().ConfigureAwait(false);
                return;
            }

            // RFC 7828 §3: the idle timeout "should be reset when that condition
            // is lifted, i.e., when a client sends a message". This runs at the
            // end of every exchange, so the deadline is always measured from the
            // last one rather than from whenever the option first arrived.
            idleTimer.Change(ServerTimeout.Value * IdleCloseFraction, Never);

        }

        #endregion

        #region (private) OnIdleDeadline(State)

        private void OnIdleDeadline(Object? State)
        {
            _ = CloseIfStillIdleAsync();
        }

        private async Task CloseIfStillIdleAsync()
        {
            try
            {

                // A query started between the deadline being scheduled and the
                // timer firing: the connection is not idle, and the exchange in
                // flight will schedule the next deadline when it finishes. Waiting
                // for the lock here would close a live session instead.
                if (!await streamLock.WaitAsync(TimeSpan.Zero).ConfigureAwait(false))
                    return;

                try
                {
                    await closeConnection().ConfigureAwait(false);
                }
                finally
                {
                    streamLock.Release();
                }

            }

            // Nothing is awaiting this, so an exception here would be unobserved.
            // The client being disposed underneath a deadline that had already
            // fired is the ordinary way to get one.
            catch (ObjectDisposedException) { }
            catch (Exception)               { }

        }

        #endregion

        #region Dispose()

        public void Dispose()
        {

            if (disposed)
                return;

            disposed = true;

            idleTimer.Change(Never, Never);
            idleTimer.Dispose();

        }

        #endregion

    }

}
