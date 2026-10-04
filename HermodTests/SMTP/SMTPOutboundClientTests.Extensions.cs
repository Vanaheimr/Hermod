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

using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// What a relayed message needs of the next hop is declared on MAIL - SMTPUTF8 (RFC 6531),
    /// BODY=8BITMIME (RFC 6152), REQUIRETLS (RFC 8689) - and a next hop that does not offer it
    /// does not get the message. Nothing is converted: that would break DKIM and OpenPGP
    /// signatures over the content.
    /// </summary>
    public partial class SMTPOutboundClientTests
    {

        private static String Mail(NextHop NextHop)
            => NextHop.Commands.FirstOrDefault(command => command.StartsWith("MAIL")) ?? "";


        [Test]
        public async Task A_UTF8_recipient_goes_out_with_SMTPUTF8()
        {

            using var nextHop = new NextHop();

            var result = await Send(nextHop, [ "jöran@next.example" ]);

            Assert.That(result.Status, Is.EqualTo(SendStatus.Success), result.ResponseText);
            Assert.That(Mail(nextHop), Does.Contain(" SMTPUTF8"));
            Assert.That(nextHop.Commands, Has.Some.EqualTo("RCPT TO:<jöran@next.example>"));

        }


        [Test]
        public async Task A_UTF8_header_needs_SMTPUTF8_as_well()
        {

            using var nextHop = new NextHop();

            await Send(nextHop, Subject: "Grüße");

            Assert.That(Mail(nextHop), Does.Contain(" SMTPUTF8"));

        }


        [Test]
        public async Task A_message_that_needs_SMTPUTF8_is_not_sent_to_a_server_without_it()
        {

            using var nextHop = new NextHop(Extensions: [ "8BITMIME", "DSN" ]);

            var result = await Send(nextHop, [ "jöran@next.example" ]);

            Assert.That(result.Status,       Is.EqualTo(SendStatus.PermFail));
            Assert.That(result.ResponseText, Does.StartWith("5.6.7 "));
            Assert.That(nextHop.Commands,    Has.None.StartsWith("MAIL"));

        }


        [Test]
        public async Task Eight_bit_content_goes_out_as_BODY_8BITMIME()
        {

            using var nextHop = new NextHop();

            var result = await Send(nextHop, Body: "Grüße aus Köln");

            Assert.That(result.Status, Is.EqualTo(SendStatus.Success), result.ResponseText);
            Assert.That(Mail(nextHop), Does.Contain(" BODY=8BITMIME").And.Not.Contain("SMTPUTF8"),
                        "8-bit in the body only: 8BITMIME, not SMTPUTF8");

        }


        [Test]
        public async Task Eight_bit_content_is_not_sent_to_a_server_without_8BITMIME()
        {

            using var nextHop = new NextHop(Extensions: [ "DSN" ]);

            var result = await Send(nextHop, Body: "Grüße aus Köln");

            Assert.That(result.Status,       Is.EqualTo(SendStatus.PermFail));
            Assert.That(result.ResponseText, Does.StartWith("5.6.3 "));
            Assert.That(nextHop.DataLines,   Is.Empty);

        }


        [Test]
        public async Task An_ASCII_message_declares_nothing()
        {

            using var nextHop = new NextHop();

            await Send(nextHop);

            Assert.That(Mail(nextHop), Is.EqualTo("MAIL FROM:<sender@client.example>"));

        }


        [Test]
        public async Task A_REQUIRETLS_message_goes_over_TLS_and_carries_REQUIRETLS()
        {

            using var nextHop = new NextHop(Certificate: SelfSigned(), RequireTls: true);

            var result = await Send(nextHop, RequireTls: true);

            Assert.That(result.Status,   Is.EqualTo(SendStatus.Success), result.ResponseText);
            Assert.That(nextHop.TlsUsed, Is.True);
            Assert.That(Mail(nextHop),   Does.Contain(" REQUIRETLS"));

        }


        [Test]
        public async Task A_REQUIRETLS_message_is_not_sent_to_a_next_hop_without_REQUIRETLS()
        {

            using var nextHop = new NextHop(Certificate: SelfSigned(), RequireTls: false);

            var result = await Send(nextHop, RequireTls: true);

            Assert.That(result.Status,       Is.EqualTo(SendStatus.PermFail));
            Assert.That(result.ResponseText, Does.StartWith("5.7.30 "));
            Assert.That(nextHop.Commands,    Has.None.StartsWith("MAIL"));

        }

    }

}
