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
/// A SunSpec-shaped Modbus holding-register store. The Modbus dispatcher
/// (<see cref="SunSpecBackend"/>) only needs this surface; it doesn't care
/// what kind of device sits behind it - meter, inverter, battery, etc.
///
/// Implementations also publish their RBAC-relevant address classification
/// (<see cref="ProtectedAddresses"/>, <see cref="CommandedAddresses"/>),
/// which the TLS frontend feeds to <see cref="AuthorizationPolicy"/>. This
/// keeps device-specific knowledge in ONE place per device type.
///
/// Authorization happens in the TLS frontend, NOT here. By the time a request
/// reaches an <see cref="ISunSpecDevice"/>, it has already been authorized.
/// </summary>
public interface ISunSpecDevice : IDisposable
{

    /// <summary>
    /// The Modbus address of the SunSpec marker (typically 40000 / 50000 / 0).
    /// All addresses passed to Read/Write are absolute Modbus addresses.
    /// </summary>
    ushort BaseAddress { get; }

    /// <summary>Number of holding registers exposed by this device.</summary>
    int RegisterCount { get; }

    /// <summary>Short, human-readable device label (used for logging).</summary>
    string DisplayName { get; }

    /// <summary>
    /// Addresses that are NETWORK-CRITICAL and require NetworkAdministrator
    /// or SuperAdministrator to write (e.g. device address, comm settings,
    /// protection setpoints).
    /// </summary>
    IReadOnlySet<ushort> ProtectedAddresses { get; }

    /// <summary>
    /// Writable "commanded / autonomous-function" registers - GridService and
    /// up may write these (e.g. curtailment commands, mode switches).
    /// </summary>
    IReadOnlySet<ushort> CommandedAddresses { get; }

    /// <summary>
    /// Read <paramref name="quantity"/> holding registers starting at
    /// <paramref name="startAddress"/>. Returns <c>null</c> when the requested
    /// range falls outside the register window.
    /// </summary>
    ushort[]? ReadHolding(ushort startAddress, ushort quantity);

    /// <summary>Write a single holding register. Returns false on range / read-only violation.</summary>
    bool WriteHolding(ushort address, ushort value);

    /// <summary>Write multiple holding registers atomically (per device).</summary>
    bool WriteHolding(ushort address, ReadOnlySpan<ushort> values);

}
