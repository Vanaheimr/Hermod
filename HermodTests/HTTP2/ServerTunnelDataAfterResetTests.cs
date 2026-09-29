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

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// DATA that reaches a CONNECT tunnel as the tunnel is reset. The read loop
    /// checks the stream's state without a lock, and then hands the chunk to the
    /// tunnel's inbound channel, which HTTP2Stream.Reset completes. A reset made
    /// on another task, a failing tunnel handler's or the writer loop's, can land
    /// in between. The chunk was then written into the completed channel, and
    /// the ChannelClosedException that threw is no stream error: the connection
    /// ended, with GOAWAY INTERNAL_ERROR, and every other stream on it with it.
    ///
    /// Such a chunk is now dropped, and its window given back to the connection
    /// at once, as for DATA on a closed stream. The tunnel's handler still reads
    /// a reset as the end of the tunnel, with null.
    /// </summary>
    [TestFixture]
    public class ServerTunnelDataAfterResetTests
    {

        #region (helpers)

        private static readonly TaskCreationOptions Async = TaskCreationOptions.RunContinuationsAsynchronously;

        private static Byte[] ASCII(String Text)

            => Encoding.ASCII.GetBytes(Text);

        /// <summary>
        /// Answers every request that is no CONNECT.
        /// </summary>
        private static Task<(List<(String Name, String Value)> ResponseHeaders, Byte[]? ResponseBody)> Other(UInt32                             StreamId,
                                                                                                            List<(String Name, String Value)>  Headers,
                                                                                                            Byte[]?                            Body,
                                                                                                            CancellationToken                  CancellationToken)

            => Task.FromResult<(List<(String Name, String Value)>, Byte[]?)>(([(":status", "200")], ASCII("other")));

        /// <summary>
        /// Whether the server still answers a ping, rather than having ended the
        /// connection.
        /// </summary>
        private static async Task<Boolean> Answers(PipedH2ServerConnection Peer)
        {

            try
            {
                await Peer.PingAsync();
                return true;
            }
            catch (EndOfStreamException)
            {
                return false;
            }

        }

        /// <summary>
        /// The GOAWAY frames the server sent.
        /// </summary>
        private static List<String> GoAways(PipedH2ServerConnection Peer)

            => [.. Peer.FramesOn(0).Where (frame => frame.Frame.Type == HTTP2FrameType.GOAWAY).
                                    Select(frame => frame.ToString())];

        /// <summary>
        /// How a tunnel's read ended: "null", the chunk it returned, the type of
        /// the exception it failed with, or "waiting" if it did not end within
        /// the step timeout.
        /// </summary>
        private static async Task<String> OutcomeOf(Task<Byte[]?> Read)
        {

            if (await Task.WhenAny(Read, Task.Delay(PipedH2ServerConnection.StepTimeout)) != Read)
                return "waiting";

            try
            {
                return await Read is { } chunk
                           ? $"\"{Encoding.ASCII.GetString(chunk)}\""
                           : "null";
            }
            catch (Exception e)
            {
                return e.GetType().Name;
            }

        }

        #endregion


        #region TunnelHandlerFailsAsAChunkArrives_ChunkDropped_ConnectionServesOn()

        /// <summary>
        /// A tunnel's handler fails once the tunnel is open, and the server ends
        /// its side of the tunnel and then resets the stream, on the handler's
        /// task. The read loop may be taking the client's next DATA frame on that
        /// stream just then: past its check of the stream's state, but before it
        /// hands the chunk to the tunnel's channel, which the reset has just
        /// completed. The chunk is dropped, and its connection window given back,
        /// as for DATA on a closed stream; the connection serves on.
        /// </summary>
        [Test]
        public async Task TunnelHandlerFailsAsAChunkArrives_ChunkDropped_ConnectionServesOn()
        {

            var tunnelOpen  = new TaskCompletionSource(Async);
            var fail        = new TaskCompletionSource(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                Other,

                ConnectHandler: (streamId, headers, cancellationToken) => Task.FromResult(new HTTP2ConnectResult {

                    StatusCode  = 200,

                    RunAsync    = async (tunnel, cancellationToken) => {

                        tunnelOpen.TrySetResult();

                        await fail.Task;

                        throw new InvalidOperationException("The tunnel's handler failed on purpose");

                    }

                }));

            try
            {

                await peer.RequestTunnelAsync(1, "tunnel.example:443");
                await tunnelOpen.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                var stream        = peer.ServerStream(1);
                var windowBefore  = stream.RecvWindow;

                await peer.HoldDataAfterStateCheckAsync(HTTP2Frame.CreateData(1, ASCII("part 1")), async () => {

                    fail.TrySetResult();

                    // The handler's task resets the stream, which completes the
                    // tunnel's channel, and sends RST_STREAM.
                    await stream.TunnelInbound!.Reader.Completion.WaitAsync(PipedH2ServerConnection.StepTimeout);
                    await peer.ReadToResetAsync(1);

                });

                var answers = await Answers(peer);

                Assert.Multiple(() =>
                {

                    Assert.That(GoAways(peer),                    Is.Empty,                      "how the server ended the connection");

                    Assert.That(stream.RecvWindow,                Is.EqualTo(windowBefore - 6),  "the stream's window, charged for the chunk as on an open stream: the reset came after the read loop's check");
                    Assert.That(peer.ConnectionWindowHeldBack(),  Is.EqualTo(0),                 "connection window held back for the chunk, which nobody will read");

                    Assert.That(peer.FramesOn(1).Select(frame => frame.ToString()),
                                Is.EqualTo(new[] { "HEADERS :status 200",
                                                   "DATA \"\" END_STREAM",
                                                   "RST_STREAM INTERNAL_ERROR" }),
                                "what the server sent on the reset stream");

                });

                Assert.That(answers, Is.True, "the connection, once the chunk that came with the reset was handled");

                await peer.RequestAsync(3, "/other");

                var other = await peer.TryResponseAsync(3, PipedH2ServerConnection.StepTimeout);

                Assert.Multiple(() =>
                {
                    Assert.That(other?.Status,  Is.EqualTo("200"),    "status of the next response");
                    Assert.That(other?.Body,    Is.EqualTo("other"),  "body of the next response");
                });

            }
            finally
            {
                fail.TrySetResult();
            }

        }

        #endregion

        #region TunnelWriteFailsAsAChunkArrives_ChunkDropped_ConnectionServesOn()

        /// <summary>
        /// A tunnel's handler reads and writes at once, as a proxy does. Its write
        /// fails in the writer loop with a stream error, and the writer loop
        /// resets the stream, on its own task, while the read loop is taking the
        /// client's next DATA frame on that stream, past its check of the stream's
        /// state. The chunk is dropped, and its connection window given back; the
        /// connection serves on. The handler's read, waiting when the reset came,
        /// returns null, the end of the tunnel, and the chunk never reaches it.
        /// </summary>
        [Test]
        public async Task TunnelWriteFailsAsAChunkArrives_ChunkDropped_ConnectionServesOn()
        {

            var tunnelOpen  = new TaskCompletionSource<Task<Byte[]?>>(Async);
            var write       = new TaskCompletionSource(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                Other,

                ConnectHandler: (streamId, headers, cancellationToken) => Task.FromResult(new HTTP2ConnectResult {

                    StatusCode  = 200,

                    RunAsync    = async (tunnel, cancellationToken) => {

                        // Without the stream's token, which a reset cancels: a read
                        // passing it would end with that cancellation instead.
                        var read = tunnel.ReadAsync(CancellationToken.None);

                        tunnelOpen.TrySetResult(read);

                        await write.Task;

                        await tunnel.WriteAsync(ASCII("answer"), cancellationToken);

                        await read;

                    }

                }));

            try
            {

                await peer.RequestTunnelAsync(1, "tunnel.example:443");

                var read          = await tunnelOpen.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);
                var stream        = peer.ServerStream(1);
                var windowBefore  = stream.RecvWindow;

                // Any stream error of the writer loop's resets its stream alike
                // (see ServerWriterLoopFailureTests); here it is made of the
                // handler's write.
                peer.FailNextDataWrite(1, new HTTP2StreamException(HTTP2ErrorCode.INTERNAL_ERROR, 1, "The write failed on purpose"));

                await peer.HoldDataAfterStateCheckAsync(HTTP2Frame.CreateData(1, ASCII("part 1")), async () => {

                    write.TrySetResult();

                    // The writer loop resets the stream, which completes the
                    // tunnel's channel, and sends RST_STREAM.
                    await stream.TunnelInbound!.Reader.Completion.WaitAsync(PipedH2ServerConnection.StepTimeout);
                    await peer.ReadToResetAsync(1);

                });

                var answers      = await Answers(peer);
                var readOutcome  = await OutcomeOf(read);

                Assert.Multiple(() =>
                {

                    Assert.That(GoAways(peer),                    Is.Empty,                      "how the server ended the connection");

                    Assert.That(stream.RecvWindow,                Is.EqualTo(windowBefore - 6),  "the stream's window, charged for the chunk as on an open stream: the reset came after the read loop's check");
                    Assert.That(peer.ConnectionWindowHeldBack(),  Is.EqualTo(0),                 "connection window held back for the chunk, which nobody will read");

                    Assert.That(readOutcome,                      Is.EqualTo("null"),            "how the tunnel's read, waiting when the stream was reset, ended");

                    Assert.That(peer.FramesOn(1).Select(frame => frame.ToString()),
                                Is.EqualTo(new[] { "HEADERS :status 200",
                                                   "RST_STREAM INTERNAL_ERROR" }),
                                "what the server sent on the reset stream");

                });

                Assert.That(answers, Is.True, "the connection, once the chunk that came with the reset was handled");

                await peer.RequestAsync(3, "/other");

                var other = await peer.TryResponseAsync(3, PipedH2ServerConnection.StepTimeout);

                Assert.Multiple(() =>
                {
                    Assert.That(other?.Status,  Is.EqualTo("200"),    "status of the next response");
                    Assert.That(other?.Body,    Is.EqualTo("other"),  "body of the next response");
                });

            }
            finally
            {
                write.TrySetResult();
            }

        }

        #endregion

    }

}
