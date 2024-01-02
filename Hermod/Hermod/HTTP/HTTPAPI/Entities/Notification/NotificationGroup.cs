/*
 * Copyright (c) 2014-2024 GraphDefined GmbH <achim.friedland@graphdefined.com> <achim.friedland@graphdefined.com>
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

using System;
using System.Linq;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP.Notifications
{

    public class NotificationGroup //: AEntity<NotificationGroup_Id,
                                   //          NotificationGroup>
    {

        #region Data

        /// <summary>
        /// The default JSON-LD context of organizations.
        /// </summary>
        public readonly static JSONLDContext DefaultJSONLDContext = JSONLDContext.Parse("https://opendata.social/contexts/UsersAPI/notificationGroup");

        #endregion

        #region Properties

        public NotificationGroup_Id                         Id               { get; }

        public I18NString                                   Title            { get; }

        public I18NString                                   Description      { get; }

        public NotificationVisibility                       Visibility       { get; }

        private readonly List<NotificationMessageDescription> notifications = new List<NotificationMessageDescription>();

        public IEnumerable<NotificationMessageDescription> Notifications
            => notifications;

        #endregion

        #region Constructor(s)

        public NotificationGroup(NotificationGroup_Id                         Id,
                                 I18NString                                   Title,
                                 I18NString                                   Description,
                                 NotificationVisibility                       Visibility,
                                 IEnumerable<NotificationMessageDescription>  Notifications)
        {

            #region Initial checks

            if (Id.IsNullOrEmpty)
                throw new ArgumentNullException(nameof(Id),             "The given notification group identification must not be null or empty!");

            if (Title.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Title),          "The given multi-language headline string must not be null or empty!");

            if (Description == null)
                throw new ArgumentNullException(nameof(Description),    "The given multi-language description string must not be null or empty!");

            if (Notifications.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Notifications),  "The given enumeration of notifications must not be null or empty!");

            #endregion

            this.Id             = Id;
            this.Title          = Title;
            this.Description    = Description;
            this.Visibility     = Visibility;
            this.notifications  = Notifications != null ? new List<NotificationMessageDescription>(Notifications) : new List<NotificationMessageDescription>();

        }

        #endregion


        public void Add(params NotificationMessageDescription[] NotificationMessageDescriptions)
        {
            if (NotificationMessageDescriptions.SafeAny())
                foreach (var notificationMessageDescription in NotificationMessageDescriptions)
                    this.notifications.Add(notificationMessageDescription);
        }

        public void Remove(NotificationMessageDescription NotificationMessageDescription)
        {
            if (NotificationMessageDescription != null)
                this.notifications.Remove(NotificationMessageDescription);
        }

        public void Remove(params NotificationMessageType[] NotificationMessageTypes)
        {

            if (NotificationMessageTypes.SafeAny())
            {

                var notificationMessageTypes = new HashSet<NotificationMessageType>(NotificationMessageTypes);

                foreach (var remove in notifications.Where(nmd => notificationMessageTypes.Overlaps(nmd.Messages)).ToArray())
                    this.notifications.Remove(remove);

            }

        }


        #region ToJSON()

        public JObject ToJSON()

            => JSONObject.Create(

                         new JProperty("@id",             Id.                  ToString()),

                         new JProperty("@context",        DefaultJSONLDContext.ToString()),

                         new JProperty("title",           Title.               ToJSON()),

                   Description.IsNotNullOrEmpty()
                       ? new JProperty("description",     Description.         ToJSON())
                       : null,

                         new JProperty("visibility",      Visibility.          ToString().ToLower()),

                   Notifications.SafeAny()
                       ? new JProperty("notifications",   new JArray(Notifications.Select(info => info.ToJSON())))
                       : null

               );

        #endregion

    }

}
