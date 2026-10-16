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
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Channels;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP;

/// <summary>
/// File-based persistent mail queue with event-based notification.
/// Each queued mail is stored as a JSON file.
/// Uses Channel&lt;T&gt; for zero-latency producer-consumer pattern.
/// </summary>
/// <remarks>
/// <para>
/// Every entry is written to a temporary file beside it, flushed to the disk and renamed over
/// the old one: a crash leaves the entry as it was or as it is now, never half of it. What a
/// crash leaves in between is tidied when the queue opens again - a stray temporary file is
/// removed, an entry that was being delivered is tried again, and one that had reached
/// delivered or failed is not delivered twice.
/// </para>
/// <para>
/// An entry that cannot be read is said so in the log as an error and set aside into
/// <c>queue/corrupt</c>, where it waits for an administrator (<see cref="GetSetAside"/>),
/// instead of being skipped over and over without a word.
/// </para>
/// <para>
/// Delivered mail is kept without its content - who it went to, where and when, for whoever
/// asks how it fared - and only for <see cref="DeliveredRetention"/>; failed mail is kept as it
/// was, for <see cref="FailedRetention"/>. Neither stays for ever with its text and the blind
/// copies' recipients.
/// </para>
/// </remarks>
public sealed class FileMailQueue : IMailQueue, IDisposable
{

    /// <summary>
    /// How long delivered mail is remembered, if not said otherwise: a week.
    /// </summary>
    public static readonly TimeSpan DefaultDeliveredRetention = TimeSpan.FromDays(7);

    /// <summary>
    /// How long failed mail is kept, if not said otherwise: thirty days.
    /// </summary>
    public static readonly TimeSpan DefaultFailedRetention    = TimeSpan.FromDays(30);

    private readonly string _queuePath;
    private readonly string _failedPath;
    private readonly string _deliveredPath;
    private readonly string _corruptPath;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Event channels
    private readonly Channel<QueuedMail> _newMailChannel;
    private readonly Channel<bool> _retryCheckChannel;
    private readonly Timer _retryTimer;
    private readonly Timer _purgeTimer;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ChannelReader<QueuedMail> NewMailReader => _newMailChannel.Reader;
    public ChannelReader<bool> RetryCheckReader => _retryCheckChannel.Reader;

    /// <summary>
    /// How long a delivered mail is remembered - without its content - before it is deleted.
    /// </summary>
    public TimeSpan DeliveredRetention { get; }

    /// <summary>
    /// How long a failed mail is kept before it is deleted.
    /// </summary>
    public TimeSpan FailedRetention { get; }

    /// <summary>
    /// A new queue below the given path, or the one that is there.
    /// </summary>
    /// <param name="basePath">The directory below which "queue" is.</param>
    /// <param name="logger">Where it says what it does.</param>
    /// <param name="deliveredRetention">How long delivered mail is remembered (default a week).</param>
    /// <param name="failedRetention">How long failed mail is kept (default thirty days).</param>
    public FileMailQueue(string basePath, ILogger logger, TimeSpan? deliveredRetention = null, TimeSpan? failedRetention = null)
    {
        _queuePath = Path.Combine(basePath, "queue", "pending");
        _failedPath = Path.Combine(basePath, "queue", "failed");
        _deliveredPath = Path.Combine(basePath, "queue", "delivered");
        _corruptPath = Path.Combine(basePath, "queue", "corrupt");
        _logger = logger;

        DeliveredRetention = deliveredRetention ?? DefaultDeliveredRetention;
        FailedRetention = failedRetention ?? DefaultFailedRetention;

        Directory.CreateDirectory(_queuePath);
        Directory.CreateDirectory(_failedPath);
        Directory.CreateDirectory(_deliveredPath);
        Directory.CreateDirectory(_corruptPath);

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

        // Tidy what a crash left, then load the pending mails - before anybody asks the queue anything.
        Recover();
        Purge(Timestamp.Now);

        // Timer to periodically check for deferred mails ready for retry
        _retryTimer = new Timer(
            _ => SignalRetryCheck(),
            null,
            TimeSpan.FromMinutes(1),    // Initial delay
            TimeSpan.FromMinutes(1)     // Check every minute
        );

        // ...and to forget delivered and failed mail once their time is up.
        _purgeTimer = new Timer(
            _ => PurgeQuietly(),
            null,
            TimeSpan.FromHours(1),
            TimeSpan.FromHours(1)
        );
    }

    #region Recover()

    /// <summary>
    /// What a crash may have left: temporary files, an entry that was being delivered, an entry
    /// that reached delivered or failed while its pending one was not yet deleted. Then every
    /// mail that is due is told to whoever delivers.
    /// </summary>
    private void Recover()
    {
        foreach (var folder in new[] { _queuePath, _failedPath, _deliveredPath })
        {
            foreach (var temporary in Directory.GetFiles(folder, ".*.tmp"))
            {
                try { File.Delete(temporary); } catch { }
            }
        }

        var now = Timestamp.Now;
        var ready = new List<QueuedMail>();

        foreach (var file in Directory.GetFiles(_queuePath, "*.json"))
        {
            try
            {
                var mail = Read(file);
                if (mail is null)
                    continue;

                if (File.Exists(GetFilePath(_deliveredPath, mail.Id)) ||
                    File.Exists(GetFilePath(_failedPath, mail.Id)))
                {
                    // It was moved, but the old one not yet deleted: it is done.
                    _logger.Log(LogLevel.Warning, $"Queued mail {mail.Id} had been delivered or had failed already - not delivered again.");
                    File.Delete(file);
                    continue;
                }

                if (mail.Status == QueueItemStatus.Processing)
                {
                    // It was being delivered when the process ended: whether it arrived, nobody knows.
                    // Twice is better than never (RFC 5321 §6.1).
                    _logger.Log(LogLevel.Warning, $"Queued mail {mail.Id} was being delivered when the queue was last closed - it is tried again.");
                    mail.Status = QueueItemStatus.Pending;
                    mail.NextRetry = now;
                    WriteFile(file, Serialize(mail));
                }

                if (mail.Status is QueueItemStatus.Pending or QueueItemStatus.Deferred &&
                    mail.NextRetry <= now)
                {
                    ready.Add(mail);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _logger.Log(LogLevel.Error, $"Queue file {file} could not be taken up again: {e.Message}");
            }
        }

        if (ready.Count > 0)
            _logger.Log(LogLevel.Info, $"Loading {ready.Count} existing queued mails...");

        foreach (var mail in ready)
            _newMailChannel.Writer.TryWrite(mail);
    }

    #endregion

    public async Task EnqueueAsync(QueuedMail mail, CancellationToken ct = default)
    {
        var filePath = GetFilePath(_queuePath, mail.Id);

        await WriteFileAsync(filePath, Serialize(mail), ct);
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
                var mail = await ReadAsync(file, ct);

                if (mail is not null &&
                    mail.Status is QueueItemStatus.Pending or QueueItemStatus.Deferred &&
                    mail.NextRetry <= now)
                {
                    candidates.Add(mail);
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

                var mail = await ReadAsync(file, ct);

                if (mail is not null &&
                    mail.Status == QueueItemStatus.Deferred &&
                    mail.NextRetry <= now)
                {
                    result.Add(mail);
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
        // Pending, failed - or delivered: whoever asks how a message fared asks after that too.
        foreach (var path in new[] { _queuePath, _failedPath, _deliveredPath })
        {
            if (await ReadAsync(GetFilePath(path, id), ct) is { } mail)
                return mail;
        }

        return null;
    }

    public async Task UpdateAsync(QueuedMail mail, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var sourcePath = GetFilePath(_queuePath, mail.Id);
            string targetPath;
            byte[] content;

            switch (mail.Status)
            {
                case QueueItemStatus.Failed:
                    targetPath = GetFilePath(_failedPath, mail.Id);
                    content = Serialize(mail);
                    break;
                case QueueItemStatus.Delivered:
                    // Who it went to, where and when - not what it said.
                    targetPath = GetFilePath(_deliveredPath, mail.Id);
                    content = SerializeWithoutContent(mail);
                    break;
                default:
                    targetPath = sourcePath;
                    content = Serialize(mail);
                    break;
            }

            await WriteFileAsync(targetPath, content, ct);

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

    public async Task RemoveAsync(string id, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var filePath = GetFilePath(_queuePath, id);
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Everything that still waits to be delivered - due now or later, being delivered
    /// now, or deferred - the next first: what an administrator asks after, where
    /// <see cref="GetPendingAsync"/> is what the queue processor takes now.
    /// </summary>
    public async Task<IReadOnlyList<QueuedMail>> GetWaitingAsync(int maxItems = 1000, CancellationToken ct = default)
    {
        var result = new List<QueuedMail>();

        foreach (var file in Directory.GetFiles(_queuePath, "*.json"))
        {
            if (await ReadAsync(file, ct) is { } mail)
                result.Add(mail);
        }

        return [.. result.OrderBy(mail => mail.NextRetry).Take(maxItems)];
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
            if (await ReadAsync(file, ct) is { } mail)
                result.Add(mail);
        }

        return result;
    }

    #region GetSetAside()

    /// <summary>
    /// The files that could not be read and were set aside, the newest first: an administrator
    /// looks at them, mends or deletes them; the queue does not touch them again.
    /// </summary>
    public IReadOnlyList<string> GetSetAside()

        => [.. new DirectoryInfo(_corruptPath).
                   GetFiles().
                   OrderByDescending(file => file.LastWriteTimeUtc).
                   Select(file => file.FullName)];

    #endregion

    #region Purge(Now)

    /// <summary>
    /// Delete what was delivered longer ago than <see cref="DeliveredRetention"/> and what
    /// failed longer ago than <see cref="FailedRetention"/>. Runs when the queue opens and
    /// every hour; what was set aside stays.
    /// </summary>
    /// <param name="now">The time to count from.</param>
    /// <returns>How many entries were deleted.</returns>
    public int Purge(DateTimeOffset now)
    {
        _lock.Wait();
        try
        {
            var deleted = 0;

            foreach (var (folder, retention) in new[] { (_deliveredPath, DeliveredRetention), (_failedPath, FailedRetention) })
            {
                foreach (var file in new DirectoryInfo(folder).GetFiles("*.json"))
                {
                    // The time it was written there is the time it was delivered or failed.
                    if (now.UtcDateTime - file.LastWriteTimeUtc > retention)
                    {
                        try
                        {
                            file.Delete();
                            deleted++;
                        }
                        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                        {
                            _logger.Log(LogLevel.Warning, $"Queue file {file.FullName} could not be deleted: {e.Message}");
                        }
                    }
                }
            }

            if (deleted > 0)
                _logger.Log(LogLevel.Info, $"Forgot {deleted} delivered or failed mails older than their time in the queue.");

            return deleted;
        }
        finally
        {
            _lock.Release();
        }
    }

    private void PurgeQuietly()
    {
        try
        {
            Purge(Timestamp.Now);
        }
        catch (ObjectDisposedException)
        {
            // Closed meanwhile.
        }
        catch (Exception e)
        {
            _logger.Log(LogLevel.Warning, $"Delivered and failed mails could not be tidied: {e.Message}");
        }
    }

    #endregion

    #region (private) Reading and writing

    private static byte[] Serialize(QueuedMail mail)
        => JsonSerializer.SerializeToUtf8Bytes(mail, JsonOptions);

    /// <summary>
    /// The entry without the message: its text and its octets are gone, the envelope and how
    /// it fared remain.
    /// </summary>
    private static byte[] SerializeWithoutContent(QueuedMail mail)
    {
        var json = JsonSerializer.SerializeToNode(mail, JsonOptions)!.AsObject();

        json[nameof(QueuedMail.MessageContent)] = "";
        json.Remove(nameof(QueuedMail.MessageOctets));

        return JsonSerializer.SerializeToUtf8Bytes(json, JsonOptions);
    }

    /// <summary>
    /// The entry in the given file; null if there is none (any more) or it cannot be read -
    /// then it is set aside.
    /// </summary>
    private async Task<QueuedMail?> ReadAsync(string file, CancellationToken ct)
    {
        string json;

        try
        {
            // Shared for writing and deleting, so that the entry can be replaced or moved meanwhile.
            await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            json = await reader.ReadToEndAsync(ct);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            // Delivered, moved or taken out just now.
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _logger.Log(LogLevel.Error, $"Queue file {file} cannot be read now: {e.Message}");
            return null;
        }

        return Parse(file, json);
    }

    private QueuedMail? Read(string file)
    {
        string json;

        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            json = reader.ReadToEnd();
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _logger.Log(LogLevel.Error, $"Queue file {file} cannot be read now: {e.Message}");
            return null;
        }

        return Parse(file, json);
    }

    private QueuedMail? Parse(string file, string json)
    {
        try
        {
            return JsonSerializer.Deserialize<QueuedMail>(json, JsonOptions)
                       ?? throw new JsonException("The file holds no queue entry.");
        }
        catch (Exception e)
        {
            SetAside(file, e.Message);
            return null;
        }
    }

    /// <summary>
    /// A file that is no queue entry - broken by a crash before entries were written atomically,
    /// by the disk, or by hand - goes where nothing reads it, and the log says so loudly: the mail
    /// in it is not delivered.
    /// </summary>
    private void SetAside(string file, string reason)
    {
        var folder = Path.GetFileName(Path.GetDirectoryName(file)) ?? "queue";
        var target = Path.Combine(_corruptPath, $"{Timestamp.Now.UtcDateTime:yyyyMMddTHHmmssZ}-{folder}-{Path.GetFileName(file)}");

        try
        {
            if (File.Exists(target))
                target = Path.Combine(_corruptPath, $"{Timestamp.Now.UtcDateTime:yyyyMMddTHHmmssZ}-{folder}-{Guid.NewGuid():N}-{Path.GetFileName(file)}");

            File.Move(file, target);

            _logger.Log(LogLevel.Error, $"Queue file {file} cannot be read ({reason}) - set aside as {target}. The mail in it is not delivered until somebody mends it and puts it back.");
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            // Replaced or moved meanwhile.
        }
        catch (Exception e)
        {
            _logger.Log(LogLevel.Error, $"Queue file {file} cannot be read ({reason}) and could not be set aside: {e.Message}");
        }
    }

    /// <summary>
    /// The entry goes to a temporary file beside it, is flushed to the disk, and then renamed
    /// over the old one: it is there as it was or as it is now, never half.
    /// </summary>
    private static async Task WriteFileAsync(string path, byte[] content, CancellationToken ct)
    {
        var temporary = TemporaryPathFor(path);

        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                await stream.WriteAsync(content, ct);
                await stream.FlushAsync(ct);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            try { File.Delete(temporary); } catch { }
            throw;
        }
    }

    private static void WriteFile(string path, byte[] content)
    {
        var temporary = TemporaryPathFor(path);

        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            try { File.Delete(temporary); } catch { }
            throw;
        }
    }

    // Starts with a dot and ends in .tmp: no "*.json" ever lists it.
    private static string TemporaryPathFor(string path)
        => Path.Combine(Path.GetDirectoryName(path) ?? ".", $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

    #endregion

    private static string GetFilePath(string folder, string id)
    {
        var safeId = string.Join("_", id.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(folder, $"{safeId}.json");
    }

    public void Dispose()
    {
        _retryTimer.Dispose();
        _purgeTimer.Dispose();
        _newMailChannel.Writer.Complete();
        _retryCheckChannel.Writer.Complete();
        _lock.Dispose();
    }
}
