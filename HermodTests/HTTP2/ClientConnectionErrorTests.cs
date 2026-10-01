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
    /// </summary>
    [TestFixture]
    public class ClientConnectionErrorTests
    {

        #region (helpers)

        /// <summary>
        /// A frame the client must answer with a connection error, sent while a
        /// request is in flight on <paramref name="StreamId"/>.
        /// </summary>
        private static HTTP2Frame Offending(String Frame, UInt32 StreamId)

            => Frame switch {

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

        #endregion


        #region ConnectionError_RequestInFlightFailsWithIt(Frame, Code, Message)

        /// <summary>
        /// The server sends a frame the client must answer with a connection
        /// error while a request is in flight. The request fails with that error,
        /// and the connection ends: Closed completes, and it takes no new requests.
        /// </summary>
        [TestCase("PUSH_PROMISE",                       HTTP2ErrorCode.PROTOCOL_ERROR,      "Server push not enabled")]
        [TestCase("SETTINGS INITIAL_WINDOW_SIZE 2^31",  HTTP2ErrorCode.FLOW_CONTROL_ERROR,  "INITIAL_WINDOW_SIZE too large")]
        [TestCase("PING of 7 octets",                   HTTP2ErrorCode.FRAME_SIZE_ERROR,    "PING must be 8 bytes")]
        public async Task ConnectionError_RequestInFlightFailsWithIt(String Frame, HTTP2ErrorCode Code, String Message)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var request     = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.http, "localhost", "/");
            var headers     = await transport.NextHeadersAsync();

            await transport.SendLastAsync(Offending(Frame, headers.StreamId));

            var (ended, failure)  = await EndOf(request);
            var closed            = (await EndOf(connection.Closed)).Ended;

            Assert.Multiple(() =>
            {

                Assert.That(ended,                Is.True,                                                          "the request in flight ended");
                Assert.That(Describe(failure),    Is.EqualTo($"{nameof(HTTP2ConnectionException)} {Code}: {Message}"), "how it failed");

                Assert.That(closed,               Is.True,                                                          "the connection ended");
                Assert.That(connection.IsUsable,  Is.False,                                                         "and takes no new requests");

            });

        }

        #endregion

        #region ConnectionError_WhileATunnelOpens_OpeningFailsWithIt()

        /// <summary>
        /// The server answers a CONNECT with a connection error rather than a
        /// status. Opening the tunnel fails with that error.
        /// </summary>
        [Test]
        public async Task ConnectionError_WhileATunnelOpens_OpeningFailsWithIt()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var opening     = connection.OpenTunnelAsync("localhost:443");
            var headers     = await transport.NextHeadersAsync();

            await transport.SendLastAsync(Offending("PING of 7 octets", headers.StreamId));

            var (ended, failure)  = await EndOf(opening);
            var closed            = (await EndOf(connection.Closed)).Ended;

            Assert.Multiple(() =>
            {

                Assert.That(ended,              Is.True,                                                                                    "opening the tunnel ended");
                Assert.That(Describe(failure),  Is.EqualTo($"{nameof(HTTP2ConnectionException)} FRAME_SIZE_ERROR: PING must be 8 bytes"),  "how it failed");

                Assert.That(closed,             Is.True,                                                                                    "the connection ended");

            });

        }

        #endregion

        #region ConnectionError_InTheFirstSettings_StartFailsWithIt()

        /// <summary>
        /// The server's first SETTINGS are a connection error. Starting the
        /// connection fails with it.
        /// </summary>
        [Test]
        public async Task ConnectionError_InTheFirstSettings_StartFailsWithIt()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = new HTTP2ClientConnection(transport.Client, new HTTP2ClientOptions());
            var starting    = connection.StartAsync();

            await transport.SendLastAsync(Offending("SETTINGS INITIAL_WINDOW_SIZE 2^31", 0));

            var (ended, failure)  = await EndOf(starting);
            var closed            = (await EndOf(connection.Closed)).Ended;

            Assert.Multiple(() =>
            {

                Assert.That(ended,              Is.True,                                                                                                 "starting the connection ended");
                Assert.That(Describe(failure),  Is.EqualTo($"{nameof(HTTP2ConnectionException)} FLOW_CONTROL_ERROR: INITIAL_WINDOW_SIZE too large"),     "how it failed");

                Assert.That(closed,             Is.True,                                                                                                 "the connection ended");

            });

        }

        #endregion

    }

}
