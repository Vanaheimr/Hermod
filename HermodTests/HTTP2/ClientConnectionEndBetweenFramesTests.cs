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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// A client connection whose end comes while its read loop is between two
    /// frames: CloseAsync is called, or the token the connection was made with
    /// is cancelled, while the loop handles a frame, or while it reads one with
    /// a read that does not look at the token. SslStream's reads do not, for
    /// what it has decrypted already: the rest of a TLS record comes out of
    /// ReadAsync whatever the token says. The loop then stopped at its
    /// condition, with no exception, and so without failing what was left on
    /// the connection. A buffered request failed all the same, as it waits with
    /// the connection's token, and an accepted tunnel reads its end, as the end
    /// of the connection resets its stream. But a streamed response waits with
    /// its caller's token alone, and its GetResponseAsync, ReadAsync and
    /// GetTrailersAsync waited for good.
    ///
    /// Every end of the read loop now fails what is left on the connection: an
    /// end between two frames with an OperationCanceledException, as an end in
    /// a read that ends with the token always has.
    /// </summary>
    [TestFixture]
    public class ClientConnectionEndBetweenFramesTests
    {

        #region (helpers)

        /// <summary>
        /// How the connection ends.
        /// </summary>
        public enum ConnectionEnd
        {

            /// <summary>
            /// The client closes the connection (<see cref="HTTP2ClientConnection.CloseAsync"/>).
            /// </summary>
            ClientCloses,

            /// <summary>
            /// The token the connection was made with is cancelled.
            /// </summary>
            TokenCancelled

        }

        /// <summary>
        /// What the transport's reads make of the connection's token.
        /// </summary>
        public enum TransportReads
        {

            /// <summary>
            /// A read that waits for bytes ends when the token is cancelled, as a
            /// NetworkStream's does.
            /// </summary>
            EndWithTheToken,

            /// <summary>
            /// A read takes the bytes that come, whatever the token says, as an
            /// SslStream's read hands out what it has decrypted already.
            /// </summary>
            IgnoreTheToken

        }

        /// <summary>
        /// The client's end of a <see cref="HoldingH2Transport"/>, whose reads
        /// make of the connection's token what <see cref="TransportReads"/>
        /// says. Writes go through as they come.
        /// </summary>
        private sealed class TokenReads(Stream Transport, TransportReads Reads) : Stream
        {

            private Int32 readsAfterTheEnd;

            private readonly TaskCompletionSource endSeen = new (TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>
            /// Completes once the token of a read has been cancelled: the
            /// connection's end has reached the read it waits in, whether that
            /// read looks at the token or not.
            /// </summary>
            public Task EndSeen
                => endSeen.Task;

            /// <summary>
            /// The reads that returned bytes once the connection's token had been
            /// cancelled: reads it would have ended, had they looked at it.
            /// </summary>
            public Int32 ReadsAfterTheEnd
                => Volatile.Read(ref readsAfterTheEnd);

            public override async ValueTask<Int32> ReadAsync(Memory<Byte> Buffer, CancellationToken CancellationToken = default)
            {

                using var end = CancellationToken.Register(() => endSeen.TrySetResult());

                var count = await Transport.ReadAsync(Buffer,
                                                      Reads == TransportReads.IgnoreTheToken
                                                          ? CancellationToken.None
                                                          : CancellationToken);

                if (CancellationToken.IsCancellationRequested)
                    Interlocked.Increment(ref readsAfterTheEnd);

                return count;

            }

            public override ValueTask WriteAsync(ReadOnlyMemory<Byte> Buffer, CancellationToken CancellationToken = default)
                => Transport.WriteAsync(Buffer, CancellationToken);

            public override Task<Int32> ReadAsync(Byte[] Buffer, Int32 Offset, Int32 Count, CancellationToken CancellationToken)
                => ReadAsync(Buffer.AsMemory(Offset, Count), CancellationToken).AsTask();

            public override Task WriteAsync(Byte[] Buffer, Int32 Offset, Int32 Count, CancellationToken CancellationToken)
                => WriteAsync(Buffer.AsMemory(Offset, Count), CancellationToken).AsTask();

            public override Task FlushAsync(CancellationToken CancellationToken)
                => Transport.FlushAsync(CancellationToken);

            // The connection only ever uses the asynchronous API.
            public override Int32 Read (Byte[] Buffer, Int32 Offset, Int32 Count) => throw new NotSupportedException();
            public override void  Write(Byte[] Buffer, Int32 Offset, Int32 Count) => throw new NotSupportedException();
            public override void  Flush() { }

            public override Boolean  CanRead   => true;
            public override Boolean  CanWrite  => true;
            public override Boolean  CanSeek   => false;
            public override Int64    Length    => throw new NotSupportedException();
            public override Int64    Position  { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override Int64    Seek(Int64 Offset, SeekOrigin Origin) => throw new NotSupportedException();
            public override void     SetLength(Int64 Value)                => throw new NotSupportedException();

        }

        /// <summary>
        /// Start a client connection made with <paramref name="Token"/> on the
        /// transport, through <paramref name="Reads"/>, as
        /// <see cref="HoldingH2Transport.ConnectAsync"/> does: answer its preface
        /// with an empty SETTINGS frame, and return once StartAsync has.
        /// </summary>
        private static async Task<HTTP2ClientConnection> ConnectAsync(HoldingH2Transport  Transport,
                                                                      TokenReads          Reads,
                                                                      CancellationToken   Token)
        {

            var connection = new HTTP2ClientConnection(Reads, new HTTP2ClientOptions(), Token);
            var starting   = connection.StartAsync();

            await Transport.SendAsync(HTTP2Frame.CreateSettings());
            await starting.WaitAsync(HoldingH2Transport.StepTimeout);

            return connection;

        }

        /// <summary>
        /// Start a streamed request, as a gRPC call or an event stream does.
        /// </summary>
        private static Task<HTTP2ClientStream> StartStreamAsync(HTTP2ClientConnection Connection)

            => Connection.StartStreamingRequestAsync(HTTPMethod.POST, URIScheme.http, "localhost", "/events").
                          WaitAsync(HoldingH2Transport.StepTimeout);

        /// <summary>
        /// End the connection as <paramref name="How"/> says, while its read loop
        /// waits for the next frame, then send it a frame that needs no answer, a
        /// WINDOW_UPDATE for the connection, and return whether the connection
        /// has ended within the step timeout. A read that ends with the token
        /// has ended the loop already, and the frame stays unread. A read that
        /// does not look at the token takes it, and the loop handles it, as it
        /// handles a frame it was handling anyway when the end came. Written, not
        /// sent: <see cref="HoldingH2Transport.SendAsync"/> would wait for the
        /// next read, which a loop that has ended never starts.
        ///
        /// CloseAsync returns once the read loop has ended, which a read that
        /// does not look at the token does only with the frame: the frame goes
        /// out once CloseAsync has cancelled the token the loop reads with, and
        /// CloseAsync is awaited after it.
        /// </summary>
        private static async Task<Boolean> EndAsync(HoldingH2Transport       Transport,
                                                    TokenReads               Reads,
                                                    HTTP2ClientConnection    Connection,
                                                    CancellationTokenSource  Token,
                                                    ConnectionEnd            How)
        {

            var ending = Task.CompletedTask;

            switch (How)
            {

                case ConnectionEnd.ClientCloses:
                    ending = Connection.CloseAsync();
                    await Reads.EndSeen.WaitAsync(HoldingH2Transport.StepTimeout);
                    break;

                case ConnectionEnd.TokenCancelled:
                    await Token.CancelAsync();
                    break;

            }

            await Transport.WriteAsync(HTTP2Frame.CreateWindowUpdate(0, 1));

            var ended = Task.WhenAll(ending, Connection.Closed);

            return await Task.WhenAny(ended, Task.Delay(HoldingH2Transport.StepTimeout)) == ended;

        }

        /// <summary>
        /// How a call ended: whether it did within the step timeout, and with
        /// which exception, if any.
        /// </summary>
        private static async Task<(Boolean Ended, Exception? Failure)> EndOf(Task Call)
        {

            if (await Task.WhenAny(Call, Task.Delay(HoldingH2Transport.StepTimeout)) != Call)
                return (false, null);

            try
            {
                await Call;
                return (true, null);
            }
            catch (Exception e)
            {
                return (true, e);
            }

        }

        #endregion


        #region AStreamedResponse_Fails_WhenTheConnectionEnds(How, Reads)

        /// <summary>
        /// A streamed request waits for its response's head, body and trailers,
        /// with no token of its own, when the connection ends. All three fail
        /// with an OperationCanceledException once the connection has ended.
        /// When the read the loop waited in did not end with the token, and the
        /// loop took one more frame, they used to wait for good.
        /// </summary>
        [TestCase(ConnectionEnd.ClientCloses,    TransportReads.EndWithTheToken)]
        [TestCase(ConnectionEnd.ClientCloses,    TransportReads.IgnoreTheToken)]
        [TestCase(ConnectionEnd.TokenCancelled,  TransportReads.EndWithTheToken)]
        [TestCase(ConnectionEnd.TokenCancelled,  TransportReads.IgnoreTheToken)]
        public async Task AStreamedResponse_Fails_WhenTheConnectionEnds(ConnectionEnd How, TransportReads Reads)
        {

            using       var token      = new CancellationTokenSource();
            await using var transport  = new HoldingH2Transport(HoldHeadersOf: streamId => false);
            var             reads      = new TokenReads(transport.Client, Reads);

            var connection  = await ConnectAsync(transport, reads, token.Token);
            var stream      = await StartStreamAsync(connection);

            var head        = stream.GetResponseAsync(CancellationToken.None);
            var reading     = stream.ReadAsync(CancellationToken.None);
            var trailers    = stream.GetTrailersAsync();
            var waited      = !head.IsCompleted && !reading.IsCompleted && !trailers.IsCompleted;

            var closed      = await EndAsync(transport, reads, connection, token, How);

            // At once: a test that fails waits out the step timeout once, not three times.
            var outcomes    = await Task.WhenAll(EndOf(head), EndOf(reading), EndOf(trailers));

            var (headEnded,     headFailure)      = outcomes[0];
            var (readEnded,     readFailure)      = outcomes[1];
            var (trailersEnded, trailersFailure)  = outcomes[2];

            Assert.Multiple(() =>
            {

                Assert.That(waited,                      Is.True,                                      "the head, the body and the trailers, waiting while the server sends nothing");
                Assert.That(closed,                      Is.True,                                      "the connection ended");
                Assert.That(reads.ReadsAfterTheEnd > 0,  Is.EqualTo(Reads == TransportReads.IgnoreTheToken),
                                                                                                       "whether a read took a frame after the end of the connection");

                Assert.That(headEnded,                   Is.True,                                      "the head failed once the connection had ended");
                Assert.That(headFailure,                 Is.InstanceOf<OperationCanceledException>(),  "how the head failed");

                Assert.That(readEnded,                   Is.True,                                      "the read failed once the connection had ended");
                Assert.That(readFailure,                 Is.InstanceOf<OperationCanceledException>(),  "how the read failed");

                Assert.That(trailersEnded,               Is.True,                                      "the trailers failed once the connection had ended");
                Assert.That(trailersFailure,             Is.InstanceOf<OperationCanceledException>(),  "how the trailers failed");

            });

        }

        #endregion

        #region AStreamedResponseBeingRead_Fails_WhenTheConnectionEnds(How, Reads)

        /// <summary>
        /// The same for a response under way: its head is in, and so is a chunk
        /// of its body, which the client has read. The next read and the
        /// trailers fail with an OperationCanceledException once the connection
        /// has ended. When the loop took one more frame after the end, they used
        /// to wait for good, and so a gRPC server stream or an event stream
        /// never learned that its connection was gone.
        /// </summary>
        [TestCase(ConnectionEnd.ClientCloses,    TransportReads.EndWithTheToken)]
        [TestCase(ConnectionEnd.ClientCloses,    TransportReads.IgnoreTheToken)]
        [TestCase(ConnectionEnd.TokenCancelled,  TransportReads.EndWithTheToken)]
        [TestCase(ConnectionEnd.TokenCancelled,  TransportReads.IgnoreTheToken)]
        public async Task AStreamedResponseBeingRead_Fails_WhenTheConnectionEnds(ConnectionEnd How, TransportReads Reads)
        {

            using       var token      = new CancellationTokenSource();
            await using var transport  = new HoldingH2Transport(HoldHeadersOf: streamId => false);
            var             reads      = new TokenReads(transport.Client, Reads);

            var connection  = await ConnectAsync(transport, reads, token.Token);
            var stream      = await StartStreamAsync(connection);

            await transport.SendHeadersAsync(stream.StreamId, [(":status", "200")]);
            await transport.SendAsync(HTTP2Frame.CreateData(stream.StreamId, Encoding.ASCII.GetBytes("first"), EndStream: false));

            var head        = await stream.GetResponseAsync().WaitAsync(HoldingH2Transport.StepTimeout);
            var first       = await stream.ReadAsync().WaitAsync(HoldingH2Transport.StepTimeout);

            var reading     = stream.ReadAsync(CancellationToken.None);
            var trailers    = stream.GetTrailersAsync();
            var waited      = !reading.IsCompleted && !trailers.IsCompleted;

            var closed      = await EndAsync(transport, reads, connection, token, How);

            var outcomes    = await Task.WhenAll(EndOf(reading), EndOf(trailers));

            var (readEnded,     readFailure)      = outcomes[0];
            var (trailersEnded, trailersFailure)  = outcomes[1];

            Assert.Multiple(() =>
            {

                Assert.That(head.Status,                 Is.EqualTo(200),                              "the response's status");
                Assert.That(first,                       Is.EqualTo(Encoding.ASCII.GetBytes("first")), "the chunk read before the end");

                Assert.That(waited,                      Is.True,                                      "the read and the trailers, waiting while the server sends nothing");
                Assert.That(closed,                      Is.True,                                      "the connection ended");
                Assert.That(reads.ReadsAfterTheEnd > 0,  Is.EqualTo(Reads == TransportReads.IgnoreTheToken),
                                                                                                       "whether a read took a frame after the end of the connection");

                Assert.That(readEnded,                   Is.True,                                      "the read failed once the connection had ended");
                Assert.That(readFailure,                 Is.InstanceOf<OperationCanceledException>(),  "how the read failed");

                Assert.That(trailersEnded,               Is.True,                                      "the trailers failed once the connection had ended");
                Assert.That(trailersFailure,             Is.InstanceOf<OperationCanceledException>(),  "how the trailers failed");

            });

        }

        #endregion

    }

}
