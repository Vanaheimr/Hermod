/*
 * Copyright (c) 2010-2023 GraphDefined GmbH
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

namespace org.GraphDefined.Vanaheimr.Hermod.Modbus
{

    /// <summary>
    /// The Modbus/TCP Read Input Registers request.
    /// 
    /// This function code is used to read from 1 to 125 contiguous input registers in a remote device.
    /// The Request PDU specifies the starting register address and the number of registers. In the
    /// PDU Registers are addressed starting at zero. Therefore input registers numbered 1-16 are
    /// addressed as 0-15.
    /// </summary>
    public class ReadInputRegistersRequest : ModbusTCPRequest
    {

        #region Properties

        /// <summary>
        /// The starting address.
        /// </summary>
        public UInt16  StartAddress         { get; }

        /// <summary>
        /// The number of input registers to read.
        /// </summary>
        public UInt16  NumberOfRegisters    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Read holding registers.
        /// </summary>
        /// <param name="ModbusClient">A Modbus/TCP client.</param>
        /// <param name="TransactionId">A transaction identifier.</param>
        /// <param name="StartAddress">The starting address.</param>
        /// <param name="NumberOfRegisters">The number of input registers to read.</param>
        /// <param name="UnitIdentifier">An optional device/unit identifier.</param>
        /// <param name="ProtocolIdentifier">An optional protocol identifier.</param>
        public ReadInputRegistersRequest(ModbusTCPClient  ModbusClient,
                                         UInt16           TransactionId,
                                         UInt16           StartAddress,
                                         UInt16           NumberOfRegisters,
                                         Byte?            UnitIdentifier       = null,
                                         UInt16?          ProtocolIdentifier   = null)

            : base(ModbusClient,
                   TransactionId,
                   FunctionCode.ReadDiscreteInputs,
                   //ModbusProtocol.CreateReadHeader(TransactionId,
                   //                                FunctionCode.ReadInputRegisters,
                                                   StartAddress,
                                                   NumberOfRegisters,
                                                   UnitIdentifier,
                                                   ProtocolIdentifier)

        {

            this.StartAddress       = StartAddress;
            this.NumberOfRegisters  = NumberOfRegisters;

        }

        #endregion

    }

}
