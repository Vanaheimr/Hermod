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
using System.Security.Cryptography;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP.Notifications;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// Each database file of an HTTPExtAPI is a hash chain of its own: every
    /// line names the hash of the line before it in the same file as its
    /// parent, the first line names none, and a restart goes on from the
    /// file's last line.
    /// </summary>
    /// <remarks>
    /// The API, its password file and its password resets file shared one
    /// chain value. A password written between two lines of the API's file
    /// left a gap there, and a restart went on from the last line of the
    /// password file instead of the API's own. And as the hash was taken
    /// outside the file lock, two lines written at once could name the same
    /// parent. Found with a probe test of the SSH key commands on 2026-10-05.
    /// </remarks>
    [TestFixture]
    public class HTTPExtAPIDatabaseHashChainTests
    {

        #region Data

        private const String Password     = "Correct-Horse-1";
        private const String NewPassword  = "Staple-Battery-2";

        private String directory = "";

        private readonly List<HTTPServer> servers = [];

        #endregion

        #region SetUp / TearDown

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), $"hermod-chain-{Guid.NewGuid():N}") + Path.DirectorySeparatorChar;
        }

        [TearDown]
        public async Task TearDown()
        {

            foreach (var server in servers)
                await server.DisposeAsync();

            servers.Clear();

            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);

        }

        #endregion


        #region A_Password_Leaves_No_Gap_In_The_Database_File()

        /// <summary>
        /// The password of a new user goes to the password file, between the
        /// user's line and the next one of the API's file.
        /// </summary>
        [Test]
        public async Task A_Password_Leaves_No_Gap_In_The_Database_File()
        {

            var api = await StartAPI();

            await NewUser (api, "hank", Password);
            await NewGroup(api, "admins");

            Assert.That(CommandsIn(HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName),
                        Is.EqualTo(new[] { "addUser", "addUserGroup" }));

            Assert.That(CommandsIn(HTTPExtAPI.DefaultPasswordFile),
                        Is.EqualTo(new[] { "addPassword" }),
                        "the password went to its own file");

            AssertChain(HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName);

        }

        #endregion

        #region A_Restart_Goes_On_From_The_Database_Files_Last_Line()

        /// <summary>
        /// The password file is read after the API's own file at a start, and
        /// its last line must not become the parent of the API's next line.
        /// </summary>
        [Test]
        public async Task A_Restart_Goes_On_From_The_Database_Files_Last_Line()
        {

            var api = await StartAPI();

            await NewUser (api, "hank", Password);
            await NewGroup(api, "admins");

            var before    = ChainOf(HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName);

            var restarted = await StartAPI();

            await NewGroup(restarted, "staff");

            var after     = ChainOf(HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName);

            Assert.That(after.Select(link => link.Command), Is.EqualTo(new[] { "addUser", "addUserGroup", "addUserGroup" }));

            Assert.That(after[2].Parent,
                        Is.EqualTo(before[1].Hash),
                        $"the first line after the restart chains to {after[2].Parent}, not to the file's last line before it; the password file's last line is {ChainOf(HTTPExtAPI.DefaultPasswordFile).Last().Hash}");

            AssertChain(HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName);

        }

        #endregion

        #region SSH_Keys_Added_And_Removed_Across_A_Restart()

        /// <summary>
        /// The probe that found it: a user with a password, an SSH key added
        /// and removed, a restart, and another key added.
        /// </summary>
        [Test]
        public async Task SSH_Keys_Added_And_Removed_Across_A_Restart()
        {

            var api   = await StartAPI();
            var hank  = await NewUser(api, "hank", Password);
            var key   = SshHostKey.GenerateEd25519();

            var added = await api.AddSSHKey(hank, SshPublicKey.FromHostKey(key, "tester").ToAuthorizedKeyLine());
            Assert.That(added.Outcome, Is.EqualTo(AddSSHKeyOutcome.Added), added.Reason);

            Assert.That(await api.RemoveSSHKey(hank.Id, SshFingerprint.Sha256(key.PublicKeyBlob)), Is.True);

            var restarted = await StartAPI();

            added = await restarted.AddSSHKey(UserOf(restarted, "hank"), SshPublicKey.FromHostKey(SshHostKey.GenerateEd25519(), "tester").ToAuthorizedKeyLine());
            Assert.That(added.Outcome, Is.EqualTo(AddSSHKeyOutcome.Added), added.Reason);

            Assert.That(CommandsIn(HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName),
                        Is.EqualTo(new[] { "addUser", "addSSHKey", "removeSSHKey", "addSSHKey" }));

            AssertChain(HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName);
            AssertChain(HTTPExtAPI.DefaultPasswordFile);

        }

        #endregion

        #region The_Password_File_Is_A_Chain_Of_Its_Own()

        /// <summary>
        /// The password file starts its own chain, and goes on from its own
        /// last line, after lines of the API's file and after a restart.
        /// </summary>
        [Test]
        public async Task The_Password_File_Is_A_Chain_Of_Its_Own()
        {

            var api   = await StartAPI();
            var hank  = await NewUser(api, "hank", Password);

            await NewGroup(api, "admins");

            var changed = await api.ChangePassword(hank, NewPassword, Password);
            Assert.That(changed.Result, Is.EqualTo(CommandResult.Success), changed.Description.FirstText());

            var restarted = await StartAPI();

            await NewGroup(restarted, "staff");

            changed = await restarted.ChangePassword(UserOf(restarted, "hank"), Password, NewPassword);
            Assert.That(changed.Result, Is.EqualTo(CommandResult.Success), changed.Description.FirstText());

            Assert.That(CommandsIn(HTTPExtAPI.DefaultPasswordFile),
                        Is.EqualTo(new[] { "addPassword", "changePassword", "changePassword" }));

            AssertChain(HTTPExtAPI.DefaultPasswordFile);
            AssertChain(HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName);

        }

        #endregion

        #region The_Password_Resets_File_Is_A_Chain_Of_Its_Own()

        /// <summary>
        /// The password resets file was not even read back into the chain
        /// value: its next line after a restart chained to the password file.
        /// </summary>
        [Test]
        public async Task The_Password_Resets_File_Is_A_Chain_Of_Its_Own()
        {

            var api   = await StartAPI();
            var hank  = await NewUser(api, "hank", Password);

            await AddReset(api, hank);
            await NewGroup(api, "admins");

            var restarted = await StartAPI();

            await NewGroup(restarted, "staff");
            await AddReset(restarted, UserOf(restarted, "hank"));

            Assert.That(CommandsIn(HTTPExtAPI.DefaultPasswordResetsFile),
                        Is.EqualTo(new[] { "add", "add" }));

            AssertChain(HTTPExtAPI.DefaultPasswordResetsFile);
            AssertChain(HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName);
            AssertChain(HTTPExtAPI.DefaultPasswordFile);

        }

        #endregion

        #region Concurrent_Writers_Keep_The_Chain()

        /// <summary>
        /// Users, groups and organizations are each locked on their own, so
        /// their lines are written at the same time: each must still name the
        /// line written before it in the file.
        /// </summary>
        [Test]
        public async Task Concurrent_Writers_Keep_The_Chain()
        {

            var api      = await StartAPI();
            var writers  = 8;
            var lines    = 50;
            var start    = new Barrier(writers);

            var tasks    = Enumerable.Range(0, writers).Select(writer => Task.Run(async () => {

                               start.SignalAndWait();

                               for (var line = 0; line < lines; line++)
                                   await api.WriteToDatabaseFile(
                                             NotificationMessageType.Parse("probe"),
                                             new JObject(
                                                 new JProperty("writer",  writer),
                                                 new JProperty("line",    line)
                                             ),
                                             EventTracking_Id.New
                                         );

                           })).ToArray();

            await Task.WhenAll(tasks);

            Assert.That(CommandsIn(HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName).Count(), Is.EqualTo(writers * lines));

            AssertChain(HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName);

            var restarted = await StartAPI();

            Assert.That(restarted.CurrentDatabaseHashValue,
                        Is.EqualTo(ChainOf(HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName).Last().Hash),
                        "a start goes on from the file's last line");

        }

        #endregion

        #region A_Line_That_Could_Not_Be_Written_Is_No_Parent()

        /// <summary>
        /// A line that never reached the file must not be named as the parent
        /// of the next one: nobody could find it.
        /// </summary>
        [Test]
        public async Task A_Line_That_Could_Not_Be_Written_Is_No_Parent()
        {

            var api = await StartAPI();

            await NewGroup(api, "admins");

            var databaseFile = Path.Combine(directory, "UsersAPI", HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName);

            // Held without sharing, the file refuses every retry of the write.
            using (new FileStream(databaseFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                await api.WriteToDatabaseFile(
                          NotificationMessageType.Parse("lost"),
                          new JObject(new JProperty("line", 2)),
                          EventTracking_Id.New
                      );
            }

            await NewGroup(api, "staff");

            Assert.That(CommandsIn(HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName),
                        Is.EqualTo(new[] { "addUserGroup", "addUserGroup" }),
                        "the lost line is not in the file");

            AssertChain(HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName);

        }

        #endregion

        #region Two_Names_Of_One_File_Are_One_Chain()

        /// <summary>
        /// A subclass may name the database file in its own way: it is still
        /// the same file, and the same chain.
        /// </summary>
        [Test]
        public async Task Two_Names_Of_One_File_Are_One_Chain()
        {

            var api = await StartAPI();

            await NewGroup(api, "admins");

            await api.WriteToDatabaseFile(
                      Path.Combine(directory, "UsersAPI", ".", HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName),
                      NotificationMessageType.Parse("probe"),
                      new JObject(new JProperty("line", 2)),
                      EventTracking_Id.New
                  );

            await NewGroup(api, "staff");

            Assert.That(CommandsIn(HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName),
                        Is.EqualTo(new[] { "addUserGroup", "probe", "addUserGroup" }));

            AssertChain(HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName);

        }

        #endregion

        #region A_Line_Without_A_Hash_Is_No_Parent()

        /// <summary>
        /// A line added by hand, without a hash, is read like any other, but
        /// the next line goes on from the last line that has one.
        /// </summary>
        [Test]
        public async Task A_Line_Without_A_Hash_Is_No_Parent()
        {

            var api = await StartAPI();

            await NewGroup(api, "admins");

            var databaseFile  = Path.Combine(directory, "UsersAPI", HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName);
            var handmade      = JObject.Parse(File.ReadLines(databaseFile).Single(line => line.StartsWith('{')));

            handmade.Remove("sha256hash");

            File.AppendAllText(databaseFile, handmade.ToString(Newtonsoft.Json.Formatting.None).Replace("\"admins\"", "\"handmade\"") + Environment.NewLine);

            var restarted = await StartAPI();

            Assert.That(restarted.TryGetUserGroup(UserGroup_Id.Parse("handmade"), out _), Is.True, "the line without a hash was not read");

            await NewGroup(restarted, "staff");

            var chain = ChainOf(HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName);

            Assert.That(chain.Select(link => link.Parent), Is.EqualTo(new[] { "", "", chain[0].Hash }));

        }

        #endregion

        #region A_Database_Of_One_Shared_Chain_Loads_And_Goes_On_Per_File()

        /// <summary>
        /// Files written before each had its own chain, with lines that name a
        /// line of another file as their parent: they load as before, and each
        /// goes on from its own last line.
        /// </summary>
        [Test]
        public async Task A_Database_Of_One_Shared_Chain_Loads_And_Goes_On_Per_File()
        {

            var api = await StartAPI();

            await NewUser (api, "hank", Password);
            await NewGroup(api, "admins");

            // Linked as one shared chain did: addUser, addPassword, addUserGroup.
            var addUser      = ChainOf(HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName)[0].Hash;
            var addPassword  = Relink(HTTPExtAPI.DefaultPasswordFile,                  0, addUser);
            var addGroup     = Relink(HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName,   1, addPassword);

            var restarted    = await StartAPI();

            Assert.Multiple(() => {
                Assert.That(restarted.VerifyPassword(User_Id.Parse("hank"), Password),       Is.True,  "the password did not load");
                Assert.That(restarted.TryGetUserGroup(UserGroup_Id.Parse("admins"), out _),  Is.True,  "the group did not load");
                Assert.That(restarted.CurrentDatabaseHashValue,                              Is.EqualTo(addGroup));
                Assert.That(restarted.HashValueOf(Path.Combine(directory, "UsersAPI", HTTPExtAPI.DefaultPasswordFile)),
                                                                                             Is.EqualTo(addPassword));
            });

            await NewGroup(restarted, "staff");

            var changed = await restarted.ChangePassword(UserOf(restarted, "hank"), NewPassword, Password);
            Assert.That(changed.Result, Is.EqualTo(CommandResult.Success), changed.Description.FirstText());

            Assert.Multiple(() => {
                Assert.That(ChainOf(HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName)[2].Parent,  Is.EqualTo(addGroup),     "the database file does not go on from its last line");
                Assert.That(ChainOf(HTTPExtAPI.DefaultPasswordFile)[1].Parent,                 Is.EqualTo(addPassword),  "the password file does not go on from its last line");
            });

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

            servers.Add(server);

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

        private static async Task NewGroup(HTTPExtAPI  API,
                                           String      Name)
        {

            var added = await API.AddUserGroup(
                                  new UserGroup(
                                      UserGroup_Id.Parse(Name),
                                      I18NString.Create(Languages.en, Name)
                                  )
                              );

            Assert.That(added.Result, Is.EqualTo(CommandResult.Success), added.Description.FirstText());

        }

        private static async Task AddReset(HTTPExtAPI  API,
                                           IUser       User)
        {

            var added = await API.AddPasswordReset(
                                  new PasswordReset(
                                      User,
                                      SecurityToken_Id.Random()
                                  ),
                                  SuppressNotifications: true
                              );

            Assert.That(added.Result, Is.EqualTo(CommandResult.Success), added.Description.FirstText());

        }

        /// <summary>
        /// The commands in the given database file of this test, in the order
        /// they were written.
        /// </summary>
        private IEnumerable<String> CommandsIn(String FileName)

            => ChainOf(FileName).Select(link => link.Command);

        /// <summary>
        /// The JSON lines of the given database file of this test: their
        /// command, the parent hash they name, the hash they claim and the
        /// hash they have.
        /// </summary>
        private IReadOnlyList<(String Command, String Parent, String Hash, String Computed)> ChainOf(String FileName)

            => File.ReadLines(Path.Combine(directory, "UsersAPI", FileName)).
                    Where (line => line.StartsWith('{')).
                    Select(line => {

                        var json    = JObject.Parse(line);
                        var hash    = json["sha256hash"]?["hashValue"]?. Value<String>() ?? "";
                        var parent  = json["sha256hash"]?["parentHash"]?.Value<String>() ?? "";

                        // The hash is taken over the line as it was before its
                        // own hash value was added, as the last property of
                        // "sha256hash", the last property of the line.
                        var hashed  = line.Replace($",\"hashValue\":\"{hash}\"}}}}", "}}");

                        var computed = Convert.ToHexStringLower(SHA256.HashData(Encoding.Unicode.GetBytes(hashed)));

                        return (((JProperty) json.First!).Name, parent, hash, computed);

                    }).
                    ToArray();

        /// <summary>
        /// Give the line with the given index of the given database file of
        /// this test another parent, and the hash that goes with it.
        /// </summary>
        /// <returns>The line's new hash.</returns>
        private String Relink(String  FileName,
                              Int32   Index,
                              String  Parent)
        {

            var path    = Path.Combine(directory, "UsersAPI", FileName);
            var lines   = File.ReadAllLines(path);
            var jsonAt  = lines.Select((line, at) => (line, at)).Where(line => line.line.StartsWith('{')).ElementAt(Index).at;
            var link    = ChainOf(FileName)[Index];

            var hashed  = lines[jsonAt].Replace($",\"hashValue\":\"{link.Hash}\"}}}}",  "}}").
                                        Replace($"\"parentHash\":\"{link.Parent}\"",    $"\"parentHash\":\"{Parent}\"");

            var hash    = Convert.ToHexStringLower(SHA256.HashData(Encoding.Unicode.GetBytes(hashed)));

            lines[jsonAt] = hashed[..^2] + $",\"hashValue\":\"{hash}\"}}}}";

            File.WriteAllLines(path, lines);

            Assert.That(ChainOf(FileName)[Index].Hash, Is.EqualTo(ChainOf(FileName)[Index].Computed));

            return hash;

        }

        /// <summary>
        /// Every line of the given database file names the hash of the line
        /// before it as its parent, the first none, and carries its own hash.
        /// </summary>
        private void AssertChain(String FileName)
        {

            var chain = ChainOf(FileName);

            Assert.That(chain, Is.Not.Empty, $"{FileName} has no lines");

            Assert.Multiple(() => {

                for (var i = 0; i < chain.Count; i++)
                {

                    var expectedParent = i == 0 ? "" : chain[i - 1].Hash;

                    Assert.That(chain[i].Parent,  Is.EqualTo(expectedParent),   $"{FileName} line {i + 1} ({chain[i].Command}) names a parent that is not the line before it");
                    Assert.That(chain[i].Hash,    Is.EqualTo(chain[i].Computed), $"{FileName} line {i + 1} ({chain[i].Command}) claims a hash it does not have");

                }

            });

        }

        #endregion

    }

}
