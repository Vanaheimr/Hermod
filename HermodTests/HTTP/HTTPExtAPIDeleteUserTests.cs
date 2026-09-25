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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// A deleted user takes its password and its memberships with it: an
    /// account created again under the same id starts with neither - before a
    /// restart and after it, whichever comes first.
    /// </summary>
    /// <remarks>
    /// Both stayed behind. The password is kept by login, so the old one
    /// signed the new account in, and the single-user ChangePassword would not
    /// give it another without knowing the old one. And a group finds its
    /// members by their id, so the new account was at once in every group the
    /// old one had been in - an administrators group, say. Found by the energy
    /// meter, which worked around both.
    /// </remarks>
    [TestFixture]
    public class HTTPExtAPIDeleteUserTests
    {

        #region Data

        private const String OldPassword  = "Correct-Horse-1";
        private const String NewPassword  = "Staple-Battery-2";

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


        #region The_Old_Password_Does_Not_Sign_In_The_Account_Made_Again()

        [Test]
        public async Task The_Old_Password_Does_Not_Sign_In_The_Account_Made_Again()
        {

            var api    = await StartAPI();
            var alice  = await NewUser(api, "alice", OldPassword);

            Assert.That(api.VerifyPassword(alice.Id, OldPassword), Is.True);

            await Delete(api, alice);

            var again  = await NewUser(api, "alice");

            Assert.That(api.VerifyPassword(again.Id, OldPassword), Is.False, "the deleted account's password signs the new one in");

            // The single-user overload, which wants the current password when
            // there is one - and there must not be one.
            var changed = await api.ChangePassword(again, NewPassword);

            Assert.Multiple(() => {
                Assert.That(changed.Result,                               Is.EqualTo(CommandResult.Success),  changed.Description.FirstText());
                Assert.That(api.VerifyPassword(again.Id, NewPassword),    Is.True);
                Assert.That(api.VerifyPassword(again.Id, OldPassword),    Is.False);
            });

            Assert.That(CommandsIn(HTTPExtAPI.DefaultPasswordFile),
                        Is.EqualTo(new[] { "addPassword", "removePassword", "addPassword" }),
                        "the password file says what happened, in order");

            var restarted = await StartAPI();

            Assert.Multiple(() => {
                Assert.That(restarted.VerifyPassword(again.Id, OldPassword),  Is.False,  "the next start gave the old password back");
                Assert.That(restarted.VerifyPassword(again.Id, NewPassword),  Is.True,   "the next start lost the new one");
            });

        }

        #endregion

        #region The_Old_Password_Does_Not_Come_Back_With_A_Restart()

        /// <summary>
        /// Deleted, restarted, and only then created again: what the replay
        /// makes of the password file is all there is.
        /// </summary>
        [Test]
        public async Task The_Old_Password_Does_Not_Come_Back_With_A_Restart()
        {

            var api    = await StartAPI();
            var alice  = await NewUser(api, "alice", OldPassword);

            await Delete(api, alice);

            var restarted  = await StartAPI();

            Assert.That(restarted.TryGetUser(alice.Id, out _), Is.False, "the deleted account came back");

            var again      = await NewUser(restarted, "alice");

            Assert.That(restarted.VerifyPassword(again.Id, OldPassword), Is.False, "the next start gave the old password back");

            var changed    = await restarted.ChangePassword(again, NewPassword);

            Assert.Multiple(() => {
                Assert.That(changed.Result,                                   Is.EqualTo(CommandResult.Success),  changed.Description.FirstText());
                Assert.That(restarted.VerifyPassword(again.Id, NewPassword),  Is.True);
            });

            var once_more  = await StartAPI();

            Assert.Multiple(() => {
                Assert.That(once_more.VerifyPassword(again.Id, OldPassword),  Is.False);
                Assert.That(once_more.VerifyPassword(again.Id, NewPassword),  Is.True);
            });

        }

        #endregion

        #region The_Account_Made_Again_With_A_Password_Has_That_One()

        /// <summary>
        /// Created again with a password of its own, as CreateUser does it: the
        /// new password takes, and the replay does not refuse it for the login
        /// having had one before.
        /// </summary>
        [Test]
        public async Task The_Account_Made_Again_With_A_Password_Has_That_One()
        {

            var api    = await StartAPI();
            var alice  = await NewUser(api, "alice", OldPassword);

            await Delete(api, alice);

            var again  = await NewUser(api, "alice", NewPassword);

            Assert.Multiple(() => {
                Assert.That(api.VerifyPassword(again.Id, NewPassword),  Is.True,   "the new account's own password did not take");
                Assert.That(api.VerifyPassword(again.Id, OldPassword),  Is.False,  "the deleted account's password signs the new one in");
            });

            var restarted = await StartAPI();

            Assert.Multiple(() => {
                Assert.That(restarted.VerifyPassword(again.Id, NewPassword),  Is.True);
                Assert.That(restarted.VerifyPassword(again.Id, OldPassword),  Is.False);
            });

        }

        #endregion


        #region The_Account_Made_Again_Is_In_None_Of_The_Old_Ones_Groups()

        [Test]
        public async Task The_Account_Made_Again_Is_In_None_Of_The_Old_Ones_Groups()
        {

            var api     = await StartAPI();
            var admins  = await NewGroup(api, "admins");
            var viewer  = await NewGroup(api, "viewer");
            var alice   = await NewUser (api, "alice", OldPassword);
            var bob     = await NewUser (api, "bob");

            Assert.That((await api.AddUserToUserGroup(alice, User2UserGroupEdgeLabel.IsAdmin,  admins)).IsSuccess,  Is.True);
            Assert.That((await api.AddUserToUserGroup(alice, User2UserGroupEdgeLabel.IsMember, viewer)).IsSuccess,  Is.True);
            Assert.That((await api.AddUserToUserGroup(bob,   User2UserGroupEdgeLabel.IsAdmin,  admins)).IsSuccess,  Is.True);

            await Delete(api, alice);

            Assert.Multiple(() => {
                Assert.That(admins.Edges(alice),              Is.Empty,  "the group still holds the deleted account");
                Assert.That(alice.User2Group_OutEdges,        Is.Empty,  "the deleted account still holds its groups");
                Assert.That(api.IsMember(bob, admins.Id),     Is.True,   "and nobody else goes with it");
            });

            var again = await NewUser(api, "alice");

            Assert.Multiple(() => {
                Assert.That(api.IsMember(again, admins.Id),   Is.False,  "the new account is an administrator at once");
                Assert.That(api.IsMember(again, viewer.Id),   Is.False);
                Assert.That(viewer.Edges(again),              Is.Empty);
            });

            // A line for each membership, and before the deletion's own: the
            // replay has to find the user to take it out of anything.
            var commands = CommandsIn(HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName).ToList();

            Assert.Multiple(() => {
                Assert.That(commands.Count(command => command == "removeUserFromUserGroup"),  Is.EqualTo(2));
                Assert.That(commands.LastIndexOf("removeUserFromUserGroup"),                  Is.LessThan(commands.IndexOf("deleteUser")));
            });

            // Given a group of its own, it is in that one and in no other.
            Assert.That((await api.AddUserToUserGroup(again, User2UserGroupEdgeLabel.IsMember, viewer)).IsSuccess,  Is.True);

            var restarted = await StartAPI();

            Assert.Multiple(() => {
                Assert.That(restarted.IsMember(UserOf(restarted, "alice"), admins.Id),  Is.False,  "the next start made the new account an administrator");
                Assert.That(restarted.IsMember(UserOf(restarted, "alice"), viewer.Id),  Is.True,   "the next start lost the group it was given");
                Assert.That(restarted.IsMember(UserOf(restarted, "bob"),   admins.Id),  Is.True);
            });

        }

        #endregion

        #region The_Account_Made_Again_After_A_Restart_Is_In_None_Either()

        /// <summary>
        /// Deleted, restarted, and only then created again: the groups as the
        /// replay leaves them.
        /// </summary>
        [Test]
        public async Task The_Account_Made_Again_After_A_Restart_Is_In_None_Either()
        {

            var api     = await StartAPI();
            var admins  = await NewGroup(api, "admins");
            var alice   = await NewUser (api, "alice", OldPassword);

            await api.AddUserToUserGroup(alice, User2UserGroupEdgeLabel.IsAdmin, admins);

            await Delete(api, alice);

            var restarted  = await StartAPI();
            var again      = await NewUser(restarted, "alice");

            Assert.That(restarted.IsMember(again, admins.Id), Is.False, "the next start gave the new account the old one's groups");

            var once_more  = await StartAPI();

            Assert.That(once_more.IsMember(UserOf(once_more, "alice"), admins.Id), Is.False);

        }

        #endregion

        #region A_Database_From_Before_This_Hands_On_No_Groups_Either()

        /// <summary>
        /// A database written while a deletion still left the memberships
        /// behind has no line taking them away; the replay of the deletion
        /// takes them away itself.
        /// </summary>
        [Test]
        public async Task A_Database_From_Before_This_Hands_On_No_Groups_Either()
        {

            var api     = await StartAPI();
            var admins  = await NewGroup(api, "admins");
            var alice   = await NewUser (api, "alice");

            await api.AddUserToUserGroup(alice, User2UserGroupEdgeLabel.IsAdmin, admins);

            await Delete(api, alice);
            await NewUser(api, "alice");

            // As it would have been written before: without the lines.
            var databaseFile = Path.Combine(directory, "UsersAPI", HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName);

            File.WriteAllLines(databaseFile,
                               File.ReadAllLines(databaseFile).
                                    Where(line => !line.StartsWith("{\"removeUserFromUserGroup\"")).
                                    ToArray());

            Assert.That(CommandsIn(HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName), Does.Not.Contain("removeUserFromUserGroup"));

            var restarted = await StartAPI();

            Assert.That(restarted.IsMember(UserOf(restarted, "alice"), admins.Id), Is.False, "the next start gave the new account the old one's groups");

        }

        #endregion


        #region The_Account_Made_Again_Is_In_None_Of_The_Old_Ones_Organizations()

        /// <summary>
        /// Hermod refuses to delete a user who is still in an organization, and
        /// an API built on it may allow it: then the organizations let go of the
        /// user too.
        /// </summary>
        [Test]
        public async Task The_Account_Made_Again_Is_In_None_Of_The_Old_Ones_Organizations()
        {

            var api    = await StartAPI(MembersMayBeDeleted: true);
            var acme   = await NewOrganization(api, "acme");
            var alice  = await NewUser(api, "alice", OldPassword);
            var bob    = await NewUser(api, "bob");

            Assert.That((await api.AddUserToOrganization(alice, User2OrganizationEdgeLabel.IsAdmin, acme)).IsSuccess,  Is.True);
            Assert.That((await api.AddUserToOrganization(bob,   User2OrganizationEdgeLabel.IsAdmin, acme)).IsSuccess,  Is.True);

            await Delete(api, alice);

            Assert.Multiple(() => {
                Assert.That(acme.User2OrganizationInEdges(alice),                     Is.Empty,  "the organization still holds the deleted account");
                Assert.That(alice.User2Organization_OutEdges,                         Is.Empty,  "the deleted account still holds its organization");
                Assert.That(acme.Admins.Select(admin => admin.Id.ToString()),         Is.EqualTo(new[] { "bob" }));
            });

            Assert.That(CommandsIn(HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName).Count(command => command == "removeUserFromOrganization"),  Is.EqualTo(1));

            var again = await NewUser(api, "alice");

            Assert.Multiple(() => {
                Assert.That(acme.User2OrganizationInEdges(again),                     Is.Empty,  "the new account is an administrator of the organization at once");
                Assert.That(again.User2Organization_OutEdges,                         Is.Empty);
            });

            var restarted = await StartAPI(MembersMayBeDeleted: true);

            Assert.That(restarted.TryGetOrganization(acme.Id, out var reloaded), Is.True);

            Assert.Multiple(() => {
                Assert.That(reloaded!.User2OrganizationInEdges(UserOf(restarted, "alice")),  Is.Empty);
                Assert.That(reloaded. Admins.Select(admin => admin.Id.ToString()),           Is.EqualTo(new[] { "bob" }));
            });

        }

        #endregion


        #region (private) Helpers

        /// <summary>
        /// An API that lets a user who is still in an organization be deleted,
        /// as a subclass may: Hermod's own refuses.
        /// </summary>
        private sealed class MembersMayBeDeletedAPI(HTTPServer  HTTPServer,
                                                    String      LoggingPath)

            : HTTPExtAPI(HTTPServer,
                         RootPath:              HTTPPath.Parse("/accounts"),
                         SkipURLTemplates:      true,
                         DisableNotifications:  true,
                         LoggingPath:           LoggingPath,
                         MinUserIdLength:       3,
                         MinUserGroupIdLength:  3)

        {

            protected internal override I18NString? canDeleteUser(IUser User)
                => null;

        }

        /// <summary>
        /// A headless HTTPExtAPI on this test's directory, with whatever its
        /// database files hold read back - a first start or a restart.
        /// </summary>
        private async Task<HTTPExtAPI> StartAPI(Boolean MembersMayBeDeleted = false)
        {

            var server = new HTTPServer(
                             IPAddress:  IPv4Address.Localhost,
                             TCPPort:    IPPort.Zero,
                             AutoStart:  false
                         );

            var api    = MembersMayBeDeleted

                             ? new MembersMayBeDeletedAPI(server, directory)

                             : new HTTPExtAPI(
                                   server,
                                   RootPath:              HTTPPath.Parse("/accounts"),
                                   SkipURLTemplates:      true,
                                   DisableNotifications:  true,
                                   LoggingPath:           directory,
                                   MinUserIdLength:       3,
                                   MinUserGroupIdLength:  3
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

        private static async Task<UserGroup> NewGroup(HTTPExtAPI  API,
                                                      String      Name)
        {

            var added = await API.AddUserGroup(
                                  new UserGroup(
                                      UserGroup_Id.Parse(Name),
                                      I18NString.Create(Languages.en, Name)
                                  )
                              );

            Assert.That(added.Result, Is.EqualTo(CommandResult.Success), added.Description.FirstText());
            Assert.That(API.TryGetUserGroup(UserGroup_Id.Parse(Name), out var group) && group is UserGroup, Is.True);

            return (UserGroup) group!;

        }

        private static async Task<IOrganization> NewOrganization(HTTPExtAPI  API,
                                                                 String      Name)
        {

            var organization = await API.CreateOrganizationIfNotExists(
                                         Organization_Id.Parse(Name),
                                         I18NString.Create(Name)
                                     );

            Assert.That(organization, Is.Not.Null, $"'{Name}' could not be created");

            return organization!;

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
