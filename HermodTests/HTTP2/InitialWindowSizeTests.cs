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
using System.Reflection;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// SETTINGS_INITIAL_WINDOW_SIZE stated while a stream is open. RFC 9113,
    /// Section 6.9.2: the send window of every open stream changes by the
    /// difference between the new value and the old one. Until a peer first
    /// states the setting, the old one is the RFC's initial 65 535 (Section
    /// 6.5.2), and both roles open streams with that much send window.
    ///
    /// Both roles took the difference from the peer's settings as they kept
    /// them, in an HTTP2Settings that starts out with what we advertise
    /// ourselves: an INITIAL_WINDOW_SIZE of 1 MiB. A peer whose first SETTINGS
    /// left the setting out, and that stated it later, while a stream was open,
    /// had that stream's window cut by 983 041 bytes too many: stating 65 535,
    /// the value in force all along, put it at -917 506. Nothing more went out
    /// on the stream, and the peer, which had been sent nothing, had no reason
    /// to grant more: the response, or the upload, stalled for good. A larger
    /// value fell short by as much.
    ///
    /// Both roles now take the difference from the value the streams were
    /// opened with, the stream manager's PeerInitialWindowSize, which starts at
    /// 65 535 and follows every change. A first SETTINGS that states the
    /// setting was never affected: no stream is open while it is handled.
    /// </summary>
    [TestFixture]
    public class InitialWindowSizeTests
    {

        #region (helpers)

        private static readonly TaskCreationOptions Async = TaskCreationOptions.RunContinuationsAsynchronously;

        /// <summary>
        /// What both tests add to the connection's send window before the stream
        /// opens, so that only the stream's own window limits what goes out.
        /// </summary>
        private const UInt32 ConnectionWindowIncrement = 1024 * 1024;

        /// <summary>
        /// A body of this many bytes.
        /// </summary>
        private static Byte[] Body(UInt32 Length)

            => Encoding.ASCII.GetBytes(new String('x', (Int32) Length));

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
        /// The bytes the client may still send on the stream before the server
        /// grants more, as the connection counts them.
        /// </summary>
        private static Int64 SendWindowOf(HTTP2ClientConnection Connection, UInt32 StreamId)

            => ((HTTP2StreamManager) typeof(HTTP2ClientConnection).
                                         GetField("streamManager", BindingFlags.NonPublic | BindingFlags.Instance)!.
                                         GetValue(Connection)!).TryGetStream(StreamId)!.SendWindow;

        /// <summary>
        /// The bytes of DATA the client sends on the stream, read until there are
        /// <paramref name="Expected"/> of them, or until the client sends nothing
        /// more within the step timeout.
        /// </summary>
        private static async Task<Int64> DataSentAsync(HoldingH2Transport  Transport,
                                                       UInt32              StreamId,
                                                       Int64               Expected)
        {

            var sent = 0L;

            try
            {
                while (sent < Expected)
                {

                    var frame = await Transport.NextFrameAsync();

                    if (frame.Type == HTTP2FrameType.DATA && frame.StreamId == StreamId)
                        sent += frame.Payload.Length;

                }
            }
            catch (OperationCanceledException)
            { }

            return sent;

        }

        #endregion


        #region Server_FirstStatedWhileAStreamIsOpen_WindowChangesByTheDifference(Value)

        /// <summary>
        /// The client's first SETTINGS leave INITIAL_WINDOW_SIZE out, as those of
        /// <see cref="PipedH2ServerConnection"/> do, and its request opens a
        /// stream: the server may send 65 535 bytes there. While the handler
        /// waits, the client states INITIAL_WINDOW_SIZE: lower, the same, higher.
        /// The stream's send window is that value then, as nothing has gone out
        /// on the stream, and a response body of exactly that many bytes goes
        /// out in full once the handler answers.
        /// </summary>
        [TestCase( 16384u)]
        [TestCase( 65535u)]
        [TestCase(131072u)]
        public async Task Server_FirstStatedWhileAStreamIsOpen_WindowChangesByTheDifference(UInt32 Value)
        {

            var answer = new TaskCompletionSource(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                async (streamId, headers, body, cancellationToken) => {

                    await answer.Task.WaitAsync(cancellationToken);

                    return ([(":status", "200")], Body(Value));

                });

            await peer.SendAsync(HTTP2Frame.CreateWindowUpdate(0, ConnectionWindowIncrement));
            await peer.RequestAsync(1, "/download");

            await peer.SendAsync(HTTP2Frame.CreateSettings((HTTP2SettingsParameter.INITIAL_WINDOW_SIZE, Value)));

            // The SETTINGS have been handled once the ping is answered.
            await peer.PingAsync();

            var window = peer.ServerStream(1).SendWindow;

            answer.SetResult();

            var response = await peer.TryResponseAsync(1, PipedH2ServerConnection.StepTimeout);

            Assert.Multiple(() =>
            {

                Assert.That(window,                 Is.EqualTo(Value),  "the stream's send window: the new value, as nothing has gone out on the stream");

                Assert.That(response?.Status,       Is.EqualTo("200"),  "status of the response");
                Assert.That(response?.Body.Length,  Is.EqualTo(Value),  "length of the response body: all of the window");

            });

        }

        #endregion

        #region Client_FirstStatedWhileAnUploadIsOpen_WindowChangesByTheDifference(Value)

        /// <summary>
        /// The server's first SETTINGS leave INITIAL_WINDOW_SIZE out, as those of
        /// <see cref="HoldingH2Transport"/> do, and the client opens an upload:
        /// it may send 65 535 bytes there. Then the server states
        /// INITIAL_WINDOW_SIZE: lower, the same, higher. The upload's send window
        /// is that value then, as nothing has gone out on the upload, and a write
        /// of exactly that many bytes goes out in full.
        /// </summary>
        [TestCase( 16384u)]
        [TestCase( 65535u)]
        [TestCase(131072u)]
        public async Task Client_FirstStatedWhileAnUploadIsOpen_WindowChangesByTheDifference(UInt32 Value)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection = await transport.ConnectAsync(new HTTP2ClientOptions());

            await transport.SendAsync(HTTP2Frame.CreateWindowUpdate(0, ConnectionWindowIncrement));

            var upload = await connection.StartStreamingRequestAsync(HTTPMethod.POST, URIScheme.http, "localhost", "/upload").
                                          WaitAsync(HoldingH2Transport.StepTimeout);

            await transport.NextHeadersAsync();

            await transport.SendAsync(HTTP2Frame.CreateSettings((HTTP2SettingsParameter.INITIAL_WINDOW_SIZE, Value)));

            var window  = SendWindowOf(connection, upload.StreamId);

            // Read as it is written: the transport holds less than the largest write.
            var writing = upload.WriteAsync(Body(Value));
            var sent    = await DataSentAsync(transport, upload.StreamId, Value);

            var (ended, failure) = await EndOf(writing);

            Assert.Multiple(() =>
            {

                Assert.That(window,   Is.EqualTo(Value),  "the upload's send window: the new value, as nothing has gone out on the upload");

                Assert.That(sent,     Is.EqualTo(Value),  "bytes of DATA that went out on the upload: all of the window");
                Assert.That(ended,    Is.True,            "the write returned");
                Assert.That(failure,  Is.Null,            "how the write ended");

            });

            await connection.CloseAsync();

        }

        #endregion

    }

}
