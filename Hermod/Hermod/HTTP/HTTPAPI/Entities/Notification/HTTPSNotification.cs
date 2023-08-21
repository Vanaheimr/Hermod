/*
 * Copyright (c) 2014-2023 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP.Notifications
{

    /// <summary>
    /// Extension methods for HTTPS Notifications.
    /// </summary>
    public static class HTTPSNotificationExtensions
    {

        #region AddHTTPSNotification(this HTTPExtAPI, User, NotificationMessageType,  RemoteURL, Method = null, BasicAuthenticationLogin = null, BasicAuthenticationPassword = null, APIKey = null)

        public static Task AddHTTPSNotification(this HTTPExtAPI            HTTPExtAPI,
                                                User                     User,
                                                NotificationMessageType  NotificationMessageType,
                                                URL                      RemoteURL,
                                                HTTPMethod?              Method               = null,
                                                String                   BasicAuthenticationLogin      = null,
                                                String                   BasicAuthenticationPassword   = null,
                                                APIKey_Id?               APIKey               = null)

            => HTTPExtAPI.AddNotification(User,
                                        new HTTPSNotification(RemoteURL,
                                                              Method,
                                                              BasicAuthenticationLogin,
                                                              BasicAuthenticationPassword,
                                                              APIKey),
                                        NotificationMessageType);

        #endregion

        #region AddHTTPSNotification(this HTTPExtAPI, User, NotificationMessageTypes, RemoteURL, Method = null, BasicAuthenticationLogin = null, BasicAuthenticationPassword = null, APIKey = null)

        public static Task AddHTTPSNotification(this HTTPExtAPI                         HTTPExtAPI,
                                                User                                  User,
                                                IEnumerable<NotificationMessageType>  NotificationMessageTypes,
                                                URL                                   RemoteURL,
                                                HTTPMethod?                           Method               = null,
                                                String                                BasicAuthenticationLogin      = null,
                                                String                                BasicAuthenticationPassword   = null,
                                                APIKey_Id?                            APIKey               = null)

            => HTTPExtAPI.AddNotification(User,
                                        new HTTPSNotification(RemoteURL,
                                                              Method,
                                                              BasicAuthenticationLogin,
                                                              BasicAuthenticationPassword,
                                                              APIKey),
                                        NotificationMessageTypes);

        #endregion


        #region GetHTTPSNotifications(this HTTPExtAPI, User,         params NotificationMessageTypes)

        public static IEnumerable<HTTPSNotification> GetHTTPSNotifications(this HTTPExtAPI                     HTTPExtAPI,
                                                                           User                              User,
                                                                           params NotificationMessageType[]  NotificationMessageTypes)


            => HTTPExtAPI.GetNotificationsOf<HTTPSNotification>(User,
                                                              NotificationMessageTypes);

        #endregion

        #region GetHTTPSNotifications(this HTTPExtAPI, Organization, params NotificationMessageTypes)

        public static IEnumerable<HTTPSNotification> GetHTTPSNotifications(this HTTPExtAPI                     HTTPExtAPI,
                                                                           Organization                      Organization,
                                                                           params NotificationMessageType[]  NotificationMessageTypes)


            => HTTPExtAPI.GetNotificationsOf<HTTPSNotification>(Organization,
                                                              NotificationMessageTypes);

        #endregion

        #region GetHTTPSNotifications(this HTTPExtAPI, UserGroup,    params NotificationMessageTypes)

        public static IEnumerable<HTTPSNotification> GetHTTPSNotifications(this HTTPExtAPI                     HTTPExtAPI,
                                                                           UserGroup                         UserGroup,
                                                                           params NotificationMessageType[]  NotificationMessageTypes)


            => HTTPExtAPI.GetNotificationsOf<HTTPSNotification>(UserGroup,
                                                              NotificationMessageTypes);

        #endregion


        //public static Notifications UnregisterHTTPSNotification(this HTTPExtAPI  HTTPExtAPI,
        //                                                        User           User,
        //                                                        String         URL,
        //                                                        String         BasicAuthenticationLogin     = null,
        //                                                        String         BasicAuthenticationPassword  = null)

        //    => HTTPExtAPI.UnregisterNotification<HTTPSNotification>(User,
        //                                                          a => a.URL                == URL &&
        //                                                               a.BasicAuthenticationLogin    == BasicAuthenticationLogin &&
        //                                                               a.BasicAuthenticationPassword == BasicAuthenticationPassword);


        //public static Notifications UnregisterHTTPSNotification(this HTTPExtAPI  HTTPExtAPI,
        //                                                        User_Id        User,
        //                                                        String         URL,
        //                                                        String         BasicAuthenticationLogin     = null,
        //                                                        String         BasicAuthenticationPassword  = null)

        //    => HTTPExtAPI.UnregisterNotification<HTTPSNotification>(User,
        //                                                          a => a.URL                == URL &&
        //                                                               a.BasicAuthenticationLogin    == BasicAuthenticationLogin &&
        //                                                               a.BasicAuthenticationPassword == BasicAuthenticationPassword);


        //public static Notifications UnregisterHTTPSNotification(this HTTPExtAPI    HTTPExtAPI,
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

        //public static Notifications UnregisterHTTPSNotification(this HTTPExtAPI    HTTPExtAPI,
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
    /// A HTTPS notification.
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
        public URL         RemoteURL             { get; }

        /// <summary>
        /// The HTTP method of this HTTPS notification.
        /// </summary>
        public HTTPMethod  Method                { get; }

        /// <summary>
        /// An optional HTTP Basic Auth login for the HTTPS notification.
        /// </summary>
        public String      BasicAuthenticationLogin       { get; }

        /// <summary>
        /// An optional HTTP Basic Auth password for the HTTPS notification.
        /// </summary>
        public String      BasicAuthenticationPassword    { get; }

        /// <summary>
        /// An optional HTTP API Key for the HTTPS notification.
        /// </summary>
        public APIKey_Id?  APIKey                { get; }

        /// <summary>
        /// An optional HTTP request timeout.
        /// </summary>
        public TimeSpan?   RequestTimeout        { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTPS notification.
        /// </summary>
        /// <param name="RemoteURL">The URL of this HTTPS notification.</param>
        /// <param name="Method">The HTTP method of this HTTPS notification.</param>
        /// <param name="BasicAuthenticationLogin">An optional HTTP Basic Auth login for the HTTPS notification.</param>
        /// <param name="BasicAuthenticationPassword">An optional HTTP Basic Auth password for the HTTPS notification.</param>
        /// <param name="APIKey">An optional HTTP API Key for the HTTPS notification.</param>
        /// <param name="RequestTimeout"></param>
        /// <param name="NotificationMessageTypes">An optional enumeration of notification message types.</param>
        /// <param name="Description">Some description to remember why this notification was created.</param>
        public HTTPSNotification(URL                                   RemoteURL,
                                 HTTPMethod?                           Method                     = null,
                                 String                                BasicAuthenticationLogin            = null,
                                 String                                BasicAuthenticationPassword         = null,
                                 APIKey_Id?                            APIKey                     = null,
                                 TimeSpan?                             RequestTimeout             = null,
                                 IEnumerable<NotificationMessageType>  NotificationMessageTypes   = null,
                                 String                                Description                = null)

            : base(NotificationMessageTypes,
                   Description,
                   String.Concat(nameof(HTTPSNotification),
                                 RemoteURL,
                                 Method ?? HTTPMethod.POST))

        {

            this.RemoteURL           = RemoteURL;
            this.Method              = Method ?? HTTPMethod.POST;
            this.BasicAuthenticationLogin     = BasicAuthenticationLogin;
            this.BasicAuthenticationPassword  = BasicAuthenticationPassword;
            this.APIKey              = APIKey;
            this.RequestTimeout      = RequestTimeout;

        }

        #endregion


        #region Parse   (JSON)

        public static HTTPSNotification Parse(JObject JSON)
        {

            if (TryParse(JSON, out HTTPSNotification Notification))
                return Notification;

            return null;

        }

        #endregion

        #region TryParse(JSON, out Notification)

        public static Boolean TryParse(JObject JSON, out HTTPSNotification Notification)
        {

            var url = JSON["URL"]?.Value<String>();

            if (JSON["@context"]?.Value<String>() == JSONLDContext &&
                url.IsNotNullOrEmpty())
            {

                Notification = new HTTPSNotification(URL.Parse(JSON["URL"           ]?.Value<String>()),
                                                     JSON["method"        ] != null ? HTTPMethod.Parse(JSON["method"].Value<String>()) : HTTPMethod.POST,
                                                     JSON["basicAuth"     ]?["login"   ]?.Value<String>(),
                                                     JSON["basicAuth"     ]?["password"]?.Value<String>(),
                                                     JSON["APIKey"] != null ? APIKey_Id.Parse(JSON["APIKey"        ]?.Value<String>()) : new APIKey_Id?(),
                                                     JSON["RequestTimeout"] != null ? TimeSpan.FromSeconds((Double) JSON["RequestTimeout"]?.Value<Int32>()) : new TimeSpan?(),
                                                    (JSON["messageTypes"  ] as JArray)?.SafeSelect(element => NotificationMessageType.Parse(element.Value<String>())),
                                                     JSON["description"   ]?.Value<String>());

                return true;

            }

            Notification = null;
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
                   new JProperty("URL",                   RemoteURL.ToString()),

                   BasicAuthenticationLogin.   IsNotNullOrEmpty() &&
                   BasicAuthenticationPassword.IsNotNullOrEmpty()
                       ? new JProperty("basicAuth",
                             new JObject(
                                 new JProperty("login",     BasicAuthenticationLogin),
                                 new JProperty("password",  BasicAuthenticationPassword)
                             )
                         )
                       : null,

                   APIKey.HasValue
                       ? new JProperty("APIKey",          APIKey.Value.ToString())
                       : null,

                   RequestTimeout.HasValue
                       ? new JProperty("RequestTimeout",  RequestTimeout.Value.TotalSeconds)
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

               String.Equals(BasicAuthenticationLogin,    other.BasicAuthenticationLogin)    &&
               String.Equals(BasicAuthenticationPassword, other.BasicAuthenticationPassword) &&
               String.Equals(APIKey,             other.APIKey)             &&

               String.Equals(Description,        other.Description)        &&

               _NotificationMessageTypes.SetEquals(other._NotificationMessageTypes);

        #endregion


        #region IComparable<HTTPSNotification> Members

        #region CompareTo(ANotification)

        public override Int32 CompareTo(ANotification other)
            => SortKey.CompareTo(other.SortKey);

        #endregion

        #region CompareTo(HTTPSNotification)

        public Int32 CompareTo(HTTPSNotification other)
        {

            var c = RemoteURL.CompareTo(other.RemoteURL);
            if (c != 0)
                return c;

            return Method.CompareTo(other.Method);

        }

        #endregion

        #endregion

        #region IEquatable<HTTPSNotification> Members

        #region Equals(ANotification)

        public override Boolean Equals(ANotification other)
            => SortKey.Equals(other.SortKey);

        #endregion

        #region Equals(HTTPSNotification)

        public Boolean Equals(HTTPSNotification other)

            => RemoteURL.Equals(other.RemoteURL) &&
               Method.   Equals(other.Method);

        #endregion

        #endregion

        #region GetHashCode()

        /// <summary>
        /// Get the hashcode of this object.
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

        #endregion

    }

}
