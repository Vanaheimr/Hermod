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
    /// Connection errors the client finds in what the server sends.
    ///
    /// What waited for the server when one came — a request in flight, a tunnel
    /// being opened, the start of the connection — failed with "A task was
    /// canceled." rather than with that error. The read loop fails them with
    /// the connection error and only then cancels the connection, but the
    /// cancellation reached their waits first. Now the error does.
    ///
    /// RFC 9218, Section 7.1: servers must not send PRIORITY_UPDATE, and a client
    /// that receives one must answer with a connection error of type
    /// PROTOCOL_ERROR. The client ignored the frame, as one of a type it does not
    /// handle.
    ///
    /// On a connection error it did find, the client ended the connection
    /// without a word to the server. RFC 9113, Section 5.4.1 has an endpoint send
    /// a GOAWAY with the error code first, as the server does. Now the client
    /// does too: the GOAWAY carries the error's code and message, and is the last
    /// frame of the connection. It may take a second, to get the write lock and
    /// to go out, and no more, so that a write that holds the lock and does not
    /// end cannot hold up the end of the connection. The tests that need that
    /// second take it from a clock of their own.
    /// </summary>
    [TestFixture]
    public class ClientConnectionErrorTests
    {

        #region (helpers)

        /// <summary>
        /// What the client says of a PRIORITY_UPDATE from the server, in its
        /// connection error and its GOAWAY.
        /// </summary>
        private const String PriorityUpdateFromServer = "PRIORITY_UPDATE received by client (servers must not send it)";

        /// <summary>
        /// A frame the client must answer with a connection error, sent while a
        /// request is in flight on <paramref name="StreamId"/>.
        /// </summary>
        private static HTTP2Frame Offending(String Frame, UInt32 StreamId)

            => Frame switch {

                   // For the request's own stream, as a server that echoes the
                   // client's signal back might send it.
                   "PRIORITY_UPDATE"                    => HTTP2Frame.CreatePriorityUpdate(StreamId, "u=0"),

                   // Promising stream 2, although the client advertised ENABLE_PUSH=0.
                   "PUSH_PROMISE"                       => new HTTP2Frame {
                                                               Type      = HTTP2FrameType.PUSH_PROMISE,
                                                               Flags     = HTTP2FrameFlags.END_HEADERS,
                                                               StreamId  = StreamId,
                                                               Payload   = [0, 0, 0, 2]
                                                           },

                   // Above the largest window flow control allows (RFC 9113, Section 6.5.2).
                   "SETTINGS INITIAL_WINDOW_SIZE 2^31"  => HTTP2Frame.CreateSettings((HTTP2SettingsParameter.INITIAL_WINDOW_SIZE, 0x80000000u)),

                   // A PING carries exactly 8 octets (Section 6.7).
                   "PING of 7 octets"                   => new HTTP2Frame {
                                                               Type      = HTTP2FrameType.PING,
                                                               Flags     = HTTP2FrameFlags.NONE,
                                                               StreamId  = 0,
                                                               Payload   = new Byte[7]
                                                           },

                   _                                    => throw new ArgumentException($"No such frame: {Frame}", nameof(Frame))

               };

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
        /// A failure as the caller reads it: its type, for a connection error the
        /// error code, and its message.
        /// </summary>
        private static String? Describe(Exception? Failure)

            => Failure switch {
                   null                        => null,
                   HTTP2ConnectionException e  => $"{nameof(HTTP2ConnectionException)} {e.ErrorCode}: {e.Message}",
                   _                           => $"{Failure.GetType().Name}: {Failure.Message}"
               };

        /// <summary>
        /// A frame as the server reads it: its stream and type, whether it is an
        /// ACK, and for a GOAWAY the error code, the last-stream-id and the debug
        /// data.
        /// </summary>
        private static String Describe(HTTP2Frame Frame)

            => Frame.Type == HTTP2FrameType.GOAWAY

                   ? $"{Frame.StreamId} GOAWAY {(HTTP2ErrorCode) BinaryPrimitives.ReadUInt32BigEndian(Frame.Payload.AsSpan(4, 4))}, " +
                     $"last stream {BinaryPrimitives.ReadUInt32BigEndian(Frame.Payload.AsSpan(0, 4)) & 0x7FFFFFFFu}: " +
                     Encoding.UTF8.GetString(Frame.Payload.AsSpan(8))

                   : $"{Frame.StreamId} {Frame.Type}" +
                     (Frame.Type is HTTP2FrameType.SETTINGS or HTTP2FrameType.PING && Frame.IsAck ? " ACK" : "");

        /// <summary>
        /// The GOAWAY the client sends over a connection error: of its code, with
        /// its message as debug data, and no stream of the server's to name.
        /// </summary>
        private static String GoAway(HTTP2ErrorCode Code, String Message)

            => $"0 GOAWAY {Code}, last stream 0: {Message}";

        /// <summary>
        /// Every frame the client sent from here on, one line per frame, up to the
        /// end of the stream — which the test makes, once the connection has ended
        /// and nothing more can come.
        /// </summary>
        private static async Task<List<String>> SentToTheEndAsync(HoldingH2Transport Transport)
        {

            await Transport.EndClientSideAsync();

            using var timeout = new CancellationTokenSource(HoldingH2Transport.StepTimeout);

            var sent = new List<String>();

            while (await Transport.NextFrameAsync(timeout.Token) is { } frame)
                sent.Add(Describe(frame));

            return sent;

        }

        /// <summary>
        /// A clock whose timers fire only when the test fires them, so that the
        /// test decides when the time a GOAWAY may take is up. Keepalive is off:
        /// the GOAWAY's is the only timer the client sets on it.
        /// </summary>
        private sealed class HeldTimers : TimeProvider
        {

            private readonly Lock                  sync     = new();
            private readonly List<HeldTimer>       timers   = [];
            private readonly TaskCompletionSource  created  = new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>
            /// Completes once the client has set a timer.
            /// </summary>
            public Task Created
                => created.Task;

            public override ITimer CreateTimer(TimerCallback  Callback,
                                               Object?        State,
                                               TimeSpan       DueTime,
                                               TimeSpan       Period)
            {

                var timer = new HeldTimer(Callback, State);

                lock (sync)
                    timers.Add(timer);

                created.TrySetResult();

                return timer;

            }

            /// <summary>
            /// Fire every timer set so far, unless disposed of.
            /// </summary>
            public void Fire()
            {

                List<HeldTimer> due;

                lock (sync)
                    due = [.. timers];

                foreach (var timer in due)
                    timer.Fire();

            }

            private sealed class HeldTimer(TimerCallback  Callback,
                                           Object?        State) : ITimer
            {

                private Int32 disposed;

                public void Fire()
                {
                    if (Volatile.Read(ref disposed) == 0)
                        Callback(State);
                }

                public Boolean Change(TimeSpan DueTime, TimeSpan Period)
                    => Volatile.Read(ref disposed) == 0;

                public void Dispose()
                    => Volatile.Write(ref disposed, 1);

                public ValueTask DisposeAsync()
                {
                    Dispose();
                    return ValueTask.CompletedTask;
                }

            }

        }

        #endregion


        #region ConnectionError_RequestInFlightFailsWithIt_GoAwaySent(Frame, Code, Message)

        /// <summary>
        /// The server sends a frame the client must answer with a connection
        /// error while a request is in flight. The request fails with that error,
        /// the connection ends — Closed completes, and it takes no new requests —
        /// and the last thing the client sends is a GOAWAY with the error's code.
        ///
        /// PRIORITY_UPDATE is the frame of RFC 9218, Section 7.1. The others were
        /// connection errors before, and show that the GOAWAY carries the code of
        /// the error at hand.
        /// </summary>
        [TestCase("PRIORITY_UPDATE",                    HTTP2ErrorCode.PROTOCOL_ERROR,      PriorityUpdateFromServer)]
        [TestCase("PUSH_PROMISE",                       HTTP2ErrorCode.PROTOCOL_ERROR,      "Server push not enabled")]
        [TestCase("SETTINGS INITIAL_WINDOW_SIZE 2^31",  HTTP2ErrorCode.FLOW_CONTROL_ERROR,  "INITIAL_WINDOW_SIZE too large")]
        [TestCase("PING of 7 octets",                   HTTP2ErrorCode.FRAME_SIZE_ERROR,    "PING must be 8 bytes")]
        public async Task ConnectionError_RequestInFlightFailsWithIt_GoAwaySent(String Frame, HTTP2ErrorCode Code, String Message)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var request     = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.http, "localhost", "/");
            var headers     = await transport.NextHeadersAsync();

            await transport.SendLastAsync(Offending(Frame, headers.StreamId));

            var (ended, failure)  = await EndOf(request);
            var closed            = (await EndOf(connection.Closed)).Ended;
            var sent              = await SentToTheEndAsync(transport);

            Assert.Multiple(() =>
            {

                Assert.That(ended,                Is.True,                                                          "the request in flight ended");
                Assert.That(Describe(failure),    Is.EqualTo($"{nameof(HTTP2ConnectionException)} {Code}: {Message}"), "how it failed");

                Assert.That(closed,               Is.True,                                                          "the connection ended");
                Assert.That(connection.IsUsable,  Is.False,                                                         "and takes no new requests");

                Assert.That(sent,                 Is.EqualTo(new[] { GoAway(Code, Message) }),                      "what the client sent after the request's HEADERS");

            });

        }

        #endregion

        #region ConnectionError_WhileATunnelOpens_OpeningFailsWithIt_GoAwaySent()

        /// <summary>
        /// The server answers a CONNECT with a connection error rather than a
        /// status. Opening the tunnel fails with that error, and a GOAWAY of its
        /// code follows the CONNECT's HEADERS.
        /// </summary>
        [Test]
        public async Task ConnectionError_WhileATunnelOpens_OpeningFailsWithIt_GoAwaySent()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var opening     = connection.OpenTunnelAsync("localhost:443");
            var headers     = await transport.NextHeadersAsync();

            await transport.SendLastAsync(Offending("PING of 7 octets", headers.StreamId));

            var (ended, failure)  = await EndOf(opening);
            var closed            = (await EndOf(connection.Closed)).Ended;
            var sent              = await SentToTheEndAsync(transport);

            Assert.Multiple(() =>
            {

                Assert.That(ended,              Is.True,                                                                                    "opening the tunnel ended");
                Assert.That(Describe(failure),  Is.EqualTo($"{nameof(HTTP2ConnectionException)} FRAME_SIZE_ERROR: PING must be 8 bytes"),  "how it failed");

                Assert.That(closed,             Is.True,                                                                                    "the connection ended");

                Assert.That(sent,               Is.EqualTo(new[] { GoAway(HTTP2ErrorCode.FRAME_SIZE_ERROR, "PING must be 8 bytes") }),     "what the client sent after the CONNECT's HEADERS");

            });

        }

        #endregion

        #region ConnectionError_InTheFirstSettings_StartFailsWithIt_GoAwaySent()

        /// <summary>
        /// The server's first SETTINGS are a connection error. Starting the
        /// connection fails with it, and after its own SETTINGS and window the
        /// client sends a GOAWAY of its code, and no SETTINGS ACK.
        /// </summary>
        [Test]
        public async Task ConnectionError_InTheFirstSettings_StartFailsWithIt_GoAwaySent()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = new HTTP2ClientConnection(transport.Client, new HTTP2ClientOptions());
            var starting    = connection.StartAsync();

            await transport.SendLastAsync(Offending("SETTINGS INITIAL_WINDOW_SIZE 2^31", 0));

            var (ended, failure)  = await EndOf(starting);
            var closed            = (await EndOf(connection.Closed)).Ended;
            var sent              = await SentToTheEndAsync(transport);

            Assert.Multiple(() =>
            {

                Assert.That(ended,              Is.True,                                                                                                 "starting the connection ended");
                Assert.That(Describe(failure),  Is.EqualTo($"{nameof(HTTP2ConnectionException)} FLOW_CONTROL_ERROR: INITIAL_WINDOW_SIZE too large"),     "how it failed");

                Assert.That(closed,             Is.True,                                                                                                 "the connection ended");

                Assert.That(sent,               Is.EqualTo(new[] { "0 SETTINGS",
                                                                   "0 WINDOW_UPDATE",
                                                                   GoAway(HTTP2ErrorCode.FLOW_CONTROL_ERROR, "INITIAL_WINDOW_SIZE too large") }),        "what the client sent after its connection preface");

            });

        }

        #endregion

        #region GoAway_WaitsForAWriteThatHoldsTheLock()

        /// <summary>
        /// A connection error comes while the start of another request holds the
        /// write lock: the test holds the client in the write of its HEADERS. The
        /// request in flight fails at once, but the GOAWAY waits for the lock, and
        /// the connection ends once the GOAWAY is out — right after those HEADERS,
        /// and the last frame of the connection. The request let go of then fails
        /// with the connection error too.
        /// </summary>
        [Test]
        public async Task GoAway_WaitsForAWriteThatHoldsTheLock()
        {

            var timers = new HeldTimers();

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => streamId == 3);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions { TimeProvider = timers });
            var first       = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.http, "localhost", "/first");
            var headers     = await transport.NextHeadersAsync();

            var second      = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.http, "localhost", "/second");
            await transport.NextHeadersAsync();

            await transport.SendLastAsync(HTTP2Frame.CreatePriorityUpdate(headers.StreamId, "u=0"));

            var (firstEnded,  firstFailure)   = await EndOf(first);
            var goAwayWaited                  = (await EndOf(timers.Created)).Ended;
            var closedWhileItWaited           = connection.Closed.IsCompleted;

            transport.Release(3);

            var closed                        = (await EndOf(connection.Closed)).Ended;
            var (secondEnded, secondFailure)  = await EndOf(second);
            var sent                          = await SentToTheEndAsync(transport);

            var protocolError                 = $"{nameof(HTTP2ConnectionException)} PROTOCOL_ERROR: {PriorityUpdateFromServer}";

            Assert.Multiple(() =>
            {

                Assert.That(firstEnded,               Is.True,                       "the request in flight ended while the other one's HEADERS were held");
                Assert.That(Describe(firstFailure),   Is.EqualTo(protocolError),     "how it failed");

                Assert.That(goAwayWaited,             Is.True,                       "the GOAWAY set its timer, to wait for the write lock");
                Assert.That(closedWhileItWaited,      Is.False,                      "the connection had not ended while the GOAWAY waited");

                Assert.That(closed,                   Is.True,                       "the connection ended once the HEADERS were let go");
                Assert.That(secondEnded,              Is.True,                       "the request whose HEADERS were held ended");
                Assert.That(Describe(secondFailure),  Is.EqualTo(protocolError),     "how it failed");

                Assert.That(sent,                     Is.EqualTo(new[] { GoAway(HTTP2ErrorCode.PROTOCOL_ERROR, PriorityUpdateFromServer) }),
                                                                                     "what the client sent after the held HEADERS");

            });

        }

        #endregion

        #region GoAway_GivenUpOnAWriteThatDoesNotEnd()

        /// <summary>
        /// As before, but the write of the HEADERS does not end, as a write to a
        /// server that no longer reads would not. Once the time the GOAWAY may
        /// take is up — the test fires the timer the client set — the connection
        /// ends without it, and the request held in its write ends as well. Were
        /// the wait unbounded, the end of the connection would wait for that
        /// write, and so would everything waiting for Closed, for good.
        /// </summary>
        [Test]
        public async Task GoAway_GivenUpOnAWriteThatDoesNotEnd()
        {

            var timers = new HeldTimers();

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => streamId == 3);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions { TimeProvider = timers });
            var first       = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.http, "localhost", "/first");
            var headers     = await transport.NextHeadersAsync();

            var second      = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.http, "localhost", "/second");
            await transport.NextHeadersAsync();

            await transport.SendLastAsync(HTTP2Frame.CreatePriorityUpdate(headers.StreamId, "u=0"));

            var (firstEnded,  firstFailure)   = await EndOf(first);
            var goAwayWaited                  = (await EndOf(timers.Created)).Ended;
            var closedWhileItWaited           = connection.Closed.IsCompleted;

            timers.Fire();

            var closed                        = (await EndOf(connection.Closed)).Ended;
            var (secondEnded, secondFailure)  = await EndOf(second);
            var sent                          = await SentToTheEndAsync(transport);

            Assert.Multiple(() =>
            {

                Assert.That(firstEnded,               Is.True,                       "the request in flight ended while the other one's HEADERS were held");
                Assert.That(Describe(firstFailure),   Is.EqualTo($"{nameof(HTTP2ConnectionException)} PROTOCOL_ERROR: {PriorityUpdateFromServer}"),
                                                                                     "how it failed");

                Assert.That(goAwayWaited,             Is.True,                       "the GOAWAY set its timer, to wait for the write lock");
                Assert.That(closedWhileItWaited,      Is.False,                      "the connection had not ended while the GOAWAY waited");

                Assert.That(closed,                   Is.True,                       "the connection ended once the GOAWAY's time was up");
                Assert.That(secondEnded,              Is.True,                       "the request held in the write of its HEADERS ended");
                Assert.That(secondFailure,            Is.Not.Null,                   "and failed");

                Assert.That(sent,                     Is.Empty,                      "what the client sent after the held HEADERS: no GOAWAY");

            });

        }

        #endregion

    }

}
