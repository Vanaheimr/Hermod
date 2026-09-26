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
    using System.Diagnostics.CodeAnalysis;

    using org.GraphDefined.Vanaheimr.Illias;

    /// <summary>
    /// One element of the Forwarded field (RFC 7239), which is what the
    /// X-Forwarded-For / X-Forwarded-Host / X-Forwarded-Proto family became
    /// once it was standardised:
    ///
    ///    Forwarded         = 1#forwarded-element
    ///    forwarded-element = [ forwarded-pair ] *( ";" [ forwarded-pair ] )
    ///    forwarded-pair    = token "=" value
    ///    value             = token / quoted-string
    ///
    /// One element per proxy, in the order they were traversed, so that the
    /// four facts a proxy hides - who connected to it, which of its own
    /// interfaces received the request, which host was asked for, and over
    /// which protocol - stay together per hop instead of being spread over
    /// four independent lists that can differ in length.
    ///
    /// Parsing is quote-aware, which the grammar requires rather than merely
    /// rewards: a value is a token *or a quoted string*, and a quoted one may
    /// contain both the comma that separates elements and the semicolon that
    /// separates pairs. (An IPv6 node such as for="[2001:db8:cafe::17]:4711"
    /// needs the quoting for a different reason - ":" and "[" are not token
    /// characters - and contains neither separator.)
    ///
    /// Everything in this field is hearsay. A proxy writes what the previous
    /// hop told it, and the first element was written by whichever hop chose to
    /// believe the client. Section 8.1 says as much; it is the reason this type
    /// parses and exposes rather than resolves.
    /// </summary>
    /// <seealso cref="https://www.rfc-editor.org/rfc/rfc7239.html"/>
    public sealed class ForwardedElement
    {

        #region Data

        private static readonly Char[] tokenCharacters = "!#$%&'*+-.^_`|~".ToCharArray();

        private readonly List<KeyValuePair<String, String>> parameters;

        #endregion

        #region Properties

        /// <summary>
        /// Every parameter of this element, in the order it was written, with
        /// lower-cased names and unquoted values.
        /// </summary>
        public IReadOnlyList<KeyValuePair<String, String>>  Parameters
            => parameters;

        /// <summary>
        /// The node that connected to the proxy which wrote this element - the
        /// client, for the first element of the chain.
        /// </summary>
        public ForwardedNode?  For      { get; }

        /// <summary>
        /// The interface of the proxy that received the request.
        /// </summary>
        public ForwardedNode?  By       { get; }

        /// <summary>
        /// The Host field as the request arrived with it, before the proxy
        /// rewrote it.
        /// </summary>
        public String?         Host     { get; }

        /// <summary>
        /// The protocol the request arrived over ("http", "https", ...).
        /// </summary>
        public String?         Proto    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new Forwarded element, e.g. to append this hop to a
        /// request that is being proxied onwards.
        /// </summary>
        /// <param name="For">The node that connected to us.</param>
        /// <param name="By">The interface of ours that received the request.</param>
        /// <param name="Host">The Host field as the request arrived with it.</param>
        /// <param name="Proto">The protocol the request arrived over.</param>
        /// <param name="Extensions">Any further parameters.</param>
        public ForwardedElement(ForwardedNode?                             For          = null,
                                ForwardedNode?                             By           = null,
                                String?                                    Host         = null,
                                String?                                    Proto        = null,
                                IEnumerable<KeyValuePair<String, String>>?  Extensions   = null)
        {

            this.For         = For;
            this.By          = By;
            this.Host        = Host;
            this.Proto       = Proto;

            this.parameters  = [];

            if (By    is not null)  parameters.Add(new ("by",    By. ToString()));
            if (For   is not null)  parameters.Add(new ("for",   For.ToString()));
            if (Host  is not null)  parameters.Add(new ("host",  Host));
            if (Proto is not null)  parameters.Add(new ("proto", Proto));

            foreach (var extension in Extensions ?? [])
                parameters.Add(new (extension.Key.ToLowerInvariant(), extension.Value));

        }

        private ForwardedElement(List<KeyValuePair<String, String>>  Parameters,
                                 ForwardedNode?                      For,
                                 ForwardedNode?                      By,
                                 String?                             Host,
                                 String?                             Proto)
        {

            this.parameters  = Parameters;
            this.For         = For;
            this.By          = By;
            this.Host        = Host;
            this.Proto       = Proto;

        }

        #endregion


        #region (static) TryParseAll(Text, out Elements)

        /// <summary>
        /// Try to parse a whole Forwarded field value into its elements.
        /// Anything malformed rejects the entire field rather than some of it:
        /// half a forwarding chain is worse than none, because it looks like a
        /// complete one.
        /// </summary>
        /// <param name="Text">A Forwarded field value.</param>
        /// <param name="Elements">The parsed elements.</param>
        public static Boolean TryParseAll(String                                                Text,
                                          [NotNullWhen(true)] out IEnumerable<ForwardedElement>?  Elements)
        {

            Elements = null;

            if (Text is null)
                return false;

            if (!TrySplit(Text, ',', out var elementTexts))
                return false;

            var elements = new List<ForwardedElement>();

            foreach (var elementText in elementTexts)
            {

                if (!TryParse(elementText, out var element))
                    return false;

                elements.Add(element);

            }

            if (elements.Count == 0)
                return false;

            Elements = elements;
            return true;

        }

        #endregion

        #region (static) TryParse   (Text, out Element)

        /// <summary>
        /// Try to parse one element of a Forwarded field value.
        /// </summary>
        /// <param name="Text">One forwarded-element.</param>
        /// <param name="Element">The parsed element.</param>
        public static Boolean TryParse(String                                    Text,
                                       [NotNullWhen(true)] out ForwardedElement?  Element)
        {

            Element = null;

            if (Text is null)
                return false;

            if (!TrySplit(Text, ';', out var pairTexts))
                return false;

            var            parameters  = new List<KeyValuePair<String, String>>();
            var            seen        = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
            ForwardedNode? forNode     = null;
            ForwardedNode? byNode      = null;
            String?        host        = null;
            String?        proto       = null;

            foreach (var pairText in pairTexts)
            {

                var pair = pairText.Trim();

                // "[ forwarded-pair ] *( ";" [ forwarded-pair ] )" - the pairs
                // are optional, so an empty one is grammatical and carries
                // nothing.
                if (pair.Length == 0)
                    continue;

                var equals = pair.IndexOf('=');

                if (equals <= 0)
                    return false;

                var name = pair[..equals].Trim();

                if (!IsToken(name))
                    return false;

                if (!TryUnquote(pair[(equals + 1)..].Trim(), out var value))
                    return false;

                name = name.ToLowerInvariant();

                // Section 4: "each parameter MUST NOT occur more than once per
                // field-value" - a repeated one is a sign of two proxies
                // writing into the same element, and there is no safe way to
                // pick a winner.
                if (!seen.Add(name))
                    return false;

                switch (name)
                {

                    case "for":
                        if (!ForwardedNode.TryParse(value, out forNode))
                            return false;
                        break;

                    case "by":
                        if (!ForwardedNode.TryParse(value, out byNode))
                            return false;
                        break;

                    case "host":
                        host  = value;
                        break;

                    case "proto":
                        proto = value;
                        break;

                }

                parameters.Add(new (name, value));

            }

            Element = new ForwardedElement(
                          parameters,
                          forNode,
                          byNode,
                          host,
                          proto
                      );

            return true;

        }

        #endregion

        #region (static) ParseAll   (Text)

        /// <summary>
        /// Parse a whole Forwarded field value into its elements.
        /// </summary>
        /// <param name="Text">A Forwarded field value.</param>
        public static IEnumerable<ForwardedElement> ParseAll(String Text)

            => TryParseAll(Text, out var elements)
                   ? elements
                   : throw new ArgumentException($"'{Text}' is not a valid RFC 7239 Forwarded field value!", nameof(Text));

        #endregion

        #region (static) Combine    (Elements)

        /// <summary>
        /// Render a forwarding chain as one field value.
        /// </summary>
        public static String Combine(IEnumerable<ForwardedElement> Elements)

            => Elements.Select(element => element.ToString()).AggregateWith(", ");

        #endregion


        #region (private static) TrySplit(Text, Separator, out Parts)

        /// <summary>
        /// Split on a separator that appears outside of quotation marks. A
        /// quoted string may contain the separator, the quote character itself
        /// when escaped, and - for an IPv6 node - both of the characters this
        /// is called with.
        /// </summary>
        private static Boolean TrySplit(String                                  Text,
                                        Char                                    Separator,
                                        [NotNullWhen(true)] out List<String>?    Parts)
        {

            Parts = null;

            var parts     = new List<String>();
            var current   = new StringBuilder();
            var inQuotes  = false;
            var escaped   = false;

            foreach (var character in Text)
            {

                if (escaped)
                {
                    current.Append(character);
                    escaped = false;
                    continue;
                }

                if (inQuotes && character == '\\')
                {
                    current.Append(character);
                    escaped = true;
                    continue;
                }

                if (character == '"')
                {
                    inQuotes = !inQuotes;
                    current.Append(character);
                    continue;
                }

                if (character == Separator && !inQuotes)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(character);

            }

            // An unterminated quoted string, or a trailing backslash inside
            // one, means the sender and we disagree about where the value ends.
            if (inQuotes || escaped)
                return false;

            parts.Add(current.ToString());

            Parts = parts;
            return true;

        }

        #endregion

        #region (private static) TryUnquote(Text, out Value)

        private static Boolean TryUnquote(String                            Text,
                                          [NotNullWhen(true)] out String?    Value)
        {

            Value = null;

            if (Text.Length == 0)
                return false;

            if (Text[0] != '"')
            {

                if (!IsToken(Text))
                    return false;

                Value = Text;
                return true;

            }

            if (Text.Length < 2 || Text[^1] != '"')
                return false;

            var unquoted  = new StringBuilder();
            var escaped   = false;

            foreach (var character in Text[1..^1])
            {

                if (escaped)
                {
                    unquoted.Append(character);
                    escaped = false;
                    continue;
                }

                if (character == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (character == '"')
                    return false;

                unquoted.Append(character);

            }

            if (escaped)
                return false;

            Value = unquoted.ToString();
            return true;

        }

        #endregion

        #region (private static) IsToken(Text) / NeedsQuoting(Text)

        private static Boolean IsToken(String Text)

            => Text.Length > 0 &&
               Text.All(character => Char.IsAsciiLetterOrDigit(character) ||
                                     tokenCharacters.Contains(character));

        private static Boolean NeedsQuoting(String Text)

            => !IsToken(Text);

        #endregion


        #region ToString()

        /// <summary>
        /// Render this element as it belongs in a Forwarded field, quoting the
        /// values that are not tokens - which is every node carrying a port,
        /// and every IPv6 node, since neither ":" nor "[" is a token character.
        /// </summary>
        public override String ToString()

            => parameters.Select(parameter => NeedsQuoting(parameter.Value)
                                                  ? $"{parameter.Key}=\"{parameter.Value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\""
                                                  : $"{parameter.Key}={parameter.Value}").
                          AggregateWith(";");

        #endregion

    }

}
