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
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Hermod.SMTP.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// The AUTH exchange in its details (RFC 4954 §4, §6): how long a response may be - RFC 4616
    /// §2 has PLAIN take 255 octets of authzid, authcid and passwd each -, the base64 it is
    /// written in, the end of an exchange that got a line too long, and a user store that
    /// cannot be asked.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        private static readonly String LongIdentity  = new String('u', 255 - "@hermod.test".Length) + "@hermod.test";
        private static readonly String LongPassword  = String.Concat(Enumerable.Range(0, 255).Select(i => (Char) ('a' + i % 26)));

        /// <summary>
        /// A user store whose database cannot be reached.
        /// </summary>
        private sealed class Unreachable : IUserStore
        {

            private static Exception Down
                => new IOException("The user database cannot be reached.");

            public Task<UserCredentials?> GetUserAsync(String username, CancellationToken ct = default)
                => Task.FromException<UserCredentials?>(Down);

            public Task<UserCredentials?> GetUserByCertificateAsync(X509Certificate2 cert, CancellationToken ct = default)
                => Task.FromException<UserCredentials?>(Down);

            public Task<Boolean> ValidatePasswordAsync(String username, String password, CancellationToken ct = default)
                => Task.FromException<Boolean>(Down);

        }

        /// <summary>
        /// EHLO, STARTTLS, EHLO: ready for AUTH.
        /// </summary>
        private static async Task<Wire> ReadyForAuth(Server Server)
        {

            var wire = await Server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("STARTTLS"),            Is.EqualTo(220));
            await wire.StartTlsAsync();
            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));

            return wire;

        }


        #region The length of a response (RFC 4954 §4, RFC 4616 §2)

        [Test]
        public async Task The_longest_PLAIN_initial_response_signs_in()
        {

            await using var server = new Server(Configure: WithServerCertificate, UserStore: new Passwords((LongIdentity, LongPassword)));
            using var wire         = await ReadyForAuth(server);

            var response = Base64($"{LongIdentity}\0{LongIdentity}\0{LongPassword}");
            Assert.That(response, Has.Length.EqualTo(1024));

            Assert.That(await wire.CommandAsync("AUTH PLAIN " + response), Is.EqualTo(235));

        }


        [Test]
        public async Task The_longest_PLAIN_response_after_the_challenge_signs_in()
        {

            await using var server = new Server(Configure: WithServerCertificate, UserStore: new Passwords((LongIdentity, LongPassword)));
            using var wire         = await ReadyForAuth(server);

            Assert.That(await wire.CommandAsync("AUTH PLAIN"),                                                    Is.EqualTo(334));
            Assert.That(await wire.CommandAsync(Base64($"{LongIdentity}\0{LongIdentity}\0{LongPassword}")),       Is.EqualTo(235));

        }


        [Test]
        public async Task A_command_line_is_still_held_to_its_own_limit()
        {

            await using var server = new Server(Configure: WithServerCertificate);
            using var wire         = await ReadyForAuth(server);

            // Longer than the 1024 octets of a command line, shorter than an AUTH line may be.
            await wire.SendAsync("NOOP " + new String('x', 2000) + "\r\n");
            var reply = await wire.ReplyAsync();

            Assert.That(reply.Code, Is.EqualTo(500));
            Assert.That(reply.Text, Does.StartWith("5.5.6 "));
            Assert.That(await wire.CommandAsync("NOOP"), Is.EqualTo(250));

        }


        [Test]
        public async Task A_line_too_long_in_the_exchange_ends_it()
        {

            await using var server = new Server(Configure: WithServerCertificate);
            using var wire         = await ReadyForAuth(server);

            Assert.That(await wire.CommandAsync("AUTH PLAIN"), Is.EqualTo(334));

            await wire.SendAsync(new String('A', 20_000) + "\r\n");
            var reply = await wire.ReplyAsync();

            Assert.That(reply.Code, Is.EqualTo(500));
            Assert.That(reply.Text, Does.StartWith("5.5.6 "));

            // The AUTH command failed: NOOP is a command, not a response read as base64.
            Assert.That(await wire.CommandAsync("NOOP"),                                         Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("AUTH PLAIN " + Base64("\0nobody\0nothing")),    Is.EqualTo(535), "and AUTH may be tried again");

        }

        #endregion


        #region Base64 and nothing else (RFC 4954 §4)

        public static IEnumerable<TestCaseData> ResponsesThatAreNoBase64()
        {

            var response = Base64("\0nobody\0nothing");

            TestCaseData Case(String Name, params String[] Lines)
                => new TestCaseData((Object) Lines).SetName($"Not base64 is 501 5.5.2: {Name}");

            yield return Case("a space inside the initial response",  "AUTH PLAIN " + response.Insert(4, " "));
            yield return Case("a space inside the response",          "AUTH PLAIN", response.Insert(4, " "));
            yield return Case("a tab inside the response",            "AUTH PLAIN", response.Insert(4, "\t"));
            yield return Case("a space after the response",           "AUTH PLAIN", response + " ");
            yield return Case("a space inside the LOGIN user name",   "AUTH LOGIN", Base64("nobody").Insert(4, " "));
            yield return Case("a pad character inside the response",  "AUTH PLAIN", response.Insert(4, "="));
            yield return Case("the response without its padding",     "AUTH PLAIN", Base64("\0nobody\0nothings").TrimEnd('='));

        }


        [TestCaseSource(nameof(ResponsesThatAreNoBase64))]
        public async Task A_response_that_is_no_base64_is_refused(String[] Lines)
        {

            await using var server = new Server(Configure: WithServerCertificate);
            using var wire         = await ReadyForAuth(server);

            foreach (var line in Lines[..^1])
                Assert.That(await wire.CommandAsync(line), Is.EqualTo(334), $"\"{line}\" continues the exchange");

            await wire.SendAsync(Lines[^1] + "\r\n");
            var reply = await wire.ReplyAsync();

            Assert.That(reply.Code, Is.EqualTo(501));
            Assert.That(reply.Text, Does.StartWith("5.5.2 "), $"reply text was \"{reply.Text}\"");
            Assert.That(await wire.CommandAsync("NOOP"), Is.EqualTo(250));

        }


        [Test]
        public async Task Base64_as_it_should_be_signs_in()
        {

            await using var server = new Server(Configure: WithServerCertificate, UserStore: new Passwords(("alice", "secret")));
            using var wire         = await ReadyForAuth(server);

            Assert.That(await wire.CommandAsync("AUTH PLAIN"),                   Is.EqualTo(334));
            Assert.That(await wire.CommandAsync(Base64("\0alice\0secret")),      Is.EqualTo(235));

        }

        #endregion


        #region A user store that cannot be asked (RFC 4954 §6)

        public static IEnumerable<TestCaseData> AuthWithAnUnreachableStore()
        {

            TestCaseData Case(String Name, params String[] Lines)
                => new TestCaseData((Object) Lines).SetName($"A user store that is down is 454 4.7.0: {Name}");

            yield return Case("PLAIN",  "AUTH PLAIN " + Base64("\0alice\0secret"));
            yield return Case("LOGIN",  "AUTH LOGIN", Base64("alice"), Base64("secret"));
            yield return Case("SCRAM-SHA-256", "AUTH SCRAM-SHA-256 " + Base64("n,,n=alice,r=fyko+d2lbbFgONRv9qkxdawL"));

        }


        [TestCaseSource(nameof(AuthWithAnUnreachableStore))]
        public async Task A_user_store_that_is_down_is_a_temporary_failure(String[] Lines)
        {

            await using var server = new Server(Configure: WithServerCertificate, UserStore: new Unreachable());
            using var wire         = await ReadyForAuth(server);

            foreach (var line in Lines[..^1])
                Assert.That(await wire.CommandAsync(line), Is.EqualTo(334), $"\"{line}\" continues the exchange");

            await wire.SendAsync(Lines[^1] + "\r\n");
            var reply = await wire.ReplyAsync();

            Assert.That(reply.Code, Is.EqualTo(454));
            Assert.That(reply.Text, Does.StartWith("4.7.0 "), $"reply text was \"{reply.Text}\"");

            // The exchange is over; AUTH may be tried again.
            Assert.That(await wire.CommandAsync("NOOP"),        Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("AUTH PLAIN"),  Is.EqualTo(334));

        }

        #endregion

    }

}
