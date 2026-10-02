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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

using static org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2.ClientWire;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// What a caller that writes on a client stream gets, now that the
    /// connection's DATA writer loop sends what it writes, from a queue per
    /// stream, as the server's writer loop sends response bodies:
    ///
    ///   - The end of a stream goes out behind what was written before it, on
    ///     DATA as on trailers.
    ///   - A write's CancellationToken ends the wait, not the write: what it
    ///     queued still goes out, in order, and an end after it follows it. The
    ///     token used to be ignored, and trailers went out ahead of a write that
    ///     waited for window, which then put its DATA after the END_STREAM.
    ///   - Nothing goes out after our side of the stream has ended (RFC 9113,
    ///     Section 5.1): a second end sends nothing, and a write or trailers
    ///     after the end fail at once. A second END_STREAM, and a write after the
    ///     end, used to go out onto the stream half-closed (local).
    /// </summary>
    [TestFixture]
    public class ClientWriteQueueTests
    {

        #region (helpers)

        private static Byte[] ASCII(String Text)

            => Encoding.ASCII.GetBytes(Text);

        /// <summary>
        /// Start a streamed POST, whose request side stays open.
        /// </summary>
        private static Task<HTTP2ClientStream> StartUploadAsync(HTTP2ClientConnection  Connection,
                                                                String                 Path)

            => Connection.StartStreamingRequestAsync(HTTPMethod.POST, URIScheme.http, "localhost", Path).
                          WaitAsync(HoldingH2Transport.StepTimeout);

        /// <summary>
        /// Open a plain CONNECT tunnel, and answer it with a 200.
        /// </summary>
        private static async Task<HTTP2ClientTunnel> OpenTunnelAsync(HoldingH2Transport     Transport,
                                                                     ClientWire             Wire,
                                                                     HTTP2ClientConnection  Connection)
        {

            var opening = Connection.OpenTunnelAsync("echo.internal:443");

            await Wire.UntilAsync(lines => lines.Contains("1 HEADERS CONNECT echo.internal:443"), "the CONNECT");

            // ":status: 200" is in HPACK's static table, so this block leaves the
            // client's dynamic table as it is.
            await Transport.SendAsync(HTTP2Frame.CreateHeaders(1,
                                                               new HPACKEncoder().EncodeHeaderBlock([(":status", "200")]),
                                                               EndStream:  false,
                                                               EndHeaders: true));

            return await opening.WaitAsync(HoldingH2Transport.StepTimeout);

        }

        /// <summary>
        /// How a call ended: whether it did within the step timeout, and with
        /// which exception, if any.
        /// </summary>
        private static async Task<(Boolean Ended, Exception? Failure)> EndOf(Task Call)
        {

            if (await Task.WhenAny(Call, Task.Delay(HoldingH2Transport.StepTimeout)) != Call)
                return (false, null);

            try
            {
                await Call;
                return (true, null);
            }
            catch (Exception e)
            {
                return (true, e);
            }

        }

        /// <summary>
        /// Start a next request, and wait for its HEADERS: whatever the client
        /// sent before them has been read by then.
        /// </summary>
        private static async Task<List<String>> NextRequestAsync(HTTP2ClientConnection  Connection,
                                                                 ClientWire             Wire,
                                                                 UInt32                 StreamId)
        {

            await StartUploadAsync(Connection, "/next");

            return await Wire.UntilAsync(lines => lines.Contains($"{StreamId} HEADERS POST /next"), "the next request's HEADERS");

        }

        #endregion


        #region ASecondEnd_SendsNothing(Tunnel)

        /// <summary>
        /// A streamed request, or a tunnel, is ended twice. The second end sends
        /// nothing, and returns: our side has ended already. It used to send a
        /// second END_STREAM onto the stream half-closed (local), which a server
        /// answers with a stream error STREAM_CLOSED (RFC 9113, Section 5.1).
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task ASecondEnd_SendsNothing(Boolean Tunnel)
        {

            await using var transport  = new HoldingH2Transport(HoldHeadersOf: streamId => false);
            await using var wire       = new ClientWire(transport);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());

            Func<Task> end;

            if (Tunnel)
            {
                var tunnel  = await OpenTunnelAsync(transport, wire, connection);
                end         = tunnel.CloseAsync;
            }
            else
            {
                var upload  = await StartUploadAsync(connection, "/upload");
                end         = () => upload.CompleteRequestAsync();
            }

            var first   = await EndOf(end());
            var second  = await EndOf(end());

            var lines   = await NextRequestAsync(connection, wire, 3);

            Assert.Multiple(() =>
            {

                Assert.That(first.Ended,   Is.True,  "the first end returned");
                Assert.That(first.Failure, Is.Null,  "how the first end failed");

                Assert.That(second.Ended,   Is.True, "the second end returned");
                Assert.That(second.Failure, Is.Null, "how the second end failed");

                Assert.That(On(lines, 1).Where(IsData), Is.EqualTo(new[] { "1 DATA 0 END_STREAM" }), "the DATA on the stream: one END_STREAM");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region AWriteOrTrailersAfterTheEnd_FailAtOnce_NothingSent()

        /// <summary>
        /// A streamed request is ended, and then written to, and ended once more,
        /// with trailers. Both fail at once with an InvalidOperationException, and
        /// send nothing: our side of the stream has ended. The write used to put
        /// DATA onto the stream half-closed (local), and the trailers a HEADERS
        /// frame.
        /// </summary>
        [Test]
        public async Task AWriteOrTrailersAfterTheEnd_FailAtOnce_NothingSent()
        {

            await using var transport  = new HoldingH2Transport(HoldHeadersOf: streamId => false);
            await using var wire       = new ClientWire(transport);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var upload      = await StartUploadAsync(connection, "/upload");

            await upload.CompleteRequestAsync().WaitAsync(HoldingH2Transport.StepTimeout);

            var writing     = upload.WriteAsync(ASCII("after the end"));
            var trailing    = upload.CompleteRequestAsync([("x-late", "after the end")]);

            var lines       = await NextRequestAsync(connection, wire, 3);

            Assert.Multiple(() =>
            {

                Assert.That(writing.IsFaulted,                       Is.True,                                      "the write, failed at once");
                Assert.That(writing.Exception?.InnerException,       Is.TypeOf<InvalidOperationException>(),       "how the write failed");

                Assert.That(trailing.IsFaulted,                      Is.True,                                      "the trailers, failed at once");
                Assert.That(trailing.Exception?.InnerException,      Is.TypeOf<InvalidOperationException>(),       "how the trailers failed");

                Assert.That(On(lines, 1),                            Is.EqualTo(new[] { "1 HEADERS POST /upload",
                                                                                         "1 DATA 0 END_STREAM" }), "what the client sent on the stream: its end, and nothing after it");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region AWriteGivenUpOn_StillGoesOut_AndTheEndFollowsIt(WithTrailers)

        /// <summary>
        /// A write waits for window, and its caller gives up on it: the token it
        /// passed is cancelled. The write is canceled with that token, but its
        /// bytes stay queued — half a write would leave a hole in what the stream
        /// carries — and the request is ended while they wait, without trailers or
        /// with them. Once the server grants window, the bytes go out, and the end
        /// after them. The token used to be ignored, and the trailers went out at
        /// once, ahead of the DATA, which then followed the END_STREAM.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task AWriteGivenUpOn_StillGoesOut_AndTheEndFollowsIt(Boolean WithTrailers)
        {

            await using var transport  = new HoldingH2Transport(HoldHeadersOf: streamId => false);
            await using var wire       = new ClientWire(transport);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());

            // No window for any new stream, until the test gives it.
            await transport.SendAsync(HTTP2Frame.CreateSettings((HTTP2SettingsParameter.INITIAL_WINDOW_SIZE, 0)));

            var upload      = await StartUploadAsync(connection, "/upload");

            using var giveUp = new CancellationTokenSource();

            var writing     = upload.WriteAsync(ASCII("given up on"), giveUp.Token);

            giveUp.Cancel();

            var (writeEnded, writeFailure) = await EndOf(writing);

            var ending      = WithTrailers
                                  ? upload.CompleteRequestAsync([("x-checksum", "deadbeef")])
                                  : upload.CompleteRequestAsync();

            var waited      = !ending.IsCompleted;

            await transport.SendAsync(HTTP2Frame.CreateWindowUpdate(upload.StreamId, 100));

            var endFailure  = (await EndOf(ending)).Failure;

            var lines       = await NextRequestAsync(connection, wire, 3);

            Assert.Multiple(() =>
            {

                Assert.That(writeEnded,                                         Is.True,               "the write ended, once its caller gave up on it");
                Assert.That((writeFailure as OperationCanceledException)?.CancellationToken,
                                                                                Is.EqualTo(giveUp.Token),
                                                                                                       "how the write ended: canceled, with the token its caller passed");

                Assert.That(waited,                                             Is.True,               "the end waited for the DATA queued before it");
                Assert.That(endFailure,                                         Is.Null,               "how the end failed");

                Assert.That(On(lines, 1),                                       Is.EqualTo(new[] { "1 HEADERS POST /upload",
                                                                                                   "1 DATA 11",
                                                                                                   WithTrailers
                                                                                                       ? "1 HEADERS trailers x-checksum=deadbeef END_STREAM"
                                                                                                       : "1 DATA 0 END_STREAM" }),
                                                                                                       "what the client sent on the stream: the bytes given up on, and then the end");

            });

            await connection.CloseAsync();

        }

        #endregion

    }

}
