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
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// PublicAddressesOnly: an MX that points into this server's own network - the loopback,
    /// 10.x, an address literal of one - is not connected to; a smart host still is.
    /// </summary>
    public partial class SMTPOutboundClientTests
    {

        #region Setup

        /// <summary>
        /// A DNS client with the MX records a test gives it, by preference; no TXT, so no MTA-STS.
        /// </summary>
        private sealed class MxDns(params (String Host, UInt16 Preference)[] Mx) : IDNSClient
        {

            private static readonly DNSServerConfig origin = new (IPv4Address.Localhost, IPPort.Parse(53));

            private Task<DNSInfo> Answer(String Name, IEnumerable<DNSResourceRecordTypes> Types)
            {

                IDNSResourceRecord[] answers = Types.Contains(DNSResourceRecordTypes.MX)
                                                   ? [ .. Mx.Select(mx => new MX(DomainName.ParseLenient(Name), DNSQueryClasses.IN, TimeSpan.FromHours(1), mx.Preference, DomainName.ParseLenient(mx.Host))) ]
                                                   : [];

                return Task.FromResult(new DNSInfo(origin, 0, true, false, true, false, DNSResponseCodes.NoError,
                                                   answers, [], [], true, false, TimeSpan.FromSeconds(1), TimeSpan.Zero));

            }

            public Task<DNSInfo> Query(DomainName DomainName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null,
                                       Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
                => Answer(DomainName.FullName, ResourceRecordTypes);

            public Task<DNSInfo> Query(DNSServiceName DNSServiceName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null,
                                       Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
                => Answer(DNSServiceName.FullName, ResourceRecordTypes);

            public void Dispose() { }

            public ValueTask DisposeAsync()
                => ValueTask.CompletedTask;

            public override String ToString()
                => "MX";

        }

        private sealed class LinesLogger : ILogger
        {
            public readonly ConcurrentQueue<String> Lines = new();
            public void Log(LogLevel level, String message) => Lines.Enqueue(message);
        }

        private static SMTPOutboundClient PublicOnlyClient(IDNSClient                          DNS,
                                                           ILogger                             Logger,
                                                           Func<System.Net.IPAddress, Boolean>? CountsAsPublic  = null,
                                                           UInt16                              MxPort          = 25,
                                                           Boolean                             PublicOnly      = true)

            => new (new SmtpOutboundConfig {
                        LocalHostname        = "relay.hermod.test",
                        ConnectTimeoutMs     = 3_000,
                        ReadTimeoutMs        = 5_000,
                        PublicAddressesOnly  = PublicOnly,
                        IsPublicAddress      = CountsAsPublic ?? PublicAddresses.IsPublic,
                        MxPort               = MxPort
                    },
                    null,
                    DNS,
                    Logger);

        #endregion


        #region Which addresses are public

        [TestCase("8.8.8.8",               true)]
        [TestCase("2a01:4f8::1",           true)]
        [TestCase("127.0.0.1",             false)]
        [TestCase("10.1.2.3",              false)]
        [TestCase("172.20.0.1",            false)]
        [TestCase("192.168.1.1",           false)]
        [TestCase("169.254.169.254",       false, Description = "cloud metadata")]
        [TestCase("100.64.0.1",            false, Description = "shared address space")]
        [TestCase("0.0.0.0",               false)]
        [TestCase("255.255.255.255",       false)]
        [TestCase("224.0.0.1",             false)]
        [TestCase("::1",                   false)]
        [TestCase("::",                    false)]
        [TestCase("fd00::1",               false)]
        [TestCase("fe80::1",               false)]
        [TestCase("ff02::1",               false)]
        [TestCase("::ffff:127.0.0.1",      false, Description = "v4 mapped into v6")]
        [TestCase("::ffff:8.8.8.8",        true,  Description = "a public v4, mapped")]
        [TestCase("::10.0.0.1",            false, Description = "v4-compatible")]
        [TestCase("64:ff9b::a9fe:a9fe",    false, Description = "NAT64 of 169.254.169.254")]
        [TestCase("64:ff9b::808:808",      true,  Description = "NAT64 of 8.8.8.8")]
        [TestCase("64:ff9b:1::1",          false, Description = "local-use NAT64")]
        [TestCase("2002:c0a8:0101::1",     false, Description = "6to4 of 192.168.1.1")]
        [TestCase("2002:0808:0808::1",     true,  Description = "6to4 of 8.8.8.8")]
        [TestCase("2001:0:4136:e378:8000:63bf:80ff:fffe", false, Description = "Teredo of 127.0.0.1")]
        [TestCase("2001:db8::1",           false, Description = "documentation")]
        [TestCase("100::1",                false, Description = "discard-only")]
        [TestCase("192.0.0.8",             false)]
        [TestCase("198.18.0.1",            false, Description = "benchmarking")]
        public void Which_addresses_are_the_internets(String Address, Boolean Public)
        {
            Assert.That(PublicAddresses.IsPublic(System.Net.IPAddress.Parse(Address)), Is.EqualTo(Public));
        }

        [TestCase("192.0.2.1",             "192.0.2.1")]
        [TestCase("[192.0.2.1]",           "192.0.2.1")]
        [TestCase("[IPv6:2001:db8::1]",    "2001:db8::1")]
        [TestCase("[ipv6:::1]",            "::1")]
        [TestCase("::1",                   "::1")]
        public void An_address_literal_is_its_address(String Host, String Address)
        {
            Assert.That(PublicAddresses.TryParseLiteral(Host, out var address), Is.True);
            Assert.That(address, Is.EqualTo(System.Net.IPAddress.Parse(Address)));
        }

        [TestCase("mail.example.org")]
        [TestCase("[mail.example.org]")]
        public void A_name_is_no_address_literal(String Host)
        {
            Assert.That(PublicAddresses.TryParseLiteral(Host, out _), Is.False);
        }

        #endregion

        #region MX hosts

        [TestCase("127.0.0.1")]
        [TestCase("localhost")]
        [TestCase("10.0.0.25")]
        [TestCase("169.254.169.254")]
        public async Task An_MX_into_the_own_network_is_not_connected_to_and_bounces(String MxHost)
        {

            var logger = new LinesLogger();
            var client = PublicOnlyClient(new MxDns((MxHost, 10)), logger);

            var result = await client.SendAsync("next.example", "sender@client.example", [ "you@next.example" ], "Subject: inside\r\n\r\nhello\r\n");

            Assert.That(result.Status,       Is.EqualTo(SendStatus.PermFail), result.ResponseText);
            Assert.That(result.ResponseCode, Is.EqualTo(550));
            Assert.That(result.ResponseText, Does.StartWith("5.4.4 No MX host of next.example has a public address").And.Contain(MxHost));
            Assert.That(logger.Lines,        Has.None.StartsWith("Connecting to"), "no connection was tried");

        }

        [Test]
        public async Task Without_the_option_an_MX_into_the_own_network_is_connected_to_as_before()
        {

            var logger = new LinesLogger();
            var client = PublicOnlyClient(new MxDns(("127.0.0.1", 10)), logger, MxPort: ClosedPort(), PublicOnly: false);

            var result = await client.SendAsync("next.example", "sender@client.example", [ "you@next.example" ], "Subject: inside\r\n\r\nhello\r\n");

            Assert.That(result.Status, Is.EqualTo(SendStatus.TempFail), result.ResponseText);
            Assert.That(logger.Lines,  Has.Some.StartsWith("Connecting to 127.0.0.1"));

        }

        [TestCase("[10.0.0.1]")]
        [TestCase("[127.0.0.1]")]
        [TestCase("[IPv6:fd00::1]")]
        public async Task An_address_literal_into_the_own_network_bounces(String Domain)
        {

            var logger = new LinesLogger();
            var client = PublicOnlyClient(new MxDns(), logger);

            var result = await client.SendAsync(Domain, "sender@client.example", [ $"you@{Domain}" ], "Subject: literal\r\n\r\nhello\r\n");

            Assert.That(result.Status,       Is.EqualTo(SendStatus.PermFail), result.ResponseText);
            Assert.That(result.ResponseText, Does.StartWith("5.4.4 "));
            Assert.That(logger.Lines,        Has.None.StartsWith("Connecting to"));

        }

        [Test]
        public async Task An_MX_into_the_own_network_next_to_one_that_cannot_be_reached_now_is_tried_again_later()
        {

            var logger = new LinesLogger();
            var client = PublicOnlyClient(new MxDns(("127.0.0.1", 10), ("unreachable.invalid", 20)), logger);

            var result = await client.SendAsync("next.example", "sender@client.example", [ "you@next.example" ], "Subject: mixed\r\n\r\nhello\r\n");

            Assert.That(result.Status, Is.EqualTo(SendStatus.TempFail), "the other MX may answer tomorrow: " + result.ResponseText);

        }

        [Test]
        public async Task A_public_MX_is_connected_to_at_its_public_addresses_only()
        {

            using var nextHop = new NextHop();

            // On this machine the loopback stands in for the internet - its IPv4 address only:
            // "localhost" has ::1 too, which must not be connected to.
            var logger  = new LinesLogger();
            var client  = PublicOnlyClient(new MxDns(("localhost", 10)),
                                           logger,
                                           CountsAsPublic: address => address.Equals(System.Net.IPAddress.Loopback),
                                           MxPort:         nextHop.Port);

            var result  = await client.SendAsync("next.example", "sender@client.example", [ "you@next.example" ], "Subject: out\r\n\r\nhello\r\n");

            Assert.That(result.Status,   Is.EqualTo(SendStatus.Success), result.ResponseText);
            Assert.That(nextHop.Commands, Has.Some.StartsWith("RCPT TO:<you@next.example>"));

        }

        [Test]
        public async Task A_smart_host_may_be_on_this_machine()
        {

            using var nextHop = new NextHop();

            var client = new SMTPOutboundClient(new SmtpOutboundConfig {
                                                    LocalHostname        = "relay.hermod.test",
                                                    SmartHost            = "127.0.0.1",
                                                    SmartHostPort        = nextHop.Port,
                                                    ConnectTimeoutMs     = 3_000,
                                                    ReadTimeoutMs        = 5_000,
                                                    PublicAddressesOnly  = true
                                                },
                                                null,
                                                new MxDns(("127.0.0.1", 10)),
                                                new QuietLogger());

            var result = await client.SendAsync("next.example", "sender@client.example", [ "you@next.example" ], "Subject: relay\r\n\r\nhello\r\n");

            Assert.That(result.Status, Is.EqualTo(SendStatus.Success), "configured, not named by a stranger: " + result.ResponseText);

        }

        [Test]
        public async Task A_domains_own_smart_host_may_be_on_this_machine_too()
        {

            using var nextHop = new NextHop();

            var client = new SMTPOutboundClient(new SmtpOutboundConfig {
                                                    LocalHostname        = "relay.hermod.test",
                                                    ConnectTimeoutMs     = 3_000,
                                                    ReadTimeoutMs        = 5_000,
                                                    PublicAddressesOnly  = true
                                                },
                                                null,
                                                new MxDns(("127.0.0.1", 10)),
                                                new QuietLogger(),
                                                smartHostFor: domain => domain == "client.example"
                                                                            ? new SmartHostRoute("127.0.0.1", nextHop.Port)
                                                                            : null);

            var result = await client.SendAsync("next.example", "sender@client.example", [ "you@next.example" ], "From: sender@client.example\r\nSubject: relay\r\n\r\nhello\r\n");

            Assert.That(result.Status, Is.EqualTo(SendStatus.Success), result.ResponseText);

        }

        #endregion

        #region MTA-STS policies

        [Test]
        public async Task An_MTA_STS_policy_is_not_fetched_from_the_own_network()
        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            var port     = ((System.Net.IPEndPoint) listener.LocalEndpoint).Port;
            var accepted = listener.AcceptTcpClientAsync();

            using var http = new HttpClient(MtaStsResolver.CreateDefaultHandler(PublicAddresses.IsPublic));

            var error = await Assert.ThrowsAsync<HttpRequestException>(() => http.GetAsync($"http://127.0.0.1:{port}/.well-known/mta-sts.txt"));
            Assert.That(error!.Message, Does.Contain("no public address"));

            await Task.Delay(200);
            Assert.That(accepted.IsCompleted, Is.False, "nothing came to the listener");

        }

        [Test]
        public void The_handler_for_public_addresses_uses_no_proxy()
        {
            using var handler = (SocketsHttpHandler) MtaStsResolver.CreateDefaultHandler(PublicAddresses.IsPublic);
            Assert.That(handler.UseProxy,           Is.False, "a proxy would be connected to instead - one in this network");
            Assert.That(handler.AllowAutoRedirect,  Is.False);
            Assert.That(handler.ConnectCallback,    Is.Not.Null);
        }

        [Test]
        public async Task The_handler_connects_where_the_addresses_are_allowed()
        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            var port = ((System.Net.IPEndPoint) listener.LocalEndpoint).Port;

            _ = Task.Run(async () => {
                using var connection = await listener.AcceptTcpClientAsync();
                using var stream     = connection.GetStream();
                var buffer = new Byte[4096];
                await stream.ReadAsync(buffer);
                await stream.WriteAsync("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok"u8.ToArray());
            });

            using var http = new HttpClient(MtaStsResolver.CreateDefaultHandler(address => System.Net.IPAddress.IsLoopback(address)));

            Assert.That(await http.GetStringAsync($"http://localhost:{port}/"), Is.EqualTo("ok"));

        }

        #endregion

        /// <summary>
        /// A port on the loopback where nothing listens.
        /// </summary>
        private static UInt16 ClosedPort()
        {
            var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            var port = (UInt16) ((System.Net.IPEndPoint) listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

    }

}
