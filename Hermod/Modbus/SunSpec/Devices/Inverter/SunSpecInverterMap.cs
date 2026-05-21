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
/// SunSpec register map for a Single-Phase Inverter (Model 101 subset).
///
/// Layout:
///   BaseAddress (40000)  : Common Model 1   (offsets 0..69)   - see SunSpecCommon
///   +70                  : Inverter Model 101 header + fields (subset)
///   +88                  : End marker  ID = 0xFFFF, L = 0
///
/// The subset below covers AC measurements + DC measurements + operational
/// state. Real Model 101 is 50 registers; we use 16 here. Enough to demonstrate
/// that the same dispatcher handles a structurally different SunSpec device.
/// </summary>
public static class SunSpecInverterMap
{
    public const ushort BaseAddress      = SunSpecCommon.BaseAddress;

    // ----- Inverter Model 101 (single-phase, int+SF) - subset -----

    public const ushort InverterModelId      = 101;
    public const ushort InverterModelLength  = 16;

    public const ushort OffInvId         = SunSpecCommon.FirstModelOffset;          // 70
    public const ushort OffInvLen        = SunSpecCommon.FirstModelOffset + 1;      // 71

    // AC current
    public const ushort OffInvA          = 72;  // total AC current (uint16)
    public const ushort OffInvA_SF       = 73;

    // AC voltage L-N
    public const ushort OffInvPhVphA     = 74;  // (uint16)
    public const ushort OffInvV_SF       = 75;

    // AC power
    public const ushort OffInvW          = 76;  // (int16)
    public const ushort OffInvW_SF       = 77;

    // AC frequency
    public const ushort OffInvHz         = 78;  // (uint16)
    public const ushort OffInvHz_SF      = 79;

    // DC current / voltage / power
    public const ushort OffInvDCA        = 80;
    public const ushort OffInvDCA_SF     = 81;
    public const ushort OffInvDCV        = 82;
    public const ushort OffInvDCV_SF     = 83;
    public const ushort OffInvDCW        = 84;
    public const ushort OffInvDCW_SF     = 85;

    // Operational state (enum16) and event flags (bitfield32, 2 regs)
    public const ushort OffInvSt         = 86;  // 1=OFF, 2=SLEEPING, 3=STARTING, 4=MPPT, 5=THROTTLED, 6=SHUTTING_DOWN, 7=FAULT, 8=STANDBY
    public const ushort OffInvStVnd      = 87;  // vendor state (commanded - GridService writable)

    // End marker
    public const ushort OffEndModelId    = 88;
    public const ushort OffEndModelLen   = 89;

    public const ushort RegisterCount    = 90;

    public static ushort Addr(ushort offset) => (ushort)(BaseAddress + offset);

    // ----- Inverter operating-state values -----
    public const ushort StateOff         = 1;
    public const ushort StateSleeping    = 2;
    public const ushort StateStarting    = 3;
    public const ushort StateMppt        = 4;
    public const ushort StateThrottled   = 5;
    public const ushort StateShutting    = 6;
    public const ushort StateFault       = 7;
    public const ushort StateStandby     = 8;

    // ----- Vendor-state command values -----
    public const ushort VendorCommandNormal  = 0;
    public const ushort VendorCommandCurtail = 1;
    public const ushort VendorCommandStandby = 2;
    public const ushort VendorCommandFault   = 7;

    /// <summary>Network-critical registers - NetworkAdministrator+ only.</summary>
    public static readonly IReadOnlySet<ushort> ProtectedAddresses = new HashSet<ushort>
    {
        Addr(SunSpecCommon.OffCommonDA),    // device address
    };

    /// <summary>Commanded / autonomous-function registers - GridService+ may write.</summary>
    public static readonly IReadOnlySet<ushort> CommandedAddresses = new HashSet<ushort>
    {
        Addr(OffInvStVnd),                  // vendor-state (e.g. external curtailment command)
    };
}
