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
    /// The Strict-Transport-Security field (RFC 6797, Section 6.1), which
    /// Hermod could emit as text and not read as a value — H-12 in
    /// HTTP1ConformanceTests.
    /// </summary>
    /// <remarks>
    /// Most of these are about what is refused, and that is the point: the
    /// alternative to refusing a malformed policy is enforcing one nobody
    /// stated. Section 6.1 item 4 says a user agent "MUST ignore any STS
    /// header field containing directives, or other header field value data,
    /// that does not conform", and item 2 that "all directives MUST appear
    /// only once".
    /// </remarks>
    [TestFixture]
    public class StrictTransportSecurityTests
    {

        #region What is accepted

        [Test]
        public void MaxAgeAlone()
        {

            Assert.That(StrictTransportSecurity.TryParse("max-age=31536000", out var policy), Is.True);

            Assert.Multiple(() => {
                Assert.That(policy.MaxAge,             Is.EqualTo(TimeSpan.FromDays(365)));
                Assert.That(policy.IncludeSubDomains,  Is.False);
                Assert.That(policy.Preload,            Is.False);
            });

        }

        [Test]
        public void MaxAgeWithSubDomains()
        {

            Assert.That(StrictTransportSecurity.TryParse("max-age=63072000; includeSubDomains", out var policy), Is.True);

            Assert.Multiple(() => {
                Assert.That(policy.MaxAge,             Is.EqualTo(TimeSpan.FromDays(730)));
                Assert.That(policy.IncludeSubDomains,  Is.True);
            });

        }

        /// <summary>
        /// "The order of appearance of directives is not significant" (item 1),
        /// "directive names are case-insensitive" (item 3).
        /// </summary>
        [Test]
        public void OrderAndCaseDoNotMatter()
        {

            Assert.That(StrictTransportSecurity.TryParse("INCLUDESUBDOMAINS; Max-Age=1", out var policy), Is.True);

            Assert.Multiple(() => {
                Assert.That(policy.MaxAge,             Is.EqualTo(TimeSpan.FromSeconds(1)));
                Assert.That(policy.IncludeSubDomains,  Is.True);
            });

        }

        /// <summary>
        /// A directive-value may be a quoted-string, read "after quoted-string
        /// unescaping, if necessary" (6.1.1).
        /// </summary>
        [Test]
        public void AQuotedMaxAgeIsUnquoted()
        {
            Assert.That(StrictTransportSecurity.TryParse("max-age=\"600\"", out var policy), Is.True);
            Assert.That(policy.MaxAge, Is.EqualTo(TimeSpan.FromSeconds(600)));
        }

        /// <summary>
        /// Item 5: an unrecognised directive is ignored and the recognised ones
        /// are still processed. Being strict here would mean a future directive
        /// disabling today's policy.
        /// </summary>
        [Test]
        public void AnUnknownDirectiveIsIgnoredAndTheRestProcessed()
        {

            Assert.That(StrictTransportSecurity.TryParse("max-age=600; somethingNew=42; includeSubDomains", out var policy), Is.True);

            Assert.Multiple(() => {
                Assert.That(policy.MaxAge,             Is.EqualTo(TimeSpan.FromSeconds(600)));
                Assert.That(policy.IncludeSubDomains,  Is.True);
            });

        }

        /// <summary>
        /// "max-age=0" is valid and means the opposite of a policy: stop
        /// regarding this host as a Known HSTS Host (the note in 6.1.1). A
        /// parser that refused zero would make a host unable to withdraw.
        /// </summary>
        [Test]
        public void ZeroIsAPolicyToo()
        {
            Assert.That(StrictTransportSecurity.TryParse("max-age=0", out var policy), Is.True);
            Assert.That(policy.MaxAge, Is.EqualTo(TimeSpan.Zero));
        }

        /// <summary>
        /// The grammar is "[ directive ] *( ";" [ directive ] )", so an empty
        /// one is allowed and a trailing semicolon is not malformed.
        /// </summary>
        [Test]
        public void AnEmptyDirectiveIsAllowedByTheGrammar()
        {
            Assert.That(StrictTransportSecurity.TryParse("max-age=1;;", out _), Is.True);
        }

        /// <summary>
        /// delta-seconds is 1*DIGIT with no upper bound, so a value no TimeSpan
        /// can hold is clamped rather than refused: it is syntactically fine,
        /// and it means "a very long time".
        /// </summary>
        [Test]
        public void AnAbsurdMaxAgeIsClampedRatherThanRefused()
        {
            Assert.That(StrictTransportSecurity.TryParse("max-age=99999999999999999999", out var policy), Is.True);
            Assert.That(policy.MaxAge, Is.EqualTo(TimeSpan.MaxValue));
        }

        #endregion

        #region What is refused

        [Test]
        public void WhatIsRefused()
        {
            Assert.Multiple(() => {

                Assert.That(StrictTransportSecurity.TryParse("",                                 out _), Is.False, "nothing at all");
                Assert.That(StrictTransportSecurity.TryParse("includeSubDomains",                out _), Is.False, "no max-age, which is REQUIRED");
                Assert.That(StrictTransportSecurity.TryParse("max-age",                          out _), Is.False, "max-age without its value");
                Assert.That(StrictTransportSecurity.TryParse("max-age=",                         out _), Is.False, "an empty value");
                Assert.That(StrictTransportSecurity.TryParse("max-age=forever",                  out _), Is.False, "a value that is not delta-seconds");
                Assert.That(StrictTransportSecurity.TryParse("max-age=-1",                       out _), Is.False, "a negative value: 1*DIGIT has no sign");
                Assert.That(StrictTransportSecurity.TryParse("max-age=1.5",                      out _), Is.False, "seconds are whole");
                Assert.That(StrictTransportSecurity.TryParse("max-age=1; max-age=2",             out _), Is.False, "a directive appearing twice");
                Assert.That(StrictTransportSecurity.TryParse("max-age=1; includeSubDomains; includeSubDomains", out _), Is.False, "the same, valueless");
                Assert.That(StrictTransportSecurity.TryParse("max-age=1; includeSubDomains=yes", out _), Is.False, "a value on a valueless directive");

            });
        }

        #endregion

        #region Generating, and the field

        [Test]
        public void WhatIsGeneratedParsesBack()
        {

            var policy = new StrictTransportSecurity(TimeSpan.FromDays(730), IncludeSubDomains: true, Preload: true);

            Assert.Multiple(() => {

                Assert.That(policy.ToString(), Is.EqualTo("max-age=63072000; includeSubDomains; preload"));

                Assert.That(StrictTransportSecurity.TryParse(policy.ToString(), out var again), Is.True);
                Assert.That(again.MaxAge,             Is.EqualTo(policy.MaxAge));
                Assert.That(again.IncludeSubDomains,  Is.True);
                Assert.That(again.Preload,            Is.True);

            });

        }

        [Test]
        public void ANegativeMaxAgeCannotBeConstructed()
        {
            Assert.That(
                () => new StrictTransportSecurity(TimeSpan.FromSeconds(-1)),
                Throws.InstanceOf<ArgumentOutOfRangeException>()
            );
        }

        /// <summary>
        /// The field, which is where the parser is reached from.
        /// </summary>
        [Test]
        public void TheFieldParsesAndSerializes()
        {
            Assert.Multiple(() => {

                Assert.That(
                    HTTPResponseHeaderField.StrictTransportSecurity.StringParser!("max-age=600; includeSubDomains", out var policy, out _),
                    Is.True
                );

                Assert.That(policy.MaxAge,            Is.EqualTo(TimeSpan.FromSeconds(600)));
                Assert.That(policy.IncludeSubDomains, Is.True);

                Assert.That(
                    HTTPResponseHeaderField.StrictTransportSecurity.ValueSerializer(
                        new StrictTransportSecurity(TimeSpan.FromSeconds(600), IncludeSubDomains: true)
                    ),
                    Is.EqualTo("max-age=600; includeSubDomains")
                );

            });
        }

        /// <summary>
        /// And the default this stack has been offering as text all along is a
        /// policy this parser accepts — the two were written years apart and
        /// nothing had ever compared them.
        /// </summary>
        [Test]
        public void TheDefaultOfSecurityHeaderOptionsIsAPolicy()
        {

            Assert.That(
                StrictTransportSecurity.TryParse(SecurityHeaderOptions.DefaultStrictTransportSecurity, out var policy),
                Is.True,
                SecurityHeaderOptions.DefaultStrictTransportSecurity
            );

            Assert.Multiple(() => {
                Assert.That(policy.MaxAge,             Is.EqualTo(TimeSpan.FromDays(730)), "two years");
                Assert.That(policy.IncludeSubDomains,  Is.True);
            });

        }

        #endregion

    }

}
