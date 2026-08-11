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
    /// The SSH connection protocol (RFC 4254) for interactive command execution: opening a
    /// <c>session</c> channel, the <c>exec</c> request, channel data with window-based flow control, and
    /// <c>exit-status</c>. This covers the "log in, run a command, capture the output, log out" use case
    /// on both roles; a full multi-channel connection object grows from here.
    /// </summary>
    public static class SshConnection
    {

        #region Constants

        private const UInt32 InitialWindow  = 2 * 1024 * 1024;   // 2 MiB
        private const UInt32 MaxPacket      = 32 * 1024;         // 32 KiB
        private const String SessionType    = "session";

        #endregion


        #region ExecuteAsync(Transport, Command, CancellationToken)

        /// <summary>
        /// Run a single command on the peer over a fresh session channel, capturing stdout, stderr and the
        /// exit status (the client side of remote command execution).
        /// </summary>
        public static async ValueTask<SshCommandResult> ExecuteAsync(SshTransport       Transport,
                                                                     String             Command,
                                                                     CancellationToken  CancellationToken = default)
        {

            const UInt32 localChannel = 0;

            await Transport.SendPacketAsync(BuildChannelOpen(localChannel, InitialWindow, MaxPacket), CancellationToken).ConfigureAwait(false);

            // Wait for the channel to be confirmed (or refused).
            var remoteChannel = await AwaitOpenConfirmationAsync(Transport, CancellationToken).ConfigureAwait(false);

            await Transport.SendPacketAsync(BuildExecRequest(remoteChannel, Command), CancellationToken).ConfigureAwait(false);

            var stdout      = new ArrayBufferWriter<Byte>();
            var stderr      = new ArrayBufferWriter<Byte>();
            var exitCode    = -1;
            var myWindow    = InitialWindow;
            var closeSent   = false;

            while (true)
            {

                var payload = await Transport.ReceivePacketAsync(CancellationToken).ConfigureAwait(false);
                var message = (SshMessageNumber) payload[0];

                switch (message)
                {

                    case SshMessageNumber.ChannelSuccess:
                        break;   // the exec request was accepted

                    case SshMessageNumber.ChannelFailure:
                        throw new SshWireException("The peer rejected the exec request.");

                    case SshMessageNumber.ChannelData:
                    {
                        var data = ParseChannelData(payload);
                        stdout.Write(data);
                        myWindow = await ReplenishAsync(Transport, remoteChannel, myWindow, (UInt32) data.Length, CancellationToken).ConfigureAwait(false);
                        break;
                    }

                    case SshMessageNumber.ChannelExtendedData:
                    {
                        var (code, data) = ParseChannelExtendedData(payload);
                        if (code == 1)   // SSH_EXTENDED_DATA_STDERR
                            stderr.Write(data);
                        myWindow = await ReplenishAsync(Transport, remoteChannel, myWindow, (UInt32) data.Length, CancellationToken).ConfigureAwait(false);
                        break;
                    }

                    case SshMessageNumber.ChannelRequest:
                    {
                        var request = ParseChannelRequest(payload);
                        if (request.RequestType == "exit-status")
                            exitCode = (Int32) request.ExitStatus;
                        else if (request.RequestType == "exit-signal")
                            exitCode = 255;
                        break;
                    }

                    case SshMessageNumber.ChannelWindowAdjust:
                    case SshMessageNumber.ChannelEof:
                        break;

                    case SshMessageNumber.ChannelClose:
                        if (!closeSent)
                            await Transport.SendPacketAsync(BuildChannelClose(remoteChannel), CancellationToken).ConfigureAwait(false);
                        return new SshCommandResult(exitCode, stdout.WrittenSpan.ToArray(), stderr.WrittenSpan.ToArray());

                    default:
                        await HandleTransportTrafficAsync(Transport, payload, CancellationToken).ConfigureAwait(false);
                        break;

                }

            }

        }

        #endregion

        #region StartCommandAsync(Transport, Command, Options, CancellationToken)

        /// <summary>
        /// Start a command on the peer over a fresh session channel and return a live
        /// <see cref="SshCommandProcess"/> for streaming interaction — incremental stdout/stderr, piped
        /// stdin, environment variables, an optional PTY, and (via <paramref name="Options"/>) keepalive and
        /// idle-timeout enforcement. The streaming counterpart to <see cref="ExecuteAsync"/>.
        /// </summary>
        public static ValueTask<SshCommandProcess> StartCommandAsync(SshTransport           Transport,
                                                                     SshCommand             Command,
                                                                     SshConnectionOptions?  Options            = null,
                                                                     CancellationToken      CancellationToken  = default)
            => SshCommandProcess.StartAsync(Transport, Command, Options, CancellationToken);

        #endregion

        #region ServeCommandAsync(Transport, Username, Handler, CancellationToken)

        /// <summary>
        /// Serve one streaming session: accept a <c>session</c> channel, gather <c>env</c>/<c>pty-req</c>,
        /// dispatch an <c>exec</c> or <c>shell</c> request to <paramref name="Handler"/> — which now also
        /// reads piped standard input via <see cref="SshExecContext.StandardInput"/> — pump its output while
        /// concurrently feeding it inbound stdin, then report the exit status. Unlike
        /// <see cref="ServeExecAsync"/>, the handler runs concurrently with the receive loop so it can
        /// consume stdin as it streams in. An optional <paramref name="Recorder"/> tees the channel output
        /// into an asciicast v2 recording and captures the command and its exit status (the recorder's
        /// lifetime — begin/complete — is owned by the caller).
        /// </summary>
        public static async ValueTask ServeCommandAsync(SshTransport       Transport,
                                                        String             Username,
                                                        SshExecHandler     Handler,
                                                        SessionRecorder?   Recorder           = null,
                                                        CancellationToken  CancellationToken   = default)
        {

            const UInt32 localChannel = 0;

            var  sendGate       = new SemaphoreSlim(1, 1);
            var  myWindow       = InitialWindow;
            long remoteWindow   = 0;
            var  hasPty         = false;
            var  closeSent      = false;

            UInt32 remoteChannel = 0;

            Pipe?                   stdinPipe   = null;
            Task<Int32>?            handlerTask = null;
            Task<Byte[]>?           receiveTask = null;
            var                     exitSent    = false;

            async ValueTask Send(Byte[] Payload)
            {
                await sendGate.WaitAsync(CancellationToken).ConfigureAwait(false);
                try     { await Transport.SendPacketAsync(Payload, CancellationToken).ConfigureAwait(false); }
                finally { sendGate.Release(); }
            }

            try
            {

                while (true)
                {

                    // Once the handler is running we race its completion against the next inbound packet.
                    receiveTask ??= Transport.ReceivePacketAsync(CancellationToken).AsTask();

                    if (handlerTask is not null && !exitSent)
                        await Task.WhenAny(receiveTask, handlerTask).ConfigureAwait(false);
                    else
                        await Task.WhenAny(receiveTask).ConfigureAwait(false);

                    // The handler finished → send exit-status, EOF and CLOSE (once).
                    if (handlerTask is not null && !exitSent && handlerTask.IsCompleted)
                    {
                        var exitCode = await handlerTask.ConfigureAwait(false);
                        if (Recorder is not null)
                            await Recorder.RecordExitAsync(exitCode, CancellationToken).ConfigureAwait(false);
                        await Send(BuildExitStatus(remoteChannel, (UInt32) exitCode)).ConfigureAwait(false);
                        await Send(BuildChannelEof(remoteChannel)).ConfigureAwait(false);
                        await Send(BuildChannelClose(remoteChannel)).ConfigureAwait(false);
                        closeSent = true;
                        exitSent  = true;
                    }

                    if (!receiveTask.IsCompleted)
                        continue;

                    var payload = await receiveTask.ConfigureAwait(false);
                    receiveTask = null;

                    var message = (SshMessageNumber) payload[0];

                    switch (message)
                    {

                        case SshMessageNumber.ChannelOpen:
                        {
                            var open = ParseChannelOpen(payload);
                            if (open.ChannelType != SessionType)
                            {
                                await Send(BuildChannelOpenFailure(open.SenderChannel)).ConfigureAwait(false);
                                break;
                            }
                            remoteChannel = open.SenderChannel;
                            remoteWindow  = open.InitialWindow;
                            await Send(BuildChannelOpenConfirmation(remoteChannel, localChannel, InitialWindow, MaxPacket)).ConfigureAwait(false);
                            break;
                        }

                        case SshMessageNumber.ChannelWindowAdjust:
                            remoteWindow += ParseWindowAdjust(payload);
                            break;

                        case SshMessageNumber.ChannelData when handlerTask is not null:
                        {
                            var data = ParseChannelData(payload);
                            await stdinPipe!.Writer.WriteAsync(data, CancellationToken).ConfigureAwait(false);
                            myWindow = await ReplenishSendingAsync(Send, remoteChannel, myWindow, (UInt32) data.Length).ConfigureAwait(false);
                            break;
                        }

                        case SshMessageNumber.ChannelEof:
                            if (stdinPipe is not null)
                                await stdinPipe.Writer.CompleteAsync().ConfigureAwait(false);
                            break;

                        case SshMessageNumber.ChannelRequest:
                        {

                            var request = ParseChannelRequest(payload);

                            if (request.RequestType is "exec" or "shell" && handlerTask is null)
                            {

                                if (request.WantReply)
                                    await Send(BuildChannelSuccess(remoteChannel)).ConfigureAwait(false);

                                if (Recorder is not null)
                                    await Recorder.StartAsync(Command: request.RequestType == "exec" ? request.Command : null,
                                                              CancellationToken: CancellationToken).ConfigureAwait(false);

                                stdinPipe = new Pipe();

                                var channel   = remoteChannel;
                                var recorder  = Recorder;
                                async ValueTask Write(ReadOnlyMemory<Byte> d, Boolean isStderr, CancellationToken ct)
                                {
                                    if (recorder is not null)
                                        await recorder.RecordOutputAsync(d, ct).ConfigureAwait(false);
                                    await WriteChannelDataGatedAsync(Send, channel, d, isStderr, () => remoteWindow, n => remoteWindow -= n).ConfigureAwait(false);
                                }

                                var context = new SshExecContext(request.Command, Username, Write, stdinPipe.Reader.AsStream(), hasPty);
                                handlerTask = Handler(context, CancellationToken).AsTask();

                            }
                            else if (request.RequestType == "pty-req")
                            {
                                hasPty = true;
                                if (request.WantReply)
                                    await Send(BuildChannelSuccess(remoteChannel)).ConfigureAwait(false);
                            }
                            else if (request.WantReply)
                            {
                                var reply = request.RequestType is "env" or "window-change"
                                                ? BuildChannelSuccess(remoteChannel)
                                                : BuildChannelFailure(remoteChannel);
                                await Send(reply).ConfigureAwait(false);
                            }

                            break;

                        }

                        case SshMessageNumber.ChannelClose:
                            if (stdinPipe is not null)
                                await stdinPipe.Writer.CompleteAsync().ConfigureAwait(false);
                            if (!closeSent)
                                await Send(BuildChannelClose(remoteChannel)).ConfigureAwait(false);
                            return;

                        case SshMessageNumber.Ping:
                        {
                            var reader = new SshPacketReader(payload);
                            reader.ReadByte();
                            var data = reader.ReadBinaryString();
                            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
                            w.WriteByte((Byte) SshMessageNumber.Pong); w.WriteBinaryString(data);
                            await Send(abw.WrittenSpan.ToArray()).ConfigureAwait(false);
                            break;
                        }

                        case SshMessageNumber.GlobalRequest:
                        {
                            var reader = new SshPacketReader(payload);
                            reader.ReadByte(); reader.ReadString();
                            if (reader.ReadBoolean())
                            {
                                var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
                                w.WriteByte((Byte) SshMessageNumber.RequestFailure);
                                await Send(abw.WrittenSpan.ToArray()).ConfigureAwait(false);
                            }
                            break;
                        }

                        default:
                            break;

                    }

                }

            }
            finally
            {
                if (handlerTask is not null)
                    try { await handlerTask.ConfigureAwait(false); } catch { }
                sendGate.Dispose();
            }

        }

        #endregion

        #region OpenSubsystemAsync(Transport, Subsystem, CancellationToken)

        /// <summary>
        /// Open a session channel and start a subsystem (e.g. <c>sftp</c>), returning a duplex byte
        /// channel for the subsystem protocol (the client side).
        /// </summary>
        public static async ValueTask<SshChannelDuplex> OpenSubsystemAsync(SshTransport       Transport,
                                                                           String             Subsystem,
                                                                           CancellationToken  CancellationToken = default)
        {

            const UInt32 localChannel = 0;

            await Transport.SendPacketAsync(BuildChannelOpen(localChannel, InitialWindow, MaxPacket), CancellationToken).ConfigureAwait(false);
            var (remoteChannel, remoteWindow) = await AwaitOpenConfirmationFullAsync(Transport, CancellationToken).ConfigureAwait(false);

            await Transport.SendPacketAsync(BuildSubsystemRequest(remoteChannel, Subsystem), CancellationToken).ConfigureAwait(false);

            while (true)
            {
                var payload = await Transport.ReceivePacketAsync(CancellationToken).ConfigureAwait(false);
                var message = (SshMessageNumber) payload[0];
                if (message == SshMessageNumber.ChannelSuccess)
                    return new SshChannelDuplex(Transport, remoteChannel, remoteWindow);
                if (message == SshMessageNumber.ChannelFailure)
                    throw new SshWireException($"The peer refused the '{Subsystem}' subsystem.");
                await HandleTransportTrafficAsync(Transport, payload, CancellationToken).ConfigureAwait(false);
            }

        }

        #endregion

        #region AcceptSubsystemAsync(Transport, Subsystem, CancellationToken)

        /// <summary>
        /// Accept a session channel and a request for the named subsystem, returning a duplex byte channel
        /// for the subsystem protocol (the server side).
        /// </summary>
        public static async ValueTask<SshChannelDuplex> AcceptSubsystemAsync(SshTransport       Transport,
                                                                             String             Subsystem,
                                                                             CancellationToken  CancellationToken = default)
        {

            const UInt32 localChannel = 0;
            UInt32 remoteChannel = 0;
            UInt32 remoteWindow  = 0;

            while (true)
            {

                var payload = await Transport.ReceivePacketAsync(CancellationToken).ConfigureAwait(false);
                var message = (SshMessageNumber) payload[0];

                switch (message)
                {

                    case SshMessageNumber.ChannelOpen:
                    {
                        var open = ParseChannelOpen(payload);
                        if (open.ChannelType != SessionType)
                        {
                            await Transport.SendPacketAsync(BuildChannelOpenFailure(open.SenderChannel), CancellationToken).ConfigureAwait(false);
                            break;
                        }
                        remoteChannel = open.SenderChannel;
                        remoteWindow  = open.InitialWindow;
                        await Transport.SendPacketAsync(BuildChannelOpenConfirmation(remoteChannel, localChannel, InitialWindow, MaxPacket), CancellationToken).ConfigureAwait(false);
                        break;
                    }

                    case SshMessageNumber.ChannelRequest:
                    {
                        var request = ParseChannelRequest(payload);
                        if (request.RequestType == "subsystem" && request.Command == Subsystem)
                        {
                            if (request.WantReply)
                                await Transport.SendPacketAsync(BuildChannelSuccess(remoteChannel), CancellationToken).ConfigureAwait(false);
                            return new SshChannelDuplex(Transport, remoteChannel, remoteWindow);
                        }
                        if (request.WantReply)
                            await Transport.SendPacketAsync(BuildChannelFailure(remoteChannel), CancellationToken).ConfigureAwait(false);
                        break;
                    }

                    default:
                        await HandleTransportTrafficAsync(Transport, payload, CancellationToken).ConfigureAwait(false);
                        break;

                }

            }

        }

        #endregion

        #region ServeExecAsync(Transport, Username, Handler, CancellationToken)

        /// <summary>
        /// Serve one interactive session: accept a <c>session</c> channel, dispatch an <c>exec</c> or
        /// <c>shell</c> request to <paramref name="Handler"/>, stream its output, and report the exit status.
        /// </summary>
        public static async ValueTask ServeExecAsync(SshTransport       Transport,
                                                     String             Username,
                                                     SshExecHandler     Handler,
                                                     CancellationToken  CancellationToken = default)
        {

            const UInt32 localChannel = 0;

            UInt32  remoteChannel  = 0;
            var     remoteWindow   = 0L;
            var     closeSent      = false;
            var     handlerDone    = false;

            while (true)
            {

                var payload = await Transport.ReceivePacketAsync(CancellationToken).ConfigureAwait(false);
                var message = (SshMessageNumber) payload[0];

                switch (message)
                {

                    case SshMessageNumber.ChannelOpen:
                    {
                        var open = ParseChannelOpen(payload);
                        if (open.ChannelType != SessionType)
                        {
                            await Transport.SendPacketAsync(BuildChannelOpenFailure(open.SenderChannel), CancellationToken).ConfigureAwait(false);
                            break;
                        }
                        remoteChannel  = open.SenderChannel;
                        remoteWindow   = open.InitialWindow;
                        await Transport.SendPacketAsync(BuildChannelOpenConfirmation(remoteChannel, localChannel, InitialWindow, MaxPacket), CancellationToken).ConfigureAwait(false);
                        break;
                    }

                    case SshMessageNumber.ChannelWindowAdjust:
                    {
                        remoteWindow += ParseWindowAdjust(payload);
                        break;
                    }

                    case SshMessageNumber.ChannelRequest:
                    {

                        var request = ParseChannelRequest(payload);

                        if (request.RequestType is "exec" or "shell")
                        {

                            if (request.WantReply)
                                await Transport.SendPacketAsync(BuildChannelSuccess(remoteChannel), CancellationToken).ConfigureAwait(false);

                            // Write callback used by the handler; chunks to the max packet and tracks the window.
                            var channel = remoteChannel;
                            ValueTask Write(ReadOnlyMemory<Byte> data, Boolean isStderr, CancellationToken ct)
                                => WriteChannelDataAsync(Transport, channel, data, isStderr, () => remoteWindow, n => remoteWindow -= n, ct);

                            var context   = new SshExecContext(request.Command, Username, Write);
                            var exitCode  = await Handler(context, CancellationToken).ConfigureAwait(false);

                            await Transport.SendPacketAsync(BuildExitStatus(remoteChannel, (UInt32) exitCode), CancellationToken).ConfigureAwait(false);
                            await Transport.SendPacketAsync(BuildChannelEof(remoteChannel), CancellationToken).ConfigureAwait(false);
                            await Transport.SendPacketAsync(BuildChannelClose(remoteChannel), CancellationToken).ConfigureAwait(false);
                            closeSent    = true;
                            handlerDone  = true;

                        }
                        else if (request.WantReply)
                        {
                            // pty-req, env, … — accept silently so the client proceeds to exec/shell.
                            var reply = request.RequestType is "pty-req" or "env" or "shell" or "window-change"
                                            ? BuildChannelSuccess(remoteChannel)
                                            : BuildChannelFailure(remoteChannel);
                            await Transport.SendPacketAsync(reply, CancellationToken).ConfigureAwait(false);
                        }

                        break;

                    }

                    case SshMessageNumber.ChannelEof:
                        break;

                    case SshMessageNumber.ChannelClose:
                        if (!closeSent)
                            await Transport.SendPacketAsync(BuildChannelClose(remoteChannel), CancellationToken).ConfigureAwait(false);
                        return;

                    default:
                        await HandleTransportTrafficAsync(Transport, payload, CancellationToken).ConfigureAwait(false);
                        break;

                }

                _ = handlerDone;   // (reserved for future multi-request sessions)

            }

        }

        #endregion


        #region (private) flow control

        // Replenish our receive window using a gated send delegate (for the concurrent streaming server).
        private static async ValueTask<UInt32> ReplenishSendingAsync(Func<Byte[], ValueTask> Send, UInt32 RemoteChannel, UInt32 Window, UInt32 Consumed)
        {

            Window = Consumed >= Window ? 0 : Window - Consumed;

            if (Window < InitialWindow / 2)
            {
                var increment = InitialWindow - Window;
                await Send(BuildWindowAdjust(RemoteChannel, increment)).ConfigureAwait(false);
                Window += increment;
            }

            return Window;

        }

        // Write channel data through a gated send delegate, chunking to the max packet and tracking the window.
        private static async ValueTask WriteChannelDataGatedAsync(Func<Byte[], ValueTask> Send, UInt32 Channel, ReadOnlyMemory<Byte> Data, Boolean IsStderr, Func<Int64> Window, Action<Int64> Consume)
        {

            var offset = 0;
            while (offset < Data.Length)
            {
                var chunk = Math.Min((Int32) MaxPacket, Data.Length - offset);
                var slice = Data.Slice(offset, chunk);

                await Send(IsStderr
                               ? BuildChannelExtendedData(Channel, 1, slice.Span)
                               : BuildChannelData(Channel, slice.Span)).ConfigureAwait(false);

                Consume(chunk);
                offset += chunk;
            }

        }

        private static async ValueTask<UInt32> ReplenishAsync(SshTransport Transport, UInt32 RemoteChannel, UInt32 Window, UInt32 Consumed, CancellationToken CancellationToken)
        {

            Window = Consumed >= Window ? 0 : Window - Consumed;

            if (Window < InitialWindow / 2)
            {
                var increment = InitialWindow - Window;
                await Transport.SendPacketAsync(BuildWindowAdjust(RemoteChannel, increment), CancellationToken).ConfigureAwait(false);
                Window += increment;
            }

            return Window;

        }

        private static async ValueTask WriteChannelDataAsync(SshTransport Transport, UInt32 Channel, ReadOnlyMemory<Byte> Data, Boolean IsStderr, Func<Int64> Window, Action<Int64> Consume, CancellationToken CancellationToken)
        {

            var offset = 0;
            while (offset < Data.Length)
            {
                var chunk = Math.Min((Int32) MaxPacket, Data.Length - offset);
                var slice = Data.Slice(offset, chunk);

                await Transport.SendPacketAsync(IsStderr
                                                    ? BuildChannelExtendedData(Channel, 1, slice.Span)
                                                    : BuildChannelData(Channel, slice.Span),
                                                CancellationToken).ConfigureAwait(false);

                Consume(chunk);
                offset += chunk;
            }

        }

        #endregion

        #region (private) transport-level traffic (PING/PONG, global requests)

        // Handle traffic that can arrive at any time: reply to ping@openssh.com PINGs and decline
        // global requests that want a reply (keepalive@openssh.com, hostkeys-00@openssh.com, …).
        private static async ValueTask HandleTransportTrafficAsync(SshTransport Transport, Byte[] Payload, CancellationToken CancellationToken)
        {

            var message = (SshMessageNumber) Payload[0];

            if (message == SshMessageNumber.Ping)
            {
                // SSH_MSG_PING(192): string data → reply SSH_MSG_PONG(193) echoing the data.
                var reader = new SshPacketReader(Payload);
                reader.ReadByte();
                var data = reader.ReadBinaryString();

                var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
                w.WriteByte((Byte) SshMessageNumber.Pong);
                w.WriteBinaryString(data);
                await Transport.SendPacketAsync(abw.WrittenSpan.ToArray(), CancellationToken).ConfigureAwait(false);
            }

            else if (message == SshMessageNumber.GlobalRequest)
            {
                var reader = new SshPacketReader(Payload);
                reader.ReadByte();
                reader.ReadString();                    // request name
                var wantReply = reader.ReadBoolean();

                if (wantReply)
                {
                    var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
                    w.WriteByte((Byte) SshMessageNumber.RequestFailure);
                    await Transport.SendPacketAsync(abw.WrittenSpan.ToArray(), CancellationToken).ConfigureAwait(false);
                }
            }

        }

        #endregion

        #region (private) message builders

        private static Byte[] BuildChannelOpen(UInt32 Sender, UInt32 Window, UInt32 MaxPacketSize)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.ChannelOpen);
            w.WriteString(SessionType);
            w.WriteUInt32(Sender); w.WriteUInt32(Window); w.WriteUInt32(MaxPacketSize);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildChannelOpenConfirmation(UInt32 Recipient, UInt32 Sender, UInt32 Window, UInt32 MaxPacketSize)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.ChannelOpenConfirmation);
            w.WriteUInt32(Recipient); w.WriteUInt32(Sender); w.WriteUInt32(Window); w.WriteUInt32(MaxPacketSize);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildChannelOpenFailure(UInt32 Recipient)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.ChannelOpenFailure);
            w.WriteUInt32(Recipient); w.WriteUInt32(3); w.WriteString("unknown channel type"); w.WriteString("");
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildSubsystemRequest(UInt32 Recipient, String Subsystem)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.ChannelRequest);
            w.WriteUInt32(Recipient); w.WriteString("subsystem"); w.WriteBoolean(true); w.WriteString(Subsystem);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildExecRequest(UInt32 Recipient, String Command)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.ChannelRequest);
            w.WriteUInt32(Recipient); w.WriteString("exec"); w.WriteBoolean(true); w.WriteString(Command);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildExitStatus(UInt32 Recipient, UInt32 Code)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.ChannelRequest);
            w.WriteUInt32(Recipient); w.WriteString("exit-status"); w.WriteBoolean(false); w.WriteUInt32(Code);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildChannelData(UInt32 Recipient, ReadOnlySpan<Byte> Data)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.ChannelData);
            w.WriteUInt32(Recipient); w.WriteBinaryString(Data);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildChannelExtendedData(UInt32 Recipient, UInt32 Code, ReadOnlySpan<Byte> Data)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.ChannelExtendedData);
            w.WriteUInt32(Recipient); w.WriteUInt32(Code); w.WriteBinaryString(Data);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildWindowAdjust(UInt32 Recipient, UInt32 Increment)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.ChannelWindowAdjust);
            w.WriteUInt32(Recipient); w.WriteUInt32(Increment);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildChannelEof(UInt32 Recipient)   => Simple(SshMessageNumber.ChannelEof,     Recipient);
        private static Byte[] BuildChannelClose(UInt32 Recipient) => Simple(SshMessageNumber.ChannelClose,   Recipient);
        private static Byte[] BuildChannelSuccess(UInt32 Recipient) => Simple(SshMessageNumber.ChannelSuccess, Recipient);
        private static Byte[] BuildChannelFailure(UInt32 Recipient) => Simple(SshMessageNumber.ChannelFailure, Recipient);

        private static Byte[] Simple(SshMessageNumber Message, UInt32 Recipient)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) Message); w.WriteUInt32(Recipient);
            return abw.WrittenSpan.ToArray();
        }

        #endregion

        #region (private) message parsers

        private static async ValueTask<UInt32> AwaitOpenConfirmationAsync(SshTransport Transport, CancellationToken CancellationToken)
        {
            while (true)
            {
                var payload = await Transport.ReceivePacketAsync(CancellationToken).ConfigureAwait(false);
                var message = (SshMessageNumber) payload[0];

                if (message == SshMessageNumber.ChannelOpenConfirmation)
                {
                    var reader = new SshPacketReader(payload);
                    reader.ReadByte();
                    reader.ReadUInt32();                        // our channel (recipient)
                    return reader.ReadUInt32();                 // the peer's channel (sender)
                }

                if (message == SshMessageNumber.ChannelOpenFailure)
                    throw new SshWireException("The peer refused to open the session channel.");
            }
        }

        private static async ValueTask<(UInt32 Channel, UInt32 Window)> AwaitOpenConfirmationFullAsync(SshTransport Transport, CancellationToken CancellationToken)
        {
            while (true)
            {
                var payload = await Transport.ReceivePacketAsync(CancellationToken).ConfigureAwait(false);
                var message = (SshMessageNumber) payload[0];

                if (message == SshMessageNumber.ChannelOpenConfirmation)
                {
                    var reader = new SshPacketReader(payload);
                    reader.ReadByte();
                    reader.ReadUInt32();                          // our channel (recipient)
                    var sender = reader.ReadUInt32();             // the peer's channel
                    var window = reader.ReadUInt32();             // the peer's initial window
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

        private readonly record struct ChannelOpenInfo(String ChannelType, UInt32 SenderChannel, UInt32 InitialWindow, UInt32 MaxPacketSize);

        private static ChannelOpenInfo ParseChannelOpen(ReadOnlySpan<Byte> Payload)
        {
            var reader = new SshPacketReader(Payload);
            reader.ReadByte();
            var type    = reader.ReadString();
            var sender  = reader.ReadUInt32();
            var window  = reader.ReadUInt32();
            var maxPkt  = reader.ReadUInt32();
            return new ChannelOpenInfo(type, sender, window, maxPkt);
        }

        private readonly record struct ChannelRequestInfo(String RequestType, Boolean WantReply, String Command, UInt32 ExitStatus);

        private static ChannelRequestInfo ParseChannelRequest(ReadOnlySpan<Byte> Payload)
        {

            var reader = new SshPacketReader(Payload);
            reader.ReadByte();
            reader.ReadUInt32();                       // recipient channel
            var type       = reader.ReadString();
            var wantReply  = reader.ReadBoolean();

            var command    = "";
            UInt32 exit    = 0;

            if (type is "exec" or "subsystem")
                command = reader.ReadString();
            else if (type == "exit-status")
                exit = reader.ReadUInt32();

            return new ChannelRequestInfo(type, wantReply, command, exit);

        }

        #endregion

    }

}
