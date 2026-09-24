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
using System.IO.Compression;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// Undoing a content coding on the way out of the body *stream*, rather than
    /// on a byte array that is already in memory (RFC 9110, Section 8.4).
    ///
    /// The difference is not an optimisation. A chunked, close-delimited or
    /// event-stream body never is a byte array, so for those the array-shaped
    /// <see cref="AHTTPPDU.DecodeBody"/> has nothing to work on at all.
    /// </summary>
    [TestFixture]
    public class ContentCodingStreamTests
    {

        #region (private) CountingStream

        /// <summary>
        /// A read-only stream that remembers how much of its source was actually
        /// consumed — the only way to tell a streaming decoder from one that
        /// quietly buffers everything first.
        /// </summary>
        private sealed class CountingStream(Byte[] Data) : Stream
        {

            private readonly MemoryStream inner = new (Data);

            public Int64 Consumed { get; private set; }

            public override Boolean  CanRead  => true;
            public override Boolean  CanSeek  => false;
            public override Boolean  CanWrite => false;
            public override Int64    Length   => throw new NotSupportedException();

            public override Int64 Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override Int32 Read(Byte[] Buffer, Int32 Offset, Int32 Count)
            {
                var read   = inner.Read(Buffer, Offset, Count);
                Consumed  += read;
                return read;
            }

            public override async ValueTask<Int32> ReadAsync(Memory<Byte>       Buffer,
                                                             CancellationToken  CancellationToken = default)
            {
                var read   = await inner.ReadAsync(Buffer, CancellationToken);
                Consumed  += read;
                return read;
            }

            public override void  Flush()                                    { }
            public override void  Write(Byte[] B, Int32 O, Int32 C)          => throw new NotSupportedException();
            public override Int64 Seek (Int64  O, SeekOrigin Origin)         => throw new NotSupportedException();
            public override void  SetLength(Int64 Value)                     => throw new NotSupportedException();

        }

        #endregion

        #region (private) Encode      (Text/Data, Coding)

        private static Byte[] Encode(String Text, String Coding)
            => HTTPContentCoding.Encode(Encoding.UTF8.GetBytes(Text), Coding);

        private static Byte[] Encode(Byte[] Data, String Coding)
            => HTTPContentCoding.Encode(Data, Coding);

        #endregion

        #region (private) Response    (ContentEncoding, Body, DeclareContentLength = true)

        /// <summary>
        /// A response whose body is a stream rather than an array — which is the
        /// whole point here, so it is built through the stream-taking overload.
        /// </summary>
        private static HTTPResponse Response(String   ContentEncoding,
                                             Byte[]   Body,
                                             Boolean  DeclareContentLength = true)

            => Response(
                   ContentEncoding,
                   new MemoryStream(Body),
                   DeclareContentLength ? (UInt64) Body.Length : null
               );

        private static HTTPResponse Response(String   ContentEncoding,
                                             Stream   Body,
                                             UInt64?  ContentLength)

            => HTTPResponse.Parse(
                   "HTTP/1.1 200 OK\r\n" +
                   "Content-Type: text/plain\r\n" +
                   $"Content-Encoding: {ContentEncoding}\r\n" +
                   (ContentLength.HasValue ? $"Content-Length: {ContentLength.Value}\r\n" : "") +
                   "\r\n",
                   Body
               );

        #endregion

        #region (private) Chunked     (Payload, Trailer = null)

        /// <summary>
        /// The wire form of a chunked body: one chunk with everything in it, the
        /// terminating zero chunk, and optionally one trailer field.
        /// </summary>
        private static Byte[] Chunked(Byte[]   Payload,
                                      String?  Trailer   = null)
        {

            var stream = new MemoryStream();

            void Write(String Text)
            {
                var bytes = Encoding.ASCII.GetBytes(Text);
                stream.Write(bytes, 0, bytes.Length);
            }

            Write($"{Payload.Length:x}\r\n");
            stream.Write(Payload, 0, Payload.Length);
            Write("\r\n0\r\n");
            Write(Trailer is not null ? $"{Trailer}\r\n\r\n" : "\r\n");

            return stream.ToArray();

        }

        #endregion

        #region (private) GZipOfNothing(Padding)

        /// <summary>
        /// A gzip member roughly <paramref name="Padding"/> bytes long that decodes
        /// to zero bytes: a run of empty stored deflate blocks (RFC 1951,
        /// Section 3.2.4 — five bytes each, no output).
        ///
        /// No compressor produces this. An attacker writes it out in a dozen lines,
        /// which is exactly the point: it is the shape that makes an *intermediate*
        /// decoding step dangerous while the final output stays tiny.
        /// </summary>
        private static Byte[] GZipOfNothing(Int32 Padding)
        {

            var output = new MemoryStream();

            // RFC 1952 member header: magic, method deflate, no flags, no mtime.
            output.Write([ 0x1f, 0x8b, 0x08, 0x00, 0, 0, 0, 0, 0x00, 0xFF ]);

            // BFINAL = 0, BTYPE = 00 (stored), then LEN = 0 and its complement.
            for (var i = 0; i < Padding / 5; i++)
                output.Write([ 0x00, 0x00, 0x00, 0xFF, 0xFF ]);

            // The same, with BFINAL = 1, so the stream actually ends.
            output.Write([ 0x01, 0x00, 0x00, 0xFF, 0xFF ]);

            // CRC32 and ISIZE of an empty output.
            output.Write([ 0, 0, 0, 0, 0, 0, 0, 0 ]);

            return output.ToArray();

        }

        #endregion


        #region AGzippedStreamDecodesToTheRepresentation()

        [Test]
        public void AGzippedStreamDecodesToTheRepresentation()
        {

            var text      = "Hello from a body that travelled compressed!";
            var response  = Response("gzip", Encode(text, "gzip"));

            Assert.That(response.TryDecodeBodyStream(out var error), Is.True, error);

            Assert.Multiple(() => {

                Assert.That(response.HTTPBodyAsUTF8String,    Is.EqualTo(text));

                // The two field lines that stopped being true of what the caller
                // now reads are gone from the parsed view.
                Assert.That(response.ContentEncoding,         Is.Empty);
                Assert.That(response.ContentLength,           Is.Null);
                Assert.That(response.DecodedContentEncoding,  Is.EqualTo("gzip"));

            });

        }

        #endregion

        #region TheRawHeaderStillRecordsWhatArrived()

        /// <summary>
        /// The parsed view describes the message as the application now sees it;
        /// the raw header is the record of what came off the socket. They are
        /// deliberately allowed to disagree here, so the disagreement is pinned
        /// rather than discovered by somebody grepping a log.
        /// </summary>
        [Test]
        public void TheRawHeaderStillRecordsWhatArrived()
        {

            var response = Response("gzip", Encode("anything", "gzip"));

            Assert.That(response.TryDecodeBodyStream(out var error), Is.True, error);

            Assert.Multiple(() => {
                Assert.That(response.RawHTTPHeader.Contains("Content-Encoding: gzip"), Is.True, response.RawHTTPHeader);
                Assert.That(response.ContentEncoding,                                  Is.Empty);
            });

        }

        #endregion

        #region ContentLengthCountedTheEncodedOctetsAndMustGo()

        /// <summary>
        /// Not a tidiness question. The buffering loop stops reading at
        /// Content-Length, so a body that shrank on the wire would be cut off at
        /// the compressed size if the field stayed behind — a silently short body,
        /// which is the worst failure mode on offer.
        /// </summary>
        [Test]
        public void ContentLengthCountedTheEncodedOctetsAndMustGo()
        {

            var text      = new String('a', 64 * 1024);
            var encoded   = Encode(text, "gzip");

            Assert.That(encoded.Length, Is.LessThan(text.Length / 10),
                        "the fixture proves nothing unless the body really did shrink");

            var response  = Response("gzip", encoded);

            Assert.That(response.TryDecodeBodyStream(out var error), Is.True, error);
            Assert.That(response.HTTPBody?.Length,  Is.EqualTo(text.Length));

        }

        #endregion

        #region CodingsAreUndoneInReverseOrder()

        /// <summary>
        /// "Content-Encoding: br, gzip" means Brotli first and gzip on top of it,
        /// so the wire has to be un-gzipped before it can be un-Brotli'd. Invisible
        /// with a single coding, which is why it is worth a test and not a comment.
        /// </summary>
        [Test]
        public void CodingsAreUndoneInReverseOrder()
        {

            var text      = "First Brotli, then gzip on top of it.";
            var response  = Response("br, gzip", Encode(Encode(text, "br"), "gzip"));

            Assert.That(response.TryDecodeBodyStream(out var error), Is.True, error);

            Assert.Multiple(() => {
                Assert.That(response.HTTPBodyAsUTF8String,    Is.EqualTo(text));
                Assert.That(response.DecodedContentEncoding,  Is.EqualTo("br, gzip"));
            });

        }

        #endregion

        #region TheOppositeOrderIsRejected()

        /// <summary>
        /// The counterpart of the test above: a body encoded the other way round
        /// must not decode, or the ordering would not be doing any work.
        ///
        /// It is detectable because gzip carries a magic number and complains
        /// within two bytes. The reverse pairing is worth knowing about and is not
        /// asserted here: Brotli, handed a gzip member, reports invalid data too —
        /// but as an InvalidOperationException, which is why
        /// <see cref="ContentDecodingStream"/> translates it.
        /// </summary>
        [Test]
        public void TheOppositeOrderIsRejected()
        {

            var response = Response("br, gzip", Encode(Encode("payload", "gzip"), "br"));

            Assert.That(response.TryDecodeBodyStream(out var error), Is.True, error);

            Assert.Throws<InvalidDataException>(() => { var _ = response.HTTPBody; });

        }

        #endregion

        #region BothFormsOfDeflateDecode(ZLibWrapped)

        /// <summary>
        /// RFC 9110 says "deflate" is zlib (RFC 1950); a good part of the web sends
        /// raw deflate (RFC 1951). Both have to work, and for a stream the choice
        /// can only be made once the first two bytes have arrived.
        /// </summary>
        [Test]
        [TestCase(true,  TestName = "BothFormsOfDeflateDecode(zlib-wrapped)")]
        [TestCase(false, TestName = "BothFormsOfDeflateDecode(raw)")]
        public void BothFormsOfDeflateDecode(Boolean ZLibWrapped)
        {

            var text    = "deflate is the one coding the wire disagrees about";
            var output  = new MemoryStream();

            using (Stream compressor = ZLibWrapped
                                           ? new ZLibStream   (output, CompressionLevel.Optimal, leaveOpen: true)
                                           : new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                compressor.Write(bytes, 0, bytes.Length);
            }

            var response = Response("deflate", output.ToArray());

            Assert.That(response.TryDecodeBodyStream(out var error), Is.True, error);
            Assert.That(response.HTTPBodyAsUTF8String, Is.EqualTo(text));

        }

        #endregion

        #region AnUnknownCodingLeavesEverythingAlone()

        [Test]
        public void AnUnknownCodingLeavesEverythingAlone()
        {

            var body      = "not really compressed".ToUTF8Bytes();
            var response  = Response("gzip, exotic", body);
            var before    = response.HTTPBodyStream;

            Assert.That(response.TryDecodeBodyStream(out var error), Is.False);

            Assert.Multiple(() => {
                Assert.That(error,                            Does.Contain("exotic"));
                Assert.That(response.HTTPBodyStream,          Is.SameAs(before));
                Assert.That(response.ContentEncoding,         Is.EqualTo(new[] { "gzip", "exotic" }));
                Assert.That(response.ContentLength,           Is.EqualTo((UInt64) body.Length));
                Assert.That(response.DecodedContentEncoding,  Is.Null);
            });

        }

        #endregion

        #region AMessageWithoutACodingIsAlreadyTheRepresentation()

        [Test]
        public void AMessageWithoutACodingIsAlreadyTheRepresentation()
        {

            var response = HTTPResponse.Parse(
                               "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 5\r\n\r\n",
                               new MemoryStream("plain".ToUTF8Bytes())
                           );

            var before = response.HTTPBodyStream;

            Assert.That(response.TryDecodeBodyStream(out var error), Is.True, error);

            Assert.Multiple(() => {
                Assert.That(response.HTTPBodyStream,          Is.SameAs(before), "nothing to undo means nothing to wrap");
                Assert.That(response.ContentLength,           Is.EqualTo((UInt64) 5));
                Assert.That(response.DecodedContentEncoding,  Is.Null);
                Assert.That(response.HTTPBodyAsUTF8String,    Is.EqualTo("plain"));
            });

        }

        #endregion

        #region ADecompressionBombIsStoppedWhileItExpands()

        /// <summary>
        /// 16 MiB of zeros are a few kilobytes of gzip. The ceiling has to bite
        /// during decompression, not after — by which time the memory is gone.
        /// </summary>
        [Test]
        public void ADecompressionBombIsStoppedWhileItExpands()
        {

            var bomb      = Encode(new Byte[16 * 1024 * 1024], "gzip");

            Assert.That(bomb.Length, Is.LessThan(64 * 1024));

            var response  = Response("gzip", bomb);

            Assert.That(response.TryDecodeBodyStream(out var error, MaxDecodedSize: 1024), Is.True, error);

            Assert.Throws<HTTPBodyTooLargeException>(() => { var _ = response.HTTPBody; });

        }

        #endregion

        #region AnIntermediateStepIsBoundedToo()

        /// <summary>
        /// With a well-formed chain the last step produces the most, so a ceiling
        /// on the final output would seem to be enough. Nothing obliges the peer to
        /// send a well-formed chain.
        ///
        /// Here "Content-Encoding: gzip, gzip" is undone in two steps. The first
        /// yields four megabytes of gzip padding; the second decodes that padding to
        /// *nothing*. A ceiling that only watched the final output would see zero
        /// bytes and be satisfied, having just moved four megabytes — scale that by
        /// the ratio the outer coding achieves and the number stops being four.
        ///
        /// So the bound sits on every step, and the failure is the size limit rather
        /// than a successful read of an empty body.
        /// </summary>
        [Test]
        public void AnIntermediateStepIsBoundedToo()
        {

            var padding   = GZipOfNothing(4 * 1024 * 1024);
            var wire      = Encode(padding, "gzip");

            Assert.Multiple(() => {
                Assert.That(padding.Length,  Is.GreaterThan(4 * 1024 * 1024));
                Assert.That(wire.Length,     Is.LessThan(64 * 1024), "the outer coding hides the padding");
            });

            var response  = Response("gzip, gzip", wire);

            Assert.That(response.TryDecodeBodyStream(out var error, MaxDecodedSize: 1024 * 1024), Is.True, error);

            Assert.Throws<HTTPBodyTooLargeException>(() => { var _ = response.HTTPBody; });

        }

        #endregion

        #region AChunkedBodyDecodesAndKeepsItsTrailers()

        /// <summary>
        /// The case the array-shaped decoder cannot reach, and the one that shows
        /// why: the body arrives chunked, so there is no Content-Length and no
        /// array — and the trailer fields arrive *after* it. Wrapping the stream in
        /// a decoder hides that it was a chunked stream, so what it was has to be
        /// remembered before the wrapping happens.
        /// </summary>
        [Test]
        public void AChunkedBodyDecodesAndKeepsItsTrailers()
        {

            var text      = "chunked and compressed, which is the normal case on the web";

            var wire      = new MemoryStream(
                                Chunked(
                                    Encode(text, "gzip"),
                                    "X-Checked-By: the trailer"
                                )
                            );

            var response  = HTTPResponse.Parse(
                                "HTTP/1.1 200 OK\r\n" +
                                "Content-Type: text/plain\r\n" +
                                "Content-Encoding: gzip\r\n" +
                                "Transfer-Encoding: chunked\r\n" +
                                "Trailer: X-Checked-By\r\n" +
                                "\r\n",
                                new ChunkedTransferEncodingStream(wire, LeaveInnerStreamOpen: true)
                            );

            Assert.That(response.TryDecodeBodyStream(out var error), Is.True, error);

            Assert.Multiple(() => {
                Assert.That(response.HTTPBodyAsUTF8String,             Is.EqualTo(text));
                Assert.That(response.TrailingHeaders["X-Checked-By"],  Is.EqualTo("the trailer"));
            });

        }

        #endregion

        #region TheDecodedStreamIsRead__Not__BufferedFirst()

        /// <summary>
        /// Streaming has to mean streaming. A consumer reading the first few bytes
        /// of the representation must not have pulled the entire encoded body
        /// through the socket to get them — which is precisely what would happen if
        /// this were buffer-then-decode wearing a stream's clothes.
        /// </summary>
        [Test]
        public async Task TheDecodedStreamIsRead__Not__BufferedFirst()
        {

            var text      = String.Join("\n", Enumerable.Range(0, 200_000).Select(i => $"line {i}"));
            var encoded   = Encode(text, "gzip");
            var source    = new CountingStream(encoded);

            Assert.That(encoded.Length, Is.GreaterThan(256 * 1024),
                        "the encoded body has to be bigger than any single buffer for this to mean anything");

            var response  = Response("gzip", source, ContentLength: null);

            Assert.That(response.TryDecodeBodyStream(out var error), Is.True, error);

            var buffer    = new Byte[64];
            var read      = await response.HTTPBodyStream!.ReadAsync(buffer);

            Assert.Multiple(() => {
                Assert.That(Encoding.UTF8.GetString(buffer, 0, read),  Does.StartWith("line 0\nline 1\n"));
                Assert.That(source.Consumed,                           Is.LessThan(encoded.Length));
            });

        }

        #endregion

    }

}
