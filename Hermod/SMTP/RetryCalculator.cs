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

using org.GraphDefined.Vanaheimr.Illias;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP;

public static class RetryCalculator
{

    // RFC 5321 recommends: retry for at least 4-5 days
    // Typical schedule: 15m, 30m, 1h, 2h, 4h, 8h, 12h, then every 24h
    private static readonly TimeSpan[] RetryIntervals =
    [
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(4),
        TimeSpan.FromHours(8),
        TimeSpan.FromHours(12),
        TimeSpan.FromHours(24),
        TimeSpan.FromHours(24),
        TimeSpan.FromHours(24),
        TimeSpan.FromHours(24),
        TimeSpan.FromHours(24),
    ];

    public const UInt16 MaxRetries = 12; // ~5 days total
    public static readonly TimeSpan MaxQueueTime = TimeSpan.FromDays(5);

    public static DateTimeOffset GetNextRetryTime(UInt16 retryCount)
    {

        var index = Math.Min(retryCount, RetryIntervals.Length - 1);
        var interval = RetryIntervals[index];

        // Add some jitter (±10%) to prevent thundering herd
        var jitter = interval.TotalSeconds * (Random.Shared.NextDouble() * 0.2 - 0.1);

        return Timestamp.Now.Add(interval).AddSeconds(jitter);

    }

    public static Boolean ShouldGiveUp(QueuedMail mail)
    {
        return mail.RetryCount >= MaxRetries ||
               Timestamp.Now - mail.QueuedAt > MaxQueueTime;
    }

}
