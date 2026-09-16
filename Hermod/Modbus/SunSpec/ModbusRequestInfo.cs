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
/// One Modbus request as the TLS frontend saw it: who asked, what they asked
/// for, whether the policy allowed it, and what went back.
/// </summary>
/// <remarks>
/// Raised for every request, refused ones included - a request that was denied
/// is the one a log is most often read for, and a log that only records what
/// was permitted answers the wrong question.
///
/// Metrics and traces already carry these numbers, but both are aggregates
/// meant for a monitoring system. This is the single request, so that a host
/// can write it down, show it, or hand it to an auditor.
/// </remarks>
/// <param name="Timestamp">When the frontend finished with it.</param>
/// <param name="ConnectionId">Which connection it arrived on.</param>
/// <param name="Peer">Where that connection comes from.</param>
/// <param name="Role">The SunSpec role of the client certificate, or null when it carries none.</param>
/// <param name="UnitId">The MBAP unit identifier.</param>
/// <param name="TransactionId">The MBAP transaction identifier, as echoed back.</param>
/// <param name="FunctionCode">What was asked.</param>
/// <param name="Address">The first register, or 0 when the PDU could not be parsed.</param>
/// <param name="Quantity">How many, or 0 when the PDU could not be parsed.</param>
/// <param name="Allowed">Whether the authorization policy let it through to the device.</param>
/// <param name="DenyReason">Why not, when it did not.</param>
/// <param name="ExceptionCode">The Modbus exception that went back, or null when the answer was an ordinary one.</param>
/// <param name="ResponseLength">How many bytes of PDU went back.</param>
/// <param name="Duration">How long the whole of it took.</param>
public sealed record ModbusRequestInfo(DateTimeOffset        Timestamp,
                                       Int64                 ConnectionId,
                                       String?               Peer,
                                       String?               Role,
                                       Byte                  UnitId,
                                       UInt16                TransactionId,
                                       ModbusFunctionCodes   FunctionCode,
                                       UInt16                Address,
                                       UInt16                Quantity,
                                       Boolean               Allowed,
                                       String?               DenyReason,
                                       ModbusExceptionCode?  ExceptionCode,
                                       Int32                 ResponseLength,
                                       TimeSpan              Duration)
{

    /// <summary>
    /// The function code as it is written down: "0x03".
    /// </summary>
    public String FunctionCodeLabel
        => $"0x{(Byte) FunctionCode:X2}";

    /// <summary>
    /// Whether the device answered with a Modbus exception, whatever the
    /// policy decided.
    /// </summary>
    public Boolean IsException
        => ExceptionCode.HasValue;

}
