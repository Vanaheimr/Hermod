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

using System.Net.Sockets;
using System.Net.Security;
using System.Buffers.Binary;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Slowloris / timeout hardening: a server with deliberately short timeouts is
    /// driven by raw TCP/TLS clients that stall at each stage (preface, partial
    /// header block, withheld payload, missing SETTINGS-ACK, no TLS ClientHello),
    /// verifying the server reclaims each one promptly, while normal requests keep
    /// working. In-process.
    /// </summary>
    [TestFixture]
    public class TimeoutHardeningTests
    {

        #region Server (short timeouts)

        private static readonly HTTP2Timeouts Timeouts = new()
        {
            Handshake   = TimeSpan.FromSeconds(2),
            Preface     = TimeSpan.FromSeconds(1),
            SettingsAck = TimeSpan.FromSeconds(1),
            Idle        = TimeSpan.FromSeconds(30),
            InProgress  = TimeSpan.FromSeconds(1),
        };

        private static Task<(List<(String, String)>, Byte[]?)> Handler(UInt32 sid, List<(String Name, String Value)> h, Byte[]? body, CancellationToken ct)
            => Task.FromResult<(List<(String, String)>, Byte[]?)>(
                   ([(":status", "200"), ("content-length", "2")], "ok"u8.ToArray()));

        private TestH2Server srv = null!;

        [OneTimeSetUp]
        public async Task StartServer()
            => srv = await TestH2Server.StartAsync(Handler, Timeouts: Timeouts);

        [OneTimeTearDown]
        public async Task StopServer()
            => await srv.DisposeAsync();

        #endregion

        #region (helpers)

        // Read one frame within a timeout; null on EOF / timeout / error.
        private static async Task<HTTP2Frame?> ReadFrameAsync(Stream s, TimeSpan timeout)
        {
            try
            {
                using var cts = new CancellationTokenSource(timeout);
                return await H2Raw.ReadFrameAsync(s, cts.Token);
            }
            catch { return null; }
        }

        // True if the connection closes within 'within' (a read returns 0 / errors).
        private static async Task<Boolean> ClosesWithinAsync(Stream s, TimeSpan within)
        {
            try
            {
                using var cts = new CancellationTokenSource(within);
                var buf = new Byte[4096];
                while (true)
                {
                    var n = await s.ReadAsync(buf, cts.Token);
                    if (n == 0) return true;   // clean close
                }
            }
            catch (OperationCanceledException) { return false; }   // still open
            catch                              { return true;  }   // reset / error == closed
        }

        // Send preface + our SETTINGS, wait for the server's SETTINGS, optionally ACK.
        private static async Task HandshakeAsync(SslStream ssl, Boolean ackServerSettings)
        {
            await ssl.WriteAsync(H2Raw.Preface);
            await ssl.WriteAsync(HTTP2Frame.CreateSettings().Serialize());
            await ssl.FlushAsync();

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
            while (DateTime.UtcNow < deadline)
            {
                var f = await ReadFrameAsync(ssl, TimeSpan.FromSeconds(2));
                if (f is null) break;
                if (f.Type == HTTP2FrameType.SETTINGS && !f.IsAck)
                {
                    if (ackServerSettings)
                    {
                        await ssl.WriteAsync(HTTP2Frame.CreateSettingsAck().Serialize());
                        await ssl.FlushAsync();
                    }
                    return;
                }
            }
        }

        private async Task<String?> SendGetAndReadStatusAsync(SslStream ssl)
        {
            var enc   = new HPACKEncoder();
            var block = enc.EncodeHeaderBlock(
                [(":method", "GET"), (":scheme", "https"), (":authority", $"localhost:{srv.Port}"), (":path", "/")]);
            await ssl.WriteAsync(HTTP2Frame.CreateHeaders(1, block, EndStream: true, EndHeaders: true).Serialize());
            await ssl.FlushAsync();

            var dec      = new HPACKDecoder();
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
            while (DateTime.UtcNow < deadline)
            {
                var f = await ReadFrameAsync(ssl, TimeSpan.FromSeconds(2));
                if (f is null) break;
                if (f.Type == HTTP2FrameType.HEADERS)
                    return dec.DecodeHeaderBlock(f.Payload).FirstOrDefault(h => h.Name == ":status").Value;
            }
            return null;
        }

        #endregion


        #region NormalRequest_UnderShortTimeouts_200()

        [Test]
        public async Task NormalRequest_UnderShortTimeouts_200()
        {
            using var ssl = await H2Raw.ConnectTlsAsync(srv.Port);
            await HandshakeAsync(ssl, ackServerSettings: true);
            Assert.That(await SendGetAndReadStatusAsync(ssl), Is.EqualTo("200"), "normal GET returns 200");
        }

        #endregion

        #region StalledPreface_ConnectionClosed()

        [Test]
        public async Task StalledPreface_ConnectionClosed()
        {
            using var ssl = await H2Raw.ConnectTlsAsync(srv.Port);
            // Send nothing after TLS — the preface timeout must reclaim us.
            var closed = await ClosesWithinAsync(ssl, Timeouts.Preface + TimeSpan.FromSeconds(2));
            Assert.That(closed, Is.True, "connection closed after preface timeout");
        }

        #endregion

        #region PartialHeaderBlock_ConnectionClosed()

        [Test]
        public async Task PartialHeaderBlock_ConnectionClosed()
        {
            using var ssl = await H2Raw.ConnectTlsAsync(srv.Port);
            await HandshakeAsync(ssl, ackServerSettings: true);

            var enc   = new HPACKEncoder();
            var block = enc.EncodeHeaderBlock(
                [(":method", "GET"), (":scheme", "https"), (":authority", $"localhost:{srv.Port}"), (":path", "/")]);
            // END_HEADERS deliberately NOT set — the server now expects CONTINUATION.
            await ssl.WriteAsync(HTTP2Frame.CreateHeaders(1, block, EndStream: false, EndHeaders: false).Serialize());
            await ssl.FlushAsync();

            var closed = await ClosesWithinAsync(ssl, Timeouts.InProgress + TimeSpan.FromSeconds(2));
            Assert.That(closed, Is.True, "connection closed after in-progress (header block) timeout");
        }

        #endregion

        #region WithheldFramePayload_ConnectionClosed()

        [Test]
        public async Task WithheldFramePayload_ConnectionClosed()
        {
            using var ssl = await H2Raw.ConnectTlsAsync(srv.Port);
            await HandshakeAsync(ssl, ackServerSettings: true);

            // A DATA frame header on stream 1 declaring 100 payload bytes — then nothing.
            var hdr = new Byte[9];
            hdr[0] = 0; hdr[1] = 0; hdr[2] = 100;                     // length = 100
            hdr[3] = (Byte) HTTP2FrameType.DATA;                      // type = DATA
            hdr[4] = 0;                                               // flags
            BinaryPrimitives.WriteUInt32BigEndian(hdr.AsSpan(5), 1);  // stream 1
            await ssl.WriteAsync(hdr);
            await ssl.FlushAsync();

            var closed = await ClosesWithinAsync(ssl, Timeouts.InProgress + TimeSpan.FromSeconds(2));
            Assert.That(closed, Is.True, "connection closed after in-progress (payload) timeout");
        }

        #endregion

        #region SettingsAckTimeout_Goaway()

        [Test]
        public async Task SettingsAckTimeout_Goaway()
        {
            using var ssl = await H2Raw.ConnectTlsAsync(srv.Port);
            await HandshakeAsync(ssl, ackServerSettings: false);   // deliberately never ACK

            HTTP2ErrorCode? goawayCode = null;
            var deadline = DateTime.UtcNow + Timeouts.SettingsAck + TimeSpan.FromSeconds(2);
            while (DateTime.UtcNow < deadline)
            {
                var f = await ReadFrameAsync(ssl, TimeSpan.FromSeconds(2));
                if (f is null) break;
                if (f.Type == HTTP2FrameType.GOAWAY)
                {
                    goawayCode = (HTTP2ErrorCode) BinaryPrimitives.ReadUInt32BigEndian(f.Payload.AsSpan(4, 4));
                    break;
                }
            }
            Assert.That(goawayCode, Is.EqualTo(HTTP2ErrorCode.SETTINGS_TIMEOUT), "GOAWAY with SETTINGS_TIMEOUT when ACK withheld");
        }

        #endregion

        #region TlsHandshakeTimeout_RawTcpDropped()

        [Test]
        public async Task TlsHandshakeTimeout_RawTcpDropped()
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync("127.0.0.1", srv.Port);
            var raw = tcp.GetStream();
            // Never start TLS — the handshake timeout must drop the raw TCP.
            var closed = await ClosesWithinAsync(raw, Timeouts.Handshake + TimeSpan.FromSeconds(2));
            Assert.That(closed, Is.True, "raw TCP dropped after handshake timeout");
        }

        #endregion

        #region ServerStillHealthy_AfterAbuse()

        [Test]
        public async Task ServerStillHealthy_AfterAbuse()
        {
            using var ssl = await H2Raw.ConnectTlsAsync(srv.Port);
            await HandshakeAsync(ssl, ackServerSettings: true);
            Assert.That(await SendGetAndReadStatusAsync(ssl), Is.EqualTo("200"), "follow-up GET still returns 200");
        }

        #endregion

    }

}
