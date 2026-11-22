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
    /// RFC 6531 §3.7.1: "the SMTPUTF8-aware SMTP client MUST send only ASCII (LDH label or A-label
    /// [RFC5890]) domains in the EHLO command" - a relay named in U-labels says its A-labels.
    /// </summary>
    public partial class SMTPOutboundClientTests
    {

        [TestCase("relais.bücher.test",        "EHLO relais.xn--bcher-kva.test")]
        [TestCase("relais.xn--bcher-kva.test", "EHLO relais.xn--bcher-kva.test")]
        [TestCase("relay.hermod.test",         "EHLO relay.hermod.test")]
        public async Task The_relay_says_EHLO_with_its_name_in_A_labels(String LocalHostname, String Ehlo)
        {

            using var nextHop = new NextHop(Octets: true);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

            var result = await new SMTPOutboundClient(new SmtpOutboundConfig {
                                                          LocalHostname     = LocalHostname,
                                                          SmartHost         = "127.0.0.1",
                                                          SmartHostPort     = nextHop.Port,
                                                          ConnectTimeoutMs  = 3_000,
                                                          ReadTimeoutMs     = 5_000,
                                                          WriteTimeoutMs    = 3_000
                                                      },
                                                      null, new NoDNS(), new QuietLogger()).
                                   SendAsync("next.example", "sender@client.example", [ "you@next.example" ], "Subject: ehlo\r\n\r\nhello\r\n", ct: cts.Token);

            await Task.Delay(100);

            Assert.That(result.Status,     Is.EqualTo(SendStatus.Success));
            Assert.That(nextHop.Commands,  Does.Contain(Ehlo));

        }

    }

}
