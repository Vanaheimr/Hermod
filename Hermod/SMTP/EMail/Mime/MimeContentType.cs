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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// The parameters of a structured MIME header - Content-Type, Content-Disposition
    /// - as RFC 2045 §5.1 writes them, with RFC 2231 on top: continuations
    /// (name*0, name*1, ...), and values with a charset and percent-encoded octets
    /// (filename*=UTF-8''Gr%C3%BC%C3%9Fe.pdf).
    /// </summary>
    /// <remarks>
    /// Names are case-insensitive and given back in lower case. Where a value comes
    /// both plain and as RFC 2231 (filename and filename*), the RFC 2231 one wins.
    /// An RFC 2047 encoded word in a plain value - forbidden there, and how a good
    /// part of the world's mail programs name their attachments - is decoded too.
    /// </remarks>
    public static class MimeParameters
    {

        #region Parse(Value, out Parameters)

        /// <summary>
        /// Split a structured header value into what comes before its first
        /// semicolon and its parameters.
        /// </summary>
        /// <param name="Value">The header value, e.g. "text/plain; charset=utf-8".</param>
        /// <param name="Parameters">The parameters, names in lower case.</param>
        /// <returns>The part before the parameters, trimmed, comments removed.</returns>
        public static String Parse(String                                   Value,
                                   out IReadOnlyDictionary<String, String>  Parameters)
        {

            var pieces      = SplitTopLevel(Value);
            var head        = WithoutComments(pieces.Count > 0 ? pieces[0] : "").Trim();

            var plain       = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            var extended    = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            var sections    = new Dictionary<String, SortedDictionary<Int32, (String Value, Boolean Encoded)>>(StringComparer.OrdinalIgnoreCase);

            foreach (var piece in pieces.Skip(1))
            {

                var equals = piece.IndexOf('=');

                if (equals <= 0)
                    continue;

                var name   = piece[..equals].Trim().ToLowerInvariant();
                var value  = Unquote(piece[(equals + 1)..].Trim());

                if (name.Length == 0)
                    continue;

                var star   = name.IndexOf('*');

                // name*=charset'language'value
                if (star == name.Length - 1)
                {
                    extended[name[..^1]] = value;
                    continue;
                }

                // name*0, name*1*, ...
                if (star > 0)
                {

                    var baseName  = name[..star];
                    var index     = name[(star + 1)..];
                    var encoded   = index.EndsWith('*');

                    if (encoded)
                        index = index[..^1];

                    if (Int32.TryParse(index, out var number) && number is >= 0 and < 1000)
                    {
                        if (!sections.TryGetValue(baseName, out var parts))
                            sections[baseName] = parts = [];
                        parts[number] = (value, encoded);
                    }

                    continue;

                }

                plain[name] = value;

            }

            var result = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);

            foreach (var (name, value) in plain)
                result[name] = value.Contains("=?", StringComparison.Ordinal)
                                   ? EncodedWords.Decode(value)
                                   : value;

            foreach (var (name, parts) in sections)
                result[name] = JoinSections(parts);

            foreach (var (name, value) in extended)
                result[name] = DecodeExtended(value, null);

            Parameters = result;
            return head;

        }

        #endregion


        #region (private) JoinSections(Parts) / DecodeExtended(Value, Charset)

        /// <summary>
        /// RFC 2231 §3/§4: the sections in their order - only as far as they go
        /// without a gap - the encoded ones percent-decoded in the charset the first
        /// one names.
        /// </summary>
        private static String JoinSections(SortedDictionary<Int32, (String Value, Boolean Encoded)> Parts)
        {

            var text      = new StringBuilder();
            var octets    = new List<Byte>();
            String? charset = null;
            var expected  = 0;

            void FlushOctets()
            {
                if (octets.Count > 0)
                    text.Append(MimeCharsets.Decode([.. octets], charset));
                octets.Clear();
            }

            foreach (var (index, (value, encoded)) in Parts)
            {

                if (index != expected++)
                    break;

                if (!encoded)
                {
                    FlushOctets();
                    text.Append(value);
                    continue;
                }

                var encodedValue = value;

                if (index == 0)
                {
                    var parts = value.Split('\'', 3);
                    if (parts.Length == 3)
                    {
                        charset       = parts[0];
                        encodedValue  = parts[2];
                    }
                }

                octets.AddRange(PercentDecode(encodedValue));

            }

            FlushOctets();

            return text.ToString();

        }

        /// <summary>
        /// RFC 2231 §4: charset'language'percent-encoded-octets.
        /// </summary>
        private static String DecodeExtended(String Value, String? Charset)
        {

            var parts = Value.Split('\'', 3);

            return parts.Length == 3
                       ? MimeCharsets.Decode(PercentDecode(parts[2]), parts[0])
                       : MimeCharsets.Decode(PercentDecode(Value),    Charset);

        }

        private static Byte[] PercentDecode(String Text)
        {

            var octets = new List<Byte>(Text.Length);

            for (var i = 0; i < Text.Length; i++)
            {

                if (Text[i] == '%' && i + 2 < Text.Length && Uri.IsHexDigit(Text[i + 1]) && Uri.IsHexDigit(Text[i + 2]))
                {
                    octets.Add(Convert.ToByte(Text.Substring(i + 1, 2), 16));
                    i += 2;
                }

                else if (Text[i] > 0x7F)
                    octets.AddRange(Encoding.UTF8.GetBytes(Text[i].ToString()));

                else
                    octets.Add((Byte) Text[i]);

            }

            return [.. octets];

        }

        #endregion

        #region (private) SplitTopLevel(Value) / Unquote(Value) / WithoutComments(Value)

        /// <summary>
        /// Split at the semicolons that separate parameters - not those inside a
        /// quoted string or a comment.
        /// </summary>
        private static List<String> SplitTopLevel(String Value)
        {

            var pieces   = new List<String>();
            var current  = new StringBuilder();
            var inQuote  = false;
            var depth    = 0;

            for (var i = 0; i < Value.Length; i++)
            {

                var c = Value[i];

                if (inQuote)
                {
                    current.Append(c);
                    if (c == '\\' && i + 1 < Value.Length) current.Append(Value[++i]);
                    else if (c == '"') inQuote = false;
                    continue;
                }

                switch (c)
                {
                    case '"': inQuote = true; current.Append(c); break;
                    case '(': depth++;        current.Append(c); break;
                    case ')': if (depth > 0) depth--; current.Append(c); break;
                    case ';' when depth == 0:
                        pieces.Add(current.ToString());
                        current.Clear();
                        break;
                    default:  current.Append(c); break;
                }

            }

            pieces.Add(current.ToString());

            return pieces;

        }

        private static String Unquote(String Value)
        {

            var value = WithoutComments(Value).Trim();

            if (value.Length < 2 || value[0] != '"')
                return value;

            var result = new StringBuilder(value.Length);

            for (var i = 1; i < value.Length; i++)
            {
                if (value[i] == '\\' && i + 1 < value.Length) result.Append(value[++i]);
                else if (value[i] == '"') break;
                else result.Append(value[i]);
            }

            return result.ToString();

        }

        private static String WithoutComments(String Value)
        {

            if (!Value.Contains('('))
                return Value;

            var result   = new StringBuilder(Value.Length);
            var inQuote  = false;
            var depth    = 0;

            for (var i = 0; i < Value.Length; i++)
            {

                var c = Value[i];

                if (inQuote)
                {
                    result.Append(c);
                    if (c == '\\' && i + 1 < Value.Length) result.Append(Value[++i]);
                    else if (c == '"') inQuote = false;
                    continue;
                }

                if (depth > 0)
                {
                    if (c == '(') depth++;
                    else if (c == ')') depth--;
                    continue;
                }

                if (c == '"') { inQuote = true; result.Append(c); }
                else if (c == '(') depth++;
                else result.Append(c);

            }

            return result.ToString();

        }

        #endregion

    }


    /// <summary>
    /// A Content-Type (RFC 2045 §5): the media type, its subtype and its parameters.
    /// </summary>
    public sealed class MimeContentType
    {

        #region Properties

        /// <summary>
        /// The top-level media type, lower case: "text", "multipart", "image", ...
        /// </summary>
        public String                               MediaType     { get; }

        /// <summary>
        /// The subtype, lower case: "plain", "html", "mixed", ...
        /// </summary>
        public String                               SubType       { get; }

        /// <summary>
        /// The parameters, names in lower case.
        /// </summary>
        public IReadOnlyDictionary<String, String>  Parameters    { get; }

        /// <summary>
        /// "type/subtype", lower case.
        /// </summary>
        public String                               MimeType
            => $"{MediaType}/{SubType}";

        /// <summary>
        /// The boundary of a multipart, or null.
        /// </summary>
        public String?                              Boundary
            => Parameters.GetValueOrDefault("boundary");

        /// <summary>
        /// The charset, or null.
        /// </summary>
        public String?                              Charset
            => Parameters.GetValueOrDefault("charset");

        /// <summary>
        /// The name parameter - the file name before Content-Disposition existed - or null.
        /// </summary>
        public String?                              Name
            => Parameters.GetValueOrDefault("name");

        /// <summary>
        /// Whether this is a multipart.
        /// </summary>
        public Boolean                              IsMultipart
            => MediaType == "multipart";

        #endregion

        #region Constructor(s)

        public MimeContentType(String                                MediaType,
                               String                                SubType,
                               IReadOnlyDictionary<String, String>?  Parameters   = null)
        {
            this.MediaType   = MediaType.ToLowerInvariant();
            this.SubType     = SubType.  ToLowerInvariant();
            this.Parameters  = Parameters ?? new Dictionary<String, String>();
        }

        #endregion


        #region TextPlain / MessageRFC822

        /// <summary>
        /// text/plain; charset=us-ascii - what a part without a Content-Type is (RFC 2045 §5.2).
        /// </summary>
        public static MimeContentType TextPlain
            => new ("text", "plain", new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase) { ["charset"] = "us-ascii" });

        /// <summary>
        /// message/rfc822 - what a part of a multipart/digest is without one (RFC 2046 §5.1.5).
        /// </summary>
        public static MimeContentType MessageRFC822
            => new ("message", "rfc822");

        #endregion

        #region (static) Parse(Value, Default)

        /// <summary>
        /// Read a Content-Type value - or take the default when there is none, or
        /// when it is not of the form type/subtype (RFC 2045 §5.2).
        /// </summary>
        public static MimeContentType Parse(String?          Value,
                                            MimeContentType  Default)
        {

            if (String.IsNullOrWhiteSpace(Value))
                return Default;

            var type  = MimeParameters.Parse(Value, out var parameters);
            var slash = type.IndexOf('/');

            if (slash <= 0 || slash == type.Length - 1)
                return Default;

            return new MimeContentType(type[..slash].Trim(), type[(slash + 1)..].Trim(), parameters);

        }

        #endregion

        #region Is(MimeType)

        /// <summary>
        /// Whether this is the given type - "text/html" - or of the given media type - "image/*".
        /// </summary>
        public Boolean Is(String MimeType)

            => MimeType.EndsWith("/*", StringComparison.Ordinal)
                   ? MediaType.Equals(MimeType[..^2], StringComparison.OrdinalIgnoreCase)
                   : this.MimeType.Equals(MimeType, StringComparison.OrdinalIgnoreCase);

        #endregion

        #region (override) ToString()

        public override String ToString()
            => MimeType;

        #endregion

    }


    /// <summary>
    /// A Content-Disposition (RFC 2183): inline or attachment, and its file name.
    /// </summary>
    public sealed class MimeContentDisposition
    {

        #region Properties

        /// <summary>
        /// "inline", "attachment", or what else the sender wrote, lower case.
        /// </summary>
        public String                               Disposition   { get; }

        /// <summary>
        /// The parameters, names in lower case.
        /// </summary>
        public IReadOnlyDictionary<String, String>  Parameters    { get; }

        /// <summary>
        /// The file name, decoded, or null.
        /// </summary>
        public String?                              FileName
            => Parameters.GetValueOrDefault("filename");

        /// <summary>
        /// Whether the sender meant it to be saved rather than shown.
        /// </summary>
        public Boolean                              IsAttachment
            => Disposition == "attachment";

        #endregion

        #region Constructor(s)

        private MimeContentDisposition(String                               Disposition,
                                       IReadOnlyDictionary<String, String>  Parameters)
        {
            this.Disposition  = Disposition.ToLowerInvariant();
            this.Parameters   = Parameters;
        }

        #endregion


        #region (static) Parse(Value)

        /// <summary>
        /// Read a Content-Disposition value, or null when there is none.
        /// </summary>
        public static MimeContentDisposition? Parse(String? Value)
        {

            if (String.IsNullOrWhiteSpace(Value))
                return null;

            var disposition = MimeParameters.Parse(Value, out var parameters);

            return new MimeContentDisposition(disposition.Length > 0 ? disposition : "attachment", parameters);

        }

        #endregion

    }

}
