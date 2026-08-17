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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// Session-channel semantics (<c>exec</c> / <c>shell</c> with <c>pty-req</c>/<c>env</c>/<c>exit-status</c>)
    /// implemented over a multiplexed <see cref="SshMuxChannel"/>, so the client and server façades share one
    /// exec implementation that runs concurrently with any other channels.
    /// </summary>
    public static class SshSessionChannel
    {

        #region ExecuteAsync(Mux, Command, CancellationToken) — client

        /// <summary>
        /// Open a session channel, run <paramref name="Command"/> and capture stdout/stderr + exit status.
        /// </summary>
        public static async ValueTask<SshCommandResult> ExecuteAsync(SshChannelMultiplexer Mux, String Command, CancellationToken CancellationToken = default)
        {
            var channel = await Mux.OpenChannelAsync("session", CancellationToken: CancellationToken).ConfigureAwait(false);
            await channel.SendRequestAsync("exec", true, EncodeString(Command), CancellationToken).ConfigureAwait(false);
            return await CaptureAsync(channel, CancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Capture a session channel's stdout/stderr and its exit status.
        /// </summary>
        public static async ValueTask<SshCommandResult> CaptureAsync(SshMuxChannel Channel, CancellationToken CancellationToken = default)
        {

            var outTask = CopyToBytesAsync(Channel.Input, CancellationToken);
            var errTask = CopyToBytesAsync(Channel.Error, CancellationToken);

            var exit = -1;
            SshChannelRequest? request;
            while ((request = await Channel.ReadRequestAsync(CancellationToken).ConfigureAwait(false)) is not null)
            {
                if      (request.Value.Type == "exit-status") exit = (Int32) ReadUInt32(request.Value.Data);
                else if (request.Value.Type == "exit-signal") exit = 255;
            }

            return new SshCommandResult(exit, await outTask.ConfigureAwait(false), await errTask.ConfigureAwait(false));

        }

        #endregion

        #region ServeAsync(Channel, Username, Handler, CancellationToken) — server

        /// <summary>
        /// Serve a session channel: accept <c>pty-req</c>/<c>env</c>, dispatch <c>exec</c>/<c>shell</c> to
        /// <paramref name="Handler"/>, dispatch a <c>subsystem</c> request to a matching handler in
        /// <paramref name="Subsystems"/> (e.g. <c>sftp</c>), stream output and report the exit status.
        /// </summary>
        public static async ValueTask ServeAsync(SshMuxChannel                                                              Channel,
                                                 String                                                                     Username,
                                                 SshExecHandler?                                                            Handler,
                                                 IReadOnlyDictionary<String, Func<SshMuxChannel, CancellationToken, ValueTask>>?  Subsystems = null,
                                                 CancellationToken                                                          CancellationToken = default,
                                                 SshSessionRestrictions?                                                    Restrictions = null)
        {

            var hasPty = false;

            SshChannelRequest? request;
            while ((request = await Channel.ReadRequestAsync(CancellationToken).ConfigureAwait(false)) is not null)
            {

                var type = request.Value.Type;

                if (type == "subsystem")
                {
                    var name = ReadString(request.Value.Data);
                    if (Subsystems is not null && Subsystems.TryGetValue(name, out var serve))
                    {
                        if (request.Value.WantReply) await Channel.ReplyAsync(true, CancellationToken).ConfigureAwait(false);
                        await serve(Channel, CancellationToken).ConfigureAwait(false);
                        return;
                    }
                    if (request.Value.WantReply) await Channel.ReplyAsync(false, CancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (type is "exec" or "shell" && Handler is not null)
                {

                    var requested = type == "exec" ? ReadString(request.Value.Data) : "";

                    // A force-command certificate confines the session to the CA's command, whatever the
                    // client asked for — including a bare shell request. What it asked for is kept for
                    // the handler's information only (OpenSSH's SSH_ORIGINAL_COMMAND).
                    var forced    = Restrictions?.ForcedCommand;
                    var command   = forced ?? requested;

                    if (request.Value.WantReply)
                        await Channel.ReplyAsync(true, CancellationToken).ConfigureAwait(false);

                    var channel = Channel;
                    var context = new SshExecContext(command, Username,
                                                     (data, isErr, ct) => isErr ? channel.SendErrorAsync(data, ct) : channel.SendDataAsync(data, ct),
                                                     hasPty: hasPty,
                                                     originalCommand: forced is null ? null : requested);

                    var exit = await Handler(context, CancellationToken).ConfigureAwait(false);

                    await Channel.SendRequestAsync("exit-status", false, EncodeUInt32((UInt32) exit), CancellationToken).ConfigureAwait(false);
                    await Channel.CloseAsync(CancellationToken).ConfigureAwait(false);
                    return;
                }

                if (type == "pty-req")
                {

                    // no-pty / restrict: refuse the terminal rather than granting it and pretending.
                    var allowed = Restrictions?.AllowPty ?? true;
                    if (allowed)
                        hasPty = true;

                    if (request.Value.WantReply) await Channel.ReplyAsync(allowed, CancellationToken).ConfigureAwait(false);

                }
                else if (type is "env" or "window-change")
                {
                    if (request.Value.WantReply) await Channel.ReplyAsync(true, CancellationToken).ConfigureAwait(false);
                }
                else if (request.Value.WantReply)
                    await Channel.ReplyAsync(false, CancellationToken).ConfigureAwait(false);

            }

        }

        #endregion


        #region (helpers) encode / decode

        /// <summary>
        /// Encode a single SSH string (for an <c>exec</c> command payload).
        /// </summary>
        public static Byte[] EncodeString(String Value)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteString(Value);
            return abw.WrittenSpan.ToArray();
        }

        /// <summary>
        /// Encode a single uint32 (for an <c>exit-status</c> payload).
        /// </summary>
        public static Byte[] EncodeUInt32(UInt32 Value)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteUInt32(Value);
            return abw.WrittenSpan.ToArray();
        }

        private static String ReadString(Byte[] Data) { var r = new SshPacketReader(Data); return r.ReadString(); }
        private static UInt32 ReadUInt32(Byte[] Data) { var r = new SshPacketReader(Data); return r.ReadUInt32(); }

        private static async ValueTask<Byte[]> CopyToBytesAsync(Stream Stream, CancellationToken CancellationToken)
        {
            using var buffer = new MemoryStream();
            await Stream.CopyToAsync(buffer, CancellationToken).ConfigureAwait(false);
            return buffer.ToArray();
        }

        #endregion

    }


    /// <summary>
    /// Bidirectional stream relay for tunnels (channel ↔ socket): copies both ways until either side ends, then tears down.
    /// </summary>
    public static class SshChannelRelay
    {

        private const Int32 Buffer = 32 * 1024;

        /// <summary>
        /// Relay bytes both ways between two streams until either ends; disposes both on completion.
        /// </summary>
        public static async Task RelayAsync(Stream A, Stream B, CancellationToken CancellationToken = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            var one = PumpAsync(A, B, cts.Token);
            var two = PumpAsync(B, A, cts.Token);
            await Task.WhenAny(one, two).ConfigureAwait(false);
            await cts.CancelAsync().ConfigureAwait(false);
            try { await Task.WhenAll(one, two).ConfigureAwait(false); } catch { }
            try { await A.DisposeAsync().ConfigureAwait(false); } catch { }
            try { await B.DisposeAsync().ConfigureAwait(false); } catch { }
        }

        private static async Task PumpAsync(Stream From, Stream To, CancellationToken CancellationToken)
        {
            var buffer = new Byte[Buffer];
            try
            {
                Int32 read;
                while ((read = await From.ReadAsync(buffer, CancellationToken).ConfigureAwait(false)) > 0)
                {
                    await To.WriteAsync(buffer.AsMemory(0, read), CancellationToken).ConfigureAwait(false);
                    await To.FlushAsync(CancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
            catch (SshChannelClosedException) { }
        }

    }

}
