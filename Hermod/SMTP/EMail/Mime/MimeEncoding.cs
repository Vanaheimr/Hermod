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

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// How a mail is written: the counterparts of EncodedWords, TransferEncodings
    /// and MimeDate - encoded words (RFC 2047), folded header lines (RFC 5322
    /// §2.2.3), quoted-printable and base64 (RFC 2045 §6.7, §6.8), parameter
    /// values (RFC 2231), dates and message ids.
    /// </summary>
    /// <remarks>
    /// Everything written here is 7-bit ASCII with lines of at most 78 characters
    /// in the header and 76 in the body: a mail that needs neither 8BITMIME nor
    /// SMTPUTF8 from any server on its way, whose DKIM signature survives every
    /// relay, and whose signed parts are what RFC 3156 asks of them.
    /// </remarks>
    public static class MimeEncoding
    {

        #region Data

        /// <summary>
        /// The length a header line is folded at (RFC 5322 §2.1.1).
        /// </summary>
        public const Int32 HeaderLineLength = 78;

        /// <summary>
        /// The length of an encoded body line (RFC 2045 §6.7, §6.8).
        /// </summary>
        public const Int32 BodyLineLength   = 76;

        private const String CRLF           = "\r\n";

        // RFC 2047 §2: a line that holds an encoded word is at most 76 characters.
        private const Int32  EncodedLineLength = 76;

        // RFC 2047 §5 (3): what may stand for itself in an encoded word of a phrase -
        // which is the strictest of the three places one can be, and so right in all.
        private const String QSafe          = "!*+-/";

        // RFC 5322 §3.2.3: the specials, which make a display name a quoted string.
        private const String Specials       = "()<>[]:;@\\,.\"";

        // RFC 2231 §7 attribute-char: what a parameter value may hold unescaped.
        private const String AttributeSafe  = "!#$&+-.^_`|~";

        // RFC 2045 §5.1 tspecials: what makes a parameter value a quoted string.
        private const String TSpecials      = "()<>@,;:\\\"/[]?=";

        private static readonly Regex AddrSpec  = new (@"^[^\s<>()\[\]\\,;:""@]+@[A-Za-z0-9](?:[A-Za-z0-9.-]*[A-Za-z0-9])?$", RegexOptions.CultureInvariant);

        #endregion


        #region IsPrintableAscii(Text)

        /// <summary>
        /// Whether the text is printable ASCII - and tab - only.
        /// </summary>
        public static Boolean IsPrintableAscii(String Text)
        {

            foreach (var character in Text)
                if (character is not ('\t' or (>= ' ' and <= '~')))
                    return false;

            return true;

        }

        #endregion

        #region CheckHeaderValue(Name, Value)

        /// <summary>
        /// A header value must not hold a line break or a NUL: whoever could put one
        /// there could add a header of their own - a Bcc:, a second From: (header
        /// injection).
        /// </summary>
        /// <exception cref="ArgumentException">There is one.</exception>
        public static String CheckHeaderValue(String Name, String Value)
        {

            if (Value.AsSpan().IndexOfAny('\r', '\n', '\0') >= 0)
                throw new ArgumentException($"The value of {Name} holds a line break.");

            return Value;

        }

        #endregion

        #region IsAddress(Address)

        /// <summary>
        /// Whether the text is an address as this writer writes one: local@domain,
        /// with neither a quoted local part nor an address literal.
        /// </summary>
        public static Boolean IsAddress(String Address)

            => AddrSpec.IsMatch(Address) && IsPrintableAscii(Address);

        #endregion


        #region EncodedWords(Text)

        /// <summary>
        /// The text as RFC 2047 encoded words in UTF-8, Q encoding, each at most 75
        /// characters long - the first at most as long as given, to fit the line it
        /// begins - right in a Subject, a comment and a display name alike. A
        /// character is never split between two words.
        /// </summary>
        public static IEnumerable<String> EncodedWords(String  Text,
                                                       Int32   FirstMaximum = 75)
        {

            const String prefix = "=?UTF-8?Q?";
            const String suffix = "?=";

            var room  = Math.Clamp(FirstMaximum, 24, 75) - 10 - 2;

            var word  = new StringBuilder();
            var utf8  = new Byte[4];

            foreach (var rune in Text.EnumerateRunes())
            {

                String encoded;

                if (rune.Value == ' ')
                    encoded = "_";

                else if (rune.IsAscii && (Char.IsAsciiLetterOrDigit((Char) rune.Value) || QSafe.Contains((Char) rune.Value)))
                    encoded = ((Char) rune.Value).ToString();

                else
                {
                    var length  = rune.EncodeToUtf8(utf8);
                    var builder = new StringBuilder(length * 3);
                    for (var i = 0; i < length; i++)
                        builder.Append('=').Append(utf8[i].ToString("X2", CultureInfo.InvariantCulture));
                    encoded = builder.ToString();
                }

                if (word.Length > 0 && word.Length + encoded.Length > room)
                {
                    yield return prefix + word + suffix;
                    word.Clear();
                    room = 75 - 10 - 2;
                }

                word.Append(encoded);

            }

            if (word.Length > 0)
                yield return prefix + word + suffix;

        }

        #endregion

        #region Fold(Name, Pieces)

        /// <summary>
        /// A header line - "Name: value" - folded before the pieces that would make
        /// it longer than 78 characters, without its line break. A piece may ask for
        /// a comma before it: the separator of an address list.
        /// </summary>
        /// <remarks>
        /// Folding inserts a line break before the white space between two pieces;
        /// unfolding takes it out again, and the value is what it was (§2.2.3). A
        /// line that holds encoded words is folded at 76 (RFC 2047 §2).
        /// </remarks>
        public static String Fold(String                                    Name,
                                  IEnumerable<(String Text, Boolean Comma)>  Pieces,
                                  Int32                                     Limit = HeaderLineLength)
        {

            var line    = new StringBuilder(Name).Append(':');
            var column  = Name.Length + 1;
            var first   = true;

            foreach (var (text, comma) in Pieces)
            {

                if (!first && comma)
                {
                    line.Append(',');
                    column++;
                }

                if (!first && column + 1 + text.Length > Limit)
                {
                    line.Append(CRLF).Append(' ');
                    column = 1;
                }
                else
                {
                    line.Append(' ');
                    column++;
                }

                line.Append(text);
                column += text.Length;
                first   = false;

            }

            return line.ToString();

        }

        #endregion

        #region UnstructuredHeader(Name, Text)

        /// <summary>
        /// A header line of free text - a Subject - folded: as it is when it is
        /// ASCII, as encoded words when it is not.
        /// </summary>
        public static String UnstructuredHeader(String Name, String Text)
        {

            CheckHeaderValue(Name, Text);

            return IsPrintableAscii(Text)
                       ? Fold(Name, Text.Split(' ').Select(word => (word, false)))
                       : Fold(Name, EncodedWords(Text, EncodedLineLength - Name.Length - 2).Select(word => (word, false)), EncodedLineLength);

        }

        #endregion

        #region MailboxPieces(Mailbox) / MailboxListHeader(Name, Mailboxes)

        /// <summary>
        /// A mailbox as the pieces of a header: the display name - its words, a
        /// quoted string or encoded words - and the address in angle brackets; or
        /// the address alone.
        /// </summary>
        /// <exception cref="ArgumentException">The address is none this writer writes.</exception>
        public static IEnumerable<String> MailboxPieces(MailboxAddress  Mailbox,
                                                        Int32           FirstMaximum = 75)
        {

            if (!IsAddress(Mailbox.Address))
                throw new ArgumentException($"'{Mailbox.Address}' is no address that can be written.");

            CheckHeaderValue("a display name", Mailbox.Name ?? "");

            var name = Mailbox.Name?.Trim();

            if (String.IsNullOrEmpty(name))
                return [ Mailbox.Address ];

            IEnumerable<String> phrase =
                !IsPrintableAscii(name)
                    ? EncodedWords(name, FirstMaximum)
                    : name.Any(Specials.Contains) || name.Contains("  ") || name.Contains('\t')
                          ? [ "\"" + name.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"" ]
                          : name.Split(' ');

            return [ .. phrase, $"<{Mailbox.Address}>" ];

        }

        /// <summary>
        /// An address header - From, To, Cc, Reply-To - folded between the mailboxes
        /// and inside long display names.
        /// </summary>
        public static String MailboxListHeader(String                       Name,
                                               IEnumerable<MailboxAddress>  Mailboxes)

            => Fold(Name,
                    Mailboxes.SelectMany((mailbox, number) => MailboxPieces(mailbox, number == 0 ? EncodedLineLength - Name.Length - 2 : 75).
                                                                 Select((piece, index) => (piece, index == 0))),
                    EncodedLineLength);

        #endregion

        #region Parameter(Name, Value)

        /// <summary>
        /// A parameter of a Content-Type or Content-Disposition - "; name=value" -
        /// as a token, a quoted string, or, when it is not ASCII, as RFC 2231 has
        /// it: name*=utf-8''percent-encoded.
        /// </summary>
        public static String Parameter(String Name, String Value)
        {

            CheckHeaderValue(Name, Value);

            if (!IsPrintableAscii(Value))
            {

                var encoded = new StringBuilder("utf-8''");

                foreach (var octet in Encoding.UTF8.GetBytes(Value))
                {
                    if (octet < 0x80 && (Char.IsAsciiLetterOrDigit((Char) octet) || AttributeSafe.Contains((Char) octet)))
                        encoded.Append((Char) octet);
                    else
                        encoded.Append('%').Append(octet.ToString("X2", CultureInfo.InvariantCulture));
                }

                return $"{Name}*={encoded}";

            }

            return Value.Length > 0 && !Value.Any(character => character <= ' ' || TSpecials.Contains(character))
                       ? $"{Name}={Value}"
                       : $"{Name}=\"{Value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

        }

        #endregion

        #region ParameterizedHeader(Name, Value, Parameters)

        /// <summary>
        /// A header with parameters - Content-Type, Content-Disposition - folded
        /// between them.
        /// </summary>
        public static String ParameterizedHeader(String                                      Name,
                                                 String                                      Value,
                                                 IEnumerable<(String Name, String Value)>    Parameters)
        {

            var pieces = new List<(String, Boolean)> { (Value + (Parameters.Any() ? ";" : ""), false) };
            var list   = Parameters.ToList();

            for (var i = 0; i < list.Count; i++)
                pieces.Add((Parameter(list[i].Name, list[i].Value) + (i < list.Count - 1 ? ";" : ""), false));

            return Fold(Name, pieces);

        }

        #endregion


        #region QuotedPrintable(Text)

        /// <summary>
        /// Text as quoted-printable (RFC 2045 §6.7), with CRLF between its lines
        /// whatever the line breaks it came with.
        /// </summary>
        /// <remarks>
        /// White space at the end of a line is encoded, so that no relay can take it
        /// away, and so is the F of a line that begins with "From ", so that no
        /// mailbox file can make it "&gt;From " - both changes a signature would not
        /// survive (RFC 3156 §3). Lines are at most 76 characters, with soft line
        /// breaks, and an escape is never split by one.
        /// </remarks>
        public static Byte[] QuotedPrintable(ReadOnlySpan<Byte> Text)
        {

            var output = new StringBuilder(Text.Length + Text.Length / 3);
            var lines  = SplitLines(Text);

            for (var l = 0; l < lines.Count; l++)
            {

                var line   = Text.Slice(lines[l].Start, lines[l].Length);
                var column = 0;

                for (var i = 0; i < line.Length; i++)
                {

                    var octet   = line[i];
                    var last    = i == line.Length - 1;

                    var encoded = octet is (Byte) ' ' or (Byte) '\t'
                                      ? last ? Hex(octet) : ((Char) octet).ToString()
                                      : octet is (Byte) '=' || octet < 33 || octet > 126
                                            ? Hex(octet)
                                            : ((Char) octet).ToString();

                    // A soft line break is "=" at the end of a line, which must fit too -
                    // unless this is the last character of the line.
                    if (column + encoded.Length > (last ? BodyLineLength : BodyLineLength - 1))
                    {
                        output.Append('=').Append(CRLF);
                        column = 0;
                    }

                    if (column == 0 && octet == (Byte) 'F' && line[i..].StartsWith("From "u8))
                        encoded = "=46";

                    output.Append(encoded);
                    column += encoded.Length;

                }

                if (l < lines.Count - 1 || lines[l].Break)
                    output.Append(CRLF);

            }

            return Encoding.ASCII.GetBytes(output.ToString());

        }

        private static String Hex(Byte Octet)
            => "=" + Octet.ToString("X2", CultureInfo.InvariantCulture);

        #endregion

        #region Base64(Data)

        /// <summary>
        /// Octets as base64, in lines of 76 characters with CRLF between them
        /// (RFC 2045 §6.8), and after the last.
        /// </summary>
        public static Byte[] Base64(ReadOnlySpan<Byte> Data)
        {

            var base64 = Convert.ToBase64String(Data);
            var output = new StringBuilder(base64.Length + base64.Length / BodyLineLength * 2 + 2);

            for (var i = 0; i < base64.Length; i += BodyLineLength)
                output.Append(base64, i, Math.Min(BodyLineLength, base64.Length - i)).Append(CRLF);

            return Encoding.ASCII.GetBytes(output.ToString());

        }

        #endregion

        #region NeedsEncoding(Text)

        /// <summary>
        /// Whether text has to be quoted-printable rather than 7bit: it is not
        /// printable ASCII, a line is longer than 76 characters, ends in white space
        /// or begins with "From ".
        /// </summary>
        public static Boolean NeedsEncoding(ReadOnlySpan<Byte> Text)
        {

            foreach (var (start, length, _) in SplitLines(Text))
            {

                var line = Text.Slice(start, length);

                if (line.Length > BodyLineLength || line.StartsWith("From "u8))
                    return true;

                if (line.Length > 0 && line[^1] is (Byte) ' ' or (Byte) '\t')
                    return true;

                foreach (var octet in line)
                    if (octet is not ((Byte) '\t' or (>= 32 and <= 126)))
                        return true;

            }

            return false;

        }

        #endregion

        #region Lines(Text)

        /// <summary>
        /// Text with CRLF between its lines, whatever the line breaks it came with.
        /// </summary>
        public static Byte[] Lines(ReadOnlySpan<Byte> Text)
        {

            var output = new List<Byte>(Text.Length + 16);
            var lines  = SplitLines(Text);

            for (var l = 0; l < lines.Count; l++)
            {
                output.AddRange(Text.Slice(lines[l].Start, lines[l].Length));
                if (l < lines.Count - 1 || lines[l].Break)
                    output.AddRange("\r\n"u8);
            }

            return [ .. output ];

        }

        #endregion

        #region (private) SplitLines(Text)

        /// <summary>
        /// Where the lines of the text are, and whether each ends with a line break -
        /// CRLF, LF or a lone CR.
        /// </summary>
        private static List<(Int32 Start, Int32 Length, Boolean Break)> SplitLines(ReadOnlySpan<Byte> Text)
        {

            var lines = new List<(Int32, Int32, Boolean)>();
            var start = 0;

            for (var i = 0; i < Text.Length; i++)
            {

                if (Text[i] is not ((Byte) '\r' or (Byte) '\n'))
                    continue;

                lines.Add((start, i - start, true));

                if (Text[i] == '\r' && i + 1 < Text.Length && Text[i + 1] == '\n')
                    i++;

                start = i + 1;

            }

            if (start < Text.Length || lines.Count == 0)
                lines.Add((start, Text.Length - start, false));

            return lines;

        }

        #endregion


        #region Date(Timestamp)

        /// <summary>
        /// A date as RFC 5322 §3.3 writes it: "Thu, 15 Oct 2026 10:00:00 +0200".
        /// </summary>
        public static String Date(DateTimeOffset Timestamp)
        {

            var offset = Timestamp.Offset;
            var sign   = offset < TimeSpan.Zero ? '-' : '+';
            offset     = offset.Duration();

            return Timestamp.ToString("ddd, d MMM yyyy HH:mm:ss ", CultureInfo.InvariantCulture) +
                   $"{sign}{offset.Hours:00}{offset.Minutes:00}";

        }

        #endregion

        #region NewMessageId(Domain)

        /// <summary>
        /// A new message id, without its angle brackets: a time-ordered random
        /// number at the given domain - the domain of the address the mail is from.
        /// </summary>
        public static String NewMessageId(String Domain)

            => $"{Guid.CreateVersion7():N}@{Domain.Trim().TrimEnd('.').ToLowerInvariant()}";

        #endregion

        #region NewBoundary()

        /// <summary>
        /// A new multipart boundary. It begins with "=_", which neither base64 nor
        /// quoted-printable ever writes - "=" is followed by two hex digits or a line
        /// break there - so no encoded part can hold it by chance.
        /// </summary>
        public static String NewBoundary()

            => $"=_{Guid.NewGuid():N}";

        #endregion

    }

}
