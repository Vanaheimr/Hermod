/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com> <achim.friedland@graphdefined.com>
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

namespace org.GraphDefined.Vanaheimr.Hermod.Modbus.Toolbox
{

    public static class MultiConverters
    {

        public static Int16[] NetworkBytesToHostInt16(Byte[] networkBytes)
        {

            if (networkBytes == null)
                throw new ArgumentNullException("networkBytes");

            var result = new Int16[networkBytes.Length / 2];
            var networkBytes2 = networkBytes.Reverse().ToArray();

            for (int i = result.Length - 1; i >= 0; i--)
                result[i] = (Int16)BitConverter.ToInt16(networkBytes2, 2 * i);

            return result.Reverse().ToArray();

        }

        public static UInt16[] NetworkBytesToHostUInt16(Byte[] networkBytes)
        {

            if (networkBytes == null)
                throw new ArgumentNullException("networkBytes");

            var result = new UInt16[networkBytes.Length / 2];
            var networkBytes2 = networkBytes.Reverse().ToArray();

            for (int i = result.Length - 1; i >= 0; i--)
                result[i] = (UInt16)BitConverter.ToUInt16(networkBytes2, 2 * i);

            return result.Reverse().ToArray();

        }

        public static Single[] NetworkBytesToHostSingle(Byte[] networkBytes)
        {

            if (networkBytes == null)
                throw new ArgumentNullException("networkBytes");

            var result = new Single[networkBytes.Length / 4];
            var networkBytes2 = networkBytes.Reverse().ToArray();

            for (int i = result.Length - 1; i >= 0; i--)
                result[i] = (Single)BitConverter.ToSingle(networkBytes2, 4 * i);

            return result.Reverse().ToArray();

        }

        public static UInt32[] NetworkBytesToHostUInt32(Byte[] networkBytes)
        {

            if (networkBytes == null)
                throw new ArgumentNullException("networkBytes");

            var result = new UInt32[networkBytes.Length / 4];
            var networkBytes2 = networkBytes.Reverse().ToArray();

            for (int i = result.Length - 1; i >= 0; i--)
                result[i] = (UInt32)BitConverter.ToUInt32(networkBytes2, 4 * i);

            return result.Reverse().ToArray();

        }

        public static Int32[] NetworkBytesToHostInt32(Byte[] networkBytes)
        {

            if (networkBytes == null)
                throw new ArgumentNullException("networkBytes");

            var result = new Int32[networkBytes.Length / 4];
            var networkBytes2 = networkBytes.Reverse().ToArray();

            for (int i = result.Length - 1; i >= 0; i--)
                result[i] = (Int32)BitConverter.ToUInt32(networkBytes2, 4 * i);

            return result.Reverse().ToArray();

        }



    }
}
