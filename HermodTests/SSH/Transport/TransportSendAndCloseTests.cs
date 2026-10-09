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

using System.IO.Pipelines;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// A packet whose flush is cut off still has its sequence number, and a
    /// connection is closed only after the send in flight is done with the
    /// writer - or cut off, where it will not be.
    /// </summary>
    /// <remarks>
    /// One full test run had a client read a packet that failed its Poly1305
    /// tag, a server close mid-packet, and a session's output not arrive, in
    /// three tests at once, and none of it again: the bytes of a packet had
    /// changed after it was sealed. Closing a connection completed its pipe
    /// writer outside the send lock while a window adjust or a DISCONNECT was
    /// still being written into it - and completing gives the writer's buffers
    /// back to the pool every connection of the process rents from.
    /// </remarks>
    [TestFixture]
    public class TransportSendAndCloseTests
    {

        #region (class) GatedWriter

        /// <summary>
        /// A pipe writer whose flush waits at a gate, and which says whether it
        /// was completed while a flush was in flight.
        /// </summary>
        private sealed class GatedWriter(PipeWriter Inner) : PipeWriter
        {

            private TaskCompletionSource  gate     = Open();
            private Int32                 flushing;

            public TaskCompletionSource   Entered  { get; private set; } = new (TaskCreationOptions.RunContinuationsAsynchronously);
            public Boolean                CompletedWhileFlushing { get; private set; }
            public Boolean                IsCompleted            { get; private set; }

            private static TaskCompletionSource Open()
            {
                var open = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                open.SetResult();
                return open;
            }

            /// <summary>Flushes wait from now on.</summary>
            public void Close()
            {
                gate    = new (TaskCreationOptions.RunContinuationsAsynchronously);
                Entered = new (TaskCreationOptions.RunContinuationsAsynchronously);
            }

            /// <summary>The flush waiting goes on.</summary>
            public void Let()               => gate.TrySetResult();

            /// <summary>The flush waiting fails, as a write to a closed socket does.</summary>
            public void Fail(Exception E)   => gate.TrySetException(E);

            public override async ValueTask<FlushResult> FlushAsync(CancellationToken CancellationToken = default)
            {

                Interlocked.Increment(ref flushing);

                try
                {
                    Entered.TrySetResult();
                    await gate.Task.WaitAsync(CancellationToken).ConfigureAwait(false);
                    return await Inner.FlushAsync(CancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Decrement(ref flushing);
                }

            }

            public override void Complete(Exception? Exception = null)
            {
                if (Volatile.Read(ref flushing) > 0)
                    CompletedWhileFlushing = true;
                IsCompleted = true;
                Inner.Complete(Exception);
            }

            public override void         Advance(Int32 Bytes)                => Inner.Advance(Bytes);
            public override Memory<Byte> GetMemory(Int32 SizeHint = 0)       => Inner.GetMemory(SizeHint);
            public override Span<Byte>   GetSpan  (Int32 SizeHint = 0)       => Inner.GetSpan  (SizeHint);
            public override void         CancelPendingFlush()                => Inner.CancelPendingFlush();

        }

        #endregion

        #region (private) Connected(Abort = null)

        /// <summary>
        /// A client and a server after their handshake, the client's writes
        /// through a writer that can be held at its flush.
        /// </summary>
        private static async Task<(SshTransport Client, SshTransport Server, GatedWriter Writer)> Connected(Action<GatedWriter>? Abort = null)
        {

            var clientToServer  = new Pipe();
            var serverToClient  = new Pipe();
            var writer          = new GatedWriter(clientToServer.Writer);

            var clientPipe      = new DuplexPipe(serverToClient.Reader, writer) { Abort = Abort is null ? null : () => Abort(writer) };
            var serverPipe      = new DuplexPipe(clientToServer.Reader, serverToClient.Writer);

            var hostKey         = Ed25519KeyPair.Generate();
            var clientTask      = SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe).AsTask();
            var serverTask      = SshTransport.ServerHandshakeAsync(serverPipe, hostKey).AsTask();

            return (await clientTask, await serverTask, writer);

        }

        #endregion


        #region A_Packet_Whose_Flush_Is_Cut_Off_Keeps_Its_Sequence_Number

        /// <summary>
        /// The flush of a packet is cancelled once it is framed and sealed: the
        /// packet is in the pipe and goes out with the next flush, so the next
        /// packet has the next number. It had the same - the peer read it with
        /// the next and refused it.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task A_Packet_Whose_Flush_Is_Cut_Off_Keeps_Its_Sequence_Number(CancellationToken CancellationToken)
        {

            var (client, server, writer) = await Connected();

            using (client)
            using (server)
            {

                writer.Close();

                using var cut = new CancellationTokenSource();

                var first = client.SendPacketAsync(new Byte[] { 94, 1, 2, 3 }, cut.Token).AsTask();

                await writer.Entered.Task.WaitAsync(CancellationToken);
                await cut.CancelAsync();

                Assert.That(async () => await first, Throws.InstanceOf<OperationCanceledException>());

                writer.Let();

                await client.SendPacketAsync(new Byte[] { 94, 4, 5, 6 }, CancellationToken);

                var one = await server.ReceivePacketAsync(CancellationToken);
                var two = await server.ReceivePacketAsync(CancellationToken);

                Assert.Multiple(() => {
                    Assert.That(one.ToArray(), Is.EqualTo(new Byte[] { 94, 1, 2, 3 }));
                    Assert.That(two.ToArray(), Is.EqualTo(new Byte[] { 94, 4, 5, 6 }), "the packet after one cut off had its number");
                });

            }

        }

        #endregion

        #region Closing_Waits_For_The_Send_In_Flight

        /// <summary>
        /// A send is held at its flush, and the connection is closed: the
        /// writer is completed after the send is done with it, not under it;
        /// and nothing is sent after - a send then is refused, and the writer
        /// is not touched.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task Closing_Waits_For_The_Send_In_Flight(CancellationToken CancellationToken)
        {

            var (client, server, writer) = await Connected();

            using (client)
            using (server)
            {

                writer.Close();

                var sending = client.SendPacketAsync(new Byte[] { 94, 1, 2, 3 }, CancellationToken).AsTask();

                await writer.Entered.Task.WaitAsync(CancellationToken);

                var closing = client.CloseOutputAsync(TimeSpan.FromSeconds(10)).AsTask();

                await Task.Delay(300, CancellationToken);

                Assert.Multiple(() => {
                    Assert.That(closing.IsCompleted,  Is.False, "closed while a send was in flight");
                    Assert.That(writer.IsCompleted,   Is.False, "the writer was completed under the send");
                });

                writer.Let();

                await sending;
                await closing.WaitAsync(CancellationToken);

                Assert.Multiple(() => {
                    Assert.That(writer.IsCompleted,             Is.True,  "closed, and the writer is not completed");
                    Assert.That(writer.CompletedWhileFlushing,  Is.False, "the writer was completed under a flush");
                });

                Assert.That(async () => await client.SendPacketAsync(new Byte[] { 94, 7 }, CancellationToken),
                            Throws.InstanceOf<SshConnectionClosedException>(), "a send after closing went on");

            }

        }

        #endregion

        #region Closing_Cuts_Off_A_Send_That_Will_Not_End

        /// <summary>
        /// A send that will not end - a peer that reads nothing - holds the
        /// connection open no longer than closing waits: then the connection
        /// is cut off under it, the send fails, and the writer is completed
        /// after that, not before.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task Closing_Cuts_Off_A_Send_That_Will_Not_End(CancellationToken CancellationToken)
        {

            var aborted = false;

            var (client, server, writer) = await Connected(Abort: gated => { aborted = true; gated.Fail(new IOException("cut off")); });

            using (client)
            using (server)
            {

                writer.Close();

                var sending = client.SendPacketAsync(new Byte[] { 94, 1, 2, 3 }, CancellationToken).AsTask();

                await writer.Entered.Task.WaitAsync(CancellationToken);

                await client.CloseOutputAsync(TimeSpan.FromMilliseconds(300)).AsTask().WaitAsync(CancellationToken);

                Assert.Multiple(() => {
                    Assert.That(aborted,                        Is.True,  "the send that would not end was not cut off");
                    Assert.That(sending.IsFaulted,              Is.True,  "the send cut off did not fail");
                    Assert.That(writer.IsCompleted,             Is.True,  "closed, and the writer is not completed");
                    Assert.That(writer.CompletedWhileFlushing,  Is.False, "the writer was completed under a flush");
                });

            }

        }

        #endregion

    }

}
