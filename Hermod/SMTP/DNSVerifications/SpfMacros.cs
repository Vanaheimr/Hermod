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

using System.Net.Sockets;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// Values available to SPF macro expansion (RFC 7208 §7.2). The current &lt;domain&gt;
    /// (%{d}) is passed separately because it changes across include/redirect recursion.
    /// </summary>
    public readonly record struct SpfMacroContext(String Sender, String LocalPart, String SenderDomain, String HeloDomain);

    /// <summary>
    /// SPF macro expansion (RFC 7208 §7).
    /// </summary>
    public static class SpfMacros
    {

        /// <summary>
        /// Expand SPF macros in a domain-spec. <paramref name="domain"/> is the current record's
        /// domain (the %{d} macro), which changes across include/redirect recursion.
        /// Note: macros that use '/' as a delimiter are not supported (would collide with CIDR).
        /// </summary>
        public static String Expand(String input, String domain, System.Net.IPAddress clientIp, in SpfMacroContext ctx)
        {

            if (input.IndexOf('%') < 0)
                return input;

            var sb = new StringBuilder(input.Length);

            for (var i = 0; i < input.Length; i++)
            {
                if (input[i] != '%' || i + 1 >= input.Length)
                {
                    sb.Append(input[i]);
                    continue;
                }

                var n = input[i + 1];
                if      (n == '%') { sb.Append('%');   i++; }
                else if (n == '_') { sb.Append(' ');   i++; }
                else if (n == '-') { sb.Append("%20"); i++; }
                else if (n == '{')
                {
                    var end = input.IndexOf('}', i + 2);
                    if (end < 0) { sb.Append('%'); continue; }   // malformed: leave literal
                    sb.Append(ExpandOne(input[(i + 2)..end], domain, clientIp, ctx));
                    i = end;
                }
                else
                    sb.Append('%');   // stray '%': leave literal
            }

            // RFC 7208 §7.3: if too long, trim whole labels from the left down to 253 chars.
            var result = sb.ToString();
            while (result.Length > 253)
            {
                var dot = result.IndexOf('.');
                if (dot < 0) break;
                result = result[(dot + 1)..];
            }
            return result;

        }

        private static String ExpandOne(String macro, String domain, System.Net.IPAddress clientIp, in SpfMacroContext ctx)
        {

            if (macro.Length == 0)
                return "";

            var letter    = Char.ToLowerInvariant(macro[0]);
            var urlEscape = Char.IsUpper(macro[0]);

            var idx        = 1;
            var digitStart = idx;
            while (idx < macro.Length && Char.IsDigit(macro[idx])) idx++;
            int? keep = idx > digitStart ? Int32.Parse(macro[digitStart..idx]) : null;

            var reverse = idx < macro.Length && macro[idx] == 'r';
            if (reverse) idx++;

            var delimiters = idx < macro.Length ? macro[idx..] : ".";
            if (delimiters.Length == 0) delimiters = ".";

            var value = letter switch
            {
                's' => ctx.Sender,
                'l' => ctx.LocalPart,
                'o' => ctx.SenderDomain,
                'd' => domain,
                'i' => FormatIp(clientIp),
                'p' => "unknown",   // validated PTR is deprecated (RFC 7208 §7.3); not performed
                'v' => clientIp.AddressFamily == AddressFamily.InterNetworkV6 ? "ip6" : "in-addr",
                'h' => ctx.HeloDomain,
                _   => ""           // c/r/t are only valid in exp text
            };

            var parts = value.Split(delimiters.ToCharArray());
            if (reverse) Array.Reverse(parts);
            if (keep is int k && k > 0 && k < parts.Length)
                parts = parts[^k..];

            var expanded = String.Join('.', parts);
            return urlEscape ? Uri.EscapeDataString(expanded) : expanded;

        }

        /// <summary>
        /// The %{i} macro value: dotted decimal for IPv4, 32 dotted nibbles (high-to-low) for IPv6.
        /// </summary>
        private static String FormatIp(System.Net.IPAddress ip)
        {

            if (ip.AddressFamily == AddressFamily.InterNetwork)
                return ip.ToString();

            var sb = new StringBuilder(63);
            foreach (var b in ip.GetAddressBytes())
            {
                sb.Append((b >> 4).ToString("x")).Append('.');
                sb.Append((b & 0xF).ToString("x")).Append('.');
            }
            sb.Length--;   // drop trailing dot
            return sb.ToString();

        }

    }

}
