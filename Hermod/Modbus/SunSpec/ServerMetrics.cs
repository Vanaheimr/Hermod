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
/// Metric instruments for <c>ModbusTlsFrontend</c>.
///
/// The RBAC-decision counter (<see cref="Requests"/> with <c>decision</c>
/// label) is the headline audit metric for NIS2/CRA reports: a sudden rise
/// in <c>decision=deny role=ReadOnlySunSpec</c> is exactly the kind of signal
/// that should fire an alert.
///
/// Label conventions:
///   * fc        = Modbus function code, "0xNN"
///   * role      = SunSpec role from the client cert; "&lt;none&gt;" for missing-role,
///                 "&lt;unknown&gt;" for non-mandatory strings (NEVER raw cert text)
///   * decision  = {allow, deny}
///   * outcome   = handshake outcome: {ok, bad_cert, no_role, protocol_error, timeout, io}
/// </summary>
public static class ServerMetrics
{
    public static readonly UpDownCounter<long> ActiveConnections =
        Telemetry.Meter.CreateUpDownCounter<long>("mbaps.server.active_connections",
            unit: "{connection}",
            description: "Currently established TLS connections.");

    public static readonly Counter<long> Handshakes =
        Telemetry.Meter.CreateCounter<long>("mbaps.server.handshakes",
            unit: "{handshake}",
            description: "TLS handshake attempts (final outcome).");

    public static readonly Histogram<double> HandshakeDuration =
        Telemetry.Meter.CreateHistogram<double>("mbaps.server.handshake.duration",
            unit: "s",
            description: "TLS handshake duration (server side).");

    public static readonly Counter<long> Requests =
        Telemetry.Meter.CreateCounter<long>("mbaps.server.requests",
            unit: "{request}",
            description: "Modbus requests received, labelled by RBAC decision.");

    public static readonly Histogram<double> RequestDuration =
        Telemetry.Meter.CreateHistogram<double>("mbaps.server.request.duration",
            unit: "s",
            description: "Server-side Modbus transaction duration.");

    public static readonly Counter<long> AuthorizationDenials =
        Telemetry.Meter.CreateCounter<long>("mbaps.server.authz.denials",
            unit: "{denial}",
            description: "RBAC denials, broken down by role.");

    /// <summary>
    /// Normalise a role string into a low-cardinality metric label. Maps any
    /// string outside the four mandatory SunSpec roles to "&lt;unknown&gt;",
    /// and null to "&lt;none&gt;". Without this normalisation a rogue cert with
    /// a unique role string would explode metric cardinality.
    /// </summary>
    public static string RoleLabel(string? role)
    {
        if (role is null) return "<none>";
        return SunSpecRoles.IsMandatoryRole(role) ? role : "<unknown>";
    }
}
