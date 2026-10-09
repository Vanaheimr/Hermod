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
    /// key, and which credentials to try. Credentials are <see cref="ISshHostKey"/> signers — a private key,
    /// an agent-backed key (<c>SshAgentKey</c>) or a certificate-bearing key (<c>CertifiedKey</c>).
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
        /// The public-key credentials to try, in order.
        /// </summary>
        public IReadOnlyList<ISshHostKey>         Credentials    { get; init; } = [];

        /// <summary>
        /// Called when the server advertises its full host-key set via <c>hostkeys-00@openssh.com</c>
        /// (OpenSSH's <c>UpdateHostKeys</c>) — the hook for rotating a <c>known_hosts</c> entry before
        /// the old key is retired. Every reported key has already proven possession of its private half,
        /// so it is safe to trust; when the server fails to prove them the callback is not invoked.
        /// Leave null to ignore the advertisement (the default).
        /// </summary>
        public Func<SshHostKeyUpdate, CancellationToken, ValueTask>?  HostKeysReceived  { get; init; }
    }


    /// <summary>
    /// A high-level SSH client over the connection multiplexer: connect and authenticate once, then run many
    /// operations concurrently on the one connection — <see cref="ExecuteAsync"/> commands and
    /// <see cref="OpenTcpStreamAsync"/> tunnels multiplex freely.
    /// </summary>
    public sealed class SshClient : IAsyncDisposable
    {

        #region Data

        private readonly IDuplexPipe             pipe;
        private readonly SshTransport            transport;
        private readonly SshChannelMultiplexer   mux;

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
        public static async ValueTask<SshClient> ConnectAsync(String host, UInt16 port, SshClientOptions Options, CancellationToken CancellationToken = default)
        {

            var pipe      = await SshTcp.ConnectAsync(host, IPPort.Parse(port), CancellationToken).ConfigureAwait(false);
            var transport = await SshTransport.ClientHandshakeAsync(pipe, VerifyHostKey: Options.VerifyHostKey, CancellationToken: CancellationToken).ConfigureAwait(false);

            var authenticated = false;
            foreach (var credential in Options.Credentials)
            {
                if (await UserAuthentication.ClientPublicKeyAuthenticateAsync(transport, Options.Username, credential, CancellationToken: CancellationToken).ConfigureAwait(false))
                {
                    authenticated = true;
                    break;
                }
            }

            if (!authenticated)
            {
                await transport.CloseOutputAsync().ConfigureAwait(false);
                transport.Dispose();
                await CloseInputAsync(pipe).ConfigureAwait(false);
                throw new SshAuthenticationException("None of the supplied credentials were accepted.");
            }

            var mux = new SshChannelMultiplexer(transport);

            if (Options.HostKeysReceived is not null)
                WireHostKeyUpdates(mux, transport.ServerHostKey, Options.HostKeysReceived);

            return new SshClient(pipe, transport, mux.Start());

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

            await mux.DisposeAsync().ConfigureAwait(false);

            // The writer is completed by the transport, after the send in flight - a window adjust,
            // the DISCONNECT above - is done with it: completed under it, its buffers went back to the
            // pool every connection rents from while still being written into.
            await transport.CloseOutputAsync().ConfigureAwait(false);
            transport.Dispose();
            await CloseInputAsync(pipe).ConfigureAwait(false);

        }

        private static async ValueTask CloseInputAsync(IDuplexPipe Pipe)
        {
            try { await Pipe.Input.CompleteAsync().ConfigureAwait(false); } catch { }
        }

        #endregion

    }

}
