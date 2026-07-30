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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP3;

/// <summary>
/// Sends the datagrams of a pump pass in as few syscalls as possible (UDP batching, phase 9). On
/// <b>Linux</b>, equal-sized datagrams are combined via <c>UDP_SEGMENT</c> (GSO) into ONE
/// <c>sendmsg</c> — the kernel splits them back into individual packets. On all other platforms
/// (incl. Windows) the sender falls back to a lean single-send loop that reuses the
/// <see cref="GsoBatcher"/> work buffer and the destination address.
/// Both are identical on the wire — GSO only saves syscalls, not bytes.
/// </summary>
internal sealed class UdpBatchSender
{
    // Linux: SOL_UDP = 17, UDP_SEGMENT = 103 (from <netinet/udp.h> / <linux/udp.h>).
    private const SocketOptionLevel SolUdp = (SocketOptionLevel)17;
    private const SocketOptionName UdpSegment = (SocketOptionName)103;

    private readonly GsoBatcher _batcher = new();
    private readonly bool _gsoSupported = OperatingSystem.IsLinux();
    private bool _gsoDisabled; // set when the kernel rejects GSO after all (then permanent fallback)

    /// <summary>
    /// Sends all <paramref name="datagrams"/> over the (connected or unconnected)
    /// <paramref name="socket"/>. When <paramref name="remote"/> is <c>null</c>, the socket counts
    /// as connected (client); otherwise the address is sent to (server).
    /// </summary>
    public void Send(Socket socket, IReadOnlyList<byte[]> datagrams, EndPoint? remote)
    {
        if (datagrams.Count == 0)
            return;

        // Without GSO: a plain single-send loop (no intermediate copy).
        if (!_gsoSupported || _gsoDisabled)
        {
            foreach (byte[] datagram in datagrams)
                SendOne(socket, datagram, datagram.Length, remote);
            return;
        }

        _batcher.Batch(datagrams, batch =>
        {
            if (batch.SegmentCount <= 1)
            {
                SendOne(socket, batch.Buffer, batch.Length, remote);
                return;
            }
            try
            {
                socket.SetRawSocketOption((int)SolUdp, (int)UdpSegment, BitConverter.GetBytes(batch.SegmentSize));
                SendOne(socket, batch.Buffer, batch.Length, remote);
                socket.SetRawSocketOption((int)SolUdp, (int)UdpSegment, BitConverter.GetBytes(0)); // GSO off again
            }
            catch (SocketException)
            {
                // Kernel/NIC rejects GSO ⇒ fall back permanently to single sends and catch up this
                // batch segment by segment (byte-identical to the GSO result).
                _gsoDisabled = true;
                for (int offset = 0; offset < batch.Length; offset += batch.SegmentSize)
                {
                    int len = Math.Min(batch.SegmentSize, batch.Length - offset);
                    SendOne(socket, batch.Buffer.AsSpan(offset, len).ToArray(), len, remote);
                }
            }
        });
    }

    private static void SendOne(Socket socket, byte[] buffer, int length, EndPoint? remote)
    {
        if (remote is null)
            socket.Send(buffer, 0, length, SocketFlags.None);
        else
            socket.SendTo(buffer, 0, length, SocketFlags.None, remote);
    }
}
