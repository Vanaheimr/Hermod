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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TOTP
{

    /// <summary>
    /// API-level behaviour of the "Authorization: TOTP" scheme carrier. The
    /// wire format itself - building, parsing, leniency and malformed-header
    /// rejection - is vector-driven in TOTPVectorTests, against the canonical
    /// HTTP authentication vectors of the conformance suite
    /// (TOTP/TestVectors/totp-http-auth-vectors.json).
    /// </summary>
    [TestFixture]
    public class HTTPTOTPAuthenticationTests
    {

        #region EmptyLogin_IsRejected

        [Test]
        public void EmptyLogin_IsRejected()
        {

            Assert.Multiple(() => {

                Assert.That(HTTPTOTPAuthentication.TryCreate("   ", "CN63y502maVh", out _),  Is.False);

                Assert.Throws<ArgumentException>(
                    () => HTTPTOTPAuthentication.Create("", "CN63y502maVh")
                );

            });

        }

        #endregion

        #region Create_SaysNothingOfTheBinding

        /// <summary>
        /// Whether a password is bound to the TLS session is what the
        /// verifier's configuration of the login says: the header carries
        /// no "tlscb", whatever the password was made with.
        /// </summary>
        [Test]
        public void Create_SaysNothingOfTheBinding()
        {

            Assert.That(HTTPTOTPAuthentication.Create("chargingstation-0001", "gAzxPfYtmRgd").HTTPText,
                        Does.Not.Contain("tlscb"));

        }

        #endregion

        #region TwoParsedHeaders_AreEqual

        /// <summary>
        /// A parsed header has no TOTP configuration: two of the same login
        /// and password are equal, and hash alike, without one.
        /// </summary>
        [Test]
        public void TwoParsedHeaders_AreEqual()
        {

            var header = HTTPTOTPAuthentication.Create("chargingstation-0001", "gAzxPfYtmRgd").HTTPText;
            var first  = HTTPTOTPAuthentication.ParseHTTPHeader(header);
            var second = HTTPTOTPAuthentication.ParseHTTPHeader(header);
            var other  = HTTPTOTPAuthentication.ParseHTTPHeader(HTTPTOTPAuthentication.Create("chargingstation-0001", "Ok8dSsA7xLe2").HTTPText);

            Assert.Multiple(() => {

                Assert.That(first.TOTPConfig,                  Is.Null);
                Assert.That(first.Equals(first),               Is.True);
                Assert.That(first.Equals(second),              Is.True);
                Assert.That(first == second,                   Is.True);
                Assert.That(first.GetHashCode(),               Is.EqualTo(second.GetHashCode()));

                Assert.That(first.Equals(other),               Is.False);
                Assert.That(first.GetHashCode(),               Is.Not.EqualTo(other.GetHashCode()));

            });

        }

        #endregion

        #region ConfigsWithoutTheBinding_HashApart

        /// <summary>
        /// A TOTP configuration that says nothing of the binding hashes by
        /// its shared secret and the rest, not as zero.
        /// </summary>
        [Test]
        public void ConfigsWithoutTheBinding_HashApart()
        {

            var first  = new TOTPConfig("secret-of-the-first-station-01");
            var second = new TOTPConfig("secret-of-the-second-station-2");

            Assert.Multiple(() => {

                Assert.That(first.GetHashCode(),               Is.Not.Zero);
                Assert.That(first.GetHashCode(),               Is.Not.EqualTo(second.GetHashCode()));
                Assert.That(first.GetHashCode(),               Is.EqualTo(new TOTPConfig("secret-of-the-first-station-01").GetHashCode()));

            });

        }

        #endregion

    }

}
