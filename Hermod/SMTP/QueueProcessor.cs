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

    /// <summary>
    /// Event-based queue processor using Channel&lt;T&gt; for zero-latency delivery.
    /// No polling - reacts immediately when new mail is queued.
    /// </summary>
    public sealed class QueueProcessor : IAsyncDisposable
    {

        private readonly IMailQueue                                    mailQueue;
        private readonly SMTPOutboundClient                            smtpOutboundClient;
        private readonly BounceHandler                                 bounceHandler;
        private readonly QueueProcessorConfig                          queueProcessorConfig;
        private readonly ILogger                                       logger;

        private readonly SemaphoreSlim                                 _deliverySemaphore;
        private readonly ConcurrentDictionary<String, DateTimeOffset>  _domainLastSend           = new();
        private readonly ConcurrentDictionary<String, Int32>           _domainSendCount          = new();
        private readonly ConcurrentDictionary<String, Boolean>         _processingIds            = new();
        private readonly HashSet<String>                               _delayNotificationsSent   = [];

        // The destinations that failed at their last attempt, by Idna.Key of the target domain.
        private readonly Dictionary<String, FailedDestination>         _failedDestinations       = [];

        /// <summary>
        /// A destination that failed: not tried again before Until - unless a message is trying
        /// it right now (Probing).
        /// </summary>
        /// <param name="Until">When the destination is tried again: when the message that failed there is due.</param>
        /// <param name="Result">How it failed.</param>
        /// <param name="Probing">Whether a message is trying it right now.</param>
        private sealed record FailedDestination(DateTimeOffset  Until,
                                                SendResult      Result,
                                                Boolean         Probing);

        /// <summary>
        /// How long a message waits while another one tries its destination.
        /// </summary>
        private static readonly TimeSpan ProbeWait = TimeSpan.FromMinutes(1);

        private CancellationTokenSource? _cts;
        private Task?                    _newMailTask;
        private Task?                    _retryCheckTask;

        public Boolean  IsRunning { get; private set; }

        /// <summary>
        /// A queued message changed: it is being delivered, deferred, delivered or failed - or
        /// a copy was queued for the recipients that refused it for now. Called after the queue
        /// knows; what a handler throws is logged and changes nothing.
        /// </summary>
        public event Action<QueuedMail>? OnStatusChanged;
        public UInt32   ActiveDeliveries
            => queueProcessorConfig.MaxConcurrentDeliveries - (UInt32) _deliverySemaphore.CurrentCount;

        public QueueProcessor(IMailQueue             MailQueue,
                              SMTPOutboundClient     SMTPOutboundClient,
                              BounceHandler          BounceHandler,
                              QueueProcessorConfig?  QueueProcessorConfig,
                              ILogger                Logger)
        {

            this.mailQueue             = MailQueue;
            this.smtpOutboundClient    = SMTPOutboundClient;
            this.bounceHandler         = BounceHandler;
            this.queueProcessorConfig  = QueueProcessorConfig ?? new QueueProcessorConfig();
            this.logger                = Logger;
            this._deliverySemaphore    = new SemaphoreSlim((Int32) queueProcessorConfig.MaxConcurrentDeliveries);

        }

        public Task StartAsync(CancellationToken ct = default)
        {

            if (IsRunning)
                return Task.CompletedTask;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            // Start two parallel consumers:
            // 1. New mail consumer - reacts immediately to new mails
            // 2. Retry check consumer - handles deferred mails ready for retry
            _newMailTask    = ConsumeNewMailsAsync(_cts.Token);
            _retryCheckTask = ConsumeRetryChecksAsync(_cts.Token);

            IsRunning = true;

            logger.Log(LogLevel.Info, $"Queue processor started (event-based, max {queueProcessorConfig.MaxConcurrentDeliveries} concurrent)");

            return Task.CompletedTask;

        }

        public async Task StopAsync()
        {

            if (!IsRunning)
                return;

            logger.Log(LogLevel.Info, "Stopping queue processor...");

            _cts?.Cancel();

            // Wait for both consumers to complete
            var tasks = new List<Task>();
            if (_newMailTask is not null) tasks.Add(_newMailTask);
            if (_retryCheckTask is not null) tasks.Add(_retryCheckTask);

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            IsRunning = false;
            logger.Log(LogLevel.Info, "Queue processor stopped");

        }

        /// <summary>
        /// Consumes new mails from the channel - zero latency delivery
        /// </summary>
        private async Task ConsumeNewMailsAsync(CancellationToken ct)
        {

            logger.Log(LogLevel.Debug, "New mail consumer started");

            try
            {
                await foreach (var mail in mailQueue.NewMailReader.ReadAllAsync(ct))
                {
                    // Skip if already being processed (deduplication)
                    if (!_processingIds.TryAdd(mail.Id, true))
                    {
                        logger.Log(LogLevel.Debug, $"Skipping duplicate: {mail.Id}");
                        continue;
                    }

                    // Check domain rate limiting
                    if (!CanSendToDomain(mail.TargetDomain))
                    {

                        logger.Log(LogLevel.Debug, $"Rate limited, deferring: {mail.Id} to {mail.TargetDomain}");
                        _processingIds.TryRemove(mail.Id, out _);

                        // Defer for later
                        mail.Status     = QueueItemStatus.Deferred;
                        mail.NextRetry  = Timestamp.Now.AddSeconds(queueProcessorConfig.DomainCooldownSeconds);
                        await UpdateAsync(mail, ct);
                        continue;

                    }

                    // A destination that failed at its last attempt is not tried again at once.
                    if (await HeldBackAsync(mail, ct))
                    {
                        _processingIds.TryRemove(mail.Id, out _);
                        continue;
                    }

                    // Start delivery in background (fire and forget with semaphore control)
                    _ = DeliverWithSemaphoreAsync(mail, ct);

                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                logger.Log(LogLevel.Debug, "New mail consumer stopped");
            }
            catch (ChannelClosedException)
            {
                logger.Log(LogLevel.Debug, "New mail channel closed");
            }

        }

        /// <summary>
        /// Consumes retry check signals - handles deferred mails
        /// </summary>
        private async Task ConsumeRetryChecksAsync(CancellationToken ct)
        {

            logger.Log(LogLevel.Debug, "Retry check consumer started");

            try
            {
                await foreach (var _ in mailQueue.RetryCheckReader.ReadAllAsync(ct))
                {
                    await ProcessDeferredMailsAsync(ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                logger.Log(LogLevel.Debug, "Retry check consumer stopped");
            }
            catch (ChannelClosedException)
            {
                logger.Log(LogLevel.Debug, "Retry check channel closed");
            }

        }

        private async Task ProcessDeferredMailsAsync(CancellationToken ct)
        {

            ResetDomainCounters();

            var deferredMails = await mailQueue.GetPendingAsync(50, ct);

            if (deferredMails.Count == 0)
                return;

            logger.Log(LogLevel.Debug, $"Processing {deferredMails.Count} deferred mails");

            foreach (var mail in deferredMails)
            {
                if (ct.IsCancellationRequested)
                    break;

                // Skip if already being processed
                if (!_processingIds.TryAdd(mail.Id, true))
                    continue;

                // Check domain rate limiting
                if (!CanSendToDomain(mail.TargetDomain))
                {
                    _processingIds.TryRemove(mail.Id, out _);
                    continue;
                }

                // A destination that failed at its last attempt waits for its time.
                if (await HeldBackAsync(mail, ct))
                {
                    _processingIds.TryRemove(mail.Id, out _);
                    continue;
                }

                _ = DeliverWithSemaphoreAsync(mail, ct);
            }

        }

        private async Task DeliverWithSemaphoreAsync(QueuedMail mail, CancellationToken ct)
        {
            await _deliverySemaphore.WaitAsync(ct);
            try
            {
                await DeliverMailAsync(mail, ct);
            }
            finally
            {
                _deliverySemaphore.Release();
                _processingIds.TryRemove(mail.Id, out _);
            }
        }

        private async Task DeliverMailAsync(QueuedMail mail, CancellationToken ct)
        {
            try
            {

                RecordDomainSend(mail.TargetDomain);

                // Mark as processing
                mail.Status = QueueItemStatus.Processing;
                await UpdateAsync(mail, ct);

                logger.Log(LogLevel.Info, $"→ Delivering {mail.Id} to {mail.TargetDomain} (attempt {mail.RetryCount + 1})");

                var result = await smtpOutboundClient.SendAsync(
                    mail.TargetDomain,
                    mail.EnvelopeFrom,
                    mail.EnvelopeTo,
                    mail.MessageContent,
                    mail.RequireTls,
                    // A queued Notify of Never is none given; a NOTIFY=NEVER is in RecipientDsns.
                    // The ENVID is the xtext as it was received or queued, and goes as it is.
                    new DsnParameters(mail.Notify == DsnNotify.Never ? null : mail.Notify, mail.Ret) { ReceivedEnvId = mail.EnvId },
                    mail.Priority,
                    ct,
                    mail.RecipientDsns,
                    mail.MessageOctets
                );

                await HandleDeliveryResultAsync(mail, result, ct);

                NoteDestination(mail, result);

            }
            catch (Exception ex)
            {

                logger.Log(LogLevel.Error, $"Delivery exception for {mail.Id}: {ex.Message}");

                // What the attempt said of its destination is not known: the next message may try it.
                lock (_failedDestinations)
                {
                    if (_failedDestinations.TryGetValue(Idna.Key(mail.TargetDomain), out var failed) && failed.Probing)
                        _failedDestinations.Remove(Idna.Key(mail.TargetDomain));
                }

                mail.LastError = ex.Message;
                mail.RetryCount++;
                mail.NextRetry = RetryCalculator.GetNextRetryTime(mail.RetryCount, queueProcessorConfig.RetryIntervals);
                mail.Status = QueueItemStatus.Deferred;

                await UpdateAsync(mail, ct);

            }
        }

        private async Task HandleDeliveryResultAsync(QueuedMail mail, SendResult result, CancellationToken ct)
        {

            // Accepted for some recipients, refused for others: each gets what it needs.
            if (result.Recipients.Select(outcome => outcome.Status).Distinct().Count() > 1)
            {
                await HandleMixedOutcomeAsync(mail, result, ct);
                return;
            }

            switch (result.Status)
            {

                case SendStatus.Success:
                    logger.Log(LogLevel.Info, 
                        $"✓ Delivered {mail.Id} via {result.RemoteMx} in {result.Duration?.TotalMilliseconds:F0}ms");

                    mail.Status          = QueueItemStatus.Delivered;
                    mail.DeliveredAt     = Timestamp.Now;
                    mail.RemoteMx        = result.RemoteMx;
                    mail.RemoteResponse  = result.ResponseText;

                    await UpdateAsync(mail, ct);

                    // Relayed status notification (RFC 3461) — only if NOTIFY=SUCCESS was requested and
                    // the next hop did not advertise DSN (else it owns the delivered notification).
                    await bounceHandler.SendRelayNotificationAsync(mail, result.RemoteSupportsDsn, ct);
                    break;

                case SendStatus.TempFail:
                    mail.RetryCount++;
                    mail.LastError = $"{result.ResponseCode} {result.ResponseText}";
                    mail.RemoteMx = result.RemoteMx;

                    if (RetryCalculator.ShouldGiveUp(mail, queueProcessorConfig.MaxRetries, queueProcessorConfig.GiveUpAfter))
                    {
                        logger.Log(LogLevel.Warning, 
                            $"✗ Giving up on {mail.Id} after {mail.RetryCount} attempts: {mail.LastError}");
                    
                        mail.Status = QueueItemStatus.Failed;
                        await UpdateAsync(mail, ct);
                    
                        await bounceHandler.SendBounceAsync(mail, result, ct);
                    }
                    else
                    {
                        mail.NextRetry = RetryCalculator.GetNextRetryTime(mail.RetryCount, queueProcessorConfig.RetryIntervals);
                        mail.Status = QueueItemStatus.Deferred;

                        logger.Log(LogLevel.Warning, 
                            $"↻ Deferred {mail.Id}: {mail.LastError} (retry #{mail.RetryCount} at {mail.NextRetry:HH:mm:ss})");

                        await UpdateAsync(mail, ct);

                        // Send delay notification if applicable
                        if (queueProcessorConfig.SendDelayNotifications && 
                            !_delayNotificationsSent.Contains(mail.Id) &&
                            Timestamp.Now - mail.QueuedAt > queueProcessorConfig.DelayNotificationAfter)
                        {
                            await bounceHandler.SendDelayNotificationAsync(mail, result, ct, queueProcessorConfig.GiveUpAfter);
                            _delayNotificationsSent.Add(mail.Id);
                        }
                    }
                    break;

                case SendStatus.PermFail:
                    logger.Log(LogLevel.Warning, 
                        $"✗ Permanent failure {mail.Id}: {result.ResponseCode} {result.ResponseText}");

                    mail.Status = QueueItemStatus.Failed;
                    mail.LastError = $"{result.ResponseCode} {result.ResponseText}";
                    mail.RemoteMx = result.RemoteMx;

                    await UpdateAsync(mail, ct);

                    await bounceHandler.SendBounceAsync(mail, result, ct);
                    break;

            }
        }

        /// <summary>
        /// The next hop answered the recipients differently. RFC 5321 §6.1: a server that accepted a
        /// message "MUST NOT lose" it - those accepted have it, each recipient refused gets a
        /// bounce to the sender, and those refused for now go back into the queue on their own,
        /// with the retry count and schedule the message had.
        /// </summary>
        private async Task HandleMixedOutcomeAsync(QueuedMail mail, SendResult result, CancellationToken ct)
        {

            var accepted   = result.Recipients.Where(outcome => outcome.Status == SendStatus.Success). Select(outcome => outcome.Recipient).ToArray();
            var refused    = result.Recipients.Where(outcome => outcome.Status == SendStatus.PermFail).ToArray();
            var refusedNow = result.Recipients.Where(outcome => outcome.Status == SendStatus.TempFail).ToArray();

            logger.Log(LogLevel.Info,
                $"Partly delivered {mail.Id} via {result.RemoteMx}: {accepted.Length} accepted, {refused.Length} refused, {refusedNow.Length} to retry");

            mail.RemoteMx        = result.RemoteMx;
            mail.RemoteResponse  = result.ResponseText;

            if (accepted.Length > 0)
            {
                mail.Status       = QueueItemStatus.Delivered;
                mail.DeliveredAt  = Timestamp.Now;
            }
            else
            {
                mail.Status       = QueueItemStatus.Failed;
                mail.LastError    = $"{result.ResponseCode} {result.ResponseText}";
            }

            await UpdateAsync(mail, ct);

            if (accepted.Length > 0)
                await bounceHandler.SendRelayNotificationAsync(CopyFor(mail, accepted, "delivered"), result.RemoteSupportsDsn, ct);

            foreach (var outcome in refused)
                await bounceHandler.SendBounceAsync(CopyFor(mail, [ outcome.Recipient ], "refused"),
                                                    SendResult.PermFail(outcome.Code, outcome.Reply ?? outcome.Text, result.RemoteMx),
                                                    ct);

            if (refusedNow.Length > 0)
            {

                var retry        = CopyFor(mail, [.. refusedNow.Select(outcome => outcome.Recipient)], $"retry-{mail.RetryCount + 1}");
                retry.RetryCount = (UInt16) (mail.RetryCount + 1);
                retry.LastError  = $"{refusedNow[0].Code} {refusedNow[0].Text}";
                retry.RemoteMx   = result.RemoteMx;
                retry.NextRetry  = RetryCalculator.GetNextRetryTime(retry.RetryCount, queueProcessorConfig.RetryIntervals);
                retry.Status     = QueueItemStatus.Deferred;

                await mailQueue.EnqueueAsync(retry, ct);
                Changed(retry);

            }

        }

        /// <summary>
        /// The queue learns of the change first, then whoever listens.
        /// </summary>
        private async Task UpdateAsync(QueuedMail mail, CancellationToken ct)
        {
            await mailQueue.UpdateAsync(mail, ct);
            Changed(mail);
        }

        private void Changed(QueuedMail mail)
        {
            try
            {
                OnStatusChanged?.Invoke(mail);
            }
            catch (Exception e)
            {
                logger.Log(LogLevel.Warning, $"QueueProcessor: a listener failed for {mail.Id}: {e.Message}");
            }
        }

        /// <summary>
        /// The same message for some of its recipients: the envelope, the transaction parameters
        /// and the per-recipient DSN data of those recipients; the time it was queued stays.
        /// </summary>
        private static QueuedMail CopyFor(QueuedMail mail, String[] recipients, String suffix)

            => new () {
                   Id              = $"{mail.Id}.{suffix}",
                   EnvelopeFrom    = mail.EnvelopeFrom,
                   EnvelopeTo      = recipients,
                   MessageContent  = mail.MessageContent,
                   MessageOctets   = mail.MessageOctets,
                   TargetDomain    = mail.TargetDomain,
                   QueuedAt        = mail.QueuedAt,
                   RequireTls      = mail.RequireTls,
                   Notify          = mail.Notify,
                   Ret             = mail.Ret,
                   EnvId           = mail.EnvId,
                   Priority        = mail.Priority,
                   RecipientDsns   = [.. mail.RecipientDsns.Where(recipientDsn => recipients.Contains(recipientDsn.Recipient))],
                   RemoteMx        = mail.RemoteMx,
                   RemoteResponse  = mail.RemoteResponse
               };

        #region Failed destinations

        /// <summary>
        /// Whether the attempt failed at its destination itself: no session came about - no
        /// connection, a connection closed unasked, a 421 - or the session was refused for now,
        /// before any recipient was answered. A refusal of a recipient, or of the message, is not
        /// the destination's failure.
        /// </summary>
        private static Boolean DestinationFailed(SendResult Result)

            => Result.Status == SendStatus.TempFail &&
               Result.Recipients.Count == 0 &&
               (Result.SessionRefused || Result.ResponseCode is 0 or 421);

        /// <summary>
        /// What an attempt said of its destination. RFC 5321 §4.5.4.1: "The sender MUST delay
        /// retrying a particular destination after one attempt has failed"; "A client SHOULD keep
        /// a list of hosts it cannot reach and corresponding connection timeouts, rather than just
        /// retrying queued mail items." One that failed is not tried again before the message that
        /// failed there is due; one that answered is tried at once again.
        /// </summary>
        private void NoteDestination(QueuedMail Mail, SendResult Result)
        {

            var destination = Idna.Key(Mail.TargetDomain);

            lock (_failedDestinations)
            {

                if (DestinationFailed(Result))
                    _failedDestinations[destination] = new FailedDestination(Mail.Status == QueueItemStatus.Deferred
                                                                                 ? Mail.NextRetry
                                                                                 : RetryCalculator.GetNextRetryTime(1, queueProcessorConfig.RetryIntervals),
                                                                             Result,
                                                                             false);

                else
                    _failedDestinations.Remove(destination);

            }

        }

        /// <summary>
        /// Whether the message waits for its destination, which failed at its last attempt: until
        /// the message that failed there is due - and while the first message that is due tries
        /// it. A message that has waited as long as it may has failed, and its sender learns why.
        /// </summary>
        private async Task<Boolean> HeldBackAsync(QueuedMail Mail, CancellationToken ct)
        {

            DateTimeOffset? until  = null;
            SendResult?     failed = null;
            var             now    = Timestamp.Now;

            lock (_failedDestinations)
            {

                var destination = Idna.Key(Mail.TargetDomain);

                if (_failedDestinations.TryGetValue(destination, out var state))
                {

                    failed = state.Result;

                    if (state.Probing)
                        until = now + ProbeWait;

                    else if (state.Until > now)
                        until = state.Until;

                    // Its time has come: this message tries it, the others wait for what it says.
                    else
                        _failedDestinations[destination] = state with { Probing = true };

                }

            }

            if (until is null || failed is null)
                return false;

            if (now - Mail.QueuedAt >= queueProcessorConfig.GiveUpAfter)
            {

                logger.Log(LogLevel.Warning, $"✗ Giving up on {Mail.Id}: {Mail.TargetDomain} could not be reached ({failed.ResponseCode} {failed.ResponseText})");

                Mail.Status    = QueueItemStatus.Failed;
                Mail.LastError = $"{failed.ResponseCode} {failed.ResponseText}";
                await UpdateAsync(Mail, ct);

                await bounceHandler.SendBounceAsync(Mail, failed, ct);
                return true;

            }

            logger.Log(LogLevel.Debug, $"Held back {Mail.Id}: {Mail.TargetDomain} failed at its last attempt, tried again at {until:HH:mm:ss}");

            Mail.Status    = QueueItemStatus.Deferred;
            Mail.NextRetry = until.Value;
            Mail.LastError ??= $"{failed.ResponseCode} {failed.ResponseText}";
            await UpdateAsync(Mail, ct);

            return true;

        }

        #endregion

        #region Domain Rate Limiting

        private bool CanSendToDomain(string domain)
        {
            // Check cooldown
            if (_domainLastSend.TryGetValue(domain, out var lastSend))
            {
                var elapsed = Timestamp.Now - lastSend;
                if (elapsed < TimeSpan.FromSeconds(queueProcessorConfig.DomainCooldownSeconds))
                {
                    return false;
                }
            }

            // Check per-domain limit
            if (_domainSendCount.TryGetValue(domain, out var count))
            {
                if (count >= queueProcessorConfig.MaxDeliveriesPerDomain)
                {
                    return false;
                }
            }

            return true;
        }

        private void RecordDomainSend(String domain)
        {
            _domainLastSend[domain] = Timestamp.Now;
            _domainSendCount.AddOrUpdate(domain, 1, (_, c) => c + 1);
        }

        private void ResetDomainCounters()
        {
            var threshold = Timestamp.Now.AddSeconds(-queueProcessorConfig.DomainCooldownSeconds * 2);
        
            foreach (var kvp in _domainLastSend)
            {
                if (kvp.Value < threshold)
                {
                    _domainSendCount.TryRemove(kvp.Key, out _);
                }
            }
        }

        #endregion

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            _cts?.Dispose();
            _deliverySemaphore.Dispose();
        }
    }

}
