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

using System.Text.RegularExpressions;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP;

/// <summary>
/// The paths of MAIL and RCPT as RFC 5321 §4.1.2 has them:
/// <code>
/// Reverse-path   = Path / "&lt;&gt;"
/// Forward-path   = Path
/// Path           = "&lt;" [ A-d-l ":" ] Mailbox "&gt;"
/// A-d-l          = At-domain *( "," At-domain )
/// Mailbox        = Local-part "@" ( Domain / address-literal )
/// Local-part     = Dot-string / Quoted-string
/// </code>
/// A Quoted-string may hold any ASCII graphic or space (qtextSMTP) - an "@", a "&gt;" - and a
/// backslash quotes the character after it (quoted-pairSMTP). The source route "MUST BE
/// accepted, SHOULD NOT be generated, and SHOULD be ignored".
/// </summary>
public static class SmtpPath
{

    // At-domain = "@" Domain: a name, or an address literal, of the hosts the route names.
    private static readonly Regex SourceRoute = new (@"^@[^\s@,:<>""]+(?:,@[^\s@,:<>""]+)*$", RegexOptions.CultureInvariant);


    /// <summary>
    /// The path and the parameters of a MAIL or RCPT argument - "FROM:&lt;path&gt; parameters"
    /// or "TO:&lt;path&gt; parameters".
    /// </summary>
    /// <param name="Argument">What follows the command verb.</param>
    /// <param name="Keyword">"FROM:" or "TO:", without regard to case.</param>
    /// <param name="Mailbox">The mailbox, without its source route; "" for "&lt;&gt;".</param>
    /// <param name="Parameters">What follows the path, trimmed.</param>
    /// <returns>False when the argument is no path: no "&lt;", no "&gt;" after the mailbox, an unclosed quoted string, a malformed route.</returns>
    public static Boolean TryParse(String      Argument,
                                   String      Keyword,
                                   out String  Mailbox,
                                   out String  Parameters)
    {

        Mailbox     = "";
        Parameters  = "";

        if (!Argument.StartsWith(Keyword, StringComparison.OrdinalIgnoreCase))
            return false;

        // §3.3 has no space after the colon; clients that write one are many, and taken.
        var position = Keyword.Length;
        while (position < Argument.Length && Argument[position] is ' ' or '\t')
            position++;

        if (position >= Argument.Length || Argument[position] != '<')
            return false;

        var start   = ++position;
        var quoted  = false;

        for (; position < Argument.Length; position++)
        {

            var character = Argument[position];

            if (quoted)
            {
                if      (character == '\\') position++;       // quoted-pairSMTP: the next one is taken as it is
                else if (character == '"')  quoted = false;
            }

            else if (character == '"')
                quoted = true;

            else if (character == '>')
                break;

        }

        if (position >= Argument.Length)
            return false;

        var path    = Argument[start..position];
        Parameters  = Argument[(position + 1)..].Trim();

        // [ A-d-l ":" ] - recognized, and ignored.
        if (path.StartsWith('@'))
        {

            var colon = path.IndexOf(':');

            if (colon < 0 || !SourceRoute.IsMatch(path[..colon]))
                return false;

            path = path[(colon + 1)..];

        }

        Mailbox = path;
        return true;

    }


    /// <summary>
    /// The domain of a mailbox: what follows the "@" that ends its local part - a quoted local
    /// part may hold one of its own. "" when there is none.
    /// </summary>
    public static String DomainOf(String Mailbox)
    {
        var at = EndOfLocalPart(Mailbox);
        return at >= 0 ? Mailbox[(at + 1)..] : "";
    }

    /// <summary>
    /// The local part of a mailbox, quotes and all; the mailbox itself when it has no domain.
    /// </summary>
    public static String LocalPartOf(String Mailbox)
    {
        var at = EndOfLocalPart(Mailbox);
        return at >= 0 ? Mailbox[..at] : Mailbox;
    }

    /// <summary>
    /// Where the "@" after the local part is, or -1.
    /// </summary>
    private static Int32 EndOfLocalPart(String Mailbox)
    {

        if (!Mailbox.StartsWith('"'))
        {
            var at = Mailbox.IndexOf('@');
            return at > 0 ? at : -1;
        }

        for (var position = 1; position < Mailbox.Length; position++)
        {

            if (Mailbox[position] == '\\')
            {
                position++;
                continue;
            }

            if (Mailbox[position] == '"')
                return position + 1 < Mailbox.Length && Mailbox[position + 1] == '@' ? position + 1 : -1;

        }

        return -1;

    }

}
