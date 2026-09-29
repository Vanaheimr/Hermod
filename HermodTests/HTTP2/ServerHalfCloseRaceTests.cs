/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using System.Text;
using System.Reflection;
using System.Diagnostics.Tracing;
using System.Threading.Channels;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// The server's half-close against a reset. Once the frame that ends the
    /// server's side of a stream is on the wire, the client may reset the stream,
    /// and the read loop may handle that RST_STREAM before the server gets round
    /// to its half-close. The half-close used to test the state and then
    /// transition, in two steps, and a reset between them made CloseLocal throw
    /// "Cannot close local on stream N in state Closed". Thrown in the
    /// connection's DATA writer loop, it ended the loop, and no body of any
    /// stream went out on that connection again. Thrown after a bodiless
    /// response, it sent a 500 on the reset stream as well.
    ///
    /// On a real connection the gap is a few instructions wide.
    /// <see cref="PipedH2ServerConnection.ResetBeforeHalfCloseAsync"/> holds the
    /// server in it, so these tests meet the race on every run.
    /// </summary>
    [TestFixture]
    public class ServerHalfCloseRaceTests
    {

        #region (scenarios)

        private static readonly TaskCreationOptions Async = TaskCreationOptions.RunContinuationsAsynchronously;

        /// <summary>
        /// What a race in the writer loop left behind: the response to a second
        /// request, if it ended; every frame the server sent on the reset stream;
        /// and every event of the stack that mentions a failed half-close, up to
        /// the end of the connection.
        /// </summary>
        internal sealed record WriterLoopRace(PipedH2ServerConnection.Response?  Second,
                                              List<String>                       ResetStreamFrames,
                                              List<String>                       HalfCloseErrors);

        /// <summary>
        /// A body's END_STREAM, which the writer loop sends, raced by a reset —
        /// then a second request.
        /// </summary>
        internal static Task<WriterLoopRace> BodyEndRacedByResetAsync(TimeSpan Wait)

            => WriterLoopRaceAsync(Wait, (firstArrived, answerFirst) =>

                   PipedH2ServerConnection.StartAsync(

                       async (streamId, headers, body, cancellationToken) => {

                           if (streamId == 1)
                           {
                               firstArrived.TrySetResult();
                               await answerFirst.Task;
                           }

                           return ([(":status", "200")], Encoding.ASCII.GetBytes(streamId == 1 ? "first" : "second"));

                       }));

        /// <summary>
        /// A streamed response's trailers, whose HEADERS end the stream and are
        /// likewise sent by the writer loop, raced by a reset — then a second
        /// request.
        /// </summary>
        internal static Task<WriterLoopRace> TrailersEndRacedByResetAsync(TimeSpan Wait)

            => WriterLoopRaceAsync(Wait, (firstArrived, answerFirst) =>

                   PipedH2ServerConnection.StartAsync(

                       (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                       StreamingHandler: async (request, response, cancellationToken) => {

                           var first = request.Headers.First(header => header.Name == ":path").Value == "/first";

                           if (first)
                           {
                               firstArrived.TrySetResult();
                               await answerFirst.Task;
                           }

                           await response.WriteHeadersAsync([(":status", "200")]);
                           await response.WriteAsync(Encoding.ASCII.GetBytes(first ? "first" : "second"));

                           if (first)
                               await response.CompleteAsync([("x-checksum", "1")]);

                       }));

        /// <summary>
        /// Request /first on stream 1 from a server <paramref name="Start"/>ed with
        /// handlers that report its arrival and hold its answer, reset stream 1
        /// between the end of that answer and the half-close, then request
        /// /second on stream 3.
        /// </summary>
        private static async Task<WriterLoopRace> WriterLoopRaceAsync(TimeSpan                                                                        Wait,
                                                                      Func<TaskCompletionSource, TaskCompletionSource, Task<PipedH2ServerConnection>>  Start)
        {

            var firstArrived  = new TaskCompletionSource(Async);
            var answerFirst   = new TaskCompletionSource(Async);

            using var events  = new HTTP2Events();

            PipedH2ServerConnection.Response?  second;
            List<String>                       resetStreamFrames;

            // Ended before the events are looked at: a writer loop that stopped is
            // reported only once the connection ends.
            await using (var peer = await Start(firstArrived, answerFirst))
            {
                try
                {

                    await peer.RequestAsync(1, "/first");
                    await firstArrived.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                    await peer.ResetBeforeHalfCloseAsync(1, Answer: () => answerFirst.TrySetResult());

                    // The client's RST_STREAM, whose effect the reset above stood in for.
                    await peer.SendAsync(HTTP2Frame.CreateRstStream(1, HTTP2ErrorCode.CANCEL));

                    await peer.RequestAsync(3, "/second");

                    second             = await peer.TryResponseAsync(3, Wait);
                    resetStreamFrames  = peer.FramesOn(1).Select(frame => frame.ToString()).ToList();

                }
                finally
                {
                    answerFirst.TrySetResult();
                }
            }

            return new WriterLoopRace(second,
                                      resetStreamFrames,
                                      events.Mentioning("Cannot close local"));

        }

        /// <summary>
        /// A bodiless response, whose HEADERS end the stream and are sent by the
        /// response task, raced by a reset. Returns how the server reported the
        /// request, and every frame it sent on the stream.
        /// </summary>
        internal static async Task<(String Report, List<String> Frames)> BodilessEndRacedByResetAsync()
        {

            var firstArrived  = new TaskCompletionSource(Async);
            var answerFirst   = new TaskCompletionSource(Async);
            var path          = $"/nothing/{Guid.NewGuid()}";

            using var events  = new HTTP2Events();

            await using var peer = await PipedH2ServerConnection.StartAsync(

                async (streamId, headers, body, cancellationToken) => {

                    firstArrived.TrySetResult();
                    await answerFirst.Task;

                    return ([(":status", "204")], null);

                });

            try
            {

                await peer.RequestAsync(1, path);
                await firstArrived.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                await peer.ResetBeforeHalfCloseAsync(1, Answer: () => answerFirst.TrySetResult());

                // RequestHandled comes once the response has been sent, and
                // HandlerFailed as soon as the half-close has failed.
                var report = await events.FirstAsync(e => (e.Name == "RequestHandled" && Equals(e.Payload[2], path)) ||
                                                          (e.Name == "HandlerFailed"  && Equals(e.Payload[0], 1)));

                await peer.PingAsync();

                return (report.Name + " " + String.Join(", ", report.Payload),
                        peer.FramesOn(1).Select(frame => frame.ToString()).ToList());

            }
            finally
            {
                answerFirst.TrySetResult();
            }

        }

        /// <summary>
        /// Events of the stack's EventSource, from its creation on, in order.
        /// </summary>
        private sealed class HTTP2Events : EventListener
        {

            // Initialised with the declaration: the base constructor may already
            // call OnEventSourceCreated, and events may follow at once.
            private readonly Channel<(String Name, List<Object?> Payload)> events = Channel.CreateUnbounded<(String, List<Object?>)>();

            protected override void OnEventSourceCreated(EventSource Source)
            {
                if (Source.Name == "Vanaheimr-Hermod-HTTP2")
                    EnableEvents(Source, EventLevel.Verbose);
            }

            protected override void OnEventWritten(EventWrittenEventArgs Event)
            {
                if (Event.EventName != "EventCounters")
                    events.Writer.TryWrite((Event.EventName ?? "?", [.. Event.Payload ?? []]));
            }

            public async Task<(String Name, List<Object?> Payload)> FirstAsync(Func<(String Name, List<Object?> Payload), Boolean> Matches)
            {

                using var timeout = new CancellationTokenSource(PipedH2ServerConnection.StepTimeout);

                while (true)
                {
                    var e = await events.Reader.ReadAsync(timeout.Token);
                    if (Matches(e))
                        return e;
                }

            }

            /// <summary>
            /// The events so far that mention <paramref name="Text"/> in a payload field.
            /// </summary>
            public List<String> Mentioning(String Text)
            {

                var mentioning = new List<String>();

                while (events.Reader.TryRead(out var e))
                {
                    if (e.Payload.Any(field => field?.ToString()?.Contains(Text) == true))
                        mentioning.Add(e.Name + " " + String.Join(", ", e.Payload));
                }

                return mentioning;

            }

        }

        #endregion


        #region ResetBeforeHalfClose_OfABody_LaterBodiesStillSent()

        /// <summary>
        /// The writer loop sends a body's last DATA frame with END_STREAM, then
        /// half-closes the stream. A reset in between made the half-close throw
        /// out of the loop, which then stopped: the second response's HEADERS,
        /// sent by its own task, still came, but its body never did — nor any
        /// other on the connection. Nothing else may betray a failed half-close
        /// either, not even one the loop survived: no further frame on the reset
        /// stream, and no event that reports it.
        /// </summary>
        [Test]
        public async Task ResetBeforeHalfClose_OfABody_LaterBodiesStillSent()
        {

            var race = await BodyEndRacedByResetAsync(PipedH2ServerConnection.StepTimeout);

            Assert.Multiple(() =>
            {
                Assert.That(race.Second?.Status,     Is.EqualTo("200"),    "status of the second response — none if the writer loop had stopped");
                Assert.That(race.Second?.Body,       Is.EqualTo("second"), "body of the second response");
                Assert.That(race.ResetStreamFrames,  Is.EqualTo(new[] { "HEADERS :status 200",
                                                                        "DATA \"first\" END_STREAM" }), "what the server sent on the reset stream");
                Assert.That(race.HalfCloseErrors,    Is.Empty,             "events reporting a failed half-close");
            });

        }

        #endregion

        #region ResetBeforeHalfClose_OfTrailers_LaterBodiesStillSent()

        /// <summary>
        /// Trailers end the stream instead of a DATA frame, and the writer loop
        /// sends them too: the same race after the trailers' HEADERS.
        /// </summary>
        [Test]
        public async Task ResetBeforeHalfClose_OfTrailers_LaterBodiesStillSent()
        {

            var race = await TrailersEndRacedByResetAsync(PipedH2ServerConnection.StepTimeout);

            Assert.Multiple(() =>
            {
                Assert.That(race.Second?.Status,     Is.EqualTo("200"),    "status of the second response — none if the writer loop had stopped");
                Assert.That(race.Second?.Body,       Is.EqualTo("second"), "body of the second response");
                Assert.That(race.ResetStreamFrames,  Is.EqualTo(new[] { "HEADERS :status 200",
                                                                        "DATA \"first\"",
                                                                        "HEADERS x-checksum 1 END_STREAM" }), "what the server sent on the reset stream");
                Assert.That(race.HalfCloseErrors,    Is.Empty,             "events reporting a failed half-close");
            });

        }

        #endregion

        #region ResetBeforeHalfClose_OfABodilessResponse_NotAnsweredAgain()

        /// <summary>
        /// A response without a body ends the stream with its HEADERS, sent by the
        /// response task, which then half-closes. A reset in between made the
        /// half-close throw into the handler's error path: it was reported as a
        /// failed handler, and a 500 went out on the reset stream after the 204 —
        /// whose body then waited for the connection to end.
        /// </summary>
        [Test]
        public async Task ResetBeforeHalfClose_OfABodilessResponse_NotAnsweredAgain()
        {

            var (report, frames) = await BodilessEndRacedByResetAsync();

            Assert.Multiple(() =>
            {
                Assert.That(report, Does.StartWith("RequestHandled"),                        "how the server reported the request");
                Assert.That(frames, Is.EqualTo(new[] { "HEADERS :status 204 END_STREAM" }), "what the server sent on the stream");
            });

        }

        #endregion


        #region (the half-close's own contract)

        private static readonly Action<HTTP2Stream> CloseLocalIfNotReset =

            typeof(HTTP2Connection).GetMethod(nameof(CloseLocalIfNotReset), BindingFlags.NonPublic | BindingFlags.Static)!.
                                    CreateDelegate<Action<HTTP2Stream>>();

        private static HTTP2Stream OpenStream()
        {
            var stream = new HTTP2Stream(1, 65535, 65535);
            stream.Open();
            return stream;
        }

        #endregion

        #region HalfClose_OpenSide_Closes()

        [Test]
        public void HalfClose_OpenSide_Closes()
        {

            var open              = OpenStream();
            var halfClosedRemote  = OpenStream();

            halfClosedRemote.CloseRemote();

            CloseLocalIfNotReset(open);
            CloseLocalIfNotReset(halfClosedRemote);

            Assert.Multiple(() =>
            {
                Assert.That(open.            State, Is.EqualTo(HTTP2StreamState.HalfClosedLocal), "an open stream");
                Assert.That(halfClosedRemote.State, Is.EqualTo(HTTP2StreamState.Closed),          "a stream the peer has ended");
            });

        }

        #endregion

        #region HalfClose_ResetStream_LeftAlone()

        [Test]
        public void HalfClose_ResetStream_LeftAlone()
        {

            var stream = OpenStream();
            stream.CloseRemote();
            stream.Reset();

            Assert.That(() => CloseLocalIfNotReset(stream), Throws.Nothing, "a reset stream has nothing left to close");

            Assert.Multiple(() =>
            {
                Assert.That(stream.State,    Is.EqualTo(HTTP2StreamState.Closed));
                Assert.That(stream.WasReset, Is.True);
            });

        }

        #endregion

        #region HalfClose_Twice_Throws(PeerEndedToo)

        /// <summary>
        /// Only a reset is skipped. An END_STREAM sent twice is a bug in the
        /// server, not a race, and still throws — also when the peer has ended
        /// its side as well, and the stream is closed without a reset.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void HalfClose_Twice_Throws(Boolean PeerEndedToo)
        {

            var stream = OpenStream();

            if (PeerEndedToo)
                stream.CloseRemote();

            CloseLocalIfNotReset(stream);

            Assert.That(() => CloseLocalIfNotReset(stream),
                        Throws.TypeOf<HTTP2StreamException>().With.Property(nameof(HTTP2StreamException.ErrorCode)).EqualTo(HTTP2ErrorCode.STREAM_CLOSED),
                        "a second half-close");

        }

        #endregion

    }

}
