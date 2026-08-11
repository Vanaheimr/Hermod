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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// A live, streaming remote command started with <see cref="SshConnection.StartCommandAsync"/>. Output
    /// is exposed as <see cref="StandardOutput"/> / <see cref="StandardError"/> streams that yield bytes as
    /// they arrive, input is written to <see cref="StandardInput"/> (with channel flow control), and
    /// <see cref="WaitForExitAsync"/> completes with the remote exit status. A background pump owns the
    /// transport's receive side for the lifetime of the command and, if configured, keeps the connection
    /// alive and enforces the idle timeout.
    /// </summary>
    public sealed class SshCommandProcess : IAsyncDisposable
    {

        #region Data

        private const UInt32 InitialWindow  = 2 * 1024 * 1024;   // 2 MiB
        private const UInt32 MaxPacket      = 32 * 1024;         // 32 KiB

        private readonly SshTransport             transport;
        private readonly UInt32                   localChannel;
        private readonly UInt32                   remoteChannel;
        private readonly Pipe                     stdoutPipe   = new ();
        private readonly Pipe                     stderrPipe   = new ();
        private readonly SemaphoreSlim            sendGate     = new (1, 1);
        private readonly CancellationTokenSource  cts          = new ();
        private readonly SshLivenessMonitor?      liveness;
        private readonly TimeProvider             timeProvider;

        private readonly TaskCompletionSource<Int32>  exitTcs  =
            new (TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly Object                   windowSync   = new ();
        private Int64                             remoteWindow;
        private TaskCompletionSource?             windowGrew;

        private UInt32                            myWindow     = InitialWindow;
        private Int32                             exitCode     = -1;
        private Boolean                           eofSent;
        private Boolean                           closeSent;

        private Task                              pumpTask     = Task.CompletedTask;
        private Task                              livenessTask = Task.CompletedTask;

        #endregion

        #region Properties

        /// <summary>The remote command's standard output as it streams in.</summary>
        public Stream  StandardOutput  => stdoutPipe.Reader.AsStream();

        /// <summary>The remote command's standard error as it streams in.</summary>
        public Stream  StandardError   => stderrPipe.Reader.AsStream();

        /// <summary>A stream that writes to the remote command's standard input; closing it sends EOF.</summary>
        public Stream  StandardInput   { get; }

        #endregion

        #region Constructor(s)

        private SshCommandProcess(SshTransport           Transport,
                                  UInt32                 LocalChannel,
                                  UInt32                 RemoteChannel,
                                  UInt32                 RemoteWindow,
                                  SshConnectionOptions?  Options)
        {

            this.transport      = Transport;
            this.localChannel   = LocalChannel;
            this.remoteChannel  = RemoteChannel;
            this.remoteWindow   = RemoteWindow;
            this.timeProvider   = Options?.TimeProvider ?? TimeProvider.System;
            this.liveness       = Options is { HasLiveness: true } ? Options.CreateLivenessMonitor() : null;
            this.StandardInput  = new ChannelInputStream(this);

        }

        #endregion


        #region (internal) StartAsync(Transport, Command, Options, CancellationToken)

        internal static async ValueTask<SshCommandProcess> StartAsync(SshTransport           Transport,
                                                                      SshCommand             Command,
                                                                      SshConnectionOptions?  Options,
                                                                      CancellationToken      CancellationToken)
        {

            const UInt32 localChannel = 0;

            await Transport.SendPacketAsync(BuildChannelOpen(localChannel, InitialWindow, MaxPacket), CancellationToken).ConfigureAwait(false);
            var (remoteChannel, remoteWindow) = await AwaitOpenConfirmationAsync(Transport, CancellationToken).ConfigureAwait(false);

            // env requests (want_reply = false) — the server silently drops any it does not accept.
            foreach (var (name, value) in Command.EnvironmentVariables)
                await Transport.SendPacketAsync(BuildEnvRequest(remoteChannel, name, value), CancellationToken).ConfigureAwait(false);

            if (Command.UsePty)
                await Transport.SendPacketAsync(BuildPtyRequest(remoteChannel, Command), CancellationToken).ConfigureAwait(false);

            await Transport.SendPacketAsync(Command.CommandLine.Length == 0
                                                ? BuildShellRequest(remoteChannel)
                                                : BuildExecRequest(remoteChannel, Command.CommandLine),
                                            CancellationToken).ConfigureAwait(false);

            var process = new SshCommandProcess(Transport, localChannel, remoteChannel, remoteWindow, Options);

            process.pumpTask = Task.Run(() => process.PumpAsync());

            if (process.liveness is not null)
                process.livenessTask = Task.Run(() => process.LivenessAsync());

            // Pipe the optional input stream to the remote stdin, then send EOF.
            if (Command.Input is not null)
                _ = Task.Run(() => process.CopyInputAsync(Command.Input));

            return process;

        }

        #endregion

        #region WaitForExitAsync(CancellationToken)

        /// <summary>Wait for the remote command to finish and return its exit status.</summary>
        /// <exception cref="SshConnectionLostException">The peer stopped responding (dead peer or idle timeout).</exception>
        public Task<Int32> WaitForExitAsync(CancellationToken CancellationToken = default)
            => exitTcs.Task.WaitAsync(CancellationToken);

        #endregion


        #region (private) receive pump

        private async Task PumpAsync()
        {

            try
            {

                while (true)
                {

                    var payload = await transport.ReceivePacketAsync(cts.Token).ConfigureAwait(false);
                    var message = (SshMessageNumber) payload[0];

                    switch (message)
                    {

                        case SshMessageNumber.ChannelData:
                        {
                            liveness?.RecordActivity();
                            var data = ParseChannelData(payload);
                            await stdoutPipe.Writer.WriteAsync(data, cts.Token).ConfigureAwait(false);
                            await ReplenishAsync((UInt32) data.Length).ConfigureAwait(false);
                            break;
                        }

                        case SshMessageNumber.ChannelExtendedData:
                        {
                            liveness?.RecordActivity();
                            var (code, data) = ParseChannelExtendedData(payload);
                            if (code == 1)   // SSH_EXTENDED_DATA_STDERR
                                await stderrPipe.Writer.WriteAsync(data, cts.Token).ConfigureAwait(false);
                            await ReplenishAsync((UInt32) data.Length).ConfigureAwait(false);
                            break;
                        }

                        case SshMessageNumber.ChannelWindowAdjust:
                        {
                            liveness?.RecordActivity();
                            GrantRemoteWindow(ParseWindowAdjust(payload));
                            break;
                        }

                        case SshMessageNumber.ChannelRequest:
                        {
                            liveness?.RecordActivity();
                            var (type, code) = ParseChannelRequest(payload);
                            if (type == "exit-status")
                            {
                                exitCode = (Int32) code;
                                exitTcs.TrySetResult(exitCode);
                            }
                            else if (type == "exit-signal")
                            {
                                exitCode = 255;
                                exitTcs.TrySetResult(exitCode);
                            }
                            break;
                        }

                        case SshMessageNumber.ChannelEof:
                            break;

                        case SshMessageNumber.ChannelClose:
                            if (!closeSent)
                            {
                                await SendAsync(Simple(SshMessageNumber.ChannelClose, remoteChannel)).ConfigureAwait(false);
                                closeSent = true;
                            }
                            await stdoutPipe.Writer.CompleteAsync().ConfigureAwait(false);
                            await stderrPipe.Writer.CompleteAsync().ConfigureAwait(false);
                            exitTcs.TrySetResult(exitCode);
                            return;

                        case SshMessageNumber.Ping:
                            liveness?.RecordKeepAliveReply();
                            await HandlePingAsync(payload).ConfigureAwait(false);
                            break;

                        case SshMessageNumber.RequestSuccess:
                        case SshMessageNumber.RequestFailure:
                            // A reply to one of our keepalive global requests — the peer is alive.
                            liveness?.RecordKeepAliveReply();
                            break;

                        case SshMessageNumber.GlobalRequest:
                            await DeclineGlobalRequestAsync(payload).ConfigureAwait(false);
                            break;

                        default:
                            break;

                    }

                }

            }
            catch (OperationCanceledException)
            {
                // Disposed or faulted elsewhere — leave the already-set completion in place.
                await stdoutPipe.Writer.CompleteAsync().ConfigureAwait(false);
                await stderrPipe.Writer.CompleteAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                await FaultAsync(exception).ConfigureAwait(false);
            }

        }

        #endregion

        #region (private) liveness loop

        private async Task LivenessAsync()
        {

            var monitor = liveness!;

            try
            {

                while (!cts.IsCancellationRequested)
                {

                    switch (monitor.Poll())
                    {

                        case SshLivenessAction.SendKeepAlive:
                            await SendAsync(BuildKeepAlive()).ConfigureAwait(false);
                            break;

                        case SshLivenessAction.PeerIsDead:
                            await FaultAsync(new SshConnectionLostException(
                                $"The peer failed to answer {monitor.KeepAliveCountMax} keepalive probes.")).ConfigureAwait(false);
                            return;

                        case SshLivenessAction.IdleTimeout:
                            await FaultAsync(new SshConnectionLostException(
                                "The session exceeded its idle timeout.", WasIdleTimeout: true)).ConfigureAwait(false);
                            return;

                        case SshLivenessAction.None:
                        default:
                            break;

                    }

                    var delay = monitor.TimeUntilNextEvent();
                    if (delay == Timeout.InfiniteTimeSpan)
                        return;

                    // A tiny floor keeps the loop from spinning when an event is due "now".
                    if (delay < TimeSpan.FromMilliseconds(10))
                        delay = TimeSpan.FromMilliseconds(10);

                    await Task.Delay(delay, timeProvider, cts.Token).ConfigureAwait(false);

                }

            }
            catch (OperationCanceledException)
            { }

        }

        #endregion


        #region (private) stdin

        // Called by ChannelInputStream. Chunks to the max packet and honours the remote receive window.
        internal async ValueTask WriteStdinAsync(ReadOnlyMemory<Byte> Data, CancellationToken CancellationToken)
        {

            var offset = 0;

            while (offset < Data.Length)
            {

                Task?  wait   = null;
                Int64  avail  = 0;

                lock (windowSync)
                {
                    if (remoteWindow <= 0)
                    {
                        windowGrew ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        wait = windowGrew.Task;
                    }
                    else
                        avail = remoteWindow;
                }

                if (wait is not null)
                {
                    await wait.WaitAsync(CancellationToken).ConfigureAwait(false);
                    continue;
                }

                var chunk = (Int32) Math.Min(Math.Min(MaxPacket, (UInt32) (Data.Length - offset)), (UInt32) Math.Min(avail, MaxPacket));
                if (chunk <= 0)
                    continue;

                await SendAsync(BuildChannelData(remoteChannel, Data.Slice(offset, chunk).Span)).ConfigureAwait(false);

                lock (windowSync)
                    remoteWindow -= chunk;

                offset += chunk;

            }

        }

        internal async ValueTask CompleteStdinAsync(CancellationToken CancellationToken = default)
        {
            if (!eofSent)
            {
                eofSent = true;
                await SendAsync(Simple(SshMessageNumber.ChannelEof, remoteChannel)).ConfigureAwait(false);
            }
        }

        private async Task CopyInputAsync(Stream Input)
        {
            try
            {
                var buffer = new Byte[MaxPacket];
                Int32 read;
                while ((read = await Input.ReadAsync(buffer, cts.Token).ConfigureAwait(false)) > 0)
                    await WriteStdinAsync(buffer.AsMemory(0, read), cts.Token).ConfigureAwait(false);

                await CompleteStdinAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            { }
            catch (Exception exception)
            {
                await FaultAsync(exception).ConfigureAwait(false);
            }
        }

        private void GrantRemoteWindow(UInt32 Increment)
        {
            TaskCompletionSource? grew;
            lock (windowSync)
            {
                remoteWindow += Increment;
                grew          = windowGrew;
                windowGrew    = null;
            }
            grew?.TrySetResult();
        }

        #endregion

        #region (private) helpers

        private async ValueTask SendAsync(ReadOnlyMemory<Byte> Payload)
        {
            await sendGate.WaitAsync(cts.Token).ConfigureAwait(false);
            try     { await transport.SendPacketAsync(Payload, cts.Token).ConfigureAwait(false); }
            finally { sendGate.Release(); }
        }

        private async ValueTask ReplenishAsync(UInt32 Consumed)
        {
            myWindow = Consumed >= myWindow ? 0 : myWindow - Consumed;
            if (myWindow < InitialWindow / 2)
            {
                var increment = InitialWindow - myWindow;
                await SendAsync(BuildWindowAdjust(remoteChannel, increment)).ConfigureAwait(false);
                myWindow += increment;
            }
        }

        private async ValueTask HandlePingAsync(Byte[] Payload)
        {
            var reader = new SshPacketReader(Payload);
            reader.ReadByte();
            var data = reader.ReadBinaryString();

            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.Pong);
            w.WriteBinaryString(data);
            await SendAsync(abw.WrittenSpan.ToArray()).ConfigureAwait(false);
        }

        private async ValueTask DeclineGlobalRequestAsync(Byte[] Payload)
        {
            var reader = new SshPacketReader(Payload);
            reader.ReadByte();
            reader.ReadString();
            var wantReply = reader.ReadBoolean();
            if (wantReply)
            {
                var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
                w.WriteByte((Byte) SshMessageNumber.RequestFailure);
                await SendAsync(abw.WrittenSpan.ToArray()).ConfigureAwait(false);
            }
        }

        private async ValueTask FaultAsync(Exception Exception)
        {
            await cts.CancelAsync().ConfigureAwait(false);
            await stdoutPipe.Writer.CompleteAsync(Exception).ConfigureAwait(false);
            await stderrPipe.Writer.CompleteAsync(Exception).ConfigureAwait(false);
            exitTcs.TrySetException(Exception);

            // Release any stdin writer blocked on the window.
            TaskCompletionSource? grew;
            lock (windowSync) { grew = windowGrew; windowGrew = null; }
            grew?.TrySetException(Exception);
        }

        #endregion

        #region DisposeAsync()

        /// <summary>Close the channel and stop the background pump and liveness loop.</summary>
        public async ValueTask DisposeAsync()
        {

            try
            {
                if (!closeSent)
                {
                    closeSent = true;
                    if (!eofSent)
                        await SendAsync(Simple(SshMessageNumber.ChannelEof, remoteChannel)).ConfigureAwait(false);
                    await SendAsync(Simple(SshMessageNumber.ChannelClose, remoteChannel)).ConfigureAwait(false);
                }
            }
            catch { /* best-effort */ }

            await cts.CancelAsync().ConfigureAwait(false);

            try { await pumpTask.ConfigureAwait(false); }     catch { }
            try { await livenessTask.ConfigureAwait(false); } catch { }

            exitTcs.TrySetResult(exitCode);
            sendGate.Dispose();
            cts.Dispose();

        }

        #endregion


        #region (private, static) message builders

        private static Byte[] BuildChannelOpen(UInt32 Sender, UInt32 Window, UInt32 MaxPacketSize)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.ChannelOpen);
            w.WriteString("session");
            w.WriteUInt32(Sender); w.WriteUInt32(Window); w.WriteUInt32(MaxPacketSize);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildExecRequest(UInt32 Recipient, String Command)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.ChannelRequest);
            w.WriteUInt32(Recipient); w.WriteString("exec"); w.WriteBoolean(true); w.WriteString(Command);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildShellRequest(UInt32 Recipient)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.ChannelRequest);
            w.WriteUInt32(Recipient); w.WriteString("shell"); w.WriteBoolean(true);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildEnvRequest(UInt32 Recipient, String Name, String Value)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.ChannelRequest);
            w.WriteUInt32(Recipient); w.WriteString("env"); w.WriteBoolean(false);
            w.WriteString(Name); w.WriteString(Value);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildPtyRequest(UInt32 Recipient, SshCommand Command)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.ChannelRequest);
            w.WriteUInt32(Recipient); w.WriteString("pty-req"); w.WriteBoolean(true);
            w.WriteString(Command.TerminalType);
            w.WriteUInt32(Command.TerminalColumns); w.WriteUInt32(Command.TerminalRows);
            w.WriteUInt32(0); w.WriteUInt32(0);                        // pixel width / height
            w.WriteBinaryString(new Byte[] { 0 });                    // encoded terminal modes: TTY_OP_END only
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildChannelData(UInt32 Recipient, ReadOnlySpan<Byte> Data)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.ChannelData);
            w.WriteUInt32(Recipient); w.WriteBinaryString(Data);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildWindowAdjust(UInt32 Recipient, UInt32 Increment)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.ChannelWindowAdjust);
            w.WriteUInt32(Recipient); w.WriteUInt32(Increment);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildKeepAlive()
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.GlobalRequest);
            w.WriteString("keepalive@openssh.com"); w.WriteBoolean(true);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] Simple(SshMessageNumber Message, UInt32 Recipient)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) Message); w.WriteUInt32(Recipient);
            return abw.WrittenSpan.ToArray();
        }

        #endregion

        #region (private, static) message parsers

        private static async ValueTask<(UInt32 Channel, UInt32 Window)> AwaitOpenConfirmationAsync(SshTransport Transport, CancellationToken CancellationToken)
        {
            while (true)
            {
                var payload = await Transport.ReceivePacketAsync(CancellationToken).ConfigureAwait(false);
                var message = (SshMessageNumber) payload[0];

                if (message == SshMessageNumber.ChannelOpenConfirmation)
                {
                    var reader = new SshPacketReader(payload);
                    reader.ReadByte();
                    reader.ReadUInt32();                          // our channel
                    var sender = reader.ReadUInt32();             // peer's channel
                    var window = reader.ReadUInt32();             // peer's initial window
                    return (sender, window);
                }

                if (message == SshMessageNumber.ChannelOpenFailure)
                    throw new SshWireException("The peer refused to open the session channel.");
            }
        }

        private static Byte[] ParseChannelData(ReadOnlySpan<Byte> Payload)
        {
            var reader = new SshPacketReader(Payload);
            reader.ReadByte(); reader.ReadUInt32();
            return reader.ReadBinaryString();
        }

        private static (UInt32 Code, Byte[] Data) ParseChannelExtendedData(ReadOnlySpan<Byte> Payload)
        {
            var reader = new SshPacketReader(Payload);
            reader.ReadByte(); reader.ReadUInt32();
            var code = reader.ReadUInt32();
            return (code, reader.ReadBinaryString());
        }

        private static UInt32 ParseWindowAdjust(ReadOnlySpan<Byte> Payload)
        {
            var reader = new SshPacketReader(Payload);
            reader.ReadByte(); reader.ReadUInt32();
            return reader.ReadUInt32();
        }

        private static (String Type, UInt32 ExitStatus) ParseChannelRequest(ReadOnlySpan<Byte> Payload)
        {
            var reader = new SshPacketReader(Payload);
            reader.ReadByte();
            reader.ReadUInt32();
            var type = reader.ReadString();
            reader.ReadBoolean();                       // want_reply
            UInt32 exit = 0;
            if (type == "exit-status")
                exit = reader.ReadUInt32();
            return (type, exit);
        }

        #endregion


        #region (private) ChannelInputStream

        /// <summary>A write-only stream that forwards writes to the remote command's standard input.</summary>
        private sealed class ChannelInputStream : Stream
        {

            private readonly SshCommandProcess process;

            public ChannelInputStream(SshCommandProcess Process)
            {
                this.process = Process;
            }

            public override Boolean CanRead   => false;
            public override Boolean CanSeek   => false;
            public override Boolean CanWrite  => true;
            public override Int64   Length            => throw new NotSupportedException();
            public override Int64   Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override void Flush() { }
            public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

            public override async ValueTask WriteAsync(ReadOnlyMemory<Byte> buffer, CancellationToken cancellationToken = default)
                => await process.WriteStdinAsync(buffer, cancellationToken).ConfigureAwait(false);

            public override void Write(Byte[] buffer, Int32 offset, Int32 count)
                => process.WriteStdinAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

            public override Task WriteAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken)
                => process.WriteStdinAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

            public override async ValueTask DisposeAsync()
                => await process.CompleteStdinAsync().ConfigureAwait(false);

            protected override void Dispose(Boolean disposing)
            {
                if (disposing)
                    process.CompleteStdinAsync().AsTask().GetAwaiter().GetResult();
            }

            public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count) => throw new NotSupportedException();
            public override Int64 Seek(Int64 offset, SeekOrigin origin)          => throw new NotSupportedException();
            public override void  SetLength(Int64 value)                         => throw new NotSupportedException();

        }

        #endregion

    }

}
