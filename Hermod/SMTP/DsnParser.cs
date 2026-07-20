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

using org.GraphDefined.Vanaheimr.Illias;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP;

/// <summary>
/// Parse DSN parameters from SMTP commands
/// </summary>
public static class DsnParser
{
    /// <summary>
    /// Parse MAIL FROM parameters for DSN
    /// </summary>
    public static (string? EnvId, DsnRet Ret) ParseMailFromParams(string parameters)
    {
        string? envId = null;
        var ret = DsnRet.Full;

        var parts = parameters.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.StartsWith("ENVID=", StringComparison.OrdinalIgnoreCase))
            {
                envId = part[6..];
            }
            else if (part.StartsWith("RET=", StringComparison.OrdinalIgnoreCase))
            {
                ret = part[4..].ToUpperInvariant() switch
                {
                    "HDRS" => DsnRet.Hdrs,
                    "FULL" => DsnRet.Full,
                    _ => DsnRet.Full
                };
            }
        }

        return (envId, ret);
    }

    /// <summary>
    /// Parse RCPT TO parameters for DSN
    /// </summary>
    public static (DsnNotify Notify, string? Orcpt) ParseRcptToParams(string parameters)
    {
        var notify = DsnNotify.Failure; // Default
        string? orcpt = null;

        var parts = parameters.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.StartsWith("NOTIFY=", StringComparison.OrdinalIgnoreCase))
            {
                notify = ParseNotifyValue(part[7..]);
            }
            else if (part.StartsWith("ORCPT=", StringComparison.OrdinalIgnoreCase))
            {
                orcpt = part[6..];
            }
        }

        return (notify, orcpt);
    }

    private static DsnNotify ParseNotifyValue(string value)
    {
        if (value.Equals("NEVER", StringComparison.OrdinalIgnoreCase))
            return DsnNotify.Never;

        var notify = DsnNotify.Never;
        var parts = value.Split(',');
        
        foreach (var part in parts)
        {
            notify |= part.Trim().ToUpperInvariant() switch
            {
                "SUCCESS" => DsnNotify.Success,
                "FAILURE" => DsnNotify.Failure,
                "DELAY" => DsnNotify.Delay,
                _ => DsnNotify.Never
            };
        }

        return notify;
    }
}
