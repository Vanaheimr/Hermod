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

using System.Text;
using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Extensions methods for DNS TXT resource records.
    /// </summary>
    public static class DNS_TXT_Extensions
    {

        #region CacheTXT(this DNSClient, DomainName, RText, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS TXT record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">The domain name of this TXT resource record.</param>
        /// <param name="RText">The text of this DNS TXT resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheTXT(this DNSClient   DNSClient,
                                    DomainName       DomainName,
                                    String           RText,
                                    DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                    TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new TXT(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                RText
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS Text (TXT) resource record (RFC 1035 §3.3.14).
    /// </summary>
    /// <remarks>
    /// The RDATA of a TXT record is a sequence of one or more &lt;character-string&gt;s
    /// of at most 255 bytes each. Two views of that sequence exist side by side:
    /// <see cref="Text"/> is their concatenation without separators, which is how
    /// SPF (RFC 7208 §3.3) and most other single-text uses read a record, and
    /// <see cref="Strings"/> keeps every character-string as it is, which is what
    /// DNS-Based Service Discovery needs (RFC 6763 §6: one <c>key=value</c> pair per
    /// string, exposed through <see cref="KeyValues"/> and <see cref="TryGetValue"/>).
    /// Character-strings are read and written as UTF-8 (RFC 6763 §6.5); for ASCII
    /// text that is byte-for-byte what the record always was.
    /// </remarks>
    public class TXT : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Text (TXT) resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes  TypeId                      = DNSResourceRecordTypes.TXT;

        /// <summary>
        /// The maximum length of a single &lt;character-string&gt; in bytes (RFC 1035 §3.3).
        /// </summary>
        public const Int32                   MaxCharacterStringLength    = 255;

        private readonly String[]                                strings;
        private          IReadOnlyDictionary<String, String?>?  keyValues;

        #endregion

        #region Properties

        /// <summary>
        /// The concatenation of all character-strings of this TXT resource record
        /// without separators (RFC 7208 §3.3).
        /// </summary>
        public String                            Text          { get; }

        /// <summary>
        /// The character-strings of this TXT resource record as they appear on the
        /// wire, each at most 255 bytes of UTF-8 (RFC 1035 §3.3.14).
        /// </summary>
        public IReadOnlyList<String>             Strings
            => strings;

        /// <summary>
        /// The DNS-SD key/value view of the character-strings (RFC 6763 §6.4):
        /// a string <c>key=value</c> maps the key to its value, a string without
        /// an equals sign is a boolean attribute and maps the key to null, a string
        /// starting with an equals sign is ignored, and only the first occurrence
        /// of a key counts. Keys are compared case-insensitively.
        /// </summary>
        public IReadOnlyDictionary<String, String?>  KeyValues
            => keyValues ??= ParseKeyValues(strings);

        #endregion

        #region Constructor

        #region TXT(DomainName,     Stream)

        /// <summary>
        /// Create a new TXT resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this TXT resource record.</param>
        /// <param name="Stream">A stream containing the TXT resource record data.</param>
        public TXT(DomainName  DomainName,
                   Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            this.strings  = ReadStrings(Stream);
            this.Text     = String.Concat(this.strings);

        }

        #endregion

        #region TXT(DNSServiceName, Stream)

        /// <summary>
        /// Create a new TXT resource record from the given name and stream.
        /// </summary>
        /// <param name="DNSServiceName">The owner name of this TXT resource record (may contain underscore labels, e.g. a DNS-SD service instance name).</param>
        /// <param name="Stream">A stream containing the TXT resource record data.</param>
        public TXT(DNSServiceName  DNSServiceName,
                   Stream          Stream)

            : base(DNSServiceName,
                   TypeId,
                   Stream)

        {

            this.strings  = ReadStrings(Stream);
            this.Text     = String.Concat(this.strings);

        }

        #endregion

        #region TXT(DomainName,     Class, TimeToLive, RText)

        /// <summary>
        /// Create a new DNS TXT resource record from a single text, which is split
        /// into character-strings of at most 255 bytes on the wire (RFC 7208 §3.3).
        /// </summary>
        /// <param name="DomainName">The domain name of this TXT resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="RText">The text of this DNS TXT resource record.</param>
        public TXT(DomainName       DomainName,
                   DNSQueryClasses  Class,
                   TimeSpan         TimeToLive,
                   String           RText)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive,
                   RText)

        {

            ArgumentNullException.ThrowIfNull(RText);

            this.strings  = SplitText(RText);
            this.Text     = RText;

        }

        #endregion

        #region TXT(DNSServiceName, Class, TimeToLive, RText)

        /// <summary>
        /// Create a new DNS TXT resource record from a single text, which is split
        /// into character-strings of at most 255 bytes on the wire (RFC 7208 §3.3).
        /// </summary>
        /// <param name="DNSServiceName">The owner name of this TXT resource record (may contain underscore labels).</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="RText">The text of this DNS TXT resource record.</param>
        public TXT(DNSServiceName   DNSServiceName,
                   DNSQueryClasses  Class,
                   TimeSpan         TimeToLive,
                   String           RText)

            : base(DNSServiceName,
                   TypeId,
                   Class,
                   TimeToLive,
                   RText)

        {

            ArgumentNullException.ThrowIfNull(RText);

            this.strings  = SplitText(RText);
            this.Text     = RText;

        }

        #endregion

        #region TXT(DomainName,     Class, TimeToLive, Strings)

        /// <summary>
        /// Create a new DNS TXT resource record from the given character-strings,
        /// each of which must not exceed 255 bytes of UTF-8 (e.g. the
        /// <c>key=value</c> pairs of a DNS-SD service, RFC 6763 §6).
        /// </summary>
        /// <param name="DomainName">The domain name of this TXT resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="Strings">The character-strings of this DNS TXT resource record.</param>
        public TXT(DomainName           DomainName,
                   DNSQueryClasses      Class,
                   TimeSpan             TimeToLive,
                   IEnumerable<String>  Strings)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.strings  = ValidateStrings(Strings);
            this.Text     = String.Concat(this.strings);

        }

        #endregion

        #region TXT(DNSServiceName, Class, TimeToLive, Strings)

        /// <summary>
        /// Create a new DNS TXT resource record from the given character-strings,
        /// each of which must not exceed 255 bytes of UTF-8 (e.g. the
        /// <c>key=value</c> pairs of a DNS-SD service, RFC 6763 §6).
        /// </summary>
        /// <param name="DNSServiceName">The owner name of this TXT resource record (may contain underscore labels, e.g. a DNS-SD service instance name).</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="Strings">The character-strings of this DNS TXT resource record.</param>
        public TXT(DNSServiceName       DNSServiceName,
                   DNSQueryClasses      Class,
                   TimeSpan             TimeToLive,
                   IEnumerable<String>  Strings)

            : base(DNSServiceName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.strings  = ValidateStrings(Strings);
            this.Text     = String.Concat(this.strings);

        }

        #endregion

        #endregion


        #region (static) FromKeyValues(DNSServiceName, Class, TimeToLive, KeyValues)

        /// <summary>
        /// Create a DNS-SD TXT resource record from the given key/value pairs (RFC 6763 §6):
        /// every pair becomes one character-string <c>key=value</c>, a null value becomes
        /// the boolean attribute <c>key</c>. Keys must be printable US-ASCII without '='.
        /// </summary>
        /// <param name="DNSServiceName">The service instance name owning this TXT resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="KeyValues">The key/value pairs in the order they shall appear.</param>
        public static TXT FromKeyValues(DNSServiceName                                  DNSServiceName,
                                        DNSQueryClasses                                 Class,
                                        TimeSpan                                        TimeToLive,
                                        IEnumerable<KeyValuePair<String, String?>>      KeyValues)
        {

            ArgumentNullException.ThrowIfNull(KeyValues);

            var strings = new List<String>();

            foreach (var keyValue in KeyValues)
            {

                if (String.IsNullOrEmpty(keyValue.Key))
                    throw new ArgumentException("A DNS-SD TXT key must not be empty!", nameof(KeyValues));

                foreach (var character in keyValue.Key)
                {
                    // RFC 6763 §6.4: keys are printable US-ASCII (0x20-0x7E) excluding '='.
                    if (character < 0x20 || character > 0x7E || character == '=')
                        throw new ArgumentException($"The DNS-SD TXT key '{keyValue.Key}' must consist of printable US-ASCII characters without '='!", nameof(KeyValues));
                }

                strings.Add(keyValue.Value is null
                                ? keyValue.Key
                                : keyValue.Key + "=" + keyValue.Value);

            }

            return new TXT(
                       DNSServiceName,
                       Class,
                       TimeToLive,
                       strings
                   );

        }

        #endregion

        #region TryGetValue(Key, out Value)

        /// <summary>
        /// Try to get the value of the given DNS-SD key (case-insensitive, RFC 6763 §6.4).
        /// A boolean attribute is present with a null value.
        /// </summary>
        /// <param name="Key">A DNS-SD key.</param>
        /// <param name="Value">The value of the key, or null for a boolean attribute.</param>
        public Boolean TryGetValue(String       Key,
                                   out String?  Value)

            => KeyValues.TryGetValue(Key, out Value);

        #endregion


        #region (static) TryParseFromJSON(Name, TimeToLive, Data)

        /// <summary>
        /// Try to parse this resource record from a DNS JSON API "data" field
        /// (e.g. Google dns.google/resolve or Cloudflare cloudflare-dns.com/dns-query).
        /// </summary>
        /// <remarks>
        /// The two APIs write a TXT's data two ways. Cloudflare's is the
        /// presentation format, every character-string quoted, and it is read
        /// as a zone file's RDATA is. Google's is the text: the strings joined,
        /// nothing quoted and nothing escaped, "v=spf1 a mx -all" for the one
        /// string v=spf1 a mx -all. That is one text, split again into
        /// character-strings of at most 255 octets.
        /// </remarks>
        /// <param name="Name">The owner name of this resource record.</param>
        /// <param name="TimeToLive">The TTL of this resource record.</param>
        /// <param name="Data">The "data" field value from the JSON response.</param>
        /// <returns>The parsed resource record, or null if parsing fails.</returns>
        public static TXT? TryParseFromJSON(DomainName Name, TimeSpan TimeToLive, String Data)
        {
            try
            {

                return IsPresentationFormat(Data, out var strings)
                           ? new TXT(Name, DNSQueryClasses.IN, TimeToLive, strings)
                           : new TXT(Name, DNSQueryClasses.IN, TimeToLive, Data);

            }
            catch { return null; }
        }

        #endregion

        #region (static) TryParseZoneFile(Name, TimeToLive, RData)

        /// <summary>
        /// Try to parse this resource record from the RDATA of a zone-file line:
        /// character-strings, each quoted or not (RFC 1035 §5.1).
        /// </summary>
        /// <param name="Name">The owner name of this resource record.</param>
        /// <param name="TimeToLive">The TTL of this resource record.</param>
        /// <param name="RData">The RDATA as written.</param>
        /// <returns>The parsed resource record, or null if parsing fails.</returns>
        public static TXT? TryParseZoneFile(DomainName Name, TimeSpan TimeToLive, String RData)
        {
            try
            {

                // Every character-string is read by the reader that knows what a
                // quote and a backslash mean, whichever way it is written. A
                // second path used to take whatever that reader refused, and any
                // line that was not quoted strings only, and keep the text as it
                // lay between the outer quotes: "a b" for two strings, a\;b with
                // its backslash, "a\256b" as six characters.
                return TryParseCharacterStrings(RData, out var strings)
                           ? new TXT(Name, DNSQueryClasses.IN, TimeToLive, strings)
                           : null;

            }
            catch { return null; }
        }

        #endregion

        #region (static) IsPresentationFormat(Data, out Strings)

        /// <summary>
        /// Whether a JSON API's TXT data is written in the presentation format
        /// rather than as Google's bare text: it starts with a quote, and it
        /// reads as character-strings to the end.
        /// </summary>
        /// <remarks>
        /// Google's text is whatever the record holds, so it may begin with a
        /// quote too. Text that begins with one and does not read to the end is
        /// still one text, and only a JSON API's data may be taken that way: a
        /// zone file has no second form, and refuses it.
        /// </remarks>
        /// <param name="Data">The "data" field value from the JSON response.</param>
        /// <param name="Strings">The character-strings, when it is the presentation format.</param>
        public static Boolean IsPresentationFormat(String                                 Data,
                                                   [NotNullWhen(true)] out List<String>?  Strings)
        {

            Strings = null;

            return Data.TrimStart().StartsWith('"') &&
                   TryParseCharacterStrings(Data, out Strings);

        }

        #endregion

        #region (static) PresentationOf(Text)

        /// <summary>
        /// The presentation format of one text: character-strings of at most 255
        /// octets each, quoted, with a quote and a backslash escaped.
        /// </summary>
        /// <param name="Text">A text, such as a JSON API's bare TXT data.</param>
        public static String PresentationOf(String Text)

            => String.Join(' ',
                           SplitText(Text).Select(text => $"\"{text.Replace("\\", "\\\\").Replace("\"", "\\\"")}\""));

        #endregion


        #region (private static) ReadStrings(Stream)

        private static String[] ReadStrings(Stream Stream)
        {

            var rdLength = Stream.ReadUInt16BE();

            // RFC 1035 §3.3.14: TXT-DATA is "one or more <character-string>s";
            // RFC 6763 §6.5 reads them as UTF-8.
            return [.. DNSTools.ExtractCharacterStrings(Stream, rdLength, Encoding.UTF8)];

        }

        #endregion

        #region (private static) SplitText(Text)

        /// <summary>
        /// Split a single text into character-strings of at most 255 bytes of UTF-8
        /// without breaking a Unicode scalar value (RFC 7208 §3.3 concatenates them again).
        /// </summary>
        private static String[] SplitText(String Text)
        {

            if (Text.Length == 0)
                return [ String.Empty ];

            var strings  = new List<String>();
            var current  = new StringBuilder();
            var bytes    = 0;

            foreach (var rune in Text.EnumerateRunes())
            {

                var runeBytes = rune.Utf8SequenceLength;

                if (bytes + runeBytes > MaxCharacterStringLength)
                {
                    strings.Add(current.ToString());
                    current.Clear();
                    bytes = 0;
                }

                current.Append(rune.ToString());
                bytes += runeBytes;

            }

            if (current.Length > 0)
                strings.Add(current.ToString());

            return [.. strings];

        }

        #endregion

        #region (private static) ValidateStrings(Strings)

        private static String[] ValidateStrings(IEnumerable<String> Strings)
        {

            ArgumentNullException.ThrowIfNull(Strings);

            var strings = Strings.ToArray();

            foreach (var text in strings)
            {

                if (text is null)
                    throw new ArgumentException("A character-string must not be null!", nameof(Strings));

                var byteCount = Encoding.UTF8.GetByteCount(text);

                if (byteCount > MaxCharacterStringLength)
                    throw new ArgumentException($"A character-string must not exceed {MaxCharacterStringLength} bytes of UTF-8, but {byteCount} bytes were given!", nameof(Strings));

            }

            // RFC 1035 §3.3.14: "one or more"
            return strings.Length == 0
                       ? [ String.Empty ]
                       : strings;

        }

        #endregion

        #region (private static) ParseKeyValues(Strings)

        private static IReadOnlyDictionary<String, String?> ParseKeyValues(IEnumerable<String> Strings)
        {

            var keyValues = new Dictionary<String, String?>(StringComparer.OrdinalIgnoreCase);

            foreach (var text in Strings)
            {

                // RFC 6763 §6.4: an empty string and a string starting with '=' are ignored.
                if (text.Length == 0 || text[0] == '=')
                    continue;

                var equals = text.IndexOf('=');

                var key    = equals < 0 ? text : text[..equals];
                var value  = equals < 0 ? null : text[(equals + 1)..];

                // RFC 6763 §6.4: "If a client receives a TXT record containing the same key
                // more than once, then the client MUST silently ignore all but the first
                // occurrence of that attribute."
                keyValues.TryAdd(key, value);

            }

            return keyValues;

        }

        #endregion

        #region (internal static) TryParseCharacterStrings(Text, out Strings)

        /// <summary>
        /// Read the character-strings of a TXT-shaped RDATA in presentation
        /// format, each written either way RFC 1035 §5.1 allows.
        /// </summary>
        /// <remarks>
        /// RFC 1035 §5.1: a character-string "is expressed in one or two ways:
        /// as a contiguous set of characters without interior spaces, or as a
        /// string beginning with a " and ending with a "". The two may be mixed
        /// on one line, and the file's two escapes hold in both. A quote ends a
        /// string written without quotes and starts the next, as it does in
        /// BIND. Whatever this reader refuses is not read some other way: a
        /// fallback that took the text as it lay kept backslashes and quotes
        /// the record never had.
        /// </remarks>
        /// <param name="Text">The RDATA as written.</param>
        /// <param name="Strings">The character-strings, unescaped.</param>
        internal static Boolean TryParseCharacterStrings(String                                 Text,
                                                         [NotNullWhen(true)] out List<String>?  Strings)
        {

            Strings = null;

            var strings  = new List<String>();
            var current  = new StringBuilder();
            var i        = 0;

            while (i < Text.Length)
            {

                if (Char.IsWhiteSpace(Text[i]))
                {
                    i++;
                    continue;
                }

                var quoted = Text[i] == '"';

                if (quoted)
                    i++;

                current.Clear();

                while (true)
                {

                    if (i >= Text.Length)
                    {

                        // A quoted string ends in a quote, and one that runs out
                        // first is not one.
                        if (quoted)
                            return false;

                        break;

                    }

                    var character = Text[i];

                    if (character == '"')
                    {

                        // The closing quote of this string, or the opening quote
                        // of the next one after a string without quotes.
                        if (quoted)
                            i++;

                        break;

                    }

                    if (!quoted && Char.IsWhiteSpace(character))
                        break;

                    if (character == '\\')
                    {

                        if (!TryParseEscape(Text, ref i, current))
                            return false;

                        continue;

                    }

                    current.Append(character);
                    i++;

                }

                strings.Add(current.ToString());

            }

            Strings = strings;
            return true;

        }

        #endregion

        #region (private static) TryParseEscape(Text, ref Index, Builder)

        /// <summary>
        /// Read the escape at Text[Index], which is a backslash, append what it
        /// stands for, and move Index past it.
        /// </summary>
        private static Boolean TryParseEscape(String         Text,
                                              ref Int32      Index,
                                              StringBuilder  Builder)
        {

            // A backslash at the very end quotes nothing.
            if (Index + 1 >= Text.Length)
                return false;

            var next = Text[Index + 1];

            // RFC 1035 §5.1: "\X where X is any character other than a digit
            // (0-9), is used to quote that character so that its special
            // meaning does not apply."
            if (!Char.IsAsciiDigit(next))
            {
                Builder.Append(next);
                Index += 2;
                return true;
            }

            // "\DDD where each D is a digit is the octet corresponding to the
            // decimal number described by DDD." A digit after the backslash
            // makes this form, and it has three of them: one or two are neither
            // form, and a number past 255 is no octet.
            if (Index + 3 >= Text.Length ||
                !Char.IsAsciiDigit(Text[Index + 2]) ||
                !Char.IsAsciiDigit(Text[Index + 3]))
            {
                return false;
            }

            var value = (next           - '0') * 100 +
                        (Text[Index + 2] - '0') *  10 +
                        (Text[Index + 3] - '0');

            if (value > 255)
                return false;

            Builder.Append((Char) value);
            Index += 4;
            return true;

        }

        #endregion


        #region (protected override) ZoneFileRData()

        /// <inheritdoc/>
        protected override String ZoneFileRData()

            => String.Join(
                   ' ',
                   strings.Select(text => $"\"{text.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"")
               );

        #endregion

        #region (protected override) SerializeRRData(Stream, UseCompression = true, CompressionOffsets = null)

        /// <summary>
        /// Serialize the concrete DNS resource record to the given stream.
        /// </summary>
        /// <param name="Stream">The stream to write to.</param>
        /// <param name="UseCompression">Whether to use name compression (true by default).</param>
        /// <param name="CompressionOffsets">An optional dictionary for name compression offsets.</param>
        protected override void SerializeRRData(Stream                      Stream,
                                                Boolean                     UseCompression       = true,
                                                Dictionary<String, Int32>?  CompressionOffsets   = null)
        {

            var dataLen = strings.Sum(text => 1 + Encoding.UTF8.GetByteCount(text));
            if (dataLen > UInt16.MaxValue)
                throw new InvalidOperationException("RDATA exceeds maximum UInt16 length (65535 bytes)!");

            // RDLENGTH
            Stream.WriteUInt16BE(dataLen);

            foreach (var text in strings)
                Stream.WriteUTF8Max255(text);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"{Text}, {base.ToString()}";

        #endregion

    }

}
