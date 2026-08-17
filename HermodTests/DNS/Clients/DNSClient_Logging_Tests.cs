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

using System.Net;
using System.Net.Sockets;

using Microsoft.Extensions.Logging;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Clients
{

    [TestFixture]
    public class DNSClient_Logging_Tests
    {

        #region Data

        private sealed record LogEntry(String Category, LogLevel LogLevel, String Message, Exception? Exception);

        private sealed class TestLoggerFactory : ILoggerFactory
        {

            private readonly Lock entryLock = new();

            public List<LogEntry> Entries { get; } = [];

            public ILogger CreateLogger(String categoryName)

                => new TestLogger(categoryName, Entries, entryLock);

            public void AddProvider(ILoggerProvider provider)
            { }

            public void Dispose()
            { }

        }

        private sealed class TestLogger(String Category,
                                        List<LogEntry> Entries,
                                        Lock EntryLock) : ILogger
        {

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull

                => NullScope.Instance;

            public Boolean IsEnabled(LogLevel logLevel)

                => true;

            public void Log<TState>(LogLevel                         logLevel,
                                    EventId                          eventId,
                                    TState                           state,
                                    Exception?                       exception,
                                    Func<TState, Exception?, String> formatter)
            {
                lock (EntryLock)
                    Entries.Add(new LogEntry(Category, logLevel, formatter(state, exception), exception));
            }

            private sealed class NullScope : IDisposable
            {

                public static readonly NullScope Instance = new();

                public void Dispose()
                { }

            }

        }

        #endregion

        #region (private static) CreateSilentUDPServer(out UDPPort)

        private static UdpClient CreateSilentUDPServer(out IPPort UDPPort)
        {

            var udpClient  = new UdpClient(
                                 new IPEndPoint(
                                     System.Net.IPAddress.Loopback,
                                     0
                                 )
                             );

            UDPPort        = IPPort.Parse(
                                 ((IPEndPoint) udpClient.Client.LocalEndPoint!).Port
                             );

            return udpClient;

        }

        #endregion

        #region Query_Logs_Orchestrator_And_Transport_Events()

        [Test]
        public async Task Query_Logs_Orchestrator_And_Transport_Events()
        {

            using var silentServer  = CreateSilentUDPServer(out var port);
            using var loggerFactory = new TestLoggerFactory();
            using var client        = new DNSClient(
                                          IPv4Address.Localhost,
                                          Port:           port,
                                          QueryTimeout:   TimeSpan.FromSeconds(5),
                                          UseQueryCache:  false,
                                          LoggerFactory:  loggerFactory
                                      );

            var response = await client.Query<A>(
                               DomainName.Parse("timeout.example"),
                               Timeout:      TimeSpan.FromMilliseconds(75),
                               ForceUpdate:  true
                           );

            Assert.That(response.IsTimeout, Is.True);

            Assert.That(
                loggerFactory.Entries.Any(entry =>
                    entry.LogLevel == LogLevel.Trace &&
                    entry.Message.Contains("Dispatching DNS query", StringComparison.Ordinal)
                ),
                Is.True
            );

            Assert.That(
                loggerFactory.Entries.Any(entry =>
                    entry.LogLevel == LogLevel.Warning &&
                    entry.Message.Contains("DNS UDP query", StringComparison.Ordinal) &&
                    entry.Message.Contains("timed out", StringComparison.Ordinal)
                ),
                Is.True
            );

        }

        #endregion

        #region ADoHClientBuiltFromAURL_IsNamedByThatURL()

        /// <summary>
        /// RemoteIPAddress is only ever set by the constructors handed one, so a
        /// client built from a URL knows no address until its socket connects -
        /// and every log line in DNSHTTPSClient printed that field. This is the
        /// "(null):443" that a whole day of DoH failures were reported against.
        /// </summary>
        [Test]
        public void ADoHClientBuiltFromAURL_IsNamedByThatURL()
        {

            using var client = new DNSHTTPSClient(
                                   URL.Parse("https://dns.example/dns-query")
                               );

            Assert.Multiple(() => {
                Assert.That(client.RemoteIPAddress,  Is.Null,  "the premise: there is no address to print yet");
                Assert.That(client.ToString(),       Is.EqualTo("Using DNS server: https://dns.example/dns-query"));
            });

        }

        #endregion

        #region AnUnreachableDoHResolver_IsNotQuotedAsHavingAnswered()

        /// <summary>
        /// A DoH query that never reached its resolver used to be reported as
        /// "DNS HTTPS query to (null):443 returned HTTP 400" — a status code the
        /// HTTP client had written itself, credited to a server which had said
        /// nothing at all.
        /// </summary>
        [Test]
        public async Task AnUnreachableDoHResolver_IsNotQuotedAsHavingAnswered()
        {

            var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            var closedPort = ((IPEndPoint) listener.LocalEndpoint).Port;
            listener.Stop();

            var url = URL.Parse($"https://127.0.0.1:{closedPort}/dns-query");

            using var loggerFactory = new TestLoggerFactory();
            using var client        = new DNSHTTPSClient(
                                          url,
                                          QueryTimeout:   TimeSpan.FromSeconds(5),
                                          LoggerFactory:  loggerFactory
                                      );

            await client.QueryHTTP(
                      DNSServiceName.Parse("example.org"),
                      [DNSResourceRecordTypes.A]
                  );

            var warnings = loggerFactory.Entries.
                               Where (entry => entry.LogLevel == LogLevel.Warning).
                               Select(entry => entry.Message).
                               ToList();

            var report   = String.Join(Environment.NewLine, warnings);

            Assert.Multiple(() => {

                Assert.That(
                    warnings.Any(message => message.Contains("was never answered", StringComparison.Ordinal) &&
                                            message.Contains(url.ToString(),       StringComparison.Ordinal)),
                    Is.True,
                    report
                );

                Assert.That(
                    warnings.Any(message => message.Contains("returned HTTP", StringComparison.Ordinal)),
                    Is.False,
                    report
                );

                Assert.That(
                    warnings.Any(message => message.Contains("(null)", StringComparison.Ordinal)),
                    Is.False,
                    report
                );

            });

        }

        #endregion

    }

}
