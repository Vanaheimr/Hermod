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
/// SunSpec register map for the demo Three-Phase Meter device (Model 213 subset).
/// Common Model 1 offsets are inherited from <see cref="SunSpecCommon"/>; this
/// class owns only what's specific to the meter.
///
/// Layout:
///   BaseAddress (40000)  : Common Model 1   (offsets 0..69)   - see SunSpecCommon
///   +70                  : Meter Model 213 header + fields (subset)
///   +96                  : End marker  ID = 0xFFFF, L = 0
/// </summary>
public static class SunSpecMeterMap
{
    public const ushort BaseAddress = SunSpecCommon.BaseAddress;
    public const uint   SunSpecMarker = SunSpecCommon.SunSpecMarker;
    public const ushort EndModelId  = SunSpecCommon.EndModelId;

    public const ushort CommonModelId      = SunSpecCommon.CommonModelId;
    public const ushort CommonModelLength  = SunSpecCommon.CommonModelLength;

    public const ushort OffMarker          = SunSpecCommon.OffMarker;
    public const ushort OffCommonId        = SunSpecCommon.OffCommonId;
    public const ushort OffCommonLen       = SunSpecCommon.OffCommonLen;
    public const ushort OffCommonMn        = SunSpecCommon.OffCommonMn;
    public const ushort OffCommonMd        = SunSpecCommon.OffCommonMd;
    public const ushort OffCommonOpt       = SunSpecCommon.OffCommonOpt;
    public const ushort OffCommonVr        = SunSpecCommon.OffCommonVr;
    public const ushort OffCommonSN        = SunSpecCommon.OffCommonSN;
    public const ushort OffCommonDA        = SunSpecCommon.OffCommonDA;
    public const ushort OffCommonPad       = SunSpecCommon.OffCommonPad;

    // ----- Meter Model 213 (Three-Phase Meter, int+SF) - subset -----

    public const ushort MeterModelId       = 213;
    public const ushort MeterModelLength   = 24;  // demo subset, NOT the full 124-reg spec model

    public const ushort OffMeterId         = SunSpecCommon.FirstModelOffset;          // 70
    public const ushort OffMeterLen        = SunSpecCommon.FirstModelOffset + 1;      // 71

    // Currents (int16) + scale factor
    public const ushort OffMeterA          = 72;  // Total AC Current
    public const ushort OffMeterAphA       = 73;
    public const ushort OffMeterAphB       = 74;
    public const ushort OffMeterAphC       = 75;
    public const ushort OffMeterA_SF       = 76;  // sint16 scale factor

    // Voltages (int16) + scale factor
    public const ushort OffMeterPhV        = 77;  // L-N average
    public const ushort OffMeterPhVphA     = 78;
    public const ushort OffMeterPhVphB     = 79;
    public const ushort OffMeterPhVphC     = 80;
    public const ushort OffMeterV_SF       = 81;

    // Frequency
    public const ushort OffMeterHz         = 82;
    public const ushort OffMeterHz_SF      = 83;

    // Powers (int16) + scale factor
    public const ushort OffMeterW          = 84;  // total real power
    public const ushort OffMeterWphA       = 85;
    public const ushort OffMeterWphB       = 86;
    public const ushort OffMeterWphC       = 87;
    public const ushort OffMeterW_SF       = 88;

    // Energy (uint32 across 2 regs) + scale factor
    public const ushort OffMeterTotWhExp   = 89;  // 2 regs
    public const ushort OffMeterTotWhImp   = 91;  // 2 regs
    public const ushort OffMeterWh_SF      = 93;

    // Demo "command" register (writable settings live here)
    public const ushort OffMeterMeterMode  = 94;  // (custom: 0=net, 1=import-only, 2=export-only)
    public const ushort OffMeterResetEnergy= 95;  // write 0xCAFE to clear counters

    // End marker
    public const ushort OffEndModelId      = 96;
    public const ushort OffEndModelLen     = 97;

    /// <summary>Total number of holding registers we expose (base + 98).</summary>
    public const ushort RegisterCount      = 98;

    /// <summary>Convert a 0-based offset into a Modbus address.</summary>
    public static ushort Addr(ushort offset) => (ushort)(BaseAddress + offset);

    /// <summary>
    /// Set of register addresses that are CRITICAL (network / protection) and
    /// thus require NetworkAdministrator or SuperAdministrator to write.
    /// </summary>
    public static readonly IReadOnlySet<ushort> ProtectedAddresses = new HashSet<ushort>
    {
        Addr(OffCommonDA),       // Device address: network-level
    };

    /// <summary>
    /// Writable "commanded / autonomous function" registers - GridService and up may write these.
    /// </summary>
    public static readonly IReadOnlySet<ushort> CommandedAddresses = new HashSet<ushort>
    {
        Addr(OffMeterMeterMode),
        Addr(OffMeterResetEnergy),
    };
}
