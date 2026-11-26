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

using System.Diagnostics.CodeAnalysis;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Hermod.HTTP.Notifications;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The notification messages read back from the database file, as they
    /// were added, updated and removed - so that a restarted API holds the
    /// ones it held before.
    /// </summary>
    public partial class HTTPExtAPI
    {

        #region (private) ProcessNotificationMessageEvent(Command, Data, out ErrorResponse)

        /// <summary>
        /// Replay a notification message command from the database file.
        /// </summary>
        private Boolean ProcessNotificationMessageEvent(String                            Command,
                                                        JObject                           Data,
                                                        [NotNullWhen(false)] out String?  ErrorResponse)
        {

            if (Command == "removeNotificationMessage")
            {

                if (!NotificationMessage_Id.TryParse(Data["@id"]?.Value<String>() ?? "", out var notificationMessageId))
                {
                    ErrorResponse = "Missing or invalid '@id'!";
                    return false;
                }

                if (notificationMessages.Remove(notificationMessageId, out var removed))
                    removed.API = null;

                ErrorResponse = null;
                return true;

            }

            if (!NotificationMessage.TryParseJSON(Data,
                                                  out var notificationMessage,
                                                  out ErrorResponse))
            {
                return false;
            }

            switch (Command)
            {

                case "addNotificationMessage":

                    if (!notificationMessages.TryAdd(notificationMessage.Id, notificationMessage))
                    {
                        ErrorResponse = $"Notification message '{notificationMessage.Id}' already exists!";
                        return false;
                    }

                    break;

                case "addNotificationMessageIfNotExists":

                    if (!notificationMessages.TryAdd(notificationMessage.Id, notificationMessage))
                        return true;

                    break;

                case "addOrUpdateNotificationMessage":

                    notificationMessages[notificationMessage.Id] = notificationMessage;

                    break;

                case "updateNotificationMessage":

                    if (!notificationMessages.ContainsKey(notificationMessage.Id))
                    {
                        ErrorResponse = $"Notification message '{notificationMessage.Id}' to be updated does not exist!";
                        return false;
                    }

                    notificationMessages[notificationMessage.Id] = notificationMessage;

                    break;

                default:
                    ErrorResponse = $"Unknown notification message command '{Command}'!";
                    return false;

            }

            notificationMessage.API = this;
            return true;

        }

        #endregion

    }

}
