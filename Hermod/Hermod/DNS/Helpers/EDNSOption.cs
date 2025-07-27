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

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// An EDNS option.
    /// </summary>
    public class EDNSOption
    {
        public UInt16  Code    { get; }
        public Byte[]  Data    { get; }

        public EDNSOption(UInt16 code, byte[] data)
        {
            Code = code;
            Data = data ?? throw new ArgumentNullException(nameof(data), "Data cannot be null");
        }

        public UInt16 Length
            => (UInt16)Data.Length;

    }

}
