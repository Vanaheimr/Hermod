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
    /// SMTPServerConfig.VerifySpf, VerifyDkim and VerifyDmarc: a check that is switched
    /// off neither rejects a message nor appears in its Authentication-Results.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        /// <summary>
        /// TXT records by owner name; every other query has no answer.
        /// </summary>
        private sealed class TxtDNS(params (String Name, String Text)[] Records) : IDNSClient
        {

            private static readonly DNSServerConfig origin = new (IPv4Address.Localhost, IPPort.Parse(53));

            private Task<DNSInfo> Answer(String Name, IEnumerable<DNSResourceRecordTypes> Types)
            {

                var name    = Name.TrimEnd('.').ToLowerInvariant();
                var answers = Types.Contains(DNSResourceRecordTypes.TXT)
                                  ? Records.Where (record => record.Name == name).
                                            Select(record => (IDNSResourceRecord) new TXT(DNSServiceName.Parse(name), DNSQueryClasses.IN, TimeSpan.FromMinutes(5), record.Text)).
                                            ToArray()
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
                => "TXT records";

        }

        private static readonly (String, String) SpfHardFail  = ("client.example",        "v=spf1 -all");
        private static readonly (String, String) DmarcReject  = ("_dmarc.client.example", "v=DMARC1; p=reject");


        /// <summary>
        /// Send one message from sender@client.example; the reply to its end of data.
        /// </summary>
        private static async Task<(Int32 Code, String Text)> SendFromClientExample(Server Server)
        {

            using var wire = await Server.ConnectAsync();

            await OpenTransaction(wire);
            Assert.That(await wire.CommandAsync("DATA"), Is.EqualTo(354));
            await wire.SendAsync("From: sender@client.example\r\nSubject: checked\r\n\r\nbody\r\n.\r\n");

            return await wire.ReplyAsync();

        }

        private static String AuthenticationResults(Server Server)
        {
            var message = Server.Storage.Messages.Single();
            return message[..message.IndexOf("Received:", StringComparison.Ordinal)];
        }


        [Test]
        public async Task An_SPF_hard_fail_is_rejected_while_VerifySpf_is_on()
        {

            await using var server = new Server(DNS: new TxtDNS(SpfHardFail));

            var reply = await SendFromClientExample(server);

            Assert.That(reply.Code, Is.EqualTo(550));
            Assert.That(reply.Text, Does.StartWith("5.7.23 "));

        }


        [Test]
        public async Task VerifySpf_off_neither_rejects_nor_reports_SPF()
        {

            await using var server = new Server(DNS: new TxtDNS(SpfHardFail), Configure: config => config with { VerifySpf = false });

            Assert.That((await SendFromClientExample(server)).Code, Is.EqualTo(250));

            var results = AuthenticationResults(server);
            Assert.That(results, Does.Not.Contain("spf="));
            Assert.That(results, Does.Contain("dkim=none").And.Contain("dmarc=none"), "the other checks still ran");

        }


        [Test]
        public async Task A_DMARC_reject_policy_is_enforced_while_VerifyDmarc_is_on()
        {

            await using var server = new Server(DNS: new TxtDNS(DmarcReject));

            var reply = await SendFromClientExample(server);

            Assert.That(reply.Code, Is.EqualTo(550));
            Assert.That(reply.Text, Does.Contain("DMARC"));

        }


        [Test]
        public async Task VerifyDmarc_off_neither_enforces_nor_reports_DMARC()
        {

            await using var server = new Server(DNS: new TxtDNS(DmarcReject), Configure: config => config with { VerifyDmarc = false });

            Assert.That((await SendFromClientExample(server)).Code, Is.EqualTo(250));

            var results = AuthenticationResults(server);
            Assert.That(results, Does.Not.Contain("dmarc="));
            Assert.That(results, Does.Contain("spf=none").And.Contain("dkim=none"), "the other checks still ran");

        }


        [Test]
        public async Task With_every_check_off_Authentication_Results_says_none()
        {

            await using var server = new Server(DNS:       new TxtDNS(SpfHardFail, DmarcReject),
                                                Configure: config => config with { VerifySpf = false, VerifyDkim = false, VerifyDmarc = false });

            Assert.That((await SendFromClientExample(server)).Code, Is.EqualTo(250));

            Assert.That(AuthenticationResults(server), Is.EqualTo("Authentication-Results: mx.hermod.test; none\r\n"));

        }

    }

}
