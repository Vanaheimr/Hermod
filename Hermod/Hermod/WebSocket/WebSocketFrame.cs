/*
 * Copyright (c) 2010-2022 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System;

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


    public static class Ext
    {

        //public static bool TryGetUTF8DecodedString(this byte[] bytes, out string s)
        //{
        //    s = null;

        //    try
        //    {
        //        s = Encoding.UTF8.GetString(bytes);
        //    }
        //    catch
        //    {
        //        return false;
        //    }

        //    return true;
        //}

        //public static bool TryGetUTF8EncodedBytes(this string s, out byte[] bytes)
        //{
        //    bytes = null;

        //    try
        //    {
        //        bytes = Encoding.UTF8.GetBytes(s);
        //    }
        //    catch
        //    {
        //        return false;
        //    }

        //    return true;
        //}

        //public static T[] Reverse<T>(this T[] array)
        //{
        //    var len = array.Length;
        //    var ret = new T[len];

        //    var end = len - 1;
        //    for (var i = 0; i <= end; i++)
        //        ret[i] = array[end - i];

        //    return ret;
        //}

        //public static byte[] ToHostOrder(this byte[] source, ByteOrder sourceOrder)
        //{
        //    if (source == null)
        //        throw new ArgumentNullException("source");

        //    if (source.Length < 2)
        //        return source;

        //    if (sourceOrder.IsHostOrder())
        //        return source;

        //    return source.Reverse();

        //}

        //public static ushort ToUInt16(this byte[] source, ByteOrder sourceOrder)
        //{
        //    return BitConverter.ToUInt16(source.ToHostOrder(sourceOrder), 0);
        //}

        //public static ulong ToUInt64(this byte[] source, ByteOrder sourceOrder)
        //{
        //    return BitConverter.ToUInt64(source.ToHostOrder(sourceOrder), 0);
        //}

        //public static T[] SubArray<T>(this T[] array, int startIndex, int length)
        //{

        //    if (array == null)
        //        throw new ArgumentNullException("array");

        //    var len = array.Length;
        //    if (len == 0)
        //    {
        //        if (startIndex != 0)
        //            throw new ArgumentOutOfRangeException("startIndex");

        //        if (length != 0)
        //            throw new ArgumentOutOfRangeException("length");

        //        return array;
        //    }

        //    if (startIndex < 0 || startIndex >= len)
        //        throw new ArgumentOutOfRangeException("startIndex");

        //    if (length < 0 || length > len - startIndex)
        //        throw new ArgumentOutOfRangeException("length");

        //    if (length == 0)
        //        return new T[0];

        //    if (length == len)
        //        return array;

        //    var subArray = new T[length];
        //    Array.Copy(array, startIndex, subArray, 0, length);

        //    return subArray;

        //}

        //public static T[] SubArray<T>(this T[] array, long startIndex, long length)
        //{
        //    if (array == null)
        //        throw new ArgumentNullException("array");

        //    var len = array.LongLength;
        //    if (len == 0)
        //    {
        //        if (startIndex != 0)
        //            throw new ArgumentOutOfRangeException("startIndex");

        //        if (length != 0)
        //            throw new ArgumentOutOfRangeException("length");

        //        return array;
        //    }

        //    if (startIndex < 0 || startIndex >= len)
        //        throw new ArgumentOutOfRangeException("startIndex");

        //    if (length < 0 || length > len - startIndex)
        //        throw new ArgumentOutOfRangeException("length");

        //    if (length == 0)
        //        return new T[0];

        //    if (length == len)
        //        return array;

        //    var subArray = new T[length];
        //    Array.Copy(array, startIndex, subArray, 0, length);

        //    return subArray;
        //}

        public static bool IsHostOrder(this ByteOrder order)
        {
            // true:  !(true ^ true)  or !(false ^ false)
            // false: !(true ^ false) or !(false ^ true)
            return !(BitConverter.IsLittleEndian ^ (order == ByteOrder.Little));
        }

        internal static byte[] InternalToByteArray(this ushort value, ByteOrder order)
        {

            var ret = BitConverter.GetBytes(value);

            if (!order.IsHostOrder())
                Array.Reverse(ret);

            return ret;

        }

        internal static byte[] InternalToByteArray(this ulong value, ByteOrder order)
        {

            var ret = BitConverter.GetBytes(value);

            if (!order.IsHostOrder())
                Array.Reverse(ret);

            return ret;

        }

        //internal static byte[] Append(this ushort code, string reason)
        //{

        //    var bytes = code.InternalToByteArray(ByteOrder.Big);

        //    if (reason == null || reason.Length == 0)
        //        return bytes;

        //    var buff = new List<byte>(bytes);
        //    buff.AddRange(Encoding.UTF8.GetBytes(reason));

        //    return buff.ToArray();

        //}



        public   static Boolean IsControl(this WebSocketFrame.Opcodes Opcode)
            => Opcode >= WebSocketFrame.Opcodes.Close;

        public   static Boolean IsData   (this WebSocketFrame.Opcodes Opcode)
            => Opcode == WebSocketFrame.Opcodes.Text ||
               Opcode == WebSocketFrame.Opcodes.Binary;

        //internal static Boolean IsReserved(this ushort code)
        //    => code == 1004 ||
        //       code == 1005 ||
        //       code == 1006 ||
        //       code == 1015;

    }


    /// <summary>
    /// A web socket frame.
    /// </summary>
    public class WebSocketFrame
    {

        #region (enum) Opcodes

        /// <summary>
        /// Web socket frame opcodes.
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
        public WebSocketFrame(Fin         FIN,
                              MaskStatus  Mask,
                              Byte[]      MaskingKey,
                              Opcodes     Opcode,
                              Byte[]      Payload,
                              Rsv         Rsv1,
                              Rsv         Rsv2,
                              Rsv         Rsv3)
        {

            if (Mask == MaskStatus.On && (MaskingKey is null || MaskingKey.Length != 4))
                throw new ArgumentException("When a web socket mask is used the given masking key must be set!");

            this.FIN         = FIN;
            this.Mask        = Mask;
            this.MaskingKey  = MaskingKey;
            this.Opcode      = Opcode;
            this.Payload     = Payload;
            this.Rsv1        = Rsv1;
            this.Rsv2        = Rsv2;
            this.Rsv3        = Rsv3;

        }

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

        #region TryParse(ByteArray, out Frame, out Length)

        public static Boolean TryParse(Byte[]              ByteArray,
                                       out WebSocketFrame  Frame,
                                       out UInt64          Length)
        {

            try
            {

                var fin            = (ByteArray[0] & 0x80) == 0x80 ? Fin.Final : Fin.More;
                var rsv1           = (ByteArray[0] & 0x40) == 0x40 ? Rsv.On    : Rsv.Off;
                var rsv2           = (ByteArray[0] & 0x20) == 0x20 ? Rsv.On    : Rsv.Off;
                var rsv3           = (ByteArray[0] & 0x10) == 0x10 ? Rsv.On    : Rsv.Off;
                var opcode         = (Opcodes) (Byte) (ByteArray[0] & 0x0f);

                //if (!opcode.IsSupported ()) {
                //    var msg = "A frame has an unsupported opcode.";
                //    //throw new WebSocketException (CloseStatusCode.ProtocolError, msg);
                //}

                if (!opcode.IsData() && rsv1 == Rsv.On)
                {
                    var msg = "A non data frame is compressed.";
                    //throw new WebSocketException (CloseStatusCode.ProtocolError, msg);
                }

                var mask           = (ByteArray[1] & 0x80) == 0x80
                                          ? MaskStatus.On
                                          : MaskStatus.Off;

                var payloadLength  = (UInt64) (ByteArray[1] & 0x7f);

                var offset         = 2U;


                if (payloadLength == 126) {

                    payloadLength  = BitConverter.ToUInt16(new Byte[] {
                                                               ByteArray[3],
                                                               ByteArray[2]
                                                           },
                                                           0);

                    offset         = 4U;

                }

                else if (payloadLength == 127) {

                    Console.WriteLine("TODO: msglen == 127, needs qword to store msglen");

                    // i don't really know the byte order, please edit this
                    // payloadLen = BitConverter.ToUInt64(new byte[] { bytes[5], bytes[4], bytes[3], bytes[2], bytes[9], bytes[8], bytes[7], bytes[6] }, 0);
                    offset         = 10U;

                }

                var payload     = new Byte[payloadLength];
                var maskingKey  = new Byte[4] { 0x00, 0x00, 0x00, 0x00 };

                if ((UInt64) ByteArray.Length < offset + payloadLength)
                {
                    Frame   = null;
                    Length  = 0;
                    return false;
                }

                if (mask == MaskStatus.Off)
                    Array.Copy(ByteArray, (Int32) offset, payload, 0, (Int32) payloadLength);

                else
                {

                    maskingKey = new Byte[4] {
                                     ByteArray[offset],
                                     ByteArray[offset + 1],
                                     ByteArray[offset + 2],
                                     ByteArray[offset + 3]
                                 };

                    offset += 4;

                    for (var i = 0UL; i < payloadLength; ++i)
                        payload[i] = (Byte) (ByteArray[offset + i] ^ maskingKey[i % 4]);

                }

                Frame   = new WebSocketFrame(fin,
                                             mask,
                                             maskingKey,
                                             opcode,
                                             payload,
                                             rsv1,
                                             rsv2,
                                             rsv3);

                Length  = offset + payloadLength;

                return true;

            }
            catch (Exception e)
            {

                DebugX.Log(nameof(WebSocketFrame) + " Exception occured: " + e.Message);

                Frame  = null;
                Length = 0;
                return false;

            }

        }

        #endregion

        #region ToByteArray()

        public Byte[] ToByteArray()
        {

            var payloadLength  = (UInt64) Payload.Length;
            var offset         = 0;

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

            try
            {
                // Mask is required when client -> server!
                if (IsMasked)
                {

                    frameBytes[1] |= 0x80;

                    frameBytes[offset]     = MaskingKey[0];
                    frameBytes[offset + 1] = MaskingKey[1];
                    frameBytes[offset + 2] = MaskingKey[2];
                    frameBytes[offset + 3] = MaskingKey[3];

                    offset += 4;

                    for (var i = 0U; i < payloadLength; ++i)
                        Payload[i] = (Byte) (Payload[i] ^ MaskingKey[i % 4]);

                }

                Array.Copy(Payload, 0, frameBytes, offset, Payload.Length);

            }
            catch (Exception e)
            {


            }

            return frameBytes;

        }

        #endregion

    }

}
