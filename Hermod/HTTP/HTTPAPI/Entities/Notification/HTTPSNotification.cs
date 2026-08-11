/*
 * Copyright (c) 2014-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using System.Diagnostics.CodeAnalysis;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP.Notifications
{

    /// <summary>
    /// Extension methods for HTTPS Notifications.
    /// </summary>
    public static class HTTPSNotificationExtensions
    {

        #region AddHTTPSNotification(this HTTPExtAPI, User, NotificationMessageType,  RemoteURL, Method = null, HTTPAuthentication = null)

        public static Task AddHTTPSNotification(this HTTPExtAPI          HTTPExtAPI,
                                                User                     User,
                                                NotificationMessageType  NotificationMessageType,
                                                URL                      RemoteURL,
                                                HTTPMethod?              Method               = null,
                                                IHTTPAuthentication?     HTTPAuthentication   = null)

            => HTTPExtAPI.AddNotification(
                   User,
                   new HTTPSNotification(
                       RemoteURL,
                       [ NotificationMessageType ],
                       Method,
                       HTTPAuthentication
                   ),
                   NotificationMessageType
               );

        #endregion

        #region AddHTTPSNotification(this HTTPExtAPI, User, NotificationMessageTypes, RemoteURL, Method = null, HTTPAuthentication = null)

        public static Task AddHTTPSNotification(this HTTPExtAPI                       HTTPExtAPI,
                                                User                                  User,
                                                IEnumerable<NotificationMessageType>  NotificationMessageTypes,
                                                URL                                   RemoteURL,
                                                HTTPMethod?                           Method               = null,
                                                IHTTPAuthentication?                  HTTPAuthentication   = null)

            => HTTPExtAPI.AddNotification(
                   User,
                   new HTTPSNotification(
                       RemoteURL,
                       NotificationMessageTypes,
                       Method,
                       HTTPAuthentication
                   ),
                   NotificationMessageTypes
               );

        #endregion


        #region GetHTTPSNotifications(this HTTPExtAPI, User,         params NotificationMessageTypes)

        public static IEnumerable<HTTPSNotification> GetHTTPSNotifications(this HTTPExtAPI                   HTTPExtAPI,
                                                                           User                              User,
                                                                           params NotificationMessageType[]  NotificationMessageTypes)


            => HTTPExtAPI.GetNotificationsOf<HTTPSNotification>(
                   User,
                   NotificationMessageTypes
               );

        #endregion

        #region GetHTTPSNotifications(this HTTPExtAPI, Organization, params NotificationMessageTypes)

        public static IEnumerable<HTTPSNotification> GetHTTPSNotifications(this HTTPExtAPI                   HTTPExtAPI,
                                                                           Organization                      Organization,
                                                                           params NotificationMessageType[]  NotificationMessageTypes)


            => HTTPExtAPI.GetNotificationsOf<HTTPSNotification>(
                   Organization,
                   NotificationMessageTypes
               );

        #endregion

        #region GetHTTPSNotifications(this HTTPExtAPI, UserGroup,    params NotificationMessageTypes)

        public static IEnumerable<HTTPSNotification> GetHTTPSNotifications(this HTTPExtAPI                   HTTPExtAPI,
                                                                           UserGroup                         UserGroup,
                                                                           params NotificationMessageType[]  NotificationMessageTypes)


            => HTTPExtAPI.GetNotificationsOf<HTTPSNotification>(
                   UserGroup,
                   NotificationMessageTypes
               );

        #endregion


        //public static Notifications UnregisterHTTPSNotification(this HTTPExtAPIX  HTTPExtAPI,
        //                                                        User           User,
        //                                                        String         URL,
        //                                                        String         BasicAuthenticationLogin     = null,
        //                                                        String         BasicAuthenticationPassword  = null)

        //    => HTTPExtAPI.UnregisterNotification<HTTPSNotification>(User,
        //                                                          a => a.URL                == URL &&
        //                                                               a.BasicAuthenticationLogin    == BasicAuthenticationLogin &&
        //                                                               a.BasicAuthenticationPassword == BasicAuthenticationPassword);


        //public static Notifications UnregisterHTTPSNotification(this HTTPExtAPIX  HTTPExtAPI,
        //                                                        User_Id        User,
        //                                                        String         URL,
        //                                                        String         BasicAuthenticationLogin     = null,
        //                                                        String         BasicAuthenticationPassword  = null)

        //    => HTTPExtAPI.UnregisterNotification<HTTPSNotification>(User,
        //                                                          a => a.URL                == URL &&
        //                                                               a.BasicAuthenticationLogin    == BasicAuthenticationLogin &&
        //                                                               a.BasicAuthenticationPassword == BasicAuthenticationPassword);


        //public static Notifications UnregisterHTTPSNotification(this HTTPExtAPIX    HTTPExtAPI,
        //                                                        User             User,
        //                                                        NotificationMessageType  NotificationMessageType,
        //                                                        String           URL,
        //                                                        String           BasicAuthenticationLogin     = null,
        //                                                        String           BasicAuthenticationPassword  = null)

        //    => HTTPExtAPI.UnregisterNotification<HTTPSNotification>(User,
        //                                                          NotificationMessageType,
        //                                                          a => a.URL                == URL &&
        //                                                               a.BasicAuthenticationLogin    == BasicAuthenticationLogin &&
        //                                                               a.BasicAuthenticationPassword == BasicAuthenticationPassword);

        //public static Notifications UnregisterHTTPSNotification(this HTTPExtAPIX    HTTPExtAPI,
        //                                                        User_Id          User,
        //                                                        NotificationMessageType  NotificationMessageType,
        //                                                        String           URL,
        //                                                        String           BasicAuthenticationLogin     = null,
        //                                                        String           BasicAuthenticationPassword  = null)

        //    => HTTPExtAPI.UnregisterNotification<HTTPSNotification>(User,
        //                                                          NotificationMessageType,
        //                                                          a => a.URL                == URL &&
        //                                                               a.BasicAuthenticationLogin    == BasicAuthenticationLogin &&
        //                                                               a.BasicAuthenticationPassword == BasicAuthenticationPassword);

    }


    /// <summary>
    /// An HTTPS notification.
    /// </summary>
    public class HTTPSNotification : ANotification,
                                     IEquatable<HTTPSNotification>,
                                     IComparable<HTTPSNotification>
    {

        #region Data

        /// <summary>
        /// The JSON-LD context of this object.
        /// </summary>
        public const String JSONLDContext = "https://opendata.social/contexts/UsersAPI/HTTPSNotification";

        #endregion

        #region Properties

        /// <summary>
        /// The URL for of HTTPS notification.
        /// </summary>
        public URL                   RemoteURL             { get; }

        /// <summary>
        /// The HTTP method of this HTTPS notification.
        /// </summary>
        public HTTPMethod            Method                { get; }

        /// <summary>
        /// An optional HTTP Basic Auth login for the HTTPS notification.
        /// </summary>
        public IHTTPAuthentication?  HTTPAuthentication    { get; }

        /// <summary>
        /// An optional HTTP request timeout.
        /// </summary>
        public TimeSpan?             RequestTimeout        { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTPS notification.
        /// </summary>
        /// <param name="RemoteURL">The URL of this HTTPS notification.</param>
        /// <param name="Method">The HTTP method of this HTTPS notification.</param>
        /// <param name="HTTPAuthentication">An optional HTTP authentication.</param>
        /// <param name="RequestTimeout"></param>
        /// <param name="NotificationMessageTypes">An optional enumeration of notification message types.</param>
        /// <param name="Description">Some description to remember why this notification was created.</param>
        public HTTPSNotification(URL                                   RemoteURL,
                                 IEnumerable<NotificationMessageType>  NotificationMessageTypes,
                                 HTTPMethod?                           Method                        = null,
                                 IHTTPAuthentication?                  HTTPAuthentication            = null,
                                 TimeSpan?                             RequestTimeout                = null,
                                 I18NString?                           Description                   = null)

            : base(NotificationMessageTypes,
                   Description,
                   String.Concat(nameof(HTTPSNotification),
                                 RemoteURL,
                                 Method ?? HTTPMethod.POST))

        {

            this.RemoteURL           = RemoteURL;
            this.Method              = Method ?? HTTPMethod.POST;
            this.HTTPAuthentication  = HTTPAuthentication;
            this.RequestTimeout      = RequestTimeout;

        }

        #endregion


        #region Parse   (JSON)

        public static HTTPSNotification Parse(JObject JSON)
        {

            if (TryParse(JSON, out var notification, out _))
                return notification;

            return null;

        }

        #endregion

        #region TryParse(JSON, out Notification)

        public static Boolean TryParse(JObject                                      JSON,
                                       [NotNullWhen(true)]  out HTTPSNotification?  Notification,
                                       [NotNullWhen(false)] out String?             ErrorResponse)
        {

            Notification = null;

            if (JSON["@context"]?.Value<String>() == JSONLDContext)
            {

                if (!JSON.ParseMandatory("url",
                                         "notification URL",
                                         URL.TryParse,
                                         out URL remoteURL,
                                         out ErrorResponse))
                {
                    return false;
                }

                if (JSON.ParseOptional("method",
                                       "notification HTTP method",
                                       s => HTTPMethod.TryParse(s),
                                       out HTTPMethod? method,
                                       out ErrorResponse))
                {
                    if (ErrorResponse is not null)
                        return false;
                }

                if (JSON.ParseOptionalJSON("description",
                                           "notification description",
                                           I18NString.TryParse,
                                           out I18NString? description,
                                           out ErrorResponse))
                {
                    if (ErrorResponse is not null)
                        return false;
                }

                Notification = new HTTPSNotification(
                                   remoteURL,
                                  (JSON["messageTypes"  ] as JArray)?.SafeSelect(element => NotificationMessageType.Parse(element.Value<String>())),
                                   method ?? HTTPMethod.POST,
                                   //JSON["basicAuth"     ]?["login"   ]?.Value<String>(),
                                   //JSON["basicAuth"     ]?["password"]?.Value<String>(),
                                   //JSON["APIKey"] is not null ? APIKey_Id.Parse(JSON["APIKey"        ]?.Value<String>()) : new APIKey_Id?(),
                                   null,
                                   JSON["requestTimeout"] is not null ? TimeSpan.FromSeconds((Double) JSON["requestTimeout"]?.Value<Int32>()) : new TimeSpan?(),
                                   description
                               );

                return true;

            }

            Notification   = null;
            ErrorResponse  = "Error parsing HTTPS notification!";
            return false;

        }

        #endregion

        #region ToJSON(Embedded = false)

        public override JObject ToJSON(Boolean Embedded = false)

            => JSONObject.Create(

                   !Embedded
                       ? new JProperty("@context",          JSONLDContext.ToString())
                       : null,

                   new JProperty("method",                Method.ToString()),
                   new JProperty("url",                   RemoteURL.ToString()),

                   //BasicAuthenticationLogin.   IsNotNullOrEmpty() &&
                   //BasicAuthenticationPassword.IsNotNullOrEmpty()
                   //    ? new JProperty("basicAuth",
                   //          new JObject(
                   //              new JProperty("login",     BasicAuthenticationLogin),
                   //              new JProperty("password",  BasicAuthenticationPassword)
                   //          )
                   //      )
                   //    : null,

                   //APIKey.HasValue
                   //    ? new JProperty("APIKey",          APIKey.Value.ToString())
                   //    : null,

                   RequestTimeout.HasValue
                       ? new JProperty("requestTimeout",  RequestTimeout.Value.TotalSeconds)
                       : null,

                   NotificationMessageTypes.SafeAny()
                       ? new JProperty("messageTypes",    new JArray(NotificationMessageTypes.Select(msgType => msgType.ToString())))
                       : null,

                   Description.IsNotNullOrEmpty()
                       ? new JProperty("description",     Description)
                       : null

               );

        #endregion


        #region OptionalEquals(EMailNotification)

        public override Boolean OptionalEquals(ANotification other)

            => other is HTTPSNotification httpsNotification &&
               this.OptionalEquals(httpsNotification);

        public Boolean OptionalEquals(HTTPSNotification other)

            => Method.   Equals(other.Method)                              &&
               RemoteURL.Equals(other.RemoteURL)                           &&

               //String.Equals(BasicAuthenticationLogin,    other.BasicAuthenticationLogin)    &&
               //String.Equals(BasicAuthenticationPassword, other.BasicAuthenticationPassword) &&
               //String.Equals(APIKey,             other.APIKey)             &&

               String.Equals(Description,        other.Description)        &&

               notificationMessageTypes.SetEquals(other.notificationMessageTypes);

        #endregion


        #region IComparable<HTTPSNotification> Members

        #region CompareTo(ANotification)

        public override Int32 CompareTo(ANotification? other)
            => other is not null
                   ? SortKey.CompareTo(other.SortKey)
                   : throw new ArgumentNullException(nameof(other), "The given notification must not be null!");

        #endregion

        #region CompareTo(HTTPSNotification)

        public Int32 CompareTo(HTTPSNotification? other)
        {

            if (other is null)
                throw new ArgumentNullException(nameof(other), "The given HTTPS notification must not be null!");

            var c = RemoteURL.CompareTo(other.RemoteURL);
            if (c != 0)
                return c;

            return Method.CompareTo(other.Method);

        }

        #endregion

        #endregion

        #region IEquatable<HTTPSNotification> Members

        #region Equals(ANotification)

        public override Boolean Equals(ANotification? other)
            => other is not null &&
               SortKey.Equals(other.SortKey);

        #endregion

        #region Equals(HTTPSNotification)

        public Boolean Equals(HTTPSNotification? other)

            => other is not null                 &&
               RemoteURL.Equals(other.RemoteURL) &&
               Method.   Equals(other.Method);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Get the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()
            => SortKey.GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
            => String.Concat(nameof(HTTPSNotification), ": ", Method, " ", RemoteURL);

        public override Int32 CompareTo(Object? obj)
        {
            throw new NotImplementedException();
        }

        #endregion

    }

}
