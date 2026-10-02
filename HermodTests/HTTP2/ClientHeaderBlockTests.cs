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
    /// Response header blocks the client receives: on a stream it no longer has
    /// an exchange for, and continued in CONTINUATION frames.
    ///
    /// A block on a stream without an exchange — trailers the server sent before
    /// it read the client's RST_STREAM, say — was dropped undecoded. The HPACK
    /// dynamic table is the connection's, though, and RFC 9113, Section 5.1 has
    /// such frames minimally processed, "updating header compression state": a
    /// block that added a field to the server's table left the client's a step
    /// behind, and the next response that referred to it was decoded wrong.
    /// Split into HEADERS and CONTINUATION, such a block ended the connection.
    /// Now every block is decoded, and then dropped if no exchange takes it.
    ///
    /// And END_STREAM is a flag of the HEADERS frame; a CONTINUATION frame has
    /// none (Section 6.10). The client took it from the CONTINUATION frame that
    /// ended the block, so a response whose head was continued, and ended the
    /// stream, never ended.
    /// </summary>
    [TestFixture]
    public class ClientHeaderBlockTests
    {

        #region (helpers)

        /// <summary>
        /// A header block in two frames: HEADERS with its first octet, END_STREAM
        /// if asked, and no END_HEADERS, and a CONTINUATION frame with the rest,
        /// which ends the block.
        /// </summary>
        private static HTTP2Frame[] InTwoFrames(UInt32 StreamId, Byte[] Block, Boolean EndStream)

            => [
                   HTTP2Frame.CreateHeaders(StreamId, Block[..1], EndStream, EndHeaders: false),
                   new HTTP2Frame {
                       Type      = HTTP2FrameType.CONTINUATION,
                       Flags     = HTTP2FrameFlags.END_HEADERS,
                       StreamId  = StreamId,
                       Payload   = Block[1..]
                   }
               ];

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

        #endregion


        #region TrailersCrossingTheClientsReset_StillDecoded(Continued)

        /// <summary>
        /// The server rejects a CONNECT with a 407 whose body is to follow, and the
        /// client resets the stream, as it reads nothing of the rest. The server's
        /// trailers cross the RST_STREAM, and add their field to the server's
        /// HPACK dynamic table. The response to the next request refers to that
        /// field in the table, and arrives as the server sent it: the client
        /// decoded the trailers, though no exchange took them. In one HEADERS
        /// frame, or continued in a CONTINUATION frame.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task TrailersCrossingTheClientsReset_StillDecoded(Boolean Continued)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());

            var opening     = connection.OpenTunnelAsync("localhost:443");
            var connect     = await transport.NextHeadersAsync();

            await transport.SendHeadersAsync(connect.StreamId, [(":status", "407")]);

            var rejected    = await EndOf(opening);

            var trailers    = transport.EncodeHeaderBlock([("x-checksum", "abc123")]);

            if (Continued)
                await transport.SendAsync(InTwoFrames(connect.StreamId, trailers, EndStream: true));
            else
                await transport.SendAsync(HTTP2Frame.CreateHeaders(connect.StreamId, trailers, EndStream: true, EndHeaders: true));

            var next        = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.https, "localhost", "/next");
            var request     = await transport.NextHeadersAsync();

            await transport.SendHeadersAsync(request.StreamId, [(":status", "200"), ("x-checksum", "abc123")], EndStream: true);

            var answered    = await EndOf(next);

            Assert.Multiple(() =>
            {

                Assert.That(rejected.Failure,  Is.InstanceOf<HTTP2StreamException>(),  "the CONNECT was rejected");

                Assert.That(answered.Ended,    Is.True,                                "the next request was answered");
                Assert.That(answered.Failure,  Is.Null,                                "and did not fail");

                if (answered.Ended && answered.Failure is null)
                    Assert.That(next.Result.HeaderValue("x-checksum"),  Is.EqualTo("abc123"),  "the field the response refers to in the dynamic table");

            });

            await connection.CloseAsync().WaitAsync(HoldingH2Transport.StepTimeout);

        }

        #endregion

        #region ContinuedHeadersWithEndStream_ResponseEnds()

        /// <summary>
        /// A response that is all head, and ends the stream: its HEADERS frame has
        /// END_STREAM and no END_HEADERS, a CONTINUATION frame ends the block. The
        /// response ends with the block, as the HEADERS frame said.
        /// </summary>
        [Test]
        public async Task ContinuedHeadersWithEndStream_ResponseEnds()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());

            var get         = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.https, "localhost", "/empty");
            var request     = await transport.NextHeadersAsync();

            await transport.SendAsync(InTwoFrames(request.StreamId,
                                                transport.EncodeHeaderBlock([(":status", "204"), ("x-request-id", "42")]),
                                                EndStream: true));

            var answered    = await EndOf(get);

            Assert.Multiple(() =>
            {

                Assert.That(answered.Ended,    Is.True,  "the response ended");
                Assert.That(answered.Failure,  Is.Null,  "and did not fail");

                if (answered.Ended && answered.Failure is null)
                {
                    Assert.That(get.Result.Status,                       Is.EqualTo(204),   "its status");
                    Assert.That(get.Result.HeaderValue("x-request-id"),  Is.EqualTo("42"),  "the field in its CONTINUATION frame");
                    Assert.That(get.Result.Body,                         Is.Empty,          "its body");
                }

            });

            await connection.CloseAsync().WaitAsync(HoldingH2Transport.StepTimeout);

        }

        #endregion

    }

}
