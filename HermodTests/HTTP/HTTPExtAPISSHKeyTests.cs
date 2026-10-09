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

using System.Net;
using System.Text;
using System.Net.Http.Headers;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// The SSH keys of a user, kept by the HTTPExt API: let in with their
    /// authorized_keys line and its options, known by their fingerprints,
    /// written to the database file with who let them in - and read back from
    /// it at the next start, taken away with their user, refused with a reason
    /// where a line cannot be held to, and managed over HTTP by their user, or
    /// whoever may impersonate it.
    /// </summary>
    [TestFixture]
    public class HTTPExtAPISSHKeyTests
    {

        #region Data

        private const String Password = "Correct-Horse-1";

        private String directory = "";

        private readonly List<HTTPServer> servers = [];

        #endregion

        #region SetUp / TearDown

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), $"hermod-sshkeys-{Guid.NewGuid():N}") + Path.DirectorySeparatorChar;
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


        #region A_Key_Let_In_Is_There_After_A_Restart_With_Its_Options

        /// <summary>
        /// A key let in is found by the key an SSH client offers, and comes back
        /// from a restart with its line - from= and all - its label, when it was
        /// let in and by whom.
        /// </summary>
        [Test]
        public async Task A_Key_Let_In_Is_There_After_A_Restart_With_Its_Options()
        {

            var key   = SshHostKey.GenerateEd25519();
            var line  = $"from=\"192.0.2.0/24\" {KeyLine(key)}";

            var api   = await StartAPI();
            var hank  = await NewUser(api, "hank");
            var added = await api.AddSSHKey(hank, line, "Hank's laptop", CurrentUserId: User_Id.Parse("root"));

            Assert.That(added.Outcome, Is.EqualTo(AddSSHKeyOutcome.Added), added.Reason);
            Assert.That(api.FindSSHKey(hank.Id, key.PublicKeyBlob, DateTimeOffset.UtcNow), Is.Not.Null, "not found before the restart");

            var again = await StartAPI();
            var found = again.FindSSHKey(hank.Id, key.PublicKeyBlob, DateTimeOffset.UtcNow);

            Assert.That(found, Is.Not.Null, "the next start lost the key");

            Assert.Multiple(() => {
                Assert.That(found!.Fingerprint,                            Is.EqualTo(SshFingerprint.Sha256(key.PublicKeyBlob)));
                Assert.That(found.Line,                                    Is.EqualTo(line));
                Assert.That(found.Label,                                   Is.EqualTo("Hank's laptop"));
                Assert.That(found.CreatedBy,                               Is.EqualTo("root"));
                Assert.That(found.Created,                                 Is.EqualTo(added.SSHKey!.Created).Within(TimeSpan.FromSeconds(1)));
                Assert.That(found.Key.Restrictions.SourceAddresses,        Is.Not.Null.And.Not.Empty, "from= was lost");
                Assert.That(again.GetSSHKeys(hank.Id).Count,               Is.EqualTo(1));
            });

        }

        #endregion

        #region A_Key_Removed_Lets_Nobody_In_Now_And_After_A_Restart

        [Test]
        public async Task A_Key_Removed_Lets_Nobody_In_Now_And_After_A_Restart()
        {

            var key   = SshHostKey.GenerateEd25519();
            var other = SshHostKey.GenerateEd25519();

            var api   = await StartAPI();
            var hank  = await NewUser(api, "hank");

            await api.AddSSHKey(hank, KeyLine(key));
            await api.AddSSHKey(hank, KeyLine(other));

            Assert.That(await api.RemoveSSHKey(hank.Id, SshFingerprint.Sha256(key.PublicKeyBlob)),  Is.True);
            Assert.That(await api.RemoveSSHKey(hank.Id, SshFingerprint.Sha256(key.PublicKeyBlob)),  Is.False, "removed twice");

            Assert.That(api.FindSSHKey(hank.Id, key.PublicKeyBlob, DateTimeOffset.UtcNow), Is.Null, "the key removed still lets hank in");

            var again = await StartAPI();

            Assert.Multiple(() => {
                Assert.That(again.FindSSHKey(hank.Id, key.  PublicKeyBlob, DateTimeOffset.UtcNow), Is.Null,     "the next start brought the key removed back");
                Assert.That(again.FindSSHKey(hank.Id, other.PublicKeyBlob, DateTimeOffset.UtcNow), Is.Not.Null, "the other key went with it");
            });

        }

        #endregion

        #region A_Line_That_Cannot_Be_Held_To_Is_Refused_And_Written_Nowhere

        [Test]
        public async Task A_Line_That_Cannot_Be_Held_To_Is_Refused_And_Written_Nowhere()
        {

            var key   = SshHostKey.GenerateEd25519();

            var api   = await StartAPI();
            var hank  = await NewUser(api, "hank");

            var permitopen  = await api.AddSSHKey(hank, $"permitopen=\"10.0.0.1:80\" {KeyLine(key)}");
            var privateKey  = await api.AddSSHKey(hank, OpenSshPrivateKey.Format(key));
            var unknown     = await api.AddSSHKey(new User(User_Id.Parse("nobody"), I18NString.Create("nobody"), SimpleEMailAddress.Parse("nobody@example.test")), KeyLine(key));

            Assert.Multiple(() => {
                Assert.That(permitopen.Outcome,  Is.EqualTo(AddSSHKeyOutcome.Refused));
                Assert.That(permitopen.Reason,   Does.Contain("'permitopen' cannot be held to"));
                Assert.That(privateKey.Outcome,  Is.EqualTo(AddSSHKeyOutcome.Refused));
                Assert.That(privateKey.Reason,   Does.Contain("private key"));
                Assert.That(unknown.Outcome,     Is.EqualTo(AddSSHKeyOutcome.UnknownUser));
                Assert.That(api.GetSSHKeys(hank.Id), Is.Empty);
                Assert.That(DatabaseLines(), Has.None.Contains("addSSHKey"), "a refused line was written");
            });

        }

        #endregion

        #region The_Same_Key_Twice_Is_There_Once

        [Test]
        public async Task The_Same_Key_Twice_Is_There_Once()
        {

            var key   = SshHostKey.GenerateEd25519();

            var api   = await StartAPI();
            var hank  = await NewUser(api, "hank");

            var first   = await api.AddSSHKey(hank, KeyLine(key), "first");
            var second  = await api.AddSSHKey(hank, $"no-pty {KeyLine(key)}", "second");

            Assert.Multiple(() => {
                Assert.That(first.Outcome,           Is.EqualTo(AddSSHKeyOutcome.Added));
                Assert.That(second.Outcome,          Is.EqualTo(AddSSHKeyOutcome.AlreadyThere));
                Assert.That(second.SSHKey!.Label,    Is.EqualTo("first"), "the key there is not the one answered");
                Assert.That(api.GetSSHKeys(hank.Id).Count,                                   Is.EqualTo(1));
                Assert.That(DatabaseLines().Count(line => line.Contains("\"addSSHKey\"")),  Is.EqualTo(1));
            });

        }

        #endregion

        #region A_Key_Goes_With_Its_User

        /// <summary>
        /// Deleting a user takes its keys, with a "removeSSHKey" line for each:
        /// the next account made under the same id, after a restart too, signs
        /// in with none of them.
        /// </summary>
        [Test]
        public async Task A_Key_Goes_With_Its_User()
        {

            var key   = SshHostKey.GenerateEd25519();

            var api   = await StartAPI();
            var hank  = await NewUser(api, "hank");

            await api.AddSSHKey(hank, KeyLine(key));

            var deleted = await api.DeleteUser(hank);

            Assert.That(deleted.Result, Is.EqualTo(CommandResult.Success), deleted.Description.FirstText());

            Assert.Multiple(() => {
                Assert.That(api.GetSSHKeys(hank.Id),                               Is.Empty);
                Assert.That(DatabaseLines().Count(line => line.Contains("\"removeSSHKey\"")), Is.EqualTo(1));
            });

            var again = await StartAPI();
            var newHank = await NewUser(again, "hank");

            Assert.That(again.FindSSHKey(newHank.Id, key.PublicKeyBlob, DateTimeOffset.UtcNow), Is.Null, "the new hank signs in with the old hank's key");

        }

        #endregion

        #region A_Key_Signs_In_Only_Within_Its_Time_And_Not_As_A_CA

        [Test]
        public async Task A_Key_Signs_In_Only_Within_Its_Time_And_Not_As_A_CA()
        {

            var expired  = SshHostKey.GenerateEd25519();
            var later    = SshHostKey.GenerateEd25519();
            var ca       = SshHostKey.GenerateEd25519();

            var api      = await StartAPI();
            var hank     = await NewUser(api, "hank");
            var now      = DateTimeOffset.UtcNow;

            await api.AddSSHKey(hank, $"expiry-time=\"{now.AddDays(-1):yyyyMMdd}\" {KeyLine(expired)}");
            await api.AddSSHKey(hank, $"not-before=\"{now.AddDays(2):yyyyMMdd}\" {KeyLine(later)}");
            await api.AddSSHKey(hank, $"cert-authority {KeyLine(ca)}");

            Assert.Multiple(() => {
                Assert.That(api.GetSSHKeys(hank.Id).Count,                               Is.EqualTo(3));
                Assert.That(api.FindSSHKey(hank.Id, expired.PublicKeyBlob, now),         Is.Null, "expired");
                Assert.That(api.FindSSHKey(hank.Id, later.  PublicKeyBlob, now),         Is.Null, "not yet");
                Assert.That(api.FindSSHKey(hank.Id, later.  PublicKeyBlob, now.AddDays(3)), Is.Not.Null, "not after its not-before either");
                Assert.That(api.FindSSHKey(hank.Id, ca.     PublicKeyBlob, now),         Is.Null, "a CA key signs in as a key");
            });

        }

        #endregion

        #region A_Key_Switched_Off_Lets_Nobody_In_Until_Switched_On_After_A_Restart_Too

        /// <summary>
        /// A key switched off stays with its user, said to be off, and lets
        /// nobody in - after a restart too - until it is switched on again.
        /// Switching it the way it is already writes nothing; a key that is not
        /// there is not switched.
        /// </summary>
        [Test]
        public async Task A_Key_Switched_Off_Lets_Nobody_In_Until_Switched_On_After_A_Restart_Too()
        {

            var key          = SshHostKey.GenerateEd25519();
            var other        = SshHostKey.GenerateEd25519();
            var fingerprint  = SshFingerprint.Sha256(key.PublicKeyBlob);

            var api          = await StartAPI();
            var hank         = await NewUser(api, "hank");

            await api.AddSSHKey(hank, KeyLine(key), "laptop");
            await api.AddSSHKey(hank, KeyLine(other));

            var off      = await api.DisableSSHKey(hank.Id, fingerprint, CurrentUserId: User_Id.Parse("root"));
            var twice    = await api.DisableSSHKey(hank.Id, fingerprint);
            var nothing  = await api.DisableSSHKey(hank.Id, SshFingerprint.Sha256(SshHostKey.GenerateEd25519().PublicKeyBlob));

            Assert.Multiple(() => {
                Assert.That(off?.IsDisabled,                                                    Is.True);
                Assert.That(off?.Label,                                                         Is.EqualTo("laptop"), "switching it off lost its label");
                Assert.That(twice?.IsDisabled,                                                  Is.True);
                Assert.That(nothing,                                                            Is.Null, "a key that is not there was switched");
                Assert.That(api.FindSSHKey(hank.Id, key.  PublicKeyBlob, DateTimeOffset.UtcNow), Is.Null,     "the key switched off lets hank in");
                Assert.That(api.FindSSHKey(hank.Id, other.PublicKeyBlob, DateTimeOffset.UtcNow), Is.Not.Null, "the other key went off with it");
                Assert.That(api.GetSSHKeys(hank.Id).Select(sshKey => sshKey.IsDisabled),         Is.EquivalentTo(new[] { true, false }), "the key switched off is gone, not off");
                Assert.That(DatabaseLines().Count(line => line.Contains("\"disableSSHKey\"")),    Is.EqualTo(1), "switched off twice, written twice");
                Assert.That(JObject.Parse(DatabaseLines().Single(line => line.Contains("\"disableSSHKey\"")))["userId"]?.Value<String>(),
                                                                                                Is.EqualTo("root"), "who switched it off is not written");
            });

            var again = await StartAPI();

            Assert.Multiple(() => {
                Assert.That(again.FindSSHKey(hank.Id, key.PublicKeyBlob, DateTimeOffset.UtcNow), Is.Null, "the next start switched the key on");
                Assert.That(again.TryGetSSHKey(hank.Id, fingerprint, out var held) && held.IsDisabled, Is.True, "the next start lost that it is off");
            });

            var on = await again.EnableSSHKey(hank.Id, fingerprint);

            Assert.Multiple(() => {
                Assert.That(on?.IsDisabled,                                                        Is.False);
                Assert.That(again.FindSSHKey(hank.Id, key.PublicKeyBlob, DateTimeOffset.UtcNow),   Is.Not.Null, "switched on, and the key lets nobody in");
            });

            var third = await StartAPI();

            Assert.That(third.FindSSHKey(hank.Id, key.PublicKeyBlob, DateTimeOffset.UtcNow), Is.Not.Null, "the next start switched the key off again");

        }

        #endregion

        #region A_Key_Added_And_Removed_Are_Links_Of_The_Hash_Chain

        /// <summary>
        /// The lines a key writes are links of the database file's hash chain:
        /// its removal names the hash of its adding as its parent.
        /// </summary>
        /// <remarks>
        /// Not asked: that the chain runs line by line through the whole file.
        /// It does not, before any SSH key - a password written elsewhere moves
        /// it on between two lines of this file, and a restart goes on from the
        /// last file read rather than from this one's last line.
        /// </remarks>
        [Test]
        public async Task A_Key_Added_And_Removed_Are_Links_Of_The_Hash_Chain()
        {

            var key   = SshHostKey.GenerateEd25519();

            var api   = await StartAPI();
            var hank  = await NewUser(api, "hank");

            await api.AddSSHKey(hank, KeyLine(key), CurrentUserId: User_Id.Parse("root"));
            await api.RemoveSSHKey(hank.Id, SshFingerprint.Sha256(key.PublicKeyBlob), CurrentUserId: User_Id.Parse("hank"));

            var lines   = DatabaseLines().Select(JObject.Parse).ToArray();
            var added   = lines.Single(line => line.ContainsKey("addSSHKey"));
            var removed = lines.Single(line => line.ContainsKey("removeSSHKey"));

            Assert.Multiple(() => {
                Assert.That(added  ["sha256hash"]?["hashValue"]?.Value<String>(),   Has.Length.EqualTo(64));
                Assert.That(added  ["sha256hash"]?["parentHash"]?.Value<String>(),  Is.Not.Empty);
                Assert.That(removed["sha256hash"]?["parentHash"]?.Value<String>(),  Is.EqualTo(added["sha256hash"]?["hashValue"]?.Value<String>()));
                Assert.That(added  ["userId"]?.Value<String>(),                     Is.EqualTo("root"), "who added it is not written");
                Assert.That(removed["userId"]?.Value<String>(),                     Is.EqualTo("hank"), "who removed it is not written");
            });

        }

        #endregion


        #region The_Owner_Lists_Adds_And_Revokes_Over_HTTP

        [Test]
        public async Task The_Owner_Lists_Adds_And_Revokes_Over_HTTP()
        {

            var key                    = SshHostKey.GenerateEd25519();
            var fingerprint            = SshFingerprint.Sha256(key.PublicKeyBlob);
            var (server, api, client)  = await StartServer();

            try
            {

                await NewAccount(api, "hank");

                var added    = await Send(client, "ADD", "hank", "users/hank/SSHKeys", new JObject(new JProperty("line", KeyLine(key)), new JProperty("label", "laptop")));
                var twice    = await Send(client, "ADD", "hank", "users/hank/SSHKeys", new JObject(new JProperty("line", KeyLine(key))));
                var refused  = await Send(client, "ADD", "hank", "users/hank/SSHKeys", new JObject(new JProperty("line", $"permitopen=\"10.0.0.1:80\" {KeyLine(SshHostKey.GenerateEd25519())}")));
                var listed   = await Send(client, "GET", "hank", "users/hank/SSHKeys");

                Assert.Multiple(() => {
                    Assert.That(added.Status,    Is.EqualTo(HttpStatusCode.OK),         added.Body);
                    Assert.That(JObject.Parse(added.Body)["fingerprint"]?.Value<String>(), Is.EqualTo(fingerprint));
                    Assert.That(twice.Status,    Is.EqualTo(HttpStatusCode.Conflict),   twice.Body);
                    Assert.That(refused.Status,  Is.EqualTo(HttpStatusCode.BadRequest), refused.Body);
                    Assert.That(refused.Body,    Does.Contain("permitopen"));
                    Assert.That(listed.Status,   Is.EqualTo(HttpStatusCode.OK),         listed.Body);
                    Assert.That(JArray.Parse(listed.Body).Select(entry => entry["label"]?.Value<String>()), Is.EqualTo(new[] { "laptop" }));
                });

                var revoked  = await Send(client, "DELETE", "hank", $"users/hank/SSHKeys/{UserSSHKey.FingerprintInURL(fingerprint)}");
                var again    = await Send(client, "DELETE", "hank", $"users/hank/SSHKeys/{UserSSHKey.FingerprintInURL(fingerprint)}");

                Assert.Multiple(() => {
                    Assert.That(revoked.Status,  Is.EqualTo(HttpStatusCode.OK),       revoked.Body);
                    Assert.That(again.Status,    Is.EqualTo(HttpStatusCode.NotFound), again.Body);
                    Assert.That(api.FindSSHKey(User_Id.Parse("hank"), key.PublicKeyBlob, DateTimeOffset.UtcNow), Is.Null, "revoked, said the route, and the key still lets hank in");
                    Assert.That(api.GetSSHKeys(User_Id.Parse("hank")), Is.Empty);
                });

            }
            finally
            {
                await Stop(server, client);
            }

        }

        #endregion

        #region Another_User_May_Not_Unless_It_May_Impersonate

        /// <summary>
        /// The keys of hank are hank's: kim gets 403 for listing, adding and
        /// revoking them - until kim is an admin of the API's admin
        /// organization, which may impersonate everybody, as with API keys.
        /// </summary>
        [Test]
        public async Task Another_User_May_Not_Unless_It_May_Impersonate()
        {

            var key                    = SshHostKey.GenerateEd25519();
            var (server, api, client)  = await StartServer();

            try
            {

                var hank  = await NewAccount(api, "hank");
                var kim   = await NewAccount(api, "kim");

                await api.AddSSHKey(hank, KeyLine(key));

                var path  = $"users/hank/SSHKeys/{UserSSHKey.FingerprintInURL(SshFingerprint.Sha256(key.PublicKeyBlob))}";

                var list   = await Send(client, "GET",    "kim", "users/hank/SSHKeys");
                var add    = await Send(client, "ADD",    "kim", "users/hank/SSHKeys", new JObject(new JProperty("line", KeyLine(SshHostKey.GenerateEd25519()))));
                var revoke = await Send(client, "DELETE", "kim", path);

                Assert.Multiple(() => {
                    Assert.That(list.  Status,  Is.EqualTo(HttpStatusCode.Forbidden), list.  Body);
                    Assert.That(add.   Status,  Is.EqualTo(HttpStatusCode.Forbidden), add.   Body);
                    Assert.That(revoke.Status,  Is.EqualTo(HttpStatusCode.Forbidden), revoke.Body);
                    Assert.That(api.GetSSHKeys(hank.Id).Count, Is.EqualTo(1));
                });

                var admins = new Organization(api.AdminOrganizationId, I18NString.Create("Admins"));

                Assert.That((await api.AddOrganization(admins)).Result,                                                    Is.EqualTo(CommandResult.Success));
                Assert.That((await api.AddUserToOrganization(kim, User2OrganizationEdgeLabel.IsAdmin, admins)).IsSuccess,  Is.True);

                var listAsAdmin   = await Send(client, "GET",    "kim", "users/hank/SSHKeys");
                var revokeAsAdmin = await Send(client, "DELETE", "kim", path);

                Assert.Multiple(() => {
                    Assert.That(listAsAdmin.  Status,  Is.EqualTo(HttpStatusCode.OK), listAsAdmin.  Body);
                    Assert.That(revokeAsAdmin.Status,  Is.EqualTo(HttpStatusCode.OK), revokeAsAdmin.Body);
                    Assert.That(api.GetSSHKeys(hank.Id), Is.Empty);
                });

            }
            finally
            {
                await Stop(server, client);
            }

        }

        #endregion

        #region The_Owner_Switches_A_Key_Off_And_On_Over_HTTP

        /// <summary>
        /// SET users/{UserId}/SSHKeys/{Fingerprint} with "isDisabled": the
        /// owner switches a key off and on, and the list says which it is.
        /// Nobody else may, a key that is not there is 404, and a request that
        /// does not say which way is 400.
        /// </summary>
        [Test]
        public async Task The_Owner_Switches_A_Key_Off_And_On_Over_HTTP()
        {

            var key                    = SshHostKey.GenerateEd25519();
            var path                   = $"users/hank/SSHKeys/{UserSSHKey.FingerprintInURL(SshFingerprint.Sha256(key.PublicKeyBlob))}";
            var (server, api, client)  = await StartServer();

            try
            {

                var hank  = await NewAccount(api, "hank");
                await NewAccount(api, "kim");

                await api.AddSSHKey(hank, KeyLine(key));

                var off      = await Send(client, "SET", "hank", path, new JObject(new JProperty("isDisabled", true)));
                var offDoor  = api.FindSSHKey(hank.Id, key.PublicKeyBlob, DateTimeOffset.UtcNow);
                var listed   = await Send(client, "GET", "hank", "users/hank/SSHKeys");

                var on       = await Send(client, "SET", "hank", path, new JObject(new JProperty("isDisabled", false)));
                var onDoor   = api.FindSSHKey(hank.Id, key.PublicKeyBlob, DateTimeOffset.UtcNow);

                var byKim    = await Send(client, "SET", "kim",  path, new JObject(new JProperty("isDisabled", true)));
                var unsaid   = await Send(client, "SET", "hank", path, new JObject());
                var unknown  = await Send(client, "SET", "hank", $"users/hank/SSHKeys/{UserSSHKey.FingerprintInURL(SshFingerprint.Sha256(SshHostKey.GenerateEd25519().PublicKeyBlob))}",
                                          new JObject(new JProperty("isDisabled", true)));

                Assert.Multiple(() => {
                    Assert.That(off.Status,                                                      Is.EqualTo(HttpStatusCode.OK),          off.Body);
                    Assert.That(JObject.Parse(off.Body)["isDisabled"]?.Value<Boolean>(),          Is.True,                                "the answer does not say it is off");
                    Assert.That(offDoor,                                                         Is.Null,                                "switched off, said the route, and the key lets hank in");
                    Assert.That(listed.Status,                                                   Is.EqualTo(HttpStatusCode.OK),          listed.Body);
                    Assert.That(JArray.Parse(listed.Body).Single()["isDisabled"]?.Value<Boolean>(), Is.True,                             "the list does not say it is off");
                    Assert.That(on.Status,                                                       Is.EqualTo(HttpStatusCode.OK),          on.Body);
                    Assert.That(onDoor,                                                          Is.Not.Null,                            "switched on, said the route, and the key lets nobody in");
                    Assert.That(byKim.Status,                                                    Is.EqualTo(HttpStatusCode.Forbidden),   byKim.Body);
                    Assert.That(unsaid.Status,                                                   Is.EqualTo(HttpStatusCode.BadRequest),  unsaid.Body);
                    Assert.That(unknown.Status,                                                  Is.EqualTo(HttpStatusCode.NotFound),    unknown.Body);
                    Assert.That(api.FindSSHKey(hank.Id, key.PublicKeyBlob, DateTimeOffset.UtcNow), Is.Not.Null,                         "kim switched it off");
                });

            }
            finally
            {
                await Stop(server, client);
            }

        }

        #endregion

        #region A_Fingerprint_Goes_Through_A_URL_And_Back

        [Test]
        public void A_Fingerprint_Goes_Through_A_URL_And_Back()
        {

            for (var i = 0; i < 64; i++)
            {

                var fingerprint = SshFingerprint.Sha256(SshHostKey.GenerateEd25519().PublicKeyBlob);
                var inURL       = UserSSHKey.FingerprintInURL(fingerprint);

                Assert.Multiple(() => {
                    Assert.That(inURL,                                   Does.Not.Contain("/").And.Not.Contain("+").And.Not.Contain(":"));
                    Assert.That(UserSSHKey.FingerprintFromURL(inURL),    Is.EqualTo(fingerprint));
                    Assert.That(UserSSHKey.FingerprintFromURL(Uri.EscapeDataString(fingerprint)), Is.EqualTo(fingerprint), "as OpenSSH writes it");
                });

            }

        }

        #endregion


        #region (private) Helpers

        private static String KeyLine(ISshHostKey Key)
            => SshPublicKey.FromHostKey(Key, "tester").ToAuthorizedKeyLine();

        /// <summary>
        /// The lines of the database file, as they stand.
        /// </summary>
        private String[] DatabaseLines()
        {

            var file = Directory.GetFiles(directory, "*.db", SearchOption.AllDirectories).
                                 Single(path => Path.GetFileName(path) == HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName);

            return [.. File.ReadAllLines(file).Select(line => line.TrimStart('\uFEFF').Trim()).Where(line => line.StartsWith('{'))];

        }

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
                             MinUserIdLength:       3
                         );

            await api.LoadDatabase();

            return api;

        }

        private async Task<(HTTPServer Server, HTTPExtAPI API, HttpClient Client)> StartServer()
        {

            var server  = await HTTPServer.StartNew();

            var api     = new HTTPExtAPI(
                              server,
                              RootPath:              HTTPPath.Parse("/accounts"),
                              DisableNotifications:  true,
                              LoggingPath:           directory,
                              MinUserIdLength:       3
                          );

            await api.LoadDatabase();

            var client  = new HttpClient() {
                              BaseAddress = new Uri($"http://127.0.0.1:{server.TCPPort}/")
                          };

            return (server, api, client);

        }

        private static async Task Stop(HTTPServer  Server,
                                       HttpClient  Client)
        {
            Client.Dispose();
            await Server.DisposeAsync();
        }

        private static async Task<User> NewUser(HTTPExtAPI  API,
                                                String      Name)
        {

            var user = await API.CreateUser(
                                 User_Id.Parse(Name),
                                 I18NString.Create(Name),
                                 SimpleEMailAddress.Parse($"{Name}@example.test"),
                                 Password,
                                 SkipDefaultNotifications:  true,
                                 SkipNewUserEMail:          true,
                                 SkipNewUserNotifications:  true,
                                 AcceptedEULA:              DateTimeOffset.UtcNow.AddDays(-1),
                                 IsAuthenticated:           true
                             );

            Assert.That(user, Is.Not.Null, $"'{Name}' could not be created");
            Assert.That(API.TryGetUser(User_Id.Parse(Name), out var created) && created is User, Is.True, $"There is no user '{Name}'.");

            return (User) created!;

        }

        /// <summary>
        /// A user the routes under users/ let in: they turn away an account
        /// that is in no organization.
        /// </summary>
        private static async Task<User> NewAccount(HTTPExtAPI  API,
                                                   String      Name)
        {

            var user  = await NewUser(API, Name);
            var acme  = API.GetOrganization(Organization_Id.Parse("acme")) as Organization
                            ?? new Organization(Organization_Id.Parse("acme"), I18NString.Create("ACME"));

            if (!API.OrganizationExists(acme.Id))
                Assert.That((await API.AddOrganization(acme)).Result, Is.EqualTo(CommandResult.Success));

            Assert.That((await API.AddUserToOrganization(user, User2OrganizationEdgeLabel.IsMember, acme)).IsSuccess, Is.True);

            return user;

        }

        /// <summary>
        /// A request as the given user, with HTTP Basic Auth.
        /// </summary>
        private static async Task<(HttpStatusCode Status, String Body)> Send(HttpClient  Client,
                                                                             String      Method,
                                                                             String      UserId,
                                                                             String      Path,
                                                                             JObject?    Body = null)
        {

            using var request = new HttpRequestMessage(new HttpMethod(Method), $"accounts/{Path}");

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new AuthenticationHeaderValue(
                                                "Basic",
                                                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{UserId}:{Password}"))
                                            );

            if (Body is not null)
                request.Content = new StringContent(Body.ToString(), Encoding.UTF8, "application/json");

            using var response = await Client.SendAsync(request);

            return (response.StatusCode, await response.Content.ReadAsStringAsync());

        }

        #endregion

    }

}
