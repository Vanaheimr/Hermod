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

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// How a message leaves: directly to the MX of every recipient domain - Host
    /// null - or through a smart host, with a login when it wants one.
    /// </summary>
    /// <param name="Host">The smart host, or null: directly.</param>
    /// <param name="Port">Its port.</param>
    /// <param name="Username">The login, if it wants one.</param>
    /// <param name="Password">The password of the login.</param>
    public sealed record SmartHostRoute(String?  Host,
                                        UInt16   Port      = 25,
                                        String?  Username  = null,
                                        String?  Password  = null)
    {

        /// <summary>
        /// Directly to the MX of every recipient domain.
        /// </summary>
        public static SmartHostRoute Direct { get; } = new (null);

        /// <summary>
        /// The way of an outbound configuration.
        /// </summary>
        public static SmartHostRoute Of(SmtpOutboundConfig Config)
            => new (Config.SmartHost, Config.SmartHostPort, Config.SmartHostUsername, Config.SmartHostPassword);

        /// <summary>
        /// The password stays out of logs and debuggers.
        /// </summary>
        public override String ToString()
            => Host is null ? "direct" : $"{Host}:{Port}{(Username is not null ? $" as {Username}" : "")}";

    }

}
