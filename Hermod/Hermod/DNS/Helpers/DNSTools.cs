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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    public static class DNSTools
    {

        public static Byte[] ExtractByteArray(Stream DNSStream, UInt32 LengthOfSegment)
        {

            if (LengthOfSegment > 0)
            {
                var ByteArray = new Byte[LengthOfSegment];
                DNSStream.Read(ByteArray, 0, (Int32) LengthOfSegment);
                return ByteArray;
            }

            return [];

        }

        public static String ExtractName(Stream DNSStream)
        {

            var DNSName          = new StringBuilder();
            var LengthOfSegment  = 0;
            var OldPosition      = 0L;
            var Alias            = String.Empty;
            var buffer           = new Byte[512];

            do
            {

                LengthOfSegment = DNSStream.ReadByte() & Byte.MaxValue;

                if (LengthOfSegment > 0)
                {

                    if (DNSName.Length > 0)
                        DNSName.Append('.');

                    // RDATA Compression
                    if ((LengthOfSegment & 0xC0) == 0xC0)
                    {

                        OldPosition         = DNSStream.Position;
                        DNSStream.Position  = ((LengthOfSegment & 0x3F) << 8) | (DNSStream.ReadByte() & Byte.MaxValue);
                        Alias               = ExtractName(DNSStream);
                        DNSStream.Position  = OldPosition + 1;

                        return Alias;

                    }

                    else
                    {
                        DNSStream.Read(buffer, 0, LengthOfSegment);
                        DNSName.Append(Encoding.ASCII.GetString(buffer, 0, LengthOfSegment));
                    }

                }

            }
            while (LengthOfSegment > 0);

            return DNSName.ToString();

        }


        public static String ExtractNameUTF8(Stream  DNSStream,
                                             Int32   Depth   = 0)
        {

            if (Depth > 10)
                throw new InvalidDataException("DNS name extraction recursion depth exceeded (possible cycle)");

            var DNSName          = new StringBuilder();
            var LengthOfSegment  = 0;
            var buffer           = new Byte[64];

            do
            {

                if (DNSStream.Position >= DNSStream.Length)
                    throw new EndOfStreamException("Unexpected end of DNS stream during name extraction");

                LengthOfSegment = DNSStream.ReadByte() & Byte.MaxValue;

                if (LengthOfSegment > 0)
                {

                    if (LengthOfSegment > 63)
                        throw new InvalidDataException($"Invalid DNS label length: {LengthOfSegment} (max 63)");

                    if (DNSName.Length > 0)
                        DNSName.Append('.');

                    // Compression
                    if ((LengthOfSegment & 0xC0) == 0xC0)
                    {

                        var pointer = ((LengthOfSegment & 0x3F) << 8) | (DNSStream.ReadByte() & Byte.MaxValue);
                        if (pointer >= DNSStream.Length || pointer < 0)
                            throw new InvalidDataException($"Invalid DNS pointer: {pointer}");

                        var oldPosition = DNSStream.Position;
                        DNSStream.Position = pointer;

                        var alias = ExtractNameUTF8(DNSStream, Depth + 1);
                        DNSStream.Position = oldPosition;

                        return alias;

                    }
                    else
                    {

                        if (DNSStream.Read(buffer, 0, LengthOfSegment) != LengthOfSegment)
                            throw new EndOfStreamException("Incomplete DNS label read");

                        DNSName.Append(Encoding.ASCII.GetString(buffer, 0, LengthOfSegment));

                    }
                }

            } while (LengthOfSegment > 0);

            return DNSName.ToString();

        }


        #region ExtractCharacterString(Stream)

        public static String ExtractCharacterString(Stream Stream)
        {

            var length = Stream.ReadByte() & byte.MaxValue;
            if (length == 0) return String.Empty;

            var buffer = new Byte[length];
            if (Stream.Read(buffer, 0, length) != length)
                throw new EndOfStreamException("Incomplete character-string read");

            return Encoding.ASCII.GetString(buffer);

        }

        #endregion


        #region Google DNS

        public static DNSClient GoogleDNS()

            => new ([
                   IPv4Address.Parse("8.8.8.8"),
                   IPv4Address.Parse("8.8.4.4"),
                   IPv6Address.Parse("2001:4860:4860::8888"),
                   IPv6Address.Parse("2001:4860:4860::8844")
               ]);

        #endregion

        #region Google DNS IPv4

        public static DNSClient GoogleDNSv4()

            => new ([
                   IPv4Address.Parse("8.8.8.8"),
                   IPv4Address.Parse("8.8.4.4")
               ]);

        #endregion

        #region Google DNS IPv6

        public static DNSClient GoogleDNSv6()

            => new ([
                   IPv6Address.Parse("2001:4860:4860::8888"),
                   IPv6Address.Parse("2001:4860:4860::8844")
               ]);

        #endregion


    }

}
