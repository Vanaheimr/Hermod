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
    /// RFC 3030 §2: "DATA and BDAT commands cannot be used in the same transaction. If a DATA
    /// statement is issued after a BDAT for the current transaction, a 503 'Bad sequence of
    /// commands' MUST be issued." In different transactions of one session both are fine.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        [Test]
        public async Task DATA_after_BDAT_in_the_same_transaction_is_503()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);

            await wire.SendAsync(Bdat("Subject: chunked\r\n\r\nfirst ", Last: false));
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250));

            Assert.That(await wire.CommandAsync("DATA"), Is.EqualTo(503));

            // The refused DATA changed nothing: the transaction finishes with BDAT, chunks intact.
            await wire.SendAsync(Bdat("second\r\n", Last: true));
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250));

            Assert.That(server.Storage.Messages.Single(), Does.EndWith("\r\n\r\nfirst second\r\n"));

        }


        [Test]
        public async Task DATA_in_the_transaction_after_a_BDAT_one_is_fine()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);
            await wire.SendAsync(Bdat("Subject: chunked\r\n\r\nbody\r\n", Last: true));
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250));

            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("RCPT TO:<alice@hermod.test>"),       Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("DATA"),                              Is.EqualTo(354));

            await wire.SendAsync("Subject: with DATA\r\n\r\nbody\r\n.\r\n");
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250));

            Assert.That(server.Storage.Messages, Has.Count.EqualTo(2));

        }

    }

}
