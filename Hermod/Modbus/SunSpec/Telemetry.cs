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

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace org.GraphDefined.Vanaheimr.Hermod.SunSpecModbusTLS.Common;

/// <summary>
/// Shared OpenTelemetry-compatible instrumentation roots for the entire stack.
///
/// All metrics are emitted on the meter named <see cref="MeterName"/>; all
/// activities (traces) on the source named <see cref="ActivitySourceName"/>.
/// Callers (CLI host) opt in by registering exporters with these names. If no
/// listener is attached, the calls are essentially free - no allocations on
/// the hot path.
///
/// Library code does NOT take a dependency on OpenTelemetry packages or on
/// Microsoft.Extensions.Logging concrete implementations. It only references
/// abstractions; the host wires up exporters.
/// </summary>
public static class Telemetry
{
    public const string MeterName          = "SunSpecModbusTLS";
    public const string ActivitySourceName = "SunSpecModbusTLS";

    public static readonly Meter           Meter          = new(MeterName, "1.0.0");
    public static readonly ActivitySource  ActivitySource = new(ActivitySourceName, "1.0.0");
}
