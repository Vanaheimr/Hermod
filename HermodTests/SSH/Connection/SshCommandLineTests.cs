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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// M6: POSIX-shell argument quoting for safe remote command composition.
    /// </summary>
    [TestFixture]
    public class SshCommandLineTests
    {

        #region Quote_SimpleArguments_AreUnchanged

        [Test]
        public void Quote_SimpleArguments_AreUnchanged()
        {
            Assert.Multiple(() => {
                Assert.That(SshCommandLine.Quote("uname"),            Is.EqualTo("uname"));
                Assert.That(SshCommandLine.Quote("/usr/bin/df"),      Is.EqualTo("/usr/bin/df"));
                Assert.That(SshCommandLine.Quote("--all"),            Is.EqualTo("--all"));
                Assert.That(SshCommandLine.Quote("a1_b2.c-3"),        Is.EqualTo("a1_b2.c-3"));
            });
        }

        #endregion

        #region Quote_SpecialCharacters_AreSingleQuoted

        [Test]
        public void Quote_SpecialCharacters_AreSingleQuoted()
        {
            Assert.Multiple(() => {
                Assert.That(SshCommandLine.Quote(""),                 Is.EqualTo("''"));
                Assert.That(SshCommandLine.Quote("a b"),              Is.EqualTo("'a b'"));
                Assert.That(SshCommandLine.Quote("$HOME"),            Is.EqualTo("'$HOME'"));
                Assert.That(SshCommandLine.Quote("`id`"),             Is.EqualTo("'`id`'"));
                Assert.That(SshCommandLine.Quote("a\nb"),             Is.EqualTo("'a\nb'"));
                // The tricky one: an embedded single quote closes, escapes, and reopens.
                Assert.That(SshCommandLine.Quote("it's"),             Is.EqualTo("'it'\\''s'"));
            });
        }

        #endregion

        #region Join_QuotesAndSpaceJoins

        [Test]
        public void Join_QuotesAndSpaceJoins()
        {
            Assert.That(SshCommandLine.Join("bash", "-lc", "echo $USER && df -h"),
                        Is.EqualTo("bash -lc 'echo $USER && df -h'"));
        }

        #endregion

    }

}
