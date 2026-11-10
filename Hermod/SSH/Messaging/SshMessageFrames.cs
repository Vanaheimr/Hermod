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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Hermod.Messaging;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// The kinds of frames of the message protocol over an SSH subsystem channel (see README.md beside
    /// this file). Kinds from 0x80 on that a side does not know are skipped; any other unknown kind is a
    /// protocol error.
    /// </summary>
    public enum SshMessageFrameType : Byte
    {

        /// <summary>
        /// Client to server, first: the subprotocols the client speaks, the largest message it takes, and
        /// whatever else the application asks for (JSON).
        /// </summary>
        Hello    = 0x01,

        /// <summary>
        /// Server to client, in answer to HELLO: the subprotocol chosen, the largest message the server
        /// takes, and whatever else the application tells (JSON).
        /// </summary>
        Welcome  = 0x02,

        /// <summary>
        /// Server to client, in answer to HELLO: why the client is not served (JSON). The server closes
        /// the channel then.
        /// </summary>
        Refused  = 0x03,

        /// <summary>
        /// A text message: UTF-8.
        /// </summary>
        Text     = 0x10,

        /// <summary>
        /// A binary message.
        /// </summary>
        Binary   = 0x11,

        /// <summary>
        /// Asks the other side for a PONG with the same payload.
        /// </summary>
        Ping     = 0x20,

        /// <summary>
        /// The answer to a PING, with its payload.
        /// </summary>
        Pong     = 0x21,

        /// <summary>
        /// The connection ends: a close code (uint16, RFC 6455's numbers) and a UTF-8 reason. Nothing is
        /// sent after it; a side that receives one answers with its own, where it has sent none yet.
        /// </summary>
        Close    = 0x30

    }


    /// <summary>
    /// A frame of the message protocol over an SSH subsystem channel.
    /// </summary>
    /// <param name="Type">The kind of frame.</param>
    /// <param name="Payload">What it carries.</param>
    public readonly record struct SshMessageFrame(SshMessageFrameType  Type,
                                                  Byte[]               Payload);


    /// <summary>
    /// The other side broke the message protocol: the connection is closed with <see cref="Code"/>.
    /// </summary>
    public sealed class SshMessageProtocolException(MessageCloseCode  Code,
                                                    String            Message)

        : SshWireException(Message)

    {

        /// <summary>
        /// The close code the connection ends with.
        /// </summary>
        public MessageCloseCode Code { get; } = Code;

    }


    /// <summary>
    /// The server refused this client: it answered HELLO with REFUSED.
    /// </summary>
    public sealed class SshMessageRefusedException(String     Code,
                                                   String?    Description,
                                                   TimeSpan?  RetryAfter   = null)

        : Exception($"The server refused the connection: {Code}{(Description is null ? "" : $" ({Description})")}{(RetryAfter is TimeSpan after ? $", try again in {after.TotalSeconds:0} s" : "")}.")

    {

        /// <summary>
        /// Why, as the server's code says it: "unsupported-subprotocol", "invalid-hello", or the application's own.
        /// </summary>
        public String     Code         { get; } = Code;

        /// <summary>
        /// Why, in words, where the server gave any.
        /// </summary>
        public String?    Description  { get; } = Description;

        /// <summary>
        /// After how long the server would take the client again, where it said: a refusal that is not
        /// final, such as <see cref="SshMessageRefusal.ConnectionExists"/>.
        /// </summary>
        public TimeSpan?  RetryAfter   { get; } = RetryAfter;

    }


    /// <summary>
    /// Reading and writing the frames of the message protocol over an SSH subsystem channel:
    /// <c>uint32 length</c> (big-endian, counting the kind and the payload), <c>byte kind</c>, payload.
    /// </summary>
    public static class SshMessageFraming
    {

        #region Constants

        /// <summary>
        /// The length and the kind before every payload.
        /// </summary>
        public const Int32   HeaderLength           = 5;

        /// <summary>
        /// The largest payload of any frame but TEXT and BINARY: HELLO, WELCOME, REFUSED, PING, PONG, CLOSE
        /// and the unknown kinds that are skipped.
        /// </summary>
        public const Int32   MaxControlPayload      = 64 * 1024;

        /// <summary>
        /// The largest message a side takes unless told otherwise: 64 MiB, as a WebSocket of Hermod takes
        /// (<see cref="WebSocket.WebSocketFrame.DefaultMaxPayloadSize"/>), so that a message that can go one
        /// way can go the other.
        /// </summary>
        public const UInt64  DefaultMaxMessageSize  = 64 * 1024 * 1024;

        /// <summary>
        /// The largest message a side can be told to take: what one array holds.
        /// </summary>
        public const UInt64  LargestMaxMessageSize  = Int32.MaxValue - HeaderLength;

        #endregion


        #region ReadAsync  (Input, MaxMessageSize, CancellationToken)

        /// <summary>
        /// Read one frame, or null where the stream ends between two frames. Its length is checked before
        /// anything is allocated for it: TEXT and BINARY against <paramref name="MaxMessageSize"/>, all
        /// other kinds against <see cref="MaxControlPayload"/>.
        /// </summary>
        /// <exception cref="SshMessageProtocolException">The frame is too long, has no kind, or the stream ends within it.</exception>
        public static async ValueTask<SshMessageFrame?> ReadAsync(Stream             Input,
                                                                  UInt64             MaxMessageSize,
                                                                  CancellationToken  CancellationToken   = default)
        {

            var header = new Byte[HeaderLength];
            var read   = await Input.ReadAtLeastAsync(header, HeaderLength, throwOnEndOfStream: false, CancellationToken).ConfigureAwait(false);

            if (read == 0)
                return null;

            if (read < HeaderLength)
                throw new SshMessageProtocolException(MessageCloseCode.ProtocolError, "The channel ended within a frame's header.");

            var length = BinaryPrimitives.ReadUInt32BigEndian(header);

            if (length == 0)
                throw new SshMessageProtocolException(MessageCloseCode.ProtocolError, "A frame without a kind.");

            var type          = (SshMessageFrameType) header[4];
            var payloadLength = (UInt64) length - 1;
            var limit         = type is SshMessageFrameType.Text or SshMessageFrameType.Binary
                                    ? Math.Min(MaxMessageSize, LargestMaxMessageSize)
                                    : MaxControlPayload;

            if (payloadLength > limit)
                throw new SshMessageProtocolException(type is SshMessageFrameType.Text or SshMessageFrameType.Binary
                                                          ? MessageCloseCode.MessageTooBig
                                                          : MessageCloseCode.ProtocolError,
                                                      $"A frame of kind 0x{(Byte) type:x2} with {payloadLength} bytes, more than the {limit} taken.");

            var payload = payloadLength == 0
                              ? []
                              : new Byte[payloadLength];

            if (payload.Length > 0 &&
                await Input.ReadAtLeastAsync(payload, payload.Length, throwOnEndOfStream: false, CancellationToken).ConfigureAwait(false) < payload.Length)
            {
                throw new SshMessageProtocolException(MessageCloseCode.ProtocolError, "The channel ended within a frame.");
            }

            return new SshMessageFrame(type, payload);

        }

        #endregion

        #region Encode     (Type, Payload)

        /// <summary>
        /// One frame, ready to be sent as a whole.
        /// </summary>
        public static Byte[] Encode(SshMessageFrameType   Type,
                                    ReadOnlySpan<Byte>    Payload)
        {

            var frame = new Byte[HeaderLength + Payload.Length];

            BinaryPrimitives.WriteUInt32BigEndian(frame, (UInt32) (Payload.Length + 1));
            frame[4] = (Byte) Type;
            Payload.CopyTo(frame.AsSpan(HeaderLength));

            return frame;

        }

        #endregion

        #region EncodeClose(Code, Reason) / DecodeClose(Payload)

        /// <summary>
        /// The payload of a CLOSE frame: the code, and as much of the reason as fits.
        /// </summary>
        public static Byte[] EncodeClose(MessageCloseCode  Code,
                                         String?           Reason)
        {

            var reason = Encoding.UTF8.GetBytes(Reason ?? "");

            // A reason is for a log; it is cut rather than let the frame be refused.
            if (reason.Length > MaxControlPayload - 2)
                reason = reason[..(MaxControlPayload - 2)];

            var payload = new Byte[2 + reason.Length];
            BinaryPrimitives.WriteUInt16BigEndian(payload, (UInt16) Code);
            reason.CopyTo(payload, 2);

            return payload;

        }

        /// <summary>
        /// The code and the reason of a CLOSE frame. An empty payload is a normal closure.
        /// </summary>
        /// <exception cref="SshMessageProtocolException">A payload of one byte.</exception>
        public static (MessageCloseCode Code, String? Reason) DecodeClose(Byte[] Payload)
        {

            if (Payload.Length == 0)
                return (MessageCloseCode.NormalClosure, null);

            if (Payload.Length == 1)
                throw new SshMessageProtocolException(MessageCloseCode.ProtocolError, "A CLOSE frame with half a close code.");

            var code   = (MessageCloseCode) BinaryPrimitives.ReadUInt16BigEndian(Payload);
            var reason = Payload.Length > 2
                             ? Encoding.UTF8.GetString(Payload, 2, Payload.Length - 2)
                             : null;

            return (code, reason);

        }

        #endregion

        #region (internal) ParseJSON(Payload, What)

        /// <summary>
        /// The JSON object a HELLO, WELCOME or REFUSED carries - read without turning date-like strings
        /// into dates, and no deeper than an application could mean.
        /// </summary>
        internal static JObject ParseJSON(Byte[]  Payload,
                                          String  What)
        {

            try
            {

                using var reader = new JsonTextReader(new StringReader(new UTF8Encoding(false, true).GetString(Payload))) {
                                       DateParseHandling   = DateParseHandling.None,
                                       FloatParseHandling  = FloatParseHandling.Decimal,
                                       MaxDepth            = 32
                                   };

                var json = JObject.Load(reader);

                // Nothing after the object.
                if (reader.Read())
                    throw new JsonReaderException("Content after the JSON object.");

                return json;

            }
            catch (Exception exception) when (exception is JsonException or DecoderFallbackException or InvalidCastException)
            {
                throw new SshMessageProtocolException(MessageCloseCode.ProtocolError, $"The {What} is not a JSON object: {exception.Message}");
            }

        }

        /// <summary>
        /// The bytes of a JSON object, as a frame carries them.
        /// </summary>
        internal static Byte[] ToBytes(JObject JSON)
            => Encoding.UTF8.GetBytes(JSON.ToString(Formatting.None));

        /// <summary>
        /// An optional positive integer of a HELLO or WELCOME.
        /// </summary>
        internal static UInt64? ReadSize(JObject  JSON,
                                         String   Name,
                                         String   What)
        {

            if (!JSON.TryGetValue(Name, out var token) || token.Type == JTokenType.Null)
                return null;

            UInt64 size = 0;

            try
            {
                if (token.Type == JTokenType.Integer)
                    size = token.Value<UInt64>();
            }
            catch (Exception exception) when (exception is OverflowException or InvalidCastException or FormatException)
            { }

            if (size == 0)
                throw new SshMessageProtocolException(MessageCloseCode.ProtocolError, $"The {What}'s '{Name}' is not a positive integer.");

            return size;

        }

        #endregion

    }


    /// <summary>
    /// What a client says first: the subprotocols it speaks, by preference, the largest message it takes,
    /// and whatever else its application asks for - all of it in <see cref="JSON"/>.
    /// </summary>
    public sealed class SshMessageHello
    {

        /// <summary>
        /// The most subprotocols a HELLO may offer.
        /// </summary>
        public const Int32 MaxSubprotocols = 32;

        /// <summary>
        /// The subprotocols the client speaks, by preference; empty where it asks for none.
        /// </summary>
        public IReadOnlyList<String>  Subprotocols    { get; }

        /// <summary>
        /// The largest message the client takes, where it said.
        /// </summary>
        public UInt64?                MaxMessageSize  { get; }

        /// <summary>
        /// The whole HELLO, for what the application reads from it.
        /// </summary>
        public JObject                JSON            { get; }

        private SshMessageHello(IReadOnlyList<String> Subprotocols, UInt64? MaxMessageSize, JObject JSON)
        {
            this.Subprotocols    = Subprotocols;
            this.MaxMessageSize  = MaxMessageSize;
            this.JSON            = JSON;
        }

        /// <summary>
        /// A HELLO with the given subprotocols and limit, and the application's own properties besides.
        /// </summary>
        /// <param name="Subprotocols">The subprotocols the client speaks, by preference.</param>
        /// <param name="MaxMessageSize">The largest message the client takes.</param>
        /// <param name="Properties">The application's own properties; "subprotocols" and "maxMessageSize" are set here.</param>
        public static SshMessageHello Create(IEnumerable<String>?  Subprotocols,
                                             UInt64?               MaxMessageSize,
                                             JObject?              Properties   = null)
        {

            var subprotocols = Subprotocols?.Distinct(StringComparer.Ordinal).ToArray() ?? [];
            var json         = (JObject?) Properties?.DeepClone() ?? [];

            if (subprotocols.Length > 0)
                json["subprotocols"]   = new JArray(subprotocols);
            else
                json.Remove("subprotocols");

            if (MaxMessageSize.HasValue)
                json["maxMessageSize"] = MaxMessageSize.Value;
            else
                json.Remove("maxMessageSize");

            return new SshMessageHello(subprotocols, MaxMessageSize, json);

        }

        /// <summary>
        /// Read a HELLO's payload.
        /// </summary>
        /// <exception cref="SshMessageProtocolException">Not a JSON object, or its subprotocols or limit are malformed.</exception>
        public static SshMessageHello Parse(Byte[] Payload)
        {

            var json          = SshMessageFraming.ParseJSON(Payload, "HELLO");
            var subprotocols  = new List<String>();

            if (json.TryGetValue("subprotocols", out var token) && token.Type != JTokenType.Null)
            {

                if (token is not JArray array || array.Count > MaxSubprotocols)
                    throw new SshMessageProtocolException(MessageCloseCode.ProtocolError, $"The HELLO's 'subprotocols' is not an array of at most {MaxSubprotocols} strings.");

                foreach (var item in array)
                {

                    if (item.Type != JTokenType.String || item.Value<String>() is not String subprotocol || subprotocol.Length == 0 || subprotocol.Length > 256)
                        throw new SshMessageProtocolException(MessageCloseCode.ProtocolError, "The HELLO names a subprotocol that is not a string of 1 to 256 characters.");

                    if (!subprotocols.Contains(subprotocol))
                        subprotocols.Add(subprotocol);

                }

            }

            return new SshMessageHello(subprotocols,
                                       SshMessageFraming.ReadSize(json, "maxMessageSize", "HELLO"),
                                       json);

        }

        /// <summary>
        /// The payload of this HELLO.
        /// </summary>
        public Byte[] ToBytes()
            => SshMessageFraming.ToBytes(JSON);

    }


    /// <summary>
    /// What a server answers a HELLO with when it serves the client: the subprotocol it chose, the largest
    /// message it takes, and whatever else its application tells - all of it in <see cref="JSON"/>.
    /// </summary>
    public sealed class SshMessageWelcome
    {

        /// <summary>
        /// The subprotocol the server chose, or null where none was asked for or none applies.
        /// </summary>
        public String?   Subprotocol     { get; }

        /// <summary>
        /// The largest message the server takes, where it said.
        /// </summary>
        public UInt64?   MaxMessageSize  { get; }

        /// <summary>
        /// The whole WELCOME, for what the application reads from it.
        /// </summary>
        public JObject   JSON            { get; }

        private SshMessageWelcome(String? Subprotocol, UInt64? MaxMessageSize, JObject JSON)
        {
            this.Subprotocol     = Subprotocol;
            this.MaxMessageSize  = MaxMessageSize;
            this.JSON            = JSON;
        }

        /// <summary>
        /// A WELCOME with the given subprotocol and limit, and the application's own properties besides.
        /// </summary>
        public static SshMessageWelcome Create(String?   Subprotocol,
                                               UInt64?   MaxMessageSize,
                                               JObject?  Properties   = null)
        {

            var json = (JObject?) Properties?.DeepClone() ?? [];

            if (Subprotocol is not null)
                json["subprotocol"]    = Subprotocol;
            else
                json.Remove("subprotocol");

            if (MaxMessageSize.HasValue)
                json["maxMessageSize"] = MaxMessageSize.Value;
            else
                json.Remove("maxMessageSize");

            return new SshMessageWelcome(Subprotocol, MaxMessageSize, json);

        }

        /// <summary>
        /// Read a WELCOME's payload.
        /// </summary>
        /// <exception cref="SshMessageProtocolException">Not a JSON object, or its subprotocol or limit are malformed.</exception>
        public static SshMessageWelcome Parse(Byte[] Payload)
        {

            var json        = SshMessageFraming.ParseJSON(Payload, "WELCOME");
            String? chosen  = null;

            if (json.TryGetValue("subprotocol", out var token) && token.Type != JTokenType.Null)
            {
                if (token.Type != JTokenType.String || token.Value<String>() is not String subprotocol || subprotocol.Length == 0)
                    throw new SshMessageProtocolException(MessageCloseCode.ProtocolError, "The WELCOME's 'subprotocol' is not a string.");
                chosen = subprotocol;
            }

            return new SshMessageWelcome(chosen,
                                         SshMessageFraming.ReadSize(json, "maxMessageSize", "WELCOME"),
                                         json);

        }

        /// <summary>
        /// The payload of this WELCOME.
        /// </summary>
        public Byte[] ToBytes()
            => SshMessageFraming.ToBytes(JSON);

    }


    /// <summary>
    /// What a server answers a HELLO with when it does not serve the client.
    /// </summary>
    public static class SshMessageRefusal
    {

        /// <summary>
        /// The client offered subprotocols, and the server speaks none of them.
        /// </summary>
        public const String UnsupportedSubprotocol  = "unsupported-subprotocol";

        /// <summary>
        /// The client's HELLO could not be read.
        /// </summary>
        public const String InvalidHello            = "invalid-hello";

        /// <summary>
        /// The client sent something else first.
        /// </summary>
        public const String HelloExpected           = "hello-expected";

        /// <summary>
        /// The client's identity has a live connection this one may not take over; it may try again after
        /// the refusal's "retryAfter" - the analogue of an HTTP 409 with Retry-After.
        /// </summary>
        public const String ConnectionExists        = "connection-exists";

        /// <summary>
        /// The longest wait a REFUSED may ask for: a day.
        /// </summary>
        public static readonly TimeSpan MaxRetryAfter = TimeSpan.FromDays(1);

        /// <summary>
        /// The payload of a REFUSED frame.
        /// </summary>
        /// <param name="Code">Why, as a code the client can act on.</param>
        /// <param name="Description">Why, in words.</param>
        /// <param name="RetryAfter">After how long the client may try again, in whole seconds; null for a
        /// refusal that is final.</param>
        public static Byte[] Encode(String     Code,
                                    String?    Description,
                                    TimeSpan?  RetryAfter   = null)
        {

            var json = new JObject { ["code"] = Code };

            if (Description is not null)
                json["description"] = Description;

            if (RetryAfter is TimeSpan after)
                json["retryAfter"]  = (UInt64) Math.Ceiling(Math.Clamp(after.TotalSeconds, 0, MaxRetryAfter.TotalSeconds));

            return SshMessageFraming.ToBytes(json);

        }

        /// <summary>
        /// The code, description and wait of a REFUSED frame. A "retryAfter" that is not a whole number of
        /// seconds from 0 to a day is read as none, and the refusal then counts as final.
        /// </summary>
        public static (String Code, String? Description, TimeSpan? RetryAfter) Decode(Byte[] Payload)
        {

            var json        = SshMessageFraming.ParseJSON(Payload, "REFUSED");
            var retryAfter  = json["retryAfter"] is JValue { Type: JTokenType.Integer } seconds &&
                              seconds.Value<Int64>() is var value && value >= 0 && value <= (Int64) MaxRetryAfter.TotalSeconds
                                  ? TimeSpan.FromSeconds(value)
                                  : (TimeSpan?) null;

            return (json["code"]?.Type        == JTokenType.String ? json["code"]!.Value<String>()!       : "refused",
                    json["description"]?.Type == JTokenType.String ? json["description"]!.Value<String>() : null,
                    retryAfter);

        }

    }

}
