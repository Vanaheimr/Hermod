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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// RFC 3030 §2: bdat-cmd ::= "BDAT" SP chunk-size [ SP end-marker ] CR LF, chunk-size ::=
    /// 1*DIGIT, end-marker ::= "LAST". The chunk follows the command at once, so a BDAT the
    /// server refuses still has its chunk behind it; every refused chunk here is two NOOPs, which
    /// a server that read them as commands would answer. And RFC 5321 §4.1.4: MAIL, RCPT, then
    /// the message, "in that order" - no RCPT once a chunk of it has been sent.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        private const String TwoNoops = "NOOP\r\nNOOP\r\n";

        private static Byte[] RawBdat(String Command, String Chunk)
            => [ .. Encoding.ASCII.GetBytes(Command + "\r\n"), .. Encoding.ASCII.GetBytes(Chunk) ];


        [TestCase("BDAT 12 FOO",     TestName = "A BDAT with an end-marker that is not LAST is 501, after its chunk")]
        [TestCase("BDAT 12 LAST X",  TestName = "A BDAT with an argument after LAST is 501, after its chunk")]
        public async Task A_BDAT_outside_the_grammar_is_501_after_its_chunk(String Command)
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);

            await wire.SendAsync(RawBdat(Command, TwoNoops));

            Assert.That(await wire.DrainAsync(TimeSpan.FromSeconds(1)),      Is.EqualTo(new[] { 501 }), "one 501, nothing to the chunk");
            Assert.That(await wire.CommandAsync("NOOP"),                    Is.EqualTo(250),           "the session is still in step");
            Assert.That(await wire.CommandAsync("RCPT TO:<bob@hermod.test>"), Is.EqualTo(503),         "the transaction is gone");
            Assert.That(server.Storage.Messages,                             Is.Empty);

        }


        [TestCase("BDAT +12 LAST",                      TestName = "A BDAT with a signed size closes the connection with 421")]
        [TestCase("BDAT -12 LAST",                      TestName = "A BDAT with a negative size closes the connection with 421")]
        [TestCase("BDAT LAST 12",                       TestName = "A BDAT with LAST where the size goes closes the connection with 421")]
        [TestCase("BDAT 12x LAST",                      TestName = "A BDAT with a size that is not digits closes the connection with 421")]
        [TestCase("BDAT 99999999999999999999 LAST",     TestName = "A BDAT with a size of 20 digits closes the connection with 421")]
        [TestCase("BDAT",                               TestName = "A BDAT without a size closes the connection with 421")]
        public async Task A_BDAT_whose_size_cannot_be_read_closes_the_connection(String Command)
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);

            await wire.SendAsync(RawBdat(Command, TwoNoops));

            // 421 and the connection closed: the drain ends at the close, not at its window.
            Assert.That(await wire.DrainAsync(TimeSpan.FromSeconds(3)), Is.EqualTo(new[] { 421 }), "one 421, nothing to the chunk's NOOPs");
            Assert.That(server.Storage.Messages,                       Is.Empty);

        }


        [TestCase("bdat {0} last",    TestName = "A BDAT in lower case is taken")]
        [TestCase("BDAT 00{0} LAST",  TestName = "A BDAT size with leading zeros is taken")]
        [TestCase("BDAT {0}  LAST",   TestName = "A BDAT with two spaces before LAST is taken")]
        public async Task A_BDAT_the_grammar_allows_is_taken(String Format)
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);

            var message = "Subject: chunked\r\n\r\nbody\r\n";
            await wire.SendAsync(RawBdat(String.Format(Format, message.Length), message));

            Assert.That((await wire.ReplyAsync()).Code,  Is.EqualTo(250));
            Assert.That(server.Storage.Messages.Single(), Does.EndWith("\r\n\r\nbody\r\n"));

        }


        [Test]
        public async Task A_RCPT_after_a_chunk_is_503_and_names_no_recipient()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);

            await wire.SendAsync(Bdat("Subject: chunked\r\n\r\nfirst ", Last: false));
            Assert.That((await wire.ReplyAsync()).Code,                     Is.EqualTo(250));

            Assert.That(await wire.CommandAsync("RCPT TO:<bob@hermod.test>"), Is.EqualTo(503));

            // The transaction goes on as it was: the message ends with BDAT LAST, for alice alone.
            await wire.SendAsync(Bdat("second\r\n", Last: true));
            Assert.That((await wire.ReplyAsync()).Code,                     Is.EqualTo(250));

            Assert.That(server.Storage.Messages.Single(),                    Does.EndWith("\r\n\r\nfirst second\r\n"));
            Assert.That(server.Storage.Envelopes.Single().To,                Is.EqualTo(new[] { "alice@hermod.test" }));

        }


        [Test]
        public async Task A_RCPT_in_the_next_transaction_is_taken()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);

            // A chunk, then RSET: the next transaction names its recipients as any does.
            await wire.SendAsync(Bdat("Subject: chunked\r\n\r\nfirst ", Last: false));
            Assert.That((await wire.ReplyAsync()).Code,                       Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("RSET"),                      Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("RCPT TO:<bob@hermod.test>"), Is.EqualTo(250));

            // A message ended with BDAT LAST, then the next one.
            await wire.SendAsync(Bdat("Subject: chunked\r\n\r\nbody\r\n", Last: true));
            Assert.That((await wire.ReplyAsync()).Code,                       Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("RCPT TO:<alice@hermod.test>"), Is.EqualTo(250));

        }

    }

}
