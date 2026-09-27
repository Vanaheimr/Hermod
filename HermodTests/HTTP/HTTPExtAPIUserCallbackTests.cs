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
    /// The OnAdded and OnUpdated delegates that a caller hands to the user
    /// methods of HTTPExtAPI - AddUser, AddUserIfNotExists, AddOrUpdateUser
    /// and UpdateUser - are run and waited for; and what one of them throws
    /// is reported to HandleErrors, and does not fail the change.
    /// </summary>
    /// <remarks>
    /// These delegates return a Task, and the methods called them and dropped
    /// the Task. So a delegate ran up to its first await that did not finish
    /// at once, and the method returned - and let go of its lock on the users
    /// - while the delegate was still at work; and whatever it threw after
    /// that, nobody ever saw. Two callers in Hermod itself finish the change
    /// in such a delegate: CreateUser sets the password in one, and AddUser
    /// with an organization adds the membership in one.
    /// </remarks>
    [TestFixture]
    public class HTTPExtAPIUserCallbackTests
    {

        #region Data

        private const String Password  = "Correct-Horse-1";

        private String directory = "";

        #endregion

        #region SetUp / TearDown

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), $"hermod-callbacks-{Guid.NewGuid():N}") + Path.DirectorySeparatorChar;
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }

        #endregion


        #region Every_User_Method_Returns_Once_The_Delegate_It_Was_Handed_Has_Finished(Site)

        /// <summary>
        /// Every place at which a user method runs the delegate it was handed:
        /// a delegate that awaits a delay has finished when the method returns.
        /// </summary>
        /// <remarks>
        /// The overloads with an organization hand the method below them a
        /// delegate of their own, which adds the membership and then runs the
        /// caller's - so for them both places have to wait, the one in the
        /// overload and the one in the method below it.
        /// </remarks>
        [TestCaseSource(nameof(EveryPlaceThatRunsADelegate))]
        public async Task Every_User_Method_Returns_Once_The_Delegate_It_Was_Handed_Has_Finished(DelegateSite Site)
        {

            var api    = await StartAPI();
            var order  = new ConcurrentQueue<String>();

            var result = await Site.Call(
                                   api,
                                   async () => {
                                       order.Enqueue("delegate began");
                                       // Long enough for a method that did not wait for this to have returned.
                                       await Task.Delay(300);
                                       order.Enqueue("delegate finished");
                                   }
                               );

            order.Enqueue("returned");

            Assert.Multiple(() => {

                Assert.That(result.Result,  Is.EqualTo(CommandResult.Success),  result.Description.FirstText());

                Assert.That(order,          Is.EqualTo(new[] { "delegate began", "delegate finished", "returned" }),
                            $"{Site} returned before the delegate it was handed had finished.");

            });

        }

        #endregion

        #region Every_User_Method_Reports_A_Delegate_That_Fails(Site)

        /// <summary>
        /// Every place at which a user method runs the delegate it was handed:
        /// a delegate that fails after its first await is reported to
        /// HandleErrors, under the name of the method that ran it, and the
        /// method says that the change was made - which it was.
        /// </summary>
        /// <remarks>
        /// Its failure used to be in a Task nobody looked at. The name tells
        /// the two places of an overload with an organization apart: the
        /// caller's delegate is run by the overload, "AddUser.OnAdded", and
        /// the overload's own delegate - the membership - by the method below
        /// it, "addUser.OnAdded".
        /// </remarks>
        [TestCaseSource(nameof(EveryPlaceThatRunsADelegate))]
        public async Task Every_User_Method_Reports_A_Delegate_That_Fails(DelegateSite Site)
        {

            var api     = await StartAPI();

            var result  = await Site.Call(
                                    api,
                                    async () => {
                                        await Task.Yield();
                                        throw new InvalidOperationException("This delegate failed after its first await.");
                                    }
                                );

            Assert.Multiple(() => {

                Assert.That(result.Result,  Is.EqualTo(CommandResult.Success),
                            $"{Site} reported the failure of the delegate it was handed as its own.");

                Assert.That(api.Errors,     Is.EqualTo(new[] { $"{Site.ReportedAs}: This delegate failed after its first await." }),
                            "The failure of the delegate was not reported, or not as the one of this method.");

            });

        }

        #endregion


        #region A_Delegate_That_Fails_Does_Not_Fail_The_Addition(ThrowsAtOnce)

        /// <summary>
        /// A delegate that fails, at once or after its first await: AddUser
        /// says the user was added, which it was, and still sends the sign-up
        /// e-mail it sends after the delegate; the failure is reported to
        /// HandleErrors.
        /// </summary>
        /// <remarks>
        /// The delegate runs once the user is in the database file and in the
        /// API, and nothing is undone when it fails. A delegate that threw
        /// before it had a task to return - one that is not async - threw out
        /// of the call itself: past the sign-up e-mail, and out of AddUser as
        /// an error, for a user who stayed. A second try was then told that
        /// the user already exists, and the user never got the e-mail with the
        /// link to set a first password. One that threw after its first await
        /// was not noticed at all. Either way it is now the same: reported,
        /// and not the answer about the user.
        /// </remarks>
        [TestCase(true,   TestName = "A_Delegate_That_Throws_At_Once_Does_Not_Fail_The_Addition")]
        [TestCase(false,  TestName = "A_Delegate_That_Fails_After_Its_First_Await_Does_Not_Fail_The_Addition")]
        public async Task A_Delegate_That_Fails_Does_Not_Fail_The_Addition(Boolean ThrowsAtOnce)
        {

            var mailer  = new NullMailer();
            var api     = await StartAPI(mailer);

            var added   = await api.AddUser(
                                    NewUser("alice"),
                                    SkipDefaultNotifications:  true,
                                    SkipNewUserEMail:          false,
                                    SkipNewUserNotifications:  true,
                                    OnAdded:                   ThrowsAtOnce

                                                                   ? (timestamp, user, eventTrackingId, currentUserId)
                                                                         => throw new InvalidOperationException("This delegate failed.")

                                                                   : async (timestamp, user, eventTrackingId, currentUserId) => {
                                                                         await Task.Yield();
                                                                         throw new InvalidOperationException("This delegate failed.");
                                                                     }
                                );

            Assert.Multiple(() => {

                Assert.That(added.Result,                                   Is.EqualTo(CommandResult.Success),
                            "AddUser reported the failure of its delegate as its own.");

                Assert.That(api.TryGetUser(User_Id.Parse("alice"), out _),  Is.True);

                Assert.That(mailer.EMailEnvelops.Select(envelope => envelope.RcptTo.ToString()),
                            Has.Exactly(1).Contains("alice@example.test"),
                            "The sign-up e-mail was not sent.");

                Assert.That(api.Errors,  Is.EqualTo(new[] { "addUser.OnAdded: This delegate failed." }),
                            "The failure of the delegate was not reported, or not as the one of AddUser.");

            });

        }

        #endregion

        #region A_Delegate_That_Is_Cancelled_Is_Reported_As_Well()

        /// <summary>
        /// A delegate that ends in an OperationCanceledException - as an HTTP
        /// request that timed out does - is reported to HandleErrors like any
        /// other failure, and AddUser says the user was added.
        /// </summary>
        /// <remarks>
        /// InvokeAllAsync keeps quiet about a cancellation, because the handlers
        /// of an event are handed the token of a connection, and when that is
        /// cancelled, the connection is shutting down. The delegates of the user
        /// methods are handed no token: a cancellation is their own, and nobody
        /// would have seen it.
        /// </remarks>
        [Test]
        public async Task A_Delegate_That_Is_Cancelled_Is_Reported_As_Well()
        {

            var api    = await StartAPI();

            var added  = await api.AddUser(
                                   NewUser("alice"),
                                   SkipDefaultNotifications:  true,
                                   SkipNewUserEMail:          true,
                                   SkipNewUserNotifications:  true,
                                   OnAdded:                   async (timestamp, user, eventTrackingId, currentUserId) => {
                                                                  await Task.Yield();
                                                                  throw new TaskCanceledException("This delegate timed out.");
                                                              }
                               );

            Assert.Multiple(() => {

                Assert.That(added.Result,  Is.EqualTo(CommandResult.Success),
                            "AddUser reported the cancellation of its delegate as its own.");

                Assert.That(api.Errors,    Is.EqualTo(new[] { "addUser.OnAdded: This delegate timed out." }),
                            "The cancellation of the delegate was not reported, or not as the one of AddUser.");

            });

        }

        #endregion


        #region CreateUser_Has_Stored_The_Password_When_It_Returns(Creation)

        /// <summary>
        /// CreateUser and CreateUserIfNotExists, with an organization and
        /// without, with a password: once they return, the password is stored -
        /// also when writing it has to wait for the lock on the database files.
        /// </summary>
        /// <remarks>
        /// They store the password in the delegate they hand to AddUser, and
        /// it used to be run and not waited for. Writing to a database file
        /// waits for a lock that all HTTPExtAPIs of a process share, and after
        /// an IOException for a retry. Whenever it had to, CreateUser returned
        /// before the password was stored, and signing in with it right away -
        /// as the self sign-up does - failed.
        ///
        /// The password quality check runs in between the user being written
        /// and the password being written, and so is where this test takes the
        /// lock: as another API that writes its own database file just then.
        /// </remarks>
        [TestCaseSource(nameof(EveryWayToCreateAUserWithAPassword))]
        public async Task CreateUser_Has_Stored_The_Password_When_It_Returns(UserCreation Creation)
        {

            Task? lockReleased = null;

            var api = await StartAPI(
                                PasswordQualityCheck: password => {
                                    lockReleased ??= DelegateAPI.HoldTheDatabaseFileLock(TimeSpan.FromMilliseconds(300));
                                    return 1.0f;
                                }
                            );

            try
            {

                var user = await Creation.Call(api, Password);

                Assert.That(user,                                   Is.Not.Null,  $"{Creation} created no user.");

                Assert.That(api.VerifyPassword(user!.Id, Password),  Is.True,
                            $"{Creation} returned before the password was stored.");

            }
            finally
            {
                if (lockReleased is not null)
                    await lockReleased;
            }

        }

        #endregion


        #region (private) Places that run a delegate

        /// <summary>
        /// One of the places at which a user method runs the delegate it was
        /// handed, and a call that gets there with a delegate that does what
        /// it is told.
        /// </summary>
        /// <param name="Name">The call, as it appears in the name of the test.</param>
        /// <param name="ReportedAs">The name under which the failure of the delegate is reported.</param>
        /// <param name="Call">Makes the call; the delegate it hands over runs the given function.</param>
        public sealed record DelegateSite(String                                                Name,
                                          String                                                ReportedAs,
                                          Func<HTTPExtAPI, Func<Task>, Task<AResult<IUser>>>  Call)
        {
            public override String ToString()
                => Name;
        }

        private static IEnumerable<TestCaseData> EveryPlaceThatRunsADelegate()
        {

            yield return Place("AddUser(User)",
                               "addUser.OnAdded",
                               async (api, body) => await api.AddUser(
                                                              NewUser("alice"),
                                                              SkipDefaultNotifications:  true,
                                                              SkipNewUserEMail:          true,
                                                              SkipNewUserNotifications:  true,
                                                              OnAdded:                   Added(body)
                                                          ));

            yield return Place("AddUser(User, AccessRight, Organization)",
                               "AddUser.OnAdded",
                               async (api, body) => await api.AddUser(
                                                              NewUser("alice"),
                                                              User2OrganizationEdgeLabel.IsMember,
                                                              await AddOrganization(api),
                                                              SkipDefaultNotifications:  true,
                                                              SkipNewUserEMail:          true,
                                                              SkipNewUserNotifications:  true,
                                                              OnAdded:                   Added(body)
                                                          ));

            yield return Place("AddUser(User, AccessRights)",
                               "AddUser.OnAdded",
                               async (api, body) => await api.AddUser(
                                                              NewUser("alice"),
                                                              [ Tuple.Create<User2OrganizationEdgeLabel, IOrganization>(User2OrganizationEdgeLabel.IsMember, await AddOrganization(api)) ],
                                                              SkipDefaultNotifications:  true,
                                                              SkipNewUserEMail:          true,
                                                              SkipNewUserNotifications:  true,
                                                              OnAdded:                   Added(body)
                                                          ));

            yield return Place("AddUserIfNotExists(User)",
                               "addUserIfNotExists.OnAdded",
                               async (api, body) => await api.AddUserIfNotExists(
                                                              NewUser("alice"),
                                                              SkipDefaultNotifications:  true,
                                                              SkipNewUserEMail:          true,
                                                              SkipNewUserNotifications:  true,
                                                              OnAdded:                   Added(body)
                                                          ));

            yield return Place("AddUserIfNotExists(User, AccessRight, Organization)",
                               "AddUserIfNotExists.OnAdded",
                               async (api, body) => await api.AddUserIfNotExists(
                                                              NewUser("alice"),
                                                              User2OrganizationEdgeLabel.IsMember,
                                                              await AddOrganization(api),
                                                              SkipDefaultNotifications:  true,
                                                              SkipNewUserEMail:          true,
                                                              SkipNewUserNotifications:  true,
                                                              OnAdded:                   Added(body)
                                                          ));

            yield return Place("AddUserIfNotExists(User, AccessRights)",
                               "AddUserIfNotExists.OnAdded",
                               async (api, body) => await api.AddUserIfNotExists(
                                                              NewUser("alice"),
                                                              [ Tuple.Create<User2OrganizationEdgeLabel, IOrganization>(User2OrganizationEdgeLabel.IsMember, await AddOrganization(api)) ],
                                                              SkipDefaultNotifications:  true,
                                                              SkipNewUserEMail:          true,
                                                              SkipNewUserNotifications:  true,
                                                              OnAdded:                   Added(body)
                                                          ));

            yield return Place("AddOrUpdateUser(User), adding",
                               "addOrUpdateUser.OnAdded",
                               async (api, body) => await api.AddOrUpdateUser(
                                                              NewUser("alice"),
                                                              SkipDefaultNotifications:  true,
                                                              SkipNewUserEMail:          true,
                                                              SkipNewUserNotifications:  true,
                                                              OnAdded:                   Added(body)
                                                          ));

            yield return Place("AddOrUpdateUser(User), updating",
                               "addOrUpdateUser.OnUpdated",
                               async (api, body) => {

                                   await AddUser(api, "alice");

                                   return await api.AddOrUpdateUser(
                                                    NewUser("alice", "Alice L."),
                                                    SkipUserUpdatedNotifications:  true,
                                                    OnUpdated:                     Updated(body)
                                                );

                               });

            yield return Place("AddOrUpdateUser(User, AccessRight, Organization), adding",
                               "AddOrUpdateUser.OnAdded",
                               async (api, body) => await api.AddOrUpdateUser(
                                                              NewUser("alice"),
                                                              User2OrganizationEdgeLabel.IsMember,
                                                              await AddOrganization(api),
                                                              SkipDefaultNotifications:  true,
                                                              SkipNewUserEMail:          true,
                                                              SkipNewUserNotifications:  true,
                                                              OnAdded:                   Added(body)
                                                          ));

            yield return Place("UpdateUser(NewUser)",
                               "updateUser.OnUpdated",
                               async (api, body) => {

                                   await AddUser(api, "alice");

                                   return await api.UpdateUser(
                                                    NewUser("alice", "Alice L."),
                                                    SkipUserUpdatedNotifications:  true,
                                                    OnUpdated:                     Updated(body)
                                                );

                               });

            yield return Place("UpdateUser(User, UpdateDelegate)",
                               "updateUser.OnUpdated",
                               async (api, body) => {

                                   var user = (await AddUser(api, "alice")).User!;

                                   return await api.UpdateUser(
                                                    user,
                                                    builder => builder.Name = I18NString.Create("Alice L."),
                                                    SkipUserUpdatedNotifications:  true,
                                                    OnUpdated:                     Updated(body)
                                                );

                               });

        }

        private static TestCaseData Place(String                                                Name,
                                          String                                                ReportedAs,
                                          Func<HTTPExtAPI, Func<Task>, Task<AResult<IUser>>>  Call)

            => new TestCaseData(new DelegateSite(Name, ReportedAs, Call)).
                   SetArgDisplayNames(Name);

        private static HTTPExtAPI.OnUserAddedDelegate Added(Func<Task> Body)

            => (timestamp, user, eventTrackingId, currentUserId) => Body();

        private static HTTPExtAPI.OnUserUpdatedDelegate Updated(Func<Task> Body)

            => (timestamp, user, oldUser, eventTrackingId, currentUserId) => Body();

        #endregion

        #region (private) Ways to create a user with a password

        /// <summary>
        /// One of the extension methods that create a user with a password,
        /// and a call to it.
        /// </summary>
        /// <param name="Name">The method, as it appears in the name of the test.</param>
        /// <param name="Call">Creates "alice" with the given password.</param>
        public sealed record UserCreation(String                                   Name,
                                          Func<HTTPExtAPI, String, Task<IUser?>>  Call)
        {
            public override String ToString()
                => Name;
        }

        private static IEnumerable<TestCaseData> EveryWayToCreateAUserWithAPassword()
        {

            yield return Creation("CreateUser",
                                  (api, password) => api.CreateUser(
                                                         User_Id.Parse("alice"),
                                                         I18NString.Create("alice"),
                                                         SimpleEMailAddress.Parse("alice@example.test"),
                                                         password,
                                                         SkipDefaultNotifications:  true,
                                                         SkipNewUserEMail:          true,
                                                         SkipNewUserNotifications:  true
                                                     ));

            yield return Creation("CreateUser with an organization",
                                  async (api, password) => await api.CreateUser(
                                                                     User_Id.Parse("alice"),
                                                                     I18NString.Create("alice"),
                                                                     SimpleEMailAddress.Parse("alice@example.test"),
                                                                     User2OrganizationEdgeLabel.IsMember,
                                                                     await AddOrganization(api),
                                                                     password,
                                                                     SkipDefaultNotifications:  true,
                                                                     SkipNewUserEMail:          true,
                                                                     SkipNewUserNotifications:  true
                                                                 ));

            yield return Creation("CreateUserIfNotExists",
                                  (api, password) => api.CreateUserIfNotExists(
                                                         User_Id.Parse("alice"),
                                                         I18NString.Create("alice"),
                                                         SimpleEMailAddress.Parse("alice@example.test"),
                                                         password,
                                                         SkipDefaultNotifications:  true,
                                                         SkipNewUserEMail:          true,
                                                         SkipNewUserNotifications:  true
                                                     ));

            yield return Creation("CreateUserIfNotExists with an organization",
                                  async (api, password) => await api.CreateUserIfNotExists(
                                                                     User_Id.Parse("alice"),
                                                                     I18NString.Create("alice"),
                                                                     SimpleEMailAddress.Parse("alice@example.test"),
                                                                     User2OrganizationEdgeLabel.IsMember,
                                                                     await AddOrganization(api),
                                                                     password,
                                                                     SkipDefaultNotifications:  true,
                                                                     SkipNewUserEMail:          true,
                                                                     SkipNewUserNotifications:  true
                                                                 ));

        }

        private static TestCaseData Creation(String                                   Name,
                                             Func<HTTPExtAPI, String, Task<IUser?>>  Call)

            => new TestCaseData(new UserCreation(Name, Call)).
                   SetArgDisplayNames(Name);

        #endregion

        #region (private) Helpers

        /// <summary>
        /// A headless HTTPExtAPI that keeps what is reported to HandleErrors.
        /// </summary>
        /// <remarks>
        /// With a robot and an external DNS name, which the sign-up e-mail is
        /// written with, the given mailer - the API's own NullMailer when there
        /// is none - and the given password quality check.
        /// </remarks>
        private sealed class DelegateAPI(HTTPServer                     HTTPServer,
                                         String                         LoggingPath,
                                         ISMTPSubmissionClient?         Mailer,
                                         PasswordQualityCheckDelegate?  PasswordQualityCheck)

            : HTTPExtAPI(HTTPServer,
                         RootPath:              HTTPPath.Parse("/accounts"),
                         ExternalDNSName:       "example.test",
                         APIRobotEMailAddress:  new EMailAddress(SimpleEMailAddress.Parse("robot@example.test"), "Robot"),
                         SMTPSubmissionClient:  Mailer,
                         PasswordQualityCheck:  PasswordQualityCheck,
                         SkipURLTemplates:      true,
                         DisableNotifications:  true,
                         LoggingPath:           LoggingPath,
                         MinUserIdLength:       3)

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

            /// <summary>
            /// Take the lock on the database files, which every HTTPExtAPI of
            /// the process shares, and let go of it after the given time.
            /// </summary>
            /// <remarks>
            /// Taken before the first await, so that it is held by the time
            /// this returns. Let go of in a finally, on whatever thread the
            /// delay ends on - not on one a scheduler or a synchronization
            /// context of the test runner hands out - so that nothing can keep
            /// a lock held that every other test needs as well.
            /// </remarks>
            /// <returns>Done once the lock is free again.</returns>
            public static async Task HoldTheDatabaseFileLock(TimeSpan Duration)
            {

                LogFileSemaphore.Wait();

                try
                {
                    await Task.Delay(Duration).ConfigureAwait(false);
                }
                finally
                {
                    LogFileSemaphore.Release();
                }

            }

        }

        private async Task<DelegateAPI> StartAPI(ISMTPSubmissionClient?         Mailer                 = null,
                                                 PasswordQualityCheckDelegate?  PasswordQualityCheck   = null)
        {

            var api = new DelegateAPI(
                          new HTTPServer(
                              IPAddress:  IPv4Address.Localhost,
                              TCPPort:    IPPort.Zero,
                              AutoStart:  false
                          ),
                          directory,
                          Mailer,
                          PasswordQualityCheck
                      );

            await api.LoadDatabase();

            return api;

        }

        private static User NewUser(String   Id,
                                    String?  Name   = null)

            => new (
                   Id:     User_Id.Parse(Id),
                   Name:   I18NString.Create(Name ?? Id),
                   EMail:  SimpleEMailAddress.Parse($"{Id}@example.test")
               );

        /// <summary>
        /// Add a new user, without its default notifications, without
        /// notifications about it, without the sign-up e-mail, and without a
        /// delegate.
        /// </summary>
        private static async Task<AddUserResult> AddUser(HTTPExtAPI  API,
                                                         String      Id)
        {

            var added = await API.AddUser(
                                  NewUser(Id),
                                  SkipDefaultNotifications:  true,
                                  SkipNewUserEMail:          true,
                                  SkipNewUserNotifications:  true
                              );

            Assert.That(added.Result,  Is.EqualTo(CommandResult.Success),  added.Description.FirstText());

            return added;

        }

        private static async Task<Organization> AddOrganization(HTTPExtAPI API)
        {

            var acme   = new Organization(Organization_Id.Parse("acme"), I18NString.Create("ACME"));
            var added  = await API.AddOrganization(acme);

            Assert.That(added.Result,  Is.EqualTo(CommandResult.Success),  added.Description.FirstText());

            return acme;

        }

        #endregion

    }

}
