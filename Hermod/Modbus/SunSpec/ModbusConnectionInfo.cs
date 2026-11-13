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

using System.Security.Authentication;

namespace org.GraphDefined.Vanaheimr.Hermod.SunSpecModbusTLS.Common;

/// <summary>
/// One Modbus/TLS connection as the TLS frontend knows it once its handshake
/// held: where it comes from, who its client's certificate says it is, and
/// the SunSpec role in it.
/// </summary>
/// <remarks>
/// A request says its connection by a number and its peer by an address, and
/// says both only once there is a request: a client that connected and has
/// asked nothing yet was known to nobody but the frontend. This is the
/// connection itself, said when it begins and again when it ends, so that a
/// host can show who is connected now.
///
/// A connection whose handshake failed never becomes one of these: there is
/// no certificate to name it by, and the frontend's log has what went wrong.
/// </remarks>
/// <param name="ConnectionId">The number the frontend gave it, as every request on it says.</param>
/// <param name="Peer">Where it comes from.</param>
/// <param name="Role">The SunSpec role of the client certificate, or null when it carries none.</param>
/// <param name="ClientSubject">The subject of the client certificate.</param>
/// <param name="ClientFingerprint">The SHA-256 fingerprint of the client certificate, in upper-case hexadecimal.</param>
/// <param name="Protocol">The TLS version the handshake agreed on.</param>
/// <param name="Since">When the handshake held.</param>
public sealed record ModbusConnectionInfo(Int64           ConnectionId,
                                          String?         Peer,
                                          String?         Role,
                                          String          ClientSubject,
                                          String          ClientFingerprint,
                                          SslProtocols    Protocol,
                                          DateTimeOffset  Since);
