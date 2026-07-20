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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP2
{
    using System.Buffers.Binary;
    using System.IO.Compression;
    using System.Text;

    /// <summary>
    /// RFC 6455 WebSocket framing (masking, opcodes, fragmentation, close
    /// handshake) layered on top of an HTTP/2 extended-CONNECT tunnel
    /// (RFC 8441) — there is no separate HTTP/1.1-style Upgrade handshake:
    /// RFC 8441's :protocol pseudo-header already established this stream is a
    /// WebSocket, so the very first bytes exchanged on the tunnel are WebSocket
    /// frames.
    ///
    /// Direction-aware via <see cref="WebSocketRole"/> (RFC 6455 Section 5.1):
    /// a client masks every frame it sends and requires the ones it receives to be
    /// unmasked; a server does the exact opposite. The masking direction is the
    /// only thing that differs between the two ends — everything else (opcodes,
    /// fragmentation, ping/pong, the close handshake) is identical.
    /// </summary>
    public sealed class WebSocketConnection
    {

        /// <summary>
        /// Hard ceiling on a single frame's declared payload length. RFC 6455
        /// itself sets no limit, but an unbounded 64-bit length field taken
        /// straight off the wire is a memory-exhaustion vector — bounded generously
        /// enough for any reasonable message, small enough to fail fast otherwise.
        /// </summary>
        private const long MaxFramePayloadLength = 16 * 1024 * 1024;   // 16 MiB

        private readonly IHTTP2Tunnel   tunnel;
        private readonly WebSocketRole  role;

        /// <summary>
        /// Whether the "permessage-deflate" extension (RFC 7692) was negotiated at
        /// the opening handshake. When true, Text/Binary message payloads are
        /// DEFLATE-compressed with the RSV1 bit set on the message's first frame.
        /// This connection always operates in *no-context-takeover* mode (each
        /// message is compressed independently, LZ77 window reset per message) —
        /// the handshake layer that flips this on is expected to have advertised
        /// <c>server_no_context_takeover; client_no_context_takeover</c>, which is
        /// what lets a fixed-window codec like <see cref="DeflateStream"/> handle
        /// each message on its own without carrying state across messages.
        /// </summary>
        private readonly bool           perMessageDeflate;

        /// <summary>Bytes read from the tunnel but not yet consumed by frame parsing.</summary>
        private byte[] buffer      = [];
        private int    bufferStart;

        private bool   closeSent;

        /// <summary>
        /// A strict UTF-8 codec that throws on any invalid byte sequence, rather
        /// than silently substituting U+FFFD. Used to enforce RFC 6455 Section 8.1
        /// (text message payloads MUST be valid UTF-8) and Section 7.1.6 (a close
        /// frame's reason MUST be valid UTF-8).
        /// </summary>
        private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);


        /// <param name="PerMessageDeflate">
        /// Whether the handshake negotiated "permessage-deflate" (RFC 7692). See
        /// <see cref="perMessageDeflate"/> — this connection always runs the
        /// extension in no-context-takeover mode.
        /// </param>
        public WebSocketConnection(IHTTP2Tunnel Tunnel, WebSocketRole Role = WebSocketRole.Server, bool PerMessageDeflate = false)
        {
            tunnel            = Tunnel;
            role              = Role;
            perMessageDeflate = PerMessageDeflate;
        }


        #region Receiving

        /// <summary>
        /// Receive the next complete application message, transparently:
        ///  - reassembling fragmented messages (a Text/Binary start frame with
        ///    FIN=0 followed by one or more Continuation frames, RFC 6455
        ///    Section 5.4),
        ///  - answering Ping with Pong without surfacing either to the caller,
        ///  - completing the close handshake on a Close frame (echoing it back
        ///    per Section 5.5.1) and on any protocol violation.
        /// Returns null once the connection is closed — either a normal close
        /// handshake or the underlying tunnel simply ending.
        /// </summary>
        public async Task<WebSocketMessage?> ReceiveAsync(CancellationToken CancellationToken)
        {

            WebSocketOpcode? fragmentOpcode    = null;
            List<byte>?      fragmentBuffer    = null;
            Decoder?         textDecoder       = null;   // strict UTF-8 state across a fragmented text message
            var              messageCompressed = false;  // RSV1 seen on this message's first frame (permessage-deflate)

            while (true)
            {

                RawFrame? frame;

                try
                {
                    frame = await ReadRawFrameAsync(CancellationToken);
                }
                catch (WebSocketProtocolException ex)
                {
                    await CloseAsync(1002, ex.Message, CancellationToken);
                    return null;
                }

                if (frame is null)
                    return null;   // Tunnel ended without a close handshake

                // RFC 7692, Section 6: RSV1 (the "compressed" bit) is only ever
                // valid on the FIRST frame of a data message. A control frame or a
                // continuation frame that carries it is a protocol error.
                if (frame.Rsv1 && frame.Opcode is not (WebSocketOpcode.Text or WebSocketOpcode.Binary))
                {
                    await CloseAsync(1002, "RSV1 set on a control or continuation frame", CancellationToken);
                    return null;
                }

                switch (frame.Opcode)
                {

                    case WebSocketOpcode.Ping:
                        await SendFrameAsync(WebSocketOpcode.Pong, frame.Payload, CancellationToken);
                        continue;

                    case WebSocketOpcode.Pong:
                        continue;   // Unsolicited pong — nothing to do

                    case WebSocketOpcode.Close:
                        await HandleCloseAsync(frame.Payload, CancellationToken);
                        return null;

                    case WebSocketOpcode.Text:
                    case WebSocketOpcode.Binary:

                        if (fragmentOpcode is not null)
                        {
                            await CloseAsync(1002, "Expected a continuation frame", CancellationToken);
                            return null;
                        }

                        if (frame.Fin)
                        {
                            // A complete, single-frame message.
                            var payload = frame.Payload;

                            // permessage-deflate: RSV1 ⇒ the payload is DEFLATE-
                            // compressed; inflate before anything else (a decode
                            // failure fails the connection, RFC 7692 Section 7.2.2).
                            if (frame.Rsv1)
                            {
                                var inflated = Inflate(payload);
                                if (inflated is null)
                                {
                                    await CloseAsync(1002, "permessage-deflate decode failed", CancellationToken);
                                    return null;
                                }
                                payload = inflated;
                            }

                            // A text payload MUST be valid UTF-8 (RFC 6455 Section 8.1),
                            // checked on the *decompressed* bytes.
                            if (frame.Opcode == WebSocketOpcode.Text && !IsValidUtf8(payload))
                            {
                                await CloseAsync(1007, "Invalid UTF-8 in text message", CancellationToken);
                                return null;
                            }
                            return new WebSocketMessage { Opcode = frame.Opcode, Payload = payload };
                        }

                        // Start of a fragmented message.
                        fragmentOpcode    = frame.Opcode;
                        fragmentBuffer    = [.. frame.Payload];
                        messageCompressed = frame.Rsv1;

                        // Uncompressed text is validated incrementally, per fragment,
                        // so an invalid byte fails the connection as soon as it
                        // arrives. A compressed message can only be validated after
                        // it's whole and inflated, so its UTF-8 check is deferred.
                        if (frame.Opcode == WebSocketOpcode.Text && !messageCompressed)
                        {
                            textDecoder = StrictUtf8.GetDecoder();
                            if (!FeedUtf8(textDecoder, frame.Payload, Flush: false))
                            {
                                await CloseAsync(1007, "Invalid UTF-8 in text message", CancellationToken);
                                return null;
                            }
                        }
                        continue;

                    case WebSocketOpcode.Continuation:

                        if (fragmentOpcode is null)
                        {
                            await CloseAsync(1002, "Unexpected continuation frame", CancellationToken);
                            return null;
                        }

                        fragmentBuffer!.AddRange(frame.Payload);

                        // Uncompressed text: validate this fragment's bytes against the
                        // running UTF-8 state; flush on the final fragment to catch a
                        // trailing partial sequence. (Compressed messages defer, below.)
                        if (textDecoder is not null && !FeedUtf8(textDecoder, frame.Payload, Flush: frame.Fin))
                        {
                            await CloseAsync(1007, "Invalid UTF-8 in text message", CancellationToken);
                            return null;
                        }

                        if (!frame.Fin)
                            continue;

                        var body = fragmentBuffer.ToArray();

                        if (messageCompressed)
                        {
                            // Inflate the whole reassembled message, then UTF-8-check
                            // the decompressed text.
                            var inflated = Inflate(body);
                            if (inflated is null)
                            {
                                await CloseAsync(1002, "permessage-deflate decode failed", CancellationToken);
                                return null;
                            }
                            body = inflated;

                            if (fragmentOpcode == WebSocketOpcode.Text && !IsValidUtf8(body))
                            {
                                await CloseAsync(1007, "Invalid UTF-8 in text message", CancellationToken);
                                return null;
                            }
                        }

                        return new WebSocketMessage { Opcode = fragmentOpcode.Value, Payload = body };

                    default:
                        await CloseAsync(1002, "Unknown opcode", CancellationToken);
                        return null;

                }

            }

        }

        #region permessage-deflate (RFC 7692)

        /// <summary>The 4-octet tail (an empty DEFLATE block) permessage-deflate strips when sending and re-appends when receiving (RFC 7692 Section 7.2).</summary>
        private static readonly byte[] DeflateTail = [0x00, 0x00, 0xFF, 0xFF];

        /// <summary>
        /// Compress a message payload with raw DEFLATE for permessage-deflate
        /// (RFC 7692 Section 7.2.1). <see cref="DeflateStream"/> emits raw DEFLATE
        /// (no zlib wrapper) and, on close, a final (BFINAL) block — valid under
        /// no-context-takeover, where each message stands alone. If the platform
        /// ever emits the <c>00 00 FF FF</c> sync-flush tail we strip it as the RFC
        /// requires; a receiver re-appends it before inflating either way.
        /// </summary>
        private static byte[] Deflate(byte[] Data)
        {

            using var output = new MemoryStream();

            using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
                deflate.Write(Data, 0, Data.Length);

            var bytes = output.ToArray();

            if (bytes.Length >= 4 &&
                bytes[^4] == 0x00 && bytes[^3] == 0x00 && bytes[^2] == 0xFF && bytes[^1] == 0xFF)
                bytes = bytes[..^4];

            return bytes;

        }

        /// <summary>
        /// Decompress a permessage-deflate message payload (RFC 7692 Section 7.2.2):
        /// re-append the <c>00 00 FF FF</c> tail the sender stripped, then raw-inflate.
        /// Returns null on any decode failure (a malformed compressed message).
        /// </summary>
        private static byte[]? Inflate(byte[] Compressed)
        {

            try
            {
                using var input = new MemoryStream(Compressed.Length + 4);
                input.Write(Compressed, 0, Compressed.Length);
                input.Write(DeflateTail, 0, DeflateTail.Length);
                input.Position = 0;

                using var deflate = new DeflateStream(input, CompressionMode.Decompress);
                using var output  = new MemoryStream();
                deflate.CopyTo(output);
                return output.ToArray();
            }
            catch (InvalidDataException)
            {
                return null;
            }

        }

        #endregion

        /// <summary>Whether the whole byte sequence decodes as valid UTF-8 (RFC 6455 Section 8.1).</summary>
        private static bool IsValidUtf8(byte[] Bytes)
        {
            try { StrictUtf8.GetCharCount(Bytes); return true; }
            catch (DecoderFallbackException) { return false; }
        }

        /// <summary>
        /// Feed one fragment into a stateful strict-UTF-8 decoder; returns false on
        /// an invalid sequence. <see cref="Decoder.Convert"/> (unlike GetCharCount)
        /// reliably retains the trailing bytes of an incomplete multi-byte character
        /// between calls, so a code point legitimately split across two frames
        /// validates cleanly; <paramref name="Flush"/> (set on the final fragment)
        /// then rejects a sequence left unfinished at the end. The decoded chars are
        /// discarded — we only care whether the bytes were well-formed. (UTF-8 never
        /// yields more UTF-16 chars than input bytes, so the buffer can't overflow.)
        /// </summary>
        private static bool FeedUtf8(Decoder Decoder, byte[] Bytes, bool Flush)
        {
            try
            {
                var chars = new char[Bytes.Length + 1];
                Decoder.Convert(Bytes, 0, Bytes.Length, chars, 0, chars.Length, Flush,
                                out _, out _, out _);
                return true;
            }
            catch (DecoderFallbackException) { return false; }
        }

        private async Task HandleCloseAsync(byte[] Payload, CancellationToken CancellationToken)
        {

            // RFC 6455, Section 5.5: a close frame carries either no payload, or a
            // 2-byte status code optionally followed by a UTF-8 reason. A single
            // leftover byte (a truncated status code) is a protocol error → 1002.
            if (Payload.Length == 1)
            {
                await CloseAsync(1002, "Malformed close frame (1-byte payload)", CancellationToken);
                return;
            }

            if (Payload.Length >= 2)
            {
                // Section 7.4.1: the status code must be one the RFC permits on the
                // wire; a reserved/undefined code (e.g. 1005, 1006, 1015, <1000,
                // 1012–2999, >4999) is a protocol error → 1002.
                var code = BinaryPrimitives.ReadUInt16BigEndian(Payload);
                if (!IsValidCloseCode(code))
                {
                    await CloseAsync(1002, "Invalid close status code", CancellationToken);
                    return;
                }

                // Section 7.1.6: the reason (everything after the code) must be
                // valid UTF-8 → else 1007.
                if (Payload.Length > 2 && !IsValidUtf8(Payload[2..]))
                {
                    await CloseAsync(1007, "Invalid UTF-8 in close reason", CancellationToken);
                    return;
                }
            }

            // A well-formed (or empty) close: Section 5.5.1 — echo it back before
            // closing (the code/reason the peer sent us is a valid reply).
            if (!closeSent)
                await SendFrameAsync(WebSocketOpcode.Close, Payload, CancellationToken);

        }

        /// <summary>
        /// Whether a close status code is valid to appear on the wire (RFC 6455
        /// Section 7.4.1): 1000–1003, 1007–1011, or the 3000–4999 registered/private
        /// range. Everything else — including 1004, the "no code"/"abnormal"/"TLS"
        /// sentinels 1005/1006/1015, the reserved 1012–2999 band, and anything below
        /// 1000 or above 4999 — must never be sent and is a protocol error if received.
        /// </summary>
        private static bool IsValidCloseCode(ushort Code)
            => Code is (>= 1000 and <= 1003) or (>= 1007 and <= 1011) or (>= 3000 and <= 4999);

        #endregion


        #region Sending

        public Task SendTextAsync(string Text, CancellationToken CancellationToken)
            => SendFrameAsync(WebSocketOpcode.Text, Encoding.UTF8.GetBytes(Text), CancellationToken);

        public Task SendBinaryAsync(byte[] Data, CancellationToken CancellationToken)
            => SendFrameAsync(WebSocketOpcode.Binary, Data, CancellationToken);

        /// <summary>
        /// Initiate (or reply to) the close handshake. Safe to call more than
        /// once — only the first call actually sends a frame.
        /// </summary>
        public Task CloseAsync(ushort Code, string Reason, CancellationToken CancellationToken)
        {

            if (closeSent)
                return Task.CompletedTask;

            var reasonBytes = Encoding.UTF8.GetBytes(Reason);
            var payload      = new byte[2 + reasonBytes.Length];
            BinaryPrimitives.WriteUInt16BigEndian(payload, Code);
            reasonBytes.CopyTo(payload, 2);

            return SendFrameAsync(WebSocketOpcode.Close, payload, CancellationToken);

        }

        /// <summary>
        /// Serialize and send a single, unfragmented frame (RFC 6455 Section 5.2).
        /// A client masks the payload with a random 4-byte key (Section 5.3) and
        /// sets the MASK bit; a server sends it unmasked. We never fragment our own
        /// output.
        /// </summary>
        private async Task SendFrameAsync(WebSocketOpcode Opcode, byte[] Payload, CancellationToken CancellationToken)
        {

            if (Opcode == WebSocketOpcode.Close)
                closeSent = true;

            // permessage-deflate (RFC 7692): compress data-message payloads and mark
            // the frame with RSV1. Control frames (Close/Ping/Pong) are never
            // compressed. We never fragment our own output, so RSV1 sits on the one
            // frame that carries the whole message.
            var compress = perMessageDeflate && Opcode is (WebSocketOpcode.Text or WebSocketOpcode.Binary);
            if (compress)
                Payload = Deflate(Payload);

            var mask     = role == WebSocketRole.Client;
            var maskFlag = mask ? 0x80 : 0x00;
            var rsv1Flag = compress ? 0x40 : 0x00;

            var header = new List<byte> { (byte) (0x80 | rsv1Flag | (byte) Opcode) };   // FIN=1 (+ RSV1)

            if (Payload.Length <= 125)
            {
                header.Add((byte) (maskFlag | Payload.Length));
            }
            else if (Payload.Length <= 65535)
            {
                header.Add((byte) (maskFlag | 126));
                var ext = new byte[2];
                BinaryPrimitives.WriteUInt16BigEndian(ext, (ushort) Payload.Length);
                header.AddRange(ext);
            }
            else
            {
                header.Add((byte) (maskFlag | 127));
                var ext = new byte[8];
                BinaryPrimitives.WriteUInt64BigEndian(ext, (ulong) Payload.Length);
                header.AddRange(ext);
            }

            byte[] body;

            if (mask)
            {
                var key = new byte[4];
                System.Security.Cryptography.RandomNumberGenerator.Fill(key);
                header.AddRange(key);

                body = new byte[Payload.Length];
                for (var i = 0; i < Payload.Length; i++)
                    body[i] = (byte) (Payload[i] ^ key[i % 4]);
            }
            else
                body = Payload;

            var frameBytes = new byte[header.Count + body.Length];
            header.CopyTo(frameBytes);
            body.CopyTo(frameBytes, header.Count);

            await tunnel.WriteAsync(frameBytes, CancellationToken);

        }

        #endregion


        #region Raw frame parsing

        private sealed record RawFrame(bool Fin, bool Rsv1, WebSocketOpcode Opcode, byte[] Payload);

        /// <summary>
        /// Read and unmask a single WebSocket frame (RFC 6455 Section 5.2).
        /// Returns null if the tunnel ended before a complete frame arrived.
        /// </summary>
        private async Task<RawFrame?> ReadRawFrameAsync(CancellationToken CancellationToken)
        {

            var header = await ReadExactAsync(2, CancellationToken);
            if (header is null)
                return null;

            var fin    = (header[0] & 0x80) != 0;
            var rsv1   = (header[0] & 0x40) != 0;
            var rsv23  =  header[0] & 0x30;
            var opcode = (WebSocketOpcode) (header[0] & 0x0F);
            var masked = (header[1] & 0x80) != 0;
            var len7   =  header[1] & 0x7F;

            // RSV2/RSV3 are never valid (no extension defines them here). RSV1 is
            // valid only when permessage-deflate (RFC 7692) was negotiated; where it
            // may appear (first data frame only) is checked in ReceiveAsync.
            if (rsv23 != 0)
                throw new WebSocketProtocolException("Reserved bits RSV2/RSV3 must be 0");
            if (rsv1 && !perMessageDeflate)
                throw new WebSocketProtocolException("RSV1 set but permessage-deflate not negotiated");

            // RFC 6455, Section 5.1: a server MUST close on an unmasked client frame;
            // a client MUST close on a masked server frame.
            if (role == WebSocketRole.Server && !masked)
                throw new WebSocketProtocolException("Client frames must be masked");
            if (role == WebSocketRole.Client && masked)
                throw new WebSocketProtocolException("Server frames must not be masked");

            long payloadLength = len7;

            if (len7 == 126)
            {
                var ext = await ReadExactAsync(2, CancellationToken);
                if (ext is null) return null;
                payloadLength = BinaryPrimitives.ReadUInt16BigEndian(ext);
            }
            else if (len7 == 127)
            {
                var ext = await ReadExactAsync(8, CancellationToken);
                if (ext is null) return null;
                payloadLength = (long) BinaryPrimitives.ReadUInt64BigEndian(ext);

                if (payloadLength < 0)
                    throw new WebSocketProtocolException("Payload length must not have the high bit set");
            }

            if (payloadLength > MaxFramePayloadLength)
                throw new WebSocketProtocolException($"Frame payload too large ({payloadLength} bytes)");

            // RFC 6455, Section 5.5: control frames must not be fragmented and
            // are capped at 125 bytes of payload.
            if (opcode is WebSocketOpcode.Close or WebSocketOpcode.Ping or WebSocketOpcode.Pong)
            {
                if (!fin)
                    throw new WebSocketProtocolException("Control frames must not be fragmented");

                if (payloadLength > 125)
                    throw new WebSocketProtocolException("Control frame payload must be <= 125 bytes");
            }

            byte[]? maskingKey = null;
            if (masked)
            {
                maskingKey = await ReadExactAsync(4, CancellationToken);
                if (maskingKey is null) return null;
            }

            var rawPayload = payloadLength > 0
                                 ? await ReadExactAsync((int) payloadLength, CancellationToken)
                                 : [];
            if (rawPayload is null) return null;

            byte[] payload;

            if (masked)
            {
                // RFC 6455, Section 5.3: unmask by XOR-ing each payload byte with the
                // masking key, cycling through its 4 bytes.
                payload = new byte[rawPayload.Length];
                for (var i = 0; i < payload.Length; i++)
                    payload[i] = (byte) (rawPayload[i] ^ maskingKey![i % 4]);
            }
            else
                payload = rawPayload;

            return new RawFrame(fin, rsv1, opcode, payload);

        }

        /// <summary>
        /// Read exactly Count bytes off the tunnel, buffering across multiple
        /// underlying reads — HTTP/2 DATA frame boundaries have no relationship
        /// to WebSocket frame boundaries, so a single tunnel chunk might contain
        /// less than one WS frame header, or several whole WS frames at once.
        /// Returns null if the tunnel ends before Count bytes are available.
        /// </summary>
        private async Task<byte[]?> ReadExactAsync(int Count, CancellationToken CancellationToken)
        {

            while (buffer.Length - bufferStart < Count)
            {

                var chunk = await tunnel.ReadAsync(CancellationToken);

                if (chunk is null)
                    return null;

                if (bufferStart > 0)
                {
                    buffer      = buffer[bufferStart..];
                    bufferStart = 0;
                }

                var combined = new byte[buffer.Length + chunk.Length];
                buffer.CopyTo(combined, 0);
                chunk.CopyTo(combined, buffer.Length);
                buffer = combined;

            }

            var result = buffer[bufferStart..(bufferStart + Count)];
            bufferStart += Count;

            return result;

        }

        #endregion

    }

}
