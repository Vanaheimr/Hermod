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

        /// <summary>
        /// The servers StartAPI made, disposed of after each test.
        /// </summary>
        private readonly List<HTTPServer> servers = [];

        #endregion

        #region SetUp / TearDown

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), $"hermod-notificationmessages-{Guid.NewGuid():N}") + Path.DirectorySeparatorChar;
        }

        [TearDown]
        public async Task TearDown()
        {

            foreach (var server in servers)
                await server.DisposeAsync();

            servers.Clear();

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


        #region A_Restarted_API_Holds_The_Notification_Messages_It_Held()

        /// <summary>
        /// Added, updated and removed, and the API started anew on the same
        /// database file: it holds the messages it held, as they were last.
        /// </summary>
        /// <remarks>
        /// The database file had every change of a notification message, but
        /// nothing read them back: a restarted API held none. Nor could it
        /// have - the parser looked for the data under "name" and refused a
        /// message of no owner, and an update, written without its JSON-LD
        /// context, was refused for that. An update also took the message out
        /// of the API without putting the new one in.
        /// </remarks>
        [Test]
        public async Task A_Restarted_API_Holds_The_Notification_Messages_It_Held()
        {

            var api       = await StartAPI();

            var kept      = await api.AddNotificationMessage(new NotificationMessage(
                                                                 Timestamp.Now,
                                                                 NotificationMessageType.Parse("maintenance"),
                                                                 new JObject(new JProperty("text", "Kept.")),
                                                                 [ Organization_Id.Parse("operatorOrg") ]
                                                             ));

            var updated   = await api.AddNotificationMessage(NewMessage());
            var removed   = await api.AddNotificationMessage(NewMessage());

            Assert.That(kept,     Is.Not.Null);
            Assert.That(updated,  Is.Not.Null);
            Assert.That(removed,  Is.Not.Null);

            await api.UpdateNotificationMessage(new NotificationMessage(
                                                    updated!.Id,
                                                    updated.Timestamp,
                                                    updated.Type,
                                                    new JObject(new JProperty("text", "Updated.")),
                                                    []
                                                ));

            await api.RemoveNotificationMessage(removed!);

            Assert.Multiple(() => {
                Assert.That(api.NotificationMessageExists(updated.Id),                   Is.True, "an update keeps the message");
                Assert.That(api.GetNotificationMessage(updated.Id)?.Data["text"]?.ToString(),  Is.EqualTo("Updated."));
            });


            var restarted = await StartAPI();

            Assert.Multiple(() => {

                Assert.That(restarted.NotificationMessages.Select(message => message.Id),
                            Is.EquivalentTo(new[] { kept!.Id, updated.Id }));

                var keptAgain = restarted.GetNotificationMessage(kept.Id);

                Assert.That(keptAgain?.Type,                                             Is.EqualTo(NotificationMessageType.Parse("maintenance")));
                Assert.That(keptAgain?.Data["text"]?.ToString(),                         Is.EqualTo("Kept."));
                Assert.That(keptAgain?.Owners,                                           Is.EqualTo(new[] { Organization_Id.Parse("operatorOrg") }));
                Assert.That(restarted.GetNotificationMessage(updated.Id)?.Data["text"]?.ToString(),  Is.EqualTo("Updated."));

            });

            // Taken over by the restarted API: removed there, and so written.
            var result = await restarted.RemoveNotificationMessage(restarted.GetNotificationMessage(kept.Id)!);

            Assert.That(result?.IsSuccess, Is.True, result?.ToString());

        }

        #endregion


        #region (private) StartAPI()

        /// <summary>
        /// A headless HTTPExtAPI on this test's directory, with what its
        /// database file holds read back - a first start or a restart.
        /// </summary>
        private async Task<HTTPExtAPI> StartAPI()
        {

            var server = new HTTPServer(
                             IPAddress:  IPv4Address.Localhost,
                             TCPPort:    IPPort.Zero,
                             AutoStart:  false
                         );

            servers.Add(server);

            var api = NewAPI(server);

            await api.LoadDatabase();

            return api;

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
