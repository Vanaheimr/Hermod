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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SSH.Security
{

    /// <summary>
    /// An authorized_keys line that is refused says why, to whoever handed it
    /// in - an account adding a key over HTTP - where a file read at sign-in
    /// had nobody to tell. And a validity time that cannot be read refuses its
    /// line, where it threw out of the whole file.
    /// </summary>
    [TestFixture]
    [Category("Security")]
    public class AuthorizedKeyRefusalTests
    {

        private static String KeyLine()
            => SshPublicKey.FromHostKey(SshHostKey.GenerateEd25519(), "device").ToAuthorizedKeyLine();


        #region A_Refused_Line_Says_Why

        [Test]
        [TestCase("permitopen=\"10.0.0.1:80\" {key}",  "'permitopen' cannot be held to")]
        [TestCase("environment=\"X=1\" {key}",         "'environment' cannot be held to")]
        [TestCase("from=\"*.example.com\" {key}",      "'*.example.com' is neither")]
        [TestCase("from=\"!10.0.0.0/8\" {key}",        "'!10.0.0.0/8' is neither")]
        [TestCase("expiry-time=\"tomorrow\" {key}",    "expiry-time=\"tomorrow\" is no time")]
        [TestCase("not-before=\"20261399\" {key}",     "not-before=\"20261399\" is no time")]
        [TestCase("no-pty just some words",           "No public key was found")]
        [TestCase("ssh-ed25519 AAAAnotbase64!!",       "cannot be read")]
        [TestCase("   ",                               "The line is empty")]
        public void A_Refused_Line_Says_Why(String Line, String Why)
        {

            var line = Line.Replace("{key}", KeyLine());

            Assert.Multiple(() => {
                Assert.That(AuthorizedKeysFile.TryParseLine(line, out var entry, out var refused), Is.False);
                Assert.That(entry,    Is.Null);
                Assert.That(refused,  Does.Contain(Why));
                Assert.That(AuthorizedKeysFile.TryParseLine(line, out _), Is.False, "the form without a reason says otherwise");
            });

        }

        #endregion

        #region A_Line_Taken_Says_Nothing

        [Test]
        public void A_Line_Taken_Says_Nothing()
        {

            Assert.Multiple(() => {
                Assert.That(AuthorizedKeysFile.TryParseLine($"from=\"192.0.2.0/24\",expiry-time=\"20991231\" {KeyLine()}", out var entry, out var refused), Is.True);
                Assert.That(entry,                Is.Not.Null);
                Assert.That(refused,              Is.Null);
                Assert.That(entry!.NotAfter,      Is.EqualTo(new DateTimeOffset(2099, 12, 31, 0, 0, 0, TimeSpan.Zero)));
            });

        }

        #endregion

        #region A_Time_That_Cannot_Be_Read_Refuses_Its_Line_Not_The_File

        /// <summary>
        /// "tomorrow" as an expiry time threw a FormatException out of
        /// AuthorizedKeysFile.Parse, and with it went every other line of the
        /// file - the good key below it as well.
        /// </summary>
        [Test]
        public void A_Time_That_Cannot_Be_Read_Refuses_Its_Line_Not_The_File()
        {

            var good    = KeyLine();
            var entries = AuthorizedKeysFile.Parse($"expiry-time=\"tomorrow\" {KeyLine()}\n{good}\n");

            Assert.That(entries.Select(entry => entry.PublicKey.ToAuthorizedKeyLine()), Is.EqualTo(new[] { good }));

        }

        #endregion

    }

}
