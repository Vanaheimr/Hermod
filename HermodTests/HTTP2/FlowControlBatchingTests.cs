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
    /// WINDOW_UPDATE batching: a raw frame-level client uploads a large request
    /// body and counts the flow-control frames the server sends back. The batched
    /// strategy replenishes only once accumulated consumption crosses half the
    /// window, so the count is a tiny fraction of the DATA-frame count. Also
    /// verifies the server raises the connection window above the 65535 default
    /// with an initial WINDOW_UPDATE. In-process.
    /// </summary>
    [TestFixture]
    public class FlowControlBatchingTests
    {

        #region (handler)

        // Reads (discards) the body and returns a tiny response, so the response
        // direction needs no client-side flow control.
        private static Task<(List<(String, String)>, Byte[]?)> Handler(UInt32 StreamId,
                                                                       List<(String Name, String Value)> Headers,
                                                                       Byte[]? Body,
                                                                       CancellationToken CancellationToken)
        {
            var reply = "ok"u8.ToArray();
            return Task.FromResult<(List<(String, String)>, Byte[]?)>(
                ([(":status", "200"), ("content-length", reply.Length.ToString())], reply));
        }

        #endregion


        #region WindowUpdatesAreBatched()

        [Test]
        public async Task WindowUpdatesAreBatched()
        {

            await using var srv = await TestH2Server.StartAsync(Handler);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            using var ssl = await H2Raw.ConnectTlsAsync(srv.Port, CancellationToken: cts.Token);

            var startupBump = await H2Raw.HandshakeAsync(ssl, cts.Token);
            Assert.That(startupBump, Is.GreaterThan(65535), "server raised the connection window at startup");

            // POST an 800 KiB body as 50 x 16 KiB DATA frames (under the 1 MiB
            // window, so no client-side flow control is needed to complete).
            const Int32 frameSize  = 16384;
            const Int32 frameCount = 50;
            const Int32 bodyLength = frameSize * frameCount;

            var encoder     = new HPACKEncoder();
            var headerBlock = encoder.EncodeHeaderBlock(
            [
                (":method", "POST"), (":scheme", "https"), (":authority", $"localhost:{srv.Port}"),
                (":path", "/upload"), ("content-length", bodyLength.ToString())
            ]);
            await ssl.WriteAsync(HTTP2Frame.CreateHeaders(1, headerBlock, EndStream: false, EndHeaders: true).Serialize(), cts.Token);

            var chunk = new Byte[frameSize];
            new Random(3).NextBytes(chunk);
            for (var i = 0; i < frameCount; i++)
                await ssl.WriteAsync(HTTP2Frame.CreateData(1, chunk, EndStream: i == frameCount - 1).Serialize(), cts.Token);
            await ssl.FlushAsync(cts.Token);

            // Read the response, counting WINDOW_UPDATE frames the server sends.
            var     windowUpdates = 0;
            String? status        = null;
            while (status is null)
            {
                var f = await H2Raw.ReadFrameAsync(ssl, cts.Token);
                if (f is null) break;
                if (f.Type == HTTP2FrameType.WINDOW_UPDATE)
                    windowUpdates++;
                else if (f.Type == HTTP2FrameType.HEADERS)
                    status = new HPACKDecoder().DecodeHeaderBlock(f.Payload).FirstOrDefault(h => h.Name == ":status").Value;
            }

            Assert.Multiple(() =>
            {
                Assert.That(status,        Is.EqualTo("200"),               "large upload succeeds (:status 200)");
                Assert.That(windowUpdates, Is.LessThanOrEqualTo(10),        $"WINDOW_UPDATEs batched ({windowUpdates} for {frameCount} DATA frames)");
                Assert.That(windowUpdates, Is.LessThan(frameCount),         "far fewer than the old 2-per-DATA-frame strategy");
            });

            try { ssl.Close(); } catch { }

        }

        #endregion

    }

}
