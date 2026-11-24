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

using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// Signing in to a smart host (RFC 4954): the AUTH=&lt;&gt; parameter on every message relayed
    /// through a server the relay authenticated to (§5), and no initial response that would make
    /// the AUTH command longer than a command line may be (§4).
    /// </summary>
    public partial class SMTPOutboundClientTests
    {

        private static SMTPOutboundClient SignedInTo(NextHop SmartHost, String? Username = "relay-user", String Password = "relay-secret")

            => new (new SmtpOutboundConfig {
                        LocalHostname            = "relay.hermod.test",
                        SmartHost                = "127.0.0.1",
                        SmartHostPort            = SmartHost.Port,
                        SmartHostUsername        = Username,
                        SmartHostPassword        = Username is not null ? Password : null,
                        SmartHostAuthWithoutTls  = true,            // relays on the loopback
                        ConnectTimeoutMs         = 3_000,
                        ReadTimeoutMs            = 5_000,
                        WriteTimeoutMs           = 3_000
                    },
                    null,
                    new NoDNS(),
                    new QuietLogger());

        private static String PlainResponse(String Username, String Password)
            => Convert.ToBase64String(Encoding.UTF8.GetBytes($"\0{Username}\0{Password}"));


        #region AUTH=<> (RFC 4954 §5)

        [Test]
        public async Task A_smart_host_signed_in_to_gets_AUTH_empty_with_MAIL()
        {

            using var smartHost = new NextHop(line => line.StartsWith("AUTH", StringComparison.OrdinalIgnoreCase) ? "235 2.7.0 ok" : null,
                                              Extensions: [ "8BITMIME", "AUTH PLAIN" ]);

            var result = await SignedInTo(smartHost).SendAsync("next.example", "anna@example.org", [ "you@next.example" ], Message());

            await Task.Delay(100);

            Assert.Multiple(() => {
                Assert.That(result.Status,                                                          Is.EqualTo(SendStatus.Success), result.ResponseText);
                Assert.That(smartHost.Commands.Single(command => command.StartsWith("MAIL")),       Is.EqualTo("MAIL FROM:<anna@example.org> AUTH=<>"));
            });

        }


        [Test]
        public async Task A_smart_host_not_signed_in_to_gets_no_AUTH_parameter()
        {

            using var smartHost = new NextHop(Extensions: [ "8BITMIME", "AUTH PLAIN" ]);

            var result = await SignedInTo(smartHost, Username: null).SendAsync("next.example", "anna@example.org", [ "you@next.example" ], Message());

            await Task.Delay(100);

            Assert.Multiple(() => {
                Assert.That(result.Status,                                                          Is.EqualTo(SendStatus.Success), result.ResponseText);
                Assert.That(smartHost.Commands,                                                     Has.None.StartsWith("AUTH"));
                Assert.That(smartHost.Commands.Single(command => command.StartsWith("MAIL")),       Is.EqualTo("MAIL FROM:<anna@example.org>"));
            });

        }

        #endregion


        #region The initial response and the line length (RFC 4954 §4, RFC 5321 §4.5.3.1.4)

        [Test]
        public async Task A_long_password_goes_to_the_smart_host_after_the_challenge()
        {

            var password = new String('p', 400);
            var response = PlainResponse("relay-user", password);

            using var smartHost = new NextHop(line => line == "AUTH PLAIN"                              ? "334 "
                                                    : line.StartsWith("AUTH", StringComparison.Ordinal)  ? "535 5.7.8 no initial response this long"
                                                    : line == response                                   ? "235 2.7.0 ok"
                                                    :                                                      null,
                                              Extensions: [ "8BITMIME", "AUTH PLAIN" ]);

            var result = await SignedInTo(smartHost, Password: password).SendAsync("next.example", "anna@example.org", [ "you@next.example" ], Message());

            await Task.Delay(100);

            var commands = smartHost.Commands.ToList();

            Assert.Multiple(() => {
                Assert.That(result.Status,                                                  Is.EqualTo(SendStatus.Success), result.ResponseText);
                Assert.That(commands.Where(command => command.StartsWith("AUTH")).Select(command => Encoding.UTF8.GetByteCount(command) + 2), Has.All.LessThanOrEqualTo(512), "the AUTH command fits a command line; the response after the 334 need not");
                Assert.That(commands,                                                       Does.Contain("AUTH PLAIN"));
                Assert.That(commands,                                                       Does.Contain(response));
            });

        }


        [Test]
        public async Task A_short_password_goes_to_the_smart_host_as_the_initial_response()
        {

            using var smartHost = new NextHop(line => line.StartsWith("AUTH", StringComparison.OrdinalIgnoreCase) ? "235 2.7.0 ok" : null,
                                              Extensions: [ "8BITMIME", "AUTH PLAIN" ]);

            var result = await SignedInTo(smartHost).SendAsync("next.example", "anna@example.org", [ "you@next.example" ], Message());

            await Task.Delay(100);

            Assert.Multiple(() => {
                Assert.That(result.Status,          Is.EqualTo(SendStatus.Success), result.ResponseText);
                Assert.That(smartHost.Commands,     Does.Contain("AUTH PLAIN " + PlainResponse("relay-user", "relay-secret")));
            });

        }

        #endregion

    }

}
