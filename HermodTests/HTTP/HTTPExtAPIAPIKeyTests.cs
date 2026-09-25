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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Mail;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// An API key comes back from a restart as it went in: expired when it had
    /// expired, not yet valid when it was not yet valid, and revocable - over
    /// HTTP as well, where the answer says whether it was revoked.
    /// </summary>
    /// <remarks>
    /// None of it held. The validity window was written as "notBefore" and
    /// "notAfter" and read back as "NotBefore" and "NotAfter", so every start
    /// gave an expired key its life back, and this time without an end. A key
    /// read back from an "addAPIKey" line was not attached to the API that
    /// read it, and RemoveAPIKey refuses a key that is not the API's own. And
    /// the route that revokes a key did not ask RemoveAPIKey how it went: it
    /// answered 200 with the key, which went on opening the door.
    /// </remarks>
    [TestFixture]
    public class HTTPExtAPIAPIKeyTests
    {

        #region Data

        private const String Password = "Correct-Horse-1";

        private String directory = "";

        #endregion

        #region SetUp / TearDown

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), $"hermod-apikeys-{Guid.NewGuid():N}") + Path.DirectorySeparatorChar;
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }

        #endregion


        #region An_Expired_Key_Stays_Expired_After_A_Restart()

        /// <summary>
        /// Next to a key that has not expired, which opens the door before the
        /// restart and after it: a test that only watched the door stay shut
        /// would pass just as well if no key could open it at all.
        /// </summary>
        [Test]
        public async Task An_Expired_Key_Stays_Expired_After_A_Restart()
        {

            var api        = await StartAPI();
            var alice      = await NewUser(api, "alice");
            var anHourAgo  = FromNow(TimeSpan.FromHours(-1));
            var inADay     = FromNow(TimeSpan.FromDays(1));

            var expired    = await NewAPIKey(api, new APIKey(APIKey_Id.Parse("alices-old-key-0123456789abcdef"), alice.Id, NotAfter: anHourAgo));
            var current    = await NewAPIKey(api, new APIKey(APIKey_Id.Parse("alices-new-key-0123456789abcdef"), alice.Id, NotAfter: inADay));

            Assert.Multiple(() => {
                Assert.That(KeyedInAs(api, expired),  Is.Null,             "the expired key opens the door");
                Assert.That(KeyedInAs(api, current),  Is.EqualTo("alice"));
            });

            var restarted  = await StartAPI();
            var reExpired  = HeldKey(restarted, expired);
            var reCurrent  = HeldKey(restarted, current);

            Assert.Multiple(() => {
                Assert.That(KeyedInAs(restarted, expired),  Is.Null,               "the next start gave the expired key its life back");
                Assert.That(KeyedInAs(restarted, current),  Is.EqualTo("alice"));
                Assert.That(reExpired.NotAfter,             Is.EqualTo(anHourAgo),  "the next start forgot when the key expired");
                Assert.That(reCurrent.NotAfter,             Is.EqualTo(inADay),     "the next start forgot when the key expires");
            });

        }

        #endregion

        #region A_Key_Not_Yet_Valid_Is_Not_Valid_After_A_Restart_Either()

        [Test]
        public async Task A_Key_Not_Yet_Valid_Is_Not_Valid_After_A_Restart_Either()
        {

            var api        = await StartAPI();
            var alice      = await NewUser(api, "alice");
            var anHourAgo  = FromNow(TimeSpan.FromHours(-1));
            var inAnHour   = FromNow(TimeSpan.FromHours(1));

            var early      = await NewAPIKey(api, new APIKey(APIKey_Id.Parse("alices-next-key-0123456789abcdef"), alice.Id, NotBefore: inAnHour));
            var started    = await NewAPIKey(api, new APIKey(APIKey_Id.Parse("alices-this-key-0123456789abcdef"), alice.Id, NotBefore: anHourAgo));

            Assert.Multiple(() => {
                Assert.That(KeyedInAs(api, early),    Is.Null,             "the key opens the door before its time");
                Assert.That(KeyedInAs(api, started),  Is.EqualTo("alice"));
            });

            var restarted  = await StartAPI();
            var reEarly    = HeldKey(restarted, early);
            var reStarted  = HeldKey(restarted, started);

            Assert.Multiple(() => {
                Assert.That(KeyedInAs(restarted, early),    Is.Null,               "the next start let the key in before its time");
                Assert.That(KeyedInAs(restarted, started),  Is.EqualTo("alice"));
                Assert.That(reEarly.  NotBefore,            Is.EqualTo(inAnHour),   "the next start forgot when the key begins");
                Assert.That(reStarted.NotBefore,            Is.EqualTo(anHourAgo),  "the next start forgot when the key began");
            });

        }

        #endregion

        #region TryParse_Reads_The_Window_ToJSON_Writes()

        /// <summary>
        /// The first two without a database file in between - and the spelling
        /// that is written, which is what every database file written so far
        /// holds, and so the one to read.
        /// </summary>
        [Test]
        public async Task TryParse_Reads_The_Window_ToJSON_Writes()
        {

            var api    = await StartAPI();
            var alice  = await NewUser(api, "alice");
            var from   = FromNow(TimeSpan.FromHours(-1));
            var until  = FromNow(TimeSpan.FromDays(1));

            var json   = new APIKey(APIKey_Id.Parse("alices-key-0123456789abcdef"), alice.Id, NotBefore: from, NotAfter: until).ToJSON();

            Assert.Multiple(() => {
                Assert.That(json.ContainsKey("notBefore"),  Is.True,  json.ToString());
                Assert.That(json.ContainsKey("notAfter"),   Is.True,  json.ToString());
            });

            Assert.That(APIKey.TryParse(json, api.TryGetUser, out var parsed, out var errorResponse), Is.True, errorResponse);

            Assert.Multiple(() => {
                Assert.That(parsed!.NotBefore,  Is.EqualTo(from),   "not valid before, as read back");
                Assert.That(parsed. NotAfter,   Is.EqualTo(until),  "not valid after, as read back");
            });

        }

        #endregion

        #region An_Update_Leaves_The_Window_As_It_Was()

        /// <summary>
        /// A key renamed through its builder keeps the window it had. The
        /// builder gave a key without a beginning one - the moment of the
        /// update - and now that the window survives a restart, a rename would
        /// have changed the key for good.
        /// </summary>
        [Test]
        public async Task An_Update_Leaves_The_Window_As_It_Was()
        {

            var api       = await StartAPI();
            var alice     = await NewUser(api, "alice");
            var inADay    = FromNow(TimeSpan.FromDays(1));
            var keyId     = await NewAPIKey(api, new APIKey(APIKey_Id.Parse("alices-key-0123456789abcdef"), alice.Id, NotAfter: inADay));

            await Rename(api, keyId, "renamed");

            var updated   = HeldKey(api, keyId);

            Assert.Multiple(() => {
                Assert.That(updated.Description.FirstText(),  Is.EqualTo("renamed"));
                Assert.That(updated.NotBefore,                Is.Null,             "the update gave the key a beginning");
                Assert.That(updated.NotAfter,                 Is.EqualTo(inADay),  "the update moved the end of the key");
            });

            var reloaded  = HeldKey(await StartAPI(), keyId);

            Assert.Multiple(() => {
                Assert.That(reloaded.Description.FirstText(),  Is.EqualTo("renamed"));
                Assert.That(reloaded.NotBefore,                Is.Null,             "the next start gave the key a beginning");
                Assert.That(reloaded.NotAfter,                 Is.EqualTo(inADay),  "the next start forgot when the key expires");
            });

        }

        #endregion


        #region A_Key_Read_Back_After_A_Restart_Can_Be_Removed()

        [Test]
        public async Task A_Key_Read_Back_After_A_Restart_Can_Be_Removed()
        {

            var api        = await StartAPI();
            var alice      = await NewUser(api, "alice");
            var keyId      = await NewAPIKey(api, new APIKey(APIKey_Id.Parse("alices-key-0123456789abcdef"), alice.Id));

            var restarted  = await StartAPI();

            Assert.That(KeyedInAs(restarted, keyId), Is.EqualTo("alice"), "the next start lost the key");

            var removed    = await restarted.RemoveAPIKey(HeldKey(restarted, keyId));

            Assert.That(removed.Result, Is.EqualTo(CommandResult.Success), removed.Description.FirstText());

            Assert.Multiple(() => {
                Assert.That(restarted.TryGetAPIKey(keyId, out _),  Is.False,  "the removed key is still there");
                Assert.That(KeyedInAs(restarted, keyId),           Is.Null,   "the removed key still opens the door");
            });

            var once_more  = await StartAPI();

            Assert.That(once_more.TryGetAPIKey(keyId, out _), Is.False, "the next start brought the removed key back");

        }

        #endregion

        #region A_Key_Read_Back_Can_Be_Removed_However_It_Was_Written()

        /// <summary>
        /// Each way of writing a key has a line of its own, and each kind of
        /// line is read back by a case of its own. Only "addAPIKey" left its
        /// key unattached; this keeps the other three from going the same way.
        /// </summary>
        [Test]
        public async Task A_Key_Read_Back_Can_Be_Removed_However_It_Was_Written()
        {

            var api             = await StartAPI();
            var alice           = await NewUser(api, "alice");

            var added           = await NewAPIKey(api, new APIKey(APIKey_Id.Parse("added-key-0123456789abcdef"),     alice.Id));
            var replaced        = await NewAPIKey(api, new APIKey(APIKey_Id.Parse("replaced-key-0123456789abcdef"),  alice.Id));
            var renamed         = await NewAPIKey(api, new APIKey(APIKey_Id.Parse("renamed-key-0123456789abcdef"),   alice.Id));
            var addedIfNew      = APIKey_Id.Parse("added-if-new-key-0123456789abcdef");
            var addedOrUpdated  = APIKey_Id.Parse("added-or-updated-key-0123456789abcdef");

            Assert.That((await api.AddAPIKeyIfNotExists(new APIKey(addedIfNew,      alice.Id))).Result,                                           Is.EqualTo(CommandResult.Success));
            Assert.That((await api.AddOrUpdateAPIKey   (new APIKey(addedOrUpdated,  alice.Id))).Result,                                           Is.EqualTo(CommandResult.Success));
            Assert.That((await api.AddOrUpdateAPIKey   (new APIKey(replaced,        alice.Id, Description: I18NString.Create("replaced")))).Result,  Is.EqualTo(CommandResult.Success));

            await Rename(api, renamed, "renamed");

            var restarted       = await StartAPI();
            var removals        = new Dictionary<String, DeleteAPIKeyResult>();

            foreach (var keyId in new[] { added, addedIfNew, addedOrUpdated, replaced, renamed })
                removals[keyId.ToString()] = await restarted.RemoveAPIKey(HeldKey(restarted, keyId));

            Assert.Multiple(() => {
                foreach (var (keyId, removed) in removals)
                    Assert.That(removed.Result,  Is.EqualTo(CommandResult.Success),  $"{keyId}: {removed.Description.FirstText()}");
            });

        }

        #endregion

        #region A_Key_Updated_Through_Its_Builder_Can_Be_Updated_And_Removed()

        /// <summary>
        /// Before any restart: the key an update builds is a new object, and
        /// that was not attached either. After one update the API would neither
        /// update the key again nor remove it - until a restart attached it.
        /// </summary>
        [Test]
        public async Task A_Key_Updated_Through_Its_Builder_Can_Be_Updated_And_Removed()
        {

            var api      = await StartAPI();
            var alice    = await NewUser(api, "alice");
            var keyId    = await NewAPIKey(api, new APIKey(APIKey_Id.Parse("alices-key-0123456789abcdef"), alice.Id));

            await Rename(api, keyId, "renamed");
            await Rename(api, keyId, "renamed again");

            var removed  = await api.RemoveAPIKey(HeldKey(api, keyId));

            Assert.That(removed.Result, Is.EqualTo(CommandResult.Success), removed.Description.FirstText());

            Assert.Multiple(() => {
                Assert.That(api.TryGetAPIKey(keyId, out _),  Is.False,  "the removed key is still there");
                Assert.That(KeyedInAs(api, keyId),           Is.Null,   "the removed key still opens the door");
            });

        }

        #endregion


        #region A_Key_Revoked_Over_HTTP_After_A_Restart_Is_Gone()

        /// <summary>
        /// DELETE users/{UserId}/APIKeys/{APIKeyId}, as the owner, after a
        /// restart: the key is gone, and stays gone after the next one.
        /// </summary>
        [Test]
        public async Task A_Key_Revoked_Over_HTTP_After_A_Restart_Is_Gone()
        {

            var keyId                  = APIKey_Id.Parse("hanks-key-0123456789abcdef");
            var (server, api, client)  = await StartServer();

            try
            {

                var hank = await NewAccount(api, "hank");

                await NewAPIKey(api, new APIKey(keyId, hank.Id));

            }
            finally
            {
                await Stop(server, client);
            }

            (server, api, client) = await StartServer();

            try
            {

                Assert.That(KeyedInAs(api, keyId), Is.EqualTo("hank"), "the next start lost the key");

                var (status, body) = await Revoke(client, "hank", keyId);

                Assert.Multiple(() => {
                    Assert.That(status,                        Is.EqualTo(HttpStatusCode.OK),  body);
                    Assert.That(api.TryGetAPIKey(keyId, out _),  Is.False,                       "revoked, said the route, and the key is still there");
                    Assert.That(KeyedInAs(api, keyId),           Is.Null,                        "revoked, said the route, and the key still opens the door");
                });

            }
            finally
            {
                await Stop(server, client);
            }

            var once_more = await StartAPI();

            Assert.That(once_more.TryGetAPIKey(keyId, out _), Is.False, "the next start brought the revoked key back");

        }

        #endregion

        #region The_Route_Says_So_When_It_Could_Not_Revoke_A_Key()

        /// <summary>
        /// Whatever RemoveAPIKey refuses, the route passes the refusal on: here
        /// a key held but not attached, as every key read back from an
        /// "addAPIKey" line used to be. It is still there, it still opens the
        /// door, and the answer says as much.
        /// </summary>
        [Test]
        public async Task The_Route_Says_So_When_It_Could_Not_Revoke_A_Key()
        {

            var (server, api, client) = await StartServer();

            try
            {

                var hank   = await NewAccount(api, "hank");
                var keyId  = APIKey_Id.Parse("hanks-key-0123456789abcdef");

                Assert.That(api.apiKeys.TryAdd(keyId, new APIKey(keyId, hank.Id)),  Is.True);
                Assert.That(KeyedInAs(api, keyId),                                   Is.EqualTo("hank"));

                var (status, body) = await Revoke(client, "hank", keyId);

                Assert.Multiple(() => {
                    Assert.That(status,                        Is.EqualTo(HttpStatusCode.FailedDependency),  "revoked, says the route: " + body);
                    Assert.That(body,                          Does.Contain("not attached"),                 "the answer does not say why");
                    Assert.That(api.TryGetAPIKey(keyId, out _),  Is.True);
                    Assert.That(KeyedInAs(api, keyId),           Is.EqualTo("hank"));
                });

            }
            finally
            {
                await Stop(server, client);
            }

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
                             MinUserIdLength:       3
                         );

            await api.LoadDatabase();

            return api;

        }

        /// <summary>
        /// The same on a running HTTP server, with the routes that manage the
        /// accounts - DELETE users/{UserId}/APIKeys/{APIKeyId} among them.
        /// </summary>
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

            await Server.Stop();

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
                                 // HTTP Basic Auth insists on one.
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
            var acme  = new Organization(Organization_Id.Parse("acme"), I18NString.Create("ACME"));

            Assert.That((await API.AddOrganization(acme)).Result,                                                     Is.EqualTo(CommandResult.Success));
            Assert.That((await API.AddUserToOrganization(user, User2OrganizationEdgeLabel.IsMember, acme)).IsSuccess,  Is.True);

            return user;

        }

        private static async Task<APIKey_Id> NewAPIKey(HTTPExtAPI  API,
                                                       APIKey      APIKey)
        {

            var added = await API.AddAPIKey(APIKey);

            Assert.That(added.Result, Is.EqualTo(CommandResult.Success), added.Description.FirstText());

            return APIKey.Id;

        }

        /// <summary>
        /// The API key the given API holds under the given identification.
        /// </summary>
        private static APIKey HeldKey(HTTPExtAPI  API,
                                      APIKey_Id   APIKeyId)
        {

            Assert.That(API.TryGetAPIKey(APIKeyId, out var apiKey) && apiKey is not null, Is.True, $"There is no API key '{APIKeyId}'.");

            return apiKey!;

        }

        /// <summary>
        /// Give the held key a new description through its builder.
        /// </summary>
        private static async Task Rename(HTTPExtAPI  API,
                                         APIKey_Id   APIKeyId,
                                         String      Description)
        {

            var updated = await API.UpdateAPIKey(
                                    HeldKey(API, APIKeyId),
                                    builder => builder.Description = I18NString.Create(Description)
                                );

            Assert.That(updated.Result, Is.EqualTo(CommandResult.Success), updated.Description.FirstText());

        }

        /// <summary>
        /// DELETE users/{UserId}/APIKeys/{APIKeyId} as that user, with HTTP
        /// Basic Auth - which, unlike a session, needs nothing carried over a
        /// restart but the password.
        /// </summary>
        private static async Task<(HttpStatusCode Status, String Body)> Revoke(HttpClient  Client,
                                                                               String      UserId,
                                                                               APIKey_Id   APIKeyId)
        {

            using var request = new HttpRequestMessage(HttpMethod.Delete, $"accounts/users/{UserId}/APIKeys/{APIKeyId}");

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new AuthenticationHeaderValue(
                                                "Basic",
                                                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{UserId}:{Password}"))
                                            );

            using var response = await Client.SendAsync(request);

            return (response.StatusCode, await response.Content.ReadAsStringAsync());

        }

        /// <summary>
        /// Who a request with the given API key is signed in as, if anybody:
        /// the way in every guarded route takes.
        /// </summary>
        private static String? KeyedInAs(HTTPExtAPI  API,
                                         APIKey_Id   APIKeyId)

            => API.TryGetHTTPUser(RequestWith($"API-Key: {APIKeyId}"), out var user)
                   ? user?.Id.ToString()
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
        /// The given time from now, in whole seconds: the database file writes
        /// milliseconds, and what it reads back is compared with this.
        /// </summary>
        private static DateTimeOffset FromNow(TimeSpan Offset)

            => DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.Add(Offset).ToUnixTimeSeconds());

        #endregion

    }

}
