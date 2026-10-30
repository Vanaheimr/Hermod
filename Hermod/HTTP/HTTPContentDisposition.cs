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

    using System.Text;
    using System.Globalization;
    using System.Diagnostics.CodeAnalysis;


    /// <summary>
    /// The value of a Content-Disposition header field (RFC 6266): whether the
    /// content is to be shown or saved, and the file name to save it under.
    ///
    /// Parsing follows Section 4.1. A value that repeats a parameter is
    /// invalid, and so is one that does not fit the grammar. "filename*" is an
    /// RFC 8187 value, and wins over "filename" when it decodes (Section 4.3);
    /// when it does not, "filename" stands. An unknown disposition type is an
    /// attachment (Section 4.2).
    ///
    /// Serialising follows Appendix D. A file name that printable ASCII can
    /// carry goes out as "filename" alone. Any other, or one with a backslash,
    /// a quote or a percent sign before two hex digits in it, goes out as
    /// "filename*" in UTF-8, after an ASCII "filename" as a fallback for
    /// recipients without RFC 8187.
    ///
    /// The file name is advisory, and Section 4.3 makes taking it at its word
    /// the recipient's risk: <see cref="FileName"/> is what was sent, and
    /// <see cref="SafeFileName"/> is what a recipient can store under.
    /// </summary>
    public sealed class HTTPContentDisposition
    {

        #region Data

        /// <summary>
        /// The parameters Section 4.3 defines. An extension parameter cannot have
        /// one of these names.
        /// </summary>
        private static readonly String[] fileNameParameters = [ "filename", "filename*" ];

        // Names with a meaning of their own on Windows, with or without an
        // extension: "nul.txt" is the null device too (Section 4.3).
        private static readonly String[] deviceNames = [
            "con", "prn", "aux", "nul",
            "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
            "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9"
        ];

        #endregion

        #region Properties

        /// <summary>
        /// The disposition type, in lower case: "inline", "attachment", or an
        /// extension's.
        /// </summary>
        public String                                       Type                { get; }

        /// <summary>
        /// Whether the content is to be shown as its media type says (Section 4.2).
        /// </summary>
        public Boolean                                      IsInline
            => Type == "inline";

        /// <summary>
        /// Whether the content is to be saved rather than shown. True for
        /// "attachment", and for any type a recipient does not know, which
        /// Section 4.2 says to treat the same way.
        /// </summary>
        public Boolean                                      IsAttachment
            => !IsInline;

        /// <summary>
        /// The file name as it was sent: "filename*" decoded where it decodes,
        /// "filename" otherwise. Advisory, and not safe to store under as it is;
        /// see <see cref="SafeFileName"/>.
        /// </summary>
        public String?                                      FileName            { get; }

        /// <summary>
        /// The language "filename*" names, if it names one. Section 4.1 expects
        /// recipients to ignore it.
        /// </summary>
        public String?                                      FileNameLanguage    { get; }

        /// <summary>
        /// The extension parameters (Section 4.4), with lower-case names, in the
        /// order they were given. A "name*" that decodes replaces "name".
        /// </summary>
        public IReadOnlyList<KeyValuePair<String, String>>  Parameters          { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// A Content-Disposition value.
        /// </summary>
        /// <param name="Type">"inline", "attachment", or an extension's disposition type, a token.</param>
        /// <param name="FileName">An optional file name, without control characters.</param>
        /// <param name="Parameters">Optional extension parameters.</param>
        public HTTPContentDisposition(String                                      Type,
                                      String?                                     FileName     = null,
                                      IEnumerable<KeyValuePair<String, String>>?  Parameters   = null)
        {

            if (!IsToken(Type))
                throw new ArgumentException($"'{Type}' is not a disposition type.", nameof(Type));

            if (FileName is not null && (FileName.Length == 0 || FileName.Any(Char.IsControl)))
                throw new ArgumentException("A file name is not empty and has no control characters.", nameof(FileName));

            var parameters = new List<KeyValuePair<String, String>>();

            foreach (var parameter in Parameters ?? [])
            {

                var name = parameter.Key.ToLowerInvariant();

                if (!IsToken(name) || name.Contains('*'))
                    throw new ArgumentException($"'{parameter.Key}' is not a parameter name.", nameof(Parameters));

                if (fileNameParameters.Contains(name))
                    throw new ArgumentException("The file name is the FileName argument, not a parameter.", nameof(Parameters));

                if (parameters.Any(known => known.Key == name))
                    throw new ArgumentException($"'{name}' is given twice, which Section 4.1 makes invalid.", nameof(Parameters));

                if (parameter.Value.Any(Char.IsControl))
                    throw new ArgumentException($"The value of '{name}' has control characters.", nameof(Parameters));

                parameters.Add(new (name, parameter.Value));

            }

            this.Type        = Type.ToLowerInvariant();
            this.FileName    = FileName;
            this.Parameters  = parameters;

        }

        private HTTPContentDisposition(String                                Type,
                                       String?                               FileName,
                                       String?                               FileNameLanguage,
                                       List<KeyValuePair<String, String>>    Parameters)
        {
            this.Type              = Type;
            this.FileName          = FileName;
            this.FileNameLanguage  = FileNameLanguage;
            this.Parameters        = Parameters;
        }

        #endregion


        #region (static) Attachment(FileName = null)

        /// <summary>
        /// Content to be saved, under this file name if one is given.
        /// </summary>
        /// <param name="FileName">An optional file name.</param>
        public static HTTPContentDisposition Attachment(String? FileName = null)

            => new ("attachment", FileName);

        #endregion

        #region (static) Inline    (FileName = null)

        /// <summary>
        /// Content to be shown, and saved under this file name if the user saves
        /// it later.
        /// </summary>
        /// <param name="FileName">An optional file name.</param>
        public static HTTPContentDisposition Inline(String? FileName = null)

            => new ("inline", FileName);

        #endregion


        #region SafeFileName

        /// <summary>
        /// The file name made fit to store under, following Section 4.3, or null
        /// when nothing usable is left:
        ///
        ///   * only the last path segment counts, after "/" or "\";
        ///   * control characters go, and so do leading and trailing whitespace;
        ///   * a character Windows does not allow in a file name, among them the
        ///     ":" of a drive-relative path such as "C:name", becomes "_";
        ///   * ".", "..", "~" and the Windows device names, with or without an
        ///     extension, are refused.
        ///
        /// What it does not do is make the extension safe: Section 4.3 leaves
        /// that to a recipient that goes by extensions, which MUST check the
        /// extension against the media type of what it received.
        /// </summary>
        public String? SafeFileName
        {
            get
            {

                if (FileName is null)
                    return null;

                var name = FileName[(FileName.LastIndexOfAny(['/', '\\']) + 1)..];

                name = new String(name.Where(character => !Char.IsControl(character)).
                                       Select(character => character is ':' or '<' or '>' or '"' or '|' or '?' or '*'
                                                               ? '_'
                                                               : character).
                                       ToArray()).
                           Trim();

                if (name.Length == 0 || name is "." or ".." or "~")
                    return null;

                var stem = name.Split('.')[0].TrimEnd().ToLowerInvariant();

                if (deviceNames.Contains(stem))
                    return null;

                return name;

            }
        }

        #endregion


        #region (static) TryParse(Text, out ContentDisposition)

        /// <summary>
        /// Parse a Content-Disposition value. False when it does not fit the
        /// grammar of Section 4.1, or repeats a parameter.
        /// </summary>
        /// <param name="Text">The field value.</param>
        /// <param name="ContentDisposition">The parsed value.</param>
        public static Boolean TryParse(String                                           Text,
                                       [NotNullWhen(true)] out HTTPContentDisposition?  ContentDisposition)
        {

            ContentDisposition = null;

            var position = 0;

            SkipWhitespace(Text, ref position);

            var type = ReadToken(Text, ref position);

            if (type is null)
                return false;

            var parameters = new List<KeyValuePair<String, String>>();

            while (true)
            {

                SkipWhitespace(Text, ref position);

                if (position == Text.Length)
                    break;

                if (Text[position] != ';')
                    return false;

                position++;
                SkipWhitespace(Text, ref position);

                var name = ReadToken(Text, ref position)?.ToLowerInvariant();

                if (name is null)
                    return false;

                SkipWhitespace(Text, ref position);

                if (position == Text.Length || Text[position] != '=')
                    return false;

                position++;
                SkipWhitespace(Text, ref position);

                // An ext-value is never quoted (RFC 8187, Section 3.2.1), and
                // its characters are token characters.
                var value = position < Text.Length && Text[position] == '"' && !name.EndsWith('*')
                                ? ReadQuotedString(Text, ref position)
                                : ReadToken(Text, ref position);

                if (value is null)
                    return false;

                if (parameters.Any(parameter => parameter.Key == name))
                    return false;

                parameters.Add(new (name, value));

            }

            String? FirstOf(String Name)
                => parameters.FirstOrDefault(parameter => parameter.Key == Name) is { Key: not null } found
                       ? found.Value
                       : null;

            var     fileName          = FirstOf("filename");
            String? fileNameLanguage  = null;

            if (FirstOf("filename*") is String encoded &&
                HTTPExtendedValue.TryDecode(encoded, out var decoded, out var language))
            {
                fileName          = decoded;
                fileNameLanguage  = language;
            }

            var extensions = parameters.Where(parameter => !fileNameParameters.Contains(parameter.Key) && !parameter.Key.EndsWith('*')).ToList();

            foreach (var parameter in parameters.Where(parameter => !fileNameParameters.Contains(parameter.Key) && parameter.Key.EndsWith('*')))
            {

                var name = parameter.Key[..^1];

                if (name.Length == 0 || !HTTPExtendedValue.TryDecode(parameter.Value, out var value, out _))
                    continue;

                var at = extensions.FindIndex(extension => extension.Key == name);

                extensions.RemoveAll(extension => extension.Key == name);
                extensions.Insert(at >= 0 ? at : extensions.Count, new (name, value));

            }

            ContentDisposition = new HTTPContentDisposition(
                                     type.ToLowerInvariant(),
                                     fileName,
                                     fileNameLanguage,
                                     extensions
                                 );

            return true;

        }

        #endregion

        #region (static) Parse   (Text)

        /// <summary>
        /// Parse a Content-Disposition value.
        /// </summary>
        /// <param name="Text">The field value.</param>
        /// <exception cref="FormatException">It does not fit the grammar of RFC 6266, Section 4.1.</exception>
        public static HTTPContentDisposition Parse(String Text)

            => TryParse(Text, out var contentDisposition)
                   ? contentDisposition
                   : throw new FormatException($"'{Text}' is not a Content-Disposition value (RFC 6266, Section 4.1).");

        #endregion


        #region (private static) Grammar

        // tchar (RFC 9110, Section 5.6.2).
        private static Boolean IsTokenChar(Char Character)

            => Character is >= 'a' and <= 'z'
                         or >= 'A' and <= 'Z'
                         or >= '0' and <= '9'
                         or '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+'
                         or '-' or '.' or '^' or '_' or '`' or '|' or '~';

        private static Boolean IsToken(String Text)

            => Text.Length > 0 && Text.All(IsTokenChar);

        private static void SkipWhitespace(String Text, ref Int32 Position)
        {
            while (Position < Text.Length && Text[Position] is ' ' or '\t')
                Position++;
        }

        private static String? ReadToken(String Text, ref Int32 Position)
        {

            var start = Position;

            while (Position < Text.Length && IsTokenChar(Text[Position]))
                Position++;

            return Position > start
                       ? Text[start..Position]
                       : null;

        }

        // A quoted-string, unquoted and unescaped; null when it does not end.
        private static String? ReadQuotedString(String Text, ref Int32 Position)
        {

            var output = new StringBuilder();

            Position++;

            while (Position < Text.Length)
            {

                var character = Text[Position++];

                if (character == '"')
                    return output.ToString();

                if (character == '\\')
                {

                    if (Position == Text.Length)
                        return null;

                    character = Text[Position++];

                }

                // qdtext and quoted-pair: HTAB, SP, VCHAR and obs-text.
                if (character != '\t' && (character < ' ' || character == '\x7F'))
                    return null;

                output.Append(character);

            }

            return null;

        }

        // Printable ASCII that a quoted-string carries without a backslash, and
        // no percent sign before two hex digits, which some recipients decode
        // and others do not (Appendix D).
        private static Boolean IsPlainFileName(String Text)
        {

            for (var i = 0; i < Text.Length; i++)
            {

                var character = Text[i];

                if (character is < ' ' or > '~' or '"' or '\\')
                    return false;

                if (character == '%' &&
                    i + 2 < Text.Length &&
                    Uri.IsHexDigit(Text[i + 1]) &&
                    Uri.IsHexDigit(Text[i + 2]))
                {
                    return false;
                }

            }

            return true;

        }

        // An ASCII stand-in for a file name that needs filename* (Appendix D):
        // the German letters spelt out, other accents dropped, and what is left
        // that cannot go into a plain filename replaced with "_".
        private static String ASCIIFallback(String Text)
        {

            var spelt = Text.Replace("Ä", "Ae").Replace("Ö", "Oe").Replace("Ü", "Ue").
                             Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").
                             Replace("ß", "ss");

            var output = new StringBuilder();

            foreach (var character in spelt.Normalize(NormalizationForm.FormD))
            {

                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                    continue;

                output.Append(character is < ' ' or > '~' or '"' or '\\' ? '_' : character);

            }

            var fallback = output.ToString();

            for (var i = fallback.IndexOf('%'); i >= 0; i = fallback.IndexOf('%', i + 1))
                if (i + 2 < fallback.Length && Uri.IsHexDigit(fallback[i + 1]) && Uri.IsHexDigit(fallback[i + 2]))
                    fallback = fallback[..i] + "_" + fallback[(i + 1)..];

            return fallback;

        }

        private static String Quote(String Text)

            => "\"" + Text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        #endregion


        #region (override) ToString()

        /// <summary>
        /// The field value, following Appendix D: "filename" alone where it is
        /// enough, and otherwise an ASCII "filename" first and "filename*" in
        /// UTF-8 after it.
        /// </summary>
        public override String ToString()
        {

            var text = new StringBuilder(Type);

            if (FileName is not null)
            {

                if (IsPlainFileName(FileName))
                    text.Append("; filename=").Append(IsToken(FileName) ? FileName : Quote(FileName));

                else
                    text.Append("; filename=").Append(Quote(ASCIIFallback(FileName))).
                         Append("; filename*=").Append(HTTPExtendedValue.Encode(FileName, FileNameLanguage));

            }

            foreach (var parameter in Parameters)
            {

                if (IsToken(parameter.Value))
                    text.Append("; ").Append(parameter.Key).Append('=').Append(parameter.Value);

                else if (parameter.Value.All(character => character is >= ' ' and <= '~'))
                    text.Append("; ").Append(parameter.Key).Append('=').Append(Quote(parameter.Value));

                else
                    text.Append("; ").Append(parameter.Key).Append("*=").Append(HTTPExtendedValue.Encode(parameter.Value));

            }

            return text.ToString();

        }

        #endregion

    }

}
