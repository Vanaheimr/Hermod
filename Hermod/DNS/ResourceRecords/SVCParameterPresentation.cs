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

using System.Net.Sockets;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// The presentation format of SvcParams (RFC 9460 §2.1), shared by SVCB and
    /// HTTPS: reading turns each value into its wire format, writing turns the
    /// wire format back into each key's own presentation.
    /// </summary>
    /// <remarks>
    /// A SvcParam holds its wire format, whichever way it arrived. RFC 9460 §2.1:
    /// "The SvcParamValue is parsed using the character-string decoding algorithm
    /// (Appendix A), producing a value. The value is then validated and converted
    /// into wire format in a manner specific to each key." Both readers used to
    /// keep the text instead - backslashes and all, and a port as the ASCII of
    /// its digits - and both writers wrote the octets back as text, so Hermod's
    /// own round trip held while every other implementation read something else.
    /// </remarks>
    internal static class SVCParameterPresentation
    {

        #region Keys (RFC 9460 §14.3.2)

        private const UInt16 Mandatory      = 0;
        private const UInt16 ALPN           = 1;
        private const UInt16 NoDefaultALPN  = 2;
        private const UInt16 Port           = 3;
        private const UInt16 IPv4Hint       = 4;
        private const UInt16 IPv6Hint       = 6;


        /// <summary>
        /// The registered name of a key, or its unknown-key form "keyNNNNN".
        /// </summary>
        internal static String KeyName(UInt16 Key)

            => Key switch {
                   Mandatory      => "mandatory",
                   ALPN           => "alpn",
                   NoDefaultALPN  => "no-default-alpn",
                   Port           => "port",
                   IPv4Hint       => "ipv4hint",
                   IPv6Hint       => "ipv6hint",
                   _              => "key" + Key
               };


        /// <summary>
        /// The key a name stands for: a registered name, or "keyNNNNN" with the
        /// number written "without leading zeros" (RFC 9460 §2.1).
        /// </summary>
        internal static UInt16? TryParseKey(String KeyName)

            => KeyName.ToLowerInvariant() switch {
                   "mandatory"        => Mandatory,
                   "alpn"             => ALPN,
                   "no-default-alpn"  => NoDefaultALPN,
                   "port"             => Port,
                   "ipv4hint"         => IPv4Hint,
                   "ech"              => 5,
                   "ipv6hint"         => IPv6Hint,
                   _ when KeyName.StartsWith("key", StringComparison.OrdinalIgnoreCase) &&
                          UInt16.TryParse(KeyName[3..], out var keyNum)
                       => keyNum,
                   _   => null
               };

        #endregion


        #region Read(SvcParams)

        /// <summary>
        /// Read the SvcParams part of a presentation-format RDATA into SvcParams
        /// in their wire format.
        /// </summary>
        /// <param name="SvcParams">The text after SvcPriority and TargetName.</param>
        /// <exception cref="FormatException">The text is not well-formed, or a value is not valid for its key.</exception>
        internal static List<SVCParameter> Read(String SvcParams)
        {

            var parameters = new List<SVCParameter>();
            var i          = 0;

            while (true)
            {

                while (i < SvcParams.Length && IsBlank(SvcParams[i]))
                    i++;

                if (i >= SvcParams.Length)
                    return parameters;

                // SvcParam = SvcParamKey ["=" SvcParamValue]. Key names are
                // "a"-"z", "0"-"9" and "-", so nothing in one needs decoding.
                var keyStart = i;

                while (i < SvcParams.Length && SvcParams[i] != '=' && !IsBlank(SvcParams[i]))
                    i++;

                var keyName  = SvcParams[keyStart..i];
                var hasValue = i < SvcParams.Length && SvcParams[i] == '=';
                var value    = Array.Empty<Byte>();

                if (hasValue)
                {
                    i++;
                    value = ReadCharacterString(SvcParams, ref i);
                }

                // A name that is neither registered nor keyNNNNN is passed over,
                // as it always was.
                var key = TryParseKey(keyName);

                if (key is null)
                    continue;

                // The unknown-key form is opaque whatever the number: "the decoded
                // value SHALL be used as its wire-format encoding" (§2.1), which is
                // how §7.1.1 can offer key1=\002h2 for an alpn-id the list syntax
                // cannot carry. Only a registered name has a format of its own.
                var opaque = keyName.Length > 3 &&
                             keyName.StartsWith("key", StringComparison.OrdinalIgnoreCase) &&
                             Char.IsAsciiDigit(keyName[3]);

                parameters.Add(new SVCParameter(key.Value, opaque ? value : ToWireFormat(key.Value, value, hasValue)));

            }

        }

        #endregion

        #region (private) ReadCharacterString(Text, ref Position)

        /// <summary>
        /// RFC 1035 §5.1's character-string, as RFC 9460 Appendix A writes it:
        /// quoted, or contiguous up to the next blank, with "\X" standing for X
        /// and "\DDD" for the octet DDD in either form.
        /// </summary>
        private static Byte[] ReadCharacterString(String Text, ref Int32 Position)
        {

            var octets = new List<Byte>();

            if (Position < Text.Length && Text[Position] == '"')
            {

                Position++;

                while (true)
                {

                    if (Position >= Text.Length)
                        throw new FormatException("A quoted SvcParamValue has no closing quote!");

                    var c = Text[Position];

                    if (c == '"')
                    {
                        Position++;
                        break;
                    }

                    if (c == '\\')
                        Position = ReadEscape(Text, Position, octets);

                    else
                        Position = AppendCharacter(Text, Position, octets);

                }

                if (Position < Text.Length && !IsBlank(Text[Position]))
                    throw new FormatException("A quoted SvcParamValue is followed by more than a blank!");

            }

            else
            {

                while (Position < Text.Length && !IsBlank(Text[Position]))
                {

                    var c = Text[Position];

                    if (c == '"')
                        throw new FormatException("An unquoted SvcParamValue contains a quote that is not escaped!");

                    if (c == '\\')
                        Position = ReadEscape(Text, Position, octets);

                    else
                        Position = AppendCharacter(Text, Position, octets);

                }

            }

            return [.. octets];

        }

        #endregion

        #region (private) ReadEscape(Text, Position, Octets)

        private static Int32 ReadEscape(String Text, Int32 Position, List<Byte> Octets)
        {

            if (Position + 1 >= Text.Length)
                throw new FormatException("A SvcParamValue ends in a backslash!");

            if (!Char.IsAsciiDigit(Text[Position + 1]))
                return AppendCharacter(Text, Position + 1, Octets);

            // "\DDD where each D is a digit is the octet corresponding to the
            //  decimal number described by DDD" - three digits, and 255 at most.
            if (Position + 3 >= Text.Length          ||
                !Char.IsAsciiDigit(Text[Position + 2]) ||
                !Char.IsAsciiDigit(Text[Position + 3]))
                throw new FormatException("A decimal escape in a SvcParamValue does not have three digits!");

            var octet = Int32.Parse(Text.AsSpan(Position + 1, 3));

            if (octet > 255)
                throw new FormatException($"The decimal escape \\{octet} does not name an octet!");

            Octets.Add((Byte) octet);

            return Position + 4;

        }

        #endregion

        #region (private) AppendCharacter(Text, Position, Octets)

        private static Int32 AppendCharacter(String Text, Int32 Position, List<Byte> Octets)
        {

            var c = Text[Position];

            if (c < 0x80)
            {
                Octets.Add((Byte) c);
                return Position + 1;
            }

            // Zone files are ASCII, and anything else arrives as UTF-8.
            var length = Char.IsSurrogatePair(Text, Position) ? 2 : 1;
            Octets.AddRange(Encoding.UTF8.GetBytes(Text.Substring(Position, length)));

            return Position + length;

        }

        #endregion

        #region (private) IsBlank(Character)

        private static Boolean IsBlank(Char Character)
            => Character is ' ' or '\t';

        #endregion


        #region (private) ToWireFormat(Key, Value, HasValue)

        /// <summary>
        /// Convert a decoded value into the wire format of its key (RFC 9460 §7,
        /// §8). A key with no format of its own keeps the decoded value: "the
        /// decoded value SHALL be used as its wire-format encoding" (§2.1).
        /// </summary>
        private static Byte[] ToWireFormat(UInt16 Key, Byte[] Value, Boolean HasValue)
        {

            switch (Key)
            {

                case Mandatory:
                {

                    // §8: keys "by their numeric values in network byte order,
                    // concatenated in strictly increasing numeric order", and
                    // "mandatory" "MUST NOT appear in its own value-list".
                    var keys = new SortedSet<UInt16>();

                    foreach (var item in ValueList(Key, Value, HasValue))
                    {

                        var name    = Encoding.ASCII.GetString(item);
                        var listed  = TryParseKey(name) ?? throw new FormatException($"'{name}' is not a SvcParamKey!");

                        if (listed == Mandatory)
                            throw new FormatException("\"mandatory\" must not list itself!");

                        if (!keys.Add(listed))
                            throw new FormatException($"'{name}' is listed twice in \"mandatory\"!");

                    }

                    var wire = new Byte[keys.Count * 2];
                    var at   = 0;

                    foreach (var listed in keys)
                    {
                        wire[at++] = (Byte) (listed >> 8);
                        wire[at++] = (Byte)  listed;
                    }

                    return wire;

                }

                case ALPN:
                {

                    // §7.1.1: each alpn-id "prefixed by its length as a single
                    // octet", and an alpn-id is 1 to 255 octets.
                    var wire = new List<Byte>();

                    foreach (var item in ValueList(Key, Value, HasValue))
                    {

                        if (item.Length > 255)
                            throw new FormatException("An alpn-id is longer than 255 octets!");

                        wire.Add((Byte) item.Length);
                        wire.AddRange(item);

                    }

                    return [.. wire];

                }

                case NoDefaultALPN:

                    // §7.1.1: "the presentation and wire-format values MUST be empty".
                    if (Value.Length > 0)
                        throw new FormatException("\"no-default-alpn\" takes no value!");

                    return [];

                case Port:
                {

                    // §7.2: "a single decimal integer between 0 and 65535 in ASCII.
                    // Any other value (e.g., an empty value) is a syntax error."
                    var text = Encoding.ASCII.GetString(Value);

                    if (text.Length == 0 || text.Length > 5 || !text.All(Char.IsAsciiDigit) ||
                        !UInt16.TryParse(text, out var port))
                        throw new FormatException($"'{text}' is not a port!");

                    return [ (Byte) (port >> 8), (Byte) port ];

                }

                case IPv4Hint:
                case IPv6Hint:
                {

                    // §7.3: "a sequence of IP addresses in network byte order (for
                    // the respective address family)", and "An empty list of
                    // addresses is invalid".
                    var family = Key == IPv4Hint ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6;
                    var wire   = new List<Byte>();

                    foreach (var item in ValueList(Key, Value, HasValue))
                    {

                        var text = Encoding.ASCII.GetString(item);

                        if (!System.Net.IPAddress.TryParse(text, out var address) ||
                             address.AddressFamily != family           ||
                             text.Contains('%')                        ||
                            (family == AddressFamily.InterNetwork && text.Split('.').Length != 4))
                            throw new FormatException($"'{text}' is not an {(Key == IPv4Hint ? "IPv4" : "IPv6")} address!");

                        wire.AddRange(address.GetAddressBytes());

                    }

                    return [.. wire];

                }

                default:
                    return Value;

            }

        }

        #endregion

        #region (private) ValueList(Key, Value, HasValue)

        /// <summary>
        /// RFC 9460 Appendix A.1: a comma-separated list, decoded after the
        /// character-string, where "\," is a comma inside an item and "\\" a
        /// backslash. Empty items are not allowed, and neither is an empty list
        /// for any key that takes one.
        /// </summary>
        private static List<Byte[]> ValueList(UInt16 Key, Byte[] Value, Boolean HasValue)
        {

            if (!HasValue || Value.Length == 0)
                throw new FormatException($"\"{KeyName(Key)}\" needs a value!");

            var items = new List<Byte[]>();
            var item  = new List<Byte>();

            for (var i = 0; i < Value.Length; i++)
            {

                if (Value[i] == (Byte) '\\')
                {

                    if (i + 1 >= Value.Length || (Value[i + 1] != (Byte) ',' && Value[i + 1] != (Byte) '\\'))
                        throw new FormatException($"A backslash in the \"{KeyName(Key)}\" list escapes neither a comma nor a backslash!");

                    item.Add(Value[++i]);

                }

                else if (Value[i] == (Byte) ',')
                {

                    if (item.Count == 0)
                        throw new FormatException($"The \"{KeyName(Key)}\" list has an empty item!");

                    items.Add([.. item]);
                    item.Clear();

                }

                else
                    item.Add(Value[i]);

            }

            if (item.Count == 0)
                throw new FormatException($"The \"{KeyName(Key)}\" list has an empty item!");

            items.Add([.. item]);

            return items;

        }

        #endregion


        #region Write(Priority, TargetName, SVCParameters)

        /// <summary>
        /// The presentation-format RDATA of an SVCB-form record. Each value is
        /// written in its key's own format where its octets are well-formed for
        /// that key, and in the unknown-key form "keyNNNNN" otherwise, which
        /// represents any octets (RFC 9460 §2.1) - so nothing is lost either way.
        /// </summary>
        internal static String Write(UInt16                     Priority,
                                     String                     TargetName,
                                     IEnumerable<SVCParameter>  SVCParameters)
        {

            var sb = new StringBuilder();

            sb.Append(Priority);
            sb.Append(' ');
            sb.Append(TargetName);

            foreach (var parameter in SVCParameters.OrderBy(p => p.Key))
            {

                sb.Append(' ');

                var presentation = TryWriteValue(parameter.Key, parameter.Value);

                if (presentation is not null)
                {

                    sb.Append(KeyName(parameter.Key));

                    if (presentation.Length > 0)
                    {
                        sb.Append('=');
                        sb.Append(presentation);
                    }

                }

                else
                {

                    sb.Append("key");
                    sb.Append(parameter.Key);

                    if (parameter.Value.Length > 0)
                    {
                        sb.Append('=');
                        sb.Append('"');
                        sb.Append(EscapeValue(parameter.Value));
                        sb.Append('"');
                    }

                }

            }

            return sb.ToString();

        }

        #endregion

        #region (private) TryWriteValue(Key, Wire)

        /// <summary>
        /// A value in its key's own presentation, "" for a key written alone, or
        /// null where the octets are not well-formed for the key.
        /// </summary>
        private static String? TryWriteValue(UInt16 Key, Byte[] Wire)
        {

            switch (Key)
            {

                case Mandatory:
                {

                    if (Wire.Length == 0 || Wire.Length % 2 != 0)
                        return null;

                    var names    = new List<String>();
                    var previous = -1;

                    for (var i = 0; i < Wire.Length; i += 2)
                    {

                        var listed = (Wire[i] << 8) | Wire[i + 1];

                        if (listed <= previous || listed == Mandatory)
                            return null;

                        names.Add(KeyName((UInt16) listed));
                        previous = listed;

                    }

                    return String.Join(",", names);

                }

                case ALPN:
                {

                    var items = new List<String>();
                    var i     = 0;

                    while (i < Wire.Length)
                    {

                        var length = Wire[i++];

                        if (length == 0 || i + length > Wire.Length)
                            return null;

                        // Appendix A.1 first, then the character-string around it.
                        var item = new List<Byte>();

                        foreach (var octet in Wire.AsSpan(i, length))
                        {
                            if (octet == (Byte) ',' || octet == (Byte) '\\')
                                item.Add((Byte) '\\');
                            item.Add(octet);
                        }

                        items.Add(EscapeValue([.. item]));
                        i += length;

                    }

                    return items.Count == 0
                               ? null
                               : "\"" + String.Join(",", items) + "\"";

                }

                case NoDefaultALPN:
                    return Wire.Length == 0 ? "" : null;

                case Port:
                    return Wire.Length == 2
                               ? ((Wire[0] << 8) | Wire[1]).ToString()
                               : null;

                case IPv4Hint:
                case IPv6Hint:
                {

                    var size = Key == IPv4Hint ? 4 : 16;

                    if (Wire.Length == 0 || Wire.Length % size != 0)
                        return null;

                    var addresses = new List<String>();

                    for (var i = 0; i < Wire.Length; i += size)
                        addresses.Add(new System.Net.IPAddress(Wire.AsSpan(i, size)).ToString());

                    return String.Join(",", addresses);

                }

                default:
                    return null;

            }

        }

        #endregion

        #region EscapeValue(Value)

        /// <summary>
        /// A character-string for a quoted value: every octet outside 0x20-0x7E,
        /// and the quote and the backslash, written \DDD (RFC 1035 §5.1).
        /// </summary>
        internal static String EscapeValue(Byte[] Value)
        {

            var sb = new StringBuilder();

            foreach (var b in Value)
            {

                if (b >= 32 && b <= 126 && b != (Byte) '\\' && b != (Byte) '"')
                    sb.Append((Char) b);

                else
                {
                    sb.Append('\\');
                    sb.Append( (b / 100).      ToString("D1"));
                    sb.Append(((b /  10) % 10).ToString("D1"));
                    sb.Append( (b        % 10).ToString("D1"));
                }

            }

            return sb.ToString();

        }

        #endregion

    }

}
