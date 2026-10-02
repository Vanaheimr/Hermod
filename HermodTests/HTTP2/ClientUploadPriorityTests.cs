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
    /// The client's own sending by RFC 9218 priority. A charging station keeps
    /// one HTTP/2 connection to its backend and runs two WebSockets over it
    /// (RFC 8441): one for small real-time messages, one that uploads a log file
    /// in the background. The upload is the station's to send, and so is the
    /// order of the two. The client wrote each stream's DATA from the writing
    /// task itself: once the connection's send window ran out, whichever task
    /// the thread pool woke first took the next window, and the upload, which
    /// took its next window right after its last frame, mostly won it. A
    /// real-time message waited behind the upload.
    ///
    /// Now one writer loop per connection sends every DATA frame, as the
    /// server's does, a frame at a time, by the priority each stream asked the
    /// server for: the <c>Priority</c> of the request, tunnel or WebSocket, a
    /// <c>priority</c> field among its headers, or a later
    /// <see cref="HTTP2ClientConnection.UpdatePriorityAsync"/>.
    ///
    /// The tests play the server over an in-memory transport, frame by frame.
    /// The server announces room for any stream to send all it has, so only the
    /// connection's window, 65 535 octets until the server grants more, holds
    /// the client back; a bulk upload takes all of it, and the tests queue what
    /// is to overtake the upload while it waits. Then the server grants more,
    /// and the order the client sends in shows its choice. They wait for frames,
    /// never for time: nothing in them depends on how fast the machine is.
    /// </summary>
    [TestFixture]
    public class ClientUploadPriorityTests
    {

        #region (helpers)

        /// <summary>
        /// What RFC 9113 starts the connection's send window with, and what the
        /// server here never raises unless the test says so (Section 6.9.2).
        /// </summary>
        private const Int32 ConnectionWindow = 65535;

        /// <summary>
        /// Real-time messages, as OCPP 2.x sends them: a Heartbeat request, and a
        /// StatusNotification queued right behind it.
        /// </summary>
        private const String Heartbeat           = "[2,\"19223201\",\"Heartbeat\",{}]";
        private const String StatusNotification  = "[2,\"19223202\",\"StatusNotification\",{\"evseId\":1,\"connectorId\":1,\"connectorStatus\":\"Occupied\"}]";

        /// <summary>
        /// The bytes a client's WebSocket frame of a short text message takes:
        /// its two header octets and the four of its mask, then the text
        /// (RFC 6455, Section 5.2).
        /// </summary>
        private static Int32 WebSocketFrameLength(String Text)

            => 2 + 4 + Encoding.UTF8.GetByteCount(Text);

        private static Byte[] ASCII(String Text)

            => Encoding.ASCII.GetBytes(Text);

        /// <summary>
        /// Connect over an in-memory transport, and announce room for any stream
        /// to send all it has: 16 MiB of stream window, while the connection's
        /// window stays at its 65 535 octets until the test grants more.
        /// </summary>
        private static async Task<HTTP2ClientConnection> ConnectAsync(HoldingH2Transport Transport)
        {

            var connection = await Transport.ConnectAsync(new HTTP2ClientOptions());

            await Transport.SendAsync(HTTP2Frame.CreateSettings((HTTP2SettingsParameter.INITIAL_WINDOW_SIZE, 16 * 1024 * 1024)));

            return connection;

        }

        /// <summary>
        /// Open a WebSocket (RFC 8441), and answer its CONNECT with a 200.
        /// </summary>
        private static async Task<WebSocketConnection> OpenWebSocketAsync(HoldingH2Transport                  Transport,
                                                                          ClientWire                          Wire,
                                                                          HTTP2ClientConnection               Connection,
                                                                          String                              Path,
                                                                          HTTP2Priority?                      Priority      = null,
                                                                          List<(String Name, String Value)>?  ExtraHeaders  = null)
        {

            var opening  = Connection.OpenWebSocketAsync("localhost", URIScheme.https, Path, ExtraHeaders, Priority: Priority);

            var lines    = await Wire.UntilAsync(lines => lines.Any(line => line.Contains($" HEADERS CONNECT {Path}")),
                                                 $"the CONNECT for {Path}");

            var streamId = StreamOf(lines.Last(line => line.Contains($" HEADERS CONNECT {Path}")));

            // ":status: 200" is in HPACK's static table, so this block leaves the
            // client's dynamic table as it is.
            await Transport.SendAsync(HTTP2Frame.CreateHeaders(streamId,
                                                               new HPACKEncoder().EncodeHeaderBlock([(":status", "200")]),
                                                               EndStream:  false,
                                                               EndHeaders: true));

            return await opening.WaitAsync(HoldingH2Transport.StepTimeout);

        }

        /// <summary>
        /// Start a streamed POST, whose request side stays open.
        /// </summary>
        private static Task<HTTP2ClientStream> StartUploadAsync(HTTP2ClientConnection               Connection,
                                                                String                              Path,
                                                                HTTP2Priority?                      Priority      = null,
                                                                List<(String Name, String Value)>?  ExtraHeaders  = null)

            => Connection.StartStreamingRequestAsync(HTTPMethod.POST, URIScheme.http, "localhost", Path, ExtraHeaders, Priority).
                          WaitAsync(HoldingH2Transport.StepTimeout);

        /// <summary>
        /// Wait until a bulk upload on <paramref name="StreamId"/> has taken the
        /// connection's whole send window: everything else queued from now on
        /// waits for more window, and goes out once the server grants it, in the
        /// order the client chooses.
        /// </summary>
        private static Task UntilTheWindowIsTakenAsync(ClientWire Wire, UInt32 StreamId)

            => Wire.UntilAsync(lines => DataBytesOn(lines, StreamId) == ConnectionWindow,
                               $"{ConnectionWindow} bytes of DATA on stream {StreamId}, the connection's whole send window");

        /// <summary>
        /// Let go of a task the test no longer waits for — the rest of a bulk
        /// upload, which fails once the connection is closed — and still observe
        /// its failure, so that none goes unobserved.
        /// </summary>
        private static async Task ObserveAsync(Task Task)
        {
            try
            {
                await Task.WaitAsync(HoldingH2Transport.StepTimeout);
            }
            catch
            { }
        }

        #endregion


        #region AMessageOnAnUrgentWebSocket_OvertakesABulkUploadOnAnother()

        /// <summary>
        /// The station's case. A log file of a mebibyte goes out as one message on
        /// a WebSocket opened with urgency 6, incremental, and takes the
        /// connection's whole send window. Then a Heartbeat and a
        /// StatusNotification are sent on a WebSocket opened with urgency 0. Once
        /// the server grants more window, both go out first, whole and in order,
        /// and the upload goes on after them, until it has taken the window again:
        /// of what the upload still has queued, nothing goes before the messages.
        /// Five times over, two messages to each window the server grants. Both
        /// CONNECTs carry their priority to the server, which sends its own DATA by
        /// it.
        ///
        /// The writers used to race for each window granted, and the thread pool
        /// decided who went first: a single round did not tell the two apart
        /// reliably, five rounds do. And two messages a round, not one: the round
        /// robin among streams, which sends from the stream served least recently,
        /// would put a single message first even without priorities, but send a
        /// frame of the upload between two.
        /// </summary>
        [Test]
        public async Task AMessageOnAnUrgentWebSocket_OvertakesABulkUploadOnAnother()
        {

            const Int32 Rounds = 5;

            await using var transport  = new HoldingH2Transport(HoldHeadersOf: streamId => false);
            await using var wire       = new ClientWire(transport);

            var connection  = await ConnectAsync(transport);

            var logs        = await OpenWebSocketAsync(transport, wire, connection, "/logs", new HTTP2Priority(6, true));
            var ocpp        = await OpenWebSocketAsync(transport, wire, connection, "/ocpp", new HTTP2Priority(0, false));

            var logStream   = ((HTTP2ClientTunnel) logs.Tunnel).StreamId;
            var ocppStream  = ((HTTP2ClientTunnel) ocpp.Tunnel).StreamId;
            var realTime    = WebSocketFrameLength(Heartbeat) + WebSocketFrameLength(StatusNotification);
            var expected    = new[] { $"{ocppStream} DATA {WebSocketFrameLength(Heartbeat)}",
                                      $"{ocppStream} DATA {WebSocketFrameLength(StatusNotification)}" };

            var upload      = logs.SendBinaryAsync(new Byte[1024 * 1024], CancellationToken.None);

            await UntilTheWindowIsTakenAsync(wire, logStream);

            var firsts      = new List<List<String>>();
            var messages    = new List<Task>();
            var lines       = new List<String>();

            for (var round = 1; round <= Rounds; round++)
            {

                // Queued at once, before the calls return, while the upload has
                // taken the whole window: the writer loop has none for them yet.
                messages.Add(ocpp.SendTextAsync(Heartbeat,          CancellationToken.None));
                messages.Add(ocpp.SendTextAsync(StatusNotification, CancellationToken.None));

                var grantedAt = wire.Count;

                await transport.SendAsync(HTTP2Frame.CreateWindowUpdate(0, ConnectionWindow));

                // All of the window granted is taken again: the two messages, and
                // the upload the rest.
                lines = await wire.UntilAsync(lines => DataBytesOn(lines.Skip(grantedAt), ocppStream) == realTime &&
                                                       DataBytesOn(lines.Skip(grantedAt), logStream)  == ConnectionWindow - realTime,
                                              $"the messages of round {round}, and the upload's share of the window granted for them");

                firsts.Add([.. lines.Skip(grantedAt).Where(IsData).Take(2)]);

            }

            await Task.WhenAll(messages).WaitAsync(HoldingH2Transport.StepTimeout);

            Assert.Multiple(() =>
            {

                Assert.That(lines,   Has.Member($"{logStream} HEADERS CONNECT /logs priority=u=6, i"),  "the upload's CONNECT, with its priority");
                Assert.That(lines,   Has.Member($"{ocppStream} HEADERS CONNECT /ocpp priority=u=0"),    "the real-time WebSocket's CONNECT, with its priority");

                Assert.That(firsts,  Has.All.EqualTo(expected),                                         "the first DATA each time the window grew: both messages, whole, ahead of the upload");

            });

            await connection.CloseAsync();
            await ObserveAsync(upload);

        }

        #endregion

        #region QueuedRequests_GoOutByUrgency()

        /// <summary>
        /// Three uploads queue a little each while a bulk upload of urgency 7 has
        /// taken the window: one with a <c>Priority</c> of urgency 5, one whose
        /// headers carry a <c>priority</c> field of urgency 1, and one with
        /// neither, which has the default urgency 3. They queue in that order.
        /// Once the server grants window, they go out by urgency — 1, 3, 5 — and
        /// the bulk upload last. A <c>priority</c> field the caller puts among the
        /// headers orders the client's sending as the <c>Priority</c> does.
        /// </summary>
        [Test]
        public async Task QueuedRequests_GoOutByUrgency()
        {

            await using var transport  = new HoldingH2Transport(HoldHeadersOf: streamId => false);
            await using var wire       = new ClientWire(transport);

            var connection  = await ConnectAsync(transport);

            var bulk        = await StartUploadAsync(connection, "/bulk",  Priority: new HTTP2Priority(7, true));
            var five        = await StartUploadAsync(connection, "/five",  Priority: new HTTP2Priority(5, false));
            var one         = await StartUploadAsync(connection, "/one",   ExtraHeaders: [("priority", "u=1")]);
            var three       = await StartUploadAsync(connection, "/three");

            var upload      = bulk.WriteAsync(new Byte[256 * 1024]);

            await UntilTheWindowIsTakenAsync(wire, bulk.StreamId);

            var writes      = new[] {
                                  five. WriteAsync(ASCII("five")),
                                  one.  WriteAsync(ASCII("one")),
                                  three.WriteAsync(ASCII("three"))
                              };

            var grantedAt   = wire.Count;

            await transport.SendAsync(HTTP2Frame.CreateWindowUpdate(0, ConnectionWindow));

            var lines       = await wire.UntilAsync(lines => DataBytesOn(lines.Skip(grantedAt), bulk.StreamId) == ConnectionWindow - "fiveonethree".Length,
                                                    "the bulk upload's share of the window granted");

            var afterGrant  = lines.Skip(grantedAt).Where(IsData).ToList();

            await Task.WhenAll(writes).WaitAsync(HoldingH2Transport.StepTimeout);

            Assert.Multiple(() =>
            {

                Assert.That(lines,                         Has.Member($"{five. StreamId} HEADERS POST /five priority=u=5"),  "the HEADERS of the upload with a Priority");
                Assert.That(lines,                         Has.Member($"{one.  StreamId} HEADERS POST /one priority=u=1"),   "the HEADERS of the upload with a priority field");
                Assert.That(lines,                         Has.Member($"{three.StreamId} HEADERS POST /three"),              "the HEADERS of the upload with neither");

                Assert.That(afterGrant.Take(3),            Is.EqualTo(new[] { $"{one.  StreamId} DATA 3",
                                                                              $"{three.StreamId} DATA 5",
                                                                              $"{five. StreamId} DATA 4" }),                   "the first DATA once the window grew: by urgency, 1, 3, 5");

                Assert.That(afterGrant.Skip(3).Select(StreamOf),
                                                           Is.All.EqualTo(bulk.StreamId),                                     "the DATA after them: the bulk upload");

            });

            await connection.CloseAsync();
            await ObserveAsync(upload);

        }

        #endregion

        #region UpdatePriority_ReordersWhatIsQueued()

        /// <summary>
        /// Two WebSockets, opened with urgency 1 and 5, each queue a message while
        /// a bulk upload has taken the window, and once the server grants window,
        /// the first one's message goes out first. Then the second is moved up to
        /// urgency 0 (<see cref="WebSocketConnection.UpdatePriorityAsync"/>), and
        /// from then on its message goes out first, four times over, each time
        /// both queue one and the server grants window. The server is told with a
        /// PRIORITY_UPDATE (RFC 9218, Section 7.1), and the tunnel reports its new
        /// priority.
        /// </summary>
        [Test]
        public async Task UpdatePriority_ReordersWhatIsQueued()
        {

            const Int32 RoundsAfter = 4;

            await using var transport  = new HoldingH2Transport(HoldHeadersOf: streamId => false);
            await using var wire       = new ClientWire(transport);

            var connection  = await ConnectAsync(transport);

            // The less urgent one opened first: between streams the round robin
            // finds equal, the one opened first goes first, and that must not be
            // what puts the other one ahead before the update.
            var bulk        = await OpenWebSocketAsync(transport, wire, connection, "/logs",  new HTTP2Priority(7, true));
            var later       = await OpenWebSocketAsync(transport, wire, connection, "/later", new HTTP2Priority(5, false));
            var first       = await OpenWebSocketAsync(transport, wire, connection, "/first", new HTTP2Priority(1, false));

            var bulkStream  = ((HTTP2ClientTunnel) bulk. Tunnel).StreamId;
            var firstData   = $"{((HTTP2ClientTunnel) first.Tunnel).StreamId} DATA {WebSocketFrameLength("first")}";
            var laterTunnel =  (HTTP2ClientTunnel) later.Tunnel;
            var laterData   = $"{laterTunnel.StreamId} DATA {WebSocketFrameLength("later")}";

            var upload      = bulk.SendBinaryAsync(new Byte[1024 * 1024], CancellationToken.None);

            await UntilTheWindowIsTakenAsync(wire, bulkStream);

            // Both WebSockets queue a message while the upload has taken the
            // window, and the server grants more: the first two DATA frames then.
            async Task<List<String>> RoundAsync(String What)
            {

                var messages  = new[] {
                                    first.SendTextAsync("first", CancellationToken.None),
                                    later.SendTextAsync("later", CancellationToken.None)
                                };

                var grantedAt = wire.Count;

                await transport.SendAsync(HTTP2Frame.CreateWindowUpdate(0, ConnectionWindow));

                var lines     = await wire.UntilAsync(lines => DataBytesOn(lines.Skip(grantedAt), bulkStream) == ConnectionWindow - 2 * WebSocketFrameLength("first"),
                                                      $"{What}: both messages, and the bulk upload's share of the window granted");

                await Task.WhenAll(messages).WaitAsync(HoldingH2Transport.StepTimeout);

                return [.. lines.Skip(grantedAt).Where(IsData).Take(2)];

            }

            var before      = await RoundAsync("the round before the update");

            await later.UpdatePriorityAsync(new HTTP2Priority(0, false)).WaitAsync(HoldingH2Transport.StepTimeout);

            var after       = new List<List<String>>();

            for (var round = 1; round <= RoundsAfter; round++)
                after.Add(await RoundAsync($"round {round} after the update"));

            var lines       = await wire.UntilAsync(lines => lines.Contains($"0 PRIORITY_UPDATE {laterTunnel.StreamId} u=0"), "the PRIORITY_UPDATE");

            Assert.Multiple(() =>
            {

                Assert.That(before,               Is.EqualTo(new[] { firstData, laterData }),                       "the first DATA once the window grew, before the update: urgency 1, then 5");
                Assert.That(after,                Has.All.EqualTo(new[] { laterData, firstData }),                  "the first DATA each time the window grew after it: the WebSocket moved up, then the other one");

                Assert.That(lines,                Has.Member($"0 PRIORITY_UPDATE {laterTunnel.StreamId} u=0"),      "the PRIORITY_UPDATE for the WebSocket moved up");
                Assert.That(laterTunnel.Priority, Is.EqualTo(new HTTP2Priority(0, false)),                          "the priority of the WebSocket moved up");

            });

            await connection.CloseAsync();
            await ObserveAsync(upload);

        }

        #endregion

        #region EquallyUrgentIncrementalUploads_TakeTurns()

        /// <summary>
        /// Two uploads of the same urgency, both incremental, queue three DATA
        /// frames' worth each while a less urgent bulk upload has taken the window.
        /// Once the server grants window for six frames, the two take turns, a
        /// frame each (RFC 9218, Section 10: incremental responses of the same
        /// urgency share the bandwidth), and the bulk upload gets none of it.
        /// </summary>
        [Test]
        public async Task EquallyUrgentIncrementalUploads_TakeTurns()
        {

            const Int32 FrameSize = 16384;   // SETTINGS_MAX_FRAME_SIZE, which the server here leaves as it is

            await using var transport  = new HoldingH2Transport(HoldHeadersOf: streamId => false);
            await using var wire       = new ClientWire(transport);

            var connection  = await ConnectAsync(transport);

            var bulk        = await StartUploadAsync(connection, "/bulk", Priority: new HTTP2Priority(7, true));
            var a           = await StartUploadAsync(connection, "/a",    Priority: new HTTP2Priority(3, true));
            var b           = await StartUploadAsync(connection, "/b",    Priority: new HTTP2Priority(3, true));

            var upload      = bulk.WriteAsync(new Byte[256 * 1024]);

            await UntilTheWindowIsTakenAsync(wire, bulk.StreamId);

            var writes      = new[] {
                                  a.WriteAsync(new Byte[3 * FrameSize]),
                                  b.WriteAsync(new Byte[3 * FrameSize])
                              };

            var grantedAt   = wire.Count;

            await transport.SendAsync(HTTP2Frame.CreateWindowUpdate(0, 6 * FrameSize));

            var lines       = await wire.UntilAsync(lines => DataBytesOn(lines.Skip(grantedAt), a.StreamId) == 3 * FrameSize &&
                                                             DataBytesOn(lines.Skip(grantedAt), b.StreamId) == 3 * FrameSize,
                                                    "both uploads, whole");

            var afterGrant  = lines.Skip(grantedAt).Where(IsData).Select(StreamOf).ToList();

            await Task.WhenAll(writes).WaitAsync(HoldingH2Transport.StepTimeout);

            Assert.Multiple(() =>
            {

                Assert.That(afterGrant,                                                Has.Count.EqualTo(6),  "the DATA frames once the window grew: three each, and none of the bulk upload");
                Assert.That(afterGrant.Zip(afterGrant.Skip(1), (x, y) => x != y),      Is.All.True,           $"the two uploads take turns: {String.Join(", ", afterGrant)}");

            });

            await connection.CloseAsync();
            await ObserveAsync(upload);

        }

        #endregion

    }

}
