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

using Microsoft.Extensions.Logging;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Clients
{

    /// <summary>
    /// Keeps what a DNS client said, so that a failing test can quote it.
    /// </summary>
    /// <remarks>
    /// The live-DNS fixtures passed no logger at all, and every client duly
    /// threw its account of a failure away. A UDP query losing its datagram and
    /// waiting out the full 23.5 seconds therefore reached the test as
    /// "Expected: True, But was: False" and nothing else.
    /// </remarks>
    public sealed class DNSTestLoggerFactory : ILoggerFactory
    {

        private readonly Lock          entriesLock = new();
        private readonly List<String>  entries     = [];

        /// <summary>
        /// Everything logged at warning level or above since the last <see cref="Clear"/>.
        /// </summary>
        public IEnumerable<String> Entries
        {
            get
            {
                lock (entriesLock)
                    return entries.ToArray();
            }
        }

        /// <summary>
        /// Forget what was logged, so that one test does not quote another's.
        /// </summary>
        public void Clear()
        {
            lock (entriesLock)
                entries.Clear();
        }

        public ILogger CreateLogger(String CategoryName)
            => new DNSTestLogger(CategoryName, entries, entriesLock);

        public void AddProvider(ILoggerProvider Provider)
        { }

        public void Dispose()
        { }


        private sealed class DNSTestLogger(String        Category,
                                           List<String>  Entries,
                                           Lock          EntriesLock) : ILogger
        {

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull

                => null;

            // Warnings and above only: the transports trace every query at debug
            // level, and a test message carrying all of that is one nobody reads.
            public Boolean IsEnabled(LogLevel logLevel)
                => logLevel >= LogLevel.Warning;

            public void Log<TState>(LogLevel                          logLevel,
                                    EventId                           eventId,
                                    TState                            state,
                                    Exception?                        exception,
                                    Func<TState, Exception?, String>  formatter)
            {

                if (logLevel < LogLevel.Warning)
                    return;

                var entry = $"[{logLevel}] {Category.Split('.').Last()}: {formatter(state, exception)}";

                if (exception is not null)
                    entry += $" <<{exception.GetType().Name}: {exception.Message}>>";

                lock (EntriesLock)
                    Entries.Add(entry);

            }

        }

    }

}
