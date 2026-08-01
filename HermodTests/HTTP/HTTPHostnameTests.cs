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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// HTTPHostname tests.
    /// </summary>
    [TestFixture]
    public class HTTPHostnameTests
    {

        #region ParseWithSeparatePort_AcceptsEverythingTheSingleArgumentOverloadDoes()

        /// <summary>
        /// Both overloads must accept the very same host syntax. The overload taking a
        /// separate port used to validate against a plain domain label regular expression
        /// and therefore rejected IPv6 literals: Parse("[::1]", 8080) threw, while the
        /// textually equivalent Parse("[::1]:8080") worked.
        /// </summary>
        [Test]
        public void ParseWithSeparatePort_AcceptsEverythingTheSingleArgumentOverloadDoes()
        {

            var port = IPPort.Parse(8080);

            Assert.That(HTTPHostname.Parse("example.org", port).ToString(), Is.EqualTo("example.org:8080"));
            Assert.That(HTTPHostname.Parse("[::1]",       port).ToString(), Is.EqualTo("[::1]:8080"));
            Assert.That(HTTPHostname.Parse("[2606:2800:220:1:248:1893:25c8:1946]", port).ToString(),
                        Is.EqualTo("[2606:2800:220:1:248:1893:25c8:1946]:8080"));

            // The "*" wildcard keeps working.
            Assert.That(HTTPHostname.Parse("*", port).ToString(), Is.EqualTo("*:8080"));

            // ...and both spellings agree.
            Assert.That(HTTPHostname.Parse("[::1]", port), Is.EqualTo(HTTPHostname.Parse("[::1]:8080")));

        }

        #endregion

        #region ParseWithSeparatePort_IsCaseInsensitiveAndTrims()

        /// <summary>
        /// Host names are case-insensitive and are normalized to lower case.
        /// </summary>
        [Test]
        public void ParseWithSeparatePort_IsCaseInsensitiveAndTrims()
        {

            var port = IPPort.Parse(443);

            Assert.That(HTTPHostname.Parse("  EXAMPLE.org  ", port).ToString(), Is.EqualTo("example.org:443"));

        }

        #endregion

        #region ParseWithSeparatePort_RejectsAnAlreadyEmbeddedPort()

        /// <summary>
        /// Giving the port twice is ambiguous and must be rejected instead of silently
        /// letting one of them win.
        /// </summary>
        [Test]
        public void ParseWithSeparatePort_RejectsAnAlreadyEmbeddedPort()
        {

            Assert.That(HTTPHostname.TryParse("example.org:1234", IPPort.Parse(8080), out _), Is.False);
            Assert.That(HTTPHostname.TryParse("[::1]:1234",       IPPort.Parse(8080), out _), Is.False);

        }

        #endregion

        #region ParseWithSeparatePort_RejectsMalformedHosts()

        /// <summary>
        /// Malformed hosts must be rejected by both overloads alike.
        /// </summary>
        [Test]
        public void ParseWithSeparatePort_RejectsMalformedHosts()
        {

            var port = IPPort.Parse(8080);

            Assert.That(HTTPHostname.TryParse("",         port, out _), Is.False);
            Assert.That(HTTPHostname.TryParse("   ",      port, out _), Is.False);
            Assert.That(HTTPHostname.TryParse("[::1",     port, out _), Is.False);
            Assert.That(HTTPHostname.TryParse("[gggg::]", port, out _), Is.False);
            Assert.That(HTTPHostname.TryParse("exa mple", port, out _), Is.False);

        }

        #endregion

        #region GetHashCode_DoesNotThrowForTheDefaultValue()

        /// <summary>
        /// A struct can not prevent its own default value from being created, so Name is
        /// null for default(HTTPHostname) and hashing it must not throw.
        /// </summary>
        [Test]
        public void GetHashCode_DoesNotThrowForTheDefaultValue()
        {

            var hostname = default(HTTPHostname);

            Assert.That(hostname.IsNullOrEmpty, Is.True);
            Assert.That(hostname.GetHashCode(), Is.EqualTo(default(HTTPHostname).GetHashCode()));

        }

        #endregion

    }

}
