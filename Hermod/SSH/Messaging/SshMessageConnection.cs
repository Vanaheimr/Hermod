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

using System.Text;
using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.Messaging;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    #region Delegates

    /// <summary>
    /// A message connection over an SSH channel was made.
    /// </summary>
    public delegate Task OnSshMessageConnectionDelegate        (DateTimeOffset        Timestamp,
                                                                ASshMessageEndpoint   Sender,
                                                                SshMessageConnection  Connection,
                                                                CancellationToken     CancellationToken);

    /// <summary>
    /// A text message was received.
    /// </summary>
    public delegate Task OnSshTextMessageReceivedDelegate      (DateTimeOffset        Timestamp,
                                                                ASshMessageEndpoint   Sender,
                                                                SshMessageConnection  Connection,
                                                                String                Text,
                                                                CancellationToken     CancellationToken);

    /// <summary>
    /// A binary message was received.
    /// </summary>
    public delegate Task OnSshBinaryMessageReceivedDelegate    (DateTimeOffset        Timestamp,
                                                                ASshMessageEndpoint   Sender,
                                                                SshMessageConnection  Connection,
                                                                Byte[]                Bytes,
                                                                CancellationToken     CancellationToken);

    /// <summary>
    /// A text message was sent - or was meant to be, as the status says.
    /// </summary>
    public delegate Task OnSshTextMessageSentDelegate          (DateTimeOffset        Timestamp,
                                                                ASshMessageEndpoint   Sender,
                                                                SshMessageConnection  Connection,
                                                                String                Text,
                                                                SentStatus            SentStatus,
                                                                CancellationToken     CancellationToken);

    /// <summary>
    /// A binary message was sent - or was meant to be, as the status says.
    /// </summary>
    public delegate Task OnSshBinaryMessageSentDelegate        (DateTimeOffset        Timestamp,
                                                                ASshMessageEndpoint   Sender,
                                                                SshMessageConnection  Connection,
                                                                Byte[]                Bytes,
                                                                SentStatus            SentStatus,
                                                                CancellationToken     CancellationToken);

    /// <summary>
    /// The answer to a PING arrived.
    /// </summary>
    public delegate Task OnSshPongReceivedDelegate             (DateTimeOffset        Timestamp,
                                                                ASshMessageEndpoint   Sender,
                                                                SshMessageConnection  Connection,
                                                                Byte[]                Payload,
                                                                CancellationToken     CancellationToken);

    /// <summary>
    /// A message connection over an SSH channel ended.
    /// </summary>
    public delegate Task OnSshMessageConnectionClosedDelegate  (DateTimeOffset        Timestamp,
                                                                ASshMessageEndpoint   Sender,
                                                                SshMessageConnection  Connection,
                                                                SshMessageCloseInfo   Close,
                                                                CancellationToken     CancellationToken);

    #endregion


    /// <summary>
    /// How a message connection over an SSH channel ended.
    /// </summary>
    /// <param name="Code">The close code of the CLOSE frame that ended it - or null where none did: the
    /// channel or the connection simply went.</param>
    /// <param name="Reason">The reason given, if any.</param>
    /// <param name="ByPeer">Whether the other side ended it.</param>
    public sealed record SshMessageCloseInfo(MessageCloseCode?  Code,
                                             String?            Reason,
                                             Boolean            ByPeer);


    /// <summary>
    /// A message connection over an SSH subsystem channel: a <see cref="IMessageConnection"/> whose
    /// <see cref="IMessageConnection.Transport"/> is <see cref="MessageTransport.SSH"/>, with what only an
    /// SSH channel has - the login, the host key, the channel itself.
    /// </summary>
    public interface ISshMessageConnection : IMessageConnection
    {

        /// <summary>
        /// The subsystem, e.g. "ocpp@open.charging.cloud".
        /// </summary>
        String                Subsystem          { get; }

        /// <summary>
        /// The SSH login: who signed in, as a server sees it; who this client signed in as.
        /// </summary>
        String                Username           { get; }

        /// <summary>
        /// On a server: who signed in - method, key, restrictions, peer. Null on a client.
        /// </summary>
        SshSessionInfo?       Session            { get; }

        /// <summary>
        /// On a client: the host key the server proved itself with. Null on a server.
        /// </summary>
        Byte[]?               ServerHostKey      { get; }

        /// <summary>
        /// The SSH channel the messages go over.
        /// </summary>
        SshMuxChannel         Channel            { get; }

        /// <summary>
        /// What the client said first.
        /// </summary>
        SshMessageHello       Hello              { get; }

        /// <summary>
        /// What the server answered.
        /// </summary>
        SshMessageWelcome     Welcome            { get; }

        /// <summary>
        /// The largest message this side takes.
        /// </summary>
        UInt64                MaxMessageSizeIn   { get; }

        /// <summary>
        /// The largest message this side sends: its own limit, or the other side's, whichever is smaller.
        /// </summary>
        UInt64                MaxMessageSizeOut  { get; }

        /// <summary>
        /// How the connection ended, once it has.
        /// </summary>
        SshMessageCloseInfo?  CloseInfo          { get; }

        /// <summary>
        /// Ask the other side for a PONG with the given payload.
        /// </summary>
        Task<SentStatus> SendPing(Byte[]?            Payload             = null,
                                  CancellationToken  CancellationToken   = default);

    }


    /// <summary>
    /// What a server and a client of messages over SSH channels have in common: their events.
    /// </summary>
    public abstract class ASshMessageEndpoint
    {

        #region Properties

        /// <summary>
        /// Where a failing event handler is logged; the other handlers run on regardless.
        /// </summary>
        public ILogger?  Logger  { get; init; }

        #endregion

        #region Events

        /// <summary>
        /// A connection was made: its HELLO answered with WELCOME.
        /// </summary>
        public event OnSshMessageConnectionDelegate?        OnNewConnection;

        /// <summary>
        /// A text message was received. Handlers run one after the other, and the connection reads its
        /// next message only after they all ran.
        /// </summary>
        public event OnSshTextMessageReceivedDelegate?      OnTextMessageReceived;

        /// <summary>
        /// A binary message was received. Handlers run one after the other, and the connection reads its
        /// next message only after they all ran.
        /// </summary>
        public event OnSshBinaryMessageReceivedDelegate?    OnBinaryMessageReceived;

        /// <summary>
        /// A text message was sent, or failed to be.
        /// </summary>
        public event OnSshTextMessageSentDelegate?          OnTextMessageSent;

        /// <summary>
        /// A binary message was sent, or failed to be.
        /// </summary>
        public event OnSshBinaryMessageSentDelegate?        OnBinaryMessageSent;

        /// <summary>
        /// The answer to a PING arrived.
        /// </summary>
        public event OnSshPongReceivedDelegate?             OnPongReceived;

        /// <summary>
        /// A connection ended.
        /// </summary>
        public event OnSshMessageConnectionClosedDelegate?  OnConnectionClosed;

        #endregion


        #region (internal) Raise...

        internal Task RaiseNewConnection(SshMessageConnection Connection, CancellationToken CancellationToken)
            => RaiseAsync(OnNewConnection, nameof(OnNewConnection), handler => handler(Timestamp.Now, this, Connection, CancellationToken));

        internal Task RaiseTextMessageReceived(SshMessageConnection Connection, String Text, CancellationToken CancellationToken)
            => RaiseAsync(OnTextMessageReceived, nameof(OnTextMessageReceived), handler => handler(Timestamp.Now, this, Connection, Text, CancellationToken));

        internal Task RaiseBinaryMessageReceived(SshMessageConnection Connection, Byte[] Bytes, CancellationToken CancellationToken)
            => RaiseAsync(OnBinaryMessageReceived, nameof(OnBinaryMessageReceived), handler => handler(Timestamp.Now, this, Connection, Bytes, CancellationToken));

        internal Task RaiseTextMessageSent(SshMessageConnection Connection, String Text, SentStatus SentStatus, CancellationToken CancellationToken)
            => RaiseAsync(OnTextMessageSent, nameof(OnTextMessageSent), handler => handler(Timestamp.Now, this, Connection, Text, SentStatus, CancellationToken));

        internal Task RaiseBinaryMessageSent(SshMessageConnection Connection, Byte[] Bytes, SentStatus SentStatus, CancellationToken CancellationToken)
            => RaiseAsync(OnBinaryMessageSent, nameof(OnBinaryMessageSent), handler => handler(Timestamp.Now, this, Connection, Bytes, SentStatus, CancellationToken));

        internal Task RaisePongReceived(SshMessageConnection Connection, Byte[] Payload, CancellationToken CancellationToken)
            => RaiseAsync(OnPongReceived, nameof(OnPongReceived), handler => handler(Timestamp.Now, this, Connection, Payload, CancellationToken));

        internal Task RaiseConnectionClosed(SshMessageConnection Connection, SshMessageCloseInfo Close, CancellationToken CancellationToken)
            => RaiseAsync(OnConnectionClosed, nameof(OnConnectionClosed), handler => handler(Timestamp.Now, this, Connection, Close, CancellationToken));

        /// <summary>
        /// Every handler, one after the other: one that fails is logged, and the others run on.
        /// </summary>
        private async Task RaiseAsync<TDelegate>(TDelegate?             Event,
                                                 String                 Name,
                                                 Func<TDelegate, Task>  Invoke)

            where TDelegate : Delegate

        {

            if (Event is null)
                return;

            foreach (var handler in Event.GetInvocationList().Cast<TDelegate>())
            {
                try
                {
                    await Invoke(handler).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    Logger?.LogWarning(exception, "A handler of {Event} failed.", Name);
                }
            }

        }

        #endregion

    }


    /// <summary>
    /// A message connection over an SSH subsystem channel - see README.md beside this file for the frames.
    /// A server makes one for every client it welcomes (<see cref="SshMessageServer"/>), a client one for
    /// every connection it makes (<see cref="SshMessageClient"/>).
    /// </summary>
    public sealed class SshMessageConnection : ISshMessageConnection
    {

        #region Data

        private static readonly UTF8Encoding                         StrictUTF8  = new (encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        private readonly ASshMessageEndpoint                         endpoint;
        private readonly SemaphoreSlim                               sendLock    = new (1, 1);
        private readonly CancellationTokenSource                     lifetime    = new ();
        private readonly ConcurrentDictionary<String, Object?>       customData  = new ();

        private          Int32                                       closeSent;
        private          Int32                                       ended;
        private          Int64                                       messagesSent;
        private          Int64                                       messagesReceived;
        private          Int64                                       lastSentTicks;
        private          Int64                                       lastReceivedTicks;
        private volatile SshMessageCloseInfo?                        closeInfo;

        #endregion

        #region Properties

        /// <summary>
        /// Messages go over an SSH channel.
        /// </summary>
        public MessageTransport      Transport          => MessageTransport.SSH;

        /// <inheritdoc/>
        public ConnectionRole        Role               { get; }

        /// <summary>
        /// The subprotocol the server chose in its WELCOME, if it chose one.
        /// </summary>
        public String?               Subprotocol        => Welcome.Subprotocol;

        /// <inheritdoc/>
        public String                Subsystem          { get; }

        /// <inheritdoc/>
        public String                Username           { get; }

        /// <inheritdoc/>
        public SshSessionInfo?       Session            { get; }

        /// <inheritdoc/>
        public Byte[]?               ServerHostKey      { get; }

        /// <inheritdoc/>
        public SshMuxChannel         Channel            { get; }

        /// <inheritdoc/>
        public SshMessageHello       Hello              { get; }

        /// <inheritdoc/>
        public SshMessageWelcome     Welcome            { get; }

        /// <inheritdoc/>
        public UInt64                MaxMessageSizeIn   { get; }

        /// <inheritdoc/>
        public UInt64                MaxMessageSizeOut  { get; }

        /// <inheritdoc/>
        public IPSocket              LocalSocket        { get; }

        /// <inheritdoc/>
        public IPSocket              RemoteSocket       { get; }

        /// <inheritdoc/>
        public DateTimeOffset        ConnectedSince     { get; }

        /// <inheritdoc/>
        public DateTimeOffset?       LastSentTimestamp
            => Interlocked.Read(ref lastSentTicks)     is var ticks && ticks != 0 ? new DateTimeOffset(ticks, TimeSpan.Zero) : null;

        /// <inheritdoc/>
        public DateTimeOffset?       LastReceivedTimestamp
            => Interlocked.Read(ref lastReceivedTicks) is var ticks && ticks != 0 ? new DateTimeOffset(ticks, TimeSpan.Zero) : null;

        /// <summary>
        /// Whether the connection lasts: no CLOSE was sent or received, and the channel is open.
        /// </summary>
        public Boolean               IsAlive            => Volatile.Read(ref ended) == 0 && Volatile.Read(ref closeSent) == 0;

        /// <inheritdoc/>
        public UInt64                MessagesReceivedCounter
            => (UInt64) Interlocked.Read(ref messagesReceived);

        /// <inheritdoc/>
        public UInt64                MessagesSentCounter
            => (UInt64) Interlocked.Read(ref messagesSent);

        /// <inheritdoc/>
        public SshMessageCloseInfo?  CloseInfo          => closeInfo;

        /// <summary>
        /// Fires once the connection has ended.
        /// </summary>
        public CancellationToken     Ended              => lifetime.Token;

        #endregion

        #region Constructor(s)

        internal SshMessageConnection(ASshMessageEndpoint  Endpoint,
                                      ConnectionRole       Role,
                                      String               Subsystem,
                                      String               Username,
                                      SshSessionInfo?      Session,
                                      Byte[]?              ServerHostKey,
                                      SshMuxChannel        Channel,
                                      IPSocket             LocalSocket,
                                      IPSocket             RemoteSocket,
                                      SshMessageHello      Hello,
                                      SshMessageWelcome    Welcome,
                                      UInt64               MaxMessageSizeIn,
                                      UInt64               MaxMessageSizeOut)
        {

            this.endpoint           = Endpoint;
            this.Role               = Role;
            this.Subsystem          = Subsystem;
            this.Username           = Username;
            this.Session            = Session;
            this.ServerHostKey      = ServerHostKey;
            this.Channel            = Channel;
            this.LocalSocket        = LocalSocket;
            this.RemoteSocket       = RemoteSocket;
            this.Hello              = Hello;
            this.Welcome            = Welcome;
            this.MaxMessageSizeIn   = Math.Min(MaxMessageSizeIn,  SshMessageFraming.LargestMaxMessageSize);
            this.MaxMessageSizeOut  = Math.Min(MaxMessageSizeOut, SshMessageFraming.LargestMaxMessageSize);
            this.ConnectedSince     = Timestamp.Now;

        }

        #endregion


        #region SendTextMessage  (Text,  CancellationToken = default)

        /// <summary>
        /// Send a text message - unless it is larger than <see cref="MaxMessageSizeOut"/>, or not text
        /// UTF-8 can carry: then nothing is sent, and the status says <see cref="SentStatus.Error"/>.
        /// </summary>
        /// <param name="Text">The text.</param>
        /// <param name="CancellationToken">Cancels waiting for an earlier message to be sent; a message
        /// that is being sent is sent whole, as half of one would break the channel.</param>
        public async Task<SentStatus> SendTextMessage(String             Text,
                                                      CancellationToken  CancellationToken   = default)
        {

            SentStatus status;

            try
            {

                var bytes = StrictUTF8.GetBytes(Text);

                status = (UInt64) bytes.LongLength > MaxMessageSizeOut
                             ? SentStatus.Error
                             : await SendFrameAsync(SshMessageFrameType.Text, bytes, CancellationToken).ConfigureAwait(false);

            }
            catch (EncoderFallbackException)
            {
                status = SentStatus.Error;
            }

            if (status == SentStatus.Success)
                Interlocked.Increment(ref messagesSent);

            await endpoint.RaiseTextMessageSent(this, Text, status, CancellationToken).ConfigureAwait(false);

            return status;

        }

        #endregion

        #region SendBinaryMessage(Bytes, CancellationToken = default)

        /// <summary>
        /// Send a binary message - unless it is larger than <see cref="MaxMessageSizeOut"/>: then nothing
        /// is sent, and the status says <see cref="SentStatus.Error"/>.
        /// </summary>
        /// <param name="Bytes">The bytes.</param>
        /// <param name="CancellationToken">Cancels waiting for an earlier message to be sent; a message
        /// that is being sent is sent whole, as half of one would break the channel.</param>
        public async Task<SentStatus> SendBinaryMessage(Byte[]             Bytes,
                                                        CancellationToken  CancellationToken   = default)
        {

            var status = (UInt64) Bytes.LongLength > MaxMessageSizeOut
                             ? SentStatus.Error
                             : await SendFrameAsync(SshMessageFrameType.Binary, Bytes, CancellationToken).ConfigureAwait(false);

            if (status == SentStatus.Success)
                Interlocked.Increment(ref messagesSent);

            await endpoint.RaiseBinaryMessageSent(this, Bytes, status, CancellationToken).ConfigureAwait(false);

            return status;

        }

        #endregion

        #region SendPing         (Payload = null, CancellationToken = default)

        /// <inheritdoc/>
        public Task<SentStatus> SendPing(Byte[]?            Payload             = null,
                                         CancellationToken  CancellationToken   = default)

            => (Payload?.Length ?? 0) > SshMessageFraming.MaxControlPayload
                   ? Task.FromResult(SentStatus.Error)
                   : SendFrameAsync(SshMessageFrameType.Ping, Payload ?? [], CancellationToken);

        #endregion

        #region Close            (Code, Reason = null, CancellationToken = default)

        /// <summary>
        /// End the connection: a CLOSE frame with the given code and reason, and the channel is closed.
        /// The other side's CLOSE in answer is not waited for - the channel's own close is a handshake.
        /// </summary>
        /// <param name="Code">Why.</param>
        /// <param name="Reason">An optional text for the other side.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task Close(MessageCloseCode   Code,
                                String?            Reason              = null,
                                CancellationToken  CancellationToken   = default)
        {

            if (Volatile.Read(ref ended) != 0)
                return;

            if (closeInfo is null)
                closeInfo = new SshMessageCloseInfo(Code, Reason, ByPeer: false);

            await SendFrameAsync(SshMessageFrameType.Close, SshMessageFraming.EncodeClose(Code, Reason), CancellationToken).ConfigureAwait(false);

            await EndAsync().ConfigureAwait(false);

        }

        #endregion


        #region (internal) RunAsync(Stopped)

        /// <summary>
        /// Read messages until the connection ends, raising the endpoint's events on the way - and close
        /// the channel at the end, whichever way it ended.
        /// </summary>
        /// <param name="Stopped">Ends reading at once: the channel or the connection went.</param>
        internal async Task RunAsync(CancellationToken Stopped)
        {

            using var reading = CancellationTokenSource.CreateLinkedTokenSource(Stopped, lifetime.Token);
            var       token   = reading.Token;

            try
            {

                while (true)
                {

                    var frame = await SshMessageFraming.ReadAsync(Channel.Input, MaxMessageSizeIn, token).ConfigureAwait(false);

                    if (frame is not SshMessageFrame received)
                    {
                        closeInfo ??= new SshMessageCloseInfo(null, "The channel ended without a CLOSE frame.", ByPeer: true);
                        return;
                    }

                    Interlocked.Exchange(ref lastReceivedTicks, Timestamp.Now.UtcTicks);

                    switch (received.Type)
                    {

                        case SshMessageFrameType.Text:

                            String text;

                            try
                            {
                                text = StrictUTF8.GetString(received.Payload);
                            }
                            catch (DecoderFallbackException)
                            {
                                throw new SshMessageProtocolException(MessageCloseCode.InvalidPayloadData, "A text message that is not UTF-8.");
                            }

                            Interlocked.Increment(ref messagesReceived);
                            await endpoint.RaiseTextMessageReceived(this, text, token).ConfigureAwait(false);
                            break;

                        case SshMessageFrameType.Binary:
                            Interlocked.Increment(ref messagesReceived);
                            await endpoint.RaiseBinaryMessageReceived(this, received.Payload, token).ConfigureAwait(false);
                            break;

                        case SshMessageFrameType.Ping:
                            await SendFrameAsync(SshMessageFrameType.Pong, received.Payload, token).ConfigureAwait(false);
                            break;

                        case SshMessageFrameType.Pong:
                            await endpoint.RaisePongReceived(this, received.Payload, token).ConfigureAwait(false);
                            break;

                        case SshMessageFrameType.Close:

                            var (code, reason) = SshMessageFraming.DecodeClose(received.Payload);

                            closeInfo ??= new SshMessageCloseInfo(code, reason, ByPeer: true);

                            // Answered with this side's own CLOSE, where it sent none: the same code, as
                            // RFC 6455 does it.
                            await SendFrameAsync(SshMessageFrameType.Close, SshMessageFraming.EncodeClose(code, null), CancellationToken.None).ConfigureAwait(false);
                            return;

                        case SshMessageFrameType.Hello:
                        case SshMessageFrameType.Welcome:
                        case SshMessageFrameType.Refused:
                            throw new SshMessageProtocolException(MessageCloseCode.ProtocolError, $"A {received.Type.ToString().ToUpperInvariant()} after the connection was made.");

                        default:

                            // Kinds from 0x80 on are for extensions a side may not know: skipped.
                            if ((Byte) received.Type < 0x80)
                                throw new SshMessageProtocolException(MessageCloseCode.ProtocolError, $"A frame of the unknown kind 0x{(Byte) received.Type:x2}.");

                            break;

                    }

                }

            }
            catch (SshMessageProtocolException exception)
            {

                closeInfo ??= new SshMessageCloseInfo(exception.Code, exception.Message, ByPeer: false);

                await SendFrameAsync(SshMessageFrameType.Close, SshMessageFraming.EncodeClose(exception.Code, exception.Message), CancellationToken.None).ConfigureAwait(false);

            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                closeInfo ??= new SshMessageCloseInfo(null, "The channel ended without a CLOSE frame.", ByPeer: true);
            }
            catch (Exception exception)
            {
                closeInfo ??= new SshMessageCloseInfo(null, exception.Message, ByPeer: true);
            }
            finally
            {
                await EndAsync().ConfigureAwait(false);
            }

        }

        #endregion

        #region (private)  SendFrameAsync(Type, Payload, CancellationToken)

        /// <summary>
        /// Send one frame, whole: one writer at a time, and nothing after a CLOSE. Once a frame is being
        /// sent, it is sent to its end or the connection ends - half a frame would break the channel for
        /// every frame after it.
        /// </summary>
        private async Task<SentStatus> SendFrameAsync(SshMessageFrameType  Type,
                                                      Byte[]               Payload,
                                                      CancellationToken    CancellationToken)
        {

            var frame = SshMessageFraming.Encode(Type, Payload);

            try
            {
                await sendLock.WaitAsync(CancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return SentStatus.Dropped;
            }

            try
            {

                if (Volatile.Read(ref ended) != 0 || Volatile.Read(ref closeSent) != 0)
                    return SentStatus.FatalError;

                if (Type == SshMessageFrameType.Close)
                    Volatile.Write(ref closeSent, 1);

                await Channel.SendDataAsync(frame, lifetime.Token).ConfigureAwait(false);

                Interlocked.Exchange(ref lastSentTicks, Timestamp.Now.UtcTicks);

                return SentStatus.Success;

            }
            catch (Exception)
            {
                return SentStatus.FatalError;
            }
            finally
            {
                sendLock.Release();
            }

        }

        #endregion

        #region (private)  EndAsync()

        /// <summary>
        /// Once: mark the connection ended, and close the channel.
        /// </summary>
        private async Task EndAsync()
        {

            if (Interlocked.Exchange(ref ended, 1) != 0)
                return;

            try
            {
                await Channel.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            { }

            try
            {
                await lifetime.CancelAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            { }

        }

        #endregion


        #region Custom data

        /// <inheritdoc/>
        public Boolean TryAddCustomData(String   Key,
                                        Object?  Value)

            => customData.TryAdd(Key, Value);

        /// <inheritdoc/>
        public Boolean TryAddCustomData(String   Key,
                                        Object?  NewValue,
                                        Object?  ComparisonValue)

            => customData.TryUpdate(Key, NewValue, ComparisonValue);

        /// <inheritdoc/>
        public Boolean HasCustomData(String Key)
            => customData.ContainsKey(Key);

        /// <inheritdoc/>
        public Object? TryGetCustomData(String Key)
            => customData.TryGetValue(Key, out var data) ? data : null;

        /// <inheritdoc/>
        public T? TryGetCustomDataAs<T>(String Key)
            where T : struct

            => customData.TryGetValue(Key, out var data) && data is T value ? value : null;

        /// <inheritdoc/>
        public Boolean TryGetCustomData(String       Key,
                                        out Object?  Value)

            => customData.TryGetValue(Key, out Value);

        /// <inheritdoc/>
        public Boolean TryGetCustomDataAs<T>(String  Key,
                                             out T?  Value)
        {

            if (customData.TryGetValue(Key, out var data) && data is T value)
            {
                Value = value;
                return true;
            }

            Value = default;
            return false;

        }

        #endregion

        #region (override) ToString()

        /// <inheritdoc/>
        public override String ToString()

            => $"{Subsystem} {(Role == ConnectionRole.Server ? "from" : "as")} '{Username}' ({RemoteSocket}){(Subprotocol is null ? "" : $", {Subprotocol}")}";

        #endregion

    }

}
