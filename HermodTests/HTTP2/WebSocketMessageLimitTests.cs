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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

using HTTP2WS = org.GraphDefined.Vanaheimr.Hermod.HTTP2;
using HTTP3WS = org.GraphDefined.Vanaheimr.Hermod.HTTP3;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// How big a message the WebSocket over HTTP/2 and HTTP/3 takes in.
    ///
    /// Each frame was capped at 16 MiB, and nothing else was. The fragments of a
    /// message piled up without a bound, so a chain of continuation frames grew a
    /// buffer for as long as the peer kept sending, and a compressed message was
    /// inflated in one go, so a few KiB of DEFLATE that inflate to gigabytes (a
    /// "deflate bomb") were inflated to gigabytes. The HTTP/1.1 stack bounds a
    /// message at 64 MiB and closes with 1009 ("message too big") past it.
    ///
    /// Now the WebSocket has <c>MaxMessageSize</c>, 64 MiB by default, and fails
    /// the connection with 1009 as early as the bytes tell: at the header of the
    /// frame that would take the message past it, and while inflating, at the
    /// first byte past it.
    ///
    /// Run against both copies of the framing, HTTP/2's and HTTP/3's, in both
    /// roles: a server's end, which takes masked frames, and a client's, which
    /// takes unmasked ones. In memory: the tunnel hands over what the test fed it
    /// and fails a read past that, so a WebSocket that reads a payload it should
    /// have refused on its header fails the test, and as no read ever waits, a
    /// receive runs to its end on the test's thread, whose allocations are then
    /// the receive's alone.
    /// </summary>
    [TestFixture]
    public class WebSocketMessageLimitTests
    {

        #region Data

        private const Byte    Continuation  = 0x0;
        private const Byte    Text          = 0x1;
        private const Byte    Binary        = 0x2;
        private const Byte    Close         = 0x8;

        private const UInt16  MessageTooBig = 1009;

        /// <summary>
        /// The limit the tests set: small, so that going past it is cheap, and
        /// still big enough to tell a bomb that inflated from one that did not.
        /// </summary>
        private const Int32   Limit         = 1024 * 1024;

        /// <summary>
        /// Zeros, 64 times the limit, deflated: about 64 KiB that inflate to 64 MiB.
        /// </summary>
        private static readonly Lazy<Byte[]> DeflateBomb = new (() => Deflate(new Byte[64 * Limit]));

        #endregion

        #region (helpers)

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
        /// A tunnel that hands over the chunks the test fed it, and fails a read
        /// past them, rather than waiting. Implements the tunnel interface of both
        /// copies, which are the same two methods.
        /// </summary>
        private sealed class ScriptedTunnel : HTTP2WS.IHTTP2Tunnel, HTTP3WS.IHTTP2Tunnel
        {

            private readonly Queue<Byte[]> chunks   = new();
            private readonly List<Byte[]>  written  = [];

            public void Feed(params Byte[][] Chunks)
            {
                foreach (var chunk in Chunks)
                    chunks.Enqueue(chunk);
            }

            public Task<Byte[]?> ReadAsync(CancellationToken CancellationToken)

                => chunks.TryDequeue(out var chunk)
                       ? Task.FromResult<Byte[]?>(chunk)
                       : throw new InvalidOperationException("The WebSocket read on, past every byte the test fed it");

            public Task WriteAsync(Byte[] Data, CancellationToken CancellationToken)
            {
                written.Add(Data);
                return Task.CompletedTask;
            }

            /// <summary>
            /// The status code of the Close frame our end sent, null if it sent none.
            /// Our end writes each frame whole, in one write, and masks it as a client
            /// (RFC 6455 Section 5.3).
            /// </summary>
            public UInt16? CloseCode
            {
                get
                {

                    foreach (var frame in written)
                    {

                        if ((frame[0] & 0x0F) != Close)
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
        /// Our end of a WebSocket over the tunnel, of the copy the stack names, in
        /// the client's role or the server's: a function that receives the next
        /// message, its payload, or null once the connection is closed.
        /// </summary>
        private static Func<Task<Byte[]?>> OurEnd(String          Stack,
                                                  Boolean         Client,
                                                  ScriptedTunnel  Tunnel,
                                                  Boolean         PerMessageDeflate = false)
        {

            if (Stack == "HTTP/2")
            {
                var webSocket = new HTTP2WS.WebSocketConnection(Tunnel, Client ? HTTP2WS.WebSocketRole.Client : HTTP2WS.WebSocketRole.Server, PerMessageDeflate, Limit);
                return async () => (await webSocket.ReceiveAsync(CancellationToken.None))?.Payload;
            }

            else
            {
                var webSocket = new HTTP3WS.WebSocketConnection(Tunnel, Client ? HTTP3WS.WebSocketRole.Client : HTTP3WS.WebSocketRole.Server, PerMessageDeflate, Limit);
                return async () => (await webSocket.ReceiveAsync(CancellationToken.None))?.Payload;
            }

        }

        /// <summary>
        /// Receive once: the message, the Close our end sent, and what this thread
        /// allocated meanwhile. No read of the tunnel waits, so the receive runs
        /// to its end right here, and that count is the receive's.
        /// </summary>
        private static (Byte[]? Message, UInt16? CloseCode, Int64 Allocated) Receive(Func<Task<Byte[]?>>  ReceiveAsync,
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
        private static Byte[] Header(Boolean Fin, Byte Opcode, Int64 Length, Boolean Masked, Boolean Rsv1 = false)
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
        private static Byte[] Frame(Boolean Fin, Byte Opcode, Byte[] Payload, Boolean Masked, Boolean Rsv1 = false)
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
        private static Byte[] Deflate(Byte[] Data)
        {

            using var output = new MemoryStream();

            using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
                deflate.Write(Data, 0, Data.Length);

            var bytes = output.ToArray();

            return bytes.Length >= 4 && bytes[^4] == 0x00 && bytes[^3] == 0x00 && bytes[^2] == 0xFF && bytes[^1] == 0xFF
                       ? bytes[..^4]
                       : bytes;

        }

        private static Byte[] Filled(Int32 Length, Byte Value)
        {
            var bytes = new Byte[Length];
            Array.Fill(bytes, Value);
            return bytes;
        }

        #endregion


        #region TheLimit_Is64MiB_AsOnTheHTTP11Stack()

        /// <summary>
        /// Unless the constructor or <see cref="HTTP2ClientConnection.OpenWebSocketAsync"/>
        /// is told otherwise, a message may have 64 MiB, as on the HTTP/1.1 stack
        /// (<see cref="WebSocketFrame.DefaultMaxPayloadSize"/>).
        /// </summary>
        [Test]
        public void TheLimit_Is64MiB_AsOnTheHTTP11Stack()
        {

            var tunnel = new ScriptedTunnel();

            Assert.Multiple(() =>
            {

                Assert.That(WebSocketFrame.DefaultMaxPayloadSize,                                         Is.EqualTo(64UL * 1024 * 1024),                  "the HTTP/1.1 stack's");

                Assert.That(HTTP2WS.WebSocketConnection.DefaultMaxMessageSize,                            Is.EqualTo(WebSocketFrame.DefaultMaxPayloadSize), "HTTP/2's default");
                Assert.That(HTTP3WS.WebSocketConnection.DefaultMaxMessageSize,                            Is.EqualTo(WebSocketFrame.DefaultMaxPayloadSize), "HTTP/3's default");

                Assert.That(new HTTP2WS.WebSocketConnection(tunnel).MaxMessageSize,                       Is.EqualTo(WebSocketFrame.DefaultMaxPayloadSize), "HTTP/2, by default");
                Assert.That(new HTTP3WS.WebSocketConnection(tunnel).MaxMessageSize,                       Is.EqualTo(WebSocketFrame.DefaultMaxPayloadSize), "HTTP/3, by default");

                Assert.That(new HTTP2WS.WebSocketConnection(tunnel, MaxMessageSize: 4096).MaxMessageSize, Is.EqualTo(4096),                                "HTTP/2, as told");
                Assert.That(new HTTP3WS.WebSocketConnection(tunnel, MaxMessageSize: 4096).MaxMessageSize, Is.EqualTo(4096),                                "HTTP/3, as told");

            });

        }

        #endregion

        #region OpenWebSocketAsync_HandsTheLimitToTheWebSocket()

        /// <summary>
        /// An HTTP/2 client's WebSocket comes from
        /// <see cref="HTTP2ClientConnection.OpenWebSocketAsync"/>, not from the
        /// constructor, so the limit has to come through there.
        /// </summary>
        [Test]
        public async Task OpenWebSocketAsync_HandsTheLimitToTheWebSocket()
        {

            await using var transport  = new HoldingH2Transport(HoldHeadersOf: streamId => false);
            await using var wire       = new ClientWire(transport);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());

            var told        = await OpenWebSocketAsync(transport, wire, connection, 1, "/told",    MaxMessageSize: 4096);
            var untold      = await OpenWebSocketAsync(transport, wire, connection, 3, "/untold");

            Assert.Multiple(() =>
            {
                Assert.That(told.  MaxMessageSize,  Is.EqualTo(4096),                                               "as told");
                Assert.That(untold.MaxMessageSize,  Is.EqualTo(HTTP2WS.WebSocketConnection.DefaultMaxMessageSize),  "by default");
            });

            await connection.CloseAsync();

        }

        /// <summary>
        /// Open a WebSocket (RFC 8441), and answer its CONNECT with a 200.
        /// </summary>
        private static async Task<HTTP2WS.WebSocketConnection> OpenWebSocketAsync(HoldingH2Transport     Transport,
                                                                             ClientWire             Wire,
                                                                             HTTP2ClientConnection  Connection,
                                                                             UInt32                 StreamId,
                                                                             String                 Path,
                                                                             UInt64?                MaxMessageSize = null)
        {

            var opening = MaxMessageSize.HasValue
                              ? Connection.OpenWebSocketAsync("localhost", URIScheme.https, Path, MaxMessageSize: MaxMessageSize.Value)
                              : Connection.OpenWebSocketAsync("localhost", URIScheme.https, Path);

            await Wire.UntilAsync(lines => lines.Contains($"{StreamId} HEADERS CONNECT {Path}"), "the CONNECT");

            await Transport.SendAsync(HTTP2Frame.CreateHeaders(StreamId,
                                                               new HPACKEncoder().EncodeHeaderBlock([(":status", "200")]),
                                                               EndStream:  false,
                                                               EndHeaders: true));

            return await opening.WaitAsync(HoldingH2Transport.StepTimeout);

        }

        #endregion


        #region ADeflateBomb_FailsWith1009_WhileItInflates(Stack, Client)

        /// <summary>
        /// About 64 KiB in one compressed frame, well within the limit as they
        /// travel, that inflate to 64 times the limit. The WebSocket stops
        /// inflating once it is past the limit and fails with 1009, having
        /// allocated a few times the limit, not the 64 MiB the bomb holds.
        /// </summary>
        [TestCaseSource(nameof(Ends))]
        public void ADeflateBomb_FailsWith1009_WhileItInflates(String Stack, Boolean Client)
        {

            var tunnel   = new ScriptedTunnel();
            var receive  = OurEnd(Stack, Client, tunnel, PerMessageDeflate: true);

            tunnel.Feed(Frame(Fin: true, Binary, DeflateBomb.Value, Masked: !Client, Rsv1: true));

            var (message, closeCode, allocated) = Receive(receive, tunnel);

            Assert.Multiple(() =>
            {
                Assert.That(DeflateBomb.Value.Length,  Is.LessThan(Limit / 8),     "the bomb, as it travels");
                Assert.That(message?.Length,           Is.Null,                    "the length of the message, none");
                Assert.That(closeCode,                 Is.EqualTo(MessageTooBig));
                Assert.That(allocated,                 Is.LessThan(8L * Limit),    "the bytes allocated while receiving");
            });

        }

        #endregion

        #region ADeflateBombInFragments_FailsWith1009_WhileItInflates(Stack, Client)

        /// <summary>
        /// The bomb again, in two fragments: a fragmented compressed message is
        /// inflated once it is whole, and is held to the limit as well.
        /// </summary>
        [TestCaseSource(nameof(Ends))]
        public void ADeflateBombInFragments_FailsWith1009_WhileItInflates(String Stack, Boolean Client)
        {

            var tunnel   = new ScriptedTunnel();
            var receive  = OurEnd(Stack, Client, tunnel, PerMessageDeflate: true);
            var bomb     = DeflateBomb.Value;
            var half     = bomb.Length / 2;

            tunnel.Feed(Frame(Fin: false, Binary,       bomb[..half], Masked: !Client, Rsv1: true),
                        Frame(Fin: true,  Continuation, bomb[half..], Masked: !Client));

            var (message, closeCode, allocated) = Receive(receive, tunnel);

            Assert.Multiple(() =>
            {
                Assert.That(message?.Length,  Is.Null,                    "the length of the message, none");
                Assert.That(closeCode,        Is.EqualTo(MessageTooBig));
                Assert.That(allocated,        Is.LessThan(8L * Limit),    "the bytes allocated while receiving");
            });

        }

        #endregion

        #region ALongChainOfFragments_FailsWith1009_AtTheHeaderOfTheFrameThatGoesPast(Stack, Client)

        /// <summary>
        /// A text message in 1 KiB fragments, 1024 of them, which fill the limit
        /// exactly, none of them the last. The next one would take the message
        /// past the limit: the WebSocket fails with 1009 on its header, and the
        /// test never feeds its payload, which the tunnel would answer with a
        /// failure, had the WebSocket gone on to read it.
        /// </summary>
        [TestCaseSource(nameof(Ends))]
        public void ALongChainOfFragments_FailsWith1009_AtTheHeaderOfTheFrameThatGoesPast(String Stack, Boolean Client)
        {

            var tunnel    = new ScriptedTunnel();
            var receive   = OurEnd(Stack, Client, tunnel);
            var fragment  = Filled(1024, (Byte) 'x');

            tunnel.Feed(Frame(Fin: false, Text, fragment, Masked: !Client));

            for (var i = 1; i < Limit / fragment.Length; i++)
                tunnel.Feed(Frame(Fin: false, Continuation, fragment, Masked: !Client));

            tunnel.Feed(Header(Fin: false, Continuation, fragment.Length, Masked: !Client));

            var (message, closeCode, _) = Receive(receive, tunnel);

            Assert.Multiple(() =>
            {
                Assert.That(message?.Length,  Is.Null, "the length of the message, none");
                Assert.That(closeCode,        Is.EqualTo(MessageTooBig));
            });

        }

        #endregion

        #region AMessageInOneFrame_OfTheLimit_IsReceived_OneByteMore_FailsWith1009(Stack, Client)

        /// <summary>
        /// One frame of exactly the limit is received whole. One of a byte more
        /// fails with 1009 on its header: the test feeds no payload after it.
        /// </summary>
        [TestCaseSource(nameof(Ends))]
        public void AMessageInOneFrame_OfTheLimit_IsReceived_OneByteMore_FailsWith1009(String Stack, Boolean Client)
        {

            var payload       = Filled(Limit, 0x5A);

            var atTheLimit    = new ScriptedTunnel();
            atTheLimit.Feed(Frame(Fin: true, Binary, payload, Masked: !Client));
            var received      = Receive(OurEnd(Stack, Client, atTheLimit), atTheLimit);

            var oneByteMore   = new ScriptedTunnel();
            oneByteMore.Feed(Header(Fin: true, Binary, Limit + 1, Masked: !Client));
            var refused       = Receive(OurEnd(Stack, Client, oneByteMore), oneByteMore);

            Assert.Multiple(() =>
            {

                Assert.That(received.Message?.Length,                 Is.EqualTo(Limit), "the length of the message of the limit");
                Assert.That(received.Message?.SequenceEqual(payload), Is.True,           "the message of the limit, byte for byte");
                Assert.That(received.CloseCode,                       Is.Null,           "the Close after the message of the limit");

                Assert.That(refused.Message?.Length,                  Is.Null,           "the length of the message of a byte more, none");
                Assert.That(refused.CloseCode,                        Is.EqualTo(MessageTooBig));

            });

        }

        #endregion

        #region AMessageInFragments_OfTheLimit_IsReceived_OneByteMore_FailsWith1009(Stack, Client)

        /// <summary>
        /// Three fragments that add up to exactly the limit are received as one
        /// message. Three that add up to a byte more fail with 1009, the last
        /// fragment's payload fed as well.
        /// </summary>
        [TestCaseSource(nameof(Ends))]
        public void AMessageInFragments_OfTheLimit_IsReceived_OneByteMore_FailsWith1009(String Stack, Boolean Client)
        {

            var third         = Limit / 3;
            var payload       = Filled(Limit + 1, 0x5A);

            var atTheLimit    = new ScriptedTunnel();
            atTheLimit.Feed(Frame(Fin: false, Binary,       payload[..third],                   Masked: !Client),
                            Frame(Fin: false, Continuation, payload[third..(2 * third)],        Masked: !Client),
                            Frame(Fin: true,  Continuation, payload[(2 * third)..Limit],        Masked: !Client));
            var received      = Receive(OurEnd(Stack, Client, atTheLimit), atTheLimit);

            var oneByteMore   = new ScriptedTunnel();
            oneByteMore.Feed(Frame(Fin: false, Binary,       payload[..third],                  Masked: !Client),
                             Frame(Fin: false, Continuation, payload[third..(2 * third)],       Masked: !Client),
                             Frame(Fin: true,  Continuation, payload[(2 * third)..],            Masked: !Client));
            var refused       = Receive(OurEnd(Stack, Client, oneByteMore), oneByteMore);

            Assert.Multiple(() =>
            {

                Assert.That(received.Message?.Length,                         Is.EqualTo(Limit), "the length of the message of the limit");
                Assert.That(received.Message?.SequenceEqual(payload[..Limit]), Is.True,           "the message of the limit, byte for byte");
                Assert.That(received.CloseCode,                               Is.Null,           "the Close after the message of the limit");

                Assert.That(refused.Message?.Length,                          Is.Null,           "the length of the message of a byte more, none");
                Assert.That(refused.CloseCode,                                Is.EqualTo(MessageTooBig));

            });

        }

        #endregion

        #region AMessageThatInflates_ToTheLimit_IsReceived_ToOneByteMore_FailsWith1009(Stack, Client)

        /// <summary>
        /// A compressed message that inflates to exactly the limit is received
        /// whole; one that inflates to a byte more fails with 1009. Both are a few
        /// KiB as they travel: it is the inflated size that counts.
        /// </summary>
        [TestCaseSource(nameof(Ends))]
        public void AMessageThatInflates_ToTheLimit_IsReceived_ToOneByteMore_FailsWith1009(String Stack, Boolean Client)
        {

            var payload       = Filled(Limit + 1, 0x5A);

            var atTheLimit    = new ScriptedTunnel();
            atTheLimit.Feed(Frame(Fin: true, Binary, Deflate(payload[..Limit]), Masked: !Client, Rsv1: true));
            var received      = Receive(OurEnd(Stack, Client, atTheLimit, PerMessageDeflate: true), atTheLimit);

            var oneByteMore   = new ScriptedTunnel();
            oneByteMore.Feed(Frame(Fin: true, Binary, Deflate(payload),          Masked: !Client, Rsv1: true));
            var refused       = Receive(OurEnd(Stack, Client, oneByteMore, PerMessageDeflate: true), oneByteMore);

            Assert.Multiple(() =>
            {

                Assert.That(received.Message?.Length,                         Is.EqualTo(Limit), "the length of the message that inflates to the limit");
                Assert.That(received.Message?.SequenceEqual(payload[..Limit]), Is.True,           "the message that inflates to the limit, byte for byte");
                Assert.That(received.CloseCode,                               Is.Null,           "the Close after the message that inflates to the limit");

                Assert.That(refused.Message?.Length,                          Is.Null,           "the length of the message that inflates to a byte more, none");
                Assert.That(refused.CloseCode,                                Is.EqualTo(MessageTooBig));

            });

        }

        #endregion

    }

}
