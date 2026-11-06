/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using NUnit.Framework;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.Messaging;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// The frames of the message protocol over an SSH subsystem channel: a length that is checked before
    /// anything is allocated for it, and HELLO, WELCOME and CLOSE read strictly.
    /// </summary>
    [TestFixture]
    public class SshMessageFramingTests
    {

        #region (private) A stream that notes how much was asked of it

        /// <summary>
        /// A stream over the given bytes that notes the largest buffer it was asked to fill - so that a test
        /// of a 2 GiB length shows it was never allocated.
        /// </summary>
        private sealed class NotingStream(Byte[] Bytes) : MemoryStream(Bytes)
        {

            public Int32 LargestRead { get; private set; }

            public override ValueTask<Int32> ReadAsync(Memory<Byte> Buffer, CancellationToken CancellationToken = default)
            {
                LargestRead = Math.Max(LargestRead, Buffer.Length);
                return base.ReadAsync(Buffer, CancellationToken);
            }

        }

        private static Byte[] Header(UInt32 Length, Byte Type)
        {
            var header = new Byte[5];
            BinaryPrimitives.WriteUInt32BigEndian(header, Length);
            header[4] = Type;
            return header;
        }

        private static Task<SshMessageFrame?> Read(Byte[] Bytes, UInt64 MaxMessageSize = 1024)
            => SshMessageFraming.ReadAsync(new MemoryStream(Bytes), MaxMessageSize).AsTask();

        private static Byte[] JSON(String Text)
            => Encoding.UTF8.GetBytes(Text);

        #endregion


        #region Frames_RoundTrip

        [Test]
        public async Task Frames_RoundTrip()
        {

            var stream = new MemoryStream();

            stream.Write(SshMessageFraming.Encode(SshMessageFrameType.Text,   Encoding.UTF8.GetBytes("[2,\"1\",\"Heartbeat\",{}]")));
            stream.Write(SshMessageFraming.Encode(SshMessageFrameType.Binary, [ 1, 2, 3 ]));
            stream.Write(SshMessageFraming.Encode(SshMessageFrameType.Ping,   []));
            stream.Position = 0;

            var text    = await SshMessageFraming.ReadAsync(stream, 1024);
            var binary  = await SshMessageFraming.ReadAsync(stream, 1024);
            var ping    = await SshMessageFraming.ReadAsync(stream, 1024);
            var end     = await SshMessageFraming.ReadAsync(stream, 1024);

            Assert.Multiple(() => {
                Assert.That(text!.Value.Type,                                 Is.EqualTo(SshMessageFrameType.Text));
                Assert.That(Encoding.UTF8.GetString(text!.Value.Payload),     Is.EqualTo("[2,\"1\",\"Heartbeat\",{}]"));
                Assert.That(binary!.Value.Payload,                            Is.EqualTo(new Byte[] { 1, 2, 3 }));
                Assert.That(ping!.Value.Type,                                 Is.EqualTo(SshMessageFrameType.Ping));
                Assert.That(ping!.Value.Payload,                              Is.Empty);
                Assert.That(end,                                              Is.Null, "the stream ended between two frames");
            });

        }

        #endregion

        #region Length_IsCheckedBeforeAnythingIsAllocated

        [TestCase(0x10, 1026u,           MessageCloseCode.MessageTooBig)]   // TEXT, one byte over 1024
        [TestCase(0x11, 0x7FFF_FFFFu,    MessageCloseCode.MessageTooBig)]   // BINARY, 2 GiB
        [TestCase(0x11, 0xFFFF_FFFFu,    MessageCloseCode.MessageTooBig)]   // BINARY, beyond Int32
        [TestCase(0x01, 65_538u,         MessageCloseCode.ProtocolError)]   // HELLO over 64 KiB
        [TestCase(0x90, 65_538u,         MessageCloseCode.ProtocolError)]   // an unknown kind over 64 KiB
        public async Task Length_IsCheckedBeforeAnythingIsAllocated(Int32 Type, UInt32 Length, MessageCloseCode Code)
        {

            var stream = new NotingStream(Header(Length, (Byte) Type));

            var error  = await Assert.ThrowsAsync<SshMessageProtocolException>(async () => await SshMessageFraming.ReadAsync(stream, 1024));

            Assert.Multiple(() => {
                Assert.That(error!.Code,         Is.EqualTo(Code));
                Assert.That(stream.LargestRead,  Is.LessThanOrEqualTo(SshMessageFraming.HeaderLength), "a buffer was sized by the peer's length");
            });

        }

        #endregion

        #region AtTheLimit_IsRead

        [Test]
        public async Task AtTheLimit_IsRead()
        {

            var frame = SshMessageFraming.Encode(SshMessageFrameType.Text, new Byte[1024]);

            Assert.That((await Read(frame, 1024))!.Value.Payload, Has.Length.EqualTo(1024));

        }

        #endregion

        #region Malformed_IsAProtocolError

        [TestCase(new Byte[] { 0, 0, 0 },                               TestName = "Malformed_TruncatedHeader")]
        [TestCase(new Byte[] { 0, 0, 0, 0, 0x10 },                      TestName = "Malformed_LengthZero")]
        [TestCase(new Byte[] { 0, 0, 0, 5, 0x10, 1, 2 },                TestName = "Malformed_TruncatedPayload")]
        public async Task Malformed_IsAProtocolError(Byte[] Bytes)
        {

            var error = await Assert.ThrowsAsync<SshMessageProtocolException>(async () => await Read(Bytes));

            Assert.That(error!.Code, Is.EqualTo(MessageCloseCode.ProtocolError));

        }

        #endregion


        #region Hello_RoundTrip_KeepsTheApplicationsProperties

        [Test]
        public void Hello_RoundTrip_KeepsTheApplicationsProperties()
        {

            var hello  = SshMessageHello.Create([ "ocpp2.1", "ocpp2.0.1", "ocpp2.1" ], 4096, new JObject { ["networkingMode"] = "OverlayNetwork", ["subprotocols"] = "overridden" });
            var parsed = SshMessageHello.Parse(hello.ToBytes());

            Assert.Multiple(() => {
                Assert.That(parsed.Subprotocols,                               Is.EqualTo(new[] { "ocpp2.1", "ocpp2.0.1" }));
                Assert.That(parsed.MaxMessageSize,                             Is.EqualTo(4096));
                Assert.That(parsed.JSON["networkingMode"]?.Value<String>(),    Is.EqualTo("OverlayNetwork"));
            });

        }

        #endregion

        #region Hello_DateLikeStrings_StayStrings

        [Test]
        public void Hello_DateLikeStrings_StayStrings()
        {

            var parsed = SshMessageHello.Parse(JSON("{\"commissioned\":\"2026-11-06T10:00:00+01:00\"}"));

            Assert.That(parsed.JSON["commissioned"]!.Type, Is.EqualTo(JTokenType.String));

        }

        #endregion

        #region Hello_Malformed_IsAProtocolError

        [TestCase("not json",                                      TestName = "Hello_NotJSON")]
        [TestCase("[\"ocpp2.1\"]",                                 TestName = "Hello_AnArray")]
        [TestCase("{} {}",                                         TestName = "Hello_TwoObjects")]
        [TestCase("{\"subprotocols\":\"ocpp2.1\"}",                TestName = "Hello_SubprotocolsNotAnArray")]
        [TestCase("{\"subprotocols\":[1]}",                        TestName = "Hello_SubprotocolNotAString")]
        [TestCase("{\"subprotocols\":[\"\"]}",                     TestName = "Hello_SubprotocolEmpty")]
        [TestCase("{\"maxMessageSize\":0}",                        TestName = "Hello_MaxMessageSizeZero")]
        [TestCase("{\"maxMessageSize\":-5}",                       TestName = "Hello_MaxMessageSizeNegative")]
        [TestCase("{\"maxMessageSize\":\"16\"}",                   TestName = "Hello_MaxMessageSizeAString")]
        [TestCase("{\"maxMessageSize\":99999999999999999999999}",  TestName = "Hello_MaxMessageSizeBeyondUInt64")]
        public void Hello_Malformed_IsAProtocolError(String Text)
        {
            Assert.That(() => SshMessageHello.Parse(JSON(Text)), Throws.InstanceOf<SshMessageProtocolException>());
        }

        [Test]
        public void Hello_TooManySubprotocols_IsAProtocolError()
        {

            var many = new JArray(Enumerable.Range(0, SshMessageHello.MaxSubprotocols + 1).Select(n => $"p{n}"));

            Assert.That(() => SshMessageHello.Parse(JSON(new JObject { ["subprotocols"] = many }.ToString())), Throws.InstanceOf<SshMessageProtocolException>());

        }

        [Test]
        public void Hello_InvalidUTF8_IsAProtocolError()
        {
            Assert.That(() => SshMessageHello.Parse([ (Byte) '{', 0xFF, (Byte) '}' ]), Throws.InstanceOf<SshMessageProtocolException>());
        }

        #endregion

        #region Welcome_And_Refusal_RoundTrip

        [Test]
        public void Welcome_And_Refusal_RoundTrip()
        {

            var welcome  = SshMessageWelcome.Parse(SshMessageWelcome.Create("ocpp2.1", 2048, new JObject { ["networkingNodeId"] = "cs001" }).ToBytes());
            var none     = SshMessageWelcome.Parse(SshMessageWelcome.Create(null, null).ToBytes());
            var refusal  = SshMessageRefusal.Decode(SshMessageRefusal.Encode("unknown-station", "Who are you?"));

            Assert.Multiple(() => {
                Assert.That(welcome.Subprotocol,                                   Is.EqualTo("ocpp2.1"));
                Assert.That(welcome.MaxMessageSize,                                Is.EqualTo(2048));
                Assert.That(welcome.JSON["networkingNodeId"]?.Value<String>(),     Is.EqualTo("cs001"));
                Assert.That(none.Subprotocol,                                      Is.Null);
                Assert.That(none.MaxMessageSize,                                   Is.Null);
                Assert.That(refusal,                                               Is.EqualTo(("unknown-station", (String?) "Who are you?")));
            });

        }

        #endregion

        #region Close_RoundTrip

        [Test]
        public void Close_RoundTrip()
        {

            var close  = SshMessageFraming.DecodeClose(SshMessageFraming.EncodeClose(MessageCloseCode.GoingAway, "stopping"));
            var empty  = SshMessageFraming.DecodeClose([]);

            Assert.Multiple(() => {
                Assert.That(close, Is.EqualTo((MessageCloseCode.GoingAway,     (String?) "stopping")));
                Assert.That(empty, Is.EqualTo((MessageCloseCode.NormalClosure, (String?) null)));
                Assert.That(() => SshMessageFraming.DecodeClose([ 3 ]), Throws.InstanceOf<SshMessageProtocolException>());
            });

        }

        #endregion

    }

}
