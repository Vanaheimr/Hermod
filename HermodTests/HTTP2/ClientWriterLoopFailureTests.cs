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
using System.Diagnostics.Tracing;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

using static org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2.ClientWire;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// The client's DATA writer loop failing while it sends for one stream. The
    /// loop sends every request body, tunnel write and END_STREAM on DATA of its
    /// connection, while each request's HEADERS go out from the request's own
    /// task. It handles its failures as the server's writer loop handles its
    /// own (see ServerWriterLoopFailureTests): a stream error resets that one
    /// stream, and the loop serves the others on; anything else ends the
    /// connection at once, with GOAWAY INTERNAL_ERROR, and every request on it
    /// fails with the reason — rather than leave the connection taking requests
    /// whose bodies nothing would send.
    /// </summary>
    [TestFixture]
    public class ClientWriterLoopFailureTests
    {

        #region (helpers)

        /// <summary>
        /// The events of the stack's EventSource while this listener lives.
        /// </summary>
        private sealed class HTTP2Events : EventListener
        {

            // Initialised with the declaration: the base constructor may already
            // call OnEventSourceCreated, and events may follow at once.
            private readonly List<(String Name, String Payload)> events = [];

            protected override void OnEventSourceCreated(EventSource Source)
            {
                if (Source.Name == "Vanaheimr-Hermod-HTTP2")
                    EnableEvents(Source, EventLevel.Verbose);
            }

            protected override void OnEventWritten(EventWrittenEventArgs Event)
            {
                if (Event.EventName != "EventCounters")
                    lock (events)
                        events.Add((Event.EventName ?? "?", String.Join(" ", Event.Payload ?? [])));
            }

            /// <summary>
            /// The payload of every event of this name so far, joined by spaces.
            /// </summary>
            public List<String> Named(String Name)
            {
                lock (events)
                    return [.. events.Where(e => e.Name == Name).Select(e => e.Payload)];
            }

        }

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
        /// A failure as the caller reads it: its type, for a stream error the
        /// stream and the error code, and its message.
        /// </summary>
        private static String? Describe(Exception? Failure)

            => Failure switch {
                   null                     => null,
                   HTTP2StreamException e   => $"{nameof(HTTP2StreamException)} on stream {e.StreamId}, {e.ErrorCode}: {e.Message}",
                   _                        => $"{Failure.GetType().Name}: {Failure.Message}"
               };

        #endregion


        #region AWriteRacingTheEndOfItsRequest_ResetsThatStreamAlone()

        /// <summary>
        /// A write races the end of its own request: the request is still open on
        /// our side when the write is queued, behind the END_STREAM the writer loop
        /// has taken and waits with for the write lock — another request's start
        /// holds it, held by the test in the write of its HEADERS. Once the loop
        /// has sent the END_STREAM, the write's DATA would follow it onto a stream
        /// half-closed (local) (RFC 9113, Section 5.1): a stream error of ours,
        /// the kind of failure the loop has to survive. It sends no such DATA. It
        /// resets that stream with RST_STREAM INTERNAL_ERROR; the write and the
        /// response side of the request fail with that code; the end, which went
        /// out, stands; and the loop goes on, with another upload on the
        /// connection written and ended in full.
        /// </summary>
        [Test]
        public async Task AWriteRacingTheEndOfItsRequest_ResetsThatStreamAlone()
        {

            using var events           = new HTTP2Events();
            await using var transport  = new HoldingH2Transport(HoldHeadersOf: streamId => streamId == 5);
            await using var wire       = new ClientWire(transport);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var upload      = await StartUploadAsync(connection, "/upload");
            var other       = await StartUploadAsync(connection, "/other");
            var holding     = StartUploadAsync(connection, "/holding");

            await wire.UntilAsync(lines => lines.Contains("5 HEADERS POST /holding"), "the HEADERS of the request that holds the write lock");

            var ending      = upload.CompleteRequestAsync();
            var writing     = upload.WriteAsync(ASCII("too late"));
            var queued      = !writing.IsCompleted;

            transport.Release(5);

            var (writeEnded, writeFailure)  = await EndOf(writing);
            var endFailure                  = (await EndOf(ending)).Failure;
            var responseFailure             = (await EndOf(upload.GetResponseAsync())).Failure;
            var holdingStarted              = (await EndOf(holding)).Ended;

            await other.WriteAsync(ASCII("other")).        WaitAsync(HoldingH2Transport.StepTimeout);
            await other.CompleteRequestAsync().            WaitAsync(HoldingH2Transport.StepTimeout);

            var lines = await wire.UntilAsync(lines => lines.Contains($"{other.StreamId} DATA 0 END_STREAM") &&
                                                       lines.Contains($"{upload.StreamId} RST_STREAM INTERNAL_ERROR"),
                                              "the other upload's end, and the reset of the first");

            var reset = $"{nameof(HTTP2StreamException)} on stream {upload.StreamId}, INTERNAL_ERROR: Stream reset by client: INTERNAL_ERROR";

            Assert.Multiple(() =>
            {

                Assert.That(queued,                     Is.True,                                                  "the write, queued while the request was still open on our side");
                Assert.That(holdingStarted,             Is.True,                                                  "the request that held the write lock started");

                Assert.That(On(lines, upload.StreamId), Is.EqualTo(new[] { $"{upload.StreamId} HEADERS POST /upload",
                                                                           $"{upload.StreamId} DATA 0 END_STREAM",
                                                                           $"{upload.StreamId} RST_STREAM INTERNAL_ERROR" }),
                                                                                                                  "what the client sent on the first upload's stream: its end, and the reset — no DATA after the END_STREAM");

                Assert.That(Describe(endFailure),       Is.Null,                                                  "how the end failed, which went out");
                Assert.That(writeEnded,                 Is.True,                                                  "the write ended while the connection was open");
                Assert.That(Describe(writeFailure),     Is.EqualTo(reset),                                        "how the write failed");
                Assert.That(Describe(responseFailure),  Is.EqualTo(reset),                                        "how the response side of the first upload failed");

                Assert.That(On(lines, other.StreamId),  Is.EqualTo(new[] { $"{other.StreamId} HEADERS POST /other",
                                                                           $"{other.StreamId} DATA 5",
                                                                           $"{other.StreamId} DATA 0 END_STREAM" }),
                                                                                                                  "what the client sent on the other upload's stream, after the reset as before it");

                Assert.That(connection.IsUsable,        Is.True,                                                  "the connection, still usable");

                Assert.That(events.Named("StreamError"),
                                                        Has.Some.StartsWith($"{upload.StreamId} STREAM_CLOSED DATA on stream {upload.StreamId} after our END_STREAM"),
                                                                                                                  "the error logged, with the code and message it was thrown with");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region ServerDataCrossingATunnelsReset_IsDropped_TheConnectionStays()

        /// <summary>
        /// A write races the close of a tunnel, as a write races the end of its
        /// request above, and the writer loop resets the tunnel. While its
        /// RST_STREAM is on the way — the test holds the client in the write of
        /// it — the server's DATA for the tunnel arrives, sent before the server
        /// read the reset, as RFC 9113, Section 5.1 allows. The read loop drops it:
        /// the reset, made on the writer loop's thread, has completed the tunnel's
        /// inbound channel, and nothing reads it any more. Writing into that
        /// channel used to throw into the read loop, and end the connection. The
        /// tunnel reads its end, the write fails with the reset's code, and the
        /// connection serves the next request.
        /// </summary>
        [Test]
        public async Task ServerDataCrossingATunnelsReset_IsDropped_TheConnectionStays()
        {

            await using var transport  = new HoldingH2Transport(HoldHeadersOf: streamId => streamId == 3,
                                                                HoldResetOf:   streamId => streamId == 1);
            await using var wire       = new ClientWire(transport);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var opening     = connection.OpenTunnelAsync("echo.internal:443");

            await wire.UntilAsync(lines => lines.Contains("1 HEADERS CONNECT echo.internal:443"), "the CONNECT");

            // ":status: 200" is in HPACK's static table, so this block leaves the
            // client's dynamic table as it is.
            await transport.SendAsync(HTTP2Frame.CreateHeaders(1,
                                                               new HPACKEncoder().EncodeHeaderBlock([(":status", "200")]),
                                                               EndStream:  false,
                                                               EndHeaders: true));

            var tunnel      = await opening.WaitAsync(HoldingH2Transport.StepTimeout);
            var holding     = StartUploadAsync(connection, "/holding");

            await wire.UntilAsync(lines => lines.Contains("3 HEADERS POST /holding"), "the HEADERS of the request that holds the write lock");

            var closing     = tunnel.CloseAsync();
            var writing     = tunnel.WriteAsync(ASCII("too late"), CancellationToken.None);

            transport.Release(3);

            await wire.UntilAsync(lines => lines.Contains("1 RST_STREAM INTERNAL_ERROR"), "the reset of the tunnel");

            var crossed     = await EndOf(transport.SendAsync(HTTP2Frame.CreateData(1, ASCII("from the server"), EndStream: false)));

            transport.Release(1);

            var read            = await tunnel.ReadAsync(CancellationToken.None).WaitAsync(HoldingH2Transport.StepTimeout);
            var writeFailure    = (await EndOf(writing)).Failure;
            var closeFailure    = (await EndOf(closing)).Failure;
            var holdingStarted  = (await EndOf(holding)).Ended;

            await StartUploadAsync(connection, "/next");

            var lines = await wire.UntilAsync(lines => lines.Contains("5 HEADERS POST /next"), "the next request's HEADERS");

            Assert.Multiple(() =>
            {

                Assert.That(crossed.Ended,           Is.True,                                                     "the read loop handled the server's DATA");
                Assert.That(Describe(crossed.Failure), Is.Null,                                                   "how sending the server's DATA failed");
                Assert.That(holdingStarted,          Is.True,                                                     "the request that held the write lock started");

                Assert.That(On(lines, 1),            Is.EqualTo(new[] { "1 HEADERS CONNECT echo.internal:443",
                                                                        "1 DATA 0 END_STREAM",
                                                                        "1 RST_STREAM INTERNAL_ERROR" }),        "what the client sent on the tunnel's stream: its end, and the reset");

                Assert.That(read,                    Is.Null,                                                     "what the tunnel read: its end, without the server's late DATA");
                Assert.That(Describe(closeFailure),  Is.Null,                                                     "how the close failed, which went out");
                Assert.That(Describe(writeFailure),  Is.EqualTo($"{nameof(HTTP2StreamException)} on stream 1, INTERNAL_ERROR: Stream reset by client: INTERNAL_ERROR"),
                                                                                                                  "how the write failed");

                Assert.That(connection.IsUsable,     Is.True,                                                     "the connection, still usable");
                Assert.That(lines,                   Has.Member("5 HEADERS POST /next"),                          "the next request, sent");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region OtherFailure_EndsTheConnectionAtOnce()

        /// <summary>
        /// A failure that is no stream error leaves the connection's state in
        /// doubt: here the transport fails the write of an upload's DATA. The
        /// client ends the connection at once, with GOAWAY INTERNAL_ERROR, instead
        /// of going on to start requests whose bodies nothing would send. The
        /// requests on it fail: the upload's response side with the transport's
        /// failure itself, and a request still waiting for its answer as well,
        /// rather than wait for one that never comes.
        /// </summary>
        [Test]
        public async Task OtherFailure_EndsTheConnectionAtOnce()
        {

            using var events           = new HTTP2Events();
            await using var transport  = new HoldingH2Transport(HoldHeadersOf: streamId => false);
            await using var wire       = new ClientWire(transport);

            var failure     = new IOException($"The write failed on purpose ({Guid.NewGuid()})");
            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());

            var pending     = await connection.StartRequestAsync(HTTPMethod.GET, URIScheme.http, "localhost", "/pending").
                                               WaitAsync(HoldingH2Transport.StepTimeout);

            var upload      = await StartUploadAsync(connection, "/upload");

            transport.FailNextDataWrite(upload.StreamId, failure);

            var writing     = upload.WriteAsync(ASCII("on a broken transport"));

            var ended       = await Task.WhenAny(connection.Closed, Task.Delay(HoldingH2Transport.StepTimeout)) == connection.Closed;

            var lines       = await wire.UntilAsync(lines => lines.Any(line => line.Contains(" GOAWAY ")), "a GOAWAY");

            var (pendingEnded, pendingFailure)  = await EndOf(pending.Response);
            var uploadFailure                   = (await EndOf(upload.GetResponseAsync())).Failure;

            await EndOf(writing);

            Assert.Multiple(() =>
            {

                Assert.That(ended,                       Is.True,                                         "the connection ended at once, rather than go on without its writer");
                Assert.That(connection.IsUsable,         Is.False,                                        "the connection, usable no more");

                Assert.That(lines.Where(line => line.Contains(" GOAWAY ")),
                                                         Is.EqualTo(new[] { "0 GOAWAY INTERNAL_ERROR" }), "how the client ended the connection");

                Assert.That(On(lines, upload.StreamId),  Is.EqualTo(new[] { $"{upload.StreamId} HEADERS POST /upload" }),
                                                                                                          "what the client sent on the upload's stream: its HEADERS, and nothing of the failed DATA");

                Assert.That(Describe(uploadFailure),     Is.EqualTo($"{nameof(IOException)}: {failure.Message}"),
                                                                                                          "how the upload's response side failed: with the transport's failure");

                Assert.That(pendingEnded,                Is.True,                                         "the request waiting for its answer ended");
                Assert.That(pendingFailure,              Is.Not.Null,                                     "the request waiting for its answer failed");

                Assert.That(events.Named("ConnectionError"),
                                                         Has.Some.Matches<String>(payload => payload.StartsWith("WRITER_LOOP ") && payload.Contains(failure.Message)),
                                                                                                          "the failure logged");

            });

        }

        #endregion

    }

}
