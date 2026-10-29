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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP3;

/// <summary>
/// Sends the datagrams of a pump pass in as few syscalls as possible (UDP batching, phase 9).
/// Equal-sized datagrams are combined into ONE send that the kernel splits back into individual
/// packets: via <c>UDP_SEGMENT</c> (GSO) on <b>Linux</b>, via <c>UDP_SEND_MSG_SIZE</c> (USO) on
/// <b>Windows</b>. Elsewhere, or where the kernel refuses, it is a plain single-send loop. Identical
/// on the wire either way: batching saves syscalls, not bytes.
/// </summary>
/// <remarks>
/// <para>On Windows loopback a datagram sent on its own cost ~9.6 µs, ~6 µs in batches of four and
/// ~4 µs in batches of sixteen. HTTP3ConformanceTests' demo host spent a third of its thread in
/// SendTo at 32 responses in flight, and seven datagrams in ten left in passes of five or more.</para>
/// <para>The segment size is a socket option, not a per-send one (.NET passes no control messages),
/// so it is set before a batch and cleared after. Anything else sent on the socket in between would
/// inherit it, and a datagram larger than the segment size would leave in pieces. Every pass
/// therefore holds a lock per socket - <c>Http3Server</c>'s parallel workers share theirs.</para>
/// </remarks>
public sealed class UdpBatchSender
{
    // Linux:   SOL_UDP = 17,     UDP_SEGMENT = 103      (<linux/udp.h>)
    // Windows: IPPROTO_UDP = 17, UDP_SEND_MSG_SIZE = 2  (<ws2ipdef.h>, Windows 10 2004 and later)
    private const int UdpLevel = 17;
    private static readonly int SegmentSizeOption = OperatingSystem.IsWindows() ? 2 : 103;
    private static readonly bool SegmentationSupported = OperatingSystem.IsLinux() || OperatingSystem.IsWindows();
    private static readonly ConditionalWeakTable<Socket, object> SendLocks = new();

    private readonly GsoBatcher _batcher = new();
    private bool _segmentationDisabled; // the kernel refused once: single sends from then on

    /// <summary>
    /// Sends all <paramref name="datagrams"/> over the (connected or unconnected)
    /// <paramref name="socket"/>. When <paramref name="remote"/> is <c>null</c>, the socket counts
    /// as connected (client); otherwise the address is sent to (server).
    /// </summary>
    public void Send(Socket socket, IReadOnlyList<byte[]> datagrams, EndPoint? remote)
    {
        if (datagrams.Count == 0)
            return;

        // No platform segmentation: nobody ever sets the option, so nothing to guard against.
        if (!SegmentationSupported)
        {
            foreach (byte[] datagram in datagrams)
                SendOne(socket, datagram, 0, datagram.Length, remote);
            return;
        }

        lock (SendLocks.GetValue(socket, _ => new object()))
        {
            if (_segmentationDisabled)
            {
                foreach (byte[] datagram in datagrams)
                    SendOne(socket, datagram, 0, datagram.Length, remote);
                return;
            }
            _batcher.Batch(datagrams, batch => SendBatch(socket, batch, remote));
        }
    }

    private void SendBatch(Socket socket, GsoBatch batch, EndPoint? remote)
    {
        if (batch.SegmentCount <= 1 || _segmentationDisabled)
        {
            SendSegments(socket, batch, remote);
            return;
        }

        bool sent = false;
        try
        {
            SetSegmentSize(socket, batch.SegmentSize);
            try
            {
                SendOne(socket, batch.Buffer, 0, batch.Length, remote);
                sent = true;
            }
            finally
            {
                // Always cleared, or the next single datagram larger than this size leaves in pieces.
                try { SetSegmentSize(socket, 0); }
                catch (SocketException) { _segmentationDisabled = true; }
            }
        }
        catch (SocketException) when (!sent)
        {
            // The kernel or the interface refuses segmentation after all: single sends from now on,
            // starting with this batch, byte-identical to what segmentation would have sent.
            _segmentationDisabled = true;
            SendSegments(socket, batch, remote);
        }
    }

    private static void SendSegments(Socket socket, GsoBatch batch, EndPoint? remote)
    {
        for (int offset = 0; offset < batch.Length; offset += batch.SegmentSize)
            SendOne(socket, batch.Buffer, offset, Math.Min(batch.SegmentSize, batch.Length - offset), remote);
    }

    private static void SetSegmentSize(Socket socket, int segmentSize)
    {
        Span<byte> value = stackalloc byte[sizeof(int)];
        MemoryMarshal.Write(value, in segmentSize); // an int on Linux, a DWORD on Windows: native order both
        socket.SetRawSocketOption(UdpLevel, SegmentSizeOption, value);
    }

    private static void SendOne(Socket socket, byte[] buffer, int offset, int length, EndPoint? remote)
    {
        if (remote is null)
            socket.Send(buffer, offset, length, SocketFlags.None);
        else
            socket.SendTo(buffer, offset, length, SocketFlags.None, remote);
    }
}
