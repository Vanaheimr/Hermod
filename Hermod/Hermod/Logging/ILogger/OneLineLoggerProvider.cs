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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Logging
{

    public sealed class OneLineLoggerOptions
    {

        public LogLevel  ConsoleMinimumLevel    { get; init; } = LogLevel.Information;

        public LogLevel  FileMinimumLevel       { get; init; } = LogLevel.None;

        public String?   FilePath               { get; init; }

    }


    public sealed class OneLineLoggerProvider : ILoggerProvider
    {

        private readonly OneLineLoggerOptions  options;
        private readonly Lock                  writeLock = new();
        private readonly StreamWriter?         fileWriter;


        public OneLineLoggerProvider(OneLineLoggerOptions Options)
        {

            this.options = Options;

            if (Options.FileMinimumLevel != LogLevel.None &&
                !String.IsNullOrWhiteSpace(Options.FilePath))
            {

                var directoryName = Path.GetDirectoryName(Options.FilePath);

                if (!String.IsNullOrWhiteSpace(directoryName))
                    Directory.CreateDirectory(directoryName);

                fileWriter = new StreamWriter(
                                 new FileStream(
                                     Options.FilePath,
                                     FileMode.Append,
                                     FileAccess.Write,
                                     FileShare.ReadWrite
                                 )
                             ) {
                                 AutoFlush = true
                             };

            }

        }


        public ILogger CreateLogger(String CategoryName)

            => new OneLineLogger(
                   CategoryName,
                   options,
                   writeLock,
                   fileWriter
               );


        public void Dispose()

            => fileWriter?.Dispose();

    }


    public static class OneLineLoggerBuilderExtensions
    {

        public static ILoggingBuilder AddOneLineLogger(this ILoggingBuilder       Builder,
                                                       OneLineLoggerOptions?      Options = null)
        {

            Builder.AddProvider(
                new OneLineLoggerProvider(
                    Options ?? new OneLineLoggerOptions()
                )
            );

            return Builder;

        }

    }


    internal sealed class OneLineLogger : ILogger
    {

        private readonly String                categoryName;
        private readonly OneLineLoggerOptions  options;
        private readonly Lock                  writeLock;
        private readonly StreamWriter?         fileWriter;


        public OneLineLogger(String                CategoryName,
                             OneLineLoggerOptions  Options,
                             Lock                  WriteLock,
                             StreamWriter?         FileWriter)
        {

            this.categoryName = CategoryName;
            this.options      = Options;
            this.writeLock    = WriteLock;
            this.fileWriter   = FileWriter;

        }


        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull

            => NullScope.Instance;


        public Boolean IsEnabled(LogLevel LogLevel)

            => LogLevel != LogLevel.None &&
               (LogLevel >= options.ConsoleMinimumLevel ||
                LogLevel >= options.FileMinimumLevel);


        public void Log<TState>(LogLevel                         LogLevel,
                                EventId                          EventId,
                                TState                           State,
                                Exception?                       Exception,
                                Func<TState, Exception?, String> Formatter)
        {

            if (!IsEnabled(LogLevel))
                return;

            var message = Formatter(State, Exception);

            if (String.IsNullOrWhiteSpace(message) &&
                Exception is null)
                return;

            var line = FormatLine(
                           LogLevel,
                           CreateContext(categoryName),
                           message,
                           Exception
                       );

            lock (writeLock)
            {

                if (LogLevel >= options.ConsoleMinimumLevel)
                    Console.WriteLine(line);

                if (fileWriter is not null &&
                    LogLevel >= options.FileMinimumLevel)
                    fileWriter.WriteLine(line);

            }

        }


        private static String FormatLine(LogLevel    LogLevel,
                                         String      Context,
                                         String      Message,
                                         Exception?  Exception)
        {

            var timestamp  = DateTimeOffset.Now.ToString("dd.MM.yyyy HH:mm:ss zzz");
            var threadId   = Environment.CurrentManagedThreadId;
            var severity   = LogLevel switch {
                                 LogLevel.Trace       => " trace",
                                 LogLevel.Debug       => "",
                                 LogLevel.Information => "",
                                 LogLevel.Warning     => " warning",
                                 LogLevel.Error       => " error",
                                 LogLevel.Critical    => " critical",
                                 _                    => ""
                             };

            var line = $"[{timestamp} T:{threadId}] {Context}{severity}: {Compact(Message)}";

            if (Exception is not null)
                line += $" ({Exception.GetType().Name}: {Compact(Exception.Message)})";

            return line;

        }


        private static String CreateContext(String CategoryName)
        {

            if (CategoryName.StartsWith("org.GraphDefined.Vanaheimr.Hermod.DNS.", StringComparison.Ordinal))
            {

                var dnsCategory = CategoryName["org.GraphDefined.Vanaheimr.Hermod.DNS.".Length..];

                return dnsCategory switch {
                           "IDNSClient"     => "DNS Client",
                           "DNSClient"      => "DNS Client",
                           "DNSUDPClient"   => "DNS UDP",
                           "DNSTCPClient"   => "DNS TCP",
                           "DNSTLSClient"   => "DNS TLS",
                           "DNSHTTPSClient" => "DNS HTTPS",
                           "DNSCache"       => "DNS Cache",
                           "DNSServer"      => "DNS Server",
                           _                => $"DNS/{dnsCategory}"
                       };

            }

            const String nornPrefix = "org.GraphDefined.Vanaheimr.Norn.";

            if (CategoryName.StartsWith(nornPrefix, StringComparison.Ordinal))
                return CategoryName[nornPrefix.Length..].Replace('.', '/');

            var lastDotIndex = CategoryName.LastIndexOf('.');

            return lastDotIndex >= 0
                       ? CategoryName[(lastDotIndex + 1)..]
                       : CategoryName;

        }


        private static String Compact(String Text)

            => Text.
                   Replace("\r", " ", StringComparison.Ordinal).
                   Replace("\n", " ", StringComparison.Ordinal).
                   Trim();


        private sealed class NullScope : IDisposable
        {

            public static readonly NullScope Instance = new();

            public void Dispose()
            { }

        }

    }

}
