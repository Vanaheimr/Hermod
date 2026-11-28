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
    /// A flush that comes back canceled has written nothing, and the packet it was to send is
    /// sent all the same.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The writer of a socket's pipe is a StreamPipeWriter. Its FlushAsync(token) registers the
    /// token to cancel a token source of its own, and resets that source only where a flush ends in
    /// a cancellation. Canceled just after a flush that succeeded - a session's EOF sent with the
    /// token of its channel, which the client's CLOSE cancels - the source stays canceled, and the
    /// next flush, of whatever packet comes next, returns a FlushResult with IsCanceled set
    /// without writing a byte. That packet stayed in the pipe, the send lock free, until some
    /// later packet was flushed; where the peer was waiting for it, never.
    /// </para>
    /// <para>
    /// Seen as a CHANNEL_OPEN_CONFIRMATION a client waited for for ever, the server's writer
    /// holding its 44 octets (the NTS Conformance Tests, NTP over SSH, 2026-11-28), and as the
    /// subsystem request of NTS-KE over SSH that went unanswered in their CI runs. CancelPendingFlush
    /// leaves the StreamPipeWriter in the same state, and makes it happen when a test wants it.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class TransportCanceledFlushTests
    {

        /// <summary>
        /// A client whose writer is a StreamPipeWriter over the stream to the server, as a socket's
        /// is, and the server, after their handshake.
        /// </summary>
        private static async Task<(SshTransport Client, SshTransport Server, PipeWriter Writer)> Connected()
        {

            var clientToServer  = new Pipe();
            var serverToClient  = new Pipe();
            var writer          = PipeWriter.Create(clientToServer.Writer.AsStream());

            var clientPipe      = new DuplexPipe(serverToClient.Reader, writer);
            var serverPipe      = new DuplexPipe(clientToServer.Reader, serverToClient.Writer);

            var hostKey         = Ed25519KeyPair.Generate();
            var clientTask      = SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe).AsTask();
            var serverTask      = SshTransport.ServerHandshakeAsync(serverPipe, hostKey).AsTask();

            return (await clientTask, await serverTask, writer);

        }


        [Test]
        [CancelAfter(20000)]
        public async Task A_Packet_Whose_Flush_Comes_Back_Canceled_Is_Sent(CancellationToken CancellationToken)
        {

            var (client, server, writer) = await Connected();

            using (client)
            using (server)
            {

                await client.SendPacketAsync(new Byte[] { 94, 1, 2, 3 }, CancellationToken);
                Assert.That((await server.ReceivePacketAsync(CancellationToken)).ToArray(), Is.EqualTo(new Byte[] { 94, 1, 2, 3 }));

                // The state a token canceled just after a flush leaves the writer in.
                writer.CancelPendingFlush();

                await client.SendPacketAsync(new Byte[] { 94, 4, 5, 6 }, CancellationToken);

                using var wait = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
                wait.CancelAfter(TimeSpan.FromSeconds(5));

                Assert.That((await server.ReceivePacketAsync(wait.Token)).ToArray(), Is.EqualTo(new Byte[] { 94, 4, 5, 6 }),
                            "the packet whose flush came back canceled");

            }

        }

    }

}
