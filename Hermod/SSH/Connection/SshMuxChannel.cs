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
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Threading.Channels;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>An inbound channel request routed to a channel (e.g. <c>exec</c>, <c>pty-req</c>, <c>exit-status</c>).</summary>
    /// <param name="Type">The request type.</param>
    /// <param name="WantReply">Whether the sender wants a CHANNEL_SUCCESS/FAILURE reply.</param>
    /// <param name="Data">The request-specific bytes (after type + want-reply).</param>
    public readonly record struct SshChannelRequest(String Type, Boolean WantReply, Byte[] Data);


    /// <summary>Thrown when a channel open is refused by the peer.</summary>
    public sealed class SshChannelOpenException : Exception
    {
        /// <summary>The SSH_MSG_CHANNEL_OPEN_FAILURE reason code.</summary>
        public UInt32 Reason { get; }
        /// <summary>Create a channel-open exception.</summary>
        public SshChannelOpenException(UInt32 Reason, String Message) : base(Message) { this.Reason = Reason; }
    }


    /// <summary>
    /// One channel on a <see cref="SshChannelMultiplexer"/>: inbound data is exposed as <see cref="Input"/> /
    /// <see cref="Error"/> streams (reading them replenishes the receive window), outbound data goes through
    /// <see cref="SendDataAsync"/> with the peer's window as backpressure, and channel requests flow both ways
    /// (<see cref="SendRequestAsync"/> / <see cref="ReadRequestAsync"/>). The multiplexer's receive loop feeds
    /// this object; consumers never touch the transport directly.
    /// </summary>
    public sealed class SshMuxChannel
    {

        #region Data

        private readonly SshChannelMultiplexer  mux;
        private readonly Pipe                   stdout = new (new PipeOptions(pauseWriterThreshold: 0, useSynchronizationContext: false));
        private readonly Pipe                   stderr = new (new PipeOptions(pauseWriterThreshold: 0, useSynchronizationContext: false));
        private readonly Channel<SshChannelRequest>  requests = System.Threading.Channels.Channel.CreateUnbounded<SshChannelRequest>();
        private readonly ConcurrentQueue<TaskCompletionSource<Boolean>>  requestReplies = new ();
        private readonly TaskCompletionSource  openTcs  = new (TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource  closeTcs = new (TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Lock                  sync = new ();

        private UInt32                         remoteId;
        private Int64                          remoteWindow;
        private TaskCompletionSource?          windowGrew;
        private Boolean                        sentEof, sentClose, inboundDone;
        private Stream?                        inputStream, errorStream;

        #endregion

        #region Properties

        /// <summary>Our local channel id.</summary>
        public UInt32   LocalId      { get; }

        /// <summary>The channel type (<c>session</c>, <c>direct-tcpip</c>, <c>forwarded-tcpip</c>, …).</summary>
        public String   ChannelType  { get; }

        /// <summary>The type-specific open bytes (e.g. the host/port block for a forwarding channel).</summary>
        public Byte[]   OpenData     { get; }

        /// <summary>The inbound data stream (stdout); reading it returns receive-window credit to the peer.</summary>
        public Stream   Input   => inputStream ??= new ChannelReadStream(stdout.Reader, n => Replenish((UInt32) n));

        /// <summary>The inbound extended-data stream (stderr); reading it also returns window credit.</summary>
        public Stream   Error   => errorStream ??= new ChannelReadStream(stderr.Reader, n => Replenish((UInt32) n));

        /// <summary>Completes when the channel is fully closed.</summary>
        public Task     Closed  => closeTcs.Task;

        internal Task   OpenAwaitable => openTcs.Task;

        #endregion

        #region Constructor(s)

        internal SshMuxChannel(SshChannelMultiplexer Mux, UInt32 LocalId, String ChannelType, Byte[] OpenData)
        {
            this.mux          = Mux;
            this.LocalId      = LocalId;
            this.ChannelType  = ChannelType;
            this.OpenData     = OpenData;
        }

        #endregion


        #region Outbound

        /// <summary>Send bytes as channel data, chunked to the max packet and honouring the peer's window.</summary>
        public async ValueTask SendDataAsync(ReadOnlyMemory<Byte> Data, CancellationToken CancellationToken = default)
        {
            var offset = 0;
            while (offset < Data.Length)
            {
                Task?  wait  = null;
                Int64  avail = 0;
                lock (sync)
                {
                    if (remoteWindow <= 0) { windowGrew ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); wait = windowGrew.Task; }
                    else avail = remoteWindow;
                }
                if (wait is not null) { await wait.WaitAsync(CancellationToken).ConfigureAwait(false); continue; }

                var chunk = (Int32) Math.Min(Math.Min(SshChannelMultiplexer.MaxPacket, (UInt32) (Data.Length - offset)), (UInt32) Math.Min(avail, SshChannelMultiplexer.MaxPacket));
                if (chunk <= 0) continue;

                await mux.SendAsync(SshChannelWire.ChannelData(remoteId, Data.Slice(offset, chunk).Span), CancellationToken).ConfigureAwait(false);
                lock (sync) remoteWindow -= chunk;
                offset += chunk;
            }
        }

        /// <summary>Send bytes as extended (stderr) channel data.</summary>
        public async ValueTask SendErrorAsync(ReadOnlyMemory<Byte> Data, CancellationToken CancellationToken = default)
            => await mux.SendAsync(SshChannelWire.ChannelExtendedData(remoteId, 1, Data.Span), CancellationToken).ConfigureAwait(false);

        /// <summary>Send a channel request; when <paramref name="WantReply"/>, await the SUCCESS/FAILURE reply.</summary>
        public async ValueTask<Boolean> SendRequestAsync(String Type, Boolean WantReply, Byte[] Payload, CancellationToken CancellationToken = default)
        {
            TaskCompletionSource<Boolean>? tcs = null;
            if (WantReply) { tcs = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously); requestReplies.Enqueue(tcs); }
            await mux.SendAsync(SshChannelWire.ChannelRequest(remoteId, Type, WantReply, Payload), CancellationToken).ConfigureAwait(false);
            return tcs is null || await tcs.Task.WaitAsync(CancellationToken).ConfigureAwait(false);
        }

        /// <summary>Send EOF (no more outbound data).</summary>
        public async ValueTask SendEofAsync(CancellationToken CancellationToken = default)
        {
            Boolean send;
            lock (sync) { send = !sentEof; sentEof = true; }
            if (send) await mux.SendAsync(SshChannelWire.Eof(remoteId), CancellationToken).ConfigureAwait(false);
        }

        /// <summary>Send EOF and CLOSE for this channel.</summary>
        public async ValueTask CloseAsync(CancellationToken CancellationToken = default)
        {
            Boolean sendEof, sendClose;
            lock (sync) { sendEof = !sentEof; sentEof = true; sendClose = !sentClose; sentClose = true; }
            if (sendEof)   await mux.SendAsync(SshChannelWire.Eof(remoteId),   CancellationToken).ConfigureAwait(false);
            if (sendClose) await mux.SendAsync(SshChannelWire.Close(remoteId), CancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Inbound

        /// <summary>Read the next inbound channel request (e.g. the server reads <c>exec</c>, the client reads <c>exit-status</c>); null at end.</summary>
        public async ValueTask<SshChannelRequest?> ReadRequestAsync(CancellationToken CancellationToken = default)
        {
            try
            {
                if (await requests.Reader.WaitToReadAsync(CancellationToken).ConfigureAwait(false) && requests.Reader.TryRead(out var req))
                    return req;
                return null;
            }
            catch (ChannelClosedException) { return null; }
        }

        /// <summary>Reply to the most recent want-reply request (rarely needed directly; used by request handlers).</summary>
        public ValueTask ReplyAsync(Boolean Success, CancellationToken CancellationToken = default)
            => mux.SendAsync(Success ? SshChannelWire.ChannelSuccess(remoteId) : SshChannelWire.ChannelFailure(remoteId), CancellationToken);

        #endregion

        #region AsStream()

        /// <summary>View this channel's data plane as a bidirectional <see cref="Stream"/> (read = stdout, write = channel data).</summary>
        public Stream AsStream() => new MuxChannelDuplexStream(this);

        private sealed class MuxChannelDuplexStream : Stream
        {
            private readonly SshMuxChannel  channel;
            private readonly Stream         input;
            private Boolean                 closed;

            public MuxChannelDuplexStream(SshMuxChannel Channel) { channel = Channel; input = Channel.Input; }

            public override ValueTask<Int32> ReadAsync(Memory<Byte> b, CancellationToken ct = default) => input.ReadAsync(b, ct);
            public override Task<Int32> ReadAsync(Byte[] b, Int32 o, Int32 c, CancellationToken ct)      => input.ReadAsync(b.AsMemory(o, c), ct).AsTask();
            public override Int32 Read(Byte[] b, Int32 o, Int32 c)                                       => input.ReadAsync(b.AsMemory(o, c)).AsTask().GetAwaiter().GetResult();

            public override ValueTask WriteAsync(ReadOnlyMemory<Byte> b, CancellationToken ct = default) => channel.SendDataAsync(b, ct);
            public override Task WriteAsync(Byte[] b, Int32 o, Int32 c, CancellationToken ct)            => channel.SendDataAsync(b.AsMemory(o, c), ct).AsTask();
            public override void Write(Byte[] b, Int32 o, Int32 c)                                       => channel.SendDataAsync(b.AsMemory(o, c)).AsTask().GetAwaiter().GetResult();

            public override Boolean CanRead => !closed;
            public override Boolean CanWrite => !closed;
            public override Boolean CanSeek => false;
            public override Int64 Length => throw new NotSupportedException();
            public override Int64 Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override Task FlushAsync(CancellationToken ct) => Task.CompletedTask;
            public override Int64 Seek(Int64 o, SeekOrigin s) => throw new NotSupportedException();
            public override void SetLength(Int64 v) => throw new NotSupportedException();

            public override async ValueTask DisposeAsync()
            {
                if (closed) return; closed = true;
                try { await channel.CloseAsync().ConfigureAwait(false); } catch { }
                await base.DisposeAsync().ConfigureAwait(false);
            }
            protected override void Dispose(Boolean disposing)
            {
                if (closed) return; closed = true;
                if (disposing) try { channel.CloseAsync().AsTask().GetAwaiter().GetResult(); } catch { }
                base.Dispose(disposing);
            }
        }

        #endregion


        #region (internal) events from the multiplexer

        internal void OnOpenConfirmed(UInt32 RemoteId, UInt32 Window)
        {
            lock (sync) { remoteId = RemoteId; remoteWindow = Window; }
            openTcs.TrySetResult();
        }

        internal void OnOpenFailed(UInt32 Reason, String Text)
            => openTcs.TrySetException(new SshChannelOpenException(Reason, Text));

        internal async ValueTask OnDataAsync(Byte[] Data)
        {
            await stdout.Writer.WriteAsync(Data).ConfigureAwait(false);
        }

        internal async ValueTask OnExtendedDataAsync(UInt32 Code, Byte[] Data)
        {
            if (Code == 1) await stderr.Writer.WriteAsync(Data).ConfigureAwait(false);
            else           Replenish((UInt32) Data.Length);   // unknown extended data: just return the window
        }

        internal void OnWindowAdjust(UInt32 Increment)
        {
            TaskCompletionSource? grew;
            lock (sync) { remoteWindow += Increment; grew = windowGrew; windowGrew = null; }
            grew?.TrySetResult();
        }

        internal async ValueTask OnEofAsync() => await CompleteDataAsync().ConfigureAwait(false);

        internal async ValueTask OnCloseAsync()
        {
            Boolean sendClose;
            lock (sync) { sendClose = !sentClose; sentClose = true; }
            if (sendClose) await mux.SendAsync(SshChannelWire.Close(remoteId)).ConfigureAwait(false);
            await CompleteDataAsync().ConfigureAwait(false);
            requests.Writer.TryComplete();   // no more inbound requests once the channel closes
            closeTcs.TrySetResult();
            mux.Remove(LocalId);
        }

        internal void OnRequest(SshChannelRequest Request) => requests.Writer.TryWrite(Request);

        internal void OnRequestReply(Boolean Success)
        {
            if (requestReplies.TryDequeue(out var tcs)) tcs.TrySetResult(Success);
        }

        internal void Fault(Exception Exception)
        {
            openTcs.TrySetException(Exception);
            closeTcs.TrySetException(Exception);
            try { stdout.Writer.Complete(Exception); } catch { }
            try { stderr.Writer.Complete(Exception); } catch { }
            requests.Writer.TryComplete();
            TaskCompletionSource? grew;
            lock (sync) { grew = windowGrew; windowGrew = null; }
            grew?.TrySetException(Exception);
        }

        #endregion

        #region (private) helpers

        private async ValueTask CompleteDataAsync()
        {
            Boolean complete;
            lock (sync) { complete = !inboundDone; inboundDone = true; }
            if (complete)
            {
                await stdout.Writer.CompleteAsync().ConfigureAwait(false);
                await stderr.Writer.CompleteAsync().ConfigureAwait(false);
            }
        }

        private void Replenish(UInt32 Bytes)
        {
            if (Bytes == 0) return;
            _ = mux.SendAsync(SshChannelWire.WindowAdjust(remoteId, Bytes));
        }

        #endregion


        #region (private) ChannelReadStream

        private sealed class ChannelReadStream : Stream
        {
            private readonly PipeReader    reader;
            private readonly Action<Int32> onConsumed;

            public ChannelReadStream(PipeReader Reader, Action<Int32> OnConsumed) { reader = Reader; onConsumed = OnConsumed; }

            public override async ValueTask<Int32> ReadAsync(Memory<Byte> Buffer, CancellationToken CancellationToken = default)
            {
                var result = await reader.ReadAsync(CancellationToken).ConfigureAwait(false);
                if (result.Buffer.IsEmpty && result.IsCompleted)
                    return 0;
                var toCopy = (Int32) Math.Min(Buffer.Length, result.Buffer.Length);
                result.Buffer.Slice(0, toCopy).CopyTo(Buffer.Span);
                reader.AdvanceTo(result.Buffer.GetPosition(toCopy));
                onConsumed(toCopy);
                return toCopy;
            }

            public override Task<Int32> ReadAsync(Byte[] b, Int32 o, Int32 c, CancellationToken ct) => ReadAsync(b.AsMemory(o, c), ct).AsTask();
            public override Int32 Read(Byte[] b, Int32 o, Int32 c) => ReadAsync(b.AsMemory(o, c), CancellationToken.None).AsTask().GetAwaiter().GetResult();

            public override Boolean CanRead => true;
            public override Boolean CanSeek => false;
            public override Boolean CanWrite => false;
            public override Int64 Length => throw new NotSupportedException();
            public override Int64 Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override Int64 Seek(Int64 o, SeekOrigin s) => throw new NotSupportedException();
            public override void SetLength(Int64 v) => throw new NotSupportedException();
            public override void Write(Byte[] b, Int32 o, Int32 c) => throw new NotSupportedException();
        }

        #endregion

    }


    #region SshChannelWire — connection-protocol message builders

    internal static class SshChannelWire
    {

        public static Byte[] ChannelOpen(String Type, UInt32 Sender, UInt32 Window, UInt32 MaxPacket, Byte[] TypeData)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.ChannelOpen);
            w.WriteString(Type); w.WriteUInt32(Sender); w.WriteUInt32(Window); w.WriteUInt32(MaxPacket);
            var head = abw.WrittenSpan.ToArray();
            return Concat(head, TypeData);
        }

        public static Byte[] ChannelOpenConfirmation(UInt32 Recipient, UInt32 Sender, UInt32 Window, UInt32 MaxPacket)
            => Build(SshMessageNumber.ChannelOpenConfirmation, (ref SshPacketWriter w) => { w.WriteUInt32(Recipient); w.WriteUInt32(Sender); w.WriteUInt32(Window); w.WriteUInt32(MaxPacket); });

        public static Byte[] ChannelOpenFailure(UInt32 Recipient, UInt32 Reason, String Description)
            => Build(SshMessageNumber.ChannelOpenFailure, (ref SshPacketWriter w) => { w.WriteUInt32(Recipient); w.WriteUInt32(Reason); w.WriteString(Description); w.WriteString(""); });

        public static Byte[] ChannelData(UInt32 Recipient, ReadOnlySpan<Byte> Data)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.ChannelData); w.WriteUInt32(Recipient); w.WriteBinaryString(Data);
            return abw.WrittenSpan.ToArray();
        }

        public static Byte[] ChannelExtendedData(UInt32 Recipient, UInt32 Code, ReadOnlySpan<Byte> Data)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.ChannelExtendedData); w.WriteUInt32(Recipient); w.WriteUInt32(Code); w.WriteBinaryString(Data);
            return abw.WrittenSpan.ToArray();
        }

        public static Byte[] WindowAdjust(UInt32 Recipient, UInt32 Increment)
            => Build(SshMessageNumber.ChannelWindowAdjust, (ref SshPacketWriter w) => { w.WriteUInt32(Recipient); w.WriteUInt32(Increment); });

        public static Byte[] ChannelRequest(UInt32 Recipient, String Type, Boolean WantReply, Byte[] Payload)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.ChannelRequest); w.WriteUInt32(Recipient); w.WriteString(Type); w.WriteBoolean(WantReply);
            return Concat(abw.WrittenSpan.ToArray(), Payload);
        }

        public static Byte[] GlobalRequest(String Name, Boolean WantReply, Byte[] Data)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.GlobalRequest); w.WriteString(Name); w.WriteBoolean(WantReply);
            return Concat(abw.WrittenSpan.ToArray(), Data);
        }

        public static Byte[] Eof(UInt32 Recipient)            => Build(SshMessageNumber.ChannelEof,     (ref SshPacketWriter w) => w.WriteUInt32(Recipient));
        public static Byte[] Close(UInt32 Recipient)          => Build(SshMessageNumber.ChannelClose,   (ref SshPacketWriter w) => w.WriteUInt32(Recipient));
        public static Byte[] ChannelSuccess(UInt32 Recipient) => Build(SshMessageNumber.ChannelSuccess, (ref SshPacketWriter w) => w.WriteUInt32(Recipient));
        public static Byte[] ChannelFailure(UInt32 Recipient) => Build(SshMessageNumber.ChannelFailure, (ref SshPacketWriter w) => w.WriteUInt32(Recipient));

        private delegate void Write(ref SshPacketWriter Writer);

        private static Byte[] Build(SshMessageNumber Message, Write Body)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) Message);
            Body(ref w);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] Concat(Byte[] A, Byte[] B)
        {
            if (B.Length == 0) return A;
            var result = new Byte[A.Length + B.Length];
            A.CopyTo(result, 0); B.CopyTo(result, A.Length);
            return result;
        }

    }

    #endregion

}
