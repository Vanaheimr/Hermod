/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using System.Buffers.Binary;
using System.IO.Compression;

using HTTP2WS = org.GraphDefined.Vanaheimr.Hermod.HTTP2;
using HTTP3WS = org.GraphDefined.Vanaheimr.Hermod.HTTP3;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// A tunnel that hands over the chunks the test fed it, and fails a read past
    /// them, rather than waiting: a WebSocket that reads a payload it should have
    /// refused fails the test, and as no read ever waits, a receive runs to its end
    /// on the test's thread. Implements the tunnel interface of both copies of the
    /// WebSocket framing, which are the same two methods.
    /// </summary>
    internal sealed class ScriptedTunnel : HTTP2WS.IHTTP2Tunnel, HTTP3WS.IHTTP2Tunnel
    {

        private readonly Queue<Byte[]?> chunks   = new();
        private readonly List<Byte[]>   written  = [];

        public void Feed(params Byte[][] Chunks)
        {
            foreach (var chunk in Chunks)
                chunks.Enqueue(chunk);
        }

        /// <summary>
        /// The peer ends its side: the read after the chunks fed so far reads null.
        /// </summary>
        public void End()

            => chunks.Enqueue(null);

        public Task<Byte[]?> ReadAsync(CancellationToken CancellationToken)

            => chunks.TryDequeue(out var chunk)
                   ? Task.FromResult(chunk)
                   : throw new InvalidOperationException("The WebSocket read on, past every byte the test fed it");

        public Task WriteAsync(Byte[] Data, CancellationToken CancellationToken)
        {
            written.Add(Data);
            return Task.CompletedTask;
        }

        /// <summary>
        /// The status code of the Close frame our end sent, null if it sent none.
        /// Our end writes each frame whole, in one write, masked when it is a client
        /// and unmasked when it is a server (RFC 6455 Section 5.3), which the frame's
        /// mask bit says.
        /// </summary>
        public UInt16? CloseCode
        {
            get
            {

                foreach (var frame in written)
                {

                    if ((frame[0] & 0x0F) != WebSocketScript.Close)
                        continue;

                    var masked  = (frame[1] & 0x80) != 0;
                    var length  =  frame[1] & 0x7F;   // a control frame's is at most 125
                    var start   = masked ? 6 : 2;

                    if (length < 2)
                        return null;

                    var code    = new Byte[2];
                    for (var i = 0; i < 2; i++)
                        code[i] = (Byte) (frame[start + i] ^ (masked ? frame[2 + i] : 0));

                    return BinaryPrimitives.ReadUInt16BigEndian(code);

                }

                return null;

            }
        }

    }


    /// <summary>
    /// Driving either copy of the WebSocket framing, HTTP/2's or HTTP/3's, in either
    /// role, over a <see cref="ScriptedTunnel"/>, with frames built by hand.
    /// </summary>
    internal static class WebSocketScript
    {

        public const Byte Continuation  = 0x0;
        public const Byte Text          = 0x1;
        public const Byte Binary        = 0x2;
        public const Byte Close         = 0x8;
        public const Byte Ping          = 0x9;

        /// <summary>
        /// Both copies, each in both roles.
        /// </summary>
        public static IEnumerable<TestCaseData> Ends()
        {
            foreach (var stack in new[] { "HTTP/2", "HTTP/3" })
                foreach (var client in new[] { false, true })
                    yield return new TestCaseData(stack, client).SetArgDisplayNames(stack, client ? "client" : "server");
        }

        /// <summary>
        /// Our end of a WebSocket over the tunnel, of the copy the stack names, in
        /// the client's role or the server's: a function that receives the next
        /// message, its payload, or null once the connection is closed.
        /// </summary>
        public static Func<Task<Byte[]?>> OurEnd(String          Stack,
                                                 Boolean         Client,
                                                 ScriptedTunnel  Tunnel,
                                                 UInt64          MaxMessageSize,
                                                 Boolean         PerMessageDeflate = false)
        {

            if (Stack == "HTTP/2")
            {
                var webSocket = new HTTP2WS.WebSocketConnection(Tunnel, Client ? HTTP2WS.WebSocketRole.Client : HTTP2WS.WebSocketRole.Server, PerMessageDeflate, MaxMessageSize);
                return async () => (await webSocket.ReceiveAsync(CancellationToken.None))?.Payload;
            }

            else
            {
                var webSocket = new HTTP3WS.WebSocketConnection(Tunnel, Client ? HTTP3WS.WebSocketRole.Client : HTTP3WS.WebSocketRole.Server, PerMessageDeflate, MaxMessageSize);
                return async () => (await webSocket.ReceiveAsync(CancellationToken.None))?.Payload;
            }

        }

        /// <summary>
        /// Receive once: the message, the Close our end sent, and what this thread
        /// allocated meanwhile. No read of the tunnel waits, so the receive runs
        /// to its end right here, and that count is the receive's.
        /// </summary>
        public static (Byte[]? Message, UInt16? CloseCode, Int64 Allocated) Receive(Func<Task<Byte[]?>>  ReceiveAsync,
                                                                                      ScriptedTunnel        Tunnel)
        {

            var before     = GC.GetAllocatedBytesForCurrentThread();
            var receiving  = ReceiveAsync();
            var allocated  = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(receiving.IsCompleted, Is.True, "the receive, run to its end without waiting");

            return (receiving.GetAwaiter().GetResult(), Tunnel.CloseCode, allocated);

        }

        /// <summary>
        /// The header of a frame from the peer of our end, announcing a payload of
        /// the given length; with a masking key when the peer is a client, which
        /// it is when our end is a server.
        /// </summary>
        public static Byte[] Header(Boolean Fin, Byte Opcode, Int64 Length, Boolean Masked, Boolean Rsv1 = false)
        {

            var header   = new List<Byte> { (Byte) ((Fin ? 0x80 : 0) | (Rsv1 ? 0x40 : 0) | Opcode) };
            var maskBit  = Masked ? 0x80 : 0x00;

            if (Length <= 125)
                header.Add((Byte) (maskBit | (Int32) Length));

            else if (Length <= 65535)
            {
                header.Add((Byte) (maskBit | 126));
                header.Add((Byte) (Length >> 8));
                header.Add((Byte)  Length);
            }

            else
            {
                var length = new Byte[8];
                BinaryPrimitives.WriteUInt64BigEndian(length, (UInt64) Length);
                header.Add((Byte) (maskBit | 127));
                header.AddRange(length);
            }

            if (Masked)
                header.AddRange(MaskingKey);

            return [.. header];

        }

        private static readonly Byte[] MaskingKey = [0x37, 0xFA, 0x21, 0x3D];

        /// <summary>
        /// A whole frame from the peer of our end.
        /// </summary>
        public static Byte[] Frame(Boolean Fin, Byte Opcode, Byte[] Payload, Boolean Masked, Boolean Rsv1 = false)
        {

            var header  = Header(Fin, Opcode, Payload.Length, Masked, Rsv1);
            var frame   = new Byte[header.Length + Payload.Length];

            header.CopyTo(frame, 0);

            for (var i = 0; i < Payload.Length; i++)
                frame[header.Length + i] = (Byte) (Payload[i] ^ (Masked ? MaskingKey[i % 4] : 0));

            return frame;

        }

        /// <summary>
        /// Raw DEFLATE without the 4-octet tail a sender strips (RFC 7692 Section 7.2.1).
        /// </summary>
        public static Byte[] Deflate(Byte[] Data)
        {

            using var output = new MemoryStream();

            using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
                deflate.Write(Data, 0, Data.Length);

            var bytes = output.ToArray();

            return bytes.Length >= 4 && bytes[^4] == 0x00 && bytes[^3] == 0x00 && bytes[^2] == 0xFF && bytes[^1] == 0xFF
                       ? bytes[..^4]
                       : bytes;

        }

        public static Byte[] Filled(Int32 Length, Byte Value)
        {
            var bytes = new Byte[Length];
            Array.Fill(bytes, Value);
            return bytes;
        }

    }

}
