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

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// URLHost tests.
    /// </summary>
    [TestFixture]
    public class URLHostTests
    {

        #region Parse_RegisteredName()

        /// <summary>
        /// A reg-name should end up as a domain name, without the trailing dot
        /// of the DNS presentation format.
        /// </summary>
        [Test]
        public void Parse_RegisteredName()
        {

            var host = URLHost.Parse("www.example.org");

            Assert.That(host.IsDomainName,        Is.True);
            Assert.That(host.IsIPAddress,         Is.False);
            Assert.That(host.DomainName,          Is.Not.Null);
            Assert.That(host.DomainName!.FullName, Is.EqualTo("www.example.org."));
            Assert.That(host.ToString(),          Is.EqualTo("www.example.org"));

        }

        #endregion

        #region Parse_IPv4Address()

        /// <summary>
        /// An IPv4 literal must not be mistaken for a four-label domain name.
        /// </summary>
        [Test]
        public void Parse_IPv4Address()
        {

            var host = URLHost.Parse("192.168.1.1");

            Assert.That(host.IsIPAddress,   Is.True);
            Assert.That(host.IsIPv4,        Is.True);
            Assert.That(host.IsDomainName,  Is.False);
            Assert.That(host.ToString(),    Is.EqualTo("192.168.1.1"));

        }

        #endregion

        #region Parse_IPv6Address()

        /// <summary>
        /// An IPv6 literal must be enclosed in brackets and must round-trip that way,
        /// see RFC 3986 section 3.2.2.
        /// </summary>
        [Test]
        public void Parse_IPv6Address()
        {

            var host = URLHost.Parse("[::1]");

            Assert.That(host.IsIPAddress,   Is.True);
            Assert.That(host.IsIPv6,        Is.True);
            Assert.That(host.IsLocalhost,   Is.True);
            Assert.That(host.ToString(),    Is.EqualTo("[::1]"));

        }

        #endregion

        #region Parse_RejectsUnbracketedIPv6AndMalformedInput()

        /// <summary>
        /// An unbracketed IPv6 address is not a valid URL host.
        /// </summary>
        [Test]
        public void Parse_RejectsUnbracketedIPv6AndMalformedInput()
        {

            Assert.That(URLHost.TryParse("::1",       out _), Is.False);
            Assert.That(URLHost.TryParse("[::1",      out _), Is.False);
            Assert.That(URLHost.TryParse("[gggg::]",  out _), Is.False);
            Assert.That(URLHost.TryParse("",          out _), Is.False);
            Assert.That(URLHost.TryParse(null,        out _), Is.False);
            Assert.That(URLHost.TryParse("exa mple",  out _), Is.False);

            Assert.Throws<ArgumentException>(() => URLHost.Parse("::1"));

        }

        #endregion

        #region Equality_IsCaseInsensitive()

        /// <summary>
        /// Host names are case-insensitive, see RFC 3986 section 3.2.2.
        /// </summary>
        [Test]
        public void Equality_IsCaseInsensitive()
        {

            var upper = URLHost.Parse("WWW.EXAMPLE.ORG");
            var lower = URLHost.Parse("www.example.org");

            Assert.That(upper,               Is.EqualTo(lower));
            Assert.That(upper.GetHashCode(), Is.EqualTo(lower.GetHashCode()));
            Assert.That(upper.CompareTo(lower), Is.EqualTo(0));
            Assert.That(upper == lower,      Is.True);

            Assert.That(URLHost.Parse("a.example.org"), Is.Not.EqualTo(URLHost.Parse("b.example.org")));

        }

        #endregion

        #region Default_DoesNotThrow()

        /// <summary>
        /// A struct can not prevent its own default value from being created, therefore
        /// every member must cope with it.
        /// </summary>
        [Test]
        public void Default_DoesNotThrow()
        {

            var host = default(URLHost);

            Assert.That(host.IsNullOrEmpty,  Is.True);
            Assert.That(host.IsDomainName,   Is.False);
            Assert.That(host.IsIPAddress,    Is.False);
            Assert.That(host.IsIPv4,         Is.False);
            Assert.That(host.IsIPv6,         Is.False);
            Assert.That(host.IsLocalhost,    Is.False);
            Assert.That(host.ToString(),     Is.EqualTo(""));
            Assert.That(host.Length,         Is.EqualTo(0));
            Assert.That(host.GetHashCode(),  Is.EqualTo(default(URLHost).GetHashCode()));

        }

        #endregion

        #region ToHTTPHostname_AddsThePortWhenGiven()

        /// <summary>
        /// The HTTP 'Host' header is "uri-host [ ':' port ]", see RFC 9110 section 7.2.
        /// </summary>
        [Test]
        public void ToHTTPHostname_AddsThePortWhenGiven()
        {

            Assert.That(URLHost.Parse("example.org").ToHTTPHostname().             ToString(), Is.EqualTo("example.org"));
            Assert.That(URLHost.Parse("example.org").ToHTTPHostname(IPPort.Parse(8443)).ToString(), Is.EqualTo("example.org:8443"));
            Assert.That(URLHost.Parse("[::1]").      ToHTTPHostname(IPPort.Parse(8080)).ToString(), Is.EqualTo("[::1]:8080"));

        }

        #endregion

        #region From_DomainNameAndIPAddress()

        /// <summary>
        /// A URL host should also be constructible from an already parsed domain name
        /// or IP address, without going through its text representation.
        /// </summary>
        [Test]
        public void From_DomainNameAndIPAddress()
        {

            Assert.That(URLHost.From(DomainName.Parse("example.org")).   ToString(), Is.EqualTo("example.org"));
            Assert.That(URLHost.From(IPv4Address.Parse("10.0.0.1")).      ToString(), Is.EqualTo("10.0.0.1"));
            Assert.That(URLHost.Localhost.IsLocalhost,                               Is.True);

        }

        #endregion

        #region From_IPv6Address_IsBracketedAndSurvivesItsOwnParser()

        /// <summary>
        /// IPv6Address.ToString() brackets "::" and "::1" but spells every other address
        /// out bare, so URLHost.From(address).ToString() used to hand back a text its own
        /// parser rejects -- the bare colons read as the host/port separator. Everything
        /// built on that text representation threw for an ordinary global address while
        /// looking perfectly healthy for the loopback tested everywhere else.
        /// </summary>
        [Test]
        public void From_IPv6Address_IsBracketedAndSurvivesItsOwnParser()
        {

            var global = IPv6Address.Parse("2606:2800:220:1:248:1893:25c8:1946");
            var text   = URLHost.From(global).ToString();

            Assert.That(text, Does.StartWith("["));
            Assert.That(text, Does.EndWith("]"));

            // The loopback shortcuts were bracketed all along and must not gain a second pair.
            Assert.That(URLHost.From(IPv6Address.Localhost).ToString(), Is.EqualTo("[::1]"));
            Assert.That(URLHost.From(IPv6Address.Any).      ToString(), Is.EqualTo("[::]"));

            // IPv4 and domain names stay untouched.
            Assert.That(URLHost.From(IPv4Address.Parse("10.0.0.1")).      ToString(), Is.EqualTo("10.0.0.1"));
            Assert.That(URLHost.From(DomainName.Parse("example.org")).    ToString(), Is.EqualTo("example.org"));

            // ...and the text representation round-trips through the parser again.
            Assert.That(URLHost.Parse(text), Is.EqualTo(URLHost.From(global)));

        }

        #endregion

        #region ToHTTPHostname_AndFrom_AcceptAGlobalIPv6Address()

        /// <summary>
        /// Every way of turning an IPv6 address into a Host header goes through that same
        /// text representation, so all of them threw for a global address.
        /// </summary>
        [Test]
        public void ToHTTPHostname_AndFrom_AcceptAGlobalIPv6Address()
        {

            var global   = IPv6Address.Parse("2606:2800:220:1:248:1893:25c8:1946");
            var host     = URLHost.From(global);
            var port     = IPPort.Parse(8080);
            var expected = $"{host}:{port}";

            Assert.That(host.ToHTTPHostname(port).       ToString(), Is.EqualTo(expected));
            Assert.That(HTTPHostname.From(host,   port). ToString(), Is.EqualTo(expected));
            Assert.That(HTTPHostname.From(global, port). ToString(), Is.EqualTo(expected));
            Assert.That(HTTPHostname.From(global, null). ToString(), Is.EqualTo(host.ToString()));

            // Without a port the address stands on its own.
            Assert.That(host.ToHTTPHostname().ToString(), Is.EqualTo(host.ToString()));

        }

        #endregion

    }

}
