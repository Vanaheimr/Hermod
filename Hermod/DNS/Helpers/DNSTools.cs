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

#region Usings

using org.GraphDefined.Vanaheimr.Illias;

using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    public static class DNSTools
    {

        private static readonly UTF8Encoding strictUTF8 = new(false, true);

        #region (static) SerializeCanonicalName(Name)

        /// <summary>
        /// Serialize a domain name into its canonical wire format: lowercased,
        /// length-prefixed labels, root-terminated, never compressed.
        /// </summary>
        /// <param name="Name">A domain name, with or without its trailing dot.</param>
        /// <remarks>
        /// RFC 4034 §6.2 defines this form for DNSSEC signing, and RFC 5155 §5
        /// reuses it as the input to the NSEC3 hash. Both need the identical
        /// bytes, which is why this lives here rather than beside either of them.
        /// </remarks>
        public static Byte[] SerializeCanonicalName(String Name)
        {

            var normalized = Name.ToLowerInvariant().TrimEnd('.');

            if (normalized.Length == 0)
                return [ 0x00 ];

            using var stream = new MemoryStream();

            foreach (var label in normalized.Split('.'))
            {
                var labelBytes = Encoding.ASCII.GetBytes(label);
                stream.WriteByte((Byte) labelBytes.Length);
                stream.Write(labelBytes, 0, labelBytes.Length);
            }

            // The root label terminates the name.
            stream.WriteByte(0x00);

            return stream.ToArray();

        }

        #endregion

        #region (static) FindLastRecordOffset(Message)

        /// <summary>
        /// Walk every record of a serialized DNS message and return the offset at
        /// which the last one begins, or -1 when the message does not parse
        /// cleanly from end to end.
        /// </summary>
        /// <param name="Message">A complete DNS message in wire format.</param>
        /// <remarks>
        /// Both meta-RRs need this and neither can use the ordinary parser to get
        /// it. TSIG (RFC 8945 §5.1) and SIG(0) (RFC 2931 §3) must be the last
        /// record in the additional section, and each is authenticated over the
        /// exact octets that precede it — so what matters is where those octets
        /// stop, not what an object graph re-serializes to.
        ///
        /// The trailing-byte check is deliberate: a message with anything after
        /// its last record is not a message with a signature at the end, and
        /// returning an offset for it would authenticate a prefix of something
        /// the peer never sent.
        /// </remarks>
        public static Int32 FindLastRecordOffset(Byte[] Message)
        {

            if (Message.Length < 12)
                return -1;

            using var stream = new MemoryStream(Message);
            stream.Position  = 12;

            var qdCount      = BinaryPrimitives.ReadUInt16BigEndian(Message.AsSpan(4,  2));
            var anCount      = BinaryPrimitives.ReadUInt16BigEndian(Message.AsSpan(6,  2));
            var nsCount      = BinaryPrimitives.ReadUInt16BigEndian(Message.AsSpan(8,  2));
            var arCount      = BinaryPrimitives.ReadUInt16BigEndian(Message.AsSpan(10, 2));

            for (var i = 0; i < qdCount; i++)
            {
                ExtractName(stream);
                stream.Position += 4;                  // QTYPE + QCLASS
            }

            var lastOffset   = -1;

            for (var i = 0; i < anCount + nsCount + arCount; i++)
            {

                lastOffset       = (Int32) stream.Position;

                ExtractName(stream);
                stream.Position += 8;                  // TYPE + CLASS + TTL

                var rdLength     = stream.ReadUInt16BE();
                stream.Position += rdLength;

                if (stream.Position > Message.Length)
                    return -1;

            }

            return stream.Position == Message.Length
                       ? lastOffset
                       : -1;

        }

        #endregion


        public static Byte[] ExtractByteArray(Stream DNSStream, UInt32 LengthOfSegment)
        {

            if (LengthOfSegment == 0)
                return [];

            var buffer = new Byte[LengthOfSegment];
            var total  = 0;

            // Stream.Read may return fewer octets than asked for even when more
            // are coming, so one call is not a read of LengthOfSegment octets.
            while (total < buffer.Length)
            {

                var read = DNSStream.Read(buffer, total, buffer.Length - total);

                if (read == 0)
                    break;

                total += read;

            }

            // And what did not arrive is not a zero. Returning the full-length
            // array would complete a truncated record with octets the wire never
            // carried, and every length check downstream would then be satisfied
            // by the padding rather than by the data — which is how RFC 4255
            // §3.1.3's twenty octets were being met by two.
            //
            // Returning what actually arrived lets each record type decide:
            // those with a fixed field width refuse the record, those that guard
            // on a minimum length degrade as they were written to. Throwing here
            // would cost every record behind this one instead (finding 21).
            return total == buffer.Length
                       ? buffer
                       : buffer[..total];

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

                dnsNameBuilder.Append(DNSServiceName.EscapeLabel(strictUTF8.GetString(buffer, 0, lengthByte)));

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

        #region ExtractCharacterStrings(Stream, RDLength)

        /// <summary>
        /// Read exactly RDLength octets of RDATA as a sequence of
        /// &lt;character-string&gt;s (RFC 1035 §3.3).
        /// </summary>
        /// <remarks>
        /// RDLENGTH delimits the RDATA (RFC 1035 §4.1.3), so the count of
        /// character-strings follows from it — there is no terminator. Reading
        /// beyond it would consume the following resource record; stopping
        /// short would leave the stream misaligned for it.
        /// </remarks>
        /// <param name="Stream">A stream positioned at the start of the RDATA.</param>
        /// <param name="RDLength">The RDATA length in octets, as read from the RDLENGTH field.</param>
        /// <param name="Encoding">The text encoding of the character-strings (default: ASCII; DNS-SD TXT records use UTF-8, RFC 6763 §6.5).</param>
        public static IEnumerable<String> ExtractCharacterStrings(Stream     Stream,
                                                                  Int32      RDLength,
                                                                  Encoding?  Encoding   = null)
        {

            var encoding   = Encoding ?? System.Text.Encoding.ASCII;
            var strings    = new List<String>();
            var remaining  = RDLength;

            while (remaining > 0)
            {

                var length = Stream.ReadByte();

                if (length < 0)
                    throw new EndOfStreamException("Unexpected end of stream while reading a character-string!");

                remaining--;

                if (length > remaining)
                    throw new InvalidDataException($"character-string of {length} bytes exceeds the {remaining} bytes remaining within RDLENGTH!");

                var buffer = new Byte[length];
                Stream.ReadExactly(buffer, 0, length);

                strings.Add(encoding.GetString(buffer));
                remaining -= length;

            }

            return strings;

        }

        #endregion

        #region ExtractCharacterStrings(Stream)

        public static IEnumerable<String> ExtractCharacterStrings(Stream Stream)
        {

            var list = new List<String>();

            while (true)
            {

                var length = Stream.ReadByte() & byte.MaxValue;
                if (length == 0)
                    return list;

                var buffer = new Byte[length];
                if (Stream.Read(buffer, 0, length) != length)
                    throw new EndOfStreamException("Incomplete character-string read");

                list.Add(Encoding.ASCII.GetString(buffer));

            }

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

            // Every suffix of this name is itself a name, and each one starts
            // where the label in front of it ends. The running offset therefore
            // has to advance label by label: measuring every suffix from
            // CurrentOffset only happens to be right for the first one, which is
            // the same fault as finding 9 and this is the copy of the code it was
            // not fixed in. A table that points at the wrong octet is worse than
            // one that never matches, because the pointer it produces decodes to
            // a name nobody wrote.
            //
            // The position is what identifies a label, not its text: Array.IndexOf
            // finds the first label spelled that way, which is the wrong one for
            // the second "example" in "example.com.example.com".
            var offset = CurrentOffset;

            for (var i = 0; i < labels.Length; i++)
            {

                var labelBytes = Encoding.ASCII.GetBytes(labels[i]);
                if (labelBytes.Length > 63)
                    throw new ArgumentException("Label too long");

                Stream.WriteByte((Byte) labelBytes.Length);
                Stream.Write    (labelBytes, 0, labelBytes.Length);

                offset += 1 + labelBytes.Length;

                var suffix = String.Join(".", labels.Skip(i + 1));
                if (!String.IsNullOrEmpty(suffix) && !Offsets.ContainsKey(suffix))
                    Offsets[suffix] = offset;

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

        public static async Task<UInt16> ReadUInt16BEAsync(this Stream       stream,
                                                           CancellationToken  CancellationToken = default)
        {

            var buffer    = new Byte[2];
            var totalRead = 0;

            while (totalRead < buffer.Length)
            {

                var bytesRead = await stream.ReadAsync(
                                          buffer.AsMemory(totalRead, buffer.Length - totalRead),
                                          CancellationToken
                                      ).ConfigureAwait(false);

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


        #region (static) ToZoneFileText(IPv6Address)

        /// <summary>
        /// The canonical text form of an IPv6 address for use in a zone file:
        /// RFC 5952 §4 — lowercase hexadecimal, no leading zeros in a group, and
        /// the longest run of all-zero groups replaced by "::".
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>IPv6Address.ToString()</c> is not this. It is shaped for an HTTP
        /// authority, where an IPv6 literal is bracketed: it returns
        /// <c>[::1]</c> and <c>[::]</c> for those two addresses, and eight
        /// zero-padded groups for every other. Brackets are not zone-file syntax,
        /// and a reader handed one would be right to refuse it.
        /// </para>
        /// <para>
        /// RFC 5952 §4.2.2 is the rule people get wrong: a single zero group is
        /// written as "0" and never compressed, because "::" standing for one
        /// group is ambiguous with "::" standing for several. §4.2.3 settles ties
        /// in favour of the leftmost run.
        /// </para>
        /// </remarks>
        /// <param name="IPv6Address">An IPv6 address.</param>
        public static String ToZoneFileText(IPv6Address IPv6Address)
        {

            var bytes   = IPv6Address.GetBytes();
            var groups  = new UInt16[8];

            for (var i = 0; i < 8; i++)
                groups[i] = (UInt16) ((bytes[2 * i] << 8) | bytes[2 * i + 1]);

            // The longest run of zero groups, leftmost on a tie (RFC 5952 §4.2.3).
            var bestStart   = -1;
            var bestLength  =  0;
            var runStart    = -1;

            for (var i = 0; i < 8; i++)
            {

                if (groups[i] == 0)
                {

                    if (runStart < 0)
                        runStart = i;

                    var runLength = i - runStart + 1;

                    if (runLength > bestLength)
                    {
                        bestLength  = runLength;
                        bestStart   = runStart;
                    }

                }
                else
                    runStart = -1;

            }

            // RFC 5952 §4.2.2: a run of exactly one zero group is written out.
            if (bestLength < 2)
            {
                bestStart   = -1;
                bestLength  =  0;
            }

            var text = new System.Text.StringBuilder();

            for (var i = 0; i < 8; i++)
            {

                if (bestStart >= 0 && i == bestStart)
                {
                    text.Append(i == 0 ? "::" : ":");
                    i += bestLength - 1;
                    continue;
                }

                text.Append(groups[i].ToString("x"));

                if (i < 7)
                    text.Append(':');

            }

            return text.ToString();

        }

        #endregion


        #region (internal static) TryReadUncompressedName(RData, ref Offset, out Name)

        /// <summary>
        /// Read one domain name out of RDATA that holds it uncompressed, and move
        /// the offset past it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// For the record types whose own specification forbids compressing their
        /// names — KX (RFC 2230 §3.1), HIP (RFC 8005 §5.6), LP (RFC 6742 §2.4.1.2) —
        /// and for every type after RFC 1035 (RFC 3597 §4). Their RDATA is read as a
        /// block of RDLENGTH octets, so a compression pointer in it points into a
        /// message this reader cannot see, and following it would read whatever
        /// lies at that offset of the RDATA instead. A pointer is therefore not a
        /// pointer here but the end of a well-formed name, and the read fails. BIND
        /// refuses the same RDATA for the same reason, "disallowed".
        /// </para>
        /// <para>
        /// So does a label that runs past the RDATA and a name with no root label
        /// before the RDATA ends: either way there is no name to read, and a guess
        /// would move the boundary to whatever follows it.
        /// </para>
        /// </remarks>
        /// <param name="RData">The RDATA of one record.</param>
        /// <param name="Offset">Where the name begins; on success, where it ends.</param>
        /// <param name="Name">The name read.</param>
        internal static Boolean TryReadUncompressedName(Byte[]                                RData,
                                                        ref Int32                             Offset,
                                                        [NotNullWhen(true)] out DomainName?   Name)
        {

            Name       = null;

            var start  = Offset;
            var index  = Offset;

            while (true)
            {

                if (index >= RData.Length)
                    return false;

                var length = RData[index];

                // 11 is a pointer, 01 and 10 are the reserved label types of
                // RFC 1035 §4.1.4 and RFC 6891 §5: none of them is a length.
                if ((length & 0xC0) != 0)
                    return false;

                index++;

                if (length == 0)
                    break;

                if (index + length > RData.Length)
                    return false;

                index += length;

            }

            // RFC 1035 §2.3.4: "255 octets or less", counted on the wire.
            if (index - start > 255)
                return false;

            try
            {

                using var stream = new MemoryStream(RData, start, index - start);

                Name = DomainName.FromWire(ExtractName(stream));

            }
            catch
            {
                return false;
            }

            Offset = index;
            return true;

        }

        #endregion

        #region (static) TryParseLocator64(Text, out Value)

        /// <summary>
        /// Parse the presentation form of a 64-bit ILNP value, a NID's NodeID or an
        /// L64's Locator64 (RFC 6742 §2.1.2, §2.3.2).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Both sections say the same three things: the value is written "using
        /// the same syntax ... used for AAAA records", as "groups of 4 hexadecimal
        /// digits separated by colons", and the "'compressed' display format
        /// (e.g., using "::") ... MUST NOT be used" — "to avoid confusion with a
        /// 128-bit IPv6 address".
        /// </para>
        /// <para>
        /// So exactly four groups and no "::". A group is one to four hexadecimal
        /// digits in either case, which is what the AAAA syntax allows a group to
        /// be and what BIND reads: "14:4fff:ff20:ee64" and "0014:4FFF:FF20:EE64"
        /// are the same value, while a fifth digit, a fifth group, a missing group
        /// and an empty one are all refused.
        /// </para>
        /// </remarks>
        /// <param name="Text">The text form.</param>
        /// <param name="Value">The 64-bit value.</param>
        public static Boolean TryParseLocator64(String      Text,
                                                out UInt64  Value)
        {

            Value = 0;

            var groups = Text.Split(':');

            if (groups.Length != 4)
                return false;

            foreach (var group in groups)
            {

                if (group.Length is < 1 or > 4)
                    return false;

                UInt16 groupValue = 0;

                foreach (var character in group)
                {

                    var digit = character switch {
                                    >= '0' and <= '9' => character - '0',
                                    >= 'a' and <= 'f' => character - 'a' + 10,
                                    >= 'A' and <= 'F' => character - 'A' + 10,
                                    _                 => -1
                                };

                    if (digit < 0)
                        return false;

                    groupValue = (UInt16) ((groupValue << 4) | digit);

                }

                Value = (Value << 16) | groupValue;

            }

            return true;

        }

        #endregion

        #region (static) ToLocator64Text(Value)

        /// <summary>
        /// The presentation form of a 64-bit ILNP value: four colon-separated
        /// groups, never compressed (RFC 6742 §2.1.2, §2.3.2).
        /// </summary>
        /// <remarks>
        /// Each group is written the way RFC 5952 §4.1 and §4.3 write an AAAA
        /// group — lowercase, its leading zeros suppressed — which is what the RFC
        /// asks for by naming the AAAA syntax, and what BIND writes:
        /// "0014:4fff:ff20:ee64" comes out of named-checkzone as
        /// "14:4fff:ff20:ee64". The one AAAA rule that does not carry over is
        /// RFC 5952 §4.2's "::", which §2.1.2 forbids outright.
        /// </remarks>
        /// <param name="Value">The 64-bit value.</param>
        public static String ToLocator64Text(UInt64 Value)

            => String.Join(':',
                           Enumerable.Range(0, 4).
                                      Select(group => ((UInt16) (Value >> (48 - 16 * group))).ToString("x")));

        #endregion

        #region (static) TryParseDottedQuad(Text, out Octets)

        /// <summary>
        /// Parse an IPv4 address in the strict dotted-quad form: four decimal
        /// numbers from 0 to 255, separated by periods, none with a leading zero.
        /// </summary>
        /// <remarks>
        /// <para>
        /// RFC 6742 §2.2.2 writes an L32's Locator32 "using the same syntax used
        /// for existing DNS A records (i.e., 4 decimal numbers separated by
        /// periods without any embedded spaces)". Its own examples in §2.2.3 then
        /// wrote "10.1.02.0", and Erratum 3413 (verified) corrected them to
        /// "10.1.2.0" — a leading zero is where a reader that follows inet_aton
        /// takes the number for octal, so "010" would be eight to one reader and
        /// ten to another. BIND refuses it as a "bad dotted quad".
        /// </para>
        /// <para>
        /// Kept apart from the IPv4 address parser, which serves more than zone
        /// files and is not the place to decide this for every caller.
        /// </para>
        /// </remarks>
        /// <param name="Text">The text form.</param>
        /// <param name="Octets">The four octets.</param>
        public static Boolean TryParseDottedQuad(String                            Text,
                                                 [NotNullWhen(true)] out Byte[]?   Octets)
        {

            Octets = null;

            var parts = Text.Split('.');

            if (parts.Length != 4)
                return false;

            var octets = new Byte[4];

            for (var i = 0; i < 4; i++)
            {

                var part = parts[i];

                if (part.Length is < 1 or > 3)
                    return false;

                if (part.Length > 1 && part[0] == '0')
                    return false;

                var value = 0;

                foreach (var character in part)
                {

                    if (character is < '0' or > '9')
                        return false;

                    value = value * 10 + (character - '0');

                }

                if (value > 255)
                    return false;

                octets[i] = (Byte) value;

            }

            Octets = octets;
            return true;

        }

        #endregion

    }

}
