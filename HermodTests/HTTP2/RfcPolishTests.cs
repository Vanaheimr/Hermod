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
    /// MUST-level RFC 9113 details h2spec does not cover: padded DATA counts fully
    /// against flow control (Section 6.1); DATA on a closed stream is still
    /// credited back to the CONNECTION window while drawing RST_STREAM/STREAM_CLOSED
    /// (Section 6.9); crumbled cookie field lines are reassembled into one
    /// "; "-joined field (Section 8.2.3). In-process.
    /// </summary>
    [TestFixture]
    public class RfcPolishTests
    {

        private const Int32 HalfWindow = 1_048_576 / 2;   // half the advertised 1 MiB initial window

        #region Server

        // /cookie echoes the received cookie header line(s), "|"-separated (so
        // un-reassembled crumbs show as "a=1|b=2"); everything else answers "ok".
        private static Task<(List<(String, String)>, Byte[]?)> Handler(UInt32 s, List<(String Name, String Value)> h, Byte[]? body, CancellationToken ct)
        {
            var path  = h.First(x => x.Name == ":path").Value;
            var reply = path == "/cookie"
                            ? Encoding.UTF8.GetBytes(String.Join("|", h.Where(x => x.Name == "cookie").Select(x => x.Value)))
                            : Encoding.UTF8.GetBytes("ok");
            return Task.FromResult<(List<(String, String)>, Byte[]?)>(
                ([(":status", "200"), ("content-length", reply.Length.ToString())], reply));
        }

        private TestH2Server srv = null!;

        [OneTimeSetUp]
        public async Task StartServer()
            => srv = await TestH2Server.StartAsync(Handler);

        [OneTimeTearDown]
        public async Task StopServer()
            => await srv.DisposeAsync();

        #endregion

        #region (helper) CreatePaddedData

        // A padded DATA frame: [Pad Length][data][padding], PADDED flag set.
        private static HTTP2Frame CreatePaddedData(UInt32 streamId, Byte[] data, Byte padLength, Boolean endStream)
        {
            var payload = new Byte[1 + data.Length + padLength];
            payload[0] = padLength;
            data.CopyTo(payload, 1);
            var f = HTTP2Frame.CreateData(streamId, payload, endStream);
            f.Flags |= HTTP2FrameFlags.PADDED;
            return f;
        }

        #endregion


        #region PaddedData_CountsFullyAgainstFlowControl()

        [Test]
        public async Task PaddedData_CountsFullyAgainstFlowControl()
        {

            // 33 padded frames: 16128 data + 1 pad-length + 255 padding = 16384 on
            // the wire. Padded bytes cross the half-window threshold (524288) at
            // frame 32 (32 x 16384); stripped data alone only at frame 33. So the
            // FIRST WINDOW_UPDATE increment reveals which length was accounted.
            const Int32 dataSize   = 16128;
            const Byte  padLength  = 255;
            const Int32 frameCount = 33;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var ssl = await H2Raw.ConnectTlsAsync(srv.Port, CancellationToken: cts.Token);
            await H2Raw.HandshakeAsync(ssl, cts.Token);

            var encoder     = new HPACKEncoder();
            var headerBlock = encoder.EncodeHeaderBlock(
                [(":method", "POST"), (":scheme", "https"), (":authority", $"localhost:{srv.Port}"),
                 (":path", "/upload"), ("content-length", (dataSize * frameCount).ToString())]);
            await ssl.WriteAsync(HTTP2Frame.CreateHeaders(1, headerBlock, EndStream: false, EndHeaders: true).Serialize(), cts.Token);

            var data = new Byte[dataSize];
            new Random(7).NextBytes(data);
            for (var i = 0; i < frameCount; i++)
                await ssl.WriteAsync(CreatePaddedData(1, data, padLength, endStream: i == frameCount - 1).Serialize(), cts.Token);
            await ssl.FlushAsync(cts.Token);

            Int64   firstStreamInc = 0, firstConnInc = 0;
            String? status         = null;
            while (status is null)
            {
                var f = await H2Raw.ReadFrameAsync(ssl, cts.Token);
                if (f is null) break;
                if (f.Type == HTTP2FrameType.WINDOW_UPDATE)
                {
                    var inc = BinaryPrimitives.ReadUInt32BigEndian(f.Payload) & 0x7FFFFFFFu;
                    if (f.StreamId == 1 && firstStreamInc == 0) firstStreamInc = inc;
                    if (f.StreamId == 0 && firstConnInc   == 0) firstConnInc   = inc;
                }
                else if (f.Type == HTTP2FrameType.HEADERS)
                    status = new HPACKDecoder().DecodeHeaderBlock(f.Payload).FirstOrDefault(h => h.Name == ":status").Value;
            }

            Assert.Multiple(() =>
            {
                Assert.That(status,         Is.EqualTo("200"),       "padded upload succeeds");
                Assert.That(firstStreamInc, Is.EqualTo(HalfWindow),  "first stream WINDOW_UPDATE at the padded byte count");
                Assert.That(firstConnInc,   Is.EqualTo(HalfWindow),  "first connection WINDOW_UPDATE at the padded byte count");
            });

            try { ssl.Close(); } catch { }

        }

        #endregion

        #region ClosedStreamData_ReplenishesConnectionWindow()

        [Test]
        public async Task ClosedStreamData_ReplenishesConnectionWindow()
        {

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var ssl = await H2Raw.ConnectTlsAsync(srv.Port, CancellationToken: cts.Token);
            await H2Raw.HandshakeAsync(ssl, cts.Token);

            var decoder = new HPACKDecoder();
            var encoder = new HPACKEncoder();

            // Open + cleanly finish stream 1 (ends up closed but known).
            await ssl.WriteAsync(HTTP2Frame.CreateHeaders(1, encoder.EncodeHeaderBlock(
                [(":method", "GET"), (":scheme", "https"), (":authority", $"localhost:{srv.Port}"), (":path", "/")]),
                EndStream: true, EndHeaders: true).Serialize(), cts.Token);
            await ssl.FlushAsync(cts.Token);

            while (true)
            {
                var f = await H2Raw.ReadFrameAsync(ssl, cts.Token) ?? throw new InvalidOperationException("connection ended");
                if (f.Type == HTTP2FrameType.HEADERS) decoder.DecodeHeaderBlock(f.Payload);
                if (f.StreamId == 1 && f.EndStream) break;
            }

            // Spray 33 x 16 KiB DATA at the closed stream 1 (540672 > 524288), then
            // a fresh request on stream 3.
            const Int32 sprayFrames = 33;
            var chunk = new Byte[16384];
            for (var i = 0; i < sprayFrames; i++)
                await ssl.WriteAsync(HTTP2Frame.CreateData(1, chunk, EndStream: false).Serialize(), cts.Token);

            await ssl.WriteAsync(HTTP2Frame.CreateHeaders(3, encoder.EncodeHeaderBlock(
                [(":method", "GET"), (":scheme", "https"), (":authority", $"localhost:{srv.Port}"), (":path", "/")]),
                EndStream: true, EndHeaders: true).Serialize(), cts.Token);
            await ssl.FlushAsync(cts.Token);

            var     rstStreamClosed = 0;
            Int64   connInc         = 0;
            String? status3         = null;
            while (status3 is null)
            {
                var f = await H2Raw.ReadFrameAsync(ssl, cts.Token);
                if (f is null) break;
                if (f.Type == HTTP2FrameType.RST_STREAM && f.StreamId == 1 &&
                    BinaryPrimitives.ReadUInt32BigEndian(f.Payload) == (UInt32) HTTP2ErrorCode.STREAM_CLOSED)
                    rstStreamClosed++;
                if (f.Type == HTTP2FrameType.WINDOW_UPDATE && f.StreamId == 0 && connInc == 0)
                    connInc = BinaryPrimitives.ReadUInt32BigEndian(f.Payload) & 0x7FFFFFFFu;
                if (f.Type == HTTP2FrameType.HEADERS && f.StreamId == 3)
                    status3 = decoder.DecodeHeaderBlock(f.Payload).FirstOrDefault(h => h.Name == ":status").Value;
            }

            Assert.Multiple(() =>
            {
                Assert.That(rstStreamClosed, Is.GreaterThanOrEqualTo(1), "closed-stream DATA draws RST_STREAM/STREAM_CLOSED");
                Assert.That(connInc,         Is.EqualTo(HalfWindow),     "closed-stream DATA credited back to the connection window");
                Assert.That(status3,         Is.EqualTo("200"),          "connection still usable afterwards");
            });

            try { ssl.Close(); } catch { }

        }

        #endregion

        #region CrumbledCookies_Reassembled()

        [Test]
        public async Task CrumbledCookies_Reassembled()
        {

            var client = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            var two = await client.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/cookie",
                          ExtraHeaders: [("cookie", "a=1"), ("cookie", "b=2")]);
            Assert.Multiple(() =>
            {
                Assert.That(two.Status,                        Is.EqualTo(200));
                Assert.That(Encoding.UTF8.GetString(two.Body),  Is.EqualTo("a=1; b=2"), "two crumbs arrive as one \"; \"-joined field");
            });

            var one = await client.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/cookie",
                          ExtraHeaders: [("cookie", "x=9")]);
            Assert.Multiple(() =>
            {
                Assert.That(one.Status,                        Is.EqualTo(200));
                Assert.That(Encoding.UTF8.GetString(one.Body),  Is.EqualTo("x=9"), "a single cookie line stays untouched");
            });

            await client.CloseAsync();

        }

        #endregion

    }

}
