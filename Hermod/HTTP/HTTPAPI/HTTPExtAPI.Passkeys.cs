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
using org.GraphDefined.Vanaheimr.Hermod.Passkeys;
using org.GraphDefined.Vanaheimr.Hermod.HTTP.Notifications;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The passkeys (WebAuthn credentials) of the users: kept per user,
    /// found by credential id, and written to the database file like every
    /// other change. The ceremonies for registering a passkey and signing
    /// in with one live in PasskeyCeremonies; the verification itself is
    /// Hermod.Passkeys.WebAuthn.
    /// </summary>
    public partial class HTTPExtAPI
    {

        #region Data

        public static NotificationMessageType  addPasskey_MessageType     = NotificationMessageType.Parse("addPasskey");
        public static NotificationMessageType  updatePasskey_MessageType  = NotificationMessageType.Parse("updatePasskey");
        public static NotificationMessageType  removePasskey_MessageType  = NotificationMessageType.Parse("removePasskey");

        private readonly ConcurrentDictionary<User_Id, ConcurrentDictionary<String, Passkey>>  passkeys       = [];
        private readonly ConcurrentDictionary<String, User_Id>                                  passkeyOwners  = new (StringComparer.Ordinal);

        #endregion

        #region Properties

        /// <summary>
        /// The WebAuthn ceremonies in progress: the challenges handed out for
        /// a registration or a sign-in, valid for two minutes, usable once.
        /// </summary>
        public CeremonyStore  PasskeyCeremonies    { get; } = new ();

        #endregion


        #region GetPasskeys(UserId)

        /// <summary>
        /// The passkeys of the given user, oldest first.
        /// </summary>
        /// <param name="UserId">A user identification.</param>
        public IEnumerable<Passkey> GetPasskeys(User_Id UserId)

            => passkeys.TryGetValue(UserId, out var userPasskeys)
                   ? userPasskeys.Values.OrderBy(passkey => passkey.CreatedAt).ToArray()
                   : [];

        #endregion

        #region TryGetPasskey(CredentialId, out User, out Passkey)

        /// <summary>
        /// Find the passkey with the given credential id and the user it belongs to.
        /// </summary>
        /// <param name="CredentialId">A credential id, Base64URL as the browser reports it.</param>
        /// <param name="User">The owner of the passkey.</param>
        /// <param name="Passkey">The passkey.</param>
        public Boolean TryGetPasskey(String                              CredentialId,
                                     [NotNullWhen(true)] out IUser?      User,
                                     [NotNullWhen(true)] out Passkey?    Passkey)
        {

            if (CredentialId.IsNotNullOrEmpty() &&
                passkeyOwners.TryGetValue(CredentialId, out var userId) &&
                passkeys.     TryGetValue(userId,       out var userPasskeys) &&
                userPasskeys. TryGetValue(CredentialId, out var passkey) &&
                TryGetUser(userId, out var user))
            {
                User     = user;
                Passkey  = passkey;
                return true;
            }

            User     = null;
            Passkey  = null;
            return false;

        }

        #endregion

        #region AddPasskey   (User, Passkey, EventTrackingId = null, CurrentUserId = null)

        /// <summary>
        /// Register a verified passkey for the given user. Fails when the user
        /// is unknown or the credential id is already registered, for anybody.
        /// </summary>
        /// <param name="User">The owner of the passkey.</param>
        /// <param name="Passkey">The passkey, as WebAuthn.TryVerifyRegistration() returned it.</param>
        /// <param name="EventTrackingId">An optional unique event tracking identification for correlating this request with other events.</param>
        /// <param name="CurrentUserId">An optional user identification initiating this command/request.</param>
        public async Task<Boolean> AddPasskey(IUser              User,
                                              Passkey            Passkey,
                                              EventTracking_Id?  EventTrackingId   = null,
                                              User_Id?           CurrentUserId     = null)
        {

            if (!UserExists(User.Id) || !passkeyOwners.TryAdd(Passkey.Id, User.Id))
                return false;

            passkeys.GetOrAdd(User.Id, _ => new (StringComparer.Ordinal))[Passkey.Id] = Passkey;

            await WriteToDatabaseFile(
                      addPasskey_MessageType,
                      PasskeyJSON(User.Id, Passkey),
                      EventTrackingId,
                      CurrentUserId
                  );

            return true;

        }

        #endregion

        #region UpdatePasskey(UserId, Passkey, EventTrackingId = null, CurrentUserId = null)

        /// <summary>
        /// Replace a stored passkey by an updated copy, e.g. with a new
        /// signature counter, last-used timestamp or name.
        /// </summary>
        /// <param name="UserId">The owner of the passkey.</param>
        /// <param name="Passkey">The updated passkey.</param>
        /// <param name="EventTrackingId">An optional unique event tracking identification for correlating this request with other events.</param>
        /// <param name="CurrentUserId">An optional user identification initiating this command/request.</param>
        public async Task<Boolean> UpdatePasskey(User_Id            UserId,
                                                 Passkey            Passkey,
                                                 EventTracking_Id?  EventTrackingId   = null,
                                                 User_Id?           CurrentUserId     = null)
        {

            if (!passkeys.TryGetValue(UserId, out var userPasskeys) ||
                !userPasskeys.ContainsKey(Passkey.Id))
            {
                return false;
            }

            userPasskeys[Passkey.Id] = Passkey;

            await WriteToDatabaseFile(
                      updatePasskey_MessageType,
                      PasskeyJSON(UserId, Passkey),
                      EventTrackingId,
                      CurrentUserId
                  );

            return true;

        }

        #endregion

        #region RemovePasskey(UserId, CredentialId, EventTrackingId = null, CurrentUserId = null)

        /// <summary>
        /// Remove the passkey with the given credential id from the given user.
        /// </summary>
        /// <param name="UserId">The owner of the passkey.</param>
        /// <param name="CredentialId">The credential id of the passkey.</param>
        /// <param name="EventTrackingId">An optional unique event tracking identification for correlating this request with other events.</param>
        /// <param name="CurrentUserId">An optional user identification initiating this command/request.</param>
        public async Task<Boolean> RemovePasskey(User_Id            UserId,
                                                 String             CredentialId,
                                                 EventTracking_Id?  EventTrackingId   = null,
                                                 User_Id?           CurrentUserId     = null)
        {

            if (!passkeys.TryGetValue(UserId, out var userPasskeys) ||
                !userPasskeys.TryRemove(CredentialId, out _))
            {
                return false;
            }

            passkeyOwners.TryRemove(CredentialId, out _);

            await WriteToDatabaseFile(
                      removePasskey_MessageType,
                      new JObject(
                          new JProperty("userId",        UserId.ToString()),
                          new JProperty("credentialId",  CredentialId)
                      ),
                      EventTrackingId,
                      CurrentUserId
                  );

            return true;

        }

        #endregion


        #region (private) ProcessPasskeyEvent(Command, Data, out ErrorResponse)

        /// <summary>
        /// Replay a passkey command from the database file.
        /// </summary>
        private Boolean ProcessPasskeyEvent(String                            Command,
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

                case "addPasskey":
                case "updatePasskey":

                    if (Data["passkey"] is not JObject passkeyJSON)
                    {
                        ErrorResponse = "Missing 'passkey'!";
                        return false;
                    }

                    if (!Passkey.TryParse(passkeyJSON, out var passkey, out ErrorResponse))
                        return false;

                    passkeys.GetOrAdd(userId, _ => new (StringComparer.Ordinal))[passkey.Id] = passkey;
                    passkeyOwners[passkey.Id] = userId;
                    return true;

                case "removePasskey":

                    var credentialId = Data["credentialId"]?.Value<String>() ?? "";

                    if (passkeys.TryGetValue(userId, out var userPasskeys))
                        userPasskeys.TryRemove(credentialId, out _);

                    passkeyOwners.TryRemove(credentialId, out _);
                    return true;

                default:
                    ErrorResponse = $"Unknown passkey command '{Command}'!";
                    return false;

            }

        }

        #endregion

        #region (private static) PasskeyJSON(UserId, Passkey)

        private static JObject PasskeyJSON(User_Id  UserId,
                                           Passkey  Passkey)

            => new (
                   new JProperty("userId",   UserId.ToString()),
                   new JProperty("passkey",  Passkey.ToStorageJSON())
               );

        #endregion

    }

}
