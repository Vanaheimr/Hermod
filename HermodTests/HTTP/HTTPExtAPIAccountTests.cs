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
                Assert.That(reloaded.Name.FirstText(),                                       Is.EqualTo("Alice"));
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

    }

}
