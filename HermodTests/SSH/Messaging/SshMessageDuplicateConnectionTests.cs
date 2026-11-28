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

using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Client;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Server;
using org.GraphDefined.Vanaheimr.Hermod.Messaging;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// A second connection of an identity that is still connected (white paper "EVQI Transport", chapter 12):
    /// what each <see cref="SshDuplicatePolicy"/> makes of it, the probe that tells a live old connection
    /// from a dead one, the close codes 4000 and 4001, and "connection-exists" with a retryAfter.
    /// </summary>
    [TestFixture]
    public class SshMessageDuplicateConnectionTests
    {

        #region (private) Fixture

        private const String OCPP = "ocpp@open.charging.cloud";

        private readonly ISshHostKey  hostKey     = SshHostKey.GenerateEd25519();
        private readonly ISshHostKey  stationKey  = SshHostKey.GenerateEd25519();

        private sealed record Running(SshServer Server, IPPort Port, SshMessageServer Messages, ConcurrentQueue<SshDuplicateConnection> Duplicates) : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => Server.DisposeAsync();
        }

        private async Task<Running> StartAsync(CancellationToken                                        CancellationToken,
                                               Func<SshMessageServerOptions, SshMessageServerOptions>?  Configure  = null,
                                               SshHelloValidator?                                       Validate   = null)
        {

            var duplicates = new ConcurrentQueue<SshDuplicateConnection>();

            var messages   = new SshMessageServer((Configure ?? (o => o))(new SshMessageServerOptions {
                                                      Subsystem     = OCPP,
                                                      Subprotocols  = [ "ocpp2.1" ],
                                                      ProbeTimeout  = TimeSpan.FromSeconds(1)
                                                  })) {
                                 ValidateHello = Validate
                             };

            messages.OnDuplicateConnection += (timestamp, sender, duplicate, ct) => {
                                                  duplicates.Enqueue(duplicate);
                                                  return Task.CompletedTask;
                                              };

            var server     = new SshServer(new SshServerOptions {
                                 HostKeys       = [ hostKey ],
                                 Authenticator  = SshUserAuthenticator.ForAuthorizedKeys(stationKey.PublicKeyBlob),
                                 Subsystems     = (session, name, ct) => ValueTask.FromResult<SshSubsystemHandler?>(name == OCPP ? messages.Handler : null)
                             });

            await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);

            return new Running(server, server.LocalEndPoint.Port, messages, duplicates);

        }

        private SSHClientOptions Login(String Username)

            => new () {
                   Username       = User_Id.Parse(Username),
                   VerifyHostKey  = blob => blob.AsSpan().SequenceEqual(hostKey.PublicKeyBlob),
                   Credentials    = [ stationKey ]
               };

        private SshMessageClient Client(Running                                                  Running,
                                        String                                                   Username   = "cs001",
                                        Func<SshMessageClientOptions, SshMessageClientOptions>?  Configure  = null)

            => new ((Configure ?? (o => o))(new SshMessageClientOptions {
                       Host          = "127.0.0.1",
                       Port          = Running.Port,
                       Ssh           = Login(Username),
                       Subsystem     = OCPP,
                       Subprotocols  = [ "ocpp2.1" ]
                   }));

        /// <summary>
        /// A connection that says HELLO, takes its WELCOME, and then reads and sends nothing more: what the
        /// server sees of a station that lost its path while the server still holds the old socket.
        /// </summary>
        private async Task<(SSHClient Ssh, SshMuxChannel Channel)> SilentAsync(Running Running, CancellationToken CancellationToken, String Username = "cs001")
        {

            var ssh     = await SSHClient.Connect("127.0.0.1", Running.Port, Login(Username), CancellationToken);
            var channel = await ssh.OpenSubsystem(OCPP, CancellationToken);

            await channel.SendDataAsync(SshMessageFraming.Encode(SshMessageFrameType.Hello, SshMessageHello.Create([ "ocpp2.1" ], null).ToBytes()));

            var welcome = await SshMessageFraming.ReadAsync(channel.Input, 1024 * 1024, CancellationToken);
            Assert.That(welcome?.Type, Is.EqualTo(SshMessageFrameType.Welcome), "the silent connection was welcomed");

            return (ssh, channel);

        }

        /// <summary>
        /// What the server sends on a silent connection, frame by frame, once it is read at last - to its end.
        /// </summary>
        private static async Task<List<SshMessageFrame>> FramesToTheEndAsync(SshMuxChannel Channel)
        {
            var frames = new List<SshMessageFrame>();
            using var within = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (await SshMessageFraming.ReadAsync(Channel.Input, 1024 * 1024, within.Token) is SshMessageFrame frame)
                frames.Add(frame);
            return frames;
        }

        private static async Task<T> Within<T>(Task<T> Happening, String What)
        {
            if (await Task.WhenAny(Happening, Task.Delay(TimeSpan.FromSeconds(10))) != Happening)
                Assert.Fail($"{What} did not happen within 10 s.");
            return await Happening;
        }

        private static async Task Eventually(Func<Boolean> Condition, String What)
        {
            var until = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10);
            while (!Condition())
            {
                if (DateTimeOffset.UtcNow > until)
                    Assert.Fail($"{What} did not happen within 10 s.");
                await Task.Delay(20);
            }
        }

        private static MessageCloseCode CloseCodeOf(SshMessageFrame Frame)
            => (MessageCloseCode) BinaryPrimitives.ReadUInt16BigEndian(Frame.Payload);

        #endregion


        #region Probe_ALiveConnection_KeepsTheIdentity_AndTheNewcomerIsRefused

        /// <summary>
        /// T1 of the white paper: somebody with the station's credentials connects while the station is
        /// online. The station's connection answers the probe and stays; the newcomer is refused with
        /// "connection-exists" and a retryAfter between 30 and 60 seconds - and nothing reaches the station.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task Probe_ALiveConnection_KeepsTheIdentity_AndTheNewcomerIsRefused(CancellationToken CancellationToken)
        {

            await using var running   = await StartAsync(CancellationToken);
            await using var station   = Client(running);
            await using var intruder  = Client(running);

            var stations = await station.ConnectAsync(CancellationToken);
            var refused  = await Assert.ThrowsAsync<SshMessageRefusedException>(async () => await intruder.ConnectAsync(CancellationToken));

            Assert.Multiple(() => {
                Assert.That(refused!.Code,                                  Is.EqualTo(SshMessageRefusal.ConnectionExists));
                Assert.That(refused.RetryAfter,                              Is.InRange(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60)));
                Assert.That(stations.IsAlive,                                Is.True,  "the station's connection is untouched");
                Assert.That(running.Messages.ConnectionsOf("cs001").Count,   Is.EqualTo(1));
                Assert.That(running.Duplicates.Single().Decision,            Is.EqualTo(SshDuplicateDecision.Refused));
                Assert.That(running.Duplicates.Single().Policy,              Is.EqualTo(SshDuplicatePolicy.Probe));
            });

        }

        #endregion

        #region Probe_AConnectionThatDoesNotAnswer_IsClosedAsUnresponsive_AndTheNewcomerServed

        /// <summary>
        /// L1 of the white paper: the station lost its path, and the server still holds the old connection.
        /// The probe - a PING with 16 random bytes - goes unanswered, the old connection is closed with 4001,
        /// and the station's new connection is served within the probe's second, not after the minutes a
        /// keepalive would take.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task Probe_AConnectionThatDoesNotAnswer_IsClosedAsUnresponsive_AndTheNewcomerServed(CancellationToken CancellationToken)
        {

            await using var running      = await StartAsync(CancellationToken);
            var (ssh, old)               = await SilentAsync(running, CancellationToken);
            await using var _            = ssh;
            await using var reconnected  = Client(running);

            var took       = Stopwatch.StartNew();
            var connection = await reconnected.ConnectAsync(CancellationToken);
            took.Stop();

            var frames     = await FramesToTheEndAsync(old);

            Assert.Multiple(() => {
                Assert.That(connection.IsAlive,                                     Is.True);
                Assert.That(took.Elapsed,                                           Is.LessThan(TimeSpan.FromSeconds(5)), "the newcomer waited for the probe, and no longer");
                Assert.That(running.Messages.ConnectionsOf("cs001").Count,          Is.EqualTo(1));
                Assert.That(running.Duplicates.Single().Decision,                   Is.EqualTo(SshDuplicateDecision.ReplacedUnresponsive));
                Assert.That(frames.Select(frame => frame.Type),                     Is.EqualTo(new[] { SshMessageFrameType.Ping, SshMessageFrameType.Close }));
                Assert.That(frames[0].Payload.Length,                               Is.EqualTo(16), "16 random bytes");
                Assert.That(CloseCodeOf(frames[1]),                                 Is.EqualTo(MessageCloseCode.Unresponsive));
            });

        }

        #endregion

        #region Probe_WhileItRuns_AFurtherNewcomerIsRefusedAtOnce

        /// <summary>
        /// One probe of an identity at a time: a third connection that comes while the probe for the second
        /// runs is refused at once, not kept waiting for its outcome.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task Probe_WhileItRuns_AFurtherNewcomerIsRefusedAtOnce(CancellationToken CancellationToken)
        {

            await using var running  = await StartAsync(CancellationToken, o => o with { ProbeTimeout = TimeSpan.FromSeconds(3) });
            var (ssh, old)           = await SilentAsync(running, CancellationToken);
            await using var _        = ssh;
            await using var second   = Client(running);
            await using var third    = Client(running);

            var secondConnects = second.ConnectAsync(CancellationToken);

            // The probe has begun once its PING is on the silent connection.
            var ping = await Within(SshMessageFraming.ReadAsync(old.Input, 1024, CancellationToken).AsTask(), "The probe's PING");

            var took    = Stopwatch.StartNew();
            var refused = await Assert.ThrowsAsync<SshMessageRefusedException>(async () => await third.ConnectAsync(CancellationToken));
            took.Stop();

            var connection = await Within(secondConnects, "The second connection");

            Assert.Multiple(() => {
                Assert.That(ping?.Type,                                                     Is.EqualTo(SshMessageFrameType.Ping));
                Assert.That(refused!.Code,                                                  Is.EqualTo(SshMessageRefusal.ConnectionExists));
                Assert.That(took.Elapsed,                                                   Is.LessThan(TimeSpan.FromSeconds(2)), "refused at once, not after the probe");
                Assert.That(connection.IsAlive,                                             Is.True,  "the second connection was served once the probe went unanswered");
                Assert.That(running.Duplicates.Select(duplicate => duplicate.Decision),    Is.EquivalentTo(new[] { SshDuplicateDecision.RefusedWhileProbing, SshDuplicateDecision.ReplacedUnresponsive }));
            });

        }

        #endregion

        #region Replace_ClosesTheOldConnectionAsSuperseded

        /// <summary>
        /// Newest wins, as before - but with 4000, so that a client that did not replace itself knows that
        /// somebody else holds its identity now.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task Replace_ClosesTheOldConnectionAsSuperseded(CancellationToken CancellationToken)
        {

            await using var running  = await StartAsync(CancellationToken, o => o with { DuplicatePolicy = SshDuplicatePolicy.Replace });
            await using var first    = Client(running);
            await using var second   = Client(running);

            var firsts   = await first. ConnectAsync(CancellationToken);
            var seconds  = await second.ConnectAsync(CancellationToken);

            await Eventually(() => firsts.Ended.IsCancellationRequested, "The first connection's end");

            Assert.Multiple(() => {
                Assert.That(firsts.CloseInfo?.Code,                       Is.EqualTo(MessageCloseCode.Superseded));
                Assert.That(firsts.CloseInfo?.ByPeer,                     Is.True);
                Assert.That(seconds.IsAlive,                              Is.True);
                Assert.That(running.Duplicates.Single().Decision,         Is.EqualTo(SshDuplicateDecision.Replaced));
            });

            await Eventually(() => running.Messages.ConnectionsOf("cs001").Count == 1, "The books holding the second connection alone");

        }

        #endregion

        #region KeepExisting_RefusesTheNewcomer_UntilTheOldConnectionIsGone

        /// <summary>
        /// The strict policy: no probe, and the newcomer refused while the old connection is on the books -
        /// even one that would not have answered. Once it is gone, the newcomer is served.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task KeepExisting_RefusesTheNewcomer_UntilTheOldConnectionIsGone(CancellationToken CancellationToken)
        {

            await using var running  = await StartAsync(CancellationToken, o => o with { DuplicatePolicy = SshDuplicatePolicy.KeepExisting });
            var (ssh, old)           = await SilentAsync(running, CancellationToken);
            await using var newcomer = Client(running);

            var refused = await Assert.ThrowsAsync<SshMessageRefusedException>(async () => await newcomer.ConnectAsync(CancellationToken));

            await ssh.DisposeAsync();
            await Eventually(() => running.Messages.ConnectionsOf("cs001").Count == 0, "The old connection's end");

            var connection = await newcomer.ConnectAsync(CancellationToken);

            Assert.Multiple(() => {
                Assert.That(refused!.Code,                                Is.EqualTo(SshMessageRefusal.ConnectionExists));
                Assert.That(connection.IsAlive,                           Is.True);
                Assert.That(running.Duplicates.Single().Decision,         Is.EqualTo(SshDuplicateDecision.Refused));
            });

        }

        #endregion

        #region Parallel_ServesSeveral_UpToMaxPerIdentity

        /// <summary>
        /// The channels of one channel session: served side by side, as many as MaxPerIdentity allows.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task Parallel_ServesSeveral_UpToMaxPerIdentity(CancellationToken CancellationToken)
        {

            await using var running  = await StartAsync(CancellationToken, o => o with { DuplicatePolicy = SshDuplicatePolicy.Parallel, MaxPerIdentity = 2 });
            await using var control  = Client(running);
            await using var realtime = Client(running);
            await using var bulk     = Client(running);

            var controls  = await control. ConnectAsync(CancellationToken);
            var realtimes = await realtime.ConnectAsync(CancellationToken);
            var refused   = await Assert.ThrowsAsync<SshMessageRefusedException>(async () => await bulk.ConnectAsync(CancellationToken));

            Assert.Multiple(() => {
                Assert.That(controls. IsAlive,                                              Is.True);
                Assert.That(realtimes.IsAlive,                                              Is.True);
                Assert.That(refused!.Code,                                                  Is.EqualTo(SshMessageRefusal.ConnectionExists));
                Assert.That(running.Messages.ConnectionsOf("cs001").Count,                  Is.EqualTo(2));
                Assert.That(running.Duplicates.Select(duplicate => duplicate.Decision),    Is.EqualTo(new[] { SshDuplicateDecision.ServedAlongside, SshDuplicateDecision.RefusedOverLimit }));
            });

        }

        #endregion

        #region TheVerdict_NamesTheIdentity_AndMayChooseItsPolicy

        /// <summary>
        /// The identity is the application's to name: two logins acting for the same networking node are
        /// duplicates of each other, while one login acting for two nodes is not. And the verdict may give an
        /// identity a policy of its own - a local controller's, say.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task TheVerdict_NamesTheIdentity_AndMayChooseItsPolicy(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken, Validate: (server, context, hello, subprotocol, ct) => {

                var node = hello.JSON["networkingNodeId"]?.ToString() ?? context.Session.Username.ToString();

                return ValueTask.FromResult(SshHelloVerdict.Accept(Identity:         node,
                                                                   DuplicatePolicy:  node == "lc01" ? SshDuplicatePolicy.Parallel : null));

            });

            SshMessageClient ActingFor(String Login, String Node)
                => Client(running, Login, o => o with { HelloProperties = new Newtonsoft.Json.Linq.JObject { ["networkingNodeId"] = Node } });

            await using var directly      = ActingFor("cs001", "cs001");
            await using var throughTheLC  = ActingFor("lc01",  "cs001");
            await using var otherNode     = ActingFor("lc01",  "cs002");
            await using var lcItself      = ActingFor("lc01",  "lc01");
            await using var lcAgain       = ActingFor("lc01",  "lc01");

            await directly.ConnectAsync(CancellationToken);
            var refused = await Assert.ThrowsAsync<SshMessageRefusedException>(async () => await throughTheLC.ConnectAsync(CancellationToken));
            await otherNode.ConnectAsync(CancellationToken);
            await lcItself. ConnectAsync(CancellationToken);
            await lcAgain.  ConnectAsync(CancellationToken);

            Assert.Multiple(() => {
                Assert.That(refused!.Code,                                  Is.EqualTo(SshMessageRefusal.ConnectionExists), "another login for the same node is a duplicate");
                Assert.That(running.Messages.ConnectionsOf("cs001").Count,  Is.EqualTo(1));
                Assert.That(running.Messages.ConnectionsOf("cs002").Count,  Is.EqualTo(1),  "the same login for another node is not");
                Assert.That(running.Messages.ConnectionsOf("lc01"). Count,  Is.EqualTo(2),  "the verdict's own policy for lc01");
            });

        }

        #endregion

    }

}
