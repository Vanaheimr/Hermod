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
/// Modbus exception codes (subset)
/// </summary>
public enum ModbusExceptionCode : Byte
{
    /// <summary>
    /// FC unsupported, or AuthZ denied (per SunSpecTCP-32 / R-66).
    /// </summary>
    IllegalFunction         = 0x01,
    IllegalDataAddress      = 0x02,
    IllegalDataValue        = 0x03,
    ServerDeviceFailure     = 0x04,
    Acknowledge             = 0x05,
    ServerDeviceBusy        = 0x06,
    MemoryParityError       = 0x08,
    GatewayPathUnavailable  = 0x0A,
    GatewayTargetFailed     = 0x0B,
}
