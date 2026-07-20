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
/// REQUIRETLS support (RFC 8689)
/// When a message has REQUIRETLS, it must only be transmitted over TLS
/// and the receiving server must also support REQUIRETLS.
/// </summary>
public sealed class RequireTlsHandler
{
    private readonly ILogger _logger;

    public RequireTlsHandler(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Check if REQUIRETLS is present in MAIL FROM
    /// </summary>
    public static bool ParseRequireTls(string mailFromParams)
    {
        return mailFromParams.Contains("REQUIRETLS", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validate that REQUIRETLS requirements are met for sending
    /// </summary>
    public bool ValidateForSending(
        bool        requireTls,
        bool        tlsActive,
        MtaStsPolicy mtaStsPolicy)
    {
        if (!requireTls)
            return true;

        // Must have TLS active on current connection
        if (!tlsActive)
        {
            _logger.Log(LogLevel.Warning, "REQUIRETLS: Connection is not TLS-secured");
            return false;
        }

        // Must have MTA-STS policy in enforce mode (or no policy check if we trust the hop)
        if (mtaStsPolicy.Mode == MtaStsMode.Enforce)
        {
            return true;
        }

        _logger.Log(LogLevel.Warning, "REQUIRETLS: Remote domain doesn't have MTA-STS enforce policy");
        return false;
    }

    /// <summary>
    /// Add REQUIRETLS to outbound message if it was on the inbound
    /// </summary>
    public static string PropagateRequireTls(string mailFromCommand, bool requireTls)
    {
        if (!requireTls)
            return mailFromCommand;

        if (mailFromCommand.Contains("REQUIRETLS", StringComparison.OrdinalIgnoreCase))
            return mailFromCommand;

        // Add REQUIRETLS before the closing >
        return mailFromCommand.TrimEnd() + " REQUIRETLS";
    }
}
