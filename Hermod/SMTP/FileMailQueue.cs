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
                            mail.NextRetry <= Timestamp.Now)
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
            var candidates = new List<QueuedMail>();
            var now = Timestamp.Now;

            // Collect every ready item, then order by MT-PRIORITY (RFC 6710) so higher-priority mail is
            // delivered first; within the same priority keep FIFO by readiness (NextRetry).
            foreach (var file in Directory.GetFiles(_queuePath, "*.json"))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file, ct);
                    var mail = JsonSerializer.Deserialize<QueuedMail>(json, JsonOptions);

                    if (mail is not null &&
                        mail.Status is QueueItemStatus.Pending or QueueItemStatus.Deferred &&
                        mail.NextRetry <= now)
                    {
                        candidates.Add(mail);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Warning, $"Failed to read queue file {file}: {ex.Message}");
                }
            }

            return candidates
                .OrderByDescending(m => m.Priority)
                .ThenBy(m => m.NextRetry)
                .Take(maxItems)
                .ToList();
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
            var now = Timestamp.Now;

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
