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
using System.Diagnostics;
using System.Net.Http.Headers;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Mail;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// The API key commands hand back what they did: the ADD route answers
    /// whether it added the key, an update hands back the key it made, and
    /// the valid keys of a user come back - and at once.
    /// </summary>
    [TestFixture]
    public class HTTPExtAPIAPIKeyResultTests
    {

        #region Data

        private const String Password  = "Correct-Horse-1";

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


        #region A_Refused_API_Key_Is_Not_Answered_As_Added()

        /// <summary>
        /// ADD ~/users/{UserId}/APIKeys answers 200 only for a key it added:
        /// one that is there already is a conflict, one that is too short a
        /// bad request - each with the reason.
        /// </summary>
        /// <remarks>
        /// The route asked whether AddAPIKey had returned a result, and it
        /// always does. So a refused key was answered 200 OK with the key, as
        /// if it had been added, and a client went away holding a key that did
        /// not work - or, here, one whose rights it had not been given. The
        /// refusal the route did have would not have said why either: its
        /// description came from the JSON parsing, which had succeeded.
        /// </remarks>
        [Test]
        public async Task A_Refused_API_Key_Is_Not_Answered_As_Added()
        {

            var (server, api, client) = await StartHTTPAPI();

            try
            {

                var cookies  = await SignIn(api, client, "hank");
                var keyId    = APIKey_Id.Parse("hanks-first-key-0123456789abcdef");

                var (status, body) = await AddOverHTTP(client, cookies, "hank", KeyJSON(keyId.ToString(), "hank", "readOnly"));

                Assert.That(status,                              Is.EqualTo(HttpStatusCode.OK),  body);
                Assert.That(api.GetAPIKey(keyId)?.AccessRights,  Is.EqualTo(APIKeyRights.ReadOnly));

                // The same key again, asking for more: the API holds that key
                // already, and keeps it as it was.
                (status, body) = await AddOverHTTP(client, cookies, "hank", KeyJSON(keyId.ToString(), "hank", "readWrite"));

                Assert.Multiple(() => {
                    Assert.That(status,                              Is.EqualTo(HttpStatusCode.Conflict),  body);
                    Assert.That(DescriptionIn(body),                 Does.Contain("already exists"));
                    Assert.That(api.GetAPIKey(keyId)?.AccessRights,  Is.EqualTo(APIKeyRights.ReadOnly));
                });

                // Shorter than MinAPIKeyLength: AddAPIKey refuses it, and the answer says why.
                (status, body) = await AddOverHTTP(client, cookies, "hank", KeyJSON("hanks-short-key", "hank", "readOnly"));

                Assert.Multiple(() => {
                    Assert.That(status,                                                          Is.EqualTo(HttpStatusCode.BadRequest),  body);
                    Assert.That(DescriptionIn(body),                                             Does.Contain("too short"));
                    Assert.That(api.TryGetAPIKey(APIKey_Id.Parse("hanks-short-key"), out _),    Is.False);
                });

            }
            finally
            {
                client.Dispose();
                await server.Stop();
            }

        }

        #endregion

        #region The_Valid_Keys_Of_A_User_Are_Found_At_Once()

        /// <summary>
        /// A user with a valid key and an expired one: the valid one comes
        /// back, and at once.
        /// </summary>
        /// <remarks>
        /// GetValidAPIKeysForUser holds the API key lock while it looks, and
        /// asked the public APIKeyIsValid about each key - which waits for the
        /// same lock. SemaphoreSlim is not reentrant, so every key of the user
        /// waited out the lock timeout of 30 seconds and was called invalid:
        /// a minute for these two, and an empty list at the end of it.
        /// </remarks>
        [Test]
        public async Task The_Valid_Keys_Of_A_User_Are_Found_At_Once()
        {

            var api      = await StartAPI();
            var alice    = await NewUser(api, "alice");

            var valid    = new APIKey(APIKey_Id.Parse("alices-valid-key-0123456789abcdef"),    alice.Id);
            var expired  = new APIKey(APIKey_Id.Parse("alices-expired-key-0123456789abcdef"),  alice.Id, NotAfter: DateTimeOffset.UtcNow.AddDays(-1));

            Assert.That((await api.AddAPIKey(valid)).  Result,  Is.EqualTo(CommandResult.Success));
            Assert.That((await api.AddAPIKey(expired)).Result,  Is.EqualTo(CommandResult.Success));

            var stopwatch  = Stopwatch.StartNew();
            var found      = api.GetValidAPIKeysForUser(alice).Select(apiKey => apiKey.Id.ToString()).ToArray();

            stopwatch.Stop();

            Assert.Multiple(() => {
                Assert.That(found,              Is.EqualTo(new[] { "alices-valid-key-0123456789abcdef" }),  "the valid key, and only that one");
                Assert.That(stopwatch.Elapsed,  Is.LessThan(TimeSpan.FromSeconds(5)),                       "waited for the lock it held itself");
            });

        }

        #endregion

        #region An_Update_Hands_Back_The_Key_It_Made()

        /// <summary>
        /// Updated through a delegate, the result holds the key as it is now -
        /// the one the API holds - rather than the one it replaced.
        /// </summary>
        [Test]
        public async Task An_Update_Hands_Back_The_Key_It_Made()
        {

            var api    = await StartAPI();
            var alice  = await NewUser(api, "alice");
            var keyId  = APIKey_Id.Parse("alices-only-key-0123456789abcdef");

            Assert.That((await api.AddAPIKey(new APIKey(keyId, alice.Id))).Result,  Is.EqualTo(CommandResult.Success));

            var updated = await api.UpdateAPIKey(api.GetAPIKey(keyId)!,
                                                 builder => builder.IsDisabled = true);

            Assert.That(updated.Result,  Is.EqualTo(CommandResult.Success),  updated.Description.FirstText());

            Assert.Multiple(() => {
                Assert.That(updated.APIKey,              Is.SameAs(api.GetAPIKey(keyId)),  "the result holds a key the API no longer holds");
                Assert.That(updated.APIKey?.IsDisabled,  Is.True,                           "the result holds the key as it was before the update");
            });

        }

        #endregion


        #region (private) Helpers

        /// <summary>
        /// A headless HTTPExtAPI on this test's directory.
        /// </summary>
        private async Task<HTTPExtAPI> StartAPI()
        {

            var api = new HTTPExtAPI(
                          new HTTPServer(
                              IPAddress:  IPv4Address.Localhost,
                              TCPPort:    IPPort.Zero,
                              AutoStart:  false
                          ),
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
        /// An HTTPExtAPI with its routes - ~/users/{UserId}/APIKeys among them -
        /// on a real HTTP server, and a client that sends JSON to it.
        /// </summary>
        private async Task<(HTTPServer Server, HTTPExtAPI API, HttpClient Client)> StartHTTPAPI()
        {

            var server  = await HTTPServer.StartNew();

            var api     = new HTTPExtAPI(
                              server,
                              RootPath:              HTTPPath.Parse("/accounts"),
                              SkipURLTemplates:      false,
                              DisableNotifications:  true,
                              LoggingPath:           directory,
                              MinUserIdLength:       3,
                              HTTPCookiePath:        "/",
                              UseSecureCookies:      false
                          );

            await api.LoadDatabase();

            // No cookie jar: SignIn hands over the session cookies, and each
            // request sends them.
            var client  = new HttpClient(new HttpClientHandler { UseCookies = false }) {
                              BaseAddress = new Uri($"http://127.0.0.1:{server.TCPPort}/")
                          };

            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            return (server, api, client);

        }

        private static async Task<IUser> NewUser(HTTPExtAPI  API,
                                                 String      Name)
        {

            var user = await API.CreateUser(
                                 User_Id.Parse(Name),
                                 I18NString.Create(Name),
                                 SimpleEMailAddress.Parse($"{Name}@example.test"),
                                 SkipDefaultNotifications:  true,
                                 SkipNewUserEMail:          true,
                                 SkipNewUserNotifications:  true
                             );

            Assert.That(user, Is.Not.Null, $"'{Name}' could not be created");

            return user!;

        }

        /// <summary>
        /// A new account in an organization - the routes under users/ answer
        /// 401 to one in none - signed in: the session cookies to send along.
        /// </summary>
        private static async Task<String> SignIn(HTTPExtAPI  API,
                                                 HttpClient  Client,
                                                 String      Name)
        {

            var user = await API.CreateUserIfNotExists(
                                 User_Id.Parse(Name),
                                 I18NString.Create(Name),
                                 SimpleEMailAddress.Parse($"{Name}@example.test"),
                                 Password:                  Password,
                                 IsAuthenticated:           true,
                                 SkipDefaultNotifications:  true,
                                 SkipNewUserEMail:          true,
                                 SkipNewUserNotifications:  true
                             );

            Assert.That(user, Is.Not.Null, $"'{Name}' could not be created");

            var acme = new Organization(Organization_Id.Parse("acme"), I18NString.Create("ACME"));

            Assert.That((await API.AddOrganization(acme)).Result,                                                          Is.EqualTo(CommandResult.Success));
            Assert.That((await API.AddUserToOrganization(user!, User2OrganizationEdgeLabel.IsMember, acme)).IsSuccess,  Is.True);

            using var response = await Client.PostAsync(
                                           "accounts/auth/login",
                                           new StringContent(
                                               new JObject(
                                                   new JProperty("login",     Name),
                                                   new JProperty("password",  Password)
                                               ).ToString(),
                                               Encoding.UTF8,
                                               "application/json"
                                           )
                                       );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());

            return String.Join("; ", response.Headers.GetValues("Set-Cookie").
                                                      Select(setCookie => setCookie.Split(';')[0]));

        }

        /// <summary>
        /// An API key as a client sends it to ADD ~/users/{UserId}/APIKeys.
        /// </summary>
        private static JObject KeyJSON(String  KeyId,
                                       String  UserId,
                                       String  AccessRights)

            => new (
                   new JProperty("@id",           KeyId),
                   new JProperty("@context",      APIKey.DefaultJSONLDContext.ToString()),
                   new JProperty("userId",        UserId),
                   new JProperty("accessRights",  AccessRights),
                   new JProperty("created",       DateTimeOffset.UtcNow.ToString("o"))
               );

        private static async Task<(HttpStatusCode Status, String Body)> AddOverHTTP(HttpClient  Client,
                                                                                   String      Cookies,
                                                                                   String      UserId,
                                                                                   JObject     APIKey)
        {

            using var request = new HttpRequestMessage(new HttpMethod("ADD"), $"accounts/users/{UserId}/APIKeys") {
                                    Content = new StringContent(APIKey.ToString(), Encoding.UTF8, "application/json")
                                };

            request.Headers.Add("Cookie", Cookies);

            using var response = await Client.SendAsync(request);

            return (response.StatusCode,
                    await response.Content.ReadAsStringAsync());

        }

        /// <summary>
        /// The "description" of a JSON answer, as text; empty when there is none.
        /// </summary>
        private static String DescriptionIn(String Body)
        {
            try
            {
                return JObject.Parse(Body)["description"]?.ToString() ?? "";
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return "";
            }
        }

        #endregion

    }

}
