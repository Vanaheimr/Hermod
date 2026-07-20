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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP2
{

    using System.Text;

    /// <summary>
    /// Shared parser for an RFC 7235 auth-param list — comma-separated
    /// <c>key=value</c> pairs, values optionally double-quoted (with backslash
    /// escapes and embedded commas). Used by the Digest (RFC 7616) and Token
    /// schemes, both of which carry their credentials as such a list.
    /// </summary>
    internal static class HTTPAuthParams
    {
        public static Dictionary<string, string> Parse(string Credentials)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var i      = 0;
            var n      = Credentials.Length;

            while (i < n)
            {
                while (i < n && (Credentials[i] == ' ' || Credentials[i] == ','))
                    i++;

                var keyStart = i;
                while (i < n && Credentials[i] != '=')
                    i++;
                if (i >= n)
                    break;

                var key = Credentials[keyStart..i].Trim();
                i++;   // skip '='

                string value;
                if (i < n && Credentials[i] == '"')
                {
                    i++;
                    var sb = new StringBuilder();
                    while (i < n && Credentials[i] != '"')
                    {
                        if (Credentials[i] == '\\' && i + 1 < n) { sb.Append(Credentials[i + 1]); i += 2; }
                        else                                     { sb.Append(Credentials[i]);     i++;    }
                    }
                    i++;   // skip closing quote
                    value = sb.ToString();
                }
                else
                {
                    var valStart = i;
                    while (i < n && Credentials[i] != ',')
                        i++;
                    value = Credentials[valStart..i].Trim();
                }

                if (key.Length > 0)
                    result[key] = value;
            }

            return result;
        }
    }

}
