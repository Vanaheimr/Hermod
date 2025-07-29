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

using org.GraphDefined.Vanaheimr.Illias;

using System.Buffers.Binary;
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


        public static DomainName     ExtractDomainName    (Stream           DNSStream)
            => DomainName.    Parse(ExtractName(DNSStream));

        public static DNSServiceName ExtractDNSServiceName(Stream           DNSStream)
            => DNSServiceName.Parse(ExtractName(DNSStream));

        public static String         ExtractName          (Stream           DNSStream,
                                                           HashSet<Int64>?  VisitedOffsets   = null)
        {

            // Track visited offsets to prevent infinite recursion
            VisitedOffsets ??= [];

            var dnsNameBuilder  = new StringBuilder();
            var buffer          = new Byte[64]; // Max DNS label length is 63 + null terminator

            while (true)
            {

                // Read the next length byte
                var lengthByte = DNSStream.ReadByte();
                if (lengthByte == -1)
                    throw new IOException("Unexpected end of stream while reading DNS name");

                // End of name (null terminator)
                if (lengthByte == 0)
                    break;

                // Handle compression pointer (high two bits are 11)
                if ((lengthByte & 0xC0) == 0xC0)
                {

                    // Read the second byte of the pointer
                    var secondByte = DNSStream.ReadByte();
                    if (secondByte == -1)
                        throw new IOException("Unexpected end of stream in compression pointer");

                    // Calculate the 14-bit offset
                    var offset = ((lengthByte & 0x3F) << 8) | (secondByte & 0xFF);
                    if (!VisitedOffsets.Add(offset))
                        throw new InvalidDataException("Cyclic compression pointer detected");

                    // Save current position and move to offset
                    var currentPosition = DNSStream.Position;
                    if (offset >= DNSStream.Length)
                        throw new InvalidDataException("Invalid compression pointer offset");

                    DNSStream.Position = offset;
                    var referencedName = ExtractName(DNSStream, [.. VisitedOffsets]);

                    // Append dot if dnsName is not empty
                    if (dnsNameBuilder.Length > 0 && !String.IsNullOrEmpty(referencedName))
                        dnsNameBuilder.Append('.');

                    dnsNameBuilder.Append(referencedName);
                    DNSStream.Position = currentPosition; // Restore position

                    // A compression pointer terminates the name!
                    break;

                }

                // Validate label length (0–63 per RFC 1035)
                if (lengthByte > 63)
                    throw new InvalidDataException($"Invalid DNS label length: {lengthByte}");

                // Append dot if the name is not empty
                if (dnsNameBuilder.Length > 0)
                    dnsNameBuilder.Append('.');

                // Read the label
                if (DNSStream.Read(buffer, 0, lengthByte) != lengthByte)
                    throw new IOException("Unexpected end of stream while reading DNS label");

                dnsNameBuilder.Append(Encoding.ASCII.GetString(buffer, 0, lengthByte));

            }

            var result = dnsNameBuilder.ToString();

            return String.IsNullOrEmpty(result)
                       ? "."
                       : result;

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


        public static String ReplaceFirstDotWithAt(String input)
        {

            var idx = input.IndexOf('.');

            if (idx < 0)
                return input;

            return String.Concat(input.AsSpan(0, idx), "@", input.AsSpan(idx + 1));

        }



        public static void Serialize(this String                 Text,
                                     Stream                      Stream,
                                     Int32                       CurrentOffset,
                                     Boolean                     UseCompression   = true,
                                     Dictionary<String, Int32>?  Offsets          = null)
        {

            Offsets ??= [];

            Text = Text.Trim();

            // Root domain
            if (Text == String.Empty || Text == ".")
            {
                Stream.WriteByte(0x00);
                return;
            }

            // Check for compression
            if (UseCompression && Offsets.TryGetValue(Text, out var pointerOffset))
            {
                // Pointer: 0xC0 | (offset >> 8), then low byte
                UInt16 pointer = (UInt16) (0xC000 | pointerOffset);
                Stream.WriteByte((Byte) (pointer >>    8));
                Stream.WriteByte((Byte) (pointer &  0xFF));
                return;
            }

            // Add offset for this name
            Offsets[Text] = CurrentOffset;

            var labels = Text.Trim('.').Split('.');

            foreach (var label in labels)
            {

                var labelBytes = Encoding.ASCII.GetBytes(label);
                if (labelBytes.Length > 63)
                    throw new ArgumentException("Label too long");

                Stream.WriteByte((Byte) labelBytes.Length);
                Stream.Write    (labelBytes, 0, labelBytes.Length);

                // Update offset for suffixes
                var suffix = String.Join(".", labels.AsEnumerable().Skip(Array.IndexOf(labels, label) + 1));
                if (!String.IsNullOrEmpty(suffix) && !Offsets.ContainsKey(suffix))
                    Offsets[suffix] = CurrentOffset + 1 + labelBytes.Length;

            }

            // End of name
            Stream.WriteByte(0x00);

        }



        public static UInt16 ReadUInt16BE(this Stream stream)
        {

            Span<Byte> buffer = stackalloc Byte[2];

            var totalRead = 0;
            while (totalRead < buffer.Length)
            {

                var bytesRead = stream.Read(buffer.Slice(totalRead));

                if (bytesRead == 0)
                    throw new EndOfStreamException("Unable to read 2 bytes for UInt16");

                totalRead += bytesRead;

            }

            return BinaryPrimitives.ReadUInt16BigEndian(buffer);

        }

        public static UInt32 ReadUInt32BE(this Stream stream)
        {

            Span<Byte> buffer = stackalloc Byte[4];

            var totalRead = 0;
            while (totalRead < buffer.Length)
            {

                var bytesRead = stream.Read(buffer.Slice(totalRead));

                if (bytesRead == 0)
                    throw new EndOfStreamException("Unable to read 4 bytes for UInt32");

                totalRead += bytesRead;

            }

            return BinaryPrimitives.ReadUInt32BigEndian(buffer);

        }




        public static void WriteUInt16BE(this Stream Stream, UInt16 value)
        {
            Span<Byte> buffer = stackalloc Byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
            Stream.Write(buffer);
        }

        public static void WriteUInt16BE(this Stream Stream, Int64 value)
        {
            Span<Byte> buffer = stackalloc Byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(buffer, (UInt16) value);
            Stream.Write(buffer);
        }

        public static void WriteUInt32BE(this Stream Stream, UInt32 Value)
        {
            Span<Byte> buffer = stackalloc Byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(buffer, Value);
            Stream.Write(buffer);
        }

        public static void WriteTimeSpanBE(this Stream Stream, TimeSpan Value)
        {
            Span<Byte> buffer = stackalloc Byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(buffer, (UInt32) Value.TotalSeconds);
            Stream.Write(buffer);
        }



        public static void WriteASCIIMax255(this Stream Stream, String Text)
        {

            if (Text.Length > 255)
                throw new InvalidOperationException("The given text exceeds maximum character-string length of 255 bytes!");

            Span<Byte> buffer = stackalloc Byte[Text.Length];

            for (var i = 0; i < Text.Length; i++)
            {

                var c = Text[i];

                if (c > 127)
                    throw new InvalidOperationException("Non-ASCII character encountered!");

                buffer[i] = (Byte) c;

            }

            Stream.WriteByte((Byte) Text.Length);
            Stream.Write(buffer);

        }

        public static void WriteUTF8Max255(this Stream Stream, String Text)
        {

            ArgumentNullException.ThrowIfNull(Text);

            var maxByteCount   = Encoding.UTF8.GetMaxByteCount(Text.Length);
            Span<Byte> buffer  = stackalloc Byte[maxByteCount];
            var byteCount      = Encoding.UTF8.GetBytes(Text, buffer);
            buffer             = buffer[..byteCount];

            if (byteCount > 255)
                throw new InvalidOperationException("The given text exceeds maximum character-string length of 255 bytes!");

            Stream.WriteByte((Byte) byteCount);
            Stream.Write(buffer);

        }




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
