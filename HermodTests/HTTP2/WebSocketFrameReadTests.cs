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

using HTTP2WS = org.GraphDefined.Vanaheimr.Hermod.HTTP2;

using static org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2.WebSocketScript;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// How the WebSocket over HTTP/2 and HTTP/3 reads its frames out of the chunks
    /// its tunnel hands over, which have no relationship to frame boundaries.
    ///
    /// Every chunk used to be appended to a copy of everything buffered before it,
    /// so a frame cost its size times the number of chunks it came in: a 16 MiB
    /// frame in HTTP/2's 16 KiB DATA frames copied about 8 GiB. Now each byte is
    /// copied once, into the frame's payload, and what is left of the last chunk
    /// stays buffered in that chunk.
    ///
    /// Run against both copies of the framing in both roles, over the scripted
    /// tunnel of <see cref="WebSocketScript"/>.
    /// </summary>
    [TestFixture]
    public class WebSocketFrameReadTests
    {

        #region AFrameInManySmallChunks_CostsAboutItsSize(Stack, Client)

        /// <summary>
        /// A 4 MiB frame in 4 KiB chunks, 1025 of them. Read with a copy of the
        /// buffer per chunk, that allocated about 2 GiB; now it allocates the
        /// payload, and once more for unmasking it on a server's end.
        /// </summary>
        [TestCaseSource(typeof(WebSocketScript), nameof(WebSocketScript.Ends))]
        public void AFrameInManySmallChunks_CostsAboutItsSize(String Stack, Boolean Client)
        {

            const Int32 size   = 4 * 1024 * 1024;
            const Int32 chunk  = 4 * 1024;

            var payload  = new Byte[size];
            new Random(6455).NextBytes(payload);

            var tunnel   = new ScriptedTunnel();
            var frame    = Frame(Fin: true, Binary, payload, Masked: !Client);

            for (var start = 0; start < frame.Length; start += chunk)
                tunnel.Feed(frame[start..Math.Min(start + chunk, frame.Length)]);

            var (message, closeCode, allocated) = Receive(OurEnd(Stack, Client, tunnel, HTTP2WS.WebSocketConnection.DefaultMaxMessageSize), tunnel);

            Assert.Multiple(() =>
            {
                Assert.That(message?.Length,                 Is.EqualTo(size),        "the length of the message");
                Assert.That(message?.SequenceEqual(payload), Is.True,                 "the message, byte for byte");
                Assert.That(closeCode,                       Is.Null,                 "the Close our end sent");
                Assert.That(allocated,                       Is.LessThan(3L * size),  "the bytes allocated while receiving");
            });

        }

        #endregion

        #region FramesCutAnywhere_AreReadAsSent(Stack, Client)

        /// <summary>
        /// Frames of every length encoding — 7 bits, 16 bits, 64 bits — and a
        /// fragmented message with a ping between its fragments, cut into chunks
        /// of one byte, of a few bytes, of 4 KiB, and sent in one: whatever the
        /// cut, the same four messages come out, and then the tunnel's end.
        /// </summary>
        [TestCaseSource(typeof(WebSocketScript), nameof(WebSocketScript.Ends))]
        public void FramesCutAnywhere_AreReadAsSent(String Stack, Boolean Client)
        {

            var medium   = Filled(  300, 0x4D);
            var large    = new Byte[70_000];
            new Random(8441).NextBytes(large);

            Byte[] sent  = [
                               .. Frame(Fin: true,  Text,         Encoding.UTF8.GetBytes("hello"),    Masked: !Client),
                               .. Frame(Fin: true,  Binary,       medium,                             Masked: !Client),
                               .. Frame(Fin: true,  Binary,       large,                              Masked: !Client),
                               .. Frame(Fin: false, Text,         Encoding.UTF8.GetBytes("frag"),     Masked: !Client),
                               .. Frame(Fin: true,  Ping,         Encoding.UTF8.GetBytes("ping"),     Masked: !Client),
                               .. Frame(Fin: true,  Continuation, Encoding.UTF8.GetBytes("mented"),   Masked: !Client)
                           ];

            Assert.Multiple(() =>
            {

                foreach (var cut in new[] { 1, 2, 3, 5, 7, 125, 4096, sent.Length })
                {

                    var tunnel   = new ScriptedTunnel();

                    for (var start = 0; start < sent.Length; start += cut)
                        tunnel.Feed(sent[start..Math.Min(start + cut, sent.Length)]);

                    tunnel.End();

                    var receive  = OurEnd(Stack, Client, tunnel, HTTP2WS.WebSocketConnection.DefaultMaxMessageSize);

                    var first    = Receive(receive, tunnel).Message;
                    var second   = Receive(receive, tunnel).Message;
                    var third    = Receive(receive, tunnel).Message;
                    var fourth   = Receive(receive, tunnel).Message;
                    var end      = Receive(receive, tunnel);

                    Assert.That(first  is not null && Encoding.UTF8.GetString(first)  == "hello",      Is.True,  $"cut {cut}: the 7-bit frame");
                    Assert.That(second?.SequenceEqual(medium),                                         Is.True,  $"cut {cut}: the 16-bit frame");
                    Assert.That(third?. SequenceEqual(large),                                          Is.True,  $"cut {cut}: the 64-bit frame");
                    Assert.That(fourth is not null && Encoding.UTF8.GetString(fourth) == "fragmented", Is.True,  $"cut {cut}: the fragmented message");
                    Assert.That(end.Message?.Length,                                                   Is.Null,  $"cut {cut}: after the tunnel's end");
                    Assert.That(end.CloseCode,                                                         Is.Null,  $"cut {cut}: the Close our end sent");

                }

            });

        }

        #endregion

        #region ATunnelEndingInsideAFrame_EndsTheReceive(Stack, Client)

        /// <summary>
        /// The tunnel ends halfway through a frame's payload: the receive returns
        /// null, without a Close, as the tunnel has gone.
        /// </summary>
        [TestCaseSource(typeof(WebSocketScript), nameof(WebSocketScript.Ends))]
        public void ATunnelEndingInsideAFrame_EndsTheReceive(String Stack, Boolean Client)
        {

            var frame    = Frame(Fin: true, Binary, Filled(1000, 0x42), Masked: !Client);
            var tunnel   = new ScriptedTunnel();

            tunnel.Feed(frame[..10], frame[10..500]);
            tunnel.End();

            var (message, closeCode, _) = Receive(OurEnd(Stack, Client, tunnel, HTTP2WS.WebSocketConnection.DefaultMaxMessageSize), tunnel);

            Assert.Multiple(() =>
            {
                Assert.That(message?.Length,  Is.Null,  "the length of the message, none");
                Assert.That(closeCode,        Is.Null,  "the Close our end sent");
            });

        }

        #endregion

    }

}
