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
using System.Diagnostics.Tracing;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// The server's DATA writer loop failing while it sends for one stream. The
    /// loop sends every body, trailer and END_STREAM on its connection, while each
    /// response's HEADERS go out from the response's own task. An exception used
    /// to end the loop, and the connection went on accepting requests and sending
    /// their HEADERS, but never a body again: their producers waited for the
    /// connection to end, and their half-closed streams counted against
    /// MAX_CONCURRENT_STREAMS until then.
    ///
    /// The loop now handles its failures as the read loop does. A stream error
    /// resets that one stream, and the loop serves the others on; anything else
    /// ends the connection at once, with GOAWAY INTERNAL_ERROR.
    /// </summary>
    [TestFixture]
    public class ServerWriterLoopFailureTests
    {

        #region (helpers)

        private static readonly TaskCreationOptions Async = TaskCreationOptions.RunContinuationsAsynchronously;

        /// <summary>
        /// The events of the stack's EventSource while this listener lives.
        /// </summary>
        private sealed class HTTP2Events : EventListener
        {

            // Initialised with the declaration: the base constructor may already
            // call OnEventSourceCreated, and events may follow at once.
            private readonly List<(String Name, String Payload)> events = [];

            protected override void OnEventSourceCreated(EventSource Source)
            {
                if (Source.Name == "Vanaheimr-Hermod-HTTP2")
                    EnableEvents(Source, EventLevel.Verbose);
            }

            protected override void OnEventWritten(EventWrittenEventArgs Event)
            {
                if (Event.EventName != "EventCounters")
                    lock (events)
                        events.Add((Event.EventName ?? "?", String.Join(" ", Event.Payload ?? [])));
            }

            /// <summary>
            /// The payload of every event of this name so far, joined by spaces.
            /// </summary>
            public List<String> Named(String Name)
            {
                lock (events)
                    return [.. events.Where(e => e.Name == Name).Select(e => e.Payload)];
            }

        }

        #endregion


        #region StreamError_ResetsThatStreamAlone()

        /// <summary>
        /// The server ends stream 1 a second time: the kind of bug a stream error
        /// in the writer loop stands for. Its half-close then finds the server's
        /// side already closed, and throws. That stream is reset with
        /// INTERNAL_ERROR, the loop goes on, and a request on stream 3 gets its
        /// whole response.
        /// </summary>
        [Test]
        public async Task StreamError_ResetsThatStreamAlone()
        {

            using var events = new HTTP2Events();

            var returnNow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    await response.WriteHeadersAsync([(":status", "200")]);
                    await response.WriteAsync(Encoding.ASCII.GetBytes(request.Headers.First(header => header.Name == ":path").Value.TrimStart('/')));
                    await response.CompleteAsync();

                    // Stream 1's handler stays: once it has returned, nothing reads
                    // what the client may still send, and the server stops the
                    // client's side with RST_STREAM NO_ERROR (RFC 9113, Section 8.1).
                    if (request.Headers.First(header => header.Name == ":path").Value == "/first")
                        await returnNow.Task;

                });

            try
            {

                // The client leaves its side of stream 1 open, so once the server has
                // ended its own, the stream is half-closed, and still one the writer
                // loop sends on.
                await peer.RequestAsync(1, "/first", EndStream: false);

                var first = await peer.TryResponseAsync(1, PipedH2ServerConnection.StepTimeout);

                Assert.That(first?.Body, Is.EqualTo("first"), "the response on stream 1");

                // Held on to: once reset, stream 1 is pruned when stream 3 opens.
                var stream1 = peer.ServerStream(1);

                // The second END_STREAM, queued as a response queues its last.
                await peer.Connection.EnqueueOutboundAsync(stream1, [], EndStream: true).WaitAsync(PipedH2ServerConnection.StepTimeout);

                await peer.RequestAsync(3, "/second");

                var second = await peer.TryResponseAsync(3, PipedH2ServerConnection.StepTimeout);

                Assert.That(second, Is.Not.Null, "the response on stream 3 never ended: the writer loop had stopped");

                Assert.Multiple(() =>
                {

                    Assert.That(second!.Status, Is.EqualTo("200"),    "status of the response on stream 3");
                    Assert.That(second. Body,   Is.EqualTo("second"), "body of the response on stream 3");

                    Assert.That(peer.FramesOn(1).Select(frame => frame.ToString()),
                                Is.EqualTo(new[] {
                                    "HEADERS :status 200",
                                    "DATA \"first\"",
                                    "DATA \"\" END_STREAM",
                                    "DATA \"\" END_STREAM",
                                    "RST_STREAM INTERNAL_ERROR"
                                }),
                                "what the server sent on stream 1");

                    Assert.That(stream1.WasReset,       Is.True,  "stream 1 reset, and so no longer counted as open");
                    Assert.That(peer.Ended.IsCompleted, Is.False, "the connection ended");

                    Assert.That(events.Named("StreamError"),
                                Has.Some.StartsWith("1 STREAM_CLOSED Cannot close local on stream 1 "),
                                "the error logged with the code and message it was thrown with");

                });

            }
            finally
            {
                returnNow.TrySetResult();
            }

        }

        #endregion

        #region OtherFailure_EndsTheConnectionAtOnce()

        /// <summary>
        /// A failure that is no stream error leaves the connection's state in
        /// doubt: here the transport fails the write of stream 3's body. The server
        /// ends the connection at once, with GOAWAY INTERNAL_ERROR, instead of
        /// going on to accept requests it cannot answer. The request still in
        /// progress on stream 1 gets no response, but its client learns at once
        /// that none will come.
        /// </summary>
        [Test]
        public async Task OtherFailure_EndsTheConnectionAtOnce()
        {

            var firstArrived  = new TaskCompletionSource(Async);
            var answerFirst   = new TaskCompletionSource(Async);
            var failure       = new IOException($"The write failed on purpose ({Guid.NewGuid()})");

            using var events  = new HTTP2Events();

            await using var peer = await PipedH2ServerConnection.StartAsync(

                async (streamId, headers, body, cancellationToken) => {

                    if (streamId == 1)
                    {
                        firstArrived.TrySetResult();
                        await answerFirst.Task;
                    }

                    return ([(":status", "200")], Encoding.ASCII.GetBytes(streamId == 1 ? "first" : "second"));

                });

            try
            {

                await peer.RequestAsync(1, "/first");
                await firstArrived.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                peer.FailNextDataWrite(3, failure);

                await peer.RequestAsync(3, "/second");

                var ended = await Task.WhenAny(peer.Ended, Task.Delay(PipedH2ServerConnection.StepTimeout)) == peer.Ended;

                Assert.That(ended, Is.True, "the connection went on without its writer");

                await peer.ReadToEndAsync();

                Assert.Multiple(() =>
                {

                    Assert.That(peer.FramesOn(3).Select(frame => frame.ToString()),
                                Is.EqualTo(new[] { "HEADERS :status 200" }),
                                "what the server sent on stream 3");

                    Assert.That(peer.FramesOn(1), Is.Empty, "what the server sent on stream 1");

                    Assert.That(peer.FramesOn(0).Where (frame => frame.Frame.Type == HTTP2FrameType.GOAWAY).
                                                 Select(frame => frame.ToString()),
                                Is.EqualTo(new[] { "GOAWAY INTERNAL_ERROR last 3" }),
                                "how the server ended the connection");

                    Assert.That(events.Named("ConnectionError"),
                                Has.Some.Matches<String>(payload => payload is not null && payload.StartsWith("WRITER_LOOP ") && payload.Contains(failure.Message)),
                                "the failure logged");

                });

            }
            finally
            {
                answerFirst.TrySetResult();
            }

        }

        #endregion

    }

}
