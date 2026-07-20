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

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// RFC 6455 conformance — the Autobahn TestSuite's critical cases (framing,
    /// ping/pong, fragmentation, reserved bits/opcodes, UTF-8 handling, close
    /// handling) plus RFC 7692 permessage-deflate — driven directly against our
    /// <see cref="WebSocketConnection"/> framing via an in-process HTTP/1.1-Upgrade
    /// echo server and a raw WebSocket client that can craft malformed frames.
    /// In-process, no Docker.
    /// </summary>
    [TestFixture]
    public class WebSocketConformanceTests
    {

        private const String WsGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        private const Int32  Text = 0x1, Binary = 0x2, Close = 0x8, Ping = 0x9, Pong = 0xA, Cont = 0x0;

        private TcpListener listener   = null!;
        private Task        serverLoop = null!;
        private Int32       port;

        #region Echo server (server-role WebSocketConnection over a raw TCP tunnel)

        [OneTimeSetUp]
        public void StartServer()
        {
            listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            port       = ((IPEndPoint) listener.LocalEndpoint).Port;
            serverLoop = Task.Run(ServerLoopAsync);
        }

        [OneTimeTearDown]
        public async Task StopServer()
        {
            listener.Stop();
            try { await serverLoop.WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
        }

        private async Task ServerLoopAsync()
        {
            while (true)
            {
                TcpClient c;
                try { c = await listener.AcceptTcpClientAsync(); } catch { break; }
                _ = Task.Run(async () =>
                {
                    using (c)
                    {
                        try
                        {
                            var s     = c.GetStream();
                            var lines = await ReadHeaders(s);
                            if (lines is null) return;
                            var key = Header(lines, "Sec-WebSocket-Key");
                            if (key is null) return;
                            var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + WsGuid)));

                            var deflate = (Header(lines, "Sec-WebSocket-Extensions") ?? "").Contains("permessage-deflate");
                            var extLine = deflate ? "Sec-WebSocket-Extensions: permessage-deflate; server_no_context_takeover; client_no_context_takeover\r\n" : "";

                            await s.WriteAsync(Encoding.ASCII.GetBytes(
                                "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n" +
                                $"Sec-WebSocket-Accept: {accept}\r\n{extLine}\r\n"));

                            var ws = new WebSocketConnection(new TcpTunnel(s), WebSocketRole.Server, PerMessageDeflate: deflate);
                            while (true)
                            {
                                WebSocketMessage? m;
                                try { m = await ws.ReceiveAsync(CancellationToken.None); }
                                catch { break; }
                                if (m is null) break;
                                if (m.Opcode == WebSocketOpcode.Text)
                                    await ws.SendTextAsync(Encoding.UTF8.GetString(m.Payload), CancellationToken.None);
                                else
                                    await ws.SendBinaryAsync(m.Payload, CancellationToken.None);
                            }
                        }
                        catch { }
                    }
                });
            }
        }

        private static async Task<String[]?> ReadHeaders(NetworkStream s)
        {
            var sb = new StringBuilder(); var one = new Byte[1];
            while (sb.Length < 16 * 1024)
            {
                if (await s.ReadAsync(one) == 0) return null;
                sb.Append((Char) one[0]);
                if (sb.Length >= 4 && sb[^1] == '\n' && sb[^2] == '\r' && sb[^3] == '\n' && sb[^4] == '\r')
                    return sb.ToString().Split("\r\n");
            }
            return null;
        }

        private static String? Header(String[] lines, String name)
        {
            foreach (var line in lines)
            {
                var i = line.IndexOf(':');
                if (i > 0 && line[..i].Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                    return line[(i + 1)..].Trim();
            }
            return null;
        }

        #endregion

        #region Raw WebSocket client helpers

        private async Task<NetworkStream> Handshake()
        {
            var tcp = new TcpClient();
            await tcp.ConnectAsync(System.Net.IPAddress.Loopback, port);
            var s = tcp.GetStream();
            var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            await s.WriteAsync(Encoding.ASCII.GetBytes(
                $"GET / HTTP/1.1\r\nHost: localhost:{port}\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n" +
                $"Sec-WebSocket-Key: {key}\r\nSec-WebSocket-Version: 13\r\n\r\n"));
            await ReadUntilBlankLine(s);
            return s;
        }

        private async Task<(NetworkStream Stream, Boolean Deflate)> HandshakeOfferingDeflate()
        {
            var tcp = new TcpClient();
            await tcp.ConnectAsync(System.Net.IPAddress.Loopback, port);
            var s = tcp.GetStream();
            var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            await s.WriteAsync(Encoding.ASCII.GetBytes(
                $"GET / HTTP/1.1\r\nHost: localhost:{port}\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n" +
                $"Sec-WebSocket-Key: {key}\r\nSec-WebSocket-Version: 13\r\nSec-WebSocket-Extensions: permessage-deflate\r\n\r\n"));
            var head = await ReadUntilBlankLine(s);
            return (s, head.Contains("permessage-deflate", StringComparison.OrdinalIgnoreCase));
        }

        private static async Task<String> ReadUntilBlankLine(NetworkStream s)
        {
            var sb = new StringBuilder(); var one = new Byte[1];
            while (true)
            {
                if (await s.ReadAsync(one) == 0) throw new IOException("handshake ended");
                sb.Append((Char) one[0]);
                if (sb.Length >= 4 && sb[^1] == '\n' && sb[^2] == '\r' && sb[^3] == '\n' && sb[^4] == '\r') break;
            }
            var head = sb.ToString();
            if (!head.StartsWith("HTTP/1.1 101")) throw new IOException("no 101");
            return head;
        }

        // Send one client frame (always masked, per RFC 6455 5.3).
        private static async Task SendRaw(NetworkStream s, Boolean fin, Int32 rsv, Int32 opcode, Byte[] payload)
        {
            var h = new List<Byte> { (Byte) ((fin ? 0x80 : 0) | ((rsv & 0x7) << 4) | (opcode & 0x0F)) };
            if (payload.Length <= 125) h.Add((Byte) (0x80 | payload.Length));
            else if (payload.Length <= 65535) { h.Add(0x80 | 126); var e = new Byte[2]; BinaryPrimitives.WriteUInt16BigEndian(e, (UInt16) payload.Length); h.AddRange(e); }
            else { h.Add(0x80 | 127); var e = new Byte[8]; BinaryPrimitives.WriteUInt64BigEndian(e, (UInt64) payload.Length); h.AddRange(e); }
            var key = RandomNumberGenerator.GetBytes(4);
            h.AddRange(key);
            var masked = new Byte[payload.Length];
            for (var i = 0; i < payload.Length; i++) masked[i] = (Byte) (payload[i] ^ key[i % 4]);
            var frame = new Byte[h.Count + masked.Length];
            h.CopyTo(frame); masked.CopyTo(frame, h.Count);
            await s.WriteAsync(frame);
            await s.FlushAsync();
        }

        private static async Task<Boolean> ReadExact(NetworkStream s, Byte[] buf)
        {
            var off = 0;
            while (off < buf.Length)
            {
                var n = await s.ReadAsync(buf.AsMemory(off, buf.Length - off));
                if (n == 0) return false;
                off += n;
            }
            return true;
        }

        private static async Task<(Int32 Opcode, Byte[] Payload)?> ReadFrame(NetworkStream s)
        {
            var h2 = new Byte[2];
            if (!await ReadExact(s, h2)) return null;
            var opcode = h2[0] & 0x0F;
            Int64 len = h2[1] & 0x7F;
            if (len == 126) { var e = new Byte[2]; if (!await ReadExact(s, e)) return null; len = BinaryPrimitives.ReadUInt16BigEndian(e); }
            else if (len == 127) { var e = new Byte[8]; if (!await ReadExact(s, e)) return null; len = (Int64) BinaryPrimitives.ReadUInt64BigEndian(e); }
            var payload = new Byte[len];
            if (len > 0 && !await ReadExact(s, payload)) return null;
            return (opcode, payload);
        }

        private static async Task<(Boolean Rsv1, Int32 Opcode, Byte[] Payload)?> ReadFrameFull(NetworkStream s)
        {
            var h2 = new Byte[2];
            if (!await ReadExact(s, h2)) return null;
            var rsv1   = (h2[0] & 0x40) != 0;
            var opcode = h2[0] & 0x0F;
            Int64 len = h2[1] & 0x7F;
            if (len == 126) { var e = new Byte[2]; if (!await ReadExact(s, e)) return null; len = BinaryPrimitives.ReadUInt16BigEndian(e); }
            else if (len == 127) { var e = new Byte[8]; if (!await ReadExact(s, e)) return null; len = (Int64) BinaryPrimitives.ReadUInt64BigEndian(e); }
            var payload = new Byte[len];
            if (len > 0 && !await ReadExact(s, payload)) return null;
            return (rsv1, opcode, payload);
        }

        private static async Task<UInt16?> ExpectClose(NetworkStream s)
        {
            while (true)
            {
                var f = await ReadFrame(s);
                if (f is null) return null;
                if (f.Value.Opcode == Close)
                    return f.Value.Payload.Length >= 2 ? BinaryPrimitives.ReadUInt16BigEndian(f.Value.Payload) : (UInt16) 0;
            }
        }

        // permessage-deflate wire codec (RFC 7692 §7.2), client side.
        private static Byte[] DeflateBody(Byte[] data)
        {
            using var o = new MemoryStream();
            using (var d = new DeflateStream(o, CompressionLevel.Optimal, leaveOpen: true)) d.Write(data, 0, data.Length);
            var b = o.ToArray();
            if (b.Length >= 4 && b[^4] == 0 && b[^3] == 0 && b[^2] == 0xFF && b[^1] == 0xFF) b = b[..^4];
            return b;
        }

        private static Byte[] InflateBody(Byte[] comp)
        {
            using var i = new MemoryStream(); i.Write(comp, 0, comp.Length); i.Write([0, 0, 0xFF, 0xFF], 0, 4); i.Position = 0;
            using var d = new DeflateStream(i, CompressionMode.Decompress);
            using var o = new MemoryStream(); d.CopyTo(o); return o.ToArray();
        }

        #endregion


        #region Framing_PingPong_Fragmentation()

        [Test]
        public async Task Framing_PingPong_Fragmentation()
        {
            var s = await Handshake();

            await SendRaw(s, true, 0, Text, Encoding.UTF8.GetBytes("Hello"));
            var echo = await ReadFrame(s);
            Assert.That(echo is { Opcode: Text } && Encoding.UTF8.GetString(echo.Value.Payload) == "Hello", Is.True, "text message echoed");

            var bin = new Byte[] { 1, 2, 3, 0, 255, 128 };
            await SendRaw(s, true, 0, Binary, bin);
            var be = await ReadFrame(s);
            Assert.That(be is { Opcode: Binary } && be.Value.Payload.SequenceEqual(bin), Is.True, "binary message echoed");

            await SendRaw(s, true, 0, Ping, Encoding.UTF8.GetBytes("pingdata"));
            var pong = await ReadFrame(s);
            Assert.That(pong is { Opcode: Pong } && Encoding.UTF8.GetString(pong.Value.Payload) == "pingdata", Is.True, "ping answered with matching pong");

            await SendRaw(s, false, 0, Text, Encoding.UTF8.GetBytes("Hel"));
            await SendRaw(s, true, 0, Cont, Encoding.UTF8.GetBytes("lo!"));
            var frag = await ReadFrame(s);
            Assert.That(frag is { Opcode: Text } && Encoding.UTF8.GetString(frag.Value.Payload) == "Hello!", Is.True, "fragmented text reassembled");

            s.Close();
        }

        #endregion

        #region ReservedBitsAndOpcodes_FailWith1002()

        [Test]
        public async Task ReservedBitsAndOpcodes_FailWith1002()
        {
            var s = await Handshake();
            await SendRaw(s, true, 0b100, Text, Encoding.UTF8.GetBytes("x"));   // RSV1 set, no extension
            Assert.That(await ExpectClose(s), Is.EqualTo(1002), "a set reserved bit fails the connection");
            s.Close();

            s = await Handshake();
            await SendRaw(s, true, 0, 0x3, [1, 2, 3]);   // reserved non-control opcode
            Assert.That(await ExpectClose(s), Is.EqualTo(1002), "a reserved opcode fails the connection");
            s.Close();

            s = await Handshake();
            await SendRaw(s, true, 0, Cont, Encoding.UTF8.GetBytes("orphan"));  // continuation with no start
            Assert.That(await ExpectClose(s), Is.EqualTo(1002), "an unexpected continuation frame fails the connection");
            s.Close();
        }

        #endregion

        #region Utf8Handling()

        [Test]
        public async Task Utf8Handling()
        {
            var s = await Handshake();
            var utf8 = Encoding.UTF8.GetBytes("Ĥéłłö 世界 🌍");
            await SendRaw(s, true, 0, Text, utf8);
            var echo = await ReadFrame(s);
            Assert.That(echo is { Opcode: Text } && echo.Value.Payload.SequenceEqual(utf8), Is.True, "valid multi-byte UTF-8 echoed");
            s.Close();

            s = await Handshake();
            await SendRaw(s, false, 0, Text, [0xC3]);
            await SendRaw(s, true, 0, Cont, [0xA9]);
            var split = await ReadFrame(s);
            Assert.That(split is { Opcode: Text } && split.Value.Payload.SequenceEqual(new Byte[] { 0xC3, 0xA9 }), Is.True,
                        "a UTF-8 code point split across fragments is valid");
            s.Close();

            s = await Handshake();
            await SendRaw(s, true, 0, Text, [0x48, 0xFF, 0x69]);
            Assert.That(await ExpectClose(s), Is.EqualTo(1007), "invalid UTF-8 in a text message fails the connection");
            s.Close();

            s = await Handshake();
            await SendRaw(s, false, 0, Text, Encoding.UTF8.GetBytes("ok"));
            await SendRaw(s, true, 0, Cont, [0xC0, 0xAF]);   // overlong "/" — invalid
            Assert.That(await ExpectClose(s), Is.EqualTo(1007), "invalid UTF-8 in a text fragment fails the connection");
            s.Close();

            s = await Handshake();
            await SendRaw(s, true, 0, Text, [0x41, 0xC3]);
            Assert.That(await ExpectClose(s), Is.EqualTo(1007), "a truncated trailing UTF-8 sequence fails the connection");
            s.Close();
        }

        #endregion

        #region CloseHandling()

        [Test]
        public async Task CloseHandling()
        {
            var s = await Handshake();
            var p = new Byte[2]; BinaryPrimitives.WriteUInt16BigEndian(p, 1000);
            await SendRaw(s, true, 0, Close, p);
            Assert.That(await ExpectClose(s), Is.EqualTo(1000), "a valid close (1000) is echoed");
            s.Close();

            s = await Handshake();
            await SendRaw(s, true, 0, Close, []);
            Assert.That(await ExpectClose(s), Is.Not.Null, "an empty close is answered with a close");
            s.Close();

            s = await Handshake();
            await SendRaw(s, true, 0, Close, [0x03]);
            Assert.That(await ExpectClose(s), Is.EqualTo(1002), "a 1-byte close payload fails the connection");
            s.Close();

            s = await Handshake();
            var pr = new Byte[2 + 3]; BinaryPrimitives.WriteUInt16BigEndian(pr, 1000); Encoding.ASCII.GetBytes("bye").CopyTo(pr, 2);
            await SendRaw(s, true, 0, Close, pr);
            Assert.That(await ExpectClose(s), Is.EqualTo(1000), "a close with code + reason is echoed");
            s.Close();

            foreach (var code in new[] { 999, 1004, 1005, 1006, 1016, 2000, 65535 })
            {
                s = await Handshake();
                var cp = new Byte[2]; BinaryPrimitives.WriteUInt16BigEndian(cp, (UInt16) code);
                await SendRaw(s, true, 0, Close, cp);
                Assert.That(await ExpectClose(s), Is.EqualTo(1002), $"invalid close code {code} fails the connection");
                s.Close();
            }

            foreach (var code in new[] { 3000, 4999 })
            {
                s = await Handshake();
                var cp = new Byte[2]; BinaryPrimitives.WriteUInt16BigEndian(cp, (UInt16) code);
                await SendRaw(s, true, 0, Close, cp);
                Assert.That(await ExpectClose(s), Is.EqualTo(code), $"valid application close code {code} is echoed");
                s.Close();
            }

            s = await Handshake();
            var badReason = new Byte[] { 0x03, 0xE8, 0xFF, 0xFF };   // code 1000, then invalid UTF-8
            await SendRaw(s, true, 0, Close, badReason);
            Assert.That(await ExpectClose(s), Is.EqualTo(1007), "invalid UTF-8 in a close reason fails the connection");
            s.Close();
        }

        #endregion

        #region PermessageDeflate()

        [Test]
        public async Task PermessageDeflate()
        {
            var (s, accepted) = await HandshakeOfferingDeflate();
            Assert.That(accepted, Is.True, "permessage-deflate is negotiated when offered");

            var text = "Hello, permessage-deflate! " + new String('x', 200);
            await SendRaw(s, true, 0b100, Text, DeflateBody(Encoding.UTF8.GetBytes(text)));
            var echo = await ReadFrameFull(s);
            Assert.That(echo is { Rsv1: true, Opcode: Text } && Encoding.UTF8.GetString(InflateBody(echo.Value.Payload)) == text,
                        Is.True, "compressed text round-trips (RSV1, inflates to original)");

            var bin = Enumerable.Range(0, 300).Select(i => (Byte) (i % 7)).ToArray();
            await SendRaw(s, true, 0b100, Binary, DeflateBody(bin));
            var be = await ReadFrameFull(s);
            Assert.That(be is { Rsv1: true, Opcode: Binary } && InflateBody(be.Value.Payload).SequenceEqual(bin),
                        Is.True, "compressed binary round-trips");

            var frag = Encoding.UTF8.GetBytes("fragmented compressed message payload, reasonably long");
            var comp = DeflateBody(frag);
            var half = comp.Length / 2;
            await SendRaw(s, false, 0b100, Text, comp[..half]);   // first frame: RSV1 set, FIN=0
            await SendRaw(s, true,  0,     Cont, comp[half..]);   // continuation: RSV1 clear, FIN=1
            var fe = await ReadFrameFull(s);
            Assert.That(fe is { Opcode: Text } && Encoding.UTF8.GetString(InflateBody(fe.Value.Payload)) == Encoding.UTF8.GetString(frag),
                        Is.True, "compressed fragmented text round-trips");

            // RFC 7692 §5.1: an uncompressed message (RSV1=0) on a deflate connection.
            await SendRaw(s, true, 0, Text, Encoding.UTF8.GetBytes("plain"));
            var pe = await ReadFrameFull(s);
            var plainText = pe!.Value.Rsv1 ? Encoding.UTF8.GetString(InflateBody(pe.Value.Payload)) : Encoding.UTF8.GetString(pe.Value.Payload);
            Assert.That(plainText, Is.EqualTo("plain"), "an uncompressed message on a deflate connection still round-trips");

            s.Close();
        }

        #endregion


        #region (nested) TcpTunnel

        /// <summary>Minimal IHTTP2Tunnel over a raw TCP stream (mirrors the Autobahn echo server).</summary>
        private sealed class TcpTunnel(NetworkStream Stream) : IHTTP2Tunnel
        {
            private readonly Byte[] buf = new Byte[64 * 1024];
            public async Task<Byte[]?> ReadAsync(CancellationToken ct)
            {
                var n = await Stream.ReadAsync(buf, ct);
                return n == 0 ? null : buf[..n];
            }
            public Task WriteAsync(Byte[] Data, CancellationToken ct) => Stream.WriteAsync(Data, ct).AsTask();
        }

        #endregion

    }

}
