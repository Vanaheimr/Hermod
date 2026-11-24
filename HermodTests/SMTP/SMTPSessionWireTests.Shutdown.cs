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

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// RFC 5321 §3.8: a server closes a connection "After detecting the need to shut down the
    /// SMTP service and returning a 421 response code" - whatever the session is doing.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        private static async Task AssertShutDown(Server server, Wire wire)
        {

            var stopping = server.StopAsync();

            var reply = await wire.ReplyAsync(TimeSpan.FromSeconds(5));

            Assert.That(reply.Code, Is.EqualTo(421));
            Assert.That(reply.Text, Does.StartWith("4.3.2 "), $"reply text was \"{reply.Text}\"");
            Assert.That(await wire.TryReplyAsync(TimeSpan.FromSeconds(3)), Is.Null, "the server closes the connection");

            await stopping.WaitAsync(TimeSpan.FromSeconds(10));

        }


        [Test]
        public async Task A_server_that_shuts_down_says_421_to_a_client_after_the_greeting()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            await AssertShutDown(server, wire);

        }


        [Test]
        public async Task A_server_that_shuts_down_says_421_in_a_transaction()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);

            await AssertShutDown(server, wire);

        }


        [Test]
        public async Task A_server_that_shuts_down_says_421_in_the_middle_of_DATA_and_delivers_nothing()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);
            Assert.That(await wire.CommandAsync("DATA"), Is.EqualTo(354));
            await wire.SendAsync("Subject: half a message\r\n\r\nand then the server stops");

            await AssertShutDown(server, wire);
            Assert.That(server.Storage.Messages, Is.Empty);

        }


        [Test]
        public async Task A_server_that_shuts_down_says_421_in_the_middle_of_a_BDAT_chunk()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);
            await wire.SendAsync("BDAT 100 LAST\r\nonly twenty octets..");

            await AssertShutDown(server, wire);
            Assert.That(server.Storage.Messages, Is.Empty);

        }


        [Test]
        public async Task A_client_that_said_QUIT_gets_no_421()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("QUIT"), Is.EqualTo(221));

            await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));

            Assert.That(await wire.TryReplyAsync(TimeSpan.FromSeconds(2)), Is.Null);

        }

    }

}
