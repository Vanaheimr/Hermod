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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// The trace fields the server writes into a message: what it keeps of the Authentication-Results
    /// fields a message brings along (RFC 8601 §5), what it says SPF authenticated (§2.7.2), and the
    /// Received field of an SMTPUTF8 transaction (RFC 6531 §3.7.3). The findings S-43 to S-47 of
    /// SMTPConformanceTests.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        #region Setup

        /// <summary>
        /// One message to alice with the given MAIL parameters and header fields; the message as stored.
        /// </summary>
        private static async Task<String> Traced(Server Server, String Header = "", String MailParameters = "", String MailFrom = "sender@client.example")
        {

            using var wire = await Server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"),                       Is.EqualTo(250));
            Assert.That(await wire.CommandAsync($"MAIL FROM:<{MailFrom}>{MailParameters}"),     Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("RCPT TO:<alice@hermod.test>"),                 Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("DATA"),                                        Is.EqualTo(354));
            await wire.SendAsync(Header + "From: <ceo@bank.example>\r\nSubject: traced\r\n\r\nbody\r\n.\r\n");
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250));

            return Server.Storage.Messages.Last();

        }

        /// <summary>
        /// The header fields of a message with the given name, unfolded, top down.
        /// </summary>
        private static List<String> FieldsNamed(String Message, String Name)
        {
            var end = Message.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            return [ .. Regex.Split(end >= 0 ? Message[..end] : Message, "\r\n(?![ \t])").
                              Where (field => field.StartsWith(Name + ":", StringComparison.OrdinalIgnoreCase)).
                              Select(field => Regex.Replace(field, "\r\n[ \t]+", " ")) ];
        }

        #endregion


        #region Authentication-Results the message brought along (S-43, S-44)

        [TestCase("mx.hermod.test",                     Description = "RFC 8601 §5: this server's authserv-id")]
        [TestCase("MX.Hermod.TEST",                     Description = "a domain name, in any case")]
        [TestCase("(added here, says the sender) mx.hermod.test", Description = "after a comment (CFWS)")]
        [TestCase("\"mx.hermod.test\"",                 Description = "as a quoted-string")]
        [TestCase("mx.hermod.test 1",                   Description = "with version 1")]
        public async Task A_forged_Authentication_Results_field_of_this_server_is_removed(String AuthServId)
        {

            await using var server = new Server(DNS: new TxtDNS());

            var message = await Traced(server, $"Authentication-Results: {AuthServId};\r\n\tdkim=pass header.d=bank.example;\r\n\tdmarc=pass header.from=bank.example\r\n");
            var fields  = FieldsNamed(message, "Authentication-Results");

            Assert.That(fields,          Has.Count.EqualTo(1), String.Join(" | ", fields));
            Assert.That(fields[0],       Does.StartWith("Authentication-Results: mx.hermod.test;"));
            Assert.That(message,         Does.Not.Contain("header.d=bank.example"));

        }

        [Test(Description = "RFC 8601 §5: \"an MTA MUST remove such a header field\" - of a version it does not support - \"if the SMTP connection [SMTP] relaying the message is not from a trusted internal MTA\"")]
        public async Task An_Authentication_Results_field_of_another_version_is_removed()
        {

            await using var server = new Server(DNS: new TxtDNS());

            var message = await Traced(server, "Authentication-Results: mx.elsewhere.example 2; dmarc=pass header.from=bank.example\r\n");

            Assert.That(FieldsNamed(message, "Authentication-Results"), Has.Count.EqualTo(1));
            Assert.That(message, Does.Not.Contain("mx.elsewhere.example"));

        }

        [Test(Description = "RFC 8601 §5: the field of another ADMD is that ADMD's to vouch for - it is passed on as it came")]
        public async Task An_Authentication_Results_field_of_another_server_is_kept()
        {

            await using var server = new Server(DNS: new TxtDNS());

            var message = await Traced(server, "Authentication-Results: mx.elsewhere.example; spf=pass smtp.mailfrom=elsewhere.example\r\n");
            var fields  = FieldsNamed(message, "Authentication-Results");

            Assert.That(fields, Has.Count.EqualTo(2));
            Assert.That(fields[0], Does.StartWith("Authentication-Results: mx.hermod.test;"), "this server's on top");
            Assert.That(fields[1], Is.EqualTo("Authentication-Results: mx.elsewhere.example; spf=pass smtp.mailfrom=elsewhere.example"));

        }

        [Test(Description = "RFC 8601 §5: \"In the case of EAI-formatted messages, this test is done after converting A-labels into U-labels\" - a server named in U-labels knows its name in A-labels")]
        public async Task A_forged_field_in_A_labels_of_a_server_named_in_U_labels_is_removed()
        {

            await using var server = new Server(DNS: new TxtDNS(), Configure: config => config with { Hostname = "mx.bücher.test" });

            var message = await Traced(server, "Authentication-Results: mx.xn--bcher-kva.test; dmarc=pass header.from=bank.example\r\n");

            Assert.That(FieldsNamed(message, "Authentication-Results"), Has.Count.EqualTo(1));
            Assert.That(message, Does.Not.Contain("dmarc=pass"), "this server found no pass to report");

        }

        [Test(Description = "Only the header: a line in the body that reads like the field is the body's")]
        public void A_line_of_the_body_is_left_alone()
        {

            var message = Encoding.Latin1.GetBytes("Subject: Grüße\r\nAuthentication-Results: mx.hermod.test;\r\n\tdkim=pass\r\nX-After: yes\r\n\r\nAuthentication-Results: mx.hermod.test; dkim=pass\r\nTschüß\r\n");
            var kept    = AuthenticationResultsFields.WithoutForeign(message, "mx.hermod.test");

            Assert.That(kept, Is.EqualTo(Encoding.Latin1.GetBytes("Subject: Grüße\r\nX-After: yes\r\n\r\nAuthentication-Results: mx.hermod.test; dkim=pass\r\nTschüß\r\n")),
                        "the folded field removed whole, every other octet as it was");

            var none = Encoding.ASCII.GetBytes("Subject: hi\r\n\r\nbody\r\n");
            Assert.That(AuthenticationResultsFields.WithoutForeign(none, "mx.hermod.test"), Is.SameAs(none));

        }

        #endregion

        #region What SPF authenticated (S-45)

        [Test(Description = "RFC 8601 §2.7.2: the pvalue \"SHOULD NOT include the local-part or the following \"@\" character\" when the SPF policy has no provisions for it")]
        public async Task The_SPF_result_names_the_domain_of_the_reverse_path()
        {

            await using var server = new Server(DNS: new TxtDNS(("client.example", "v=spf1 ip4:127.0.0.1 ip6:::1 -all")));

            var results = FieldsNamed(await Traced(server), "Authentication-Results").Single();

            Assert.That(results, Does.Contain("spf=pass smtp.mailfrom=client.example;").And.Not.Contain("sender@"));

        }

        #endregion

        #region The Received field of an SMTPUTF8 transaction (S-46, S-47)

        [TestCase(" SMTPUTF8", "UTF8SMTP", Description = "RFC 6531 §3.7.3, §4.3")]
        [TestCase("",          "ESMTP",    Description = "RFC 3848")]
        public async Task The_Received_field_says_UTF8SMTP_for_an_SMTPUTF8_transaction(String MailParameters, String Protocol)
        {

            await using var server = new Server(DNS: new TxtDNS());

            var received = FieldsNamed(await Traced(server, MailParameters: MailParameters), "Received").First();

            Assert.That(received, Does.Match($@"\swith {Protocol}\s"));

        }

        [TestCase(" SMTPUTF8", "mx.bücher.test",        Description = "RFC 6531 §3.7.3: \"that server SHOULD use the U-label form for internationalized domain names in the new trace field\"")]
        [TestCase("",          "mx.xn--bcher-kva.test", Description = "RFC 6531 §3.7.3: without SMTPUTF8, \"trace field values must conform to RFC 5321\" (S-36)")]
        public async Task A_server_named_in_U_labels_names_itself_in_the_trace_fields_as_the_transaction_allows(String MailParameters, String Name)
        {

            await using var server = new Server(DNS: new TxtDNS(), Configure: config => config with { Hostname = "mx.bücher.test" });

            var message = await Traced(server, MailParameters: MailParameters);

            Assert.That(FieldsNamed(message, "Received").First(),               Does.Contain($" by {Name} "));
            Assert.That(FieldsNamed(message, "Authentication-Results").Single(), Does.StartWith($"Authentication-Results: {Name};"));

        }

        #endregion

    }

}
