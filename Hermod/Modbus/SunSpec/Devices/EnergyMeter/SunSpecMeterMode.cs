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
/// What kind of meter <see cref="SunSpecMeterDevice"/> pretends to be, held in
/// the mode register (<see cref="SunSpecMeterMap.OffMeterMeterMode"/>) so that
/// a Modbus client can read it and, with the right role, change it.
///
/// This register is not part of SunSpec Model 213; it is the simulator's own,
/// and these three values are its whole vocabulary. What they mean is where
/// the meter sits in an installation, because that - and not a flag - is what
/// decides which way energy flows through it.
/// </summary>
public enum SunSpecMeterMode : UInt16
{

    /// <summary>
    /// A meter at the grid connection point: the site draws and the site
    /// feeds back, so power is signed and both energy counters move. What a
    /// local controller steering a whole site reads.
    /// </summary>
    Net         = 0,

    /// <summary>
    /// A meter in front of a load - a charging station, say. Power is never
    /// negative and only the imported energy counter moves.
    /// </summary>
    ImportOnly  = 1,

    /// <summary>
    /// A meter in front of a generator. Power is never positive and only the
    /// exported energy counter moves. A photovoltaic meter reads zero at
    /// night, and so does this one.
    /// </summary>
    ExportOnly  = 2

}


/// <summary>
/// Extension methods for <see cref="SunSpecMeterMode"/>.
/// </summary>
public static class SunSpecMeterModeExtensions
{

    /// <summary>
    /// The mode as somebody would say it: for a command line, a JSON field
    /// and a line in a log, so that all three say the same words.
    /// </summary>
    public static String AsText(this SunSpecMeterMode Mode)

        => Mode switch {
               SunSpecMeterMode.ImportOnly  => "import",
               SunSpecMeterMode.ExportOnly  => "export",
               _                            => "net"
           };

    /// <summary>
    /// What the mode means, in a sentence: what a person is told when they
    /// have just changed it and may not have meant to.
    /// </summary>
    public static String Description(this SunSpecMeterMode Mode)

        => Mode switch {
               SunSpecMeterMode.ImportOnly  => "in front of a load: importing only",
               SunSpecMeterMode.ExportOnly  => "in front of a generator: exporting only",
               _                            => "at the grid connection point: importing and exporting"
           };

    /// <summary>
    /// Parse a mode from what somebody typed or sent: its name, a prefix of
    /// it, or the number that stands in the register.
    /// </summary>
    public static Boolean TryParse(String? Text, out SunSpecMeterMode Mode)
    {

        switch (Text?.Trim().ToLowerInvariant())
        {

            case "0":
            case "net":
            case "bidirectional":
                Mode = SunSpecMeterMode.Net;
                return true;

            case "1":
            case "import":
            case "import-only":
            case "importonly":
            case "consumption":
                Mode = SunSpecMeterMode.ImportOnly;
                return true;

            case "2":
            case "export":
            case "export-only":
            case "exportonly":
            case "generation":
                Mode = SunSpecMeterMode.ExportOnly;
                return true;

            default:
                Mode = SunSpecMeterMode.Net;
                return false;

        }

    }

}
