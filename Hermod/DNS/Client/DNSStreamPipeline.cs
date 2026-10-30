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
    /// Several queries outstanding on one TCP or TLS connection at once, and
    /// each answer handed to the query it belongs to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// RFC 7766 §6.2.1.1: "When a DNS client sends multiple queries to a server,
    /// it SHOULD NOT wait for an outstanding reply before sending the next query."
    /// RFC 7858 §3.3 says it again for a TLS session. Both clients used to hold
    /// one lock from writing a query to reading its answer, so the second of two
    /// queries went out only after the first answer was in.
    /// </para>
    /// <para>
    /// A client now holds its stream lock only while it connects and writes one
    /// query, and registers the query here before writing it. Reading is done by
    /// whichever waiting query gets the read turn: it reads one message, hands it
    /// to the query it answers - which may be another one - and lets the turn go.
    /// No reader runs that nobody is waiting on, so there is nothing to stop when
    /// the client goes away.
    /// </para>
    /// <para>
    /// A message that answers no outstanding query is skipped. RFC 7766 §7: "Stub
    /// and recursive resolvers MUST be able to process responses that arrive in a
    /// different order than that in which the requests were sent, regardless of
    /// the transport protocol in use" - which on a reused connection includes the
    /// answer to a query that has already given up.
    /// </para>
    /// </remarks>
    internal sealed class DNSStreamPipeline
    {

        #region (enum) Ending

        /// <summary>
        /// How an exchange ended.
        /// </summary>
        internal enum Ending
        {

            /// <summary>
            /// Its answer arrived.
            /// </summary>
            Answered,

            /// <summary>
            /// The server ended the connection, or the message that answered it
            /// could not be read: the query failed.
            /// </summary>
            Failed,

            /// <summary>
            /// The connection broke under it: the query may be asked again on a
            /// new one.
            /// </summary>
            Broken

        }

        #endregion

        #region (class) Exchange

        /// <summary>
        /// One query that has been, or is about to be, written to a connection.
        /// </summary>
        internal sealed class Exchange
        {

            internal Exchange(UInt16                     TransactionId,
                              Int32                      Connection,
                              Func<Byte[], Task<DNSInfo?>>  Accept)
            {
                this.TransactionId  = TransactionId;
                this.Connection     = Connection;
                this.Accept         = Accept;
            }

            /// <summary>
            /// The query's message ID: a message with another ID is not its answer.
            /// </summary>
            internal UInt16                        TransactionId    { get; }

            /// <summary>
            /// The connection the query was written to, as the client counts them.
            /// </summary>
            internal Int32                         Connection       { get; }

            /// <summary>
            /// The query's own matching rule: the parsed answer, or null when the
            /// message is not its answer (RFC 7766 §7, and RFC 8945 §5.4 for a
            /// signed query).
            /// </summary>
            internal Func<Byte[], Task<DNSInfo?>>  Accept           { get; }

            internal TaskCompletionSource<(Ending Ending, DNSInfo? Answer)> Outcome { get; }
                = new(TaskCreationOptions.RunContinuationsAsynchronously);

        }

        #endregion


        #region Data

        private readonly Lock            sync      = new();
        private readonly List<Exchange>  pending   = [];
        private readonly SemaphoreSlim   readTurn  = new(1, 1);

        private          TaskCompletionSource drained = Done();

        private static TaskCompletionSource Done()
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            done.SetResult();
            return done;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The number of queries written, or about to be, whose answers have not arrived.
        /// </summary>
        public Int32 Outstanding
        {
            get
            {
                lock (sync)
                    return pending.Count;
            }
        }

        /// <summary>
        /// Completes once no query is outstanding.
        /// </summary>
        public Task Drained
        {
            get
            {
                lock (sync)
                    return drained.Task;
            }
        }

        #endregion


        #region Register(TransactionId, Connection, Accept)

        /// <summary>
        /// Register a query before it is written, so that an answer arriving at
        /// once already has somewhere to go.
        /// </summary>
        public Exchange Register(UInt16                        TransactionId,
                                 Int32                         Connection,
                                 Func<Byte[], Task<DNSInfo?>>  Accept)
        {

            var exchange = new Exchange(TransactionId, Connection, Accept);

            lock (sync)
            {

                if (pending.Count == 0)
                    drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                pending.Add(exchange);

            }

            return exchange;

        }

        #endregion

        #region Withdraw(Exchange)

        /// <summary>
        /// Take a query out: answered, given up on, or never written.
        /// </summary>
        public void Withdraw(Exchange Exchange)
        {

            TaskCompletionSource? nowDrained = null;

            lock (sync)
            {
                if (pending.Remove(Exchange) && pending.Count == 0)
                    nowDrained = drained;
            }

            nowDrained?.TrySetResult();

        }

        #endregion

        #region End(Connection, Ending)

        /// <summary>
        /// End every query still outstanding on a connection that is gone.
        /// </summary>
        public void End(Int32 Connection, Ending Ending)
        {

            List<Exchange>        ended;
            TaskCompletionSource? nowDrained = null;

            lock (sync)
            {

                ended = pending.Where(exchange => exchange.Connection == Connection).ToList();
                pending.RemoveAll(exchange => exchange.Connection == Connection);

                if (ended.Count > 0 && pending.Count == 0)
                    nowDrained = drained;

            }

            foreach (var exchange in ended)
                exchange.Outcome.TrySetResult((Ending, null));

            nowDrained?.TrySetResult();

        }

        #endregion


        #region AwaitAsync(Exchange, StreamOf, CloseConnection, OnAnswer, CancellationToken)

        /// <summary>
        /// Wait for a query's answer, and read the connection while nobody else is.
        /// </summary>
        /// <param name="Exchange">The query, as registered.</param>
        /// <param name="StreamOf">The stream of the given connection, or null when that connection is no longer the client's.</param>
        /// <param name="CloseConnection">Close the given connection, if it is still the client's.</param>
        /// <param name="OnAnswer">What the client does with every answer it reads, whoever's it is - RFC 7828 §3.2.2 for both clients.</param>
        /// <param name="CancellationToken">The query's deadline.</param>
        public async Task<(Ending Ending, DNSInfo? Answer)> AwaitAsync(Exchange                Exchange,
                                                                       Func<Int32, Stream?>    StreamOf,
                                                                       Func<Int32, Task>       CloseConnection,
                                                                       Func<DNSInfo, Task>     OnAnswer,
                                                                       CancellationToken       CancellationToken)
        {

            try
            {

                while (true)
                {

                    if (Exchange.Outcome.Task.IsCompleted)
                        return await Exchange.Outcome.Task.ConfigureAwait(false);

                    // Whichever comes first: the answer, handed over by a query
                    // that is reading, or the turn to read.
                    var turn   = readTurn.WaitAsync(CancellationToken);
                    var first  = await Task.WhenAny(Exchange.Outcome.Task, turn).ConfigureAwait(false);

                    if (first == Exchange.Outcome.Task && !turn.IsCompleted)
                    {

                        // The turn was asked for and is not needed. It is passed
                        // on the moment it arrives, so that nobody else waits on a
                        // turn this query will never take.
                        _ = turn.ContinueWith(asked => {
                                if (asked.Status == TaskStatus.RanToCompletion)
                                    readTurn.Release();
                            }, TaskScheduler.Default);

                        return await Exchange.Outcome.Task.ConfigureAwait(false);

                    }

                    await turn.ConfigureAwait(false);

                    try
                    {

                        if (Exchange.Outcome.Task.IsCompleted)
                            continue;

                        var stream = StreamOf(Exchange.Connection);

                        if (stream is null)
                        {
                            End(Exchange.Connection, Ending.Broken);
                            continue;
                        }

                        await ReadOneAsync(stream, Exchange.Connection, CloseConnection, OnAnswer, CancellationToken).
                                  ConfigureAwait(false);

                    }
                    finally
                    {
                        readTurn.Release();
                    }

                }

            }
            finally
            {
                Withdraw(Exchange);
            }

        }

        #endregion

        #region (private) ReadOneAsync(Stream, Connection, CloseConnection, OnAnswer, CancellationToken)

        /// <summary>
        /// Read one message and hand it to the query it answers.
        /// </summary>
        private async Task ReadOneAsync(Stream                Stream,
                                        Int32                 Connection,
                                        Func<Int32, Task>     CloseConnection,
                                        Func<DNSInfo, Task>   OnAnswer,
                                        CancellationToken     CancellationToken)
        {

            UInt16 length;

            try
            {
                length = await Stream.ReadUInt16BEAsync(CancellationToken).
                                      ConfigureAwait(false);
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException)
            {
                await CloseConnection(Connection).ConfigureAwait(false);
                End(Connection, Ending.Broken);
                return;
            }

            // From the first octet of the body onwards the frame boundary is only
            // known for as long as exactly that many octets are consumed. Giving up
            // in the middle loses it, and nothing repairs a stream whose boundary is
            // unknown - no matching rule can, since every later read starts
            // mid-message. The connection goes instead, and every query still on it
            // may be asked again on a new one.
            var buffer    = new Byte[length];
            var totalRead = 0;

            try
            {

                while (totalRead < length)
                {

                    var bytesRead = await Stream.ReadAsync(
                                              buffer.AsMemory(totalRead, length - totalRead),
                                              CancellationToken
                                          ).ConfigureAwait(false);

                    if (bytesRead == 0)
                    {
                        await CloseConnection(Connection).ConfigureAwait(false);
                        End(Connection, Ending.Failed);
                        return;
                    }

                    totalRead += bytesRead;

                }

            }
            catch (Exception e) when (e is IOException or ObjectDisposedException)
            {
                await CloseConnection(Connection).ConfigureAwait(false);
                End(Connection, Ending.Broken);
                return;
            }
            catch
            {
                await CloseConnection(Connection).ConfigureAwait(false);
                End(Connection, Ending.Broken);
                throw;
            }

            // A length below the 12-octet header cannot be a DNS message - but its
            // octets are part of the framing and have now been read, so the stream
            // is still aligned and the next message can follow.
            if (length < 12)
                return;

            var transactionId = (UInt16) ((buffer[0] << 8) | buffer[1]);

            Exchange[] candidates;

            lock (sync)
                candidates = [.. pending.Where(exchange => exchange.Connection    == Connection &&
                                                           exchange.TransactionId == transactionId)];

            foreach (var candidate in candidates)
            {

                DNSInfo? answer;

                try
                {
                    // A copy each: a signed answer is verified and stripped in place.
                    answer = await candidate.Accept([.. buffer]).ConfigureAwait(false);
                }
                catch
                {
                    Withdraw(candidate);
                    candidate.Outcome.TrySetResult((Ending.Failed, null));
                    return;
                }

                if (answer is null)
                    continue;

                // Out before the client sees the answer, so that what it decides
                // about the connection counts only the queries still waiting.
                Withdraw(candidate);

                await OnAnswer(answer).ConfigureAwait(false);

                candidate.Outcome.TrySetResult((Ending.Answered, answer));

                return;

            }

            // Nobody's: the answer to a query that already gave up, or not an
            // answer at all. Skipped, and the stream is still aligned.

        }

        #endregion

    }

}
