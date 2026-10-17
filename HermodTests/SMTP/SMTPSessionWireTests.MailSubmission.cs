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
using System.Security.Cryptography.X509Certificates;
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.SMTP;
using org.GraphDefined.Vanaheimr.Hermod.SMTP.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// Message submission (RFC 6409) through an application: AUTH where the configuration
    /// offers it - the submission port, the mechanisms named - PLAIN checking the password
    /// of who signs in, and a signed-in client's message handed over whole, with its
    /// recipients here and elsewhere; refused where the application says so.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        /// <summary>
        /// Accounts and their passwords - no SCRAM keys, no certificates.
        /// </summary>
        private sealed class Passwords(params (String User, String Password)[] Accounts) : IUserStore
        {

            public Task<UserCredentials?> GetUserAsync(String username, CancellationToken ct = default)
                => Task.FromResult<UserCredentials?>(null);

            public Task<UserCredentials?> GetUserByCertificateAsync(X509Certificate2 cert, CancellationToken ct = default)
                => Task.FromResult<UserCredentials?>(null);

            public Task<Boolean> ValidatePasswordAsync(String username, String password, CancellationToken ct = default)
                => Task.FromResult(Accounts.Any(account => account.User == username && account.Password == password));

        }

        /// <summary>
        /// An application that takes what alice sends from her own address, refuses the rest,
        /// and remembers what it was handed.
        /// </summary>
        private sealed class Application : IMailSubmission
        {

            public readonly ConcurrentQueue<(String User, String From, IReadOnlyList<String> To, String Message)> Submitted = new();

            public Exception?     Failure;
            public SMTPRefusal?   Refusal;

            public Task<SMTPRefusal?> CheckSenderAsync(String User, String EnvelopeFrom, CancellationToken CancellationToken = default)

                => Task.FromResult(EnvelopeFrom.Equals($"{User}@hermod.test", StringComparison.OrdinalIgnoreCase)
                                       ? null
                                       : new SMTPRefusal(550, $"5.7.1 <{EnvelopeFrom}> is not yours"));

            public Task<SMTPRefusal?> SubmitAsync(EMailMessage Message, String User, String EnvelopeFrom, IReadOnlyList<String> EnvelopeTo, CancellationToken CancellationToken = default)
            {

                if (Failure is not null)
                    throw Failure;

                Submitted.Enqueue((User, EnvelopeFrom, EnvelopeTo, Message.RawMessage));
                return Task.FromResult(Refusal);

            }

        }

        private static Func<SMTPServerConfig, SMTPServerConfig> SubmissionOnly
            => config => WithServerCertificate(config) with { AuthOnMtaPort = false, AuthMechanisms = [ "PLAIN", "LOGIN" ] };

        private static String Plain(String Authzid, String Authcid, String Password)
            => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Authzid}\0{Authcid}\0{Password}"));

        /// <summary>
        /// A connection to the given port, inside TLS, after EHLO: the EHLO keywords.
        /// </summary>
        private static async Task<(Wire Wire, List<String> Keywords)> SecureAsync(Server Server, UInt16 Port)
        {

            var wire = await Server.ConnectAsync(Port);

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("STARTTLS"),            Is.EqualTo(220));
            await wire.StartTlsAsync();

            var (code, keywords) = await wire.CommandLinesAsync("EHLO client.example");
            Assert.That(code, Is.EqualTo(250));

            return (wire, keywords);

        }


        [Test]
        public async Task AUTH_is_offered_where_mail_programs_send_with_the_mechanisms_named()
        {

            await using var server = new Server(Configure: SubmissionOnly, UserStore: new Passwords(("alice", "secret")));

            var (mta, mtaKeywords)               = await SecureAsync(server, server.Port);
            var authOnMta                        = await mta.CommandLinesAsync("AUTH PLAIN " + Plain("", "alice", "secret"));

            var (submission, submissionKeywords) = await SecureAsync(server, server.SubmissionPort);
            var scram                            = await submission.CommandLinesAsync("AUTH SCRAM-SHA-256");

            Assert.Multiple(() => {

                Assert.That(mtaKeywords.Any(keyword => keyword.StartsWith("AUTH")),  Is.False, "other servers deliver on 25 - nobody signs in there");
                Assert.That(authOnMta.Code,                                          Is.EqualTo(502));
                Assert.That(authOnMta.Lines.Single(),                                Does.StartWith("5.5.1 "));

                Assert.That(submissionKeywords,                                      Does.Contain("AUTH PLAIN LOGIN"), "SCRAM left out: there are no keys for it");
                Assert.That(scram.Code,                                              Is.EqualTo(504));

            });

            mta.Dispose();
            submission.Dispose();

        }


        [Test]
        public async Task PLAIN_checks_the_password_of_who_signs_in_and_lets_nobody_act_as_another()
        {

            await using var server = new Server(Configure: SubmissionOnly, UserStore: new Passwords(("alice", "secret"), ("bob", "other")));

            async Task<Int32> SignIn(String Authzid, String Authcid, String Password)
            {
                var (wire, _) = await SecureAsync(server, server.SubmissionPort);
                using (wire)
                    return await wire.CommandAsync("AUTH PLAIN " + Plain(Authzid, Authcid, Password));
            }

            var plain     = await SignIn("",      "alice", "secret");
            var herself   = await SignIn("alice", "alice", "secret");
            var asBob     = await SignIn("bob",   "alice", "secret");
            var asAlice   = await SignIn("alice", "bob",   "secret");
            var wrong     = await SignIn("",      "alice", "other");

            Assert.Multiple(() => {
                Assert.That(plain,    Is.EqualTo(235));
                Assert.That(herself,  Is.EqualTo(235), "naming oneself is acting as oneself");
                Assert.That(asBob,    Is.EqualTo(535), "alice's password does not make her bob");
                Assert.That(asAlice,  Is.EqualTo(535), "nor bob alice - with her password, as it was");
                Assert.That(wrong,    Is.EqualTo(535));
            });

        }


        [Test]
        public async Task A_signed_in_clients_message_is_handed_to_the_application_whole()
        {

            var application = new Application();

            await using var server = new Server(Configure: SubmissionOnly, UserStore: new Passwords(("alice", "secret")), Submission: application);

            var (wire, _) = await SecureAsync(server, server.SubmissionPort);

            using (wire)
            {

                Assert.That(await wire.CommandAsync("AUTH PLAIN " + Plain("", "alice", "secret")), Is.EqualTo(235));

                var notHers = await wire.CommandLinesAsync("MAIL FROM:<boss@hermod.test>");

                Assert.That(await wire.CommandAsync("MAIL FROM:<alice@hermod.test>"),         Is.EqualTo(250));
                Assert.That(await wire.CommandAsync("RCPT TO:<bob@hermod.test>"),             Is.EqualTo(250));
                Assert.That(await wire.CommandAsync("RCPT TO:<carol@elsewhere.example>"),     Is.EqualTo(250), "relayed - to the application, not to a queue");
                Assert.That(await wire.CommandAsync("DATA"),                                  Is.EqualTo(354));
                await wire.SendAsync("From: alice@hermod.test\r\nSubject: submitted\r\n\r\nbody\r\n.\r\n");

                var taken = await wire.ReplyAsync();

                // Refused by the application at the end of data, and failing there.
                application.Refusal = new SMTPRefusal(554, "5.7.1 not today");
                var refused = await SendOneAsync(wire);

                application.Refusal = null;
                application.Failure = new IOException("disk full");
                var failed  = await SendOneAsync(wire);

                Assert.Multiple(() => {

                    Assert.That(notHers.Code,                  Is.EqualTo(550));
                    Assert.That(notHers.Lines.Single(),        Does.StartWith("5.7.1 ").And.Contain("boss@hermod.test"));

                    Assert.That(taken.Code,                    Is.EqualTo(250));
                    var (user, from, to, message) = application.Submitted.First();
                    Assert.That(user,                          Is.EqualTo("alice"));
                    Assert.That(from,                          Is.EqualTo("alice@hermod.test"));
                    Assert.That(to,                            Is.EqualTo(new[] { "bob@hermod.test", "carol@elsewhere.example" }));
                    Assert.That(message,                       Does.StartWith("Received: ").And.Contain("Subject: submitted"));
                    Assert.That(server.Storage.Messages,       Is.Empty, "neither stored by the server itself");

                    Assert.That(refused,                       Is.EqualTo((554, "5.7.1 not today")));
                    Assert.That(failed.Code,                   Is.EqualTo(451));

                });

            }

        }

        // One more message on a signed-in connection: the reply to its end of data.
        private static async Task<(Int32 Code, String Text)> SendOneAsync(Wire Wire)
        {
            Assert.That(await Wire.CommandAsync("MAIL FROM:<alice@hermod.test>"), Is.EqualTo(250));
            Assert.That(await Wire.CommandAsync("RCPT TO:<bob@hermod.test>"),     Is.EqualTo(250));
            Assert.That(await Wire.CommandAsync("DATA"),                          Is.EqualTo(354));
            await Wire.SendAsync("From: alice@hermod.test\r\nSubject: again\r\n\r\nbody\r\n.\r\n");
            return await Wire.ReplyAsync();
        }


        [Test]
        public async Task A_relayed_recipient_with_nowhere_to_go_is_refused_at_RCPT()
        {

            // Signed in, but neither a queue nor an application: the message would have been dropped.
            await using var server = new Server(Configure: SubmissionOnly, UserStore: new Passwords(("alice", "secret")));

            var (wire, _) = await SecureAsync(server, server.SubmissionPort);

            using (wire)
            {

                Assert.That(await wire.CommandAsync("AUTH PLAIN " + Plain("", "alice", "secret")), Is.EqualTo(235));
                Assert.That(await wire.CommandAsync("MAIL FROM:<alice@hermod.test>"),             Is.EqualTo(250));

                var relayed = await wire.CommandLinesAsync("RCPT TO:<carol@elsewhere.example>");

                Assert.That(relayed.Code,          Is.EqualTo(550));
                Assert.That(relayed.Lines.Single(), Does.StartWith("5.7.1 "));

            }

        }

    }

}
