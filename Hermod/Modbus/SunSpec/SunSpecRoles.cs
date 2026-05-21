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

namespace org.GraphDefined.Vanaheimr.Hermod.SunSpecModbusTLS.Common;

/// <summary>
/// SunSpec Modbus/TLS (mbaps) X.509v3 Role Extension constants.
/// References:
///   [MBTLS]   Modbus.org "MODBUS/TCP Security" v36, 2021-07-30, sec. 8.4
///   [SUNSPEC] "Secure SunSpec Modbus Specification" (Approved), SunSpecTCP-22..36
///
/// SunSpecTCP-22: An mbaps device MUST support the four mandatory roles below.
/// SunSpecTCP-29: The Role MUST use the Modbus.org PEN OID (1.3.6.1.4.1.50316.802.1).
/// SunSpecTCP-30: Encoded as ASN.1 UTF8String.
/// SunSpecTCP-31: Exactly one role per certificate (entire String is one role).
/// SunSpecTCP-32: Missing role -> server MUST return Modbus exception 01.
/// </summary>
public static class SunSpecRoles
{

    /// <summary>
    /// Modbus.org PEN-derived OID for the X.509 Role Extension.
    /// 1.3.6.1.4.1.50316       = Modbus.org Private Enterprise (IANA PEN 50316)
    /// 1.3.6.1.4.1.50316.802.1 = Role Extension (port 802 = mbaps)
    /// </summary>
    public const String RoleOid                 = "1.3.6.1.4.1.50316.802.1";


    // --- Four mandatory SunSpec mbaps roles (SunSpecTCP-22) ---

    /// <summary>
    /// Read access to all data. No write permissions.
    /// </summary>
    public const String ReadOnly                = "ReadOnlySunSpec";

    /// <summary>
    /// Read access to all data. Write access to commanded and autonomous
    /// functions. NO access to networking or protection settings.
    /// (Typical role for VPP / aggregator certificates.)
    /// </summary>
    public const String GridService             = "GridServiceSunSpec";

    /// <summary>
    /// Network configuration capabilities (in addition to GridService rights).
    /// </summary>
    public const String NetworkAdministrator    = "NetworkAdministratorSunSpec";

    /// <summary>
    /// Full administrative privileges (read + write all, incl. protection).
    /// </summary>
    public const String SuperAdministrator      = "SuperAdministratorSunSpec";

    /// <summary>
    /// All four mandatory role identifiers, in escalating privilege order.
    /// </summary>
    public static readonly IReadOnlyList<String> AllMandatory = [
        ReadOnly,
        GridService,
        NetworkAdministrator,
        SuperAdministrator
    ];

    /// <summary>
    /// True if <paramref name="Role"/> is one of the four mandatory SunSpec roles.
    /// (Vendor-specific or IEC 62351-8 roles like VIEWER/OPERATOR/ENGINEER/INSTALLER/SECADM/SECAUD
    /// may additionally be supported per SunSpecTCP-23 -- not enforced here.)
    /// </summary>
    public static Boolean IsMandatoryRole(String? Role)

        => Role is not null &&
           AllMandatory.Contains(Role, StringComparer.Ordinal);

}
