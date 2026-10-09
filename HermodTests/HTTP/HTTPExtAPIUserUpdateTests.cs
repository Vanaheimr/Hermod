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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Mail;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// An account being saved is there to be found all the while: the old
    /// one until the new one stands in its place, never neither. A request
    /// signed in by its session finds its account by the session's user id;
    /// one that came while the account was taken out to be put back found
    /// nobody, was answered 401, and the page that sent it signed out - right
    /// after its own account was saved (found by the meter).
    /// </summary>
    [TestFixture]
    public class HTTPExtAPIUserUpdateTests
    {

        #region Data

        private String directory = "";

        private readonly List<HTTPServer> servers = [];

        /// <summary>
        /// An account that looks, while what it is linked to is copied over to
        /// it - the edges to its groups and organizations, the last thing done
        /// before it takes the old one's place - whether its id finds anybody.
        /// </summary>
        /// <remarks>
        /// Asks the store it is given, not User.API: that is set by the store
        /// as it saves, and a test of the store should not lean on it.
        /// </remarks>
        private sealed class Looking(HTTPExtAPI  Accounts,
                                     User_Id     Id,
                                     String      Name)

            : User(Id,
                   I18NString.Create(Name),
                   SimpleEMailAddress.Parse($"{Id}@example.test"),
                   AcceptedEULA:     DateTimeOffset.UtcNow.AddDays(-1),
                   IsAuthenticated:  true)

        {

            public Boolean? FoundWhileCopying { get; private set; }

            public override void CopyAllLinkedDataFromBase(User OldUser)
            {
                FoundWhileCopying = Accounts.TryGetUser(Id, out var found) && found is not null;
                base.CopyAllLinkedDataFromBase(OldUser);
            }

        }

        #endregion

        #region SetUp / TearDown

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), $"hermod-user-update-{Guid.NewGuid():N}") + Path.DirectorySeparatorChar;
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


        #region An_Account_Saved_Whole_Is_There_All_The_While()

        /// <summary>
        /// AddOrUpdateUser - what SET ~/users/{UserId} saves with - and
        /// UpdateUser with the new account: while the new account is being
        /// linked, the id still finds the old one.
        /// </summary>
        [Test]
        public async Task An_Account_Saved_Whole_Is_There_All_The_While()
        {

            var api    = await StartAPI();
            var alice  = await NewUser(api, "alice");

            var saved  = new Looking(api, alice.Id, "Alice Liddell");
            var added  = await api.AddOrUpdateUser(saved, SkipUserUpdatedNotifications: true);

            Assert.That(added.Result, Is.EqualTo(CommandResult.Success), added.Description.FirstText());

            var again   = new Looking(api, alice.Id, "Alice L.");
            var updated = await api.UpdateUser(again, SkipUserUpdatedNotifications: true);

            Assert.That(updated.Result, Is.EqualTo(CommandResult.Success), updated.Description.FirstText());

            Assert.Multiple(() => {
                Assert.That(saved.FoundWhileCopying,  Is.True, "AddOrUpdateUser took the account out before the new one stood in its place");
                Assert.That(again.FoundWhileCopying,  Is.True, "UpdateUser took the account out before the new one stood in its place");
                Assert.That(api.TryGetUser(alice.Id, out var now) ? now?.Name.FirstText() : null, Is.EqualTo("Alice L."));
            });

        }

        #endregion

        #region An_Account_Changed_By_Its_Builder_Is_There_All_The_While()

        /// <summary>
        /// UpdateUser with a delegate makes the new account itself, so nothing
        /// can look from inside it: a reader asks for the account, as every
        /// request signed in by a session does, all through 300 changes, and
        /// must never find nobody.
        /// </summary>
        [Test]
        public async Task An_Account_Changed_By_Its_Builder_Is_There_All_The_While()
        {

            var api      = await StartAPI();
            var alice    = await NewUser(api, "alice");
            var stop     = 0;
            var missed   = 0;
            var asked    = 0L;

            var reader   = Task.Factory.StartNew(() => {
                               while (Volatile.Read(ref stop) == 0)
                               {
                                   if (!api.TryGetUser(alice.Id, out var found) || found is null)
                                       Interlocked.Increment(ref missed);
                                   asked++;
                               }
                           }, TaskCreationOptions.LongRunning);

            try
            {
                for (var i = 0; i < 300; i++)
                {

                    Assert.That(api.TryGetUser(alice.Id, out var current) && current is not null, Is.True, $"change {i}: nobody to change");

                    var changed = await api.UpdateUser(current!,
                                                       builder => builder.Telegram = $"alice{i}",
                                                       SkipUserUpdatedNotifications: true);

                    Assert.That(changed.Result, Is.EqualTo(CommandResult.Success), changed.Description.FirstText());

                }
            }
            finally
            {
                Volatile.Write(ref stop, 1);
                await reader;
            }

            Assert.That(asked,   Is.Positive, "the reader asked nothing");
            Assert.That(missed,  Is.Zero,     $"the account was not there {missed} time(s) of {asked} while it was being changed");

        }

        #endregion


        #region (private) Helpers

        private async Task<HTTPExtAPI> StartAPI()
        {

            var server = new HTTPServer(
                             IPAddress:  IPv4Address.Localhost,
                             TCPPort:    IPPort.Zero,
                             AutoStart:  false
                         );

            servers.Add(server);

            var api    = new HTTPExtAPI(
                             server,
                             RootPath:              HTTPPath.Parse("/accounts"),
                             SkipURLTemplates:      true,
                             DisableNotifications:  true,
                             LoggingPath:           directory,
                             MinUserIdLength:       3
                         );

            await api.LoadDatabase();

            return api;

        }

        private static async Task<IUser> NewUser(HTTPExtAPI  API,
                                                 String      Name)
        {

            var user = await API.CreateUser(
                                 User_Id.Parse(Name),
                                 I18NString.Create(Name),
                                 SimpleEMailAddress.Parse($"{Name}@example.test"),
                                 "Correct-Horse-1",
                                 SkipDefaultNotifications:  true,
                                 SkipNewUserEMail:          true,
                                 SkipNewUserNotifications:  true,
                                 AcceptedEULA:              DateTimeOffset.UtcNow.AddDays(-1),
                                 IsAuthenticated:           true
                             );

            Assert.That(user, Is.Not.Null, $"'{Name}' could not be created");

            return user!;

        }

        #endregion

    }

}
