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


public static class FunctionCodeExtensions
{

    /// <summary>
    /// True if the FC will modify state on the server (writes).
    /// </summary>
    public static Boolean IsWrite(this ModbusFunctionCodes ModbusFunctionCode)

        => ModbusFunctionCode switch {
               ModbusFunctionCodes.WriteSingleCoil          => true,
               ModbusFunctionCodes.WriteSingleRegister      => true,
               ModbusFunctionCodes.WriteMultipleCoils       => true,
               ModbusFunctionCodes.WriteMultipleRegisters   => true,
               ModbusFunctionCodes.ReadWriteMultipleRegisters => true,
               _ => false,
           };

}


/// <summary>
/// Complete list of all public MODBUS Application Protocol function codes
/// (per MODBUS Application Protocol V1.1b3)
/// </summary>
public enum ModbusFunctionCodes : Byte
{

    // Data Access - Bit Access
    ReadCoils                       = 0x01,   // Read Coils
    ReadDiscreteInputs              = 0x02,   // Read Discrete Inputs

    // Data Access - 16-bit Access
    ReadHoldingRegisters            = 0x03,   // Read Holding Registers
    ReadInputRegisters              = 0x04,   // Read Input Registers
    WriteSingleCoil                 = 0x05,   // Write Single Coil
    WriteSingleRegister             = 0x06,   // Write Single Register
    WriteMultipleCoils              = 0x0F,   // Write Multiple Coils
    WriteMultipleRegisters          = 0x10,   // Write Multiple Registers
    ReadWriteMultipleRegisters      = 0x17,   // Read/Write Multiple Registers

    // File Record Access
    ReadFileRecord                  = 0x14,   // Read File Record
    WriteFileRecord                 = 0x15,   // Write File Record

    // Other
    MaskWriteRegister               = 0x16,   // Mask Write Register
    ReadFifoQueue                   = 0x18,   // Read FIFO Queue

    // Serial line specific functions (mostly unused in Modbus TCP)
    ReadExceptionStatus             = 0x07,   // Read Exception Status  (Serial Line only)
    Diagnostics                     = 0x08,   // Diagnostics            (Serial Line only)
    GetCommEventCounter             = 0x0B,   // Get Comm Event Counter (Serial Line only)
    GetCommEventLog                 = 0x0C,   // Get Comm Event Log     (Serial Line only)
    ReportServerId                  = 0x11,   // Report Server ID       (Serial Line only) – previously "Report Slave ID"

    // Encapsulated Interface Transport (0x2B)
    EncapsulatedInterfaceTransport  = 0x2B,   // e.g. Read Device Identification (MEI 0x0E)

}
