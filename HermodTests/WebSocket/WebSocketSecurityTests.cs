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

using System.Text;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    [TestFixture]
    public class WebSocketSecurityTests
    {

        [Test]
        public void InboundMessagesHaveFiniteDefaults()
        {
            var server = new WebSocketServer(HTTPPort: IPPort.Zero);
            var client = new WebSocketClient(URL.Parse("ws://127.0.0.1:12345"));

            Assert.That(server.MaxTextMessageSizeIn,   Is.EqualTo(WebSocketFrame.DefaultMaxPayloadSize));
            Assert.That(server.MaxBinaryMessageSizeIn, Is.EqualTo(WebSocketFrame.DefaultMaxPayloadSize));
            Assert.That(client.MaxTextMessageSizeIn,   Is.EqualTo(WebSocketFrame.DefaultMaxPayloadSize));
            Assert.That(client.MaxBinaryMessageSizeIn, Is.EqualTo(WebSocketFrame.DefaultMaxPayloadSize));
        }

        [Test]
        public async Task FragmentedMessageExceedingConfiguredLimitIsClosed()
        {
            var server = new WebSocketServer(HTTPPort: IPPort.Zero,
                                             RequireAuthentication: false,
                                             AutoStart: true) {
                             MaxTextMessageSizeIn = 5
                         };

            var client = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{server.IPPort}"));
            var close  = new TaskCompletionSource<WebSocketFrame.ClosingStatusCode>(
                             TaskCreationOptions.RunContinuationsAsynchronously
                         );

            client.OnCloseMessageReceived += (timestamp, sender, connection, frame,
                                              eventTrackingId, statusCode, reason, cancellationToken) => {
                close.TrySetResult(statusCode);
                return Task.CompletedTask;
            };

            try
            {
                Assert.That((await client.Connect()).Item2.HTTPStatusCode.Code, Is.EqualTo(101));

                await client.SendWebSocketFrame(WebSocketFrame.Text("abc", WebSocketFrame.Fin.More,
                                                                   Mask: WebSocketFrame.MaskStatus.On,
                                                                   MaskingKey: [1, 2, 3, 4]));

                await client.SendWebSocketFrame(WebSocketFrame.Continuation(Encoding.UTF8.GetBytes("def"),
                                                                           Mask: WebSocketFrame.MaskStatus.On,
                                                                           MaskingKey: [5, 6, 7, 8]));

                Assert.That(await close.Task.WaitAsync(TimeSpan.FromSeconds(5)),
                            Is.EqualTo(WebSocketFrame.ClosingStatusCode.MessageTooBig));
            }
            finally
            {
                client.Disconnect();
                await server.Shutdown(Wait: true);
            }
        }

        [Test]
        public void IncompleteFrameDoesNotAllocateAdvertisedPayload()
        {
            // Masked binary frame advertising 64 MiB, without its payload.
            Byte[] header = [0x82, 0xff, 0, 0, 0, 0, 4, 0, 0, 0, 1, 2, 3, 4];

            var before = GC.GetAllocatedBytesForCurrentThread();
            var result = WebSocketFrame.Parse(header,
                                              out _, out _, out _, out _);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(result, Is.EqualTo(WebSocketFrame.ParseResult.IncompleteData));
            Assert.That(allocated, Is.LessThan(1024 * 1024));
        }

        [Test]
        public void DecompressionHonorsMessageLimit()
        {
            var extension  = new WebSocketPerMessageDeflate();
            var plainText  = Encoding.UTF8.GetBytes(new String('a', 4096));
            var compressed = extension.Compress(plainText);

            Assert.That(extension.TryDecompress(compressed, out _, out _, out var exceeded,
                                                MessageSizeLimit: 128), Is.False);
            Assert.That(exceeded, Is.True);

            Assert.That(extension.TryDecompress(compressed, out var decompressed, out _, out exceeded,
                                                MessageSizeLimit: 4096), Is.True);
            Assert.That(exceeded, Is.False);
            Assert.That(decompressed, Is.EqualTo(plainText));
        }

    }

}
