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

using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// Every subscriber of an event that HTTPExtAPI raises about a change it
    /// has made - a user added, a user group added, and sixteen more like
    /// them - is called and waited for, one after another; and what one of
    /// them throws goes to HandleErrors, not to whoever made the change.
    /// </summary>
    /// <remarks>
    /// The events used to be raised by calling them and awaiting what came
    /// back. A multicast delegate that returns a Task returns the task of its
    /// last subscriber, and only that one. The subscribers before it were
    /// started and never waited for: AddUser returned while they were still at
    /// work, and whatever they threw afterwards nobody ever saw. A subscriber
    /// that threw before it had a task to return - one that is not async -
    /// ended the call right there, and the subscribers behind it were never
    /// called at all. And what did come out of the call, from the last
    /// subscriber or from one that threw at once, came out of AddUser as an
    /// error, for a user who had been added all the same.
    ///
    /// Two of the eighteen events are tested here, one of each of the two
    /// shapes their call sites had: OnUserAdded, whose sites hand every
    /// subscriber a timestamp taken beforehand, and OnUserGroupAdded, whose
    /// sites took it inside the call.
    /// </remarks>
    [TestFixture]
    public class HTTPExtAPIEventTests
    {

        #region Data

        private String directory = "";

        /// <summary>
        /// The servers this test made. Dropped without being disposed of,
        /// each kept its timers running for the rest of the test run.
        /// </summary>
        private readonly List<HTTPServer> servers = [];

        #endregion

        #region SetUp / TearDown

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), $"hermod-events-{Guid.NewGuid():N}") + Path.DirectorySeparatorChar;
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


        #region AddUser_Waits_For_Every_Subscriber_Not_Only_The_Last()

        /// <summary>
        /// A slow subscriber, and a fast one behind it: AddUser returns once
        /// both have finished, and the second is called once the first has.
        /// </summary>
        /// <remarks>
        /// Calling the event handed back the fast subscriber's task, finished
        /// before it was even looked at. So AddUser returned - and let go of
        /// the lock on the users - while the slow one had only just begun.
        /// </remarks>
        [Test]
        public async Task AddUser_Waits_For_Every_Subscriber_Not_Only_The_Last()
        {

            var api    = await StartAPI();
            var order  = new ConcurrentQueue<String>();

            api.OnUserAdded += async (timestamp, user, eventTrackingId, currentUserId) => {
                order.Enqueue("first began");
                // Long enough for an AddUser that did not wait for this to have returned.
                await Task.Delay(300);
                order.Enqueue("first finished");
            };

            api.OnUserAdded += (timestamp, user, eventTrackingId, currentUserId) => {
                order.Enqueue("second");
                return Task.CompletedTask;
            };

            var added = await AddUser(api, "alice");

            order.Enqueue("added");

            Assert.Multiple(() => {

                Assert.That(added.Result,  Is.EqualTo(CommandResult.Success),  added.Description.FirstText());

                Assert.That(order,         Is.EqualTo(new[] { "first began", "first finished", "second", "added" }),
                            "AddUser returned, or the second subscriber was called, before the first one had finished.");

            });

        }

        #endregion

        #region A_Subscriber_That_Throws_At_Once_Does_Not_Silence_The_Ones_Behind_It()

        /// <summary>
        /// A subscriber that throws before it has returned a task, and one
        /// behind it that is called all the same. AddUser says the user was
        /// added, and the failure is reported to HandleErrors.
        /// </summary>
        /// <remarks>
        /// Not async, and that is the point: its exception comes out of the
        /// call itself rather than out of a task, while the invocation list is
        /// still being worked through - and ended it there.
        /// </remarks>
        [Test]
        public async Task A_Subscriber_That_Throws_At_Once_Does_Not_Silence_The_Ones_Behind_It()
        {

            var api           = await StartAPI();
            var secondCalled  = false;

            api.OnUserAdded += (timestamp, user, eventTrackingId, currentUserId)
                => throw new InvalidOperationException("This subscriber is broken.");

            api.OnUserAdded += (timestamp, user, eventTrackingId, currentUserId) => {
                secondCalled = true;
                return Task.CompletedTask;
            };

            var added = await AddUser(api, "alice");

            Assert.Multiple(() => {

                Assert.That(secondCalled,  Is.True,
                            "A subscriber that threw kept the one behind it from being called.");

                Assert.That(added.Result,  Is.EqualTo(CommandResult.Success),
                            "AddUser reported the subscriber's failure as its own.");

                Assert.That(api.Errors,    Is.EqualTo(new[] { "addUser.OnUserAdded: This subscriber is broken." }),
                            "The subscriber's failure was not reported, or not as the one of this event.");

            });

        }

        #endregion

        #region A_Subscriber_That_Fails_Does_Not_Fail_The_Addition()

        /// <summary>
        /// A subscriber that fails after its first await: AddUser says the
        /// user was added, which it was, and still sends the sign-up e-mail it
        /// sends after the event; the failure is reported to HandleErrors.
        /// </summary>
        /// <remarks>
        /// The event is raised once the user is in the database file and in
        /// the API. It tells of a change, it is not asked about one, and there
        /// is nothing left for it to stop. But what the last subscriber threw
        /// came out of addUser all the same, past the sign-up e-mail, and
        /// AddUser turned it into an error - for a user who stayed. Whoever
        /// had asked was told it had failed, a second try was told that the
        /// user already exists, and the user never got the e-mail with the
        /// link to set a first password.
        ///
        /// The only subscriber, and so the last: an earlier one that failed
        /// after its first await was never looked at, so whether a failing
        /// subscriber failed AddUser depended on where it stood and on when it
        /// threw.
        /// </remarks>
        [Test]
        public async Task A_Subscriber_That_Fails_Does_Not_Fail_The_Addition()
        {

            var mailer  = new NullMailer();
            var api     = await StartAPI(mailer);

            api.OnUserAdded += async (timestamp, user, eventTrackingId, currentUserId) => {
                await Task.Yield();
                throw new InvalidOperationException("This subscriber failed after its first await.");
            };

            var added = await AddUser(api, "alice", SkipNewUserEMail: false);

            Assert.Multiple(() => {

                Assert.That(added.Result,                                   Is.EqualTo(CommandResult.Success),
                            "AddUser reported the subscriber's failure as its own.");

                Assert.That(api.TryGetUser(User_Id.Parse("alice"), out _),  Is.True);

                Assert.That(mailer.EMailEnvelops.Select(envelope => envelope.RcptTo.ToString()),
                            Has.Exactly(1).Contains("alice@example.test"),
                            "The sign-up e-mail was not sent.");

                Assert.That(api.Errors,  Is.EqualTo(new[] { "addUser.OnUserAdded: This subscriber failed after its first await." }),
                            "The subscriber's failure was not reported, or not as the one of this event.");

            });

        }

        #endregion


        #region AddUserGroup_Waits_For_Every_Subscriber_Not_Only_The_Last()

        /// <summary>
        /// The same for a user group: a slow subscriber and a fast one behind
        /// it, and AddUserGroup returns once both have finished, in turn.
        /// </summary>
        [Test]
        public async Task AddUserGroup_Waits_For_Every_Subscriber_Not_Only_The_Last()
        {

            var api    = await StartAPI();
            var order  = new ConcurrentQueue<String>();

            api.OnUserGroupAdded += async (timestamp, userGroup, eventTrackingId, currentUserId) => {
                order.Enqueue("first began");
                await Task.Delay(300);
                order.Enqueue("first finished");
            };

            api.OnUserGroupAdded += (timestamp, userGroup, eventTrackingId, currentUserId) => {
                order.Enqueue("second");
                return Task.CompletedTask;
            };

            var added = await api.AddUserGroup(NewGroup("admins"));

            order.Enqueue("added");

            Assert.Multiple(() => {

                Assert.That(added.Result,  Is.EqualTo(CommandResult.Success),  added.Description.FirstText());

                Assert.That(order,         Is.EqualTo(new[] { "first began", "first finished", "second", "added" }),
                            "AddUserGroup returned, or the second subscriber was called, before the first one had finished.");

            });

        }

        #endregion

        #region A_User_Group_Subscriber_That_Throws_At_Once_Does_Not_Silence_The_Ones_Behind_It()

        /// <summary>
        /// The same for a user group: a subscriber that throws before it has
        /// returned a task does not keep the one behind it from being called,
        /// nor AddUserGroup from saying that the group was added.
        /// </summary>
        [Test]
        public async Task A_User_Group_Subscriber_That_Throws_At_Once_Does_Not_Silence_The_Ones_Behind_It()
        {

            var api           = await StartAPI();
            var secondCalled  = false;

            api.OnUserGroupAdded += (timestamp, userGroup, eventTrackingId, currentUserId)
                => throw new InvalidOperationException("This subscriber is broken.");

            api.OnUserGroupAdded += (timestamp, userGroup, eventTrackingId, currentUserId) => {
                secondCalled = true;
                return Task.CompletedTask;
            };

            var added = await api.AddUserGroup(NewGroup("admins"));

            Assert.Multiple(() => {

                Assert.That(secondCalled,  Is.True,
                            "A subscriber that threw kept the one behind it from being called.");

                Assert.That(added.Result,  Is.EqualTo(CommandResult.Success),
                            "AddUserGroup reported the subscriber's failure as its own.");

                Assert.That(api.Errors,    Is.EqualTo(new[] { "addUserGroup.OnUserGroupAdded: This subscriber is broken." }),
                            "The subscriber's failure was not reported, or not as the one of this event.");

            });

        }

        #endregion

        #region Every_Subscriber_Is_Told_The_Same_Time()

        /// <summary>
        /// Two subscribers, the first of them slow: both are told the same
        /// time for the same user group.
        /// </summary>
        /// <remarks>
        /// This passed before, when all subscribers were handed the arguments
        /// of the one call. It is here for what replaced that call: the
        /// arguments are now put together once for each subscriber, in the
        /// lambda handed to InvokeAllAsync, and a Timestamp.Now in there would
        /// tell each subscriber the time it got its turn - the second one here
        /// a good 300 ms after the first.
        /// </remarks>
        [Test]
        public async Task Every_Subscriber_Is_Told_The_Same_Time()
        {

            var api         = await StartAPI();
            var timestamps  = new ConcurrentQueue<DateTimeOffset>();

            api.OnUserGroupAdded += async (timestamp, userGroup, eventTrackingId, currentUserId) => {
                timestamps.Enqueue(timestamp);
                await Task.Delay(300);
            };

            api.OnUserGroupAdded += (timestamp, userGroup, eventTrackingId, currentUserId) => {
                timestamps.Enqueue(timestamp);
                return Task.CompletedTask;
            };

            var added = await api.AddUserGroup(NewGroup("admins"));

            Assert.Multiple(() => {

                Assert.That(added.Result,           Is.EqualTo(CommandResult.Success),  added.Description.FirstText());

                Assert.That(timestamps,             Has.Count.EqualTo(2));

                Assert.That(timestamps.Distinct(),  Has.Exactly(1).Items,
                            "The subscribers were told different times for the same user group.");

            });

        }

        #endregion


        #region (private) Helpers

        /// <summary>
        /// A headless HTTPExtAPI that keeps what is reported to HandleErrors.
        /// </summary>
        /// <remarks>
        /// With a robot and an external DNS name, which the sign-up e-mail is
        /// written with, and the given mailer - the API's own NullMailer when
        /// there is none.
        /// </remarks>
        private sealed class ReportingAPI(HTTPServer              HTTPServer,
                                          String                  LoggingPath,
                                          ISMTPSubmissionClient?  Mailer)

            : HTTPExtAPI(HTTPServer,
                         RootPath:              HTTPPath.Parse("/accounts"),
                         ExternalDNSName:       "example.test",
                         APIRobotEMailAddress:  new EMailAddress(SimpleEMailAddress.Parse("robot@example.test"), "Robot"),
                         SMTPSubmissionClient:  Mailer,
                         SkipURLTemplates:      true,
                         DisableNotifications:  true,
                         LoggingPath:           LoggingPath,
                         MinUserIdLength:       3,
                         MinUserGroupIdLength:  3)

        {

            private readonly ConcurrentQueue<String> errors = new();

            /// <summary>
            /// Everything reported to HandleErrors so far, as "Caller: message".
            /// </summary>
            public IEnumerable<String> Errors
                => errors.ToArray();

            public override Task HandleErrors(String     Module,
                                              String     Caller,
                                              Exception  ExceptionOccurred)
            {
                errors.Enqueue($"{Caller}: {ExceptionOccurred.Message}");
                return Task.CompletedTask;
            }

        }

        private async Task<ReportingAPI> StartAPI(ISMTPSubmissionClient? Mailer = null)
        {

            var api = new ReportingAPI(
                          new HTTPServer(
                              IPAddress:  IPv4Address.Localhost,
                              TCPPort:    IPPort.Zero,
                              AutoStart:  false
                          ),
                          directory,
                          Mailer
                      );

            servers.Add(api.HTTPServer);

            await api.LoadDatabase();

            return api;

        }

        /// <summary>
        /// Add a new user of the given name, without its default notifications,
        /// without notifications about it, and without the sign-up e-mail
        /// unless asked for.
        /// </summary>
        private static Task<AddUserResult> AddUser(HTTPExtAPI  API,
                                                   String      Name,
                                                   Boolean     SkipNewUserEMail   = true)

            => API.AddUser(
                   new User(
                       Id:     User_Id.Parse(Name),
                       Name:   I18NString.Create(Name),
                       EMail:  SimpleEMailAddress.Parse($"{Name}@example.test")
                   ),
                   SkipDefaultNotifications:  true,
                   SkipNewUserEMail:          SkipNewUserEMail,
                   SkipNewUserNotifications:  true
               );

        private static UserGroup NewGroup(String Name)

            => new (
                   UserGroup_Id.Parse(Name),
                   I18NString.Create(Languages.en, Name)
               );

        #endregion

    }

}
