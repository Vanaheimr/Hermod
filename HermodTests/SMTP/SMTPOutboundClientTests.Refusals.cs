/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// A next hop that refuses the session - greets with no 2yz - says nothing of the message:
    /// another address of the host, another MX host may take it. RFC 5321 §5.1: "the SMTP client
    /// MUST be able to try (and retry) each of the relevant addresses in this list in order, until
    /// a delivery attempt succeeds"; "the SMTP sender MUST try them in the order presented".
    /// </summary>
    public partial class SMTPOutboundClientTests
    {

        #region Setup

        // The second host: 127.0.0.2, on the port of the first. Linux and Windows answer on all of 127/8.
        private static readonly System.Net.IPAddress SecondLoopback = System.Net.IPAddress.Parse("127.0.0.2");

        /// <summary>
        /// A next hop that greets with the given line, answers QUIT with 221 and anything else with
        /// 503 (RFC 5321 §3.1) - or, greeting with 220, takes a message like any other.
        /// </summary>
        private sealed class Greeter : IDisposable
        {

            private readonly TcpListener  listener;
            private readonly String       greeting;
            private          Int32        sessions;

            public readonly ConcurrentQueue<String> Commands = new();

            public Int32 Sessions
                => Volatile.Read(ref sessions);

            public Greeter(System.Net.IPAddress Address, IPPort Port, String Greeting)
            {
                greeting  = Greeting;
                listener  = new TcpListener(Address, Port.ToInt32());
                listener.Start();
                _ = Task.Run(AcceptAsync);
            }

            private async Task AcceptAsync()
            {
                try
                {
                    while (true)
                    {
                        var client = await listener.AcceptTcpClientAsync();
                        Interlocked.Increment(ref sessions);
                        _ = Task.Run(() => ServeAsync(client));
                    }
                }
                catch
                {
                    // the listener was stopped
                }
            }

            private async Task ServeAsync(TcpClient Client)
            {
                try
                {

                    using var client = Client;
                    var stream = client.GetStream();
                    var reader = new StreamReader(stream, Encoding.UTF8);

                    Task Write(String Text)
                        => stream.WriteAsync(Encoding.UTF8.GetBytes(Text + "\r\n")).AsTask();

                    await Write(greeting);

                    var accepting = greeting.StartsWith('2');
                    var inData    = false;

                    while (await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)) is { } line)
                    {

                        if (inData)
                        {
                            if (line == ".")
                            {
                                inData = false;
                                await Write("250 2.0.0 queued");
                            }
                            continue;
                        }

                        Commands.Enqueue(line);

                        var verb = line.Split(' ')[0].ToUpperInvariant();

                        if (verb == "QUIT")
                        {
                            await Write("221 2.0.0 bye");
                            return;
                        }

                        if (!accepting)
                        {
                            await Write("503 5.5.1 Bad sequence of commands");
                            continue;
                        }

                        inData = verb == "DATA";

                        await Write(verb switch {
                                        "EHLO"  => "250-second.hop\r\n250 8BITMIME",
                                        "DATA"  => "354 go ahead",
                                        _       => "250 2.0.0 ok"
                                    });

                    }

                }
                catch
                {
                    // the client hung up
                }
            }

            public void Dispose()
                => listener.Stop();

        }

        /// <summary>
        /// The MX records of a domain, and the A records of host names - in the order given.
        /// </summary>
        private sealed class HostsDns(IReadOnlyList<(String Host, UInt16 Preference)>     Mx,
                                      IReadOnlyDictionary<String, System.Net.IPAddress[]>  Hosts) : IDNSClient
        {

            private static readonly DNSServerConfig origin = new (IPv4Address.Localhost, IPPort.Parse(53));

            private Task<DNSInfo> Answer(String Name, IEnumerable<DNSResourceRecordTypes> Types)
            {

                var name    = Name.TrimEnd('.');
                var answers = new List<IDNSResourceRecord>();

                if (Types.Contains(DNSResourceRecordTypes.MX))
                    answers.AddRange(Mx.Select(mx => new MX(DomainName.ParseLenient(name), DNSQueryClasses.IN, TimeSpan.FromHours(1), mx.Preference, DomainName.ParseLenient(mx.Host))));

                if (Types.Contains(DNSResourceRecordTypes.A) && Hosts.TryGetValue(name, out var addresses))
                    answers.AddRange(addresses.Select(address => new A(DomainName.ParseLenient(name), DNSQueryClasses.IN, TimeSpan.FromHours(1), IPv4Address.Parse(address.ToString()))));

                return Task.FromResult(new DNSInfo(origin, 0, true, false, true, false, DNSResponseCodes.NoError,
                                                   answers, [], [], true, false, TimeSpan.FromSeconds(1), TimeSpan.Zero));

            }

            public Task<DNSInfo> Query(DomainName DomainName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null,
                                       Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
                => Answer(DomainName.FullName, ResourceRecordTypes);

            public Task<DNSInfo> Query(DNSServiceName DNSServiceName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null,
                                       Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
                => Answer(DNSServiceName.FullName, ResourceRecordTypes);

            public override String ToString()
                => nameof(HostsDns);

            public void Dispose() { }

            public ValueTask DisposeAsync()
                => ValueTask.CompletedTask;

        }

        private static Task<SendResult> SendThrough(IDNSClient DNS, IPPort Port, String? SmartHost = null)
        {

            var client = new SMTPOutboundClient(new SmtpOutboundConfig {
                                                    LocalHostname     = "relay.hermod.test",
                                                    SmartHost         = SmartHost,
                                                    SmartHostPort     = Port,
                                                    MxPort            = Port,
                                                    ConnectTimeoutMs  = 3_000,
                                                    ReadTimeoutMs     = 5_000,
                                                    WriteTimeoutMs    = 3_000
                                                },
                                                null,
                                                DNS,
                                                new QuietLogger());

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

            return client.SendAsync("next.example", "sender@client.example", [ "you@next.example" ], "Subject: refused\r\n\r\nhello\r\n", ct: cts.Token);

        }

        private static IDNSClient TwoMxHosts()
            => new HostsDns([ ("127.0.0.2", 10), ("127.0.0.1", 20) ], new Dictionary<String, System.Net.IPAddress[]>());

        #endregion


        #region The next MX host

        [TestCase("554 5.7.1 no SMTP service for you",   Description = "RFC 5321 §3.1: a session refused")]
        [TestCase("521 5.3.2 does not accept mail",      Description = "RFC 7504 §3: a host that takes no mail")]
        [TestCase("421 4.3.2 busy, try again later",     Description = "a refusal for now")]
        public async Task A_session_refused_by_the_first_MX_host_goes_to_the_next(String Greeting)
        {

            using var second = new NextHop();
            using var first  = new Greeter(SecondLoopback, second.Port, Greeting);

            var result = await SendThrough(TwoMxHosts(), second.Port);

            Assert.That(first.Sessions,    Is.EqualTo(1),                  "the first MX host is tried first");
            Assert.That(first.Commands,    Has.Some.EqualTo("QUIT"),       "and its session ended with QUIT (RFC 5321 §3.1)");
            Assert.That(result.Status,     Is.EqualTo(SendStatus.Success), $"{result.ResponseCode} {result.ResponseText}");
            Assert.That(second.Commands,   Has.Some.StartsWith("RCPT TO:<you@next.example>"));

        }

        [Test]
        public async Task A_message_refused_by_the_first_MX_host_is_not_offered_to_the_next()
        {

            // 127.0.0.1 is the first MX host here, and refuses the sender: that is the domain's answer.
            using var first  = new NextHop(line => line.StartsWith("MAIL") ? "550 5.7.1 sender refused" : null);
            using var second = new Greeter(SecondLoopback, first.Port, "220 second.hop ESMTP");

            var result = await SendThrough(new HostsDns([ ("127.0.0.1", 10), ("127.0.0.2", 20) ], new Dictionary<String, System.Net.IPAddress[]>()),
                                           first.Port);

            Assert.That(result.Status,    Is.EqualTo(SendStatus.PermFail), $"{result.ResponseCode} {result.ResponseText}");
            Assert.That(result.ResponseCode, Is.EqualTo(550));
            Assert.That(second.Sessions,  Is.Zero, "the second MX host is not asked");

        }

        [Test]
        public async Task Every_MX_host_refusing_the_session_is_a_permanent_failure()
        {

            using var second = new NextHop(Greeting: "554 5.7.1 not you either");
            using var first  = new Greeter(SecondLoopback, second.Port, "554 5.7.1 no SMTP service for you");

            var result = await SendThrough(TwoMxHosts(), second.Port);

            Assert.That(first.Sessions, Is.EqualTo(1));
            Assert.That(result.Status,  Is.EqualTo(SendStatus.PermFail), $"{result.ResponseCode} {result.ResponseText}");
            Assert.That(result.ResponseCode, Is.EqualTo(554));

        }

        [Test]
        public async Task A_refusal_for_now_at_one_MX_host_is_not_outweighed_by_a_refusal_at_another()
        {

            using var second = new NextHop(Greeting: "554 5.7.1 no SMTP service for you");
            using var first  = new Greeter(SecondLoopback, second.Port, "421 4.3.2 busy, try again later");

            var result = await SendThrough(TwoMxHosts(), second.Port);

            Assert.That(result.Status,       Is.EqualTo(SendStatus.TempFail), $"{result.ResponseCode} {result.ResponseText}");
            Assert.That(result.ResponseCode, Is.EqualTo(421));

        }

        #endregion


        #region The next address of the host

        [TestCase("421 4.3.2 busy, try again later")]
        [TestCase("554 5.7.1 no SMTP service for you")]
        public async Task A_session_refused_at_the_first_address_goes_to_the_next_address(String Greeting)
        {

            using var second = new NextHop();
            using var first  = new Greeter(SecondLoopback, second.Port, Greeting);

            // One smart host, two addresses: 127.0.0.2 first.
            var dns    = new HostsDns([], new Dictionary<String, System.Net.IPAddress[]> {
                                              ["smart.next.example"] = [ SecondLoopback, System.Net.IPAddress.Loopback ]
                                          });

            var result = await SendThrough(dns, second.Port, SmartHost: "smart.next.example");

            Assert.That(first.Sessions,  Is.EqualTo(1),                  "the first address is tried first");
            Assert.That(result.Status,   Is.EqualTo(SendStatus.Success), $"{result.ResponseCode} {result.ResponseText}");
            Assert.That(second.Commands, Has.Some.StartsWith("RCPT TO:<you@next.example>"));

        }

        [Test]
        public async Task An_address_that_is_not_listening_is_followed_by_the_next()
        {

            using var second = new NextHop();

            // Nothing listens on 127.0.0.2 at that port: refused at once.
            var dns    = new HostsDns([], new Dictionary<String, System.Net.IPAddress[]> {
                                              ["smart.next.example"] = [ SecondLoopback, System.Net.IPAddress.Loopback ]
                                          });

            var result = await SendThrough(dns, second.Port, SmartHost: "smart.next.example");

            Assert.That(result.Status, Is.EqualTo(SendStatus.Success), $"{result.ResponseCode} {result.ResponseText}");

        }

        [Test]
        public async Task A_session_refused_at_one_address_and_another_address_not_reached_is_tried_again_later()
        {

            using var first = new NextHop(Greeting: "554 5.7.1 no SMTP service for you");

            // 127.0.0.2 does not answer at that port: the next attempt may find it.
            var dns    = new HostsDns([], new Dictionary<String, System.Net.IPAddress[]> {
                                              ["smart.next.example"] = [ System.Net.IPAddress.Loopback, SecondLoopback ]
                                          });

            var result = await SendThrough(dns, first.Port, SmartHost: "smart.next.example");

            Assert.That(result.Status, Is.EqualTo(SendStatus.TempFail), $"{result.ResponseCode} {result.ResponseText}");

        }

        #endregion

    }

}
