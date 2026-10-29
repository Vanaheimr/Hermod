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
    using System.Text;
    using System.Diagnostics.CodeAnalysis;


    /// <summary>
    /// The value of a Link header field (RFC 8288, Section 3): zero or more
    /// link-values, separated by commas. Repeated field lines are one value
    /// joined by commas (RFC 9110, Section 5.3), as Section 3.5's last example
    /// says.
    ///
    /// Parsing follows Appendix B, which is permissive by design:
    ///
    ///   * a link-value without "rel" is no link (Section 3.3: "MUST be
    ///     present");
    ///   * only the first "rel", "anchor", "media", "title", "title*" and "type"
    ///     of a link-value count (Sections 3.2–3.4.1);
    ///   * a token and a quoted-string are the same value (Section 3);
    ///   * "title*" and any other "name*" are RFC 8187 values, and replace the
    ///     plain parameter of the same name. One that does not decode is
    ///     dropped, and the plain one stands (Section 3.4.2);
    ///   * parsing stops at the first thing that is not a link-value, and keeps
    ///     the links before it (B.2, step 2.2).
    ///
    /// One deviation, on purpose: B.3 stops at the comma between two
    /// link-values without consuming it, and B.2 then finds no "&lt;" and
    /// returns. Read literally, that ends every field after its first
    /// link-value, and contradicts Section 3.5, whose comma-separated example is
    /// "equivalent" to two field lines. The comma is consumed here.
    /// </summary>
    public sealed class HTTPLinks : IEnumerable<HTTPLink>
    {

        #region Data

        private readonly HTTPLink[] links;

        #endregion

        #region Properties

        /// <summary>
        /// The number of link-values.
        /// </summary>
        public Int32 Count
            => links.Length;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// A Link field value with the given links.
        /// </summary>
        /// <param name="Links">The links, in the order they are to be sent.</param>
        public HTTPLinks(params HTTPLink[] Links)
        {
            this.links = [.. Links];
        }

        /// <summary>
        /// A Link field value with the given links.
        /// </summary>
        /// <param name="Links">The links, in the order they are to be sent.</param>
        public HTTPLinks(IEnumerable<HTTPLink> Links)
        {
            this.links = [.. Links];
        }

        #endregion


        #region WithRelation(Relation)

        /// <summary>
        /// The links with this relation type, compared case-insensitively.
        /// </summary>
        public IEnumerable<HTTPLink> WithRelation(String Relation)

            => links.Where(link => link.HasRelation(Relation));

        #endregion


        #region (static) TryParse (Text, out Links)

        /// <summary>
        /// Parse a Link field value. False when it has no link in it at all.
        /// </summary>
        /// <param name="Text">The field value, with repeated field lines already joined by commas.</param>
        /// <param name="Links">The parsed links.</param>
        public static Boolean TryParse(String                              Text,
                                       [NotNullWhen(true)] out HTTPLinks?  Links)
        {

            var parsed = Parse(Text);

            Links = parsed.Count > 0
                        ? parsed
                        : null;

            return Links is not null;

        }

        #endregion

        #region (static) Parse    (Text)

        /// <summary>
        /// Parse a Link field value into the links it has, which may be none.
        /// </summary>
        /// <param name="Text">The field value, with repeated field lines already joined by commas.</param>
        public static HTTPLinks Parse(String Text)
        {

            var links     = new List<HTTPLink>();
            var position  = 0;

            while (true)
            {

                SkipWhitespace(Text, ref position);

                if (position >= Text.Length || Text[position] != '<')
                    break;

                var end = Text.IndexOf('>', position + 1);

                if (end < 0)
                    break;

                var target      = Text[(position + 1)..end];
                position        = end + 1;

                var parameters  = ParseParameters(Text, ref position);
                var link        = ToLink(target, parameters);

                if (link is not null)
                    links.Add(link);

                SkipWhitespace(Text, ref position);

                if (position < Text.Length && Text[position] == ',')
                    position++;

                else
                    break;

            }

            return new HTTPLinks(links);

        }

        #endregion

        #region (private static) ParseParameters(Text, ref Position)

        /// <summary>
        /// Appendix B.3: the parameters after a target, as lower-case names and
        /// unquoted values. Stops before a "," or at the end.
        /// </summary>
        private static List<KeyValuePair<String, String>> ParseParameters(String   Text,
                                                                         ref Int32  Position)
        {

            var parameters = new List<KeyValuePair<String, String>>();

            while (true)
            {

                SkipWhitespace(Text, ref Position);

                if (Position >= Text.Length || Text[Position] != ';')
                    return parameters;

                Position++;
                SkipWhitespace(Text, ref Position);

                var start = Position;

                while (Position < Text.Length && Text[Position] is not (' ' or '\t' or '=' or ';' or ','))
                    Position++;

                var name   = Text[start..Position].ToLowerInvariant();
                var value  = "";

                SkipWhitespace(Text, ref Position);

                if (Position < Text.Length && Text[Position] == '=')
                {

                    Position++;
                    SkipWhitespace(Text, ref Position);

                    if (Position < Text.Length && Text[Position] == '"')
                        value = ParseQuotedString(Text, ref Position);

                    else
                    {

                        start = Position;

                        while (Position < Text.Length && Text[Position] is not (';' or ','))
                            Position++;

                        value = Text[start..Position].TrimEnd(' ', '\t');

                    }

                }

                if (name.Length > 0)
                    parameters.Add(new (name, value));

            }

        }

        #endregion

        #region (private static) ParseQuotedString(Text, ref Position)

        /// <summary>
        /// Appendix B.4: a quoted-string, unquoted and unescaped. An unterminated
        /// one runs to the end.
        /// </summary>
        private static String ParseQuotedString(String    Text,
                                                 ref Int32  Position)
        {

            var output = new StringBuilder();

            Position++;

            while (Position < Text.Length)
            {

                var character = Text[Position++];

                if (character == '\\')
                {

                    if (Position >= Text.Length)
                        break;

                    output.Append(Text[Position++]);

                }

                else if (character == '"')
                    break;

                else
                    output.Append(character);

            }

            return output.ToString();

        }

        #endregion

        #region (private static) ToLink(Target, Parameters)

        /// <summary>
        /// Appendix B.2, steps 9 to 17, for one link-value.
        /// </summary>
        private static HTTPLink? ToLink(String                                    Target,
                                        List<KeyValuePair<String, String>>        Parameters)
        {

            String? FirstOf(String Name)
                => Parameters.FirstOrDefault(parameter => parameter.Key == Name) is { Key: not null } found
                       ? found.Value
                       : null;

            var rel = FirstOf("rel");

            if (rel is null)
                return null;

            var relations = HTTPLink.SplitRelations(rel);

            if (relations.Length == 0)
                return null;

            var     title          = FirstOf("title");
            String? titleLanguage  = null;

            if (FirstOf("title*") is String encodedTitle &&
                HTTPExtendedValue.TryDecode(encodedTitle, out var decodedTitle, out var language))
            {
                title          = decodedTitle;
                titleLanguage  = language;
            }

            // Extension attributes: everything Section 3 does not define. A
            // "name*" replaces every "name" when it decodes, and is dropped when
            // it does not (Appendix B.2, step 16; Section 3.4.2). Defined
            // parameters have no starred form here but "title*".
            var attributes = new List<KeyValuePair<String, String>>();

            foreach (var parameter in Parameters)
            {

                var name = parameter.Key;

                if (name.EndsWith('*'))
                    continue;

                if (HTTPLink.DefinedParameters.Contains(name))
                    continue;

                attributes.Add(parameter);

            }

            foreach (var parameter in Parameters.Where(parameter => parameter.Key.EndsWith('*')))
            {

                var name = parameter.Key[..^1];

                if (name.Length == 0 || HTTPLink.DefinedParameters.Contains(name))
                    continue;

                if (!HTTPExtendedValue.TryDecode(parameter.Value, out var decoded, out _))
                    continue;

                var at = attributes.FindIndex(attribute => attribute.Key == name);

                attributes.RemoveAll(attribute => attribute.Key == name);

                attributes.Insert(at >= 0 ? at : attributes.Count, new (name, decoded));

            }

            return HTTPLink.Parsed(
                       Target,
                       relations,
                       FirstOf("anchor"),
                       [.. Parameters.Where(parameter => parameter.Key == "hreflang").Select(parameter => parameter.Value)],
                       FirstOf("media"),
                       title,
                       titleLanguage,
                       FirstOf("type"),
                       attributes
                   );

        }

        #endregion

        #region (private static) SkipWhitespace(Text, ref Position)

        private static void SkipWhitespace(String Text, ref Int32 Position)
        {
            while (Position < Text.Length && Text[Position] is ' ' or '\t')
                Position++;
        }

        #endregion


        #region GetEnumerator()

        public IEnumerator<HTTPLink> GetEnumerator()
            => ((IEnumerable<HTTPLink>) links).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => links.GetEnumerator();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// The field value: the link-values, separated by commas.
        /// </summary>
        public override String ToString()

            => String.Join(", ", links.Select(link => link.ToString()));

        #endregion

    }

}
