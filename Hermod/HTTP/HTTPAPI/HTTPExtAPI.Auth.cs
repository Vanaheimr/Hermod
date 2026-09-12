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

using System.Diagnostics.CodeAnalysis;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.Passkeys;

using WebAuthnCeremonies = org.GraphDefined.Vanaheimr.Hermod.Passkeys.WebAuthn;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The JSON account routes of the HTTPExtAPI, for single-page applications
    /// and other JSON clients, all below "auth/": sign-in and sign-out with the
    /// same two cookies as the HTML sign-in form, the signed-in account with
    /// its display name and password, and passkeys (WebAuthn) when relying
    /// party settings were given. Self sign-up is an opt-in, see SelfSignUpAPI.
    /// Errors are JSON objects with a "description".
    /// </summary>
    public partial class HTTPExtAPI
    {

        #region Data

        /// <summary>
        /// The maximum length of a password.
        /// </summary>
        public const Int32 MaxPasswordLength   = 128;

        /// <summary>
        /// The maximum length of a display name.
        /// </summary>
        public const Int32 MaxUserNameLength   = 64;

        // A sign-in attempt for an unknown login still costs a hash computation,
        // so that the response time does not reveal whether an account exists.
        private static readonly SecurePassword unknownLoginPassword = SecurePassword.Create("no such account");

        private readonly InMemoryTokenBucketRateLimiter loginIPRateLimiter         = new (
                                                                                          Capacity:        10,
                                                                                          RefillPeriod:    TimeSpan.FromMinutes(1),
                                                                                          MaximumBuckets:  10_000,
                                                                                          BucketLifetime:  TimeSpan.FromMinutes(10)
                                                                                      );

        private readonly InMemoryTokenBucketRateLimiter loginAccountRateLimiter    = new (
                                                                                          Capacity:        10,
                                                                                          RefillPeriod:    TimeSpan.FromMinutes(1),
                                                                                          MaximumBuckets:  20_000,
                                                                                          BucketLifetime:  TimeSpan.FromMinutes(10)
                                                                                      );

        // Passkey options are requested on every visit of a sign-in page
        // (conditional UI), so they get a lenient limiter of their own.
        private readonly InMemoryTokenBucketRateLimiter passkeyOptionsRateLimiter  = new (
                                                                                          Capacity:        60,
                                                                                          RefillPeriod:    TimeSpan.FromMinutes(1),
                                                                                          MaximumBuckets:  10_000,
                                                                                          BucketLifetime:  TimeSpan.FromMinutes(10)
                                                                                      );

        #endregion


        #region (private) RegisterAuthTemplates()

        /// <summary>
        /// Register the JSON account routes. They belong to the API itself and
        /// are registered even when the HTML templates are skipped.
        /// </summary>
        private void RegisterAuthTemplates()
        {

            AddHandler(HTTPMethod.POST,    HTTPPath.Root + "auth/login",     HTTPDelegate: AuthLogin);
            AddHandler(HTTPMethod.POST,    HTTPPath.Root + "auth/logout",    HTTPDelegate: AuthLogout);
            AddHandler(HTTPMethod.GET,     HTTPPath.Root + "auth/me",        HTTPDelegate: AuthMe);
            AddHandler(HTTPMethod.PUT,     HTTPPath.Root + "auth/me",        HTTPDelegate: AuthUpdateMe);
            AddHandler(HTTPMethod.POST,    HTTPPath.Root + "auth/password",  HTTPDelegate: AuthChangePassword);

            if (WebAuthnSettings is not null)
            {
                AddHandler(HTTPMethod.POST,    HTTPPath.Root + "auth/passkeys/register/options",  HTTPDelegate: PasskeyRegisterOptions);
                AddHandler(HTTPMethod.POST,    HTTPPath.Root + "auth/passkeys/register",          HTTPDelegate: PasskeyRegister);
                AddHandler(HTTPMethod.POST,    HTTPPath.Root + "auth/passkeys/login/options",     HTTPDelegate: PasskeyLoginOptions);
                AddHandler(HTTPMethod.POST,    HTTPPath.Root + "auth/passkeys/login",             HTTPDelegate: PasskeyLogin);
                AddHandler(HTTPMethod.GET,     HTTPPath.Root + "auth/passkeys",                   HTTPDelegate: ListPasskeys);
                AddHandler(HTTPMethod.PUT,     HTTPPath.Root + "auth/passkeys/{id}",              HTTPDelegate: RenamePasskey);
                AddHandler(HTTPMethod.DELETE,  HTTPPath.Root + "auth/passkeys/{id}",              HTTPDelegate: DeletePasskey);
            }

        }

        #endregion


        #region (private) AuthLogin          (Request)

        /// <summary>
        /// POST auth/login with { "login": "username or e-mail", "password": "..." }.
        /// Answers 200 with the account and the session, sets the session cookies.
        /// </summary>
        private async Task<HTTPResponse> AuthLogin(HTTPRequest Request)
        {

            if (!Request.TryParseJSONObjectRequestBody(out var json, out var errorResponse))
                return errorResponse.AsImmutable;

            var login     = json.Value<String>("login")?.Trim() ?? "";
            var password  = json.Value<String>("password")      ?? "";

            if (CheckPasswordRateLimit(Request, "auth/login", loginIPRateLimiter, login, loginAccountRateLimiter) is { } limited)
                return limited.AsImmutable;

            var candidates  = LoginCandidates(login);
            var validUsers  = candidates.Where(user => VerifyPassword(user.Id, password)).ToList();

            if (candidates.Count == 0)
                unknownLoginPassword.Verify(password);

            if (validUsers.Count == 0)
                return AuthError(Request, HTTPStatusCode.Unauthorized, "Unknown login or wrong password.");

            // Several accounts may share an e-mail address; the password decides.
            if (validUsers.Count > 1)
                return AuthError(Request, HTTPStatusCode.MultipleChoices, "The login is ambiguous, please sign in with the username.");

            var user = await SignInNoted(validUsers[0], Request.EventTrackingId);

            return SignedIn(Request, HTTPStatusCode.OK, user, Sessions.Create(user.Id));

        }

        #endregion

        #region (private) AuthLogout         (Request)

        /// <summary>
        /// POST auth/logout: ends the session of the cookie and expires the cookies.
        /// </summary>
        private Task<HTTPResponse> AuthLogout(HTTPRequest Request)
        {

            if (TryGetSecurityTokenFromCookie(Request, out var tokenId))
                Sessions.Remove(tokenId);

            return Task.FromResult(
                       new HTTPResponse.Builder(Request) {
                           HTTPStatusCode  = HTTPStatusCode.NoContent,
                           CacheControl    = "no-store",
                           SetCookie       = ExpiredSessionCookies()
                       }.WithCommonSecurityHeaders().AsImmutable
                   );

        }

        #endregion

        #region (private) AuthMe             (Request)

        /// <summary>
        /// GET auth/me: the signed-in account, its session and the number of its sessions.
        /// </summary>
        private Task<HTTPResponse> AuthMe(HTTPRequest Request)
        {

            if (!TryGetSignedInUser(Request, out var user, out var session, out var unauthorized))
                return Task.FromResult(unauthorized);

            var json = MeJSON(user, session);
            json.Add(new JProperty("activeSessions", Sessions.CountForUser(user.Id)));

            return Task.FromResult(AuthJSON(Request, HTTPStatusCode.OK, json));

        }

        #endregion

        #region (private) AuthUpdateMe       (Request)

        /// <summary>
        /// PUT auth/me with { "displayName": "..." }; an empty display name shows the username.
        /// </summary>
        private async Task<HTTPResponse> AuthUpdateMe(HTTPRequest Request)
        {

            if (!TryGetSignedInUser(Request, out var user, out var session, out var unauthorized))
                return unauthorized;

            if (!Request.TryParseJSONObjectRequestBody(out var json, out var errorResponse))
                return errorResponse.AsImmutable;

            var displayName = json.Value<String>("displayName")?.Trim();

            if (String.IsNullOrEmpty(displayName))
                displayName = null;

            if (displayName is { Length: > MaxUserNameLength })
                return AuthError(Request, HTTPStatusCode.BadRequest, $"The display name must not have more than {MaxUserNameLength} characters.");

            if (displayName is not null && displayName.Length < MinUserNameLength)
                return AuthError(Request, HTTPStatusCode.BadRequest, $"The display name must have at least {MinUserNameLength} characters.");

            var name    = I18NString.Create(displayName ?? user.Id.ToString());
            var result  = await UpdateUser(
                                    user,
                                    builder => builder.Name = name,
                                    SkipUserUpdatedNotifications:  true,
                                    EventTrackingId:               Request.EventTrackingId
                                );

            if (result.Result != CommandResult.Success)
                return AuthError(Request, HTTPStatusCode.BadRequest, result.Description.FirstText());

            if (TryGetUser(user.Id, out var updated) && updated is not null)
                user = updated;

            return AuthJSON(Request, HTTPStatusCode.OK, MeJSON(user, session));

        }

        #endregion

        #region (private) AuthChangePassword (Request)

        /// <summary>
        /// POST auth/password with { "currentPassword": "...", "newPassword": "..." }.
        /// Answers 204 and ends every other session of the account.
        /// </summary>
        private async Task<HTTPResponse> AuthChangePassword(HTTPRequest Request)
        {

            if (!TryGetSignedInUser(Request, out var user, out var session, out var unauthorized))
                return unauthorized;

            if (!Request.TryParseJSONObjectRequestBody(out var json, out var errorResponse))
                return errorResponse.AsImmutable;

            var currentPassword  = json.Value<String>("currentPassword") ?? "";
            var newPassword      = json.Value<String>("newPassword")     ?? "";

            if (ValidatePassword(newPassword) is { } problem)
                return AuthError(Request, HTTPStatusCode.BadRequest, problem);

            if (!VerifyPassword(user.Id, currentPassword))
                return AuthError(Request, HTTPStatusCode.Forbidden, "The current password is wrong.");

            var result = await ChangePassword(
                                   user,
                                   newPassword,
                                   currentPassword,
                                   SuppressNotifications:  true,
                                   EventTrackingId:        Request.EventTrackingId
                               );

            if (result.Result != CommandResult.Success)
                return AuthError(Request, HTTPStatusCode.BadRequest, result.Description.FirstText());

            // Everybody else who knew the old password is signed out.
            Sessions.RemoveAllForUser(user.Id, ExceptToken: session?.Token);

            return new HTTPResponse.Builder(Request) {
                       HTTPStatusCode  = HTTPStatusCode.NoContent,
                       CacheControl    = "no-store"
                   }.WithCommonSecurityHeaders().AsImmutable;

        }

        #endregion


        #region (private) PasskeyRegisterOptions(Request)

        private Task<HTTPResponse> PasskeyRegisterOptions(HTTPRequest Request)
        {

            if (!TryGetSignedInUser(Request, out var user, out _, out var unauthorized))
                return Task.FromResult(unauthorized);

            var ceremony = PasskeyCeremonies.Create(CeremonyType.Registration, user.Id);
            var options  = WebAuthnCeremonies.CreationOptions(
                               WebAuthnSettings!,
                               ceremony,
                               user.Id,
                               user.Id.ToString(),
                               user.Name.FirstText(),
                               GetPasskeys(user.Id)
                           );

            return Task.FromResult(
                       AuthJSON(
                           Request,
                           HTTPStatusCode.OK,
                           new JObject(
                               new JProperty("ceremonyId",  ceremony.Id),
                               new JProperty("publicKey",   options.ToJSON())
                           )
                       )
                   );

        }

        #endregion

        #region (private) PasskeyRegister       (Request)

        private async Task<HTTPResponse> PasskeyRegister(HTTPRequest Request)
        {

            if (!TryGetSignedInUser(Request, out var user, out _, out var unauthorized))
                return unauthorized;

            if (!Request.TryParseJSONObjectRequestBody(out var json, out var errorResponse))
                return errorResponse.AsImmutable;

            var name = json.Value<String>("name")?.Trim() ?? "";

            if (ValidatePasskeyName(name) is { } problem)
                return AuthError(Request, HTTPStatusCode.BadRequest, problem);

            if (json["credential"] is not JObject credential)
                return AuthError(Request, HTTPStatusCode.BadRequest, "The request needs a 'credential' object.");

            if (!PasskeyCeremonies.TryTake(json.Value<String>("ceremonyId") ?? "", CeremonyType.Registration, out var ceremony) ||
                ceremony.UserId != user.Id)
            {
                return AuthError(Request, HTTPStatusCode.BadRequest, "The registration timed out or was already used; please start again.");
            }

            if (!WebAuthnCeremonies.TryVerifyRegistration(WebAuthnSettings!, ceremony, credential, name, out var passkey, out var error))
                return AuthError(Request, HTTPStatusCode.BadRequest, error);

            // A credential id is unique across all accounts; AddPasskey refuses duplicates.
            if (!await AddPasskey(user, passkey, Request.EventTrackingId))
                return AuthError(Request, HTTPStatusCode.Conflict, "This passkey is already registered.");

            return AuthJSON(
                       Request,
                       HTTPStatusCode.Created,
                       new JObject(
                           new JProperty("passkey",   passkey.ToJSON()),
                           new JProperty("passkeys",  PasskeysJSON(user))
                       )
                   );

        }

        #endregion

        #region (private) PasskeyLoginOptions   (Request)

        private Task<HTTPResponse> PasskeyLoginOptions(HTTPRequest Request)
        {

            if (CheckPasswordRateLimit(Request, "auth/passkeys/login/options", passkeyOptionsRateLimiter) is { } limited)
                return Task.FromResult(limited.AsImmutable);

            // An optional login restricts the credentials (username-first, for
            // authenticators without discoverable credentials). Unknown logins
            // get the same empty list as no login, so nothing is revealed.
            IEnumerable<Passkey>? allowed = null;

            if (Request.HTTPBodyAsUTF8String is { Length: > 0 } text)
            {
                try
                {

                    var login = JObject.Parse(text).Value<String>("login")?.Trim();

                    if (!String.IsNullOrEmpty(login) && LoginCandidates(login) is { Count: 1 } candidates)
                        allowed = GetPasskeys(candidates[0].Id);

                }
                catch (JsonException)
                {
                    return Task.FromResult(AuthError(Request, HTTPStatusCode.BadRequest, "Invalid JSON."));
                }
            }

            var ceremony = PasskeyCeremonies.Create(CeremonyType.Authentication);

            return Task.FromResult(
                       AuthJSON(
                           Request,
                           HTTPStatusCode.OK,
                           new JObject(
                               new JProperty("ceremonyId",  ceremony.Id),
                               new JProperty("publicKey",   WebAuthnCeremonies.RequestOptions(WebAuthnSettings!, ceremony, allowed).ToJSON())
                           )
                       )
                   );

        }

        #endregion

        #region (private) PasskeyLogin          (Request)

        private async Task<HTTPResponse> PasskeyLogin(HTTPRequest Request)
        {

            if (CheckPasswordRateLimit(Request, "auth/passkeys/login", loginIPRateLimiter) is { } limited)
                return limited.AsImmutable;

            if (!Request.TryParseJSONObjectRequestBody(out var json, out var errorResponse))
                return errorResponse.AsImmutable;

            if (json["credential"] is not JObject credential)
                return AuthError(Request, HTTPStatusCode.BadRequest, "The request needs a 'credential' object.");

            if (!PasskeyCeremonies.TryTake(json.Value<String>("ceremonyId") ?? "", CeremonyType.Authentication, out var ceremony))
                return AuthError(Request, HTTPStatusCode.BadRequest, "The sign-in timed out or was already used; please try again.");

            var credentialId = credential.Value<String>("id") ?? "";

            if (!TryGetPasskey(credentialId, out var user, out var passkey))
                return AuthError(Request, HTTPStatusCode.Unauthorized, "Unknown passkey.");

            if (!WebAuthnCeremonies.TryVerifyAuthentication(WebAuthnSettings!, ceremony, credential, passkey, user.Id, out var result, out var error))
                return AuthError(Request, HTTPStatusCode.Unauthorized, error);

            await UpdatePasskey(
                      user.Id,
                      passkey with {
                          SignCount   = result.SignCount,
                          LastUsedAt  = Timestamp.Now,
                          BackedUp    = result.BackedUp
                      },
                      Request.EventTrackingId
                  );

            user = await SignInNoted(user, Request.EventTrackingId);

            return SignedIn(Request, HTTPStatusCode.OK, user, Sessions.Create(user.Id));

        }

        #endregion

        #region (private) ListPasskeys          (Request)

        private Task<HTTPResponse> ListPasskeys(HTTPRequest Request)
        {

            if (!TryGetSignedInUser(Request, out var user, out _, out var unauthorized))
                return Task.FromResult(unauthorized);

            return Task.FromResult(
                       AuthJSON(
                           Request,
                           HTTPStatusCode.OK,
                           new JObject(new JProperty("passkeys", PasskeysJSON(user)))
                       )
                   );

        }

        #endregion

        #region (private) RenamePasskey         (Request)

        private async Task<HTTPResponse> RenamePasskey(HTTPRequest Request)
        {

            if (!TryGetSignedInUser(Request, out var user, out _, out var unauthorized))
                return unauthorized;

            if (!Request.TryParseJSONObjectRequestBody(out var json, out var errorResponse))
                return errorResponse.AsImmutable;

            var id    = Request.TryGetURLParameter("id") ?? "";
            var name  = json.Value<String>("name")?.Trim() ?? "";

            if (ValidatePasskeyName(name) is { } problem)
                return AuthError(Request, HTTPStatusCode.BadRequest, problem);

            var passkey = GetPasskeys(user.Id).FirstOrDefault(passkey => passkey.Id == id);

            if (passkey is null)
                return AuthError(Request, HTTPStatusCode.NotFound, "Unknown passkey.");

            await UpdatePasskey(user.Id, passkey with { Name = name }, Request.EventTrackingId);

            return AuthJSON(
                       Request,
                       HTTPStatusCode.OK,
                       new JObject(new JProperty("passkeys", PasskeysJSON(user)))
                   );

        }

        #endregion

        #region (private) DeletePasskey         (Request)

        private async Task<HTTPResponse> DeletePasskey(HTTPRequest Request)
        {

            if (!TryGetSignedInUser(Request, out var user, out _, out var unauthorized))
                return unauthorized;

            var id = Request.TryGetURLParameter("id") ?? "";

            if (!await RemovePasskey(user.Id, id, Request.EventTrackingId))
                return AuthError(Request, HTTPStatusCode.NotFound, "Unknown passkey.");

            return new HTTPResponse.Builder(Request) {
                       HTTPStatusCode  = HTTPStatusCode.NoContent,
                       CacheControl    = "no-store"
                   }.WithCommonSecurityHeaders().AsImmutable;

        }

        #endregion


        #region TryGetSignedInUser(Request, out User, out Session, out Unauthorized)

        /// <summary>
        /// The account behind the request (session cookie, HTTP Basic Auth or
        /// API key) and, when the request carries a session cookie, its session.
        /// Otherwise the 401 response, which also expires a stale session cookie.
        /// </summary>
        public Boolean TryGetSignedInUser(HTTPRequest                             Request,
                                          [NotNullWhen(true)]  out IUser?         User,
                                          out Session?                            Session,
                                          [NotNullWhen(false)] out HTTPResponse?  Unauthorized)
        {

            Session = TryGetSecurityTokenFromCookie(Request, out var tokenId) &&
                      Sessions.TryGet(tokenId, out var session)
                          ? session
                          : null;

            if (TryGetHTTPUser(Request, out User) && User is not null)
            {
                Unauthorized = null;
                return true;
            }

            User = null;

            var builder = new HTTPResponse.Builder(Request) {
                              HTTPStatusCode  = HTTPStatusCode.Unauthorized,
                              ContentType     = HTTPContentType.Application.JSON_UTF8,
                              Content         = new JObject(new JProperty("description", "Sign in required.")).ToString(Formatting.None).ToUTF8Bytes(),
                              CacheControl    = "no-store"
                          };

            // Drop a stale cookie, so that the browser stops sending it.
            if (Request.Cookies?.TryGet(SessionCookieName, out _) == true)
                builder.SetCookie = ExpiredSessionCookies();

            Unauthorized = builder.WithCommonSecurityHeaders().AsImmutable;
            return false;

        }

        #endregion

        #region LoginCandidates   (Login)

        /// <summary>
        /// The accounts whose username or e-mail address equals the given login, case-insensitively.
        /// </summary>
        public List<IUser> LoginCandidates(String Login)
        {

            var login = Login.Trim();

            if (login.Length == 0)
                return [];

            return Users.Where(user => user.Id.ToString().Equals(login, StringComparison.OrdinalIgnoreCase) ||
                                       user.EMail.Address.ToString().Equals(login, StringComparison.OrdinalIgnoreCase)).
                         ToList();

        }

        #endregion

        #region ValidatePassword  (Password)

        /// <summary>
        /// Null when the password passes the password quality check and the
        /// length limit, otherwise the reason.
        /// </summary>
        public String? ValidatePassword(String? Password)
        {

            if (Password is null || PasswordQualityCheck(Password) < 1.0)
                return "The password does not meet the password quality check.";

            if (Password.Length > MaxPasswordLength)
                return $"The password must not have more than {MaxPasswordLength} characters.";

            return null;

        }

        #endregion

        #region ValidatePasskeyName(Name)

        /// <summary>
        /// Null when the passkey name is acceptable, otherwise the reason.
        /// </summary>
        public static String? ValidatePasskeyName(String? Name)

            => String.IsNullOrWhiteSpace(Name)
                   ? "The passkey needs a name."
                   : Name.Length > Passkey.MaxNameLength
                         ? $"The passkey name must not have more than {Passkey.MaxNameLength} characters."
                         : null;

        #endregion


        #region (internal) SignInNoted(User, EventTrackingId)

        /// <summary>
        /// Record the sign-in on the account and return its current state.
        /// </summary>
        internal async Task<IUser> SignInNoted(IUser             User,
                                               EventTracking_Id  EventTrackingId)
        {

            var now = Timestamp.Now;

            var result = await UpdateUser(
                                   User,
                                   builder => builder.LastLoginAt = now,
                                   SkipUserUpdatedNotifications:  true,
                                   EventTrackingId:               EventTrackingId
                               );

            if (result.Result != CommandResult.Success)
                DebugX.Log($"{nameof(HTTPExtAPI)}: The sign-in of '{User.Id}' could not be recorded: {result.Description.FirstText()}");

            return TryGetUser(User.Id, out var updated) && updated is not null
                       ? updated
                       : User;

        }

        #endregion

        #region (internal) SignedIn   (Request, StatusCode, User, Session)

        /// <summary>
        /// The JSON response of a sign-in: the account and the session, with
        /// the same two cookies the HTML sign-in form sets.
        /// </summary>
        internal HTTPResponse SignedIn(HTTPRequest     Request,
                                       HTTPStatusCode  StatusCode,
                                       IUser           User,
                                       Session         Session)

            => new HTTPResponse.Builder(Request) {
                   HTTPStatusCode  = StatusCode,
                   ContentType     = HTTPContentType.Application.JSON_UTF8,
                   Content         = MeJSON(User, Session).ToString(Formatting.None).ToUTF8Bytes(),
                   CacheControl    = "no-store",
                   SetCookie       = SessionCookies(User, Session)
               }.WithCommonSecurityHeaders().AsImmutable;

        #endregion

        #region (internal) AuthJSON   (Request, StatusCode, JSON)

        internal static HTTPResponse AuthJSON(HTTPRequest     Request,
                                              HTTPStatusCode  StatusCode,
                                              JToken          JSON)

            => new HTTPResponse.Builder(Request) {
                   HTTPStatusCode  = StatusCode,
                   ContentType     = HTTPContentType.Application.JSON_UTF8,
                   Content         = JSON.ToString(Formatting.None).ToUTF8Bytes(),
                   CacheControl    = "no-store"
               }.WithCommonSecurityHeaders().AsImmutable;

        #endregion

        #region (internal) AuthError  (Request, StatusCode, Description)

        internal static HTTPResponse AuthError(HTTPRequest     Request,
                                               HTTPStatusCode  StatusCode,
                                               String          Description)

            => AuthJSON(
                   Request,
                   StatusCode,
                   new JObject(new JProperty("description", Description))
               );

        #endregion

        #region (private) SessionCookies(User, Session) / ExpiredSessionCookies()

        /// <summary>
        /// The two cookies of a sign-in: the readable account data and the
        /// HttpOnly session token, both with the settings of the HTML sign-in.
        /// </summary>
        private HTTPCookies SessionCookies(IUser    User,
                                           Session  Session)

            => HTTPCookies.Parse(
                   String.Concat(CookieName,
                                 GenerateCookieUserData(User),
                                 GenerateCookieSettings(Session.ExpiresAt)),
                   String.Concat(SessionCookieName, "=", Session.Token.ToString(),
                                 GenerateCookieSettings(Session.ExpiresAt),
                                 "; HttpOnly")
               );

        /// <summary>
        /// Both cookies with an expiry in the past, so that the browser drops them.
        /// </summary>
        private HTTPCookies ExpiredSessionCookies()

            => HTTPCookies.Parse(
                   String.Concat(CookieName,        "=", GenerateCookieSettings(DateTimeOffset.UnixEpoch)),
                   String.Concat(SessionCookieName, "=", GenerateCookieSettings(DateTimeOffset.UnixEpoch), "; HttpOnly")
               );

        #endregion

        #region UserJSON(User) / MeJSON(User, Session)

        /// <summary>
        /// The public view of an account for JSON clients: the username doubles
        /// as the account id, the display name is the first text of the name.
        /// </summary>
        public JObject UserJSON(IUser User)

            => new (
                   new JProperty("id",           User.Id.ToString()),
                   new JProperty("username",     User.Id.ToString()),
                   new JProperty("email",        User.EMail.Address.ToString()),
                   new JProperty("displayName",  User.Name.FirstText()),
                   new JProperty("createdAt",    User.CreatedAt.ToISO8601()),
                   new JProperty("lastLoginAt",  User.LastLoginAt?.ToISO8601()),
                   new JProperty("passkeys",     GetPasskeys(User.Id).Count())
               );

        /// <summary>
        /// The account and its session (null without a session cookie, e.g. HTTP Basic Auth).
        /// </summary>
        public JObject MeJSON(IUser     User,
                              Session?  Session)

            => new (
                   new JProperty("user",     UserJSON(User)),
                   new JProperty("session",  Session is not null
                                                 ? new JObject(
                                                       new JProperty("createdAt",  Session.CreatedAt.ToISO8601()),
                                                       new JProperty("expiresAt",  Session.ExpiresAt.ToISO8601())
                                                   )
                                                 : null)
               );

        private JArray PasskeysJSON(IUser User)
            => new (GetPasskeys(User.Id).Select(passkey => passkey.ToJSON()));

        #endregion

    }

}
