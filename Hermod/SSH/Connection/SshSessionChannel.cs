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
        public static ValueTask ServeAsync(SshMuxChannel                                                              Channel,
                                           String                                                                     Username,
                                           SshExecHandler?                                                            Handler,
                                           IReadOnlyDictionary<String, Func<SshMuxChannel, CancellationToken, ValueTask>>?  Subsystems = null,
                                           CancellationToken                                                          CancellationToken = default,
                                           SshSessionRestrictions?                                                    Restrictions = null)

            => ServeAsync(Channel,
                          new SshSessionInfo("", Username, "", null, null, null, null, Restrictions ?? SshSessionRestrictions.None),
                          Handler,
                          ShellHandler:       null,
                          Subsystems:         Subsystems,
                          AuditSink:          null,
                          Stopping:           CancellationToken.None,
                          CancellationToken:  CancellationToken);


        /// <summary>
        /// Serve a session channel: accept <c>pty-req</c>/<c>env</c>; run a <c>shell</c> request with
        /// <paramref name="ShellHandler"/> where there is one, and an <c>exec</c> request - or a
        /// <c>shell</c> request where there is none - with <paramref name="ExecHandler"/>; dispatch a
        /// <c>subsystem</c> request to a matching handler in <paramref name="Subsystems"/>; and report
        /// the exit status. Everything else is refused.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A shell is a conversation, so the channel's requests go on being read while it runs: a
        /// <c>window-change</c>, a <c>signal</c> and a <c>break</c> reach it as they come, and every
        /// other request is answered with failure rather than left waiting until the shell ends.
        /// </para>
        /// <para>
        /// A credential confined to one command (<c>force-command</c>) is never given a shell: its shell
        /// request runs that command with the exec handler instead, as before - and is refused where there
        /// is no exec handler, rather than widened into the shell it was not given.
        /// </para>
        /// </remarks>
        /// <param name="Channel">The session channel.</param>
        /// <param name="Session">Who the session is with.</param>
        /// <param name="ExecHandler">Runs <c>exec</c> requests; none, and they are refused.</param>
        /// <param name="ShellHandler">Runs <c>shell</c> requests; none, and they go to the exec handler.</param>
        /// <param name="Subsystems">The subsystems served, by name.</param>
        /// <param name="AuditSink">Where what the session asked for and was refused is recorded.</param>
        /// <param name="Stopping">Fires when the server is stopping: a shell is then asked to end.</param>
        /// <param name="CancellationToken">Ends everything at once.</param>
        public static async ValueTask ServeAsync(SshMuxChannel                                                                    Channel,
                                                 SshSessionInfo                                                                   Session,
                                                 SshExecHandler?                                                                  ExecHandler,
                                                 SshShellHandler?                                                                 ShellHandler,
                                                 IReadOnlyDictionary<String, Func<SshMuxChannel, CancellationToken, ValueTask>>?  Subsystems,
                                                 ISshAuditSink?                                                                   AuditSink,
                                                 CancellationToken                                                                Stopping,
                                                 CancellationToken                                                                CancellationToken)
        {

            var hasPty        = false;
            SshPty? pty       = null;
            var environment   = new Dictionary<String, String>(StringComparer.Ordinal);
            var restrictions  = Session.Restrictions;

            SshChannelRequest? request;
            while ((request = await Channel.ReadRequestAsync(CancellationToken).ConfigureAwait(false)) is not null)
            {

                var type = request.Value.Type;

                if (type == "subsystem")
                {
                    var name = ReadString(request.Value.Data);
                    if (Subsystems is not null && Subsystems.TryGetValue(name, out var serve))
                    {
                        await EmitAsync(AuditSink, new SubsystemRequestedEvent(DateTimeOffset.UtcNow, name), CancellationToken).ConfigureAwait(false);
                        if (request.Value.WantReply) await Channel.ReplyAsync(true, CancellationToken).ConfigureAwait(false);
                        await serve(Channel, CancellationToken).ConfigureAwait(false);
                        return;
                    }
                    await EmitAsync(AuditSink, new PolicyDeniedEvent(DateTimeOffset.UtcNow, "subsystem", name, "not served"), CancellationToken).ConfigureAwait(false);
                    if (request.Value.WantReply) await Channel.ReplyAsync(false, CancellationToken).ConfigureAwait(false);
                    continue;
                }

                // A shell, where the credential is not confined to one command.
                if (type == "shell" && ShellHandler is not null && restrictions.ForcedCommand is null)
                {

                    if (request.Value.WantReply)
                        await Channel.ReplyAsync(true, CancellationToken).ConfigureAwait(false);

                    await EmitAsync(AuditSink, new SessionOpenedEvent(DateTimeOffset.UtcNow, Session.Username), CancellationToken).ConfigureAwait(false);

                    try
                    {
                        await RunShellAsync(Channel, Session, ShellHandler, pty, environment, Stopping, CancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        await EmitAsync(AuditSink, new SessionClosedEvent(DateTimeOffset.UtcNow, Session.Username), CancellationToken.None).ConfigureAwait(false);
                    }

                    return;

                }

                if (type is "exec" or "shell" && ExecHandler is not null)
                {

                    var requested = type == "exec" ? ReadString(request.Value.Data) : "";

                    // A force-command certificate confines the session to the CA's command, whatever the
                    // client asked for — including a bare shell request. What it asked for is kept for
                    // the handler's information only (OpenSSH's SSH_ORIGINAL_COMMAND).
                    var forced    = restrictions.ForcedCommand;
                    var command   = forced ?? requested;

                    await EmitAsync(AuditSink, new ExecRequestedEvent(DateTimeOffset.UtcNow, command), CancellationToken).ConfigureAwait(false);

                    if (request.Value.WantReply)
                        await Channel.ReplyAsync(true, CancellationToken).ConfigureAwait(false);

                    var channel = Channel;
                    var context = new SshExecContext(command, Session.Username,
                                                     (data, isErr, ct) => isErr ? channel.SendErrorAsync(data, ct) : channel.SendDataAsync(data, ct),
                                                     hasPty: hasPty,
                                                     originalCommand: forced is null ? null : requested);

                    var exit = await ExecHandler(context, CancellationToken).ConfigureAwait(false);

                    await Channel.SendRequestAsync("exit-status", false, EncodeUInt32((UInt32) exit), CancellationToken).ConfigureAwait(false);
                    await Channel.CloseAsync(CancellationToken).ConfigureAwait(false);
                    return;
                }

                if (type is "exec" or "shell")
                {
                    await EmitAsync(AuditSink, new PolicyDeniedEvent(DateTimeOffset.UtcNow, type, type == "exec" ? ReadString(request.Value.Data) : "", "not served"), CancellationToken).ConfigureAwait(false);
                    if (request.Value.WantReply) await Channel.ReplyAsync(false, CancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (type == "pty-req")
                {

                    // no-pty / restrict: refuse the terminal rather than granting it and pretending.
                    var allowed = restrictions.AllowPty;

                    if (allowed)
                    {
                        try
                        {
                            pty     = SshPty.Parse(request.Value.Data);
                            hasPty  = true;
                        }
                        catch (SshWireException)
                        {
                            // A terminal that cannot be read is not granted.
                            allowed = false;
                        }
                    }

                    if (request.Value.WantReply) await Channel.ReplyAsync(allowed, CancellationToken).ConfigureAwait(false);

                }
                else if (type == "env")
                {

                    // Kept for a shell to read, within reason: a client can send any number of these.
                    var accepted = false;

                    try
                    {
                        var (name, value) = ReadNameAndValue(request.Value.Data);
                        if (environment.Count < MaxEnvironmentVariables && name.Length <= 256 && value.Length <= 4096)
                        {
                            environment[name] = value;
                            accepted = true;
                        }
                    }
                    catch (SshWireException)
                    { }

                    if (request.Value.WantReply) await Channel.ReplyAsync(accepted, CancellationToken).ConfigureAwait(false);

                }
                else if (type == "window-change")
                {

                    if (pty is not null)
                    {
                        try
                        {
                            pty = pty with { Size = SshWindowSize.ParseWindowChange(request.Value.Data) };
                        }
                        catch (SshWireException)
                        { }
                    }

                    if (request.Value.WantReply) await Channel.ReplyAsync(true, CancellationToken).ConfigureAwait(false);

                }
                else
                {

                    if (type is "x11-req" or "auth-agent-req@openssh.com")
                        await EmitAsync(AuditSink, new PolicyDeniedEvent(DateTimeOffset.UtcNow, type, "", "not served"), CancellationToken).ConfigureAwait(false);

                    if (request.Value.WantReply)
                        await Channel.ReplyAsync(false, CancellationToken).ConfigureAwait(false);

                }

            }

        }

        #endregion

        #region (private) RunShellAsync(Channel, Session, Handler, Pty, Environment, Stopping, CancellationToken)

        /// <summary>
        /// Run a shell to its end, reading the channel's requests beside it, and tell the client its exit
        /// status - where the client is still there to be told.
        /// </summary>
        private static async Task RunShellAsync(SshMuxChannel                        Channel,
                                                SshSessionInfo                       Session,
                                                SshShellHandler                      Handler,
                                                SshPty?                              Pty,
                                                IReadOnlyDictionary<String, String>  Environment,
                                                CancellationToken                    Stopping,
                                                CancellationToken                    CancellationToken)
        {

            // The client closed the channel, or the connection went: the channel's Closed task ends either
            // way, faulted in the second case.
            using var closed  = new CancellationTokenSource();
            using var ended   = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, Stopping, closed.Token);

            _ = Channel.Closed.ContinueWith(_ => {
                    try { closed.Cancel(); } catch (ObjectDisposedException) { }
                }, TaskScheduler.Default);

            var context   = new SshShellContext(Channel, Session, Pty, Environment, closed.Token, Stopping);

            // On a task of its own, so that a handler that does not await anything for a while does not
            // keep the requests below from being read.
            var running   = Task.Run(async () => await Handler(context, ended.Token).ConfigureAwait(false));
            var requested = Channel.ReadRequestAsync(CancellationToken).AsTask();

            while (true)
            {

                if (await Task.WhenAny(running, requested).ConfigureAwait(false) == running)
                    break;

                SshChannelRequest? request;

                try
                {
                    request = await requested.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                // No more requests: the channel is closed. The handler hears of it through its token.
                if (request is not SshChannelRequest next)
                    break;

                var handled = false;

                try
                {
                    switch (next.Type)
                    {

                        case "window-change":
                            context.OnWindowChange(SshWindowSize.ParseWindowChange(next.Data));
                            handled = true;
                            break;

                        case "signal":
                            context.OnSignal(ReadString(next.Data));
                            handled = true;
                            break;

                        case "break":
                            context.OnBreak(ReadUInt32(next.Data));
                            handled = true;
                            break;

                    }
                }
                catch (SshWireException)
                {
                    handled = false;
                }

                // Only a break is answered with success (RFC 4335); the others want no reply, and
                // whatever else arrives now is refused - a second pty, a second shell.
                if (next.WantReply)
                    await Channel.ReplyAsync(handled && next.Type == "break", CancellationToken).ConfigureAwait(false);

                requested = Channel.ReadRequestAsync(CancellationToken).AsTask();

            }

            // A read still waiting ends with the channel; nothing waits for it, and nothing it ends with
            // is anybody's business.
            _ = requested.ContinueWith(task => _ = task.Exception, TaskContinuationOptions.OnlyOnFaulted);

            Int32 exit;

            try
            {
                exit = await running.ConfigureAwait(false);
            }
            catch
            {
                exit = 255;
            }

            if (closed.IsCancellationRequested)
                return;

            try
            {
                await Channel.SendRequestAsync("exit-status", false, EncodeUInt32((UInt32) exit), CancellationToken).ConfigureAwait(false);
                await Channel.CloseAsync(CancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // The client went while it was being told.
            }

        }

        #endregion

        #region (private) EmitAsync(Sink, Event, CancellationToken)

        private static ValueTask EmitAsync(ISshAuditSink? Sink, SshAuditEvent Event, CancellationToken CancellationToken)
            => Sink?.WriteAsync(Event, CancellationToken) ?? ValueTask.CompletedTask;

        #endregion

        /// <summary>
        /// How many <c>env</c> requests a session keeps.
        /// </summary>
        private const Int32 MaxEnvironmentVariables = 64;

        private static (String Name, String Value) ReadNameAndValue(Byte[] Data)
        {
            var r = new SshPacketReader(Data);
            return (r.ReadString(), r.ReadString());
        }


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
