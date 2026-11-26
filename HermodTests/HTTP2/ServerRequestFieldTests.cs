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

using System.Buffers.Binary;
using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// The regular fields of a request (RFC 9113, Section 8.2): a field name is
    /// lowercase (Section 8.2.1), a connection-specific field is not carried at
    /// all, and te carries "trailers" and nothing else (Section 8.2.2) — in its
    /// header block and in its trailers. A request that breaks one is malformed,
    /// its stream is reset with PROTOCOL_ERROR, it reaches no handler, and the
    /// connection goes on. The client holds a response to the same rules
    /// (ClientResponseFieldTests); the characters of a field are
    /// ServerRequestFieldCharacterTests'.
    ///
    /// te's "trailers" is read in any case, as RFC 9110's ABNF reads a transfer
    /// coding's name, and as nghttp2 and the client read it. The server took it
    /// in lowercase only, and reset a request with "te: Trailers".
    /// </summary>
    [TestFixture]
    public class ServerRequestFieldTests
    {

        #region (helpers)

        private static readonly TimeSpan StepTimeout = PipedH2ServerConnection.StepTimeout;

        /// <summary>
        /// Takes down the fields of every request that reaches it, and answers
        /// each with 200 "ok".
        /// </summary>
        private sealed class Handler
        {

            private readonly ConcurrentQueue<List<(String Name, String Value)>> requests = new();

            public List<List<(String Name, String Value)>> Requests
                => [.. requests];

            public Task<(List<(String Name, String Value)> ResponseHeaders, Byte[]? ResponseBody)> Buffered(UInt32                             StreamId,
                                                                                                           List<(String Name, String Value)>  Headers,
                                                                                                           Byte[]?                            Body,
                                                                                                           CancellationToken                  CancellationToken)
            {
                requests.Enqueue([.. Headers]);
                return Task.FromResult<(List<(String Name, String Value)>, Byte[]?)>(([(":status", "200")], "ok"u8.ToArray()));
            }

        }

        /// <summary>
        /// The pseudo-header fields of a GET for /, as RequestAsync sends them.
        /// </summary>
        private static List<(String Name, String Value)> Get()

            => [(":method",    "GET"),
                (":scheme",    "http"),
                (":authority", "localhost"),
                (":path",      "/")];

        /// <summary>
        /// Send a header block in as many frames as it takes.
        /// </summary>
        private static async Task SendBlockAsync(PipedH2ServerConnection  Peer,
                                                 UInt32                   StreamId,
                                                 Byte[]                   Block,
                                                 Boolean                  EndStream)
        {
            foreach (var frame in RawHeaderBlock.Frames(StreamId, Block, EndStream))
                await Peer.SendAsync(frame).WaitAsync(StepTimeout);
        }

        /// <summary>
        /// The error code of the RST_STREAM the server sent on the stream, or
        /// null if it sent none within the step timeout.
        /// </summary>
        private static async Task<HTTP2ErrorCode?> ResetOfAsync(PipedH2ServerConnection Peer, UInt32 StreamId)
        {
            try
            {
                return await Peer.ReadToResetAsync(StreamId).WaitAsync(StepTimeout);
            }
            catch (TimeoutException)
            {
                return null;
            }
        }

        /// <summary>
        /// The error codes of the GOAWAY frames the server sent.
        /// </summary>
        private static List<HTTP2ErrorCode> GoAways(PipedH2ServerConnection Peer)

            => [.. Peer.FramesOn(0).Where (frame => frame.Frame.Type == HTTP2FrameType.GOAWAY).
                                    Select(frame => (HTTP2ErrorCode) BinaryPrimitives.ReadUInt32BigEndian(frame.Frame.Payload.AsSpan(4)))];

        /// <summary>
        /// Send a GET on stream 1 with these regular fields, and, if
        /// <paramref name="Trailers"/> are given, a body and these trailers after
        /// it. Then a GET for /next on stream 3. Assert that stream 1 was reset
        /// with PROTOCOL_ERROR, that only the next request reached the handler,
        /// and that it was answered, the connection going on.
        /// </summary>
        private static async Task AssertResetAsync((String Name, String Value)[]   Fields,
                                                   (String Name, String Value)[]?  Trailers = null)
        {

            var handler = new Handler();

            await using var peer = await PipedH2ServerConnection.StartAsync(handler.Buffered);

            await SendBlockAsync(peer, 1, RawHeaderBlock.Literals([.. Get(), .. Fields]), EndStream: Trailers is null);

            if (Trailers is not null)
            {
                await peer.SendAsync(HTTP2Frame.CreateData(1, "body"u8.ToArray())).WaitAsync(StepTimeout);
                await SendBlockAsync(peer, 1, RawHeaderBlock.Literals(Trailers), EndStream: true);
            }

            var reset  = await ResetOfAsync(peer, 1);

            await peer.RequestAsync(3, "/next");

            var next   = await peer.TryResponseAsync(3, StepTimeout);

            Assert.Multiple(() =>
            {
                Assert.That(reset,                    Is.EqualTo(HTTP2ErrorCode.PROTOCOL_ERROR),  "how the server reset the malformed request's stream");
                Assert.That(handler.Requests.Count,   Is.EqualTo(1),                              "requests that reached the handler: only the next");
                Assert.That(next?.Status,             Is.EqualTo("200"),                          "status of the next request");
                Assert.That(GoAways(peer),            Is.Empty,                                   "GOAWAY frames the server sent");
            });

        }

        /// <summary>
        /// Send a GET on stream 1 with these regular fields, and, if
        /// <paramref name="Trailers"/> are given, a body and these trailers after
        /// it. Assert that it was served, and that the handler was given the
        /// fields of its header block as they were sent.
        /// </summary>
        private static async Task AssertServedAsync((String Name, String Value)[]   Fields,
                                                    (String Name, String Value)[]?  Trailers = null)
        {

            var handler = new Handler();

            await using var peer = await PipedH2ServerConnection.StartAsync(handler.Buffered);

            await SendBlockAsync(peer, 1, RawHeaderBlock.Literals([.. Get(), .. Fields]), EndStream: Trailers is null);

            if (Trailers is not null)
            {
                await peer.SendAsync(HTTP2Frame.CreateData(1, "body"u8.ToArray())).WaitAsync(StepTimeout);
                await SendBlockAsync(peer, 1, RawHeaderBlock.Literals(Trailers), EndStream: true);
            }

            var response = await peer.TryResponseAsync(1, StepTimeout);

            Assert.Multiple(() =>
            {
                Assert.That(response?.Status,         Is.EqualTo("200"),  "status of the request");
                Assert.That(handler.Requests.Count,   Is.EqualTo(1),      "requests that reached the handler");
                if (handler.Requests.Count == 1)
                    Assert.That(handler.Requests[0].Where(field => field.Name[0] != ':'),  Is.EqualTo(Fields),  "the fields the handler was given");
                Assert.That(GoAways(peer),            Is.Empty,           "GOAWAY frames the server sent");
            });

        }

        #endregion


        #region TeTrailers_InAnyCase_Served(Value)

        /// <summary>
        /// te with "trailers", in any case: the request is served, and the handler
        /// is given te as it was sent. The server used to reset all but the first.
        /// </summary>
        [TestCase("trailers")]
        [TestCase("Trailers")]
        [TestCase("TRAILERS")]
        [TestCase("tRaIlErS")]
        public Task TeTrailers_InAnyCase_Served(String Value)

            => AssertServedAsync([("te", Value), ("x-request-id", "ABC")]);

        #endregion

        #region TeOtherThanTrailers_ResetsTheStream(Value)

        /// <summary>
        /// te carries "trailers" and nothing else (Section 8.2.2): not another
        /// transfer coding, not a list with "trailers" in it, not a parameter.
        /// </summary>
        [TestCase("gzip")]
        [TestCase("trailers, deflate")]
        [TestCase("trailers;q=1")]
        [TestCase("trailers,trailers")]
        [TestCase("trailer")]
        [TestCase("")]
        public Task TeOtherThanTrailers_ResetsTheStream(String Value)

            => AssertResetAsync([("te", Value)]);

        #endregion

        #region ConnectionSpecificField_ResetsTheStream(Name, Value)

        /// <summary>
        /// A connection-specific field makes the request malformed, whatever its
        /// value (Section 8.2.2).
        /// </summary>
        [TestCase("connection",         "keep-alive")]
        [TestCase("keep-alive",         "timeout=5")]
        [TestCase("proxy-connection",   "close")]
        [TestCase("transfer-encoding",  "chunked")]
        [TestCase("upgrade",            "h2c")]
        public Task ConnectionSpecificField_ResetsTheStream(String Name, String Value)

            => AssertResetAsync([(Name, Value)]);

        #endregion

        #region UppercaseFieldName_ResetsTheStream(Name)

        /// <summary>
        /// A field name is lowercase (Section 8.2.1): one uppercase letter makes
        /// the request malformed, wherever it is.
        /// </summary>
        [TestCase("Content-Type")]
        [TestCase("x-requesT-id")]
        [TestCase("TE")]
        public Task UppercaseFieldName_ResetsTheStream(String Name)

            => AssertResetAsync([(Name, "trailers")]);

        #endregion

        #region InTrailers(Name, Value, Served)

        /// <summary>
        /// The rules hold for trailers' fields as well: a malformed field there
        /// resets the request, which had not ended and reaches no handler; te
        /// with "Trailers" there is served.
        /// </summary>
        [TestCase("te",                 "Trailers",  true)]
        [TestCase("te",                 "gzip",      false)]
        [TestCase("transfer-encoding",  "chunked",   false)]
        [TestCase("X-Checksum",         "abc",       false)]
        public Task InTrailers(String Name, String Value, Boolean Served)

            => Served ? AssertServedAsync([("x-request-id", "ABC")], [("x-ok", "1"), (Name, Value)])
                      : AssertResetAsync ([("x-request-id", "ABC")], [("x-ok", "1"), (Name, Value)]);

        #endregion

    }

}
