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

using System;
using System.Linq;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
//using org.GraphDefined.Vanaheimr.Hermod;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP.Notifications
{

    public class NotificationMessageDescription
    {

        #region Properties

        public I18NString                            Title          { get; }

        public I18NString                            Description    { get; }

        public NotificationVisibility                Visibility     { get; }

        public IEnumerable<NotificationTag>          Tags           { get; }

        public IEnumerable<NotificationMessageType>  Messages       { get; }

        #endregion

        #region Constructor(s)

        public NotificationMessageDescription(I18NString                    Title,
                                              I18NString                    Description,
                                              NotificationVisibility        Visibility,
                                              NotificationMessageType       Message)

            : this(Title,
                   Description,
                   Visibility,
                   new NotificationTag[0],
                   new NotificationMessageType[] { Message })

        { }

        public NotificationMessageDescription(I18NString                    Title,
                                              I18NString                    Description,
                                              NotificationVisibility        Visibility,
                                              NotificationTag               Tag,
                                              NotificationMessageType       Message)

            : this(Title,
                   Description,
                   Visibility,
                   new NotificationTag[] { Tag },
                   new NotificationMessageType[] { Message })

        { }

        public NotificationMessageDescription(I18NString                    Title,
                                              I18NString                    Description,
                                              NotificationVisibility        Visibility,
                                              IEnumerable<NotificationTag>  Tags,
                                              NotificationMessageType       Message)

            : this(Title,
                   Description,
                   Visibility,
                   Tags,
                   new NotificationMessageType[] { Message })

        { }

        public NotificationMessageDescription(I18NString                            Title,
                                              I18NString                            Description,
                                              NotificationVisibility                Visibility,
                                              IEnumerable<NotificationTag>          Tags,
                                              IEnumerable<NotificationMessageType>  Messages)
        {

            #region Initial checks

            if (Title.      IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Title),        "The given multi-language headline string must not be null or empty!");

            if (Description.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Description),  "The given multi-language description string must not be null or empty!");

            if (Messages.   IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Messages),     "The given enumeration of notification messages must not be null or empty!");

            #endregion

            this.Title        = Title;
            this.Description  = Description;
            this.Visibility   = Visibility;
            this.Tags         = Tags ?? new NotificationTag[0];
            this.Messages     = Messages;

        }

        #endregion


        #region ToJSON()

        public JObject ToJSON()

            => JSONObject.Create(

                         new JProperty("title",        Title.ToJSON()),

                   Description.IsNotNullOrEmpty()
                       ? new JProperty("description",  Description.ToJSON())
                       : null,

                         new JProperty("visibility",   Visibility.ToString().ToLower()),

                   Tags.SafeAny()
                       ? new JProperty("tags",         new JArray(Tags.Select(tag => tag.ToString())))
                       : null,

                   Messages.SafeAny()
                       ? new JProperty("messages",     new JArray(Messages.Select(message => message.ToString())))
                       : null

               );

        #endregion

    }

}
