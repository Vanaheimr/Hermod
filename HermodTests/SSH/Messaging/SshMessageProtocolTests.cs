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
using System.Buffers.Binary;
using System.Collections.Concurrent;

using NUnit.Framework;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Client;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Server;
using org.GraphDefined.Vanaheimr.Hermod.Messaging;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// The message protocol over an SSH subsystem, end to end: an <see cref="SshServer"/> serving
    /// "ocpp@open.charging.cloud" with an <see cref="SshMessageServer"/>, and an <see cref="SshMessageClient"/>
    /// signing in as a charging station. HELLO, WELCOME and REFUSED; messages both ways; the limits; the
    /// close handshake; and reconnecting.
    /// </summary>
    [TestFixture]
    public class SshMessageProtocolTests
    {

        #region (private) Fixture

        private const String OCPP = "ocpp@open.charging.cloud";

        private readonly ISshHostKey  hostKey     = SshHostKey.GenerateEd25519();
        private readonly ISshHostKey  stationKey  = SshHostKey.GenerateEd25519();

        private sealed record Running(SshServer Server, UInt16 Port, SshMessageServer Messages, CollectingAuditSink Audit) : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => Server.DisposeAsync();
        }

        private async Task<Running> StartAsync(CancellationToken                                  CancellationToken,
                                               Func<SshMessageServerOptions, SshMessageServerOptions>?  Configure  = null,
                                               SshHelloValidator?                                  Validate   = null,
                                               (String Name, SshSubsystemHandler Handler)?         Further    = null)
        {

            var messages = new SshMessageServer((Configure ?? (o => o))(new SshMessageServerOptions {
                                                    Subsystem     = OCPP,
                                                    Subprotocols  = [ "ocpp2.0.1", "ocpp2.1" ]
                                                })) {
                               ValidateHello = Validate
                           };

            var audit    = new CollectingAuditSink();

            var server   = new SshServer(new SshServerOptions {
                               HostKeys       = [ hostKey ],
                               Authenticator  = SshUserAuthenticator.ForAuthorizedKeys(stationKey.PublicKeyBlob),
                               AuditSink      = audit,
                               Subsystems     = (session, name, ct) => ValueTask.FromResult(name == OCPP
                                                                                            ? messages.Handler
                                                                                            : Further is { } further && name == further.Name
                                                                                                  ? further.Handler
                                                                                                  : null)
                           });

            await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);

            return new Running(server, (UInt16) server.LocalEndPoint.Port.ToInt32(), messages, audit);

        }

        private SshClientOptions Login(String Username = "cs001")

            => new () {
                   Username       = Username,
                   VerifyHostKey  = blob => blob.AsSpan().SequenceEqual(hostKey.PublicKeyBlob),
                   Credentials    = [ stationKey ]
               };

        private SshMessageClient Client(Running                                               Running,
                                        Func<SshMessageClientOptions, SshMessageClientOptions>?  Configure  = null,
                                        String                                                Username   = "cs001")

            => new ((Configure ?? (o => o))(new SshMessageClientOptions {
                       Host          = "127.0.0.1",
                       Port          = Running.Port,
                       Ssh           = Login(Username),
                       Subsystem     = OCPP,
                       Subprotocols  = [ "ocpp2.1", "ocpp2.0.1" ]
                   }));

        private static async Task<T> Within<T>(Task<T> Happening, String What)
        {
            if (await Task.WhenAny(Happening, Task.Delay(TimeSpan.FromSeconds(10))) != Happening)
                Assert.Fail($"{What} did not happen within 10 s.");
            return await Happening;
        }

        private static async Task Within(Task Happening, String What)
        {
            if (await Task.WhenAny(Happening, Task.Delay(TimeSpan.FromSeconds(10))) != Happening)
                Assert.Fail($"{What} did not happen within 10 s.");
            await Happening;
        }

        /// <summary>
        /// The server side of the client's connection, once the server registered it.
        /// </summary>
        private static Task<SshMessageConnection> ServerSideOf(Running Running)

            => Within(Task.Run(async () => {
                   while (!Running.Messages.Connections.Any())
                       await Task.Delay(10);
                   return Running.Messages.Connections.First();
               }), "The server's connection");

        /// <summary>
        /// A raw channel to the subsystem, for frames no well-behaved client sends.
        /// </summary>
        private async Task<(SshClient Ssh, SshMuxChannel Channel)> RawAsync(Running Running, CancellationToken CancellationToken)
        {
            var ssh     = await SshClient.ConnectAsync("127.0.0.1", Running.Port, Login(), CancellationToken);
            var channel = await ssh.OpenSubsystemAsync(OCPP, CancellationToken);
            return (ssh, channel);
        }

        private static ValueTask SendAsync(SshMuxChannel Channel, SshMessageFrameType Type, Byte[] Payload)
            => Channel.SendDataAsync(SshMessageFraming.Encode(Type, Payload));

        /// <summary>
        /// What the server sends on a raw channel, frame by frame, to its end.
        /// </summary>
        private static async Task<List<SshMessageFrame>> FramesToTheEndAsync(SshMuxChannel Channel)
        {
            var frames = new List<SshMessageFrame>();
            using var within = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (await SshMessageFraming.ReadAsync(Channel.Input, 1024 * 1024, within.Token) is SshMessageFrame frame)
                frames.Add(frame);
            return frames;
        }

        private static Byte[] Hello(params String[] Subprotocols)
            => SshMessageHello.Create(Subprotocols, null).ToBytes();

        #endregion


        #region Connect_ChoosesTheClientsFirstSubprotocolTheServerSpeaks

        [Test]
        [CancelAfter(30000)]
        public async Task Connect_ChoosesTheClientsFirstSubprotocolTheServerSpeaks(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);
            await using var client  = Client(running);

            var connection = await client.ConnectAsync(CancellationToken);
            var serverSide = await ServerSideOf(running);

            Assert.Multiple(() => {

                Assert.That(connection.Transport,      Is.EqualTo(MessageTransport.SSH));
                Assert.That(connection.Role,           Is.EqualTo(ConnectionRole.Client));
                Assert.That(connection.Subprotocol,    Is.EqualTo("ocpp2.1"), "the client's first choice, which the server speaks too");
                Assert.That(connection.Subsystem,      Is.EqualTo(OCPP));
                Assert.That(connection.Username,       Is.EqualTo("cs001"));
                Assert.That(connection.ServerHostKey,  Is.EqualTo(hostKey.PublicKeyBlob));
                Assert.That(connection.Session,        Is.Null);
                Assert.That(connection.RemoteSocket.Port.ToInt32(), Is.EqualTo(running.Port));
                Assert.That(connection.TryGetSSH(out var ssh), Is.True);
                Assert.That(ssh,                       Is.SameAs(connection));
                Assert.That(connection.TryGetWebSocket(out _), Is.False);

                Assert.That(serverSide.Role,               Is.EqualTo(ConnectionRole.Server));
                Assert.That(serverSide.Subprotocol,        Is.EqualTo("ocpp2.1"));
                Assert.That(serverSide.Username,           Is.EqualTo("cs001"));
                Assert.That(serverSide.Session?.Username,  Is.EqualTo("cs001"));
                Assert.That(serverSide.Session?.PublicKeyBlob, Is.EqualTo(stationKey.PublicKeyBlob));
                Assert.That(serverSide.ServerHostKey,      Is.Null);

                Assert.That(running.Audit.Events.OfType<SubsystemRequestedEvent>().Select(e => e.Subsystem), Is.EqualTo(new[] { OCPP }));

            });

        }

        #endregion

        #region Messages_GoBothWays

        [Test]
        [CancelAfter(30000)]
        public async Task Messages_GoBothWays(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);
            await using var client  = Client(running);

            var serverGotText    = new TaskCompletionSource<String>(TaskCreationOptions.RunContinuationsAsynchronously);
            var clientGotBinary  = new TaskCompletionSource<Byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            var serverSentText   = new TaskCompletionSource<SentStatus>(TaskCreationOptions.RunContinuationsAsynchronously);

            running.Messages.OnTextMessageReceived += (timestamp, sender, connection, text, ct) => { serverGotText.TrySetResult(text); return Task.CompletedTask; };
            running.Messages.OnTextMessageSent     += (timestamp, sender, connection, text, status, ct) => { serverSentText.TrySetResult(status); return Task.CompletedTask; };
            client.OnBinaryMessageReceived         += (timestamp, sender, connection, bytes, ct) => { clientGotBinary.TrySetResult(bytes); return Task.CompletedTask; };

            var connection = await client.ConnectAsync(CancellationToken);

            Assert.That(await connection.SendTextMessage("[2,\"19223201\",\"BootNotification\",{}]"), Is.EqualTo(SentStatus.Success));

            var text       = await Within(serverGotText.Task, "The server's receipt");
            var serverSide = await ServerSideOf(running);

            IMessageConnection common = serverSide;

            Assert.That(await common.SendBinaryMessage([ 0xC0, 0xFF, 0xEE ]), Is.EqualTo(SentStatus.Success));
            Assert.That(await common.SendTextMessage  ("[3,\"19223201\",{}]"),  Is.EqualTo(SentStatus.Success));

            var bytes      = await Within(clientGotBinary.Task, "The client's receipt");
            var sentStatus = await Within(serverSentText. Task, "The server's OnTextMessageSent");

            Assert.Multiple(() => {
                Assert.That(text,                               Is.EqualTo("[2,\"19223201\",\"BootNotification\",{}]"));
                Assert.That(bytes,                              Is.EqualTo(new Byte[] { 0xC0, 0xFF, 0xEE }));
                Assert.That(sentStatus,                         Is.EqualTo(SentStatus.Success));
                Assert.That(connection.MessagesSentCounter,     Is.EqualTo(1));
                Assert.That(serverSide.MessagesReceivedCounter, Is.EqualTo(1));
                Assert.That(serverSide.MessagesSentCounter,     Is.EqualTo(2));
                Assert.That(serverSide.LastReceivedTimestamp,   Is.Not.Null);
            });

        }

        #endregion

        #region NoCommonSubprotocol_IsRefused

        [Test]
        [CancelAfter(30000)]
        public async Task NoCommonSubprotocol_IsRefused(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);
            await using var client  = Client(running, o => o with { Subprotocols = [ "ocpp1.6" ] });

            var error = await Assert.ThrowsAsync<SshMessageRefusedException>(async () => await client.ConnectAsync(CancellationToken));

            Assert.Multiple(() => {
                Assert.That(error!.Code,                     Is.EqualTo(SshMessageRefusal.UnsupportedSubprotocol));
                Assert.That(error!.Description,              Does.Contain("ocpp2.1"));
                Assert.That(running.Messages.Connections,    Is.Empty);
            });

        }

        #endregion

        #region ServerThatChoosesASubprotocolNotOffered_IsNotTrusted

        /// <summary>
        /// A server may choose no subprotocol, but not one the client did not offer: the client would
        /// otherwise speak a protocol it does not know.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task ServerThatChoosesASubprotocolNotOffered_IsNotTrusted(CancellationToken CancellationToken)
        {

            await using var rogue = new SshServer(new SshServerOptions {
                                        HostKeys       = [ hostKey ],
                                        Authenticator  = SshUserAuthenticator.ForAuthorizedKeys(stationKey.PublicKeyBlob),
                                        Subsystems     = (session, name, ct) => ValueTask.FromResult<SshSubsystemHandler?>(async (context, token) => {
                                                             await SshMessageFraming.ReadAsync(context.Channel.Input, 0, token);
                                                             await context.Channel.SendDataAsync(SshMessageFraming.Encode(SshMessageFrameType.Welcome, SshMessageWelcome.Create("ocpp9.9", null).ToBytes()), token);
                                                             await Task.Delay(Timeout.Infinite, token);
                                                         })
                                    });

            await rogue.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);

            await using var client = Client(new Running(rogue, (UInt16) rogue.LocalEndPoint.Port.ToInt32(), null!, null!));

            var error = await Assert.ThrowsAsync<SshMessageProtocolException>(async () => await client.ConnectAsync(CancellationToken));

            Assert.That(error!.Message, Does.Contain("ocpp9.9"));

        }

        #endregion

        #region NoSubprotocolOffered_IsServedWithoutOne

        /// <summary>
        /// The case of "wwcp@open.charging.cloud": the client asks for no subprotocol, and gets none.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task NoSubprotocolOffered_IsServedWithoutOne(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);
            await using var client  = Client(running, o => o with { Subprotocols = [] });

            var connection = await client.ConnectAsync(CancellationToken);

            Assert.Multiple(() => {
                Assert.That(connection.Subprotocol,                          Is.Null);
                Assert.That(connection.Hello.JSON.ContainsKey("subprotocols"), Is.False);
            });

        }

        #endregion

        #region ValidateHello_RefusesOrWelcomesWithWhatTheApplicationSays

        [Test]
        [CancelAfter(30000)]
        public async Task ValidateHello_RefusesOrWelcomesWithWhatTheApplicationSays(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken, Validate: (server, context, hello, subprotocol, ct) =>

                ValueTask.FromResult(
                    hello.JSON["networkingNodeId"]?.Value<String>() is String asked && asked != context.Session.Username
                        ? SshHelloVerdict.Refuse("not-yours", $"'{context.Session.Username}' may not act for '{asked}'.")
                        : SshHelloVerdict.Accept(new JObject { ["networkingNodeId"] = context.Session.Username },
                                                 new Dictionary<String, Object?> { ["networkingNodeId"] = context.Session.Username })
                )

            );

            await using var impostor = Client(running, o => o with { HelloProperties = new JObject { ["networkingNodeId"] = "cs999" } });
            await using var station  = Client(running, o => o with { HelloProperties = new JObject { ["networkingNodeId"] = "cs001" } });

            var refused    = await Assert.ThrowsAsync<SshMessageRefusedException>(async () => await impostor.ConnectAsync(CancellationToken));
            var connection = await station.ConnectAsync(CancellationToken);
            var serverSide = await ServerSideOf(running);

            Assert.Multiple(() => {
                Assert.That(refused!.Code,                                           Is.EqualTo("not-yours"));
                Assert.That(connection.Welcome.JSON["networkingNodeId"]?.Value<String>(), Is.EqualTo("cs001"));
                Assert.That(serverSide.TryGetCustomData("networkingNodeId"),          Is.EqualTo("cs001"));
                Assert.That(serverSide.Hello.JSON["networkingNodeId"]?.Value<String>(), Is.EqualTo("cs001"));
            });

        }

        #endregion

        #region Hello_NotSentInTime_ClosesTheChannel

        [Test]
        [CancelAfter(30000)]
        public async Task Hello_NotSentInTime_ClosesTheChannel(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken, o => o with { HelloTimeout = TimeSpan.FromMilliseconds(300) });

            var (ssh, channel) = await RawAsync(running, CancellationToken);
            await using var _  = ssh;

            var frames = await FramesToTheEndAsync(channel);

            Assert.That(frames, Is.Empty, "the server answered a client that said nothing");

        }

        #endregion

        #region Hello_NotFirst_IsRefused

        [Test]
        [CancelAfter(30000)]
        public async Task Hello_NotFirst_IsRefused(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);

            var (ssh, channel) = await RawAsync(running, CancellationToken);
            await using var _  = ssh;

            await SendAsync(channel, SshMessageFrameType.Ping, [ 1 ]);

            var frames = await FramesToTheEndAsync(channel);

            Assert.Multiple(() => {
                Assert.That(frames.Select(f => f.Type),                         Is.EqualTo(new[] { SshMessageFrameType.Refused }));
                Assert.That(SshMessageRefusal.Decode(frames[0].Payload).Code,   Is.EqualTo(SshMessageRefusal.HelloExpected));
            });

        }

        #endregion

        #region TooLargeAMessage_ClosesWithMessageTooBig_AndIsNeverSent

        [Test]
        [CancelAfter(30000)]
        public async Task TooLargeAMessage_ClosesWithMessageTooBig_AndIsNeverSent(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken, o => o with { MaxMessageSizeIn = 1024 });

            var closed = new TaskCompletionSource<SshMessageCloseInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
            running.Messages.OnConnectionClosed += (timestamp, sender, connection, close, ct) => { closed.TrySetResult(close); return Task.CompletedTask; };

            // A well-behaved client keeps to the limit the server told it.
            await using (var client = Client(running))
            {

                var connection = await client.ConnectAsync(CancellationToken);
                var tooLarge   = await connection.SendTextMessage(new String('x', 1025));
                var aliveAfter = connection.IsAlive;
                var atTheLimit = await connection.SendTextMessage(new String('x', 1024));

                Assert.Multiple(() => {
                    Assert.That(connection.MaxMessageSizeOut,  Is.EqualTo(1024));
                    Assert.That(tooLarge,                      Is.EqualTo(SentStatus.Error));
                    Assert.That(aliveAfter,                    Is.True, "a message refused before sending broke the connection");
                    Assert.That(atTheLimit,                    Is.EqualTo(SentStatus.Success));
                });

            }

            await Within(closed.Task, "The first connection's end");

            // One that does not is closed: the length is read, and nothing allocated for it.
            closed = new TaskCompletionSource<SshMessageCloseInfo>(TaskCreationOptions.RunContinuationsAsynchronously);

            var (ssh, channel) = await RawAsync(running, CancellationToken);
            await using var _  = ssh;

            await SendAsync(channel, SshMessageFrameType.Hello, Hello("ocpp2.1"));

            var header = new Byte[5];
            BinaryPrimitives.WriteUInt32BigEndian(header, 0x7FFF_FFFF);
            header[4] = (Byte) SshMessageFrameType.Text;
            await channel.SendDataAsync(header);

            var frames = await FramesToTheEndAsync(channel);
            var close  = await Within(closed.Task, "The server's close");

            Assert.Multiple(() => {
                Assert.That(frames.Select(f => f.Type),                          Is.EqualTo(new[] { SshMessageFrameType.Welcome, SshMessageFrameType.Close }));
                Assert.That(SshMessageFraming.DecodeClose(frames[1].Payload).Code, Is.EqualTo(MessageCloseCode.MessageTooBig));
                Assert.That(close.Code,                                          Is.EqualTo(MessageCloseCode.MessageTooBig));
                Assert.That(close.ByPeer,                                        Is.False);
            });

        }

        #endregion

        #region TextThatIsNotUTF8_ClosesWithInvalidPayloadData

        [Test]
        [CancelAfter(30000)]
        public async Task TextThatIsNotUTF8_ClosesWithInvalidPayloadData(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);

            var (ssh, channel) = await RawAsync(running, CancellationToken);
            await using var _  = ssh;

            await SendAsync(channel, SshMessageFrameType.Hello, Hello("ocpp2.1"));
            await SendAsync(channel, SshMessageFrameType.Text,  [ (Byte) '[', 0xFF, (Byte) ']' ]);

            var frames = await FramesToTheEndAsync(channel);

            Assert.That(SshMessageFraming.DecodeClose(frames.Last().Payload).Code, Is.EqualTo(MessageCloseCode.InvalidPayloadData));

        }

        #endregion

        #region UnknownKinds_FromOx80AreSkipped_OthersCloseTheConnection

        [Test]
        [CancelAfter(30000)]
        public async Task UnknownKinds_FromOx80AreSkipped_OthersCloseTheConnection(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);

            var texts = new ConcurrentQueue<String>();
            running.Messages.OnTextMessageReceived += (timestamp, sender, connection, text, ct) => { texts.Enqueue(text); return Task.CompletedTask; };

            var (ssh, channel) = await RawAsync(running, CancellationToken);
            await using var _  = ssh;

            await SendAsync(channel, SshMessageFrameType.Hello,                Hello("ocpp2.1"));
            await SendAsync(channel, (SshMessageFrameType) 0x80,               [ 1, 2, 3 ]);
            await SendAsync(channel, SshMessageFrameType.Text,                 Encoding.UTF8.GetBytes("after the extension"));
            await SendAsync(channel, (SshMessageFrameType) 0x40,               []);
            await SendAsync(channel, SshMessageFrameType.Text,                 Encoding.UTF8.GetBytes("after the error"));

            var frames = await FramesToTheEndAsync(channel);

            Assert.Multiple(() => {
                Assert.That(texts,                                                    Is.EqualTo(new[] { "after the extension" }));
                Assert.That(frames.Select(f => f.Type),                               Is.EqualTo(new[] { SshMessageFrameType.Welcome, SshMessageFrameType.Close }));
                Assert.That(SshMessageFraming.DecodeClose(frames[1].Payload).Code,    Is.EqualTo(MessageCloseCode.ProtocolError));
            });

        }

        #endregion

        #region Close_IsAHandshake_WithCodeAndReason

        [Test]
        [CancelAfter(30000)]
        public async Task Close_IsAHandshake_WithCodeAndReason(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);
            await using var client  = Client(running);

            var serverClosed = new TaskCompletionSource<SshMessageCloseInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
            var clientClosed = new TaskCompletionSource<SshMessageCloseInfo>(TaskCreationOptions.RunContinuationsAsynchronously);

            running.Messages.OnConnectionClosed += (timestamp, sender, connection, close, ct) => { serverClosed.TrySetResult(close); return Task.CompletedTask; };
            client.OnConnectionClosed           += (timestamp, sender, connection, close, ct) => { clientClosed.TrySetResult(close); return Task.CompletedTask; };

            var connection = await client.ConnectAsync(CancellationToken);
            await ServerSideOf(running);

            await connection.Close(MessageCloseCode.PolicyViolation, "the station is decommissioned");

            var atServer = await Within(serverClosed.Task, "The server's close");
            var atClient = await Within(clientClosed.Task, "The client's close");

            Assert.Multiple(() => {
                Assert.That(atServer, Is.EqualTo(new SshMessageCloseInfo(MessageCloseCode.PolicyViolation, "the station is decommissioned", ByPeer: true)));
                Assert.That(atClient, Is.EqualTo(new SshMessageCloseInfo(MessageCloseCode.PolicyViolation, "the station is decommissioned", ByPeer: false)));
                Assert.That(connection.IsAlive,                 Is.False);
                Assert.That(running.Messages.Connections,       Is.Empty);
            });

        }

        #endregion

        #region TheSshConnection_CarriesAFurtherChannel_AndStaysTheClients

        /// <summary>
        /// The SSH connection of the messages is handed out while they go over it: another subsystem of the
        /// same server is opened on it as a further channel - NTS-KE beside OCPP, section 8.3 of the white
        /// paper "NTS Key Establishment over SSH" - and that channel ending leaves the messages going. It is
        /// handed out no more once they end.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task TheSshConnection_CarriesAFurtherChannel_AndStaysTheClients(CancellationToken CancellationToken)
        {

            const String Time = "time@test";

            await using var running = await StartAsync(CancellationToken,
                                                       Further: (Time, async (context, ct) => await context.Channel.SendDataAsync("tick"u8.ToArray(), ct)));
            await using var client  = Client(running);

            var serverGotText = new TaskCompletionSource<String>(TaskCreationOptions.RunContinuationsAsynchronously);
            running.Messages.OnTextMessageReceived += (timestamp, sender, connection, text, ct) => { serverGotText.TrySetResult(text); return Task.CompletedTask; };

            var before     = client.Ssh;
            var connection = await client.ConnectAsync(CancellationToken);
            var ssh        = client.Ssh;

            Assert.That(ssh, Is.Not.Null, "No SSH connection while the messages go over one.");

            var channel    = await ssh!.OpenSubsystemAsync(Time, CancellationToken);
            using var read = new MemoryStream();
            await channel.Input.CopyToAsync(read, CancellationToken);

            Assert.That(await connection.SendTextMessage("[2,\"19223203\",\"Heartbeat\",{}]"), Is.EqualTo(SentStatus.Success));

            var text       = await Within(serverGotText.Task, "The server's receipt after the further channel ended");

            await connection.Close(MessageCloseCode.NormalClosure);

            Assert.Multiple(() => {
                Assert.That(before,                              Is.Null, "An SSH connection before any was made.");
                Assert.That(Encoding.UTF8.GetString(read.ToArray()), Is.EqualTo("tick"));
                Assert.That(text,                                Does.Contain("Heartbeat"));
                Assert.That(ssh.ServerHostKey,                   Is.EqualTo(hostKey.PublicKeyBlob), "Not the connection the messages went over.");
                Assert.That(client.Ssh,                          Is.Null, "An SSH connection handed out after the messages ended.");
            });

        }

        #endregion

        #region ServerThatStops_SaysGoingAway

        [Test]
        [CancelAfter(30000)]
        public async Task ServerThatStops_SaysGoingAway(CancellationToken CancellationToken)
        {

            var running = await StartAsync(CancellationToken);
            await using var client = Client(running);

            var clientClosed = new TaskCompletionSource<SshMessageCloseInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.OnConnectionClosed += (timestamp, sender, connection, close, ct) => { clientClosed.TrySetResult(close); return Task.CompletedTask; };

            await client.ConnectAsync(CancellationToken);
            await ServerSideOf(running);

            await running.DisposeAsync();

            var close = await Within(clientClosed.Task, "The client's close");

            Assert.Multiple(() => {
                Assert.That(close.Code,   Is.EqualTo(MessageCloseCode.GoingAway));
                Assert.That(close.ByPeer, Is.True);
            });

        }

        #endregion

        #region ServerThatStops_SaysGoingAway_ToAClientStillBusyWithAMessage

        /// <summary>
        /// The server's CLOSE arrives while the client is still busy with the message before it, and the
        /// SSH connection has ended by the time the client is done: the CLOSE is read all the same.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task ServerThatStops_SaysGoingAway_ToAClientStillBusyWithAMessage(CancellationToken CancellationToken)
        {

            var running = await StartAsync(CancellationToken);
            await using var client = Client(running);

            var clientBusy   = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var clientClosed = new TaskCompletionSource<SshMessageCloseInfo>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Busy until the client has seen its SSH connection end: the handler's token tells of that.
            client.OnTextMessageReceived += async (timestamp, sender, connection, text, ct) => {
                clientBusy.TrySetResult();
                try { await Task.Delay(TimeSpan.FromSeconds(5), ct); } catch (OperationCanceledException) { }
            };
            client.OnConnectionClosed    += (timestamp, sender, connection, close, ct) => { clientClosed.TrySetResult(close); return Task.CompletedTask; };

            await client.ConnectAsync(CancellationToken);
            var serverSide = await ServerSideOf(running);

            Assert.That(await serverSide.SendTextMessage("[2,\"19223202\",\"Reset\",{}]"), Is.EqualTo(SentStatus.Success));
            await Within(clientBusy.Task, "The client's receipt");

            await running.DisposeAsync();

            var close = await Within(clientClosed.Task, "The client's close");

            Assert.Multiple(() => {
                Assert.That(close.Code,   Is.EqualTo(MessageCloseCode.GoingAway));
                Assert.That(close.ByPeer, Is.True);
            });

        }

        #endregion

        #region ClientThatCloses_IsHeard_ByAServerStillBusyWithAMessage

        /// <summary>
        /// The client's CLOSE arrives while the server is still busy with the message before it, and the
        /// channel has been closed by the time the server is done: the CLOSE is read all the same.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task ClientThatCloses_IsHeard_ByAServerStillBusyWithAMessage(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);
            await using var client  = Client(running);

            var serverBusy   = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var serverClosed = new TaskCompletionSource<SshMessageCloseInfo>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Busy until the server has seen the channel closed: the handler's token tells of that.
            running.Messages.OnTextMessageReceived += async (timestamp, sender, connection, text, ct) => {
                serverBusy.TrySetResult();
                try { await Task.Delay(TimeSpan.FromSeconds(5), ct); } catch (OperationCanceledException) { }
            };
            running.Messages.OnConnectionClosed    += (timestamp, sender, connection, close, ct) => { serverClosed.TrySetResult(close); return Task.CompletedTask; };

            var connection = await client.ConnectAsync(CancellationToken);

            Assert.That(await connection.SendTextMessage("[2,\"19223203\",\"BootNotification\",{}]"), Is.EqualTo(SentStatus.Success));
            await Within(serverBusy.Task, "The server's receipt");

            await connection.Close(MessageCloseCode.PolicyViolation, "the station is decommissioned");

            var close = await Within(serverClosed.Task, "The server's close");

            Assert.That(close, Is.EqualTo(new SshMessageCloseInfo(MessageCloseCode.PolicyViolation, "the station is decommissioned", ByPeer: true)));

        }

        #endregion

        #region ConcurrentSenders_NeverInterleave

        /// <summary>
        /// Messages larger than one SSH channel packet (32 KiB), sent from many tasks at once, arrive whole:
        /// the channel itself does not keep two writers apart.
        /// </summary>
        [Test]
        [CancelAfter(60000)]
        public async Task ConcurrentSenders_NeverInterleave(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);
            await using var client  = Client(running);

            const Int32 Senders  = 8;
            const Int32 Messages = 20;

            var received = new ConcurrentBag<String>();
            var allIn    = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            running.Messages.OnTextMessageReceived += (timestamp, sender, connection, text, ct) => {
                received.Add(text);
                if (received.Count == Senders * Messages)
                    allIn.TrySetResult();
                return Task.CompletedTask;
            };

            var connection = await client.ConnectAsync(CancellationToken);

            var statuses = await Task.WhenAll(Enumerable.Range(0, Senders).Select(sender => Task.Run(async () => {
                               var mine = new List<SentStatus>();
                               for (var n = 0; n < Messages; n++)
                                   mine.Add(await connection.SendTextMessage($"{sender:D2}/{n:D3}:" + new String((Char) ('a' + sender), 100_000)));
                               return mine;
                           })));

            await Within(allIn.Task, "Every message");

            Assert.Multiple(() => {

                Assert.That(statuses.SelectMany(s => s), Is.All.EqualTo(SentStatus.Success));

                foreach (var text in received)
                {
                    var sender = Int32.Parse(text[..2]);
                    Assert.That(text.Length,        Is.EqualTo(7 + 100_000),                          "a message arrived cut");
                    Assert.That(text[7..].Distinct(), Is.EqualTo(new[] { (Char) ('a' + sender) }),  "two messages were mixed");
                }

            });

        }

        #endregion

        #region Ping_IsAnsweredWithItsPayload

        [Test]
        [CancelAfter(30000)]
        public async Task Ping_IsAnsweredWithItsPayload(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);
            await using var client  = Client(running);

            var pong = new TaskCompletionSource<Byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.OnPongReceived += (timestamp, sender, connection, payload, ct) => { pong.TrySetResult(payload); return Task.CompletedTask; };

            var connection = await client.ConnectAsync(CancellationToken);

            Assert.That(await connection.SendPing([ 4, 2 ]), Is.EqualTo(SentStatus.Success));
            Assert.That(await Within(pong.Task, "The PONG"), Is.EqualTo(new Byte[] { 4, 2 }));

        }

        #endregion

        #region Reconnect_AfterTheServerEndedTheConnection

        [Test]
        [CancelAfter(30000)]
        public async Task Reconnect_AfterTheServerEndedTheConnection(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);
            await using var client  = Client(running, o => o with { Reconnect = new WebSocketClientReconnectPolicy(InitialDelay: TimeSpan.FromMilliseconds(50), MaxDelay: TimeSpan.FromMilliseconds(200)) });

            var connected = new ConcurrentQueue<SshMessageConnection>();
            var second    = new TaskCompletionSource<SshMessageConnection>(TaskCreationOptions.RunContinuationsAsynchronously);

            client.OnNewConnection += (timestamp, sender, connection, ct) => {
                connected.Enqueue(connection);
                if (connected.Count == 2)
                    second.TrySetResult(connection);
                return Task.CompletedTask;
            };

            var first      = await client.ConnectAsync(CancellationToken);
            var serverSide = await ServerSideOf(running);

            await serverSide.Close(MessageCloseCode.GoingAway, "go and come back");

            var again = await Within(second.Task, "The second connection");

            Assert.Multiple(() => {
                Assert.That(again,              Is.Not.SameAs(first));
                Assert.That(client.Connection,  Is.SameAs(again));
                Assert.That(again.IsAlive,      Is.True);
                Assert.That(first.IsAlive,      Is.False);
            });

            await client.Close();

            Assert.That(client.IsConnected, Is.False);

        }

        #endregion


        #region Refused_WithRetryAfter_TellsTheClientWhenToComeBack

        [Test]
        [CancelAfter(30000)]
        public async Task Refused_WithRetryAfter_TellsTheClientWhenToComeBack(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken, Validate: (server, context, hello, subprotocol, ct) =>
                                          ValueTask.FromResult(SshHelloVerdict.Refuse(SshMessageRefusal.ConnectionExists,
                                                                                      "cs001 is connected, and answers.",
                                                                                      TimeSpan.FromSeconds(30))));

            await using var client  = Client(running);

            var refused = await Assert.ThrowsAsync<SshMessageRefusedException>(async () => await client.ConnectAsync(CancellationToken));

            Assert.Multiple(() => {
                Assert.That(refused!.Code,       Is.EqualTo("connection-exists"));
                Assert.That(refused.Description, Is.EqualTo("cs001 is connected, and answers."));
                Assert.That(refused.RetryAfter,  Is.EqualTo(TimeSpan.FromSeconds(30)));
                Assert.That(refused.Message,     Does.Contain("try again in 30 s"));
            });

        }

        #endregion

        #region Reconnect_WaitsForTheRetryAfterOfARefusal

        /// <summary>
        /// A reconnecting client that is refused with a "retryAfter" asks again no earlier than that,
        /// although its own policy would have asked again after 50 ms.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task Reconnect_WaitsForTheRetryAfterOfARefusal(CancellationToken CancellationToken)
        {

            var hellos   = new ConcurrentQueue<DateTimeOffset>();

            await using var running = await StartAsync(CancellationToken, Validate: (server, context, hello, subprotocol, ct) => {
                                          hellos.Enqueue(DateTimeOffset.UtcNow);
                                          // The first connection is served, the second refused for a second, the third served.
                                          return ValueTask.FromResult(hellos.Count == 2
                                                                          ? SshHelloVerdict.Refuse(SshMessageRefusal.ConnectionExists, null, TimeSpan.FromSeconds(1))
                                                                          : SshHelloVerdict.Accept());
                                      });

            await using var client  = Client(running, o => o with { Reconnect = new WebSocketClientReconnectPolicy(InitialDelay: TimeSpan.FromMilliseconds(50), MaxDelay: TimeSpan.FromMilliseconds(50)) });

            var failed = new TaskCompletionSource<(Exception Error, TimeSpan? Next)>(TaskCreationOptions.RunContinuationsAsynchronously);
            var third  = new TaskCompletionSource<SshMessageConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            var count  = 0;

            client.OnConnectFailed += (timestamp, sender, error, attempt, next, ct) => { failed.TrySetResult((error, next)); return Task.CompletedTask; };
            client.OnNewConnection += (timestamp, sender, connection, ct) => {
                if (Interlocked.Increment(ref count) == 2)
                    third.TrySetResult(connection);
                return Task.CompletedTask;
            };

            await client.ConnectAsync(CancellationToken);
            var serverSide = await ServerSideOf(running);

            await serverSide.Close(MessageCloseCode.GoingAway, "go and come back");

            var (error, next) = await Within(failed.Task, "The refusal");
            await Within(third.Task, "The connection after the refusal");

            var times = hellos.ToArray();

            Assert.Multiple(() => {
                Assert.That(error,                Is.TypeOf<SshMessageRefusedException>());
                Assert.That(next,                 Is.EqualTo(TimeSpan.FromSeconds(1)), "the next attempt is announced for when the server said");
                Assert.That(times,                Has.Length.EqualTo(3));
                Assert.That(times[2] - times[1],  Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(950)), "the client came back before the server's retryAfter");
            });

        }

        #endregion

        #region SilentClient_IsTakenForDead_AfterMaxOutstandingPings

        /// <summary>
        /// A client that says HELLO and then nothing - it does not even answer the PINGs - is taken for
        /// dead after MaxOutstandingPings intervals: the server ends the connection without a CLOSE.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task SilentClient_IsTakenForDead_AfterMaxOutstandingPings(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken, o => o with { PingEvery = TimeSpan.FromMilliseconds(200), MaxOutstandingPings = 2 });

            var closed = new TaskCompletionSource<SshMessageCloseInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
            running.Messages.OnConnectionClosed += (timestamp, sender, connection, close, ct) => { closed.TrySetResult(close); return Task.CompletedTask; };

            var (ssh, channel) = await RawAsync(running, CancellationToken);
            await using var _  = ssh;

            await SendAsync(channel, SshMessageFrameType.Hello, Hello("ocpp2.1"));

            var started = DateTimeOffset.UtcNow;
            var close   = await Within(closed.Task, "The end of the silent client's connection");
            var after   = DateTimeOffset.UtcNow - started;
            var frames  = await FramesToTheEndAsync(channel);

            Assert.Multiple(() => {
                Assert.That(close.Code,    Is.Null,  "no CLOSE is sent to a peer taken for dead");
                Assert.That(close.ByPeer,  Is.False);
                Assert.That(close.Reason,  Does.Contain("Nothing came from the peer"));
                Assert.That(after,         Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(400)), "ended before two intervals had passed");
                Assert.That(frames[0].Type, Is.EqualTo(SshMessageFrameType.Welcome));
                Assert.That(frames.Skip(1).Select(frame => frame.Type), Is.All.EqualTo(SshMessageFrameType.Ping));
            });

        }

        #endregion

        #region AnsweringClient_StaysConnected

        /// <summary>
        /// A client that answers the PINGs - Hermod's own, which does so by itself - stays connected,
        /// however many intervals pass.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task AnsweringClient_StaysConnected(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken, o => o with { PingEvery = TimeSpan.FromMilliseconds(100), MaxOutstandingPings = 2 });
            await using var client  = Client(running);

            var connection = await client.ConnectAsync(CancellationToken);
            var serverSide = await ServerSideOf(running);

            await Task.Delay(TimeSpan.FromMilliseconds(1200), CancellationToken);

            Assert.Multiple(() => {
                Assert.That(serverSide.IsAlive, Is.True);
                Assert.That(connection.IsAlive, Is.True);
            });

        }

        #endregion

        #region SilentServer_IsTakenForDead_ByAPingingClient

        /// <summary>
        /// The other way round: a client that PINGs a server that welcomed it and then says nothing ends
        /// the connection after MaxOutstandingPings intervals, without a CLOSE.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task SilentServer_IsTakenForDead_ByAPingingClient(CancellationToken CancellationToken)
        {

            const String Mute = "mute@open.charging.cloud";

            await using var running = await StartAsync(CancellationToken, Further: (Mute, async (context, ct) => {
                                          await SshMessageFraming.ReadAsync(context.Channel.Input, 0, ct);
                                          await context.Channel.SendDataAsync(SshMessageFraming.Encode(SshMessageFrameType.Welcome,
                                                                                                       SshMessageWelcome.Create("ocpp2.1", null).ToBytes()), ct);
                                          // ... and never a word more, not even a PONG, until the channel goes.
                                          try { await Task.Delay(Timeout.Infinite, context.Closed); } catch (OperationCanceledException) { }
                                      }));

            await using var client  = Client(running, o => o with { Subsystem = Mute, PingEvery = TimeSpan.FromMilliseconds(200), MaxOutstandingPings = 2 });

            var closed = new TaskCompletionSource<SshMessageCloseInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.OnConnectionClosed += (timestamp, sender, connection, close, ct) => { closed.TrySetResult(close); return Task.CompletedTask; };

            await client.ConnectAsync(CancellationToken);

            var close = await Within(closed.Task, "The end of the connection to the silent server");

            Assert.Multiple(() => {
                Assert.That(close.Code,   Is.Null);
                Assert.That(close.ByPeer, Is.False);
                Assert.That(close.Reason, Does.Contain("Nothing came from the peer"));
            });

        }

        #endregion

    }

}
