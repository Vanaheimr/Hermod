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

using org.GraphDefined.Vanaheimr.Illias;

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    public sealed class ConsoleLogger : ILogger
    {
        public void Log(LogLevel level, string message)
        {
            var color = level switch
            {
                LogLevel.Debug   => ConsoleColor.Gray,
                LogLevel.Info    => ConsoleColor.White,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error   => ConsoleColor.Red,
                _                => ConsoleColor.White
            };

            var prefix = level switch
            {
                LogLevel.Debug   => "[DBG]",
                LogLevel.Info    => "[INF]",
                LogLevel.Warning => "[WRN]",
                LogLevel.Error   => "[ERR]",
                _                => "[???]"
            };

            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine($"{Timestamp.Now:HH:mm:ss.fff} {prefix} {message}");
            Console.ForegroundColor = oldColor;
        }

    }

}
