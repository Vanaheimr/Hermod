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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Mail;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// Taking a user out of a user group: off the user, off the group, and
    /// still out after a restart has read the database files back.
    /// </summary>
    /// <remarks>
    /// The group is what <see cref="HTTPExtAPI.IsMember"/> asks, and taking a
    /// user out of it put them straight back on it - so a user taken out of a
    /// group stayed in it, with the removal written down as done. And what was
    /// written down was never read back, so a removal that had worked would
    /// still have been undone by the next start. Found by a meter whose
    /// administrator could not be made a guest: the answer said "guest", and
    /// the next request was an administrator's again.
    /// </remarks>
    [TestFixture]
    public class HTTPExtAPIUserGroupTests
    {

        #region Data

        private String directory = "";

        #endregion

        #region SetUp / TearDown

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), $"hermod-groups-{Guid.NewGuid():N}") + Path.DirectorySeparatorChar;
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }

        #endregion


        #region A_User_Taken_Out_Of_A_Group_Is_Out_Of_It()

        [Test]
        public async Task A_User_Taken_Out_Of_A_Group_Is_Out_Of_It()
        {

            var api    = await StartAPI();
            var group  = await NewGroup(api, "viewer");
            var alice  = await NewUser (api, "alice");
            var bob    = await NewUser (api, "bob");

            Assert.That((await api.AddUserToUserGroup(alice, User2UserGroupEdgeLabel.IsMember, group)).IsSuccess,  Is.True);
            Assert.That((await api.AddUserToUserGroup(bob,   User2UserGroupEdgeLabel.IsMember, group)).IsSuccess,  Is.True);

            var removed = await api.RemoveUserFromUserGroup(alice, group);

            Assert.Multiple(() => {

                Assert.That(removed.IsSuccess,              Is.True);

                Assert.That(api.IsMember(alice, group.Id),  Is.False,  "the group still counts her");
                Assert.That(group.Edges(alice),             Is.Empty,  "the group still holds her edge");
                Assert.That(alice.User2Group_OutEdges,      Is.Empty,  "she still holds the group's edge");

                Assert.That(api.IsMember(bob,   group.Id),  Is.True,   "and nobody else goes with her");

            });

            // Once, and read back: at a restart the database files are all
            // there is.
            Assert.That(RemovalsWritten(), Is.EqualTo(1));

            var restarted  = await StartAPI();
            var aliceAgain = UserOf(restarted, "alice");

            Assert.Multiple(() => {
                Assert.That(restarted.IsMember(aliceAgain, group.Id),                 Is.False,  "the next start put her back in");
                Assert.That(aliceAgain.User2Group_OutEdges,                           Is.Empty,  "the next start gave her the group's edge back");
                Assert.That(restarted.IsMember(UserOf(restarted, "bob"), group.Id),   Is.True,   "the group and its other member came back");
            });

        }

        #endregion

        #region Only_The_Given_Label_Is_Taken_Off()

        [Test]
        public async Task Only_The_Given_Label_Is_Taken_Off()
        {

            var api    = await StartAPI();
            var group  = await NewGroup(api, "systemadmin");
            var alice  = await NewUser (api, "alice");

            await api.AddUserToUserGroup(alice, User2UserGroupEdgeLabel.IsAdmin,  group);
            await api.AddUserToUserGroup(alice, User2UserGroupEdgeLabel.IsMember, group);

            var removed = await api.RemoveUserFromUserGroup(alice, User2UserGroupEdgeLabel.IsAdmin, group);

            Assert.Multiple(() => {
                Assert.That(removed.IsSuccess,                                       Is.True);
                Assert.That(group.HasEdge(User2UserGroupEdgeLabel.IsAdmin,  alice),  Is.False,  "the label taken off is still on the group");
                Assert.That(group.HasEdge(User2UserGroupEdgeLabel.IsMember, alice),  Is.True,   "the other label went with it");
                Assert.That(alice.EdgeLabels(group),                                 Is.EqualTo(new[] { User2UserGroupEdgeLabel.IsMember }));
            });

            var restarted = await StartAPI();

            Assert.That(restarted.TryGetUserGroup(group.Id, out var reloaded), Is.True);

            Assert.Multiple(() => {
                Assert.That(reloaded!.HasEdge(User2UserGroupEdgeLabel.IsAdmin,  UserOf(restarted, "alice")),  Is.False);
                Assert.That(reloaded. HasEdge(User2UserGroupEdgeLabel.IsMember, UserOf(restarted, "alice")),  Is.True);
            });

        }

        #endregion

        #region A_User_Taken_Out_Can_Be_Put_Back()

        /// <summary>
        /// Taken out and put back in, as a role change that is undone does -
        /// and the start after it arrives at the same answer.
        /// </summary>
        /// <remarks>
        /// Refused before, with "The given edge already exists!": the group
        /// still held the edge the removal had put back on it.
        /// </remarks>
        [Test]
        public async Task A_User_Taken_Out_Can_Be_Put_Back()
        {

            var api    = await StartAPI();
            var group  = await NewGroup(api, "auditor");
            var alice  = await NewUser (api, "alice");

            await api.AddUserToUserGroup     (alice, User2UserGroupEdgeLabel.IsMember, group);
            await api.RemoveUserFromUserGroup(alice, group);

            var again = await api.AddUserToUserGroup(alice, User2UserGroupEdgeLabel.IsMember, group);

            Assert.Multiple(() => {
                Assert.That(again.IsSuccess,                Is.True,  "a user taken out could not be put back in");
                Assert.That(api.IsMember(alice, group.Id),  Is.True);
                Assert.That(group.Edges(alice).Count(),     Is.EqualTo(1),  "and is in it once");
            });

            var restarted = await StartAPI();

            Assert.That(restarted.IsMember(UserOf(restarted, "alice"), group.Id), Is.True);

        }

        #endregion


        #region (private) Helpers

        /// <summary>
        /// A headless HTTPExtAPI on this test's directory, with whatever its
        /// database files hold read back - a first start or a restart.
        /// </summary>
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
                             MinUserIdLength:       3,
                             MinUserGroupIdLength:  3
                         );

            await api.LoadDatabase();

            return api;

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

        private static async Task<User> NewUser(HTTPExtAPI  API,
                                                String      Name)
        {

            var user = await API.CreateUser(
                                 User_Id.Parse(Name),
                                 I18NString.Create(Name),
                                 SimpleEMailAddress.Parse($"{Name}@example.test"),
                                 "Correct-Horse-1",
                                 SkipDefaultNotifications:  true,
                                 SkipNewUserEMail:          true,
                                 SkipNewUserNotifications:  true
                             );

            Assert.That(user, Is.Not.Null);

            return UserOf(API, Name);

        }

        private static User UserOf(HTTPExtAPI  API,
                                   String      Name)
        {

            Assert.That(API.TryGetUser(User_Id.Parse(Name), out var user) && user is User, Is.True, $"There is no user '{Name}'.");

            return (User) user!;

        }

        /// <summary>
        /// How many lines of the database file say that a user was taken out
        /// of a group.
        /// </summary>
        private Int32 RemovalsWritten()

            => File.ReadLines(Path.Combine(directory, "UsersAPI", HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName)).
                    Count(line => line.Contains("removeUserFromUserGroup"));

        #endregion

    }

}
