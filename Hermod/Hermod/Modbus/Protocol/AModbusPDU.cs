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
    /// An abstract Modbus PDU for the given function code.
    /// </summary>
    public abstract class AModbusPDU
    {

        #region Properties

        /// <summary>
        /// The Modbus device.
        /// </summary>
        public IModbusDevice    ModbusDevice    { get; }

        /// <summary>
        /// The Modbus transaction identification.
        /// </summary>
        public UInt16           TransactionId    { get; }

        /// <summary>
        /// The function code of this PDU.
        /// </summary>
        public FunctionCode     FunctionCode    { get; }

        /// <summary>
        /// A serialized representation of this PDU.
        /// </summary>
        public abstract Byte[]  Serialized      { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Creates an abstract Modbus PDU for the given function code.
        /// </summary>
        /// <param name="ModbusDevice">The Modbus device.</param>
        /// <param name="FunctionCode">The function code.</param>
        protected AModbusPDU(IModbusDevice  ModbusDevice,
                             FunctionCode   FunctionCode)
        {

            this.ModbusDevice   = ModbusDevice;
            this.FunctionCode   = FunctionCode;

            this.TransactionId  = this.ModbusDevice.NextInvocationId;

        }

        #endregion

    }

}
