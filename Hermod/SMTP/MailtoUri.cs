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

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// A 'mailto' URI (RFC 6068), as DMARC (RFC 7489 §6.2) and TLS-RPT (RFC 8460 §3) name the
    /// destinations of their reports:
    /// <code>
    /// mailtoURI    = "mailto:" [ to ] [ hfields ]
    /// to           = addr-spec *("," addr-spec )
    /// hfields      = "?" hfield *( "&amp;" hfield )
    /// hfield       = hfname "=" hfvalue
    /// addr-spec    = local-part "@" domain
    /// local-part   = dot-atom-text / quoted-string
    /// domain       = dot-atom-text / "[" *dtext-no-obs "]"
    /// </code>
    /// The characters of an addr-spec that cannot be in a URI are percent-encoded, in UTF-8.
    /// </summary>
    public static class MailtoUri
    {

        #region Addresses(Uri)

        /// <summary>
        /// The addresses a mailto URI names: those of its "to" and of its "to" header fields,
        /// percent-decoded. None when it is no mailto URI - another scheme, a character no URI
        /// has (RFC 3986 §2: a line break, a space, a control character, a non-ASCII one), a "%"
        /// that is no percent-encoding, an octet sequence that is no UTF-8 - and without those
        /// of its addresses that are no addr-spec. Its other header fields - a subject, a body
        /// - are for a person who writes a message, not for a report.
        /// </summary>
        public static IReadOnlyList<String> Addresses(String Uri)
        {

            if (!Uri.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) || !Uri.All(IsUriCharacter))
                return [];

            var rest      = Uri[7..];
            var query     = rest.IndexOf('?');
            var to        = query >= 0 ? rest[..query] : rest;
            var hfields   = query >= 0 ? rest[(query + 1)..] : "";
            var addresses = new List<String>();

            if (!AddTo(addresses, to))
                return [];

            foreach (var hfield in hfields.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {

                var equals = hfield.IndexOf('=');
                if (equals < 0)
                    continue;

                if (Decode(hfield[..equals]) is not { } name)
                    return [];

                if (name.Equals("to", StringComparison.OrdinalIgnoreCase) && !AddTo(addresses, hfield[(equals + 1)..]))
                    return [];

            }

            return addresses.Distinct(StringComparer.Ordinal).ToList();

        }

        /// <summary>
        /// Add the addr-specs of a "to", percent-decoded; false when it is no percent-encoding.
        /// </summary>
        private static Boolean AddTo(List<String> Addresses, String To)
        {

            if (Decode(To) is not { } decoded)
                return false;

            foreach (var address in SplitAtCommas(decoded))
                if (IsAddrSpec(address))
                    Addresses.Add(address);

            return true;

        }

        #endregion

        #region Of(Address)

        /// <summary>
        /// The mailto URI of an address: its characters but the unreserved ones (RFC 3986 §2.3),
        /// "@" and the sub-delimiters RFC 6068 leaves as they are, percent-encoded in UTF-8 -
        /// "!", "," and ";" too, which DMARC and TLS-RPT take for their own delimiters.
        /// </summary>
        public static String Of(String Address)
        {

            var uri = new StringBuilder("mailto:");

            foreach (var octet in Encoding.UTF8.GetBytes(Address))
            {
                if (octet is >= (Byte) 'A' and <= (Byte) 'Z' or >= (Byte) 'a' and <= (Byte) 'z' or >= (Byte) '0' and <= (Byte) '9' ||
                    "-._~@$'()*+:".Contains((Char) octet))
                    uri.Append((Char) octet);
                else
                    uri.Append('%').Append(octet.ToString("X2"));
            }

            return uri.ToString();

        }

        #endregion


        #region (private) helpers

        // RFC 3986 §2: unreserved / reserved / "%" - ASCII graphic characters but '"', "<", ">",
        // "\", "^", "`", "{", "|", "}".
        private static Boolean IsUriCharacter(Char c)
            => c is >= '!' and <= '~' && !"\"<>\\^`{|}".Contains(c);

        /// <summary>
        /// Percent-decoded, as UTF-8 (RFC 6068 §2); null when a "%" is no percent-encoding or
        /// the octets are no UTF-8.
        /// </summary>
        private static String? Decode(String Text)
        {

            var octets = new List<Byte>(Text.Length);

            for (var i = 0; i < Text.Length; i++)
            {

                if (Text[i] != '%')
                {
                    octets.Add((Byte) Text[i]);
                    continue;
                }

                if (i + 2 >= Text.Length || !Byte.TryParse(Text.AsSpan(i + 1, 2), System.Globalization.NumberStyles.AllowHexSpecifier, null, out var octet))
                    return null;

                octets.Add(octet);
                i += 2;

            }

            try
            {
                return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(octets.ToArray());
            }
            catch (ArgumentException)
            {
                return null;
            }

        }

        /// <summary>
        /// The addr-specs of a "to", at the commas between them - not at one in a quoted local part.
        /// </summary>
        private static IEnumerable<String> SplitAtCommas(String To)
        {

            var start  = 0;
            var quoted = false;

            for (var i = 0; i < To.Length; i++)
            {

                if (quoted && To[i] == '\\')
                    i++;

                else if (To[i] == '"')
                    quoted = !quoted;

                else if (To[i] == ',' && !quoted)
                {
                    yield return To[start..i];
                    start = i + 1;
                }

            }

            yield return To[start..];

        }

        /// <summary>
        /// Whether a text is an addr-spec of RFC 6068 §2 - RFC 5322 §3.4.1 without comments,
        /// white space and the obsolete forms, UTF-8 in it as RFC 6532 §3.2 has it.
        /// </summary>
        internal static Boolean IsAddrSpec(String Address)
        {

            var at = Address.StartsWith('"') ? EndOfQuotedString(Address) : Address.LastIndexOf('@');

            if (at <= 0 || at >= Address.Length - 1 || Address[at] != '@')
                return false;

            var localPart = Address[..at];
            var domain    = Address[(at + 1)..];

            return (localPart.StartsWith('"') ? IsQuotedString(localPart) : IsDotAtomText(localPart)) &&
                   (domain.StartsWith('[')    ? IsDomainLiteral(domain)   : IsDotAtomText(domain));

        }

        // Where the quoted string a local part begins with ends, one past its closing quote.
        private static Int32 EndOfQuotedString(String Address)
        {
            for (var i = 1; i < Address.Length; i++)
            {
                if      (Address[i] == '\\') i++;
                else if (Address[i] == '"')  return i + 1;
            }
            return -1;
        }

        // atext: ALPHA, DIGIT and "!#$%&'*+-/=?^_`{|}~" (RFC 5322 §3.2.3), UTF8-non-ascii (RFC 6532 §3.2).
        private static Boolean IsAtext(Char c)
            => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' ||
               "!#$%&'*+-/=?^_`{|}~".Contains(c) ||
               c > '\x7F' && !Char.IsControl(c) && !Char.IsWhiteSpace(c);

        // dot-atom-text = 1*atext *("." 1*atext)
        private static Boolean IsDotAtomText(String Text)
            => Text.Length > 0 && Text.Split('.').All(atom => atom.Length > 0 && atom.All(IsAtext));

        // quoted-string without CFWS: DQUOTE *([FWS] qcontent) [FWS] DQUOTE - qtext (%d33,
        // %d35-91, %d93-126, UTF8-non-ascii), quoted-pair ("\" VCHAR / WSP), and white space.
        private static Boolean IsQuotedString(String Text)
        {

            if (Text.Length < 2 || Text[0] != '"' || Text[^1] != '"')
                return false;

            for (var i = 1; i < Text.Length - 1; i++)
            {

                var c = Text[i];

                if (c == '\\')
                {
                    if (++i >= Text.Length - 1 || !(Text[i] is >= ' ' and <= '~' or '\t' || Text[i] > '\x7F' && !Char.IsControl(Text[i])))
                        return false;
                }

                else if (!(c is ' ' or '\t' or '!' or >= '#' and <= '[' or >= ']' and <= '~' || c > '\x7F' && !Char.IsControl(c)))
                    return false;

            }

            return true;

        }

        // "[" *dtext-no-obs "]", dtext-no-obs = %d33-90 / %d94-126
        private static Boolean IsDomainLiteral(String Text)
            => Text.Length >= 2 && Text[0] == '[' && Text[^1] == ']' &&
               Text[1..^1].All(c => c is >= '!' and <= 'Z' or >= '^' and <= '~');

        #endregion

    }

}
