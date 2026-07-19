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

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP;

/// <summary>
/// SMTP transport priority (MT-PRIORITY, RFC 6710): a message priority in the range -9..9,
/// default 0, where a higher value is more urgent. This is the transport-level priority used for
/// queue scheduling — distinct from the header-level <c>Importance</c> shown in a mail client.
/// </summary>
public static class MtPriority
{

    /// <summary>The lowest priority (-9).</summary>
    public const SByte Min      = -9;

    /// <summary>The highest priority (9).</summary>
    public const SByte Max      =  9;

    /// <summary>The default priority (0).</summary>
    public const SByte Default  =  0;

    /// <summary>The EHLO keyword advertising the extension.</summary>
    public const String Keyword = "MT-PRIORITY";


    /// <summary>Clamp a value into the valid -9..9 range.</summary>
    public static SByte Clamp(Int32 value)
        => (SByte) Math.Clamp(value, Min, Max);

    /// <summary>Parse a single MT-PRIORITY value; returns <see cref="Default"/> if absent or invalid.</summary>
    public static SByte Parse(String? value)
        => SByte.TryParse(value, out var p) ? Clamp(p) : Default;

    /// <summary>
    /// Extract the MT-PRIORITY value from a MAIL FROM parameter string (0 if not present).
    /// </summary>
    public static SByte ParseFromMailParams(String parameters)
    {

        foreach (var part in parameters.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            if (part.StartsWith("MT-PRIORITY=", StringComparison.OrdinalIgnoreCase))
                return Parse(part["MT-PRIORITY=".Length..]);

        return Default;

    }

    /// <summary>
    /// Append "MT-PRIORITY=&lt;n&gt;" to a MAIL FROM command, but only when the remote advertised the
    /// extension and a non-default priority was set.
    /// </summary>
    public static String AppendMailFromParam(String mailFromCommand, SByte priority, Boolean remoteSupportsMtPriority)
        => remoteSupportsMtPriority && priority != Default
               ? $"{mailFromCommand} MT-PRIORITY={priority}"
               : mailFromCommand;

}
