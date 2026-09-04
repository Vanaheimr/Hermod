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

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// The type bitmap of NSEC records (RFC 4034 §4.1.2) as Multicast DNS uses it for
    /// negative responses (RFC 6762 §6.1): a responder owning a name but not the queried
    /// type answers with an NSEC record listing the types it does own.
    /// </summary>
    public static class MulticastDNSTypeBitmap
    {

        #region Encode(Types)

        /// <summary>
        /// Encode the given resource record types as NSEC type bitmap.
        /// </summary>
        /// <param name="Types">The resource record types.</param>
        public static Byte[] Encode(IEnumerable<DNSResourceRecordTypes> Types)
        {

            ArgumentNullException.ThrowIfNull(Types);

            var windows = new SortedDictionary<Byte, Byte[]>();

            foreach (var type in Types.Distinct())
            {

                var value   = (UInt16) type;
                var window  = (Byte) (value >> 8);
                var low     = (Byte) (value & 0xFF);

                if (!windows.TryGetValue(window, out var bitmap))
                {
                    bitmap = new Byte[32];
                    windows[window] = bitmap;
                }

                bitmap[low / 8] |= (Byte) (0x80 >> (low % 8));

            }

            using var stream = new MemoryStream();

            foreach (var (window, bitmap) in windows)
            {

                var length = 32;
                while (length > 0 && bitmap[length - 1] == 0)
                    length--;

                if (length == 0)
                    continue;

                stream.WriteByte(window);
                stream.WriteByte((Byte) length);
                stream.Write(bitmap, 0, length);

            }

            return stream.ToArray();

        }

        #endregion

        #region Decode(TypeBitMaps)

        /// <summary>
        /// Decode the resource record types of the given NSEC type bitmap.
        /// </summary>
        /// <param name="TypeBitMaps">The NSEC type bitmap.</param>
        public static IEnumerable<DNSResourceRecordTypes> Decode(Byte[] TypeBitMaps)
        {

            ArgumentNullException.ThrowIfNull(TypeBitMaps);

            var types   = new List<DNSResourceRecordTypes>();
            var offset  = 0;

            while (offset + 2 <= TypeBitMaps.Length)
            {

                var window  = TypeBitMaps[offset];
                var length  = TypeBitMaps[offset + 1];

                offset += 2;

                if (length == 0 || length > 32 || offset + length > TypeBitMaps.Length)
                    break;

                for (var i = 0; i < length; i++)
                {
                    for (var bit = 0; bit < 8; bit++)
                    {
                        if ((TypeBitMaps[offset + i] & (0x80 >> bit)) != 0)
                            types.Add((DNSResourceRecordTypes) ((window << 8) | (i * 8 + bit)));
                    }
                }

                offset += length;

            }

            return types;

        }

        #endregion

        #region Contains(TypeBitMaps, Type)

        /// <summary>
        /// Whether the given NSEC type bitmap contains the given resource record type.
        /// </summary>
        /// <param name="TypeBitMaps">The NSEC type bitmap.</param>
        /// <param name="Type">A resource record type.</param>
        public static Boolean Contains(Byte[]                  TypeBitMaps,
                                       DNSResourceRecordTypes  Type)

            => Decode(TypeBitMaps).Contains(Type);

        #endregion

    }

}
