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
    /// one connection at about a quarter of what Kestrel carries. Now a writer
    /// leaves the flush to the next one waiting, and the DATA writer loop holds
    /// a batch open while it has DATA to send.
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


        #region HeadersQueuedBehindOneAnother_GoOutInOneWrite(WriteBufferSize, HeadersInFirstWrite)

        /// <summary>
        /// Eight responses whose HEADERS all wait for the write lock: each writer
        /// leaves the flush to the one behind it, so the first write to the
        /// transport after the lock is free carries all eight. Without the
        /// buffer, the old way, each goes out in a write of its own: the control
        /// that shows the test can tell the two apart.
        /// </summary>
        [TestCase(HTTP2FrameWriter.DefaultBufferSize, 8)]
        [TestCase(0,                                  1)]
        public async Task HeadersQueuedBehindOneAnother_GoOutInOneWrite(Int32 WriteBufferSize, Int32 HeadersInFirstWrite)
        {

            await using var peer = await PipedH2ServerConnection.StartAsync(SmallResponse, WriteBufferSize: WriteBufferSize);

            var streams  = Enumerable.Range(0, 8).Select(i => (UInt32) (2 * i + 1)).ToArray();
            var before   = 0;

            using (var held = await peer.HoldWritesAsync())
            {

                foreach (var streamId in streams)
                    await peer.RequestAsync(streamId, "/");

                await peer.WritersWaitingAsync(streams.Length);

                before = peer.Writes.Count;

            }

            foreach (var streamId in streams)
            {

                var response = await peer.TryResponseAsync(streamId, PipedH2ServerConnection.StepTimeout);

                Assert.That(response?.Status, Is.EqualTo("200"),
                            () => $"stream {streamId} was answered; received per stream: " +
                                  String.Join(" | ", streams.Select(id => $"{id}: {String.Join(", ", peer.FramesOn(id))}")) +
                                  "; written per transport write: " +
                                  String.Join(" | ", peer.Writes.Select(write => String.Join(", ", write))));

            }

            var firstWrite = peer.Writes[before];

            Assert.That(firstWrite.Count(frame => frame.Type == HTTP2FrameType.HEADERS), Is.EqualTo(HeadersInFirstWrite),
                        $"the first write after the lock came free: {String.Join(", ", firstWrite)}");

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
