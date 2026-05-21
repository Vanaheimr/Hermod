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

using System.Diagnostics.Metrics;

namespace org.GraphDefined.Vanaheimr.Hermod.SunSpecModbusTLS.Common;

/// <summary>
/// Metric instruments for <c>ModbusTlsClient</c>. Centralised so the names,
/// units and label conventions are identical regardless of where they're
/// recorded from.
///
/// Label conventions:
///   * fc       = Modbus function code, formatted as "0xNN" (low cardinality)
///   * outcome  = one of {ok, modbus_exc, transient, permanent, cancelled}
///   * reason   = retry trigger: {timeout, io, framing}
///
/// We do NOT label by peer, address, register-range, role, or anything else
/// with unbounded cardinality - those go into traces / logs instead.
/// </summary>
public static class ClientMetrics
{

    public static readonly Counter<long> Requests =
        Telemetry.Meter.CreateCounter<long>("mbaps.client.requests",
            unit: "{request}",
            description: "Modbus requests sent by the client (final outcome).");

    public static readonly Histogram<double> RequestDuration =
        Telemetry.Meter.CreateHistogram<double>("mbaps.client.request.duration",
            unit: "s",
            description: "End-to-end Modbus transaction duration including any retries.");

    public static readonly Counter<long> Retries =
        Telemetry.Meter.CreateCounter<long>("mbaps.client.retries",
            unit: "{retry}",
            description: "Retry attempts triggered by transient failures.");

    public static readonly Histogram<double> HandshakeDuration =
        Telemetry.Meter.CreateHistogram<double>("mbaps.client.handshake.duration",
            unit: "s",
            description: "TLS handshake (TCP connect + TLS authenticate) duration.");

    public static readonly Counter<long> Handshakes =
        Telemetry.Meter.CreateCounter<long>("mbaps.client.handshakes",
            unit: "{handshake}",
            description: "TLS handshake attempts (final outcome).");

}
