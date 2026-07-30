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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP3;

/// <summary>
/// Priority per RFC 9218 (Extensible Prioritization Scheme for HTTP): <see cref="Urgency"/>
/// <c>u</c> = 0 (highest) … 7 (background), default 3; <see cref="Incremental"/> <c>i</c> = the
/// response is usable piecewise (equally urgent incremental responses share the bandwidth),
/// default false. Encoded as a structured-fields dictionary in the <c>priority</c> header
/// or in the PRIORITY_UPDATE frame.
/// </summary>
public readonly record struct Http3Priority(int Urgency, bool Incremental)
{
    /// <summary>
    /// The default priority (u=3, non-incremental) — applies when no parameters are signalled.
    /// </summary>
    public static readonly Http3Priority Default = new(3, false);

    /// <summary>
    /// Serialises as a structured-fields dictionary (RFC 9218 §5), e.g. "u=0", "u=5, i" or "" for
    /// the default values (omitting a parameter = default value).
    /// </summary>
    public string ToHeaderValue()
    {
        var parts = new List<string>(2);
        if (Urgency != 3)
            parts.Add($"u={Urgency}");
        if (Incremental)
            parts.Add("i");
        return string.Join(", ", parts);
    }

    /// <summary>
    /// Parses a structured-fields dictionary (RFC 9218 §4, fault-tolerant): unknown parameters,
    /// out-of-range values or unexpected types MUST be ignored; with repeated keys the last one
    /// wins (Structured Fields §3.2). Anything unusable falls back to the default values.
    /// </summary>
    public static Http3Priority Parse(string? value)
    {
        int urgency = 3;
        bool incremental = false;
        if (string.IsNullOrWhiteSpace(value))
            return new Http3Priority(urgency, incremental);

        foreach (string memberRaw in value.Split(','))
        {
            string member = memberRaw.Trim();
            int semicolon = member.IndexOf(';'); // member parameters (";…") do not concern us
            if (semicolon >= 0)
                member = member[..semicolon].TrimEnd();

            string key = member;
            string? itemValue = null;
            int equals = member.IndexOf('=');
            if (equals >= 0)
            {
                key = member[..equals].Trim();
                itemValue = member[(equals + 1)..].Trim();
            }

            switch (key)
            {
                case "u": // integer 0..7 (RFC 9218 §4.1); out of range/wrong type ⇒ ignore
                    if (itemValue is not null && int.TryParse(itemValue, out int u) && u is >= 0 and <= 7)
                        urgency = u;
                    break;
                case "i": // boolean (RFC 9218 §4.2): bare key or "?1" = true, "?0" = false
                    if (itemValue is null || itemValue == "?1")
                        incremental = true;
                    else if (itemValue == "?0")
                        incremental = false;
                    break;
                // Unknown parameters ⇒ ignore (RFC 9218 §4 MUST).
            }
        }
        return new Http3Priority(urgency, incremental);
    }
}
