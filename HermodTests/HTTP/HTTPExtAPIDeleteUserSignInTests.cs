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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.Passkeys;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// A deleted user takes every way in with it: its sessions, its passkeys,
    /// its API keys and its pending password resets let nobody into an account
    /// created again under the same id - before a restart and after it.
    /// </summary>
    /// <remarks>
    /// All four name their account by id and look it up by that id when they
    /// are used, so each of them outlived the account it was made for and
    /// opened the next one of that name. HTTPExtAPIDeleteUserTests has the
    /// password and the memberships, which were left behind the same way.
    /// </remarks>
    [TestFixture]
    public class HTTPExtAPIDeleteUserSignInTests
    {

        #region Data

        private const String OldPassword  = "Correct-Horse-1";
        private const String NewPassword  = "Staple-Battery-2";
        private const String ResetTo      = "Battery-Horse-3";

        private String directory = "";

        #endregion

        #region SetUp / TearDown

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), $"hermod-deleted-{Guid.NewGuid():N}") + Path.DirectorySeparatorChar;
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }

        #endregion


        #region The_Old_Sessions_Do_Not_Sign_In_The_Account_Made_Again()

        [Test]
        public async Task The_Old_Sessions_Do_Not_Sign_In_The_Account_Made_Again()
        {

            var api            = await StartAPI();
            var alice          = await NewUser(api, "alice", OldPassword);
            var bob            = await NewUser(api, "bob");

            var alicesSession  = api.Sessions.Create(alice.Id);
            var bobsSession    = api.Sessions.Create(bob.  Id);

            // Each acting as the other: a session names two users then.
            var aliceAsBob     = api.Sessions.Create(bob.  Id, SuperUserId: alice.Id);
            var bobAsAlice     = api.Sessions.Create(alice.Id, SuperUserId: bob.  Id);

            Assert.That(SignedInAs(api, alicesSession), Is.EqualTo("alice"));

            await Delete(api, alice);
            await NewUser(api, "alice");

            Assert.Multiple(() => {
                Assert.That(SignedInAs (api, alicesSession),                   Is.Null,            "the deleted account's session signs the new one in");
                Assert.That(SuperUserOf(api, aliceAsBob),                      Is.Null,            "the new account acts as bob");
                Assert.That(SignedInAs (api, bobAsAlice),                      Is.Null,            "bob acts as the new account");
                Assert.That(api.Sessions.TryGet(alicesSession.Token, out _),   Is.False,           "the deleted account's session lives on");
                Assert.That(api.Sessions.TryGet(aliceAsBob.   Token, out _),   Is.False,           "so does the one it acted as somebody else in");
                Assert.That(api.Sessions.TryGet(bobAsAlice.   Token, out _),   Is.False,           "and the one somebody else acted as it in");
                Assert.That(SignedInAs (api, bobsSession),                     Is.EqualTo("bob"),  "and nobody else is signed out");
            });

            var restarted = await StartAPI();

            Assert.Multiple(() => {
                Assert.That(SignedInAs (restarted, alicesSession),  Is.Null,            "the next start brought the session back");
                Assert.That(SuperUserOf(restarted, aliceAsBob),     Is.Null);
                Assert.That(SignedInAs (restarted, bobAsAlice),     Is.Null);
                Assert.That(SignedInAs (restarted, bobsSession),    Is.EqualTo("bob"),  "the next start lost somebody else's session");
            });

        }

        #endregion

        #region The_Old_Session_Does_Not_Come_Back_With_A_Restart()

        /// <summary>
        /// Deleted, restarted, and only then created again: what the session
        /// store makes of its file is all there is.
        /// </summary>
        [Test]
        public async Task The_Old_Session_Does_Not_Come_Back_With_A_Restart()
        {

            var api            = await StartAPI();
            var alice          = await NewUser(api, "alice", OldPassword);
            var alicesSession  = api.Sessions.Create(alice.Id);

            await Delete(api, alice);

            var restarted      = await StartAPI();

            Assert.That(restarted.Sessions.TryGet(alicesSession.Token, out _),  Is.False,  "the next start brought the deleted account's session back");

            await NewUser(restarted, "alice");

            Assert.That(SignedInAs(restarted, alicesSession),                   Is.Null,   "the deleted account's session signs the new one in");

        }

        #endregion


        #region The_Old_Passkeys_Do_Not_Sign_In_The_Account_Made_Again()

        [Test]
        public async Task The_Old_Passkeys_Do_Not_Sign_In_The_Account_Made_Again()
        {

            var api    = await StartAPI();
            var alice  = await NewUser(api, "alice", OldPassword);
            var bob    = await NewUser(api, "bob");

            Assert.That(await api.AddPasskey(alice, NewPasskey("alices-phone")),   Is.True);
            Assert.That(await api.AddPasskey(alice, NewPasskey("alices-laptop")),  Is.True);
            Assert.That(await api.AddPasskey(bob,   NewPasskey("bobs-phone")),     Is.True);

            Assert.That(OwnerOf(api, "alices-phone"), Is.EqualTo("alice"));

            await Delete(api, alice);

            var again = await NewUser(api, "alice");

            Assert.Multiple(() => {
                Assert.That(OwnerOf(api, "alices-phone"),   Is.Null,            "the deleted account's passkey signs the new one in");
                Assert.That(OwnerOf(api, "alices-laptop"),  Is.Null);
                Assert.That(api.GetPasskeys(again.Id),      Is.Empty,           "the new account has the old one's passkeys");
                Assert.That(OwnerOf(api, "bobs-phone"),     Is.EqualTo("bob"),  "and nobody else loses one");
            });

            // A line for each, and before the deletion's own, as for the memberships.
            var commands = CommandsIn(HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName).ToList();

            Assert.Multiple(() => {
                Assert.That(commands.Count(command => command == "removePasskey"),  Is.EqualTo(2));
                Assert.That(commands.LastIndexOf("removePasskey"),                  Is.LessThan(commands.IndexOf("deleteUser")));
            });

            // The phone registered again, by the new account now: a credential id
            // is registered once, for anybody, and the old registration let go of it.
            Assert.That(await api.AddPasskey(again, NewPasskey("alices-phone")),  Is.True,  "the deleted account still holds the credential id");

            var restarted = await StartAPI();

            Assert.Multiple(() => {
                Assert.That(OwnerOf(restarted, "alices-laptop"),                            Is.Null,                         "the next start brought the deleted account's passkey back");
                Assert.That(OwnerOf(restarted, "alices-phone"),                             Is.EqualTo("alice"),             "the next start lost the passkey registered again");
                Assert.That(restarted.GetPasskeys(again.Id).Select(passkey => passkey.Id),  Is.EqualTo(new[] { "alices-phone" }));
                Assert.That(OwnerOf(restarted, "bobs-phone"),                               Is.EqualTo("bob"));
            });

        }

        #endregion

        #region The_Old_API_Keys_Do_Not_Sign_In_The_Account_Made_Again()

        [Test]
        public async Task The_Old_API_Keys_Do_Not_Sign_In_The_Account_Made_Again()
        {

            var api          = await StartAPI();
            var alice        = await NewUser(api, "alice", OldPassword);
            var bob          = await NewUser(api, "bob");

            var alicesKey    = await NewAPIKey(api, alice, "alices-key-0123456789abcdef");
            var alicesOther  = await NewAPIKey(api, alice, "alices-other-key-0123456789");
            var bobsKey      = await NewAPIKey(api, bob,   "bobs-key-0123456789abcdef");

            Assert.That(KeyedInAs(api, alicesKey), Is.EqualTo("alice"));

            await Delete(api, alice);

            var again = await NewUser(api, "alice");

            Assert.Multiple(() => {
                Assert.That(KeyedInAs(api, alicesKey),     Is.Null,            "the deleted account's API key signs the new one in");
                Assert.That(KeyedInAs(api, alicesOther),   Is.Null);
                Assert.That(api.GetAPIKeysForUser(again),  Is.Empty,           "the new account has the old one's API keys");
                Assert.That(KeyedInAs(api, bobsKey),       Is.EqualTo("bob"),  "and nobody else loses one");
            });

            // A line for each, and before the deletion's own: the replay reads an
            // API key only while its user is there.
            var commands = CommandsIn(HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName).ToList();

            Assert.Multiple(() => {
                Assert.That(commands.Count(command => command == "removeAPIKey"),  Is.EqualTo(2));
                Assert.That(commands.LastIndexOf("removeAPIKey"),                  Is.LessThan(commands.IndexOf("deleteUser")));
            });

            var restarted = await StartAPI();

            Assert.Multiple(() => {
                Assert.That(restarted.TryGetAPIKey(alicesKey, out _),  Is.False,           "the next start brought the deleted account's API key back");
                Assert.That(KeyedInAs(restarted, alicesKey),           Is.Null);
                Assert.That(KeyedInAs(restarted, alicesOther),         Is.Null);
                Assert.That(KeyedInAs(restarted, bobsKey),             Is.EqualTo("bob"),  "the next start lost somebody else's API key");
            });

        }

        #endregion

        #region A_Database_From_Before_This_Hands_On_No_Passkeys_And_No_API_Keys()

        /// <summary>
        /// A database written while a deletion still left the passkeys and the
        /// API keys behind has no line removing them; the replay of the deletion
        /// removes them itself.
        /// </summary>
        [Test]
        public async Task A_Database_From_Before_This_Hands_On_No_Passkeys_And_No_API_Keys()
        {

            var api        = await StartAPI();
            var alice      = await NewUser(api, "alice");

            Assert.That(await api.AddPasskey(alice, NewPasskey("alices-phone")), Is.True);

            var alicesKey  = await NewAPIKey(api, alice, "alices-key-0123456789abcdef");

            await Delete(api, alice);
            await NewUser(api, "alice");

            // As it would have been written before: without the lines.
            var databaseFile = Path.Combine(directory, "UsersAPI", HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName);

            File.WriteAllLines(databaseFile,
                               File.ReadAllLines(databaseFile).
                                    Where(line => !line.StartsWith("{\"removePasskey\"") &&
                                                  !line.StartsWith("{\"removeAPIKey\"")).
                                    ToArray());

            Assert.That(CommandsIn(HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName),
                        Has.None.EqualTo("removePasskey").And.None.EqualTo("removeAPIKey"));

            var restarted = await StartAPI();

            Assert.Multiple(() => {
                Assert.That(OwnerOf  (restarted, "alices-phone"),  Is.Null,  "the next start gave the new account the old one's passkey");
                Assert.That(KeyedInAs(restarted, alicesKey),       Is.Null,  "the next start gave the new account the old one's API key");
            });

        }

        #endregion


        #region The_Old_Password_Reset_Does_Not_Set_The_Password_Of_The_Account_Made_Again()

        [Test]
        public async Task The_Old_Password_Reset_Does_Not_Set_The_Password_Of_The_Account_Made_Again()
        {

            var api    = await StartAPI();
            var alice  = await NewUser(api, "alice", OldPassword);
            var token  = await NewPasswordReset(api, alice);

            await Delete(api, alice);

            var again  = await NewUser(api, "alice", NewPassword);
            var reset  = await api.ResetPassword(token, ResetTo);

            Assert.Multiple(() => {
                Assert.That(api.VerifyPassword(again.Id, ResetTo),      Is.False,                               "the deleted account's reset set the password of the new one");
                Assert.That(api.VerifyPassword(again.Id, NewPassword),  Is.True);
                Assert.That(reset.Result,                               Is.Not.EqualTo(CommandResult.Success),  "the deleted account's reset was taken");
            });

            Assert.That(CommandsIn(HTTPExtAPI.DefaultPasswordResetsFile),
                        Is.EqualTo(new[] { "add", "remove" }),
                        "the password resets file says what happened, in order");

            var restarted = await StartAPI();

            reset = await restarted.ResetPassword(token, ResetTo);

            Assert.Multiple(() => {
                Assert.That(reset.Result,                                     Is.Not.EqualTo(CommandResult.Success),  "the next start brought the deleted account's reset back");
                Assert.That(restarted.VerifyPassword(again.Id, ResetTo),      Is.False);
                Assert.That(restarted.VerifyPassword(again.Id, NewPassword),  Is.True);
            });

        }

        #endregion

        #region A_Password_Reset_Shared_With_Another_Login_Stays_For_That_One()

        /// <summary>
        /// A reset may be for several logins at once, of one person with one
        /// e-mail address: the deleted login leaves it, the others keep it, with
        /// the same token - and after a restart as well.
        /// </summary>
        [Test]
        public async Task A_Password_Reset_Shared_With_Another_Login_Stays_For_That_One()
        {

            var api     = await StartAPI();
            var alice   = await NewUser(api, "alice", OldPassword);
            var bob     = await NewUser(api, "bob",   OldPassword);

            var first   = await NewPasswordReset(api, alice, bob);
            var second  = await NewPasswordReset(api, alice, bob);

            await Delete(api, alice);

            var again   = await NewUser(api, "alice", NewPassword);
            var reset   = await api.ResetPassword(first, ResetTo);

            Assert.Multiple(() => {
                Assert.That(api.VerifyPassword(again.Id, ResetTo),      Is.False,                           "the reset set the password of the account made again");
                Assert.That(api.VerifyPassword(again.Id, NewPassword),  Is.True);
                Assert.That(api.VerifyPassword(bob.  Id, ResetTo),      Is.True,                            "the reset no longer works for the other login");
                Assert.That(reset.Result,                               Is.EqualTo(CommandResult.Success),  reset.Description.FirstText());
            });

            // Each one taken away, put back without alice - and the first one
            // taken away once more, as used.
            var commands = CommandsIn(HTTPExtAPI.DefaultPasswordResetsFile).ToList();

            Assert.Multiple(() => {
                Assert.That(commands.Count(command => command == "add"),     Is.EqualTo(4));
                Assert.That(commands.Count(command => command == "remove"),  Is.EqualTo(3));
            });

            var restarted = await StartAPI();

            reset = await restarted.ResetPassword(second, OldPassword);

            Assert.Multiple(() => {
                Assert.That(reset.Result,                                     Is.EqualTo(CommandResult.Success),  reset.Description.FirstText());
                Assert.That(restarted.VerifyPassword(bob.  Id, OldPassword),  Is.True,                            "the next start lost the reset of the other login");
                Assert.That(restarted.VerifyPassword(again.Id, OldPassword),  Is.False,                           "the next start let the reset set the password of the account made again");
                Assert.That(restarted.VerifyPassword(again.Id, NewPassword),  Is.True);
            });

        }

        #endregion


        #region What_A_Start_Read_Back_Goes_As_Well()

        /// <summary>
        /// Deleted after a restart, when every way in was read back from the
        /// database files rather than made in this run: it goes all the same.
        /// </summary>
        [Test]
        public async Task What_A_Start_Read_Back_Goes_As_Well()
        {

            var api            = await StartAPI();
            var alice          = await NewUser(api, "alice", OldPassword);
            var alicesSession  = api.Sessions.Create(alice.Id);
            var alicesKey      = await NewAPIKey(api, alice, "alices-key-0123456789abcdef");
            var firstToken     = await NewPasswordReset(api, alice);
            var secondToken    = await NewPasswordReset(api, alice);

            Assert.That(await api.AddPasskey(alice, NewPasskey("alices-phone")), Is.True);

            var restarted      = await StartAPI();
            var reset          = await restarted.ResetPassword(firstToken, "Horse-Battery-4");

            Assert.Multiple(() => {
                Assert.That(SignedInAs(restarted, alicesSession),                 Is.EqualTo("alice"),                "the session was not read back");
                Assert.That(KeyedInAs (restarted, alicesKey),                     Is.EqualTo("alice"),                "the API key was not read back");
                Assert.That(OwnerOf   (restarted, "alices-phone"),                Is.EqualTo("alice"),                "the passkey was not read back");
                Assert.That(reset.Result,                                         Is.EqualTo(CommandResult.Success),  "the password resets were not read back");
                Assert.That(restarted.VerifyPassword(alice.Id, "Horse-Battery-4"),  Is.True);
            });

            await Delete(restarted, UserOf(restarted, "alice"));

            var again          = await NewUser(restarted, "alice", NewPassword);

            reset              = await restarted.ResetPassword(secondToken, ResetTo);

            Assert.Multiple(() => {
                Assert.That(SignedInAs(restarted, alicesSession),        Is.Null,   "the deleted account's session signs the new one in");
                Assert.That(KeyedInAs (restarted, alicesKey),            Is.Null,   "the deleted account's API key signs the new one in");
                Assert.That(OwnerOf   (restarted, "alices-phone"),       Is.Null,   "the deleted account's passkey signs the new one in");
                Assert.That(restarted.VerifyPassword(again.Id, ResetTo), Is.False,  "the deleted account's reset set the password of the new one");
            });

            var once_more      = await StartAPI();

            reset              = await once_more.ResetPassword(secondToken, ResetTo);

            Assert.Multiple(() => {
                Assert.That(SignedInAs(once_more, alicesSession),            Is.Null);
                Assert.That(KeyedInAs (once_more, alicesKey),                Is.Null);
                Assert.That(OwnerOf   (once_more, "alices-phone"),           Is.Null);
                Assert.That(once_more.VerifyPassword(again.Id, ResetTo),     Is.False);
                Assert.That(once_more.VerifyPassword(again.Id, NewPassword), Is.True);
            });

        }

        #endregion


        #region (private) Helpers

        /// <summary>
        /// A headless HTTPExtAPI on this test's directory, with whatever its
        /// database files hold read back - a first start or a restart.
        /// </summary>
        /// <remarks>
        /// With a robot and an external DNS name, which the "password changed"
        /// e-mail of a password reset is written with; it goes nowhere.
        /// </remarks>
        private async Task<HTTPExtAPI> StartAPI()
        {

            var server = new HTTPServer(
                             IPAddress:  IPv4Address.Localhost,
                             TCPPort:    IPPort.Zero,
                             AutoStart:  false
                         );

            var api    = new HTTPExtAPI(
                             server,
                             RootPath:              HTTPPath.Parse("/accounts"),
                             SkipURLTemplates:      true,
                             DisableNotifications:  true,
                             LoggingPath:           directory,
                             ExternalDNSName:       "example.test",
                             APIRobotEMailAddress:  new EMailAddress(SimpleEMailAddress.Parse("robot@example.test"), "Robot"),
                             MinUserIdLength:       3
                         );

            await api.LoadDatabase();

            return api;

        }

        private static async Task<User> NewUser(HTTPExtAPI  API,
                                                String      Name,
                                                String?     Password   = null)
        {

            var user = await API.CreateUser(
                                 User_Id.Parse(Name),
                                 I18NString.Create(Name),
                                 SimpleEMailAddress.Parse($"{Name}@example.test"),
                                 Password,
                                 SkipDefaultNotifications:  true,
                                 SkipNewUserEMail:          true,
                                 SkipNewUserNotifications:  true
                             );

            Assert.That(user, Is.Not.Null, $"'{Name}' could not be created");

            return UserOf(API, Name);

        }

        private static User UserOf(HTTPExtAPI  API,
                                   String      Name)
        {

            Assert.That(API.TryGetUser(User_Id.Parse(Name), out var user) && user is User, Is.True, $"There is no user '{Name}'.");

            return (User) user!;

        }

        private static async Task Delete(HTTPExtAPI  API,
                                         IUser       User)
        {

            var deleted = await API.DeleteUser(User, SkipUserDeletedNotifications: true);

            Assert.That(deleted.Result, Is.EqualTo(CommandResult.Success), deleted.Description.FirstText());

        }

        private static Passkey NewPasskey(String CredentialId)

            => new (
                   Id:              CredentialId,
                   PublicKey:       new Byte[32],
                   Algorithm:       WebAuthn.ES256,
                   SignCount:       0,
                   Transports:      [ "internal" ],
                   Discoverable:    true,
                   BackupEligible:  false,
                   BackedUp:        false,
                   AAGUID:          Guid.Empty,
                   Name:            CredentialId,
                   CreatedAt:       DateTimeOffset.UtcNow,
                   LastUsedAt:      null
               );

        private static async Task<APIKey_Id> NewAPIKey(HTTPExtAPI  API,
                                                       IUser       User,
                                                       String      Id)
        {

            var apiKeyId  = APIKey_Id.Parse(Id);
            var added     = await API.AddAPIKey(new APIKey(apiKeyId, User.Id));

            Assert.That(added.Result, Is.EqualTo(CommandResult.Success), added.Description.FirstText());

            return apiKeyId;

        }

        private static async Task<SecurityToken_Id> NewPasswordReset(HTTPExtAPI      API,
                                                                     params IUser[]  Users)
        {

            var token  = SecurityToken_Id.Random();
            var added  = await API.AddPasswordReset(
                                   new PasswordReset(Users, token),
                                   SuppressNotifications: true
                               );

            Assert.That(added.Result, Is.EqualTo(CommandResult.Success), added.Description.FirstText());

            return token;

        }

        /// <summary>
        /// Who a request with the cookie of the given session is signed in as,
        /// if anybody: the way in every guarded route takes.
        /// </summary>
        private static String? SignedInAs(HTTPExtAPI  API,
                                          Session     Session)

            => API.TryGetHTTPUser(RequestWith($"Cookie: {API.SessionCookieName}={Session.Token}"), out var user)
                   ? user?.Id.ToString()
                   : null;

        /// <summary>
        /// Who really signed in the given session, when it acts as somebody else.
        /// </summary>
        private static String? SuperUserOf(HTTPExtAPI  API,
                                           Session     Session)

            => API.TryGetSuperUser(RequestWith($"Cookie: {API.SessionCookieName}={Session.Token}"), out var user)
                   ? user.Id.ToString()
                   : null;

        /// <summary>
        /// Who a request with the given API key is signed in as, if anybody.
        /// </summary>
        private static String? KeyedInAs(HTTPExtAPI  API,
                                         APIKey_Id   APIKeyId)

            => API.TryGetHTTPUser(RequestWith($"API-Key: {APIKeyId}"), out var user)
                   ? user?.Id.ToString()
                   : null;

        /// <summary>
        /// Whom the passkey with the given credential id signs in, if anybody:
        /// what a passkey sign-in looks up once the browser names it.
        /// </summary>
        private static String? OwnerOf(HTTPExtAPI  API,
                                       String      CredentialId)

            => API.TryGetPasskey(CredentialId, out var owner, out _)
                   ? owner.Id.ToString()
                   : null;

        private static HTTPRequest RequestWith(String HeaderLine)
        {

            Assert.That(HTTPRequest.TryParse("GET /accounts/auth/me HTTP/1.1\r\n" +
                                             "Host: example.test\r\n" +
                                             HeaderLine + "\r\n" +
                                             "\r\n",
                                             out var request),
                        Is.True);

            return request!;

        }

        /// <summary>
        /// The commands in the given database file of this test, in the order
        /// they were written.
        /// </summary>
        private IEnumerable<String> CommandsIn(String FileName)

            => File.ReadLines(Path.Combine(directory, "UsersAPI", FileName)).
                    Where (line => line.StartsWith('{')).
                    Select(line => ((JProperty) JObject.Parse(line).First!).Name).
                    ToArray();

        #endregion

    }

}
