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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP;

/// <summary>
/// When a message refused for now is tried again, and when it is given up on - RFC 5321
/// §4.5.4.1, the sending strategy. The defaults are here; an operator sets their own in
/// QueueProcessorConfig: "The parameters to the retry algorithm MUST be configurable."
/// </summary>
public static class RetryCalculator
{

    /// <summary>
    /// The default retry intervals: the first after the first attempt, the last for every retry
    /// after. RFC 5321 §4.5.4.1: "In general, the retry interval SHOULD be at least 30 minutes";
    /// "Experience suggests that failures are typically transient ... favoring a policy of two
    /// connection attempts in the first hour the message is in the queue, and then backing off".
    /// </summary>
    public static IReadOnlyList<TimeSpan> DefaultRetryIntervals { get; } = [
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(4),
        TimeSpan.FromHours(8),
        TimeSpan.FromHours(12),
        TimeSpan.FromHours(24)
    ];

    /// <summary>
    /// The default number of attempts after which a message refused for now is given up on.
    /// </summary>
    public const UInt16 MaxRetries = 12;

    /// <summary>
    /// The default time after which a message refused for now is given up on. RFC 5321
    /// §4.5.4.1: "the give-up time generally needs to be at least 4-5 days".
    /// </summary>
    public static readonly TimeSpan MaxQueueTime = TimeSpan.FromDays(5);


    /// <summary>
    /// When a message refused for now RetryCount times is tried again, with the default intervals.
    /// </summary>
    /// <param name="RetryCount">How often the message was tried, at least once.</param>
    public static DateTimeOffset GetNextRetryTime(UInt16 RetryCount)
        => GetNextRetryTime(RetryCount, DefaultRetryIntervals);

    /// <summary>
    /// When a message refused for now RetryCount times is tried again: the interval for that
    /// retry - the first after the first attempt, the last for every retry after it - and a little
    /// more. The jitter spreads the retries of messages that failed together, and only ever adds:
    /// an interval is the least time between two attempts.
    /// </summary>
    /// <param name="RetryCount">How often the message was tried, at least once.</param>
    /// <param name="RetryIntervals">The retry intervals; none, the default ones.</param>
    public static DateTimeOffset GetNextRetryTime(UInt16 RetryCount, IReadOnlyList<TimeSpan>? RetryIntervals)
    {

        var intervals = RetryIntervals is { Count: > 0 } ? RetryIntervals : DefaultRetryIntervals;
        var interval  = intervals[Math.Clamp(RetryCount - 1, 0, intervals.Count - 1)];

        if (interval < TimeSpan.Zero)
            interval = TimeSpan.Zero;

        // Up to 10 % more, to keep messages that failed together from all coming back at once.
        return Timestamp.Now + interval + interval * (Random.Shared.NextDouble() * 0.1);

    }

    /// <summary>
    /// Whether a message refused for now is given up on, by the default parameters.
    /// </summary>
    public static Boolean ShouldGiveUp(QueuedMail Mail)
        => ShouldGiveUp(Mail, MaxRetries, MaxQueueTime);

    /// <summary>
    /// Whether a message refused for now is given up on: it was tried MaxRetries times, or it
    /// has been in the queue for GiveUpAfter.
    /// </summary>
    /// <param name="Mail">The message.</param>
    /// <param name="MaxRetries">The number of attempts after which it is given up on.</param>
    /// <param name="GiveUpAfter">The time in the queue after which it is given up on.</param>
    public static Boolean ShouldGiveUp(QueuedMail Mail, UInt16 MaxRetries, TimeSpan GiveUpAfter)
        => Mail.RetryCount >= MaxRetries ||
           Timestamp.Now - Mail.QueuedAt >= GiveUpAfter;

}
