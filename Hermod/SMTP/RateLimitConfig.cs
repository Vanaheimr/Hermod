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

using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP.Server;

public sealed record RateLimitConfig
{
    /// <summary>Maximum concurrent connections total</summary>
    public int      MaxTotalConnections         { get; init; } = 100;

    /// <summary>Maximum concurrent connections per IP</summary>
    public int      MaxConnectionsPerIp         { get; init; } = 10;

    /// <summary>Maximum new connections per IP per minute</summary>
    public int      MaxConnectionsPerIpPerMinute{ get; init; } = 30;

    /// <summary>Maximum AUTH attempts per IP per hour</summary>
    public int      MaxAuthAttemptsPerIpPerHour { get; init; } = 10;

    /// <summary>Maximum RCPT TO commands per session</summary>
    public int      MaxRcptPerSession           { get; init; } = 100;

    /// <summary>Maximum messages per authenticated session</summary>
    public int      MaxMessagesPerSession       { get; init; } = 100;

    /// <summary>Maximum messages per IP per hour (unauthenticated)</summary>
    public int      MaxMessagesPerIpPerHour     { get; init; } = 50;

    /// <summary>Maximum invalid commands before disconnect</summary>
    public int      MaxInvalidCommands          { get; init; } = 5;

    /// <summary>Delay after failed AUTH (milliseconds)</summary>
    public int      AuthFailDelayMs             { get; init; } = 3000;

    /// <summary>IPs that bypass rate limiting</summary>
    public HashSet<string> WhitelistedIps       { get; init; } = ["127.0.0.1", "::1"];

    /// <summary>IPs that are always blocked</summary>
    public HashSet<string> BlacklistedIps       { get; init; } = [];

}
