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
    /// HTTPMethod tests.
    ///
    /// Note: The HTTP method registry is static, therefore these tests must not run in
    ///       parallel with anything else touching it.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class HTTPMethodTests
    {

        #region TryParse_DoesNotGrowTheRegistry()

        /// <summary>
        /// Parse(...)/TryParse(...) must never add to the registry, otherwise a remote peer
        /// could grow it without any bound, simply by sending a new method on every request.
        /// </summary>
        [Test]
        public void TryParse_DoesNotGrowTheRegistry()
        {

            var before = HTTPMethod.RegisteredCount;

            Assert.That(HTTPMethod.TryParse("METHODFROMTHEWIRE1", out var method1), Is.True);
            Assert.That(HTTPMethod.TryParse("METHODFROMTHEWIRE2", out _),           Is.True);
            Assert.That(HTTPMethod.Parse   ("METHODFROMTHEWIRE3"),                  Is.Not.Null);

            Assert.That(HTTPMethod.RegisteredCount, Is.EqualTo(before));

            // An unknown method is still parsed successfully...
            Assert.That(method1!.MethodName,        Is.EqualTo("METHODFROMTHEWIRE1"));

            // ...and is fully interchangeable with a registered one.
            Assert.That(HTTPMethod.TryParse("METHODFROMTHEWIRE1", out var again), Is.True);
            Assert.That(again,                      Is.EqualTo(method1));
            Assert.That(again!.GetHashCode(),       Is.EqualTo(method1.GetHashCode()));

        }

        #endregion

        #region Register_GrowsTheRegistry()

        /// <summary>
        /// Register(...) is the only way to grow the registry.
        /// </summary>
        [Test]
        public void Register_GrowsTheRegistry()
        {

            var before   = HTTPMethod.RegisteredCount;
            var declared = HTTPMethod.Register("HTTPMETHODTESTS-DECLARED",
                                               IsSafe:        false,
                                               IsIdempotent:  true,
                                               Description:   "A method declared by the unit tests");

            Assert.That(HTTPMethod.RegisteredCount, Is.EqualTo(before + 1));
            Assert.That(declared.MethodName,        Is.EqualTo("HTTPMETHODTESTS-DECLARED"));
            Assert.That(declared.IsSafe,            Is.False);
            Assert.That(declared.IsIdempotent,      Is.True);

            // Registering the very same method again returns the already registered one.
            Assert.That(HTTPMethod.Register("HTTPMETHODTESTS-DECLARED"), Is.SameAs(declared));
            Assert.That(HTTPMethod.RegisteredCount, Is.EqualTo(before + 1));

            // ...and now Parse(...) finds it.
            Assert.That(HTTPMethod.Parse("HTTPMETHODTESTS-DECLARED"), Is.SameAs(declared));

        }

        #endregion

        #region MethodNames_AreValidatedAgainstRFC9110()

        /// <summary>
        /// RFC 9110 section 9.1: the method token is a token as defined in section 5.6.2,
        /// which allows "!#$%&amp;'*+-.^_`|~", DIGIT and ALPHA.
        /// </summary>
        [Test]
        public void MethodNames_AreValidatedAgainstRFC9110()
        {

            Assert.That(HTTPMethod.TryParse("GET",         out _), Is.True);
            Assert.That(HTTPMethod.TryParse("X-My_Verb!",  out _), Is.True);

            Assert.That(HTTPMethod.TryParse(null,          out _), Is.False);
            Assert.That(HTTPMethod.TryParse("",            out _), Is.False);
            Assert.That(HTTPMethod.TryParse("GE T",        out _), Is.False);
            Assert.That(HTTPMethod.TryParse("GET/x",       out _), Is.False);
            Assert.That(HTTPMethod.TryParse("GET(x)",      out _), Is.False);

            Assert.Throws<ArgumentException>(() => HTTPMethod.Parse   ("bad method"));
            Assert.Throws<ArgumentException>(() => HTTPMethod.Register("bad method"));

        }

        #endregion

        #region MethodNames_AreCaseSensitive()

        /// <summary>
        /// RFC 9110 section 9.1: "The method token is case-sensitive". Unlike an URL scheme
        /// a method name must therefore NOT be normalized.
        /// </summary>
        [Test]
        public void MethodNames_AreCaseSensitive()
        {

            var lowerCase = HTTPMethod.Parse("get");

            Assert.That(lowerCase.MethodName,   Is.EqualTo("get"));
            Assert.That(lowerCase,              Is.Not.EqualTo(HTTPMethod.GET));
            Assert.That(HTTPMethod.Parse("GET"), Is.SameAs(HTTPMethod.GET));

        }

        #endregion

        #region Comparison_TreatsNullConsistently()

        /// <summary>
        /// CompareTo(null) returns 1, so null sorts before any HTTP method. All comparison
        /// operators must agree with that instead of throwing.
        /// </summary>
        [Test]
        public void Comparison_TreatsNullConsistently()
        {

            HTTPMethod? nothing = null;

            Assert.That(nothing <  HTTPMethod.GET,  Is.True);
            Assert.That(nothing <= HTTPMethod.GET,  Is.True);
            Assert.That(nothing >  HTTPMethod.GET,  Is.False);
            Assert.That(nothing >= HTTPMethod.GET,  Is.False);

            Assert.That(HTTPMethod.GET >  nothing,  Is.True);
            Assert.That(HTTPMethod.GET >= nothing,  Is.True);
            Assert.That(HTTPMethod.GET <  nothing,  Is.False);
            Assert.That(HTTPMethod.GET <= nothing,  Is.False);

            Assert.That(nothing <= (HTTPMethod?) null, Is.True);
            Assert.That(nothing >= (HTTPMethod?) null, Is.True);
            Assert.That(nothing <  (HTTPMethod?) null, Is.False);

            Assert.That(HTTPMethod.GET.CompareTo(null), Is.EqualTo(1));

        }

        #endregion

        #region WellKnownMethods_CarryTheirMetadata()

        /// <summary>
        /// The well-known methods should know whether they are safe and/or idempotent.
        /// </summary>
        [Test]
        public void WellKnownMethods_CarryTheirMetadata()
        {

            Assert.That(HTTPMethod.GET.    IsSafe,       Is.True);
            Assert.That(HTTPMethod.GET.    IsIdempotent, Is.True);
            Assert.That(HTTPMethod.HEAD.   IsSafe,       Is.True);
            Assert.That(HTTPMethod.OPTIONS.IsSafe,       Is.True);

            Assert.That(HTTPMethod.POST.   IsSafe,       Is.False);
            Assert.That(HTTPMethod.POST.   IsIdempotent, Is.False);

            Assert.That(HTTPMethod.PUT.    IsSafe,       Is.False);
            Assert.That(HTTPMethod.PUT.    IsIdempotent, Is.True);
            Assert.That(HTTPMethod.DELETE. IsIdempotent, Is.True);

            Assert.That(HTTPMethod.GET.    ToString(),   Is.EqualTo("GET"));

        }

        #endregion

    }

}
