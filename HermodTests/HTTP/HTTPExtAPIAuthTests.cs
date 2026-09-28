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
using System.Reflection;
using System.Diagnostics;

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

        private static async Task<(HTTPServer Server, HTTPExtAPI API, HttpClient Client, String Directory)> StartAsync(Boolean                            WithTemplates   = false,
                                                                                                                       SelfSignUpAPI.OnSignedUpDelegate?  OnSignedUp      = null)
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

            _ = new SelfSignUpAPI(api, OnSignedUp: OnSignedUp);

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

        /// <summary>
        /// An account the sign-in form lets in: the form wants it to be in an
        /// organization, which auth/login does not ask.
        /// </summary>
        private static async Task AUserWhoSignsInAtTheForm(HTTPExtAPI  API,
                                                           String      Username,
                                                           String      Password)
        {

            var user = await API.CreateUserIfNotExists(
                                 User_Id.Parse(Username),
                                 I18NString.Create(Username),
                                 SimpleEMailAddress.Parse($"{Username}@example.test"),
                                 Password:                  Password,
                                 IsAuthenticated:           true,
                                 AcceptedEULA:              DateTimeOffset.UtcNow.AddDays(-1),
                                 SkipNewUserEMail:          true,
                                 SkipNewUserNotifications:  true,
                                 SkipDefaultNotifications:  true
                             ) ?? throw new InvalidOperationException($"'{Username}' was not created!");

            var team = await API.CreateOrganizationIfNotExists(Organization_Id.Parse("form-team"), I18NString.Create("Form Team"))
                           ?? throw new InvalidOperationException("The organization was not created!");

            Assert.That((await API.AddUserToOrganization(user, User2OrganizationEdgeLabel.IsMember, team)).IsSuccess,  Is.True);

        }

        /// <summary>
        /// One sign-in at the form, as a browser posts it: form-urlencoded, with
        /// the fields "login" and "password". The form is one of the URL
        /// templates, so the API has to be started with them.
        /// </summary>
        private static async Task<HttpStatusCode> AtTheForm(HttpClient  Client,
                                                            String      Login,
                                                            String      Password)
        {

            using var response = await Client.PostAsync("accounts/login",
                                                         new FormUrlEncodedContent([
                                                             new KeyValuePair<String, String>("login",     Login),
                                                             new KeyValuePair<String, String>("password",  Password)
                                                         ]));

            return response.StatusCode;

        }

        /// <summary>
        /// The same sign-in at the form, and what the answer said.
        /// </summary>
        private static async Task<(HttpStatusCode Status, String Text)> AnswerAtTheForm(HttpClient  Client,
                                                                                       String      Login,
                                                                                       String      Password)
        {

            using var response = await Client.PostAsync("accounts/login",
                                                         new FormUrlEncodedContent([
                                                             new KeyValuePair<String, String>("login",     Login),
                                                             new KeyValuePair<String, String>("password",  Password)
                                                         ]));

            return (response.StatusCode, await response.Content.ReadAsStringAsync());

        }

        /// <summary>
        /// One sign-in through AUTH ~/users/{UserId}, the other door among the
        /// URL templates: the login in the path, the password in a JSON body.
        /// </summary>
        private static async Task<(HttpStatusCode Status, String Text)> AtAuthUsers(HttpClient  Client,
                                                                                   String      Login,
                                                                                   String      Password)
        {

            using var request  = new HttpRequestMessage(new HttpMethod("AUTH"), $"accounts/users/{Uri.EscapeDataString(Login)}") {
                                     Content = new StringContent(JsonConvert.SerializeObject(new { password = Password }), Encoding.UTF8, "application/json")
                                 };

            using var response = await Client.SendAsync(request);

            return (response.StatusCode, await response.Content.ReadAsStringAsync());

        }

        /// <summary>
        /// Every turn at verifying a password taken, as by sign-ins in the middle
        /// of their hashes: the number taken, to give back.
        /// </summary>
        private static Int32 TakeEveryTurn(HTTPExtAPI API, out SemaphoreSlim Verifiers)
        {

            Verifiers  = typeof(HTTPExtAPI).GetField("passwordVerifiers", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(API) as SemaphoreSlim
                             ?? throw new InvalidOperationException("The ceiling of password verifications is not where this test looks for it!");

            var taken  = 0;

            while (Verifiers.Wait(0))
                taken++;

            return taken;

        }

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

        #region SignUp_Hands_The_Account_To_OnSignedUp_Before_Signing_It_In()

        /// <summary>
        /// The API owner decides where an account goes: the delegate runs
        /// after the account exists and before the session is handed out, so
        /// that "who am I" a moment later already sees the organization. And
        /// when the owner cannot set the account up, nobody is signed in.
        /// </summary>
        [Test]
        public async Task SignUp_Hands_The_Account_To_OnSignedUp_Before_Signing_It_In()
        {

            IUser?         signedUp  = null;
            IOrganization? drivers   = null;
            HTTPExtAPI?    extAPI    = null;

            var (server, api, client, directory) = await StartAsync(OnSignedUp: async (user, request) => {

                signedUp = user;

                if (user.Id.ToString() == "mallory")
                    return "The account was made, but there is no room for it. Ask the operator.";

                drivers ??= await extAPI!.CreateOrganizationIfNotExists(Organization_Id.Parse("drivers"), I18NString.Create("Drivers"));

                var joined = await extAPI!.AddUserToOrganization(user, User2OrganizationEdgeLabel.IsMember, drivers!);

                return joined.IsSuccess ? null : "The account could not be put into the organization.";

            });

            extAPI = api;

            try
            {

                var browser        = new Browser(client);
                var (status, json) = await SignUp(browser, "alice");

                Assert.That(status,                          Is.EqualTo(HttpStatusCode.Created));
                Assert.That(signedUp?.Id.ToString(),         Is.EqualTo("alice"));
                Assert.That(browser.HasSession,              Is.True);
                Assert.That(UserOf(api, "alice").User2Organization_OutEdges.Count(),  Is.EqualTo(1),
                            "the delegate ran before the answer, and the account is in its organization");

                // A refusal is a 500 that says what the delegate said, with an
                // account that exists and a browser that is not signed in.
                var other = new Browser(client);

                (status, json) = await SignUp(other, "mallory");

                Assert.That(status,                                       Is.EqualTo(HttpStatusCode.InternalServerError));
                Assert.That(json?["description"]?.ToString(),             Does.Contain("no room"));
                Assert.That(other.HasSession,                             Is.False);
                Assert.That(api.TryGetUser(User_Id.Parse("mallory"), out _),  Is.True);
                Assert.That(api.Sessions.CountForUser(User_Id.Parse("mallory")),  Is.EqualTo(0));

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

        #region BasicAuth_TheRightPasswordIsVerifiedOnceAndNotRationedAgain()

        /// <summary>
        /// A client that knows the password and sends it with every request is
        /// not somebody guessing it: its credentials are verified once and
        /// remembered, and the ration is left to the guesses.
        /// </summary>
        /// <remarks>
        /// Before, every request with Basic Auth was verified again and drew on
        /// the ration of the sign-in route, so the eleventh in a minute came back
        /// 401 with the right password - measured against an application on this
        /// API: 200 ten times, then 401, then one 200 every six seconds. Fifteen
        /// here, and every one of them gets through.
        ///
        /// And the guesses are still rationed, as BasicAuth_IsRationedLikeTheSignInRoute
        /// says: once they have spent the ration, the right password in a form
        /// that was never verified - the e-mail address for the username - is
        /// refused like any guess, while the credentials that were verified go
        /// on being believed.
        /// </remarks>
        [Test]
        public async Task BasicAuth_TheRightPasswordIsVerifiedOnceAndNotRationedAgain()
        {

            var (server, api, client, directory) = await StartAsync();

            try
            {

                await api.CreateUserIfNotExists(
                          User_Id.Parse("frank"),
                          I18NString.Create("Frank"),
                          SimpleEMailAddress.Parse("frank@example.test"),
                          Password:                  "Correct-Horse-8",
                          IsAuthenticated:           true,
                          AcceptedEULA:              DateTimeOffset.UtcNow.AddDays(-1),
                          SkipNewUserEMail:          true,
                          SkipNewUserNotifications:  true,
                          SkipDefaultNotifications:  true
                      );

                async Task<HttpStatusCode> WithBasicAuth(String Username, String Password)
                {

                    using var request = new HttpRequestMessage(HttpMethod.Get, "accounts/auth/me");

                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                                                        "Basic",
                                                        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Username}:{Password}"))
                                                    );

                    using var response = await client.SendAsync(request);

                    return response.StatusCode;

                }

                for (var request = 1; request <= 15; request++)
                    Assert.That(await WithBasicAuth("frank", "Correct-Horse-8"),
                                Is.EqualTo(HttpStatusCode.OK),
                                $"request {request} with the right password");

                for (var attempt = 1; attempt <= 10; attempt++)
                    Assert.That(await WithBasicAuth("frank", "Wrong-Horse-" + attempt),
                                Is.EqualTo(HttpStatusCode.Unauthorized),
                                $"guess {attempt}");

                var byEMail = await WithBasicAuth("frank@example.test", "Correct-Horse-8");
                var byName  = await WithBasicAuth("frank",              "Correct-Horse-8");

                Assert.Multiple(() => {

                    Assert.That(byEMail,
                                Is.EqualTo(HttpStatusCode.Unauthorized),
                                "the right password, but never verified in this form: rationed like a guess, and the guesses spent the ration");

                    Assert.That(byName,
                                Is.EqualTo(HttpStatusCode.OK),
                                "while what was verified goes on being believed");

                });

            }
            finally
            {
                await StopAsync(server, client, directory);
            }

        }

        #endregion

        #region BasicAuth_ARememberedPasswordGoesWithAPasswordChange()

        /// <summary>
        /// Credentials that were verified are believed only for as long as the
        /// password they were verified against is the account's: after a change
        /// the old password is refused at once, not when it would have been
        /// forgotten anyway.
        /// </summary>
        [Test]
        public async Task BasicAuth_ARememberedPasswordGoesWithAPasswordChange()
        {

            var (server, api, client, directory) = await StartAsync();

            try
            {

                var created = await api.CreateUserIfNotExists(
                                        User_Id.Parse("grace"),
                                        I18NString.Create("Grace"),
                                        SimpleEMailAddress.Parse("grace@example.test"),
                                        Password:                  "Correct-Horse-9",
                                        IsAuthenticated:           true,
                                        AcceptedEULA:              DateTimeOffset.UtcNow.AddDays(-1),
                                        SkipNewUserEMail:          true,
                                        SkipNewUserNotifications:  true,
                                        SkipDefaultNotifications:  true
                                    );

                Assert.That(created, Is.Not.Null);

                async Task<HttpStatusCode> WithBasicAuth(String Password)
                {

                    using var request = new HttpRequestMessage(HttpMethod.Get, "accounts/auth/me");

                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                                                        "Basic",
                                                        Convert.ToBase64String(Encoding.UTF8.GetBytes($"grace:{Password}"))
                                                    );

                    using var response = await client.SendAsync(request);

                    return response.StatusCode;

                }

                Assert.That(await WithBasicAuth("Correct-Horse-9"),  Is.EqualTo(HttpStatusCode.OK),  "verified, and remembered from now on");

                var changed = await api.ChangePassword(created!, "Staple-Battery-9", CurrentPassword: "Correct-Horse-9");

                Assert.That(changed.Result, Is.EqualTo(CommandResult.Success), changed.Description.FirstText());

                var withTheOld = await WithBasicAuth("Correct-Horse-9");
                var withTheNew = await WithBasicAuth("Staple-Battery-9");

                Assert.Multiple(() => {
                    Assert.That(withTheOld,  Is.EqualTo(HttpStatusCode.Unauthorized),  "the old password, remembered a moment ago, is let go of with the change");
                    Assert.That(withTheNew,  Is.EqualTo(HttpStatusCode.OK),            "and the new one is verified");
                });

            }
            finally
            {
                await StopAsync(server, client, directory);
            }

        }

        #endregion

        #region SignInForm_IsRationedLikeTheSignInRoute()

        /// <summary>
        /// The form a browser posts its sign-in to - ~/login, form-urlencoded -
        /// verified every password it was sent, as fast as they came, while
        /// auth/login next door counted every attempt.
        /// </summary>
        /// <remarks>
        /// Measured against a charging station on this API, whose web interface
        /// signs in at this form: fifteen wrong passwords in three seconds, each
        /// one verified and answered "Invalid password!" - and still verified
        /// after auth/login had begun to answer 429.
        ///
        /// As for Basic Auth, what this asserts is behaviour and not a
        /// stopwatch: once the ration is spent, the RIGHT password is refused
        /// too. Before, it signed in.
        /// </remarks>
        [Test]
        public async Task SignInForm_IsRationedLikeTheSignInRoute()
        {

            var (server, api, client, directory) = await StartAsync(WithTemplates: true);

            try
            {

                await AUserWhoSignsInAtTheForm(api, "judy", "Correct-Horse-11");

                for (var attempt = 1; attempt <= 10; attempt++)
                    Assert.That(await AtTheForm(client, "judy", "Wrong-Horse-" + attempt),
                                Is.EqualTo(HttpStatusCode.Unauthorized),
                                $"attempt {attempt}");

                Assert.That(await AtTheForm(client, "judy", "Correct-Horse-11"),
                            Is.EqualTo(HttpStatusCode.TooManyRequests),
                            "and once the ration is spent the right password does not get through either - which is what proves no hash was computed");

            }
            finally
            {
                await StopAsync(server, client, directory);
            }

        }

        #endregion

        #region SignInForm_AndTheJSONSignIn_ShareOneRation()

        /// <summary>
        /// Guessing a password is guessing a password, whichever door it comes
        /// through: the form and auth/login draw on one ration, so that two doors
        /// with ten attempts each are not twenty.
        /// </summary>
        [Test]
        public async Task SignInForm_AndTheJSONSignIn_ShareOneRation()
        {

            var (server, api, client, directory) = await StartAsync(WithTemplates: true);

            try
            {

                await AUserWhoSignsInAtTheForm(api, "kate", "Correct-Horse-12");

                var browser = new Browser(client);

                for (var attempt = 1; attempt <= 5; attempt++)
                {

                    Assert.That(await AtTheForm(client, "kate", "Wrong-Horse-" + attempt),
                                Is.EqualTo(HttpStatusCode.Unauthorized),
                                $"guess {attempt} at the form");

                    Assert.That((await browser.Call(HttpMethod.Post, "accounts/auth/login", new { login = "kate", password = "Wrong-Horse-" + attempt })).Status,
                                Is.EqualTo(HttpStatusCode.Unauthorized),
                                $"guess {attempt} at auth/login");

                }

                var atTheForm  = await AtTheForm(client, "kate", "Correct-Horse-12");
                var atTheJSON  = (await browser.Call(HttpMethod.Post, "accounts/auth/login", new { login = "kate", password = "Correct-Horse-12" })).Status;

                Assert.Multiple(() => {
                    Assert.That(atTheForm,  Is.EqualTo(HttpStatusCode.TooManyRequests),  "the form, after ten guesses between the two doors");
                    Assert.That(atTheJSON,  Is.EqualTo(HttpStatusCode.TooManyRequests),  "and auth/login, after the same ten");
                });

            }
            finally
            {
                await StopAsync(server, client, directory);
            }

        }

        #endregion

        #region SignInForm_VerifiesOnlyInItsTurn()

        /// <summary>
        /// How many passwords are verified at the same time is one ceiling for
        /// every route together, and the form verifies in its turn as auth/login
        /// does: a sign-in that gets no turn in time is told the server is busy,
        /// rather than making it busier.
        /// </summary>
        /// <remarks>
        /// Every turn is taken here, as by sign-ins in the middle of their
        /// hashes. The form used to verify regardless; it waits now for as long
        /// as auth/login waits, and then answers 503.
        /// </remarks>
        [Test]
        public async Task SignInForm_VerifiesOnlyInItsTurn()
        {

            var (server, api, client, directory) = await StartAsync(WithTemplates: true);

            var verifiers  = typeof(HTTPExtAPI).GetField("passwordVerifiers", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(api) as SemaphoreSlim
                                 ?? throw new InvalidOperationException("The ceiling of password verifications is not where this test looks for it!");
            var taken      = 0;

            try
            {

                await AUserWhoSignsInAtTheForm(api, "liam", "Correct-Horse-13");

                while (verifiers.Wait(0))
                    taken++;

                Assert.That(taken, Is.EqualTo(HTTPExtAPI.DefaultPasswordVerifiers));

                Assert.That(await AtTheForm(client, "liam", "Correct-Horse-13"),
                            Is.EqualTo(HttpStatusCode.ServiceUnavailable),
                            "no turn came in time, so nothing was verified");

            }
            finally
            {

                if (taken > 0)
                    verifiers.Release(taken);

                await StopAsync(server, client, directory);

            }

        }

        #endregion

        #region AuthUsers_IsRationedLikeTheSignInRoute()

        /// <summary>
        /// AUTH ~/users/{UserId} is a sign-in as well - the login in the path,
        /// the password in a JSON body, a session at the end - and it verified
        /// every password it was sent, as fast as they came, as the form did.
        /// </summary>
        /// <remarks>
        /// Once the ration is spent, the right password is refused too. Before,
        /// it signed in.
        /// </remarks>
        [Test]
        public async Task AuthUsers_IsRationedLikeTheSignInRoute()
        {

            var (server, api, client, directory) = await StartAsync(WithTemplates: true);

            try
            {

                await AUserWhoSignsInAtTheForm(api, "nina", "Correct-Horse-15");

                for (var attempt = 1; attempt <= 10; attempt++)
                    Assert.That((await AtAuthUsers(client, "nina", "Wrong-Horse-" + attempt)).Status,
                                Is.Not.EqualTo(HttpStatusCode.TooManyRequests).And.Not.EqualTo(HttpStatusCode.Created),
                                $"attempt {attempt}");

                var right = await AtAuthUsers(client, "nina", "Correct-Horse-15");

                Assert.That(right.Status,
                            Is.EqualTo(HttpStatusCode.TooManyRequests),
                            "and once the ration is spent the right password does not get through either: " + right.Text);

            }
            finally
            {
                await StopAsync(server, client, directory);
            }

        }

        #endregion

        #region AuthUsers_VerifiesOnlyInItsTurn()

        /// <summary>
        /// AUTH ~/users/{UserId} verifies in its turn, under the one ceiling of
        /// password verifications, as the form and auth/login do.
        /// </summary>
        [Test]
        public async Task AuthUsers_VerifiesOnlyInItsTurn()
        {

            var (server, api, client, directory) = await StartAsync(WithTemplates: true);

            SemaphoreSlim? verifiers = null;
            var taken = 0;

            try
            {

                await AUserWhoSignsInAtTheForm(api, "olga", "Correct-Horse-16");

                taken = TakeEveryTurn(api, out verifiers);

                Assert.That(taken, Is.EqualTo(HTTPExtAPI.DefaultPasswordVerifiers));

                var answer = await AtAuthUsers(client, "olga", "Correct-Horse-16");

                Assert.That(answer.Status,
                            Is.EqualTo(HttpStatusCode.ServiceUnavailable),
                            "no turn came in time, so nothing was verified: " + answer.Text);

            }
            finally
            {

                if (taken > 0)
                    verifiers!.Release(taken);

                await StopAsync(server, client, directory);

            }

        }

        #endregion

        #region SignInForm_AnswersAnUnknownLoginAsAWrongPassword() / AuthUsers_AnswersAnUnknownLoginAsAWrongPassword()

        /// <summary>
        /// Whether an account exists is not told to somebody who does not know
        /// its password: at the form, a login nobody has is answered as a wrong
        /// password is, word for word.
        /// </summary>
        /// <remarks>
        /// The form answered 404 "Unknown login!" for the one and 401 "Invalid
        /// password!" for the other. auth/login has said one sentence for both
        /// all along.
        /// </remarks>
        [Test]
        public async Task SignInForm_AnswersAnUnknownLoginAsAWrongPassword()
        {

            var (server, api, client, directory) = await StartAsync(WithTemplates: true);

            try
            {

                await AUserWhoSignsInAtTheForm(api, "paul", "Correct-Horse-17");

                var unknown  = await AnswerAtTheForm(client, "nobody-here", "Wrong-Horse-1");
                var wrong    = await AnswerAtTheForm(client, "paul",        "Wrong-Horse-1");

                Assert.Multiple(() => {
                    Assert.That(unknown.Status,  Is.EqualTo(HttpStatusCode.Unauthorized));
                    Assert.That(unknown.Status,  Is.EqualTo(wrong.Status),  "the same status");
                    Assert.That(unknown.Text,    Is.EqualTo(wrong.Text),    "and the same words");
                });

            }
            finally
            {
                await StopAsync(server, client, directory);
            }

        }

        /// <summary>
        /// The same at AUTH ~/users/{UserId}, which answered 404 for both and
        /// said "Unknown login!" for the one and "Invalid password!" for the
        /// other.
        /// </summary>
        [Test]
        public async Task AuthUsers_AnswersAnUnknownLoginAsAWrongPassword()
        {

            var (server, api, client, directory) = await StartAsync(WithTemplates: true);

            try
            {

                await AUserWhoSignsInAtTheForm(api, "quinn", "Correct-Horse-18");

                var unknown  = await AtAuthUsers(client, "nobody-here", "Wrong-Horse-1");
                var wrong    = await AtAuthUsers(client, "quinn",       "Wrong-Horse-1");

                Assert.Multiple(() => {
                    Assert.That(unknown.Status,  Is.EqualTo(wrong.Status),  "the same status");
                    Assert.That(unknown.Text,    Is.EqualTo(wrong.Text),    "and the same words");
                });

            }
            finally
            {
                await StopAsync(server, client, directory);
            }

        }

        #endregion

        #region SignInForm_TakesAsLongOverAnUnknownLogin() / AuthUsers_TakesAsLongOverAnUnknownLogin()

        /// <summary>
        /// How long an answer takes over a login nobody has, against how long
        /// it takes over a wrong password: the quickest of three of each, after
        /// one of each to warm up - eight attempts, within the ration of ten.
        /// </summary>
        private static async Task<(TimeSpan Unknown, TimeSpan Wrong)> Timed(Func<String, String, Task> SignIn,
                                                                           String                     Known)
        {

            await SignIn(Known,         "Wrong-Horse-0");
            await SignIn("nobody-here", "Wrong-Horse-0");

            var unknown  = new List<TimeSpan>();
            var wrong    = new List<TimeSpan>();
            var watch    = new Stopwatch();

            for (var attempt = 1; attempt <= 3; attempt++)
            {

                watch.Restart();
                await SignIn(Known, "Wrong-Horse-" + attempt);
                wrong.Add(watch.Elapsed);

                watch.Restart();
                await SignIn("nobody-here", "Wrong-Horse-" + attempt);
                unknown.Add(watch.Elapsed);

            }

            return (unknown.Min(), wrong.Min());

        }

        /// <summary>
        /// A login nobody has costs the hash a wrong password costs, so that the
        /// time the answer takes does not tell the one from the other.
        /// </summary>
        /// <remarks>
        /// The one assertion about sign-ins here that is a stopwatch, because
        /// time is what it is about - and so a generous one. A hash is 600 000
        /// rounds of PBKDF2, tens of milliseconds at the least, and an answer
        /// without one takes about one: an unknown login has to take at least a
        /// quarter of what a wrong password takes, which it cannot without the
        /// hash and does with it, on any machine these tests run on.
        /// </remarks>
        [Test]
        public async Task SignInForm_TakesAsLongOverAnUnknownLogin()
        {

            var (server, api, client, directory) = await StartAsync(WithTemplates: true);

            try
            {

                await AUserWhoSignsInAtTheForm(api, "rosa", "Correct-Horse-19");

                var (unknown, wrong) = await Timed((login, password) => AtTheForm(client, login, password), "rosa");

                Assert.That(unknown, Is.GreaterThanOrEqualTo(wrong / 4),
                            $"an unknown login took {unknown.TotalMilliseconds:F1} ms, a wrong password {wrong.TotalMilliseconds:F1} ms");

            }
            finally
            {
                await StopAsync(server, client, directory);
            }

        }

        /// <summary>
        /// The same at AUTH ~/users/{UserId}.
        /// </summary>
        [Test]
        public async Task AuthUsers_TakesAsLongOverAnUnknownLogin()
        {

            var (server, api, client, directory) = await StartAsync(WithTemplates: true);

            try
            {

                await AUserWhoSignsInAtTheForm(api, "sven", "Correct-Horse-20");

                var (unknown, wrong) = await Timed((login, password) => AtAuthUsers(client, login, password), "sven");

                Assert.That(unknown, Is.GreaterThanOrEqualTo(wrong / 4),
                            $"an unknown login took {unknown.TotalMilliseconds:F1} ms, a wrong password {wrong.TotalMilliseconds:F1} ms");

            }
            finally
            {
                await StopAsync(server, client, directory);
            }

        }

        #endregion

        #region SignInForm_VerifiesAnUnknownLoginInItsTurn() / AuthUsers_VerifiesAnUnknownLoginInItsTurn()

        /// <summary>
        /// A login nobody has is verified against a password nobody has, in its
        /// turn like any other, so that the time an answer takes does not say
        /// whether the account exists either.
        /// </summary>
        /// <remarks>
        /// Behaviour and not a stopwatch, as for the ceiling itself: with every
        /// turn taken, an unknown login waits and is told the server is busy,
        /// as a known one is. It used to be answered at once, without a hash.
        /// </remarks>
        [Test]
        public async Task SignInForm_VerifiesAnUnknownLoginInItsTurn()
        {

            var (server, api, client, directory) = await StartAsync(WithTemplates: true);

            SemaphoreSlim? verifiers = null;
            var taken = 0;

            try
            {

                taken = TakeEveryTurn(api, out verifiers);

                Assert.That(await AtTheForm(client, "nobody-here", "Wrong-Horse-1"),
                            Is.EqualTo(HttpStatusCode.ServiceUnavailable),
                            "an unknown login waited for a turn, as a known one does");

            }
            finally
            {

                if (taken > 0)
                    verifiers!.Release(taken);

                await StopAsync(server, client, directory);

            }

        }

        /// <summary>
        /// The same at AUTH ~/users/{UserId}.
        /// </summary>
        [Test]
        public async Task AuthUsers_VerifiesAnUnknownLoginInItsTurn()
        {

            var (server, api, client, directory) = await StartAsync(WithTemplates: true);

            SemaphoreSlim? verifiers = null;
            var taken = 0;

            try
            {

                taken = TakeEveryTurn(api, out verifiers);

                var answer = await AtAuthUsers(client, "nobody-here", "Wrong-Horse-1");

                Assert.That(answer.Status,
                            Is.EqualTo(HttpStatusCode.ServiceUnavailable),
                            "an unknown login waited for a turn, as a known one does: " + answer.Text);

            }
            finally
            {

                if (taken > 0)
                    verifiers!.Release(taken);

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

        #region Disabling_An_Account_Closes_Every_Door()

        /// <summary>
        /// A disabled account cannot sign in, cannot go on using the session it
        /// signed in with earlier, cannot come back through HTTP Basic Auth, and
        /// cannot be reached through one of its API keys.
        /// </summary>
        /// <remarks>
        /// Every door is opened first and closed afterwards in the same test,
        /// with the same credentials. A test that only checked the refusals
        /// would pass just as happily if the account had never been able to get
        /// in at all - which is the way a test about being shut out fools you.
        ///
        /// The session is the interesting one. It is minted while the account is
        /// still enabled, so nothing about the cookie changes when the account is
        /// disabled; what changes is that the account behind it no longer counts.
        ///
        /// Four password attempts in total, and the ration is ten a minute -
        /// close enough to be worth saying out loud.
        /// </remarks>
        [Test]
        public async Task Disabling_An_Account_Closes_Every_Door()
        {

            var (server, api, client, directory) = await StartAsync();

            try
            {

                #region An account that can use all four doors

                var created = await api.CreateUserIfNotExists(
                                        User_Id.Parse("ivan"),
                                        I18NString.Create("Ivan"),
                                        SimpleEMailAddress.Parse("ivan@example.test"),
                                        Password:                  "Correct-Horse-4",
                                        IsAuthenticated:           true,
                                        // HTTP Basic Auth insists on one.
                                        AcceptedEULA:              DateTimeOffset.UtcNow.AddDays(-1),
                                        SkipNewUserEMail:          true,
                                        SkipNewUserNotifications:  true,
                                        SkipDefaultNotifications:  true
                                    );

                Assert.That(created,              Is.Not.Null);
                Assert.That(created!.IsDisabled,  Is.False);

                var keyId = APIKey_Id.Parse("ivans-only-key-0123456789abcdef");

                Assert.That((await api.AddAPIKey(new APIKey(keyId, User_Id.Parse("ivan")))).Result,  Is.EqualTo(CommandResult.Success));

                async Task<HttpStatusCode> WithBasicAuth()
                {

                    using var request = new HttpRequestMessage(HttpMethod.Get, "accounts/auth/me");

                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                                                        "Basic",
                                                        Convert.ToBase64String(Encoding.UTF8.GetBytes("ivan:Correct-Horse-4"))
                                                    );

                    using var response = await client.SendAsync(request);

                    return response.StatusCode;

                }

                async Task<HttpStatusCode> WithAPIKey()
                {

                    using var request = new HttpRequestMessage(HttpMethod.Get, "accounts/auth/me");

                    request.Headers.Add("API-Key", keyId.ToString());

                    using var response = await client.SendAsync(request);

                    return response.StatusCode;

                }

                #endregion

                #region All four are open

                var browser         = new Browser(client);
                var (status, json)  = await browser.Call(HttpMethod.Post, "accounts/auth/login",
                                                         new { login = "ivan", password = "Correct-Horse-4" });

                Assert.Multiple(() =>
                {
                    Assert.That(status,              Is.EqualTo(HttpStatusCode.OK),  browser.LastBody);
                    Assert.That(browser.HasSession,  Is.True);
                });

                Assert.That((await browser.Call(HttpMethod.Get, "accounts/auth/me")).Status,  Is.EqualTo(HttpStatusCode.OK),  "the session");
                Assert.That(await WithBasicAuth(),                                            Is.EqualTo(HttpStatusCode.OK),  "HTTP Basic Auth");
                Assert.That(await WithAPIKey(),                                               Is.EqualTo(HttpStatusCode.OK),  "the API key");

                #endregion

                #region Disable it, and all four are shut

                Assert.That(api.TryGetUser(User_Id.Parse("ivan"), out var ivan),  Is.True);

                var disabled = await api.UpdateUser(
                                         ivan!,
                                         builder => builder.IsDisabled = true,
                                         SkipUserUpdatedNotifications:  true
                                     );

                Assert.That(disabled.Result,  Is.EqualTo(CommandResult.Success),  disabled.Description.FirstText());

                // The same cookie jar, holding the same session as a moment ago.
                // Gathered, so that neutralising the check reports all three doors
                // at once rather than stopping at whichever one is asked first.
                var session  = (await browser.Call(HttpMethod.Get, "accounts/auth/me")).Status;
                var basic    = await WithBasicAuth();
                var apiKey   = await WithAPIKey();

                // And it cannot sign in again to get a fresh session. Forbidden
                // rather than Unauthorized: the password was right, the account
                // is not.
                var again = new Browser(client);

                (status, json) = await again.Call(HttpMethod.Post, "accounts/auth/login",
                                                  new { login = "ivan", password = "Correct-Horse-4" });

                // Every door reported together. Asserting them one after another
                // would stop at whichever is asked first, and a check that only
                // ever proves one door is a check that lets the others rot.
                Assert.Multiple(() =>
                {
                    Assert.That(session,                                           Is.EqualTo(HttpStatusCode.Unauthorized),  "the session it already had");
                    Assert.That(basic,                                             Is.EqualTo(HttpStatusCode.Unauthorized),  "HTTP Basic Auth");
                    Assert.That(apiKey,                                            Is.EqualTo(HttpStatusCode.Unauthorized),  "the API key");
                    Assert.That(status,                                            Is.EqualTo(HttpStatusCode.Forbidden),     "signing in again: " + again.LastBody);
                    Assert.That(again.HasSession,                                  Is.False,                                 "and no session was handed out");
                    Assert.That(api.Sessions.CountForUser(User_Id.Parse("ivan")),  Is.EqualTo(1),                            "still only the one it had before");
                });

                #endregion

            }
            finally
            {
                await StopAsync(server, client, directory);
            }

        }

        #endregion

        #region Disabling_The_Last_Administrator_Answers_409()

        /// <summary>
        /// Over HTTP, the refusal to leave an organization without an enabled
        /// administrator is a conflict rather than a bad request.
        /// </summary>
        /// <remarks>
        /// The distinction is worth a test because the two are easy to confuse
        /// and only one of them is honest: 400 tells a client that it sent
        /// something wrong and should fix its request, which invites it to try
        /// again with a tidier body forever. Nothing about this body is wrong.
        /// The API will not go to the state it asks for, and that is 409.
        ///
        /// The refusal also names the organization, because "no" without a
        /// reason is the kind of answer somebody debugs for an hour.
        /// </remarks>
        [Test]
        public async Task Disabling_The_Last_Administrator_Answers_409()
        {

            var (server, api, client, directory) = await StartAsync(WithTemplates: true);

            try
            {

                var browser = new Browser(client);

                Assert.That((await SignUp(browser, "alice")).Status,  Is.EqualTo(HttpStatusCode.Created));

                var acme = new Organization(Organization_Id.Parse("acme"), I18NString.Create("ACME"));

                Assert.That((await api.AddOrganization(acme)).Result,                                                                     Is.EqualTo(CommandResult.Success));
                Assert.That((await api.AddUserToOrganization(UserOf(api, "alice"), User2OrganizationEdgeLabel.IsAdmin, acme)).IsSuccess,   Is.True);

                var body = new JObject(
                               new JProperty("@id",         "alice"),
                               new JProperty("@context",    "https://opendata.social/contexts/UsersAPI/user"),
                               new JProperty("name",        new JObject(new JProperty("en", "alice"))),
                               new JProperty("email",       "alice@example.test"),
                               new JProperty("isDisabled",  true)
                           );

                var (status, json) = await browser.Call(new HttpMethod("SET"), "accounts/users/alice", body);

                Assert.Multiple(() =>
                {
                    Assert.That(status,                                Is.EqualTo(HttpStatusCode.Conflict),  browser.LastBody);
                    Assert.That(json?["description"]?.ToString(),      Does.Contain("acme"),                 "the refusal names the organization");
                });

                // Nothing was written, and the account carries on as before.
                var stillSignedIn = (await browser.Call(HttpMethod.Get, "accounts/auth/me")).Status;

                Assert.Multiple(() =>
                {
                    Assert.That(UserOf(api, "alice").IsDisabled,  Is.False);
                    Assert.That(stillSignedIn,                    Is.EqualTo(HttpStatusCode.OK));
                });

            }
            finally
            {
                await StopAsync(server, client, directory);
            }

        }

        #endregion

    }

}
