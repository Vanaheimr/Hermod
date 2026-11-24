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

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP;

/// <summary>
/// The Authentication-Results fields a message brings along (RFC 8601 §5): those that claim to
/// be this server's, and those of a version it does not know, are not passed on.
/// </summary>
internal static partial class AuthenticationResultsFields
{

    // authserv-id = value = token / quoted-string (RFC 8601 §2.2, RFC 2045 §5.1), then
    // [ CFWS authres-version ] - authres-version = 1*DIGIT - and ";" or the end (no-result: "none").
    [GeneratedRegex(@"^(?<id>""(?:[^""\\]|\\.)*""|[^\s;""]+)(?:\s+(?<version>\d+))?\s*(?:;|$)")]
    private static partial Regex AuthServIdAndVersion();


    /// <summary>
    /// The message without the Authentication-Results fields of its header that RFC 8601 §5 has
    /// an MTA delete: "any discovered instance of this header field that claims, by virtue of its
    /// authentication service identifier, to have been added within its trust boundary but that
    /// did not come directly from another trusted MTA", and "such a header field" of a version
    /// it does not support "if the SMTP connection [SMTP] relaying the message is not from a
    /// trusted internal MTA". This server trusts no other to add fields in its name: every
    /// connection is one of those. The octets are kept as they came, but for the fields removed;
    /// the same array when there is none.
    /// </summary>
    /// <param name="Message">The message as it arrived.</param>
    /// <param name="AuthServId">The authentication service identifier of this server - its host name, in either form.</param>
    public static Byte[] WithoutForeign(Byte[] Message, String AuthServId)
    {

        var drop  = new List<(Int32 Start, Int32 End)>();
        var start = -1;
        var pos   = 0;

        void Field(Int32 End)
        {
            if (start >= 0 && Removed(Encoding.UTF8.GetString(Message, start, End - start), AuthServId))
                drop.Add((start, End));
        }

        // The header: lines up to the first empty one; a line that begins with white space
        // continues the field before it (RFC 5322 §2.2.3).
        while (pos < Message.Length)
        {

            var lf      = Array.IndexOf(Message, (Byte) '\n', pos);
            var next    = lf < 0 ? Message.Length : lf + 1;
            var content = (lf < 0 ? Message.Length : lf) - pos;

            if (content > 0 && Message[pos + content - 1] == '\r')
                content--;

            if (content == 0)
                break;

            if (Message[pos] is (Byte) ' ' or (Byte) '\t' && start >= 0)
            {
                pos = next;
                continue;
            }

            Field(pos);
            start = pos;
            pos   = next;

        }

        Field(pos);

        if (drop.Count == 0)
            return Message;

        using var kept = new MemoryStream(Message.Length);
        var at = 0;

        foreach (var (from, to) in drop)
        {
            kept.Write(Message, at, from - at);
            at = to;
        }

        kept.Write(Message, at, Message.Length - at);

        return kept.ToArray();

    }


    /// <summary>
    /// Whether a header field is an Authentication-Results field that is not passed on: one of
    /// this server's authserv-id - compared as domain names, A-labels and U-labels alike (RFC 8601
    /// §5: "In the case of EAI-formatted messages, this test is done after converting A-labels into
    /// U-labels") - or of a version other than 1.
    /// </summary>
    /// <param name="Field">The field, name and value, folded or not.</param>
    /// <param name="AuthServId">The authentication service identifier of this server.</param>
    internal static Boolean Removed(String Field, String AuthServId)
    {

        var colon = Field.IndexOf(':');

        if (colon < 0 || !Field[..colon].TrimEnd(' ', '\t').Equals("Authentication-Results", StringComparison.OrdinalIgnoreCase))
            return false;

        var value = WithoutComments(Field[(colon + 1)..].Replace("\r", "").Replace("\n", "")).Trim();
        var match = AuthServIdAndVersion().Match(value);

        if (!match.Success)
            return false;

        var id = match.Groups["id"].Value;

        if (id.StartsWith('"'))
            id = Regex.Replace(id[1..^1], @"\\(.)", "$1");

        var version = match.Groups["version"].Success ? match.Groups["version"].Value : null;

        return Idna.SameDomain(id, AuthServId) ||
               (version is not null && (!Int32.TryParse(version, out var number) || number != 1));

    }


    /// <summary>
    /// The text without its comments (RFC 5322 §3.2.2: "(" ... ")", nested, with quoted-pairs) -
    /// a space where each one stood; quoted strings are left as they are.
    /// </summary>
    private static String WithoutComments(String Text)
    {

        var result  = new StringBuilder(Text.Length);
        var depth   = 0;
        var quoted  = false;

        for (var i = 0; i < Text.Length; i++)
        {

            var c = Text[i];

            if (c == '\\' && i + 1 < Text.Length && (quoted || depth > 0))
            {
                if (depth == 0)
                    result.Append(c).Append(Text[i + 1]);
                i++;
                continue;
            }

            if (depth == 0 && c == '"')
                quoted = !quoted;

            else if (!quoted && c == '(')
            {
                if (depth++ == 0)
                    result.Append(' ');
                continue;
            }

            else if (!quoted && c == ')' && depth > 0)
            {
                depth--;
                continue;
            }

            if (depth == 0)
                result.Append(c);

        }

        return result.ToString();

    }

}
