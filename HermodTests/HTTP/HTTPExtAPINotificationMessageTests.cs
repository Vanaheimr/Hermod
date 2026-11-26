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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP.Notifications;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// A notification message an API holds can be removed from it - unless a
    /// kind of API says why not.
    /// </summary>
    /// <remarks>
    /// CanDeleteNotificationMessage answered "Currently not possible!" for
    /// every message, so RemoveNotificationMessage never removed one.
    /// </remarks>
    [TestFixture]
    public class HTTPExtAPINotificationMessageTests
    {

        #region Data

        private String directory = "";

        #endregion

        #region SetUp / TearDown

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), $"hermod-notificationmessages-{Guid.NewGuid():N}") + Path.DirectorySeparatorChar;
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }

        #endregion


        #region A_Notification_Message_Is_Removed()

        [Test]
        public async Task A_Notification_Message_Is_Removed()
        {

            var server = await HTTPServer.StartNew();

            try
            {

                var api      = NewAPI(server);
                await api.LoadDatabase();

                var message  = await api.AddNotificationMessage(NewMessage());

                Assert.That(message,                                     Is.Not.Null);
                Assert.That(api.NotificationMessageExists(message!.Id),  Is.True);

                var result   = await api.RemoveNotificationMessage(message);

                Assert.Multiple(() => {
                    Assert.That(result?.IsSuccess,                       Is.True, result?.ToString());
                    Assert.That(api.NotificationMessageExists(message.Id),  Is.False);
                    Assert.That(api.NotificationMessages,                Is.Empty);
                });

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region A_Kind_Of_API_May_Still_Refuse_It()

        [Test]
        public async Task A_Kind_Of_API_May_Still_Refuse_It()
        {

            var server = await HTTPServer.StartNew();

            try
            {

                var api      = new RefusingAPI(server, directory);
                await api.LoadDatabase();

                var message  = await api.AddNotificationMessage(NewMessage());
                var result   = await api.RemoveNotificationMessage(message!);

                Assert.Multiple(() => {
                    Assert.That(result?.IsSuccess,                        Is.False);
                    Assert.That(result?.ToString(),                       Does.Contain("Kept for the audit"));
                    Assert.That(api.NotificationMessageExists(message!.Id),  Is.True);
                });

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion


        #region (private) NewAPI(Server) / NewMessage()

        private HTTPExtAPI NewAPI(HTTPServer Server)

            => new (Server,
                    RootPath:              HTTPPath.Parse("/accounts"),
                    SkipURLTemplates:      true,
                    DisableNotifications:  true,
                    LoggingPath:           directory);

        private static NotificationMessage NewMessage()

            => new (Timestamp.Now,
                    NotificationMessageType.Parse("maintenance"),
                    new JObject(new JProperty("text", "The station is down for maintenance.")),
                    []);

        #endregion

        #region (private class) RefusingAPI

        private sealed class RefusingAPI(HTTPServer  Server,
                                         String      Directory)

            : HTTPExtAPI(Server,
                         RootPath:              HTTPPath.Parse("/accounts"),
                         SkipURLTemplates:      true,
                         DisableNotifications:  true,
                         LoggingPath:           Directory)

        {

            protected override I18NString? CanDeleteNotificationMessage(NotificationMessage NotificationMessage)

                => I18NString.Create("Kept for the audit!");

        }

        #endregion

    }

}
