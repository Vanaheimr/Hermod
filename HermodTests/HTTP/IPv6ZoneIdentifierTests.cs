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

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// IPv6 zone identifiers in URIs: RFC 6874. H-20 in HTTP1ConformanceTests.
    /// </summary>
    /// <remarks>
    /// A zone identifier is attached to an address with "%", and in a URI that
    /// "%" is percent-encoded, so the scoped address fe80::a%en1 is written
    /// http://[fe80::a%25en1] (§2). The plain address parser splits at the
    /// first "%" and read the zone of that URL as "25en1" — not a refusal but
    /// a success with the wrong answer, which is the worse of the two.
    ///
    /// The two forms genuinely collide, which is why the decode is a separate
    /// method and not a flag: bare, "fe80::1%25" is Windows naming interface
    /// index 25, and the identical text in a URI is an address whose ZoneID is
    /// empty.
    /// </remarks>
    [TestFixture]
    public class IPv6ZoneIdentifierTests
    {

        #region The URI form

        [Test]
        public void TheEncodedSeparatorIsDecoded()
        {

            Assert.That(IPv6Address.TryParseURIHost("[fe80::a%25en1]", out var address), Is.True);

            Assert.Multiple(() => {
                Assert.That(address.InterfaceId,  Is.EqualTo("en1"));
                Assert.That(address.ToString(),   Does.Contain("fe80"));
            });

        }

        [Test]
        public void BracketsAreOptional()
        {
            Assert.That(IPv6Address.TryParseURIHost("fe80::a%25en1", out var address), Is.True);
            Assert.That(address.InterfaceId, Is.EqualTo("en1"));
        }

        /// <summary>
        /// A numeric zone, which is what Windows uses, and the case that shows
        /// the encoding is real: "%2525" is "%25" encoded, so the ZoneID is 25.
        /// </summary>
        [Test]
        public void ANumericZoneIsEncodedTwice()
        {
            Assert.That(IPv6Address.TryParseURIHost("[fe80::a%2525]", out var address), Is.True);
            Assert.That(address.InterfaceId, Is.EqualTo("25"));
        }

        /// <summary>
        /// §2 allows a ZoneID to carry percent-encoded characters "for
        /// compatibility with existing devices that use them".
        /// </summary>
        [Test]
        public void AnEncodedCharacterInsideTheZoneIsDecoded()
        {
            Assert.That(IPv6Address.TryParseURIHost("[fe80::a%25eth%300]", out var address), Is.True);
            Assert.That(address.InterfaceId, Is.EqualTo("eth00"));
        }

        /// <summary>
        /// A bare "%" is accepted, which §3 suggests rather than requires — "be
        /// liberal with what you accept", so that an address pasted out of a
        /// ping command works. That section is explicitly non-normative.
        /// </summary>
        [Test]
        public void ABareSeparatorIsAcceptedToo()
        {
            Assert.That(IPv6Address.TryParseURIHost("[fe80::a%en1]", out var address), Is.True);
            Assert.That(address.InterfaceId, Is.EqualTo("en1"));
        }

        [Test]
        public void AnAddressWithoutAZoneIsUnaffected()
        {
            Assert.That(IPv6Address.TryParseURIHost("[::1]", out var address), Is.True);
            Assert.That(address.InterfaceId, Is.Empty);
        }

        /// <summary>
        /// ZoneID = 1*( unreserved / pct-encoded ), so an empty one is not a
        /// ZoneID: this is malformed rather than unscoped.
        /// </summary>
        [Test]
        public void AnEmptyZoneIsRefused()
        {
            Assert.That(IPv6Address.TryParseURIHost("[fe80::a%25]", out _), Is.False);
        }

        #endregion

        #region The bare form is left alone

        /// <summary>
        /// The collision, pinned: the plain parser must keep reading
        /// "fe80::1%25" as Windows means it — interface index 25 — because
        /// that text has not been through a URI.
        /// </summary>
        [Test]
        public void ThePlainParserStillReadsAWindowsNumericZone()
        {

            Assert.That(IPv6Address.TryParse("fe80::1%25", out var address), Is.True);
            Assert.That(address.InterfaceId, Is.EqualTo("25"), "a bare %25 is zone 25, not an encoded separator");

            // And through a URI the same text means something else.
            Assert.That(IPv6Address.TryParseURIHost("[fe80::1%25]", out _), Is.False);

        }

        #endregion

        #region In a URL, and in the Host field

        [Test]
        public void AURLKeepsWhatWasWritten()
        {

            var url = URL.Parse("http://[fe80::a%25en1]:8080/status");

            Assert.Multiple(() => {
                Assert.That(url.Host.ToString(),  Does.Contain("%25en1"), "the URI form is what a URI carries");
                Assert.That(url.Port?.ToUInt16(), Is.EqualTo(8080),       "and the port is still found behind the brackets");
                Assert.That(url.Path.ToString(),  Is.EqualTo("/status"));
            });

        }

        /// <summary>
        /// RFC 6874 §3: "URIs including a ZoneID have no meaning outside the
        /// originating node. It would therefore be highly desirable for a
        /// browser to remove the ZoneID from a URI before including that URI in
        /// an HTTP request." Highly desirable rather than required — the
        /// section makes no normative statements — and still right: the zone
        /// names an interface of this machine, which the peer has no use for.
        /// </summary>
        [Test]
        public void TheHostFieldCarriesNoZone()
        {

            var url = URL.Parse("http://[fe80::a%25en1]:8080/status");

            Assert.Multiple(() => {

                Assert.That(url.HostHeader.ToString(), Does.Not.Contain("en1"),  "the zone is gone");
                Assert.That(url.HostHeader.ToString(), Does.Not.Contain("%"),    "and so is its separator");
                Assert.That(url.HostHeader.ToString(), Does.Contain("8080"),     "the port stays");

                // The address itself comes back fully expanded, which is what
                // HTTPHostname.From makes of it — a valid IPv6address and not
                // RFC 5952's canonical short form. Pinned here rather than
                // asserted as "fe80::a", which is what this test expected
                // first and is not what the type does.
                Assert.That(url.HostHeader.ToString(), Is.EqualTo("[fe80:0000:0000:0000:0000:0000:0000:000a]:8080"));

            });

        }

        [Test]
        public void AHostFieldWithoutAZoneIsUnchanged()
        {

            var url = URL.Parse("http://[::1]:8080/");

            Assert.That(url.HostHeader.ToString(), Does.Contain("::1"));

        }

        #endregion

    }

}
