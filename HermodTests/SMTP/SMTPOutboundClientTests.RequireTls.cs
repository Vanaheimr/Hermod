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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// REQUIRETLS (RFC 8689) beyond the option itself: the MX hosts a message that requires TLS
    /// may go to (§2, §4.2.1), the next one after a host that cannot take it, 5.7.10 for no TLS,
    /// a bounce that carries the option (§5), and "TLS-Required: No" (§3). The findings O-23 to
    /// O-27 of SMTPConformanceTests.
    /// </summary>
    public partial class SMTPOutboundClientTests
    {

        #region Setup

        private const String TlsRequiredNoMessage = "Subject: your certificate expired\r\nTLS-Required: No\r\n\r\nhello\r\n";

        /// <summary>
        /// The MX records of next.example, the addresses of their hosts, and - when given - the
        /// _mta-sts TXT record of next.example.
        /// </summary>
        private sealed class RouteDns(IReadOnlyList<(String Host, UInt16 Preference)>  Mx,
                                      IReadOnlyDictionary<String, String>               Hosts,
                                      Boolean                                           MtaSts) : IDNSClient
        {

            private static readonly DNSServerConfig origin = new (IPv4Address.Localhost, IPPort.Parse(53));

            private Task<DNSInfo> Answer(String Name, IEnumerable<DNSResourceRecordTypes> Types)
            {

                var name    = Name.TrimEnd('.');
                var answers = new List<IDNSResourceRecord>();

                if (Types.Contains(DNSResourceRecordTypes.MX) && name == PolicyDomain)
                    answers.AddRange(Mx.Select(mx => new MX(DomainName.Parse(PolicyDomain), DNSQueryClasses.IN, TimeSpan.FromHours(1), mx.Preference, DomainName.Parse(mx.Host))));

                if (Types.Contains(DNSResourceRecordTypes.A) && Hosts.TryGetValue(name, out var address))
                    answers.Add(new A(DomainName.Parse(name), DNSQueryClasses.IN, TimeSpan.FromHours(1), IPv4Address.Parse(address)));

                if (Types.Contains(DNSResourceRecordTypes.TXT) && MtaSts && name == $"_mta-sts.{PolicyDomain}")
                    answers.Add(new TXT(DomainName.ParseLenient(name), DNSQueryClasses.IN, TimeSpan.FromHours(1), "v=STSv1; id=20261124;"));

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
                => nameof(RouteDns);

            public void Dispose() { }

            public ValueTask DisposeAsync()
                => ValueTask.CompletedTask;

        }

        /// <summary>
        /// One MX host of next.example, mail.next.example - named by the enforce policy - or another.
        /// </summary>
        private static RouteDns OneMx(String Host = "mail.next.example", Boolean MtaSts = true)
            => new ([ (Host, 10) ], new Dictionary<String, String> { [Host] = "127.0.0.1" }, MtaSts);

        /// <summary>
        /// Send to the MX hosts of next.example on the port given, trusting every certificate, with
        /// the MTA-STS policy of the Policy Host given.
        /// </summary>
        private static async Task<SendResult> SendToMx(IDNSClient   Dns,
                                                       IPPort       Port,
                                                       Boolean      RequireTls  = true,
                                                       String       From        = "sender@client.example",
                                                       String       Message     = "Subject: requiretls\r\n\r\nhello\r\n",
                                                       PolicyHost?  Policy      = null,
                                                       Boolean      TrustAny    = true,
                                                       DS?          TrustAnchor = null)
        {

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var client = new SMTPOutboundClient(new SmtpOutboundConfig {
                                                    LocalHostname               = "relay.hermod.test",
                                                    MxPort                      = Port,
                                                    ConnectTimeoutMs            = 3_000,
                                                    ReadTimeoutMs               = 5_000,
                                                    WriteTimeoutMs              = 3_000,
                                                    RemoteCertificateValidator  = TrustAny ? (_, _, _, _) => true : null,
                                                    MtaStsHttpHandler           = Policy ?? new PolicyHost(),
                                                    EnableDane                  = TrustAnchor is not null,
                                                    DnssecTrustAnchors          = TrustAnchor is not null ? [ TrustAnchor ] : null
                                                },
                                                null,
                                                Dns,
                                                new QuietLogger());

            var result = await client.SendAsync(PolicyDomain, From, [ "you@next.example" ], Message, requireTls: RequireTls, ct: cts.Token);

            await Task.Delay(100);
            return result;

        }

        #endregion


        #region The MX hosts: DNSSEC or MTA-STS (O-23)

        [Test(Description = "RFC 8689 §4.2.1: an MX record without a DNSSEC signature, and no MTA-STS policy - the message is not transmitted, 5.7.10")]
        public async Task A_REQUIRETLS_message_goes_to_no_MX_host_nothing_vouches_for()
        {

            using var nextHop = new NextHop(Certificate: SelfSigned(), RequireTls: true);

            var result = await SendToMx(OneMx(MtaSts: false), nextHop.Port);

            Assert.That(result.Status,       Is.EqualTo(SendStatus.PermFail));
            Assert.That(result.ResponseText, Does.StartWith("5.7.10 "));
            Assert.That(nextHop.Commands,    Is.Empty, "not even connected to");

        }

        [Test(Description = "RFC 8689 §4.2.1: an MX host the domain's MTA-STS policy names gets the message")]
        public async Task A_REQUIRETLS_message_goes_to_an_MX_host_its_MTA_STS_policy_names()
        {

            using var nextHop = new NextHop(Certificate: SelfSigned(), RequireTls: true);

            var result = await SendToMx(OneMx(), nextHop.Port);

            Assert.That(result.Status,   Is.EqualTo(SendStatus.Success), $"{result.ResponseCode} {result.ResponseText}");
            Assert.That(nextHop.TlsUsed, Is.True);
            Assert.That(Mail(nextHop),   Does.EndWith(" REQUIRETLS"));

        }

        [Test(Description = "RFC 8689 §2: \"the MX hostname MUST be validated by an MTA-STS policy\" - one in testing mode names its MX hosts as well, and one it does not name gets nothing")]
        public async Task An_MTA_STS_policy_in_testing_mode_vouches_only_for_the_MX_hosts_it_names()
        {

            using var nextHop = new NextHop(Certificate: SelfSigned(), RequireTls: true);

            var testing = new PolicyHost { Policy = EnforcePolicy.Replace("mode: enforce", "mode: testing") };

            var named    = await SendToMx(OneMx(),                    nextHop.Port, Policy: testing);
            var notNamed = await SendToMx(OneMx("smtp.next.example"), nextHop.Port, Policy: testing);

            Assert.That(named.Status,          Is.EqualTo(SendStatus.Success), $"{named.ResponseCode} {named.ResponseText}");
            Assert.That(notNamed.Status,       Is.EqualTo(SendStatus.PermFail));
            Assert.That(notNamed.ResponseText, Does.StartWith("5.7.10 "));

        }

        [Test(Description = "RFC 8689 §2: \"its name MUST be validated via a DNSSEC signature on the recipient domain's MX record\" - a signed MX record vouches for its host without an MTA-STS policy")]
        public async Task A_REQUIRETLS_message_goes_to_the_MX_host_of_a_signed_MX_record()
        {

            using var nextHop = new NextHop(Certificate: SelfSigned(), RequireTls: true);
            using var dns     = new SignedDns(PolicyDomain);

            dns.Answer(PolicyDomain, DNSResourceRecordTypes.MX, true, new MX(DomainName.Parse(PolicyDomain), DNSQueryClasses.IN, TimeSpan.FromHours(1), 10, DomainName.Parse(PolicyDomain)));
            dns.NoSuchName($"_{nextHop.Port}._tcp.{PolicyDomain}", DNSResourceRecordTypes.TLSA);

            var result = await SendToMx(dns, nextHop.Port, TrustAnchor: dns.TrustAnchor);

            Assert.That(result.Status, Is.EqualTo(SendStatus.Success), $"{result.ResponseCode} {result.ResponseText}");
            Assert.That(Mail(nextHop), Does.EndWith(" REQUIRETLS"));

        }

        [Test(Description = "RFC 8689 §4.2.1: an MX record whose signature does not validate is no MX record to go by - for now: the next attempt asks again")]
        public async Task A_REQUIRETLS_message_waits_for_an_MX_record_that_does_not_validate()
        {

            using var nextHop = new NextHop(Certificate: SelfSigned(), RequireTls: true);
            using var dns     = new SignedDns(PolicyDomain);

            // Unsigned inside the anchored zone: a stripped signature (RFC 4035 §4.3), bogus.
            dns.Answer(PolicyDomain, DNSResourceRecordTypes.MX, false, new MX(DomainName.Parse(PolicyDomain), DNSQueryClasses.IN, TimeSpan.FromHours(1), 10, DomainName.Parse(PolicyDomain)));
            dns.NoSuchName($"_{nextHop.Port}._tcp.{PolicyDomain}", DNSResourceRecordTypes.TLSA);

            var result = await SendToMx(dns, nextHop.Port, TrustAnchor: dns.TrustAnchor);

            Assert.That(result.Status,       Is.EqualTo(SendStatus.TempFail), $"{result.ResponseCode} {result.ResponseText}");
            Assert.That(result.ResponseText, Does.Contain("REQUIRETLS"), "held back for its MX records, not for DANE");
            Assert.That(nextHop.Commands,    Is.Empty);

        }

        [Test(Description = "RFC 8689 §4.2.1 is about the MX lookup: through a smart host there is none, and the smart host gets the message as before")]
        public async Task A_REQUIRETLS_message_goes_to_a_smart_host_without_an_MTA_STS_policy()
        {

            using var nextHop = new NextHop(Certificate: SelfSigned(), RequireTls: true);

            var result = await Send(nextHop, RequireTls: true);

            Assert.That(result.Status, Is.EqualTo(SendStatus.Success), $"{result.ResponseCode} {result.ResponseText}");

        }

        #endregion

        #region The next MX host (O-24)

        /// <summary>
        /// The first MX host, at 127.0.0.2 on the port of the second.
        /// </summary>
        private static NextHop FirstHost(String Kind, IPPort Port)

            => Kind switch {
                   "offers no REQUIRETLS"       => new NextHop(Certificate: SelfSigned(), RequireTls: false, Address: SecondLoopback, Port: Port),
                   "offers no STARTTLS"         => new NextHop(Address: SecondLoopback, Port: Port),
                   "refuses STARTTLS for good"  => new NextHop(Extensions: [ "8BITMIME", "STARTTLS" ],
                                                               Reply:      line => line.Equals("STARTTLS", StringComparison.OrdinalIgnoreCase) ? "554 5.7.0 no TLS here" : null,
                                                               Address:    SecondLoopback,
                                                               Port:       Port),
                   "refuses STARTTLS for now"   => new NextHop(Extensions: [ "8BITMIME", "STARTTLS" ],
                                                               Reply:      line => line.Equals("STARTTLS", StringComparison.OrdinalIgnoreCase) ? "454 4.7.0 TLS not available" : null,
                                                               Address:    SecondLoopback,
                                                               Port:       Port),
                   _                            => throw new ArgumentException(Kind)
               };

        [TestCase("offers no REQUIRETLS")]
        [TestCase("offers no STARTTLS")]
        [TestCase("refuses STARTTLS for good")]
        [TestCase("refuses STARTTLS for now")]
        public async Task A_REQUIRETLS_message_goes_on_to_the_next_MX_host_after_one_that(String Kind)
        {

            using var second = new NextHop(Certificate: SelfSigned(), RequireTls: true);
            using var first  = FirstHost(Kind, second.Port);

            var dns = new RouteDns([ ("a.mx.next.example", 10), ("b.mx.next.example", 20) ],
                                   new Dictionary<String, String> { ["a.mx.next.example"] = "127.0.0.2", ["b.mx.next.example"] = "127.0.0.1" },
                                   MtaSts: true);

            var result = await SendToMx(dns, second.Port);

            Assert.That(first.Commands,  Is.Not.Empty, "the first MX host was tried first");
            Assert.That(result.Status,   Is.EqualTo(SendStatus.Success), $"{result.ResponseCode} {result.ResponseText}");
            Assert.That(Delivered(first), Is.False);
            Assert.That(Mail(second),    Does.EndWith(" REQUIRETLS"));
            Assert.That(first.Commands,  Does.Contain("QUIT"), "RFC 8689 §4.2.1: \"the client MUST issue a QUIT to the server\"");

        }

        [Test(Description = "RFC 8689 §4.2.1: when no MX host offers REQUIRETLS, the message is not transmitted - 5.7.30, a bounce")]
        public async Task A_REQUIRETLS_message_no_MX_host_can_take_bounces()
        {

            using var second = new NextHop(Certificate: SelfSigned(), RequireTls: false);
            using var first  = new NextHop(Certificate: SelfSigned(), RequireTls: false, Address: SecondLoopback, Port: second.Port);

            var dns = new RouteDns([ ("a.mx.next.example", 10), ("b.mx.next.example", 20) ],
                                   new Dictionary<String, String> { ["a.mx.next.example"] = "127.0.0.2", ["b.mx.next.example"] = "127.0.0.1" },
                                   MtaSts: true);

            var result = await SendToMx(dns, second.Port);

            Assert.That(result.Status,       Is.EqualTo(SendStatus.PermFail));
            Assert.That(result.ResponseText, Does.StartWith("5.7.30 "));
            Assert.That(Delivered(first) || Delivered(second), Is.False);

        }

        #endregion

        #region No TLS: 5.7.10 (O-25)

        [Test(Description = "RFC 8689 §4.2.1: \"Unable to establish TLS-protected SMTP session: 5.7.10 Encryption needed\" - a next hop without STARTTLS")]
        public async Task No_STARTTLS_for_a_REQUIRETLS_message_is_5_7_10()
        {

            using var nextHop = new NextHop();

            var result = await Send(nextHop, RequireTls: true);

            Assert.That(result.Status,       Is.EqualTo(SendStatus.PermFail));
            Assert.That(result.ResponseText, Does.StartWith("5.7.10 "));
            Assert.That(Delivered(nextHop),  Is.False);

        }

        [Test(Description = "RFC 8689 §4.2.1: a certificate the relay cannot authenticate is no TLS-protected session - 5.7.10")]
        public async Task A_certificate_not_trusted_for_a_REQUIRETLS_message_is_5_7_10()
        {

            using var nextHop = new NextHop(Certificate: SelfSigned(), RequireTls: true);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var result = await ClientFor(nextHop).SendAsync("next.example", "sender@client.example", [ "you@next.example" ],
                                                             "Subject: requiretls\r\n\r\nhello\r\n", requireTls: true, ct: cts.Token);

            Assert.That(result.Status,       Is.EqualTo(SendStatus.PermFail));
            Assert.That(result.ResponseText, Does.StartWith("5.7.10 "));
            Assert.That(Delivered(nextHop),  Is.False);

        }

        [Test(Description = "A STARTTLS refused for now is a failure for now - the next attempt may have TLS")]
        public async Task STARTTLS_refused_for_now_for_a_REQUIRETLS_message_is_a_failure_for_now()
        {

            using var nextHop = new NextHop(Extensions: [ "8BITMIME", "STARTTLS" ],
                                            Reply:      line => line.Equals("STARTTLS", StringComparison.OrdinalIgnoreCase) ? "454 4.7.0 TLS not available" : null);

            var result = await Send(nextHop, RequireTls: true);

            Assert.That(result.Status,      Is.EqualTo(SendStatus.TempFail), $"{result.ResponseCode} {result.ResponseText}");
            Assert.That(Delivered(nextHop), Is.False);

        }

        #endregion

        #region A bounce with REQUIRETLS (O-26)

        [Test(Description = "RFC 8689 §5: \"When the MAIL FROM return-path is empty, the REQUIRETLS parameter SHOULD NOT cause a bounce message to be discarded even if the next-hop relay does not advertise REQUIRETLS\" - over TLS where offered")]
        public async Task A_bounce_with_REQUIRETLS_goes_to_a_next_hop_without_REQUIRETLS()
        {

            using var nextHop = new NextHop(Certificate: SelfSigned(), RequireTls: false);

            var result = await Send(nextHop, RequireTls: true, From: "");

            Assert.That(result.Status,   Is.EqualTo(SendStatus.Success), $"{result.ResponseCode} {result.ResponseText}");
            Assert.That(nextHop.TlsUsed, Is.True, "STARTTLS where the next hop offers it");
            Assert.That(Mail(nextHop),   Is.EqualTo("MAIL FROM:<>"));

        }

        [Test(Description = "RFC 8689 §5: a bounce of a REQUIRETLS message \"MUST also specify the REQUIRETLS SMTP option\" - where the next hop offers it")]
        public async Task A_bounce_with_REQUIRETLS_carries_it_to_a_next_hop_that_offers_it()
        {

            using var nextHop = new NextHop(Certificate: SelfSigned(), RequireTls: true);

            var result = await Send(nextHop, RequireTls: true, From: "");

            Assert.That(result.Status, Is.EqualTo(SendStatus.Success), $"{result.ResponseCode} {result.ResponseText}");
            Assert.That(Mail(nextHop), Does.StartWith("MAIL FROM:<>").And.EndWith(" REQUIRETLS"));

        }

        [Test(Description = "RFC 8689 §5: a bounce with REQUIRETLS goes in cleartext to a next hop without STARTTLS, rather than not at all")]
        public async Task A_bounce_with_REQUIRETLS_goes_to_a_next_hop_without_STARTTLS()
        {

            using var nextHop = new NextHop();

            var result = await Send(nextHop, RequireTls: true, From: "");

            Assert.That(result.Status, Is.EqualTo(SendStatus.Success), $"{result.ResponseCode} {result.ResponseText}");

        }

        #endregion

        #region "TLS-Required: No" (O-27)

        [TestCase("TLS-Required: No",                                    true)]
        [TestCase("tls-required:no",                                     true,  Description = "field names and the ABNF's strings are case-insensitive")]
        [TestCase("TLS-Required:\r\n No",                                true,  Description = "folded")]
        [TestCase("TLS-Required: Yes",                                   false)]
        [TestCase("TLS-Required: No\r\nTLS-Required: No",                false, Description = "\"More than one instance ... MUST NOT appear\"")]
        [TestCase("X-TLS-Required: No",                                  false)]
        [TestCase("Subject: hi\r\n\r\nTLS-Required: No",                 false, Description = "in the body, not the header")]
        public void TLS_Required_No_is_read_from_the_header(String Header, Boolean Expected)

            => Assert.That(SMTPOutboundClient.TlsRequiredNo($"From: <a@client.example>\r\n{Header}\r\n\r\nbody\r\n"), Is.EqualTo(Expected));


        [Test(Description = "RFC 8689 §3: \"TLS-Required: No\" - an enforce policy that would hold the message back for an MX host without STARTTLS is set aside")]
        public async Task TLS_Required_No_sets_aside_the_MTA_STS_policy_for_a_host_without_STARTTLS()
        {

            using var nextHop = new NextHop();

            var optional  = await SendToMx(OneMx(), nextHop.Port, RequireTls: false, Message: TlsRequiredNoMessage);
            var required  = await SendToMx(OneMx(), nextHop.Port, RequireTls: false);

            Assert.That(optional.Status, Is.EqualTo(SendStatus.Success),  $"{optional.ResponseCode} {optional.ResponseText}");
            Assert.That(required.Status, Is.EqualTo(SendStatus.TempFail), "without the field, the policy holds");

        }

        [Test(Description = "RFC 8689 §3: \"TLS-Required: No\" - an MX host the enforce policy does not name gets the message, over STARTTLS where offered: \"the client SHOULD negotiate STARTTLS with the server if available\"")]
        public async Task TLS_Required_No_sets_aside_the_MTA_STS_policy_for_a_host_it_does_not_name()
        {

            using var nextHop = new NextHop(Certificate: SelfSigned());

            var result = await SendToMx(OneMx("smtp.next.example"), nextHop.Port, RequireTls: false, Message: TlsRequiredNoMessage, TrustAny: false);

            Assert.That(result.Status,   Is.EqualTo(SendStatus.Success), $"{result.ResponseCode} {result.ResponseText}");
            Assert.That(nextHop.TlsUsed, Is.True);

        }

        [Test(Description = "RFC 8689 §4.1: \"If the REQUIRETLS MAIL FROM parameter is specified, the TLS-Required header field MUST be ignored\"")]
        public async Task With_REQUIRETLS_the_TLS_Required_field_is_ignored()
        {

            using var nextHop = new NextHop();

            var result = await SendToMx(OneMx(), nextHop.Port, RequireTls: true, Message: TlsRequiredNoMessage);

            Assert.That(result.Status,       Is.EqualTo(SendStatus.PermFail));
            Assert.That(result.ResponseText, Does.StartWith("5.7.10 "));
            Assert.That(Delivered(nextHop),  Is.False);

        }

        #endregion

    }

}
