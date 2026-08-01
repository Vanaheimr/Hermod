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
    /// URL tests.
    /// </summary>
    [TestFixture]
    public class URLTests
    {

        #region ParseIPv6_WithAndWithoutPort()

        /// <summary>
        /// URL should parse IPv6 literals, with and without an explicit port.
        /// The variant without a port used to fail, as the ':' of the port was
        /// searched for with LastIndexOf(...) and thus found inside the brackets.
        /// </summary>
        [Test]
        public void ParseIPv6_WithAndWithoutPort()
        {

            Assert.That(URL.TryParse("http://[::1]/",       out var withoutPort), Is.True);
            Assert.That(withoutPort.Host.ToString(),  Is.EqualTo("[::1]"));
            Assert.That(withoutPort.Host.IsIPv6,      Is.True);
            Assert.That(withoutPort.Port,             Is.EqualTo(IPPort.HTTP));
            Assert.That(withoutPort.Path.ToString(),  Is.EqualTo("/"));

            Assert.That(URL.TryParse("http://[::1]:8080/x", out var withPort),    Is.True);
            Assert.That(withPort.   Host.ToString(),  Is.EqualTo("[::1]"));
            Assert.That(withPort.   Host.IsIPv6,      Is.True);
            Assert.That(withPort.   Port,             Is.EqualTo(IPPort.Parse(8080)));
            Assert.That(withPort.   Path.ToString(),  Is.EqualTo("/x"));

        }

        #endregion

        #region ParseIPv6_RejectsMalformedLiterals()

        /// <summary>
        /// URL should reject malformed IPv6 literals.
        /// </summary>
        [Test]
        public void ParseIPv6_RejectsMalformedLiterals()
        {

            Assert.That(URL.TryParse("http://[::1/x",    out _), Is.False);
            Assert.That(URL.TryParse("http://[gggg::]/", out _), Is.False);

        }

        #endregion

        #region ParseQueryString_ASlashInTheQueryStaysOutOfThePath()

        /// <summary>
        /// The query string is cut off before the path is determined, therefore a '/'
        /// within the query can no longer end up within the path.
        /// </summary>
        [Test]
        public void ParseQueryString_ASlashInTheQueryStaysOutOfThePath()
        {

            var url = URL.Parse("https://example.org/p?redirect=/foo/bar");

            Assert.That(url.Path.ToString(),        Is.EqualTo("/p"));
            Assert.That(url.QueryString,            Is.Not.Null);
            Assert.That(url.QueryString!.ToString(), Does.Contain("redirect"));

        }

        #endregion

        #region ParseFragment()

        /// <summary>
        /// URL should parse the fragment and keep it out of the path and the query string.
        /// </summary>
        [Test]
        public void ParseFragment()
        {

            var withQuery = URL.Parse("https://example.org/p?a=1#frag");

            Assert.That(withQuery.Path.ToString(),  Is.EqualTo("/p"));
            Assert.That(withQuery.QueryString?.ToString(), Is.EqualTo("?a=1"));
            Assert.That(withQuery.Fragment,         Is.EqualTo("frag"));

            var withoutQuery = URL.Parse("https://example.org/a/b#only-fragment");

            Assert.That(withoutQuery.Path.ToString(), Is.EqualTo("/a/b"));
            Assert.That(withoutQuery.QueryString,     Is.Null);
            Assert.That(withoutQuery.Fragment,        Is.EqualTo("only-fragment"));

        }

        #endregion

        #region ParseScheme_IsCaseInsensitiveAndPreservesUnknownSchemes()

        /// <summary>
        /// URL schemes are case-insensitive, see RFC 3986 section 3.1. An unknown scheme
        /// must not silently be turned into https.
        /// </summary>
        [Test]
        public void ParseScheme_IsCaseInsensitiveAndPreservesUnknownSchemes()
        {

            Assert.That(URL.Parse("HTTP://example.org/").Scheme, Is.EqualTo(URIScheme.http));
            Assert.That(URL.Parse("ftp://example.org/"). Scheme?.SchemeName, Is.EqualTo("ftp"));

            // Without any scheme https is assumed.
            Assert.That(URL.Parse("example.org").Scheme, Is.EqualTo(URIScheme.https));

            Assert.That(URL.TryParse("ht tp://example.org/", out _), Is.False);

        }

        #endregion

        #region ParseScheme_DefaultPortsComeFromTheRegistry()

        /// <summary>
        /// The default port is taken from the URL scheme registry, therefore every
        /// registered scheme knowing a default port gets one.
        /// </summary>
        [Test]
        public void ParseScheme_DefaultPortsComeFromTheRegistry()
        {

            Assert.That(URL.Parse("http://h/").   Port, Is.EqualTo(IPPort.HTTP));
            Assert.That(URL.Parse("https://h/").  Port, Is.EqualTo(IPPort.HTTPS));
            Assert.That(URL.Parse("ws://h/").     Port, Is.EqualTo(IPPort.HTTP));
            Assert.That(URL.Parse("wss://h/").    Port, Is.EqualTo(IPPort.HTTPS));
            Assert.That(URL.Parse("modbus://h/"). Port, Is.EqualTo(IPPort.ModbusTCP));
            Assert.That(URL.Parse("smodbus://h/").Port, Is.EqualTo(IPPort.ModbusTLS));

        }

        #endregion

        #region ParseUserInformation()

        /// <summary>
        /// URL should parse the optional login and password.
        /// </summary>
        [Test]
        public void ParseUserInformation()
        {

            var withPassword = URL.Parse("https://user:pw@example.org:8443/a");

            Assert.That(withPassword.Login,             Is.EqualTo("user"));
            Assert.That(withPassword.Password,          Is.EqualTo("pw"));
            Assert.That(withPassword.Host.ToString(),   Is.EqualTo("example.org"));
            Assert.That(withPassword.Host.IsDomainName, Is.True);
            Assert.That(withPassword.Port,              Is.EqualTo(IPPort.Parse(8443)));

            var withoutPassword = URL.Parse("https://user@example.org/a");

            Assert.That(withoutPassword.Login,          Is.EqualTo("user"));
            Assert.That(withoutPassword.Password,       Is.Null);

        }

        #endregion

        #region Clone_KeepsAllComponents()

        /// <summary>
        /// Clone() must not lose the login, the password, the query string or the fragment.
        /// </summary>
        [Test]
        public void Clone_KeepsAllComponents()
        {

            var url    = URL.Parse("https://user:pw@example.org:8443/a?x=1#f");
            var cloned = url.Clone();

            Assert.That(cloned.Scheme,                 Is.EqualTo(url.Scheme));
            Assert.That(cloned.Login,                    Is.EqualTo("user"));
            Assert.That(cloned.Password,                 Is.EqualTo("pw"));
            Assert.That(cloned.Host.ToString(),          Is.EqualTo("example.org"));
            Assert.That(cloned.Port,                     Is.EqualTo(IPPort.Parse(8443)));
            Assert.That(cloned.Path.ToString(),          Is.EqualTo("/a"));
            Assert.That(cloned.QueryString?.ToString(),  Is.EqualTo("?x=1"));
            Assert.That(cloned.Fragment,                 Is.EqualTo("f"));
            Assert.That(cloned,                          Is.EqualTo(url));

        }

        #endregion

        #region Clone_CopiesTheMutableQueryString()

        /// <summary>
        /// QueryString is mutable, as Add(...) modifies the instance and returns it.
        /// A clone therefore must not share the very same instance.
        /// </summary>
        [Test]
        public void Clone_CopiesTheMutableQueryString()
        {

            var url    = URL.Parse("https://example.org/a?x=1");
            var cloned = url.Clone();

            Assert.That(cloned.QueryString,              Is.Not.Null);
            Assert.That(ReferenceEquals(cloned.QueryString, url.QueryString), Is.False);

            cloned.QueryString!.Add("y", "2");

            Assert.That(url.QueryString?.ToString(),     Is.EqualTo("?x=1"));

        }

        #endregion

        #region OperatorPlus_KeepsAllComponents()

        /// <summary>
        /// Appending a path suffix must not lose the login, the password or the fragment.
        /// </summary>
        [Test]
        public void OperatorPlus_KeepsAllComponents()
        {

            var url = URL.Parse("https://user:pw@example.org/a") + "b";

            Assert.That(url.Login,              Is.EqualTo("user"));
            Assert.That(url.Password,           Is.EqualTo("pw"));
            Assert.That(url.Path.ToString(),    Is.EqualTo("/a/b"));
            Assert.That(url.ToString(),         Is.EqualTo("https://user:pw@example.org/a/b"));

        }

        #endregion

        #region OperatorPlus_DoesNotProduceDoubleSlashes()

        /// <summary>
        /// Appending a path suffix to an URL already ending with a '/' must not
        /// produce a double slash.
        /// </summary>
        [Test]
        public void OperatorPlus_DoesNotProduceDoubleSlashes()
        {

            Assert.That((URL.Parse("https://example.org/")  + "foo").              ToString(), Is.EqualTo("https://example.org/foo"));
            Assert.That((URL.Parse("https://example.org/")  + HTTPPath.Parse("/foo")).ToString(), Is.EqualTo("https://example.org/foo"));
            Assert.That((URL.Parse("https://example.org/a") + "/b").               ToString(), Is.EqualTo("https://example.org/a/b"));

        }

        #endregion

        #region OperatorPlus_QueryStringSuffixDoesNotEndUpInThePath()

        /// <summary>
        /// A suffix starting with '?' is a query string and must not be appended to the path.
        /// </summary>
        [Test]
        public void OperatorPlus_QueryStringSuffixDoesNotEndUpInThePath()
        {

            var url = URL.Parse("https://example.org/p") + "?a=1";

            Assert.That(url.Path.ToString(),            Is.EqualTo("/p"));
            Assert.That(url.QueryString?.ToString(),    Is.EqualTo("?a=1"));

        }

        #endregion

        #region OperatorPlus_MergesAnExistingQueryString()

        /// <summary>
        /// When the URL already has a query string, both are merged instead of being
        /// concatenated into an invalid "?a=1?b=2".
        /// </summary>
        [Test]
        public void OperatorPlus_MergesAnExistingQueryString()
        {

            var url = URL.Parse("https://example.org/p?a=1") + "?b=2";

            Assert.That(url.QueryString?.ToString(),    Is.EqualTo("?a=1&b=2"));

        }

        #endregion

        #region Equality_SchemeAndHostAreCaseInsensitive()

        /// <summary>
        /// Scheme and host are case-insensitive, see RFC 3986 section 6.2.2.1.
        /// </summary>
        [Test]
        public void Equality_SchemeAndHostAreCaseInsensitive()
        {

            Assert.That(URL.Parse("https://EXAMPLE.org/Path"), Is.EqualTo(URL.Parse("https://example.org/Path")));
            Assert.That(URL.Parse("HTTPS://example.org/Path"), Is.EqualTo(URL.Parse("https://example.org/Path")));

            Assert.That(new HashSet<URL> {
                            URL.Parse("https://EXAMPLE.org/p"),
                            URL.Parse("https://example.org/p")
                        }.Count,
                        Is.EqualTo(1));

        }

        #endregion

        #region Equality_PathQueryFragmentAndUserInfoAreCaseSensitive()

        /// <summary>
        /// Everything but the scheme and the host is case-sensitive,
        /// see RFC 3986 section 6.2.2.1.
        /// </summary>
        [Test]
        public void Equality_PathQueryFragmentAndUserInfoAreCaseSensitive()
        {

            Assert.That(URL.Parse("https://h/Foo"),      Is.Not.EqualTo(URL.Parse("https://h/foo")));
            Assert.That(URL.Parse("https://h/p?a=X"),    Is.Not.EqualTo(URL.Parse("https://h/p?a=x")));
            Assert.That(URL.Parse("https://h/p#F"),      Is.Not.EqualTo(URL.Parse("https://h/p#f")));
            Assert.That(URL.Parse("https://User@h/p"),   Is.Not.EqualTo(URL.Parse("https://user@h/p")));

            Assert.That(new HashSet<URL> {
                            URL.Parse("https://h/Foo"),
                            URL.Parse("https://h/foo")
                        }.Count,
                        Is.EqualTo(2));

        }

        #endregion

        #region Equality_AgreesWithCompareToAndGetHashCode()

        /// <summary>
        /// Equals(...), CompareTo(...) and GetHashCode() must all use the very same
        /// component comparisons, otherwise hash based collections misbehave.
        /// </summary>
        [Test]
        public void Equality_AgreesWithCompareToAndGetHashCode()
        {

            var a = URL.Parse("https://EXAMPLE.org/Path?q=V#F");
            var b = URL.Parse("https://example.org/Path?q=V#F");

            Assert.That(a.Equals(b),           Is.True);
            Assert.That(a.CompareTo(b),        Is.EqualTo(0));
            Assert.That(a.GetHashCode(),       Is.EqualTo(b.GetHashCode()));

            var c = URL.Parse("https://example.org/path?q=V#F");

            Assert.That(a.Equals(c),           Is.False);
            Assert.That(a.CompareTo(c),        Is.Not.EqualTo(0));

        }

        #endregion

        #region Equality_AnOmittedDefaultPortEqualsTheExplicitOne()

        /// <summary>
        /// As the default port of the scheme is filled in while parsing, an URL with an
        /// omitted port equals the very same URL with its default port spelled out.
        /// </summary>
        [Test]
        public void Equality_AnOmittedDefaultPortEqualsTheExplicitOne()
        {

            Assert.That(URL.Parse("https://example.org/"), Is.EqualTo(URL.Parse("https://example.org:443/")));
            Assert.That(URL.Parse("http://example.org/"),  Is.EqualTo(URL.Parse("http://example.org:80/")));

            Assert.That(URL.Parse("https://example.org/"), Is.Not.EqualTo(URL.Parse("https://example.org:8443/")));

        }

        #endregion

        #region HostHeader_OmitsTheDefaultPortAndKeepsEveryOther()

        /// <summary>
        /// RFC 9110 section 7.2 defines the 'Host' header as "uri-host [ ':' port ]", and
        /// RFC 3986 section 3.2.3 says the port is omitted when it is the scheme's default.
        /// Before this existed, the port was dropped unconditionally, so a request against
        /// https://host:8443/ announced "Host: host" and could hit the wrong vHost.
        /// </summary>
        [Test]
        public void HostHeader_OmitsTheDefaultPortAndKeepsEveryOther()
        {

            // Default ports are omitted...
            Assert.That(URL.Parse("https://example.org/").   HostHeader.ToString(), Is.EqualTo("example.org"));
            Assert.That(URL.Parse("https://example.org:443/").HostHeader.ToString(), Is.EqualTo("example.org"));
            Assert.That(URL.Parse("http://example.org/").    HostHeader.ToString(), Is.EqualTo("example.org"));
            Assert.That(URL.Parse("http://example.org:80/"). HostHeader.ToString(), Is.EqualTo("example.org"));

            // ...every other port is kept.
            Assert.That(URL.Parse("https://example.org:8443/").HostHeader.ToString(), Is.EqualTo("example.org:8443"));
            Assert.That(URL.Parse("http://example.org:8080/"). HostHeader.ToString(), Is.EqualTo("example.org:8080"));

            // Also for IPv6 literals, which keep their brackets.
            Assert.That(URL.Parse("http://[::1]/").          HostHeader.ToString(), Is.EqualTo("[::1]"));
            Assert.That(URL.Parse("http://[::1]:8080/").     HostHeader.ToString(), Is.EqualTo("[::1]:8080"));

            // A scheme without a default port always keeps its port.
            Assert.That(URL.Parse("tcp://example.org:1234/").HostHeader.ToString(), Is.EqualTo("example.org:1234"));

        }

        #endregion

        #region Host_ExposesTheAlreadyParsedDomainNameOrIPAddress()

        /// <summary>
        /// The host is parsed once, so consumers must not have to re-parse its text.
        /// </summary>
        [Test]
        public void Host_ExposesTheAlreadyParsedDomainNameOrIPAddress()
        {

            var byName = URL.Parse("https://www.example.org/");

            Assert.That(byName.Host.IsDomainName,        Is.True);
            Assert.That(byName.Host.DomainName,          Is.Not.Null);
            Assert.That(byName.Host.DomainName!.FullName, Is.EqualTo("www.example.org."));
            Assert.That(byName.Host.IPAddress,           Is.Null);

            var byIPv4 = URL.Parse("https://192.168.1.1/");

            Assert.That(byIPv4.Host.IsIPAddress,         Is.True);
            Assert.That(byIPv4.Host.IsIPv4,              Is.True);
            Assert.That(byIPv4.Host.DomainName,          Is.Null);

            var byIPv6 = URL.Parse("https://[::1]/");

            Assert.That(byIPv6.Host.IsIPAddress,         Is.True);
            Assert.That(byIPv6.Host.IsIPv6,              Is.True);
            Assert.That(byIPv6.Host.DomainName,          Is.Null);

        }

        #endregion

        #region Default_ProtocolDoesNotThrow()

        /// <summary>
        /// A struct can not prevent its own default value from being created. As URLScheme
        /// is a class, URL.Protocol would be null for default(URL) and every access would
        /// throw. It falls back to https, which is also the fail-safe direction for TLS.
        /// </summary>
        [Test]
        public void Default_HasNoSchemeInsteadOfAnInventedOne()
        {

            var url = default(URL);

            Assert.That(url.IsNullOrEmpty,        Is.True);
            Assert.That(url.ToString(),           Is.EqualTo(""));
            Assert.That(url.GetHashCode(),        Is.EqualTo(default(URL).GetHashCode()));

            // Null on purpose: default(URL) genuinely has no scheme, and only the caller
            // knows what that should mean for it. A non-nullable property papering over it
            // with https once turned every client whose RemoteURL was never assigned into
            // one demanding a TLS handshake on a plain connection - see AHTTPClient.
            Assert.That(url.Scheme,               Is.Null);

            // ...and the caller decides, here: do not enforce TLS.
            Assert.That(url.Scheme?.EnforcesTLS == true, Is.False);

        }

        #endregion

        #region TryParse_RejectsNullAndEmptyInput()

        /// <summary>
        /// TryParse(...) must never throw, not even for null or empty input.
        /// </summary>
        [Test]
        public void TryParse_RejectsNullAndEmptyInput()
        {

            Assert.That(URL.TryParse(null!, out _), Is.False);
            Assert.That(URL.TryParse("",    out _), Is.False);
            Assert.That(URL.TryParse("   ", out _), Is.False);
            Assert.That(URL.TryParse("://", out _), Is.False);

            Assert.Throws<ArgumentException>(() => URL.Parse("://"));

        }

        #endregion

    }

}
