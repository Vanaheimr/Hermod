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

using org.GraphDefined.Vanaheimr.Hermod.HTTP3;
using org.GraphDefined.Vanaheimr.Hermod.HTTP3.Qpack;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.Messages;

[TestFixture]
public class Http3FrameTests
{
    [Test]
    public void BuildAndParse_DataFrame_RoundTrips()
    {
        byte[] frame = Http3Frames.Build(Http3FrameType.Data, [0x48, 0x69]); // "Hi"

        Assert.That(Http3Frames.TryReadAll(frame, out var frames, out int consumed), Is.True);
        Assert.That(consumed, Is.EqualTo(frame.Length));
        var parsed = Expect.Single(frames);
        Assert.That(parsed.Type, Is.EqualTo(Http3FrameType.Data));
        Assert.That(parsed.Payload.ToArray(), Is.EqualTo(new byte[] { 0x48, 0x69 }));
    }

    [Test]
    public void TryReadAll_ParsesMultipleFrames_AndReportsPartialTail()
    {
        // HEADERS(2 bytes) + DATA(3 bytes) + a started third frame (length 10, but only 1 byte present).
        byte[] buffer =
        [
            0x01, 0x02, 0xaa, 0xbb,              // HEADERS len=2
            0x00, 0x03, 0x01, 0x02, 0x03,        // DATA len=3
            0x00, 0x0a, 0xff,                    // DATA len=10 (incomplete)
        ];

        Assert.That(Http3Frames.TryReadAll(buffer, out var frames, out int consumed), Is.True);
        Assert.That(frames.Count, Is.EqualTo(2));
        Assert.That(frames[0].Type, Is.EqualTo(Http3FrameType.Headers));
        Assert.That(frames[1].Type, Is.EqualTo(Http3FrameType.Data));
        Assert.That(consumed, Is.EqualTo(9)); // the incomplete third frame stays put
    }
}

public class Http3RequestTests
{
    [Test]
    public void ToHeaderFields_ProducesPseudoHeadersInOrder()
    {
        var request = Http3Request.Get("example.com", "/index.html");
        List<HeaderField> fields = request.ToHeaderFields();

        Assert.That(fields[0], Is.EqualTo(new HeaderField(":method", "GET")));
        Assert.That(fields[1], Is.EqualTo(new HeaderField(":scheme", "https")));
        Assert.That(fields[2], Is.EqualTo(new HeaderField(":authority", "example.com")));
        Assert.That(fields[3], Is.EqualTo(new HeaderField(":path", "/index.html")));
    }

    [Test]
    public void RequestHeaders_SurviveQpackAndHttp3FrameRoundTrip()
    {
        // This is how the client builds the request: QPACK header block in a HEADERS frame.
        var request = Http3Request.Get("cloudflare-quic.com", "/");
        byte[] headerBlock = QpackEncoder.Encode(request.ToHeaderFields());
        byte[] frame = Http3Frames.Build(Http3FrameType.Headers, headerBlock);

        // Receiver side: parse the frame -> decode QPACK.
        Assert.That(Http3Frames.TryReadAll(frame, out var frames, out _), Is.True);
        Assert.That(frames[0].Type, Is.EqualTo(Http3FrameType.Headers));
        Assert.That(QpackDecoder.Decode(frames[0].Payload.Span, out var headers), Is.EqualTo(QpackResult.Ok));
        Assert.That(headers, Is.EqualTo(request.ToHeaderFields()));
    }

    [Test]
    public void ResponseHeaders_DecodeStatusAndContentType()
    {
        // A typical server HEADERS block (static table + literals), the way Cloudflare sends it.
        byte[] headerBlock = QpackEncoder.Encode(
        [
            new HeaderField(":status", "200"),
            new HeaderField("content-type", "text/html"),
            new HeaderField("server", "cloudflare"),
        ]);
        byte[] frame = Http3Frames.Build(Http3FrameType.Headers, headerBlock);

        Assert.That(Http3Frames.TryReadAll(frame, out var frames, out _), Is.True);
        Assert.That(QpackDecoder.Decode(frames[0].Payload.Span, out var headers), Is.EqualTo(QpackResult.Ok));

        Assert.That(headers, Does.Contain(new HeaderField(":status", "200")));
        Assert.That(headers, Does.Contain(new HeaderField("content-type", "text/html")));
    }
}
