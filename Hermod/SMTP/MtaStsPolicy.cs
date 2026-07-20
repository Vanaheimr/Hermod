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

using System.Collections.Concurrent;
using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP;

public sealed partial record MtaStsPolicy
{
    public MtaStsMode      Mode        { get; init; } = MtaStsMode.None;
    public List<String>    MxPatterns  { get; init; } = [];
    public TimeSpan        MaxAge      { get; init; } = TimeSpan.Zero;
    public DateTimeOffset  FetchedAt   { get; init; } = Timestamp.Now;
    public String?         PolicyId    { get; init; }
    public Boolean         IsValid
        => Mode != MtaStsMode.None &&
           Timestamp.Now - FetchedAt < MaxAge;


    /// <summary>
    /// Check if an MX host matches the policy
    /// </summary>
    public bool MatchesMx(string mxHost)
    {
        foreach (var pattern in MxPatterns)
        {
            if (pattern.StartsWith("*."))
            {
                // Wildcard match: *.example.com matches mail.example.com
                var suffix = pattern[1..]; // .example.com
                if (mxHost.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ||
                    mxHost.Equals(pattern[2..], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else
            {
                // Exact match
                if (mxHost.Equals(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public static MtaStsPolicy None => new() { Mode = MtaStsMode.None };
}
