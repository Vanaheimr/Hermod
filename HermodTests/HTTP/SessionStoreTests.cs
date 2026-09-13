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

using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// SessionStore: sign-in sessions with idle and maximum lifetimes,
    /// revocation per user and a file that survives restarts.
    /// </summary>
    [TestFixture]
    public class SessionStoreTests
    {

        #region Data

        private static readonly User_Id  alice  = User_Id.Parse("alice");
        private static readonly User_Id  bob    = User_Id.Parse("bob");
        private static readonly User_Id  admin  = User_Id.Parse("admin");

        #endregion


        #region Create_And_TryGet()

        [Test]
        public void Create_And_TryGet()
        {

            var store    = new SessionStore(MaximumLifetime: TimeSpan.FromHours(1));
            var session  = store.Create(alice);

            Assert.That(session.UserId,                          Is.EqualTo(alice));
            Assert.That(session.SuperUserId,                     Is.Null);
            Assert.That(session.ExpiresAt - session.CreatedAt,   Is.EqualTo(TimeSpan.FromHours(1)));
            Assert.That(store.Count,                             Is.EqualTo(1));
            Assert.That(store.CountForUser(alice),               Is.EqualTo(1));
            Assert.That(store.CountForUser(bob),                 Is.EqualTo(0));

            Assert.That(store.TryGet(session.Token, out var found),  Is.True);
            Assert.That(found,                                       Is.EqualTo(session),  "without an idle timeout a use changes nothing");
            Assert.That(store.TryGet(SecurityToken_Id.Random(), out _),  Is.False);
            Assert.That(store.TryGet(default, out _),                    Is.False);

        }

        #endregion

        #region Tokens_Are_Random_And_Cookie_Safe()

        [Test]
        public void Tokens_Are_Random_And_Cookie_Safe()
        {

            var store   = new SessionStore();
            var tokens  = Enumerable.Range(0, 500).Select(_ => store.Create(alice).Token.ToString()).ToList();

            Assert.That(tokens.Distinct().Count(),  Is.EqualTo(500));
            Assert.That(tokens,                     Has.All.Matches<String>(token => Regex.IsMatch(token, "^[A-Za-z0-9_-]{43}$")));

        }

        #endregion

        #region Remove_Ends_A_Session()

        [Test]
        public void Remove_Ends_A_Session()
        {

            var store    = new SessionStore();
            var session  = store.Create(alice);

            Assert.That(store.Remove(session.Token),               Is.True);
            Assert.That(store.TryGet(session.Token, out _),        Is.False);
            Assert.That(store.Remove(session.Token),               Is.False,  "a second removal changes nothing");
            Assert.That(store.Count,                               Is.EqualTo(0));

        }

        #endregion

        #region Idle_Timeout_Slides_Until_The_Maximum_Lifetime()

        [Test]
        public async Task Idle_Timeout_Slides_Until_The_Maximum_Lifetime()
        {

            var store    = new SessionStore(IdleTimeout: TimeSpan.FromMilliseconds(400), MaximumLifetime: TimeSpan.FromMilliseconds(1000));
            var session  = store.Create(alice);

            Assert.That(session.ExpiresAt - session.CreatedAt,  Is.EqualTo(TimeSpan.FromMilliseconds(400)));

            await Task.Delay(250);

            Assert.That(store.TryGet(session.Token, out var used),  Is.True);
            Assert.That(used!.LastSeenAt,                            Is.GreaterThan(session.LastSeenAt));
            Assert.That(used.ExpiresAt,                              Is.GreaterThan(session.ExpiresAt),  "a use extends the session");

            await Task.Delay(250);

            Assert.That(store.TryGet(session.Token, out var usedAgain),  Is.True);
            Assert.That(usedAgain!.ExpiresAt,                             Is.LessThanOrEqualTo(session.CreatedAt + TimeSpan.FromMilliseconds(1000)),  "but never beyond the maximum lifetime");

            await Task.Delay(600);

            Assert.That(store.TryGet(session.Token, out _),  Is.False,  "the maximum lifetime ends the session no matter how busy it was");

        }

        #endregion

        #region Idle_Sessions_Expire()

        [Test]
        public async Task Idle_Sessions_Expire()
        {

            var store    = new SessionStore(IdleTimeout: TimeSpan.FromMilliseconds(150));
            var session  = store.Create(alice);

            await Task.Delay(300);

            Assert.That(store.TryGet(session.Token, out _),  Is.False);
            Assert.That(store.Count,                         Is.EqualTo(0));
            Assert.That(store.CountForUser(alice),           Is.EqualTo(0));
            Assert.That(store,                               Is.Empty);

        }

        #endregion

        #region RemoveAllForUser_Keeps_The_Current_Session()

        [Test]
        public void RemoveAllForUser_Keeps_The_Current_Session()
        {

            var store   = new SessionStore();
            var first   = store.Create(alice);
            var second  = store.Create(alice);
            var third   = store.Create(alice);
            var other   = store.Create(bob);

            Assert.That(store.RemoveAllForUser(alice, ExceptToken: second.Token),  Is.EqualTo(2));
            Assert.That(store.TryGet(first.Token,  out _),                         Is.False);
            Assert.That(store.TryGet(second.Token, out _),                         Is.True);
            Assert.That(store.TryGet(third.Token,  out _),                         Is.False);
            Assert.That(store.TryGet(other.Token,  out _),                         Is.True);
            Assert.That(store.CountForUser(alice),                                 Is.EqualTo(1));

        }

        #endregion

        #region Impersonation_Belongs_To_Both_Users()

        [Test]
        public void Impersonation_Belongs_To_Both_Users()
        {

            var store    = new SessionStore();
            var session  = store.Create(bob, SuperUserId: admin);

            Assert.That(session.UserId,               Is.EqualTo(bob));
            Assert.That(session.SuperUserId,          Is.EqualTo(admin));
            Assert.That(store.CountForUser(bob),      Is.EqualTo(1));
            Assert.That(store.CountForUser(admin),    Is.EqualTo(0),  "the admin does not act as themselves in it");

            Assert.That(store.RemoveAllForUser(admin),  Is.EqualTo(1),  "but revoking the admin's sessions ends it");
            Assert.That(store.TryGet(session.Token, out _),  Is.False);

        }

        #endregion

        #region File_Survives_Restarts_And_Persists_Sign_Outs()

        [Test]
        public void File_Survives_Restarts_And_Persists_Sign_Outs()
        {

            var filePath = Path.Combine(Path.GetTempPath(), $"hermod-sessions-{Guid.NewGuid():N}.db");

            try
            {

                var first   = new SessionStore(FilePath: filePath);
                var kept    = first.Create(alice);
                var gone    = first.Create(bob, SuperUserId: admin);
                var signed  = first.Create(bob);

                Assert.That(first.Remove(signed.Token),  Is.True);
                Assert.That(File.ReadAllLines(filePath), Has.Length.EqualTo(4),  "three creations and one removal were appended");

                // A restart: the file is replayed and compacted.
                var second = new SessionStore(FilePath: filePath);

                Assert.That(second.Count,                             Is.EqualTo(2));
                Assert.That(second.TryGet(kept.Token,   out var one),  Is.True);
                Assert.That(one,                                       Is.EqualTo(kept));
                Assert.That(second.TryGet(gone.Token,   out var two),  Is.True);
                Assert.That(two!.SuperUserId,                          Is.EqualTo(admin));
                Assert.That(second.TryGet(signed.Token, out _),        Is.False,  "a sign-out survives the restart");
                Assert.That(File.ReadAllLines(filePath),               Has.Length.EqualTo(2),  "compacted to the live sessions");

                Assert.That(second.Remove(gone.Token),  Is.True);

                var third = new SessionStore(FilePath: filePath);

                Assert.That(third.Count,                  Is.EqualTo(1));
                Assert.That(third.TryGet(gone.Token, out _),  Is.False);
                Assert.That(third.TryGet(kept.Token, out _),  Is.True);

            }
            finally
            {
                File.Delete(filePath);
            }

        }

        #endregion

        #region Expired_And_Malformed_Lines_Are_Skipped()

        [Test]
        public void Expired_And_Malformed_Lines_Are_Skipped()
        {

            var filePath  = Path.Combine(Path.GetTempPath(), $"hermod-sessions-{Guid.NewGuid():N}.db");
            var now       = DateTimeOffset.UtcNow;
            var live      = SecurityToken_Id.Random();
            var expired   = SecurityToken_Id.Random();

            try
            {

                File.WriteAllLines(
                    filePath,
                    [
                        "# a comment",
                        "",
                        "this is not a session line",
                        $"add;{live};alice;{now:o};{now:o};{now.AddHours(1):o};",
                        $"add;{expired};bob;{now.AddHours(-2):o};{now.AddHours(-2):o};{now.AddHours(-1):o};",
                        $"add;{SecurityToken_Id.Random()};carol;not a time;{now:o};{now.AddHours(1):o};"
                    ]
                );

                var store = new SessionStore(FilePath: filePath);

                Assert.That(store.Count,                         Is.EqualTo(1));
                Assert.That(store.TryGet(live,    out var found),  Is.True);
                Assert.That(found!.UserId,                        Is.EqualTo(alice));
                Assert.That(store.TryGet(expired, out _),          Is.False);
                Assert.That(File.ReadAllLines(filePath),           Has.Length.EqualTo(1),  "compacted to the one live session");

            }
            finally
            {
                File.Delete(filePath);
            }

        }

        #endregion

        #region Invalid_Arguments_Are_Rejected()

        [Test]
        public void Invalid_Arguments_Are_Rejected()
        {
            Assert.That(() => new SessionStore(IdleTimeout:     TimeSpan.Zero),      Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new SessionStore(MaximumLifetime: TimeSpan.FromDays(-1)),  Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new SessionStore().Create(default),                     Throws.ArgumentException);
            Assert.That(() => new SessionStore().AttachFile(""),                      Throws.ArgumentException);
        }

        #endregion

    }

}
