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
    /// RFC 5321 §4.5.3.2: the server waits SessionTimeout for the client, then gives up; §3.8
    /// allows closing then. It says why first: 421 4.4.2.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        private static SMTPServerConfig WithShortTimeout(SMTPServerConfig config)
            => config with { SessionTimeout = TimeSpan.FromSeconds(1) };


        private static async Task AssertTimedOut(Wire wire)
        {

            var reply = await wire.ReplyAsync(TimeSpan.FromSeconds(5));

            Assert.That(reply.Code, Is.EqualTo(421));
            Assert.That(reply.Text, Does.StartWith("4.4.2 "), $"reply text was \"{reply.Text}\"");
            Assert.That(await wire.TryReplyAsync(TimeSpan.FromSeconds(3)), Is.Null, "the server closes the connection");

        }


        [Test]
        public async Task An_idle_client_gets_421_and_the_connection_closes()
        {

            await using var server = new Server(Configure: WithShortTimeout);
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));

            await AssertTimedOut(wire);

        }


        [Test]
        public async Task A_client_idle_in_the_middle_of_DATA_gets_421_and_nothing_is_delivered()
        {

            await using var server = new Server(Configure: WithShortTimeout);
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);
            Assert.That(await wire.CommandAsync("DATA"), Is.EqualTo(354));
            await wire.SendAsync("Subject: half a message\r\n\r\nand then nothing");

            await AssertTimedOut(wire);
            Assert.That(server.Storage.Messages, Is.Empty);

        }


        [Test]
        public async Task A_client_idle_in_the_middle_of_a_BDAT_chunk_gets_421()
        {

            await using var server = new Server(Configure: WithShortTimeout);
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);
            await wire.SendAsync("BDAT 100 LAST\r\nonly twenty octets..");

            await AssertTimedOut(wire);
            Assert.That(server.Storage.Messages, Is.Empty);

        }


        [Test]
        public async Task A_client_that_keeps_talking_is_not_timed_out()
        {

            await using var server = new Server(Configure: WithShortTimeout);
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));

            for (var i = 0; i < 4; i++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(600));
                Assert.That(await wire.CommandAsync("NOOP"), Is.EqualTo(250));
            }

        }

    }

}
