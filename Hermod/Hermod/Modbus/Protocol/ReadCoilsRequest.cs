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
    /// This class is a Modbus PDU for the Read Coils function code (0x01).
    /// It is used to read from 1 to 2000 contiguous status coils (bits)
    /// in a remote device starting at the given address and for the given
    /// number of coils (bits).
    /// </summary>
    public class ReadCoilsRequest : ModbusTCPRequest
    {

        #region Properties

        /// <summary>
        /// The starting address.
        /// </summary>
        public UInt16  StartAddress     { get; }

        /// <summary>
        /// The number of coils (bits) to read.
        /// </summary>
        public UInt16  NumberOfCoils    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// This creates a new Modbus PDU for the Read Coils function code (0x01).
        /// It is used to read from 1 to 2000 contiguous status coils (bits)
        /// in a remote device starting at the given address and for the given
        /// number of coils (bits).
        /// </summary>
        /// <param name="StartAddress">The starting address.</param>
        /// <param name="NumberOfCoils">The number of coils to read.</param>
        public ReadCoilsRequest(ModbusTCPClient  ModbusClient,
                                UInt16           InvocationId,
                                UInt16           StartAddress,
                                UInt16           NumberOfCoils)

            : base(ModbusClient,
                   InvocationId,
                   FunctionCode.ReadCoils,
                   ModbusProtocol.CreateReadHeader(InvocationId,
                                                   StartAddress,
                                                   NumberOfCoils,
                                                   FunctionCode.ReadCoils).ToArray())

        {

            this.StartAddress   = StartAddress;
            this.NumberOfCoils  = NumberOfCoils;

        }

        #endregion

    }

}
