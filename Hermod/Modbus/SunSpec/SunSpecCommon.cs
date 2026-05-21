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
/// SunSpec common constants shared by every device, regardless of which
/// model(s) follow. Per the SunSpec specification, every SunSpec device
/// starts with the "SunS" marker and Common Model 1.
/// </summary>
public static class SunSpecCommon
{
    /// <summary>Standard SunSpec base address (one of 40000 / 50000 / 0).</summary>
    public const ushort BaseAddress = 40000;

    /// <summary>SunSpec start marker. Per spec, the value 0x53756e53 ("SunS").</summary>
    public const uint SunSpecMarker = 0x53756E53;

    /// <summary>End-of-models marker.</summary>
    public const ushort EndModelId  = 0xFFFF;

    // ----- Common Model 1 (mandatory) -----
    public const ushort CommonModelId      = 1;
    public const ushort CommonModelLength  = 66; // standard fixed length

    // Offsets relative to SunS marker (i.e. relative to BaseAddress)
    public const ushort OffMarker          = 0;   // 2 regs
    public const ushort OffCommonId        = 2;   // 1 reg
    public const ushort OffCommonLen       = 3;   // 1 reg
    public const ushort OffCommonMn        = 4;   // 16 regs (Manufacturer)
    public const ushort OffCommonMd        = 20;  // 16 regs (Model)
    public const ushort OffCommonOpt       = 36;  // 8 regs  (Options)
    public const ushort OffCommonVr        = 44;  // 8 regs  (Version)
    public const ushort OffCommonSN        = 52;  // 16 regs (Serial Number)
    public const ushort OffCommonDA        = 68;  // 1 reg   (Device Address)
    public const ushort OffCommonPad       = 69;  // 1 reg

    /// <summary>First offset AFTER Common Model 1, i.e. where the next model header starts.</summary>
    public const ushort FirstModelOffset   = 70;
}
