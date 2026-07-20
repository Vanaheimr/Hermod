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

using System.Threading.Channels;
using System.Collections.Concurrent;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    public static class QueueStatisticsExtensions
    {
        public static async Task<QueueStatistics> GetStatisticsAsync(this IMailQueue queue, CancellationToken ct = default)
        {

            var pending =  await queue.GetQueueLengthAsync(ct);
            var failed  = (await queue.GetFailedAsync(1000, ct)).Count;

            return new QueueStatistics(
                PendingCount: pending,
                ProcessingCount: 0,
                DeferredCount: 0,
                FailedCount: failed,
                DeliveredCount: 0,
                TotalCount: pending + failed
            );

        }
    }

    public sealed record QueueStatistics(
        int     PendingCount,
        int     ProcessingCount,
        int     DeferredCount,
        int     FailedCount,
        int     DeliveredCount,
        int     TotalCount
    );

}
