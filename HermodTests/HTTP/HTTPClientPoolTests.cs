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
    /// HTTP client pool tests.
    /// </summary>
    [TestFixture]
    public class HTTPClientPoolTests
    {

        #region CreateRequest_AsksTheURLWhichPortBelongsIntoTheHostHeader()

        /// <summary>
        /// The Host header names the authority the request is addressed to, and the port
        /// is part of that authority unless it is the default port of the scheme.
        ///
        /// CreateRequest used to build that text itself and asked whether the port was 80
        /// or 443 rather than whether it was the scheme's default, so it dropped the port
        /// from http://...:443/ and https://...:80/ -- a request to one port announcing
        /// itself as another, which is exactly what a name-based virtual host or a reverse
        /// proxy routes on.
        /// </summary>
        [Test]
        public void CreateRequest_AsksTheURLWhichPortBelongsIntoTheHostHeader()
        {

            static String HostHeaderOf(String URLText)
            {
                var pool = new HTTPClientPool(URL.Parse(URLText));
                return pool.CreateRequest(HTTPMethod.GET, HTTPPath.Root).Host.ToString();
            }

            // The default port of the scheme is left out...
            Assert.That(HostHeaderOf("http://example.org/"),       Is.EqualTo("example.org"));
            Assert.That(HostHeaderOf("http://example.org:80/"),    Is.EqualTo("example.org"));
            Assert.That(HostHeaderOf("https://example.org/"),      Is.EqualTo("example.org"));
            Assert.That(HostHeaderOf("https://example.org:443/"),  Is.EqualTo("example.org"));

            // ...every other port is not.
            Assert.That(HostHeaderOf("http://example.org:8080/"),  Is.EqualTo("example.org:8080"));
            Assert.That(HostHeaderOf("https://example.org:8443/"), Is.EqualTo("example.org:8443"));

            // The default port of the *other* scheme is an ordinary port here.
            Assert.That(HostHeaderOf("http://example.org:443/"),   Is.EqualTo("example.org:443"));
            Assert.That(HostHeaderOf("https://example.org:80/"),   Is.EqualTo("example.org:80"));

            // ...and it agrees with the URL itself, which is where it now comes from.
            Assert.That(HostHeaderOf("http://example.org:443/"),
                        Is.EqualTo(URL.Parse("http://example.org:443/").HostHeader.ToString()));

        }

        #endregion

        #region CreateRequest_BracketsAnIPv6Authority()

        /// <summary>
        /// An IPv6 address has to reach the Host header in brackets, or its colons read as
        /// the host/port separator.
        /// </summary>
        [Test]
        public void CreateRequest_BracketsAnIPv6Authority()
        {

            var pool = new HTTPClientPool(URL.Parse("http://[2606:2800:220:1:248:1893:25c8:1946]:8080/"));
            var host = pool.CreateRequest(HTTPMethod.GET, HTTPPath.Root).Host;

            Assert.That(host.ToString(), Does.StartWith("[2606:2800:"));
            Assert.That(host.Port,       Is.EqualTo(IPPort.Parse(8080)));

        }

        #endregion

    }

}
