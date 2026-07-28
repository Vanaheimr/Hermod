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

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;

/// <summary>
/// A GSO batch (generic segmentation offload): one contiguous buffer of multiple equal-sized
/// UDP payloads, which the kernel splits via <c>UDP_SEGMENT</c> in a single send call into
/// <see cref="SegmentCount"/> datagrams of size <see cref="SegmentSize"/> (the last segment may
/// be smaller). <see cref="Buffer"/>/<see cref="Length"/> are a slice of a pooled work buffer —
/// only valid until the next batch.
/// </summary>
public readonly struct GsoBatch(byte[] buffer, int length, int segmentSize, int segmentCount)
{
    public byte[] Buffer { get; } = buffer;
    public int Length { get; } = length;
    public int SegmentSize { get; } = segmentSize;
    public int SegmentCount { get; } = segmentCount;
}

/// <summary>
/// Groups outgoing datagrams into GSO batches (phase-9 UDP batching). In a bulk transfer QUIC
/// sends one ~MTU datagram per 1-RTT packet; GSO combines several of them into ONE
/// <c>sendmsg</c>. The kernel's precondition: all segments of a send are equal-sized — only the
/// LAST may be smaller. This batcher forms exactly such runs (maximal prefix of equal size,
/// optionally plus one smaller final segment), capped at <see cref="MaxSegments"/> segments and
/// <see cref="MaxBatchBytes"/> bytes. The actual kernel wiring is platform-specific; this
/// splitting is pure and therefore deterministically testable.
/// </summary>
public sealed class GsoBatcher
{
    /// <summary>
    /// Maximum number of segments per GSO send (Linux allows up to 64 with UDP_SEGMENT).
    /// </summary>
    public const int MaxSegments = 64;

    /// <summary>
    /// Upper bound of the total size of a batch (a UDP payload holds at most 65535 bytes).
    /// </summary>
    public const int MaxBatchBytes = 65535;

    private byte[] _work = new byte[MaxSegments * 1500];

    /// <summary>
    /// Splits <paramref name="datagrams"/> into consecutive GSO batches. Each batch is handed back
    /// via <paramref name="onBatch"/> BEFORE the next one is formed (the work buffer is reused) —
    /// so the callback must send/copy the batch immediately. A batch with
    /// <see cref="GsoBatch.SegmentCount"/> == 1 is an ordinary single datagram.
    /// </summary>
    public void Batch(IReadOnlyList<byte[]> datagrams, Action<GsoBatch> onBatch)
    {
        int i = 0;
        int n = datagrams.Count;
        while (i < n)
        {
            int segmentSize = datagrams[i].Length;
            int count = 0;
            int bytes = 0;

            // Maximal run of equal-sized datagrams (the last may be smaller).
            while (i + count < n && count < MaxSegments)
            {
                int len = datagrams[i + count].Length;
                if (len > segmentSize)
                    break; // larger datagram ⇒ new batch
                if (bytes + len > MaxBatchBytes)
                    break;
                bytes += len;
                count++;
                if (len < segmentSize)
                    break; // a smaller datagram MUST be the last segment (UDP_SEGMENT rule)
            }

            EnsureWork(bytes);
            int offset = 0;
            for (int k = 0; k < count; k++)
            {
                byte[] dg = datagrams[i + k];
                dg.CopyTo(_work.AsSpan(offset));
                offset += dg.Length;
            }
            onBatch(new GsoBatch(_work, bytes, segmentSize, count));
            i += count;
        }
    }

    private void EnsureWork(int needed)
    {
        if (_work.Length < needed)
            _work = new byte[Math.Max(needed, _work.Length * 2)];
    }
}
