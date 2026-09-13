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

using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.Mail;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Self sign-up for an HTTPExtAPI, as an opt-in: attaching this to an API
    /// registers POST auth/signup, which creates an account from
    /// { "username", "email", "password", "displayName"? } and signs it in.
    /// Without it accounts are created by administrators only.
    /// </summary>
    public partial class SelfSignUpAPI
    {

        #region Data

        /// <summary>
        /// The default maximum length of a username.
        /// </summary>
        public const UInt16 DefaultMaxUserIdLength = 32;

        // Usernames: letters, digits, dots, dashes and underscores, starting
        // and ending with a letter or digit. The length is checked separately.
        [GeneratedRegex(@"^[A-Za-z0-9](?:[A-Za-z0-9._-]*[A-Za-z0-9])?$")]
        private static partial Regex DefaultUserIdPattern();

        private readonly InMemoryTokenBucketRateLimiter rateLimiter;

        #endregion

        #region Properties

        /// <summary>
        /// The API the sign-ups go to.
        /// </summary>
        public HTTPExtAPI  API               { get; }

        /// <summary>
        /// The maximum length of a username; the minimum is the API's MinUserIdLength.
        /// </summary>
        public UInt16      MaxUserIdLength   { get; }

        /// <summary>
        /// The pattern a username must match.
        /// </summary>
        public Regex       UserIdPattern     { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Attach self sign-up to the given API.
        /// </summary>
        /// <param name="API">The HTTPExtAPI.</param>
        /// <param name="MaxUserIdLength">The maximum length of a username.</param>
        /// <param name="UserIdPattern">An optional pattern a username must match.</param>
        /// <param name="RateLimiter">An optional rate limiter per remote address (default: 5 sign-ups per 10 minutes).</param>
        public SelfSignUpAPI(HTTPExtAPI                       API,
                             UInt16                           MaxUserIdLength   = DefaultMaxUserIdLength,
                             Regex?                           UserIdPattern     = null,
                             InMemoryTokenBucketRateLimiter?  RateLimiter       = null)
        {

            this.API              = API;
            this.MaxUserIdLength  = MaxUserIdLength;
            this.UserIdPattern    = UserIdPattern ?? DefaultUserIdPattern();
            this.rateLimiter      = RateLimiter   ?? new InMemoryTokenBucketRateLimiter(
                                                         Capacity:        5,
                                                         RefillPeriod:    TimeSpan.FromMinutes(10),
                                                         MaximumBuckets:  10_000,
                                                         BucketLifetime:  TimeSpan.FromMinutes(30)
                                                     );

            API.AddHandler(HTTPMethod.POST, HTTPPath.Root + "auth/signup", HTTPDelegate: SignUp);

        }

        #endregion


        #region ValidateUserId(Username)

        /// <summary>
        /// Null when the username is acceptable, otherwise the reason.
        /// </summary>
        public String? ValidateUserId(String? Username)

            => Username is null                    ||
               Username.Length < API.MinUserIdLength ||
               Username.Length > MaxUserIdLength    ||
               !UserIdPattern.IsMatch(Username)
                   ? $"The username must have {API.MinUserIdLength} to {MaxUserIdLength} characters: letters, digits, dots, dashes or underscores, starting and ending with a letter or digit."
                   : null;

        #endregion

        #region (private) SignUp(Request)

        private async Task<HTTPResponse> SignUp(HTTPRequest Request)
        {

            if (API.CheckPasswordRateLimit(Request, "auth/signup", rateLimiter) is { } limited)
                return limited.AsImmutable;

            if (!Request.TryParseJSONObjectRequestBody(out var json, out var errorResponse))
                return errorResponse.AsImmutable;

            var username     = (json.Value<String>("username") ?? "").Trim();
            var email        = (json.Value<String>("email")    ?? "").Trim();
            var password     =  json.Value<String>("password") ?? "";
            var displayName  =  json.Value<String>("displayName")?.Trim();

            if (String.IsNullOrEmpty(displayName))
                displayName = null;

            if (ValidateUserId(username) is { } usernameProblem)
                return HTTPExtAPI.AuthError(Request, HTTPStatusCode.BadRequest, usernameProblem);

            if (email.Length > 254 || !SimpleEMailAddress.TryParse(email, out var emailAddress))
                return HTTPExtAPI.AuthError(Request, HTTPStatusCode.BadRequest, "This does not look like an email address.");

            if (API.ValidatePassword(password) is { } passwordProblem)
                return HTTPExtAPI.AuthError(Request, HTTPStatusCode.BadRequest, passwordProblem);

            if (displayName is { Length: > HTTPExtAPI.MaxUserNameLength })
                return HTTPExtAPI.AuthError(Request, HTTPStatusCode.BadRequest, $"The display name must not have more than {HTTPExtAPI.MaxUserNameLength} characters.");

            if (displayName is not null && displayName.Length < API.MinUserNameLength)
                return HTTPExtAPI.AuthError(Request, HTTPStatusCode.BadRequest, $"The display name must have at least {API.MinUserNameLength} characters.");

            if (!User_Id.TryParse(username, out var userId))
                return HTTPExtAPI.AuthError(Request, HTTPStatusCode.BadRequest, "The username is invalid.");

            // The username is the account id and, like the e-mail address, unique ignoring case.
            var taken = API.LoginCandidates(username);

            if (taken.Count > 0)
                return HTTPExtAPI.AuthError(Request, HTTPStatusCode.Conflict, "This username is already taken.");

            if (API.LoginCandidates(email).Count > 0)
                return HTTPExtAPI.AuthError(Request, HTTPStatusCode.Conflict, "This email address is already registered.");

            var user = await API.CreateUser(
                                 userId,
                                 I18NString.Create(displayName ?? username),
                                 emailAddress,
                                 password,
                                 SkipDefaultNotifications:  true,
                                 SkipNewUserEMail:          true,
                                 SkipNewUserNotifications:  true,
                                 EventTrackingId:           Request.EventTrackingId
                             );

            if (user is null)
                return HTTPExtAPI.AuthError(Request, HTTPStatusCode.InternalServerError, "The account could not be created.");

            return API.SignedIn(Request, HTTPStatusCode.Created, user, API.Sessions.Create(user.Id));

        }

        #endregion

    }

}
