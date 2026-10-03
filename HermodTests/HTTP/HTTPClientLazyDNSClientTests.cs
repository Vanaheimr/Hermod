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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// A client makes the DNS client it was handed none of when it is first
    /// asked for one, and not in its constructor.
    ///
    /// The default DNS client searches the machine's network configuration for
    /// resolvers - two sweeps of every network interface - and that was 38.3 ms
    /// of the 39.4 ms a fresh client per request cost, for a request taking
    /// 1.06 ms. The same loop with one shared DNS client took 0.449 ms, which
    /// is what named the constructor rather than the connection as the cost.
    ///
    /// Dialling a literal IP address resolves nothing at all, so for those
    /// clients the work was not merely early but never needed. Measured by
    /// `tests/h1bench -- connect` in HTTP1ConformanceTests, where this is H-27.
    /// </summary>
    /// <remarks>
    /// Observed through the logger factory rather than through a clock: the
    /// default DNS client is made with a logger of its own, and asking the
    /// factory for one is the only visible trace of its construction.
    /// ATCPClient asks for an IDNSClient logger at exactly one place, which is
    /// the line these tests are about.
    /// </remarks>
    [TestFixture]
    public class HTTPClientLazyDNSClientTests
    {

        #region (class) CountingLoggerFactory

        /// <summary>
        /// A logger factory that counts how often a logger for an IDNSClient
        /// was asked for, and so how many default DNS clients were made.
        /// </summary>
        private sealed class CountingLoggerFactory : ILoggerFactory
        {

            private Int32 dnsLoggers;

            public Int32 DNSClientsMade
                => Volatile.Read(ref dnsLoggers);

            public ILogger CreateLogger(String CategoryName)
            {

                if (CategoryName.Contains(nameof(IDNSClient)))
                    Interlocked.Increment(ref dnsLoggers);

                return NullLogger.Instance;

            }

            public void AddProvider(ILoggerProvider Provider) { }

            public void Dispose() { }

        }

        #endregion

        #region Data

        private HTTPServer?  httpServer;
        private HTTPAPI?     httpAPI;

        private URL URL
            => URL.Parse($"http://127.0.0.1:{httpServer!.TCPPort}");

        #endregion

        #region Setup / Teardown

        [OneTimeSetUp]
        public void Init()
        {

            httpServer = new HTTPServer(
                             TCPPort:    IPPort.Zero,
                             AutoStart:  true
                         );

            httpAPI    = new HTTPAPI(httpServer);

            httpAPI.AddHandler(HTTPPath.Root + "ping",
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.OK,
                                                      ContentType     = HTTPContentType.Text.PLAIN,
                                                      Content         = "pong".ToUTF8Bytes(),
                                                      Connection      = ConnectionType.Close
                                                  }.AsImmutable));

        }

        [OneTimeTearDown]
        public async Task Shutdown()
        {
            if (httpServer is not null)
                await httpServer.DisposeAsync();
        }

        #endregion


        #region ANewClientHasMadeNoDNSClientYet()

        [Test]
        public void ANewClientHasMadeNoDNSClientYet()
        {

            var loggerFactory  = new CountingLoggerFactory();

            using var client   = new HTTPClient(
                                     URL.Parse("http://127.0.0.1:9"),
                                     LoggerFactory:  loggerFactory
                                 );

            Assert.That(
                loggerFactory.DNSClientsMade,
                Is.Zero,
                "the constructor made a DNS client"
            );

        }

        #endregion

        #region AskingForTheDNSClientMakesExactlyOne()

        /// <summary>
        /// And every reader gets that one: a property making a new DNS client
        /// per call would hand out caches nobody shares and timers nobody
        /// stops.
        /// </summary>
        [Test]
        public void AskingForTheDNSClientMakesExactlyOne()
        {

            var loggerFactory  = new CountingLoggerFactory();

            using var client   = new HTTPClient(
                                     URL.Parse("http://127.0.0.1:9"),
                                     LoggerFactory:  loggerFactory
                                 );

            var first          = client.DNSClient;
            var second         = client.DNSClient;

            Assert.Multiple(() => {

                Assert.That(first,                          Is.Not.Null,       "no DNS client was handed out");
                Assert.That(second,                         Is.SameAs(first),  "a second reader was given a second DNS client");
                Assert.That(loggerFactory.DNSClientsMade,   Is.EqualTo(1),     "DNS clients made");

            });

        }

        #endregion

        #region DisposingOfAnUnusedClientMakesNoDNSClient()

        /// <summary>
        /// Disposing of a client disposes of the DNS client it made itself, and
        /// reading the property to find one would make the very client that
        /// line then throws away. The cost would move from the way in to the
        /// way out and still be paid in full.
        /// </summary>
        [Test]
        public async Task DisposingOfAnUnusedClientMakesNoDNSClient()
        {

            var loggerFactory  = new CountingLoggerFactory();

            var client         = new HTTPClient(
                                     URL.Parse("http://127.0.0.1:9"),
                                     LoggerFactory:  loggerFactory
                                 );

            await client.DisposeAsync();

            Assert.That(
                loggerFactory.DNSClientsMade,
                Is.Zero,
                "disposing of a client that never resolved anything made a DNS client"
            );

        }

        #endregion

        #region ALentDNSClientIsHandedBackAsItIs()

        [Test]
        public void ALentDNSClientIsHandedBackAsItIs()
        {

            var loggerFactory  = new CountingLoggerFactory();
            var dnsClient      = new FakeDNSClient();

            using var client   = new HTTPClient(
                                     URL.Parse("http://127.0.0.1:9"),
                                     DNSClient:      dnsClient,
                                     LoggerFactory:  loggerFactory
                                 );

            Assert.Multiple(() => {

                Assert.That(client.DNSClient,              Is.SameAs(dnsClient),  "the DNS client handed out was not the one lent");
                Assert.That(loggerFactory.DNSClientsMade,  Is.Zero,               "a DNS client was made although one was lent");

            });

        }

        #endregion

        #region AWholeRequestToALiteralAddressMakesNoDNSClient()

        /// <summary>
        /// The request, and not merely the construction: an URL that already
        /// carries an address is resolved by nobody, so a client dialling one
        /// must get all the way through without ever making a resolver.
        /// </summary>
        [Test]
        public async Task AWholeRequestToALiteralAddressMakesNoDNSClient()
        {

            var loggerFactory   = new CountingLoggerFactory();

            await using var client = new HTTPClient(
                                         URL,
                                         LoggerFactory:  loggerFactory
                                     );

            var response        = await client.RunRequest(
                                            HTTPMethod.GET,
                                            HTTPPath.Root + "ping",
                                            RequestTimeout:  TimeSpan.FromSeconds(10)
                                        );

            Assert.Multiple(() => {

                // First, because a request that never happened resolves no
                // names either, and would make the check below true for a
                // reason that has nothing to do with DNS.
                Assert.That(response.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.OK),  "the request did not get through");
                Assert.That(response.HTTPBodyAsUTF8String, Is.EqualTo("pong"),             "the request got through to something else");

                Assert.That(loggerFactory.DNSClientsMade,  Is.Zero,                        "a request to a literal address made a DNS client");

            });

        }

        #endregion

    }

}
