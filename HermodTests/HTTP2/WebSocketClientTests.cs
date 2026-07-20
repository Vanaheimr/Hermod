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

using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Our client's CONNECT tunnel + WebSocket support, driven against our own
    /// server (which implements the server side of CONNECT / extended CONNECT /
    /// RFC 6455 / RFC 7692): plain-CONNECT byte loopback, a full WebSocket session
    /// (text, binary, larger frames, close handshake), a rejected CONNECT, and
    /// permessage-deflate over the RFC 8441 CONNECT path — both ends hand-rolled.
    /// In-process.
    /// </summary>
    [TestFixture]
    public class WebSocketClientTests
    {

        #region Server (CONNECT handler: WS echo + raw byte loopback)

        private static Task<(List<(String, String)>, Byte[]?)> Unused(UInt32 s, List<(String Name, String Value)> h, Byte[]? b, CancellationToken ct)
            => Task.FromResult<(List<(String, String)>, Byte[]?)>(([(":status", "404")], null));

        private static Task<HTTP2ConnectResult> Connect(UInt32 sid, List<(String Name, String Value)> headers, CancellationToken ct)
        {
            var protocol = headers.FirstOrDefault(h => h.Name == ":protocol").Value;
            var path     = headers.FirstOrDefault(h => h.Name == ":path").Value;

            if (protocol == "websocket" && path == "/ws-echo")
            {
                var offer   = headers.FirstOrDefault(h => h.Name == "sec-websocket-extensions").Value;
                var deflate = WebSocketDeflate.ShouldAccept(offer, out var responseExt);

                return Task.FromResult(new HTTP2ConnectResult
                {
                    StatusCode   = 200,
                    ExtraHeaders = deflate ? [("sec-websocket-extensions", responseExt!)] : null,
                    RunAsync     = async (tunnel, ct2) =>
                    {
                        var ws = new WebSocketConnection(tunnel, WebSocketRole.Server, PerMessageDeflate: deflate);
                        while (true)
                        {
                            var msg = await ws.ReceiveAsync(ct2);
                            if (msg is null) break;
                            if (msg.Opcode == WebSocketOpcode.Text)
                                await ws.SendTextAsync(Encoding.UTF8.GetString(msg.Payload), ct2);
                            else
                                await ws.SendBinaryAsync(msg.Payload, ct2);
                        }
                    }
                });
            }

            if (protocol is null)   // plain CONNECT: raw byte loopback
                return Task.FromResult(new HTTP2ConnectResult
                {
                    StatusCode = 200,
                    RunAsync   = async (tunnel, ct2) =>
                    {
                        Byte[]? chunk;
                        while ((chunk = await tunnel.ReadAsync(ct2)) is not null)
                            await tunnel.WriteAsync(chunk, ct2);
                    }
                });

            return Task.FromResult(new HTTP2ConnectResult { StatusCode = 404 });
        }

        private TestH2Server srv = null!;

        [OneTimeSetUp]
        public async Task StartServer()
            => srv = await TestH2Server.StartAsync(Unused, ConnectHandler: Connect);

        [OneTimeTearDown]
        public async Task StopServer()
            => await srv.DisposeAsync();

        #endregion

        #region (helper) ReadExactTunnelAsync

        private static async Task<Byte[]> ReadExactTunnelAsync(IHTTP2Tunnel t, Int32 n, CancellationToken ct)
        {
            var buf = new List<Byte>();
            while (buf.Count < n)
            {
                var chunk = await t.ReadAsync(ct);
                if (chunk is null) break;
                buf.AddRange(chunk);
            }
            return buf.ToArray();
        }

        #endregion


        #region PlainConnect_ByteLoopback()

        [Test]
        public async Task PlainConnect_ByteLoopback()
        {
            var conn   = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var tunnel = await conn.OpenTunnelAsync("echo.internal");

            Assert.That(tunnel.StreamId % 2, Is.EqualTo(1u), "plain CONNECT accepted on an odd stream id");

            foreach (var payload in new[] { "hello tunnel", "second round", "third 🎉 unicode" })
            {
                var bytes = Encoding.UTF8.GetBytes(payload);
                await tunnel.WriteAsync(bytes, CancellationToken.None);
                var echo = await ReadExactTunnelAsync(tunnel, bytes.Length, CancellationToken.None);
                Assert.That(echo, Is.EqualTo(bytes), $"loopback echoes '{payload}'");
            }

            await tunnel.CloseAsync();
            await conn.CloseAsync();
        }

        #endregion

        #region ExtendedConnect_WebSocketSession()

        [Test]
        public async Task ExtendedConnect_WebSocketSession()
        {
            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var ws   = await conn.OpenWebSocketAsync("localhost", "https", "/ws-echo");

            await ws.SendTextAsync("hello websocket", CancellationToken.None);
            var m1 = await ws.ReceiveAsync(CancellationToken.None);
            Assert.That(m1 is { Opcode: WebSocketOpcode.Text } && Encoding.UTF8.GetString(m1.Payload) == "hello websocket",
                        Is.True, "text message echoed");

            var bin = new Byte[] { 1, 2, 3, 250, 251, 252 };
            await ws.SendBinaryAsync(bin, CancellationToken.None);
            var m2 = await ws.ReceiveAsync(CancellationToken.None);
            Assert.That(m2 is { Opcode: WebSocketOpcode.Binary } && m2.Payload.AsSpan().SequenceEqual(bin),
                        Is.True, "binary message echoed");

            var big = new String('x', 1000);
            await ws.SendTextAsync(big, CancellationToken.None);
            var m3 = await ws.ReceiveAsync(CancellationToken.None);
            Assert.That(m3 is { Opcode: WebSocketOpcode.Text } && Encoding.UTF8.GetString(m3.Payload) == big,
                        Is.True, "1000-byte text echoed");

            await ws.CloseAsync(1000, "bye", CancellationToken.None);
            var end = await ws.ReceiveAsync(CancellationToken.None);
            Assert.That(end, Is.Null, "close handshake completes (ReceiveAsync -> null)");

            await conn.CloseAsync();
        }

        #endregion

        #region RejectedConnect_SurfacesException()

        [Test]
        public async Task RejectedConnect_SurfacesException()
        {
            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            Assert.That(async () => await conn.OpenWebSocketAsync("localhost", "https", "/nonexistent"),
                        Throws.Exception, "unknown WebSocket path is rejected");

            // Connection still usable for an ordinary request afterward.
            var resp = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/");
            Assert.That(resp.Status, Is.EqualTo(404), "connection healthy after a rejected CONNECT");

            await conn.CloseAsync();
        }

        #endregion

        #region PermessageDeflate_ManualNegotiation()

        [Test]
        public async Task PermessageDeflate_ManualNegotiation()
        {
            var conn   = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var tunnel = await conn.OpenTunnelAsync("localhost", "websocket", "https", "/ws-echo",
                             [("sec-websocket-extensions", WebSocketDeflate.Offer)]);
            var echoed = tunnel.ResponseHeaders.FirstOrDefault(h => h.Name == "sec-websocket-extensions").Value;
            Assert.That(WebSocketDeflate.WasAccepted(echoed), Is.True, "server echoes permessage-deflate acceptance");

            var ws = new WebSocketConnection(tunnel, WebSocketRole.Client, PerMessageDeflate: WebSocketDeflate.WasAccepted(echoed));

            var compressible = new String('A', 2000) + "🎉 unicode survives compression 🎉";
            await ws.SendTextAsync(compressible, CancellationToken.None);
            var d1 = await ws.ReceiveAsync(CancellationToken.None);
            Assert.That(d1 is { Opcode: WebSocketOpcode.Text } && Encoding.UTF8.GetString(d1.Payload) == compressible,
                        Is.True, "deflate text message round-trips");

            var binData = new Byte[4096];
            for (var i = 0; i < binData.Length; i++) binData[i] = (Byte) (i % 7);
            await ws.SendBinaryAsync(binData, CancellationToken.None);
            var d2 = await ws.ReceiveAsync(CancellationToken.None);
            Assert.That(d2 is { Opcode: WebSocketOpcode.Binary } && d2.Payload.AsSpan().SequenceEqual(binData),
                        Is.True, "deflate binary message round-trips");

            await ws.CloseAsync(1000, "bye", CancellationToken.None);
            Assert.That(await ws.ReceiveAsync(CancellationToken.None), Is.Null, "deflate close handshake completes");

            await conn.CloseAsync();
        }

        #endregion

        #region PermessageDeflate_ConvenienceApi()

        [Test]
        public async Task PermessageDeflate_ConvenienceApi()
        {
            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var ws   = await conn.OpenWebSocketAsync("localhost", "https", "/ws-echo", PerMessageDeflate: true);

            var msg = new String('Z', 1500);
            await ws.SendTextAsync(msg, CancellationToken.None);
            var back = await ws.ReceiveAsync(CancellationToken.None);
            Assert.That(back is { Opcode: WebSocketOpcode.Text } && Encoding.UTF8.GetString(back.Payload) == msg,
                        Is.True, "convenience deflate API round-trips");

            await conn.CloseAsync();
        }

        #endregion

    }

}
