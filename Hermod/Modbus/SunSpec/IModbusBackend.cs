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
/// Backend executor behind the TLS frontend. The frontend has already enforced
/// transport security (mTLS) and the role-based authorization policy. The backend
/// is allowed (and expected) to execute the request.
///
/// The backend MAY still respond with a Modbus exception PDU if it cannot fulfil
/// the request (e.g. illegal data address, target device gone, busy). It MUST NOT
/// re-implement role-based authorization - that is the frontend's job and reusing
/// the policy here would silently desync the two layers.
/// </summary>
public interface IModbusBackend : IAsyncDisposable
{

    /// <summary>
    /// Process one Modbus request PDU and return the response PDU (incl. function
    /// code byte). Both arrays use the on-the-wire layout, MBAP excluded.
    /// </summary>
    /// <param name="unitId">MBAP Unit Identifier (Slave ID) from the inbound frame.</param>
    /// <param name="requestPdu">Request PDU starting with the function code.</param>
    /// <param name="ct">Cancellation token bound to the connection lifetime.</param>
    Task<byte[]> ProcessRequestAsync(byte unitId, ReadOnlyMemory<byte> requestPdu, CancellationToken ct);

}
