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
using System.Threading.Channels;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>An inbound channel-open request the multiplexer is asked to accept or reject.</summary>
    /// <param name="ChannelType">The channel type (e.g. <c>session</c>, <c>direct-tcpip</c>, <c>forwarded-tcpip</c>).</param>
    /// <param name="SenderChannel">The peer's channel id.</param>
    /// <param name="InitialWindow">The peer's initial window.</param>
    /// <param name="MaxPacket">The peer's maximum packet size.</param>
    /// <param name="TypeData">The remaining type-specific bytes (e.g. host/port for direct-tcpip).</param>
    public readonly record struct SshChannelOpenInfo(String ChannelType, UInt32 SenderChannel, UInt32 InitialWindow, UInt32 MaxPacket, Byte[] TypeData);

    /// <summary>An inbound global request (e.g. <c>tcpip-forward</c>).</summary>
    /// <param name="Name">The request name.</param>
    /// <param name="WantReply">Whether the peer expects a reply.</param>
    /// <param name="Data">The remaining request-specific bytes.</param>
    public readonly record struct SshGlobalRequestInfo(String Name, Boolean WantReply, Byte[] Data);

    /// <summary>
    /// The answer to a global request: REQUEST_SUCCESS or REQUEST_FAILURE, plus any response payload.
    /// Most requests carry no payload; <c>hostkeys-prove-00@openssh.com</c> answers with signatures.
    /// </summary>
    /// <param name="Success">Whether to answer REQUEST_SUCCESS (else REQUEST_FAILURE).</param>
    /// <param name="Data">The response-specific bytes that follow the reply message number.</param>
    public readonly record struct SshGlobalRequestReply(Boolean Success, Byte[] Data)
    {

        /// <summary>A successful reply without a payload.</summary>
        public static SshGlobalRequestReply Ok       { get; } = new (true,  []);

        /// <summary>A REQUEST_FAILURE reply.</summary>
        public static SshGlobalRequestReply Failed   { get; } = new (false, []);

        /// <summary>A successful reply carrying a response payload.</summary>
        public static SshGlobalRequestReply WithData(Byte[] Data) => new (true, Data);

    }


    /// <summary>
    /// The SSH connection multiplexer: a single background loop reads the transport and demultiplexes every
    /// packet to the right channel (or to connection-global handling), so <b>many channels run concurrently</b>
    /// on one connection — the foundation for parallel exec/SFTP/forwards and for remote (<c>-R</c>)
    /// forwarding. Both roles use it: open channels with <see cref="OpenChannelAsync"/>, accept incoming ones
    /// with <see cref="AcceptChannelAsync"/> (gated by <see cref="ChannelAcceptor"/>), and drive global
    /// requests via <see cref="SendGlobalRequestAsync"/> / <see cref="GlobalRequestHandler"/>. All sends are
    /// serialized; PING/PONG, peer-initiated rekey and DISCONNECT are handled centrally.
    /// </summary>
    public sealed class SshChannelMultiplexer : IAsyncDisposable
    {

        #region Constants

        internal const UInt32 InitialWindow  = 2 * 1024 * 1024;
        internal const UInt32 MaxPacket      = 32 * 1024;

        #endregion

        #region Data

        private readonly SshTransport                              transport;
        private readonly SemaphoreSlim                             sendGate = new (1, 1);
        private readonly CancellationTokenSource                   cts      = new ();
        private readonly ConcurrentDictionary<UInt32, SshMuxChannel>  channels = new ();
        private readonly Channel<SshMuxChannel>                    accepted = System.Threading.Channels.Channel.CreateUnbounded<SshMuxChannel>();
        private readonly ConcurrentQueue<TaskCompletionSource<SshGlobalRequestReply>>  pendingGlobalReplies = new ();
        private Int32                                             nextLocalId;
        private Task                                             runTask = Task.CompletedTask;

        #endregion

        #region Properties

        /// <summary>The underlying transport.</summary>
        public SshTransport Transport => transport;

        /// <summary>The session identifier of the underlying transport (the first exchange hash).</summary>
        public Byte[] SessionId => transport.SessionId;

        /// <summary>Whether this end is the server.</summary>
        public Boolean IsServer => transport.IsServer;

        /// <summary>
        /// Decides whether to accept an inbound channel-open (default: accept only <c>session</c>). Return
        /// true to confirm the channel (it is then queued for <see cref="AcceptChannelAsync"/>), false to
        /// refuse it with ADMINISTRATIVELY_PROHIBITED.
        /// </summary>
        public Func<SshChannelOpenInfo, ValueTask<Boolean>> ChannelAcceptor { get; set; }
            = info => ValueTask.FromResult(info.ChannelType == "session");

        /// <summary>
        /// Handles an inbound global request (e.g. <c>tcpip-forward</c>); return true to answer
        /// REQUEST_SUCCESS, false for REQUEST_FAILURE. Default: refuse everything.
        /// </summary>
        public Func<SshGlobalRequestInfo, ValueTask<Boolean>> GlobalRequestHandler { get; set; }
            = _ => ValueTask.FromResult(false);

        /// <summary>
        /// An optional global-request handler that can answer with a <b>response payload</b> (which
        /// <see cref="GlobalRequestHandler"/> cannot). It is consulted <i>first</i>; returning
        /// <c>null</c> means "not mine" and falls through to <see cref="GlobalRequestHandler"/>, so
        /// independent features (e.g. <c>hostkeys-prove-00@openssh.com</c> and <c>tcpip-forward</c>)
        /// can be wired side by side without clobbering each other.
        /// </summary>
        public Func<SshGlobalRequestInfo, ValueTask<SshGlobalRequestReply?>>? GlobalRequestResponder { get; set; }

        #endregion

        #region Constructor(s) / Start

        /// <summary>Create a multiplexer over an established transport (call <see cref="Start"/> to run the loop).</summary>
        public SshChannelMultiplexer(SshTransport Transport)
        {
            this.transport = Transport;
        }

        /// <summary>Start the background receive/dispatch loop.</summary>
        public SshChannelMultiplexer Start()
        {
            runTask = Task.Run(RunAsync);
            return this;
        }

        #endregion


        #region OpenChannelAsync(ChannelType, TypeData, CancellationToken)

        /// <summary>
        /// Open a channel of the given type (client-initiated) and await the peer's confirmation. For
        /// <c>direct-tcpip</c> pass the host/port block as <paramref name="TypeData"/>.
        /// </summary>
        public async ValueTask<SshMuxChannel> OpenChannelAsync(String ChannelType, Byte[]? TypeData = null, CancellationToken CancellationToken = default)
        {

            var localId = (UInt32) Interlocked.Increment(ref nextLocalId);
            var channel = new SshMuxChannel(this, localId, ChannelType, TypeData ?? []);
            channels[localId] = channel;

            await SendAsync(SshChannelWire.ChannelOpen(ChannelType, localId, InitialWindow, MaxPacket, TypeData ?? []), CancellationToken).ConfigureAwait(false);

            await channel.OpenAwaitable.WaitAsync(CancellationToken).ConfigureAwait(false);
            return channel;

        }

        #endregion

        #region AcceptChannelAsync(CancellationToken)

        /// <summary>Await the next inbound channel that <see cref="ChannelAcceptor"/> accepted.</summary>
        public async ValueTask<SshMuxChannel> AcceptChannelAsync(CancellationToken CancellationToken = default)
            => await accepted.Reader.ReadAsync(CancellationToken).ConfigureAwait(false);

        #endregion

        #region SendGlobalRequestAsync(Name, WantReply, Data, CancellationToken)

        /// <summary>Send a global request (e.g. <c>tcpip-forward</c>); when <paramref name="WantReply"/>, await the SUCCESS/FAILURE reply.</summary>
        public async ValueTask<Boolean> SendGlobalRequestAsync(String Name, Boolean WantReply, Byte[] Data, CancellationToken CancellationToken = default)
            => (await SendGlobalRequestWithReplyAsync(Name, WantReply, Data, CancellationToken).ConfigureAwait(false)).Success;

        /// <summary>
        /// Send a global request and return the peer's full reply <i>including any response payload</i> —
        /// needed for requests that answer with data, such as <c>hostkeys-prove-00@openssh.com</c>.
        /// </summary>
        public async ValueTask<SshGlobalRequestReply> SendGlobalRequestWithReplyAsync(String Name, Boolean WantReply, Byte[] Data, CancellationToken CancellationToken = default)
        {

            TaskCompletionSource<SshGlobalRequestReply>? tcs = null;
            if (WantReply)
            {
                tcs = new TaskCompletionSource<SshGlobalRequestReply>(TaskCreationOptions.RunContinuationsAsynchronously);
                pendingGlobalReplies.Enqueue(tcs);
            }

            await SendAsync(SshChannelWire.GlobalRequest(Name, WantReply, Data), CancellationToken).ConfigureAwait(false);

            return tcs is null
                       ? SshGlobalRequestReply.Ok
                       : await tcs.Task.WaitAsync(CancellationToken).ConfigureAwait(false);

        }

        #endregion


        #region (internal) send helpers used by channels

        internal async ValueTask SendAsync(Byte[] Payload, CancellationToken CancellationToken = default)
        {
            await sendGate.WaitAsync(CancellationToken).ConfigureAwait(false);
            try     { await transport.SendPacketAsync(Payload, CancellationToken).ConfigureAwait(false); }
            finally { sendGate.Release(); }
        }

        internal void Remove(UInt32 LocalId) => channels.TryRemove(LocalId, out _);

        #endregion

        #region (private) run loop

        private async Task RunAsync()
        {
            try
            {
                while (true)
                {
                    var payload = await transport.ReceivePacketAsync(cts.Token).ConfigureAwait(false);
                    await DispatchAsync(payload).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)         { FaultAll(exception); }
            finally                             { CompleteAll(); }
        }

        private async ValueTask DispatchAsync(Byte[] Payload)
        {

            var message = (SshMessageNumber) Payload[0];

            switch (message)
            {

                case SshMessageNumber.ChannelOpen:
                    await HandleChannelOpenAsync(Payload).ConfigureAwait(false);
                    break;

                case SshMessageNumber.ChannelOpenConfirmation:
                {
                    var r = new SshPacketReader(Payload); r.ReadByte();
                    var local = r.ReadUInt32(); var remote = r.ReadUInt32(); var window = r.ReadUInt32();
                    if (channels.TryGetValue(local, out var ch)) ch.OnOpenConfirmed(remote, window);
                    break;
                }

                case SshMessageNumber.ChannelOpenFailure:
                {
                    var r = new SshPacketReader(Payload); r.ReadByte();
                    var local = r.ReadUInt32(); var reason = r.ReadUInt32(); var text = r.ReadString();
                    if (channels.TryRemove(local, out var ch)) ch.OnOpenFailed(reason, text);
                    break;
                }

                case SshMessageNumber.ChannelData:
                {
                    var r = new SshPacketReader(Payload); r.ReadByte();
                    var local = r.ReadUInt32(); var data = r.ReadBinaryString();
                    if (channels.TryGetValue(local, out var ch)) await ch.OnDataAsync(data).ConfigureAwait(false);
                    break;
                }

                case SshMessageNumber.ChannelExtendedData:
                {
                    var r = new SshPacketReader(Payload); r.ReadByte();
                    var local = r.ReadUInt32(); var code = r.ReadUInt32(); var data = r.ReadBinaryString();
                    if (channels.TryGetValue(local, out var ch)) await ch.OnExtendedDataAsync(code, data).ConfigureAwait(false);
                    break;
                }

                case SshMessageNumber.ChannelWindowAdjust:
                {
                    var r = new SshPacketReader(Payload); r.ReadByte();
                    var local = r.ReadUInt32(); var inc = r.ReadUInt32();
                    if (channels.TryGetValue(local, out var ch)) ch.OnWindowAdjust(inc);
                    break;
                }

                case SshMessageNumber.ChannelEof:
                {
                    var local = LocalOf(Payload);
                    if (channels.TryGetValue(local, out var ch)) await ch.OnEofAsync().ConfigureAwait(false);
                    break;
                }

                case SshMessageNumber.ChannelClose:
                {
                    var local = LocalOf(Payload);
                    if (channels.TryGetValue(local, out var ch)) await ch.OnCloseAsync().ConfigureAwait(false);
                    break;
                }

                case SshMessageNumber.ChannelRequest:
                {
                    var r = new SshPacketReader(Payload); r.ReadByte();
                    var local = r.ReadUInt32(); var type = r.ReadString(); var wantReply = r.ReadBoolean();
                    var rest  = Payload.AsSpan((Int32) r.Position).ToArray();
                    if (channels.TryGetValue(local, out var ch)) ch.OnRequest(new SshChannelRequest(type, wantReply, rest));
                    break;
                }

                case SshMessageNumber.ChannelSuccess:
                {
                    var local = LocalOf(Payload);
                    if (channels.TryGetValue(local, out var ch)) ch.OnRequestReply(true);
                    break;
                }

                case SshMessageNumber.ChannelFailure:
                {
                    var local = LocalOf(Payload);
                    if (channels.TryGetValue(local, out var ch)) ch.OnRequestReply(false);
                    break;
                }

                case SshMessageNumber.GlobalRequest:
                    await HandleGlobalRequestAsync(Payload).ConfigureAwait(false);
                    break;

                case SshMessageNumber.RequestSuccess:
                    if (pendingGlobalReplies.TryDequeue(out var okTcs))
                        okTcs.TrySetResult(new SshGlobalRequestReply(true, Payload.AsSpan(1).ToArray()));
                    break;

                case SshMessageNumber.RequestFailure:
                    if (pendingGlobalReplies.TryDequeue(out var failTcs)) failTcs.TrySetResult(SshGlobalRequestReply.Failed);
                    break;

                case SshMessageNumber.Ping:
                {
                    var r = new SshPacketReader(Payload); r.ReadByte(); var data = r.ReadBinaryString();
                    var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
                    w.WriteByte((Byte) SshMessageNumber.Pong); w.WriteBinaryString(data);
                    await SendAsync(abw.WrittenSpan.ToArray()).ConfigureAwait(false);
                    break;
                }

                case SshMessageNumber.KexInit:
                    await transport.RespondToRekeyAsync(Payload, cts.Token).ConfigureAwait(false);
                    break;

                case SshMessageNumber.Disconnect:
                    await cts.CancelAsync().ConfigureAwait(false);
                    break;

                default:
                    break;   // IGNORE / DEBUG / unhandled — skip

            }

        }

        private async ValueTask HandleChannelOpenAsync(Byte[] Payload)
        {

            var r      = new SshPacketReader(Payload); r.ReadByte();
            var type   = r.ReadString();
            var sender = r.ReadUInt32();
            var window = r.ReadUInt32();
            var maxPkt = r.ReadUInt32();
            var rest   = Payload.AsSpan((Int32) r.Position).ToArray();

            var info   = new SshChannelOpenInfo(type, sender, window, maxPkt, rest);

            if (await ChannelAcceptor(info).ConfigureAwait(false))
            {
                var localId = (UInt32) Interlocked.Increment(ref nextLocalId);
                var channel = new SshMuxChannel(this, localId, type, rest);
                channel.OnOpenConfirmed(sender, window);
                channels[localId] = channel;
                await SendAsync(SshChannelWire.ChannelOpenConfirmation(sender, localId, InitialWindow, MaxPacket)).ConfigureAwait(false);
                accepted.Writer.TryWrite(channel);
            }
            else
                await SendAsync(SshChannelWire.ChannelOpenFailure(sender, 1, "administratively prohibited")).ConfigureAwait(false);

        }

        private async ValueTask HandleGlobalRequestAsync(Byte[] Payload)
        {
            var r         = new SshPacketReader(Payload); r.ReadByte();
            var name      = r.ReadString();
            var wantReply = r.ReadBoolean();
            var rest      = Payload.AsSpan((Int32) r.Position).ToArray();

            var info  = new SshGlobalRequestInfo(name, wantReply, rest);

            // The payload-capable responder gets first refusal; null means "not mine".
            var reply = GlobalRequestResponder is not null
                            ? await GlobalRequestResponder(info).ConfigureAwait(false)
                            : null;

            reply ??= await GlobalRequestHandler(info).ConfigureAwait(false)
                          ? SshGlobalRequestReply.Ok
                          : SshGlobalRequestReply.Failed;

            if (wantReply)
            {
                var data     = reply.Value.Data;
                var response = new Byte[1 + data.Length];
                response[0]  = (Byte) (reply.Value.Success ? SshMessageNumber.RequestSuccess : SshMessageNumber.RequestFailure);
                data.CopyTo(response, 1);
                await SendAsync(response).ConfigureAwait(false);
            }
        }

        private static UInt32 LocalOf(Byte[] Payload)
        {
            var r = new SshPacketReader(Payload); r.ReadByte();
            return r.ReadUInt32();
        }

        private void FaultAll(Exception Exception)
        {
            foreach (var ch in channels.Values) ch.Fault(Exception);
            while (pendingGlobalReplies.TryDequeue(out var tcs)) tcs.TrySetException(Exception);
            accepted.Writer.TryComplete(Exception);
        }

        private void CompleteAll()
        {
            foreach (var ch in channels.Values) ch.Fault(new SshChannelClosedException());
            accepted.Writer.TryComplete();
        }

        #endregion

        #region DisposeAsync()

        /// <summary>Stop the multiplexer and tear down all channels.</summary>
        public async ValueTask DisposeAsync()
        {
            await cts.CancelAsync().ConfigureAwait(false);
            try { await runTask.ConfigureAwait(false); } catch { }
            sendGate.Dispose();
            cts.Dispose();
        }

        #endregion

    }

}
