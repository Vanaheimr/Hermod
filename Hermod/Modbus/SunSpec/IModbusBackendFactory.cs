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
/// Factory for per-connection backend instances. A connection-scoped backend
/// (e.g. the proxy) gets a fresh instance per inbound TLS connection; a singleton
/// backend (e.g. the in-memory meter) returns the same shared object.
/// </summary>
public interface IModbusBackendFactory
{

    /// <summary>
    /// Create a backend for the given inbound connection. <paramref name="role"/>
    /// is provided for diagnostics/logging only - authorization decisions are
    /// already made by the frontend.
    /// </summary>
    Task<IModbusBackend> CreateAsync(long connectionId, string? role, CancellationToken ct);

}
