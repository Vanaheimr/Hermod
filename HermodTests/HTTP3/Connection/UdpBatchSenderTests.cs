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

using System.Net;
using System.Net.Sockets;

using org.GraphDefined.Vanaheimr.Hermod.HTTP3;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.Connection;

/// <summary>
/// Batched sends over real loopback sockets - USO on Windows, GSO on Linux, single sends elsewhere -
/// must arrive as exactly the datagrams that were handed over.
/// </summary>
[TestFixture]
public class UdpBatchSenderTests
{
    [Test]
    public void MixedSizes_ArriveAsTheSameDatagrams()
    {
        // Runs of equal size become one segmented send, a smaller datagram closes a run, a larger one
        // starts the next; each datagram carries its own index so a split or a merge shows.
        int[] sizes = [.. Enumerable.Repeat(1200, 10), 800, .. Enumerable.Repeat(1252, 5), 300, 300, 1300, 1300, 40, 1200];
        List<byte[]> datagrams = [.. sizes.Select((size, index) => Datagram(size, tag: index))];

        using var receiver = Receiver(out IPEndPoint to);
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(System.Net.IPAddress.Loopback, 0));

        new UdpBatchSender().Send(socket, datagrams, to);

        List<byte[]> received = ReceiveAll(receiver, datagrams.Count);
        Assert.That(received.Select(d => d.Length), Is.EqualTo(sizes), "the same sizes, in order");
        for (int i = 0; i < datagrams.Count; i++)
            Assert.That(received[i], Is.EqualTo(datagrams[i]), $"datagram {i} byte for byte");

        // A single datagram after a batch: the segment size must have been cleared, or this one
        // (larger than the 1200 the last batch used) would leave in two pieces.
        new UdpBatchSender().Send(socket, [Datagram(1400, tag: 99)], to);
        Assert.That(ReceiveAll(receiver, 1)[0].Length, Is.EqualTo(1400));
    }

    [Test]
    public void SendersSharingOneSocket_NeverSplitEachOthersDatagrams()
    {
        // The segment size is a socket option. Four workers, each with its own sender as
        // Http3Server's parallel workers have, send batches of four different sizes over ONE socket.
        // Without the per-socket lock, one worker's option applies to another's send. Its batch then
        // leaves as one oversized datagram, or its single datagram leaves in pieces. Either way a
        // datagram arrives at a size no worker sent, and that is what this checks.
        //
        // Not that every datagram arrives. A receive buffer is the system's to size - Linux caps it
        // at net.core.rmem_max - and loopback drops what does not fit. A drop is no split, so the
        // count is only checked for being large enough to mean something.
        const int workers = 4, batches = 60, perBatch = 8;
        using var receiver = Receiver(out IPEndPoint to);
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(System.Net.IPAddress.Loopback, 0));

        var received = new List<byte[]>();
        bool sent = false;
        receiver.ReceiveTimeout = 300;
        Task reading = Task.Run(() =>
        {
            // Reads alongside the senders; stops at the first quiet spell after they are done.
            var buffer = new byte[65536];
            while (true)
            {
                try { received.Add(buffer.AsSpan(0, receiver.Receive(buffer)).ToArray()); }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.TimedOut)
                {
                    if (Volatile.Read(ref sent))
                        return;
                }
            }
        });

        Parallel.For(0, workers, new ParallelOptions { MaxDegreeOfParallelism = workers }, w =>
        {
            var sender = new UdpBatchSender();
            int size = 1000 + 100 * w;
            for (int b = 0; b < batches; b++)
                sender.Send(socket, [.. Enumerable.Range(0, perBatch).Select(_ => Datagram(size, tag: w))], to);
        });
        Volatile.Write(ref sent, true);
        reading.Wait();

        var wrong = received.Where(d => d[0] >= workers || d.Length != 1000 + 100 * d[0]).ToList();
        Assert.That(wrong, Is.Empty,
                    $"{wrong.Count} of {received.Count} datagrams arrived at a size no worker sent, e.g. {wrong.FirstOrDefault()?.Length} bytes");
        Assert.That(received.Count, Is.GreaterThan(workers * batches * perBatch / 10),
                    "too few arrived to say anything");
    }

    private static byte[] Datagram(int size, int tag)
    {
        var datagram = new byte[size];
        datagram[0] = (byte) tag;
        for (int i = 1; i < size; i++)
            datagram[i] = (byte) (i * 7 + tag);
        return datagram;
    }

    private static Socket Receiver(out IPEndPoint endpoint)
    {
        var receiver = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
        {
            ReceiveBufferSize = 8 * 1024 * 1024,
            ReceiveTimeout    = 2000,
        };
        receiver.Bind(new IPEndPoint(System.Net.IPAddress.Loopback, 0));
        endpoint = (IPEndPoint) receiver.LocalEndPoint!;
        return receiver;
    }

    private static List<byte[]> ReceiveAll(Socket receiver, int expected)
    {
        var received = new List<byte[]>();
        var buffer = new byte[65536];
        try
        {
            while (received.Count < expected)
                received.Add(buffer.AsSpan(0, receiver.Receive(buffer)).ToArray());
        }
        catch (SocketException e) when (e.SocketErrorCode == SocketError.TimedOut)
        {
            Assert.Fail($"{received.Count} of {expected} datagrams arrived");
        }
        return received;
    }
}
