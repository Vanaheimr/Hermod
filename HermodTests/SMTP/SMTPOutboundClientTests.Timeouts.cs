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
    /// RFC 5321 §4.5.3.2: the timeouts of the relay - per command, and 10 minutes for the reply
    /// to the end of data (§4.5.3.2.6), "since it has been successfully sent and the server has
    /// accepted responsibility for delivery".
    /// </summary>
    public partial class SMTPOutboundClientTests
    {

        private static async Task<SendResult> SendWithTimeouts(NextHop NextHop, UInt32 ReadTimeoutMs, UInt32 DataTerminationTimeoutMs)
        {

            var client = new SMTPOutboundClient(new SmtpOutboundConfig {
                                                    LocalHostname             = "relay.hermod.test",
                                                    SmartHost                 = "127.0.0.1",
                                                    SmartHostPort             = NextHop.Port,
                                                    ConnectTimeoutMs          = 3_000,
                                                    ReadTimeoutMs             = ReadTimeoutMs,
                                                    WriteTimeoutMs            = 3_000,
                                                    DataTerminationTimeoutMs  = DataTerminationTimeoutMs
                                                },
                                                null,
                                                new NoDNS(),
                                                new QuietLogger());

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

            return await client.SendAsync("next.example", "sender@client.example", [ "you@next.example" ],
                                          "Subject: slow\r\n\r\nhello\r\n", ct: cts.Token);

        }

        // A next hop that takes its time to deliver: the reply to the end of data comes late.
        private static NextHop Delivering(TimeSpan Delay)
            => new (line => {
                        if (line != ".")
                            return null;
                        Thread.Sleep(Delay);
                        return "250 2.0.0 delivered";
                    });


        [Test(Description = "RFC 5321 §4.5.3.2.6: the reply to the end of data has a timeout of its own, longer than that of a command - a next hop that answers after the read timeout, within it, has the message, and the relay knows it")]
        public async Task The_reply_to_the_end_of_data_is_waited_for_longer_than_a_command()
        {

            using var nextHop = Delivering(TimeSpan.FromMilliseconds(1500));

            var result = await SendWithTimeouts(nextHop, ReadTimeoutMs: 500, DataTerminationTimeoutMs: 6_000);

            Assert.That(result.Status, Is.EqualTo(SendStatus.Success), result.ResponseText);

        }


        [Test(Description = "RFC 5321 §4.5.3.2: \"An SMTP client MUST provide a timeout mechanism\" - past the DATA termination timeout the relay gives up, for now")]
        public async Task Past_the_data_termination_timeout_the_relay_gives_up_for_now()
        {

            using var nextHop = Delivering(TimeSpan.FromMilliseconds(2500));

            var result = await SendWithTimeouts(nextHop, ReadTimeoutMs: 5_000, DataTerminationTimeoutMs: 500);

            Assert.That(result.Status, Is.EqualTo(SendStatus.TempFail), result.ResponseText);

        }


        [Test(Description = "RFC 5321 §4.5.3.2: \"the minimum per-command timeout values SHOULD be as follows\" - 5 minutes for the greeting, MAIL and RCPT, 3 for a data block, 10 for the reply to the end of data: the defaults are no shorter")]
        public void The_default_timeouts_are_those_of_RFC_5321()
        {

            var config = new SmtpOutboundConfig { LocalHostname = "relay.hermod.test" };

            Assert.Multiple(() => {
                Assert.That(config.ReadTimeoutMs,             Is.GreaterThanOrEqualTo(300_000));
                Assert.That(config.WriteTimeoutMs,            Is.GreaterThanOrEqualTo(180_000));
                Assert.That(config.DataTerminationTimeoutMs,  Is.GreaterThanOrEqualTo(600_000));
            });

        }

    }

}
