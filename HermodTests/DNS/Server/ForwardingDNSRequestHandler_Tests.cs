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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.Mail;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Server
{

    /// <summary>
    /// <see cref="ForwardingDNSRequestHandler"/>: what a client asks is asked further up,
    /// what the local zones answer is answered from them, and only their refusals go on.
    /// </summary>
    [TestFixture]
    public class ForwardingDNSRequestHandler_Tests
    {

        #region (private) Fixture

        private static readonly DNSServerConfig  origin   = new (IPv4Address.Localhost, IPPort.Parse(53));

        /// <summary>
        /// A resolver further up that answers as it is told and writes down what it was asked.
        /// </summary>
        private sealed class ScriptedUpstream(Func<DNSServiceName, DNSResourceRecordTypes, DNSInfo> Answer) : IDNSClient
        {

            public List<(DNSServiceName Name, DNSResourceRecordTypes[] Types, Boolean? RecursionDesired, TimeSpan? Timeout)> Asked { get; } = [];

            public Int32 Disposed;

            public Task<DNSInfo> Query(DomainName                           DomainName,
                                       IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                       TimeSpan?                            Timeout             = null,
                                       Boolean?                             RecursionDesired    = null,
                                       Boolean?                             ForceUpdate         = false,
                                       CancellationToken                    CancellationToken   = default)

                => Query(DNSServiceName.Parse(DomainName.FullName), ResourceRecordTypes, Timeout, RecursionDesired, ForceUpdate, CancellationToken);

            public Task<DNSInfo> Query(DNSServiceName                       DNSServiceName,
                                       IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                       TimeSpan?                            Timeout             = null,
                                       Boolean?                             RecursionDesired    = null,
                                       Boolean?                             ForceUpdate         = false,
                                       CancellationToken                    CancellationToken   = default)
            {
                var types = ResourceRecordTypes.ToArray();
                Asked.Add((DNSServiceName, types, RecursionDesired, Timeout));
                return Task.FromResult(Answer(DNSServiceName, types[0]));
            }

            public void Dispose()
                => Interlocked.Increment(ref Disposed);

            public ValueTask DisposeAsync()
            {
                Interlocked.Increment(ref Disposed);
                return ValueTask.CompletedTask;
            }

            public override String ToString()
                => "scripted upstream";

        }

        private static DNSInfo Found(params IDNSResourceRecord[] Answers)

            => new (origin, 1, false, false, true, true, DNSResponseCodes.NoError,
                    Answers, [], [], IsValid: true, IsTimeout: false, TimeSpan.FromSeconds(1), TimeSpan.Zero,
                    AuthenticData: true);

        private static DNSInfo NoSuchName(params IDNSResourceRecord[] Authorities)

            => new (origin, 1, false, false, true, true, DNSResponseCodes.NameError,
                    [], Authorities, [], IsValid: true, IsTimeout: false, TimeSpan.FromSeconds(1), TimeSpan.Zero);

        private static A AnA(String Name, String Address)

            => new (DomainName.Parse(Name), DNSQueryClasses.IN, TimeSpan.FromMinutes(5), IPv4Address.Parse(Address));

        private static DNSPacket QueryFor(String                  Name,
                                          DNSResourceRecordTypes  Type,
                                          Boolean                 WithOPT          = false,
                                          Boolean                 RecursionDesired = true)

            => DNSPacket.Query(DNSServiceName.Parse(Name), (UInt16) (WithOPT ? 1232 : 0), RecursionDesired, Type);

        /// <summary>
        /// The zone of "lc.example.test", which a local controller answers itself.
        /// </summary>
        private static InMemoryDNSZone LocalZone()

            => new InMemoryDNSZone().
                   Add(new SOA(DomainName.Parse("lc.example.test."),
                               DNSQueryClasses.IN,
                               TimeSpan.FromMinutes(5),
                               DomainName.Parse("ns.lc.example.test."),
                               SimpleEMailAddress.Parse("hostmaster@lc.example.test"),
                               1, TimeSpan.FromHours(1), TimeSpan.FromMinutes(10), TimeSpan.FromDays(1), TimeSpan.FromMinutes(1))).
                   Add(AnA("csms.lc.example.test.", "10.0.0.7"));

        #endregion


        #region Passes_A_Query_On_And_Hands_Back_What_It_Learns()

        [Test]
        public async Task Passes_A_Query_On_And_Hands_Back_What_It_Learns()
        {

            var upstream = new ScriptedUpstream((name, type) => Found(AnA("csms.example.org.", "192.0.2.10")));
            var handler  = new ForwardingDNSRequestHandler(upstream);

            var request  = QueryFor("csms.example.org.", DNSResourceRecordTypes.A);
            var response = await handler.ProcessDNSRequest(request);

            Assert.That(response,                                              Is.Not.Null);
            Assert.That(response!.ResponseCode,                                Is.EqualTo(DNSResponseCodes.NoError));
            Assert.That(response.TransactionId,                                Is.EqualTo(request.TransactionId));
            Assert.That(response.QueryOrResponse,                              Is.EqualTo(DNSQueryResponse.Response));
            Assert.That(response.AnswerRRs.OfType<A>().Single().IPv4Address,   Is.EqualTo(IPv4Address.Parse("192.0.2.10")));
            Assert.That(response.Questions.Single().DomainName.FullName,       Does.StartWith("csms.example.org"));

            Assert.That(upstream.Asked,                                        Has.Count.EqualTo(1));
            Assert.That(upstream.Asked[0].Name.FullName,                       Does.StartWith("csms.example.org"));
            Assert.That(upstream.Asked[0].Types,                               Is.EqualTo(new[] { DNSResourceRecordTypes.A }));
            Assert.That(upstream.Asked[0].RecursionDesired,                    Is.True);

        }

        #endregion

        #region Says_It_Recurses_And_Is_Not_Authoritative()

        [Test]
        public async Task Says_It_Recurses_And_Is_Not_Authoritative()
        {

            var handler  = new ForwardingDNSRequestHandler(new ScriptedUpstream((name, type) => Found(AnA("csms.example.org.", "192.0.2.10"))));

            var response = await handler.ProcessDNSRequest(QueryFor("csms.example.org.", DNSResourceRecordTypes.A));

            Assert.That(response!.RecursionAvailable,   Is.True);
            Assert.That(response.AuthoritativeAnswer,   Is.False);
            Assert.That(response.RecursionDesired,      Is.True);
            // RFC 4035 §3.2.3: nothing was validated here, whatever the resolver further up said.
            Assert.That(response.AuthenticData,         Is.False);

        }

        #endregion

        #region Hands_Back_A_Name_Error_With_Its_Authority()

        [Test]
        public async Task Hands_Back_A_Name_Error_With_Its_Authority()
        {

            var soa      = new SOA(DomainName.Parse("example.org."), DNSQueryClasses.IN, TimeSpan.FromMinutes(5),
                                   DomainName.Parse("ns.example.org."), SimpleEMailAddress.Parse("hostmaster@example.org"),
                                   7, TimeSpan.FromHours(1), TimeSpan.FromMinutes(10), TimeSpan.FromDays(1), TimeSpan.FromMinutes(1));

            var handler  = new ForwardingDNSRequestHandler(new ScriptedUpstream((name, type) => NoSuchName(soa)));

            var response = await handler.ProcessDNSRequest(QueryFor("missing.example.org.", DNSResourceRecordTypes.A));

            Assert.That(response!.ResponseCode,                       Is.EqualTo(DNSResponseCodes.NameError));
            Assert.That(response.AnswerRRs,                           Is.Empty);
            Assert.That(response.AuthorityRRs.OfType<SOA>().Single().Serial, Is.EqualTo(7));

        }

        #endregion

        #region An_Upstream_That_Times_Out_Is_A_Server_Failure()

        [Test]
        public async Task An_Upstream_That_Times_Out_Is_A_Server_Failure()
        {

            var handler  = new ForwardingDNSRequestHandler(new ScriptedUpstream((name, type) => DNSInfo.TimedOut(origin, 1, TimeSpan.FromSeconds(1))));

            var response = await handler.ProcessDNSRequest(QueryFor("slow.example.org.", DNSResourceRecordTypes.A));

            Assert.That(response!.ResponseCode,  Is.EqualTo(DNSResponseCodes.ServerFailure));
            Assert.That(response.AnswerRRs,      Is.Empty);

        }

        #endregion

        #region What_The_Upstream_Did_Not_Get_In_Time_Or_Whole_Is_Not_Handed_Back()

        /// <summary>
        /// An answer the client further up marks as timed out or not valid is no answer,
        /// whatever its response code and records say.
        /// </summary>
        [Test]
        public async Task What_The_Upstream_Did_Not_Get_In_Time_Or_Whole_Is_Not_Handed_Back()
        {

            DNSInfo Marked(Boolean IsValid, Boolean IsTimeout)

                => new (origin, 1, false, false, true, true, DNSResponseCodes.NoError,
                        [ AnA("csms.example.org.", "192.0.2.10") ], [], [],
                        IsValid, IsTimeout, TimeSpan.FromSeconds(1), TimeSpan.Zero);

            var late    = await new ForwardingDNSRequestHandler(new ScriptedUpstream((name, type) => Marked(IsValid: true,  IsTimeout: true))).
                                    ProcessDNSRequest(QueryFor("csms.example.org.", DNSResourceRecordTypes.A));

            var invalid = await new ForwardingDNSRequestHandler(new ScriptedUpstream((name, type) => Marked(IsValid: false, IsTimeout: false))).
                                    ProcessDNSRequest(QueryFor("csms.example.org.", DNSResourceRecordTypes.A));

            Assert.That(late!   .ResponseCode,  Is.EqualTo(DNSResponseCodes.ServerFailure));
            Assert.That(late    .AnswerRRs,     Is.Empty);
            Assert.That(invalid!.ResponseCode,  Is.EqualTo(DNSResponseCodes.ServerFailure));
            Assert.That(invalid .AnswerRRs,     Is.Empty);

        }

        #endregion

        #region An_Upstream_That_Throws_Is_A_Server_Failure()

        [Test]
        public async Task An_Upstream_That_Throws_Is_A_Server_Failure()
        {

            var handler  = new ForwardingDNSRequestHandler(new ScriptedUpstream((name, type) => throw new InvalidOperationException("no name server")));

            var response = await handler.ProcessDNSRequest(QueryFor("broken.example.org.", DNSResourceRecordTypes.A));

            Assert.That(response!.ResponseCode,  Is.EqualTo(DNSResponseCodes.ServerFailure));

        }

        #endregion

        #region Asks_The_Upstream_Of_The_Time()

        [Test]
        public async Task Asks_The_Upstream_Of_The_Time()
        {

            var first    = new ScriptedUpstream((name, type) => Found(AnA("csms.example.org.", "192.0.2.1")));
            var second   = new ScriptedUpstream((name, type) => Found(AnA("csms.example.org.", "192.0.2.2")));
            IDNSClient now = first;

            var handler  = new ForwardingDNSRequestHandler(() => now, Timeout: TimeSpan.FromSeconds(3));

            var before   = await handler.ProcessDNSRequest(QueryFor("csms.example.org.", DNSResourceRecordTypes.A));
            now          = second;
            var after    = await handler.ProcessDNSRequest(QueryFor("csms.example.org.", DNSResourceRecordTypes.A));

            Assert.That(before!.AnswerRRs.OfType<A>().Single().IPv4Address,  Is.EqualTo(IPv4Address.Parse("192.0.2.1")));
            Assert.That(after! .AnswerRRs.OfType<A>().Single().IPv4Address,  Is.EqualTo(IPv4Address.Parse("192.0.2.2")));
            Assert.That(second.Asked[0].Timeout,                              Is.EqualTo(TimeSpan.FromSeconds(3)));
            Assert.That(first.Disposed + second.Disposed,                     Is.Zero, "the handler borrows its upstream, it does not own it");

        }

        #endregion

        #region Answers_EDNS_With_Its_Own_OPT_Record()

        [Test]
        public async Task Answers_EDNS_With_Its_Own_OPT_Record()
        {

            var upstreamOPT = new OPT(UDPPayloadSize: 4096, ExtendedRCODE: 0, Version: 0, Flags: 0x8000);

            var handler  = new ForwardingDNSRequestHandler(
                               new ScriptedUpstream((name, type) =>
                                   new DNSInfo(origin, 1, false, false, true, true, DNSResponseCodes.NoError,
                                               [ AnA("csms.example.org.", "192.0.2.10") ], [], [ upstreamOPT ],
                                               IsValid: true, IsTimeout: false, TimeSpan.FromSeconds(1), TimeSpan.Zero))
                           );

            var withOPT    = await handler.ProcessDNSRequest(QueryFor("csms.example.org.", DNSResourceRecordTypes.A, WithOPT: true));
            var withoutOPT = await handler.ProcessDNSRequest(QueryFor("csms.example.org.", DNSResourceRecordTypes.A, WithOPT: false));

            var opt = withOPT!.AdditionalRRs.OfType<OPT>().Single();
            Assert.That(opt.UDPPayloadSize,                      Is.EqualTo(ForwardingDNSRequestHandler.DefaultUDPPayloadSize));
            Assert.That(opt.DnssecOK,                            Is.False, "the OPT record of the hop further up is not passed on");
            Assert.That(withoutOPT!.AdditionalRRs.OfType<OPT>(), Is.Empty);

        }

        #endregion

        #region Refuses_What_It_Does_Not_Implement()

        [Test]
        public async Task Refuses_What_It_Does_Not_Implement()
        {

            var upstream = new ScriptedUpstream((name, type) => Found(AnA("csms.example.org.", "192.0.2.10")));
            var handler  = new ForwardingDNSRequestHandler(upstream);

            var notify   = new DNSPacket(4711, DNSQueryResponse.Query, 4, false, false, false, false, DNSResponseCodes.NoError,
                                         [ new DNSQuestion(DNSServiceName.Parse("example.org."), DNSResourceRecordTypes.SOA, DNSQueryClasses.IN) ],
                                         [], [], []);

            var twoQuestions = new DNSPacket(4712, DNSQueryResponse.Query, 0, false, false, true, false, DNSResponseCodes.NoError,
                                             [ new DNSQuestion(DNSServiceName.Parse("a.example.org."), DNSResourceRecordTypes.A, DNSQueryClasses.IN),
                                               new DNSQuestion(DNSServiceName.Parse("b.example.org."), DNSResourceRecordTypes.A, DNSQueryClasses.IN) ],
                                             [], [], []);

            var chaos    = new DNSPacket(4713, DNSQueryResponse.Query, 0, false, false, true, false, DNSResponseCodes.NoError,
                                         [ new DNSQuestion(DNSServiceName.Parse("version.bind."), DNSResourceRecordTypes.TXT, (DNSQueryClasses) 3) ],
                                         [], [], []);

            var badVersion = new DNSPacket(4714, DNSQueryResponse.Query, 0, false, false, true, false, DNSResponseCodes.NoError,
                                           [ new DNSQuestion(DNSServiceName.Parse("csms.example.org."), DNSResourceRecordTypes.A, DNSQueryClasses.IN) ],
                                           [], [], [ new OPT(UDPPayloadSize: 1232, ExtendedRCODE: 0, Version: 1, Flags: 0) ]);

            Assert.That((await handler.ProcessDNSRequest(notify))!      .ResponseCode,  Is.EqualTo(DNSResponseCodes.NotImplemented));
            Assert.That((await handler.ProcessDNSRequest(twoQuestions))!.ResponseCode,  Is.EqualTo(DNSResponseCodes.FormatError));
            Assert.That((await handler.ProcessDNSRequest(chaos))!       .ResponseCode,  Is.EqualTo(DNSResponseCodes.Refused));
            Assert.That((await handler.ProcessDNSRequest(badVersion))!  .ResponseCode,  Is.EqualTo(DNSResponseCodes.BadVersion));
            Assert.That(upstream.Asked,                                                Is.Empty);

        }

        #endregion

        #region Does_Not_Answer_A_Response()

        [Test]
        public async Task Does_Not_Answer_A_Response()
        {

            var upstream = new ScriptedUpstream((name, type) => Found(AnA("csms.example.org.", "192.0.2.10")));

            var response = new DNSPacket(4715, DNSQueryResponse.Response, 0, false, false, true, true, DNSResponseCodes.NoError,
                                         [ new DNSQuestion(DNSServiceName.Parse("csms.example.org."), DNSResourceRecordTypes.A, DNSQueryClasses.IN) ],
                                         [], [], []);

            Assert.That(await new ForwardingDNSRequestHandler(upstream).ProcessDNSRequest(response), Is.Null);
            Assert.That(upstream.Asked, Is.Empty);

        }

        #endregion

        #region Answers_Its_Own_Zone_Itself_And_Passes_The_Rest_On()

        [Test]
        public async Task Answers_Its_Own_Zone_Itself_And_Passes_The_Rest_On()
        {

            var upstream = new ScriptedUpstream((name, type) => Found(AnA(name.FullName, "192.0.2.99")));
            var handler  = new ForwardingDNSRequestHandler(upstream, Local: new AuthoritativeDNSRequestHandler(LocalZone()));

            var own      = await handler.ProcessDNSRequest(QueryFor("csms.lc.example.test.",    DNSResourceRecordTypes.A));
            var missing  = await handler.ProcessDNSRequest(QueryFor("missing.lc.example.test.", DNSResourceRecordTypes.A));
            var foreign  = await handler.ProcessDNSRequest(QueryFor("csms.example.org.",        DNSResourceRecordTypes.A));

            Assert.That(own!.AnswerRRs.OfType<A>().Single().IPv4Address,     Is.EqualTo(IPv4Address.Parse("10.0.0.7")));
            Assert.That(own.AuthoritativeAnswer,                             Is.True);

            // A name of its own zone that is not there is not there: no question further up.
            Assert.That(missing!.ResponseCode,                               Is.EqualTo(DNSResponseCodes.NameError));

            Assert.That(foreign!.AnswerRRs.OfType<A>().Single().IPv4Address, Is.EqualTo(IPv4Address.Parse("192.0.2.99")));
            Assert.That(foreign.AuthoritativeAnswer,                         Is.False);

            Assert.That(upstream.Asked.Select(asked => asked.Name.FullName), Has.Exactly(1).StartsWith("csms.example.org"));
            Assert.That(upstream.Asked,                                      Has.Count.EqualTo(1));

        }

        #endregion

        #region A_Server_Forwards_Over_TCP_To_A_Server_Further_Up()

        /// <summary>
        /// The whole way: a client asks a DNS server over TCP, whose handler asks a second
        /// DNS server over UDP with Hermod's DNS client - the server further up knows the name.
        /// </summary>
        [Test]
        public async Task A_Server_Forwards_Over_TCP_To_A_Server_Further_Up()
        {

            var further = new DNSServer(
                              new AuthoritativeDNSRequestHandler(new InMemoryDNSZone().Add(AnA("csms.example.test.", "127.0.0.42"))),
                              new DNSServerOptions {
                                  UDPUnicastSocket    = new IPSocket(IPv4Address.Localhost, IPPort.Zero),
                                  EnableUDPMulticast  = false,
                                  EnableTCPUnicast    = false
                              }
                          );

            DNSServer? forwarder = null;

            try
            {

                await further.Start();

                using var upstream = new DNSClient(
                                         IPv4Address.Localhost,
                                         Port:           further.ActiveUDPUnicastSocket!.Value.Port,
                                         QueryTimeout:   TimeSpan.FromSeconds(2),
                                         UseQueryCache:  false
                                     );

                forwarder = new DNSServer(
                                new ForwardingDNSRequestHandler(upstream),
                                new DNSServerOptions {
                                    EnableUDPUnicast    = false,
                                    EnableUDPMulticast  = false,
                                    EnableTCPUnicast    = true,
                                    TCPUnicastSocket    = new IPSocket(IPv4Address.Localhost, IPPort.Zero)
                                }
                            );

                await forwarder.Start();

                using var client = new DNSTCPClient(
                                       IPv4Address.Localhost,
                                       Port:          forwarder.ActiveTCPUnicastSocket!.Value.Port,
                                       QueryTimeout:  TimeSpan.FromSeconds(3)
                                   );

                var response = await client.Query<A>(DomainName.Parse("csms.example.test."), Timeout: TimeSpan.FromSeconds(3), ForceUpdate: true);

                Assert.That(response.ResponseCode,                          Is.EqualTo(DNSResponseCodes.NoError));
                Assert.That(response.RecursionAvailable,                    Is.True);
                Assert.That(response.AuthoritativeAnswer,                   Is.False);
                Assert.That(response.FilteredAnswers.Single().IPv4Address,  Is.EqualTo(IPv4Address.Parse("127.0.0.42")));

            }
            finally
            {
                if (forwarder is not null)
                    await forwarder.Stop();
                await further.Stop();
            }

        }

        #endregion

    }

}
