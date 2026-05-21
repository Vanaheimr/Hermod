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

using System.Formats.Asn1;
using System.Security.Cryptography.X509Certificates;

namespace org.GraphDefined.Vanaheimr.Hermod.SunSpecModbusTLS.Common;

/// <summary>
/// Extracts the SunSpec role from a peer's X.509v3 certificate.
/// Per [MBTLS] §8.4 / SunSpecTCP-29..31:
///   * OID 1.3.6.1.4.1.50316.802.1
///   * Value is a single ASN.1 UTF8String
///   * Exactly one role per certificate
/// </summary>
public static class RoleExtractor
{
    /// <summary>
    /// Read the role string out of <paramref name="cert"/>'s Role Extension.
    /// Returns null when the extension is absent or unparseable.
    /// Per SunSpecTCP-32, absent role -> server MUST reject the request
    /// (we let the caller decide; this method does not throw on missing role).
    /// </summary>
    public static string? TryExtractRole(X509Certificate2 cert)
    {
        var ext = cert.Extensions[SunSpecRoles.RoleOid];
        if (ext is null) return null;

        try
        {
            var reader = new AsnReader(ext.RawData, AsnEncodingRules.DER);
            var role = reader.ReadCharacterString(UniversalTagNumber.UTF8String);

            if (reader.HasData)
                return null;

            // Defensive: SunSpecTCP-31 - a role is treated as a single string.
            // We disallow embedded NUL bytes which some vendors used to sneak in
            // alternate roles past naive parsers.
            if (string.IsNullOrEmpty(role) || role.Contains('\0'))
                return null;

            return role;
        }
        catch (AsnContentException)
        {
            return null;
        }
    }
}
