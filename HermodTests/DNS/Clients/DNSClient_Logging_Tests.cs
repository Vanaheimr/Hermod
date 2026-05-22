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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.DNS;

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

    }

}
