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

using org.GraphDefined.Vanaheimr.Hermod.HTTP3.Qpack;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP3;

/// <summary>
/// An HTTP/3 request. The pseudo-headers (:method/:scheme/:authority/:path) are mandatory (RFC 9114 §4.3.1).
/// The optional body is sent as a series of DATA frames after the HEADERS frame (RFC 9114 §4.1).
/// </summary>
public sealed record Http3Request(string Method, string Scheme, string Authority, string Path)
{
    public IReadOnlyList<HeaderField> AdditionalHeaders { get; init; } = [];

    /// <summary>
    /// The request body (content, RFC 9114 §4.1 item 2). Empty = no DATA frame.
    /// </summary>
    public byte[] Body { get; init; } = [];

    /// <summary>
    /// Optional trailer section (RFC 9114 §4.1 item 3): sent as a final HEADERS frame after the
    /// content, or stored here on receipt. Trailers must carry NO pseudo-headers.
    /// </summary>
    public IReadOnlyList<HeaderField> Trailers { get; init; } = [];

    /// <summary>
    /// Optional priority (RFC 9218): sent as the <c>priority</c> header and, on the client, also
    /// applied to the request stream's send scheduler. <c>null</c> = default (u=3, non-incremental),
    /// no header is sent.
    /// </summary>
    public Http3Priority? Priority { get; init; }

    /// <summary>
    /// The <c>:protocol</c> pseudo-header of an Extended CONNECT (RFC 8441 §4 / RFC 9220),
    /// e.g. "websocket"; <c>null</c> for normal requests.
    /// </summary>
    public string? Protocol { get; init; }

    public static Http3Request Get(string authority, string path = "/", string scheme = "https")
        => new("GET", scheme, authority, path);

    /// <summary>
    /// Creates a POST request with a body and content type.
    /// </summary>
    public static Http3Request Post(string authority, string path, byte[] body,
                                    string contentType = "application/octet-stream", string scheme = "https")
        => new("POST", scheme, authority, path)
        {
            Body = body,
            AdditionalHeaders = [new HeaderField("content-type", contentType)],
        };

    /// <summary>
    /// Produces the header field list in the order HTTP/3 requires (pseudo-headers first).
    /// With a non-empty body, <c>content-length</c> is added (RFC 9110 §8.6 SHOULD; RFC 9114 §4.1.2:
    /// the value MUST equal the sum of the DATA frame lengths — we send exactly <see cref="Body"/>).
    /// </summary>
    public List<HeaderField> ToHeaderFields()
    {
        var fields = new List<HeaderField>
        {
            new(":method", Method),
            new(":scheme", Scheme),
            new(":authority", Authority),
            new(":path", Path),
        };
        fields.AddRange(AdditionalHeaders);
        if (Body.Length > 0 && !AdditionalHeaders.Any(h => h.Name == "content-length"))
            fields.Add(new HeaderField("content-length", Body.Length.ToString()));
        if (Priority is { } priority && !AdditionalHeaders.Any(h => h.Name == "priority"))
        {
            string value = priority.ToHeaderValue();
            if (value.Length > 0)
                fields.Add(new HeaderField("priority", value)); // RFC 9218 §5
        }
        return fields;
    }
}

/// <summary>
/// An interim response (1xx, RFC 9114 §4.1 / RFC 9110 §15.2) that precedes the final response —
/// e.g. 103 Early Hints. Interim responses carry neither content nor trailers.
/// </summary>
public sealed record Http3InterimResponse(int Status, IReadOnlyList<HeaderField> Headers);

/// <summary>
/// An HTTP/3 response: status, headers and body, optionally interim responses (1xx) before and a
/// trailer section after (RFC 9114 §4.1).
/// </summary>
public sealed class Http3Response
{
    public int Status { get; init; }
    public IReadOnlyList<HeaderField> Headers { get; init; } = [];
    public byte[] Body { get; init; } = [];

    /// <summary>
    /// Streaming response body — the alternative to the fully buffered <see cref="Body"/>. The
    /// server pulls chunks from the stream and emits them as DATA frames as the send window allows,
    /// so a large or open-ended body never has to sit in memory. Read to the end (0 bytes) marks the
    /// end of the body; the server disposes the stream afterwards. When set, <see cref="Body"/> is
    /// ignored.
    /// </summary>
    public Stream? BodyStream { get; init; }

    /// <summary>
    /// Interim responses (1xx) that preceded or should precede the final response —
    /// when sending, the server writes one separate HEADERS section per entry BEFORE the final one.
    /// </summary>
    public IReadOnlyList<Http3InterimResponse> InterimResponses { get; init; } = [];

    /// <summary>
    /// Optional trailer section (RFC 9114 §4.1 item 3): a final HEADERS frame after the content.
    /// </summary>
    public IReadOnlyList<HeaderField> Trailers { get; init; } = [];

    /// <summary>
    /// The body as UTF-8 text.
    /// </summary>
    public string BodyText => System.Text.Encoding.UTF8.GetString(Body);

    public string? GetHeader(string name)
    {
        foreach (HeaderField h in Headers)
            if (h.Name == name)
                return h.Value;
        return null;
    }
}
