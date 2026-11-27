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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    using System.Collections;
    using System.Diagnostics.CodeAnalysis;
    using System.Security.Cryptography;
    using System.Text;


    /// <summary>
    /// A multipart/form-data body (RFC 7578): the fields and files of a form,
    /// each a part under the name of its field, in the form's order.
    ///
    /// The framing is RFC 2046's, Section 5.1.1:
    ///
    ///    multipart-body    = [ preamble CRLF ] dash-boundary transport-padding CRLF
    ///                        body-part *encapsulation close-delimiter
    ///                        transport-padding [ CRLF epilogue ]
    ///    encapsulation     = delimiter transport-padding CRLF body-part
    ///    delimiter         = CRLF dash-boundary
    ///    close-delimiter   = delimiter "--"
    ///    dash-boundary     = "--" boundary
    ///
    /// So the CRLF in front of a delimiter belongs to the delimiter and not to
    /// the part before it, a delimiter is one only at the start of a line and
    /// followed by nothing but padding and a CRLF, and preamble and epilogue
    /// are ignored. A body that ends before its close delimiter is cut off,
    /// and refused: the last part could be a file that lost its end.
    ///
    /// Each part MUST have a Content-Disposition of type "form-data" with a
    /// "name" (RFC 7578, Section 4.2), and may have a Content-Type, which
    /// defaults to text/plain (Section 4.4). A part without either, a header
    /// line that is not one, and a header section without its blank line make
    /// the body malformed, as do more parts, or a longer part header section,
    /// than the limits allow.
    /// </summary>
    public sealed class HTTPMultipartFormData : IEnumerable<HTTPFormDataPart>
    {

        #region Data

        /// <summary>
        /// The number of parts read before a body is refused.
        /// </summary>
        public const Int32 DefaultMaxParts               = 1000;

        /// <summary>
        /// The size of a part's header section, in octets, before a body is
        /// refused.
        /// </summary>
        public const Int32 DefaultMaxPartHeaderSize      = 8192;

        /// <summary>
        /// The longest boundary RFC 2046 allows.
        /// </summary>
        public const Int32 MaxBoundaryLength             = 70;

        private static readonly Byte[]        CRLF        = "\r\n"u8.ToArray();
        private static readonly Byte[]        CRLFCRLF    = "\r\n\r\n"u8.ToArray();
        private static readonly UTF8Encoding  StrictUTF8  = new (encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        private const String BoundaryCharacters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        #endregion

        #region Properties

        /// <summary>
        /// The parts, in the order of the body.
        /// </summary>
        public IReadOnlyList<HTTPFormDataPart>  Parts          { get; }

        /// <summary>
        /// The boundary that delimits them.
        /// </summary>
        public String                           Boundary       { get; }

        /// <summary>
        /// The value of the form's "_charset_" field, the charset of its text
        /// parts that name none (RFC 7578, Section 4.6); null without one.
        /// </summary>
        public String?                          Charset        { get; }

        /// <summary>
        /// The Content-Type of this body: multipart/form-data with its boundary.
        /// </summary>
        public HTTPContentType                  ContentType
            => new ("multipart", "form-data", null, null, Boundary);

        /// <summary>
        /// The first part of the given field, or null. Field names are compared
        /// as they were sent, case included.
        /// </summary>
        /// <param name="Name">The name of a form field.</param>
        public HTTPFormDataPart? this[String Name]
            => Parts.FirstOrDefault(part => part.Name == Name);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// A body of the given parts, to be sent.
        /// </summary>
        /// <param name="Parts">The parts, in the order of the form.</param>
        /// <param name="Boundary">The boundary; a new random one when not given.</param>
        /// <exception cref="ArgumentException">The boundary is not one RFC 2046 allows, or it occurs in a part.</exception>
        public HTTPMultipartFormData(IEnumerable<HTTPFormDataPart>  Parts,
                                     String?                        Boundary   = null)
        {

            this.Parts = [.. Parts];

            if (Boundary is not null)
            {

                if (!IsValidBoundary(Boundary))
                    throw new ArgumentException($"'{Boundary}' is not a boundary RFC 2046, Section 5.1.1 allows.", nameof(Boundary));

                if (OccursIn(this.Parts, Boundary))
                    throw new ArgumentException("The boundary occurs in a part, which RFC 7578, Section 4.1 forbids.", nameof(Boundary));

                this.Boundary = Boundary;

            }

            else
            {

                do
                {
                    this.Boundary = NewBoundary();
                }
                while (OccursIn(this.Parts, this.Boundary));

            }

        }

        private HTTPMultipartFormData(IReadOnlyList<HTTPFormDataPart>  Parts,
                                      String                           Boundary,
                                      String?                          Charset)
        {
            this.Parts     = Parts;
            this.Boundary  = Boundary;
            this.Charset   = Charset;
        }

        #endregion


        #region GetAll(Name)

        /// <summary>
        /// Every part of the given field, in order: the files of a field that
        /// takes several (RFC 7578, Section 4.3), or each value of a field the
        /// form repeats.
        /// </summary>
        /// <param name="Name">The name of a form field.</param>
        public IEnumerable<HTTPFormDataPart> GetAll(String Name)

            => Parts.Where(part => part.Name == Name);

        #endregion

        #region (static) IsValidBoundary(Boundary)

        /// <summary>
        /// Whether the given text is a boundary RFC 2046, Section 5.1.1 allows:
        /// one to seventy of its bchars, the last not a space.
        ///
        ///    boundary      := 0*69&lt;bchars&gt; bcharsnospace
        ///    bchars        := bcharsnospace / " "
        ///    bcharsnospace := DIGIT / ALPHA / "'" / "(" / ")" / "+" / "_"
        ///                     / "," / "-" / "." / "/" / ":" / "=" / "?"
        /// </summary>
        /// <param name="Boundary">The boundary.</param>
        public static Boolean IsValidBoundary(String? Boundary)

            => Boundary is { Length: > 0 and <= MaxBoundaryLength } &&
               Boundary[^1] != ' ' &&
               Boundary.All(character => character is >= '0' and <= '9'
                                                   or >= 'A' and <= 'Z'
                                                   or >= 'a' and <= 'z'
                                                   or '\'' or '(' or ')' or '+' or '_' or ','
                                                   or '-' or '.' or '/' or ':' or '=' or '?' or ' ');

        #endregion

        #region (static) NewBoundary()

        /// <summary>
        /// A new random boundary: 24 dashes and 24 random letters and digits, as
        /// unlikely to occur in content as 143 random bits make it.
        /// </summary>
        public static String NewBoundary()

            => String.Concat(new String('-', 24),
                             RandomNumberGenerator.GetString(BoundaryCharacters, 24));

        #endregion


        #region ToBytes()

        /// <summary>
        /// The body, as it is sent with <see cref="ContentType"/>.
        ///
        /// A field name and a file name go into a quoted string with a quotation
        /// mark, a CR and an LF percent-encoded, which is what browsers do
        /// (WHATWG HTML, "multipart/form-data encoding algorithm") and what
        /// RFC 7578, Section 5.1.3 describes; a backslash is written as it is,
        /// as browsers write it.
        /// </summary>
        public Byte[] ToBytes()
        {

            using var body = new MemoryStream();

            void Write(String Text)
                => body.Write(Encoding.UTF8.GetBytes(Text));

            foreach (var part in Parts)
            {

                Write($"--{Boundary}\r\n");
                Write($"Content-Disposition: form-data; name=\"{Escape(part.Name)}\"");

                if (part.FileName is not null)
                    Write($"; filename=\"{Escape(part.FileName)}\"");

                Write("\r\n");

                if (part.ContentType is not null)
                    Write($"Content-Type: {part.ContentType}\r\n");

                Write("\r\n");
                body.Write(part.Content);
                Write("\r\n");

            }

            Write($"--{Boundary}--\r\n");

            return body.ToArray();

        }

        private static String Escape(String Text)

            => Text.Replace("\"", "%22").
                    Replace("\r", "%0D").
                    Replace("\n", "%0A");

        #endregion


        #region (static) TryParse(Body, Boundary, out FormData, out Error, MaxParts = DefaultMaxParts, MaxPartHeaderSize = DefaultMaxPartHeaderSize)

        /// <summary>
        /// Read a multipart/form-data body, delimited by the given boundary, the
        /// "boundary" parameter of its Content-Type.
        /// </summary>
        /// <param name="Body">The body.</param>
        /// <param name="Boundary">The boundary.</param>
        /// <param name="FormData">The form data.</param>
        /// <param name="Error">Why the body is malformed, when it is.</param>
        /// <param name="MaxParts">The number of parts read before the body is refused.</param>
        /// <param name="MaxPartHeaderSize">The size of a part's header section, in octets, before the body is refused.</param>
        public static Boolean TryParse(ReadOnlySpan<Byte>                              Body,
                                       String?                                         Boundary,
                                       [NotNullWhen(true)]  out HTTPMultipartFormData?  FormData,
                                       [NotNullWhen(false)] out String?                 Error,
                                       Int32                                           MaxParts            = DefaultMaxParts,
                                       Int32                                           MaxPartHeaderSize   = DefaultMaxPartHeaderSize)
        {

            FormData = null;

            if (!IsValidBoundary(Boundary))
            {
                Error = "The boundary is not one RFC 2046, Section 5.1.1 allows.";
                return false;
            }

            var dashBoundary  = Encoding.ASCII.GetBytes($"--{Boundary}");
            var delimiter     = Encoding.ASCII.GetBytes($"\r\n--{Boundary}");

            #region The first delimiter: at the start of the body, or of a line after the preamble

            var  position  = 0;
            var  found     = false;
            Boolean isClose = false;

            while (true)
            {

                var at = Body[position..].IndexOf(dashBoundary);

                if (at < 0)
                    break;

                at += position;

                if ((at == 0 || (at >= 2 && Body[at - 2] == '\r' && Body[at - 1] == '\n')) &&
                    TryMatchDelimiterLine(Body, at + dashBoundary.Length, out var next, out isClose))
                {
                    position = next;
                    found    = true;
                    break;
                }

                position = at + 1;

            }

            if (!found)
            {
                Error = "The body has no delimiter.";
                return false;
            }

            #endregion

            var parts = new List<(String Name, String? FileName, HTTPContentType? ContentType, List<KeyValuePair<String, String>> Headers, Byte[] Content)>();

            while (!isClose)
            {

                #region The part up to the next delimiter

                var partStart = position;
                var end       = -1;

                while (true)
                {

                    var at = Body[position..].IndexOf(delimiter);

                    if (at < 0)
                        break;

                    at += position;

                    if (TryMatchDelimiterLine(Body, at + delimiter.Length, out var next, out isClose))
                    {
                        end      = at;
                        position = next;
                        break;
                    }

                    position = at + 1;

                }

                if (end < 0)
                {
                    Error = "The body ends before its close delimiter.";
                    return false;
                }

                if (parts.Count == MaxParts)
                {
                    Error = $"The body has more than {MaxParts} parts.";
                    return false;
                }

                #endregion

                if (!TryParsePart(Body[partStart..end], MaxPartHeaderSize, out var part, out Error))
                {
                    Error = $"Part {parts.Count + 1}: {Error}";
                    return false;
                }

                parts.Add(part);

            }

            #region The form's charset (RFC 7578, Section 4.6)

            String? charset = null;

            foreach (var part in parts)
            {
                if (part.Name == "_charset_" && part.FileName is null)
                {
                    charset = Encoding.ASCII.GetString(part.Content).Trim();
                    break;
                }
            }

            #endregion

            FormData = new HTTPMultipartFormData(
                           [.. parts.Select(part => new HTTPFormDataPart(
                                                        part.Name,
                                                        part.FileName,
                                                        part.ContentType,
                                                        part.Headers,
                                                        part.Content,
                                                        part.ContentType?.CharSet ?? (charset is { Length: > 0 } ? charset : "utf-8")
                                                    ))],
                           Boundary!,
                           charset
                       );

            Error = null;
            return true;

        }

        #endregion

        #region (private static) TryMatchDelimiterLine(Body, Position, out Next, out IsClose)

        /// <summary>
        /// Whether what follows a dash-boundary at the given position makes it a
        /// delimiter: "--" for the close delimiter, then transport padding, then
        /// a CRLF, which the close delimiter may also go without at the end of
        /// the body. Anything else, as in "--boundaryX", is content.
        /// </summary>
        private static Boolean TryMatchDelimiterLine(ReadOnlySpan<Byte>  Body,
                                                     Int32               Position,
                                                     out Int32           Next,
                                                     out Boolean         IsClose)
        {

            Next     = Position;
            IsClose  = Body[Position..].StartsWith("--"u8);

            if (IsClose)
                Next += 2;

            // transport-padding := *LWSP-char (RFC 2046, Section 5.1.1), which
            // receivers MUST be able to handle.
            while (Next < Body.Length && Body[Next] is (Byte) ' ' or (Byte) '\t')
                Next++;

            if (Body[Next..].StartsWith(CRLF))
            {
                Next += 2;
                return true;
            }

            return IsClose && Next == Body.Length;

        }

        #endregion

        #region (private static) TryParsePart(Part, MaxHeaderSize, out Result, out Error)

        private static Boolean TryParsePart(ReadOnlySpan<Byte>                                                                                                   Part,
                                            Int32                                                                                                                MaxHeaderSize,
                                            out (String Name, String? FileName, HTTPContentType? ContentType, List<KeyValuePair<String, String>> Headers, Byte[] Content)  Result,
                                            [NotNullWhen(false)] out String?                                                                                     Error)
        {

            Result = default;

            // A part with no header lines starts with the CRLF that ends its
            // header section (RFC 2046, Section 5.1.1); RFC 7578 then finds no
            // Content-Disposition in it.
            var headerEnd = Part.StartsWith(CRLF)
                                ? 0
                                : Part.IndexOf(CRLFCRLF);

            if (headerEnd < 0)
            {
                Error = Part.Length > MaxHeaderSize
                            ? $"its header section is longer than {MaxHeaderSize} octets."
                            : "its header section has no end.";
                return false;
            }

            if (headerEnd > MaxHeaderSize)
            {
                Error = $"its header section is longer than {MaxHeaderSize} octets.";
                return false;
            }

            var content = headerEnd == 0
                              ? Part[2..]
                              : Part[(headerEnd + 4)..];

            String headerText;

            try
            {
                headerText = StrictUTF8.GetString(Part[..headerEnd]);
            }
            catch (DecoderFallbackException)
            {
                // Not UTF-8: one character per octet, as HTTP reads a field.
                headerText = Encoding.Latin1.GetString(Part[..headerEnd]);
            }

            #region Header lines, an obs-fold unfolded

            var lines = new List<String>();

            if (headerText.Length > 0)
            {
                foreach (var line in headerText.Split("\r\n"))
                {

                    if (line.Contains('\r') || line.Contains('\n'))
                    {
                        Error = "a header line holds a bare CR or LF.";
                        return false;
                    }

                    if (line.Length > 0 && line[0] is ' ' or '\t')
                    {

                        if (lines.Count == 0)
                        {
                            Error = "its header section begins with a continuation line.";
                            return false;
                        }

                        lines[^1] = $"{lines[^1]} {line.Trim(' ', '\t')}";

                    }

                    else
                        lines.Add(line);

                }
            }

            #endregion

            var     headers      = new List<KeyValuePair<String, String>>(lines.Count);
            String? disposition  = null;
            String? contentType  = null;

            foreach (var line in lines)
            {

                var colon = line.IndexOf(':');

                if (colon <= 0 || !line[..colon].All(IsTokenCharacter))
                {
                    Error = "a header line is not a field.";
                    return false;
                }

                var name  = line[..colon];
                var value = line[(colon + 1)..].Trim(' ', '\t');

                headers.Add(new (name, value));

                if (name.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase))
                {

                    if (disposition is not null)
                    {
                        Error = "it has two Content-Disposition fields.";
                        return false;
                    }

                    disposition = value;

                }

                else if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                {

                    if (contentType is not null)
                    {
                        Error = "it has two Content-Type fields.";
                        return false;
                    }

                    contentType = value;

                }

            }

            if (disposition is null)
            {
                Error = "it has no Content-Disposition, which RFC 7578, Section 4.2 requires.";
                return false;
            }

            if (!TryParseDisposition(disposition, out var fieldName, out var fileName))
            {
                Error = "its Content-Disposition is not form-data with a name.";
                return false;
            }

            HTTPContentType? parsedContentType = null;

            if (contentType is not null && !HTTPContentType.TryParse(contentType, out parsedContentType))
            {
                Error = "its Content-Type is not a media type.";
                return false;
            }

            Result = (fieldName, fileName, parsedContentType, headers, content.ToArray());
            Error  = null;
            return true;

        }

        #endregion

        #region (private static) TryParseDisposition(Text, out Name, out FileName)

        /// <summary>
        /// Read a part's Content-Disposition: the type "form-data", a "name",
        /// and perhaps a "filename" (RFC 7578, Section 4.2). Other parameters,
        /// "filename*" among them, are ignored; one given twice makes the field
        /// invalid.
        ///
        /// A quoted string is read as browsers write it: a backslash quotes the
        /// character after it only when that is a quotation mark or another
        /// backslash, and is itself otherwise, because browsers do not escape it
        /// (WHATWG HTML) and a file name "C:\x" would lose it.
        /// </summary>
        private static Boolean TryParseDisposition(String                             Text,
                                                   [NotNullWhen(true)] out String?    Name,
                                                   out String?                        FileName)
        {

            Name      = null;
            FileName  = null;

            var position = 0;

            var type = ReadToken(Text, ref position);

            if (type is null || !type.Equals("form-data", StringComparison.OrdinalIgnoreCase))
                return false;

            var names = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

            while (true)
            {

                SkipWhitespace(Text, ref position);

                if (position == Text.Length)
                    break;

                if (Text[position] != ';')
                    return false;

                position++;
                SkipWhitespace(Text, ref position);

                if (position == Text.Length)
                    break;

                var parameter = ReadToken(Text, ref position);

                if (parameter is null)
                    return false;

                SkipWhitespace(Text, ref position);

                if (position == Text.Length || Text[position] != '=')
                    return false;

                position++;
                SkipWhitespace(Text, ref position);

                var value = position < Text.Length && Text[position] == '"'
                                ? ReadQuotedString(Text, ref position)
                                : ReadToken       (Text, ref position);

                if (value is null || !names.Add(parameter))
                    return false;

                if (parameter.Equals("name", StringComparison.OrdinalIgnoreCase))
                    Name = value;

                else if (parameter.Equals("filename", StringComparison.OrdinalIgnoreCase))
                    FileName = value;

            }

            return Name is not null;

        }

        private static void SkipWhitespace(String Text, ref Int32 Position)
        {
            while (Position < Text.Length && Text[Position] is ' ' or '\t')
                Position++;
        }

        private static String? ReadToken(String Text, ref Int32 Position)
        {

            var start = Position;

            while (Position < Text.Length && IsTokenCharacter(Text[Position]))
                Position++;

            return Position > start
                       ? Text[start..Position]
                       : null;

        }

        private static String? ReadQuotedString(String Text, ref Int32 Position)
        {

            var value = new StringBuilder();

            Position++;

            while (Position < Text.Length)
            {

                var character = Text[Position++];

                if (character == '"')
                    return value.ToString();

                if (character == '\\' && Position < Text.Length && Text[Position] is '"' or '\\')
                    character = Text[Position++];

                value.Append(character);

            }

            return null;

        }

        private static Boolean IsTokenCharacter(Char Character)

            => Character is >= 'a' and <= 'z'
                         or >= 'A' and <= 'Z'
                         or >= '0' and <= '9'
                         or '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+'
                         or '-' or '.' or '^' or '_' or '`' or '|' or '~';

        #endregion

        #region (private static) OccursIn(Parts, Boundary)

        private static Boolean OccursIn(IEnumerable<HTTPFormDataPart> Parts, String Boundary)
        {

            var dashBoundary = Encoding.ASCII.GetBytes($"--{Boundary}");

            return Parts.Any(part => part.Content.AsSpan().IndexOf(dashBoundary) >= 0 ||
                                     part.Name.Contains(Boundary, StringComparison.Ordinal) ||
                                     (part.FileName?.Contains(Boundary, StringComparison.Ordinal) ?? false));

        }

        #endregion


        #region IEnumerable<HTTPFormDataPart>

        /// <summary>
        /// The parts, in order.
        /// </summary>
        public IEnumerator<HTTPFormDataPart> GetEnumerator()
            => Parts.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => Parts.GetEnumerator();

        #endregion

    }

}
