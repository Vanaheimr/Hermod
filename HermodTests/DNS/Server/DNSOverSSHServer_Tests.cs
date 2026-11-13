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
using System.Net.Sockets;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Client;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Server
{

    /// <summary>
    /// DNS over SSH, the server side: <see cref="DNSServer.ServeSSHSubsystemAsync"/> behind an
    /// <see cref="SshServer"/>, asked with raw octets on a channel of the subsystem
    /// "dns@open.charging.cloud" - the byte stream of DNS over TCP, unchanged.
    /// </summary>
    /// <remarks>
    /// No DNS client is used here, so that these tests say what the server does whatever a client of
    /// Hermod's does. The numbers T1 to T3 are those of the test criteria of the white paper
    /// "DNS over SSH" (chapter 11), the queries those of its appendix A.
    /// </remarks>
    [TestFixture]
    public class DNSOverSSHServer_Tests
    {

        #region (private) Fixture

        private const String Echo      = "echo@test.open.charging.cloud";

        /// <summary>
        /// A login the SSH server knows, and does not offer the DNS subsystem to.
        /// </summary>
        private const String OCPPOnly  = "ocpp-only";

        /// <summary>
        /// How many TXT records "big.example.org" has: an answer of a little under 60,000 octets.
        /// </summary>
        private const Int32  BigTXTs   = 260;

        /// <summary>
        /// Appendix A.1 of the white paper: the length 0x0080 and the query for "csms.example.org",
        /// type A, ID 0x0001, RD set, padded to 128 octets.
        /// </summary>
        private const String A1 =
            "0080" +
            "00010100000100000000000104637" + "36d73076578616d706c65036f726700" +
            "0001000100002904d0000000000053" + "000c004f" +
            "00000000000000000000000000000000000000000000000000000000000000000000000000000000" +
            "000000000000000000000000000000000000000000000000000000000000000000000000000000";

        private readonly ISshHostKey  hostKey     = SshHostKey.GenerateEd25519();
        private readonly ISshHostKey  stationKey  = SshHostKey.GenerateEd25519();

        /// <summary>
        /// What the DNS server was asked, over what, and by whom.
        /// </summary>
        private sealed record Asked(String ServerType, String? ClientIdentity, String Name);

        private sealed record Running(DNSServer DNS, SshServer SSH, UInt16 Port, Int32 TCPPort, CollectingAuditSink Audit, ConcurrentQueue<Asked> Requests) : IAsyncDisposable
        {
            public async ValueTask DisposeAsync()
            {
                await SSH.DisposeAsync();
                await DNS.Stop();
            }
        }

        /// <summary>
        /// The zone of appendix A - "csms.example.org" is 192.0.2.10 for 300 seconds - and a name with
        /// many TXT records.
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
                           Add(new NS(
                                   DomainName.Parse("example.org."),
                                   DNSQueryClasses.IN,
                                   TimeSpan.  FromSeconds(300),
                                   DomainName.Parse("ns.example.org.")
                               )).
                           Add(new A(
                                   DomainName. Parse("ns.example.org."),
                                   DNSQueryClasses.IN,
                                   TimeSpan.   FromSeconds(300),
                                   IPv4Address.Parse("192.0.2.53")
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

            for (var i = 0; i < BigTXTs; i++)
                zone.Add(new TXT(
                             DomainName.Parse("big.example.org."),
                             DNSQueryClasses.IN,
                             TimeSpan.  FromSeconds(300),
                             $"{i:D4} ".PadRight(200, 'x')
                         ));

            return zone;

        }

        /// <summary>
        /// A DNS server with a TCP listener on a free loopback port - for the same query over TCP - and
        /// an SSH server that hands the DNS subsystem to it for every login but <see cref="OCPPOnly"/>.
        /// </summary>
        private async Task<Running> StartAsync(CancellationToken  CancellationToken,
                                               TimeSpan?          ReadTimeout   = null)
        {

            var requests  = new ConcurrentQueue<Asked>();

            var dns       = new DNSServer(
                                new AuthoritativeDNSRequestHandler(CreateTestZone()),
                                new DNSServerOptions {
                                    EnableUDPUnicast    = false,
                                    EnableUDPMulticast  = false,
                                    EnableTCPUnicast    = true,
                                    TCPUnicastSocket    = new IPSocket(IPv4Address.Localhost, IPPort.Parse(0)),
                                    TCPReadTimeout      = ReadTimeout ?? TimeSpan.FromSeconds(30)
                                }
                            );

            dns.OnDNSRequestReceived += (timestamp, server, serverType, request, ct) => {
                requests.Enqueue(new Asked(serverType, request.ClientIdentity, request.Questions.First().DomainName.ToString()));
                return Task.CompletedTask;
            };

            await dns.Start();

            var audit     = new CollectingAuditSink();

            var ssh       = new SshServer(new SshServerOptions {
                                HostKeys       = [ hostKey ],
                                Authenticator  = SshUserAuthenticator.ForAuthorizedKeys(stationKey.PublicKeyBlob),
                                AuditSink      = audit,
                                Subsystems     = (session, name, ct) => ValueTask.FromResult<SshSubsystemHandler?>(name switch {

                                                     DNSServer.SSHSubsystemName when session.Username != OCPPOnly
                                                         => dns.ServeSSHSubsystemAsync,

                                                     Echo
                                                         => async (context, token) => {
                                                                await context.Channel.SendDataAsync("echo\n"u8.ToArray(), token);
                                                            },

                                                     _   => null

                                                 })
                            });

            await ssh.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);

            return new Running(
                       dns,
                       ssh,
                       (UInt16) ssh.LocalEndPoint.Port.ToInt32(),
                       dns.ActiveTCPUnicastSocket!.Value.Port.ToInt32(),
                       audit,
                       requests
                   );

        }

        private ValueTask<SshClient> ConnectAsync(Running            Running,
                                                  CancellationToken  CancellationToken,
                                                  String             Username   = "cs001")

            => SshClient.ConnectAsync(
                   "127.0.0.1",
                   Running.Port,
                   new SshClientOptions {
                       Username       = Username,
                       VerifyHostKey  = blob => blob.AsSpan().SequenceEqual(hostKey.PublicKeyBlob),
                       Credentials    = [ stationKey ]
                   },
                   CancellationToken
               );

        private static async Task<T> Within<T>(Task<T> Happening, String What)
        {
            if (await Task.WhenAny(Happening, Task.Delay(TimeSpan.FromSeconds(10))) != Happening)
                Assert.Fail($"{What} did not happen within 10 s.");
            return await Happening;
        }

        #endregion

        #region (private) Octets: queries as appendix A writes them, and messages off a stream

        /// <summary>
        /// A query as appendix A.1 of the white paper writes it, without its length: RD set, one
        /// question of class IN, and an OPT record with a UDP payload size of 1232 whose Padding option
        /// pads the message to 128 octets.
        /// </summary>
        private static Byte[] PaddedQuery(UInt16 Id, String Name, UInt16 Type)
        {

            var message = new MemoryStream();

            message.Write([ (Byte) (Id >> 8), (Byte) Id, 0x01, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01 ]);

            foreach (var label in Name.TrimEnd('.').Split('.'))
            {
                message.WriteByte((Byte) label.Length);
                message.Write(System.Text.Encoding.ASCII.GetBytes(label));
            }

            message.WriteByte(0);
            message.Write([ (Byte) (Type >> 8), (Byte) Type, 0x00, 0x01 ]);

            // The OPT record up to its RDLENGTH, and the header of the Padding option.
            var padding = 128 - ((Int32) message.Length + 11 + 4);

            message.Write([ 0x00, 0x00, 0x29, 0x04, 0xd0, 0x00, 0x00, 0x00, 0x00 ]);
            message.Write([ (Byte) ((padding + 4) >> 8), (Byte) (padding + 4), 0x00, 0x0c, (Byte) (padding >> 8), (Byte) padding ]);
            message.Write(new Byte[padding]);

            return message.ToArray();

        }

        /// <summary>
        /// A message with its two-octet length before it: what the stream carries.
        /// </summary>
        private static Byte[] Framed(Byte[] Message)

            => [ (Byte) (Message.Length >> 8), (Byte) Message.Length, .. Message ];

        /// <summary>
        /// The next message of a stream, without its length; null where the stream ended before one began.
        /// </summary>
        private static async Task<Byte[]?> ReadMessageAsync(Stream Stream, CancellationToken CancellationToken)
        {

            using var within = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            within.CancelAfter(TimeSpan.FromSeconds(10));

            var length = new Byte[2];

            if (await Stream.ReadAtLeastAsync(length, 2, throwOnEndOfStream: false, within.Token) < 2)
                return null;

            var message = new Byte[BinaryPrimitives.ReadUInt16BigEndian(length)];

            await Stream.ReadExactlyAsync(message, within.Token);

            return message;

        }

        /// <summary>
        /// A message as an answer to the given question.
        /// </summary>
        private static DNSInfo Parse(Byte[] Message, UInt16 Id, String Name, DNSResourceRecordTypes Type)

            => DNSInfo.ReadResponse(
                   new DNSServerConfig(IPv4Address.Localhost, IPPort.SSH),
                   Id,
                   [ new DNSQuestion(DNSServiceName.Parse(Name), Type, DNSQueryClasses.IN) ],
                   new MemoryStream(Message),
                   TimeSpan.FromSeconds(10),
                   TimeSpan.Zero
               );

        /// <summary>
        /// How a channel ended: what data was still to come, every exit-status the server sent before
        /// it closed the channel, and that it did close it.
        /// </summary>
        private static async Task<(Byte[] Data, UInt32[] ExitStatuses)> ToTheEndAsync(SshMuxChannel Channel)
        {

            using var within  = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            var data          = new MemoryStream();
            await Channel.Input.CopyToAsync(data, within.Token);

            var statuses      = new List<UInt32>();

            while (await Channel.ReadRequestAsync(within.Token) is SshChannelRequest request)
                if (request.Type == "exit-status")
                    statuses.Add(BinaryPrimitives.ReadUInt32BigEndian(request.Data));

            await Channel.Closed.WaitAsync(within.Token);

            return (data.ToArray(), [.. statuses]);

        }

        /// <summary>
        /// Ask for "csms.example.org" on a new channel of the given connection: the address it has
        /// there, which shows that the connection goes on and that the DNS server is behind it.
        /// </summary>
        private static async Task<String> AskOnANewChannelAsync(SshClient Ssh, UInt16 Id, CancellationToken CancellationToken)
        {

            var channel = await Ssh.OpenSubsystemAsync(DNSServer.SSHSubsystemName, CancellationToken);

            await channel.SendDataAsync(Framed(PaddedQuery(Id, "csms.example.org", 1)), CancellationToken);

            var answer  = await ReadMessageAsync(channel.Input, CancellationToken);

            await channel.CloseAsync(CancellationToken);

            return answer is null
                       ? "no answer"
                       : Parse(answer, Id, "csms.example.org.", DNSResourceRecordTypes.A).Answers.OfType<A>().Select(a => a.IPv4Address.ToString()).AggregateWith(",");

        }

        #endregion


        #region AppendixA1_IsTheQueryTheseTestsWrite()

        /// <summary>
        /// The vector of appendix A.1, octet for octet, is what <see cref="PaddedQuery"/> makes for it:
        /// the further queries of these tests are of the same make.
        /// </summary>
        [Test]
        public void AppendixA1_IsTheQueryTheseTestsWrite()
        {

            var vector = Convert.FromHexString(A1);

            Assert.Multiple(() => {
                Assert.That(vector.Length,                                         Is.EqualTo(130));
                Assert.That(Framed(PaddedQuery(0x0001, "csms.example.org", 1)),    Is.EqualTo(vector));
            });

        }

        #endregion

        #region T1_AQueryOverTheChannel_GetsTheAnswerItGetsOverTCP(...)

        /// <summary>
        /// T1: the query of appendix A.1 over the channel, and the same octets over TCP to the same
        /// server. The answers are the same, and the answer is the one the zone has.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task T1_AQueryOverTheChannel_GetsTheAnswerItGetsOverTCP(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);
            await using var ssh     = await ConnectAsync(running, CancellationToken);

            var query    = Convert.FromHexString(A1);

            var channel  = await ssh.OpenSubsystemAsync(DNSServer.SSHSubsystemName, CancellationToken);
            await channel.SendDataAsync(query, CancellationToken);
            var overSSH  = await ReadMessageAsync(channel.Input, CancellationToken);

            using var tcp = new TcpClient();
            await tcp.ConnectAsync(System.Net.IPAddress.Loopback, running.TCPPort, CancellationToken);
            await tcp.GetStream().WriteAsync(query, CancellationToken);
            var overTCP  = await ReadMessageAsync(tcp.GetStream(), CancellationToken);

            Assert.That(overSSH, Is.Not.Null, "no answer over the channel");
            Assert.That(overTCP, Is.Not.Null, "no answer over TCP");

            var sshAnswer = Parse(overSSH!, 0x0001, "csms.example.org.", DNSResourceRecordTypes.A);
            var tcpAnswer = Parse(overTCP!, 0x0001, "csms.example.org.", DNSResourceRecordTypes.A);

            Assert.Multiple(() => {

                Assert.That(sshAnswer.ResponseCode,                                          Is.EqualTo(DNSResponseCodes.NoError));
                Assert.That(sshAnswer.Answers.OfType<A>().Select(a => a.IPv4Address),        Is.EqualTo(new[] { IPv4Address.Parse("192.0.2.10") }));
                Assert.That(sshAnswer.Answers.OfType<A>().Select(a => a.TimeToLive),         Is.EqualTo(new[] { TimeSpan.FromSeconds(300) }));

                Assert.That(sshAnswer.Answers.Select(rr => rr.ToString()),                   Is.EqualTo(tcpAnswer.Answers.Select(rr => rr.ToString())), "the answer sections differ");
                Assert.That(sshAnswer.ResponseCode,                                          Is.EqualTo(tcpAnswer.ResponseCode));
                Assert.That(overSSH,                                                         Is.EqualTo(overTCP), "the answers differ in their octets");

                // T22, the server's half: the query was padded, so the answer is, to a multiple of 468.
                Assert.That(overSSH!.Length % 468,                                           Is.EqualTo(0), $"an answer of {overSSH.Length} octets is not padded to a multiple of 468");

            });

        }

        #endregion

        #region T2_ThreeQueriesInOneWrite_GetThreeAnswers_MatchedByID(...)

        /// <summary>
        /// T2: the three queries of appendix A in one write - "csms.example.org" for A (the vector of
        /// A.1) and for AAAA, "nosuch.example.org" for A. Three answers, each matched to its query by
        /// its ID, in whatever order they come: the address, the IPv6 address, and NXDOMAIN with the
        /// zone's SOA record.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task T2_ThreeQueriesInOneWrite_GetThreeAnswers_MatchedByID(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);
            await using var ssh     = await ConnectAsync(running, CancellationToken);

            var channel = await ssh.OpenSubsystemAsync(DNSServer.SSHSubsystemName, CancellationToken);

            Byte[] oneWrite = [
                .. Convert.FromHexString(A1),
                .. Framed(PaddedQuery(0x0002, "csms.example.org",   28)),
                .. Framed(PaddedQuery(0x0003, "nosuch.example.org",  1))
            ];

            await channel.SendDataAsync(oneWrite, CancellationToken);

            var answers = new Dictionary<UInt16, Byte[]>();

            for (var i = 0; i < 3; i++)
            {
                var answer = await ReadMessageAsync(channel.Input, CancellationToken);
                Assert.That(answer, Is.Not.Null, $"the channel ended after {i} of 3 answers");
                answers.Add(BinaryPrimitives.ReadUInt16BigEndian(answer), answer!);
            }

            Assert.That(answers.Keys, Is.EquivalentTo(new UInt16[] { 1, 2, 3 }));

            var first   = Parse(answers[1], 1, "csms.example.org.",   DNSResourceRecordTypes.A);
            var second  = Parse(answers[2], 2, "csms.example.org.",   DNSResourceRecordTypes.AAAA);
            var third   = Parse(answers[3], 3, "nosuch.example.org.", DNSResourceRecordTypes.A);

            Assert.Multiple(() => {

                Assert.That(first. IsValid,                                                  Is.True, "the answer with ID 1 does not answer the question of ID 1");
                Assert.That(first. Answers.OfType<A>().Select(a => a.IPv4Address),           Is.EqualTo(new[] { IPv4Address.Parse("192.0.2.10") }));

                Assert.That(second.IsValid,                                                  Is.True, "the answer with ID 2 does not answer the question of ID 2");
                Assert.That(second.Answers.OfType<AAAA>().Select(a => a.IPv6Address),        Is.EqualTo(new[] { IPv6Address.Parse("2001:db8::10") }));

                Assert.That(third. IsValid,                                                  Is.True, "the answer with ID 3 does not answer the question of ID 3");
                Assert.That(third. ResponseCode,                                             Is.EqualTo(DNSResponseCodes.NameError));
                Assert.That(third. Answers,                                                  Is.Empty);
                Assert.That(third. Authorities.OfType<SOA>().Count(),                        Is.EqualTo(1), "NXDOMAIN came without the SOA record of the zone");

                Assert.That(answers.Values.Select(answer => answer.Length % 468),            Is.All.EqualTo(0), "an answer to a padded query is not padded to a multiple of 468 octets");

            });

            // And nothing else: the channel is still open, and holds no fourth message.
            Assert.That(running.Requests.Count(asked => asked.ServerType == DNSServer.SSHServerType), Is.EqualTo(3));

        }

        #endregion

        #region T3_AQueryWrittenOneOctetPerWrite_IsAnswered(...)

        /// <summary>
        /// T3, the first half: the 130 octets of appendix A.1 in 130 writes, each an
        /// SSH_MSG_CHANNEL_DATA of its own. The server does not end the channel because a read held
        /// only part of a message (RFC 7766, section 8), and answers once the last octet is in.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task T3_AQueryWrittenOneOctetPerWrite_IsAnswered(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);
            await using var ssh     = await ConnectAsync(running, CancellationToken);

            var channel = await ssh.OpenSubsystemAsync(DNSServer.SSHSubsystemName, CancellationToken);
            var query   = Convert.FromHexString(A1);

            for (var i = 0; i < query.Length; i++)
            {

                // Nothing is answered, and nothing ends, before the last octet went out.
                if (i == query.Length - 1)
                {
                    await Task.Delay(200, CancellationToken);
                    Assert.That(running.Requests,           Is.Empty,  "the server took part of a message for a query");
                    Assert.That(channel.Closed.IsCompleted, Is.False,  "the server closed the channel on part of a message");
                }

                await channel.SendDataAsync(query.AsMemory(i, 1), CancellationToken);

            }

            var answer = await ReadMessageAsync(channel.Input, CancellationToken);

            Assert.That(answer, Is.Not.Null, "no answer");
            Assert.That(Parse(answer!, 0x0001, "csms.example.org.", DNSResourceRecordTypes.A).Answers.OfType<A>().Select(a => a.IPv4Address),
                        Is.EqualTo(new[] { IPv4Address.Parse("192.0.2.10") }));

        }

        #endregion

        #region T3_AnAnswerOfNearly60000Octets_ArrivesWhole(...)

        /// <summary>
        /// T3, the second half: an answer longer than one SSH packet may carry - 260 TXT records of
        /// 200 octets each - arrives as one message under one length, every record in it.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task T3_AnAnswerOfNearly60000Octets_ArrivesWhole(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);
            await using var ssh     = await ConnectAsync(running, CancellationToken);

            var channel = await ssh.OpenSubsystemAsync(DNSServer.SSHSubsystemName, CancellationToken);

            // Two of them and a short one behind: a long answer does not run into the next one either.
            Byte[] oneWrite = [
                .. Framed(PaddedQuery(0x0010, "big.example.org",  16)),
                .. Framed(PaddedQuery(0x0011, "big.example.org",  16)),
                .. Framed(PaddedQuery(0x0012, "csms.example.org",  1))
            ];

            await channel.SendDataAsync(oneWrite, CancellationToken);

            var first   = await ReadMessageAsync(channel.Input, CancellationToken);
            var second  = await ReadMessageAsync(channel.Input, CancellationToken);
            var third   = await ReadMessageAsync(channel.Input, CancellationToken);

            Assert.That(first,  Is.Not.Null);
            Assert.That(second, Is.Not.Null);
            Assert.That(third,  Is.Not.Null);

            var big1    = Parse(first!,  0x0010, "big.example.org.",  DNSResourceRecordTypes.TXT);
            var big2    = Parse(second!, 0x0011, "big.example.org.",  DNSResourceRecordTypes.TXT);
            var small   = Parse(third!,  0x0012, "csms.example.org.", DNSResourceRecordTypes.A);

            Assert.Multiple(() => {

                Assert.That(first!.Length,                                  Is.InRange(55000, 65535), "the answer is not the long one this test is about");
                Assert.That(first. Length,                                  Is.GreaterThan(32768),    "the answer fits one SSH packet");
                Assert.That(big1.  IsTruncated,                             Is.False);
                Assert.That(big1.  Answers.OfType<TXT>().Count(),           Is.EqualTo(BigTXTs));
                Assert.That(big1.  Answers.OfType<TXT>().Select(txt => txt.Text[..4]).Order(),
                                                                            Is.EqualTo(Enumerable.Range(0, BigTXTs).Select(i => $"{i:D4}")));

                Assert.That(big2.  Answers.OfType<TXT>().Count(),           Is.EqualTo(BigTXTs));
                Assert.That(small. Answers.OfType<A>().Select(a => a.IPv4Address),
                                                                            Is.EqualTo(new[] { IPv4Address.Parse("192.0.2.10") }));

            });

        }

        #endregion

        #region ClientIdentity_IsTheSSHLogin_AndNullOverTCP(...)

        /// <summary>
        /// What a request handler sees: over SSH the login the SSH server authenticated, for each
        /// connection its own; over TCP, where a client is an address, nothing.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task ClientIdentity_IsTheSSHLogin_AndNullOverTCP(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);
            await using var cs001   = await ConnectAsync(running, CancellationToken, "cs001");
            await using var cs002   = await ConnectAsync(running, CancellationToken, "cs002");

            Assert.That(await AskOnANewChannelAsync(cs001, 0x0021, CancellationToken), Is.EqualTo("192.0.2.10"));
            Assert.That(await AskOnANewChannelAsync(cs002, 0x0022, CancellationToken), Is.EqualTo("192.0.2.10"));

            using var tcp = new TcpClient();
            await tcp.ConnectAsync(System.Net.IPAddress.Loopback, running.TCPPort, CancellationToken);
            await tcp.GetStream().WriteAsync(Framed(PaddedQuery(0x0023, "ns.example.org", 1)), CancellationToken);

            Assert.That(await ReadMessageAsync(tcp.GetStream(), CancellationToken), Is.Not.Null, "no answer over TCP");

            Assert.That(running.Requests.ToArray(),
                        Is.EqualTo(new[] {
                            new Asked(DNSServer.SSHServerType, "cs001", "csms.example.org."),
                            new Asked(DNSServer.SSHServerType, "cs002", "csms.example.org."),
                            new Asked("TCP Unicast",           null,    "ns.example.org.")
                        }));

        }

        #endregion

        #region AMessageWhoseHeaderCanBeReadButWhoseRestCannot_IsAnsweredWithFORMERR(...)

        /// <summary>
        /// Section 3.5: a message that holds a DNS header announcing a question, and no question, is no
        /// protocol error of the stream - its length said where it ends. It is answered with FORMERR
        /// under its ID, and the channel goes on: the query written behind it is answered.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task AMessageWhoseHeaderCanBeReadButWhoseRestCannot_IsAnsweredWithFORMERR(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);
            await using var ssh     = await ConnectAsync(running, CancellationToken);

            var channel = await ssh.OpenSubsystemAsync(DNSServer.SSHSubsystemName, CancellationToken);

            Byte[] oneWrite = [
                .. Framed([ 0x00, 0x71, 0x01, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 ]),
                .. Framed(PaddedQuery(0x0072, "csms.example.org", 1))
            ];

            await channel.SendDataAsync(oneWrite, CancellationToken);

            var first   = await ReadMessageAsync(channel.Input, CancellationToken);
            Assert.That(first,  Is.Not.Null, "the channel ended on a message whose question could not be read");

            var second  = await ReadMessageAsync(channel.Input, CancellationToken);
            Assert.That(second, Is.Not.Null, "the channel ended after the FORMERR");

            Assert.Multiple(() => {

                Assert.That(first,                       Is.EqualTo(new Byte[] { 0x00, 0x71, 0x81, 0x01, 0, 0, 0, 0, 0, 0, 0, 0 }), "not a FORMERR under the ID of the message");

                Assert.That(Parse(second!, 0x0072, "csms.example.org.", DNSResourceRecordTypes.A).Answers.OfType<A>().Select(a => a.IPv4Address),
                                                         Is.EqualTo(new[] { IPv4Address.Parse("192.0.2.10") }));

                Assert.That(channel.Closed.IsCompleted,  Is.False);

            });

        }

        #endregion

        #region ClientEOF_EndsTheChannelWithExitStatus0_AndTheConnectionGoesOn(...)

        /// <summary>
        /// Section 3.5: the client ends the channel with EOF once it expects no further answers. The
        /// server answers with exit-status 0, EOF and CLOSE - of the channel; the connection carries a
        /// second channel afterwards.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task ClientEOF_EndsTheChannelWithExitStatus0_AndTheConnectionGoesOn(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);
            await using var ssh     = await ConnectAsync(running, CancellationToken);

            var channel = await ssh.OpenSubsystemAsync(DNSServer.SSHSubsystemName, CancellationToken);

            await channel.SendDataAsync(Convert.FromHexString(A1), CancellationToken);
            Assert.That(await ReadMessageAsync(channel.Input, CancellationToken), Is.Not.Null, "no answer before the EOF");

            await channel.SendEofAsync(CancellationToken);

            var (data, exitStatuses) = await ToTheEndAsync(channel);

            Assert.Multiple(() => {
                Assert.That(data,                    Is.Empty,                        "the server wrote after the client's EOF");
                Assert.That(exitStatuses,            Is.EqualTo(new UInt32[] { 0 }),  "a client's EOF did not end with exit-status 0, once");
                Assert.That(ssh.Closed.IsCompleted,  Is.False,                        "the SSH connection ended with the channel");
            });

            Assert.That(await AskOnANewChannelAsync(ssh, 0x0031, CancellationToken), Is.EqualTo("192.0.2.10"), "no answer on a second channel of the connection");

        }

        #endregion

        #region AProtocolError_EndsTheChannelWithExitStatus1_AndTheConnectionGoesOn(...)

        /// <summary>
        /// Section 3.5: a length of 0, a message too short for the 12 octets of a DNS header, and a
        /// stream that ends within a message end the channel with exit-status 1 - and only the
        /// channel.
        /// </summary>
        /// <remarks>
        /// The positive control is the query before the error, on the same channel: it is answered, so
        /// the channel that then ends was the DNS server's, and it ended for what came after.
        /// </remarks>
        [TestCase("0000",                   false, TestName = "AProtocolError_EndsTheChannelWithExitStatus1_AndTheConnectionGoesOn(a length of 0)")]
        [TestCase("00050001010000",         false, TestName = "AProtocolError_EndsTheChannelWithExitStatus1_AndTheConnectionGoesOn(a message of 5 octets)")]
        [TestCase("000b0001010000010000000000", false, TestName = "AProtocolError_EndsTheChannelWithExitStatus1_AndTheConnectionGoesOn(a message of 11 octets)")]
        [TestCase("0080000101000001",       true,  TestName = "AProtocolError_EndsTheChannelWithExitStatus1_AndTheConnectionGoesOn(a stream that ends within a message)")]
        [TestCase("00",                     true,  TestName = "AProtocolError_EndsTheChannelWithExitStatus1_AndTheConnectionGoesOn(a stream that ends within a length)")]
        [CancelAfter(30000)]
        public async Task AProtocolError_EndsTheChannelWithExitStatus1_AndTheConnectionGoesOn(String Octets, Boolean ThenEOF, CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(CancellationToken);
            await using var ssh     = await ConnectAsync(running, CancellationToken);

            var channel = await ssh.OpenSubsystemAsync(DNSServer.SSHSubsystemName, CancellationToken);

            await channel.SendDataAsync(Convert.FromHexString(A1), CancellationToken);
            Assert.That(await ReadMessageAsync(channel.Input, CancellationToken), Is.Not.Null, "the positive control: no answer before the protocol error");

            await channel.SendDataAsync(Convert.FromHexString(Octets), CancellationToken);

            if (ThenEOF)
                await channel.SendEofAsync(CancellationToken);

            var (data, exitStatuses) = await ToTheEndAsync(channel);

            Assert.Multiple(() => {
                Assert.That(data,                    Is.Empty,                        "the server answered a protocol error");
                Assert.That(exitStatuses,            Is.EqualTo(new UInt32[] { 1 }),  "a protocol error did not end with exit-status 1, once");
                Assert.That(ssh.Closed.IsCompleted,  Is.False,                        "the SSH connection ended with the channel");
                Assert.That(running.Requests.Count,  Is.EqualTo(1),                   "the protocol error reached the request handlers");
            });

            Assert.That(await AskOnANewChannelAsync(ssh, 0x0041, CancellationToken), Is.EqualTo("192.0.2.10"), "no answer on a second channel of the connection");

        }

        #endregion

        #region AnIdleChannel_EndsWithExitStatus0_AndTheConnectionGoesOn(...)

        /// <summary>
        /// T19: a channel nothing was read from for <see cref="DNSServerOptions.TCPReadTimeout"/> is
        /// closed by the server with exit-status 0, EOF and CLOSE - not before that time, and the
        /// channel only: the next query opens a new one on the same connection.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task AnIdleChannel_EndsWithExitStatus0_AndTheConnectionGoesOn(CancellationToken CancellationToken)
        {

            var readTimeout = TimeSpan.FromMilliseconds(600);

            await using var running = await StartAsync(CancellationToken, readTimeout);
            await using var ssh     = await ConnectAsync(running, CancellationToken);

            var channel = await ssh.OpenSubsystemAsync(DNSServer.SSHSubsystemName, CancellationToken);

            await channel.SendDataAsync(Convert.FromHexString(A1), CancellationToken);
            Assert.That(await ReadMessageAsync(channel.Input, CancellationToken), Is.Not.Null, "the positive control: no answer on the channel that is then left idle");

            var idleSince = Stopwatch.GetTimestamp();

            var (data, exitStatuses) = await ToTheEndAsync(channel);

            var idleFor   = Stopwatch.GetElapsedTime(idleSince);

            Assert.Multiple(() => {
                Assert.That(data,                    Is.Empty);
                Assert.That(exitStatuses,            Is.EqualTo(new UInt32[] { 0 }),               "an idle timeout did not end with exit-status 0, once");
                Assert.That(idleFor,                 Is.GreaterThan(readTimeout - TimeSpan.FromMilliseconds(150)), "the channel was closed before it had been idle for the timeout");
                Assert.That(ssh.Closed.IsCompleted,  Is.False,                                     "the SSH connection ended with the channel");
            });

            Assert.That(await AskOnANewChannelAsync(ssh, 0x0051, CancellationToken), Is.EqualTo("192.0.2.10"), "no answer on a second channel of the connection");

            Assert.That(running.Audit.Events.OfType<AuthenticationSucceededEvent>().Count(), Is.EqualTo(1), "the second channel needed a second SSH authentication");

        }

        #endregion

        #region ALoginWithoutTheSubsystem_IsRefused_AndTheConnectionGoesOn(...)

        /// <summary>
        /// T11: a login the SSH server does not offer the subsystem to gets SSH_MSG_CHANNEL_FAILURE,
        /// and nothing reaches the DNS server; what else the login may have, it still gets on the same
        /// connection.
        /// </summary>
        /// <remarks>
        /// The positive control: another login asks the same server the same way and is answered - so
        /// the refusal is one of this login, not a server that serves nobody.
        /// </remarks>
        [Test]
        [CancelAfter(30000)]
        public async Task ALoginWithoutTheSubsystem_IsRefused_AndTheConnectionGoesOn(CancellationToken CancellationToken)
        {

            await using var running  = await StartAsync(CancellationToken);
            await using var ocppOnly = await ConnectAsync(running, CancellationToken, OCPPOnly);
            await using var cs001    = await ConnectAsync(running, CancellationToken, "cs001");

            var refused = await Assert.ThrowsAsync<SshSubsystemRefusedException>(async () => await ocppOnly.OpenSubsystemAsync(DNSServer.SSHSubsystemName, CancellationToken));

            Assert.That(refused!.Subsystem,  Is.EqualTo(DNSServer.SSHSubsystemName));
            Assert.That(running.Requests,    Is.Empty);

            // The connection of the refused login goes on.
            var echo = await ocppOnly.OpenSubsystemAsync(Echo, CancellationToken);
            var (said, _) = await ToTheEndAsync(echo);
            Assert.That(System.Text.Encoding.UTF8.GetString(said), Is.EqualTo("echo\n"));

            // The positive control.
            Assert.That(await AskOnANewChannelAsync(cs001, 0x0061, CancellationToken), Is.EqualTo("192.0.2.10"));
            Assert.That(running.Requests.Select(asked => asked.ClientIdentity), Is.EqualTo(new[] { "cs001" }));

        }

        #endregion

    }

}
