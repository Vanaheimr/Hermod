/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP.Notifications;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// What adding an SSH key to a user came to.
    /// </summary>
    public enum AddSSHKeyOutcome
    {
        /// <summary>The key was let in.</summary>
        Added,
        /// <summary>The user has this key already; nothing was written.</summary>
        AlreadyThere,
        /// <summary>The line was refused, for the reason given.</summary>
        Refused,
        /// <summary>There is no such user.</summary>
        UnknownUser
    }

    /// <summary>
    /// What adding an SSH key to a user came to: the key, where it was added or
    /// there already, and the reason, where it was refused.
    /// </summary>
    /// <param name="Outcome">What it came to.</param>
    /// <param name="SSHKey">The key added, or the one there already.</param>
    /// <param name="Reason">Why the line was refused, or the user unknown.</param>
    public sealed record AddSSHKeyResult(AddSSHKeyOutcome  Outcome,
                                         UserSSHKey?       SSHKey,
                                         String?           Reason)
    {

        /// <summary>Whether the key was let in now.</summary>
        public Boolean IsAdded
            => Outcome == AddSSHKeyOutcome.Added;

    }


    /// <summary>
    /// The SSH public keys of the users: kept per user and known by their
    /// fingerprints, written to the database file like every other change -
    /// so that who let which key in, and when, is in its hash chain - and read
    /// back from it at every start.
    /// </summary>
    /// <remarks>
    /// Where an SSH server lets somebody in with a key, it asks here, from
    /// memory: a key removed lets nobody in from that moment on. What may be
    /// done with the keys of another user is what may be done with that user's
    /// API keys: <see cref="CanImpersonate"/>.
    /// </remarks>
    public partial class HTTPExtAPI
    {

        #region Data

        public const  String                   SSHKeyFingerprintParameter  = "Fingerprint";

        public static NotificationMessageType  addSSHKey_MessageType       = NotificationMessageType.Parse("addSSHKey");
        public static NotificationMessageType  removeSSHKey_MessageType    = NotificationMessageType.Parse("removeSSHKey");

        private readonly ConcurrentDictionary<User_Id, ConcurrentDictionary<String, UserSSHKey>>  sshKeys = [];

        #endregion


        #region GetSSHKeys(UserId)

        /// <summary>
        /// The SSH keys of the given user, oldest first.
        /// </summary>
        /// <param name="UserId">A user identification.</param>
        public IReadOnlyList<UserSSHKey> GetSSHKeys(User_Id UserId)

            => sshKeys.TryGetValue(UserId, out var userKeys)
                   ? [.. userKeys.Values.OrderBy(key => key.Created).ThenBy(key => key.Fingerprint, StringComparer.Ordinal)]
                   : [];

        #endregion

        #region TryGetSSHKey(UserId, Fingerprint, out SSHKey)

        /// <summary>
        /// The SSH key of the given user with the given fingerprint.
        /// </summary>
        /// <param name="UserId">A user identification.</param>
        /// <param name="Fingerprint">A fingerprint, "SHA256:...".</param>
        /// <param name="SSHKey">The key.</param>
        public Boolean TryGetSSHKey(User_Id                               UserId,
                                    String                                Fingerprint,
                                    [NotNullWhen(true)] out UserSSHKey?   SSHKey)
        {

            if (sshKeys.TryGetValue(UserId, out var userKeys) &&
                userKeys.TryGetValue(Fingerprint, out SSHKey))
            {
                return true;
            }

            SSHKey = null;
            return false;

        }

        #endregion

        #region FindSSHKey(UserId, PublicKeyBlob, At)

        /// <summary>
        /// The key of the given user an SSH client offers - by its public key
        /// blob - where it may sign in at the given time: not one that only
        /// vouches for certificates, and only within its not-before and
        /// expiry-time. Null where there is none.
        /// </summary>
        /// <param name="UserId">A user identification.</param>
        /// <param name="PublicKeyBlob">The public key blob the client offers.</param>
        /// <param name="At">When the client signs in.</param>
        public UserSSHKey? FindSSHKey(User_Id             UserId,
                                      ReadOnlySpan<Byte>  PublicKeyBlob,
                                      DateTimeOffset      At)
        {

            if (!sshKeys.TryGetValue(UserId, out var userKeys))
                return null;

            foreach (var key in userKeys.Values)
                if (!key.Key.IsCertAuthority &&
                     key.Key.Matches(PublicKeyBlob) &&
                     key.Key.IsValidAt(At))
                {
                    return key;
                }

            return null;

        }

        #endregion

        #region AddSSHKey   (User, Line, Label = null, EventTrackingId = null, CurrentUserId = null, CreatedBy = null)

        /// <summary>
        /// Let the given user in with the key of the given <c>authorized_keys</c>
        /// line: written to the database file, with who did it and when - or
        /// refused, with why.
        /// </summary>
        /// <param name="User">The user.</param>
        /// <param name="Line">One line, as an authorized_keys file holds it, options and all.</param>
        /// <param name="Label">What the key is called, if anything.</param>
        /// <param name="EventTrackingId">An optional unique event tracking identification for correlating this request with other events.</param>
        /// <param name="CurrentUserId">An optional user identification initiating this command/request.</param>
        /// <param name="CreatedBy">Who lets the key in, where that is not a user - "the command line" - otherwise CurrentUserId.</param>
        public async Task<AddSSHKeyResult> AddSSHKey(IUser              User,
                                                     String             Line,
                                                     String?            Label             = null,
                                                     EventTracking_Id?  EventTrackingId   = null,
                                                     User_Id?           CurrentUserId     = null,
                                                     String?            CreatedBy         = null)
        {

            if (!UserExists(User.Id))
                return new (AddSSHKeyOutcome.UnknownUser, null, $"There is no user '{User.Id}'.");

            if (!UserSSHKey.TryCreate(Line,
                                      Label,
                                      Timestamp.Now,
                                      CreatedBy ?? CurrentUserId?.ToString(),
                                      out var sshKey,
                                      out var refused))
            {
                return new (AddSSHKeyOutcome.Refused, null, refused);
            }

            var userKeys = sshKeys.GetOrAdd(User.Id, _ => new (StringComparer.Ordinal));

            if (!userKeys.TryAdd(sshKey.Fingerprint, sshKey))
                return new (AddSSHKeyOutcome.AlreadyThere, userKeys.TryGetValue(sshKey.Fingerprint, out var there) ? there : sshKey, null);

            await WriteToDatabaseFile(
                      addSSHKey_MessageType,
                      SSHKeyJSON(User.Id, sshKey),
                      EventTrackingId,
                      CurrentUserId
                  );

            return new (AddSSHKeyOutcome.Added, sshKey, null);

        }

        #endregion

        #region RemoveSSHKey(UserId, Fingerprint, EventTrackingId = null, CurrentUserId = null)

        /// <summary>
        /// Take the key with the given fingerprint from the given user: nobody
        /// signs in with it from this moment on.
        /// </summary>
        /// <param name="UserId">The user.</param>
        /// <param name="Fingerprint">The fingerprint of the key, "SHA256:...".</param>
        /// <param name="EventTrackingId">An optional unique event tracking identification for correlating this request with other events.</param>
        /// <param name="CurrentUserId">An optional user identification initiating this command/request.</param>
        public async Task<Boolean> RemoveSSHKey(User_Id            UserId,
                                                String             Fingerprint,
                                                EventTracking_Id?  EventTrackingId   = null,
                                                User_Id?           CurrentUserId     = null)
        {

            if (!sshKeys.TryGetValue(UserId, out var userKeys) ||
                !userKeys.TryRemove(Fingerprint, out _))
            {
                return false;
            }

            await WriteToDatabaseFile(
                      removeSSHKey_MessageType,
                      new JObject(
                          new JProperty("userId",       UserId.ToString()),
                          new JProperty("fingerprint",  Fingerprint)
                      ),
                      EventTrackingId,
                      CurrentUserId
                  );

            return true;

        }

        #endregion


        #region (private) RemoveAllSSHKeys(User, EventTrackingId, CurrentUserId)

        /// <summary>
        /// Remove every SSH key of the given user, each with its "removeSSHKey"
        /// line: part of deleting the user, so that the next account made under
        /// the same id does not sign in with them.
        /// </summary>
        private async Task RemoveAllSSHKeys(IUser             User,
                                            EventTracking_Id  EventTrackingId,
                                            User_Id?          CurrentUserId)
        {

            foreach (var sshKey in GetSSHKeys(User.Id))
                await RemoveSSHKey(User.Id, sshKey.Fingerprint, EventTrackingId, CurrentUserId);

            ForgetSSHKeys(User.Id);

        }

        #endregion

        #region (private) ForgetSSHKeys   (UserId)

        /// <summary>
        /// Drop the SSH keys of the given user and write nothing: what the
        /// replay of a "deleteUser" line does.
        /// </summary>
        private void ForgetSSHKeys(User_Id UserId)

            => sshKeys.TryRemove(UserId, out _);

        #endregion


        #region (private) ProcessSSHKeyEvent(Command, Data, out ErrorResponse)

        /// <summary>
        /// Replay an SSH key command from the database file.
        /// </summary>
        private Boolean ProcessSSHKeyEvent(String                            Command,
                                           JObject                           Data,
                                           [NotNullWhen(false)] out String?  ErrorResponse)
        {

            ErrorResponse = null;

            if (!User_Id.TryParse(Data["userId"]?.Value<String>() ?? "", out var userId))
            {
                ErrorResponse = "Missing or invalid 'userId'!";
                return false;
            }

            switch (Command)
            {

                case "addSSHKey":

                    if (Data["sshKey"] is not JObject sshKeyJSON)
                    {
                        ErrorResponse = "Missing 'sshKey'!";
                        return false;
                    }

                    if (!UserSSHKey.TryParse(sshKeyJSON, out var sshKey, out ErrorResponse))
                        return false;

                    sshKeys.GetOrAdd(userId, _ => new (StringComparer.Ordinal))[sshKey.Fingerprint] = sshKey;
                    return true;

                case "removeSSHKey":

                    if (sshKeys.TryGetValue(userId, out var userKeys))
                        userKeys.TryRemove(Data["fingerprint"]?.Value<String>() ?? "", out _);

                    return true;

                default:
                    ErrorResponse = $"Unknown SSH key command '{Command}'!";
                    return false;

            }

        }

        #endregion

        #region (private static) SSHKeyJSON(UserId, SSHKey)

        private static JObject SSHKeyJSON(User_Id     UserId,
                                          UserSSHKey  SSHKey)

            => new (
                   new JProperty("userId",  UserId.ToString()),
                   new JProperty("sshKey",  SSHKey.ToJSON())
               );

        #endregion


        #region (private) RegisterSSHKeyURLTemplates()

        /// <summary>
        /// GET, ADD and DELETE ~/users/{UserId}/SSHKeys: what a user may do with
        /// their own keys, and whoever may impersonate them with theirs - as with
        /// the API keys.
        /// </summary>
        private void RegisterSSHKeyURLTemplates()
        {

            #region GET         ~/users/{UserId}/SSHKeys

            // ------------------------------------------------------------------------------------
            // curl -v -H "Accept: application/json" http://127.0.0.1:2100/users/ahzf/SSHKeys
            // ------------------------------------------------------------------------------------
            AddHandler(
                HTTPMethod.GET,
                HTTPPath.Root + "users/{UserId}/SSHKeys",
                HTTPContentType.Application.JSON_UTF8,
                HTTPDelegate: Request => {

                    if (!MayManageSSHKeysOf(Request, out var user, out var refusal, [ HTTPMethod.ADD, HTTPMethod.GET ]))
                        return Task.FromResult(refusal.AsImmutable);

                    return Task.FromResult(
                               SSHKeyResponse(Request,
                                              HTTPStatusCode.OK,
                                              new JArray(GetSSHKeys(user.Id).Select(sshKey => sshKey.ToJSON())),
                                              [ HTTPMethod.ADD, HTTPMethod.GET ]).AsImmutable
                           );

                });

            #endregion

            #region ADD         ~/users/{UserId}/SSHKeys

            // ------------------------------------------------------------------------------------------------------
            // curl -v -X ADD -H "Content-Type: application/json" -d '{"line":"ssh-ed25519 AAAA... alice@laptop",
            //      "label":"Alice's laptop"}' http://127.0.0.1:2100/users/ahzf/SSHKeys
            // ------------------------------------------------------------------------------------------------------
            AddHandler(
                HTTPMethod.ADD,
                HTTPPath.Root + "users/{UserId}/SSHKeys",
                HTTPContentType.Application.JSON_UTF8,
                HTTPDelegate: async Request => {

                    if (!MayManageSSHKeysOf(Request, out var user, out var refusal, [ HTTPMethod.ADD, HTTPMethod.GET ], out var httpUser))
                        return refusal.AsImmutable;

                    if (!Request.TryParseJSONObjectRequestBody(out var json, out var bodyRefusal))
                        return bodyRefusal!.AsImmutable;

                    if (json["line"]?.Value<String>() is not String line)
                        return SSHKeyResponse(Request,
                                              HTTPStatusCode.BadRequest,
                                              JSONObject.Create(new JProperty("description", "Missing 'line': the key, as one line of an authorized_keys file.")),
                                              [ HTTPMethod.ADD, HTTPMethod.GET ]).AsImmutable;

                    var added = await AddSSHKey(user,
                                                line,
                                                json["label"]?.Value<String>(),
                                                Request.EventTrackingId,
                                                httpUser.Id);

                    return (added.Outcome switch {

                               AddSSHKeyOutcome.Added         => SSHKeyResponse(Request, HTTPStatusCode.OK,         added.SSHKey!.ToJSON(),
                                                                                [ HTTPMethod.ADD, HTTPMethod.GET ]),

                               AddSSHKeyOutcome.AlreadyThere  => SSHKeyResponse(Request, HTTPStatusCode.Conflict,   JSONObject.Create(
                                                                                    new JProperty("description",  $"The key {added.SSHKey!.Fingerprint} is there already."),
                                                                                    new JProperty("fingerprint",  added.SSHKey!.Fingerprint)
                                                                                ),
                                                                                [ HTTPMethod.ADD, HTTPMethod.GET ]),

                               _                              => SSHKeyResponse(Request, HTTPStatusCode.BadRequest, JSONObject.Create(
                                                                                    new JProperty("description",  added.Reason)
                                                                                ),
                                                                                [ HTTPMethod.ADD, HTTPMethod.GET ])

                           }).AsImmutable;

                });

            #endregion

            #region DELETE      ~/users/{UserId}/SSHKeys/{Fingerprint}

            // ----------------------------------------------------------------------------------------------------------------------
            // curl -v -X DELETE -H "Accept: application/json" http://127.0.0.1:2100/users/ahzf/SSHKeys/7xq3Vb...-_ (Base64url)
            // ----------------------------------------------------------------------------------------------------------------------
            AddHandler(
                HTTPMethod.DELETE,
                // One string first: HTTPPath + string puts a '/' between the
                // two, which made the parameter's braces segments of their own.
                HTTPPath.Root + ("users/{UserId}/SSHKeys/{" + SSHKeyFingerprintParameter + "}"),
                HTTPContentType.Application.JSON_UTF8,
                HTTPDelegate: async Request => {

                    if (!MayManageSSHKeysOf(Request, out var user, out var refusal, [ HTTPMethod.DELETE ], out var httpUser))
                        return refusal.AsImmutable;

                    if (!Request.TryGetURLParameter(SSHKeyFingerprintParameter, out var fingerprintText) ||
                        fingerprintText.IsNullOrEmpty())
                    {
                        return SSHKeyResponse(Request,
                                              HTTPStatusCode.BadRequest,
                                              JSONObject.Create(new JProperty("description", "Missing fingerprint!")),
                                              [ HTTPMethod.DELETE ]).AsImmutable;
                    }

                    var fingerprint = UserSSHKey.FingerprintFromURL(fingerprintText);

                    if (!TryGetSSHKey(user.Id, fingerprint, out _))
                        return SSHKeyResponse(Request,
                                              HTTPStatusCode.NotFound,
                                              JSONObject.Create(new JProperty("description", $"'{user.Id}' has no key {fingerprint}.")),
                                              [ HTTPMethod.DELETE ]).AsImmutable;

                    return await RemoveSSHKey(user.Id, fingerprint, Request.EventTrackingId, httpUser.Id)

                               ? SSHKeyResponse(Request,
                                                HTTPStatusCode.OK,
                                                JSONObject.Create(new JProperty("fingerprint", fingerprint)),
                                                [ HTTPMethod.DELETE ]).AsImmutable

                               // Gone between the look and the removal: somebody else took it.
                               : SSHKeyResponse(Request,
                                                HTTPStatusCode.NotFound,
                                                JSONObject.Create(new JProperty("description", $"'{user.Id}' has no key {fingerprint}.")),
                                                [ HTTPMethod.DELETE ]).AsImmutable;

                });

            #endregion

        }

        #endregion

        #region (private) MayManageSSHKeysOf(Request, out User, out Refusal, Methods, out HTTPUser)

        private Boolean MayManageSSHKeysOf(HTTPRequest                                     Request,
                                           [NotNullWhen(true)]  out IUser?                 User,
                                           [NotNullWhen(false)] out HTTPResponse.Builder?  Refusal,
                                           HTTPMethod[]                                    Methods)

            => MayManageSSHKeysOf(Request, out User, out Refusal, Methods, out _);

        /// <summary>
        /// The user of the URL, where the one signed in may manage their SSH
        /// keys - their own, or those of a user they may impersonate - or the
        /// answer that says why not: 401 for nobody signed in, 404 for no such
        /// user, 403 for somebody else's keys.
        /// </summary>
        private Boolean MayManageSSHKeysOf(HTTPRequest                                     Request,
                                           [NotNullWhen(true)]  out IUser?                 User,
                                           [NotNullWhen(false)] out HTTPResponse.Builder?  Refusal,
                                           HTTPMethod[]                                    Methods,
                                           out IUser                                       HTTPUser)
        {

            User      = null;
            HTTPUser  = null!;

            // HTTP 401 Unauthorized, where nobody is signed in.
            if (!TryGetHTTPUser(Request,
                                out var httpUser,
                                out _,
                                out Refusal,
                                Recursive: true))
            {
                return false;
            }

            HTTPUser = httpUser;

            if (!Request.ParseUser(this,
                                   out var userId,
                                   out var user,
                                   out Refusal))
            {
                return false;
            }

            if (httpUser.Id != userId && !CanImpersonate(httpUser, user))
            {
                Refusal = SSHKeyResponse(Request,
                                         HTTPStatusCode.Forbidden,
                                         JSONObject.Create(new JProperty("description", "This operation is not allowed!")),
                                         Methods);
                return false;
            }

            User = user;
            return true;

        }

        #endregion

        #region (private) SSHKeyResponse(Request, StatusCode, Content, Methods)

        private HTTPResponse.Builder SSHKeyResponse(HTTPRequest     Request,
                                                    HTTPStatusCode  StatusCode,
                                                    JToken          Content,
                                                    HTTPMethod[]    Methods)

            => new (Request) {
                   HTTPStatusCode             = StatusCode,
                   Server                     = HTTPServer?.HTTPServerName,
                   Date                       = Timestamp.Now,
                   AccessControlAllowOrigin   = "*",
                   AccessControlAllowMethods  = Methods,
                   AccessControlAllowHeaders  = [ "Content-Type", "Accept", "Authorization" ],
                   ContentType                = HTTPContentType.Application.JSON_UTF8,
                   Content                    = Content.ToString(Newtonsoft.Json.Formatting.None).ToUTF8Bytes(),
                   Connection                 = ConnectionType.KeepAlive,
                   Vary                       = "Accept"
               };

        #endregion

    }

}
