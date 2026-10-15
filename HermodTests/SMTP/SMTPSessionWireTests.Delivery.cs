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

using System.Collections.Concurrent;
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.SMTP;
using org.GraphDefined.Vanaheimr.Hermod.SMTP.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// What the server hands to the mail store, and what it answers when the store or the
    /// recipients cannot take a message: the octets as they arrived, 550 5.1.1 at RCPT for a
    /// local mailbox that does not exist, and 451 for a store that fails.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        #region Test doubles

        /// <summary>
        /// The mailboxes of hermod.test: a list of addresses that exist, one that is busy and
        /// one whose directory is unreachable. Remembers what it was asked about.
        /// </summary>
        private sealed class Mailboxes(params String[] Known) : IRecipientValidator
        {

            public readonly ConcurrentQueue<String> Asked = new();

            public Task<RecipientCheck> CheckAsync(String recipient, CancellationToken ct = default)
            {

                Asked.Enqueue(recipient);

                if (recipient.StartsWith("broken@", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The directory is unreachable.");

                if (recipient.StartsWith("busy@", StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(RecipientCheck.TryLater);

                return Task.FromResult(Known.Contains(recipient, StringComparer.OrdinalIgnoreCase)
                                           ? RecipientCheck.Accept
                                           : RecipientCheck.Unknown);

            }

        }

        /// <summary>
        /// A message whose header and body are ISO-8859-1, sent as 8-bit: "Grüße" in the subject
        /// and "Schöne Grüße" in the body, as single octets 0xFC, 0xDF and 0xF6 that are not UTF-8.
        /// </summary>
        private static Byte[] Latin1Message()
            => Encoding.Latin1.GetBytes("From: sender@client.example\r\n" +
                                        "To: alice@hermod.test\r\n" +
                                        "Subject: Grüße\r\n" +
                                        "Content-Type: text/plain; charset=iso-8859-1\r\n" +
                                        "Content-Transfer-Encoding: 8bit\r\n" +
                                        "\r\n" +
                                        "Schöne Grüße\r\n");

        private static Byte[] Concat(params Byte[][] Parts)
            => [.. Parts.SelectMany(part => part)];

        #endregion


        #region The octets as they arrived

        [Test]
        public async Task An_8bit_message_that_is_not_UTF8_is_stored_as_the_octets_that_arrived_with_DATA()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);
            Assert.That(await wire.CommandAsync("DATA"), Is.EqualTo(354));

            var message = Latin1Message();

            await wire.SendAsync(Concat(message, ".\r\n"u8.ToArray()));
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250));

            var stored = server.Storage.RawBytes.Single();

            Assert.That(stored, Is.Not.Null);

            // The trace headers of this hop in front, and then every octet as it was sent: the
            // 0xFC of "Grüße" is still 0xFC, not the three octets of U+FFFD.
            Assert.That(stored!.AsSpan().EndsWith(message), Is.True, "the message after the trace headers is the message that arrived");
            Assert.That(Encoding.ASCII.GetString(stored, 0, stored.Length - message.Length), Does.Contain("Received: ").And.EndWith("\r\n"));

        }

        [Test]
        public async Task An_8bit_message_that_is_not_UTF8_is_stored_as_the_octets_that_arrived_with_BDAT()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);

            var message = Latin1Message();

            await wire.SendAsync(Concat(Encoding.ASCII.GetBytes($"BDAT {message.Length} LAST\r\n"), message));
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250));

            Assert.That(server.Storage.RawBytes.Single()!.AsSpan().EndsWith(message), Is.True);

        }

        [Test]
        public async Task The_file_store_writes_the_octets_that_arrived()
        {

            var directory = Directory.CreateTempSubdirectory("hermod-filestore-").FullName;

            try
            {

                var message = Latin1Message();
                var path    = await new FileMailStorage(directory, new QuietLogger()).
                                        StoreAsync(EMailMessage.Parse(message), "sender@client.example", [ "alice@hermod.test" ]);

                var written = await File.ReadAllBytesAsync(path);

                Assert.Multiple(() =>
                {
                    Assert.That(written.AsSpan().EndsWith(message),                          Is.True, "after the X-Envelope headers, the message as it arrived");
                    Assert.That(Encoding.UTF8.GetString(written),                            Does.StartWith("X-Envelope-From: sender@client.example\r\n"));
                    Assert.That(written.AsSpan().IndexOf(Encoding.UTF8.GetBytes("�")), Is.EqualTo(-1), "no octet was replaced");
                });

            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }

        }

        #endregion

        #region Recipients that do not exist

        [Test]
        public async Task An_unknown_local_recipient_is_refused_at_RCPT_and_the_known_one_gets_the_message()
        {

            var mailboxes          = new Mailboxes("alice@hermod.test");

            await using var server = new Server(RecipientValidator: mailboxes);
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"),               Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"), Is.EqualTo(250));

            await wire.SendAsync("RCPT TO:<nobody@hermod.test>\r\n");
            var refused = await wire.ReplyAsync();

            Assert.That(refused.Code, Is.EqualTo(550));
            Assert.That(refused.Text, Does.StartWith("5.1.1 "), "RFC 3463: bad destination mailbox address");

            Assert.That(await wire.CommandAsync("RCPT TO:<ALICE@hermod.test>"),       Is.EqualTo(250), "the check is the validator's, case and all");
            Assert.That(await wire.CommandAsync("DATA"),                              Is.EqualTo(354));

            await wire.SendAsync("Subject: hello\r\n\r\nhi\r\n.\r\n");
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250));

            Assert.That(server.Storage.Envelopes.Single().To, Is.EqualTo(new[] { "ALICE@hermod.test" }), "only the accepted recipient is delivered to");

        }

        [TestCase("postmaster")]
        [TestCase("Postmaster")]
        [TestCase("postmaster@hermod.test")]
        [TestCase("POSTMASTER@hermod.test")]
        public async Task Postmaster_is_accepted_without_asking(String Recipient)
        {

            // Nobody exists - and postmaster still does (RFC 5321 §4.5.1).
            var mailboxes          = new Mailboxes();

            await using var server = new Server(RecipientValidator: mailboxes);
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"),               Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync($"RCPT TO:<{Recipient}>"),            Is.EqualTo(250));

            Assert.That(mailboxes.Asked, Is.Empty);

        }

        [Test]
        public async Task A_relayed_recipient_is_none_of_the_validators_business()
        {

            var mailboxes          = new Mailboxes();

            await using var server = new Server(RecipientValidator: mailboxes);
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"),               Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"), Is.EqualTo(250));

            await wire.SendAsync("RCPT TO:<someone@elsewhere.example>\r\n");
            var refused = await wire.ReplyAsync();

            // Refused by the relay rules, as before, and not as an unknown mailbox.
            Assert.That(refused.Code,     Is.EqualTo(550));
            Assert.That(refused.Text,     Does.StartWith("5.7.1 "));
            Assert.That(mailboxes.Asked,  Is.Empty);

        }

        [Test]
        public async Task A_domain_added_while_the_server_runs_is_local_from_the_next_RCPT_on()
        {

            var domains            = new ConcurrentDictionary<String, Byte>(StringComparer.OrdinalIgnoreCase);
            domains["hermod.test"] = 0;

            await using var server = new Server(Configure: config => config with {
                                                    LocalDomainCheck = domain => domains.ContainsKey(domain)
                                                });
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"),               Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("RCPT TO:<bob@new.example>"),         Is.EqualTo(550), "not ours yet: a relay, and refused");

            domains["new.example"] = 0;

            Assert.That(await wire.CommandAsync("RCPT TO:<bob@NEW.example>"),         Is.EqualTo(250), "ours now, in the same session");

        }

        [TestCase("busy@hermod.test")]
        [TestCase("broken@hermod.test")]
        public async Task A_recipient_that_cannot_be_checked_now_is_deferred(String Recipient)
        {

            await using var server = new Server(RecipientValidator: new Mailboxes());
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"),               Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"), Is.EqualTo(250));

            await wire.SendAsync($"RCPT TO:<{Recipient}>\r\n");
            var deferred = await wire.ReplyAsync();

            Assert.That(deferred.Code, Is.EqualTo(451));
            Assert.That(deferred.Text, Does.StartWith("4.3.0 "));

            // The session goes on.
            Assert.That(await wire.CommandAsync("NOOP"), Is.EqualTo(250));

        }

        #endregion

        #region A store that fails

        [Test]
        public async Task A_store_that_fails_is_a_451_and_the_session_goes_on()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            server.Storage.Failure = new IOException("There is not enough space on the disk.");

            await OpenTransaction(wire);
            Assert.That(await wire.CommandAsync("DATA"), Is.EqualTo(354));

            await wire.SendAsync("Subject: first try\r\n\r\nhi\r\n.\r\n");
            var failed = await wire.ReplyAsync();

            // A temporary failure the client can act on - it tries again later - and not a
            // connection that just goes away.
            Assert.That(failed.Code, Is.EqualTo(451));
            Assert.That(failed.Text, Does.StartWith("4.3.0 "));
            Assert.That(server.Storage.Messages, Is.Empty);

            // The same connection, the next transaction: the store works again.
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"), Is.EqualTo(250), "the failed transaction was reset");
            Assert.That(await wire.CommandAsync("RCPT TO:<alice@hermod.test>"),       Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("DATA"),                              Is.EqualTo(354));

            await wire.SendAsync("Subject: second try\r\n\r\nhi\r\n.\r\n");
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250));

            Assert.That(server.Storage.Messages.Single(), Does.Contain("Subject: second try"));

        }

        #endregion

    }

}
