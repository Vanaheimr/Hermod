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

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// RFC 6531 §3.2: "When doing lookups, the SMTPUTF8-aware SMTP client or server MUST either
    /// use a Unicode-aware DNS library, or transform the internationalized domain name to A-label
    /// form" - the MX hosts of "bücher.test" are those of "xn--bcher-kva.test".
    /// </summary>
    public partial class SMTPOutboundRoutingTests
    {

        [TestCase("bücher.test")]
        [TestCase("Bücher.Test")]
        [TestCase("xn--bcher-kva.test")]
        public async Task The_MX_hosts_of_a_domain_in_U_labels_are_those_of_its_A_labels(String Domain)
        {

            var dns = new ZoneDNS().Mx("xn--bcher-kva.test", 10, "mx.xn--bcher-kva.test");

            await Send(dns, Domain);

            var queries = String.Join(", ", dns.Queries.Select(query => $"{query.Name} {query.Type}"));

            Assert.That(dns.LookedUpAddressOf("mx.xn--bcher-kva.test"),                             Is.True, queries);
            Assert.That(dns.Queries.All(query => query.Name.All(c => c < 0x80)),                     Is.True, queries);
            Assert.That(dns.Queries.Any(query => query.Name == "_mta-sts.xn--bcher-kva.test" &&
                                                 query.Type == DNSResourceRecordTypes.TXT),           Is.True, queries);

        }


        [Test]
        public async Task A_relay_named_in_U_labels_knows_itself_among_the_MX_hosts_in_A_labels()
        {

            var dns    = new ZoneDNS().Mx("next.example", 10, "relay.xn--bcher-kva.test");

            var result = await new SMTPOutboundClient(new SmtpOutboundConfig {
                                                          LocalHostname     = "relay.bücher.test",
                                                          ConnectTimeoutMs  = 3_000,
                                                          ReadTimeoutMs     = 5_000
                                                      },
                                                      null, dns, new QuietLogger()).
                                   SendAsync("next.example", "sender@client.example", [ "you@next.example" ], "Subject: routing\r\n\r\nhello\r\n");

            Assert.That(result.Status,        Is.EqualTo(SendStatus.PermFail));
            Assert.That(result.ResponseText,  Does.Contain("5.4.6"), "itself the best MX host: a routing loop");

        }

    }

}
