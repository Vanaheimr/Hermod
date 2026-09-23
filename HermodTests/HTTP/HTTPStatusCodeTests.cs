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

using System.Reflection;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// HTTPStatusCode tests.
    ///
    /// Note: Name is not decoration. HTTPResponseBuilder builds the status line
    ///       as "{ProtocolName}/{ProtocolVersion} {Code} {Name}", so a wrong
    ///       Name is wrong on the wire, not just in the API — which is what
    ///       made "425 No code" a defect rather than a naming quibble.
    /// </summary>
    [TestFixture]
    public class HTTPStatusCodeTests
    {

        #region Data

        /// <summary>
        /// Every status code in the IANA "HTTP Status Codes" registry, with the
        /// reason phrase the registry gives it.
        /// https://www.iana.org/assignments/http-status-codes/http-status-codes.xhtml
        ///
        /// 509 is deliberately absent: it is an Apache extension that was never
        /// registered. Hermod defines it anyway, which this table does not mind —
        /// it asserts that the registry is covered, not that nothing else exists.
        /// </summary>
        private static readonly (UInt16 Code, String Name)[] registry = [

            (100, "Continue"),
            (101, "Switching Protocols"),
            (102, "Processing"),
            (103, "Early Hints"),

            (200, "OK"),
            (201, "Created"),
            (202, "Accepted"),
            (203, "Non-Authoritative Information"),
            (204, "No Content"),
            (205, "Reset Content"),
            (206, "Partial Content"),
            (207, "Multi-Status"),
            (208, "Already Reported"),
            (226, "IM Used"),

            (300, "Multiple Choices"),
            (301, "Moved Permanently"),
            (302, "Found"),
            (303, "See Other"),
            (304, "Not Modified"),
            (305, "Use Proxy"),
            (306, "(Unused)"),
            (307, "Temporary Redirect"),
            (308, "Permanent Redirect"),

            (400, "Bad Request"),
            (401, "Unauthorized"),
            (402, "Payment Required"),
            (403, "Forbidden"),
            (404, "Not Found"),
            (405, "Method Not Allowed"),
            (406, "Not Acceptable"),
            (407, "Proxy Authentication Required"),
            (408, "Request Timeout"),
            (409, "Conflict"),
            (410, "Gone"),
            (411, "Length Required"),
            (412, "Precondition Failed"),
            (413, "Content Too Large"),
            (414, "URI Too Long"),
            (415, "Unsupported Media Type"),
            (416, "Range Not Satisfiable"),
            (417, "Expectation Failed"),
            (418, "(Unused)"),
            (421, "Misdirected Request"),
            (422, "Unprocessable Content"),
            (423, "Locked"),
            (424, "Failed Dependency"),
            (425, "Too Early"),
            (426, "Upgrade Required"),
            (428, "Precondition Required"),
            (429, "Too Many Requests"),
            (431, "Request Header Fields Too Large"),
            (451, "Unavailable For Legal Reasons"),

            (500, "Internal Server Error"),
            (501, "Not Implemented"),
            (502, "Bad Gateway"),
            (503, "Service Unavailable"),
            (504, "Gateway Timeout"),
            (505, "HTTP Version Not Supported"),
            (506, "Variant Also Negotiates"),
            (507, "Insufficient Storage"),
            (508, "Loop Detected"),
            (510, "Not Extended"),
            (511, "Network Authentication Required")

        ];

        /// <summary>
        /// Every HTTPStatusCode this class defines as a public static field —
        /// which is also exactly the set TryParseUInt16(...) searches, since it
        /// resolves a code by reflecting over these fields.
        /// </summary>
        private static IEnumerable<HTTPStatusCode> Defined()

            => typeof(HTTPStatusCode).
                   GetFields(BindingFlags.Public | BindingFlags.Static).
                   Select (fieldInfo => fieldInfo.GetValue(null) as HTTPStatusCode).
                   Where  (statusCode => statusCode is not null)!;

        #endregion


        #region EveryRegisteredStatusCodeIsDefined()

        /// <summary>
        /// TryParseUInt16(...) never fails: an unknown code is answered with a
        /// synthesized HTTPStatusCode whose Name is the number itself and whose
        /// Description is null. That is the right behaviour for a code arriving
        /// from the wire, and the wrong one for a code we are supposed to know —
        /// so "is it defined" has to be asked as "is it more than the number".
        /// </summary>
        [Test]
        public void EveryRegisteredStatusCodeIsDefined()
        {

            var missing = registry.
                              Where (entry => HTTPStatusCode.ParseUInt16(entry.Code).Name == entry.Code.ToString()).
                              Select(entry => $"{entry.Code} {entry.Name}").
                              ToArray();

            Assert.That(missing,
                        Is.Empty,
                        $"Registered status codes with no definition: {String.Join(", ", missing)}");

        }

        #endregion

        #region EveryStatusCodeIsDefinedExactlyOnce()

        /// <summary>
        /// TryParseUInt16(...) takes the *first* field whose Code matches, so a
        /// second field for the same code would silently shadow one of the two,
        /// and which one would depend on field order. This is the reason 425 was
        /// renamed from NoCode rather than joined by a TooEarly beside it.
        /// </summary>
        [Test]
        public void EveryStatusCodeIsDefinedExactlyOnce()
        {

            var duplicates = Defined().
                                 GroupBy(statusCode => statusCode.Code).
                                 Where  (group      => group.Count() > 1).
                                 Select (group      => $"{group.Key} ({String.Join(" / ", group.Select(statusCode => statusCode.Name))})").
                                 ToArray();

            Assert.That(duplicates,
                        Is.Empty,
                        $"Status codes defined more than once: {String.Join(", ", duplicates)}");

        }

        #endregion

        #region NamesMatchTheRegistry_ExceptForSixKnownLegacyPhrases()

        /// <summary>
        /// The reason phrase is advisory (RFC 9110, Section 15: a client SHOULD
        /// ignore its content), so the six below are not defects — but they are
        /// drift, and drift that nothing records is drift nobody can decide about.
        ///
        /// 306 and 418 are reserved codes carrying the names they had before they
        /// were reserved; 413, 414, 416 and 422 carry their RFC 7231 names, which
        /// RFC 9110 renamed. Renaming the fields would be a breaking API change
        /// for every downstream Vanaheimr project, so it is a decision, not a fix.
        ///
        /// This test asserts the *exact set* of divergences: a new one fails here,
        /// and so does silently resolving one of these six without saying so.
        /// </summary>
        [Test]
        public void NamesMatchTheRegistry_ExceptForSixKnownLegacyPhrases()
        {

            var drifted = registry.
                              Where (entry => HTTPStatusCode.ParseUInt16(entry.Code).Name != entry.Name).
                              Select(entry => entry.Code).
                              ToArray();

            Assert.That(drifted,
                        Is.EquivalentTo(new UInt16[] { 306, 413, 414, 416, 418, 422 }),
                        "The set of reason phrases diverging from the IANA registry changed.");

            // And they are these, so that a rename cannot pass as a no-op.
            Assert.Multiple(() => {
                Assert.That(HTTPStatusCode.SwitchProxy.                 Name, Is.EqualTo("Switch Proxy"));
                Assert.That(HTTPStatusCode.RequestEntityTooLarge.       Name, Is.EqualTo("Request Entity Too Large"));
                Assert.That(HTTPStatusCode.RequestURITooLong.           Name, Is.EqualTo("Request-URI Too Long"));
                Assert.That(HTTPStatusCode.RequestedRangeNotSatisfiable.Name, Is.EqualTo("Requested Range Not Satisfiable"));
                Assert.That(HTTPStatusCode.ImATeapot.                   Name, Is.EqualTo("I'm a teapot"));
                Assert.That(HTTPStatusCode.UnprocessableEntity.         Name, Is.EqualTo("Unprocessable Entity"));
            });

        }

        #endregion

        #region TooEarlyReplacedNoCode()

        /// <summary>
        /// 425 used to be defined as NoCode / "No code", which would have gone out
        /// on the wire as the status line "HTTP/1.1 425 No code". RFC 8470 assigns
        /// 425 to Too Early, and the HTTP/2 stack already answers 425 for exactly
        /// that reason — it simply never went through HTTPStatusCode to do it.
        /// </summary>
        [Test]
        public void TooEarlyReplacedNoCode()
        {

            Assert.Multiple(() => {

                Assert.That(HTTPStatusCode.TooEarly.Code,               Is.EqualTo(425));
                Assert.That(HTTPStatusCode.TooEarly.Name,               Is.EqualTo("Too Early"));
                Assert.That(HTTPStatusCode.TooEarly.Description,        Is.Not.Null);

                // The wire-visible half.
                Assert.That(HTTPStatusCode.TooEarly.HTTPResponseString, Is.EqualTo("HTTP/1.1 425 Too Early"));
                Assert.That(HTTPStatusCode.ParseUInt16(425).Name,       Is.EqualTo("Too Early"));

                // NoCode is gone rather than kept beside it.
                Assert.That(typeof(HTTPStatusCode).GetField("NoCode", BindingFlags.Public | BindingFlags.Static),
                            Is.Null);

            });

        }

        #endregion

        #region CodesAddedForTheHTTP1AuditCarryTheirRegisteredName()

        /// <summary>
        /// The nine codes this change added, spelled out one by one rather than
        /// only covered by the registry sweep above: a sweep that is wrong in the
        /// same way as the code it checks passes, and the whole point of these was
        /// that they had to be typed in by hand from the RFCs.
        /// </summary>
        [Test]
        public void CodesAddedForTheHTTP1AuditCarryTheirRegisteredName()
        {

            Assert.Multiple(() => {

                Assert.That(HTTPStatusCode.Processing.                   Code, Is.EqualTo(102));
                Assert.That(HTTPStatusCode.Processing.                   Name, Is.EqualTo("Processing"));

                Assert.That(HTTPStatusCode.EarlyHints.                   Code, Is.EqualTo(103));
                Assert.That(HTTPStatusCode.EarlyHints.                   Name, Is.EqualTo("Early Hints"));

                Assert.That(HTTPStatusCode.AlreadyReported.              Code, Is.EqualTo(208));
                Assert.That(HTTPStatusCode.AlreadyReported.              Name, Is.EqualTo("Already Reported"));

                Assert.That(HTTPStatusCode.IMUsed.                       Code, Is.EqualTo(226));
                Assert.That(HTTPStatusCode.IMUsed.                       Name, Is.EqualTo("IM Used"));

                Assert.That(HTTPStatusCode.PermanentRedirect.            Code, Is.EqualTo(308));
                Assert.That(HTTPStatusCode.PermanentRedirect.            Name, Is.EqualTo("Permanent Redirect"));

                Assert.That(HTTPStatusCode.MisdirectedRequest.           Code, Is.EqualTo(421));
                Assert.That(HTTPStatusCode.MisdirectedRequest.           Name, Is.EqualTo("Misdirected Request"));

                Assert.That(HTTPStatusCode.UnavailableForLegalReasons.   Code, Is.EqualTo(451));
                Assert.That(HTTPStatusCode.UnavailableForLegalReasons.   Name, Is.EqualTo("Unavailable For Legal Reasons"));

                Assert.That(HTTPStatusCode.LoopDetected.                 Code, Is.EqualTo(508));
                Assert.That(HTTPStatusCode.LoopDetected.                 Name, Is.EqualTo("Loop Detected"));

                Assert.That(HTTPStatusCode.NetworkAuthenticationRequired.Code, Is.EqualTo(511));
                Assert.That(HTTPStatusCode.NetworkAuthenticationRequired.Name, Is.EqualTo("Network Authentication Required"));

                // Every one of them says what it is for; the old 425 did not.
                foreach (var statusCode in new[] {
                             HTTPStatusCode.Processing,       HTTPStatusCode.EarlyHints,
                             HTTPStatusCode.AlreadyReported,  HTTPStatusCode.IMUsed,
                             HTTPStatusCode.PermanentRedirect, HTTPStatusCode.MisdirectedRequest,
                             HTTPStatusCode.UnavailableForLegalReasons,
                             HTTPStatusCode.LoopDetected,     HTTPStatusCode.NetworkAuthenticationRequired
                         })
                {
                    Assert.That(statusCode.Description, Is.Not.Null.And.Not.Empty, $"{statusCode.Code} has no description");
                }

            });

        }

        #endregion

        #region ClassificationAgreesWithTheCodeRange()

        /// <summary>
        /// Exactly one of the five class predicates holds for every real status
        /// code. The ClientError sentinel (code 0) is the one exception — it is
        /// not a status code at all, but a marker for "the request never went out".
        /// </summary>
        [Test]
        public void ClassificationAgreesWithTheCodeRange()
        {

            Assert.Multiple(() => {

                foreach (var statusCode in Defined().Where(statusCode => statusCode.Code > 0))
                {

                    var classes = new[] {
                                      statusCode.IsInformational,
                                      statusCode.IsSuccessful,
                                      statusCode.IsRedirection,
                                      statusCode.IsClientError,
                                      statusCode.IsServerError
                                  }.Count(isInClass => isInClass);

                    Assert.That(classes,
                                Is.EqualTo(1),
                                $"{statusCode.SimpleString} is in {classes} of the five status classes");

                }

                Assert.That(HTTPStatusCode.ClientError.Code,            Is.EqualTo(0));
                Assert.That(HTTPStatusCode.ClientError.IsClientError,   Is.False, "The ClientError sentinel is code 0, not a 4xx");

            });

        }

        #endregion

        #region IsNotSuccessfulIsTheNegationOfIsSuccessful()

        /// <summary>
        /// IsNotSuccessful read "Code &lt; 200 &amp;&amp; Code >= 300", which no
        /// number satisfies — the property was a constant false for every status
        /// code, including 404 and 500. Nothing in the three conformance
        /// repositories called it, which is why it survived; a caller outside
        /// them would have been silently wrong on every response.
        /// </summary>
        [Test]
        public void IsNotSuccessfulIsTheNegationOfIsSuccessful()
        {

            Assert.Multiple(() => {

                Assert.That(HTTPStatusCode.OK.             IsNotSuccessful, Is.False);
                Assert.That(HTTPStatusCode.NoContent.      IsNotSuccessful, Is.False);
                Assert.That(HTTPStatusCode.Continue.       IsNotSuccessful, Is.True);
                Assert.That(HTTPStatusCode.NotFound.       IsNotSuccessful, Is.True);
                Assert.That(HTTPStatusCode.InternalServerError.IsNotSuccessful, Is.True);

                foreach (var statusCode in Defined())
                    Assert.That(statusCode.IsNotSuccessful,
                                Is.EqualTo(!statusCode.IsSuccessful),
                                $"{statusCode.SimpleString}");

            });

        }

        #endregion

    }

}
