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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TOTP
{

    /// <summary>
    /// The "Authorization: TOTP" scheme of the TOTP HTTP authentication
    /// specification (TOTPConformanceTests, spec/totp-http-authentication.md):
    /// a MANDATORY type digit (0 = raw, 1 = bound to the TLS session), a
    /// space, then base64(username):base64(totp) - mirroring the "TOTP"
    /// request header, which puts its type digit before the value in the same
    /// way. Username and TOTP are Base64-encoded SEPARATELY, so - unlike HTTP
    /// Basic Auth - both may contain a colon. The example tokens are
    /// canonical conformance vectors.
    /// </summary>
    [TestFixture]
    public class HTTPTOTPAuthenticationTests
    {

        #region Data

        private const String username    = "chargingstation-0001";
        private const String rawTOTP     = "CN63y502maVh";              // vector "defaults-mid-slot"
        private const String boundTOTP   = "gAzxPfYtmRgd";              // vector "tls-binding-sha256"

        private const String rawHeader   = "TOTP 0 Y2hhcmdpbmdzdGF0aW9uLTAwMDE=:Q042M3k1MDJtYVZo";
        private const String boundHeader = "TOTP 1 Y2hhcmdpbmdzdGF0aW9uLTAwMDE=:Z0F6eFBmWXRtUmdk";

        #endregion


        #region RawTOTP_RoundTrip

        [Test]
        public void RawTOTP_RoundTrip()
        {

            var auth = HTTPTOTPAuthentication.Create(username, rawTOTP);

            Assert.Multiple(() => {
                Assert.That(auth.Type,      Is.EqualTo(TOTPHTTPHeaderType.RAW));
                Assert.That(auth.HTTPText,  Is.EqualTo(rawHeader));
            });

            Assert.That(HTTPTOTPAuthentication.TryParseHTTPHeader(auth.HTTPText, out var parsed), Is.True);

            Assert.Multiple(() => {
                Assert.That(parsed?.Username,  Is.EqualTo(username));
                Assert.That(parsed?.TOTP,      Is.EqualTo(rawTOTP));
                Assert.That(parsed?.Type,      Is.EqualTo(TOTPHTTPHeaderType.RAW));
                Assert.That(parsed,            Is.EqualTo(auth));
            });

        }

        #endregion

        #region BoundTOTP_RoundTrip

        [Test]
        public void BoundTOTP_RoundTrip()
        {

            var auth = HTTPTOTPAuthentication.Create(username, boundTOTP, TOTPHTTPHeaderType.TLSChannelBinding);

            Assert.That(auth.HTTPText, Is.EqualTo(boundHeader));

            Assert.That(HTTPTOTPAuthentication.TryParseHTTPHeader(auth.HTTPText, out var parsed), Is.True);

            Assert.Multiple(() => {
                Assert.That(parsed?.Username,  Is.EqualTo(username));
                Assert.That(parsed?.TOTP,      Is.EqualTo(boundTOTP));
                Assert.That(parsed?.Type,      Is.EqualTo(TOTPHTTPHeaderType.TLSChannelBinding));
                Assert.That(parsed,            Is.EqualTo(auth));
            });

        }

        #endregion

        #region SchemeName_IsCaseInsensitive

        [Test]
        public void SchemeName_IsCaseInsensitive()
        {

            Assert.That(HTTPTOTPAuthentication.TryParseHTTPHeader("totp 0 Y2hhcmdpbmdzdGF0aW9uLTAwMDE=:Q042M3k1MDJtYVZo",
                                                                  out var parsed), Is.True);

            Assert.That(parsed?.Username, Is.EqualTo(username));

        }

        #endregion

        #region UsernameWithColon_SurvivesTheRoundTrip

        /// <summary>
        /// Username and TOTP are Base64-encoded separately, so a colon inside
        /// the username - HTTP Basic Auth's classic ambiguity - is harmless.
        /// </summary>
        [Test]
        public void UsernameWithColon_SurvivesTheRoundTrip()
        {

            var auth = HTTPTOTPAuthentication.Create("EVSE:DE*GEF*E1234*1", rawTOTP);

            Assert.That(HTTPTOTPAuthentication.TryParseHTTPHeader(auth.HTTPText, out var parsed), Is.True);

            Assert.That(parsed?.Username, Is.EqualTo("EVSE:DE*GEF*E1234*1"));

        }

        #endregion

        #region TypeDigit_IsMandatory

        /// <summary>
        /// The type digit is not optional: credentials without one - including
        /// the colon-separated forms of earlier drafts - are rejected.
        /// </summary>
        [Test]
        public void TypeDigit_IsMandatory()
        {

            Assert.Multiple(() => {

                // No type digit.
                Assert.That(HTTPTOTPAuthentication.TryParseHTTPHeader("TOTP Y2hhcmdpbmdzdGF0aW9uLTAwMDE=:Q042M3k1MDJtYVZo", out _), Is.False);

                // Colon-separated type digit (an earlier draft form).
                Assert.That(HTTPTOTPAuthentication.TryParseHTTPHeader("TOTP 1:Y2hhcmdpbmdzdGF0aW9uLTAwMDE=:Q042M3k1MDJtYVZo", out _), Is.False);

            });

        }

        #endregion

        #region MalformedHeaders_AreRejected

        [Test]
        public void MalformedHeaders_AreRejected()
        {

            Assert.Multiple(() => {

                // Unknown type digit.
                Assert.That(HTTPTOTPAuthentication.TryParseHTTPHeader("TOTP 2 Y2hhcmdpbmdzdGF0aW9uLTAwMDE=:Q042M3k1MDJtYVZo", out _), Is.False);

                // Type digit without credentials.
                Assert.That(HTTPTOTPAuthentication.TryParseHTTPHeader("TOTP 1", out _), Is.False);

                // Credentials without a colon.
                Assert.That(HTTPTOTPAuthentication.TryParseHTTPHeader("TOTP 0 Y2hhcmdpbmdzdGF0aW9uLTAwMDE=", out _), Is.False);

                // Three colon-separated segments.
                Assert.That(HTTPTOTPAuthentication.TryParseHTTPHeader("TOTP 0 YQ==:YQ==:YQ==", out _), Is.False);

                // A fourth whitespace-separated part.
                Assert.That(HTTPTOTPAuthentication.TryParseHTTPHeader("TOTP 0 Y2hhcmdpbmdzdGF0aW9uLTAwMDE= Q042M3k1MDJtYVZo", out _), Is.False);

                // Not Base64.
                Assert.That(HTTPTOTPAuthentication.TryParseHTTPHeader("TOTP 0 not-base64!:Q042M3k1MDJtYVZo", out _), Is.False);

                // Wrong scheme.
                Assert.That(HTTPTOTPAuthentication.TryParseHTTPHeader("Basic 0 Y2hhcmdpbmdzdGF0aW9uLTAwMDE=:Q042M3k1MDJtYVZo", out _), Is.False);

            });

        }

        #endregion

    }

}
