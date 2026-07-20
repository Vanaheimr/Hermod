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

public interface IMailQueue
{
    /// <summary>
    /// Enqueue a new mail for delivery
    /// </summary>
    Task EnqueueAsync(QueuedMail mail, CancellationToken ct = default);
    
    /// <summary>
    /// Get pending mails ready for delivery (NextRetry <= now)
    /// </summary>
    Task<IReadOnlyList<QueuedMail>> GetPendingAsync(int maxItems = 50, CancellationToken ct = default);
    
    /// <summary>
    /// Get a specific mail by ID
    /// </summary>
    Task<QueuedMail?> GetByIdAsync(string id, CancellationToken ct = default);
    
    /// <summary>
    /// Update mail status
    /// </summary>
    Task UpdateAsync(QueuedMail mail, CancellationToken ct = default);
    
    /// <summary>
    /// Remove mail from queue
    /// </summary>
    Task RemoveAsync(string id, CancellationToken ct = default);
    
    /// <summary>
    /// Get queue length
    /// </summary>
    Task<int> GetQueueLengthAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Get failed mails
    /// </summary>
    Task<IReadOnlyList<QueuedMail>> GetFailedAsync(int maxItems = 100, CancellationToken ct = default);
    
    /// <summary>
    /// Channel reader for new mail notifications (event-based)
    /// </summary>
    ChannelReader<QueuedMail> NewMailReader { get; }
    
    /// <summary>
    /// Signal that deferred mails should be rechecked
    /// </summary>
    void SignalRetryCheck();
    
    /// <summary>
    /// Channel reader for retry check signals
    /// </summary>
    ChannelReader<bool> RetryCheckReader { get; }
}
