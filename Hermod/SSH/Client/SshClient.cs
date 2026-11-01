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

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Client
{

    /// <summary>
    /// Options for a high-level <see cref="SshClient"/> connection: who to log in as, how to trust the host
    /// key, and which credentials to try - keys (<see cref="Credentials"/>), answers to
    /// <see cref="KeyboardInteractive"/> prompts and a <see cref="Password"/>, in that order and each only
    /// where the server takes it. Several of them together also complete a multi-factor chain such as OpenSSH's
    /// <c>AuthenticationMethods publickey,password</c>.
    /// </summary>
    public sealed record SshClientOptions
    {
        /// <summary>
        /// The user to authenticate as.
        /// </summary>
        public required String                    Username       { get; init; }

        /// <summary>
        /// The host-key trust decision — <b>required</b>, because omitting it would leave the server
        /// unauthenticated and the session open to a machine-in-the-middle, and a silent default is
        /// exactly the kind of mistake that is never noticed.
        ///
        /// <para>
        /// Normally comes from a <see cref="HostKeyPolicy"/>: <c>policy.ForHost(host, port)</c> chains
        /// pinned fingerprints, <c>known_hosts</c>, host certificates, SSHFP and TOFU. For loopback
        /// tests and demos, pass <see cref="SshHostKeyVerification.AcceptAnyUnsafe"/> — deliberately
        /// spelled out rather than reached by leaving a property unset.
        /// </para>
        /// </summary>
        public required Func<Byte[], Boolean>     VerifyHostKey  { get; init; }

        /// <summary>
        /// The public-key credentials to try, in order: <see cref="ISshHostKey"/> signers - a private key,
        /// an agent-backed key (<c>SshAgentKey</c>) or a certificate-bearing key (<c>CertifiedKey</c>).
        /// </summary>
        public IReadOnlyList<ISshHostKey>         Credentials    { get; init; } = [];

        /// <summary>
        /// The password (RFC 4252 §8), or null to log in without one. It is sent only to a server that
        /// lists <c>password</c> among the methods it takes - and only after the host key has been
        /// verified, like everything else that follows the key exchange.
        /// </summary>
        public String?                            Password       { get; init; }

        /// <summary>
        /// Answers the server's <c>keyboard-interactive</c> prompts (RFC 4256) - a one-time code, say, or
        /// the questions of a PAM stack: one answer per prompt, in order. Null to leave the method out.
        /// </summary>
        public Func<SshKeyboardInteractiveChallenge, CancellationToken, ValueTask<String[]>>?  KeyboardInteractive  { get; init; }

        /// <summary>
        /// Called when the server advertises its full host-key set via <c>hostkeys-00@openssh.com</c>
        /// (OpenSSH's <c>UpdateHostKeys</c>) — the hook for rotating a <c>known_hosts</c> entry before
        /// the old key is retired. Every reported key has already proven possession of its private half,
        /// so it is safe to trust; when the server fails to prove them the callback is not invoked.
        /// Leave null to ignore the advertisement (the default).
        /// </summary>
        public Func<SshHostKeyUpdate, CancellationToken, ValueTask>?  HostKeysReceived  { get; init; }

        /// <summary>
        /// When the connection renews its keys by itself (default 1 GiB / 1 h, RFC 4253 §9), or null to
        /// rekey only when the server asks.
        /// </summary>
        public SshRekeyLimits?                    RekeyLimits    { get; init; } = SshRekeyLimits.Default;

        /// <summary>
        /// How long connecting may take - the TCP connection, the version exchange and the key exchange, as
        /// OpenSSH's <c>ConnectTimeout</c> - before it fails with a <see cref="TimeoutException"/>; null (the
        /// default) for no limit of our own. Authentication is not counted: it may be waiting for a person
        /// to answer a prompt.
        /// </summary>
        public TimeSpan?                          ConnectTimeout     { get; init; }

        /// <summary>
        /// Ask the server whether it is still there, every interval - <c>keepalive@openssh.com</c>, as
        /// OpenSSH's <c>ServerAliveInterval</c> - or null (the default) never to ask. Without it, a server
        /// that died, or a network that dropped the connection without a word, is noticed only when TCP
        /// gives up, which on an idle connection can be never. The probes also keep a NAT or firewall
        /// from forgetting an idle connection.
        /// </summary>
        public TimeSpan?                          KeepAliveInterval  { get; init; }

        /// <summary>
        /// Unanswered keepalive probes in a row after which the connection is given up (default 3, as
        /// OpenSSH's <c>ServerAliveCountMax</c>): it is closed, and everything still waiting on it fails
        /// with a <see cref="TimeoutException"/>.
        /// </summary>
        public Int32                              KeepAliveCountMax  { get; init; } = 3;
    }


    /// <summary>
    /// A high-level SSH client over the connection multiplexer: connect and authenticate once, then run many
    /// operations concurrently on the one connection — <see cref="ExecuteAsync"/> commands and
    /// <see cref="OpenTcpStreamAsync"/> tunnels multiplex freely.
    /// </summary>
    public sealed class SshClient : IAsyncDisposable
    {

        #region Data

        /// <summary>
        /// The global request a keepalive probe sends: OpenSSH's, which every server answers - with a
        /// refusal, mostly, and that is answer enough.
        /// </summary>
        public const String KeepAliveRequestName = "keepalive@openssh.com";

        private readonly IDuplexPipe              pipe;
        private readonly SshTransport             transport;
        private readonly SshChannelMultiplexer    mux;
        private readonly CancellationTokenSource  stopping   = new ();
        private          Task                     keepAlive  = Task.CompletedTask;
        private          Int32                    disposed;
        private          Int32                    closed;

        #endregion

        #region Properties

        /// <summary>
        /// The underlying multiplexer, for advanced channel operations.
        /// </summary>
        public SshChannelMultiplexer Multiplexer => mux;

        #endregion

        #region Constructor(s)

        private SshClient(IDuplexPipe Pipe, SshTransport Transport, SshChannelMultiplexer Mux)
        {
            this.pipe      = Pipe;
            this.transport = Transport;
            this.mux       = Mux;
        }

        #endregion


        #region (static) ConnectAsync(Host, Port, Options, CancellationToken)

        /// <summary>
        /// Connect, verify the host key, authenticate with the first working credential and start multiplexing.
        /// </summary>
        /// <exception cref="TimeoutException">Connecting took longer than <see cref="SshClientOptions.ConnectTimeout"/>.</exception>
        /// <exception cref="SshAuthenticationException">None of the credentials was accepted.</exception>
        public static async ValueTask<SshClient> ConnectAsync(String host, UInt16 port, SshClientOptions Options, CancellationToken CancellationToken = default)
        {

            IDuplexPipe?  pipe = null;
            SshTransport  transport;

            using (var connecting = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken))
            {

                if (Options.ConnectTimeout is TimeSpan limit)
                    connecting.CancelAfter(limit);

                try
                {
                    pipe      = await SshTcp.ConnectAsync(host, IPPort.Parse(port), connecting.Token).ConfigureAwait(false);
                    transport = await SshTransport.ClientHandshakeAsync(pipe, VerifyHostKey: Options.VerifyHostKey, CancellationToken: connecting.Token).ConfigureAwait(false);
                }
                catch (Exception exception)
                {

                    // A server that took the connection and then said nothing - or the wrong thing - would
                    // otherwise keep the socket until the process ends.
                    if (pipe is not null)
                        await AbandonAsync(pipe).ConfigureAwait(false);

                    if (exception is OperationCanceledException && connecting.IsCancellationRequested && !CancellationToken.IsCancellationRequested)
                        throw new TimeoutException(FormattableString.Invariant($"Could not connect to {host}:{port} within {Options.ConnectTimeout!.Value.TotalSeconds:0.###} s."), exception);

                    throw;

                }

            }

            Boolean authenticated;

            try
            {
                authenticated = await UserAuthentication.ClientAuthenticateAsync(transport,
                                                                                 Options.Username,
                                                                                 Options.Credentials,
                                                                                 Options.Password,
                                                                                 Options.KeyboardInteractive,
                                                                                 CancellationToken: CancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await CloseTransportAsync(transport, pipe).ConfigureAwait(false);
                throw;
            }

            if (!authenticated)
            {
                await CloseTransportAsync(transport, pipe).ConfigureAwait(false);
                throw new SshAuthenticationException("None of the supplied credentials were accepted.");
            }

            var mux = new SshChannelMultiplexer(transport) {
                          RekeyLimits = Options.RekeyLimits
                      };

            if (Options.HostKeysReceived is not null)
                WireHostKeyUpdates(mux, transport.ServerHostKey, Options.HostKeysReceived);

            var client = new SshClient(pipe, transport, mux.Start());

            if (Options.KeepAliveInterval is TimeSpan interval)
                client.keepAlive = client.KeepAliveAsync(interval, Math.Max(1, Options.KeepAliveCountMax));

            return client;

        }

        // Handle the server's hostkeys-00@openssh.com advertisement. The proof exchange must run OFF the
        // receive loop: it awaits a REQUEST_SUCCESS that this very loop is the one to dispatch, so doing
        // it inline would deadlock. The connect-scoped token is deliberately not captured — the callback
        // outlives ConnectAsync; a torn-down multiplexer faults the pending reply and ends the task.
        private static void WireHostKeyUpdates(SshChannelMultiplexer                              Mux,
                                               Byte[]                                             CurrentHostKey,
                                               Func<SshHostKeyUpdate, CancellationToken, ValueTask>  Callback)
        {
            Mux.GlobalRequestHandler = info =>
            {

                if (info.Name != SshHostKeyRotation.AnnouncementRequestName)
                    return ValueTask.FromResult(false);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var update = await SshHostKeyRotation.HandleAnnouncementAsync(Mux, info.Data, CurrentHostKey).ConfigureAwait(false);
                        if (update.ProvenKeys.Count > 0)
                            await Callback(update, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch { /* the advertisement is advisory — never break the connection over it */ }
                });

                return ValueTask.FromResult(true);

            };
        }

        #endregion

        #region (private) KeepAliveAsync(Interval, CountMax)

        /// <summary>
        /// Ask every interval, and give up once as many questions in a row as allowed went unanswered -
        /// any reply is an answer, as the refusal a server gives a request it does not know. The connection
        /// is then cut without the DISCONNECT nobody would read, and what waits on it learns why.
        /// </summary>
        private async Task KeepAliveAsync(TimeSpan Interval, Int32 CountMax)
        {

            var token      = stopping.Token;
            var unanswered = 0;

            try
            {
                while (true)
                {

                    await Task.Delay(Interval, token).ConfigureAwait(false);

                    try
                    {
                        // The wait includes the send: on a network that is gone, a write can hang too.
                        await mux.SendGlobalRequestWithReplyAsync(KeepAliveRequestName, true, [], token).
                                  AsTask().
                                  WaitAsync(Interval, token).
                                  ConfigureAwait(false);
                        unanswered = 0;
                    }
                    catch (TimeoutException)
                    {
                        if (++unanswered >= CountMax)
                        {
                            mux.Abort(new TimeoutException(FormattableString.Invariant($"The server did not answer {unanswered} keepalive probe(s) in a row, {Interval.TotalSeconds:0.###} s apart.")));
                            await CloseAsync(SayGoodbye: false).ConfigureAwait(false);
                            return;
                        }
                    }

                }
            }
            catch
            {
                // Disposed, or the connection ended otherwise - and what ended it is what its users see.
            }

        }

        #endregion

        #region ExecuteAsync(Command, CancellationToken)

        /// <summary>
        /// Run a command on the server and capture its stdout/stderr + exit status (log in once, run, capture, log out).
        /// </summary>
        public ValueTask<SshCommandResult> ExecuteAsync(String Command, CancellationToken CancellationToken = default)
            => SshSessionChannel.ExecuteAsync(mux, Command, CancellationToken);

        #endregion

        #region OpenShellAsync(Pty = null, Environment = null, CancellationToken = default)

        /// <summary>
        /// Start an interactive shell, with the given pseudo-terminal where there is one - what
        /// <c>ssh host</c> does at a terminal.
        /// </summary>
        public ValueTask<SshClientShell> OpenShellAsync(SshPty?                               Pty                = null,
                                                        IReadOnlyDictionary<String, String>?  Environment        = null,
                                                        CancellationToken                     CancellationToken  = default)

            => SshClientShell.OpenAsync(mux, Pty, Environment, CancellationToken);

        #endregion

        #region OpenTcpStreamAsync(Host, Port, CancellationToken)

        /// <summary>
        /// Open a <c>direct-tcpip</c> tunnel to <paramref name="Host"/>:<paramref name="Port"/> through the server as a plain <see cref="Stream"/>.
        /// </summary>
        public async ValueTask<Stream> OpenTcpStreamAsync(String Host, UInt16 Port, CancellationToken CancellationToken = default)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteString(Host); w.WriteUInt32(Port); w.WriteString("127.0.0.1"); w.WriteUInt32(0);
            var channel = await mux.OpenChannelAsync("direct-tcpip", abw.WrittenSpan.ToArray(), CancellationToken).ConfigureAwait(false);
            return channel.AsStream();
        }

        #endregion

        #region OpenSftpClientAsync(CancellationToken)

        /// <summary>
        /// Open the <c>sftp</c> subsystem over a multiplexed channel and return an SFTP client (runs concurrently with exec/tunnels).
        /// </summary>
        public async ValueTask<SftpClient> OpenSftpClientAsync(CancellationToken CancellationToken = default)
        {
            var channel = await mux.OpenChannelAsync("session", CancellationToken: CancellationToken).ConfigureAwait(false);
            if (!await channel.SendRequestAsync("subsystem", true, SshSessionChannel.EncodeString("sftp"), CancellationToken).ConfigureAwait(false))
                throw new SshWireException("The server refused the 'sftp' subsystem.");
            return await SftpClient.OpenAsync(new StreamSftpDuplex(channel.AsStream()), CancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region DisposeAsync()

        /// <summary>
        /// Close the connection and stop multiplexing.
        /// </summary>
        /// <remarks>
        /// The server is told - a DISCONNECT, as every client says goodbye - and the connection is closed.
        /// Neither happened before: the socket stayed open until the process ended, and a server saw a
        /// client that had long gone as one still connected.
        /// </remarks>
        public async ValueTask DisposeAsync()
        {

            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;

            await stopping.CancelAsync().ConfigureAwait(false);

            try   { await keepAlive.ConfigureAwait(false); }
            catch { }

            await CloseAsync(SayGoodbye: true).ConfigureAwait(false);

            stopping.Dispose();

        }

        // Once, whoever comes first: DisposeAsync, or the keepalive that found the server gone - which
        // says no goodbye, and cuts the socket first, so that nothing waits on a write the peer never takes.
        private async ValueTask CloseAsync(Boolean SayGoodbye)
        {

            if (Interlocked.Exchange(ref closed, 1) != 0)
                return;

            if (SayGoodbye)
            {
                try
                {
                    var abw = new ArrayBufferWriter<Byte>();
                    var w   = new SshPacketWriter(abw);
                    w.WriteByte((Byte) SshMessageNumber.Disconnect);
                    w.WriteUInt32((UInt32) DisconnectReason.ByApplication);
                    w.WriteString("the client is closing the connection");
                    w.WriteString("");
                    await mux.SendAsync(abw.WrittenSpan.ToArray()).AsTask().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                }
                catch
                {
                    // The connection may be gone already.
                }
            }

            else if (pipe is DuplexPipe { Abort: { } abort })
            {
                try   { abort(); }
                catch { }
            }

            await mux.DisposeAsync().ConfigureAwait(false);
            await CloseTransportAsync(transport, pipe).ConfigureAwait(false);

        }

        // The writer is completed by the transport, after the send in flight - a window adjust, the
        // DISCONNECT above - is done with it: completed under it, its buffers went back to the pool every
        // connection rents from while still being written into.
        private static async ValueTask CloseTransportAsync(SshTransport Transport, IDuplexPipe Pipe)
        {
            await Transport.CloseOutputAsync().ConfigureAwait(false);
            Transport.Dispose();
            await CloseInputAsync(Pipe).ConfigureAwait(false);
        }

        // No transport yet, so nothing is being written: cut the connection, then let both halves go.
        private static async ValueTask AbandonAsync(IDuplexPipe Pipe)
        {

            if (Pipe is DuplexPipe { Abort: { } abort })
            {
                try   { abort(); }
                catch { }
            }

            try { await Pipe.Output.CompleteAsync().ConfigureAwait(false); } catch { }
            await CloseInputAsync(Pipe).ConfigureAwait(false);

        }

        private static async ValueTask CloseInputAsync(IDuplexPipe Pipe)
        {
            try { await Pipe.Input.CompleteAsync().ConfigureAwait(false); } catch { }
        }

        #endregion

    }

}
