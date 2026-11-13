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
using System.Diagnostics;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Client;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Clients
{

    /// <summary>
    /// DNS over SSH, the client side: a <see cref="DNSSSHClient"/> against
    /// <see cref="DNSServer.ServeSSHSubsystemAsync"/> behind an <see cref="SshServer"/> - and, where a
    /// server has to misbehave on purpose, against a subsystem that answers by a script.
    /// </summary>
    /// <remarks>
    /// One channel for all queries; a new channel on the same SSH connection after the server closed
    /// an idle one; a connection of the client's own and one it is handed; and the failures a caller
    /// has to tell apart: a refused subsystem and a host key that is not the one expected.
    /// </remarks>
    [TestFixture]
    public class DNSSSHClient_Tests
    {

        #region (private) Fixture

        private const String Echo      = "echo@test.open.charging.cloud";

        /// <summary>
        /// A login the SSH server knows, and does not offer the DNS subsystem to.
        /// </summary>
        private const String OCPPOnly  = "ocpp-only";

        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        private readonly ISshHostKey  hostKey     = SshHostKey.GenerateEd25519();
        private readonly ISshHostKey  stationKey  = SshHostKey.GenerateEd25519();

        private sealed class Running(DNSServer DNS, SshServer SSH, CollectingAuditSink Audit) : IAsyncDisposable
        {

            private Int32 channels;
            private Int32 channelsEnded;

            public DNSServer                   DNS       { get; } = DNS;
            public SshServer                   SSH       { get; } = SSH;
            public CollectingAuditSink         Audit     { get; } = Audit;
            public UInt16                      Port      => (UInt16) SSH.LocalEndPoint.Port.ToInt32();

            /// <summary>
            /// What the DNS server was asked, and by whom.
            /// </summary>
            public ConcurrentQueue<DNSPacket>  Requests  { get; } = new();

            /// <summary>
            /// How many channels the DNS subsystem was served on so far.
            /// </summary>
            public Int32                       Channels  => Volatile.Read(ref channels);

            /// <summary>
            /// How many of them ended: the client closed its channel, or the server did.
            /// </summary>
            public Int32                       ChannelsEnded  => Volatile.Read(ref channelsEnded);

            public Int32 ChannelOpened()
                => Interlocked.Increment(ref channels);

            public Int32 ChannelEnded()
                => Interlocked.Increment(ref channelsEnded);

            public Int32 Count<TEvent>() where TEvent : SshAuditEvent
                => Audit.Events.OfType<TEvent>().Count();

            public async ValueTask DisposeAsync()
            {
                await SSH.DisposeAsync();
                await DNS.Stop();
            }

        }

        /// <summary>
        /// "csms.example.org" as in appendix A of the white paper, and sixteen hosts with an address
        /// each, so that an answer handed to the wrong query shows.
        /// </summary>
        private static InMemoryDNSZone CreateTestZone()
        {

            var zone = new InMemoryDNSZone().
                           Add(new SOA(
                                   DomainName.        Parse("example.org."),
                                   DNSQueryClasses.IN,
                                   TimeSpan.          FromSeconds(300),
                                   DomainName.        Parse("ns.example.org."),
                                   SimpleEMailAddress.Parse("hostmaster@example.org"),
                                   1,
                                   TimeSpan.          FromSeconds(3600),
                                   TimeSpan.          FromSeconds(600),
                                   TimeSpan.          FromSeconds(86400),
                                   TimeSpan.          FromSeconds(300)
                               )).
                           Add(new A(
                                   DomainName. Parse("csms.example.org."),
                                   DNSQueryClasses.IN,
                                   TimeSpan.   FromSeconds(300),
                                   IPv4Address.Parse("192.0.2.10")
                               )).
                           Add(new AAAA(
                                   DomainName. Parse("csms.example.org."),
                                   DNSQueryClasses.IN,
                                   TimeSpan.   FromSeconds(300),
                                   IPv6Address.Parse("2001:db8::10")
                               ));

            for (var i = 0; i < 16; i++)
                zone.Add(new A(
                             DomainName. Parse($"host{i:D2}.example.org."),
                             DNSQueryClasses.IN,
                             TimeSpan.   FromSeconds(300),
                             IPv4Address.Parse($"192.0.2.{100 + i}")
                         ));

            return zone;

        }

        /// <summary>
        /// An SSH server that hands the DNS subsystem to a DNS server without a listener of its own -
        /// for every login but <see cref="OCPPOnly"/> - or to the given script instead.
        /// </summary>
        /// <param name="ReadTimeout">After how long the DNS server closes an idle channel.</param>
        /// <param name="Script">Serves the DNS subsystem in place of the DNS server: the number of the channel, 1 for the first one, and the channel.</param>
        private async Task<Running> StartAsync(CancellationToken                                      CancellationToken,
                                               TimeSpan?                                              ReadTimeout   = null,
                                               Func<Int32, SshMuxChannel, CancellationToken, Task>?   Script        = null)
        {

            var dns      = new DNSServer(
                               new AuthoritativeDNSRequestHandler(CreateTestZone()),
                               new DNSServerOptions {
                                   EnableUDPUnicast    = false,
                                   EnableUDPMulticast  = false,
                                   EnableTCPUnicast    = false,
                                   TCPReadTimeout      = ReadTimeout ?? TimeSpan.FromSeconds(30)
                               }
                           );

            var audit    = new CollectingAuditSink();
            Running? running = null;

            dns.OnDNSRequestReceived += (timestamp, server, serverType, request, ct) => {
                running!.Requests.Enqueue(request);
                return Task.CompletedTask;
            };

            var ssh      = new SshServer(new SshServerOptions {
                               HostKeys       = [ hostKey ],
                               Authenticator  = SshUserAuthenticator.ForAuthorizedKeys(stationKey.PublicKeyBlob),
                               AuditSink      = audit,
                               Subsystems     = (session, name, ct) => ValueTask.FromResult<SshSubsystemHandler?>(name switch {

                                                    DNSServer.SSHSubsystemName when session.Username != OCPPOnly
                                                        => async (context, token) => {

                                                               var number = running!.ChannelOpened();

                                                               try
                                                               {

                                                                   if (Script is null)
                                                                       await dns.ServeSSHSubsystemAsync(context, token);

                                                                   else
                                                                       await Script(number, context.Channel, token);

                                                               }
                                                               catch (OperationCanceledException)
                                                               { }
                                                               finally
                                                               {
                                                                   running.ChannelEnded();
                                                               }

                                                           },

                                                    Echo
                                                        => async (context, token) => {
                                                               await context.Channel.SendDataAsync("echo\n"u8.ToArray(), token);
                                                           },

                                                    _   => null

                                                })
                           });

            running = new Running(dns, ssh, audit);

            await ssh.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);

            return running;

        }

        private SshClientOptions Login(String Username = "cs001")

            => new () {
                   Username       = Username,
                   VerifyHostKey  = blob => blob.AsSpan().SequenceEqual(hostKey.PublicKeyBlob),
                   Credentials    = [ stationKey ]
               };

        /// <summary>
        /// A client with an SSH connection of its own.
        /// </summary>
        private DNSSSHClient Client(Running Running, String Username = "cs001")

            => new (
                   "127.0.0.1",
                   Login(Username),
                   IPPort.Parse(Running.Port),
                   QueryTimeout: Timeout
               );

        private static String[] AddressesOf(DNSInfo Answer)
            => [.. Answer.Answers.OfType<A>().Select(a => a.IPv4Address.ToString())];

        private static async Task Eventually(Func<Boolean> Holds, String What)
        {

            var since = Stopwatch.GetTimestamp();

            while (!Holds())
            {

                if (Stopwatch.GetElapsedTime(since) > TimeSpan.FromSeconds(10))
                    Assert.Fail($"{What} did not happen within 10 s.");

                await Task.Delay(20);

            }

        }

        /// <summary>
        /// The next message of a channel, without its length; null where the client ended the channel.
        /// </summary>
        private static async Task<Byte[]?> ReadQueryAsync(SshMuxChannel Channel, CancellationToken CancellationToken)
        {

            var length = new Byte[2];

            if (await Channel.Input.ReadAtLeastAsync(length, 2, throwOnEndOfStream: false, CancellationToken) < 2)
                return null;

            var message = new Byte[(length[0] << 8) | length[1]];

            await Channel.Input.ReadExactlyAsync(message, CancellationToken);

            return message;

        }

        /// <summary>
        /// Write a message with its two-octet length, in one write.
        /// </summary>
        private static ValueTask WriteAnswerAsync(SshMuxChannel Channel, Byte[] Message, CancellationToken CancellationToken)

            => Channel.SendDataAsync((Byte[]) [ (Byte) (Message.Length >> 8), (Byte) Message.Length, .. Message ], CancellationToken);

        /// <summary>
        /// What an "echo" channel of the given connection says: that the connection carries other
        /// subsystems, still.
        /// </summary>
        private static async Task<String> EchoAsync(SshClient Ssh, CancellationToken CancellationToken)
        {

            var channel  = await Ssh.OpenSubsystemAsync(Echo, CancellationToken);

            using var within = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            within.CancelAfter(TimeSpan.FromSeconds(10));

            var said     = new MemoryStream();
            await channel.Input.CopyToAsync(said, within.Token);

            return System.Text.Encoding.UTF8.GetString(said.ToArray());

        }

        #endregion


        #region AQuery_IsAnswered_AndTheServerKnowsTheClientByItsLogin(...)

        /// <summary>
        /// One query over a connection of the client's own, made at that first query: the answer of
        /// the zone, from an origin that says SSH; and at the server the login as the identity of the
        /// request, and a query padded as an encrypted transport's is (RFC 8467: to 128 octets).
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task AQuery_IsAnswered_AndTheServerKnowsTheClientByItsLogin(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);
            await using var client  = Client(running);

            Assert.That(running.Count<ConnectionOpenedEvent>(), Is.EqualTo(0), "the client connected before it was asked anything");

            var answer = await client.Query(DomainName.Parse("csms.example.org."), [ DNSResourceRecordTypes.A ], CancellationToken: CancellationToken);

            Assert.Multiple(() => {

                Assert.That(answer.IsValid,                    Is.True);
                Assert.That(answer.ResponseCode,               Is.EqualTo(DNSResponseCodes.NoError));
                Assert.That(AddressesOf(answer),               Is.EqualTo(new[] { "192.0.2.10" }));
                Assert.That(answer.Origin.Transport,           Is.EqualTo(DNSTransport.SSH));
                Assert.That(answer.Origin.Port,                Is.EqualTo(IPPort.Parse(running.Port)));

                Assert.That(running.Requests.Count,            Is.EqualTo(1));
                Assert.That(running.Requests.Select(request => request.ClientIdentity),          Is.EqualTo(new[] { "cs001" }));
                Assert.That(running.Requests.Select(request => DNSPadding.IsPadded(request)),    Is.EqualTo(new[] { true }), "the query went out unpadded");

                Assert.That(client.ChannelsOpened,             Is.EqualTo(1));
                Assert.That(client.ConnectionsMade,            Is.EqualTo(1));
                Assert.That(client.OwnsConnection,             Is.True);
                Assert.That(client.LastConnectionError,        Is.Null);
                Assert.That(client.Connection?.Username,       Is.EqualTo("cs001"));

            });

        }

        #endregion

        #region AQueryForTwoTypes_IsTwoQueriesOfOneQuestionEach_OnOneChannel(...)

        /// <summary>
        /// RFC 9619: a query for A and AAAA is two messages of one question each. Both go over the one
        /// channel, and their answers come back as one.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task AQueryForTwoTypes_IsTwoQueriesOfOneQuestionEach_OnOneChannel(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);
            await using var client  = Client(running);

            var answer = await client.Query(DomainName.Parse("csms.example.org."), [ DNSResourceRecordTypes.A, DNSResourceRecordTypes.AAAA ], CancellationToken: CancellationToken);

            Assert.Multiple(() => {

                Assert.That(AddressesOf(answer),                                                         Is.EqualTo(new[] { "192.0.2.10" }));
                Assert.That(answer.Answers.OfType<AAAA>().Select(aaaa => aaaa.IPv6Address),              Is.EqualTo(new[] { IPv6Address.Parse("2001:db8::10") }));

                Assert.That(running.Requests.Select(request => request.Questions.Count()),               Is.EqualTo(new[] { 1, 1 }));
                Assert.That(running.Requests.Select(request => request.Questions.First().QueryType),     Is.EquivalentTo(new[] { DNSResourceRecordTypes.A, DNSResourceRecordTypes.AAAA }));

                Assert.That(running.Channels,                                                            Is.EqualTo(1));
                Assert.That(client.ChannelsOpened,                                                       Is.EqualTo(1));

            });

        }

        #endregion

        #region ConcurrentQueries_GoOverOneChannel_EachWithItsOwnAnswer(...)

        /// <summary>
        /// Sixteen queries at once, for sixteen names: one SSH connection, one channel, one
        /// authentication - and every query gets the address of the name it asked for.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task ConcurrentQueries_GoOverOneChannel_EachWithItsOwnAnswer(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);
            await using var client  = Client(running);

            var answers = await Task.WhenAll(
                                    Enumerable.Range(0, 16).
                                               Select(i => client.Query(DomainName.Parse($"host{i:D2}.example.org."), [ DNSResourceRecordTypes.A ], CancellationToken: CancellationToken))
                                );

            Assert.Multiple(() => {

                for (var i = 0; i < 16; i++)
                    Assert.That(AddressesOf(answers[i]), Is.EqualTo(new[] { $"192.0.2.{100 + i}" }), $"the answer to the query for host{i:D2}");

                Assert.That(running.Channels,                              Is.EqualTo(1), "the server served more than one DNS channel");
                Assert.That(running.Count<ConnectionOpenedEvent>(),        Is.EqualTo(1));
                Assert.That(running.Count<AuthenticationSucceededEvent>(), Is.EqualTo(1));
                Assert.That(running.Requests.Count,                        Is.EqualTo(16));

                Assert.That(client.ChannelsOpened,                         Is.EqualTo(1));
                Assert.That(client.ConnectionsMade,                        Is.EqualTo(1));

            });

        }

        #endregion

        #region Queries_ArePipelined_AndAnswersOutOfOrderAreMatchedByID(...)

        /// <summary>
        /// The server reads two queries before it answers either, and answers the second one first.
        /// That it gets to read the second at all shows that the client did not wait for the first
        /// answer before it wrote it (RFC 7766 §6.2.1.1); and each query gets its own answer although
        /// they came the other way round (RFC 7858 §3.3).
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task Queries_ArePipelined_AndAnswersOutOfOrderAreMatchedByID(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken, Script: async (number, channel, token) => {

                var first   = await ReadQueryAsync(channel, token);
                var second  = await ReadQueryAsync(channel, token);

                // The answer says which query it answers by its last octet: 192.0.2.2, then 192.0.2.1.
                var answerToSecond = ScriptedDNSTCPServer.Answer(second!);
                answerToSecond[^1] = 2;

                await WriteAnswerAsync(channel, answerToSecond,                       token);
                await WriteAnswerAsync(channel, ScriptedDNSTCPServer.Answer(first!),  token);

                while (await ReadQueryAsync(channel, token) is not null)
                { }

            });

            await using var client  = Client(running);

            // The channel is opened first, so that the order the two queries are written in is the
            // order they are asked in.
            await client.ConnectAsync(CancellationToken);

            var first   = client.Query(DomainName.Parse("one.example."), [ DNSResourceRecordTypes.A ], CancellationToken: CancellationToken);
            await Task.Delay(100, CancellationToken);
            var second  = client.Query(DomainName.Parse("two.example."), [ DNSResourceRecordTypes.A ], CancellationToken: CancellationToken);

            var answers = await Task.WhenAll(first, second);

            Assert.Multiple(() => {
                Assert.That(AddressesOf(answers[0]),  Is.EqualTo(new[] { "192.0.2.1" }), "the first query got the answer to the second");
                Assert.That(AddressesOf(answers[1]),  Is.EqualTo(new[] { "192.0.2.2" }), "the second query got the answer to the first");
                Assert.That(running.Channels,         Is.EqualTo(1));
            });

        }

        #endregion

        #region AfterTheServerClosedTheIdleChannel_TheNextQueryOpensANewOneOnTheSameConnection(...)

        /// <summary>
        /// T19 from the client's side: the server closes the channel that was idle for its timeout.
        /// The next query is answered all the same, over a second channel of the same SSH connection -
        /// one connection and one authentication at the server, two channels.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task AfterTheServerClosedTheIdleChannel_TheNextQueryOpensANewOneOnTheSameConnection(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken, ReadTimeout: TimeSpan.FromMilliseconds(400));
            await using var client  = Client(running);

            var first      = await client.Query(DomainName.Parse("host01.example.org."), [ DNSResourceRecordTypes.A ], CancellationToken: CancellationToken);
            var connection = client.Connection;

            Assert.That(AddressesOf(first), Is.EqualTo(new[] { "192.0.2.101" }));
            Assert.That(connection,         Is.Not.Null);

            // The server ends the channel, and not the connection.
            await Eventually(() => running.ChannelsEnded == 1, "The server closing the idle channel");
            await Task.Delay(100, CancellationToken);

            Assert.That(running.Count<ConnectionClosedEvent>(), Is.EqualTo(0), "the positive control: the server ended the connection, not just the channel");

            var second     = await client.Query(DomainName.Parse("host02.example.org."), [ DNSResourceRecordTypes.A ], CancellationToken: CancellationToken);

            Assert.Multiple(() => {

                Assert.That(AddressesOf(second),                           Is.EqualTo(new[] { "192.0.2.102" }));

                Assert.That(client.ChannelsOpened,                         Is.EqualTo(2));
                Assert.That(client.ConnectionsMade,                        Is.EqualTo(1));
                Assert.That(client.Connection,                             Is.SameAs(connection));

                Assert.That(running.Channels,                              Is.EqualTo(2));
                Assert.That(running.Count<ConnectionOpenedEvent>(),        Is.EqualTo(1), "the client made a second SSH connection");
                Assert.That(running.Count<AuthenticationSucceededEvent>(), Is.EqualTo(1), "the client authenticated a second time");

            });

        }

        #endregion

        #region AChannelThatEndsUnderAQuery_IsReplaced_AndTheQueryAskedAgain(...)

        /// <summary>
        /// Section 3.3 of the white paper: "a client re-sends the queries left unanswered on a closed
        /// channel over a new one." The first channel ends on the first query it reads; the query is
        /// answered over the second - once more, not for ever: a server that ends every channel gets a
        /// failed query, not a loop.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task AChannelThatEndsUnderAQuery_IsReplaced_AndTheQueryAskedAgain(CancellationToken CancellationToken)
        {

            var endEveryChannel = false;

            await using var running = await StartAsync(CancellationToken, Script: async (number, channel, token) => {

                while (await ReadQueryAsync(channel, token) is Byte[] query)
                {

                    // Returning ends the channel: exit-status, EOF and CLOSE, and no answer.
                    if (number == 1 || endEveryChannel)
                        return;

                    await WriteAnswerAsync(channel, ScriptedDNSTCPServer.Answer(query), token);

                }

            });

            await using var client  = Client(running);

            var answer = await client.Query(DomainName.Parse("one.example."), [ DNSResourceRecordTypes.A ], CancellationToken: CancellationToken);

            Assert.Multiple(() => {
                Assert.That(AddressesOf(answer),                     Is.EqualTo(new[] { "192.0.2.1" }));
                Assert.That(running.Channels,                        Is.EqualTo(2));
                Assert.That(client.ChannelsOpened,                   Is.EqualTo(2));
                Assert.That(client.ConnectionsMade,                  Is.EqualTo(1));
                Assert.That(running.Count<ConnectionOpenedEvent>(),  Is.EqualTo(1));
            });

            endEveryChannel = true;

            var asked  = Stopwatch.GetTimestamp();
            var failed = await client.Query(DomainName.Parse("two.example."), [ DNSResourceRecordTypes.A ], CancellationToken: CancellationToken);

            Assert.Multiple(() => {
                Assert.That(failed.IsValid,                          Is.False);
                Assert.That(failed.IsTimeout,                        Is.False,                 "a channel that ended twice was reported as a timeout");
                Assert.That(Stopwatch.GetElapsedTime(asked),         Is.LessThan(Timeout),     "the query waited out its timeout");
                Assert.That(running.Channels,                        Is.EqualTo(3),            "the query was not asked exactly twice: on the channel that lasted, and on one more");
                Assert.That(running.Count<ConnectionOpenedEvent>(),  Is.EqualTo(1));
            });

        }

        #endregion

        #region AKeepaliveTimeout_ClosesTheChannelBeforeItExpires_AndNotTheConnection(...)

        /// <summary>
        /// Section 3.4 of the white paper: the timeout of edns-tcp-keepalive (RFC 7828) "applies to the
        /// channel". The server advertises two seconds; the client closes its channel before they are over
        /// - the server reads the end of it - and keeps the SSH connection, on which the next query
        /// opens a new channel.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task AKeepaliveTimeout_ClosesTheChannelBeforeItExpires_AndNotTheConnection(CancellationToken CancellationToken)
        {

            var ended = new ConcurrentQueue<(Int32 Channel, TimeSpan AfterTheAnswer)>();

            await using var running = await StartAsync(CancellationToken, Script: async (number, channel, token) => {

                var answered = 0L;

                try
                {
                    while (await ReadQueryAsync(channel, token) is Byte[] query)
                    {
                        // TIMEOUT is in units of 100 ms.
                        await WriteAnswerAsync(channel, ScriptedDNSTCPServer.Answer(query, 20), token);
                        answered = Stopwatch.GetTimestamp();
                    }
                }
                finally
                {
                    // The client's EOF, or its CLOSE right behind it - whichever the server saw first.
                    ended.Enqueue((number, Stopwatch.GetElapsedTime(answered)));
                }

            });

            await using var client  = Client(running);

            var first      = await client.Query(DomainName.Parse("one.example."), [ DNSResourceRecordTypes.A ], CancellationToken: CancellationToken);
            var connection = client.Connection;

            Assert.That(AddressesOf(first),             Is.EqualTo(new[] { "192.0.2.1" }));
            Assert.That(client.ServerKeepaliveTimeout,  Is.EqualTo(TimeSpan.FromSeconds(2)));

            await Eventually(() => !ended.IsEmpty, "The client closing its channel");

            Assert.Multiple(() => {
                Assert.That(ended.Single().Channel,          Is.EqualTo(1));
                Assert.That(ended.Single().AfterTheAnswer,   Is.InRange(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)), "the client did not close its channel shortly before the timeout the server advertised");
                Assert.That(client.Connection,               Is.SameAs(connection),                       "the SSH connection went with the channel");
            });

            var second     = await client.Query(DomainName.Parse("two.example."), [ DNSResourceRecordTypes.A ], CancellationToken: CancellationToken);

            Assert.Multiple(() => {
                Assert.That(AddressesOf(second),                     Is.EqualTo(new[] { "192.0.2.1" }));
                Assert.That(running.Channels,                        Is.EqualTo(2));
                Assert.That(client.ConnectionsMade,                  Is.EqualTo(1));
                Assert.That(running.Count<ConnectionOpenedEvent>(),  Is.EqualTo(1));
            });

        }

        #endregion

        #region AnOwnConnectionThatEnded_IsMadeAgainAtTheNextQuery(...)

        /// <summary>
        /// The SSH connection of the client's own ends between two queries. The next query dials a new
        /// one: two connections and two authentications at the server.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task AnOwnConnectionThatEnded_IsMadeAgainAtTheNextQuery(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);
            await using var client  = Client(running);

            var first = await client.Query(DomainName.Parse("host01.example.org."), [ DNSResourceRecordTypes.A ], CancellationToken: CancellationToken);
            Assert.That(AddressesOf(first), Is.EqualTo(new[] { "192.0.2.101" }));

            // The connection goes, as a connection does: without the DNS client being told.
            await client.Connection!.DisposeAsync();
            Assert.That(client.Connection, Is.Null);

            var second = await client.Query(DomainName.Parse("host02.example.org."), [ DNSResourceRecordTypes.A ], CancellationToken: CancellationToken);

            Assert.Multiple(() => {
                Assert.That(AddressesOf(second),                           Is.EqualTo(new[] { "192.0.2.102" }));
                Assert.That(client.ConnectionsMade,                        Is.EqualTo(2));
                Assert.That(client.ChannelsOpened,                         Is.EqualTo(2));
                Assert.That(running.Count<AuthenticationSucceededEvent>(), Is.EqualTo(2));
            });

        }

        #endregion

        #region AnOwnConnection_IsClosedWhenTheClientIsDisposed(...)

        /// <summary>
        /// Disposing of a client with a connection of its own closes that connection - the server sees
        /// it go - and a query asked afterwards is refused.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task AnOwnConnection_IsClosedWhenTheClientIsDisposed(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);

            var client     = Client(running);
            var answer     = await client.Query(DomainName.Parse("host01.example.org."), [ DNSResourceRecordTypes.A ], CancellationToken: CancellationToken);
            var connection = client.Connection;

            Assert.That(AddressesOf(answer),                    Is.EqualTo(new[] { "192.0.2.101" }));
            Assert.That(connection,                             Is.Not.Null);
            Assert.That(running.Count<ConnectionClosedEvent>(), Is.EqualTo(0));

            await client.DisposeAsync();

            await Eventually(() => running.Count<ConnectionClosedEvent>() == 1, "The server seeing the connection go");

            Assert.That(connection!.Closed.IsCompleted, Is.True);
            Assert.That(client.Connection,              Is.Null);

            await Assert.ThrowsAsync<ObjectDisposedException>(async () => await client.Query(DomainName.Parse("host02.example.org."), [ DNSResourceRecordTypes.A ]));

        }

        #endregion

        #region AHandedConnection_IsNotClosedWhenTheClientIsDisposed_AndCarriesOtherSubsystems(...)

        /// <summary>
        /// T12 in small: a connection others share - an <see cref="SshSharedConnection"/> - is made by
        /// the DNS client's first query, carries another subsystem beside the DNS channel, and goes on
        /// after the DNS client was disposed of: it closed its channel, and nothing else.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task AHandedConnection_IsNotClosedWhenTheClientIsDisposed_AndCarriesOtherSubsystems(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);
            await using var shared  = new SshSharedConnection("127.0.0.1", running.Port, Login());

            var client = new DNSSSHClient(shared.GetAsync, QueryTimeout: Timeout);

            Assert.That(shared.Made, Is.EqualTo(0));

            var answer = await client.Query(DomainName.Parse("host01.example.org."), [ DNSResourceRecordTypes.A ], CancellationToken: CancellationToken);
            var ssh    = shared.Current;

            Assert.Multiple(() => {
                Assert.That(AddressesOf(answer),         Is.EqualTo(new[] { "192.0.2.101" }));
                Assert.That(answer.Origin.Transport,     Is.EqualTo(DNSTransport.SSH));
                Assert.That(answer.Origin.Port,          Is.EqualTo(IPPort.Parse(running.Port)), "the origin of an answer over a handed connection is not where that connection goes");
                Assert.That(shared.Made,                 Is.EqualTo(1),   "the query did not have the shared connection made");
                Assert.That(ssh,                         Is.Not.Null);
                Assert.That(client.Connection,           Is.SameAs(ssh));
                Assert.That(client.OwnsConnection,       Is.False);
            });

            // Beside the DNS channel.
            Assert.That(await EchoAsync(ssh!, CancellationToken), Is.EqualTo("echo\n"));

            await client.DisposeAsync();

            // The server sees the DNS channel go.
            await Eventually(() => running.ChannelsEnded == 1, "The server seeing the DNS channel closed");

            Assert.Multiple(() => {
                Assert.That(ssh!.Closed.IsCompleted,                 Is.False, "the client closed a connection it was handed");
                Assert.That(shared.Current,                          Is.SameAs(ssh));
                Assert.That(running.Count<ConnectionClosedEvent>(),  Is.EqualTo(0));
            });

            // After it.
            Assert.That(await EchoAsync(ssh!, CancellationToken), Is.EqualTo("echo\n"));

            Assert.Multiple(() => {
                Assert.That(shared.Made,                                   Is.EqualTo(1));
                Assert.That(running.Count<ConnectionOpenedEvent>(),        Is.EqualTo(1));
                Assert.That(running.Count<AuthenticationSucceededEvent>(), Is.EqualTo(1), "one SSH authentication for the DNS channel and the two others");
            });

        }

        #endregion

        #region AHandedConnectionThatEnded_IsAskedForAgain(...)

        /// <summary>
        /// The connection the client was handed ends. At its next query the client asks whoever hands
        /// it out again, and gets the next one - which it has not made itself, and does not close.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task AHandedConnectionThatEnded_IsAskedForAgain(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);
            await using var shared  = new SshSharedConnection("127.0.0.1", running.Port, Login());

            var handedOut = 0;

            await using var client  = new DNSSSHClient(
                                          cancellationToken => {
                                              Interlocked.Increment(ref handedOut);
                                              return shared.GetAsync(cancellationToken);
                                          },
                                          QueryTimeout: Timeout
                                      );

            var first  = await client.Query(DomainName.Parse("host01.example.org."), [ DNSResourceRecordTypes.A ], CancellationToken: CancellationToken);
            var second = await client.Query(DomainName.Parse("host02.example.org."), [ DNSResourceRecordTypes.A ], CancellationToken: CancellationToken);

            Assert.That(AddressesOf(first),   Is.EqualTo(new[] { "192.0.2.101" }));
            Assert.That(AddressesOf(second),  Is.EqualTo(new[] { "192.0.2.102" }));
            Assert.That(handedOut,            Is.EqualTo(1), "the client asked for a connection while the one it had lasted");

            var before = shared.Current!;

            // The server ends every connection it has; the shared connection has none that lasts.
            await before.DisposeAsync();
            Assert.That(shared.Current, Is.Null);

            var third  = await client.Query(DomainName.Parse("host03.example.org."), [ DNSResourceRecordTypes.A ], CancellationToken: CancellationToken);

            Assert.Multiple(() => {
                Assert.That(AddressesOf(third),          Is.EqualTo(new[] { "192.0.2.103" }));
                Assert.That(handedOut,                   Is.EqualTo(2));
                Assert.That(shared.Made,                 Is.EqualTo(2));
                Assert.That(client.ConnectionsMade,      Is.EqualTo(2));
                Assert.That(client.Connection,           Is.SameAs(shared.Current));
                Assert.That(client.Connection,           Is.Not.SameAs(before));
            });

        }

        #endregion

        #region ARefusedSubsystem_IsAFailureOfItsOwn_NotATimeout(...)

        /// <summary>
        /// T11 from the client's side: the server does not offer the subsystem to this login and
        /// answers SSH_MSG_CHANNEL_FAILURE. The query fails at once - it does not time out - and the
        /// client says why: an <see cref="SshSubsystemRefusedException"/>, kept as
        /// <see cref="DNSSSHClient.LastConnectionError"/> and thrown by
        /// <see cref="DNSSSHClient.ConnectAsync"/>. The SSH connection goes on.
        /// </summary>
        /// <remarks>
        /// The positive control: another login asks the same server the same way and is answered.
        /// </remarks>
        [Test]
        [CancelAfter(30000)]
        public async Task ARefusedSubsystem_IsAFailureOfItsOwn_NotATimeout(CancellationToken CancellationToken)
        {

            await using var running  = await StartAsync(CancellationToken);
            await using var refused  = Client(running, OCPPOnly);
            await using var admitted = Client(running, "cs001");

            var asked  = Stopwatch.GetTimestamp();
            var answer = await refused.Query(DomainName.Parse("csms.example.org."), [ DNSResourceRecordTypes.A ], CancellationToken: CancellationToken);
            var took   = Stopwatch.GetElapsedTime(asked);

            Assert.Multiple(() => {

                Assert.That(answer.IsValid,                    Is.False);
                Assert.That(answer.IsTimeout,                  Is.False,             "a refused subsystem was reported as a timeout");
                Assert.That(took,                              Is.LessThan(Timeout), "the query waited out its timeout");
                Assert.That(answer.Answers,                    Is.Empty);

                Assert.That(refused.LastConnectionError,       Is.InstanceOf<SshSubsystemRefusedException>());
                Assert.That((refused.LastConnectionError as SshSubsystemRefusedException)?.Subsystem,
                                                               Is.EqualTo(DNSServer.SSHSubsystemName));

                Assert.That(refused.ChannelsOpened,            Is.EqualTo(0));
                Assert.That(refused.Connection,                Is.Not.Null,          "the SSH connection ended with the refusal");
                Assert.That(running.Requests,                  Is.Empty);
                Assert.That(running.Channels,                  Is.EqualTo(0));

            });

            var thrown = await Assert.ThrowsAsync<SshSubsystemRefusedException>(async () => await refused.ConnectAsync(CancellationToken));

            Assert.That(thrown!.Subsystem,        Is.EqualTo(DNSServer.SSHSubsystemName));
            Assert.That(refused.ConnectionsMade,  Is.EqualTo(1), "asking again made a second SSH connection");

            // The connection of the refused login carries what the login may have.
            Assert.That(await EchoAsync(refused.Connection!, CancellationToken), Is.EqualTo("echo\n"));

            // The positive control.
            var control = await admitted.Query(DomainName.Parse("csms.example.org."), [ DNSResourceRecordTypes.A ], CancellationToken: CancellationToken);

            Assert.That(AddressesOf(control),            Is.EqualTo(new[] { "192.0.2.10" }));
            Assert.That(admitted.LastConnectionError,    Is.Null);

        }

        #endregion

        #region AHostKeyTheVerifierRejects_NoQueryIsSent(...)

        /// <summary>
        /// T5 from the client's side: the verifier does not take the server's host key. No query is
        /// sent - the server authenticates nobody, opens no channel, is asked nothing - and the query
        /// fails without waiting out its timeout.
        /// </summary>
        /// <remarks>
        /// The positive control: the server was reached. It opened a connection, and the key the
        /// verifier was shown is the server's - so what kept the query back was the verifier's answer,
        /// not a server that was not there.
        /// </remarks>
        [Test]
        [CancelAfter(30000)]
        public async Task AHostKeyTheVerifierRejects_NoQueryIsSent(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);

            var shown = new ConcurrentQueue<Byte[]>();

            await using var client  = new DNSSSHClient(
                                          "127.0.0.1",
                                          Login() with {
                                              VerifyHostKey = blob => {
                                                  shown.Enqueue(blob);
                                                  return false;
                                              }
                                          },
                                          IPPort.Parse(running.Port),
                                          QueryTimeout: Timeout
                                      );

            var asked  = Stopwatch.GetTimestamp();
            var answer = await client.Query(DomainName.Parse("csms.example.org."), [ DNSResourceRecordTypes.A ], CancellationToken: CancellationToken);
            var took   = Stopwatch.GetElapsedTime(asked);

            await Eventually(() => running.Count<ConnectionClosedEvent>() == 1, "The server seeing the connection go");

            Assert.Multiple(() => {

                Assert.That(answer.IsValid,                                Is.False);
                Assert.That(answer.IsTimeout,                              Is.False,             "a rejected host key was reported as a timeout");
                Assert.That(took,                                          Is.LessThan(Timeout), "the query waited out its timeout");

                Assert.That(client.LastConnectionError,                    Is.Not.Null);
                Assert.That(client.LastConnectionError,                    Is.Not.InstanceOf<SshSubsystemRefusedException>());
                Assert.That(client.Connection,                             Is.Null);
                Assert.That(client.ChannelsOpened,                         Is.EqualTo(0));

                // The positive control.
                Assert.That(running.Count<ConnectionOpenedEvent>(),        Is.EqualTo(1),        "the server was not reached at all");
                Assert.That(shown.Select(blob => Convert.ToHexString(blob)),
                                                                           Is.EqualTo(new[] { Convert.ToHexString(hostKey.PublicKeyBlob) }), "the verifier was not shown the server's host key");

                Assert.That(running.Count<AuthenticationSucceededEvent>(), Is.EqualTo(0),        "the client authenticated to a server whose key it rejected");
                Assert.That(running.Count<SubsystemRequestedEvent>(),      Is.EqualTo(0));
                Assert.That(running.Channels,                              Is.EqualTo(0));
                Assert.That(running.Requests,                              Is.Empty,             "a query reached the server");

            });

        }

        #endregion

        #region AServerOverSSH_IsNotMadeFromADNSServerConfigAlone(...)

        /// <summary>
        /// <see cref="DNSTransport.SSH"/> names where an answer came from, and has the port of SSH as
        /// its default. A <see cref="DNSClient"/> given such a server that does not say how its SSH
        /// connection is had refuses it by name, when it is given it: it has no login and no host key
        /// to make the connection with, and does not ask the server over something else instead.
        /// </summary>
        [Test]
        public void AServerOverSSH_IsNotMadeFromADNSServerConfigAlone()
        {

            var config = new DNSServerConfig(IPv4Address.Localhost, Transport: DNSTransport.SSH);

            Assert.That(config.Port,        Is.EqualTo(IPPort.SSH));
            Assert.That(config.ToString(),  Does.StartWith("ssh://127.0.0.1:22"));

            var refused = Assert.Throws<NotSupportedException>(() => new DNSClient(
                                                                         [ config ],
                                                                         SearchForIPv4DNSServers:  false,
                                                                         SearchForIPv6DNSServers:  false
                                                                     ));

            Assert.That(refused!.Message, Does.Contain(nameof(DNSServerConfig.OverSSH)));

            using var dnsClient = new DNSClient(
                                      [ new DNSServerConfig(IPv4Address.Localhost, Transport: DNSTransport.TCP) ],
                                      SearchForIPv4DNSServers:  false,
                                      SearchForIPv6DNSServers:  false
                                  );

            Assert.Throws<NotSupportedException>(() => dnsClient.SetDNSServers([ config ]));

            Assert.That(dnsClient.DNSServers.Select(dnsServer => dnsServer.Transport), Is.EqualTo(new[] { DNSTransport.TCP }), "a refused list of servers replaced the servers");

        }

        #endregion

        #region AServerOverSSH_IsOneOfTheServersOfADNSClient(...)

        /// <summary>
        /// A server over SSH is a server of a <see cref="DNSClient"/> like one over UDP, TCP or TLS:
        /// named by <see cref="DNSServerConfig.OverSSH(IIPAddress, SshClientOptions, IPPort?, TimeSpan?, String?)"/>,
        /// asked like the others, and its channel kept for the next query.
        /// </summary>
        [Test]
        [CancelAfter(30_000)]
        public async Task AServerOverSSH_IsOneOfTheServersOfADNSClient(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);

            var overSSH = DNSServerConfig.OverSSH(IPv4Address.Localhost, Login(), IPPort.Parse(running.Port));

            using var dnsClient = new DNSClient(
                                      [ overSSH ],
                                      SearchForIPv4DNSServers:  false,
                                      SearchForIPv6DNSServers:  false,
                                      UseQueryCache:            false
                                  );

            var first   = await dnsClient.Query(DomainName.Parse("csms.example.org"),   [ DNSResourceRecordTypes.A ], CancellationToken: CancellationToken);
            var second  = await dnsClient.Query(DomainName.Parse("host07.example.org"), [ DNSResourceRecordTypes.A ], CancellationToken: CancellationToken);

            Assert.Multiple(() => {
                Assert.That(overSSH.Transport,                                      Is.EqualTo(DNSTransport.SSH));
                Assert.That(AddressesOf(first),                                     Is.Not.Empty, "the server over SSH did not answer");
                Assert.That(AddressesOf(second),                                    Is.Not.Empty);
                Assert.That(AddressesOf(second),                                    Is.Not.EqualTo(AddressesOf(first)));
                Assert.That(running.Requests.Select(request => request.ClientIdentity).Distinct(), Is.EqualTo(new[] { "cs001" }), "the server was not asked over SSH");
                Assert.That(running.Channels,                                       Is.EqualTo(1), "the second query did not go over the channel of the first");
            });

        }

        #endregion

        #region AServerOverSSH_AnswersBesideAServerOverTCPThatDoesNot(...)

        /// <summary>
        /// A mixed group: a server over TCP nobody listens at, and a server over SSH. The answer is
        /// the SSH server's - and a positive control: taken out of the group, there is none.
        /// </summary>
        [Test]
        [CancelAfter(60_000)]
        public async Task AServerOverSSH_AnswersBesideAServerOverTCPThatDoesNot(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);

            // A port that was free a moment ago, and that nobody listens at.
            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            var nobodysPort = IPPort.Parse((UInt16) ((System.Net.IPEndPoint) listener.LocalEndpoint).Port);
            listener.Stop();

            var overTCP = new DNSServerConfig(IPv4Address.Localhost, nobodysPort, DNSTransport.TCP);
            var overSSH = DNSServerConfig.OverSSH(IPv4Address.Localhost, Login(), IPPort.Parse(running.Port));

            using var mixed = new DNSClient(
                                  [ overTCP, overSSH ],
                                  QueryTimeout:             TimeSpan.FromSeconds(5),
                                  SearchForIPv4DNSServers:  false,
                                  SearchForIPv6DNSServers:  false,
                                  UseQueryCache:            false
                              );

            using var alone = new DNSClient(
                                  [ overTCP ],
                                  QueryTimeout:             TimeSpan.FromSeconds(2),
                                  SearchForIPv4DNSServers:  false,
                                  SearchForIPv6DNSServers:  false,
                                  UseQueryCache:            false
                              );

            var answered    = await mixed.Query(DomainName.Parse("csms.example.org"), [ DNSResourceRecordTypes.A ], CancellationToken: CancellationToken);
            var unanswered  = await alone.Query(DomainName.Parse("csms.example.org"), [ DNSResourceRecordTypes.A ], CancellationToken: CancellationToken);

            Assert.Multiple(() => {
                Assert.That(mixed.DNSServers.Select(dnsServer => dnsServer.Transport),  Is.EquivalentTo(new[] { DNSTransport.TCP, DNSTransport.SSH }));
                Assert.That(AddressesOf(answered),                                      Is.Not.Empty, "the server over SSH did not answer beside one that does not");
                Assert.That(running.Requests.Count,                                     Is.GreaterThan(0));
                Assert.That(AddressesOf(unanswered),                                    Is.Empty,     "the server over TCP answered: this test shows nothing");
            });

        }

        #endregion

        #region AServerOverSSH_OnAConnectionSomebodyElseKeeps_IsOneOfTheServersOfADNSClient(...)

        /// <summary>
        /// A server over SSH on a connection somebody else keeps - an <see cref="SshSharedConnection"/>
        /// - is known by the name of its host, which nobody resolves, and the connection outlives the
        /// <see cref="DNSClient"/> that asked over it.
        /// </summary>
        [Test]
        [CancelAfter(30_000)]
        public async Task AServerOverSSH_OnAConnectionSomebodyElseKeeps_IsOneOfTheServersOfADNSClient(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);
            await using var shared  = new SshSharedConnection("127.0.0.1", running.Port, Login());

            var overSSH = DNSServerConfig.OverSSH(DomainName.Parse("lc.example.invalid"), shared.GetAsync);

            DNSInfo answer;

            using (var dnsClient = new DNSClient(
                                       [ overSSH ],
                                       SearchForIPv4DNSServers:  false,
                                       SearchForIPv6DNSServers:  false,
                                       UseQueryCache:            false
                                   ))
            {
                answer = await dnsClient.Query(DomainName.Parse("csms.example.org"), [ DNSResourceRecordTypes.A ], CancellationToken: CancellationToken);
            }

            var kept = shared.Current;

            Assert.Multiple(() => {
                Assert.That(AddressesOf(answer),        Is.Not.Empty, "the server on the shared connection did not answer");
                Assert.That(shared.Made,                Is.EqualTo(1));
                Assert.That(kept,                       Is.Not.Null,  "the connection somebody else keeps went with the DNS client");
                Assert.That(kept?.Closed.IsCompleted,   Is.False,     "the connection somebody else keeps was closed by the DNS client");
            });

            // And it still serves: another channel of it answers.
            await using var again = new DNSSSHClient(shared.GetAsync, QueryTimeout: Timeout);

            Assert.That(AddressesOf(await again.Query(DomainName.Parse("csms.example.org"), [ DNSResourceRecordTypes.A ], CancellationToken: CancellationToken)), Is.Not.Empty);
            Assert.That(shared.Made, Is.EqualTo(1));

        }

        #endregion


    }

}
