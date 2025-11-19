/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
using System.Diagnostics.CodeAnalysis;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    public enum ByteOrder
    {

        /// <summary>
        /// Specifies Little-endian.
        /// </summary>
        Little,

        /// <summary>
        /// Specifies Big-endian.
        /// </summary>
        Big

    }


    public static class WebSocketFrameExtensions
    {

        public static Boolean IsHostOrder(this ByteOrder order)
        {
            // true:  !(true ^ true)  or !(false ^ false)
            // false: !(true ^ false) or !(false ^ true)
            return !(BitConverter.IsLittleEndian ^ (order == ByteOrder.Little));
        }

        internal static Byte[] InternalToByteArray(this ushort value, ByteOrder order)
        {

            var ret = BitConverter.GetBytes(value);

            if (!order.IsHostOrder())
                Array.Reverse(ret);

            return ret;

        }

        internal static Byte[] InternalToByteArray(this ulong value, ByteOrder order)
        {

            var ret = BitConverter.GetBytes(value);

            if (!order.IsHostOrder())
                Array.Reverse(ret);

            return ret;

        }

        public static Boolean IsControl(this WebSocketFrame.Opcodes Opcode)
            => Opcode >= WebSocketFrame.Opcodes.Close;

        public static Boolean IsData   (this WebSocketFrame.Opcodes Opcode)
            => Opcode == WebSocketFrame.Opcodes.Text ||
               Opcode == WebSocketFrame.Opcodes.Binary;

        public static Boolean IsControl(this WebSocketFrame Frame)
            => Frame.IsControl();

        public static Boolean IsData   (this WebSocketFrame Frame)
            => Frame.IsData();


        public static WebSocketFrame.ClosingStatusCode GetClosingStatusCode(this WebSocketFrame Frame)
        {

            if (Frame.Opcode == WebSocketFrame.Opcodes.Close &&
                Frame.Payload.Length >= 2)
            {

                return BitConverter.IsLittleEndian
                           ? (WebSocketFrame.ClosingStatusCode) BitConverter.ToUInt16([ Frame.Payload[1], Frame.Payload[0] ], 0)
                           : (WebSocketFrame.ClosingStatusCode) BitConverter.ToUInt16(Frame.Payload, 0);

            }

            // https://www.rfc-editor.org/rfc/rfc6455#section-7.4.1
            // 1005 is a reserved value and MUST NOT be set as a status code in a
            // Close control frame by an endpoint.  It is designated for use in
            // applications expecting a status code to indicate that no status
            // code was actually present.
            return WebSocketFrame.ClosingStatusCode.NoStatusReceived;

        }

        public static String? GetClosingReason(this WebSocketFrame Frame)
        {

            if (Frame.Opcode == WebSocketFrame.Opcodes.Close &&
                Frame.Payload.Length > 2)
            {

                // https://www.rfc-editor.org/rfc/rfc6455#section-7.4.1
                //    0- 999: Not used
                // 1000-2999: Reserved for definition by the WebSocket specification and IETF
                // 3000-3999: Available for use by libraries and frameworks
                // 4000-4999: Available for use by applications
                return Encoding.UTF8.GetString(
                           Frame.Payload,
                           2,
                           Frame.Payload.Length - 2
                       );

            }

            return null;

        }


    }


    /// <summary>
    /// A web socket frame.
    /// </summary>
    public class WebSocketFrame
    {

        #region (enum) Opcodes

        /// <summary>
        /// Web socket frame opcodes.
        /// 
        /// All control frames MUST have a payload length of 125 bytes or less
        /// and MUST NOT be fragmented.
        /// 
        /// https://www.rfc-editor.org/rfc/rfc6455#section-5.2
        /// </summary>
        public enum Opcodes : byte
        {

            /// <summary>
            /// Equivalent to numeric value 0. Indicates continuation frame.
            /// </summary>
            Continuation  = 0x0,

            /// <summary>
            /// Equivalent to numeric value 1. Indicates text frame.
            /// </summary>
            Text          = 0x1,

            /// <summary>
            /// Equivalent to numeric value 2. Indicates binary frame.
            /// </summary>
            Binary        = 0x2,

            // 0x3 - 0x7 are reserved for further non-control frames


            /// <summary>
            /// Equivalent to numeric value 8. Indicates connection close frame.
            /// </summary>
            Close         = 0x8,

            /// <summary>
            /// Equivalent to numeric value 9. Indicates ping frame.
            /// </summary>
            Ping          = 0x9,

            /// <summary>
            /// Equivalent to numeric value 10. Indicates pong frame.
            /// </summary>
            Pong          = 0xa

            // 0xb - 0xf are reserved for further control frames

        }

        #endregion

        #region (enum) Fin

        public enum Fin : byte
        {

            /// <summary>
            /// Equivalent to numeric value 0. Indicates more frames of a message follow.
            /// </summary>
            More   = 0x0,

            /// <summary>
            /// Equivalent to numeric value 1. Indicates the final frame of a message.
            /// </summary>
            Final  = 0x1

        }

        #endregion

        #region (enum) Rsv

        public enum Rsv : byte
        {

            /// <summary>
            /// Equivalent to numeric value 0. Indicates zero.
            /// </summary>
            Off  = 0x0,

            /// <summary>
            /// Equivalent to numeric value 1. Indicates non-zero.
            /// </summary>
            On   = 0x1

        }

        #endregion

        #region (enum) MaskStatus

        public enum MaskStatus : byte
        {

            /// <summary>
            /// Equivalent to numeric value 0. Indicates not masked.
            /// </summary>
            Off  = 0x0,

            /// <summary>
            /// Equivalent to numeric value 1. Indicates masked.
            /// </summary>
            On   = 0x1

        }

        #endregion

        #region (enum) ClosingStatusCode

        /// <summary>
        /// When closing an established connection (e.g. when sending a Close
        /// frame, after the opening handshake has completed), an endpoint MAY
        /// indicate a reason for closure.
        /// 
        ///    0- 999: Not used
        /// 1000-2999: Reserved for definition by the WebSocket specification and IETF
        /// 3000-3999: Available for use by libraries and frameworks
        /// 4000-4999: Available for use by applications
        /// 
        /// https://www.rfc-editor.org/rfc/rfc6455#section-7.4
        /// </summary>
        public enum ClosingStatusCode : UInt16
        {

            /// <summary>
            /// 1000 indicates a normal closure, meaning that the purpose for
            /// which the connection was established has been fulfilled.
            /// </summary>
            NormalClosure           = 1000,

            /// <summary>
            /// 1001 indicates that an endpoint is "going away", such as a server
            /// going down or a browser having navigated away from a page.
            /// </summary>
            GoingAway               = 1001,

            /// <summary>
            /// 1002 indicates that an endpoint is terminating the connection
            /// due to a protocol error.
            /// </summary>
            ProtocolError           = 1002,

            /// <summary>
            /// 1003 indicates that an endpoint is terminating the connection
            /// because it has received a type of data it cannot accept (e.g., an
            /// endpoint that understands only text data MAY send this if it
            /// receives a binary message).
            /// </summary>
            UnsupportedData         = 1003,

            // 1004                   Reserved. The specific meaning might be defined in the future.

            /// <summary>
            /// 1005 is a reserved value and MUST NOT be set as a status code in a
            /// Close control frame by an endpoint.  It is designated for use in
            /// applications expecting a status code to indicate that no status
            /// code was actually present.
            /// </summary>
            NoStatusReceived        = 1005,

            /// <summary>
            /// 1006 is a reserved value and MUST NOT be set as a status code in a
            /// Close control frame by an endpoint.  It is designated for use in
            /// applications expecting a status code to indicate that the
            /// connection was closed abnormally, e.g., without sending or
            /// receiving a Close control frame.
            /// </summary>
            AbnormalClosure         = 1006,

            /// <summary>
            /// 1007 indicates that an endpoint is terminating the connection
            /// because it has received data within a message that was not
            /// consistent with the type of the message (e.g., non-UTF-8 [RFC3629]
            /// data within a text message).
            /// </summary>
            InvalidPayloadData      = 1007,

            /// <summary>
            /// 1008 indicates that an endpoint is terminating the connection
            /// because it has received a message that violates its policy.
            /// This is a generic status code that can be returned when there is no
            /// other more suitable status code (e.g., 1003 or 1009) or if there
            /// is a need to hide specific details about the policy.
            /// </summary>
            PolicyViolation         = 1008,

            /// <summary>
            /// 1009 indicates that an endpoint is terminating the connection
            /// because it has received a message that is too big for it to
            /// process.
            /// </summary>
            MessageTooBig           = 1009,

            /// <summary>
            /// 1010 indicates that an endpoint (client) is terminating the
            /// connection because it has expected the server to negotiate one or
            /// more extension, but the server didn't return them in the response
            /// message of the WebSocket handshake.  The list of extensions that
            /// are needed SHOULD appear in the /reason/ part of the Close frame.
            /// Note that this status code is not used by the server, because it
            /// can fail the WebSocket handshake instead.
            /// </summary>
            MandatoryExtension      = 1010,

            /// <summary>
            /// 1011 indicates that a server is terminating the connection because
            /// it encountered an unexpected condition that prevented it from
            /// fulfilling the request.
            /// </summary>
            InternalServerError     = 1011,

            /// <summary>
            /// This status code is meant to indicate that the server is restarting.
            /// A service can send this close frame to tell the client it's going
            /// down for a short period, usually for maintenance or deployment.
            /// </summary>
            ServiceRestart          = 1012,

            /// <summary>
            /// This status code is meant to indicate that the server is currently
            /// unable to handle the connection (e.g., due to overloading or
            /// maintenance), but it is suggested that the client reconnect at a
            /// later time.
            /// </summary>
            TryAgainLater           = 1013,

            /// <summary>
            /// This status code is not defined in any RFC for WebSockets.
            /// However, it is defined for HTTP and indicates that the server, while
            /// acting as a gateway or proxy, received an invalid response from an
            /// inbound server it accessed while attempting to fulfill the request.
            /// </summary>
            BadGateway              = 1014,

            /// <summary>
            /// 1015 is a reserved value and MUST NOT be set as a status code in a
            /// Close control frame by an endpoint.  It is designated for use in
            /// applications expecting a status code to indicate that the
            /// connection was closed due to a failure to perform a TLS handshake
            /// (e.g., the server certificate can't be verified).
            /// </summary>
            TlsHandshakeFailure     = 1015

        }

        #endregion


        #region Properties

        public Fin         FIN           { get; }
        public MaskStatus  Mask          { get; }
        public Byte[]      MaskingKey    { get; }
        public Opcodes     Opcode        { get; }
        public Byte[]      Payload       { get; }
        public Rsv         Rsv1          { get; }
        public Rsv         Rsv2          { get; }
        public Rsv         Rsv3          { get; }


        public Boolean IsBinary
            => Opcode == Opcodes.Binary;

        public Boolean IsClose
            => Opcode == Opcodes.Close;

        public Boolean IsCompressed
            => Rsv1   == Rsv.On;

        public Boolean IsContinuation
            => Opcode == Opcodes.Continuation;

        public Boolean IsControl
            => Opcode.IsControl();

        public Boolean IsData
            => Opcode.IsData();

        public Boolean IsFinal
            => FIN    == Fin.Final;

        public Boolean IsFragment
            => FIN    == Fin.More || Opcode == Opcodes.Continuation;

        public Boolean IsMasked
            => Mask   == MaskStatus.On;

        public Boolean IsPing
            => Opcode == Opcodes.Ping;

        public Boolean IsPong
            => Opcode == Opcodes.Pong;

        public Boolean IsText
            => Opcode == Opcodes.Text;

        /// <summary>
        /// The event tracking identification for correlating this HTTP WebSocket frame with other events.</param>
        /// </summary>
        public EventTracking_Id  EventTrackingId { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new web socket frame.
        /// </summary>
        /// <param name="FIN">Whether this frame is the final frame of a larger fragmented frame.</param>
        /// <param name="Mask">The status of the frame mask.</param>
        /// <param name="MaskingKey">The masking key.</param>
        /// <param name="Opcode">The opcode.</param>
        /// <param name="Payload">The payload.</param>
        /// <param name="Rsv1">Reserved 1</param>
        /// <param name="Rsv2">Reserved 2</param>
        /// <param name="Rsv3">Reserved 3</param>
        /// 
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param></param>
        private WebSocketFrame(Opcodes            Opcode,
                               Byte[]?            Payload           = null,
                               Fin                FIN               = Fin.Final,
                               MaskStatus         Mask              = MaskStatus.Off,
                               Byte[]?            MaskingKey        = null,
                               Rsv                Rsv1              = Rsv.Off,
                               Rsv                Rsv2              = Rsv.Off,
                               Rsv                Rsv3              = Rsv.Off,

                               EventTracking_Id?  EventTrackingId   = null)

        {

            if (Mask == MaskStatus.On && (MaskingKey is null || MaskingKey.Length != 4))
                throw new ArgumentException("When a web socket mask is used the given masking key must be set!");

            this.Opcode           = Opcode;
            this.Payload          = Payload    ?? [];
            this.FIN              = FIN;
            this.Mask             = Mask;
            this.MaskingKey       = MaskingKey ?? [];
            this.Rsv1             = Rsv1;
            this.Rsv2             = Rsv2;
            this.Rsv3             = Rsv3;

            this.EventTrackingId  = EventTrackingId ?? EventTracking_Id.New;

        }

        #endregion


        #region (static) Continuation(Payload = null, ...)

        /// <summary>
        /// Create a new 'Continuation' frame.
        /// </summary>
        /// <param name="Payload">The payload.</param>
        /// <param name="FIN">Whether this frame is the final frame of a larger fragmented frame.</param>
        /// <param name="Mask">The status of the frame mask.</param>
        /// <param name="MaskingKey">The masking key.</param>
        /// <param name="Rsv1">Reserved 1</param>
        /// <param name="Rsv2">Reserved 2</param>
        /// <param name="Rsv3">Reserved 3</param>
        public static WebSocketFrame Continuation(Byte[]?     Payload      = null,
                                                  Fin         FIN          = Fin.Final,
                                                  MaskStatus  Mask         = MaskStatus.Off,
                                                  Byte[]?     MaskingKey   = null,
                                                  Rsv         Rsv1         = Rsv.Off,
                                                  Rsv         Rsv2         = Rsv.Off,
                                                  Rsv         Rsv3         = Rsv.Off)

            => new (Opcodes.Continuation,
                    Payload,
                    FIN,
                    Mask,
                    MaskingKey,
                    Rsv1,
                    Rsv2,
                    Rsv3);

        #endregion

        #region (static) Text        (Text    = null, ...)

        /// <summary>
        /// Create a new 'Text' frame.
        /// </summary>
        /// <param name="Text">The text payload.</param>
        /// <param name="FIN">Whether this frame is the final frame of a larger fragmented frame.</param>
        /// <param name="Mask">The status of the frame mask.</param>
        /// <param name="MaskingKey">The masking key.</param>
        /// <param name="Rsv1">Reserved 1</param>
        /// <param name="Rsv2">Reserved 2</param>
        /// <param name="Rsv3">Reserved 3</param>
        public static WebSocketFrame Text(String?     Text         = null,
                                          Fin         FIN          = Fin.Final,
                                          MaskStatus  Mask         = MaskStatus.Off,
                                          Byte[]?     MaskingKey   = null,
                                          Rsv         Rsv1         = Rsv.Off,
                                          Rsv         Rsv2         = Rsv.Off,
                                          Rsv         Rsv3         = Rsv.Off)

            => new (Opcodes.Text,
                    Text?.ToUTF8Bytes(),
                    FIN,
                    Mask,
                    MaskingKey,
                    Rsv1,
                    Rsv2,
                    Rsv3);

        #endregion

        #region (static) Binary      (Payload = null, ...)

        /// <summary>
        /// Create a new 'Binary' frame.
        /// </summary>
        /// <param name="Payload">The payload.</param>
        /// <param name="FIN">Whether this frame is the final frame of a larger fragmented frame.</param>
        /// <param name="Mask">The status of the frame mask.</param>
        /// <param name="MaskingKey">The masking key.</param>
        /// <param name="Rsv1">Reserved 1</param>
        /// <param name="Rsv2">Reserved 2</param>
        /// <param name="Rsv3">Reserved 3</param>
        public static WebSocketFrame Binary(Byte[]?     Payload      = null,
                                            Fin         FIN          = Fin.Final,
                                            MaskStatus  Mask         = MaskStatus.Off,
                                            Byte[]?     MaskingKey   = null,
                                            Rsv         Rsv1         = Rsv.Off,
                                            Rsv         Rsv2         = Rsv.Off,
                                            Rsv         Rsv3         = Rsv.Off)

            => new (Opcodes.Binary,
                    Payload,
                    FIN,
                    Mask,
                    MaskingKey,
                    Rsv1,
                    Rsv2,
                    Rsv3);

        #endregion

        #region (static) Close       (StatusCode = 1000, Reason = null, ...)

        /// <summary>
        /// Create a new 'Close' frame.
        /// </summary>
        /// <param name="StatusCode">A status code for closing.</param>
        /// <param name="Reason">An optional reason for closing.</param>
        /// <param name="FIN">Whether this frame is the final frame of a larger fragmented frame.</param>
        /// <param name="Mask">The status of the frame mask.</param>
        /// <param name="MaskingKey">The masking key.</param>
        /// <param name="Rsv1">Reserved 1</param>
        /// <param name="Rsv2">Reserved 2</param>
        /// <param name="Rsv3">Reserved 3</param>
        public static WebSocketFrame Close(ClosingStatusCode  StatusCode   = ClosingStatusCode.NormalClosure,
                                           String?            Reason       = null,
                                           Fin                FIN          = Fin.Final,
                                           MaskStatus         Mask         = MaskStatus.Off,
                                           Byte[]?            MaskingKey   = null,
                                           Rsv                Rsv1         = Rsv.Off,
                                           Rsv                Rsv2         = Rsv.Off,
                                           Rsv                Rsv3         = Rsv.Off)
        {

            var payload     = new Byte[2 + (Reason?.Length ?? 0)];

            var statusCode  = BitConverter.GetBytes((UInt16) StatusCode);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(statusCode);

            Array.Copy(statusCode, 0, payload, 0, 2);

            if (Reason is not null)
                Array.Copy(Reason.ToUTF8Bytes(), 0, payload, 2, Reason.Length);

            return new (Opcodes.Close,
                        payload,
                        FIN,
                        Mask,
                        MaskingKey,
                        Rsv1,
                        Rsv2,
                        Rsv3);

        }

        #endregion

        #region (static) Ping        (Payload = null, ...)

        /// <summary>
        /// Create a new 'Ping' frame.
        /// </summary>
        /// <param name="Payload">The payload.</param>
        /// <param name="FIN">Whether this frame is the final frame of a larger fragmented frame.</param>
        /// <param name="Mask">The status of the frame mask.</param>
        /// <param name="MaskingKey">The masking key.</param>
        /// <param name="Rsv1">Reserved 1</param>
        /// <param name="Rsv2">Reserved 2</param>
        /// <param name="Rsv3">Reserved 3</param>
        public static WebSocketFrame Ping(Byte[]?     Payload      = null,
                                          Fin         FIN          = Fin.Final,
                                          MaskStatus  Mask         = MaskStatus.Off,
                                          Byte[]?     MaskingKey   = null,
                                          Rsv         Rsv1         = Rsv.Off,
                                          Rsv         Rsv2         = Rsv.Off,
                                          Rsv         Rsv3         = Rsv.Off)

            => new (Opcodes.Ping,
                    Payload,
                    FIN,
                    Mask,
                    MaskingKey,
                    Rsv1,
                    Rsv2,
                    Rsv3);

        #endregion

        #region (static) Pong        (Payload = null, ...)

        /// <summary>
        /// Create a new 'Pong' frame.
        /// </summary>
        /// <param name="Payload">The payload.</param>
        /// <param name="FIN">Whether this frame is the final frame of a larger fragmented frame.</param>
        /// <param name="Mask">The status of the frame mask.</param>
        /// <param name="MaskingKey">The masking key.</param>
        /// <param name="Rsv1">Reserved 1</param>
        /// <param name="Rsv2">Reserved 2</param>
        /// <param name="Rsv3">Reserved 3</param>
        public static WebSocketFrame Pong(Byte[]?     Payload      = null,
                                          Fin         FIN          = Fin.Final,
                                          MaskStatus  Mask         = MaskStatus.Off,
                                          Byte[]?     MaskingKey   = null,
                                          Rsv         Rsv1         = Rsv.Off,
                                          Rsv         Rsv2         = Rsv.Off,
                                          Rsv         Rsv3         = Rsv.Off)

            => new (Opcodes.Pong,
                    Payload,
                    FIN,
                    Mask,
                    MaskingKey,
                    Rsv1,
                    Rsv2,
                    Rsv3);

        #endregion


        #region Documentation

        //   0                   1                   2                   3
        //   0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
        //  +-+-+-+-+-------+-+-------------+-------------------------------+
        //  |F|R|R|R| opcode|M| Payload len |    Extended payload length    |
        //  |I|S|S|S|  (4)  |A|     (7)     |             (16/64)           |
        //  |N|V|V|V|       |S|             |   (if payload len==126/127)   |
        //  | |1|2|3|       |K|             |                               |
        //  +-+-+-+-+-------+-+-------------+ - - - - - - - - - - - - - - - +
        //  |     Extended payload length continued, if payload len == 127  |
        //  + - - - - - - - - - - - - - - - +-------------------------------+
        //  |                               |Masking-key, if MASK set to 1  |
        //  +-------------------------------+-------------------------------+
        //  | Masking-key (continued)       |          Payload Data         |
        //  +-------------------------------- - - - - - - - - - - - - - - - +
        //  :                     Payload Data continued ...                :
        //  + - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - +
        //  |                     Payload Data continued ...                |
        //  +---------------------------------------------------------------+

        #endregion

        #region TryParse(ByteArray, out Frame, out Length, EventTrackingId = null)

        public static Boolean TryParse(Byte[]                                    ByteArray,
                                       [NotNullWhen(true)]  out WebSocketFrame?  Frame,
                                       [NotNullWhen(true)]  out UInt64           Length,
                                       [NotNullWhen(false)] out String?          ErrorResponse,
                                       EventTracking_Id?                         EventTrackingId = null)
        {

            try
            {

                Frame          = null;
                Length         = 0;
                ErrorResponse  = null;

                if (ByteArray is null || ByteArray.Length < 5) {
                    ErrorResponse = "Invalid byte array!";
                    return false;
                }

                var fin            = (ByteArray[0] & 0x80) == 0x80 ? Fin.Final : Fin.More;
                var rsv1           = (ByteArray[0] & 0x40) == 0x40 ? Rsv.On    : Rsv.Off;
                var rsv2           = (ByteArray[0] & 0x20) == 0x20 ? Rsv.On    : Rsv.Off;
                var rsv3           = (ByteArray[0] & 0x10) == 0x10 ? Rsv.On    : Rsv.Off;
                var opcode         = (Opcodes) (Byte) (ByteArray[0] & 0x0f);

                //if (!opcode.IsSupported ()) {
                //    //throw new WebSocketException (CloseStatusCode.ProtocolError, msg);
                //    ErrorResponse  = "A frame has an unsupported opcode!";
                //    return false;
                //}

                if (!opcode.IsData() && rsv1 == Rsv.On) {
                    ErrorResponse = "A non data frame is compressed!";
                    return false;
                }

                var mask           = (ByteArray[1] & 0x80) == 0x80
                                          ? MaskStatus.On
                                          : MaskStatus.Off;


                // The payload length of HTTP WebSockets frames can be encoded using one of three
                // different formats, depending on the size of the payload:
                // 
                // If the payload length is between 0 and 125 bytes, the length is encoded as a
                // single byte in binary format, with the most significant bit set to 0. This
                // byte directly represents the length of the payload.
                // 
                // If the payload length is between 126 and 65535 bytes, the length is encoded
                // in two bytes in binary format, with the most significant bit of the first
                // byte set to 0 and the most significant bit of the second byte set to 1.
                // The remaining 15 bits of these two bytes are used to represent the length of
                // the payload.
                // 
                // If the payload length is greater than 65535 bytes, the length is encoded in
                // eight bytes in binary format. The first byte has the most significant bit set
                // to 0 and the next seven bits set to 1. The remaining seven bytes are used to
                // represent the length of the payload in network byte order (big-endian).
                // 
                // The encoding of the payload length is included in the WebSocket frame header,
                // along with other information such as the opcode, masking key (if any), and
                // payload data.

                var payloadLength  = (UInt64) (ByteArray[1] & 0x7f);
                var offset         = 2U;

                if (payloadLength == 126) {

                    payloadLength  = (UInt64) ((ByteArray[2] << 8) | ByteArray[3]);

                    offset         = 4U;

                }

                else if (payloadLength == 127) {

                    payloadLength  = ((UInt64) ByteArray[2] << 56) |
                                     ((UInt64) ByteArray[3] << 48) |
                                     ((UInt64) ByteArray[4] << 40) |
                                     ((UInt64) ByteArray[5] << 32) |
                                     ((UInt64) ByteArray[6] << 24) |
                                     ((UInt64) ByteArray[7] << 16) |
                                     ((UInt64) ByteArray[8] <<  8) |
                                               ByteArray[9];

                    offset         = 10U;

                }

                var payload     = new Byte[payloadLength];
                var maskingKey  = new Byte[4] { 0x00, 0x00, 0x00, 0x00 };

                if ((UInt64) ByteArray.Length < offset + payloadLength)
                {
                    ErrorResponse = "Web socket frame is shorter than advertised!";
                    return false;
                }

                if (mask == MaskStatus.Off)
                    Array.Copy(ByteArray, (Int32) offset, payload, 0, (Int32) payloadLength);

                else
                {

                    maskingKey = [
                                     ByteArray[offset],
                                     ByteArray[offset + 1],
                                     ByteArray[offset + 2],
                                     ByteArray[offset + 3]
                                 ];

                    offset += 4;

                    for (var i = 0UL; i < payloadLength; ++i)
                        payload[i] = (Byte) (ByteArray[offset + i] ^ maskingKey[i % 4]);

                }

                // DebugX.Log(String.Concat("Received a '", opcode, "' web socket frame with ", payloadLength, " bytes payload: '", payload.ToUTF8String(), "'!"));

                Frame = new WebSocketFrame(
                            opcode,
                            payload,
                            fin,
                            mask,
                            maskingKey,
                            rsv1,
                            rsv2,
                            rsv3,
                            EventTrackingId
                        );

                Length  = offset + payloadLength;

                return true;

            }
            catch (Exception e)
            {
                DebugX.Log(nameof(WebSocketFrame) + " Exception occurred: " + e.Message);
                Frame          = null;
                Length         = 0;
                ErrorResponse  = "An exception occurred: " + e.Message;
                return false;
            }

        }

        #endregion

        #region ToByteArray()

        public Byte[] ToByteArray()
        {

            var payloadLength  = (UInt64) Payload.Length;
            var offset         = 0U;

            Byte[] frameBytes;

            if (payloadLength < 126)
            {
                frameBytes     = new Byte[payloadLength + 2 + (IsMasked ? 4U : 0U)];
                frameBytes[1]  = (Byte) payloadLength;
                offset         = 2;
            }

            else if (payloadLength >= 126 && payloadLength <= 65536)
            {
                frameBytes = new Byte[payloadLength + 4 + (IsMasked ? 4U : 0U)];
                frameBytes[1] = (Byte) 126;
                var extPayloadLength = ((ushort) payloadLength).InternalToByteArray(ByteOrder.Big);
                frameBytes[2] = extPayloadLength[0];
                frameBytes[3] = extPayloadLength[1];
                offset = 4;
            }

            else
            {
                frameBytes = new Byte[payloadLength + 10 + (IsMasked ? 4U : 0U)];
                var extPayloadLength = payloadLength.InternalToByteArray(ByteOrder.Big);
                frameBytes[1] = (Byte) 127;
                frameBytes[2] = extPayloadLength[0];
                frameBytes[3] = extPayloadLength[1];
                frameBytes[4] = extPayloadLength[2];
                frameBytes[5] = extPayloadLength[3];
                frameBytes[6] = extPayloadLength[4];
                frameBytes[7] = extPayloadLength[5];
                frameBytes[8] = extPayloadLength[6];
                frameBytes[9] = extPayloadLength[7];
                offset = 10;
            }


            if (FIN == Fin.Final)
                frameBytes[0] |= 0x80;

            if (Rsv1 == Rsv.On)
                frameBytes[0] |= 0x40;

            if (Rsv2 == Rsv.On)
                frameBytes[0] |= 0x20;

            if (Rsv3 == Rsv.On)
                frameBytes[0] |= 0x10;

            frameBytes[0] |= (Byte) Opcode;


            // Mask is only required for client -> server communication!
            if (IsMasked)
            {

                frameBytes[1] |= 0x80;

                frameBytes[offset]     = MaskingKey[0];
                frameBytes[offset + 1] = MaskingKey[1];
                frameBytes[offset + 2] = MaskingKey[2];
                frameBytes[offset + 3] = MaskingKey[3];

                offset += 4;

                for (var i = 0U; i < payloadLength; ++i)
                    frameBytes[i + offset] = (Byte) (Payload[i] ^ MaskingKey[i % 4]);

            }
            else
                Array.Copy(Payload, 0, frameBytes, offset, Payload.Length);

            return frameBytes;

        }

        #endregion


        #region ToJSON(CustomWebSocketFrameSerializer = null, ...)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="CustomWebSocketFrameSerializer">A delegate to serialize WebSocketFrames.</param>
        public JObject ToJSON(CustomJObjectSerializerDelegate<WebSocketFrame>? CustomWebSocketFrameSerializer = null)
        {

            var json = JSONObject.Create(

                           new JProperty("opcode",    Opcode.ToString()),

                           new JProperty("payload",   Opcode switch {
                                                          Opcodes.Continuation  => Payload.ToHexString(),
                                                          Opcodes.Text          => Payload.ToUTF8String(),
                                                          Opcodes.Binary        => Payload.ToHexString(),
                                                          Opcodes.Ping          => Payload.ToUTF8String(),
                                                          Opcodes.Pong          => Payload.ToUTF8String(),
                                                          _                     => null // also: Close
                                                      }),
                           new JProperty("length",    Payload.Length),

                           Opcode == Opcodes.Close
                               ? new JProperty("closingStatusCode",  this.GetClosingStatusCode().ToString())
                               : null,

                           Opcode == Opcodes.Close
                               ? new JProperty("closingReason",      this.GetClosingReason())
                               : null,

                           new JProperty("FIN",       FIN.   ToString()),

                           new JProperty("Rsv1",      Rsv1.  ToString()),
                           new JProperty("Rsv2",      Rsv2.  ToString()),
                           new JProperty("Rsv3",      Rsv3.  ToString())

                       );

            return CustomWebSocketFrameSerializer is not null
                       ? CustomWebSocketFrameSerializer(this, json)
                       : json;

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => Opcode switch {
                   Opcodes.Continuation  => $"Continuation '{Payload.ToHexString().SubstringMax(50)}' ({Payload.Length})",
                   Opcodes.Text          => $"Text '{Payload.ToUTF8String().SubstringMax(50)}' ({Payload.ToUTF8String().Length})",
                   Opcodes.Binary        => $"Binary '{Payload.ToHexString().SubstringMax(50)}' ({Payload.Length})",
                   Opcodes.Close         => $"Close '{this.GetClosingStatusCode()}' '{this.GetClosingReason()}'",
                   Opcodes.Ping          => $"Ping '{Payload.ToUTF8String().SubstringMax(50)}' ({Payload.Length})",
                   Opcodes.Pong          => $"Pong '{Payload.ToUTF8String().SubstringMax(50)}' ({Payload.Length})",
                   _                     => $"Unknown web socket opcode '{Opcode}'!"
               };

        #endregion


    }

}
