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

using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// A smart host per domain: a message leaves through the way of the domain of
    /// its From: - its own relay, with its own login - and through the way of the
    /// configuration when its domain has none.
    /// </summary>
    public partial class SMTPOutboundClientTests
    {

        [Test]
        public async Task Every_domain_leaves_through_its_own_smart_host_with_its_own_login()
        {

            String? Auth(String Line)
                => Line.StartsWith("AUTH", StringComparison.OrdinalIgnoreCase) ? "235 2.7.0 ok" : null;

            using var server  = new NextHop(Auth, Extensions: [ "8BITMIME", "AUTH PLAIN" ]);   // the server's
            using var own     = new NextHop(Auth, Extensions: [ "8BITMIME", "AUTH PLAIN" ]);   // example.org's provider

            var client = new SMTPOutboundClient(new SmtpOutboundConfig {
                                                    LocalHostname            = "relay.hermod.test",
                                                    SmartHost                = "127.0.0.1",
                                                    SmartHostPort            = server.Port,
                                                    SmartHostUsername        = "server",
                                                    SmartHostPassword        = "server-secret",
                                                    SmartHostAuthWithoutTls  = true,            // relays on the loopback
                                                    ConnectTimeoutMs         = 3_000,
                                                    ReadTimeoutMs            = 5_000,
                                                    WriteTimeoutMs           = 3_000
                                                },
                                                null,
                                                new NoDNS(),
                                                new QuietLogger(),
                                                smartHostFor: domain => domain == "example.org"
                                                                            ? new SmartHostRoute("127.0.0.1", own.Port, "org", "org-secret")
                                                                            : null);

            var fromOrg   = await client.SendAsync("next.example", "anna@example.org", [ "you@next.example" ], Message("anna@example.org"));
            var fromNet   = await client.SendAsync("next.example", "ben@example.net",  [ "you@next.example" ], Message("ben@example.net"));

            await Task.Delay(100);

            String Login(NextHop NextHop)
                => Encoding.UTF8.GetString(Convert.FromBase64String(NextHop.Commands.Single(command => command.StartsWith("AUTH PLAIN ")).Split(' ')[2])).Split('\0')[1];

            Assert.Multiple(() => {

                Assert.That(fromOrg.Status,  Is.EqualTo(SendStatus.Success), fromOrg.ResponseText);
                Assert.That(fromNet.Status,  Is.EqualTo(SendStatus.Success), fromNet.ResponseText);

                Assert.That(own.   Commands.Count(command => command.StartsWith("MAIL")), Is.EqualTo(1), "example.org through its own relay");
                Assert.That(server.Commands.Count(command => command.StartsWith("MAIL")), Is.EqualTo(1), "example.net through the server's");
                Assert.That(own.   Commands.Single(command => command.StartsWith("MAIL")), Does.Contain("anna@example.org"));

                Assert.That(Login(own),     Is.EqualTo("org"),    "with its own login");
                Assert.That(Login(server),  Is.EqualTo("server"));

                Assert.That(new SmartHostRoute("relay.example", 587, "org", "org-secret").ToString(), Does.Not.Contain("secret"), "no password in a log");

            });

        }

    }

}
