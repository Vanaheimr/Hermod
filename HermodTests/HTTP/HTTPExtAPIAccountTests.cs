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
using org.GraphDefined.Vanaheimr.Hermod.Passkeys;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// A headless HTTPExtAPI as an account store: users with their
    /// timestamps, passwords and passkeys are written to its database files
    /// and come back after a restart.
    /// </summary>
    [TestFixture]
    public class HTTPExtAPIAccountTests
    {

        #region (private) NewAPI(DataDirectory)

        private static HTTPExtAPI NewAPI(String DataDirectory)
        {

            var server = new HTTPServer(
                             IPAddress:  IPv4Address.Localhost,
                             TCPPort:    IPPort.Zero,
                             AutoStart:  false
                         );

            return new HTTPExtAPI(
                       server,
                       RootPath:              HTTPPath.Parse("/accounts"),
                       SkipURLTemplates:      true,
                       DisableNotifications:  true,
                       LoggingPath:           DataDirectory,
                       MinUserIdLength:       3
                   );

        }

        #endregion


        #region Users_Passkeys_And_Timestamps_Survive_A_Restart()

        [Test]
        public async Task Users_Passkeys_And_Timestamps_Survive_A_Restart()
        {

            var directory = Path.Combine(Path.GetTempPath(), $"hermod-accounts-{Guid.NewGuid():N}") + Path.DirectorySeparatorChar;

            try
            {

                var api = NewAPI(directory);
                await api.LoadDatabase();

                Assert.That(api.Users,  Is.Empty);

                var user = await api.CreateUser(
                                     User_Id.Parse("alice"),
                                     I18NString.Create("Alice"),
                                     SimpleEMailAddress.Parse("alice@example.test"),
                                     "Correct-Horse-1",
                                     SkipDefaultNotifications:  true,
                                     SkipNewUserEMail:          true,
                                     SkipNewUserNotifications:  true
                                 );

                Assert.That(user,                                              Is.Not.Null);
                Assert.That(user!.CreatedAt,                                   Is.EqualTo(DateTimeOffset.UtcNow).Within(TimeSpan.FromSeconds(10)));
                Assert.That(user.LastLoginAt,                                  Is.Null);
                Assert.That(api.VerifyPassword(user.Id, "Correct-Horse-1"),    Is.True);
                Assert.That(api.VerifyPassword(user.Id, "Wrong-Horse-1"),      Is.False);

                // The password reset token only travels inside the new-user e-mail,
                // so without that e-mail none is created.
                Assert.That(File.Exists(Path.Combine(directory, "UsersAPI", HTTPExtAPI.DefaultPasswordResetsFile)),  Is.False);

                // All database files live together below UsersAPI.
                Assert.That(File.Exists(Path.Combine(directory, "UsersAPI", HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName)),  Is.True);
                Assert.That(File.Exists(Path.Combine(directory, "UsersAPI", HTTPExtAPI.DefaultPasswordFile)),                Is.True);
                Assert.That(File.Exists(Path.Combine(directory, HTTPExtAPI.DefaultHTTPExtAPI_DatabaseFileName)),             Is.False);

                var lastLogin  = DateTimeOffset.Parse("2026-09-12T10:00:00+00:00");
                var updated    = await api.UpdateUser(
                                           user,
                                           builder => builder.LastLoginAt = lastLogin,
                                           SkipUserUpdatedNotifications:  true
                                       );

                Assert.That(updated.Result,  Is.EqualTo(CommandResult.Success));

                // A second update of the same account used to fail with "not attached to this API".
                Assert.That(api.TryGetUser(user.Id, out var current),  Is.True);

                var renamed = await api.UpdateUser(
                                        current!,
                                        builder => builder.Name = I18NString.Create("Alice L."),
                                        SkipUserUpdatedNotifications:  true
                                    );

                Assert.That(renamed.Result,  Is.EqualTo(CommandResult.Success),  renamed.Description.FirstText());
                Assert.That(api.TryGetUser(user.Id, out current) && current!.LastLoginAt == lastLogin,  Is.True,  "the builder carries the earlier change");

                var passkey = new Passkey(
                                  Id:              "credential-1",
                                  PublicKey:       new Byte[32],
                                  Algorithm:       WebAuthn.ES256,
                                  SignCount:       0,
                                  Transports:      [ "internal" ],
                                  Discoverable:    true,
                                  BackupEligible:  false,
                                  BackedUp:        false,
                                  AAGUID:          Guid.Empty,
                                  Name:            "Test key",
                                  CreatedAt:       DateTimeOffset.UtcNow,
                                  LastUsedAt:      null
                              );

                Assert.That(await api.AddPasskey   (user, passkey),                                              Is.True);
                Assert.That(await api.AddPasskey   (user, passkey),                                              Is.False,  "credential ids are unique");
                Assert.That(await api.UpdatePasskey(user.Id, passkey with { SignCount = 5, Name = "Renamed" }),  Is.True);
                Assert.That(await api.AddPasskey   (user, passkey with { Id = "credential-2", Name = "Second" }), Is.True);
                Assert.That(await api.RemovePasskey(user.Id, "credential-2"),                                    Is.True);
                Assert.That(await api.RemovePasskey(user.Id, "credential-2"),                                    Is.False);
                Assert.That(api.GetPasskeys(user.Id).Select(p => p.Id),                                          Is.EqualTo(new[] { "credential-1" }));

                // A restart replays the database files.
                var restarted = NewAPI(directory);
                await restarted.LoadDatabase();

                Assert.That(restarted.TryGetUser(User_Id.Parse("alice"), out var reloaded),  Is.True);
                Assert.That(reloaded!.EMail.Address.ToString(),                              Is.EqualTo("alice@example.test"));
                Assert.That(reloaded.Name.FirstText(),                                       Is.EqualTo("Alice L."));
                Assert.That(reloaded.CreatedAt,                                              Is.EqualTo(user.CreatedAt).Within(TimeSpan.FromSeconds(1)));
                Assert.That(reloaded.LastLoginAt,                                            Is.EqualTo(lastLogin));
                Assert.That(restarted.VerifyPassword(reloaded.Id, "Correct-Horse-1"),        Is.True);

                var passkeys = restarted.GetPasskeys(reloaded.Id).ToList();

                Assert.That(passkeys,                Has.Count.EqualTo(1));
                Assert.That(passkeys[0].Id,          Is.EqualTo("credential-1"));
                Assert.That(passkeys[0].SignCount,   Is.EqualTo(5));
                Assert.That(passkeys[0].Name,        Is.EqualTo("Renamed"));
                Assert.That(passkeys[0].PublicKey,   Is.EqualTo(passkey.PublicKey));

                Assert.That(restarted.TryGetPasskey("credential-1", out var owner, out _),  Is.True);
                Assert.That(owner!.Id,                                                       Is.EqualTo(reloaded.Id));
                Assert.That(restarted.TryGetPasskey("credential-2", out _, out _),          Is.False);

            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }

        }

        #endregion

        #region Authenticated_And_Disabled_Do_Not_Swap_Places()

        /// <summary>
        /// An authenticated account must not come back disabled.
        /// </summary>
        /// <remarks>
        /// The two flags are given opposite values here on purpose. Both are
        /// Booleans sitting next to each other in a long argument list, so
        /// handing them over the wrong way round is something no compiler can
        /// see - and giving them the same value in a test would hide exactly
        /// that, because a swap and a clean round-trip then look alike.
        ///
        /// A second restart with a save in between is what the last part is
        /// for. Reading swapped the flags and writing them back made the swap
        /// permanent, so they changed places at every start and were never
        /// wrong twice in the same direction - which is how this survived
        /// being looked at.
        /// </remarks>
        [Test]
        public async Task Authenticated_And_Disabled_Do_Not_Swap_Places()
        {

            var directory = Path.Combine(Path.GetTempPath(), $"hermod-accounts-{Guid.NewGuid():N}") + Path.DirectorySeparatorChar;

            try
            {

                var api = NewAPI(directory);
                await api.LoadDatabase();

                var user = await api.CreateUser(
                                     User_Id.Parse("alice"),
                                     I18NString.Create("Alice"),
                                     SimpleEMailAddress.Parse("alice@example.test"),
                                     "Correct-Horse-1",
                                     IsAuthenticated:           true,
                                     IsDisabled:                false,
                                     SkipDefaultNotifications:  true,
                                     SkipNewUserEMail:          true,
                                     SkipNewUserNotifications:  true
                                 );

                Assert.That(user,                   Is.Not.Null);
                Assert.That(user!.IsAuthenticated,  Is.True);
                Assert.That(user.IsDisabled,        Is.False);

                #region A restart replays the database files

                var restarted = NewAPI(directory);
                await restarted.LoadDatabase();

                Assert.That(restarted.TryGetUser(user.Id, out var reloaded),  Is.True);
                Assert.That(reloaded!.IsAuthenticated,  Is.True,   "an authenticated account came back unauthenticated");
                Assert.That(reloaded.IsDisabled,        Is.False,  "an account that was never disabled came back disabled");

                #endregion

                #region And a second one, with a save in between

                var updated = await restarted.UpdateUser(
                                        reloaded,
                                        builder => builder.LastLoginAt = DateTimeOffset.Parse("2026-09-18T10:00:00+00:00"),
                                        SkipUserUpdatedNotifications:  true
                                    );

                Assert.That(updated.Result,  Is.EqualTo(CommandResult.Success),  updated.Description.FirstText());

                var again = NewAPI(directory);
                await again.LoadDatabase();

                Assert.That(again.TryGetUser(user.Id, out var twice),  Is.True);
                Assert.That(twice!.IsAuthenticated,  Is.True,   "the flags settle rather than changing places at every start");
                Assert.That(twice.IsDisabled,        Is.False);

                #endregion

            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }

        }

        #endregion

        #region The_Last_Enabled_Administrator_Cannot_Be_Disabled()

        /// <summary>
        /// An organization may not be left without an enabled administrator, and
        /// gets one back the moment somebody else can administer it.
        /// </summary>
        /// <remarks>
        /// Disabling is meant to shut one person out. Disabling the last enabled
        /// administrator shuts everybody out, including whoever would have to
        /// enable the account again - and the account that could do that is the
        /// one that was just disabled. What is left is editing the database file
        /// by hand.
        ///
        /// The middle of this test is the part that matters. A guard that simply
        /// refused every attempt would sail through a test that only checked the
        /// refusals, so a second administrator is added and the first one is then
        /// disabled successfully. The guard has to let go, not only to hold.
        /// </remarks>
        [Test]
        public async Task The_Last_Enabled_Administrator_Cannot_Be_Disabled()
        {

            var directory = Path.Combine(Path.GetTempPath(), $"hermod-accounts-{Guid.NewGuid():N}") + Path.DirectorySeparatorChar;

            try
            {

                var api = NewAPI(directory);
                await api.LoadDatabase();

                var created = await api.CreateOrganizationIfNotExists(
                                        Organization_Id.Parse("acme"),
                                        I18NString.Create("ACME")
                                    );

                Assert.That(created,  Is.Not.Null,  "the organization could not be created");
                Assert.That(created,  Is.InstanceOf<Organization>());

                var acme = (Organization) created!;

                async Task<IUser> AdminCalled(String Name)
                {

                    var user = await api.CreateUser(
                                         User_Id.Parse(Name),
                                         I18NString.Create(Name),
                                         SimpleEMailAddress.Parse($"{Name}@example.test"),
                                         User2OrganizationEdgeLabel.IsAdmin,
                                         acme,
                                         Password:                  "Correct-Horse-1",
                                         SkipDefaultNotifications:  true,
                                         SkipNewUserEMail:          true,
                                         SkipNewUserNotifications:  true
                                     );

                    Assert.That(user,  Is.Not.Null,  $"'{Name}' could not be created");

                    return user!;

                }

                // Always the account as this API holds it: the organization edges
                // hang on that one, and an update returns a new object each time.
                async Task<UpdateUserResult> Disable(String Name)
                {

                    Assert.That(api.TryGetUser(User_Id.Parse(Name), out var current) && current is not null,  Is.True);

                    return await api.UpdateUser(
                                     current!,
                                     builder => builder.IsDisabled = true,
                                     SkipUserUpdatedNotifications:  true
                                 );

                }

                Boolean IsDisabled(String Name)
                    => api.TryGetUser(User_Id.Parse(Name), out var user) &&
                       user is not null &&
                       user.IsDisabled;

                #region The only administrator stays

                await AdminCalled("alice");

                var refused = await Disable("alice");

                Assert.Multiple(() =>
                {
                    Assert.That(refused.Result,                   Is.Not.EqualTo(CommandResult.Success),  "the only administrator was disabled");
                    Assert.That(refused.Description.FirstText(),  Does.Contain("acme"),                   "the refusal names the organization that would have been left without one");
                    Assert.That(IsDisabled("alice"),              Is.False,                               "and nothing was written");
                });

                #endregion

                #region With somebody else to administer it, the first one may go

                await AdminCalled("bernd");

                var allowed = await Disable("alice");

                Assert.Multiple(() =>
                {
                    Assert.That(allowed.Result,       Is.EqualTo(CommandResult.Success),  allowed.Description.FirstText());
                    Assert.That(IsDisabled("alice"),  Is.True);
                });

                #endregion

                #region Which makes the other one the last, and it stays

                var refusedToo = await Disable("bernd");

                Assert.Multiple(() =>
                {
                    Assert.That(refusedToo.Result,  Is.Not.EqualTo(CommandResult.Success),  "a disabled administrator still counted as one");
                    Assert.That(IsDisabled("bernd"),  Is.False);
                });

                #endregion

            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }

        }

        #endregion

    }

}
