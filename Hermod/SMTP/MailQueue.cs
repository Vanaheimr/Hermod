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

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP;

#region Queue Item

public enum QueueItemStatus
{
    Pending,
    Processing,
    Deferred,
    Failed,
    Delivered
}

public sealed class QueuedMail
{

    // Constructed only inside this library (via MailSender or the internal report/relay paths);
    // external callers must go through MailSender with a typed EMail/EMailEnvelop instead of
    // hand-crafting a raw message string. [JsonConstructor] keeps FileMailQueue persistence working.
    [JsonConstructor]
    internal QueuedMail() { }

    public required String    Id                { get; init; }
    public required String    EnvelopeFrom      { get; init; }
    public required String[]  EnvelopeTo        { get; init; }
    public required String    MessageContent    { get; init; }
    public required String    TargetDomain      { get; init; }
    public DateTime           QueuedAt          { get; init; } = DateTime.UtcNow;
    public DateTime           NextRetry         { get; set;  }  = DateTime.UtcNow;
    public UInt16             RetryCount        { get; set;  }  = 0;
    public String?            LastError         { get; set;  }
    public QueueItemStatus    Status            { get; set;  }  = QueueItemStatus.Pending;
    public DateTime?          DeliveredAt       { get; set;  }
    public String?            RemoteMx          { get; set;  }
    public String?            RemoteResponse    { get; set;  }
    public Boolean            RequireTls        { get; init; } = false;  // RFC 8689

    // DSN request (RFC 3461) attached by the sender; carried onto the outbound MAIL FROM / RCPT TO
    // (if the remote advertises DSN) and used to decide whether to emit a success DSN on delivery.
    public DsnNotify          Notify            { get; init; } = DsnNotify.Never;
    public DsnRet             Ret               { get; init; } = DsnRet.Full;
    public String?            EnvId             { get; init; }

}

#endregion

#region Queue Interface

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

#endregion

#region File-Based Queue

/// <summary>
/// File-based persistent mail queue with event-based notification.
/// Each queued mail is stored as a JSON file.
/// Uses Channel&lt;T&gt; for zero-latency producer-consumer pattern.
/// </summary>
public sealed class FileMailQueue : IMailQueue, IDisposable
{
    private readonly string _queuePath;
    private readonly string _failedPath;
    private readonly string _deliveredPath;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    // Event channels
    private readonly Channel<QueuedMail> _newMailChannel;
    private readonly Channel<bool> _retryCheckChannel;
    private readonly Timer _retryTimer;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ChannelReader<QueuedMail> NewMailReader => _newMailChannel.Reader;
    public ChannelReader<bool> RetryCheckReader => _retryCheckChannel.Reader;

    public FileMailQueue(string basePath, ILogger logger)
    {
        _queuePath = Path.Combine(basePath, "queue", "pending");
        _failedPath = Path.Combine(basePath, "queue", "failed");
        _deliveredPath = Path.Combine(basePath, "queue", "delivered");
        _logger = logger;

        Directory.CreateDirectory(_queuePath);
        Directory.CreateDirectory(_failedPath);
        Directory.CreateDirectory(_deliveredPath);

        // Create unbounded channels for notifications
        _newMailChannel = Channel.CreateUnbounded<QueuedMail>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });
        
        _retryCheckChannel = Channel.CreateUnbounded<bool>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        // Timer to periodically check for deferred mails ready for retry
        _retryTimer = new Timer(
            _ => SignalRetryCheck(),
            null,
            TimeSpan.FromMinutes(1),    // Initial delay
            TimeSpan.FromMinutes(1)     // Check every minute
        );

        // Load existing pending mails on startup
        _ = LoadExistingMailsAsync();
    }

    private async Task LoadExistingMailsAsync()
    {
        try
        {
            var files = Directory.GetFiles(_queuePath, "*.json");
            if (files.Length > 0)
            {
                _logger.Log(LogLevel.Info, $"Loading {files.Length} existing queued mails...");
                
                foreach (var file in files)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(file);
                        var mail = JsonSerializer.Deserialize<QueuedMail>(json, JsonOptions);
                        
                        if (mail is not null && 
                            mail.Status is QueueItemStatus.Pending or QueueItemStatus.Deferred &&
                            mail.NextRetry <= DateTime.UtcNow)
                        {
                            await _newMailChannel.Writer.WriteAsync(mail);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(LogLevel.Warning, $"Failed to load queue file {file}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Failed to load existing mails: {ex.Message}");
        }
    }

    public async Task EnqueueAsync(QueuedMail mail, CancellationToken ct = default)
    {
        var filePath = GetFilePath(_queuePath, mail.Id);
        var json = JsonSerializer.Serialize(mail, JsonOptions);
        
        await File.WriteAllTextAsync(filePath, json, ct);
        _logger.Log(LogLevel.Info, $"Queued mail {mail.Id} to {mail.TargetDomain} ({mail.EnvelopeTo.Length} recipients)");
        
        // Notify consumers immediately via channel
        await _newMailChannel.Writer.WriteAsync(mail, ct);
    }

    public void SignalRetryCheck()
    {
        // Non-blocking write - if channel is full, skip (will retry next interval)
        _retryCheckChannel.Writer.TryWrite(true);
    }

    public async Task<IReadOnlyList<QueuedMail>> GetPendingAsync(int maxItems = 50, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var result = new List<QueuedMail>();
            var now = DateTime.UtcNow;

            var files = Directory.GetFiles(_queuePath, "*.json")
                .OrderBy(f => File.GetCreationTimeUtc(f))
                .Take(maxItems * 2);

            foreach (var file in files)
            {
                if (result.Count >= maxItems)
                    break;

                try
                {
                    var json = await File.ReadAllTextAsync(file, ct);
                    var mail = JsonSerializer.Deserialize<QueuedMail>(json, JsonOptions);

                    if (mail is not null && 
                        mail.Status is QueueItemStatus.Pending or QueueItemStatus.Deferred &&
                        mail.NextRetry <= now)
                    {
                        result.Add(mail);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Warning, $"Failed to read queue file {file}: {ex.Message}");
                }
            }

            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<QueuedMail>> GetDeferredReadyAsync(int maxItems = 50, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var result = new List<QueuedMail>();
            var now = DateTime.UtcNow;

            var files = Directory.GetFiles(_queuePath, "*.json");

            foreach (var file in files)
            {
                if (result.Count >= maxItems)
                    break;

                try
                {
                    var json = await File.ReadAllTextAsync(file, ct);
                    var mail = JsonSerializer.Deserialize<QueuedMail>(json, JsonOptions);

                    if (mail is not null && 
                        mail.Status == QueueItemStatus.Deferred &&
                        mail.NextRetry <= now)
                    {
                        result.Add(mail);
                    }
                }
                catch
                {
                    // Skip invalid files
                }
            }

            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<QueuedMail?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var filePath = GetFilePath(_queuePath, id);
        if (!File.Exists(filePath))
        {
            filePath = GetFilePath(_failedPath, id);
            if (!File.Exists(filePath))
                return null;
        }

        var json = await File.ReadAllTextAsync(filePath, ct);
        return JsonSerializer.Deserialize<QueuedMail>(json, JsonOptions);
    }

    public async Task UpdateAsync(QueuedMail mail, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var sourcePath = GetFilePath(_queuePath, mail.Id);
            string targetPath;

            switch (mail.Status)
            {
                case QueueItemStatus.Failed:
                    targetPath = GetFilePath(_failedPath, mail.Id);
                    break;
                case QueueItemStatus.Delivered:
                    targetPath = GetFilePath(_deliveredPath, mail.Id);
                    break;
                default:
                    targetPath = sourcePath;
                    break;
            }

            var json = JsonSerializer.Serialize(mail, JsonOptions);
            await File.WriteAllTextAsync(targetPath, json, ct);

            // Move file if status changed
            if (targetPath != sourcePath && File.Exists(sourcePath))
            {
                File.Delete(sourcePath);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task RemoveAsync(string id, CancellationToken ct = default)
    {
        var filePath = GetFilePath(_queuePath, id);
        if (File.Exists(filePath))
            File.Delete(filePath);

        return Task.CompletedTask;
    }

    public Task<int> GetQueueLengthAsync(CancellationToken ct = default)
    {
        var count = Directory.GetFiles(_queuePath, "*.json").Length;
        return Task.FromResult(count);
    }

    public async Task<IReadOnlyList<QueuedMail>> GetFailedAsync(int maxItems = 100, CancellationToken ct = default)
    {
        var result = new List<QueuedMail>();
        var files = Directory.GetFiles(_failedPath, "*.json")
            .OrderByDescending(f => File.GetCreationTimeUtc(f))
            .Take(maxItems);

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, ct);
                var mail = JsonSerializer.Deserialize<QueuedMail>(json, JsonOptions);
                if (mail is not null)
                    result.Add(mail);
            }
            catch
            {
                // Skip invalid files
            }
        }

        return result;
    }

    private static string GetFilePath(string folder, string id)
    {
        var safeId = string.Join("_", id.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(folder, $"{safeId}.json");
    }

    public void Dispose()
    {
        _retryTimer.Dispose();
        _newMailChannel.Writer.Complete();
        _retryCheckChannel.Writer.Complete();
        _lock.Dispose();
    }
}

#endregion

#region Retry Calculator

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

    public static DateTime GetNextRetryTime(UInt16 retryCount)
    {

        var index = Math.Min(retryCount, RetryIntervals.Length - 1);
        var interval = RetryIntervals[index];

        // Add some jitter (±10%) to prevent thundering herd
        var jitter = interval.TotalSeconds * (Random.Shared.NextDouble() * 0.2 - 0.1);

        return DateTime.UtcNow.Add(interval).AddSeconds(jitter);

    }

    public static Boolean ShouldGiveUp(QueuedMail mail)
    {
        return mail.RetryCount >= MaxRetries ||
               DateTime.UtcNow - mail.QueuedAt > MaxQueueTime;
    }

}

#endregion
