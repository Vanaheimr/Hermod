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

using Microsoft.Extensions.Logging;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.Messaging;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Client;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// Connecting failed, and the client will try again - or not, as <paramref name="NextAttemptIn"/> says.
    /// </summary>
    public delegate Task OnSshMessageConnectFailedDelegate(DateTimeOffset     Timestamp,
                                                           SshMessageClient   Sender,
                                                           Exception          Error,
                                                           UInt32             Attempt,
                                                           TimeSpan?          NextAttemptIn,
                                                           CancellationToken  CancellationToken);


    /// <summary>
    /// Where and how an <see cref="SshMessageClient"/> connects.
    /// </summary>
    public sealed record SshMessageClientOptions
    {

        /// <summary>
        /// The server's host name or address.
        /// </summary>
        public required String                           Host               { get; init; }

        /// <summary>
        /// The server's SSH port.
        /// </summary>
        public UInt16                                    Port               { get; init; } = 22;

        /// <summary>
        /// The SSH login: user name, credentials, how the server's host key is checked, keepalive.
        /// </summary>
        public required SshClientOptions                 Ssh                { get; init; }

        /// <summary>
        /// The subsystem asked for, e.g. "ocpp@open.charging.cloud".
        /// </summary>
        public required String                           Subsystem          { get; init; }

        /// <summary>
        /// The subprotocols offered, by preference; empty to ask for none.
        /// </summary>
        public IReadOnlyList<String>                     Subprotocols       { get; init; } = [];

        /// <summary>
        /// What the HELLO tells besides the subprotocols and the limit.
        /// </summary>
        public JObject?                                  HelloProperties    { get; init; }

        /// <summary>
        /// The largest message the client takes - told to the server in its HELLO.
        /// </summary>
        public UInt64                                    MaxMessageSizeIn   { get; init; } = SshMessageFraming.DefaultMaxMessageSize;

        /// <summary>
        /// The largest message the client sends - or the server's limit, where that is smaller.
        /// </summary>
        public UInt64                                    MaxMessageSizeOut  { get; init; } = SshMessageFraming.DefaultMaxMessageSize;

        /// <summary>
        /// How long the server has to answer the HELLO.
        /// </summary>
        public TimeSpan                                  WelcomeTimeout     { get; init; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Every how long a PING is sent; null for none.
        /// </summary>
        public TimeSpan?                                 PingEvery          { get; init; }

        /// <summary>
        /// How the client connects again after a connection ended that it did not end itself - or failed
        /// to be made; null to stay disconnected.
        /// </summary>
        public WebSocketClientReconnectPolicy?           Reconnect          { get; init; }

    }


    /// <summary>
    /// A client of messages over an SSH subsystem: it signs in, asks for the subsystem, says HELLO, and is
    /// welcomed or refused - see README.md. Where it has a reconnect policy, it connects again after the
    /// connection ended, until it is closed or disposed of.
    /// </summary>
    public sealed class SshMessageClient : ASshMessageEndpoint, IAsyncDisposable
    {

        #region Data

        private readonly CancellationTokenSource  stopping  = new ();
        private readonly SemaphoreSlim            connectLock = new (1, 1);
        private volatile SshMessageConnection?    connection;
        private volatile SshClient?               sshClient;
        private          Task                     supervising = Task.CompletedTask;
        private          Int32                    stopped;

        #endregion

        #region Properties

        /// <summary>
        /// Where and how the client connects.
        /// </summary>
        public SshMessageClientOptions  Options       { get; }

        /// <summary>
        /// The connection made last - which may have ended since.
        /// </summary>
        public SshMessageConnection?    Connection    => connection;

        /// <summary>
        /// Whether the client has a connection that lasts.
        /// </summary>
        public Boolean                  IsConnected   => connection?.IsAlive == true;

        /// <summary>
        /// The SSH connection the messages go over, while they do - for a further channel on it, such as
        /// another subsystem of the same server: the key exchange of NTS, as section 8.3 of the white paper
        /// "NTS Key Establishment over SSH" has it, with the login, the host key and the exporter of this
        /// connection. Null where there is no connection that lasts.
        /// </summary>
        /// <remarks>
        /// It stays this client's. Whoever opens a channel on it closes that channel, never the connection:
        /// it ends with the messages, and the next connection a reconnect makes is a connection of its own.
        /// </remarks>
        public SshClient?               Ssh
            => connection?.IsAlive == true && sshClient is SshClient ssh && !ssh.Closed.IsCompleted
                   ? ssh
                   : null;

        #endregion

        #region Events

        /// <summary>
        /// Connecting failed.
        /// </summary>
        public event OnSshMessageConnectFailedDelegate?  OnConnectFailed;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// A client of messages over the given SSH subsystem; <see cref="ConnectAsync"/> connects it.
        /// </summary>
        public SshMessageClient(SshMessageClientOptions Options)
        {
            this.Options = Options;
        }

        #endregion


        #region ConnectAsync(CancellationToken = default)

        /// <summary>
        /// Connect: sign in, ask for the subsystem, say HELLO, and wait for the server's answer. With a
        /// reconnect policy, a connection that ends later is made again - but this first one is not
        /// retried: a failure here is the caller's.
        /// </summary>
        /// <exception cref="SshMessageRefusedException">The server answered REFUSED.</exception>
        /// <exception cref="SshAuthenticationException">The server took none of the credentials.</exception>
        /// <exception cref="SshMessageProtocolException">The server answered with something else than WELCOME or REFUSED.</exception>
        public async Task<SshMessageConnection> ConnectAsync(CancellationToken CancellationToken = default)
        {

            ObjectDisposedException.ThrowIf(Volatile.Read(ref stopped) != 0, this);

            return await ConnectOnceAsync(CancellationToken).ConfigureAwait(false);

        }

        #endregion

        #region Close(Code = NormalClosure, Reason = null)

        /// <summary>
        /// End the connection, if there is one, and connect no more.
        /// </summary>
        public async Task Close(MessageCloseCode  Code    = MessageCloseCode.NormalClosure,
                                String?           Reason  = null)
        {

            if (Interlocked.Exchange(ref stopped, 1) != 0)
                return;

            await stopping.CancelAsync().ConfigureAwait(false);

            if (connection is SshMessageConnection current)
                await current.Close(Code, Reason).ConfigureAwait(false);

            try
            {
                await supervising.ConfigureAwait(false);
            }
            catch
            { }

            if (sshClient is SshClient ssh)
                await ssh.DisposeAsync().ConfigureAwait(false);

        }

        #endregion

        #region DisposeAsync()

        /// <summary>
        /// End the connection (going away), and connect no more.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await Close(MessageCloseCode.GoingAway).ConfigureAwait(false);
        }

        #endregion


        #region (private) ConnectOnceAsync(CancellationToken)

        private async Task<SshMessageConnection> ConnectOnceAsync(CancellationToken CancellationToken)
        {

            await connectLock.WaitAsync(CancellationToken).ConfigureAwait(false);

            try
            {

                var ssh = await SshClient.ConnectAsync(Options.Host, Options.Port, Options.Ssh, CancellationToken).ConfigureAwait(false);

                try
                {

                    var channel = await ssh.OpenSubsystemAsync(Options.Subsystem, CancellationToken).ConfigureAwait(false);
                    var hello   = SshMessageHello.Create(Options.Subprotocols, Options.MaxMessageSizeIn, Options.HelloProperties);

                    await channel.SendDataAsync(SshMessageFraming.Encode(SshMessageFrameType.Hello, hello.ToBytes()), CancellationToken).ConfigureAwait(false);

                    SshMessageFrame? answer;

                    using (var welcomeWithin = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken))
                    {

                        welcomeWithin.CancelAfter(Options.WelcomeTimeout);

                        try
                        {
                            answer = await SshMessageFraming.ReadAsync(channel.Input, 0, welcomeWithin.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (!CancellationToken.IsCancellationRequested)
                        {
                            throw new TimeoutException($"The server did not answer the HELLO within {Options.WelcomeTimeout.TotalSeconds:0.###} s.");
                        }

                    }

                    if (answer is not SshMessageFrame frame)
                        throw new SshMessageProtocolException(MessageCloseCode.ProtocolError, "The server closed the subsystem before it answered the HELLO.");

                    if (frame.Type == SshMessageFrameType.Refused)
                    {
                        var (code, description) = SshMessageRefusal.Decode(frame.Payload);
                        throw new SshMessageRefusedException(code, description);
                    }

                    if (frame.Type != SshMessageFrameType.Welcome)
                        throw new SshMessageProtocolException(MessageCloseCode.ProtocolError, $"The server answered the HELLO with a frame of kind 0x{(Byte) frame.Type:x2}.");

                    var welcome = SshMessageWelcome.Parse(frame.Payload);

                    // A server may choose none, but not one that was not offered.
                    if (welcome.Subprotocol is String chosen && !hello.Subprotocols.Contains(chosen, StringComparer.Ordinal))
                        throw new SshMessageProtocolException(MessageCloseCode.ProtocolError, $"The server chose the subprotocol '{chosen}', which was not offered.");

                    var made = new SshMessageConnection(this,
                                                        ConnectionRole.Client,
                                                        Options.Subsystem,
                                                        Options.Ssh.Username,
                                                        null,
                                                        ssh.ServerHostKey,
                                                        channel,
                                                        ssh.LocalSocket  ?? IPSocket.Zero,
                                                        ssh.RemoteSocket ?? IPSocket.Zero,
                                                        hello,
                                                        welcome,
                                                        Options.MaxMessageSizeIn,
                                                        Math.Min(Options.MaxMessageSizeOut, welcome.MaxMessageSize ?? Options.MaxMessageSizeOut));

                    // The connection before - ended, or it would not be replaced - and its SSH client go.
                    var previous = sshClient;

                    sshClient   = ssh;
                    connection  = made;

                    if (previous is not null && !ReferenceEquals(previous, ssh))
                        await previous.DisposeAsync().ConfigureAwait(false);

                    await RaiseNewConnection(made, CancellationToken).ConfigureAwait(false);

                    supervising = SuperviseAsync(made, ssh);

                    return made;

                }
                catch
                {
                    await ssh.DisposeAsync().ConfigureAwait(false);
                    throw;
                }

            }
            finally
            {
                connectLock.Release();
            }

        }

        #endregion

        #region (private) SuperviseAsync(Connection, Ssh)

        /// <summary>
        /// Read the connection's messages until it ends; then tell, and - where the client was not
        /// stopped and has a reconnect policy - connect again.
        /// </summary>
        private async Task SuperviseAsync(SshMessageConnection  Connection,
                                          SshClient             Ssh)
        {

            await Task.Yield();

            var pinging = Options.PingEvery is TimeSpan every
                              ? SshMessageServer.PingAsync(Connection, every)
                              : Task.CompletedTask;

            using (var gone = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token))
            {

                // The SSH connection ending tells the handlers at once. The channel's reads end on their
                // own, after what arrived before.
                _ = Ssh.Closed.ContinueWith(_ => {
                        try { gone.Cancel(); } catch (ObjectDisposedException) { }
                    }, TaskScheduler.Default);

                await Connection.RunAsync(gone.Token).ConfigureAwait(false);

            }

            await pinging.ConfigureAwait(false);

            await RaiseConnectionClosed(Connection,
                                        Connection.CloseInfo ?? new SshMessageCloseInfo(null, null, ByPeer: true),
                                        CancellationToken.None).ConfigureAwait(false);

            if (Volatile.Read(ref stopped) != 0 || Options.Reconnect is not WebSocketClientReconnectPolicy policy)
            {
                await Ssh.DisposeAsync().ConfigureAwait(false);
                return;
            }

            await Ssh.DisposeAsync().ConfigureAwait(false);

            for (var attempt = 1u; Volatile.Read(ref stopped) == 0; attempt++)
            {

                try
                {
                    await Task.Delay(policy.DelayForAttempt(attempt), stopping.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                try
                {
                    // Supervised by the next SuperviseAsync from here on.
                    await ConnectOnceAsync(stopping.Token).ConfigureAwait(false);
                    return;
                }
                catch (OperationCanceledException) when (stopping.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {

                    var last = policy.MaxAttempts is UInt32 max && attempt >= max;

                    await RaiseConnectFailed(exception, attempt, last ? null : policy.DelayForAttempt(attempt + 1)).ConfigureAwait(false);

                    if (last)
                        return;

                }

            }

        }

        #endregion

        #region (private) RaiseConnectFailed(Error, Attempt, NextAttemptIn)

        private async Task RaiseConnectFailed(Exception  Error,
                                              UInt32     Attempt,
                                              TimeSpan?  NextAttemptIn)
        {

            if (OnConnectFailed is null)
                return;

            foreach (var handler in OnConnectFailed.GetInvocationList().Cast<OnSshMessageConnectFailedDelegate>())
            {
                try
                {
                    await handler(Timestamp.Now, this, Error, Attempt, NextAttemptIn, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    Logger?.LogWarning(exception, "A handler of {Event} failed.", nameof(OnConnectFailed));
                }
            }

        }

        #endregion

    }

}
