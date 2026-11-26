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

    public sealed record QueueProcessorConfig
    {

        public UInt32    MaxConcurrentDeliveries    { get; init; } = 10;
        public UInt32    MaxDeliveriesPerDomain     { get; init; } = 5;
        public UInt32    DomainCooldownSeconds      { get; init; } = 60;
        public Boolean   SendDelayNotifications     { get; init; } = true;
        public TimeSpan  DelayNotificationAfter     { get; init; } = TimeSpan.FromHours(4);

        /// <summary>
        /// The least time between two attempts of a message refused for now: the first interval
        /// after the first attempt, the second after the second, the last after every later one.
        /// A destination that failed is not tried again for other messages either, until the
        /// message that failed there is due. RFC 5321 §4.5.4.1: "The parameters to the retry
        /// algorithm MUST be configurable"; "In general, the retry interval SHOULD be at least 30
        /// minutes". Empty: the default intervals, 30 minutes up to a day.
        /// </summary>
        public IReadOnlyList<TimeSpan>  RetryIntervals  { get; init; } = RetryCalculator.DefaultRetryIntervals;

        /// <summary>
        /// The time in the queue after which a message refused for now has failed, and its sender
        /// gets a bounce. RFC 5321 §4.5.4.1: "the give-up time generally needs to be at least 4-5
        /// days". Five days by default.
        /// </summary>
        public TimeSpan                 GiveUpAfter     { get; init; } = RetryCalculator.MaxQueueTime;

        /// <summary>
        /// The number of attempts after which a message refused for now has failed, whatever its
        /// time in the queue. 12 by default.
        /// </summary>
        public UInt16                   MaxRetries      { get; init; } = RetryCalculator.MaxRetries;

    }

}
