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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// HTTP WebSocket frame tests.
    /// </summary>
    [TestFixture]
    public class WebSocketFrameTests
    {

        #region TextFrame_JSONRoundtrip_Test()

        [Test]
        public void TextFrame_JSONRoundtrip_Test()
        {

            var frame = WebSocketFrame.Text(
                            "Hello WebSocket!",
                            WebSocketFrame.Fin.Final,
                            Rsv1: WebSocketFrame.Rsv.On
                        );

            var json = frame.ToJSON();

            Assert.That(
                WebSocketFrame.TryParse(
                    json,
                    out var parsedFrame,
                    out var errorResponse
                ),
                Is.True,
                errorResponse ?? "Could not parse WebSocket frame JSON."
            );

            Assert.That(parsedFrame, Is.Not.Null);
            Assert.That(parsedFrame!.Opcode, Is.EqualTo(frame.Opcode));
            Assert.That(parsedFrame.FIN, Is.EqualTo(frame.FIN));
            Assert.That(parsedFrame.Rsv1, Is.EqualTo(frame.Rsv1));
            Assert.That(parsedFrame.Payload.ToUTF8String(), Is.EqualTo(frame.Payload.ToUTF8String()));
            Assert.That(parsedFrame.IsMasked, Is.False);

        }

        #endregion

        #region ContinuationFrame_JSONRoundtrip_Test()

        [Test]
        public void ContinuationFrame_JSONRoundtrip_Test()
        {

            var payload = new Byte[] { 0x10, 0x20, 0x30, 0x40 };
            var frame   = WebSocketFrame.Continuation(
                              payload,
                              WebSocketFrame.Fin.More
                          );

            var json = frame.ToJSON();

            Assert.That(
                WebSocketFrame.TryParse(
                    json,
                    out var parsedFrame,
                    out var errorResponse
                ),
                Is.True,
                errorResponse ?? "Could not parse WebSocket frame JSON."
            );

            Assert.That(parsedFrame, Is.Not.Null);
            Assert.That(parsedFrame!.Opcode, Is.EqualTo(frame.Opcode));
            Assert.That(parsedFrame.FIN, Is.EqualTo(frame.FIN));
            Assert.That(parsedFrame.Payload.ToHexString(), Is.EqualTo(frame.Payload.ToHexString()));
            Assert.That(parsedFrame.IsMasked, Is.False);

        }

        #endregion

        #region BinaryFrame_JSONRoundtrip_Test()

        [Test]
        public void BinaryFrame_JSONRoundtrip_Test()
        {

            var payload = new Byte[] { 0x00, 0x01, 0x02, 0xfe, 0xff };
            var frame   = WebSocketFrame.Binary(payload);
            var json    = frame.ToJSON();

            Assert.That(
                WebSocketFrame.TryParse(
                    json,
                    out var parsedFrame,
                    out var errorResponse
                ),
                Is.True,
                errorResponse ?? "Could not parse WebSocket frame JSON."
            );

            Assert.That(parsedFrame, Is.Not.Null);
            Assert.That(parsedFrame!.Opcode, Is.EqualTo(frame.Opcode));
            Assert.That(parsedFrame.Payload.Length, Is.EqualTo(frame.Payload.Length));
            Assert.That(parsedFrame.Payload.ToHexString(), Is.EqualTo(frame.Payload.ToHexString()));

        }

        #endregion

        #region PingFrame_JSONRoundtrip_Test()

        [Test]
        public void PingFrame_JSONRoundtrip_Test()
        {

            var frame = WebSocketFrame.Ping("ping".ToUTF8Bytes());
            var json  = frame.ToJSON();

            Assert.That(
                WebSocketFrame.TryParse(
                    json,
                    out var parsedFrame,
                    out var errorResponse
                ),
                Is.True,
                errorResponse ?? "Could not parse WebSocket frame JSON."
            );

            Assert.That(parsedFrame, Is.Not.Null);
            Assert.That(parsedFrame!.Opcode, Is.EqualTo(frame.Opcode));
            Assert.That(parsedFrame.FIN, Is.EqualTo(frame.FIN));
            Assert.That(parsedFrame.Payload.ToUTF8String(), Is.EqualTo(frame.Payload.ToUTF8String()));
            Assert.That(parsedFrame.IsMasked, Is.False);

        }

        #endregion

        #region PongFrame_JSONRoundtrip_Test()

        [Test]
        public void PongFrame_JSONRoundtrip_Test()
        {

            var frame = WebSocketFrame.Pong("pong".ToUTF8Bytes());
            var json  = frame.ToJSON();

            Assert.That(
                WebSocketFrame.TryParse(
                    json,
                    out var parsedFrame,
                    out var errorResponse
                ),
                Is.True,
                errorResponse ?? "Could not parse WebSocket frame JSON."
            );

            Assert.That(parsedFrame, Is.Not.Null);
            Assert.That(parsedFrame!.Opcode, Is.EqualTo(frame.Opcode));
            Assert.That(parsedFrame.FIN, Is.EqualTo(frame.FIN));
            Assert.That(parsedFrame.Payload.ToUTF8String(), Is.EqualTo(frame.Payload.ToUTF8String()));
            Assert.That(parsedFrame.IsMasked, Is.False);

        }

        #endregion

        #region CloseFrame_JSONRoundtrip_Test()

        [Test]
        public void CloseFrame_JSONRoundtrip_Test()
        {

            var frame = WebSocketFrame.Close(
                            WebSocketFrame.ClosingStatusCode.NormalClosure,
                            "Done"
                        );

            var json = frame.ToJSON();

            Assert.That(
                WebSocketFrame.TryParse(
                    json,
                    out var parsedFrame,
                    out var errorResponse
                ),
                Is.True,
                errorResponse ?? "Could not parse WebSocket frame JSON."
            );

            Assert.That(parsedFrame, Is.Not.Null);
            Assert.That(parsedFrame!.Opcode, Is.EqualTo(frame.Opcode));
            Assert.That(parsedFrame.GetClosingStatusCode(), Is.EqualTo(frame.GetClosingStatusCode()));
            Assert.That(parsedFrame.GetClosingReason(), Is.EqualTo(frame.GetClosingReason()));
            Assert.That(parsedFrame.Payload.ToHexString(), Is.EqualTo(frame.Payload.ToHexString()));

        }

        #endregion

        #region EmptyCloseFrame_JSONRoundtrip_Test()

        [Test]
        public void EmptyCloseFrame_JSONRoundtrip_Test()
        {

            Assert.That(
                WebSocketFrame.TryParse(
                    [ 0x88, 0x00 ],
                    out var frame,
                    out _,
                    out var parseError
                ),
                Is.True,
                parseError ?? "Could not parse WebSocket frame."
            );

            var json = frame!.ToJSON();

            Assert.That(
                WebSocketFrame.TryParse(
                    json,
                    out var parsedFrame,
                    out var jsonParseError
                ),
                Is.True,
                jsonParseError ?? "Could not parse WebSocket frame JSON."
            );

            Assert.That(parsedFrame, Is.Not.Null);
            Assert.That(parsedFrame!.Opcode, Is.EqualTo(WebSocketFrame.Opcodes.Close));
            Assert.That(parsedFrame.GetClosingStatusCode(), Is.EqualTo(WebSocketFrame.ClosingStatusCode.NoStatusReceived));
            Assert.That(parsedFrame.Payload.Length, Is.EqualTo(0));
            Assert.That(parsedFrame.GetClosingReason(), Is.Null);

        }

        #endregion

    }

}
