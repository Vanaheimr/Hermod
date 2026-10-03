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
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

using HTTP2WS = org.GraphDefined.Vanaheimr.Hermod.HTTP2;
using HTTP3WS = org.GraphDefined.Vanaheimr.Hermod.HTTP3;

using static org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2.WebSocketScript;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// How big a message the WebSocket over HTTP/2 and HTTP/3 takes in.
    ///
    /// Each frame was capped at 16 MiB, and nothing else was. The fragments of a
    /// message piled up without a bound, so a chain of continuation frames grew a
    /// buffer for as long as the peer kept sending, and a compressed message was
    /// inflated in one go, so a few KiB of DEFLATE that inflate to gigabytes (a
    /// "deflate bomb") were inflated to gigabytes. The HTTP/1.1 stack bounds a
    /// message at 64 MiB and closes with 1009 ("message too big") past it.
    ///
    /// Now the WebSocket has <c>MaxMessageSize</c>, 64 MiB by default, and fails
    /// the connection with 1009 as early as the bytes tell: at the header of the
    /// frame that would take the message past it, and while inflating, at the
    /// first byte past it.
    ///
    /// Run against both copies of the framing, HTTP/2's and HTTP/3's, in both
    /// roles: a server's end, which takes masked frames, and a client's, which
    /// takes unmasked ones. In memory: the tunnel hands over what the test fed it
    /// and fails a read past that, so a WebSocket that reads a payload it should
    /// have refused on its header fails the test, and as no read ever waits, a
    /// receive runs to its end on the test's thread, whose allocations are then
    /// the receive's alone.
    /// </summary>
    [TestFixture]
    public class WebSocketMessageLimitTests
    {

        #region Data

        private const UInt16  MessageTooBig = 1009;

        /// <summary>
        /// The limit the tests set: small, so that going past it is cheap, and
        /// still big enough to tell a bomb that inflated from one that did not.
        /// </summary>
        private const Int32   Limit         = 1024 * 1024;

        /// <summary>
        /// Zeros, 64 times the limit, deflated: about 64 KiB that inflate to 64 MiB.
        /// </summary>
        private static readonly Lazy<Byte[]> DeflateBomb = new (() => Deflate(new Byte[64 * Limit]));

        #endregion


        #region TheLimit_Is64MiB_AsOnTheHTTP11Stack()

        /// <summary>
        /// Unless the constructor or <see cref="HTTP2ClientConnection.OpenWebSocketAsync"/>
        /// is told otherwise, a message may have 64 MiB, as on the HTTP/1.1 stack
        /// (<see cref="WebSocketFrame.DefaultMaxPayloadSize"/>).
        /// </summary>
        [Test]
        public void TheLimit_Is64MiB_AsOnTheHTTP11Stack()
        {

            var tunnel = new ScriptedTunnel();

            Assert.Multiple(() =>
            {

                Assert.That(WebSocketFrame.DefaultMaxPayloadSize,                                         Is.EqualTo(64UL * 1024 * 1024),                  "the HTTP/1.1 stack's");

                Assert.That(HTTP2WS.WebSocketConnection.DefaultMaxMessageSize,                            Is.EqualTo(WebSocketFrame.DefaultMaxPayloadSize), "HTTP/2's default");
                Assert.That(HTTP3WS.WebSocketConnection.DefaultMaxMessageSize,                            Is.EqualTo(WebSocketFrame.DefaultMaxPayloadSize), "HTTP/3's default");

                Assert.That(new HTTP2WS.WebSocketConnection(tunnel).MaxMessageSize,                       Is.EqualTo(WebSocketFrame.DefaultMaxPayloadSize), "HTTP/2, by default");
                Assert.That(new HTTP3WS.WebSocketConnection(tunnel).MaxMessageSize,                       Is.EqualTo(WebSocketFrame.DefaultMaxPayloadSize), "HTTP/3, by default");

                Assert.That(new HTTP2WS.WebSocketConnection(tunnel, MaxMessageSize: 4096).MaxMessageSize, Is.EqualTo(4096),                                "HTTP/2, as told");
                Assert.That(new HTTP3WS.WebSocketConnection(tunnel, MaxMessageSize: 4096).MaxMessageSize, Is.EqualTo(4096),                                "HTTP/3, as told");

            });

        }

        #endregion

        #region OpenWebSocketAsync_HandsTheLimitToTheWebSocket()

        /// <summary>
        /// An HTTP/2 client's WebSocket comes from
        /// <see cref="HTTP2ClientConnection.OpenWebSocketAsync"/>, not from the
        /// constructor, so the limit has to come through there.
        /// </summary>
        [Test]
        public async Task OpenWebSocketAsync_HandsTheLimitToTheWebSocket()
        {

            await using var transport  = new HoldingH2Transport(HoldHeadersOf: streamId => false);
            await using var wire       = new ClientWire(transport);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());

            var told        = await OpenWebSocketAsync(transport, wire, connection, 1, "/told",    MaxMessageSize: 4096);
            var untold      = await OpenWebSocketAsync(transport, wire, connection, 3, "/untold");

            Assert.Multiple(() =>
            {
                Assert.That(told.  MaxMessageSize,  Is.EqualTo(4096),                                               "as told");
                Assert.That(untold.MaxMessageSize,  Is.EqualTo(HTTP2WS.WebSocketConnection.DefaultMaxMessageSize),  "by default");
            });

            await connection.CloseAsync();

        }

        /// <summary>
        /// Open a WebSocket (RFC 8441), and answer its CONNECT with a 200.
        /// </summary>
        private static async Task<HTTP2WS.WebSocketConnection> OpenWebSocketAsync(HoldingH2Transport     Transport,
                                                                             ClientWire             Wire,
                                                                             HTTP2ClientConnection  Connection,
                                                                             UInt32                 StreamId,
                                                                             String                 Path,
                                                                             UInt64?                MaxMessageSize = null)
        {

            var opening = MaxMessageSize.HasValue
                              ? Connection.OpenWebSocketAsync("localhost", URIScheme.https, Path, MaxMessageSize: MaxMessageSize.Value)
                              : Connection.OpenWebSocketAsync("localhost", URIScheme.https, Path);

            await Wire.UntilAsync(lines => lines.Contains($"{StreamId} HEADERS CONNECT {Path}"), "the CONNECT");

            await Transport.SendAsync(HTTP2Frame.CreateHeaders(StreamId,
                                                               new HPACKEncoder().EncodeHeaderBlock([(":status", "200")]),
                                                               EndStream:  false,
                                                               EndHeaders: true));

            return await opening.WaitAsync(HoldingH2Transport.StepTimeout);

        }

        #endregion


        #region ADeflateBomb_FailsWith1009_WhileItInflates(Stack, Client)

        /// <summary>
        /// About 64 KiB in one compressed frame, well within the limit as they
        /// travel, that inflate to 64 times the limit. The WebSocket stops
        /// inflating once it is past the limit and fails with 1009, having
        /// allocated a few times the limit, not the 64 MiB the bomb holds.
        /// </summary>
        [TestCaseSource(typeof(WebSocketScript), nameof(WebSocketScript.Ends))]
        public void ADeflateBomb_FailsWith1009_WhileItInflates(String Stack, Boolean Client)
        {

            var tunnel   = new ScriptedTunnel();
            var receive  = OurEnd(Stack, Client, tunnel, Limit, PerMessageDeflate: true);

            tunnel.Feed(Frame(Fin: true, Binary, DeflateBomb.Value, Masked: !Client, Rsv1: true));

            var (message, closeCode, allocated) = Receive(receive, tunnel);

            Assert.Multiple(() =>
            {
                Assert.That(DeflateBomb.Value.Length,  Is.LessThan(Limit / 8),     "the bomb, as it travels");
                Assert.That(message?.Length,           Is.Null,                    "the length of the message, none");
                Assert.That(closeCode,                 Is.EqualTo(MessageTooBig));
                Assert.That(allocated,                 Is.LessThan(8L * Limit),    "the bytes allocated while receiving");
            });

        }

        #endregion

        #region ADeflateBombInFragments_FailsWith1009_WhileItInflates(Stack, Client)

        /// <summary>
        /// The bomb again, in two fragments: a fragmented compressed message is
        /// inflated once it is whole, and is held to the limit as well.
        /// </summary>
        [TestCaseSource(typeof(WebSocketScript), nameof(WebSocketScript.Ends))]
        public void ADeflateBombInFragments_FailsWith1009_WhileItInflates(String Stack, Boolean Client)
        {

            var tunnel   = new ScriptedTunnel();
            var receive  = OurEnd(Stack, Client, tunnel, Limit, PerMessageDeflate: true);
            var bomb     = DeflateBomb.Value;
            var half     = bomb.Length / 2;

            tunnel.Feed(Frame(Fin: false, Binary,       bomb[..half], Masked: !Client, Rsv1: true),
                        Frame(Fin: true,  Continuation, bomb[half..], Masked: !Client));

            var (message, closeCode, allocated) = Receive(receive, tunnel);

            Assert.Multiple(() =>
            {
                Assert.That(message?.Length,  Is.Null,                    "the length of the message, none");
                Assert.That(closeCode,        Is.EqualTo(MessageTooBig));
                Assert.That(allocated,        Is.LessThan(8L * Limit),    "the bytes allocated while receiving");
            });

        }

        #endregion

        #region ALongChainOfFragments_FailsWith1009_AtTheHeaderOfTheFrameThatGoesPast(Stack, Client)

        /// <summary>
        /// A text message in 1 KiB fragments, 1024 of them, which fill the limit
        /// exactly, none of them the last. The next one would take the message
        /// past the limit: the WebSocket fails with 1009 on its header, and the
        /// test never feeds its payload, which the tunnel would answer with a
        /// failure, had the WebSocket gone on to read it.
        /// </summary>
        [TestCaseSource(typeof(WebSocketScript), nameof(WebSocketScript.Ends))]
        public void ALongChainOfFragments_FailsWith1009_AtTheHeaderOfTheFrameThatGoesPast(String Stack, Boolean Client)
        {

            var tunnel    = new ScriptedTunnel();
            var receive   = OurEnd(Stack, Client, tunnel, Limit);
            var fragment  = Filled(1024, (Byte) 'x');

            tunnel.Feed(Frame(Fin: false, Text, fragment, Masked: !Client));

            for (var i = 1; i < Limit / fragment.Length; i++)
                tunnel.Feed(Frame(Fin: false, Continuation, fragment, Masked: !Client));

            tunnel.Feed(Header(Fin: false, Continuation, fragment.Length, Masked: !Client));

            var (message, closeCode, _) = Receive(receive, tunnel);

            Assert.Multiple(() =>
            {
                Assert.That(message?.Length,  Is.Null, "the length of the message, none");
                Assert.That(closeCode,        Is.EqualTo(MessageTooBig));
            });

        }

        #endregion

        #region AMessageInOneFrame_OfTheLimit_IsReceived_OneByteMore_FailsWith1009(Stack, Client)

        /// <summary>
        /// One frame of exactly the limit is received whole. One of a byte more
        /// fails with 1009 on its header: the test feeds no payload after it.
        /// </summary>
        [TestCaseSource(typeof(WebSocketScript), nameof(WebSocketScript.Ends))]
        public void AMessageInOneFrame_OfTheLimit_IsReceived_OneByteMore_FailsWith1009(String Stack, Boolean Client)
        {

            var payload       = Filled(Limit, 0x5A);

            var atTheLimit    = new ScriptedTunnel();
            atTheLimit.Feed(Frame(Fin: true, Binary, payload, Masked: !Client));
            var received      = Receive(OurEnd(Stack, Client, atTheLimit, Limit), atTheLimit);

            var oneByteMore   = new ScriptedTunnel();
            oneByteMore.Feed(Header(Fin: true, Binary, Limit + 1, Masked: !Client));
            var refused       = Receive(OurEnd(Stack, Client, oneByteMore, Limit), oneByteMore);

            Assert.Multiple(() =>
            {

                Assert.That(received.Message?.Length,                 Is.EqualTo(Limit), "the length of the message of the limit");
                Assert.That(received.Message?.SequenceEqual(payload), Is.True,           "the message of the limit, byte for byte");
                Assert.That(received.CloseCode,                       Is.Null,           "the Close after the message of the limit");

                Assert.That(refused.Message?.Length,                  Is.Null,           "the length of the message of a byte more, none");
                Assert.That(refused.CloseCode,                        Is.EqualTo(MessageTooBig));

            });

        }

        #endregion

        #region AMessageInFragments_OfTheLimit_IsReceived_OneByteMore_FailsWith1009(Stack, Client)

        /// <summary>
        /// Three fragments that add up to exactly the limit are received as one
        /// message. Three that add up to a byte more fail with 1009, the last
        /// fragment's payload fed as well.
        /// </summary>
        [TestCaseSource(typeof(WebSocketScript), nameof(WebSocketScript.Ends))]
        public void AMessageInFragments_OfTheLimit_IsReceived_OneByteMore_FailsWith1009(String Stack, Boolean Client)
        {

            var third         = Limit / 3;
            var payload       = Filled(Limit + 1, 0x5A);

            var atTheLimit    = new ScriptedTunnel();
            atTheLimit.Feed(Frame(Fin: false, Binary,       payload[..third],                   Masked: !Client),
                            Frame(Fin: false, Continuation, payload[third..(2 * third)],        Masked: !Client),
                            Frame(Fin: true,  Continuation, payload[(2 * third)..Limit],        Masked: !Client));
            var received      = Receive(OurEnd(Stack, Client, atTheLimit, Limit), atTheLimit);

            var oneByteMore   = new ScriptedTunnel();
            oneByteMore.Feed(Frame(Fin: false, Binary,       payload[..third],                  Masked: !Client),
                             Frame(Fin: false, Continuation, payload[third..(2 * third)],       Masked: !Client),
                             Frame(Fin: true,  Continuation, payload[(2 * third)..],            Masked: !Client));
            var refused       = Receive(OurEnd(Stack, Client, oneByteMore, Limit), oneByteMore);

            Assert.Multiple(() =>
            {

                Assert.That(received.Message?.Length,                         Is.EqualTo(Limit), "the length of the message of the limit");
                Assert.That(received.Message?.SequenceEqual(payload[..Limit]), Is.True,           "the message of the limit, byte for byte");
                Assert.That(received.CloseCode,                               Is.Null,           "the Close after the message of the limit");

                Assert.That(refused.Message?.Length,                          Is.Null,           "the length of the message of a byte more, none");
                Assert.That(refused.CloseCode,                                Is.EqualTo(MessageTooBig));

            });

        }

        #endregion

        #region AMessageThatInflates_ToTheLimit_IsReceived_ToOneByteMore_FailsWith1009(Stack, Client)

        /// <summary>
        /// A compressed message that inflates to exactly the limit is received
        /// whole; one that inflates to a byte more fails with 1009. Both are a few
        /// KiB as they travel: it is the inflated size that counts.
        /// </summary>
        [TestCaseSource(typeof(WebSocketScript), nameof(WebSocketScript.Ends))]
        public void AMessageThatInflates_ToTheLimit_IsReceived_ToOneByteMore_FailsWith1009(String Stack, Boolean Client)
        {

            var payload       = Filled(Limit + 1, 0x5A);

            var atTheLimit    = new ScriptedTunnel();
            atTheLimit.Feed(Frame(Fin: true, Binary, Deflate(payload[..Limit]), Masked: !Client, Rsv1: true));
            var received      = Receive(OurEnd(Stack, Client, atTheLimit, Limit, PerMessageDeflate: true), atTheLimit);

            var oneByteMore   = new ScriptedTunnel();
            oneByteMore.Feed(Frame(Fin: true, Binary, Deflate(payload),          Masked: !Client, Rsv1: true));
            var refused       = Receive(OurEnd(Stack, Client, oneByteMore, Limit, PerMessageDeflate: true), oneByteMore);

            Assert.Multiple(() =>
            {

                Assert.That(received.Message?.Length,                         Is.EqualTo(Limit), "the length of the message that inflates to the limit");
                Assert.That(received.Message?.SequenceEqual(payload[..Limit]), Is.True,           "the message that inflates to the limit, byte for byte");
                Assert.That(received.CloseCode,                               Is.Null,           "the Close after the message that inflates to the limit");

                Assert.That(refused.Message?.Length,                          Is.Null,           "the length of the message that inflates to a byte more, none");
                Assert.That(refused.CloseCode,                                Is.EqualTo(MessageTooBig));

            });

        }

        #endregion

    }

}
