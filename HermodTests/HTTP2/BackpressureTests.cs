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
using System.Buffers.Binary;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Consumption-driven backpressure + bounded buffered body: a streaming/tunnel
    /// receive window is returned only as the handler CONSUMES body chunks (so a
    /// handler that hasn't read leaves the peer's window depleted), and the
    /// buffered path is bounded by <c>MaxRequestBodySize</c> (an over-cap body
    /// resets the stream with ENHANCE_YOUR_CALM while the connection stays usable).
    /// In-process, driven by a raw frame-level client.
    /// </summary>
    [TestFixture]
    public class BackpressureTests
    {

        private const Int32 HalfWindow = 1_048_576 / 2;   // half the 1 MiB initial stream window

        private static Task<(List<(String, String)>, Byte[]?)> BufferedUnused(UInt32 s, List<(String Name, String Value)> h, Byte[]? b, CancellationToken c)
            => Task.FromResult<(List<(String, String)>, Byte[]?)>(([(":status", "200")], null));


        #region Streaming_WindowReturnedOnConsumptionNotReceipt()

        [Test]
        public async Task Streaming_WindowReturnedOnConsumptionNotReceipt()
        {

            // A streaming handler held shut by a gate: it consumes nothing until the
            // test opens the gate, then drains the whole body and replies 200.
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            HTTP2StreamingHandler streaming = async (req, resp, ct) =>
            {
                await gate.Task.WaitAsync(ct);
                while (await req.ReadAsync(ct) is not null) { }   // drain
                await resp.WriteHeadersAsync([(":status", "200"), ("content-length", "2")], ct);
                await resp.WriteAsync("ok"u8.ToArray(), ct);
                await resp.CompleteAsync(null, ct);
            };

            await using var srv = await TestH2Server.StartAsync(BufferedUnused, StreamingHandler: streaming);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var ssl = await H2Raw.ConnectTlsAsync(srv.Port, CancellationToken: cts.Token);
            await H2Raw.HandshakeAsync(ssl, cts.Token);

            var encoder     = new HPACKEncoder();
            var headerBlock = encoder.EncodeHeaderBlock(
                [(":method", "POST"), (":scheme", "https"), (":authority", $"localhost:{srv.Port}"), (":path", "/stream")]);
            await ssl.WriteAsync(HTTP2Frame.CreateHeaders(1, headerBlock, EndStream: false, EndHeaders: true).Serialize(), cts.Token);

            // Upload 34 x 16 KiB = 544 KiB — past half the window (524288), under 1 MiB.
            const Int32 frameSize  = 16384;
            const Int32 frameCount = 34;
            var chunk = new Byte[frameSize];
            new Random(11).NextBytes(chunk);
            for (var i = 0; i < frameCount; i++)
                await ssl.WriteAsync(HTTP2Frame.CreateData(1, chunk, EndStream: false).Serialize(), cts.Token);
            await ssl.FlushAsync(cts.Token);

            // Phase 1: gate shut -> handler consumed nothing -> NO WINDOW_UPDATE.
            var windowUpdatesWhileGated = 0;
            try
            {
                using var idle = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                idle.CancelAfter(TimeSpan.FromMilliseconds(700));
                while (true)
                {
                    var f = await H2Raw.ReadFrameAsync(ssl, idle.Token);
                    if (f is null) break;
                    if (f.Type == HTTP2FrameType.WINDOW_UPDATE) windowUpdatesWhileGated++;
                }
            }
            catch (OperationCanceledException) { /* expected: idle timeout, no frames */ }

            Assert.That(windowUpdatesWhileGated, Is.EqualTo(0),
                        "no WINDOW_UPDATE while the handler has consumed nothing (backpressure holds)");

            // Phase 2: open the gate -> handler drains -> window credited back, then 200.
            gate.SetResult();
            await ssl.WriteAsync(HTTP2Frame.CreateData(1, [], EndStream: true).Serialize(), cts.Token);
            await ssl.FlushAsync(cts.Token);

            Int64   credited = 0;
            String? status   = null;
            while (status is null)
            {
                var f = await H2Raw.ReadFrameAsync(ssl, cts.Token);
                if (f is null) break;
                if (f.Type == HTTP2FrameType.WINDOW_UPDATE)
                    credited += BinaryPrimitives.ReadUInt32BigEndian(f.Payload) & 0x7FFFFFFFu;
                else if (f.Type == HTTP2FrameType.HEADERS)
                    status = new HPACKDecoder().DecodeHeaderBlock(f.Payload).FirstOrDefault(h => h.Name == ":status").Value;
            }

            Assert.Multiple(() =>
            {
                Assert.That(credited, Is.GreaterThanOrEqualTo(HalfWindow), "window credited back once the handler consumes the body");
                Assert.That(status,   Is.EqualTo("200"),                  "streaming request completes");
            });

            try { ssl.Close(); } catch { }

        }

        #endregion

        #region BufferedBody_BoundedByMaxRequestBodySize()

        [Test]
        public async Task BufferedBody_BoundedByMaxRequestBodySize()
        {

            const Int32 cap = 1024;   // tiny cap for the test

            await using var srv = await TestH2Server.StartAsync(
                (s, h, body, c) =>
                {
                    var reply = Encoding.UTF8.GetBytes($"got {body?.Length ?? 0}");
                    return Task.FromResult<(List<(String, String)>, Byte[]?)>(
                        ([(":status", "200"), ("content-length", reply.Length.ToString())], reply));
                },
                MaxRequestBodySize: cap);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var ssl = await H2Raw.ConnectTlsAsync(srv.Port, CancellationToken: cts.Token);
            await H2Raw.HandshakeAsync(ssl, cts.Token);

            var encoder = new HPACKEncoder();

            List<(String, String)> Req(String method, String? contentLength)
            {
                var hdrs = new List<(String, String)>
                {
                    (":method", method), (":scheme", "https"), (":authority", $"localhost:{srv.Port}"), (":path", "/upload")
                };
                if (contentLength is not null) hdrs.Add(("content-length", contentLength));
                return hdrs;
            }

            async Task<(Byte? RstCode, String? Status)> DriveResponse(UInt32 streamId)
            {
                while (true)
                {
                    var f = await H2Raw.ReadFrameAsync(ssl, cts.Token);
                    if (f is null) return (null, null);
                    if (f.Type == HTTP2FrameType.RST_STREAM && f.StreamId == streamId)
                        return ((Byte) BinaryPrimitives.ReadUInt32BigEndian(f.Payload), null);
                    if (f.Type == HTTP2FrameType.HEADERS && f.StreamId == streamId)
                        return (null, new HPACKDecoder().DecodeHeaderBlock(f.Payload).FirstOrDefault(h => h.Name == ":status").Value);
                }
            }

            // (a) Declared content-length over the cap -> refused up front.
            await ssl.WriteAsync(HTTP2Frame.CreateHeaders(1, encoder.EncodeHeaderBlock(Req("POST", (cap * 100).ToString())), EndStream: false, EndHeaders: true).Serialize(), cts.Token);
            await ssl.FlushAsync(cts.Token);
            var (rst1, _) = await DriveResponse(1);

            // (b) Undeclared length, body streamed past the cap -> refused mid-stream.
            await ssl.WriteAsync(HTTP2Frame.CreateHeaders(3, encoder.EncodeHeaderBlock(Req("POST", null)), EndStream: false, EndHeaders: true).Serialize(), cts.Token);
            var body = new Byte[512];
            new Random(5).NextBytes(body);
            for (var i = 0; i < 4; i++)   // 4 x 512 = 2048 > 1024 cap
                await ssl.WriteAsync(HTTP2Frame.CreateData(3, body, EndStream: false).Serialize(), cts.Token);
            await ssl.FlushAsync(cts.Token);
            var (rst2, _) = await DriveResponse(3);

            // (c) A body under the cap still succeeds — the connection stayed usable.
            await ssl.WriteAsync(HTTP2Frame.CreateHeaders(5, encoder.EncodeHeaderBlock(Req("POST", "10")), EndStream: false, EndHeaders: true).Serialize(), cts.Token);
            await ssl.WriteAsync(HTTP2Frame.CreateData(5, Encoding.UTF8.GetBytes("0123456789"), EndStream: true).Serialize(), cts.Token);
            await ssl.FlushAsync(cts.Token);
            var (_, status3) = await DriveResponse(5);

            Assert.Multiple(() =>
            {
                Assert.That(rst1,    Is.EqualTo((Byte) HTTP2ErrorCode.ENHANCE_YOUR_CALM), "over-cap declared content-length refused");
                Assert.That(rst2,    Is.EqualTo((Byte) HTTP2ErrorCode.ENHANCE_YOUR_CALM), "over-cap streamed body refused");
                Assert.That(status3, Is.EqualTo("200"),                                   "under-cap body still succeeds (connection usable)");
            });

            try { ssl.Close(); } catch { }

        }

        #endregion

    }

}
