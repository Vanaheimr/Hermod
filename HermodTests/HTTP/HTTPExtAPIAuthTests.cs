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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.Passkeys;
using org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.Passkeys;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// The JSON account routes of the HTTPExtAPI over a real HTTP server:
    /// self sign-up, sign-in and sign-out with the session cookies, the
    /// signed-in account, its password and display name.
    /// </summary>
    [TestFixture]
    public class HTTPExtAPIAuthTests
    {

        #region (private class) Browser

        /// <summary>
        /// A JSON client with a cookie jar: what a single-page application does.
        /// </summary>
        private sealed class Browser(HttpClient Client)
        {

            public Dictionary<String, String> Cookies { get; } = [];

            /// <summary>
            /// The body of the last response, for failure messages.
            /// </summary>
            public String LastBody { get; private set; } = "";

            public Boolean HasSession
                => Cookies.Keys.Any(name => name.EndsWith("Session", StringComparison.Ordinal));

            public async Task<(HttpStatusCode Status, JObject? JSON)> Call(HttpMethod  Method,
                                                                          String      Path,
                                                                          Object?     Body = null)
            {

                var request = new HttpRequestMessage(Method, Path);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                if (Body is not null)
                    request.Content = new StringContent(JsonConvert.SerializeObject(Body), Encoding.UTF8, "application/json");

                if (Cookies.Count > 0)
                    request.Headers.Add("Cookie", String.Join("; ", Cookies.Select(cookie => $"{cookie.Key}={cookie.Value}")));

                var response = await Client.SendAsync(request);

                if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
                {
                    foreach (var setCookie in setCookies)
                    {

                        var pair    = setCookie.Split(';')[0];
                        var equals  = pair.IndexOf('=');
                        var name    = pair[..equals];
                        var value   = pair[(equals + 1)..];
                        var expired = setCookie.Contains("Expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase);

                        if (value.Length == 0 || expired)
                            Cookies.Remove(name);
                        else
                            Cookies[name] = value;

                    }
                }

                var text = await response.Content.ReadAsStringAsync();
                LastBody = text;

                JObject? json = null;

                try
                {
                    if (text.Length > 0)
                        json = JObject.Parse(text);
                }
                catch (JsonException)
                { }

                return (response.StatusCode, json);

            }

        }

        #endregion

        #region (private) StartAsync()

        private static async Task<(HTTPServer Server, HTTPExtAPI API, HttpClient Client, String Directory)> StartAsync(Boolean WithTemplates = false)
        {

            var directory  = Path.Combine(Path.GetTempPath(), $"hermod-auth-{Guid.NewGuid():N}") + Path.DirectorySeparatorChar;
            var server     = await HTTPServer.StartNew();
            var origin     = $"http://localhost:{server.TCPPort}";

            var api        = new HTTPExtAPI(
                                 server,
                                 RootPath:              HTTPPath.Parse("/accounts"),
                                 SkipURLTemplates:      !WithTemplates,
                                 DisableNotifications:  true,
                                 LoggingPath:           directory,
                                 MinUserIdLength:       3,
                                 MinUserNameLength:     1,
                                 HTTPCookiePath:        "/",
                                 UseSecureCookies:      false,
                                 WebAuthnSettings:      new WebAuthnSettings("localhost", "Hermod Tests", [ origin ])
                             );

            await api.LoadDatabase();

            _ = new SelfSignUpAPI(api);

            // No automatic cookie jar: the Browser class above sends the cookies deliberately.
            var client = new HttpClient(new HttpClientHandler { UseCookies = false }) {
                             BaseAddress = new Uri($"http://127.0.0.1:{server.TCPPort}/")
                         };

            return (server, api, client, directory);

        }

        private static async Task StopAsync(HTTPServer Server, HttpClient Client, String DataDirectory)
        {

            Client.Dispose();
            await Server.Stop();

            if (Directory.Exists(DataDirectory))
                Directory.Delete(DataDirectory, recursive: true);

        }

        private static IUser UserOf(HTTPExtAPI  API,
                                    String      Username)

            => API.TryGetUser(User_Id.Parse(Username), out var user) && user is not null
                   ? user
                   : throw new InvalidOperationException($"Unknown user '{Username}'!");

        private static Task<(HttpStatusCode Status, JObject? JSON)> SignUp(Browser  Browser,
                                                                          String   Username,
                                                                          String   Password     = "Correct-Horse-1",
                                                                          String?  DisplayName  = null)

            => Browser.Call(HttpMethod.Post, "accounts/auth/signup", new {
                   username     = Username,
                   email        = $"{Username}@example.test",
                   password     = Password,
                   displayName  = DisplayName
               });

        #endregion


        #region SignUp_SignsIn_And_Refuses_Duplicates()

        [Test]
        public async Task SignUp_SignsIn_And_Refuses_Duplicates()
        {

            var (server, api, client, directory) = await StartAsync();

            try
            {

                var browser = new Browser(client);

                var (status, json) = await SignUp(browser, "alice", DisplayName: "Alice");

                Assert.That(status,                               Is.EqualTo(HttpStatusCode.Created));
                Assert.That(json?["user"]?["id"]?.ToString(),     Is.EqualTo("alice"));
                Assert.That(json?["user"]?["displayName"]?.ToString(),  Is.EqualTo("Alice"));
                Assert.That(json?["session"]?["expiresAt"],       Is.Not.Null);
                Assert.That(browser.HasSession,                   Is.True,  "the sign-up sets the session cookie");
                Assert.That(api.Sessions.CountForUser(User_Id.Parse("alice")),  Is.EqualTo(1));

                (status, json) = await browser.Call(HttpMethod.Get, "accounts/auth/me");

                Assert.That(status,                                    Is.EqualTo(HttpStatusCode.OK));
                Assert.That(json?["user"]?["username"]?.ToString(),    Is.EqualTo("alice"));
                Assert.That(json?["user"]?["email"]?.ToString(),       Is.EqualTo("alice@example.test"));
                Assert.That(json?["user"]?["passkeys"]?.Value<Int32>(), Is.EqualTo(0));
                Assert.That(json?["activeSessions"]?.Value<Int32>(),   Is.EqualTo(1));

                // The username is unique ignoring case, so is the e-mail address.
                var other = new Browser(client);

                (status, json) = await other.Call(HttpMethod.Post, "accounts/auth/signup", new { username = "ALICE", email = "other@example.test", password = "Correct-Horse-1" });
                Assert.That(status,  Is.EqualTo(HttpStatusCode.Conflict));

                (status, json) = await other.Call(HttpMethod.Post, "accounts/auth/signup", new { username = "alice2", email = "Alice@example.test", password = "Correct-Horse-1" });
                Assert.That(status,  Is.EqualTo(HttpStatusCode.Conflict));

                (status, json) = await other.Call(HttpMethod.Post, "accounts/auth/signup", new { username = "bad name", email = "bad@example.test", password = "Correct-Horse-1" });
                Assert.That(status,  Is.EqualTo(HttpStatusCode.BadRequest));
                Assert.That(json?["description"]?.ToString(),  Does.Contain("username"));

                (status, json) = await other.Call(HttpMethod.Post, "accounts/auth/signup", new { username = "weak", email = "weak@example.test", password = "short" });
                Assert.That(status,  Is.EqualTo(HttpStatusCode.BadRequest));
                Assert.That(json?["description"]?.ToString(),  Does.Contain("password"));

                Assert.That(other.HasSession,  Is.False);

            }
            finally
            {
                await StopAsync(server, client, directory);
            }

        }

        #endregion

        #region Login_And_Logout_With_The_Session_Cookies()

        [Test]
        public async Task Login_And_Logout_With_The_Session_Cookies()
        {

            var (server, api, client, directory) = await StartAsync();

            try
            {

                Assert.That((await SignUp(new Browser(client), "bob")).Status,  Is.EqualTo(HttpStatusCode.Created));

                var browser = new Browser(client);

                var (status, json) = await browser.Call(HttpMethod.Post, "accounts/auth/login", new { login = "bob", password = "Wrong-Horse-1" });
                Assert.That(status,                             Is.EqualTo(HttpStatusCode.Unauthorized));
                Assert.That(json?["description"]?.ToString(),   Is.EqualTo("Unknown login or wrong password."));
                Assert.That(browser.HasSession,                 Is.False);

                (status, json) = await browser.Call(HttpMethod.Post, "accounts/auth/login", new { login = "nobody", password = "Correct-Horse-1" });
                Assert.That(status,  Is.EqualTo(HttpStatusCode.Unauthorized));

                (status, json) = await browser.Call(HttpMethod.Get, "accounts/auth/me");
                Assert.That(status,                             Is.EqualTo(HttpStatusCode.Unauthorized));
                Assert.That(json?["description"]?.ToString(),   Is.EqualTo("Sign in required."));

                // The e-mail address works as login too, ignoring case.
                (status, json) = await browser.Call(HttpMethod.Post, "accounts/auth/login", new { login = "Bob@Example.test", password = "Correct-Horse-1" });
                Assert.That(status,                                     Is.EqualTo(HttpStatusCode.OK));
                Assert.That(json?["user"]?["id"]?.ToString(),           Is.EqualTo("bob"));
                Assert.That(json?["user"]?["lastLoginAt"]?.Type,        Is.Not.EqualTo(JTokenType.Null));
                Assert.That(browser.HasSession,                         Is.True);
                Assert.That(browser.Cookies,                            Has.Count.EqualTo(2),  "the account data cookie and the session cookie");

                (status, json) = await browser.Call(HttpMethod.Get, "accounts/auth/me");
                Assert.That(status,  Is.EqualTo(HttpStatusCode.OK));

                (status, _) = await browser.Call(HttpMethod.Post, "accounts/auth/logout");
                Assert.That(status,              Is.EqualTo(HttpStatusCode.NoContent));
                Assert.That(browser.HasSession,  Is.False,  "the sign-out expires the cookies");
                Assert.That(api.Sessions.CountForUser(User_Id.Parse("bob")),  Is.EqualTo(1),  "the session of the sign-up remains");

                (status, _) = await browser.Call(HttpMethod.Get, "accounts/auth/me");
                Assert.That(status,  Is.EqualTo(HttpStatusCode.Unauthorized));

            }
            finally
            {
                await StopAsync(server, client, directory);
            }

        }

        #endregion

        #region PasswordChange_Ends_The_Other_Sessions()

        [Test]
        public async Task PasswordChange_Ends_The_Other_Sessions()
        {

            var (server, api, client, directory) = await StartAsync();

            try
            {

                var laptop = new Browser(client);
                var phone  = new Browser(client);

                Assert.That((await SignUp(laptop, "carol")).Status,  Is.EqualTo(HttpStatusCode.Created));
                Assert.That((await phone.Call(HttpMethod.Post, "accounts/auth/login", new { login = "carol", password = "Correct-Horse-1" })).Status,  Is.EqualTo(HttpStatusCode.OK));
                Assert.That(api.Sessions.CountForUser(User_Id.Parse("carol")),  Is.EqualTo(2));

                var (status, json) = await laptop.Call(HttpMethod.Post, "accounts/auth/password", new { currentPassword = "Wrong-Horse-1", newPassword = "Correct-Horse-2" });
                Assert.That(status,  Is.EqualTo(HttpStatusCode.Forbidden));

                (status, json) = await laptop.Call(HttpMethod.Post, "accounts/auth/password", new { currentPassword = "Correct-Horse-1", newPassword = "short" });
                Assert.That(status,                             Is.EqualTo(HttpStatusCode.BadRequest));
                Assert.That(json?["description"]?.ToString(),   Does.Contain("password"));

                (status, _) = await laptop.Call(HttpMethod.Post, "accounts/auth/password", new { currentPassword = "Correct-Horse-1", newPassword = "Correct-Horse-2" });
                Assert.That(status,  Is.EqualTo(HttpStatusCode.NoContent));

                Assert.That((await laptop.Call(HttpMethod.Get, "accounts/auth/me")).Status,  Is.EqualTo(HttpStatusCode.OK),            "the session that changed the password stays");
                Assert.That((await phone. Call(HttpMethod.Get, "accounts/auth/me")).Status,  Is.EqualTo(HttpStatusCode.Unauthorized),  "every other session is gone");

                Assert.That((await phone.Call(HttpMethod.Post, "accounts/auth/login", new { login = "carol", password = "Correct-Horse-1" })).Status,  Is.EqualTo(HttpStatusCode.Unauthorized));
                Assert.That((await phone.Call(HttpMethod.Post, "accounts/auth/login", new { login = "carol", password = "Correct-Horse-2" })).Status,  Is.EqualTo(HttpStatusCode.OK));

            }
            finally
            {
                await StopAsync(server, client, directory);
            }

        }

        #endregion

        #region DisplayName_Can_Be_Changed()

        [Test]
        public async Task DisplayName_Can_Be_Changed()
        {

            var (server, api, client, directory) = await StartAsync();

            try
            {

                var browser = new Browser(client);

                Assert.That((await SignUp(browser, "dave")).Status,  Is.EqualTo(HttpStatusCode.Created));

                var (status, json) = await browser.Call(HttpMethod.Put, "accounts/auth/me", new { displayName = "Dave L." });
                Assert.That(status,                                        Is.EqualTo(HttpStatusCode.OK));
                Assert.That(json?["user"]?["displayName"]?.ToString(),     Is.EqualTo("Dave L."));

                Assert.That(api.TryGetUser(User_Id.Parse("dave"), out var user),  Is.True);
                Assert.That(user!.Name.FirstText(),                                Is.EqualTo("Dave L."));

                (status, json) = await browser.Call(HttpMethod.Put, "accounts/auth/me", new { displayName = new String('x', HTTPExtAPI.MaxUserNameLength + 1) });
                Assert.That(status,  Is.EqualTo(HttpStatusCode.BadRequest));

                // Without a display name the account shows its username.
                (status, json) = await browser.Call(HttpMethod.Put, "accounts/auth/me", new { displayName = "" });
                Assert.That(status,                                        Is.EqualTo(HttpStatusCode.OK));
                Assert.That(json?["user"]?["displayName"]?.ToString(),     Is.EqualTo("dave"));

                Assert.That((await new Browser(client).Call(HttpMethod.Put, "accounts/auth/me", new { displayName = "Eve" })).Status,  Is.EqualTo(HttpStatusCode.Unauthorized));

            }
            finally
            {
                await StopAsync(server, client, directory);
            }

        }

        #endregion

        #region BasicAuth_IsRationedLikeTheSignInRoute()

        /// <summary>
        /// An "Authorization: Basic" header is reached from TryGetSignedInUser,
        /// which every guarded route calls - so it used to buy a full password
        /// verification on ANY route, as often as anybody cared to ask, while
        /// auth/login next door counted every attempt.
        /// </summary>
        /// <remarks>
        /// 600 000 rounds of PBKDF2 per request and an oracle that says whether
        /// a guess was right: the careful limiters were sidestepped by moving
        /// the credentials out of the body and into a header.
        ///
        /// What this asserts is behaviour and not a stopwatch: once the ration
        /// is spent, even the RIGHT credentials are refused. Before the fix that
        /// last request succeeded, which is the whole finding in one line.
        /// </remarks>
        [Test]
        public async Task BasicAuth_IsRationedLikeTheSignInRoute()
        {

            var (server, api, client, directory) = await StartAsync();

            try
            {

                await api.CreateUserIfNotExists(
                          User_Id.Parse("erin"),
                          I18NString.Create("Erin"),
                          SimpleEMailAddress.Parse("erin@example.test"),
                          Password:                  "Correct-Horse-7",
                          IsAuthenticated:           true,
                          // Basic auth wants one; without it this user could not
                          // sign in that way at all and the last assertion below
                          // would pass while proving nothing. Found by removing
                          // the ration and watching the test stay green.
                          AcceptedEULA:              DateTimeOffset.UtcNow.AddDays(-1),
                          SkipNewUserEMail:          true,
                          SkipNewUserNotifications:  true,
                          SkipDefaultNotifications:  true
                      );

                async Task<HttpStatusCode> WithBasicAuth(String Password)
                {

                    using var request = new HttpRequestMessage(HttpMethod.Get, "accounts/auth/me");

                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                                                        "Basic",
                                                        Convert.ToBase64String(Encoding.UTF8.GetBytes($"erin:{Password}"))
                                                    );

                    using var response = await client.SendAsync(request);

                    return response.StatusCode;

                }

                // The ration is ten a minute, and it is the same bucket the
                // sign-in route uses: guessing is guessing, whichever door.
                for (var attempt = 1; attempt <= 10; attempt++)
                    Assert.That(await WithBasicAuth("Wrong-Horse-" + attempt),
                                Is.EqualTo(HttpStatusCode.Unauthorized),
                                $"attempt {attempt}");

                Assert.That(await WithBasicAuth("Correct-Horse-7"),
                            Is.EqualTo(HttpStatusCode.Unauthorized),
                            "and once the ration is spent the right password does not get through either - which is what proves no hash was computed");

            }
            finally
            {
                await StopAsync(server, client, directory);
            }

        }

        #endregion

        #region AUserCreatedInCode_IsEnabledAndCanSignIn()

        /// <summary>
        /// A user made by CreateUserIfNotExists, rather than by signing up over
        /// HTTP, has to be the user that was asked for.
        /// </summary>
        /// <remarks>
        /// The two Booleans were handed to User's constructor in the wrong
        /// order - it takes IsDisabled first and IsAuthenticated second, and
        /// they were passed the other way round - so every user created as
        /// authenticated came out disabled instead and could not sign in. Both
        /// are Booleans, so nothing complained; the first sign-in simply said
        /// "Unknown login or wrong password", which is what it says for a user
        /// who is not there at all.
        ///
        /// The sign-in at the end is the point of the test. Asserting the two
        /// flags alone would pass again the day somebody swaps them back and
        /// also swaps what the assertion reads.
        /// </remarks>
        [Test]
        public async Task AUserCreatedInCode_IsEnabledAndCanSignIn()
        {

            var (server, api, client, directory) = await StartAsync();

            try
            {

                var created = await api.CreateUserIfNotExists(
                                        User_Id.Parse("dora"),
                                        I18NString.Create("Dora"),
                                        SimpleEMailAddress.Parse("dora@example.test"),
                                        Password:                  "Correct-Horse-9",
                                        IsAuthenticated:           true,
                                        SkipNewUserEMail:          true,
                                        SkipNewUserNotifications:  true,
                                        SkipDefaultNotifications:  true
                                    );

                Assert.That(created, Is.Not.Null);

                Assert.Multiple(() =>
                {
                    Assert.That(created!.IsDisabled,       Is.False, "asked for an enabled user and got one");
                    Assert.That(created!.IsAuthenticated,  Is.True,  "asked for an authenticated user and got one");
                });

                var browser         = new Browser(client);
                var (status, json)  = await browser.Call(HttpMethod.Post, "accounts/auth/login",
                                                         new { login = "dora", password = "Correct-Horse-9" });

                Assert.Multiple(() =>
                {
                    Assert.That(status,                          Is.EqualTo(HttpStatusCode.OK), "and can sign in, which is what the flags are for");
                    Assert.That(json?["user"]?["id"]?.ToString(), Is.EqualTo("dora"));
                    Assert.That(browser.HasSession,              Is.True);
                });

            }
            finally
            {
                await StopAsync(server, client, directory);
            }

        }

        #endregion

        #region Passkeys_Register_And_SignIn_Over_HTTP()

        [Test]
        public async Task Passkeys_Register_And_SignIn_Over_HTTP()
        {

            var (server, api, client, directory) = await StartAsync();

            try
            {

                using var authenticator = new WebAuthnTests.SoftwareAuthenticator();

                var origin   = $"http://localhost:{server.TCPPort}";
                var browser  = new Browser(client);
                var erin     = User_Id.Parse("erin");

                Assert.That((await SignUp(browser, "erin")).Status,  Is.EqualTo(HttpStatusCode.Created));

                // Registration: options for the signed-in account, then the answer of the authenticator.
                var (status, json) = await browser.Call(HttpMethod.Post, "accounts/auth/passkeys/register/options", new { });
                Assert.That(status,                                              Is.EqualTo(HttpStatusCode.OK));
                Assert.That(json?["publicKey"]?["rp"]?["id"]?.ToString(),        Is.EqualTo("localhost"));
                Assert.That(json?["publicKey"]?["user"]?["name"]?.ToString(),    Is.EqualTo("erin"));

                var ceremonyId  = json?["ceremonyId"]?.ToString();
                var challenge   = json?["publicKey"]?["challenge"]?.ToString();

                (status, json) = await browser.Call(HttpMethod.Post, "accounts/auth/passkeys/register", new {
                                     ceremonyId,
                                     name        = "Test key",
                                     credential  = authenticator.Register(challenge!, Origin: origin)
                                 });
                Assert.That(status,                                    Is.EqualTo(HttpStatusCode.Created));
                Assert.That(json?["passkey"]?["id"]?.ToString(),       Is.EqualTo(authenticator.CredentialIdText));
                Assert.That(json?["passkeys"]?.Count(),                Is.EqualTo(1));

                // A ceremony is used once.
                (status, _) = await browser.Call(HttpMethod.Post, "accounts/auth/passkeys/register", new {
                                  ceremonyId,
                                  name        = "Again",
                                  credential  = authenticator.Register(challenge!, Origin: origin)
                              });
                Assert.That(status,  Is.EqualTo(HttpStatusCode.BadRequest));

                (status, json) = await browser.Call(HttpMethod.Put, $"accounts/auth/passkeys/{authenticator.CredentialIdText}", new { name = "Renamed" });
                Assert.That(status,                                          Is.EqualTo(HttpStatusCode.OK));
                Assert.That(json?["passkeys"]?[0]?["name"]?.ToString(),      Is.EqualTo("Renamed"));

                Assert.That((await browser.Call(HttpMethod.Post, "accounts/auth/logout")).Status,  Is.EqualTo(HttpStatusCode.NoContent));
                Assert.That((await browser.Call(HttpMethod.Get,  "accounts/auth/me")).Status,      Is.EqualTo(HttpStatusCode.Unauthorized));

                // Sign-in with the passkey: discoverable, so no login is needed.
                (status, json) = await browser.Call(HttpMethod.Post, "accounts/auth/passkeys/login/options", new { });
                Assert.That(status,  Is.EqualTo(HttpStatusCode.OK));

                ceremonyId  = json?["ceremonyId"]?.ToString();
                challenge   = json?["publicKey"]?["challenge"]?.ToString();

                (status, json) = await browser.Call(HttpMethod.Post, "accounts/auth/passkeys/login", new {
                                     ceremonyId,
                                     credential = authenticator.Authenticate(challenge!, erin, Origin: origin)
                                 });
                Assert.That(status,                                       Is.EqualTo(HttpStatusCode.OK));
                Assert.That(json?["user"]?["id"]?.ToString(),             Is.EqualTo("erin"));
                Assert.That(json?["user"]?["passkeys"]?.Value<Int32>(),   Is.EqualTo(1));
                Assert.That(browser.HasSession,                           Is.True);

                (status, json) = await browser.Call(HttpMethod.Get, "accounts/auth/passkeys");
                Assert.That(json?["passkeys"]?[0]?["signCount"]?.Value<Int32>(),   Is.EqualTo(1));
                Assert.That(json?["passkeys"]?[0]?["lastUsedAt"]?.Type,            Is.Not.EqualTo(JTokenType.Null));

                // A cloned authenticator with a stuck signature counter is refused.
                (status, json) = await browser.Call(HttpMethod.Post, "accounts/auth/passkeys/login/options", new { });
                ceremonyId  = json?["ceremonyId"]?.ToString();
                challenge   = json?["publicKey"]?["challenge"]?.ToString();
                authenticator.Counter = 0;

                (status, json) = await browser.Call(HttpMethod.Post, "accounts/auth/passkeys/login", new {
                                     ceremonyId,
                                     credential = authenticator.Authenticate(challenge!, erin, BumpCounter: false, Origin: origin)
                                 });
                Assert.That(status,                             Is.EqualTo(HttpStatusCode.Unauthorized));
                Assert.That(json?["description"]?.ToString(),   Does.Contain("counter"));

                (status, _) = await browser.Call(HttpMethod.Delete, $"accounts/auth/passkeys/{authenticator.CredentialIdText}");
                Assert.That(status,                     Is.EqualTo(HttpStatusCode.NoContent));
                Assert.That(api.GetPasskeys(erin),      Is.Empty);

            }
            finally
            {
                await StopAsync(server, client, directory);
            }

        }

        #endregion

        #region Unknown_Paths_Answer_404()

        [Test]
        public async Task Unknown_Paths_Answer_404()
        {

            var (server, api, client, directory) = await StartAsync();

            try
            {

                var browser = new Browser(client);

                // The API root without a route, an unknown route below it, a path
                // outside every API: all 404, none of them a server error.
                Assert.That((await browser.Call(HttpMethod.Get,  "accounts/")).Status,             Is.EqualTo(HttpStatusCode.NotFound));
                Assert.That((await browser.Call(HttpMethod.Get,  "accounts")).Status,              Is.EqualTo(HttpStatusCode.NotFound));
                Assert.That((await browser.Call(HttpMethod.Get,  "accounts/auth/nothing")).Status, Is.EqualTo(HttpStatusCode.NotFound));
                Assert.That((await browser.Call(HttpMethod.Post, "accounts/nothing")).Status,      Is.EqualTo(HttpStatusCode.NotFound));
                Assert.That((await browser.Call(HttpMethod.Get,  "nothing")).Status,               Is.EqualTo(HttpStatusCode.NotFound));

            }
            finally
            {
                await StopAsync(server, client, directory);
            }

        }

        #endregion

        #region SET_Password_Ends_The_Other_Sessions()

        [Test]
        public async Task SET_Password_Ends_The_Other_Sessions()
        {

            // The management route of the HTML templates behaves like POST auth/password.
            var (server, api, client, directory) = await StartAsync(WithTemplates: true);

            try
            {

                var laptop = new Browser(client);
                var phone  = new Browser(client);

                Assert.That((await SignUp(laptop, "hank")).Status,  Is.EqualTo(HttpStatusCode.Created));

                // The management routes are for organization members: give the account one it may write to.
                Assert.That(api.TryGetUser(User_Id.Parse("hank"), out var hank),  Is.True);

                var acme = new Organization(Organization_Id.Parse("acme"), I18NString.Create("ACME"));

                Assert.That((await api.AddOrganization(acme)).Result,                                                       Is.EqualTo(CommandResult.Success));
                Assert.That((await api.AddUserToOrganization(hank!, User2OrganizationEdgeLabel.IsMember, acme)).IsSuccess,  Is.True);
                Assert.That((await phone.Call(HttpMethod.Post, "accounts/auth/login", new { login = "hank", password = "Correct-Horse-1" })).Status,  Is.EqualTo(HttpStatusCode.OK));
                Assert.That(api.Sessions.CountForUser(User_Id.Parse("hank")),  Is.EqualTo(2));

                var (status, setJSON) = await laptop.Call(new HttpMethod("SET"), "accounts/users/hank/password", new { currentPassword = "Correct-Horse-1", newPassword = "Correct-Horse-2" });
                Assert.That(status,  Is.EqualTo(HttpStatusCode.OK),  laptop.LastBody);

                Assert.That((await laptop.Call(HttpMethod.Get, "accounts/auth/me")).Status,  Is.EqualTo(HttpStatusCode.OK),            "the session that changed the password stays");
                Assert.That((await phone. Call(HttpMethod.Get, "accounts/auth/me")).Status,  Is.EqualTo(HttpStatusCode.Unauthorized),  "every other session is gone");
                Assert.That((await phone.Call(HttpMethod.Post, "accounts/auth/login", new { login = "hank", password = "Correct-Horse-2" })).Status,  Is.EqualTo(HttpStatusCode.OK));

            }
            finally
            {
                await StopAsync(server, client, directory);
            }

        }

        #endregion

        #region API_Keys_Are_Deleted_By_The_Named_APIKeyId()

        [Test]
        public async Task API_Keys_Are_Deleted_By_The_Named_APIKeyId()
        {

            // The management routes read their URL parameters by the names of the
            // template parameters: here the API key id, not the user id in front of it.
            var (server, api, client, directory) = await StartAsync(WithTemplates: true);

            try
            {

                var browser = new Browser(client);

                Assert.That((await SignUp(browser, "hank")).Status,  Is.EqualTo(HttpStatusCode.Created));

                var acme = new Organization(Organization_Id.Parse("acme"), I18NString.Create("ACME"));

                Assert.That((await api.AddOrganization(acme)).Result,                                                                    Is.EqualTo(CommandResult.Success));
                Assert.That((await api.AddUserToOrganization(UserOf(api, "hank"), User2OrganizationEdgeLabel.IsMember, acme)).IsSuccess,  Is.True);

                var keyId = APIKey_Id.Parse("hanks-first-key-0123456789abcdef");

                Assert.That((await api.AddAPIKey(new APIKey(keyId, User_Id.Parse("hank")))).Result,  Is.EqualTo(CommandResult.Success));
                Assert.That(api.TryGetAPIKey(keyId, out _),  Is.True);

                Assert.That((await browser.Call(HttpMethod.Delete, "accounts/users/hank/APIKeys/no-such-key")).Status,      Is.EqualTo(HttpStatusCode.NotFound),  browser.LastBody);
                Assert.That((await browser.Call(HttpMethod.Delete, "accounts/users/hank/APIKeys/hanks-first-key-0123456789abcdef")).Status,  Is.EqualTo(HttpStatusCode.OK),        browser.LastBody);
                Assert.That(api.TryGetAPIKey(keyId, out _),  Is.False);

            }
            finally
            {
                await StopAsync(server, client, directory);
            }

        }

        #endregion

        #region Organization_Members_Are_Added_By_The_Named_UserId()

        [Test]
        public async Task Organization_Members_Are_Added_By_The_Named_UserId()
        {

            var (server, api, client, directory) = await StartAsync(WithTemplates: true);

            try
            {

                var hank   = new Browser(client);
                var paula  = new Browser(client);

                Assert.That((await SignUp(hank,  "hank")).Status,   Is.EqualTo(HttpStatusCode.Created));
                Assert.That((await SignUp(paula, "paula")).Status,  Is.EqualTo(HttpStatusCode.Created));

                // Only an organization admin may add members.
                var acme = new Organization(Organization_Id.Parse("acme"), I18NString.Create("ACME"));

                Assert.That((await api.AddOrganization(acme)).Result,                                                                   Is.EqualTo(CommandResult.Success));
                Assert.That((await api.AddUserToOrganization(UserOf(api, "hank"), User2OrganizationEdgeLabel.IsAdmin, acme)).IsSuccess,  Is.True);

                Assert.That((await hank.Call(new HttpMethod("ADD"), "accounts/organizations/acme/members/paula")).Status,   Is.EqualTo(HttpStatusCode.OK),        hank.LastBody);
                Assert.That((await hank.Call(new HttpMethod("ADD"), "accounts/organizations/acme/members/nobody")).Status,  Is.EqualTo(HttpStatusCode.NotFound),  hank.LastBody);

                Assert.That(api.TryGetOrganization(Organization_Id.Parse("acme"), out var organization),  Is.True);
                Assert.That(organization?.Members.Select(member => member.Id.ToString()),                  Does.Contain("paula"));

            }
            finally
            {
                await StopAsync(server, client, directory);
            }

        }

        #endregion

    }

}
