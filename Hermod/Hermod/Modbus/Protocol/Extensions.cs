/*
 * Copyright (c) 2010-2023 GraphDefined GmbH
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

        public static void WriteWord(this MemoryStream  MemoryStream,
                                     UInt16             Word,
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


        #region Write(this MemoryStream, Bytes, Offset)

        public static void Write(this MemoryStream  MemoryStream,
                                 Byte[]             Bytes,
                                 Int32              Offset)
        {

            MemoryStream.Write(Bytes,
                               Offset,
                               Bytes.Length);

        }

        #endregion

    }

}
