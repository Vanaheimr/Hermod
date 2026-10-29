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


    /// <summary>
    /// One link-value of a Link header field (RFC 8288, Section 3). It is a link
    /// from the link context to a target, with one or more relation types and
    /// target attributes. The context is the response's own URL unless an anchor
    /// names another (Section 3.2).
    ///
    /// Constructing one checks what a sender must get right. The target and the
    /// anchor are URI references. Every relation type is a registered one
    /// (lower-case letters, digits, "." and "-") or an absolute URI
    /// (Section 3.3). The language tags have the shape of language tags, and
    /// <c>type</c> is a media type. Parsing, in <see cref="HTTPLinks"/>, does not
    /// check these: Appendix B's algorithms take what they are given.
    ///
    /// Relation types compare case-insensitively (Sections 2.1.1 and 2.1.2) and
    /// are kept in lower case, as Appendix B keeps them.
    /// </summary>
    public sealed class HTTPLink
    {

        #region Data

        /// <summary>
        /// The link-params Section 3 defines for the link itself and for the
        /// target attributes of this serialisation. An extension attribute cannot
        /// have one of these names. "rev", deprecated by Section 3.3, is treated
        /// as an extension attribute, as Appendix B treats it.
        /// </summary>
        public static readonly String[] DefinedParameters = ["rel", "anchor", "hreflang", "media", "title", "type"];

        #endregion

        #region Properties

        /// <summary>
        /// The link target, a URI reference as sent. A relative one is relative
        /// to the URL of the response (Section 3.1).
        /// </summary>
        public String                                       Target           { get; }

        /// <summary>
        /// The relation types, in lower case. At least one.
        /// </summary>
        public IReadOnlyList<String>                        Relations        { get; }

        /// <summary>
        /// Another link context than the response's URL, as a URI reference.
        /// </summary>
        public String?                                      Anchor           { get; }

        /// <summary>
        /// The languages the target is available in (Section 3.4.1).
        /// </summary>
        public IReadOnlyList<String>                        HrefLang         { get; }

        /// <summary>
        /// The media the target is intended for, a media query list.
        /// </summary>
        public String?                                      Media            { get; }

        /// <summary>
        /// A human-readable label for the target.
        /// </summary>
        public String?                                      Title            { get; }

        /// <summary>
        /// The language of the title, when it names one.
        /// </summary>
        public String?                                      TitleLanguage    { get; }

        /// <summary>
        /// A hint at the media type of the target.
        /// </summary>
        public String?                                      Type             { get; }

        /// <summary>
        /// The extension target attributes (Section 3.4.2), with lower-case
        /// names, in the order they were given.
        /// </summary>
        public IReadOnlyList<KeyValuePair<String, String>>  Attributes       { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a link.
        /// </summary>
        /// <param name="Target">The link target, a URI reference.</param>
        /// <param name="Relation">One or more relation types, separated by spaces, e.g. "next" or "start http://example.net/relation/other".</param>
        /// <param name="Anchor">Another link context than the response's URL, a URI reference.</param>
        /// <param name="HrefLang">The languages the target is available in.</param>
        /// <param name="Media">The media the target is intended for.</param>
        /// <param name="Title">A human-readable label for the target.</param>
        /// <param name="TitleLanguage">The language of the title.</param>
        /// <param name="Type">A hint at the media type of the target, e.g. "text/html".</param>
        /// <param name="Attributes">Extension target attributes.</param>
        public HTTPLink(String                                        Target,
                        String                                        Relation,
                        String?                                       Anchor          = null,
                        IEnumerable<String>?                          HrefLang        = null,
                        String?                                       Media           = null,
                        String?                                       Title           = null,
                        String?                                       TitleLanguage   = null,
                        String?                                       Type            = null,
                        IEnumerable<KeyValuePair<String, String>>?    Attributes      = null)
        {

            if (!IsURIReference(Target))
                throw new ArgumentException($"'{Target}' is not a URI reference.", nameof(Target));

            var relations = SplitRelations(Relation);

            if (relations.Length == 0)
                throw new ArgumentException("A link has at least one relation type (RFC 8288, Section 3.3).", nameof(Relation));

            foreach (var relation in relations)
                if (!IsRelationType(relation))
                    throw new ArgumentException($"'{relation}' is neither a registered relation type nor an absolute URI (RFC 8288, Section 3.3).", nameof(Relation));

            if (Anchor is not null && !IsURIReference(Anchor))
                throw new ArgumentException($"'{Anchor}' is not a URI reference.", nameof(Anchor));

            var hrefLang = HrefLang?.ToArray() ?? [];

            foreach (var language in hrefLang)
                if (!HTTPExtendedValue.IsLanguageTag(language))
                    throw new ArgumentException($"'{language}' is not a language tag.", nameof(HrefLang));

            if (Media is not null && !Media.All(character => character is >= ' ' and <= '~'))
                throw new ArgumentException("A media query list is printable ASCII.", nameof(Media));

            if (TitleLanguage is not null)
            {

                if (Title is null)
                    throw new ArgumentException("A title language needs a title.", nameof(TitleLanguage));

                if (!HTTPExtendedValue.IsLanguageTag(TitleLanguage))
                    throw new ArgumentException($"'{TitleLanguage}' is not a language tag.", nameof(TitleLanguage));

            }

            if (Type is not null && !IsMediaType(Type))
                throw new ArgumentException($"'{Type}' is not a media type.", nameof(Type));

            var attributes = new List<KeyValuePair<String, String>>();

            foreach (var attribute in Attributes ?? [])
            {

                var name = attribute.Key.ToLowerInvariant();

                // Section 2.2: a token, and better without "%", "'" or "*".
                if (name.Length == 0 || !name.All(IsTokenChar) || name.IndexOfAny(['%', '\'', '*']) >= 0)
                    throw new ArgumentException($"'{attribute.Key}' is not a target attribute name.", nameof(Attributes));

                if (DefinedParameters.Contains(name))
                    throw new ArgumentException($"'{name}' is a link-param RFC 8288 Section 3 defines, not an extension attribute.", nameof(Attributes));

                attributes.Add(new (name, attribute.Value));

            }

            this.Target         = Target;
            this.Relations      = relations;
            this.Anchor         = Anchor;
            this.HrefLang       = hrefLang;
            this.Media          = Media;
            this.Title          = Title;
            this.TitleLanguage  = TitleLanguage;
            this.Type           = Type;
            this.Attributes     = attributes;

        }

        /// <summary>
        /// A parsed link, taken as it came.
        /// </summary>
        private HTTPLink(String                                     Target,
                         String[]                                   Relations,
                         String?                                    Anchor,
                         String[]                                   HrefLang,
                         String?                                    Media,
                         String?                                    Title,
                         String?                                    TitleLanguage,
                         String?                                    Type,
                         List<KeyValuePair<String, String>>         Attributes)
        {

            this.Target         = Target;
            this.Relations      = Relations;
            this.Anchor         = Anchor;
            this.HrefLang       = HrefLang;
            this.Media          = Media;
            this.Title          = Title;
            this.TitleLanguage  = TitleLanguage;
            this.Type           = Type;
            this.Attributes     = Attributes;

        }

        internal static HTTPLink Parsed(String                                  Target,
                                        String[]                                Relations,
                                        String?                                 Anchor,
                                        String[]                                HrefLang,
                                        String?                                 Media,
                                        String?                                 Title,
                                        String?                                 TitleLanguage,
                                        String?                                 Type,
                                        List<KeyValuePair<String, String>>      Attributes)

            => new (Target, Relations, Anchor, HrefLang, Media, Title, TitleLanguage, Type, Attributes);

        #endregion


        #region HasRelation(Relation)

        /// <summary>
        /// Whether the link has this relation type, compared case-insensitively.
        /// </summary>
        public Boolean HasRelation(String Relation)

            => Relations.Contains(Relation, StringComparer.OrdinalIgnoreCase);

        #endregion

        #region (static) SplitRelations(Text)

        /// <summary>
        /// The relation types of a rel value, split on whitespace and in lower case.
        /// </summary>
        internal static String[] SplitRelations(String Text)

            => Text.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).
                    Select(relation => relation.ToLowerInvariant()).
                    ToArray();

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

        // The characters a URI reference is made of (RFC 3986, Section 2):
        // unreserved, reserved and the "%" of a percent-encoding.
        private static Boolean IsURIReference(String Text)

            => Text.All(character => character is >= 'a' and <= 'z'
                                               or >= 'A' and <= 'Z'
                                               or >= '0' and <= '9'
                                               or '-' or '.' or '_' or '~'
                                               or ':' or '/' or '?' or '#' or '[' or ']' or '@'
                                               or '!' or '$' or '&' or '\'' or '(' or ')'
                                               or '*' or '+' or ',' or ';' or '='
                                               or '%');

        // reg-rel-type = LOALPHA *( LOALPHA / DIGIT / "." / "-" ), or an absolute URI.
        private static Boolean IsRelationType(String Relation)
        {

            if (Relation.Length > 0 &&
                Relation[0] is >= 'a' and <= 'z' &&
                Relation.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '-'))
            {
                return true;
            }

            // A scheme of its own (RFC 3986, Section 3.1), checked here rather
            // than by Uri.TryCreate, which on Unix takes "/relative" for an
            // absolute file path.
            var colon = Relation.IndexOf(':');

            return colon > 0 &&
                   Char.IsAsciiLetter(Relation[0]) &&
                   Relation[..colon].All(character => Char.IsAsciiLetterOrDigit(character) || character is '+' or '-' or '.') &&
                   IsURIReference(Relation);

        }

        // type-name "/" subtype-name (RFC 6838, Section 4.2).
        private static Boolean IsMediaType(String Text)
        {

            var slash = Text.IndexOf('/');

            return slash > 0 &&
                   IsToken(Text[..slash]) &&
                   IsToken(Text[(slash + 1)..]);

        }

        // Printable ASCII, which a quoted-string can carry once '"' and '\' are escaped.
        private static Boolean IsQuotable(String Text)

            => Text.All(character => character is >= ' ' and <= '~');

        private static String Quote(String Text)

            => "\"" + Text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        #endregion


        #region ToString()

        /// <summary>
        /// The link-value. "rel", "anchor", "title" and "media" are quoted and
        /// "hreflang" is a token, the forms Section 3 calls the most
        /// interoperable. A title that is not printable ASCII, or that has a
        /// language, goes out as "title*" (RFC 8187). An extension attribute goes
        /// out as a token, a quoted-string, or with an asterisk and RFC 8187
        /// encoding, whichever its value needs.
        /// </summary>
        public override String ToString()
        {

            var text = new StringBuilder();

            text.Append('<').Append(Target).Append('>');
            text.Append("; rel=").Append(Quote(String.Join(' ', Relations)));

            if (Anchor is not null)
                text.Append("; anchor=").Append(Quote(Anchor));

            foreach (var language in HrefLang)
                text.Append("; hreflang=").Append(language);

            if (Media is not null)
                text.Append("; media=").Append(Quote(Media));

            if (Title is not null)
            {
                if (TitleLanguage is null && IsQuotable(Title))
                    text.Append("; title=").Append(Quote(Title));
                else
                    text.Append("; title*=").Append(HTTPExtendedValue.Encode(Title, TitleLanguage));
            }

            if (Type is not null)
                text.Append("; type=").Append(Quote(Type));

            foreach (var attribute in Attributes)
            {

                if (attribute.Value.Length == 0)
                    text.Append("; ").Append(attribute.Key);

                else if (IsToken(attribute.Value))
                    text.Append("; ").Append(attribute.Key).Append('=').Append(attribute.Value);

                else if (IsQuotable(attribute.Value))
                    text.Append("; ").Append(attribute.Key).Append('=').Append(Quote(attribute.Value));

                else
                    text.Append("; ").Append(attribute.Key).Append("*=").Append(HTTPExtendedValue.Encode(attribute.Value));

            }

            return text.ToString();

        }

        #endregion

    }

}
