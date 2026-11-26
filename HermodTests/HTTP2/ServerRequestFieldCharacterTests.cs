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
    /// The characters of a request's fields (RFC 9113, Section 8.2.1): a field
    /// name carries no octet from 0x00 to 0x20, none from 0x7F to 0xFF, and no
    /// colon but the one that opens a pseudo-header field's name; a field value
    /// carries no NUL, LF or CR, and does not start or end with SP or HTAB. A
    /// request with a field that breaks one is malformed, and "Malformed requests
    /// or responses that are detected MUST be treated as a stream error".
    ///
    /// The server held a request's field names to lowercase, and no field to
    /// these: every one of them reached the handler. HPACK carries any octet,
    /// so a field could carry what HTTP/1.1 could not even have framed, a CR LF
    /// in a value to a handler that writes it into an HTTP/1.1 request, say.
    /// Now the stream is reset with PROTOCOL_ERROR, the request reaches no
    /// handler, and the connection goes on.
    ///
    /// The HPACK decoder read a raw literal as ASCII, and made every octet above
    /// 0x7F a '?': a name with one came through as a legal name. It reads one
    /// character per octet now, as it always did a Huffman-coded literal.
    /// </summary>
    [TestFixture]
    public class ServerRequestFieldCharacterTests
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
        /// A field as a literal without indexing, raw or Huffman-coded.
        /// </summary>
        private static Byte[] Field(String Name, String Value, Boolean Huffman)

            => Huffman ? RawHeaderBlock.HuffmanLiteral(Name, Value)
                       : RawHeaderBlock.Literal       (Name, Value);

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
        /// Send a request on stream 1 whose header block is this one, then — as a
        /// client would that had not yet read a RST_STREAM — the rest of it, if
        /// <paramref name="Trailers"/> are given: a body, and the trailers, raw.
        /// Then a GET for /next on stream 3. Assert that stream 1 was reset with
        /// PROTOCOL_ERROR, that only the next request reached the handler, and
        /// that it was answered, the connection going on.
        /// </summary>
        private static async Task AssertResetAsync(Byte[] Block, Byte[]? Trailers = null)
        {

            var handler = new Handler();

            await using var peer = await PipedH2ServerConnection.StartAsync(handler.Buffered);

            await SendBlockAsync(peer, 1, Block, EndStream: Trailers is null);

            if (Trailers is not null)
            {
                await peer.SendAsync(HTTP2Frame.CreateData(1, "body"u8.ToArray())).WaitAsync(StepTimeout);
                await SendBlockAsync(peer, 1, Trailers, EndStream: true);
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

        #endregion


        #region FieldNameCharacter_ResetsTheStream(Octet, Huffman)

        /// <summary>
        /// One octet the rule leaves out, in the middle of a regular field's
        /// name, raw or Huffman-coded.
        /// </summary>
        [TestCase(0x00, false)]
        [TestCase(0x09, false)]
        [TestCase(0x0A, false)]
        [TestCase(0x0D, false)]
        [TestCase(0x20, false)]
        [TestCase(0x3A, false)]
        [TestCase(0x7F, false)]
        [TestCase(0x80, false)]
        [TestCase(0xE9, false)]
        [TestCase(0xFF, false)]
        [TestCase(0x00, true)]
        [TestCase(0x20, true)]
        [TestCase(0x80, true)]
        [TestCase(0xE9, true)]
        public Task FieldNameCharacter_ResetsTheStream(Int32 Octet, Boolean Huffman)

            => AssertResetAsync([.. RawHeaderBlock.Literals(Get()),
                                 .. Field($"x-{(Char) Octet}-id", "1", Huffman)]);

        #endregion

        #region FieldValueCharacter_ResetsTheStream(Name, Value)

        /// <summary>
        /// A value with NUL, LF or CR anywhere, or with SP or HTAB at either end,
        /// a regular field's or a pseudo-header field's: the rule is on every
        /// field value. A pseudo-header field named here takes the place of the
        /// GET's own.
        /// </summary>
        [TestCase("x-id",        "a\0b")]
        [TestCase("x-id",        "ab\0")]
        [TestCase("x-id",        "a\nb")]
        [TestCase("x-id",        "a\rb")]
        [TestCase("x-id",        "a\r\nx-injected: 1")]
        [TestCase("x-id",        " ab")]
        [TestCase("x-id",        "\tab")]
        [TestCase("x-id",        "ab ")]
        [TestCase("x-id",        "ab\t")]
        [TestCase("x-id",        " ")]
        [TestCase("te",          "trailers ")]
        [TestCase(":path",       " /")]
        [TestCase(":path",       "/\r\n")]
        [TestCase(":authority",  "localhost ")]
        [TestCase(":authority",  "local\0host")]
        [TestCase(":method",     "GET\t")]
        public Task FieldValueCharacter_ResetsTheStream(String Name, String Value)
        {

            var fields = Get();
            var index  = fields.FindIndex(field => field.Name == Name);

            if (index >= 0)
                fields[index] = (Name, Value);
            else
                fields.Add((Name, Value));

            return AssertResetAsync(RawHeaderBlock.Literals(fields));

        }

        #endregion

        #region FieldCharacterInTrailers_ResetsTheStream(Name, Value)

        /// <summary>
        /// The rules hold for trailers' fields as well: the request had not ended,
        /// and reaches no handler.
        /// </summary>
        [TestCase("x-checksum",   "abc\r")]
        [TestCase("x-checksum",   " abc")]
        [TestCase("x checksum",   "abc")]
        [TestCase("x-ch\u00E9ck", "abc")]
        public Task FieldCharacterInTrailers_ResetsTheStream(String Name, String Value)

            => AssertResetAsync(RawHeaderBlock.Literals(Get()),
                                RawHeaderBlock.Literals([("x-ok", "1"), (Name, Value)]));

        #endregion

        #region InnerWhitespace_AndOctetsAbove0x7F_InValues_Stand(Huffman)

        /// <summary>
        /// Whitespace inside a value, an empty value, and octets above 0x7F in a
        /// value — obs-text, which RFC 9110 still lets a value carry — are no
        /// reason to reset: the request reaches the handler with each value as it
        /// was sent, an octet as the character of the same number, raw or
        /// Huffman-coded. A raw literal used to make every octet above 0x7F a
        /// '?', where a Huffman one gave the octet.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task InnerWhitespace_AndOctetsAbove0x7F_InValues_Stand(Boolean Huffman)
        {

            var handler = new Handler();

            await using var peer = await PipedH2ServerConnection.StartAsync(handler.Buffered);

            (String Name, String Value)[] fields = [("x-a", "a b\tc"), ("x-b", "caf\u00E9 \u0080\u00FF"), ("x-c", "")];

            await SendBlockAsync(peer, 1, [.. RawHeaderBlock.Literals(Get()),
                                           .. fields.SelectMany(field => Field(field.Name, field.Value, Huffman))],
                                 EndStream: true);

            var response = await peer.TryResponseAsync(1, StepTimeout);

            Assert.Multiple(() =>
            {
                Assert.That(response?.Status,         Is.EqualTo("200"),  "status of the request");
                Assert.That(handler.Requests.Count,   Is.EqualTo(1),      "requests that reached the handler");
                if (handler.Requests.Count == 1)
                    Assert.That(handler.Requests[0].Where(field => field.Name.StartsWith("x-")),  Is.EqualTo(fields),  "the fields the handler was given");
                Assert.That(GoAways(peer),            Is.Empty,           "GOAWAY frames the server sent");
            });

        }

        #endregion

    }

}
