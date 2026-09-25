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

namespace org.GraphDefined.Vanaheimr.Hermod.Modbus
{

    public enum ByteOrder
    {
        Unmodified,
        HostToNetwork,
        NetworkToHost
    }

    public static class Extensions
    {

        #region WriteWord(this MemoryStream, Word, ByteOrder = ByteOrder.Unmodified)

        public static void WriteWord(this MemoryStream  MemoryStream,
                                     Int16              Word,
                                     ByteOrder          ByteOrder = ByteOrder.Unmodified)
        {

            var byteArray = ByteOrder switch {
                ByteOrder.HostToNetwork  => BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(Word)),
                ByteOrder.NetworkToHost  => BitConverter.GetBytes(System.Net.IPAddress.NetworkToHostOrder(Word)),
                _                        => BitConverter.GetBytes(Word),
            };

            MemoryStream.WriteByte(byteArray[0]);  // high byte
            MemoryStream.WriteByte(byteArray[1]);  // low  byte

        }

        #endregion

        #region WriteWord(this MemoryStream, Word, ByteOrder = ByteOrder.Unmodified)

        /// <remarks>
        /// Written as the Int16 with the same two bytes. IPAddress.HostToNetworkOrder
        /// has no overload for a UInt16: one given to it goes to the Int32 overload,
        /// and the first two bytes of an Int32 in network order are its upper half,
        /// which for every UInt16 is zero.
        /// </remarks>
        public static void WriteWord(this MemoryStream  MemoryStream,
                                     UInt16             Word,
                                     ByteOrder          ByteOrder = ByteOrder.Unmodified)

            => MemoryStream.WriteWord(unchecked((Int16) Word),
                                      ByteOrder);

        #endregion


        #region Write(this MemoryStream, Bytes, Offset)

        /// <summary>
        /// Write all of the given bytes into the stream, beginning at the given
        /// position in it.
        /// </summary>
        /// <remarks>
        /// Not the offset of Stream.Write(Buffer, Offset, Count), which is where
        /// to begin reading in the buffer: this one is where to begin writing in
        /// the stream - the position in a frame at which the bytes belong.
        /// </remarks>
        /// <param name="MemoryStream">A memory stream.</param>
        /// <param name="Bytes">The bytes to write, all of them.</param>
        /// <param name="Offset">The position in the stream to write them at.</param>
        public static void Write(this MemoryStream  MemoryStream,
                                 Byte[]             Bytes,
                                 Int32              Offset)
        {

            MemoryStream.Position = Offset;

            MemoryStream.Write(Bytes,
                               0,
                               Bytes.Length);

        }

        #endregion

    }

}
