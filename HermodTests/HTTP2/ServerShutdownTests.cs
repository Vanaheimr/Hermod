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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Verifies <see cref="HTTP2Server"/> graceful shutdown: <c>StopAsync</c> sends
    /// a GOAWAY (NO_ERROR, last-stream-id reflecting the served stream) to connected
    /// peers instead of silently cancelling, and the accept loop actually returns.
    /// In-process.
    /// </summary>
    [TestFixture]
    public class ServerShutdownTests
    {

        #region (handler)

        private static Task<(List<(String, String)>, Byte[]?)> Handler(UInt32 StreamId,
                                                                      List<(String Name, String Value)> Headers,
                                                                      Byte[]? Body,
                                                                      CancellationToken CancellationToken)
            => Task.FromResult<(List<(String, String)>, Byte[]?)>(
                   ([(":status", "200"), ("content-length", "2")], "ok"u8.ToArray()));

        #endregion


        #region StopAsync_SendsGoawayAndStopsListener()

        [Test]
        public async Task StopAsync_SendsGoawayAndStopsListener()
        {

            await using var srv = await TestH2Server.StartAsync(Handler);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var ssl = await H2Raw.ConnectTlsAsync(srv.Port, CancellationToken: cts.Token);

            await ssl.WriteAsync(H2Raw.Preface, cts.Token);
            await ssl.WriteAsync(HTTP2Frame.CreateSettings().Serialize(), cts.Token);
            await ssl.FlushAsync(cts.Token);

            var goaway    = new TaskCompletionSource<(HTTP2ErrorCode Code, UInt32 LastStreamId)>(TaskCreationOptions.RunContinuationsAsynchronously);
            var status200 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            // Background reader: drains frames, signalling the 200 and the GOAWAY.
            _ = Task.Run(async () =>
            {
                var decoder = new HPACKDecoder();
                try
                {
                    while (true)
                    {
                        var f = await H2Raw.ReadFrameAsync(ssl, cts.Token);
                        if (f is null) break;

                        if (f.Type == HTTP2FrameType.GOAWAY)
                        {
                            var code = (HTTP2ErrorCode) BinaryPrimitives.ReadUInt32BigEndian(f.Payload.AsSpan(4, 4));
                            var last = BinaryPrimitives.ReadUInt32BigEndian(f.Payload.AsSpan(0, 4)) & 0x7FFFFFFFu;
                            goaway.TrySetResult((code, last));
                        }
                        else if (f.Type == HTTP2FrameType.HEADERS)
                        {
                            if (decoder.DecodeHeaderBlock(f.Payload).Any(h => h.Name == ":status" && h.Value == "200"))
                                status200.TrySetResult();
                        }
                    }
                }
                catch { /* connection closed once shutdown completes — expected */ }
            });

            // A normal request -> 200 baseline.
            var encoder  = new HPACKEncoder();
            var reqBlock = encoder.EncodeHeaderBlock(
                [(":method", "GET"), (":scheme", "https"), (":authority", "localhost"), (":path", "/")]);
            await ssl.WriteAsync(new HTTP2Frame
            {
                Type     = HTTP2FrameType.HEADERS,
                StreamId = 1,
                Flags    = HTTP2FrameFlags.END_HEADERS | HTTP2FrameFlags.END_STREAM,
                Payload  = reqBlock
            }.Serialize(), cts.Token);
            await ssl.FlushAsync(cts.Token);

            await status200.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Now stop, and observe the GOAWAY + the listener actually stopping.
            await srv.StopAsync();

            var (code, lastStreamId) = await goaway.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Multiple(() =>
            {
                Assert.That(code,         Is.EqualTo(HTTP2ErrorCode.NO_ERROR), "GOAWAY carries NO_ERROR");
                Assert.That(lastStreamId, Is.EqualTo(1u),                      "GOAWAY last-stream-id reflects the served stream");
            });

            var listenerStopped = await Task.WhenAny(srv.Running, Task.Delay(3000)) == srv.Running;
            Assert.That(listenerStopped, Is.True, "RunAsync returned — listener actually stopped");

        }

        #endregion

    }

}
