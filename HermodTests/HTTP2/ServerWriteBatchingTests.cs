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

using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// The server's writes to its transport, with the frame writer's buffer in
    /// place as on a socket. Each frame used to be written and flushed on its
    /// own, and under load the flush became the cost of a response: h2bench put
    /// one connection at about a quarter of what Kestrel carries. Now the frames
    /// are buffered and sent outside the write lock: what is written during a
    /// send goes out with the next, and the DATA writer loop holds a batch open
    /// while it has DATA to send.
    /// </summary>
    [TestFixture]
    public class ServerWriteBatchingTests
    {

        #region (helpers)

        private static readonly HTTP2RequestHandler SmallResponse =

            (streamId, headers, body, cancellationToken) =>
                Task.FromResult<(List<(String, String)>, Byte[]?)>(
                    ([(":status", "200")], Encoding.ASCII.GetBytes("hello")));

        #endregion


        #region ResponsesWrittenDuringASend_GoOutTogetherAfterIt()

        /// <summary>
        /// Eight responses written while the send of a first one waits on the
        /// transport: their writers take the write lock, write into the buffer,
        /// and leave, without waiting for that send, and all eight go out
        /// together after it, in one write. When the send was made under the
        /// lock, the eight waited for it to take the lock at all, and the first
        /// of a burst went out only with the last.
        /// </summary>
        [Test]
        public async Task ResponsesWrittenDuringASend_GoOutTogetherAfterIt()
        {

            await using var peer = await PipedH2ServerConnection.StartAsync(SmallResponse, WriteBufferSize: HTTP2FrameWriter.DefaultBufferSize);

            // What the connection sends of its own is out before the hold.
            await peer.PingAsync();

            var streams            = Enumerable.Range(1, 8).Select(i => (UInt32) (2 * i + 1)).ToArray();
            var (started, release) = peer.HoldNextWrite();

            await peer.RequestAsync(1, "/");
            await started.WaitAsync(PipedH2ServerConnection.StepTimeout);

            foreach (var streamId in streams)
                await peer.RequestAsync(streamId, "/");

            await peer.BufferedAsync(HTTP2FrameType.HEADERS, streams.Length);

            var before = peer.Writes.Count;

            release();

            foreach (var streamId in streams.Prepend(1u))
            {
                var response = await peer.TryResponseAsync(streamId, PipedH2ServerConnection.StepTimeout);
                Assert.That(response?.Status, Is.EqualTo("200"),
                            () => $"stream {streamId} was answered; written per transport write: " +
                                  String.Join(" | ", peer.Writes.Select(write => String.Join(", ", write))));
            }

            var held = peer.Writes[before];
            var next = peer.Writes[before + 1];

            Assert.That(held.Select(frame => frame.StreamId).Distinct(), Is.EqualTo(new[] { 1u }),
                        $"the held write carried the first response alone: {String.Join(", ", held)}");
            Assert.That(next.Count(frame => frame.Type == HTTP2FrameType.HEADERS), Is.EqualTo(streams.Length),
                        $"the write after it: {String.Join(", ", next)}");

        }

        #endregion

        #region ABatchBlockedByFlowControl_IsFlushed()

        /// <summary>
        /// With a stream window of one octet, the writer loop sends one octet of
        /// DATA and then has nothing it may send. Its batch must end there, and
        /// the octet go out: the peer will not grant more window for DATA it has
        /// not received, and a batch left open would hold back every flush on
        /// the connection, the answer to a ping included.
        /// </summary>
        [Test]
        public async Task ABatchBlockedByFlowControl_IsFlushed()
        {

            await using var peer = await PipedH2ServerConnection.StartAsync(SmallResponse, WriteBufferSize: HTTP2FrameWriter.DefaultBufferSize);

            await peer.SendAsync(HTTP2Frame.CreateSettings((HTTP2SettingsParameter.INITIAL_WINDOW_SIZE, 1u)));
            await peer.PingAsync();

            await peer.RequestAsync(1, "/");

            var sent = Task.Run(async () => {
                           while (!peer.FramesOn(1).Any(received => received.Frame.Type == HTTP2FrameType.DATA))
                               await peer.PingAsync();
                       });

            Assert.That(await Task.WhenAny(sent, Task.Delay(PipedH2ServerConnection.StepTimeout)), Is.SameAs(sent),
                        "the octet of DATA the window allowed reached the peer");

            await sent;

            var data = peer.FramesOn(1).First(received => received.Frame.Type == HTTP2FrameType.DATA).Frame;

            Assert.That(data.Length,    Is.EqualTo(1u));
            Assert.That(data.EndStream, Is.False);

        }

        #endregion

        #region ASmallResponse_GoesOutWithItsHeaders()

        /// <summary>
        /// A body that fits into one DATA frame and the send windows goes out
        /// with its HEADERS, in one hold of the write lock, while no stream has
        /// DATA queued. Through the writer loop, the HEADERS were flushed on
        /// their own, and the DATA went out after them, in a write of its own.
        /// </summary>
        [Test]
        public async Task ASmallResponse_GoesOutWithItsHeaders()
        {

            await using var peer = await PipedH2ServerConnection.StartAsync(SmallResponse, WriteBufferSize: HTTP2FrameWriter.DefaultBufferSize);

            await peer.RequestAsync(1, "/");

            var response    = await peer.TryResponseAsync(1, PipedH2ServerConnection.StepTimeout);
            var withHeaders = peer.Writes.Single(write => write.Contains((HTTP2FrameType.HEADERS, 1u)));

            Assert.Multiple(() =>
            {

                Assert.That(response?.Status, Is.EqualTo("200"));
                Assert.That(response?.Body,   Is.EqualTo("hello"));

                Assert.That(withHeaders, Does.Contain((HTTP2FrameType.DATA, 1u)),
                            $"the write with the HEADERS: {String.Join(", ", withHeaders)}");

            });

        }

        #endregion

        #region ASmallResponse_WhileAnotherStreamHasDataQueued_GoesThroughTheWriterLoop()

        /// <summary>
        /// While another stream has DATA queued, a small response goes the usual
        /// way: its HEADERS, then its body through the writer loop, which
        /// decides whose DATA goes next (RFC 9218). Here stream 1's body waits
        /// for window, and stream 3 has window of its own, so its body would
        /// have fitted to go with its HEADERS.
        /// </summary>
        [Test]
        public async Task ASmallResponse_WhileAnotherStreamHasDataQueued_GoesThroughTheWriterLoop()
        {

            var answer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            await using var peer = await PipedH2ServerConnection.StartAsync(
                                             async (streamId, headers, body, cancellationToken) => {

                                                 if (headers.First(header => header.Name == ":path").Value == "/large")
                                                     return ([(":status", "200")], new Byte[1000]);

                                                 await answer.Task;

                                                 return ([(":status", "200")], Encoding.ASCII.GetBytes("hello"));

                                             },
                                             WriteBufferSize: HTTP2FrameWriter.DefaultBufferSize);

            // A stream window of one octet: stream 1 sends one, and the rest of
            // its body stays queued.
            await peer.SendAsync(HTTP2Frame.CreateSettings((HTTP2SettingsParameter.INITIAL_WINDOW_SIZE, 1u)));
            await peer.PingAsync();

            await peer.RequestAsync(1, "/large");

            var octet = Task.Run(async () => {
                            while (!peer.FramesOn(1).Any(received => received.Frame.Type == HTTP2FrameType.DATA))
                                await peer.PingAsync();
                        });

            Assert.That(await Task.WhenAny(octet, Task.Delay(PipedH2ServerConnection.StepTimeout)), Is.SameAs(octet),
                        "stream 1 sent the octet its window allowed");

            await octet;

            // Window for stream 3's whole body, handled before it answers.
            await peer.RequestAsync(3, "/");
            await peer.SendAsync(HTTP2Frame.CreateWindowUpdate(3, 100));
            await peer.PingAsync();

            answer.SetResult();

            var response    = await peer.TryResponseAsync(3, PipedH2ServerConnection.StepTimeout);
            var withHeaders = peer.Writes.Single(write => write.Contains((HTTP2FrameType.HEADERS, 3u)));

            Assert.Multiple(() =>
            {

                Assert.That(response?.Status, Is.EqualTo("200"));
                Assert.That(response?.Body,   Is.EqualTo("hello"));

                Assert.That(withHeaders, Does.Not.Contain((HTTP2FrameType.DATA, 3u)),
                            $"the write with the HEADERS: {String.Join(", ", withHeaders)}");

            });

        }

        #endregion

    }

}
