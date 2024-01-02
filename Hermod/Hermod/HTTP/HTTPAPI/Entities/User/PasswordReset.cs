/*
 * Copyright (c) 2014-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of HTTPExtAPI <https://www.github.com/Vanaheimr/HTTPExtAPI>
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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Information for resetting user passwords.
    /// </summary>
    public class PasswordReset
    {

        #region Data

        /// <summary>
        /// The JSON-LD context of this object.
        /// </summary>
        public const String JSONLDContext  = "https://opendata.social/contexts/HTTPExtAPI+json/passwordReset";

        #endregion

        #region Properties

        /// <summary>
        /// The creation timestamp of the password-reset-information.
        /// </summary>
        public DateTime            Timestamp          { get; }

        /// <summary>
        /// An enumeration of valid users.
        /// </summary>
        public IEnumerable<IUser>  Users              { get; }

        /// <summary>
        /// A security token to authorize the password reset.
        /// </summary>
        public SecurityToken_Id    SecurityToken1     { get; }

        /// <summary>
        /// An optional second security token to authorize the password reset.
        /// </summary>
        public SecurityToken_Id?   SecurityToken2     { get; }

        /// <summary>
        /// An optional unique event tracking identification for correlating this request with other events.
        /// </summary>
        public EventTracking_Id    EventTrackingId    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new information object for resetting user passwords.
        /// </summary>
        /// <param name="User">A valid user.</param>
        /// <param name="SecurityToken1">A security token to authorize the password reset.</param>
        /// <param name="SecurityToken2">An optional second security token to authorize the password reset.</param>
        /// <param name="EventTrackingId">An optional unique event tracking identification for correlating this request with other events.</param>
        public PasswordReset(IUser              User,
                             SecurityToken_Id   SecurityToken1,
                             SecurityToken_Id?  SecurityToken2    = null,
                             EventTracking_Id?  EventTrackingId   = null)

            : this(org.GraphDefined.Vanaheimr.Illias.Timestamp.Now,
                   new[] { User },
                   SecurityToken1,
                   SecurityToken2,
                   EventTrackingId)

        { }


        /// <summary>
        /// Create a new information object for resetting user passwords.
        /// </summary>
        /// <param name="Users">An enumeration of valid users.</param>
        /// <param name="SecurityToken1">A security token to authorize the password reset.</param>
        /// <param name="SecurityToken2">An optional second security token to authorize the password reset.</param>
        /// <param name="EventTrackingId">An optional unique event tracking identification for correlating this request with other events.</param>
        public PasswordReset(IEnumerable<IUser>  Users,
                             SecurityToken_Id    SecurityToken1,
                             SecurityToken_Id?   SecurityToken2    = null,
                             EventTracking_Id?   EventTrackingId   = null)

            : this(org.GraphDefined.Vanaheimr.Illias.Timestamp.Now,
                   Users,
                   SecurityToken1,
                   SecurityToken2,
                   EventTrackingId)

        { }


        /// <summary>
        /// Create a new information object for resetting user passwords.
        /// </summary>
        /// <param name="Timestamp">The creation timestamp of the password-reset-information.</param>
        /// <param name="Users">An enumeration of valid users.</param>
        /// <param name="SecurityToken1">A security token to authorize the password reset.</param>
        /// <param name="SecurityToken2">An optional second security token to authorize the password reset.</param>
        /// <param name="EventTrackingId">An optional unique event tracking identification for correlating this request with other events.</param>
        public PasswordReset(DateTime            Timestamp,
                             IEnumerable<IUser>  Users,
                             SecurityToken_Id    SecurityToken1,
                             SecurityToken_Id?   SecurityToken2    = null,
                             EventTracking_Id?   EventTrackingId   = null)
        {

            if (!Users.Any())
                throw new ArgumentNullException(nameof(Users), "The given enumeration of users must not be null or empty!");

            this.Timestamp        = Timestamp;
            this.Users            = new HashSet<IUser>(Users);
            this.SecurityToken1   = SecurityToken1;
            this.SecurityToken2   = SecurityToken2;
            this.EventTrackingId  = EventTrackingId ?? EventTracking_Id.New;

        }

        #endregion


        #region ToJSON(Embedded = true)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="Embedded">Whether this data structure is embedded into another data structure.</param>
        public JObject ToJSON(Boolean Embedded = true)

            => JSONObject.Create(

                   Embedded
                       ? null
                       : new JProperty("@context",        JSONLDContext.       ToString()),

                   new JProperty("timestamp",             Timestamp.           ToIso8601()),
                   new JProperty("userIds",               new JArray(Users.Select(user => user.Id.ToString()))),
                   new JProperty("securityToken1",        SecurityToken1.      ToString()),

                   SecurityToken2.HasValue
                       ? new JProperty("securityToken2",  SecurityToken2.Value.ToString())
                       : null,

                   new JProperty("eventTrackingId",       EventTrackingId.     ToString())

               );

        #endregion

        #region (static) TryParseJSON(JSONObject, UserProvider, out PasswordReset, out ErrorResponse, IgnoreContextMismatches = true)

        public static Boolean TryParseJSON(JObject               JSONObject,
                                           UserProviderDelegate  UserProvider,
                                           out PasswordReset?    PasswordReset,
                                           out String?           ErrorResponse,
                                           Boolean               IgnoreContextMismatches = true)

        {

            try
            {

                PasswordReset = default;

                if (JSONObject is null)
                {
                    ErrorResponse = "The given JSON object must not be null!";
                    return false;
                }

                #region Parse Context            [mandatory]

                if (!IgnoreContextMismatches)
                {

                    if (!JSONObject.ParseMandatoryText("@context",
                                                       "JSON-LD context",
                                                       out String Context,
                                                       out ErrorResponse))
                    {
                        ErrorResponse = @"The JSON-LD ""@context"" information is missing!";
                        return false;
                    }

                    if (Context != JSONLDContext)
                    {
                        ErrorResponse = @"The given JSON-LD ""@context"" information '" + Context + "' is not supported!";
                        return false;
                    }

                }

                #endregion

                #region Parse Timestamp          [mandatory]

                if (!JSONObject.ParseMandatory("timestamp",
                                               "timestamp",
                                               out DateTime Timestamp,
                                               out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region Parse Users              [mandatory]

                if (!JSONObject.ParseMandatory("userIds",
                                               "user identifications",
                                               out JArray UserIdArray,
                                               out ErrorResponse))
                {
                    return false;
                }

                HashSet<User_Id>? userIds = null;

                try
                {

                    userIds = UserIdArray.
                                  Where (jsonvalue => jsonvalue != null).
                                  Select(jsonvalue => User_Id.Parse(jsonvalue.Value<String>())).
                                  ToHashSet();

                }
                catch
                {
                    ErrorResponse = "The given array of users '" + UserIdArray + "' is invalid!";
                    return false;
                }

                if (!userIds.Any())
                {
                    ErrorResponse = "The given array of users '" + UserIdArray + "' must not be empty!";
                    return false;
                }

                HashSet<IUser> Users = new();

                foreach (var userId in userIds)
                {

                    if (!UserProvider(userId, out var user))
                    {
                        ErrorResponse = "The given user '" + userId + "' is unknown or invalid!";
                        return false;
                    }

                    Users.Add(user);

                }

                #endregion

                #region Parse SecurityToken1     [mandatory]

                if (!JSONObject.ParseMandatory("securityToken1",
                                               "security token #1",
                                               SecurityToken_Id.TryParse,
                                               out SecurityToken_Id SecurityToken1,
                                               out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region Parse SecurityToken2     [optional]

                if (JSONObject.ParseOptional("securityToken2",
                                             "security token #2",
                                             SecurityToken_Id.TryParse,
                                             out SecurityToken_Id? SecurityToken2,
                                             out ErrorResponse))
                {
                    if (ErrorResponse is not null)
                        return false;
                }

                #endregion

                #region Parse EventTrackingId    [optional]

                if (JSONObject.ParseOptional("eventTrackingId",
                                             "event tracking identification",
                                             EventTracking_Id.TryParse,
                                             out EventTracking_Id EventTrackingId,
                                             out ErrorResponse))
                {
                    if (ErrorResponse is not null)
                        return false;
                }

                #endregion


                PasswordReset = new PasswordReset(Timestamp,
                                                  Users,
                                                  SecurityToken1,
                                                  SecurityToken2,
                                                  EventTrackingId);

                return true;

            }
            catch (Exception e)
            {
                ErrorResponse = e.Message;
                PasswordReset = null;
                return false;
            }

        }

        #endregion


    }

}
