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
    /// URLScheme tests.
    ///
    /// Note: The URL scheme registry is static, therefore these tests must not run in
    ///       parallel with anything else touching it.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class URLSchemeTests
    {

        #region TryParse_DoesNotGrowTheRegistry()

        /// <summary>
        /// Parse(...)/TryParse(...) must never add to the registry, otherwise a remote peer
        /// could grow it without any bound, simply by sending a new scheme on every request.
        /// </summary>
        [Test]
        public void TryParse_DoesNotGrowTheRegistry()
        {

            var before = URIScheme.RegisteredCount;

            Assert.That(URIScheme.TryParse("scheme-from-the-wire-1", out var scheme1), Is.True);
            Assert.That(URIScheme.TryParse("scheme-from-the-wire-2", out _),           Is.True);
            Assert.That(URIScheme.Parse   ("scheme-from-the-wire-3"),                  Is.Not.Null);

            Assert.That(URIScheme.RegisteredCount, Is.EqualTo(before));

            // An unknown scheme is still parsed successfully...
            Assert.That(scheme1!.SchemeName,       Is.EqualTo("scheme-from-the-wire-1"));

            // ...and is fully interchangeable with a registered one.
            Assert.That(URIScheme.TryParse("scheme-from-the-wire-1", out var again), Is.True);
            Assert.That(again,                     Is.EqualTo(scheme1));
            Assert.That(again!.GetHashCode(),      Is.EqualTo(scheme1.GetHashCode()));

        }

        #endregion

        #region Register_GrowsTheRegistry()

        /// <summary>
        /// Register(...) is the only way to grow the registry.
        /// </summary>
        [Test]
        public void Register_GrowsTheRegistry()
        {

            var before   = URIScheme.RegisteredCount;
            var declared = URIScheme.Register("urlschemetests-declared",
                                              EnforcesTLS:  true,
                                              DefaultPort:  IPPort.Parse(9999),
                                              Description:  "A scheme declared by the unit tests");

            Assert.That(URIScheme.RegisteredCount, Is.EqualTo(before + 1));
            Assert.That(declared.SchemeName,       Is.EqualTo("urlschemetests-declared"));
            Assert.That(declared.EnforcesTLS,      Is.True);
            Assert.That(declared.DefaultPort,      Is.EqualTo(IPPort.Parse(9999)));
            Assert.That(declared.Prefix,           Is.EqualTo("urlschemetests-declared://"));

            // Registering the very same scheme again returns the already registered one.
            Assert.That(URIScheme.Register("urlschemetests-declared"), Is.SameAs(declared));
            Assert.That(URIScheme.RegisteredCount, Is.EqualTo(before + 1));

            // ...and now Parse(...) finds it.
            Assert.That(URIScheme.Parse("urlschemetests-declared"), Is.SameAs(declared));

        }

        #endregion

        #region SchemeNames_AreValidatedAgainstRFC3986()

        /// <summary>
        /// RFC 3986 section 3.1: scheme = ALPHA *( ALPHA / DIGIT / "+" / "-" / "." )
        /// </summary>
        [Test]
        public void SchemeNames_AreValidatedAgainstRFC3986()
        {

            Assert.That(URIScheme.TryParse("http",      out _), Is.True);
            Assert.That(URIScheme.TryParse("a+b-c.d1",  out _), Is.True);

            Assert.That(URIScheme.TryParse(null,        out _), Is.False);
            Assert.That(URIScheme.TryParse("",          out _), Is.False);
            Assert.That(URIScheme.TryParse("1http",     out _), Is.False);   // must start with ALPHA
            Assert.That(URIScheme.TryParse("ht tp",     out _), Is.False);
            Assert.That(URIScheme.TryParse("ht/tp",     out _), Is.False);
            Assert.That(URIScheme.TryParse("../..",     out _), Is.False);

            Assert.Throws<ArgumentException>(() => URIScheme.Parse   ("1bad"));
            Assert.Throws<ArgumentException>(() => URIScheme.Register("1bad"));

        }

        #endregion

        #region SchemeNames_AreCaseInsensitiveAndNormalized()

        /// <summary>
        /// URL schemes are case-insensitive and normalized to lower case,
        /// see RFC 3986 section 3.1.
        /// </summary>
        [Test]
        public void SchemeNames_AreCaseInsensitiveAndNormalized()
        {

            Assert.That(URIScheme.Parse("HTTPS"),            Is.EqualTo(URIScheme.https));
            Assert.That(URIScheme.Parse("HtTpS").SchemeName, Is.EqualTo("https"));

            // Also for an unknown, unregistered scheme.
            Assert.That(URIScheme.Parse("UNKNOWNSCHEME").SchemeName, Is.EqualTo("unknownscheme"));

        }

        #endregion

        #region Comparison_TreatsNullConsistently()

        /// <summary>
        /// CompareTo(null) returns 1, so null sorts before any URL scheme. All comparison
        /// operators must agree with that instead of throwing.
        /// </summary>
        [Test]
        public void Comparison_TreatsNullConsistently()
        {

            URIScheme? nothing = null;

            Assert.That(nothing <  URIScheme.http,  Is.True);
            Assert.That(nothing <= URIScheme.http,  Is.True);
            Assert.That(nothing >  URIScheme.http,  Is.False);
            Assert.That(nothing >= URIScheme.http,  Is.False);

            Assert.That(URIScheme.http >  nothing,  Is.True);
            Assert.That(URIScheme.http >= nothing,  Is.True);
            Assert.That(URIScheme.http <  nothing,  Is.False);
            Assert.That(URIScheme.http <= nothing,  Is.False);

            Assert.That(nothing <= (URIScheme?) null, Is.True);
            Assert.That(nothing >= (URIScheme?) null, Is.True);
            Assert.That(nothing <  (URIScheme?) null, Is.False);

            Assert.That(URIScheme.http.CompareTo(null), Is.EqualTo(1));

        }

        #endregion

        #region WellKnownSchemes_CarryTheirMetadata()

        /// <summary>
        /// The well-known schemes should know whether they enforce TLS and their default port.
        /// </summary>
        [Test]
        public void WellKnownSchemes_CarryTheirMetadata()
        {

            Assert.That(URIScheme.http.   EnforcesTLS, Is.False);
            Assert.That(URIScheme.https.  EnforcesTLS, Is.True);
            Assert.That(URIScheme.ws.     EnforcesTLS, Is.False);
            Assert.That(URIScheme.wss.    EnforcesTLS, Is.True);
            Assert.That(URIScheme.smodbus.EnforcesTLS, Is.True);

            Assert.That(URIScheme.http.   DefaultPort, Is.EqualTo(IPPort.HTTP));
            Assert.That(URIScheme.https.  DefaultPort, Is.EqualTo(IPPort.HTTPS));
            Assert.That(URIScheme.modbus. DefaultPort, Is.EqualTo(IPPort.ModbusTCP));
            Assert.That(URIScheme.smodbus.DefaultPort, Is.EqualTo(IPPort.ModbusTLS));

            Assert.That(URIScheme.tcp.    DefaultPort, Is.Null);

            Assert.That(URIScheme.https.  Prefix,      Is.EqualTo("https://"));
            Assert.That(URIScheme.https.  ToString(),  Is.EqualTo("https"));

        }

        #endregion

    }

}
