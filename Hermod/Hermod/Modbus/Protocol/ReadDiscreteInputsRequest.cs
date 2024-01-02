/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com> <achim.friedland@graphdefined.com>
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
    /// The Modbus/TCP Read Discrete Inputs request.
    /// 
    /// This function code is used to read from 1 to 2000 contiguous status of discrete inputs in a
    /// remote device.The Request PDU specifies the starting address, i.e.the address of the first
    /// input specified, and the number of inputs. In the PDU Discrete Inputs are addressed starting
    /// at zero. Therefore Discrete inputs numbered 1-16 are addressed as 0-15.
    /// </summary>
    public class ReadDiscreteInputsRequest : ModbusTCPRequest
    {

        #region Properties

        /// <summary>
        /// The starting address.
        /// </summary>
        public UInt16  StartingAddress    { get; }

        /// <summary>
        /// The number of discrete inputs to read.
        /// </summary>
        public UInt16  NumberOfInputs     { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Read discrete inputs.
        /// </summary>
        /// <param name="ModbusClient">A Modbus/TCP client.</param>
        /// <param name="TransactionId">A transaction identifier.</param>
        /// <param name="StartingAddress">The starting address.</param>
        /// <param name="NumberOfInputs">The number of discrete inputs to read.</param>
        /// <param name="UnitIdentifier">An optional device/unit identifier.</param>
        /// <param name="ProtocolIdentifier">An optional protocol identifier.</param>
        public ReadDiscreteInputsRequest(ModbusTCPClient  ModbusClient,
                                         UInt16           TransactionId,
                                         UInt16           StartingAddress,
                                         UInt16           NumberOfInputs,
                                         Byte?            UnitIdentifier       = null,
                                         UInt16?          ProtocolIdentifier   = null)

            : base(ModbusClient,
                   TransactionId,
                   FunctionCode.ReadDiscreteInputs,
                   StartingAddress,
                   NumberOfInputs,
                   UnitIdentifier,
                   ProtocolIdentifier)

        {

            this.StartingAddress  = StartingAddress;
            this.NumberOfInputs   = NumberOfInputs;

        }

        #endregion

    }

}
