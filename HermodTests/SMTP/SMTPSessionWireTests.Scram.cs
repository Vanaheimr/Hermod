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
    /// SCRAM-SHA-256 on the server (RFC 5802, RFC 7677) in its details: the server-final-message
    /// as a challenge, the GS2 header - the channel binding flag, c= and an authorization
    /// identity -, escaped user names, mandatory extensions, and SASLprep. The findings S-20
    /// to S-26 of SMTPConformanceTests.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        #region Setup

        private const String ScramPassword = "correct horse battery staple";

        /// <summary>
        /// A server whose user store holds the given SCRAM-SHA-256 accounts.
        /// </summary>
        private static Server ServerWithScramUsers(params (String Name, String Password)[] Users)

            => new (Configure: config => {

                        // username:password_sha256:scram_salt:scram_stored_key:scram_server_key:iterations:cert_thumbprints
                        File.WriteAllLines(Path.Combine(config.MailStoragePath, "users.txt"),
                                           Users.Select(user => (user.Name, Credentials: ScramCredentialGenerator.Generate(user.Password))).
                                                 Select(user => $"{user.Name}::{user.Credentials.SaltBase64}:{user.Credentials.StoredKeyBase64}:" +
                                                                $"{user.Credentials.ServerKeyBase64}:{user.Credentials.Iterations}:"));

                        return config;

                    });

        /// <summary>
        /// One SCRAM-SHA-256 exchange, with what a test changes in it: the GS2 header, the name
        /// as sent, an attribute before it, the c= value, an extension of the client-final-message,
        /// the response to the server-final-message. The password is hashed as given - not
        /// normalized. The reply code that ended it: 235 after the server-final-message - whose
        /// signature is checked - or the refusal before.
        /// </summary>
        private static async Task<Int32> ScramExchange(Wire     wire,
                                                       String   Password,
                                                       String   Name            = "alice",
                                                       String   Gs2Header       = "n,,",
                                                       String   Prefix          = "",
                                                       String?  ChannelBinding  = null,
                                                       String   FinalExtension  = "",
                                                       String   EmptyResponse   = "")
        {

            var clientFirstBare  = $"{Prefix}n={Name},r={Convert.ToBase64String(RandomNumberGenerator.GetBytes(18))}";

            await wire.SendAsync($"AUTH SCRAM-SHA-256 {Base64(Gs2Header + clientFirstBare)}\r\n");
            var serverFirst      = await wire.ReplyAsync();
            if (serverFirst.Code != 334)
                return serverFirst.Code;

            var serverFirstText  = Encoding.UTF8.GetString(Convert.FromBase64String(serverFirst.Text));
            var attributes       = serverFirstText.Split(',').ToDictionary(attribute => attribute[..1], attribute => attribute[2..]);

            var clientFinalBare  = $"c={ChannelBinding ?? Base64(Gs2Header)},r={attributes["r"]}{FinalExtension}";
            var authMessage      = $"{clientFirstBare},{serverFirstText},{clientFinalBare}";
            var saltedPassword   = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(Password),
                                                             Convert.FromBase64String(attributes["s"]),
                                                             Int32.Parse(attributes["i"]),
                                                             HashAlgorithmName.SHA256,
                                                             32);
            var clientKey        = HMACSHA256.HashData(saltedPassword, "Client Key"u8);
            var clientSignature  = HMACSHA256.HashData(SHA256.HashData(clientKey), Encoding.UTF8.GetBytes(authMessage));
            var proof            = clientKey.Zip(clientSignature, (a, b) => (Byte) (a ^ b)).ToArray();

            await wire.SendAsync($"{Base64($"{clientFinalBare},p={Convert.ToBase64String(proof)}")}\r\n");

            var serverFinal      = await wire.ReplyAsync();
            if (serverFinal.Code != 334)
                return serverFinal.Code;

            var serverKey        = HMACSHA256.HashData(saltedPassword, "Server Key"u8);
            Assert.That(Encoding.UTF8.GetString(Convert.FromBase64String(serverFinal.Text)),
                        Is.EqualTo("v=" + Convert.ToBase64String(HMACSHA256.HashData(serverKey, Encoding.UTF8.GetBytes(authMessage)))),
                        "the server-final-message carries the ServerSignature");

            await wire.SendAsync($"{EmptyResponse}\r\n");

            return (await wire.ReplyAsync()).Code;

        }

        private static async Task<Int32> ScramWith(String Password, String Name = "alice", String Gs2Header = "n,,", String Prefix = "",
                                                   String? ChannelBinding = null, String FinalExtension = "", String EmptyResponse = "",
                                                   params (String Name, String Password)[] Users)
        {

            await using var server = ServerWithScramUsers(Users.Length > 0 ? Users : [ ("alice", ScramPassword), ("bob", ScramPassword) ]);
            using var wire         = await server.ConnectAsync(server.SubmissionPort);

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));

            return await ScramExchange(wire, Password, Name, Gs2Header, Prefix, ChannelBinding, FinalExtension, EmptyResponse);

        }

        #endregion


        #region The server-final-message (S-20)

        [TestCase("",  Description = "an empty line")]
        [TestCase("=", Description = "\"=\", the empty response of RFC 4954 §4")]
        [Test(Description = "RFC 4954 §4: \"the SMTP protocol doesn't support the SASL feature of returning additional data with a successful outcome\"; RFC 4422 §3: \"the additional data is sent as a challenge whose response is empty. After receiving this response, the server then indicates the successful outcome\"")]
        public async Task The_server_final_message_is_a_challenge(String EmptyResponse)
        {
            Assert.That(await ScramWith(ScramPassword, EmptyResponse: EmptyResponse), Is.EqualTo(235));
        }


        [Test(Description = "RFC 4422 §3: the challenge's \"response is empty\" - one with data fails the exchange")]
        public async Task A_response_with_data_to_the_server_final_message_fails()
        {
            Assert.That(await ScramWith(ScramPassword, EmptyResponse: Base64("v=nonsense")), Is.EqualTo(535));
        }


        [Test(Description = "RFC 4954 §4: \"*\" cancels the exchange, after the server-final-message too - not authenticated, and AUTH may be tried again")]
        public async Task Cancelling_after_the_server_final_message_leaves_the_client_unauthenticated()
        {

            await using var server = ServerWithScramUsers(("alice", ScramPassword));
            using var wire         = await server.ConnectAsync(server.SubmissionPort);

            Assert.That(await wire.CommandAsync("EHLO client.example"),         Is.EqualTo(250));
            Assert.That(await ScramExchange(wire, ScramPassword, EmptyResponse: "*"), Is.EqualTo(501));
            Assert.That(await ScramExchange(wire, ScramPassword),               Is.EqualTo(235), "a second AUTH is allowed - the first did not complete");

        }

        #endregion

        #region The GS2 header (S-21 to S-23)

        [Test(Description = "RFC 5802 §6: a client that supports channel binding and does not see the -PLUS mechanism sends \"y\"; \"If the flag is set to \"y\" and the server supports channel binding, the server MUST fail authentication\" - this server offers no SCRAM-SHA-256-PLUS")]
        public async Task The_y_flag_is_authenticated()
        {
            Assert.That(await ScramWith(ScramPassword, Gs2Header: "y,,"), Is.EqualTo(235));
        }


        [Test(Description = "RFC 5802 §6: \"p=\" asks for channel binding, which this server does not support")]
        public async Task The_p_flag_is_refused()
        {
            Assert.That(await ScramWith(ScramPassword, Gs2Header: "p=tls-unique,,"), Is.EqualTo(535));
        }


        [Test(Description = "RFC 5802 §6: \"The server MUST always validate the client's \"c=\" field\" - a c= of \"y,,\" after a GS2 header of \"n,,\" is the downgrade it tells apart")]
        public async Task A_c_attribute_that_is_not_the_GS2_header_is_refused()
        {
            Assert.That(await ScramWith(ScramPassword, ChannelBinding: Base64("y,,")), Is.EqualTo(535));
        }


        [Test(Description = "RFC 5802 §6: c= is the GS2 header, with the authorization identity in it")]
        public async Task A_c_attribute_without_the_authorization_identity_is_refused()
        {
            Assert.That(await ScramWith(ScramPassword, Gs2Header: "n,a=alice,", ChannelBinding: Base64("n,,")), Is.EqualTo(535));
        }


        [Test(Description = "RFC 5802 §5.1, a: \"Failed verification results in failed authentication exchange\" - alice may not act as bob")]
        public async Task Asking_to_act_as_another_user_is_refused()
        {
            Assert.That(await ScramWith(ScramPassword, Gs2Header: "n,a=bob,"), Is.EqualTo(535));
        }


        [Test(Description = "RFC 5802 §5.1, a: alice naming herself as the authorization identity is authenticated")]
        public async Task Naming_oneself_as_the_authorization_identity_is_authenticated()
        {
            Assert.That(await ScramWith(ScramPassword, Gs2Header: "n,a=alice,"), Is.EqualTo(235));
        }

        #endregion

        #region User names and extensions (S-24, S-25)

        [TestCase("a=3Db", "a=b")]
        [TestCase("a=2Cb", "a,b")]
        [TestCase("a=2cb", "a,b")]
        [Test(Description = "RFC 5802 §5.1, n: \"The characters ',' or '=' in usernames are sent as '=2C' and '=3D' respectively\"")]
        public async Task A_user_name_with_an_escaped_comma_or_equals_sign_is_authenticated(String Sent, String User)
        {
            Assert.That(await ScramWith(ScramPassword, Name: Sent, Users: [ (User, ScramPassword) ]), Is.EqualTo(235));
        }


        [TestCase("a=b")]
        [TestCase("a=")]
        [TestCase("a=4Fb")]
        [Test(Description = "RFC 5802 §5.1, n: \"If the server receives a username that contains '=' not followed by either '2C' or '3D', then the server MUST fail the authentication\"")]
        public async Task A_user_name_with_an_unescaped_equals_sign_is_refused(String Sent)
        {
            Assert.That(await ScramWith(ScramPassword, Name: Sent, Users: [ ("a=b", ScramPassword) ]), Is.EqualTo(535));
        }


        [Test(Description = "RFC 5802 §5.1, m: \"its presence in a client or a server message MUST cause authentication failure when the attribute is parsed by the other end\" - in the client-first-message")]
        public async Task A_mandatory_extension_in_the_client_first_message_fails()
        {
            Assert.That(await ScramWith(ScramPassword, Prefix: "m=future,"), Is.EqualTo(535));
        }


        [Test(Description = "RFC 5802 §5.1, m: and in the client-final-message")]
        public async Task A_mandatory_extension_in_the_client_final_message_fails()
        {
            Assert.That(await ScramWith(ScramPassword, FinalExtension: ",m=future"), Is.EqualTo(535));
        }


        [Test(Description = "RFC 5802 §7: client-final-message-without-proof = channel-binding \",\" nonce [\",\" extensions] - an extension the server does not know is part of the AuthMessage, and the proof over it holds")]
        public async Task An_optional_extension_in_the_client_final_message_is_in_the_AuthMessage()
        {
            Assert.That(await ScramWith(ScramPassword, FinalExtension: ",x=future"), Is.EqualTo(235));
        }

        #endregion

        #region SASLprep (S-26)

        [Test(Description = "RFC 5802 §2.2: SaltedPassword := Hi(Normalize(password), salt, i), \"Normalize(str): Apply the SASLprep profile [RFC4013] to the \"str\" string\" - a password set as \"A\" and a combining diaeresis is the \"Ä\" a client proves it knows")]
        public async Task A_password_is_normalized_as_SASLprep_has_it()
        {
            Assert.That(await ScramWith("Äpfelbaum", Name: "bjoern", Users: [ ("bjoern", "Äpfelbaum") ]), Is.EqualTo(235));
        }


        [Test(Description = "RFC 4013 §2.1: a non-ASCII space is a SPACE, and a soft hyphen is nothing")]
        public async Task A_password_is_mapped_as_SASLprep_has_it()
        {
            Assert.That(await ScramWith("pass word", Name: "bjoern", Users: [ ("bjoern", "pass wo­rd") ]), Is.EqualTo(235));
        }

        #endregion

    }

}
