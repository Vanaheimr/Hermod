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
    /// Read holding registers.
    /// </summary>
    public class ReadHoldingRegistersRequest : ModbusTCPRequest
    {

        #region Properties

        /// <summary>
        /// The starting address.
        /// </summary>
        public UInt16  StartAddress         { get; }

        /// <summary>
        /// The number of holding registers to read.
        /// </summary>
        public UInt16  NumberOfRegisters    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Read holding registers.
        /// </summary>
        /// <param name="StartAddress">The starting address.</param>
        /// <param name="NumberOfRegisters">The number of holding registers to read.</param>
        public ReadHoldingRegistersRequest(ModbusTCPClient  ModbusClient,
                                           UInt16           InvocationId,
                                           UInt16           StartAddress,
                                           UInt16           NumberOfRegisters)

            : base(ModbusClient,
                   InvocationId,
                   FunctionCode.ReadDiscreteInputs,
                   ModbusProtocol.CreateReadHeader(InvocationId,
                                                   StartAddress,
                                                   NumberOfRegisters,
                                                   FunctionCode.ReadHoldingRegister).ToArray())

        {

            this.StartAddress       = StartAddress;
            this.NumberOfRegisters  = NumberOfRegisters;

        }

        #endregion

    }

}
