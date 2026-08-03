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

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS
{

    /// <summary>
    /// RFC 9525 § 6.3: the DNS names a server certificate may present, and what they match.
    ///
    /// <para>
    /// Every rule here exists because getting it wrong means accepting a certificate for a host
    /// it was not issued for. That is the failure mode throughout — not a crash, not a rejected
    /// connection, but a connection to the wrong server that looks exactly like a connection to
    /// the right one. So the tests are weighted towards what must <em>not</em> match.
    /// </para>
    /// <para>
    /// The one exception is the wildcard-parse rules, where the failure runs the other way:
    /// § 6.3 says an invalid presented identifier "MUST be ignored", so a certificate carrying
    /// "w*.example.com" alongside a good name must still be usable through the good one.
    /// </para>
    /// </summary>
    [TestFixture]
    public class DNSNamePattern_Tests
    {

        #region What parses, and what does not

        /// <summary>
        /// An ordinary host name is a pattern that matches itself and nothing else.
        /// </summary>
        [Test]
        public void AnExactName_MatchesItselfOnly()
        {

            var pattern = DNSNamePattern.Parse("www.example.com");

            Assert.Multiple(() => {

                Assert.That(pattern.IsWildcard,                             Is.False);
                Assert.That(pattern.FullName,                               Is.EqualTo("www.example.com"));

                Assert.That(pattern.Matches("www.example.com"),             Is.True);
                Assert.That(pattern.Matches("other.example.com"),           Is.False);
                Assert.That(pattern.Matches("example.com"),                 Is.False);
                Assert.That(pattern.Matches("sub.www.example.com"),         Is.False);

            });

        }


        /// <summary>
        /// A leading "*." label is a wildcard, and what remains is an ordinary domain name.
        /// </summary>
        [Test]
        public void AWildcardName_KeepsItsBaseNameSeparately()
        {

            var pattern = DNSNamePattern.Parse("*.example.com");

            Assert.Multiple(() => {

                Assert.That(pattern.IsWildcard,          Is.True);
                Assert.That(pattern.FullName,            Is.EqualTo("*.example.com"));

                Assert.That(pattern.BaseName.Trimmed,
                            Is.EqualTo("example.com"),
                            "the anchor is a real domain name, which is what a caller needs in " +
                            "order to check it against a public suffix list — RFC 9525 § 7.1 " +
                            "leaves that to them");

            });

        }


        /// <summary>
        /// The trailing root dot is not part of the pattern.
        /// </summary>
        /// <remarks>
        /// Certificates carry names without it, but one arrives often enough from configuration
        /// or from text copied out of a zone file. Two spellings of one name would compare
        /// unequal and, worse, a caller comparing <c>FullName</c> strings would find a mismatch
        /// where there is none.
        ///
        /// The normalization is <see cref="DomainName.Trimmed"/>'s rather than this type's — an
        /// explicit trim here turned out to be dead code, since the base name goes through
        /// DomainName either way. Asserted anyway, because it is part of this type's contract
        /// whichever layer happens to provide it.
        /// </remarks>
        [Test]
        public void ATrailingDot_IsNotPartOfThePattern()
        {

            Assert.Multiple(() => {

                Assert.That(DNSNamePattern.Parse("www.example.com."),
                            Is.EqualTo(DNSNamePattern.Parse("www.example.com")));

                Assert.That(DNSNamePattern.Parse("*.example.com.").FullName,
                            Is.EqualTo("*.example.com"));

            });

        }


        /// <summary>
        /// A wildcard is only ever the complete content of the left-most label.
        /// </summary>
        /// <remarks>
        /// <para>
        /// RFC 9525 § 6.3, requirements 1 and 2. The partial forms are the ones this is really
        /// about: RFC 6125 tolerated "f*.example.com" as a SHOULD NOT, implementations disagreed
        /// about what it covered, and RFC 9525 settled it by making such an identifier invalid.
        /// </para>
        /// <para>
        /// An implementation that took the wildcard as a substring match would read
        /// "*.example.com" as covering "evil.attacker.example.com" — the whole point of the
        /// left-most-label rule.
        /// </para>
        /// </remarks>
        [TestCase("w*.example.com",     TestName = "AnInvalidWildcard_IsNotAPattern(partial, prefix)")]
        [TestCase("*w.example.com",     TestName = "AnInvalidWildcard_IsNotAPattern(partial, suffix)")]
        [TestCase("f*o.example.com",    TestName = "AnInvalidWildcard_IsNotAPattern(partial, infix)")]
        [TestCase("www.*.example.com",  TestName = "AnInvalidWildcard_IsNotAPattern(not left-most)")]
        [TestCase("*.*.example.com",    TestName = "AnInvalidWildcard_IsNotAPattern(two wildcards)")]
        [TestCase("example.*",          TestName = "AnInvalidWildcard_IsNotAPattern(right-most)")]
        public void AnInvalidWildcard_IsNotAPattern(String Text)
        {

            Assert.That(DNSNamePattern.TryParse(Text, out _, out var errorResponse),
                        Is.False,
                        $"'{Text}' is not a valid presented identifier and RFC 9525 § 6.3 says " +
                        $"it MUST be ignored");

            // The message, not merely that there is one. DomainName's parse would refuse all of
            // these anyway — an asterisk is not a legal character in a label — so the only thing
            // the wildcard rule contributes here is a reader being told which rule was broken
            // instead of "does not match the required format".
            Assert.That(errorResponse,
                        Does.Contain("wildcard"),
                        $"'{Text}' was refused for the wrong stated reason: {errorResponse}");

        }


        /// <summary>
        /// A bare "*" is refused, though the RFC's two stated requirements would admit it.
        /// </summary>
        /// <remarks>
        /// It would vouch for every single-label name there is — "localhost" included — and no
        /// certificate authority issues one. RFC 9525 § 3 allows an application to be stricter
        /// about wildcards than the document is; there is nothing here worth being less strict
        /// for.
        /// </remarks>
        [Test]
        public void ABareWildcard_IsRefused()
        {

            Assert.That(DNSNamePattern.TryParse("*", out _, out var errorResponse), Is.False);

            // The specific message, because the message is the only thing this rule contributes:
            // without it "*" would be reported as a badly placed wildcard, which it is not — it
            // is a perfectly placed wildcard over nothing at all.
            Assert.That(errorResponse,
                        Does.Contain("needs a domain"),
                        $"refused for the wrong stated reason: {errorResponse}");

        }


        /// <summary>
        /// What is below the wildcard still has to be a domain name.
        /// </summary>
        /// <remarks>
        /// Delegated to <see cref="DomainName"/> rather than re-implemented, so that the label
        /// length limits, the total length limit and the hyphen rules cannot drift apart from
        /// the ones every other name in Hermod is held to.
        /// </remarks>
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("*.-example.com")]
        [TestCase("*.example-.com")]
        [TestCase("exa mple.com")]
        public void AMalformedName_IsNotAPattern(String Text)
        {

            Assert.That(DNSNamePattern.TryParse(Text, out _, out _), Is.False);

        }


        /// <summary>
        /// Reading a certificate's names keeps the good ones and drops the rest.
        /// </summary>
        /// <remarks>
        /// § 6.3 again: an invalid presented identifier "MUST be ignored". Not "the certificate
        /// is invalid" — a certificate carrying one bad entry alongside good ones is still
        /// usable through the good ones, and a reader that threw would take the whole
        /// certificate down with the one entry.
        /// </remarks>
        [Test]
        public void ParseAll_KeepsTheValidNamesAndDropsTheRest()
        {

            var patterns = DNSNamePattern.ParseAll([
                               "www.example.com",
                               "w*.example.com",      // partial wildcard
                               "*.example.com",
                               "*",                   // no domain to be a wildcard of
                               "",                    // nothing at all
                               "*.*.example.com"      // two wildcards
                           ]).ToArray();

            Assert.That(patterns.Select(pattern => pattern.FullName),
                        Is.EqualTo(new[] { "www.example.com", "*.example.com" }).AsCollection);

        }

        #endregion


        #region What a wildcard matches

        /// <summary>
        /// A wildcard stands for exactly one label — never zero, never two.
        /// </summary>
        /// <remarks>
        /// RFC 9525 § 7.1: wildcard certificates "automatically vouch for any single-label
        /// hostnames within their domain, but not multiple levels of domains". Both ends of that
        /// are load-bearing. Matching the bare domain would hand "*.example.com" the apex it was
        /// not issued for; matching two labels would hand it every host in every subdomain,
        /// which is the attack § 7.1 cites.
        /// </remarks>
        [TestCase("www.example.com",        true,  TestName = "AWildcard_StandsForExactlyOneLabel(one label)")]
        [TestCase("WWW.EXAMPLE.COM",        true,  TestName = "AWildcard_StandsForExactlyOneLabel(case-insensitive)")]
        [TestCase("www.example.com.",       true,  TestName = "AWildcard_StandsForExactlyOneLabel(trailing dot)")]
        [TestCase("example.com",            false, TestName = "AWildcard_StandsForExactlyOneLabel(no label)")]
        [TestCase("a.b.example.com",        false, TestName = "AWildcard_StandsForExactlyOneLabel(two labels)")]
        [TestCase("www.example.org",        false, TestName = "AWildcard_StandsForExactlyOneLabel(other domain)")]
        [TestCase("evil.attacker.example",  false, TestName = "AWildcard_StandsForExactlyOneLabel(unrelated)")]
        [TestCase("wwwexample.com",         false, TestName = "AWildcard_StandsForExactlyOneLabel(no label boundary)")]
        public void AWildcard_StandsForExactlyOneLabel(String HostName, Boolean Expected)
        {

            Assert.That(DNSNamePattern.Parse("*.example.com").Matches(HostName),
                        Is.EqualTo(Expected),
                        $"'*.example.com' against '{HostName}'");

        }


        /// <summary>
        /// A wildcard deeper in the tree behaves the same way.
        /// </summary>
        /// <remarks>
        /// Not a restatement of the test above: it is the case where the base name has more than
        /// two labels, which is where an off-by-one in the label comparison would show up and the
        /// two-label case would not.
        /// </remarks>
        [Test]
        public void ADeeperWildcard_StillStandsForExactlyOneLabel()
        {

            var pattern = DNSNamePattern.Parse("*.eu.services.example.com");

            Assert.Multiple(() => {

                Assert.That(pattern.Matches("api.eu.services.example.com"),      Is.True);
                Assert.That(pattern.Matches("eu.services.example.com"),          Is.False);
                Assert.That(pattern.Matches("a.api.eu.services.example.com"),    Is.False);
                Assert.That(pattern.Matches("api.us.services.example.com"),      Is.False);

            });

        }


        /// <summary>
        /// Matching is case-insensitive in both directions, as RFC 4343 requires of any name.
        /// </summary>
        [Test]
        public void MatchingIgnoresCase_OnBothSides()
        {

            Assert.Multiple(() => {

                Assert.That(DNSNamePattern.Parse("*.EXAMPLE.com").Matches("www.example.COM"), Is.True);
                Assert.That(DNSNamePattern.Parse("WWW.Example.Com").Matches("www.example.com"), Is.True);

            });

        }


        /// <summary>
        /// A reference identifier that is not a host name matches nothing.
        /// </summary>
        /// <remarks>
        /// The reference identifier is the name the client set out to reach, so text that cannot
        /// be a domain name at all is the caller's mistake. Returning false rather than throwing
        /// keeps a hostname check from turning a bad input into an exception on the TLS path.
        /// </remarks>
        [TestCase("")]
        [TestCase("not a hostname")]
        [TestCase("*.example.com")]
        public void AnUnparseableHostName_MatchesNothing(String HostName)
        {

            Assert.That(DNSNamePattern.Parse("*.example.com").Matches(HostName),
                        Is.False,
                        "a wildcard is a presented identifier, never a reference one — § 6.3 " +
                        "covers \"only wildcard characters in presented identifiers, not " +
                        "wildcard characters in reference identifiers\"");

        }

        #endregion


        #region Behaving like a DomainName

        /// <summary>
        /// Equality is about the pattern, not about what it covers.
        /// </summary>
        /// <remarks>
        /// "*.example.com" and "www.example.com" overlap without being the same thing, and a
        /// collection of patterns that treated them as equal would silently lose one.
        /// </remarks>
        [Test]
        public void EqualityComparesThePattern_NotWhatItCovers()
        {

            var wildcard = DNSNamePattern.Parse("*.example.com");
            var exact    = DNSNamePattern.Parse("www.example.com");

            Assert.Multiple(() => {

                Assert.That(wildcard, Is.Not.EqualTo(exact));
                Assert.That(wildcard.Matches("www.example.com"), Is.True);

                Assert.That(DNSNamePattern.Parse("*.EXAMPLE.com"),
                            Is.EqualTo(wildcard),
                            "and case is not a difference (RFC 4343)");

                Assert.That(DNSNamePattern.Parse("*.EXAMPLE.com").GetHashCode(),
                            Is.EqualTo(wildcard.GetHashCode()),
                            "which the hash code has to agree with, or a HashSet loses them");

            });

        }


        /// <summary>
        /// Patterns sort, so a set of them can be reported in a stable order.
        /// </summary>
        [Test]
        public void PatternsSortCaseInsensitively()
        {

            var sorted = new[] {
                             DNSNamePattern.Parse("www.example.com"),
                             DNSNamePattern.Parse("*.example.com"),
                             DNSNamePattern.Parse("API.example.com")
                         }.Order().Select(pattern => pattern.FullName).ToArray();

            Assert.That(sorted,
                        Is.EqualTo(new[] { "*.example.com", "API.example.com", "www.example.com" }).AsCollection);

        }

        #endregion

    }

}
