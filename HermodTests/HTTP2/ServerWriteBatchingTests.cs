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

    }

}
