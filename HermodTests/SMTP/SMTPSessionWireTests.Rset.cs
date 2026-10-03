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

using System.Security.Cryptography;
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.SMTP;
using org.GraphDefined.Vanaheimr.Hermod.SMTP.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// RSET aborts the mail transaction and nothing else (RFC 5321 §4.1.1.5): a completed
    /// AUTH lasts for the session (RFC 4954 §4).
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        private const String SubmissionUser      = "alice";
        private const String SubmissionPassword  = "correct horse battery staple";

        /// <summary>
        /// A server whose user store holds one SCRAM-SHA-256 account. The server's default
        /// user store is users.txt in its mail storage directory, and Configure runs before
        /// the server and its store are built.
        /// </summary>
        private static Server ServerWithSubmissionUser()

            => new (Configure: config => {

                        var credentials = ScramCredentialGenerator.Generate(SubmissionPassword);

                        // username:password_sha256:scram_salt:scram_stored_key:scram_server_key:iterations:cert_thumbprints
                        File.WriteAllText(Path.Combine(config.MailStoragePath, "users.txt"),
                                          $"{SubmissionUser}::{credentials.SaltBase64}:{credentials.StoredKeyBase64}:{credentials.ServerKeyBase64}:{credentials.Iterations}:\n");

                        return config;

                    });

        /// <summary>
        /// AUTH SCRAM-SHA-256 (RFC 7677) in cleartext - the one mechanism offered without TLS.
        /// </summary>
        private static async Task AuthenticateWithScram(Wire wire)
        {

            var clientNonce      = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
            var clientFirstBare  = $"n={SubmissionUser},r={clientNonce}";

            await wire.SendAsync($"AUTH SCRAM-SHA-256 {Base64("n,," + clientFirstBare)}\r\n");
            var serverFirst      = await wire.ReplyAsync();
            Assert.That(serverFirst.Code, Is.EqualTo(334));

            var serverFirstText  = Encoding.UTF8.GetString(Convert.FromBase64String(serverFirst.Text));
            var attributes       = serverFirstText.Split(',').ToDictionary(attribute => attribute[..1], attribute => attribute[2..]);

            var clientFinalBare  = $"c={Base64("n,,")},r={attributes["r"]}";
            var authMessage      = $"{clientFirstBare},{serverFirstText},{clientFinalBare}";
            var saltedPassword   = ScramSha256Client.SaltedPassword(SubmissionPassword,
                                                                    Convert.FromBase64String(attributes["s"]),
                                                                    Int32.Parse(attributes["i"]));
            var proof            = ScramSha256Client.ClientProof(saltedPassword, authMessage);

            await wire.SendAsync($"{Base64($"{clientFinalBare},p={Convert.ToBase64String(proof)}")}\r\n");
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(235));

        }

        private static String Base64(String text)
            => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));


        [Test]
        public async Task RSET_keeps_a_completed_AUTH_on_the_submission_port()
        {

            await using var server = ServerWithSubmissionUser();
            using var wire         = await server.ConnectAsync(server.SubmissionPort);

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));
            await AuthenticateWithScram(wire);

            // A transaction that is abandoned: RSET.
            Assert.That(await wire.CommandAsync("MAIL FROM:<alice@hermod.test>"),   Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("RCPT TO:<bob@hermod.test>"),       Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("RSET"),                            Is.EqualTo(250));

            // Relaying needs the AUTH, so a remote recipient shows that it outlived RSET.
            Assert.That(await wire.CommandAsync("MAIL FROM:<alice@hermod.test>"),   Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("RCPT TO:<carol@remote.example>"),  Is.EqualTo(250), "a relay RCPT is still authenticated");
            Assert.That(await wire.CommandAsync("RSET"),                            Is.EqualTo(250));

            // And so does a message on the submission port, which needs it as well.
            Assert.That(await wire.CommandAsync("MAIL FROM:<alice@hermod.test>"),   Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("RCPT TO:<bob@hermod.test>"),       Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("DATA"),                            Is.EqualTo(354), "DATA is still authenticated");

            await wire.SendAsync("Subject: after RSET\r\n\r\nbody\r\n.\r\n");

            Assert.That((await wire.ReplyAsync()).Code,                             Is.EqualTo(250));
            Assert.That(server.Storage.Envelopes.Single().To,                       Is.EqualTo(new[] { "bob@hermod.test" }),
                        "only the last transaction is delivered");

            // Still one AUTH per session (RFC 4954 §4): RSET did not make room for another.
            Assert.That(await wire.CommandAsync("AUTH SCRAM-SHA-256"),              Is.EqualTo(503));

        }

    }

}
