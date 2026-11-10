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

using System.Collections.Concurrent;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.Messaging;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// What a server's <see cref="SshMessageServer.ValidateHello"/> decided about a client.
    /// </summary>
    public sealed class SshHelloVerdict
    {

        /// <summary>
        /// Whether the client is served.
        /// </summary>
        public Boolean                                       Accepted         { get; }

        /// <summary>
        /// Why not: a REFUSED frame's code, where the client is not served.
        /// </summary>
        public String?                                       RefusalCode      { get; }

        /// <summary>
        /// Why not, in words, where the client is not served.
        /// </summary>
        public String?                                       Description      { get; }

        /// <summary>
        /// What the WELCOME tells besides the subprotocol and the limit.
        /// </summary>
        public JObject?                                      WelcomeProperties { get; }

        /// <summary>
        /// What the connection carries as custom data from the start - e.g. who the client acts for.
        /// </summary>
        public IReadOnlyDictionary<String, Object?>?         CustomData       { get; }

        /// <summary>
        /// After how long the client may try again, where it is not served now but may be later.
        /// </summary>
        public TimeSpan?                                     RetryAfter       { get; }

        private SshHelloVerdict(Boolean Accepted, String? RefusalCode, String? Description, JObject? WelcomeProperties, IReadOnlyDictionary<String, Object?>? CustomData, TimeSpan? RetryAfter = null)
        {
            this.Accepted           = Accepted;
            this.RefusalCode        = RefusalCode;
            this.Description        = Description;
            this.WelcomeProperties  = WelcomeProperties;
            this.CustomData         = CustomData;
            this.RetryAfter         = RetryAfter;
        }

        /// <summary>
        /// Serve the client.
        /// </summary>
        /// <param name="WelcomeProperties">What the WELCOME tells besides the subprotocol and the limit.</param>
        /// <param name="CustomData">What the connection carries as custom data from the start.</param>
        public static SshHelloVerdict Accept(JObject?                               WelcomeProperties  = null,
                                             IReadOnlyDictionary<String, Object?>?  CustomData         = null)

            => new (true, null, null, WelcomeProperties, CustomData);

        /// <summary>
        /// Do not serve the client: it is told so with a REFUSED frame, and the channel is closed.
        /// </summary>
        /// <param name="Code">Why, as a code the client can act on.</param>
        /// <param name="Description">Why, in words.</param>
        /// <param name="RetryAfter">After how long the client may try again, as "retryAfter" in whole
        /// seconds; null for a refusal that is final.</param>
        public static SshHelloVerdict Refuse(String     Code,
                                             String?    Description   = null,
                                             TimeSpan?  RetryAfter    = null)

            => new (false, Code, Description, null, null, RetryAfter);

    }


    /// <summary>
    /// Asked, before a client is welcomed, whether to serve it - with who signed in, what it said, and the
    /// subprotocol the server chose.
    /// </summary>
    public delegate ValueTask<SshHelloVerdict> SshHelloValidator(SshMessageServer     Server,
                                                                 SshSubsystemContext  Context,
                                                                 SshMessageHello      Hello,
                                                                 String?              Subprotocol,
                                                                 CancellationToken    CancellationToken);


    /// <summary>
    /// How an <see cref="SshMessageServer"/> serves its subsystem.
    /// </summary>
    public sealed record SshMessageServerOptions
    {

        /// <summary>
        /// The subsystem served, e.g. "ocpp@open.charging.cloud".
        /// </summary>
        public required String               Subsystem          { get; init; }

        /// <summary>
        /// The subprotocols served. A client that offers some, and none of these, is refused; one that
        /// offers none is served without one, unless <see cref="SshMessageServer.ValidateHello"/> says otherwise.
        /// </summary>
        public IReadOnlyList<String>         Subprotocols       { get; init; } = [];

        /// <summary>
        /// The largest message the server takes - told to every client in its WELCOME.
        /// </summary>
        public UInt64                        MaxMessageSizeIn   { get; init; } = SshMessageFraming.DefaultMaxMessageSize;

        /// <summary>
        /// The largest message the server sends - or the client's limit, where that is smaller.
        /// </summary>
        public UInt64                        MaxMessageSizeOut  { get; init; } = SshMessageFraming.DefaultMaxMessageSize;

        /// <summary>
        /// How long a client has, after the subsystem was granted, to send its HELLO.
        /// </summary>
        public TimeSpan                      HelloTimeout       { get; init; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Every how long a PING is sent on every connection; null for none.
        /// </summary>
        public TimeSpan?                     PingEvery          { get; init; }

        /// <summary>
        /// After how many <see cref="PingEvery"/> intervals without any frame from a client - a message, a
        /// PONG or anything else - the client is taken for dead and its channel is closed, without a CLOSE
        /// it could not read: 3 by default, as for a WebSocket; 0 never to give up on it.
        /// </summary>
        public UInt32                        MaxOutstandingPings { get; init; } = DefaultMaxOutstandingPings;

        /// <summary>
        /// The default of <see cref="MaxOutstandingPings"/>: 3.
        /// </summary>
        public const UInt32                  DefaultMaxOutstandingPings = 3;

    }


    /// <summary>
    /// Serves messages over one subsystem of an SSH server: hand <see cref="Handler"/> to the server's
    /// <see cref="SshServerOptions.Subsystems"/> factory for the name in <see cref="SshMessageServerOptions.Subsystem"/>.
    /// Every client says HELLO, is welcomed or refused, and then exchanges messages - see README.md.
    /// </summary>
    public sealed class SshMessageServer : ASshMessageEndpoint
    {

        #region Data

        private readonly ConcurrentDictionary<SshMessageConnection, Byte>  connections = new ();

        #endregion

        #region Properties

        /// <summary>
        /// How the subsystem is served.
        /// </summary>
        public SshMessageServerOptions            Options        { get; }

        /// <summary>
        /// The subsystem served.
        /// </summary>
        public String                             Subsystem      => Options.Subsystem;

        /// <summary>
        /// Asked, before a client is welcomed, whether to serve it. None, and every client is served.
        /// </summary>
        public SshHelloValidator?                 ValidateHello  { get; set; }

        /// <summary>
        /// The connections served now.
        /// </summary>
        public IEnumerable<SshMessageConnection>  Connections    => connections.Keys;

        /// <summary>
        /// What serves the subsystem on a channel: for an <see cref="SshServerOptions.Subsystems"/> factory.
        /// </summary>
        public SshSubsystemHandler                Handler        => ServeAsync;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// A server of messages over the given subsystem.
        /// </summary>
        public SshMessageServer(SshMessageServerOptions Options)
        {
            this.Options = Options;
        }

        #endregion


        #region ServeAsync(Context, CancellationToken)

        /// <summary>
        /// Serve one client on its channel: HELLO, WELCOME or REFUSED, and then its messages until the
        /// connection ends. A server that stops tells its clients with CLOSE (going away).
        /// </summary>
        public async ValueTask ServeAsync(SshSubsystemContext  Context,
                                          CancellationToken    CancellationToken)
        {

            var channel = Context.Channel;

            #region The client's HELLO

            SshMessageFrame? first;

            using (var helloWithin = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken))
            {

                helloWithin.CancelAfter(Options.HelloTimeout);

                try
                {
                    first = await SshMessageFraming.ReadAsync(channel.Input, 0, helloWithin.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Too late, or gone: the channel is closed once this returns.
                    return;
                }
                catch (SshMessageProtocolException exception)
                {
                    await RefuseAsync(channel, SshMessageRefusal.InvalidHello, exception.Message).ConfigureAwait(false);
                    return;
                }
                catch (Exception)
                {
                    return;
                }

            }

            if (first is not SshMessageFrame frame)
                return;

            #endregion

            await ServeAfterAsync(Context, frame, CancellationToken).ConfigureAwait(false);

        }

        #endregion

        #region ServeAfterAsync(Context, First, CancellationToken)

        /// <summary>
        /// Serve one client whose first frame was read already - by a handler in front of several servers
        /// that read the HELLO to choose one of them (see <see cref="RoutedBySubprotocol"/>).
        /// </summary>
        /// <param name="Context">The client's channel.</param>
        /// <param name="First">The client's first frame, read already: its HELLO, or what came instead.</param>
        /// <param name="CancellationToken">Ends serving it.</param>
        public async ValueTask ServeAfterAsync(SshSubsystemContext  Context,
                                               SshMessageFrame      First,
                                               CancellationToken    CancellationToken)
        {

            var channel  = Context.Channel;
            var frame    = First;

            #region The client's HELLO

            if (frame.Type != SshMessageFrameType.Hello)
            {
                await RefuseAsync(channel, SshMessageRefusal.HelloExpected, $"Expected HELLO, not a frame of kind 0x{(Byte) frame.Type:x2}.").ConfigureAwait(false);
                return;
            }

            SshMessageHello hello;

            try
            {
                hello = SshMessageHello.Parse(frame.Payload);
            }
            catch (SshMessageProtocolException exception)
            {
                await RefuseAsync(channel, SshMessageRefusal.InvalidHello, exception.Message).ConfigureAwait(false);
                return;
            }

            #endregion

            #region The subprotocol: the client's first choice the server speaks

            String? subprotocol = null;

            if (hello.Subprotocols.Count > 0 && Options.Subprotocols.Count > 0)
            {

                subprotocol = hello.Subprotocols.FirstOrDefault(offered => Options.Subprotocols.Contains(offered, StringComparer.Ordinal));

                if (subprotocol is null)
                {
                    await RefuseAsync(channel,
                                      SshMessageRefusal.UnsupportedSubprotocol,
                                      $"This subsystem speaks {String.Join(", ", Options.Subprotocols.Select(id => $"'{id}'"))}.").ConfigureAwait(false);
                    return;
                }

            }

            #endregion

            #region The application's verdict

            var verdict = SshHelloVerdict.Accept();

            if (ValidateHello is SshHelloValidator validate)
            {
                try
                {
                    verdict = await validate(this, Context, hello, subprotocol, CancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    verdict = SshHelloVerdict.Refuse("refused", $"The server could not decide: {exception.Message}");
                }
            }

            if (!verdict.Accepted)
            {
                await RefuseAsync(channel, verdict.RefusalCode ?? "refused", verdict.Description, verdict.RetryAfter).ConfigureAwait(false);
                return;
            }

            #endregion

            #region WELCOME, and the connection

            var welcome = SshMessageWelcome.Create(subprotocol, Options.MaxMessageSizeIn, verdict.WelcomeProperties);

            try
            {
                await channel.SendDataAsync(SshMessageFraming.Encode(SshMessageFrameType.Welcome, welcome.ToBytes()), CancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                return;
            }

            var connection = new SshMessageConnection(this,
                                                      ConnectionRole.Server,
                                                      Context.Name,
                                                      Context.Session.Username,
                                                      Context.Session,
                                                      null,
                                                      channel,
                                                      Context.Session.Local ?? IPSocket.Zero,
                                                      Context.Session.Peer  ?? IPSocket.Zero,
                                                      hello,
                                                      welcome,
                                                      Options.MaxMessageSizeIn,
                                                      Math.Min(Options.MaxMessageSizeOut, hello.MaxMessageSize ?? Options.MaxMessageSizeOut));

            if (verdict.CustomData is not null)
                foreach (var (key, value) in verdict.CustomData)
                    connection.TryAddCustomData(key, value);

            #endregion

            connections.TryAdd(connection, 0);

            // A server that stops says goodbye; the read loop below then ends with the channel.
            using var stopping = Context.Stopping.Register(() => _ = connection.Close(MessageCloseCode.GoingAway, "The server is stopping."));

            try
            {

                await RaiseNewConnection(connection, CancellationToken).ConfigureAwait(false);

                var pinging = Options.PingEvery is TimeSpan every
                                  ? PingAsync(connection, every, Options.MaxOutstandingPings)
                                  : Task.CompletedTask;

                await connection.RunAsync(Context.Closed).ConfigureAwait(false);

                await pinging.ConfigureAwait(false);

            }
            finally
            {

                connections.TryRemove(connection, out _);

                await RaiseConnectionClosed(connection,
                                            connection.CloseInfo ?? new SshMessageCloseInfo(null, null, ByPeer: true),
                                            CancellationToken.None).ConfigureAwait(false);

            }

        }

        #endregion

        #region (static) RoutedBySubprotocol(Servers)

        /// <summary>
        /// One handler in front of several servers of the same subsystem - one per node that speaks a
        /// version, say: it reads the HELLO, and hands the client to the first server that speaks the
        /// client's first choice it can serve, as that server would have chosen it. A client that offers
        /// none of them is refused with "unsupported-subprotocol", naming what they speak together; one
        /// that offers none at all goes to the first server.
        /// </summary>
        /// <remarks>
        /// For "OCPP over SSH" a charging station's commissioning listener: OCPP 2.x and OCPP 1.6 are two
        /// nodes, and a subsystem has one handler (white paper "OCPP over SSH", decision 13).
        /// </remarks>
        /// <param name="Servers">The servers, by preference where a client's choice is spoken by several.</param>
        public static SshSubsystemHandler RoutedBySubprotocol(params SshMessageServer[] Servers)

            => async (context, cancellationToken) => {

                   if (Servers.Length == 0)
                       return;

                   var helloTimeout = Servers[0].Options.HelloTimeout;

                   SshMessageFrame? first;

                   using (var helloWithin = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                   {

                       helloWithin.CancelAfter(helloTimeout);

                       try
                       {
                           first = await SshMessageFraming.ReadAsync(context.Channel.Input, 0, helloWithin.Token).ConfigureAwait(false);
                       }
                       catch (OperationCanceledException)
                       {
                           return;
                       }
                       catch (SshMessageProtocolException exception)
                       {
                           await RefuseAsync(context.Channel, SshMessageRefusal.InvalidHello, exception.Message).ConfigureAwait(false);
                           return;
                       }
                       catch (Exception)
                       {
                           return;
                       }

                   }

                   if (first is not SshMessageFrame frame)
                       return;

                   // A frame that is no HELLO, or one that cannot be read, is the first server's to refuse.
                   var chosen = Servers[0];

                   if (frame.Type == SshMessageFrameType.Hello)
                   {

                       try
                       {

                           var hello = SshMessageHello.Parse(frame.Payload);

                           if (hello.Subprotocols.Count > 0)
                           {

                               var serving = hello.Subprotocols.
                                                 Select (offered => Servers.FirstOrDefault(server => server.Options.Subprotocols.Contains(offered, StringComparer.Ordinal))).
                                                 FirstOrDefault(server => server is not null);

                               if (serving is null)
                               {
                                   await RefuseAsync(context.Channel,
                                                     SshMessageRefusal.UnsupportedSubprotocol,
                                                     $"This subsystem speaks {String.Join(", ", Servers.SelectMany(server => server.Options.Subprotocols).Distinct(StringComparer.Ordinal).Select(id => $"'{id}'"))}.").ConfigureAwait(false);
                                   return;
                               }

                               chosen = serving;

                           }

                       }
                       catch (SshMessageProtocolException)
                       { }

                   }

                   await chosen.ServeAfterAsync(context, frame, cancellationToken).ConfigureAwait(false);

               };

        #endregion

        #region (private static) RefuseAsync(Channel, Code, Description)

        /// <summary>
        /// Tell the client why it is not served; the channel is closed once the handler returns.
        /// </summary>
        private static async Task RefuseAsync(SshMuxChannel  Channel,
                                              String         Code,
                                              String?        Description,
                                              TimeSpan?      RetryAfter   = null)
        {
            try
            {
                await Channel.SendDataAsync(SshMessageFraming.Encode(SshMessageFrameType.Refused, SshMessageRefusal.Encode(Code, Description, RetryAfter))).ConfigureAwait(false);
            }
            catch
            { }
        }

        #endregion

        #region (internal static) PingAsync(Connection, Every, MaxOutstandingPings)

        /// <summary>
        /// A PING every so often, until the connection ends - carrying when it was sent. A peer from which
        /// nothing has come for <paramref name="MaxOutstandingPings"/> intervals - no message, no PONG, no
        /// frame at all - is taken for dead, and the connection is ended without a CLOSE (see
        /// <see cref="SshMessageConnection.AbortAsync"/>), as a WebSocket of Hermod ends one whose pongs
        /// stay away. Every frame counts, not only a PONG: a PONG may wait behind a long message.
        /// </summary>
        internal static async Task PingAsync(SshMessageConnection  Connection,
                                             TimeSpan              Every,
                                             UInt32                MaxOutstandingPings   = SshMessageServerOptions.DefaultMaxOutstandingPings)
        {
            try
            {
                while (true)
                {

                    await Task.Delay(Every, Connection.Ended).ConfigureAwait(false);

                    var silence = Timestamp.Now - (Connection.LastReceivedTimestamp ?? Connection.ConnectedSince);

                    if (MaxOutstandingPings > 0 && silence > TimeSpan.FromTicks(Every.Ticks * MaxOutstandingPings))
                    {
                        await Connection.AbortAsync($"Nothing came from the peer for {silence.TotalSeconds:0} s, {MaxOutstandingPings} PINGs of {Every.TotalSeconds:0.###} s.").ConfigureAwait(false);
                        return;
                    }

                    await Connection.SendPing(BitConverter.GetBytes(DateTimeOffset.UtcNow.UtcTicks), Connection.Ended).ConfigureAwait(false);

                }
            }
            catch (OperationCanceledException)
            { }
        }

        #endregion

    }

}
