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
/// Authorization decision: does <paramref name="role"/> (from the client's cert)
/// permit the requested function code on the requested address range?
///
/// Implements a minimal, vendor-defined Roles-to-Rights mapping per
/// SunSpecTCP-24..26. The role-to-rights logic itself is universal across
/// SunSpec device types; only the *address classification* (which registers
/// are protected vs. commanded) varies per device. Those classifications are
/// passed in via the constructor, typically pulled from
/// <see cref="ISunSpecDevice.ProtectedAddresses"/> /
/// <see cref="ISunSpecDevice.CommandedAddresses"/>.
///
/// Truth table (independent of device):
///                            FC03/04 read    FC06/16 write to       FC06/16 write to
///                                            "commanded" reg        "protected" reg
///   ReadOnlySunSpec              YES               NO                       NO
///   GridServiceSunSpec           YES               YES                      NO
///   NetworkAdministratorSunSpec  YES               YES                      YES
///   SuperAdministratorSunSpec    YES               YES                      YES
///
/// Unknown / missing role => DENY (returns Modbus exception 0x01).
/// </summary>
public sealed class AuthorizationPolicy
{

    private readonly IReadOnlySet<ushort> _protectedAddresses;
    private readonly IReadOnlySet<ushort> _commandedAddresses;

    /// <summary>
    /// Build a policy from explicit address sets. Use this when the protected /
    /// commanded sets don't come from a single device (e.g. proxy in front of
    /// a non-introspectable downstream).
    /// </summary>
    public AuthorizationPolicy(IReadOnlySet<ushort> protectedAddresses, IReadOnlySet<ushort> commandedAddresses)
    {
        _protectedAddresses = protectedAddresses;
        _commandedAddresses = commandedAddresses;
    }

    /// <summary>
    /// Build a policy that mirrors a specific device's address classification.
    /// </summary>
    public AuthorizationPolicy(ISunSpecDevice device)
        : this(device.ProtectedAddresses, device.CommandedAddresses) { }

    public AuthorizationDecision Authorize(string? role, ModbusFunctionCodes fc, ushort firstAddress, ushort quantity)
    {

        // SunSpecTCP-32: missing role => Illegal Function
        if (role is null)
            return AuthorizationDecision.Deny("no role extension in client cert");

        // Reads are permitted by all four mandatory roles.
        if (!fc.IsWrite())
            return SunSpecRoles.IsMandatoryRole(role)
                ? AuthorizationDecision.Allow()
                : AuthorizationDecision.Deny($"unknown role '{role}'");

        // Writes => check per-register class
        var lastAddress = (ushort)(firstAddress + quantity - 1);
        for (var addr = firstAddress; addr <= lastAddress; addr++)
        {
            var d = AuthorizeWrite(role, addr);
            if (!d.Allowed) return d;
        }

        return AuthorizationDecision.Allow();

    }

    private AuthorizationDecision AuthorizeWrite(string role, ushort address)
    {

        var isProtected = _protectedAddresses.Contains(address);
        var isCommanded = _commandedAddresses.Contains(address);

        return role switch
        {
            SunSpecRoles.ReadOnly =>
                AuthorizationDecision.Deny($"ReadOnlySunSpec cannot write (addr 0x{address:X4})"),

            SunSpecRoles.GridService when isProtected =>
                AuthorizationDecision.Deny($"GridServiceSunSpec cannot write protected/network reg 0x{address:X4}"),

            SunSpecRoles.GridService when isCommanded => AuthorizationDecision.Allow(),
            SunSpecRoles.GridService                  =>
                AuthorizationDecision.Deny($"GridServiceSunSpec may only write commanded regs (addr 0x{address:X4})"),

            SunSpecRoles.NetworkAdministrator => AuthorizationDecision.Allow(),
            SunSpecRoles.SuperAdministrator   => AuthorizationDecision.Allow(),

            _ => AuthorizationDecision.Deny($"unknown role '{role}'"),
        };

    }

}
