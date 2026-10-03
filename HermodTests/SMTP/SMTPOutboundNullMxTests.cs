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
    /// A domain with a null MX (RFC 7505, "MX 0 .") accepts no mail: the relay client
    /// bounces at once instead of deferring and retrying.
    /// </summary>
    [TestFixture]
    public class SMTPOutboundNullMxTests
    {

        /// <summary>
        /// Answers every MX query with the null MX; nothing else.
        /// </summary>
        private sealed class NullMxDNS : IDNSClient
        {

            private static readonly DNSServerConfig origin = new (IPv4Address.Localhost, IPPort.Parse(53));

            public Task<DNSInfo> Query(DomainName DomainName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null,
                                       Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
            {

                IDNSResourceRecord[] answers = ResourceRecordTypes.Contains(DNSResourceRecordTypes.MX)
                                                   ? [ new MX(DomainName, DNSQueryClasses.IN, TimeSpan.FromMinutes(5), 0, DomainName.Parse(".")) ]
                                                   : [];

                return Task.FromResult(new DNSInfo(origin, 0, true, false, true, false, DNSResponseCodes.NoError,
                                                   answers, [], [], true, false, TimeSpan.FromSeconds(1), TimeSpan.Zero));

            }

            public Task<DNSInfo> Query(DNSServiceName DNSServiceName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null,
                                       Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
                => Task.FromResult(new DNSInfo(origin, 0, true, false, true, false, DNSResponseCodes.NoError,
                                               [], [], [], true, false, TimeSpan.FromSeconds(1), TimeSpan.Zero));

            public void Dispose() { }

            public ValueTask DisposeAsync()
                => ValueTask.CompletedTask;

            public override String ToString()
                => "null MX";

        }

        private sealed class QuietLogger : ILogger
        {
            public void Log(LogLevel level, String message) { }
        }


        [Test]
        public async Task A_null_MX_is_a_permanent_failure_without_a_delivery_attempt()
        {

            var client = new SMTPOutboundClient(new SmtpOutboundConfig {
                                                    LocalHostname     = "mx.hermod.test",
                                                    ConnectTimeoutMs  = 3000,
                                                    ReadTimeoutMs     = 3000
                                                },
                                                null,
                                                new NullMxDNS(),
                                                new QuietLogger());

            var result = await client.SendAsync("no-mail.example",
                                                "sender@hermod.test",
                                                [ "nobody@no-mail.example" ],
                                                "Subject: undeliverable\r\n\r\nbody\r\n");

            // RFC 7505 §4.1: no delivery attempt, and a bounce rather than a retry.
            Assert.That(result.Status,       Is.EqualTo(SendStatus.PermFail), result.ResponseText);
            Assert.That(result.ResponseCode, Is.EqualTo(556));
            Assert.That(result.ResponseText, Does.StartWith("5.1.10 "));
            Assert.That(result.RemoteMx,     Is.Null, "no MX was contacted");

        }

    }

}
