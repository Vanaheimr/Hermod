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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// The stream window the client grants the server for a tunnel or a streamed
    /// response, which the application reads chunk by chunk (RFC 9113, Section
    /// 6.9). The client gave it back as the DATA arrived, read or not, and
    /// counted nothing it received against it: a tunnel or a streamed response
    /// the application did not read took whatever the server sent, and the
    /// client buffered all of it.
    ///
    /// Now the stream window of what the application is to read goes back once
    /// it has read it, so that a stream holds no more than its window for an
    /// application that does not read: 1 MiB. DATA beyond the window is a
    /// connection error, FLOW_CONTROL_ERROR. The connection window still goes
    /// back on receipt, as does the stream window of a buffered response and of
    /// padding, which nobody reads: an unread stream takes no window from the
    /// others. And no stream-level WINDOW_UPDATE goes out once the server can
    /// send no more DATA on the stream.
    /// </summary>
    [TestFixture]
    public class ClientStreamWindowTests
    {

        #region (helpers)

        /// <summary>
        /// A stream's receive window, the SETTINGS_INITIAL_WINDOW_SIZE the client
        /// advertises: 1 MiB.
        /// </summary>
        private const Int32 StreamWindow = HTTP2FlowControl.StreamWindowSize;

        /// <summary>
        /// The largest DATA frame the client takes, its SETTINGS_MAX_FRAME_SIZE.
        /// </summary>
        private const Int32 MaxFrame = 16 * 1024;

        /// <summary>
        /// The two kinds of stream whose DATA the application reads chunk by
        /// chunk: a tunnel, or a streamed response.
        /// </summary>
        public enum Kind
        {
            Tunnel,
            StreamedResponse
        }

        /// <summary>
        /// A stream the application reads chunk by chunk, whichever kind it is.
        /// </summary>
        private sealed record Reader(UInt32                       StreamId,
                                     Func<Task<Byte[]?>>          ReadAsync,
                                     HTTP2ClientTunnel?           Tunnel,
                                     HTTP2ClientStream?           Response);

        /// <summary>
        /// Open a stream of this kind on the next stream ID, and answer it with a
        /// 200 whose DATA is to follow: a CONNECT tunnel, or a streamed GET,
        /// whose request side the client has ended.
        /// </summary>
        private static async Task<Reader> OpenAsync(Kind                   Kind,
                                                    HoldingH2Transport     Transport,
                                                    HTTP2ClientConnection  Connection)
        {

            if (Kind == Kind.Tunnel)
            {

                var opening  = Connection.OpenTunnelAsync("localhost:443");
                var headers  = await Transport.NextHeadersAsync();

                await Transport.SendHeadersAsync(headers.StreamId, [(":status", "200")]);

                var tunnel   = await opening.WaitAsync(HoldingH2Transport.StepTimeout);

                return new Reader(tunnel.StreamId, () => tunnel.ReadAsync(CancellationToken.None), tunnel, null);

            }

            var response = await Connection.StartStreamingRequestAsync(HTTPMethod.GET, URIScheme.https, "localhost", "/download").
                                             WaitAsync(HoldingH2Transport.StepTimeout);

            await Transport.NextHeadersAsync();
            await response.CompleteRequestAsync().WaitAsync(HoldingH2Transport.StepTimeout);

            await Transport.SendHeadersAsync(response.StreamId, [(":status", "200")]);
            await response.GetResponseAsync().WaitAsync(HoldingH2Transport.StepTimeout);

            return new Reader(response.StreamId, () => response.ReadAsync(), null, response);

        }

        /// <summary>
        /// DATA on a stream, in frames as large as the client takes, the last of
        /// them with END_STREAM if asked.
        /// </summary>
        private static HTTP2Frame[] Data(UInt32 StreamId, Int32 Bytes, Boolean EndStream = false)
        {

            var frames = new List<HTTP2Frame>();

            for (var sent = 0; sent < Bytes; sent += MaxFrame)
            {
                var size = Math.Min(MaxFrame, Bytes - sent);
                frames.Add(HTTP2Frame.CreateData(StreamId, new Byte[size], EndStream && sent + size >= Bytes));
            }

            return [.. frames];

        }

        /// <summary>
        /// A padded DATA frame (RFC 9113, Section 6.1): the Pad Length octet, the
        /// data, and that many octets of padding, all of which count against flow
        /// control.
        /// </summary>
        private static HTTP2Frame PaddedData(UInt32 StreamId, Byte[] Data, Byte Padding)

            => new() {
                   Type      = HTTP2FrameType.DATA,
                   Flags     = HTTP2FrameFlags.PADDED,
                   StreamId  = StreamId,
                   Payload   = [Padding, .. Data, .. new Byte[Padding]]
               };

        /// <summary>
        /// The increments of the WINDOW_UPDATEs among the frames, on this stream.
        /// </summary>
        private static List<Int64> WindowUpdates(IEnumerable<HTTP2Frame> Frames, UInt32 StreamId)

            => [.. Frames.Where (frame => frame.Type == HTTP2FrameType.WINDOW_UPDATE && frame.StreamId == StreamId).
                          Select(frame => (Int64) (BinaryPrimitives.ReadUInt32BigEndian(frame.Payload) & 0x7FFFFFFF))];

        /// <summary>
        /// Read chunks until this many bytes are read, and return how many were:
        /// fewer if the stream ended first.
        /// </summary>
        private static async Task<Int64> ReadAsync(Reader Reader, Int64 Bytes)
        {

            var read = 0L;

            while (read < Bytes &&
                   await Reader.ReadAsync().WaitAsync(HoldingH2Transport.StepTimeout) is { } chunk)
            {
                read += chunk.Length;
            }

            return read;

        }

        /// <summary>
        /// Whether the task ends within the step timeout.
        /// </summary>
        private static async Task<Boolean> EndsInTime(Task Task)

            => await System.Threading.Tasks.Task.WhenAny(Task, System.Threading.Tasks.Task.Delay(HoldingH2Transport.StepTimeout)) == Task;

        #endregion


        #region UnreadStreams_StreamWindowWithheld_ConnectionWindowGivenBack(Kind)

        /// <summary>
        /// The server sends a whole stream window on each of two streams that the
        /// application does not read. The client gives back none of either
        /// stream's window, so the server can send them no more, and the
        /// connection's, of which 2 MiB of 4 MiB are owed now, it gives back on
        /// receipt, as before: an unread stream keeps no window from the others.
        /// It used to give back each stream's window as well, half a window at a
        /// time, and took whatever the server sent next.
        /// </summary>
        [TestCase(Kind.Tunnel)]
        [TestCase(Kind.StreamedResponse)]
        public async Task UnreadStreams_StreamWindowWithheld_ConnectionWindowGivenBack(Kind Kind)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var first       = await OpenAsync(Kind, transport, connection);
            var second      = await OpenAsync(Kind, transport, connection);

            await transport.SentUntilPingAckAsync();

            await transport.SendAsync(Data(first. StreamId, StreamWindow));
            await transport.SendAsync(Data(second.StreamId, StreamWindow));

            var sent        = await transport.SentUntilPingAckAsync();

            Assert.Multiple(() =>
            {

                Assert.That(WindowUpdates(sent, first. StreamId),  Is.Empty,                                   "stream-level WINDOW_UPDATEs on the first stream");
                Assert.That(WindowUpdates(sent, second.StreamId),  Is.Empty,                                   "stream-level WINDOW_UPDATEs on the second stream");
                Assert.That(WindowUpdates(sent, 0),                Is.EqualTo(new Int64[] { 2 * StreamWindow }), "connection-level WINDOW_UPDATEs: the 2 MiB received, half the connection window");

            });

            await connection.CloseAsync().WaitAsync(HoldingH2Transport.StepTimeout);

        }

        #endregion

        #region BeyondTheStreamWindow_FlowControlError(Kind)

        /// <summary>
        /// A stream the application does not read holds no more than its window:
        /// the server sends a whole stream window, and then one octet more, which
        /// the client did not grant. That is a connection error of type
        /// FLOW_CONTROL_ERROR (RFC 9113, Section 6.9.1), and the connection ends.
        /// The client used to count nothing it received, and took the octet, and
        /// whatever else the server sent.
        /// </summary>
        [TestCase(Kind.Tunnel)]
        [TestCase(Kind.StreamedResponse)]
        public async Task BeyondTheStreamWindow_FlowControlError(Kind Kind)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var reader      = await OpenAsync(Kind, transport, connection);

            await transport.SendAsync(Data(reader.StreamId, StreamWindow));

            var heldAWindow = !connection.Closed.IsCompleted;

            await transport.WriteAsync(HTTP2Frame.CreateData(reader.StreamId, [0x42]));

            var ended       = await EndsInTime(connection.Closed);

            Assert.Multiple(() =>
            {

                Assert.That(heldAWindow,  Is.True,  "the connection takes a whole stream window, unread");
                Assert.That(ended,        Is.True,  "the connection ends at the octet beyond it");

                if (reader.Response is not null && ended)
                    Assert.That(async () => await reader.Response.GetTrailersAsync(),
                                Throws.TypeOf<HTTP2ConnectionException>().With.Property(nameof(HTTP2ConnectionException.ErrorCode)).EqualTo(HTTP2ErrorCode.FLOW_CONTROL_ERROR),
                                "how the response failed");

            });

        }

        #endregion

        #region Reading_GivesTheStreamWindowBack(Kind)

        /// <summary>
        /// The application reads the stream window it was sent: once it has read
        /// half of it, the client gives that half back with one WINDOW_UPDATE, and
        /// the other half once it has read that, too. The server can then send a
        /// whole stream window again, and does. The client used to give the
        /// window back as the DATA arrived, and nothing as it was read.
        /// </summary>
        [TestCase(Kind.Tunnel)]
        [TestCase(Kind.StreamedResponse)]
        public async Task Reading_GivesTheStreamWindowBack(Kind Kind)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var reader      = await OpenAsync(Kind, transport, connection);

            await transport.SendAsync(Data(reader.StreamId, StreamWindow));
            await transport.SentUntilPingAckAsync();

            var firstHalf   = await ReadAsync(reader, StreamWindow / 2);
            var afterFirst  = await transport.SentUntilPingAckAsync();

            var secondHalf  = await ReadAsync(reader, StreamWindow / 2);
            var afterSecond = await transport.SentUntilPingAckAsync();

            await transport.SendAsync(Data(reader.StreamId, StreamWindow));

            var again       = await ReadAsync(reader, StreamWindow);

            Assert.Multiple(() =>
            {

                Assert.That(firstHalf,                                  Is.EqualTo(StreamWindow / 2),                      "bytes read first");
                Assert.That(WindowUpdates(afterFirst,  reader.StreamId), Is.EqualTo(new Int64[] { StreamWindow / 2 }),     "stream-level WINDOW_UPDATEs once half the window is read");

                Assert.That(secondHalf,                                 Is.EqualTo(StreamWindow / 2),                      "bytes read then");
                Assert.That(WindowUpdates(afterSecond, reader.StreamId), Is.EqualTo(new Int64[] { StreamWindow / 2 }),     "stream-level WINDOW_UPDATEs once all of it is read");

                Assert.That(again,                                      Is.EqualTo(StreamWindow),                          "bytes of the next stream window, as the client took them");
                Assert.That(connection.Closed.IsCompleted,              Is.False,                                          "the connection is open");

            });

            await connection.CloseAsync().WaitAsync(HoldingH2Transport.StepTimeout);

        }

        #endregion

        #region Padding_StreamWindowGivenBackOnReceipt()

        /// <summary>
        /// Padding counts against flow control (RFC 9113, Section 6.1), but nobody
        /// reads it: its stream window goes back as it arrives, as the window of a
        /// buffered response does. The server sends 2048 padded DATA frames on a
        /// tunnel, each with one octet of data, the Pad Length octet and 255
        /// octets of padding. The client gives back the 512 KiB of the padding and
        /// the Pad Length octets with one WINDOW_UPDATE, once half the stream
        /// window is owed, and withholds the 2048 octets of data, which nobody
        /// has read. It used to give back all of each frame.
        /// </summary>
        [Test]
        public async Task Padding_StreamWindowGivenBackOnReceipt()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var reader      = await OpenAsync(Kind.Tunnel, transport, connection);

            await transport.SentUntilPingAckAsync();

            await transport.SendAsync([.. Enumerable.Range(0, 2048).Select(_ => PaddedData(reader.StreamId, [0x42], Padding: 255))]);

            var sent        = await transport.SentUntilPingAckAsync();

            Assert.That(WindowUpdates(sent, reader.StreamId),
                        Is.EqualTo(new Int64[] { 2048 * 256 }),
                        "stream-level WINDOW_UPDATEs: the padding and its length octets, not the data");

            await connection.CloseAsync().WaitAsync(HoldingH2Transport.StepTimeout);

        }

        #endregion

        #region ReadAfterTheServerEnded_NoStreamWindowUpdate(Kind)

        /// <summary>
        /// The server sends a whole stream window and ends its side of the stream.
        /// It can send no more DATA there, so the client gives back none of the
        /// stream's window, neither as the DATA arrives nor as the application
        /// reads it afterwards: a WINDOW_UPDATE would tell the server nothing. All
        /// of it is read. The client used to give back the stream's window as the
        /// DATA arrived.
        /// </summary>
        [TestCase(Kind.Tunnel)]
        [TestCase(Kind.StreamedResponse)]
        public async Task ReadAfterTheServerEnded_NoStreamWindowUpdate(Kind Kind)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection   = await transport.ConnectAsync(new HTTP2ClientOptions());
            var reader       = await OpenAsync(Kind, transport, connection);

            await transport.SentUntilPingAckAsync();

            await transport.SendAsync(Data(reader.StreamId, StreamWindow, EndStream: true));

            var whileUnread  = await transport.SentUntilPingAckAsync();
            var read         = await ReadAsync(reader, Int64.MaxValue);
            var afterReading = await transport.SentUntilPingAckAsync();

            Assert.Multiple(() =>
            {

                Assert.That(read,                                          Is.EqualTo(StreamWindow),  "bytes read, to the end of the stream");
                Assert.That(WindowUpdates(whileUnread,  reader.StreamId),  Is.Empty,                  "stream-level WINDOW_UPDATEs as the DATA arrived");
                Assert.That(WindowUpdates(afterReading, reader.StreamId),  Is.Empty,                  "stream-level WINDOW_UPDATEs as it was read");

            });

            await connection.CloseAsync().WaitAsync(HoldingH2Transport.StepTimeout);

        }

        #endregion

        #region BufferedResponse_NoStreamWindowUpdateWithItsEnd()

        /// <summary>
        /// A buffered response's stream window goes back on receipt, half a window
        /// at a time — but not with the DATA frame that ends the server's side,
        /// after which it can send nothing more on the stream. The server sends a
        /// whole stream window and ends the response with its last frame: one
        /// stream-level WINDOW_UPDATE, once half the window is owed, and no
        /// second one with the end. The client used to send that, too.
        /// </summary>
        [Test]
        public async Task BufferedResponse_NoStreamWindowUpdateWithItsEnd()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());

            var get         = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.https, "localhost", "/file");
            var request     = await transport.NextHeadersAsync();

            await transport.SendHeadersAsync(request.StreamId, [(":status", "200")]);
            await transport.SendAsync(Data(request.StreamId, StreamWindow, EndStream: true));

            var response    = await get.WaitAsync(HoldingH2Transport.StepTimeout);
            var sent        = await transport.SentUntilPingAckAsync();

            Assert.Multiple(() =>
            {

                Assert.That(response.Body.Length,                  Is.EqualTo(StreamWindow),                      "bytes of the response");
                Assert.That(WindowUpdates(sent, request.StreamId), Is.EqualTo(new Int64[] { StreamWindow / 2 }),  "stream-level WINDOW_UPDATEs");

            });

            await connection.CloseAsync().WaitAsync(HoldingH2Transport.StepTimeout);

        }

        #endregion

        #region WindowUpdateWaitingForTheWriteLock_ServerResetsMeanwhile_NotSent()

        /// <summary>
        /// The application reads the chunk that makes half the stream window owed,
        /// and its WINDOW_UPDATE waits for the connection's write lock, which
        /// another request's start holds: the test holds it in the write of its
        /// HEADERS. Meanwhile the read loop handles the server's RST_STREAM. Once
        /// the WINDOW_UPDATE gets the lock, it finds the stream reset, and stays
        /// unsent: a closed stream gets nothing but PRIORITY (RFC 9113, Section
        /// 5.1). The read returns its chunk.
        /// </summary>
        [Test]
        public async Task WindowUpdateWaitingForTheWriteLock_ServerResetsMeanwhile_NotSent()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => streamId == 3);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var reader      = await OpenAsync(Kind.Tunnel, transport, connection);

            await transport.SendAsync(Data(reader.StreamId, StreamWindow));

            var readFirst   = await ReadAsync(reader, StreamWindow / 2 - MaxFrame);

            var other       = connection.StartStreamingRequestAsync(HTTPMethod.GET, URIScheme.https, "localhost", "/other");

            await transport.NextHeadersAsync();

            var reading     = reader.ReadAsync();
            var waited      = !reading.IsCompleted;

            await transport.SendAsync(HTTP2Frame.CreateRstStream(reader.StreamId, HTTP2ErrorCode.CANCEL));

            transport.Release(3);

            var chunk        = await reading.WaitAsync(HoldingH2Transport.StepTimeout);
            var otherStarted = await EndsInTime(other);

            var sent         = await transport.SentUntilPingAckAsync();

            Assert.Multiple(() =>
            {

                Assert.That(readFirst,                           Is.EqualTo(StreamWindow / 2 - MaxFrame),  "bytes read before");
                Assert.That(waited,                              Is.True,                                  "the read waited for the other request's start");
                Assert.That(chunk?.Length,                       Is.EqualTo(MaxFrame),                     "the chunk the read returned");
                Assert.That(otherStarted,                        Is.True,                                  "the other request started");

                Assert.That(WindowUpdates(sent, reader.StreamId), Is.Empty,                                "stream-level WINDOW_UPDATEs after the reset");

            });

            await connection.CloseAsync().WaitAsync(HoldingH2Transport.StepTimeout);

        }

        #endregion

        #region UnreadTunnel_ReadLate_EverythingArrives()

        /// <summary>
        /// Against the server: a tunnel's handler sends 40 chunks of 64 KiB, two
        /// and a half stream windows, and then ends the tunnel. The application
        /// reads nothing until the server has sent the first of them, and then
        /// all of it: the server goes on as the client reads, and everything
        /// arrives, in order.
        /// </summary>
        [Test]
        public async Task UnreadTunnel_ReadLate_EverythingArrives()
        {

            const Int32 Chunk  = 64 * 1024;
            const Int32 Chunks = 40;

            var firstSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            await using var server = await TestH2Server.StartAsync(

                                         (streamId, headers, body, cancellationToken)
                                             => Task.FromResult<(List<(String Name, String Value)>, Byte[]?)>(([(":status", "404")], null)),

                                         ConnectHandler: (streamId, headers, cancellationToken) => Task.FromResult(new HTTP2ConnectResult {

                                             StatusCode  = 200,

                                             RunAsync    = async (tunnel, token) => {

                                                               for (var i = 0; i < Chunks; i++)
                                                               {
                                                                   await tunnel.WriteAsync([.. Enumerable.Repeat((Byte) i, Chunk)], token);
                                                                   firstSent.TrySetResult();
                                                               }

                                                           }

                                         }));

            var connection = await HTTP2Client.ConnectAsync("localhost", server.Port, H2.AcceptAnyServerCert);

            try
            {

                var tunnel    = await connection.OpenTunnelAsync($"localhost:{server.Port}").WaitAsync(HoldingH2Transport.StepTimeout);
                var started   = await EndsInTime(firstSent.Task);

                var received  = new MemoryStream();

                while (await tunnel.ReadAsync(CancellationToken.None).WaitAsync(HoldingH2Transport.StepTimeout) is { } chunk)
                    received.Write(chunk);

                var bytes     = received.ToArray();

                Assert.Multiple(() =>
                {

                    Assert.That(started,       Is.True,                    "the server sent the first chunk");
                    Assert.That(bytes.Length,  Is.EqualTo(Chunks * Chunk),  "bytes received, to the end of the tunnel");

                    Assert.That(Enumerable.Range(0, Chunks).All(i => bytes.AsSpan(i * Chunk, Chunk).IndexOfAnyExcept((Byte) i) < 0),
                                Is.True,
                                "every chunk where it was sent");

                });

            }
            finally
            {
                await connection.CloseAsync().WaitAsync(HoldingH2Transport.StepTimeout);
            }

        }

        #endregion

    }

}
