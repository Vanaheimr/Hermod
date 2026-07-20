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
using System.Text.RegularExpressions;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// RFC 6376 canonicalization shared by the DKIM signer and verifier so both produce
    /// byte-identical hash input. Everything operates on the raw message text; callers hash
    /// the result as UTF-8 (which reconstructs the original wire octets for UTF-8 messages).
    /// </summary>
    public static partial class DkimCanonicalization
    {

        #region Split(message)

        /// <summary>
        /// Split a raw message into its header block and body. The body excludes the blank
        /// separator line. Line endings in the returned parts are normalized to CRLF.
        /// </summary>
        public static (String Headers, String Body) Split(String message)
        {

            var norm = message.Replace("\r\n", "\n");
            var idx  = norm.IndexOf("\n\n", StringComparison.Ordinal);

            if (idx < 0)
                return (message, "");

            var headers = norm[..idx].       Replace("\n", "\r\n");
            var body    = norm[(idx + 2)..]. Replace("\n", "\r\n");

            return (headers, body);

        }

        #endregion

        #region ParseFields(headerBlock)

        /// <summary>
        /// Parse a raw header block into ordered fields, preserving original folding and whitespace.
        /// </summary>
        public static List<DkimHeaderField> ParseFields(String headerBlock)
        {

            var fields = new List<DkimHeaderField>();
            var lines  = headerBlock.Replace("\r\n", "\n").Split('\n');

            String? name = null;
            var     raw  = new StringBuilder();

            void Flush()
            {
                if (name is not null)
                {
                    var rawField = raw.ToString();
                    var colon    = rawField.IndexOf(':');
                    var rawValue = colon >= 0 ? rawField[(colon + 1)..] : "";
                    fields.Add(new DkimHeaderField(name, rawValue, rawField));
                }
                name = null;
                raw.Clear();
            }

            foreach (var line in lines)
            {
                // Continuation line (folded): starts with WSP.
                if (line.Length > 0 && (line[0] == ' ' || line[0] == '\t'))
                {
                    if (name is not null)
                        raw.Append("\r\n").Append(line);
                }
                else
                {
                    Flush();
                    var colon = line.IndexOf(':');
                    if (colon > 0)
                    {
                        name = line[..colon].Trim();
                        raw.Append(line);
                    }
                }
            }

            Flush();
            return fields;

        }

        #endregion

        #region CanonicalizeHeader(field, method)

        /// <summary>
        /// Canonicalize a header field (RFC 6376 §3.4.1 simple / §3.4.2 relaxed).
        /// No trailing CRLF is added; the caller appends it where required.
        /// </summary>
        public static String CanonicalizeHeader(DkimHeaderField field, String method)
        {

            if (method == "relaxed")
            {
                var name  = field.Name.ToLowerInvariant();
                var value = WhitespaceRegex().Replace(Unfold(field.RawValue), " ").Trim();
                return $"{name}:{value}";
            }

            // simple: exactly as it appears in the message.
            return field.RawField;

        }

        #endregion

        #region CanonicalizeBody(body, method)

        /// <summary>
        /// Canonicalize a message body (RFC 6376 §3.4.3 simple / §3.4.4 relaxed).
        /// </summary>
        public static String CanonicalizeBody(String body, String method)
        {

            if (method == "relaxed")
            {

                var lines = body.Replace("\r\n", "\n").Split('\n').
                                 Select(line => WhitespaceRegex().Replace(line, " ").TrimEnd()).
                                 ToList();

                // Ignore all empty lines at the end of the body.
                while (lines.Count > 0 && lines[^1].Length == 0)
                    lines.RemoveAt(lines.Count - 1);

                // An empty relaxed body canonicalizes to the empty string.
                return lines.Count == 0 ? "" : String.Join("\r\n", lines) + "\r\n";

            }
            else
            {

                var result = body.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");

                // An empty simple body canonicalizes to a single CRLF.
                if (result.Length == 0)
                    return "\r\n";

                while (result.EndsWith("\r\n\r\n"))
                    result = result[..^2];

                if (!result.EndsWith("\r\n"))
                    result += "\r\n";

                return result;

            }

        }

        #endregion

        #region BuildHeaderHashInput(fields, signedHeaderNames, dkimSignatureField, method)

        /// <summary>
        /// Build the header hashing input: each header named in h= (selected bottom-up per
        /// name, RFC 6376 §5.4.2) canonicalized and CRLF-terminated, followed by the
        /// DKIM-Signature field with its b= value removed and NO trailing CRLF.
        /// Names in h= with no (remaining) matching field contribute nothing (§3.7).
        /// </summary>
        public static String BuildHeaderHashInput(List<DkimHeaderField>  fields,
                                                  IEnumerable<String>    signedHeaderNames,
                                                  DkimHeaderField        dkimSignatureField,
                                                  String                 method)
        {

            var sb   = new StringBuilder();
            var used = new Dictionary<String, Int32>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawName in signedHeaderNames)
            {

                var name = rawName.Trim();
                if (name.Length == 0)
                    continue;

                var occurrences = fields.Where(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
                var already     = used.GetValueOrDefault(name, 0);

                // Take instances from the bottom of the header block upward.
                if (already < occurrences.Count)
                    sb.Append(CanonicalizeHeader(occurrences[occurrences.Count - 1 - already], method)).Append("\r\n");

                used[name] = already + 1;

            }

            sb.Append(CanonicalizeHeader(RemoveSignatureValue(dkimSignatureField), method));
            return sb.ToString();

        }

        #endregion

        #region RemoveSignatureValue(field)

        /// <summary>
        /// Return the DKIM-Signature field with the value of its b= tag removed (tag-boundary
        /// aware, so a "b=" substring inside e.g. the bh= base64 is never touched).
        /// </summary>
        public static DkimHeaderField RemoveSignatureValue(DkimHeaderField field)
        {

            var rawField = BValueRegex().Replace(field.RawField, "$1$2");
            var colon    = rawField.IndexOf(':');
            var rawValue = colon >= 0 ? rawField[(colon + 1)..] : "";

            return new DkimHeaderField(field.Name, rawValue, rawField);

        }

        #endregion


        private static String Unfold(String value)
            => value.Replace("\r\n", "").Replace("\n", "");

        [GeneratedRegex(@"[ \t]+")]
        private static partial Regex WhitespaceRegex();

        // The b= tag only at a tag boundary (start of value or after ';'); keeps "b=", drops its value.
        [GeneratedRegex(@"(^|;)(\s*b\s*=)[^;]*")]
        private static partial Regex BValueRegex();

    }

}
