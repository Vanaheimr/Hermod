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

using System.Buffers;
using System.IO.Pipelines;

using org.GraphDefined.Vanaheimr.Hermod.Messaging;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// SSH over WebSocket: the byte stream of an SSH connection in the binary
    /// messages of a WebSocket connection of its own, chosen by its subprotocol.
    /// </summary>
    /// <remarks>
    /// SSH is not changed by it - key exchange, host key and authentication are
    /// those of SSH, end to end -, and the WebSocket is a way through a firewall
    /// or a proxy that lets nothing out but HTTPS. One protocol a connection:
    /// OCPP and the rest go over connections of their own, so that a large SFTP
    /// transfer holds none of them up, as WebSocket has no flow control of its
    /// own for one stream among others.
    /// </remarks>
    public static class SshOverWebSocket
    {

        /// <summary>
        /// The WebSocket subprotocol of SSH over WebSocket. Not "ssh@open.charging.cloud",
        /// as the SSH subsystem of OCPP is "ocpp@open.charging.cloud": a subprotocol is a
        /// token (RFC 6455, Section 4.1), and "@" is none of its characters - clients such
        /// as the one of .NET refuse to offer it.
        /// </summary>
        public const String  Subprotocol            = "ssh.open.charging.cloud";

        /// <summary>
        /// The most bytes of the SSH stream one binary message carries, by default:
        /// 32 KiB - as much as an SSH packet of a usual size, and small enough that a
        /// ping or a close is not held up behind it for long.
        /// </summary>
        public const Int32   DefaultMessageSize     = 32 * 1024;

        /// <summary>
        /// The largest binary message taken from the other end: 1 MiB. Where the
        /// frames are cut has no meaning, but a peer that sends one larger than this
        /// sends more than anything here sends at once, and is closed with 1009.
        /// </summary>
        public const UInt64  MaxIncomingMessageSize = 1024 * 1024;

        /// <summary>
        /// How many received bytes SSH may leave unread before the WebSocket is read
        /// no further: 1 MiB. Its connection then waits, and with it TCP, and the
        /// sender's window fills - memory held for a connection stays bounded.
        /// </summary>
        public const Int64   DefaultReceiveBuffer   = 1024 * 1024;

    }


    /// <summary>
    /// The bytes of an SSH connection over a WebSocket connection, as the
    /// <see cref="DuplexPipe"/> the SSH transport runs on: what SSH writes goes out
    /// in binary messages of at most a given size, and the binary messages that come
    /// in are read by SSH one after the other - where the frames were cut has no
    /// meaning, an SSH packet may span several, and one may carry several packets.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A text message ends the connection with 1003 (Unsupported Data): SSH is
    /// carried in binary messages alone. The end of the WebSocket - a close frame,
    /// a lost connection - is the end of what SSH reads; SSH ending its output
    /// closes the WebSocket with 1000.
    /// </para>
    /// <para>
    /// Both ways are bounded. A received message is given to SSH before the next is
    /// read, and a send is waited for before the next: neither side holds more than
    /// <see cref="SshOverWebSocket.DefaultReceiveBuffer"/> and one message.
    /// </para>
    /// </remarks>
    public sealed class WebSocketSshBridge
    {

        #region Data

        private readonly IMessageConnection  connection;
        private readonly Pipe                incoming;
        private readonly Pipe                outgoing;
        private readonly Int32               messageSize;
        private readonly SemaphoreSlim       writing       = new (1, 1);
        private          Int32               ended;
        private          Int32               closing;

        #endregion

        #region Properties

        /// <summary>
        /// The WebSocket connection the SSH stream goes over.
        /// </summary>
        public IMessageConnection  Connection    => connection;

        /// <summary>
        /// The pipe SSH runs on: <see cref="DuplexPipe.Abort"/> cuts the WebSocket, as
        /// it cuts a socket.
        /// </summary>
        public DuplexPipe          Pipe          { get; }

        /// <summary>
        /// Done once nothing is sent any more: SSH ended its output, a send failed,
        /// or the WebSocket ended.
        /// </summary>
        public Task                Sending       { get; }

        /// <summary>
        /// Done once the WebSocket has ended, however it ended.
        /// </summary>
        public Task                Ended         => endedSource.Task;

        private readonly TaskCompletionSource endedSource = new (TaskCreationOptions.RunContinuationsAsynchronously);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Carry an SSH stream over the given WebSocket connection, which has been
        /// opened already. Whoever receives its messages hands its binary ones to
        /// <see cref="ReceivedAsync"/> and its text ones to <see cref="TextReceivedAsync"/>,
        /// and tells of its end by <see cref="EndAsync"/>.
        /// </summary>
        /// <param name="Connection">The WebSocket connection.</param>
        /// <param name="LocalSocket">Where this end of the connection is.</param>
        /// <param name="RemoteSocket">Where the other end is.</param>
        /// <param name="MessageSize">The most bytes one binary message sent carries.</param>
        /// <param name="ReceiveBuffer">How many received bytes SSH may leave unread before the WebSocket is read no further.</param>
        public WebSocketSshBridge(IMessageConnection  Connection,
                                  IPSocket?           LocalSocket     = null,
                                  IPSocket?           RemoteSocket    = null,
                                  Int32               MessageSize     = SshOverWebSocket.DefaultMessageSize,
                                  Int64               ReceiveBuffer   = SshOverWebSocket.DefaultReceiveBuffer)
        {

            ArgumentOutOfRangeException.ThrowIfLessThan(MessageSize,   1);
            ArgumentOutOfRangeException.ThrowIfLessThan(ReceiveBuffer, 1);

            this.connection   = Connection;
            this.messageSize  = MessageSize;

            this.incoming     = new Pipe(new PipeOptions(pauseWriterThreshold:       ReceiveBuffer,
                                                         resumeWriterThreshold:      Math.Max(1, ReceiveBuffer / 2),
                                                         useSynchronizationContext:  false));

            // What SSH writes waits for no more than a few messages to go out.
            this.outgoing     = new Pipe(new PipeOptions(pauseWriterThreshold:       4L * MessageSize,
                                                         resumeWriterThreshold:      2L * MessageSize,
                                                         useSynchronizationContext:  false));

            this.Pipe         = new DuplexPipe(incoming.Reader, outgoing.Writer) {
                                    Abort         = Abort,
                                    LocalSocket   = LocalSocket,
                                    RemoteSocket  = RemoteSocket
                                };

            this.Sending      = Task.Run(SendAsync);

        }

        #endregion


        #region ReceivedAsync     (Bytes, CancellationToken = default)

        /// <summary>
        /// Hand the bytes of a binary message received to SSH - waiting, where SSH has
        /// left as much unread as it may, until it has read enough of it: whoever reads
        /// the WebSocket reads no further meanwhile.
        /// </summary>
        /// <param name="Bytes">The bytes of the message.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task ReceivedAsync(ReadOnlyMemory<Byte>  Bytes,
                                        CancellationToken     CancellationToken = default)
        {

            var readsNoMore = false;

            await writing.WaitAsync(CancellationToken).ConfigureAwait(false);

            try
            {

                if (Volatile.Read(ref ended) != 0)
                    return;

                var flushed = await incoming.Writer.WriteAsync(Bytes, CancellationToken).ConfigureAwait(false);

                readsNoMore = flushed.IsCompleted;

            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (InvalidOperationException)
            {
                // Completed meanwhile: the connection has ended.
                return;
            }
            finally
            {
                writing.Release();
            }

            // SSH reads nothing any more: neither will anybody else.
            if (readsNoMore)
                await CloseAsync(MessageCloseCode.NormalClosure, null).ConfigureAwait(false);

        }

        #endregion

        #region TextReceivedAsync (CancellationToken = default)

        /// <summary>
        /// A text message was received, and SSH is carried in binary messages alone:
        /// close the connection with 1003 (Unsupported Data).
        /// </summary>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task TextReceivedAsync(CancellationToken CancellationToken = default)

            => CloseAsync(MessageCloseCode.UnsupportedData,
                          "SSH over WebSocket is carried in binary messages alone.",
                          CancellationToken);

        #endregion

        #region EndAsync          (Error = null)

        /// <summary>
        /// The WebSocket has ended: SSH reads to the end of what it was given, and then
        /// nothing - with the error, where there is one -, and nothing is sent any more.
        /// Once; a second call does nothing.
        /// </summary>
        /// <param name="Error">Why the connection ended, where it did not end as it should.</param>
        public async Task EndAsync(Exception? Error = null)
        {

            if (Interlocked.Exchange(ref ended, 1) != 0)
                return;

            outgoing.Reader.CancelPendingRead();
            incoming.Writer.CancelPendingFlush();

            await writing.WaitAsync().ConfigureAwait(false);

            try
            {
                await incoming.Writer.CompleteAsync(Error).ConfigureAwait(false);
            }
            catch
            { }
            finally
            {
                writing.Release();
            }

            endedSource.TrySetResult();

        }

        #endregion

        #region CloseAsync        (Code, Reason, CancellationToken = default)

        /// <summary>
        /// Close the WebSocket with the given code, once, and end what SSH reads.
        /// </summary>
        /// <param name="Code">Why the connection is closed.</param>
        /// <param name="Reason">An optional text for the other end.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task CloseAsync(MessageCloseCode   Code,
                                     String?            Reason,
                                     CancellationToken  CancellationToken = default)
        {

            if (Interlocked.Exchange(ref closing, 1) == 0)
            {
                try
                {
                    await connection.Close(Code, Reason, CancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // Gone already.
                }
            }

            await EndAsync(Code == MessageCloseCode.NormalClosure
                               ? null
                               : new IOException($"The WebSocket was closed with {(UInt16) Code} ({Code}){(Reason is null ? "" : $": {Reason}")}")).ConfigureAwait(false);

        }

        #endregion


        #region (private) SendAsync()

        // What SSH writes, in binary messages of at most messageSize bytes - each send
        // waited for before the next, so that no more than one is queued at a time.
        private async Task SendAsync()
        {

            Exception? failure = null;

            try
            {

                while (true)
                {

                    var read    = await outgoing.Reader.ReadAsync().ConfigureAwait(false);
                    var buffer  = read.Buffer;
                    var sent    = true;

                    if (read.IsCanceled)
                    {
                        outgoing.Reader.AdvanceTo(buffer.Start, buffer.End);
                        break;
                    }

                    while (!buffer.IsEmpty)
                    {

                        var message  = buffer.Slice(0, Math.Min(messageSize, buffer.Length));
                        var status   = await connection.SendBinaryMessage(message.ToArray()).ConfigureAwait(false);

                        if (status != SentStatus.Success)
                        {
                            failure  = new IOException($"A binary message of the SSH stream could not be sent: {status}.");
                            sent     = false;
                            break;
                        }

                        buffer = buffer.Slice(message.End);

                    }

                    outgoing.Reader.AdvanceTo(buffer.Start, buffer.End);

                    if (!sent)
                        break;

                    // SSH has said all it had to say.
                    if (read.IsCompleted)
                    {
                        await CloseAsync(MessageCloseCode.NormalClosure, null).ConfigureAwait(false);
                        break;
                    }

                }

            }
            catch (Exception exception)
            {
                failure = exception;
            }

            try
            {
                await outgoing.Reader.CompleteAsync(failure).ConfigureAwait(false);
            }
            catch
            { }

            if (failure is not null)
                await CloseAsync(MessageCloseCode.InternalServerError, null).ConfigureAwait(false);

        }

        #endregion

        #region (private) Abort()

        // As a socket is closed under whatever reads or writes it: nothing is sent any
        // more, SSH reads the end, and the other end is told that this one went away.
        private void Abort()
        {

            outgoing.Reader.CancelPendingRead();

            _ = CloseAsync(MessageCloseCode.GoingAway, null);

        }

        #endregion

    }

}
