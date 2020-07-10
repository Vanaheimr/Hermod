/*
 * Copyright (c) 2010-2020, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Threading;

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

        public static bool TryGetUTF8DecodedString(this byte[] bytes, out string s)
        {
            s = null;

            try
            {
                s = Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return false;
            }

            return true;
        }

        public static bool TryGetUTF8EncodedBytes(this string s, out byte[] bytes)
        {
            bytes = null;

            try
            {
                bytes = Encoding.UTF8.GetBytes(s);
            }
            catch
            {
                return false;
            }

            return true;
        }

        public static T[] Reverse<T>(this T[] array)
        {
            var len = array.Length;
            var ret = new T[len];

            var end = len - 1;
            for (var i = 0; i <= end; i++)
                ret[i] = array[end - i];

            return ret;
        }

        public static byte[] ToHostOrder(this byte[] source, ByteOrder sourceOrder)
        {
            if (source == null)
                throw new ArgumentNullException("source");

            if (source.Length < 2)
                return source;

            if (sourceOrder.IsHostOrder())
                return source;

            return source.Reverse();
        }

        public static ushort ToUInt16(this byte[] source, ByteOrder sourceOrder)
        {
            return BitConverter.ToUInt16(source.ToHostOrder(sourceOrder), 0);
        }

        public static ulong ToUInt64(this byte[] source, ByteOrder sourceOrder)
        {
            return BitConverter.ToUInt64(source.ToHostOrder(sourceOrder), 0);
        }

        public static T[] SubArray<T>(this T[] array, int startIndex, int length)
        {

            if (array == null)
                throw new ArgumentNullException("array");

            var len = array.Length;
            if (len == 0)
            {
                if (startIndex != 0)
                    throw new ArgumentOutOfRangeException("startIndex");

                if (length != 0)
                    throw new ArgumentOutOfRangeException("length");

                return array;
            }

            if (startIndex < 0 || startIndex >= len)
                throw new ArgumentOutOfRangeException("startIndex");

            if (length < 0 || length > len - startIndex)
                throw new ArgumentOutOfRangeException("length");

            if (length == 0)
                return new T[0];

            if (length == len)
                return array;

            var subArray = new T[length];
            Array.Copy(array, startIndex, subArray, 0, length);

            return subArray;

        }

        public static T[] SubArray<T>(this T[] array, long startIndex, long length)
        {
            if (array == null)
                throw new ArgumentNullException("array");

            var len = array.LongLength;
            if (len == 0)
            {
                if (startIndex != 0)
                    throw new ArgumentOutOfRangeException("startIndex");

                if (length != 0)
                    throw new ArgumentOutOfRangeException("length");

                return array;
            }

            if (startIndex < 0 || startIndex >= len)
                throw new ArgumentOutOfRangeException("startIndex");

            if (length < 0 || length > len - startIndex)
                throw new ArgumentOutOfRangeException("length");

            if (length == 0)
                return new T[0];

            if (length == len)
                return array;

            var subArray = new T[length];
            Array.Copy(array, startIndex, subArray, 0, length);

            return subArray;
        }

        public static bool IsHostOrder(this ByteOrder order)
        {
            // true: !(true ^ true) or !(false ^ false)
            // false: !(true ^ false) or !(false ^ true)
            return !(BitConverter.IsLittleEndian ^ (order == ByteOrder.Little));
        }

        public static bool IsControl(this byte opcode)
        {
            return opcode > 0x7 && opcode < 0x10;
        }

        public static bool IsControl(this WebSocketFrame.Opcodes opcode)
        {
            return opcode >= WebSocketFrame.Opcodes.Close;
        }

        public static bool IsData(this byte opcode)
        {
            return opcode == 0x1 || opcode == 0x2;
        }

        internal static bool IsData(this WebSocketFrame.Opcodes opcode)
        {
            return opcode == WebSocketFrame.Opcodes.Text || opcode == WebSocketFrame.Opcodes.Binary;
        }

        internal static bool IsReserved(this ushort code)
        {

            return code == 1004 ||
                   code == 1005 ||
                   code == 1006 ||
                   code == 1015;

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

        internal static byte[] Append(this ushort code, string reason)
        {

            var bytes = code.InternalToByteArray(ByteOrder.Big);

            if (reason == null || reason.Length == 0)
                return bytes;

            var buff = new List<byte>(bytes);
            buff.AddRange(Encoding.UTF8.GetBytes(reason));

            return buff.ToArray();

        }

    }



    public class WebSocketFrame
    {

        public enum Opcodes : byte
        {

            /// <summary>
            /// Equivalent to numeric value 0. Indicates continuation frame.
            /// </summary>
            Continuation    = 0x0,

            /// <summary>
            /// Equivalent to numeric value 1. Indicates text frame.
            /// </summary>
            Text            = 0x1,

            /// <summary>
            /// Equivalent to numeric value 2. Indicates binary frame.
            /// </summary>
            Binary          = 0x2,

            /// <summary>
            /// Equivalent to numeric value 8. Indicates connection close frame.
            /// </summary>
            Close           = 0x8,

            /// <summary>
            /// Equivalent to numeric value 9. Indicates ping frame.
            /// </summary>
            Ping            = 0x9,

            /// <summary>
            /// Equivalent to numeric value 10. Indicates pong frame.
            /// </summary>
            Pong            = 0xa

        }

        public enum Fin : byte
        {

            /// <summary>
            /// Equivalent to numeric value 0. Indicates more frames of a message follow.
            /// </summary>
            More = 0x0,

            /// <summary>
            /// Equivalent to numeric value 1. Indicates the final frame of a message.
            /// </summary>
            Final = 0x1

        }

        public enum Rsv : byte
        {

            /// <summary>
            /// Equivalent to numeric value 0. Indicates zero.
            /// </summary>
            Off = 0x0,

            /// <summary>
            /// Equivalent to numeric value 1. Indicates non-zero.
            /// </summary>
            On = 0x1

        }


        public enum MaskStatus : byte
        {

            /// <summary>
            /// Equivalent to numeric value 0. Indicates not masked.
            /// </summary>
            Off = 0x0,

            /// <summary>
            /// Equivalent to numeric value 1. Indicates masked.
            /// </summary>
            On = 0x1

        }


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
            => Opcode >= Opcodes.Close;

        public Boolean IsData
            => Opcode == Opcodes.Text || Opcode == Opcodes.Binary;

        public Boolean IsFinal
            => FIN    == Fin.Final;

        public Boolean IsFragment
            => FIN == Fin.More || Opcode == Opcodes.Continuation;

        public Boolean IsMasked
            => Mask == MaskStatus.On;

        public Boolean IsPing
            => Opcode == Opcodes.Ping;

        public Boolean IsPong
            => Opcode == Opcodes.Pong;

        public Boolean IsText
            => Opcode == Opcodes.Text;






        public WebSocketFrame(Fin         FIN,
                              MaskStatus  Mask,
                              Byte[]      MaskingKey,
                              Opcodes     Opcode,
                              Byte[]      Payload,
                              Rsv         Rsv1,
                              Rsv         Rsv2,
                              Rsv         Rsv3)
        {

            this.FIN         = FIN;
            this.Mask        = Mask;
            this.MaskingKey  = MaskingKey;
            this.Opcode      = Opcode;
            this.Payload     = Payload;
            this.Rsv1        = Rsv1;
            this.Rsv2        = Rsv2;
            this.Rsv3        = Rsv3;

        }


        internal static readonly Byte[] EmptyBytes;


        public Byte[] ToByteArray()
        {

            var payloadLength = (UInt64) Payload.Length;
            var offset        = 0;

            Byte[] frameBytes;

            if (payloadLength < 126)
            {
                frameBytes = new Byte[payloadLength +  2];
                frameBytes[1] = (Byte) payloadLength;
                offset = 2;
            }

            else if (payloadLength >= 126 && payloadLength <= 65536)
            {
                frameBytes = new Byte[payloadLength +  4];
                frameBytes[1] = (Byte) 126;
                var extPayloadLength = ((ushort) payloadLength).InternalToByteArray(ByteOrder.Big);
                frameBytes[2] = extPayloadLength[0];
                frameBytes[3] = extPayloadLength[1];
                offset = 4;
            }

            else
            {
                frameBytes = new Byte[payloadLength + 10];
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

            Array.Copy(Payload, 0, frameBytes, offset, Payload.Length);



            if (FIN == Fin.Final)
                frameBytes[0] |= 0x80;

            if (Rsv1 == Rsv.On)
                frameBytes[0] |= 0x40;

            if (Rsv2 == Rsv.On)
                frameBytes[0] |= 0x20;

            if (Rsv3 == Rsv.On)
                frameBytes[0] |= 0x10;

            frameBytes[0] |= (Byte) Opcode;


            return frameBytes;

        }




        public static WebSocketFrame ParseFrame(Byte[] Frame)
        {

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

            var fin            = (Frame[0] & 0x80) == 0x80 ? Fin.Final : Fin.More;
            var rsv1           = (Frame[0] & 0x40) == 0x40 ? Rsv.On    : Rsv.Off;
            var rsv2           = (Frame[0] & 0x20) == 0x20 ? Rsv.On    : Rsv.Off;
            var rsv3           = (Frame[0] & 0x10) == 0x10 ? Rsv.On    : Rsv.Off;
            var opcode         = (Opcodes) (Byte) (Frame[0] & 0x0f);

            var mask           = (Frame[1] & 0x80) == 0x80 ? MaskStatus.On   : MaskStatus.Off;
            var payloadLength  = (UInt64) (Frame[1] & 0x7f);

            var offset         = 2UL;


            // Extended payload length 1
            if (payloadLength == 126)
            {
                payloadLength  = BitConverter.ToUInt16(new Byte[] { Frame[3], Frame[2] }, 0);
                offset         = 4UL;
            }

            else if (payloadLength == 127)
            {
                Console.WriteLine("TODO: msglen == 127, needs qword to store msglen");
                // i don't really know the byte order, please edit this
                // payloadLen = BitConverter.ToUInt64(new byte[] { bytes[5], bytes[4], bytes[3], bytes[2], bytes[9], bytes[8], bytes[7], bytes[6] }, 0);
                offset         = 10UL;
            }

            var maskingKey  = new Byte[4] {
                                    Frame[offset],
                                    Frame[offset + 1],
                                    Frame[offset + 2],
                                    Frame[offset + 3]
                                };

            offset += 4;


            var payload = new Byte[payloadLength];

            for (UInt64 i = 0; i < payloadLength; ++i)
                payload[i] = (Byte) (Frame[offset + i] ^ maskingKey[i % 4]);




            //if (!opcode.IsSupported ()) {
            //    var msg = "A frame has an unsupported opcode.";
            //    //throw new WebSocketException (CloseStatusCode.ProtocolError, msg);
            //}

            if (!opcode.IsData () && rsv1 == Rsv.On) {
                var msg = "A non data frame is compressed.";
                //throw new WebSocketException (CloseStatusCode.ProtocolError, msg);
            }


            return new WebSocketFrame(fin,
                                        mask,
                                        maskingKey,
                                        opcode,
                                        payload,
                                        rsv1,
                                        rsv2,
                                        rsv3);

        }

    }


}
